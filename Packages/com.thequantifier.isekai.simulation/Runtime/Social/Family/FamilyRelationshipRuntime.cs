using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Relationships;

namespace UnityIsekaiGame.Social.Family
{
    public sealed class FamilyRelationshipRuntime
    {
        private readonly Dictionary<string, HouseholdRecordData> householdsById = new Dictionary<string, HouseholdRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, HouseholdMembershipData> membershipsById = new Dictionary<string, HouseholdMembershipData>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> adultPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private string worldId = PersistenceService.LocalWorldId;
        private RelationshipRuntime relationships;
        private InterpersonalAttitudeRuntime attitudes;
        private SocialInteractionRuntime interactions;
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int HouseholdCount => householdsById.Count;
        public IReadOnlyList<HouseholdSnapshot> Households => OrderedHouseholds(householdsById.Values).Select(item => Snapshot(item.householdId)).Where(item => item != null).ToArray();

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> persons,
            RelationshipRuntime relationshipRuntime,
            InterpersonalAttitudeRuntime attitudeRuntime,
            SocialInteractionRuntime interactionRuntime,
            string world = null,
            IEnumerable<string> adultPersons = null)
        {
            registry = definitionRegistry;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            adultPersonIds = new HashSet<string>((adultPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            relationships = relationshipRuntime;
            attitudes = attitudeRuntime;
            interactions = interactionRuntime;
            worldId = string.IsNullOrWhiteSpace(world) ? PersistenceService.LocalWorldId : world.Trim();
        }

        public FamilyRelationshipMutationResult RecordParentage(FamilyParentageRequest request)
        {
            request ??= new FamilyParentageRequest();
            long before = RelationshipRevision;
            if (!ValidatePerson(request.parentPersonId, out string failure) || !ValidatePerson(request.childPersonId, out failure))
            {
                return FamilyRelationshipMutationResult.Failure(RomanticEligibilityStatus.UnknownPerson, failure, before);
            }

            string parent = request.parentPersonId.Trim();
            string child = request.childPersonId.Trim();
            if (string.Equals(parent, child, StringComparison.Ordinal))
            {
                return FamilyRelationshipMutationResult.Failure(RomanticEligibilityStatus.InvalidRequest, "A Person cannot be their own parent.", before);
            }

            string definitionId = ParentageDefinitionId(request.parentageKind);
            if (!HasDefinition<RelationshipDefinition>(definitionId))
            {
                return FamilyRelationshipMutationResult.Failure(RomanticEligibilityStatus.MissingPolicy, $"Relationship Definition '{definitionId}' is missing.", before);
            }

            KinshipPathResult proposedParentToChild = ResolveKinship(parent, child, KinshipTraversalLimits.Default, privileged: true);
            if (IsDownlineKinship(proposedParentToChild.Classification))
            {
                return FamilyRelationshipMutationResult.Failure(RomanticEligibilityStatus.ProhibitedKinship, $"Parentage would create an ancestry cycle between '{parent}' and '{child}'.", before);
            }

            RelationshipOperationResult create = relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = string.IsNullOrWhiteSpace(request.recordId) ? ParentageRecordId(definitionId, parent, child) : request.recordId.Trim(),
                relationshipDefinitionId = definitionId,
                firstPersonId = parent,
                firstRoleId = IsGuardianKind(request.parentageKind) ? "guardian" : "parent",
                secondPersonId = child,
                secondRoleId = IsGuardianKind(request.parentageKind) ? "dependent" : "child",
                startWorldTime = request.worldTime,
                sourceEventId = request.sourceEventId,
                sourceRecordId = request.sourceRecordId,
                tags = ParentageTags(request.parentageKind, request.evidenceStatus, request.visibility),
                preview = request.preview
            });

            if (!create.Succeeded)
            {
                return FamilyRelationshipMutationResult.Failure(Map(create.Status), create.Message, before);
            }

            return FamilyRelationshipMutationResult.Success(create.Preview ? RomanticEligibilityStatus.Preview : create.Duplicate ? RomanticEligibilityStatus.Duplicate : RomanticEligibilityStatus.Eligible, create.Snapshot, create.Message, create.Preview, create.Duplicate, before, RelationshipRevision);
        }

        public IReadOnlyList<RelationshipSnapshot> GetParents(string childPersonId, ParentageKind? kind = null, bool activeOnly = true, bool privileged = false)
        {
            return ParentageRecords(activeOnly, privileged)
                .Where(record => string.Equals(PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "dependent" : "child"), childPersonId, StringComparison.Ordinal)
                    && (!kind.HasValue || ParentageKindForDefinition(record.RelationshipDefinitionId) == kind.Value))
                .OrderBy(record => ParentageKindForDefinition(record.RelationshipDefinitionId))
                .ThenBy(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "guardian" : "parent"), StringComparer.Ordinal)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<RelationshipSnapshot> GetChildren(string parentPersonId, ParentageKind? kind = null, bool activeOnly = true, bool privileged = false)
        {
            return ParentageRecords(activeOnly, privileged)
                .Where(record => string.Equals(PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "guardian" : "parent"), parentPersonId, StringComparison.Ordinal)
                    && (!kind.HasValue || ParentageKindForDefinition(record.RelationshipDefinitionId) == kind.Value))
                .OrderBy(record => ParentageKindForDefinition(record.RelationshipDefinitionId))
                .ThenBy(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "dependent" : "child"), StringComparer.Ordinal)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<RelationshipSnapshot> GetGuardians(string dependentPersonId, bool activeOnly = true, bool privileged = false)
        {
            return GetParents(dependentPersonId, ParentageKind.Legal, activeOnly, privileged)
                .Concat(GetParents(dependentPersonId, ParentageKind.Foster, activeOnly, privileged))
                .OrderBy(record => record.RecordId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<string> GetPartners(string personId, bool includeFormer = false)
        {
            return ActiveOrHistoricalPartnerRecords(includeFormer)
                .Where(record => record.IncludesPerson(personId))
                .Select(record => OtherPerson(record, personId))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public KinshipPathResult ResolveKinship(string fromPersonId, string toPersonId, KinshipTraversalLimits limits = null, bool privileged = false)
        {
            limits = (limits ?? KinshipTraversalLimits.Default).Clone();
            if (string.IsNullOrWhiteSpace(fromPersonId) || string.IsNullOrWhiteSpace(toPersonId))
            {
                return Result(fromPersonId, toPersonId, KinshipClassification.Indeterminate, KinshipLineageKind.None, "Person IDs are required.");
            }

            string from = fromPersonId.Trim();
            string to = toPersonId.Trim();
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                return Result(from, to, KinshipClassification.Indeterminate, KinshipLineageKind.None, "Kinship to self is indeterminate for relationship eligibility.");
            }

            KinshipPathResult direct = DirectKinship(from, to, privileged);
            if (direct.Classification != KinshipClassification.Unrelated)
            {
                return direct;
            }

            KinshipPathResult sibling = SiblingKinship(from, to, privileged);
            if (sibling.Classification != KinshipClassification.Unrelated)
            {
                return sibling;
            }

            KinshipPathResult inLaw = InLawKinship(from, to, privileged, limits);
            if (inLaw.Classification != KinshipClassification.Unrelated)
            {
                return inLaw;
            }

            AncestorPath ancestor = FindAncestorPath(from, to, limits, privileged);
            if (ancestor.Found)
            {
                KinshipClassification classification = ancestor.Depth == 2 ? KinshipClassification.Grandparent : KinshipClassification.Ancestor;
                return new KinshipPathResult(from, to, classification, ancestor.Lineage, ancestor.Steps, to, 0, 0, ancestor.Truncated, ancestor.Diagnostics);
            }

            AncestorPath descendant = FindAncestorPath(to, from, limits, privileged);
            if (descendant.Found)
            {
                KinshipClassification classification = descendant.Depth == 2 ? KinshipClassification.Grandchild : KinshipClassification.Descendant;
                return new KinshipPathResult(from, to, classification, descendant.Lineage, ReverseSteps(descendant.Steps), from, 0, 0, descendant.Truncated, descendant.Diagnostics);
            }

            KinshipPathResult avuncular = AvuncularKinship(from, to, limits, privileged);
            if (avuncular.Classification != KinshipClassification.Unrelated)
            {
                return avuncular;
            }

            KinshipPathResult cousin = CousinKinship(from, to, limits, privileged);
            if (cousin.Classification != KinshipClassification.Unrelated)
            {
                return cousin;
            }

            bool truncated = ancestor.Truncated || descendant.Truncated;
            return new KinshipPathResult(from, to, truncated ? KinshipClassification.Truncated : KinshipClassification.Unrelated, KinshipLineageKind.None, Array.Empty<KinshipPathStep>(), string.Empty, 0, 0, truncated, truncated ? new[] { "Traversal limit reached before all kinship paths were exhausted." } : Array.Empty<string>());
        }

        public FamilyTreeSnapshot CreateFamilyTreeSnapshot(string focalPersonId, KinshipTraversalLimits limits = null, bool privileged = false)
        {
            limits = (limits ?? KinshipTraversalLimits.Default).Clone();
            string focal = focalPersonId ?? string.Empty;
            List<string> persons = new List<string> { focal };
            List<string> diagnostics = new List<string>();
            bool truncated = false;
            Queue<(string Person, int Depth)> queue = new Queue<(string, int)>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { focal };
            queue.Enqueue((focal, 0));

            while (queue.Count > 0)
            {
                (string person, int depth) = queue.Dequeue();
                if (visited.Count >= limits.maximumVisitedPersons)
                {
                    truncated = true;
                    diagnostics.Add("Maximum visited Persons reached.");
                    break;
                }

                if (depth >= Math.Max(limits.maximumAncestorDepth, limits.maximumDescendantDepth))
                {
                    continue;
                }

                IEnumerable<string> neighbors = ParentageRecords(activeOnly: false, privileged)
                    .Where(record => record.IncludesPerson(person))
                    .Select(record => OtherParentagePerson(record, person))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .OrderBy(value => value, StringComparer.Ordinal);
                foreach (string neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                    {
                        persons.Add(neighbor);
                        queue.Enqueue((neighbor, depth + 1));
                    }
                }
            }

            RelationshipSnapshot[] records = ParentageRecords(activeOnly: false, privileged)
                .Concat(ActiveOrHistoricalPartnerRecords(includeFormer: true))
                .Where(record => record.Participants.Any(endpoint => visited.Contains(endpoint.personId)))
                .OrderBy(record => record.RecordId, StringComparer.Ordinal)
                .ToArray();
            KinshipPathResult[] kinships = persons.Where(person => !string.Equals(person, focal, StringComparison.Ordinal))
                .OrderBy(person => person, StringComparer.Ordinal)
                .Select(person => ResolveKinship(focal, person, limits, privileged))
                .ToArray();
            return new FamilyTreeSnapshot(focal, limits, records, kinships, RelationshipRevision, privileged, truncated || kinships.Any(item => item.Truncated), diagnostics);
        }

        public RomanticEligibilityResult EvaluateRomanticEligibility(RomanticEligibilityRequest request)
        {
            request ??= new RomanticEligibilityRequest();
            if (relationships == null || attitudes == null)
            {
                return Eligibility(false, RomanticEligibilityStatus.MissingRuntime, request, null, "Relationship and attitude runtimes are required.");
            }

            if (!ValidatePerson(request.actorPersonId, out string failure) || !ValidatePerson(request.targetPersonId, out failure))
            {
                return Eligibility(false, RomanticEligibilityStatus.UnknownPerson, request, null, failure);
            }

            if (string.Equals(request.actorPersonId, request.targetPersonId, StringComparison.Ordinal))
            {
                return Eligibility(false, RomanticEligibilityStatus.InvalidRequest, request, null, "Romantic eligibility cannot target the same Person.");
            }

            if (!TryGetPolicy(request.policyDefinitionId, out RomanticEligibilityPolicyDefinition policy))
            {
                return Eligibility(false, RomanticEligibilityStatus.MissingPolicy, request, null, $"Romantic Eligibility Policy '{request.policyDefinitionId}' is missing.");
            }

            string actor = request.actorPersonId.Trim();
            string target = request.targetPersonId.Trim();
            KinshipPathResult kinship = ResolveKinship(actor, target, KinshipTraversalLimits.Default, privileged: true);
            bool actorAdult = adultPersonIds.Contains(actor);
            bool targetAdult = adultPersonIds.Contains(target);
            bool guardianDependent = IsGuardianDependent(actor, target);
            bool exclusiveConflict = policy.ExclusivePartnerships && HasActiveExclusivePartner(actor, target);
            bool consentAccepted = IsAcceptedConsent(request.ConsentKind(), request.consentInteractionId);
            int actorAttraction = attitudes.ResolveValue(actor, target, PrototypeAttitudeDefinitionFactory.RomanticAttractionId).EffectiveValue;
            int targetAttraction = attitudes.ResolveValue(target, actor, PrototypeAttitudeDefinitionFactory.RomanticAttractionId).EffectiveValue;
            int actorAffection = attitudes.ResolveValue(actor, target, PrototypeAttitudeDefinitionFactory.AffectionId).EffectiveValue;
            int targetAffection = attitudes.ResolveValue(target, actor, PrototypeAttitudeDefinitionFactory.AffectionId).EffectiveValue;
            List<string> reasons = new List<string>();

            if (policy.RequireAdults && adultPersonIds.Count == 0)
            {
                reasons.Add("Adult life-stage authority is unresolved.");
                return new RomanticEligibilityResult(false, RomanticEligibilityStatus.UnresolvedLifeStage, actor, target, policy.Id, kinship, false, false, guardianDependent, exclusiveConflict, consentAccepted, actorAttraction, targetAttraction, actorAffection, targetAffection, reasons, request.preview);
            }

            if (policy.RequireAdults && (!actorAdult || !targetAdult))
            {
                reasons.Add("Both participants must be adults.");
            }

            if (policy.ProhibitGuardianDependent && guardianDependent)
            {
                reasons.Add("Current guardian-dependent relationships prohibit romantic progression.");
            }

            if (policy.ProhibitedKinshipClassifications.Contains(kinship.Classification) || kinship.IsProhibitedRomanceKinship)
            {
                reasons.Add($"Kinship '{kinship.Classification}' prohibits romantic progression.");
            }

            if (exclusiveConflict)
            {
                reasons.Add("An active exclusive partnership already exists.");
            }

            if (policy.RequireConsent && !consentAccepted)
            {
                reasons.Add(string.IsNullOrWhiteSpace(request.consentInteractionId) ? "Explicit consent is required." : "The supplied consent reference is not accepted consent.");
            }

            if (policy.MinimumRomanticAttraction > 0 && (actorAttraction < policy.MinimumRomanticAttraction || targetAttraction < policy.MinimumRomanticAttraction))
            {
                reasons.Add("Romantic attraction is below the authored threshold.");
            }

            if (policy.MinimumAffection > 0 && (actorAffection < policy.MinimumAffection || targetAffection < policy.MinimumAffection))
            {
                reasons.Add("Affection is below the authored threshold.");
            }

            RomanticEligibilityStatus status = reasons.Count == 0
                ? RomanticEligibilityStatus.Eligible
                : reasons.Any(reason => reason.Contains("adult", StringComparison.OrdinalIgnoreCase)) ? RomanticEligibilityStatus.NonAdult
                : guardianDependent ? RomanticEligibilityStatus.GuardianDependent
                : exclusiveConflict ? RomanticEligibilityStatus.ExistingExclusivePartnership
                : !consentAccepted ? (request.consentKind == RomanticConsentKind.Compliance || request.consentKind == RomanticConsentKind.Influence || request.consentKind == RomanticConsentKind.InferredFromAttraction ? RomanticEligibilityStatus.InvalidConsent : RomanticEligibilityStatus.MissingConsent)
                : RomanticEligibilityStatus.ProhibitedKinship;

            return new RomanticEligibilityResult(reasons.Count == 0, status, actor, target, policy.Id, kinship, actorAdult, targetAdult, guardianDependent, exclusiveConflict, consentAccepted, actorAttraction, targetAttraction, actorAffection, targetAffection, reasons, request.preview);
        }

        public RomanticTransitionResult ExecuteRomanticTransition(RomanticTransitionRequest request)
        {
            request ??= new RomanticTransitionRequest();
            long before = revision + RelationshipRevision;
            if (string.IsNullOrWhiteSpace(request.transactionId))
            {
                return TransitionFailure(RomanticEligibilityStatus.InvalidRequest, "Romantic transition requires a transaction ID.", request, null, before);
            }

            string transactionId = request.transactionId.Trim();
            if (processedTransactionIds.Contains(transactionId))
            {
                RelationshipSnapshot existing = RelationshipForTransition(request);
                return new RomanticTransitionResult(true, RomanticEligibilityStatus.Duplicate, existing, null, null, "Romantic transition transaction was already processed.", false, true, before, before);
            }

            RomanticEligibilityResult eligibility = EvaluateRomanticEligibility(new RomanticEligibilityRequest
            {
                actorPersonId = request.actorPersonId,
                targetPersonId = request.targetPersonId,
                policyDefinitionId = request.policyDefinitionId,
                transitionKind = request.transitionKind,
                consentKind = request.consentKind,
                consentInteractionId = request.consentInteractionId,
                preview = request.preview
            });
            if (!eligibility.Eligible && RequiresEligibility(request.transitionKind))
            {
                return TransitionFailure(eligibility.Status, string.Join(" ", eligibility.FailureReasons), request, eligibility, before);
            }

            string destination = RomanticRelationshipDefinitionFor(request.transitionKind);
            if (string.IsNullOrWhiteSpace(destination))
            {
                return TransitionFailure(RomanticEligibilityStatus.InvalidRequest, $"Unsupported romantic transition '{request.transitionKind}'.", request, eligibility, before);
            }

            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            RelationshipRuntimeSaveData relationshipRollback = relationships?.CreateSaveData();
            RelationshipSnapshot ended = null;
            if (!string.IsNullOrWhiteSpace(request.currentRelationshipRecordId))
            {
                RelationshipOperationResult end = relationships.EndRelationship(new RelationshipEndRequest
                {
                    recordId = request.currentRelationshipRecordId,
                    endWorldTime = request.worldTime,
                    sourceRecordId = request.consentInteractionId,
                    preview = request.preview
                });
                if (!end.Succeeded && !end.Duplicate)
                {
                    RestoreInternal(rollback);
                    relationships?.RestoreFromSaveData(relationshipRollback, registry, knownPersonIds, restoring: true);
                    return TransitionFailure(Map(end.Status), end.Message, request, eligibility, before);
                }

                ended = end.Snapshot;
            }

            RelationshipOperationResult create = relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = string.IsNullOrWhiteSpace(request.relationshipRecordId) ? RomanticRecordId(destination, request.actorPersonId, request.targetPersonId, request.transitionKind) : request.relationshipRecordId.Trim(),
                relationshipDefinitionId = destination,
                firstPersonId = request.actorPersonId,
                firstRoleId = "partner",
                secondPersonId = request.targetPersonId,
                secondRoleId = "partner",
                startWorldTime = request.worldTime,
                sourceRecordId = request.consentInteractionId,
                tags = new[] { "romantic", $"transition:{request.transitionKind}", "consent:explicit" },
                preview = request.preview
            });

            if (!create.Succeeded)
            {
                RestoreInternal(rollback);
                relationships?.RestoreFromSaveData(relationshipRollback, registry, knownPersonIds, restoring: true);
                return TransitionFailure(Map(create.Status), create.Message, request, eligibility, before);
            }

            if (request.preview)
            {
                RestoreInternal(rollback);
                relationships?.RestoreFromSaveData(relationshipRollback, registry, knownPersonIds, restoring: true);
                return new RomanticTransitionResult(true, RomanticEligibilityStatus.Preview, create.Snapshot, ended, eligibility, "Romantic transition previewed.", true, false, before, before);
            }

            processedTransactionIds.Add(transactionId);
            revision++;
            dirty = true;
            return new RomanticTransitionResult(true, create.Duplicate ? RomanticEligibilityStatus.Duplicate : RomanticEligibilityStatus.Eligible, create.Snapshot, ended, eligibility, "Romantic transition executed.", false, create.Duplicate, before, revision + RelationshipRevision);
        }

        public HouseholdMutationResult CreateHousehold(HouseholdMutationRequest request)
        {
            request ??= new HouseholdMutationRequest();
            long before = revision;
            if (string.IsNullOrWhiteSpace(request.transactionId))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRequest, "Household creation requires a transaction ID.", before);
            }

            if (processedTransactionIds.Contains(request.transactionId.Trim()))
            {
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Duplicate, "Household transaction already processed.", Snapshot(request.householdId), false, true, before, before);
            }

            string householdId = request.householdId?.Trim();
            if (string.IsNullOrWhiteSpace(householdId))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRequest, "Household ID is required.", before);
            }

            if (householdsById.ContainsKey(householdId))
            {
                return HouseholdFailure(HouseholdOperationStatus.DuplicateHousehold, $"Household '{householdId}' already exists.", before);
            }

            if (!TryGetHouseholdDefinition(request.householdDefinitionId, out HouseholdDefinition definition, out string failure))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingDefinition, failure, before);
            }

            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            householdsById[householdId] = new HouseholdRecordData
            {
                householdId = householdId,
                householdDefinitionId = definition.Id,
                worldId = worldId,
                residencePlaceId = request.residencePlaceId ?? string.Empty,
                propertyReferenceId = request.propertyReferenceId ?? string.Empty,
                createdWorldTime = request.worldTime,
                sourceEventId = request.sourceEventId ?? string.Empty,
                accessPolicyId = definition.DefaultAccessPolicyId,
                tags = HouseholdRecordData.Clean(definition.Tags),
                revision = 1L
            };

            if (!string.IsNullOrWhiteSpace(request.personId))
            {
                if (!ValidatePerson(request.personId, out failure))
                {
                    RestoreInternal(rollback);
                    return HouseholdFailure(HouseholdOperationStatus.UnknownPerson, failure, before);
                }

                if (!definition.AllowsRole(request.role))
                {
                    RestoreInternal(rollback);
                    return HouseholdFailure(HouseholdOperationStatus.InvalidRole, $"Household role '{request.role}' is not allowed.", before);
                }

                string membershipId = string.IsNullOrWhiteSpace(request.membershipId) ? MembershipId(householdId, request.personId, request.worldTime) : request.membershipId.Trim();
                if (membershipsById.ContainsKey(membershipId))
                {
                    RestoreInternal(rollback);
                    return HouseholdFailure(HouseholdOperationStatus.DuplicateMembership, $"Membership '{membershipId}' already exists.", before);
                }

                membershipsById[membershipId] = new HouseholdMembershipData
                {
                    membershipId = membershipId,
                    householdId = householdId,
                    personId = request.personId.Trim(),
                    role = request.role,
                    joinWorldTime = request.worldTime,
                    sourceEventId = request.sourceEventId ?? string.Empty,
                    sourceInteractionId = request.sourceInteractionId ?? string.Empty,
                    revision = 1L
                };
            }

            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, worldId, out failure))
            {
                RestoreInternal(rollback);
                return HouseholdFailure(HouseholdOperationStatus.ValidationFailed, failure, before);
            }

            HouseholdSnapshot snapshot = Snapshot(householdId);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Preview, "Household creation previewed.", snapshot, true, false, before, before);
            }

            processedTransactionIds.Add(request.transactionId.Trim());
            revision++;
            dirty = true;
            return new HouseholdMutationResult(true, HouseholdOperationStatus.Succeeded, "Household created.", Snapshot(householdId), false, false, before, revision);
        }

        public HouseholdMutationResult AddMember(HouseholdMutationRequest request)
        {
            return AddMemberInternal(request, markTransaction: true);
        }

        public HouseholdMutationResult ChangeMemberRole(HouseholdMutationRequest request)
        {
            request ??= new HouseholdMutationRequest();
            long before = revision;
            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            if (!TryGetActiveMembership(request.householdId, request.personId, out HouseholdMembershipData membership))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingParticipant, "Active household membership is missing.", before);
            }

            if (!TryGetHouseholdDefinition(householdsById[membership.householdId].householdDefinitionId, out HouseholdDefinition definition, out string failure) || !definition.AllowsRole(request.role))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRole, "Household role is not allowed by the definition.", before);
            }

            membership.role = request.role;
            membership.revision++;
            return CommitHouseholdMutation(request, rollback, membership.householdId, "Household membership role changed.", before);
        }

        public HouseholdMutationResult EndMembership(HouseholdMutationRequest request)
        {
            request ??= new HouseholdMutationRequest();
            long before = revision;
            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            if (!TryGetActiveMembership(request.householdId, request.personId, out HouseholdMembershipData membership))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingParticipant, "Active household membership is missing.", before);
            }

            membership.status = HouseholdMembershipStatus.Ended;
            membership.leaveWorldTime = Math.Max(request.worldTime, membership.joinWorldTime);
            membership.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? membership.sourceEventId : request.sourceEventId.Trim();
            membership.sourceInteractionId = string.IsNullOrWhiteSpace(request.sourceInteractionId) ? membership.sourceInteractionId : request.sourceInteractionId.Trim();
            membership.revision++;
            return CommitHouseholdMutation(request, rollback, membership.householdId, "Household membership ended.", before);
        }

        public HouseholdMutationResult SetResidence(HouseholdMutationRequest request)
        {
            request ??= new HouseholdMutationRequest();
            long before = revision;
            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            if (!householdsById.TryGetValue(request.householdId ?? string.Empty, out HouseholdRecordData household))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingHousehold, $"Household '{request.householdId}' is missing.", before);
            }

            household.residencePlaceId = request.residencePlaceId ?? string.Empty;
            household.propertyReferenceId = request.propertyReferenceId ?? string.Empty;
            household.revision++;
            return CommitHouseholdMutation(request, rollback, household.householdId, "Household residence reference updated.", before);
        }

        public HouseholdMutationResult DissolveHousehold(HouseholdMutationRequest request)
        {
            request ??= new HouseholdMutationRequest();
            long before = revision;
            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            if (!householdsById.TryGetValue(request.householdId ?? string.Empty, out HouseholdRecordData household))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingHousehold, $"Household '{request.householdId}' is missing.", before);
            }

            household.status = HouseholdLifecycleStatus.Dissolved;
            household.endedWorldTime = Math.Max(request.worldTime, household.createdWorldTime);
            household.revision++;
            foreach (HouseholdMembershipData membership in ActiveMemberships(household.householdId))
            {
                membership.status = HouseholdMembershipStatus.Ended;
                membership.leaveWorldTime = household.endedWorldTime;
                membership.revision++;
            }

            return CommitHouseholdMutation(request, rollback, household.householdId, "Household dissolved.", before);
        }

        public HouseholdMutationResult SplitHousehold(HouseholdTransferRequest request)
        {
            request ??= new HouseholdTransferRequest();
            long before = revision;
            if (string.IsNullOrWhiteSpace(request.transactionId))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRequest, "Household split requires a transaction ID.", before);
            }

            if (processedTransactionIds.Contains(request.transactionId.Trim()))
            {
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Duplicate, "Household split transaction already processed.", Snapshot(request.targetHouseholdId), false, true, before, before);
            }

            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            if (!householdsById.TryGetValue(request.sourceHouseholdId ?? string.Empty, out HouseholdRecordData source))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingHousehold, $"Source household '{request.sourceHouseholdId}' is missing.", before);
            }

            string targetHouseholdId = request.targetHouseholdId?.Trim();
            if (string.IsNullOrWhiteSpace(targetHouseholdId) || householdsById.ContainsKey(targetHouseholdId))
            {
                RestoreInternal(rollback);
                return HouseholdFailure(string.IsNullOrWhiteSpace(targetHouseholdId) ? HouseholdOperationStatus.InvalidRequest : HouseholdOperationStatus.DuplicateHousehold, $"Target household '{request.targetHouseholdId}' is invalid or already exists.", before);
            }

            string targetDefinitionId = string.IsNullOrWhiteSpace(request.targetHouseholdDefinitionId) ? source.householdDefinitionId : request.targetHouseholdDefinitionId.Trim();
            if (!TryGetHouseholdDefinition(targetDefinitionId, out HouseholdDefinition targetDefinition, out string targetFailure))
            {
                RestoreInternal(rollback);
                return HouseholdFailure(HouseholdOperationStatus.MissingDefinition, targetFailure, before);
            }

            householdsById[targetHouseholdId] = new HouseholdRecordData
            {
                householdId = targetHouseholdId,
                householdDefinitionId = targetDefinition.Id,
                worldId = worldId,
                residencePlaceId = request.residencePlaceId ?? string.Empty,
                propertyReferenceId = request.propertyReferenceId ?? string.Empty,
                createdWorldTime = request.worldTime,
                accessPolicyId = targetDefinition.DefaultAccessPolicyId,
                tags = HouseholdRecordData.Clean(targetDefinition.Tags),
                revision = 1L
            };
            source.status = HouseholdLifecycleStatus.Split;
            source.revision++;

            foreach (string personId in request.memberPersonIds ?? Array.Empty<string>())
            {
                if (!TryGetActiveMembership(source.householdId, personId, out HouseholdMembershipData membership))
                {
                    RestoreInternal(rollback);
                    return HouseholdFailure(HouseholdOperationStatus.MissingParticipant, $"Person '{personId}' is not an active member of household '{source.householdId}'.", before);
                }

                membership.status = HouseholdMembershipStatus.Ended;
                membership.leaveWorldTime = request.worldTime;
                HouseholdMutationResult added = AddMemberInternal(new HouseholdMutationRequest
                {
                    transactionId = request.transactionId + ".member." + personId,
                    householdId = request.targetHouseholdId,
                    personId = personId,
                    role = membership.role,
                    worldTime = request.worldTime
                }, markTransaction: false);
                if (!added.Succeeded)
                {
                    RestoreInternal(rollback);
                    return added;
                }
            }

            return CommitTransferMutation(request, rollback, request.targetHouseholdId, "Household split executed.", before);
        }

        public HouseholdMutationResult MergeHouseholds(HouseholdTransferRequest request)
        {
            request ??= new HouseholdTransferRequest();
            long before = revision;
            if (string.IsNullOrWhiteSpace(request.transactionId))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRequest, "Household merge requires a transaction ID.", before);
            }

            if (processedTransactionIds.Contains(request.transactionId.Trim()))
            {
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Duplicate, "Household merge transaction already processed.", Snapshot(request.targetHouseholdId), false, true, before, before);
            }

            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            if (!householdsById.TryGetValue(request.sourceHouseholdId ?? string.Empty, out HouseholdRecordData source)
                || !householdsById.TryGetValue(request.targetHouseholdId ?? string.Empty, out HouseholdRecordData target))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingHousehold, "Source or target household is missing.", before);
            }

            source.status = HouseholdLifecycleStatus.Merged;
            source.endedWorldTime = request.worldTime;
            source.revision++;
            foreach (HouseholdMembershipData membership in ActiveMemberships(source.householdId).ToArray())
            {
                membership.status = HouseholdMembershipStatus.Ended;
                membership.leaveWorldTime = request.worldTime;
                if (!TryGetActiveMembership(target.householdId, membership.personId, out _))
                {
                    HouseholdMutationResult added = AddMemberInternal(new HouseholdMutationRequest
                    {
                        transactionId = request.transactionId + ".member." + membership.personId,
                        householdId = target.householdId,
                        personId = membership.personId,
                        role = membership.role,
                        worldTime = request.worldTime
                    }, markTransaction: false);
                    if (!added.Succeeded)
                    {
                        RestoreInternal(rollback);
                        return added;
                    }
                }
            }

            return CommitTransferMutation(request, rollback, target.householdId, "Households merged.", before);
        }

        public IReadOnlyList<HouseholdSnapshot> QueryHouseholdsByPerson(string personId, bool activeOnly = true)
        {
            return OrderedMemberships(membershipsById.Values)
                .Where(membership => string.Equals(membership.personId, personId, StringComparison.Ordinal) && (!activeOnly || membership.status == HouseholdMembershipStatus.Active))
                .Select(membership => Snapshot(membership.householdId))
                .Where(snapshot => snapshot != null)
                .GroupBy(snapshot => snapshot.HouseholdId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(snapshot => snapshot.HouseholdId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool TryGetHousehold(string householdId, out HouseholdSnapshot snapshot)
        {
            snapshot = Snapshot(householdId);
            return snapshot != null;
        }

        public FamilyRelationshipRuntimeSaveData CreateSaveData()
        {
            return new FamilyRelationshipRuntimeSaveData
            {
                schemaVersion = FamilyRelationshipRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                households = OrderedHouseholds(householdsById.Values).Select(item => item.Clone()).ToList(),
                memberships = OrderedMemberships(membershipsById.Values).Select(item => item.Clone()).ToList(),
                processedTransactionIds = processedTransactionIds.OrderBy(value => value, StringComparer.Ordinal).ToList()
            };
        }

        public RomanticTransitionResult RestoreFromSaveData(FamilyRelationshipRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, worldId, out string failure))
            {
                return new RomanticTransitionResult(false, RomanticEligibilityStatus.RestoreFailed, null, null, null, failure, false, false, before, before);
            }

            registry = definitionRegistry;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            RestoreInternal(saveData ?? new FamilyRelationshipRuntimeSaveData());
            dirty = !restoringState;
            return new RomanticTransitionResult(true, RomanticEligibilityStatus.Eligible, null, null, null, "Family relationship runtime restored.", false, false, before, revision);
        }

        public static bool ValidateSaveData(FamilyRelationshipRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, string expectedWorldId, out string failure)
        {
            HashSet<string> known = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            return ValidateAll(saveData ?? new FamilyRelationshipRuntimeSaveData(), definitionRegistry, known, string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim(), out failure);
        }

        private HouseholdMutationResult AddMemberInternal(HouseholdMutationRequest request, bool markTransaction)
        {
            request ??= new HouseholdMutationRequest();
            long before = revision;
            if (markTransaction && string.IsNullOrWhiteSpace(request.transactionId))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRequest, "Household member mutation requires a transaction ID.", before);
            }

            if (markTransaction && processedTransactionIds.Contains(request.transactionId.Trim()))
            {
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Duplicate, "Household member transaction already processed.", Snapshot(request.householdId), false, true, before, before);
            }

            if (!householdsById.TryGetValue(request.householdId ?? string.Empty, out HouseholdRecordData household))
            {
                return HouseholdFailure(HouseholdOperationStatus.MissingHousehold, $"Household '{request.householdId}' is missing.", before);
            }

            if (!ValidatePerson(request.personId, out string failure))
            {
                return HouseholdFailure(HouseholdOperationStatus.UnknownPerson, failure, before);
            }

            if (!TryGetHouseholdDefinition(household.householdDefinitionId, out HouseholdDefinition definition, out failure) || !definition.AllowsRole(request.role))
            {
                return HouseholdFailure(HouseholdOperationStatus.InvalidRole, $"Household role '{request.role}' is not allowed.", before);
            }

            if (TryGetActiveMembership(household.householdId, request.personId, out _))
            {
                return HouseholdFailure(HouseholdOperationStatus.DuplicateActiveMembership, $"Person '{request.personId}' already has active membership in household '{household.householdId}'.", before);
            }

            FamilyRelationshipRuntimeSaveData rollback = CreateSaveData();
            string membershipId = string.IsNullOrWhiteSpace(request.membershipId) ? MembershipId(household.householdId, request.personId, request.worldTime) : request.membershipId.Trim();
            if (membershipsById.ContainsKey(membershipId))
            {
                return HouseholdFailure(HouseholdOperationStatus.DuplicateMembership, $"Membership '{membershipId}' already exists.", before);
            }

            membershipsById[membershipId] = new HouseholdMembershipData
            {
                membershipId = membershipId,
                householdId = household.householdId,
                personId = request.personId.Trim(),
                role = request.role,
                joinWorldTime = request.worldTime,
                sourceEventId = request.sourceEventId ?? string.Empty,
                sourceInteractionId = request.sourceInteractionId ?? string.Empty,
                revision = 1L
            };
            return CommitHouseholdMutation(request, rollback, household.householdId, "Household member added.", before, markTransaction);
        }

        private HouseholdMutationResult CommitHouseholdMutation(HouseholdMutationRequest request, FamilyRelationshipRuntimeSaveData rollback, string householdId, string message, long before, bool markTransaction = true)
        {
            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, worldId, out string failure))
            {
                RestoreInternal(rollback);
                return HouseholdFailure(HouseholdOperationStatus.ValidationFailed, failure, before);
            }

            HouseholdSnapshot snapshot = Snapshot(householdId);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Preview, message, snapshot, true, false, before, before);
            }

            if (markTransaction && !string.IsNullOrWhiteSpace(request.transactionId))
            {
                processedTransactionIds.Add(request.transactionId.Trim());
            }

            revision++;
            dirty = true;
            return new HouseholdMutationResult(true, HouseholdOperationStatus.Succeeded, message, Snapshot(householdId), false, false, before, revision);
        }

        private HouseholdMutationResult CommitTransferMutation(HouseholdTransferRequest request, FamilyRelationshipRuntimeSaveData rollback, string householdId, string message, long before)
        {
            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, worldId, out string failure))
            {
                RestoreInternal(rollback);
                return HouseholdFailure(HouseholdOperationStatus.ValidationFailed, failure, before);
            }

            HouseholdSnapshot snapshot = Snapshot(householdId);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return new HouseholdMutationResult(true, HouseholdOperationStatus.Preview, message, snapshot, true, false, before, before);
            }

            if (!string.IsNullOrWhiteSpace(request.transactionId))
            {
                processedTransactionIds.Add(request.transactionId.Trim());
            }

            revision++;
            dirty = true;
            return new HouseholdMutationResult(true, HouseholdOperationStatus.Succeeded, message, Snapshot(householdId), false, false, before, revision);
        }

        private RelationshipSnapshot RelationshipForTransition(RomanticTransitionRequest request)
        {
            string destination = RomanticRelationshipDefinitionFor(request?.transitionKind ?? RomanticTransitionKind.ProposeCourtship);
            return relationships?.QueryBetween(request?.actorPersonId, request?.targetPersonId, activeOnly: false)
                .FirstOrDefault(record => string.Equals(record.RelationshipDefinitionId, destination, StringComparison.Ordinal));
        }

        private RomanticTransitionResult TransitionFailure(RomanticEligibilityStatus status, string message, RomanticTransitionRequest request, RomanticEligibilityResult eligibility, long before)
        {
            return new RomanticTransitionResult(false, status, null, null, eligibility, message, request?.preview ?? false, false, before, before);
        }

        private static RomanticEligibilityStatus Map(RelationshipOperationStatus status)
        {
            return status switch
            {
                RelationshipOperationStatus.Duplicate => RomanticEligibilityStatus.Duplicate,
                RelationshipOperationStatus.Preview => RomanticEligibilityStatus.Preview,
                RelationshipOperationStatus.MissingDefinition => RomanticEligibilityStatus.MissingPolicy,
                RelationshipOperationStatus.UnknownPerson => RomanticEligibilityStatus.UnknownPerson,
                RelationshipOperationStatus.SelfRelationshipNotAllowed => RomanticEligibilityStatus.InvalidRequest,
                _ => RomanticEligibilityStatus.InvalidRequest
            };
        }

        private RomanticEligibilityResult Eligibility(bool eligible, RomanticEligibilityStatus status, RomanticEligibilityRequest request, KinshipPathResult kinship, string reason)
        {
            return new RomanticEligibilityResult(eligible, status, request?.actorPersonId, request?.targetPersonId, request?.policyDefinitionId, kinship, false, false, false, false, false, 0, 0, 0, 0, string.IsNullOrWhiteSpace(reason) ? Array.Empty<string>() : new[] { reason }, request?.preview ?? false);
        }

        private bool IsAcceptedConsent(RomanticConsentKind kind, string consentInteractionId)
        {
            if (kind == RomanticConsentKind.PlayerChoice || kind == RomanticConsentKind.ScriptedAuthority)
            {
                return true;
            }

            return kind == RomanticConsentKind.ExplicitAcceptedInteraction && !string.IsNullOrWhiteSpace(consentInteractionId);
        }

        private bool HasActiveExclusivePartner(string actor, string target)
        {
            return ActiveOrHistoricalPartnerRecords(includeFormer: false)
                .Any(record => record.IncludesPerson(actor)
                    && !record.IncludesPerson(target)
                    && (record.RelationshipDefinitionId == PrototypeRelationshipDefinitionFactory.SpouseRelationshipId
                        || record.RelationshipDefinitionId == PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId
                        || record.RelationshipDefinitionId == PrototypeRelationshipDefinitionFactory.EngagedPartnerRelationshipId));
        }

        private bool IsGuardianDependent(string first, string second)
        {
            return relationships != null
                && relationships.QueryBetween(first, second, activeOnly: true).Any(record => IsGuardianDefinition(record.RelationshipDefinitionId));
        }

        private bool RequiresEligibility(RomanticTransitionKind transition)
        {
            return transition != RomanticTransitionKind.RejectCourtship
                && transition != RomanticTransitionKind.RejectEngagement
                && transition != RomanticTransitionKind.EndCourtship
                && transition != RomanticTransitionKind.EndPartnership
                && transition != RomanticTransitionKind.RecordWidowhood;
        }

        private static string RomanticRelationshipDefinitionFor(RomanticTransitionKind transition)
        {
            return transition switch
            {
                RomanticTransitionKind.ProposeCourtship => PrototypeRelationshipDefinitionFactory.CourtshipPartnerRelationshipId,
                RomanticTransitionKind.AcceptCourtship => PrototypeRelationshipDefinitionFactory.CourtshipPartnerRelationshipId,
                RomanticTransitionKind.ProposeEngagement => PrototypeRelationshipDefinitionFactory.EngagedPartnerRelationshipId,
                RomanticTransitionKind.AcceptEngagement => PrototypeRelationshipDefinitionFactory.EngagedPartnerRelationshipId,
                RomanticTransitionKind.EstablishPartnership => PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId,
                RomanticTransitionKind.EstablishMarriage => PrototypeRelationshipDefinitionFactory.SpouseRelationshipId,
                RomanticTransitionKind.RequestSeparation => PrototypeRelationshipDefinitionFactory.SeparatedPartnerRelationshipId,
                RomanticTransitionKind.ConfirmSeparation => PrototypeRelationshipDefinitionFactory.SeparatedPartnerRelationshipId,
                RomanticTransitionKind.Reconcile => PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId,
                RomanticTransitionKind.RejectCourtship => PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId,
                RomanticTransitionKind.RejectEngagement => PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId,
                RomanticTransitionKind.EndCourtship => PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId,
                RomanticTransitionKind.EndPartnership => PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId,
                RomanticTransitionKind.RecordWidowhood => PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId,
                _ => string.Empty
            };
        }

        private KinshipPathResult DirectKinship(string from, string to, bool privileged)
        {
            foreach (RelationshipSnapshot record in ParentageRecords(activeOnly: true, privileged))
            {
                bool guardian = IsGuardianDefinition(record.RelationshipDefinitionId);
                string parentRole = guardian ? "guardian" : "parent";
                string childRole = guardian ? "dependent" : "child";
                string parent = PersonByRole(record, parentRole);
                string child = PersonByRole(record, childRole);
                ParentageKind kind = ParentageKindForDefinition(record.RelationshipDefinitionId);
                if (string.Equals(from, parent, StringComparison.Ordinal) && string.Equals(to, child, StringComparison.Ordinal))
                {
                    return new KinshipPathResult(from, to, guardian ? KinshipClassification.Guardian : kind == ParentageKind.Biological ? KinshipClassification.BiologicalParent : kind == ParentageKind.Adoptive ? KinshipClassification.AdoptiveParent : KinshipClassification.Parent, Lineage(kind), Step(record, from, parentRole, to, childRole), string.Empty, 0, 0, false, Array.Empty<string>());
                }

                if (string.Equals(from, child, StringComparison.Ordinal) && string.Equals(to, parent, StringComparison.Ordinal))
                {
                    return new KinshipPathResult(from, to, guardian ? KinshipClassification.Dependent : kind == ParentageKind.Biological ? KinshipClassification.BiologicalChild : kind == ParentageKind.Adoptive ? KinshipClassification.AdoptiveChild : KinshipClassification.Child, Lineage(kind), Step(record, from, childRole, to, parentRole), string.Empty, 0, 0, false, Array.Empty<string>());
                }
            }

            foreach (RelationshipSnapshot record in ActiveOrHistoricalPartnerRecords(includeFormer: true))
            {
                if (record.IncludesPerson(from) && record.IncludesPerson(to))
                {
                    KinshipClassification classification = record.RelationshipDefinitionId == PrototypeRelationshipDefinitionFactory.SpouseRelationshipId
                        ? KinshipClassification.Spouse
                        : record.RelationshipDefinitionId == PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId
                            ? KinshipClassification.FormerPartner
                            : KinshipClassification.Partner;
                    return new KinshipPathResult(from, to, classification, KinshipLineageKind.None, Step(record, from, "partner", to, "partner"), string.Empty, 0, 0, false, Array.Empty<string>());
                }
            }

            return Result(from, to, KinshipClassification.Unrelated, KinshipLineageKind.None);
        }

        private KinshipPathResult SiblingKinship(string from, string to, bool privileged)
        {
            RelationshipSnapshot[] fromBio = GetParents(from, ParentageKind.Biological, true, privileged).ToArray();
            RelationshipSnapshot[] toBio = GetParents(to, ParentageKind.Biological, true, privileged).ToArray();
            int sharedBio = SharedParents(fromBio, toBio);
            if (sharedBio >= 2)
            {
                return Result(from, to, KinshipClassification.FullSibling, KinshipLineageKind.Biological);
            }

            if (sharedBio == 1)
            {
                return Result(from, to, KinshipClassification.HalfSibling, KinshipLineageKind.Biological);
            }

            if (SharedParents(GetParents(from, ParentageKind.Adoptive, true, privileged), GetParents(to, ParentageKind.Adoptive, true, privileged)) > 0)
            {
                return Result(from, to, KinshipClassification.AdoptiveSibling, KinshipLineageKind.Adoptive);
            }

            string[] fromParents = ParentPeople(from, privileged);
            string[] toParents = ParentPeople(to, privileged);
            bool step = fromParents.Any(parent => ActiveOrHistoricalPartnerRecords(includeFormer: false).Any(record => record.IncludesPerson(parent) && toParents.Contains(OtherPerson(record, parent), StringComparer.Ordinal)));
            return step ? Result(from, to, KinshipClassification.StepSibling, KinshipLineageKind.Step) : Result(from, to, KinshipClassification.Unrelated, KinshipLineageKind.None);
        }

        private KinshipPathResult AvuncularKinship(string from, string to, KinshipTraversalLimits limits, bool privileged)
        {
            string[] toParents = ParentPeople(to, privileged);
            if (toParents.Any(parent => SiblingKinship(from, parent, privileged).Classification is KinshipClassification.FullSibling or KinshipClassification.HalfSibling or KinshipClassification.AdoptiveSibling))
            {
                return Result(from, to, KinshipClassification.AuntOrUncle, KinshipLineageKind.Mixed);
            }

            string[] fromParents = ParentPeople(from, privileged);
            if (fromParents.Any(parent => SiblingKinship(to, parent, privileged).Classification is KinshipClassification.FullSibling or KinshipClassification.HalfSibling or KinshipClassification.AdoptiveSibling))
            {
                return Result(from, to, KinshipClassification.NieceOrNephew, KinshipLineageKind.Mixed);
            }

            return Result(from, to, KinshipClassification.Unrelated, KinshipLineageKind.None);
        }

        private KinshipPathResult CousinKinship(string from, string to, KinshipTraversalLimits limits, bool privileged)
        {
            Dictionary<string, AncestorPath> fromAncestors = AncestorMap(from, limits, privileged);
            Dictionary<string, AncestorPath> toAncestors = AncestorMap(to, limits, privileged);
            string common = fromAncestors.Keys.Intersect(toAncestors.Keys, StringComparer.Ordinal).OrderBy(id => Math.Max(fromAncestors[id].Depth, toAncestors[id].Depth)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(common))
            {
                return Result(from, to, KinshipClassification.Unrelated, KinshipLineageKind.None);
            }

            int degree = Math.Min(fromAncestors[common].Depth, toAncestors[common].Depth) - 1;
            int removal = Math.Abs(fromAncestors[common].Depth - toAncestors[common].Depth);
            bool truncated = degree > limits.maximumCousinDegree || removal > limits.maximumRemovalCount || fromAncestors[common].Truncated || toAncestors[common].Truncated;
            KinshipClassification classification = truncated
                ? KinshipClassification.Truncated
                : degree <= 1 && removal == 0 ? KinshipClassification.FirstCousin : KinshipClassification.MoreDistantCousin;
            KinshipLineageKind lineage = Combine(fromAncestors[common].Lineage, toAncestors[common].Lineage);
            return new KinshipPathResult(from, to, classification, lineage, fromAncestors[common].Steps.Concat(ReverseSteps(toAncestors[common].Steps)), common, Math.Max(1, degree), removal, truncated, truncated ? new[] { "Cousin traversal exceeded authored limits." } : Array.Empty<string>());
        }

        private KinshipPathResult InLawKinship(string from, string to, bool privileged, KinshipTraversalLimits limits)
        {
            if (limits.maximumInLawTraversalDepth <= 0)
            {
                return Result(from, to, KinshipClassification.Unrelated, KinshipLineageKind.None);
            }

            foreach (string partner in GetPartners(from))
            {
                KinshipPathResult partnerToTarget = ResolveKinship(partner, to, new KinshipTraversalLimits { maximumInLawTraversalDepth = limits.maximumInLawTraversalDepth - 1 }, privileged);
                if (partnerToTarget.Classification is KinshipClassification.Parent or KinshipClassification.BiologicalParent or KinshipClassification.AdoptiveParent)
                {
                    return Result(from, to, KinshipClassification.ParentInLaw, KinshipLineageKind.Mixed);
                }

                if (partnerToTarget.Classification is KinshipClassification.Child or KinshipClassification.BiologicalChild or KinshipClassification.AdoptiveChild)
                {
                    return Result(from, to, KinshipClassification.ChildInLaw, KinshipLineageKind.Mixed);
                }

                if (partnerToTarget.Classification is KinshipClassification.FullSibling or KinshipClassification.HalfSibling or KinshipClassification.AdoptiveSibling)
                {
                    return Result(from, to, KinshipClassification.SiblingInLaw, KinshipLineageKind.Mixed);
                }
            }

            return Result(from, to, KinshipClassification.Unrelated, KinshipLineageKind.None);
        }

        private AncestorPath FindAncestorPath(string descendant, string ancestor, KinshipTraversalLimits limits, bool privileged)
        {
            Dictionary<string, AncestorPath> map = AncestorMap(descendant, limits, privileged);
            return map.TryGetValue(ancestor, out AncestorPath path) ? path : AncestorPath.NotFound;
        }

        private Dictionary<string, AncestorPath> AncestorMap(string person, KinshipTraversalLimits limits, bool privileged)
        {
            Dictionary<string, AncestorPath> result = new Dictionary<string, AncestorPath>(StringComparer.Ordinal);
            Queue<AncestorPath> queue = new Queue<AncestorPath>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { person };
            foreach (RelationshipSnapshot parentRecord in GetParents(person, null, true, privileged))
            {
                string parent = PersonByRole(parentRecord, IsGuardianDefinition(parentRecord.RelationshipDefinitionId) ? "guardian" : "parent");
                KinshipLineageKind lineage = Lineage(ParentageKindForDefinition(parentRecord.RelationshipDefinitionId));
                queue.Enqueue(new AncestorPath(parent, 1, Step(parentRecord, person, IsGuardianDefinition(parentRecord.RelationshipDefinitionId) ? "dependent" : "child", parent, IsGuardianDefinition(parentRecord.RelationshipDefinitionId) ? "guardian" : "parent"), lineage, false, Array.Empty<string>()));
            }

            while (queue.Count > 0)
            {
                AncestorPath current = queue.Dequeue();
                if (!visited.Add(current.PersonId))
                {
                    continue;
                }

                result[current.PersonId] = current;
                if (current.Depth >= limits.maximumAncestorDepth || visited.Count >= limits.maximumVisitedPersons)
                {
                    result[current.PersonId] = current.WithTruncated("Ancestor traversal limit reached.");
                    continue;
                }

                foreach (RelationshipSnapshot parentRecord in GetParents(current.PersonId, null, true, privileged))
                {
                    string parent = PersonByRole(parentRecord, IsGuardianDefinition(parentRecord.RelationshipDefinitionId) ? "guardian" : "parent");
                    KinshipLineageKind lineage = Combine(current.Lineage, Lineage(ParentageKindForDefinition(parentRecord.RelationshipDefinitionId)));
                    queue.Enqueue(new AncestorPath(parent, current.Depth + 1, current.Steps.Concat(Step(parentRecord, current.PersonId, IsGuardianDefinition(parentRecord.RelationshipDefinitionId) ? "dependent" : "child", parent, IsGuardianDefinition(parentRecord.RelationshipDefinitionId) ? "guardian" : "parent")), lineage, current.Truncated, current.Diagnostics));
                }
            }

            return result;
        }

        private IReadOnlyList<RelationshipSnapshot> ParentageRecords(bool activeOnly, bool privileged)
        {
            return (relationships == null ? Array.Empty<RelationshipSnapshot>() : relationships.Snapshots)
                .Where(record => IsParentageDefinition(record.RelationshipDefinitionId) && (!activeOnly || record.Status == RelationshipLifecycleStatus.Active) && IsVisible(record, privileged))
                .OrderBy(record => record.RelationshipDefinitionId, StringComparer.Ordinal)
                .ThenBy(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "dependent" : "child"), StringComparer.Ordinal)
                .ThenBy(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "guardian" : "parent"), StringComparer.Ordinal)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal)
                .ToArray();
        }

        private IReadOnlyList<RelationshipSnapshot> ActiveOrHistoricalPartnerRecords(bool includeFormer)
        {
            string[] ids =
            {
                PrototypeRelationshipDefinitionFactory.CourtshipPartnerRelationshipId,
                PrototypeRelationshipDefinitionFactory.EngagedPartnerRelationshipId,
                PrototypeRelationshipDefinitionFactory.SpouseRelationshipId,
                PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId,
                PrototypeRelationshipDefinitionFactory.SeparatedPartnerRelationshipId,
                PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId
            };
            return (relationships == null ? Array.Empty<RelationshipSnapshot>() : relationships.Snapshots)
                .Where(record => ids.Contains(record.RelationshipDefinitionId, StringComparer.Ordinal)
                    && (includeFormer || record.Status == RelationshipLifecycleStatus.Active)
                    && (includeFormer || record.RelationshipDefinitionId != PrototypeRelationshipDefinitionFactory.FormerRomanticPartnerRelationshipId))
                .OrderBy(record => record.RelationshipDefinitionId, StringComparer.Ordinal)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal)
                .ToArray();
        }

        private bool IsVisible(RelationshipSnapshot record, bool privileged)
        {
            if (privileged)
            {
                return true;
            }

            FamilyVisibility visibility = VisibilityFromTags(record.Tags);
            return visibility == FamilyVisibility.Public || visibility == FamilyVisibility.FamilyKnown || visibility == FamilyVisibility.ParticipantKnown;
        }

        private static FamilyVisibility VisibilityFromTags(IEnumerable<string> tags)
        {
            foreach (string tag in tags ?? Array.Empty<string>())
            {
                if (tag != null && tag.StartsWith("visibility:", StringComparison.Ordinal) && Enum.TryParse(tag.Substring("visibility:".Length), out FamilyVisibility visibility))
                {
                    return visibility;
                }
            }

            return FamilyVisibility.Public;
        }

        private static string[] ParentageTags(ParentageKind kind, ParentageEvidenceStatus evidence, FamilyVisibility visibility)
        {
            return new[] { "family", "parentage", $"parentage:{kind}", $"evidence:{evidence}", $"visibility:{visibility}" };
        }

        private static string ParentageDefinitionId(ParentageKind kind)
        {
            return kind switch
            {
                ParentageKind.Biological => PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId,
                ParentageKind.Adoptive => PrototypeRelationshipDefinitionFactory.AdoptiveParentChildRelationshipId,
                ParentageKind.Legal => PrototypeRelationshipDefinitionFactory.LegalGuardianDependentRelationshipId,
                ParentageKind.Foster => PrototypeRelationshipDefinitionFactory.FosterGuardianDependentRelationshipId,
                _ => PrototypeRelationshipDefinitionFactory.ParentChildRelationshipId
            };
        }

        private static ParentageKind ParentageKindForDefinition(string definitionId)
        {
            return definitionId switch
            {
                PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId => ParentageKind.Biological,
                PrototypeRelationshipDefinitionFactory.AdoptiveParentChildRelationshipId => ParentageKind.Adoptive,
                PrototypeRelationshipDefinitionFactory.LegalGuardianDependentRelationshipId => ParentageKind.Legal,
                PrototypeRelationshipDefinitionFactory.FosterGuardianDependentRelationshipId => ParentageKind.Foster,
                _ => ParentageKind.Unknown
            };
        }

        private static bool IsParentageDefinition(string definitionId)
        {
            return definitionId == PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId
                || definitionId == PrototypeRelationshipDefinitionFactory.AdoptiveParentChildRelationshipId
                || definitionId == PrototypeRelationshipDefinitionFactory.LegalGuardianDependentRelationshipId
                || definitionId == PrototypeRelationshipDefinitionFactory.FosterGuardianDependentRelationshipId
                || definitionId == PrototypeRelationshipDefinitionFactory.ParentChildRelationshipId;
        }

        private static bool IsGuardianDefinition(string definitionId)
        {
            return definitionId == PrototypeRelationshipDefinitionFactory.LegalGuardianDependentRelationshipId
                || definitionId == PrototypeRelationshipDefinitionFactory.FosterGuardianDependentRelationshipId;
        }

        private static bool IsGuardianKind(ParentageKind kind)
        {
            return kind == ParentageKind.Legal || kind == ParentageKind.Foster;
        }

        private static bool IsDownlineKinship(KinshipClassification classification)
        {
            return classification is KinshipClassification.Child
                or KinshipClassification.BiologicalChild
                or KinshipClassification.AdoptiveChild
                or KinshipClassification.Dependent
                or KinshipClassification.Grandchild
                or KinshipClassification.Descendant;
        }

        private static KinshipLineageKind Lineage(ParentageKind kind)
        {
            return kind switch
            {
                ParentageKind.Biological => KinshipLineageKind.Biological,
                ParentageKind.Adoptive => KinshipLineageKind.Adoptive,
                ParentageKind.Legal => KinshipLineageKind.Legal,
                ParentageKind.Foster => KinshipLineageKind.Foster,
                _ => KinshipLineageKind.Mixed
            };
        }

        private static KinshipLineageKind Combine(KinshipLineageKind first, KinshipLineageKind second)
        {
            return first == second ? first : first == KinshipLineageKind.None ? second : second == KinshipLineageKind.None ? first : KinshipLineageKind.Mixed;
        }

        private static IReadOnlyList<KinshipPathStep> Step(RelationshipSnapshot record, string from, string fromRole, string to, string toRole)
        {
            return new[] { new KinshipPathStep(record.RecordId, record.RelationshipDefinitionId, from, fromRole, to, toRole, Lineage(ParentageKindForDefinition(record.RelationshipDefinitionId))) };
        }

        private static IReadOnlyList<KinshipPathStep> ReverseSteps(IEnumerable<KinshipPathStep> steps)
        {
            return (steps ?? Array.Empty<KinshipPathStep>()).Reverse().Select(step => new KinshipPathStep(step.RelationshipRecordId, step.RelationshipDefinitionId, step.ToPersonId, step.ToRoleId, step.FromPersonId, step.FromRoleId, step.LineageKind)).ToArray();
        }

        private static KinshipPathResult Result(string from, string to, KinshipClassification classification, KinshipLineageKind lineage, string diagnostic = "")
        {
            return new KinshipPathResult(from, to, classification, lineage, Array.Empty<KinshipPathStep>(), string.Empty, 0, 0, false, string.IsNullOrWhiteSpace(diagnostic) ? Array.Empty<string>() : new[] { diagnostic });
        }

        private static int SharedParents(IEnumerable<RelationshipSnapshot> first, IEnumerable<RelationshipSnapshot> second)
        {
            HashSet<string> left = new HashSet<string>((first ?? Array.Empty<RelationshipSnapshot>()).Select(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "guardian" : "parent")), StringComparer.Ordinal);
            return (second ?? Array.Empty<RelationshipSnapshot>()).Select(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "guardian" : "parent")).Count(parent => left.Contains(parent));
        }

        private string[] ParentPeople(string child, bool privileged)
        {
            return GetParents(child, null, true, privileged).Select(record => PersonByRole(record, IsGuardianDefinition(record.RelationshipDefinitionId) ? "guardian" : "parent")).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static string OtherParentagePerson(RelationshipSnapshot record, string person)
        {
            return OtherPerson(record, person);
        }

        private static string OtherPerson(RelationshipSnapshot record, string person)
        {
            return (record?.Participants ?? Array.Empty<RelationshipEndpointData>()).Select(endpoint => endpoint.personId).FirstOrDefault(id => !string.Equals(id, person, StringComparison.Ordinal)) ?? string.Empty;
        }

        private static string PersonByRole(RelationshipSnapshot record, string role)
        {
            return (record?.Participants ?? Array.Empty<RelationshipEndpointData>()).FirstOrDefault(endpoint => string.Equals(endpoint.roleId, role, StringComparison.Ordinal))?.personId ?? string.Empty;
        }

        private bool TryGetPolicy(string policyId, out RomanticEligibilityPolicyDefinition policy)
        {
            policy = null;
            return registry != null && !string.IsNullOrWhiteSpace(policyId) && registry.TryGet(policyId.Trim(), out policy);
        }

        private bool TryGetHouseholdDefinition(string definitionId, out HouseholdDefinition definition, out string failure)
        {
            definition = null;
            if (registry == null)
            {
                failure = "Definition registry is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definitionId) || !registry.TryGet(definitionId.Trim(), out definition))
            {
                failure = $"Household Definition '{definitionId}' is missing.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private bool HasDefinition<T>(string id) where T : class, IGameDefinition
        {
            return registry != null && !string.IsNullOrWhiteSpace(id) && registry.TryGet(id, out T _);
        }

        private bool ValidatePerson(string personId, out string failure)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                failure = "Person ID is required.";
                return false;
            }

            if (knownPersonIds.Count > 0 && !knownPersonIds.Contains(personId.Trim()))
            {
                failure = $"Person '{personId}' is unknown.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private HouseholdSnapshot Snapshot(string householdId)
        {
            if (string.IsNullOrWhiteSpace(householdId) || !householdsById.TryGetValue(householdId.Trim(), out HouseholdRecordData household))
            {
                return null;
            }

            return new HouseholdSnapshot(household, OrderedMemberships(membershipsById.Values.Where(membership => string.Equals(membership.householdId, household.householdId, StringComparison.Ordinal))));
        }

        private IEnumerable<HouseholdMembershipData> ActiveMemberships(string householdId)
        {
            return membershipsById.Values.Where(membership => string.Equals(membership.householdId, householdId, StringComparison.Ordinal) && membership.status == HouseholdMembershipStatus.Active);
        }

        private bool TryGetActiveMembership(string householdId, string personId, out HouseholdMembershipData membership)
        {
            membership = membershipsById.Values.FirstOrDefault(item => item.status == HouseholdMembershipStatus.Active && string.Equals(item.householdId, householdId, StringComparison.Ordinal) && string.Equals(item.personId, personId, StringComparison.Ordinal));
            return membership != null;
        }

        private static IEnumerable<HouseholdRecordData> OrderedHouseholds(IEnumerable<HouseholdRecordData> households)
        {
            return (households ?? Array.Empty<HouseholdRecordData>()).OrderBy(item => item.worldId, StringComparer.Ordinal).ThenBy(item => item.householdId, StringComparer.Ordinal);
        }

        private static IEnumerable<HouseholdMembershipData> OrderedMemberships(IEnumerable<HouseholdMembershipData> memberships)
        {
            return (memberships ?? Array.Empty<HouseholdMembershipData>()).OrderBy(item => item.householdId, StringComparer.Ordinal).ThenBy(item => item.personId, StringComparer.Ordinal).ThenBy(item => item.membershipId, StringComparer.Ordinal);
        }

        private void RestoreInternal(FamilyRelationshipRuntimeSaveData saveData)
        {
            householdsById.Clear();
            membershipsById.Clear();
            processedTransactionIds.Clear();
            foreach (HouseholdRecordData household in saveData?.households ?? new List<HouseholdRecordData>())
            {
                HouseholdRecordData clone = household.Clone();
                householdsById[clone.householdId] = clone;
            }

            foreach (HouseholdMembershipData membership in saveData?.memberships ?? new List<HouseholdMembershipData>())
            {
                HouseholdMembershipData clone = membership.Clone();
                membershipsById[clone.membershipId] = clone;
            }

            foreach (string transactionId in saveData?.processedTransactionIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    processedTransactionIds.Add(transactionId.Trim());
                }
            }

            revision = saveData?.revision ?? 0L;
        }

        private static bool ValidateAll(FamilyRelationshipRuntimeSaveData saveData, DefinitionRegistry registry, HashSet<string> knownPersons, string expectedWorldId, out string failure)
        {
            failure = string.Empty;
            if (saveData.schemaVersion != FamilyRelationshipRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Family Relationship save schema version {saveData.schemaVersion}.";
                return false;
            }

            if (registry == null)
            {
                failure = "Family Relationship runtime requires a definition registry.";
                return false;
            }

            HashSet<string> householdIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (HouseholdRecordData household in saveData.households ?? new List<HouseholdRecordData>())
            {
                if (household == null || string.IsNullOrWhiteSpace(household.householdId) || !householdIds.Add(household.householdId))
                {
                    failure = $"Household save contains duplicate or empty household ID '{household?.householdId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(household.householdDefinitionId) || !registry.TryGet(household.householdDefinitionId, out HouseholdDefinition _))
                {
                    failure = $"Household '{household.householdId}' references missing Household Definition '{household.householdDefinitionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(expectedWorldId) && !string.IsNullOrWhiteSpace(household.worldId) && !string.Equals(household.worldId, expectedWorldId, StringComparison.Ordinal))
                {
                    failure = $"Household '{household.householdId}' belongs to world '{household.worldId}', not '{expectedWorldId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(HouseholdLifecycleStatus), household.status))
                {
                    failure = $"Household '{household.householdId}' has invalid lifecycle status.";
                    return false;
                }
            }

            HashSet<string> membershipIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activePairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (HouseholdMembershipData membership in saveData.memberships ?? new List<HouseholdMembershipData>())
            {
                if (membership == null || string.IsNullOrWhiteSpace(membership.membershipId) || !membershipIds.Add(membership.membershipId))
                {
                    failure = $"Household save contains duplicate or empty membership ID '{membership?.membershipId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(membership.householdId) || !householdIds.Contains(membership.householdId))
                {
                    failure = $"Household membership '{membership.membershipId}' references missing household '{membership.householdId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(membership.personId) || (knownPersons != null && knownPersons.Count > 0 && !knownPersons.Contains(membership.personId)))
                {
                    failure = $"Household membership '{membership.membershipId}' references unknown Person '{membership.personId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(HouseholdRole), membership.role) || !Enum.IsDefined(typeof(HouseholdMembershipStatus), membership.status))
                {
                    failure = $"Household membership '{membership.membershipId}' has invalid role or lifecycle.";
                    return false;
                }

                if (membership.status == HouseholdMembershipStatus.Active && !activePairs.Add($"{membership.householdId}|{membership.personId}"))
                {
                    failure = $"Household '{membership.householdId}' has duplicate active membership for Person '{membership.personId}'.";
                    return false;
                }
            }

            foreach (HouseholdRecordData household in saveData.households ?? new List<HouseholdRecordData>())
            {
                if (!registry.TryGet(household.householdDefinitionId, out HouseholdDefinition definition))
                {
                    continue;
                }

                HouseholdMembershipData[] active = (saveData.memberships ?? new List<HouseholdMembershipData>()).Where(item => item.householdId == household.householdId && item.status == HouseholdMembershipStatus.Active).ToArray();
                if (household.status == HouseholdLifecycleStatus.Active && (active.Length < definition.MinimumActiveMembers || active.Length > definition.MaximumActiveMembers))
                {
                    failure = $"Household '{household.householdId}' active member count is outside definition limits.";
                    return false;
                }

                int heads = active.Count(item => item.role == HouseholdRole.Head || item.role == HouseholdRole.CoHead);
                if (household.status == HouseholdLifecycleStatus.Active && (heads < definition.MinimumHeads || heads > definition.MaximumHeads))
                {
                    failure = $"Household '{household.householdId}' active head count is outside definition limits.";
                    return false;
                }
            }

            return true;
        }

        private HouseholdMutationResult HouseholdFailure(HouseholdOperationStatus status, string message, long before)
        {
            return new HouseholdMutationResult(false, status, message, null, false, false, before, before);
        }

        private long RelationshipRevision => relationships?.Revision ?? 0L;

        private static string ParentageRecordId(string definitionId, string parent, string child)
        {
            return $"relationship-record.{definitionId}.{parent}.{child}";
        }

        private static string RomanticRecordId(string definitionId, string first, string second, RomanticTransitionKind transition)
        {
            string[] ordered = new[] { first ?? string.Empty, second ?? string.Empty }.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return $"relationship-record.{definitionId}.{ordered[0]}.{ordered[1]}.{transition}";
        }

        private static string MembershipId(string householdId, string personId, double worldTime)
        {
            return $"household-membership.{householdId}.{personId}.{worldTime:0.###}";
        }

        private sealed class AncestorPath
        {
            public static readonly AncestorPath NotFound = new AncestorPath(string.Empty, 0, Array.Empty<KinshipPathStep>(), KinshipLineageKind.None, false, Array.Empty<string>());

            public AncestorPath(string personId, int depth, IEnumerable<KinshipPathStep> steps, KinshipLineageKind lineage, bool truncated, IEnumerable<string> diagnostics)
            {
                PersonId = personId ?? string.Empty;
                Depth = depth;
                Steps = (steps ?? Array.Empty<KinshipPathStep>()).ToArray();
                Lineage = lineage;
                Truncated = truncated;
                Diagnostics = (diagnostics ?? Array.Empty<string>()).ToArray();
            }

            public string PersonId { get; }
            public int Depth { get; }
            public IReadOnlyList<KinshipPathStep> Steps { get; }
            public KinshipLineageKind Lineage { get; }
            public bool Truncated { get; }
            public IReadOnlyList<string> Diagnostics { get; }
            public bool Found => !string.IsNullOrWhiteSpace(PersonId);

            public AncestorPath WithTruncated(string diagnostic)
            {
                return new AncestorPath(PersonId, Depth, Steps, Lineage, true, Diagnostics.Concat(new[] { diagnostic }));
            }
        }
    }

    internal static class RomanticEligibilityRequestExtensions
    {
        public static RomanticConsentKind ConsentKind(this RomanticEligibilityRequest request)
        {
            return request?.consentKind ?? RomanticConsentKind.None;
        }
    }
}
