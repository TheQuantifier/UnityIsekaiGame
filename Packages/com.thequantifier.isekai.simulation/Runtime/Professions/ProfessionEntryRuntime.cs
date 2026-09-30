using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Requirements;

namespace UnityIsekaiGame.Professions
{
    public sealed class ProfessionEntryRuntime
    {
        private static readonly string[] AllProjectionFields =
        {
            "request-id",
            "applicant-person-id",
            "profession-id",
            "entry-path-id",
            "specialization-id",
            "authority-id",
            "sponsor-person-id",
            "submitted-world-time",
            "state",
            "evaluation-summary",
            "relationship-id",
            "access-policy",
            "provenance"
        };

        private readonly Dictionary<string, ProfessionEntryRequestData> requestsById = new Dictionary<string, ProfessionEntryRequestData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> processedTransactions = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<ProfessionEntryHistoryHookData> historyHooks = new List<ProfessionEntryHistoryHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int Count => requestsById.Count;
        public IReadOnlyList<ProfessionEntryHistoryHookData> HistoryHooks => historyHooks.Select(hook => hook.Clone()).ToArray();
        public IReadOnlyList<ProfessionEntryRequestSnapshot> Requests => requestsById.Values
            .OrderBy(request => request.applicantPersonId, StringComparer.Ordinal)
            .ThenBy(request => request.professionId, StringComparer.Ordinal)
            .ThenBy(request => request.requestId, StringComparer.Ordinal)
            .Select(request => new ProfessionEntryRequestSnapshot(request))
            .ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, IEnumerable<string> persons = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }

        public ProfessionEligibilityResult Evaluate(ProfessionEligibilityContext context, bool perceived = false)
        {
            context ??= new ProfessionEligibilityContext(string.Empty, string.Empty, string.Empty);
            long before = revision;
            ProfessionEntryRuntimeTokenData token = CreateRuntimeToken(context);
            List<ProfessionEligibilityRequirementResult> results = new List<ProfessionEligibilityRequirementResult>();
            List<string> failures = new List<string>();
            List<string> conflicts = new List<string>();

            if (registry == null || professions == null)
            {
                return Fail(context, ProfessionEligibilityStatus.MissingRuntime, "Profession entry runtime is not configured.", token, results, failures, conflicts, before);
            }

            if (string.IsNullOrWhiteSpace(context.PersonId) || (knownPersonIds.Count > 0 && !knownPersonIds.Contains(context.PersonId)))
            {
                return Fail(context, ProfessionEligibilityStatus.MissingPerson, $"Person '{context.PersonId}' is not known.", token, results, failures, conflicts, before);
            }

            if (!registry.TryGet(context.EntryPathId, out ProfessionEntryPathDefinition path))
            {
                return Fail(context, ProfessionEligibilityStatus.MissingEntryPath, $"Profession entry path '{context.EntryPathId}' is missing.", token, results, failures, conflicts, before);
            }

            if (!registry.TryGet(path.ProfessionId, out ProfessionDefinition profession))
            {
                return Fail(context, ProfessionEligibilityStatus.MissingDefinition, $"Profession '{path.ProfessionId}' is missing.", token, results, failures, conflicts, before);
            }

            if (!string.Equals(context.ProfessionId, path.ProfessionId, StringComparison.Ordinal))
            {
                return Fail(context, ProfessionEligibilityStatus.ProfessionMismatch, $"Entry path '{path.Id}' belongs to '{path.ProfessionId}', not '{context.ProfessionId}'.", token, results, failures, conflicts, before);
            }

            if (!string.Equals(context.SpecializationId ?? string.Empty, path.SpecializationId, StringComparison.Ordinal))
            {
                return Fail(context, ProfessionEligibilityStatus.SpecializationMismatch, $"Entry path '{path.Id}' specialization mismatch.", token, results, failures, conflicts, before);
            }

            if (context.ExpectedRuntimeToken != null && !context.ExpectedRuntimeToken.SemanticallyEquals(token))
            {
                return Fail(context, ProfessionEligibilityStatus.StaleEvaluation, "Eligibility evaluation token is stale.", token, results, failures, conflicts, before);
            }

            if (path.Formality == ProfessionEntryFormality.Formal && !context.Formal)
            {
                AddFailure(results, failures, "formality", ProfessionEligibilityStatus.FormalityMismatch, "Formal entry path requires a formal request.");
            }

            if (path.Formality == ProfessionEntryFormality.Informal && context.Formal)
            {
                AddFailure(results, failures, "formality", ProfessionEligibilityStatus.FormalityMismatch, "Informal entry path cannot be used as formal recognition.");
            }

            if (path.SelfDeclarationPolicy == ProfessionSelfDeclarationPolicy.Disallowed && context.SelfDeclared)
            {
                AddFailure(results, failures, "self-declaration", ProfessionEligibilityStatus.SelfDeclarationBlocked, "Self declaration is not allowed for this entry path.");
            }

            if (path.SelfDeclarationPolicy == ProfessionSelfDeclarationPolicy.Required && !context.SelfDeclared)
            {
                AddFailure(results, failures, "self-declaration", ProfessionEligibilityStatus.SelfDeclarationBlocked, "Self declaration is required for this entry path.");
            }

            if (!profession.SelfDeclarationAllowed && context.SelfDeclared)
            {
                AddFailure(results, failures, "profession-self-declaration", ProfessionEligibilityStatus.SelfDeclarationBlocked, "Profession disallows self declaration.");
            }

            if ((path.RequiresRecognizingAuthority || context.Formal) && string.IsNullOrWhiteSpace(context.AuthorityId))
            {
                AddFailure(results, failures, "authority", ProfessionEligibilityStatus.MissingAuthority, "Formal entry requires a recognizing authority.");
            }
            else if (!string.IsNullOrWhiteSpace(context.AuthorityId)
                && (!path.AllowsAuthority(context.AuthorityId)
                    || (profession.RecognizingAuthorityIds.Count > 0 && !profession.RecognizingAuthorityIds.Contains(context.AuthorityId, StringComparer.Ordinal))))
            {
                AddFailure(results, failures, "authority", ProfessionEligibilityStatus.InvalidAuthority, $"Authority '{context.AuthorityId}' cannot recognize this entry path.");
            }

            if (path.MinimumAge > 0 && context.Age < path.MinimumAge)
            {
                AddFailure(results, failures, "age", ProfessionEligibilityStatus.AgeOrLifeStageBlocked, $"Minimum age {path.MinimumAge} is not met.");
            }

            if (path.AllowedLifeStageIds.Count > 0 && !path.AllowedLifeStageIds.Any(id => context.LifeStageIds.Contains(id, StringComparer.Ordinal)))
            {
                AddFailure(results, failures, "life-stage", ProfessionEligibilityStatus.AgeOrLifeStageBlocked, "Required life stage is not present.");
            }

            EvaluateSharedRequirements(context, path, results, failures);
            EvaluateStringRequirements(path.RequiredKnowledgeSubjectIds, context.KnowledgeSubjectIds, "knowledge", ProfessionEligibilityStatus.MissingKnowledge, results, failures);
            EvaluateStringRequirements(path.RequiredCapabilityIds, context.CapabilityIds, "capability", ProfessionEligibilityStatus.MissingCapability, results, failures);
            EvaluateStringRequirements(path.RequiredTraitIds, context.TraitIds, "trait", ProfessionEligibilityStatus.MissingTrait, results, failures);
            EvaluateStringRequirements(path.RequiredStatusIds, context.StatusIds, "status", ProfessionEligibilityStatus.MissingStatus, results, failures);
            EvaluateStringRequirements(path.RequiredOrganizationIds, context.OrganizationIds, "organization", ProfessionEligibilityStatus.MissingOrganization, results, failures);
            EvaluateStringRequirements(path.RequiredAccessKeys, context.AccessKeys, "access", ProfessionEligibilityStatus.AccessDenied, results, failures);
            EvaluateSkillRequirements(path.RequiredSkillIds, context.SkillStates, results, failures);

            IReadOnlyList<PersonProfessionSnapshot> personProfessions = professions.QueryByPerson(context.PersonId);
            bool hasActiveSameProfession = personProfessions.Any(snapshot => snapshot.Active && string.Equals(snapshot.ProfessionId, path.ProfessionId, StringComparison.Ordinal));
            bool isSpecializationPath = path.EntryType == ProfessionEntryType.Specialization || !string.IsNullOrWhiteSpace(path.SpecializationId);
            bool isReentryPath = path.EntryType == ProfessionEntryType.Reentry || path.ReentryPolicy != ProfessionReentryPolicy.NotApplicable;

            if (!isSpecializationPath && !isReentryPath && hasActiveSameProfession)
            {
                AddFailure(results, failures, "duplicate-active-profession", ProfessionEligibilityStatus.DuplicateActiveRelationship, "Person already has an active relationship for this profession.");
                conflicts.Add(path.ProfessionId);
            }

            foreach (string professionId in path.RequiredActiveProfessionIds)
            {
                if (!personProfessions.Any(snapshot => snapshot.Active && string.Equals(snapshot.ProfessionId, professionId, StringComparison.Ordinal)))
                {
                    AddFailure(results, failures, $"required-profession:{professionId}", ProfessionEligibilityStatus.Conflict, $"Required active profession '{professionId}' is missing.");
                }
            }

            foreach (string professionId in path.ProhibitedActiveProfessionIds.Concat(path.ExclusiveProfessionIds).Distinct(StringComparer.Ordinal))
            {
                if (personProfessions.Any(snapshot => snapshot.Active && string.Equals(snapshot.ProfessionId, professionId, StringComparison.Ordinal)))
                {
                    AddFailure(results, failures, $"conflicting-profession:{professionId}", ProfessionEligibilityStatus.Conflict, $"Conflicting active profession '{professionId}' is present.");
                    conflicts.Add(professionId);
                }
            }

            if (isSpecializationPath)
            {
                PersonProfessionSnapshot parent = personProfessions.FirstOrDefault(snapshot => snapshot.Active && string.Equals(snapshot.ProfessionId, path.ProfessionId, StringComparison.Ordinal));
                if (path.SpecializationRequiresParentActive && parent == null)
                {
                    AddFailure(results, failures, "specialization-parent", ProfessionEligibilityStatus.Conflict, "Specialization entry requires active parent profession practice.");
                }

                if (parent != null && parent.SpecializationIds.Contains(path.SpecializationId, StringComparer.Ordinal))
                {
                    AddFailure(results, failures, "duplicate-specialization", ProfessionEligibilityStatus.Conflict, "Specialization is already active on the parent profession.");
                }
            }

            if (isReentryPath && !ReentryAllowed(personProfessions, path, out string reentryFailure))
            {
                AddFailure(results, failures, "reentry", ProfessionEligibilityStatus.Conflict, reentryFailure);
            }

            if (failures.Count > 0)
            {
                ProfessionEligibilityStatus status = results.FirstOrDefault(result => !result.Passed)?.Status ?? ProfessionEligibilityStatus.RequirementFailed;
                return ProfessionEligibilityResult.Failure(context, status, failures[0], token, results, failures, conflicts, before, perceived);
            }

            return ProfessionEligibilityResult.Success(context, token, results, before, perceived);
        }

        public ProfessionEntryOperationResult EnterInformal(ProfessionEligibilityContext context, string transactionId, string relationshipId = "")
        {
            long before = revision;
            ProfessionEligibilityResult eligibility = Evaluate(context);
            if (!eligibility.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.EligibilityFailed, eligibility.Message, before);
            }

            if (context.Formal)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidRequest, "Informal entry cannot commit a formal entry context.", before);
            }

            registry.TryGet(context.EntryPathId, out ProfessionEntryPathDefinition path);
            string resolvedRelationshipId = string.IsNullOrWhiteSpace(relationshipId) ? RelationshipId(context.PersonId, context.ProfessionId) : relationshipId.Trim();
            AddProfessionRelationshipRequest request = new AddProfessionRelationshipRequest
            {
                relationshipId = resolvedRelationshipId,
                personId = context.PersonId,
                professionId = context.ProfessionId,
                state = path != null && path.AllowSecretEntry ? ProfessionRelationshipState.Secret : ProfessionRelationshipState.Practicing,
                formalPractice = false,
                informalPractice = true,
                selfDeclared = context.SelfDeclared,
                recognized = false,
                startWorldTime = context.WorldTime.ToString("R"),
                accessPolicyId = path == null ? string.Empty : path.DefaultAccessPolicyId,
                provenanceId = context.CorrelationId,
                tags = path != null && path.AllowSecretEntry ? new[] { "profession.secret" } : Array.Empty<string>(),
                transactionId = transactionId,
                preview = context.Preview
            };
            ProfessionOperationResult relationship = professions.AddRelationship(request);
            if (!relationship.Succeeded && !relationship.Duplicate)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.DuplicateRelationship, relationship.Message, before);
            }

            if (context.Preview)
            {
                return ProfessionEntryOperationResult.Success("Informal profession entry previewed.", before, before, relationship: relationship.Snapshot, preview: true);
            }

            revision++;
            dirty = true;
            AddHook(ProfessionEntryHistoryHookKind.InformalEntry, context, string.Empty, resolvedRelationshipId, transactionId);
            return ProfessionEntryOperationResult.Success("Informal profession entry committed.", before, revision, relationship: relationship.Snapshot, duplicate: relationship.Duplicate);
        }

        public ProfessionEntryOperationResult SubmitFormalRequest(ProfessionEligibilityContext context, string transactionId, string requestId = "")
        {
            long before = revision;
            if (processedTransactions.TryGetValue(TransactionKey(transactionId), out string existingRequestId)
                && requestsById.TryGetValue(existingRequestId, out ProfessionEntryRequestData existingProcessed))
            {
                return ProfessionEntryOperationResult.Success("Duplicate formal entry request ignored.", before, before, new ProfessionEntryRequestSnapshot(existingProcessed), duplicate: true);
            }

            ProfessionEligibilityResult eligibility = Evaluate(context);
            if (!eligibility.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.EligibilityFailed, eligibility.Message, before);
            }

            if (!context.Formal)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidRequest, "Formal entry request requires a formal context.", before);
            }

            string resolvedRequestId = string.IsNullOrWhiteSpace(requestId)
                ? $"profession-entry-request.{context.PersonId}.{context.EntryPathId}.{context.CorrelationId}".TrimEnd('.')
                : requestId.Trim();
            if (requestsById.TryGetValue(resolvedRequestId, out ProfessionEntryRequestData existing))
            {
                if (Equivalent(existing, context))
                {
                    return ProfessionEntryOperationResult.Success("Formal entry request already exists.", before, before, new ProfessionEntryRequestSnapshot(existing), duplicate: true);
                }

                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidRequest, $"Formal entry request '{resolvedRequestId}' already exists with different identity.", before);
            }

            ProfessionEntryRequestData request = new ProfessionEntryRequestData
            {
                requestId = resolvedRequestId,
                applicantPersonId = context.PersonId,
                professionId = context.ProfessionId,
                entryPathId = context.EntryPathId,
                specializationId = context.SpecializationId,
                authorityId = context.AuthorityId,
                sponsorPersonId = context.SponsorPersonId,
                submittedWorldTime = context.WorldTime.ToString("R"),
                state = ProfessionEntryRequestState.Submitted,
                evaluationSummary = eligibility.Message,
                evaluationToken = eligibility.RuntimeToken,
                skillStates = context.SkillStates.Select(skill => skill.Clone()).ToList(),
                knowledgeSubjectIds = context.KnowledgeSubjectIds.ToArray(),
                capabilityIds = context.CapabilityIds.ToArray(),
                traitIds = context.TraitIds.ToArray(),
                statusIds = context.StatusIds.ToArray(),
                organizationIds = context.OrganizationIds.ToArray(),
                accessKeys = context.AccessKeys.ToArray(),
                lifeStageIds = context.LifeStageIds.ToArray(),
                activeActivityIds = context.ActiveActivityIds.ToArray(),
                age = context.Age,
                accessPolicyId = ResolvePath(context.EntryPathId)?.DefaultAccessPolicyId ?? string.Empty,
                provenanceId = context.CorrelationId,
                revision = 1L
            };

            if (context.Preview)
            {
                return ProfessionEntryOperationResult.Success("Formal entry request previewed.", before, before, new ProfessionEntryRequestSnapshot(request), preview: true);
            }

            requestsById.Add(request.requestId, request);
            processedTransactions[TransactionKey(transactionId)] = request.requestId;
            revision++;
            dirty = true;
            AddHook(ProfessionEntryHistoryHookKind.RequestSubmitted, context, request.requestId, string.Empty, transactionId);
            return ProfessionEntryOperationResult.Success("Formal entry request submitted.", before, revision, new ProfessionEntryRequestSnapshot(request));
        }

        public ProfessionEntryOperationResult ApproveFormalRequest(string requestId, string authorityId, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!requestsById.TryGetValue(requestId ?? string.Empty, out ProfessionEntryRequestData request))
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.MissingRequest, $"Formal entry request '{requestId}' is missing.", before);
            }

            if (request.state != ProfessionEntryRequestState.Submitted && request.state != ProfessionEntryRequestState.UnderReview)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidState, $"Formal entry request '{requestId}' is {request.state}.", before);
            }

            if (!string.Equals(request.authorityId, authorityId ?? string.Empty, StringComparison.Ordinal))
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidAuthority, $"Authority '{authorityId}' does not match request authority '{request.authorityId}'.", before);
            }

            ProfessionEligibilityContext context = ContextFromRequest(request, preview);
            ProfessionEligibilityResult eligibility = Evaluate(context);
            if (!eligibility.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(eligibility.Status == ProfessionEligibilityStatus.StaleEvaluation ? ProfessionEntryOperationStatus.StaleEvaluation : ProfessionEntryOperationStatus.EligibilityFailed, eligibility.Message, before);
            }

            string relationshipId = RelationshipId(request.applicantPersonId, request.professionId);
            AddProfessionRelationshipRequest relationshipRequest = new AddProfessionRelationshipRequest
            {
                relationshipId = relationshipId,
                personId = request.applicantPersonId,
                professionId = request.professionId,
                formalPractice = true,
                informalPractice = false,
                selfDeclared = false,
                recognized = true,
                startWorldTime = request.submittedWorldTime,
                specializationIds = string.IsNullOrWhiteSpace(request.specializationId) ? Array.Empty<string>() : new[] { request.specializationId },
                recognizingAuthorityId = request.authorityId,
                recognitionReferenceId = request.requestId,
                accessPolicyId = request.accessPolicyId,
                provenanceId = request.provenanceId,
                transactionId = transactionId,
                preview = preview
            };

            ProfessionOperationResult relationship = professions.AddRelationship(relationshipRequest);
            if (!relationship.Succeeded && !relationship.Duplicate)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.DuplicateRelationship, relationship.Message, before);
            }

            ProfessionEntryRequestData working = request.Clone();
            working.state = ProfessionEntryRequestState.Approved;
            working.relationshipId = relationshipId;
            working.revision++;
            if (preview)
            {
                return ProfessionEntryOperationResult.Success("Formal entry approval previewed.", before, before, new ProfessionEntryRequestSnapshot(working), relationship.Snapshot, preview: true);
            }

            requestsById[request.requestId] = working;
            revision++;
            dirty = true;
            AddHook(ProfessionEntryHistoryHookKind.RequestApproved, context, request.requestId, relationshipId, transactionId);
            return ProfessionEntryOperationResult.Success("Formal entry request approved.", before, revision, new ProfessionEntryRequestSnapshot(working), relationship.Snapshot, duplicate: relationship.Duplicate);
        }

        public ProfessionEntryOperationResult RejectFormalRequest(string requestId, string authorityId, string transactionId, bool preview = false)
        {
            return MutateRequest(requestId, transactionId, preview, request =>
            {
                if (!string.Equals(request.authorityId, authorityId ?? string.Empty, StringComparison.Ordinal))
                {
                    return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidAuthority, $"Authority '{authorityId}' does not match request authority '{request.authorityId}'.", revision);
                }

                if (request.state != ProfessionEntryRequestState.Submitted && request.state != ProfessionEntryRequestState.UnderReview)
                {
                    return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidState, $"Formal entry request '{requestId}' is {request.state}.", revision);
                }

                request.state = ProfessionEntryRequestState.Rejected;
                return null;
            }, ProfessionEntryHistoryHookKind.RequestRejected, "Formal entry request rejected.");
        }

        public ProfessionEntryOperationResult WithdrawFormalRequest(string requestId, string applicantPersonId, string transactionId, bool preview = false)
        {
            return MutateRequest(requestId, transactionId, preview, request =>
            {
                if (!string.Equals(request.applicantPersonId, applicantPersonId ?? string.Empty, StringComparison.Ordinal))
                {
                    return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidRequest, $"Applicant '{applicantPersonId}' does not own request '{requestId}'.", revision);
                }

                if (request.state == ProfessionEntryRequestState.Approved)
                {
                    return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.InvalidState, "Approved formal entry requests cannot be withdrawn.", revision);
                }

                request.state = ProfessionEntryRequestState.Withdrawn;
                return null;
            }, ProfessionEntryHistoryHookKind.RequestWithdrawn, "Formal entry request withdrawn.");
        }

        public ProfessionEntryOperationResult EnterSpecialization(ProfessionEligibilityContext context, string relationshipId, string transactionId)
        {
            long before = revision;
            ProfessionEligibilityResult eligibility = Evaluate(context);
            if (!eligibility.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.EligibilityFailed, eligibility.Message, before);
            }

            ProfessionOperationResult specialization = professions.AddSpecialization(relationshipId, context.SpecializationId, transactionId, context.Preview);
            if (!specialization.Succeeded && !specialization.Duplicate)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.ValidationFailed, specialization.Message, before);
            }

            if (context.Preview)
            {
                return ProfessionEntryOperationResult.Success("Profession specialization previewed.", before, before, relationship: specialization.Snapshot, preview: true);
            }

            revision++;
            dirty = true;
            AddHook(ProfessionEntryHistoryHookKind.SpecializationEntered, context, string.Empty, relationshipId, transactionId);
            return ProfessionEntryOperationResult.Success("Profession specialization entered.", before, revision, relationship: specialization.Snapshot, duplicate: specialization.Duplicate);
        }

        public ProfessionEntryOperationResult ResumeInactive(ProfessionEligibilityContext context, string relationshipId, string transactionId)
        {
            long before = revision;
            ProfessionEligibilityResult eligibility = Evaluate(context);
            if (!eligibility.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.EligibilityFailed, eligibility.Message, before);
            }

            ProfessionOperationResult resumed = professions.Activate(relationshipId, true, transactionId, context.Preview);
            if (!resumed.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.ValidationFailed, resumed.Message, before);
            }

            if (context.Preview)
            {
                return ProfessionEntryOperationResult.Success("Profession reentry previewed.", before, before, relationship: resumed.Snapshot, preview: true);
            }

            revision++;
            dirty = true;
            AddHook(ProfessionEntryHistoryHookKind.ProfessionResumed, context, string.Empty, relationshipId, transactionId);
            return ProfessionEntryOperationResult.Success("Inactive profession relationship resumed.", before, revision, relationship: resumed.Snapshot);
        }

        public ProfessionEntryOperationResult ReinstateRecognition(ProfessionEligibilityContext context, string relationshipId, string authorityId, string transactionId)
        {
            long before = revision;
            ProfessionEligibilityResult eligibility = Evaluate(context);
            if (!eligibility.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.EligibilityFailed, eligibility.Message, before);
            }

            ProfessionOperationResult recognized = professions.Recognize(relationshipId, authorityId, $"reinstatement.{transactionId}", transactionId, context.Preview);
            if (!recognized.Succeeded)
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.ValidationFailed, recognized.Message, before);
            }

            if (context.Preview)
            {
                return ProfessionEntryOperationResult.Success("Recognition reinstatement previewed.", before, before, relationship: recognized.Snapshot, preview: true);
            }

            revision++;
            dirty = true;
            AddHook(ProfessionEntryHistoryHookKind.RecognitionReinstated, context, string.Empty, relationshipId, transactionId);
            return ProfessionEntryOperationResult.Success("Profession recognition reinstated.", before, revision, relationship: recognized.Snapshot);
        }

        public bool TryGetRequest(string requestId, out ProfessionEntryRequestSnapshot snapshot)
        {
            if (requestsById.TryGetValue(requestId ?? string.Empty, out ProfessionEntryRequestData request))
            {
                snapshot = new ProfessionEntryRequestSnapshot(request);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<ProfessionEntryRequestSnapshot> QueryRequestsByApplicant(string personId)
        {
            return requestsById.Values
                .Where(request => string.Equals(request.applicantPersonId, personId, StringComparison.Ordinal))
                .OrderBy(request => request.requestId, StringComparer.Ordinal)
                .Select(request => new ProfessionEntryRequestSnapshot(request))
                .ToArray();
        }

        public ProfessionEntryProjection<ProfessionEntryRequestSnapshot> ProjectRequest(string requestId, ProfessionEntryProjectionAudience audience, InformationAccessDecision decision = null)
        {
            if (!TryGetRequest(requestId, out ProfessionEntryRequestSnapshot snapshot))
            {
                return new ProfessionEntryProjection<ProfessionEntryRequestSnapshot>(null, audience, decision, redacted: false, denied: true, Array.Empty<string>(), Array.Empty<string>());
            }

            if (audience == ProfessionEntryProjectionAudience.AuthoritativeInternal || audience == ProfessionEntryProjectionAudience.PrivilegedDebug || decision == null)
            {
                return new ProfessionEntryProjection<ProfessionEntryRequestSnapshot>(snapshot, audience, decision, redacted: false, denied: false, AllProjectionFields, Array.Empty<string>());
            }

            if (decision.Denied)
            {
                return new ProfessionEntryProjection<ProfessionEntryRequestSnapshot>(null, audience, decision, redacted: false, denied: true, Array.Empty<string>(), AllProjectionFields);
            }

            bool redacted = !decision.FullAccess;
            ProfessionEntryRequestSnapshot projected = redacted ? new ProfessionEntryRequestSnapshot(Redacted(snapshot.Data)) : snapshot;
            return new ProfessionEntryProjection<ProfessionEntryRequestSnapshot>(projected, audience, decision, redacted, denied: false, decision.AllowedDetails, decision.RedactedDetails.Concat(decision.HiddenDetails).ToArray());
        }

        public ProfessionEntryRuntimeSaveData CreateSaveData()
        {
            return new ProfessionEntryRuntimeSaveData
            {
                schemaVersion = ProfessionEntryRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                requests = requestsById.Values
                    .OrderBy(request => request.applicantPersonId, StringComparer.Ordinal)
                    .ThenBy(request => request.professionId, StringComparer.Ordinal)
                    .ThenBy(request => request.requestId, StringComparer.Ordinal)
                    .Select(request => request.Clone())
                    .ToList()
            };
        }

        public ProfessionEntryOperationResult RestoreFromSaveData(ProfessionEntryRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, IEnumerable<string> persons, bool restoring = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, professionRuntime, persons, out string failure))
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.RestoreFailed, failure, before);
            }

            Configure(definitionRegistry, professionRuntime, persons);
            RestoreInternal(saveData, clearTransient: true);
            dirty = !restoring;
            return ProfessionEntryOperationResult.Success("Profession entry requests restored.", before, revision);
        }

        public static bool ValidateSaveData(ProfessionEntryRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Profession entry save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > ProfessionEntryRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported profession entry schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> knownPersons = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfessionEntryRequestData request in saveData.requests ?? new List<ProfessionEntryRequestData>())
            {
                if (request == null)
                {
                    failure = "Profession entry save contains a null request.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(request.requestId) || !ids.Add(request.requestId))
                {
                    failure = $"Profession entry save has duplicate or blank request ID '{request.requestId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(request.applicantPersonId) || (knownPersons.Count > 0 && !knownPersons.Contains(request.applicantPersonId)))
                {
                    failure = $"Profession entry request '{request.requestId}' references unknown applicant '{request.applicantPersonId}'.";
                    return false;
                }

                if (definitionRegistry == null || !definitionRegistry.TryGet(request.entryPathId, out ProfessionEntryPathDefinition path))
                {
                    failure = $"Profession entry request '{request.requestId}' references missing entry path '{request.entryPathId}'.";
                    return false;
                }

                if (!string.Equals(request.professionId, path.ProfessionId, StringComparison.Ordinal)
                    || !string.Equals(request.specializationId ?? string.Empty, path.SpecializationId, StringComparison.Ordinal))
                {
                    failure = $"Profession entry request '{request.requestId}' no longer matches entry path '{request.entryPathId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(ProfessionEntryRequestState), request.state))
                {
                    failure = $"Profession entry request '{request.requestId}' has invalid state '{request.state}'.";
                    return false;
                }

                if ((path.RequiresRecognizingAuthority || path.Formality == ProfessionEntryFormality.Formal)
                    && (string.IsNullOrWhiteSpace(request.authorityId) || !path.AllowsAuthority(request.authorityId)))
                {
                    failure = $"Profession entry request '{request.requestId}' has invalid authority '{request.authorityId}'.";
                    return false;
                }

                if (request.state == ProfessionEntryRequestState.Approved
                    && !string.IsNullOrWhiteSpace(request.relationshipId)
                    && professionRuntime != null
                    && !professionRuntime.TryGetSnapshot(request.relationshipId, out _))
                {
                    failure = $"Approved profession entry request '{request.requestId}' references missing relationship '{request.relationshipId}'.";
                    return false;
                }
            }

            return true;
        }

        private ProfessionEntryOperationResult MutateRequest(string requestId, string transactionId, bool preview, Func<ProfessionEntryRequestData, ProfessionEntryOperationResult> mutation, ProfessionEntryHistoryHookKind hookKind, string message)
        {
            long before = revision;
            if (!requestsById.TryGetValue(requestId ?? string.Empty, out ProfessionEntryRequestData request))
            {
                return ProfessionEntryOperationResult.Failure(ProfessionEntryOperationStatus.MissingRequest, $"Formal entry request '{requestId}' is missing.", before);
            }

            ProfessionEntryRequestData working = request.Clone();
            ProfessionEntryOperationResult failure = mutation(working);
            if (failure != null)
            {
                return failure;
            }

            working.revision++;
            if (preview)
            {
                return ProfessionEntryOperationResult.Success($"{message} Preview only.", before, before, new ProfessionEntryRequestSnapshot(working), preview: true);
            }

            requestsById[request.requestId] = working;
            revision++;
            dirty = true;
            AddHook(hookKind, working, transactionId);
            return ProfessionEntryOperationResult.Success(message, before, revision, new ProfessionEntryRequestSnapshot(working));
        }

        private void EvaluateSharedRequirements(ProfessionEligibilityContext context, ProfessionEntryPathDefinition path, List<ProfessionEligibilityRequirementResult> results, List<string> failures)
        {
            if (path.RequirementSet == null)
            {
                return;
            }

            RequirementEvaluationResult requirement = CapabilityRequirementEvaluator.Evaluate(path.RequirementSet, context.RequirementContext);
            foreach (RequirementNodeResult node in requirement.NodeResults)
            {
                results.Add(new ProfessionEligibilityRequirementResult(
                    string.IsNullOrWhiteSpace(node.NodeId) ? path.RequirementSet.Id : node.NodeId,
                    node.Passed,
                    ProfessionEligibilityStatus.RequirementFailed,
                    node.Passed ? string.Empty : node.InternalReason,
                    node.FailureVisibility == RequirementFailureVisibility.Hidden));
            }

            if (!requirement.Passed)
            {
                failures.AddRange(requirement.TestLabFailureReasons.Count == 0 ? new[] { $"Requirement set '{path.RequirementSet.Id}' failed." } : requirement.TestLabFailureReasons);
            }
        }

        private static void EvaluateStringRequirements(IReadOnlyList<string> required, IReadOnlyList<string> actual, string label, ProfessionEligibilityStatus status, List<ProfessionEligibilityRequirementResult> results, List<string> failures)
        {
            foreach (string id in required ?? Array.Empty<string>())
            {
                bool passed = actual != null && actual.Contains(id, StringComparer.Ordinal);
                string requirementId = $"{label}:{id}";
                results.Add(new ProfessionEligibilityRequirementResult(requirementId, passed, status, passed ? string.Empty : $"Required {label} '{id}' is missing."));
                if (!passed)
                {
                    failures.Add($"Required {label} '{id}' is missing.");
                }
            }
        }

        private static void EvaluateSkillRequirements(IReadOnlyList<string> required, IReadOnlyList<ProfessionEntrySkillStateData> actual, List<ProfessionEligibilityRequirementResult> results, List<string> failures)
        {
            foreach (string id in required ?? Array.Empty<string>())
            {
                bool passed = actual != null && actual.Any(skill => string.Equals(skill.skillId, id, StringComparison.Ordinal) && skill.grade > 0);
                results.Add(new ProfessionEligibilityRequirementResult($"skill:{id}", passed, ProfessionEligibilityStatus.MissingSkill, passed ? string.Empty : $"Required skill '{id}' is missing."));
                if (!passed)
                {
                    failures.Add($"Required skill '{id}' is missing.");
                }
            }
        }

        private bool ReentryAllowed(IReadOnlyList<PersonProfessionSnapshot> relationships, ProfessionEntryPathDefinition path, out string failure)
        {
            failure = string.Empty;
            PersonProfessionSnapshot prior = relationships
                .Where(snapshot => string.Equals(snapshot.ProfessionId, path.ProfessionId, StringComparison.Ordinal))
                .OrderByDescending(snapshot => snapshot.Revision)
                .FirstOrDefault();
            if (prior == null)
            {
                failure = "Reentry requires an existing profession relationship.";
                return false;
            }

            if (prior.Active)
            {
                failure = "Reentry target profession is already active.";
                return false;
            }

            if (prior.State == ProfessionRelationshipState.Revoked)
            {
                if (path.ReentryPolicy != ProfessionReentryPolicy.AllowRevokedWithExplicitReinstatement)
                {
                    failure = "Revoked profession relationships require explicit reinstatement-compatible reentry.";
                    return false;
                }

                return true;
            }

            if (prior.State == ProfessionRelationshipState.Suspended)
            {
                if (path.ReentryPolicy == ProfessionReentryPolicy.AllowSuspendedWithAuthority || path.ReentryPolicy == ProfessionReentryPolicy.AllowRevokedWithExplicitReinstatement)
                {
                    return true;
                }

                failure = "Suspended profession relationships require authority-compatible reentry.";
                return false;
            }

            return path.ReentryPolicy != ProfessionReentryPolicy.NotApplicable;
        }

        private ProfessionEntryRuntimeTokenData CreateRuntimeToken(ProfessionEligibilityContext context)
        {
            return new ProfessionEntryRuntimeTokenData
            {
                professionRevision = professions?.Revision ?? 0L,
                knowledgeRevision = 0L,
                organizationRevision = 0L,
                accessRevision = 0L,
                statusRevision = 0L,
                bodyRevision = 0L,
                activityRevision = 0L,
                contextHash = context?.CreateContextHash() ?? string.Empty
            };
        }

        private static void AddFailure(List<ProfessionEligibilityRequirementResult> results, List<string> failures, string requirementId, ProfessionEligibilityStatus status, string message)
        {
            results.Add(new ProfessionEligibilityRequirementResult(requirementId, false, status, message));
            failures.Add(message);
        }

        private static ProfessionEligibilityResult Fail(ProfessionEligibilityContext context, ProfessionEligibilityStatus status, string message, ProfessionEntryRuntimeTokenData token, List<ProfessionEligibilityRequirementResult> results, List<string> failures, List<string> conflicts, long revision)
        {
            failures.Add(message);
            return ProfessionEligibilityResult.Failure(context, status, message, token, results, failures, conflicts, revision);
        }

        private ProfessionEntryPathDefinition ResolvePath(string entryPathId)
        {
            return registry != null && registry.TryGet(entryPathId, out ProfessionEntryPathDefinition path) ? path : null;
        }

        private ProfessionEligibilityContext ContextFromRequest(ProfessionEntryRequestData request, bool preview, ProfessionEntryRuntimeTokenData expectedToken = null)
        {
            return new ProfessionEligibilityContext(
                request.applicantPersonId,
                request.professionId,
                request.entryPathId,
                request.specializationId,
                formal: true,
                selfDeclared: false,
                authorityId: request.authorityId,
                sponsorPersonId: request.sponsorPersonId,
                worldTime: ParseDouble(request.submittedWorldTime),
                age: request.age,
                correlationId: request.provenanceId,
                preview: preview,
                skills: request.skillStates,
                knowledgeSubjects: request.knowledgeSubjectIds,
                capabilities: request.capabilityIds,
                traits: request.traitIds,
                statuses: request.statusIds,
                organizations: request.organizationIds,
                access: request.accessKeys,
                lifeStages: request.lifeStageIds,
                activeActivities: request.activeActivityIds,
                expectedRuntimeToken: expectedToken);
        }

        private static bool Equivalent(ProfessionEntryRequestData existing, ProfessionEligibilityContext context)
        {
            return existing != null
                && string.Equals(existing.applicantPersonId, context.PersonId, StringComparison.Ordinal)
                && string.Equals(existing.professionId, context.ProfessionId, StringComparison.Ordinal)
                && string.Equals(existing.entryPathId, context.EntryPathId, StringComparison.Ordinal)
                && string.Equals(existing.specializationId ?? string.Empty, context.SpecializationId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.authorityId ?? string.Empty, context.AuthorityId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.sponsorPersonId ?? string.Empty, context.SponsorPersonId ?? string.Empty, StringComparison.Ordinal);
        }

        private static string RelationshipId(string personId, string professionId)
        {
            return $"profession-relationship.{personId}.{professionId}".Replace("..", ".");
        }

        private static string TransactionKey(string transactionId)
        {
            return string.IsNullOrWhiteSpace(transactionId) ? string.Empty : transactionId.Trim();
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, out double parsed) ? parsed : 0d;
        }

        private void AddHook(ProfessionEntryHistoryHookKind kind, ProfessionEligibilityContext context, string requestId, string relationshipId, string transactionId)
        {
            historyHooks.Add(new ProfessionEntryHistoryHookData
            {
                kind = kind,
                personId = context?.PersonId ?? string.Empty,
                professionId = context?.ProfessionId ?? string.Empty,
                entryPathId = context?.EntryPathId ?? string.Empty,
                specializationId = context?.SpecializationId ?? string.Empty,
                authorityId = context?.AuthorityId ?? string.Empty,
                requestId = requestId ?? string.Empty,
                relationshipId = relationshipId ?? string.Empty,
                worldTime = context == null ? string.Empty : context.WorldTime.ToString("R"),
                transactionId = transactionId ?? string.Empty
            });
        }

        private void AddHook(ProfessionEntryHistoryHookKind kind, ProfessionEntryRequestData request, string transactionId)
        {
            historyHooks.Add(new ProfessionEntryHistoryHookData
            {
                kind = kind,
                personId = request?.applicantPersonId ?? string.Empty,
                professionId = request?.professionId ?? string.Empty,
                entryPathId = request?.entryPathId ?? string.Empty,
                specializationId = request?.specializationId ?? string.Empty,
                authorityId = request?.authorityId ?? string.Empty,
                requestId = request?.requestId ?? string.Empty,
                relationshipId = request?.relationshipId ?? string.Empty,
                worldTime = request?.submittedWorldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            });
        }

        private static ProfessionEntryRequestData Redacted(ProfessionEntryRequestData source)
        {
            ProfessionEntryRequestData data = source.Clone();
            data.requestId = string.Empty;
            data.applicantPersonId = string.Empty;
            data.authorityId = string.Empty;
            data.sponsorPersonId = string.Empty;
            data.evaluationToken = null;
            data.provenanceId = string.Empty;
            return data;
        }

        private void RestoreInternal(ProfessionEntryRuntimeSaveData saveData, bool clearTransient)
        {
            requestsById.Clear();
            foreach (ProfessionEntryRequestData request in saveData?.requests ?? new List<ProfessionEntryRequestData>())
            {
                if (request != null)
                {
                    requestsById[request.requestId] = request.Clone();
                }
            }

            revision = saveData?.revision ?? 0L;
            if (clearTransient)
            {
                processedTransactions.Clear();
                historyHooks.Clear();
            }
        }
    }
}
