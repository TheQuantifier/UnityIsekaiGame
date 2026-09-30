using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Factions
{
    public sealed class FactionRuntime : IDisposable
    {
        private readonly Dictionary<string, FactionRecordData> factionsById = new Dictionary<string, FactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionNameRecordData> namesById = new Dictionary<string, FactionNameRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionAffiliationRecordData> affiliationsById = new Dictionary<string, FactionAffiliationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionRoleAssignmentRecordData> rolesById = new Dictionary<string, FactionRoleAssignmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionPositionRecordData> positionsById = new Dictionary<string, FactionPositionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionVoteRecommendationRecordData> recommendationsById = new Dictionary<string, FactionVoteRecommendationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionDispositionRecordData> dispositionsById = new Dictionary<string, FactionDispositionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionStructuralEventRecordData> structuralEventsById = new Dictionary<string, FactionStructuralEventRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionTransactionRecordData> transactionsById = new Dictionary<string, FactionTransactionRecordData>(StringComparer.Ordinal);
        private readonly HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private OrganizationMembershipRuntime memberships;
        private OrganizationAuthorityRuntime authority;
        private OrganizationResourceRuntime resources;
        private OrganizationDecisionRuntime decisions;
        private string worldId = string.Empty;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => !disposed && registry != null && !string.IsNullOrWhiteSpace(worldId);
        public int FactionCount => factionsById.Count;
        public int AffiliationCount => affiliationsById.Count;
        public int RoleAssignmentCount => rolesById.Count;
        public int PositionCount => positionsById.Count;
        public IReadOnlyList<FactionRecordData> Factions => Ordered(factionsById.Values, item => item.foundingWorldTime, item => item.factionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionNameRecordData> Names => Ordered(namesById.Values, item => item.effectiveStartWorldTime, item => item.nameRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionAffiliationRecordData> Affiliations => Ordered(affiliationsById.Values, item => item.startWorldTime, item => item.affiliationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionRoleAssignmentRecordData> RoleAssignments => Ordered(rolesById.Values, item => item.startWorldTime, item => item.roleAssignmentId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionPositionRecordData> Positions => Ordered(positionsById.Values, item => item.startWorldTime, item => item.positionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionVoteRecommendationRecordData> VoteRecommendations => Ordered(recommendationsById.Values, item => item.issuedWorldTime, item => item.recommendationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionDispositionRecordData> Dispositions => Ordered(dispositionsById.Values, item => item.startWorldTime, item => item.dispositionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FactionStructuralEventRecordData> StructuralEvents => Ordered(structuralEventsById.Values, item => item.worldTime, item => item.structuralEventId).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationResourceRuntime resourceRuntime, OrganizationDecisionRuntime decisionRuntime, string world, IEnumerable<string> persons)
        {
            registry = definitionRegistry ?? registry;
            organizations = organizationRuntime ?? organizations;
            memberships = membershipRuntime ?? memberships;
            authority = authorityRuntime ?? authority;
            resources = resourceRuntime ?? resources;
            decisions = decisionRuntime ?? decisions;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            knownPersonIds.Clear();
            foreach (string person in FactionModelUtility.Clean(persons)) knownPersonIds.Add(person);
            disposed = false;
        }

        public bool TryGetFaction(string factionId, out FactionRecordData faction)
        {
            faction = null;
            if (!factionsById.TryGetValue(FactionModelUtility.Normalize(factionId), out FactionRecordData found)) return false;
            faction = found.Clone();
            return true;
        }

        public FactionOperationResult CreateFaction(FactionCreateRequest request)
        {
            request ??= new FactionCreateRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string factionId = FactionModelUtility.Normalize(request.factionId);
            string tx = FactionModelUtility.Normalize(request.transactionId);
            if (TryDuplicate(tx, factionId, "create-faction", before, out FactionOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(request.factionDefinitionId)) return Fail(FactionOperationCode.InvalidRequest, "Faction ID and definition ID are required.", before, request.preview);
            if (!registry.TryGet(request.factionDefinitionId, out FactionDefinition definition)) return Fail(FactionOperationCode.MissingDefinition, $"Faction definition '{request.factionDefinitionId}' is missing.", before, request.preview);
            if (factionsById.ContainsKey(factionId)) return Fail(FactionOperationCode.InvalidConflict, $"Faction '{factionId}' already exists.", before, request.preview);
            FactionHostContextData host = request.hostContext?.Clone() ?? FactionHostContextData.Independent();
            if (!ValidateHost(definition, host, out string hostFailure)) return Fail(FactionOperationCode.InvalidHost, hostFailure, before, request.preview);
            if (!string.IsNullOrWhiteSpace(request.founderPersonId) && !KnownPerson(request.founderPersonId)) return Fail(FactionOperationCode.MissingPerson, $"Founder Person '{request.founderPersonId}' is not known.", before, request.preview);
            if (!string.IsNullOrWhiteSpace(request.founderOrganizationId) && !OrganizationExists(request.founderOrganizationId)) return Fail(FactionOperationCode.MissingOrganization, $"Founder Organization '{request.founderOrganizationId}' is not known.", before, request.preview);

            FactionRuntimeSaveData rollback = CreateSaveData();
            FactionRecordData record = new FactionRecordData
            {
                factionId = factionId,
                factionDefinitionId = request.factionDefinitionId.Trim(),
                officialName = string.IsNullOrWhiteSpace(request.officialName) ? definition.DisplayName : request.officialName.Trim(),
                publicDescription = request.publicDescription ?? string.Empty,
                foundingWorldTime = request.worldTime,
                lifecycleState = request.initialState == FactionLifecycleState.Invalid ? FactionLifecycleState.Active : request.initialState,
                hostContext = host,
                founderPersonId = request.founderPersonId ?? string.Empty,
                founderOrganizationId = request.founderOrganizationId ?? string.Empty,
                parentFactionId = request.parentFactionId ?? string.Empty,
                visibility = request.visibility,
                tags = FactionModelUtility.Clean(request.tags),
                revision = 1L
            };
            factionsById.Add(record.factionId, record);
            AddNameInternal(record.factionId, $"{record.factionId}.name.official", record.officialName, FactionNameCategory.Official, request.worldTime, record.visibility);
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(FactionOperationCode.PersistenceInvalid, validationFailure, before, request.preview);
            }

            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction creation previewed.", before, before, true, record.factionId, faction: record);
            }

            CompleteTransaction(tx, "create-faction", record.factionId);
            Touch();
            return Success("Faction created.", before, Revision, false, record.factionId, faction: record);
        }

        public FactionOperationResult RenameFaction(string transactionId, string factionId, string nameRecordId, string newName, FactionNameCategory category, double worldTime, FactionVisibility visibility = FactionVisibility.Public, bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            factionId = FactionModelUtility.Normalize(factionId);
            if (TryDuplicate(transactionId, factionId, "rename-faction", before, out FactionOperationResult duplicate)) return duplicate;
            if (!factionsById.TryGetValue(factionId, out FactionRecordData record)) return Fail(FactionOperationCode.MissingFaction, $"Faction '{factionId}' is missing.", before, preview);
            if (string.IsNullOrWhiteSpace(newName)) return Fail(FactionOperationCode.InvalidRequest, "Faction name cannot be empty.", before, preview);
            FactionRuntimeSaveData rollback = CreateSaveData();
            AddNameInternal(factionId, nameRecordId, newName, category, worldTime, visibility);
            if (category == FactionNameCategory.Official || category == FactionNameCategory.Public)
            {
                record.officialName = newName.Trim();
                record.revision++;
            }
            if (preview)
            {
                RestoreInternal(rollback);
                return Success("Faction rename previewed.", before, before, true, factionId, faction: record);
            }
            CompleteTransaction(transactionId, "rename-faction", factionId);
            Touch();
            return Success("Faction renamed.", before, Revision, false, factionId, faction: record);
        }

        public FactionOperationResult TransitionFaction(FactionLifecycleRequest request)
        {
            request ??= new FactionLifecycleRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string factionId = FactionModelUtility.Normalize(request.factionId);
            if (TryDuplicate(request.transactionId, factionId, "transition-faction", before, out FactionOperationResult duplicate)) return duplicate;
            if (!factionsById.TryGetValue(factionId, out FactionRecordData record)) return Fail(FactionOperationCode.MissingFaction, $"Faction '{factionId}' is missing.", before, request.preview);
            if (!IsValidTransition(record.lifecycleState, request.targetState)) return Fail(FactionOperationCode.InvalidLifecycle, $"Cannot transition faction from {record.lifecycleState} to {request.targetState}.", before, request.preview);
            FactionRuntimeSaveData rollback = CreateSaveData();
            record.lifecycleState = request.targetState;
            if (!string.IsNullOrWhiteSpace(request.successorFactionId))
            {
                record.successorFactionIds = FactionModelUtility.Clean(record.successorFactionIds.Concat(new[] { request.successorFactionId }));
            }
            record.revision++;
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction lifecycle transition previewed.", before, before, true, factionId, faction: record);
            }
            CompleteTransaction(request.transactionId, "transition-faction", factionId);
            Touch();
            return Success("Faction lifecycle transitioned.", before, Revision, false, factionId, faction: record);
        }

        public FactionEligibilityResult EvaluateAffiliationEligibility(FactionAffiliationRequest request)
        {
            request ??= new FactionAffiliationRequest();
            if (!IsReady) return new FactionEligibilityResult(false, FactionOperationCode.MissingRuntime.ToString(), "Faction runtime is not ready.", false);
            if (!factionsById.TryGetValue(FactionModelUtility.Normalize(request.factionId), out FactionRecordData faction)) return new FactionEligibilityResult(false, FactionOperationCode.MissingFaction.ToString(), "Faction is missing.", false);
            if (!faction.IsActive) return new FactionEligibilityResult(false, FactionOperationCode.InvalidLifecycle.ToString(), "Faction is not active.", false);
            if (!registry.TryGet(request.affiliationDefinitionId, out FactionAffiliationDefinition definition)) return new FactionEligibilityResult(false, FactionOperationCode.MissingDefinition.ToString(), "Affiliation definition is missing.", false);
            string personId = FactionModelUtility.Normalize(request.personId);
            bool subjectIsOrganization = string.IsNullOrWhiteSpace(personId);
            if (!subjectIsOrganization && !KnownPerson(personId)) return new FactionEligibilityResult(false, FactionOperationCode.MissingPerson.ToString(), "Person is not known.", RequiresConsent(definition));
            if (subjectIsOrganization && !OrganizationExists(request.organizationSubjectId)) return new FactionEligibilityResult(false, FactionOperationCode.MissingOrganization.ToString(), "Organization subject is not known.", RequiresConsent(definition));
            if (RequiresHostOrganizationMembershipForAffiliation(definition, faction) && !subjectIsOrganization && !HasActiveOrganizationMembership(personId, faction, request.organizationContextId)) return new FactionEligibilityResult(false, FactionOperationCode.InvalidEligibility.ToString(), "Required host organization membership is missing.", RequiresConsent(definition));
            return new FactionEligibilityResult(true, FactionOperationCode.Success.ToString(), "Eligible.", RequiresConsent(definition));
        }

        public FactionOperationResult ApplyAffiliation(FactionAffiliationRequest request)
        {
            request ??= new FactionAffiliationRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string affiliationId = FactionModelUtility.Normalize(request.affiliationId);
            string tx = FactionModelUtility.Normalize(request.transactionId);
            if (TryDuplicate(tx, affiliationId, "apply-affiliation", before, out FactionOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(affiliationId)) return Fail(FactionOperationCode.InvalidRequest, "Affiliation ID is required.", before, request.preview);
            if (!registry.TryGet(request.affiliationDefinitionId, out FactionAffiliationDefinition definition)) return Fail(FactionOperationCode.MissingDefinition, $"Affiliation definition '{request.affiliationDefinitionId}' is missing.", before, request.preview);
            FactionEligibilityResult eligibility = EvaluateAffiliationEligibility(request);
            if (!eligibility.Eligible) return Fail(Enum.TryParse(eligibility.Code, out FactionOperationCode code) ? code : FactionOperationCode.InvalidEligibility, eligibility.Message, before, request.preview);
            if (RequiresConsent(definition) && !request.explicitConsent) return Fail(FactionOperationCode.MissingConsent, "Explicit affiliation consent is required.", before, request.preview);
            if (!definition.SimultaneousAllowed && HasActiveDuplicateAffiliation(request.factionId, request.personId, request.organizationSubjectId, definition.Category, affiliationId)) return Fail(FactionOperationCode.InvalidConflict, "An active affiliation of this category already exists for this subject.", before, request.preview);
            if (affiliationsById.ContainsKey(affiliationId)) return Fail(FactionOperationCode.InvalidConflict, $"Affiliation '{affiliationId}' already exists.", before, request.preview);

            FactionRuntimeSaveData rollback = CreateSaveData();
            FactionAffiliationRecordData record = new FactionAffiliationRecordData
            {
                affiliationId = affiliationId,
                factionId = FactionModelUtility.Normalize(request.factionId),
                subjectId = string.IsNullOrWhiteSpace(request.personId) ? FactionModelUtility.Normalize(request.organizationSubjectId) : FactionModelUtility.Normalize(request.personId),
                subjectIsOrganization = string.IsNullOrWhiteSpace(request.personId),
                affiliationDefinitionId = request.affiliationDefinitionId.Trim(),
                status = request.targetStatus == FactionAffiliationStatus.Invalid ? FactionAffiliationStatus.Active : request.targetStatus,
                publicAlignment = request.publicAlignment,
                privateAlignment = request.privateAlignment,
                publicFactionId = request.publicFactionId ?? string.Empty,
                coverFactionId = request.coverFactionId ?? string.Empty,
                consentRecorded = request.explicitConsent,
                startWorldTime = request.worldTime,
                organizationContextId = request.organizationContextId ?? string.Empty,
                visibility = request.visibility,
                sourceRecordId = request.sourceRecordId ?? string.Empty,
                revision = 1L
            };
            if (definition.Category == FactionAffiliationCategory.SecretMember && record.status == FactionAffiliationStatus.Active) record.status = FactionAffiliationStatus.SecretActive;
            affiliationsById.Add(record.affiliationId, record);
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(FactionOperationCode.PersistenceInvalid, validationFailure, before, request.preview);
            }
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction affiliation previewed.", before, before, true, record.affiliationId, affiliation: record);
            }
            CompleteTransaction(tx, "apply-affiliation", record.affiliationId);
            Touch();
            return Success("Faction affiliation applied.", before, Revision, false, record.affiliationId, affiliation: record);
        }

        public FactionOperationResult AssignRole(FactionRoleAssignmentRequest request)
        {
            request ??= new FactionRoleAssignmentRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string roleId = FactionModelUtility.Normalize(request.roleAssignmentId);
            if (TryDuplicate(request.transactionId, roleId, "assign-role", before, out FactionOperationResult duplicate)) return duplicate;
            if (!registry.TryGet(request.roleDefinitionId, out FactionRoleDefinition definition)) return Fail(FactionOperationCode.MissingDefinition, "Faction role definition is missing.", before, request.preview);
            if (!affiliationsById.TryGetValue(FactionModelUtility.Normalize(request.affiliationId), out FactionAffiliationRecordData affiliation)) return Fail(FactionOperationCode.MissingAffiliation, "Faction affiliation is missing.", before, request.preview);
            if (definition.RequiresActiveAffiliation && !affiliation.IsActive) return Fail(FactionOperationCode.InvalidEligibility, "Role assignment requires active affiliation.", before, request.preview);
            if (affiliation.subjectIsOrganization) return Fail(FactionOperationCode.InvalidEligibility, "Faction roles require a Person subject.", before, request.preview);
            if (!definition.AllowsMultipleActiveHolders && rolesById.Values.Any(item => item.factionId == affiliation.factionId && item.roleDefinitionId == request.roleDefinitionId && item.IsActive)) return Fail(FactionOperationCode.InvalidConflict, "This faction role already has an active holder.", before, request.preview);
            if (string.IsNullOrWhiteSpace(roleId)) return Fail(FactionOperationCode.InvalidRequest, "Role assignment ID is required.", before, request.preview);
            FactionRuntimeSaveData rollback = CreateSaveData();
            FactionRoleAssignmentRecordData role = new FactionRoleAssignmentRecordData
            {
                roleAssignmentId = roleId,
                affiliationId = affiliation.affiliationId,
                factionId = affiliation.factionId,
                personId = affiliation.subjectId,
                roleDefinitionId = request.roleDefinitionId.Trim(),
                state = request.acting ? FactionRoleAssignmentState.Acting : request.state,
                startWorldTime = request.worldTime,
                visibility = request.visibility,
                revision = 1L
            };
            rolesById.Add(role.roleAssignmentId, role);
            affiliation.factionRoleAssignmentIds = FactionModelUtility.Clean(affiliation.factionRoleAssignmentIds.Concat(new[] { role.roleAssignmentId }));
            affiliation.revision++;
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction role assignment previewed.", before, before, true, role.roleAssignmentId, role: role);
            }
            CompleteTransaction(request.transactionId, "assign-role", role.roleAssignmentId);
            Touch();
            return Success("Faction role assigned.", before, Revision, false, role.roleAssignmentId, role: role);
        }

        public FactionOperationResult SetPosition(FactionPositionRequest request)
        {
            request ??= new FactionPositionRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string positionId = FactionModelUtility.Normalize(request.positionId);
            if (TryDuplicate(request.transactionId, positionId, "set-position", before, out FactionOperationResult duplicate)) return duplicate;
            if (!factionsById.TryGetValue(FactionModelUtility.Normalize(request.factionId), out FactionRecordData faction) || !faction.IsActive) return Fail(FactionOperationCode.MissingFaction, "Active faction is missing.", before, request.preview);
            if (!registry.TryGet(request.positionDefinitionId, out FactionPositionDefinition definition)) return Fail(FactionOperationCode.MissingDefinition, "Faction position definition is missing.", before, request.preview);
            if (definition.TargetKind != FactionPositionTargetKind.Custom && request.targetKind != definition.TargetKind) return Fail(FactionOperationCode.InvalidRequest, "Position target kind does not match definition.", before, request.preview);
            if (request.endWorldTime >= 0d && (!definition.TemporaryAllowed || request.endWorldTime <= request.worldTime)) return Fail(FactionOperationCode.InvalidRequest, "Position end time is invalid.", before, request.preview);
            if (string.IsNullOrWhiteSpace(positionId) || string.IsNullOrWhiteSpace(request.targetId)) return Fail(FactionOperationCode.InvalidRequest, "Position ID and target ID are required.", before, request.preview);
            FactionPositionRecordData record = new FactionPositionRecordData
            {
                positionId = positionId,
                factionId = request.factionId.Trim(),
                positionDefinitionId = request.positionDefinitionId.Trim(),
                targetKind = request.targetKind,
                targetId = request.targetId.Trim(),
                stance = request.stance,
                weight = Math.Max(0, request.weight),
                axisValue = request.axisValue,
                startWorldTime = request.worldTime,
                endWorldTime = request.endWorldTime,
                internallyDisputed = request.internallyDisputed,
                visibility = request.visibility,
                sourceProposalId = request.sourceProposalId ?? string.Empty,
                sourcePolicyId = request.sourcePolicyId ?? string.Empty,
                revision = 1L
            };
            FactionRuntimeSaveData rollback = CreateSaveData();
            positionsById.Add(record.positionId, record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction position previewed.", before, before, true, record.positionId, position: record);
            }
            CompleteTransaction(request.transactionId, "set-position", record.positionId);
            Touch();
            return Success("Faction position recorded.", before, Revision, false, record.positionId, position: record);
        }

        public FactionOperationResult RecommendVote(FactionRecommendationRequest request)
        {
            request ??= new FactionRecommendationRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string recommendationId = FactionModelUtility.Normalize(request.recommendationId);
            if (TryDuplicate(request.transactionId, recommendationId, "recommend-vote", before, out FactionOperationResult duplicate)) return duplicate;
            if (!factionsById.TryGetValue(FactionModelUtility.Normalize(request.factionId), out FactionRecordData faction) || !faction.IsActive) return Fail(FactionOperationCode.MissingFaction, "Active faction is missing.", before, request.preview);
            if (decisions == null || !decisions.Proposals.Any(item => item.proposalId == request.proposalId)) return Fail(FactionOperationCode.MissingProposal, $"Proposal '{request.proposalId}' is missing.", before, request.preview);
            if (string.IsNullOrWhiteSpace(recommendationId)) return Fail(FactionOperationCode.InvalidRequest, "Recommendation ID is required.", before, request.preview);
            FactionVoteRecommendationRecordData record = new FactionVoteRecommendationRecordData
            {
                recommendationId = recommendationId,
                factionId = request.factionId.Trim(),
                proposalId = request.proposalId.Trim(),
                recommendation = request.recommendation,
                issuedByPersonId = request.issuedByPersonId ?? string.Empty,
                issuedWorldTime = request.worldTime,
                endWorldTime = request.endWorldTime,
                visibility = request.visibility,
                revision = 1L
            };
            FactionRuntimeSaveData rollback = CreateSaveData();
            recommendationsById.Add(record.recommendationId, record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction vote recommendation previewed.", before, before, true, record.recommendationId, recommendation: record);
            }
            CompleteTransaction(request.transactionId, "recommend-vote", record.recommendationId);
            Touch();
            return Success("Faction vote recommendation recorded.", before, Revision, false, record.recommendationId, recommendation: record);
        }

        public FactionOperationResult SetDisposition(FactionDispositionRequest request)
        {
            request ??= new FactionDispositionRequest();
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string dispositionId = FactionModelUtility.Normalize(request.dispositionId);
            if (TryDuplicate(request.transactionId, dispositionId, "set-disposition", before, out FactionOperationResult duplicate)) return duplicate;
            if (request.sourceFactionId == request.targetFactionId) return Fail(FactionOperationCode.InvalidRequest, "Faction disposition cannot target itself.", before, request.preview);
            if (!factionsById.ContainsKey(FactionModelUtility.Normalize(request.sourceFactionId)) || !factionsById.ContainsKey(FactionModelUtility.Normalize(request.targetFactionId))) return Fail(FactionOperationCode.MissingFaction, "Source or target faction is missing.", before, request.preview);
            FactionDispositionRecordData record = new FactionDispositionRecordData
            {
                dispositionId = dispositionId,
                sourceFactionId = request.sourceFactionId.Trim(),
                targetFactionId = request.targetFactionId.Trim(),
                disposition = request.disposition,
                intensity = Math.Max(-100, Math.Min(100, request.intensity)),
                startWorldTime = request.worldTime,
                endWorldTime = request.endWorldTime,
                visibility = request.visibility,
                revision = 1L
            };
            FactionRuntimeSaveData rollback = CreateSaveData();
            dispositionsById.Add(record.dispositionId, record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Success("Faction disposition previewed.", before, before, true, record.dispositionId, disposition: record);
            }
            CompleteTransaction(request.transactionId, "set-disposition", record.dispositionId);
            Touch();
            return Success("Directional faction disposition recorded.", before, Revision, false, record.dispositionId, disposition: record);
        }

        public FactionOperationResult SplitFaction(string transactionId, string sourceFactionId, IEnumerable<FactionCreateRequest> successorRequests, IEnumerable<string> affiliationIdsToTransfer, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            sourceFactionId = FactionModelUtility.Normalize(sourceFactionId);
            if (TryDuplicate(transactionId, sourceFactionId, "split-faction", before, out FactionOperationResult duplicate)) return duplicate;
            if (!factionsById.TryGetValue(sourceFactionId, out FactionRecordData source) || !source.IsActive) return Fail(FactionOperationCode.MissingFaction, "Source faction is missing or inactive.", before, preview);
            FactionRuntimeSaveData rollback = CreateSaveData();
            List<string> successors = new List<string>();
            foreach (FactionCreateRequest request in successorRequests ?? Array.Empty<FactionCreateRequest>())
            {
                request.worldTime = worldTime;
                request.preview = false;
                FactionOperationResult created = CreateFaction(request);
                if (!created.Succeeded || created.Faction == null)
                {
                    RestoreInternal(rollback);
                    return Fail(created.Code, $"Split failed while creating successor: {created.Message}", before, preview);
                }
                successors.Add(created.Faction.factionId);
            }
            foreach (string affiliationId in FactionModelUtility.Clean(affiliationIdsToTransfer))
            {
                if (!affiliationsById.TryGetValue(affiliationId, out FactionAffiliationRecordData affiliation)) continue;
                affiliation.status = FactionAffiliationStatus.Historical;
                affiliation.endWorldTime = worldTime;
                affiliation.revision++;
            }
            source.lifecycleState = FactionLifecycleState.Split;
            source.successorFactionIds = FactionModelUtility.Clean(source.successorFactionIds.Concat(successors));
            source.revision++;
            AddStructuralEvent(transactionId, "split", new[] { sourceFactionId }, successors, worldTime);
            if (preview)
            {
                RestoreInternal(rollback);
                return Success("Faction split previewed.", before, before, true, sourceFactionId, faction: source);
            }
            CompleteTransaction(transactionId, "split-faction", sourceFactionId);
            Touch();
            return Success("Faction split recorded.", before, Revision, false, sourceFactionId, faction: source);
        }

        public FactionOperationResult MergeFactions(string transactionId, IEnumerable<string> sourceFactionIds, FactionCreateRequest survivorRequest, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out FactionOperationResult runtimeFailure)) return runtimeFailure;
            string[] sources = FactionModelUtility.Clean(sourceFactionIds);
            if (TryDuplicate(transactionId, string.Join(",", sources), "merge-factions", before, out FactionOperationResult duplicate)) return duplicate;
            if (sources.Length < 2) return Fail(FactionOperationCode.InvalidRequest, "A faction merge requires at least two source factions.", before, preview);
            foreach (string source in sources) if (!factionsById.TryGetValue(source, out FactionRecordData record) || !record.IsActive) return Fail(FactionOperationCode.MissingFaction, $"Source faction '{source}' is missing or inactive.", before, preview);
            FactionRuntimeSaveData rollback = CreateSaveData();
            survivorRequest.worldTime = worldTime;
            survivorRequest.preview = false;
            FactionOperationResult created = CreateFaction(survivorRequest);
            if (!created.Succeeded || created.Faction == null)
            {
                RestoreInternal(rollback);
                return Fail(created.Code, $"Merge failed while creating survivor: {created.Message}", before, preview);
            }
            foreach (string source in sources)
            {
                FactionRecordData record = factionsById[source];
                record.lifecycleState = FactionLifecycleState.Merged;
                record.successorFactionIds = FactionModelUtility.Clean(record.successorFactionIds.Concat(new[] { created.Faction.factionId }));
                record.revision++;
            }
            AddStructuralEvent(transactionId, "merge", sources, new[] { created.Faction.factionId }, worldTime);
            if (preview)
            {
                RestoreInternal(rollback);
                return Success("Faction merge previewed.", before, before, true, created.Faction.factionId, faction: created.Faction);
            }
            CompleteTransaction(transactionId, "merge-factions", created.Faction.factionId);
            Touch();
            return Success("Faction merge recorded.", before, Revision, false, created.Faction.factionId, faction: created.Faction);
        }

        public FactionInfluenceReport CreateInfluenceReport(string factionId, string organizationId, double worldTime, bool includeSecret = false)
        {
            factionId = FactionModelUtility.Normalize(factionId);
            organizationId = FactionModelUtility.Normalize(organizationId);
            List<FactionInfluenceInput> inputs = new List<FactionInfluenceInput>();
            FactionAffiliationRecordData[] activeAffiliations = affiliationsById.Values.Where(item => item.factionId == factionId && item.IsActive && (includeSecret || !FactionModelUtility.IsSecret(item.visibility))).OrderBy(item => item.affiliationId, StringComparer.Ordinal).ToArray();
            int publicCount = activeAffiliations.Count(item => !FactionModelUtility.IsSecret(item.visibility));
            int secretCount = activeAffiliations.Count(item => FactionModelUtility.IsSecret(item.visibility));
            if (publicCount > 0) inputs.Add(new FactionInfluenceInput(FactionInfluenceInputKind.ActiveMembership, "affiliations.public", publicCount * 10, "Public active faction affiliations."));
            if (secretCount > 0) inputs.Add(new FactionInfluenceInput(FactionInfluenceInputKind.SecretSupport, "affiliations.secret", includeSecret ? secretCount * 6 : 0, "Secret faction affiliations are uncertainty unless authorized."));
            int offices = activeAffiliations.Count(item => !item.subjectIsOrganization && HasActiveOfficeInOrganization(item.subjectId, organizationId));
            if (offices > 0) inputs.Add(new FactionInfluenceInput(FactionInfluenceInputKind.OfficePenetration, organizationId, offices * 25, "Faction-affiliated Persons hold active offices in the organization."));
            int recommendations = recommendationsById.Values.Count(item => item.factionId == factionId && item.IsActiveAt(worldTime));
            if (recommendations > 0) inputs.Add(new FactionInfluenceInput(FactionInfluenceInputKind.ProposalActivity, "recommendations", recommendations * 5, "Active faction proposal recommendations."));
            return new FactionInfluenceReport(factionId, organizationId, inputs, includeSecret ? 0 : secretCount);
        }

        public FactionVoteCohesionReport CreateVoteCohesionReport(string factionId, string proposalId, double worldTime, bool includeSecret = false)
        {
            FactionVoteRecommendationRecordData recommendation = recommendationsById.Values.Where(item => item.factionId == factionId && item.proposalId == proposalId && item.IsActiveAt(worldTime)).OrderByDescending(item => item.issuedWorldTime).ThenBy(item => item.recommendationId, StringComparer.Ordinal).FirstOrDefault();
            string[] members = affiliationsById.Values.Where(item => item.factionId == factionId && item.IsActive && !item.subjectIsOrganization && (includeSecret || !FactionModelUtility.IsSecret(item.visibility))).Select(item => item.subjectId).Distinct(StringComparer.Ordinal).ToArray();
            int aligned = 0;
            int opposed = 0;
            int abstained = 0;
            int unknown = 0;
            foreach (string member in members)
            {
                OrganizationVoteRecordData vote = decisions?.Votes.Where(item => item.proposalId == proposalId && item.voterPersonId == member && item.lifecycleState == OrganizationVoteLifecycleState.Active).OrderByDescending(item => item.castWorldTime).ThenBy(item => item.voteId, StringComparer.Ordinal).FirstOrDefault();
                if (vote == null)
                {
                    unknown++;
                    continue;
                }
                if (vote.choice == OrganizationVoteChoice.Abstain)
                {
                    abstained++;
                    continue;
                }
                bool voteSupports = vote.choice == OrganizationVoteChoice.Approve;
                bool recommendedSupport = recommendation == null || recommendation.recommendation == FactionVoteRecommendationKind.Support || recommendation.recommendation == FactionVoteRecommendationKind.FreeVote;
                bool recommendedOppose = recommendation != null && recommendation.recommendation == FactionVoteRecommendationKind.Oppose;
                if ((recommendedSupport && voteSupports) || (recommendedOppose && !voteSupports)) aligned++;
                else opposed++;
            }
            return new FactionVoteCohesionReport(factionId, proposalId, aligned, opposed, abstained, unknown);
        }

        public FactionProjection GetFactionProjection(string factionId, FactionProjectionContext context)
        {
            context ??= new FactionProjectionContext();
            if (!factionsById.TryGetValue(FactionModelUtility.Normalize(factionId), out FactionRecordData faction)) return new FactionProjection(FactionProjectionAccess.Denied, Subject(factionId), null, Array.Empty<FactionAffiliationRecordData>(), Array.Empty<FactionPositionRecordData>(), "Faction is missing.");
            if (context.developmentView || context.privileged) return new FactionProjection(context.developmentView ? FactionProjectionAccess.Development : FactionProjectionAccess.Full, Subject(factionId), faction, affiliationsById.Values.Where(item => item.factionId == faction.factionId).ToArray(), positionsById.Values.Where(item => item.factionId == faction.factionId).ToArray(), "Full faction projection.");
            if (faction.visibility == FactionVisibility.Secret) return new FactionProjection(FactionProjectionAccess.Concealed, Subject(factionId), null, Array.Empty<FactionAffiliationRecordData>(), Array.Empty<FactionPositionRecordData>(), "Faction is concealed.");
            if (faction.visibility == FactionVisibility.Hidden) return new FactionProjection(FactionProjectionAccess.Redacted, Subject(factionId), Redact(faction), Array.Empty<FactionAffiliationRecordData>(), Array.Empty<FactionPositionRecordData>(), "Faction is redacted.");
            FactionAffiliationRecordData[] publicAffiliations = affiliationsById.Values.Where(item => item.factionId == faction.factionId && !FactionModelUtility.IsSecret(item.visibility)).ToArray();
            FactionPositionRecordData[] publicPositions = positionsById.Values.Where(item => item.factionId == faction.factionId && !FactionModelUtility.IsSecret(item.visibility)).ToArray();
            return new FactionProjection(FactionProjectionAccess.Full, Subject(factionId), faction, publicAffiliations, publicPositions, "Public faction projection.");
        }

        public FactionRuntimeSaveData CreateSaveData()
        {
            return new FactionRuntimeSaveData
            {
                schemaVersion = FactionRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                factions = Factions.Select(item => item.Clone()).ToList(),
                names = Names.Select(item => item.Clone()).ToList(),
                affiliations = Affiliations.Select(item => item.Clone()).ToList(),
                roles = RoleAssignments.Select(item => item.Clone()).ToList(),
                positions = Positions.Select(item => item.Clone()).ToList(),
                recommendations = VoteRecommendations.Select(item => item.Clone()).ToList(),
                dispositions = Dispositions.Select(item => item.Clone()).ToList(),
                structuralEvents = StructuralEvents.Select(item => item.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToList()
            };
        }

        public FactionOperationResult RestoreFromSaveData(FactionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationResourceRuntime resourceRuntime, OrganizationDecisionRuntime decisionRuntime, string expectedWorldId, IEnumerable<string> persons, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, organizationRuntime, membershipRuntime, expectedWorldId, persons, out string failure)) return Fail(FactionOperationCode.PersistenceInvalid, failure, Revision);
            Configure(definitionRegistry, organizationRuntime, membershipRuntime, authorityRuntime, resourceRuntime, decisionRuntime, expectedWorldId, persons);
            RestoreInternal(saveData ?? new FactionRuntimeSaveData());
            IsDirty = !restoring;
            return Success(restoring ? "Faction runtime restored." : "Faction runtime loaded.", Revision, Revision, false, string.Empty);
        }

        public static bool ValidateSaveData(FactionRuntimeSaveData saveData, DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationMembershipRuntime memberships, string expectedWorldId, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            saveData ??= new FactionRuntimeSaveData();
            if (saveData.schemaVersion != FactionRuntimeSaveData.CurrentSchemaVersion) return Invalid($"Unsupported faction save schema {saveData.schemaVersion}.", out failure);
            string world = FactionModelUtility.Normalize(expectedWorldId);
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.IsNullOrWhiteSpace(world) && saveData.worldId != world) return Invalid($"Faction save world '{saveData.worldId}' does not match expected world '{world}'.", out failure);
            HashSet<string> knownPersons = new HashSet<string>(FactionModelUtility.Clean(persons), StringComparer.Ordinal);
            HashSet<string> factionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FactionRecordData faction in saveData.factions ?? new List<FactionRecordData>())
            {
                if (faction == null) return Invalid("Faction save contains a null faction record.", out failure);
                if (string.IsNullOrWhiteSpace(faction.factionId) || !factionIds.Add(faction.factionId)) return Invalid($"Faction save contains missing or duplicate faction ID '{faction?.factionId}'.", out failure);
                if (registry == null || !registry.TryGet(faction.factionDefinitionId, out FactionDefinition _)) return Invalid($"Faction '{faction.factionId}' references missing definition '{faction.factionDefinitionId}'.", out failure);
                if (!ValidateHostReferences(faction.hostContext, organizations, out failure)) return false;
            }
            HashSet<string> affiliationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FactionAffiliationRecordData affiliation in saveData.affiliations ?? new List<FactionAffiliationRecordData>())
            {
                if (affiliation == null) return Invalid("Faction save contains a null affiliation record.", out failure);
                if (string.IsNullOrWhiteSpace(affiliation.affiliationId) || !affiliationIds.Add(affiliation.affiliationId)) return Invalid($"Faction save contains missing or duplicate affiliation ID '{affiliation?.affiliationId}'.", out failure);
                if (!factionIds.Contains(affiliation.factionId)) return Invalid($"Affiliation '{affiliation.affiliationId}' references missing faction '{affiliation.factionId}'.", out failure);
                if (registry == null || !registry.TryGet(affiliation.affiliationDefinitionId, out FactionAffiliationDefinition _)) return Invalid($"Affiliation '{affiliation.affiliationId}' references missing definition '{affiliation.affiliationDefinitionId}'.", out failure);
                if (!affiliation.subjectIsOrganization && knownPersons.Count > 0 && !knownPersons.Contains(affiliation.subjectId)) return Invalid($"Affiliation '{affiliation.affiliationId}' references unknown person '{affiliation.subjectId}'.", out failure);
            }
            foreach (FactionRoleAssignmentRecordData role in saveData.roles ?? new List<FactionRoleAssignmentRecordData>())
            {
                if (role == null) return Invalid("Faction save contains a null role assignment.", out failure);
                if (!factionIds.Contains(role.factionId) || !affiliationIds.Contains(role.affiliationId)) return Invalid($"Role assignment '{role.roleAssignmentId}' references missing faction or affiliation.", out failure);
                if (registry == null || !registry.TryGet(role.roleDefinitionId, out FactionRoleDefinition _)) return Invalid($"Role assignment '{role.roleAssignmentId}' references missing role definition '{role.roleDefinitionId}'.", out failure);
            }
            foreach (FactionPositionRecordData position in saveData.positions ?? new List<FactionPositionRecordData>())
            {
                if (position == null) return Invalid("Faction save contains a null position.", out failure);
                if (!factionIds.Contains(position.factionId)) return Invalid($"Position '{position.positionId}' references missing faction '{position.factionId}'.", out failure);
                if (registry == null || !registry.TryGet(position.positionDefinitionId, out FactionPositionDefinition _)) return Invalid($"Position '{position.positionId}' references missing position definition '{position.positionDefinitionId}'.", out failure);
            }
            foreach (FactionDispositionRecordData disposition in saveData.dispositions ?? new List<FactionDispositionRecordData>())
            {
                if (disposition == null) return Invalid("Faction save contains a null disposition.", out failure);
                if (disposition.sourceFactionId == disposition.targetFactionId || !factionIds.Contains(disposition.sourceFactionId) || !factionIds.Contains(disposition.targetFactionId)) return Invalid($"Disposition '{disposition.dispositionId}' has invalid source or target faction.", out failure);
            }
            return true;
        }

        public void Reset()
        {
            factionsById.Clear();
            namesById.Clear();
            affiliationsById.Clear();
            rolesById.Clear();
            positionsById.Clear();
            recommendationsById.Clear();
            dispositionsById.Clear();
            structuralEventsById.Clear();
            transactionsById.Clear();
            Revision = 0L;
            IsDirty = false;
        }

        public void Dispose()
        {
            disposed = true;
            Reset();
        }

        private void AddNameInternal(string factionId, string nameRecordId, string value, FactionNameCategory category, double worldTime, FactionVisibility visibility)
        {
            if (string.IsNullOrWhiteSpace(nameRecordId)) nameRecordId = $"{factionId}.name.{category}.{worldTime:0.###}";
            namesById[nameRecordId] = new FactionNameRecordData
            {
                nameRecordId = nameRecordId,
                factionId = factionId,
                value = value ?? string.Empty,
                category = category,
                effectiveStartWorldTime = worldTime,
                visibility = visibility,
                revision = 1L
            };
        }

        private void AddStructuralEvent(string eventId, string operation, IEnumerable<string> sources, IEnumerable<string> successors, double worldTime)
        {
            string id = string.IsNullOrWhiteSpace(eventId) ? $"faction-structure.{operation}.{worldTime:0.###}" : eventId.Trim();
            structuralEventsById[id] = new FactionStructuralEventRecordData
            {
                structuralEventId = id,
                operation = operation ?? string.Empty,
                sourceFactionIds = FactionModelUtility.Clean(sources),
                successorFactionIds = FactionModelUtility.Clean(successors),
                worldTime = worldTime
            };
        }

        private bool ValidateHost(FactionDefinition definition, FactionHostContextData host, out string failure)
        {
            failure = string.Empty;
            if (host == null) return Invalid("Faction host context is missing.", out failure);
            if (!Enum.IsDefined(typeof(FactionHostContextKind), host.contextKind) || host.contextKind == FactionHostContextKind.Unknown) return Invalid("Faction host context kind is invalid.", out failure);
            if (definition.SupportedHostContext != FactionHostContextKind.Global && definition.SupportedHostContext != FactionHostContextKind.Independent && host.contextKind != definition.SupportedHostContext && !(definition.MaySpanOrganizations && host.contextKind == FactionHostContextKind.MultipleOrganizations)) return Invalid("Faction host context does not match definition policy.", out failure);
            return ValidateHostReferences(host, organizations, out failure);
        }

        private static bool ValidateHostReferences(FactionHostContextData host, OrganizationRuntime organizations, out string failure)
        {
            failure = string.Empty;
            if (host == null) return Invalid("Host context is missing.", out failure);
            string[] organizationIds = FactionModelUtility.Clean(host.organizationIds.Concat(new[] { host.primaryOrganizationId, host.branchOrganizationId }));
            foreach (string organizationId in organizationIds)
            {
                if (!string.IsNullOrWhiteSpace(organizationId) && organizations != null && !organizations.Snapshots.Any(item => item.OrganizationId == organizationId)) return Invalid($"Host context references missing Organization '{organizationId}'.", out failure);
            }
            return true;
        }

        private bool RequiresOrganizationMembership(FactionRecordData faction)
        {
            return registry != null && registry.TryGet(faction.factionDefinitionId, out FactionDefinition definition) && definition.OrganizationMembershipRequired;
        }

        private bool RequiresHostOrganizationMembershipForAffiliation(FactionAffiliationDefinition affiliationDefinition, FactionRecordData faction)
        {
            if (affiliationDefinition == null) return false;
            if (affiliationDefinition.SupportWithoutMembership) return false;
            return affiliationDefinition.OrganizationMembershipRequired || RequiresOrganizationMembership(faction);
        }

        private bool HasActiveOrganizationMembership(string personId, FactionRecordData faction, string explicitOrganizationId)
        {
            string organizationId = FactionModelUtility.Normalize(explicitOrganizationId);
            if (string.IsNullOrWhiteSpace(organizationId)) organizationId = faction.hostContext?.primaryOrganizationId ?? string.Empty;
            if (memberships == null || string.IsNullOrWhiteSpace(organizationId)) return false;
            return memberships.Memberships.Any(item => item.PersonId == personId && item.OrganizationId == organizationId && item.IsActive);
        }

        private bool HasActiveOfficeInOrganization(string personId, string organizationId)
        {
            if (memberships == null || string.IsNullOrWhiteSpace(organizationId)) return false;
            return memberships.Offices.Any(office => office.Data.organizationId == organizationId && office.Assignments.Any(assignment => assignment.personId == personId && assignment.IsActive));
        }

        private bool HasActiveDuplicateAffiliation(string factionId, string personId, string organizationSubjectId, FactionAffiliationCategory category, string exceptId)
        {
            string subjectId = string.IsNullOrWhiteSpace(personId) ? FactionModelUtility.Normalize(organizationSubjectId) : FactionModelUtility.Normalize(personId);
            return affiliationsById.Values.Any(item => item.affiliationId != exceptId && item.factionId == factionId && item.subjectId == subjectId && item.IsActive && registry.TryGet(item.affiliationDefinitionId, out FactionAffiliationDefinition definition) && definition.Category == category);
        }

        private bool KnownPerson(string personId) => knownPersonIds.Count == 0 || knownPersonIds.Contains(FactionModelUtility.Normalize(personId));
        private bool OrganizationExists(string organizationId) => organizations == null || organizations.Snapshots.Any(item => item.OrganizationId == FactionModelUtility.Normalize(organizationId));
        private static bool RequiresConsent(FactionAffiliationDefinition definition) => definition != null && definition.ConsentPolicy != FactionAffiliationConsentPolicy.NoConsentRequired;
        private static bool IsValidTransition(FactionLifecycleState current, FactionLifecycleState target) => target != FactionLifecycleState.Invalid && current != FactionLifecycleState.Dissolved && current != FactionLifecycleState.Archived;

        private bool CanMutate(out FactionOperationResult failure)
        {
            failure = null;
            if (disposed)
            {
                failure = Fail(FactionOperationCode.Disposed, "Faction runtime is disposed.", Revision);
                return false;
            }
            if (!IsReady)
            {
                failure = Fail(FactionOperationCode.MissingRuntime, "Faction runtime is not ready.", Revision);
                return false;
            }
            return true;
        }

        private bool TryDuplicate(string transactionId, string subjectId, string operation, long before, out FactionOperationResult result)
        {
            result = null;
            transactionId = FactionModelUtility.Normalize(transactionId);
            if (string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!transactionsById.TryGetValue(transactionId, out FactionTransactionRecordData existing)) return false;
            bool same = existing.operation == operation && existing.subjectId == (subjectId ?? string.Empty);
            result = same ? new FactionOperationResult(FactionOperationCode.Duplicate, "Duplicate faction transaction ignored.", before, before, subjectId: subjectId) : Fail(FactionOperationCode.InvalidConflict, "Transaction ID was already used for a different faction mutation.", before);
            return true;
        }

        private void CompleteTransaction(string transactionId, string operation, string subjectId)
        {
            transactionId = FactionModelUtility.Normalize(transactionId);
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                transactionsById[transactionId] = new FactionTransactionRecordData { transactionId = transactionId, operation = operation ?? string.Empty, subjectId = subjectId ?? string.Empty, revision = Revision + 1L };
            }
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private bool ValidateCurrent(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), registry, organizations, memberships, worldId, knownPersonIds, out failure);
        }

        private void RestoreInternal(FactionRuntimeSaveData saveData)
        {
            saveData ??= new FactionRuntimeSaveData();
            factionsById.Clear();
            namesById.Clear();
            affiliationsById.Clear();
            rolesById.Clear();
            positionsById.Clear();
            recommendationsById.Clear();
            dispositionsById.Clear();
            structuralEventsById.Clear();
            transactionsById.Clear();
            foreach (FactionRecordData item in saveData.factions ?? new List<FactionRecordData>()) factionsById[item.factionId] = item.Clone();
            foreach (FactionNameRecordData item in saveData.names ?? new List<FactionNameRecordData>()) namesById[item.nameRecordId] = item.Clone();
            foreach (FactionAffiliationRecordData item in saveData.affiliations ?? new List<FactionAffiliationRecordData>()) affiliationsById[item.affiliationId] = item.Clone();
            foreach (FactionRoleAssignmentRecordData item in saveData.roles ?? new List<FactionRoleAssignmentRecordData>()) rolesById[item.roleAssignmentId] = item.Clone();
            foreach (FactionPositionRecordData item in saveData.positions ?? new List<FactionPositionRecordData>()) positionsById[item.positionId] = item.Clone();
            foreach (FactionVoteRecommendationRecordData item in saveData.recommendations ?? new List<FactionVoteRecommendationRecordData>()) recommendationsById[item.recommendationId] = item.Clone();
            foreach (FactionDispositionRecordData item in saveData.dispositions ?? new List<FactionDispositionRecordData>()) dispositionsById[item.dispositionId] = item.Clone();
            foreach (FactionStructuralEventRecordData item in saveData.structuralEvents ?? new List<FactionStructuralEventRecordData>()) structuralEventsById[item.structuralEventId] = item.Clone();
            foreach (FactionTransactionRecordData item in saveData.transactions ?? new List<FactionTransactionRecordData>()) transactionsById[item.transactionId] = item.Clone();
            worldId = saveData.worldId ?? worldId;
            Revision = Math.Max(0L, saveData.revision);
            IsDirty = false;
        }

        private static FactionRecordData Redact(FactionRecordData faction)
        {
            FactionRecordData redacted = faction.Clone();
            redacted.officialName = string.IsNullOrWhiteSpace(redacted.officialName) ? "Restricted faction" : redacted.officialName;
            redacted.publicDescription = string.Empty;
            redacted.founderPersonId = string.Empty;
            redacted.founderOrganizationId = string.Empty;
            redacted.tags = Array.Empty<string>();
            return redacted;
        }

        private static InformationSubjectReferenceData Subject(string factionId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Affiliation,
                subjectId = factionId ?? string.Empty,
                tags = new[] { "faction", "politics" }
            };
        }

        private static FactionOperationResult Success(string message, long before, long after, bool preview = false, string subjectId = "", FactionRecordData faction = null, FactionAffiliationRecordData affiliation = null, FactionRoleAssignmentRecordData role = null, FactionPositionRecordData position = null, FactionVoteRecommendationRecordData recommendation = null, FactionDispositionRecordData disposition = null)
        {
            return new FactionOperationResult(preview ? FactionOperationCode.Preview : FactionOperationCode.Success, message, before, after, preview, subjectId, faction, affiliation, role, position, recommendation, disposition);
        }

        private static FactionOperationResult Fail(FactionOperationCode code, string message, long before, bool preview = false)
        {
            return new FactionOperationResult(code, message, before, before, preview);
        }

        private static bool Invalid(string message, out string failure)
        {
            failure = message;
            return false;
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> values, Func<T, double> time, Func<T, string> id)
        {
            return (values ?? Array.Empty<T>()).OrderBy(time).ThenBy(id, StringComparer.Ordinal);
        }
    }
}
