using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Social.Relationships;

namespace UnityIsekaiGame.Social.Family
{
    [Serializable]
    public sealed class FamilyParentageRequest
    {
        public string transactionId;
        public string recordId;
        public string parentPersonId;
        public string childPersonId;
        public ParentageKind parentageKind = ParentageKind.Biological;
        public ParentageEvidenceStatus evidenceStatus = ParentageEvidenceStatus.Confirmed;
        public FamilyVisibility visibility = FamilyVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public double worldTime;
        public bool preview;
    }

    public sealed class FamilyRelationshipMutationResult
    {
        private FamilyRelationshipMutationResult(bool succeeded, RomanticEligibilityStatus status, RelationshipSnapshot relationship, string message, bool preview, bool duplicate, long revisionBefore, long revisionAfter)
        {
            Succeeded = succeeded;
            Status = status;
            Relationship = relationship;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public RomanticEligibilityStatus Status { get; }
        public RelationshipSnapshot Relationship { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }

        public static FamilyRelationshipMutationResult Success(RomanticEligibilityStatus status, RelationshipSnapshot relationship, string message, bool preview, bool duplicate, long before, long after)
        {
            return new FamilyRelationshipMutationResult(true, status, relationship, message, preview, duplicate, before, after);
        }

        public static FamilyRelationshipMutationResult Failure(RomanticEligibilityStatus status, string message, long before)
        {
            return new FamilyRelationshipMutationResult(false, status, null, message, false, false, before, before);
        }
    }

    [Serializable]
    public sealed class KinshipTraversalLimits
    {
        public int maximumAncestorDepth = 8;
        public int maximumDescendantDepth = 8;
        public int maximumVisitedPersons = 128;
        public int maximumReturnedPaths = 8;
        public int maximumCousinDegree = 4;
        public int maximumRemovalCount = 4;
        public int maximumInLawTraversalDepth = 2;

        public KinshipTraversalLimits Clone()
        {
            return new KinshipTraversalLimits
            {
                maximumAncestorDepth = Math.Max(0, maximumAncestorDepth),
                maximumDescendantDepth = Math.Max(0, maximumDescendantDepth),
                maximumVisitedPersons = Math.Max(1, maximumVisitedPersons),
                maximumReturnedPaths = Math.Max(1, maximumReturnedPaths),
                maximumCousinDegree = Math.Max(0, maximumCousinDegree),
                maximumRemovalCount = Math.Max(0, maximumRemovalCount),
                maximumInLawTraversalDepth = Math.Max(0, maximumInLawTraversalDepth)
            };
        }

        public static KinshipTraversalLimits Default => new KinshipTraversalLimits();
    }

    public sealed class KinshipPathStep
    {
        public KinshipPathStep(string relationshipRecordId, string relationshipDefinitionId, string fromPersonId, string fromRoleId, string toPersonId, string toRoleId, KinshipLineageKind lineageKind)
        {
            RelationshipRecordId = relationshipRecordId ?? string.Empty;
            RelationshipDefinitionId = relationshipDefinitionId ?? string.Empty;
            FromPersonId = fromPersonId ?? string.Empty;
            FromRoleId = fromRoleId ?? string.Empty;
            ToPersonId = toPersonId ?? string.Empty;
            ToRoleId = toRoleId ?? string.Empty;
            LineageKind = lineageKind;
        }

        public string RelationshipRecordId { get; }
        public string RelationshipDefinitionId { get; }
        public string FromPersonId { get; }
        public string FromRoleId { get; }
        public string ToPersonId { get; }
        public string ToRoleId { get; }
        public KinshipLineageKind LineageKind { get; }
    }

    public sealed class KinshipPathResult
    {
        public KinshipPathResult(string fromPersonId, string toPersonId, KinshipClassification classification, KinshipLineageKind lineageKind, IEnumerable<KinshipPathStep> steps, string commonAncestorPersonId, int cousinDegree, int removalCount, bool truncated, IEnumerable<string> diagnostics)
        {
            FromPersonId = fromPersonId ?? string.Empty;
            ToPersonId = toPersonId ?? string.Empty;
            Classification = classification;
            LineageKind = lineageKind;
            Steps = (steps ?? Array.Empty<KinshipPathStep>()).ToArray();
            CommonAncestorPersonId = commonAncestorPersonId ?? string.Empty;
            CousinDegree = cousinDegree;
            RemovalCount = removalCount;
            Truncated = truncated;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public string FromPersonId { get; }
        public string ToPersonId { get; }
        public KinshipClassification Classification { get; }
        public KinshipLineageKind LineageKind { get; }
        public IReadOnlyList<KinshipPathStep> Steps { get; }
        public int PathLength => Steps.Count;
        public string CommonAncestorPersonId { get; }
        public int CousinDegree { get; }
        public int RemovalCount { get; }
        public bool Truncated { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsProhibitedRomanceKinship => Classification == KinshipClassification.Parent
            || Classification == KinshipClassification.Child
            || Classification == KinshipClassification.BiologicalParent
            || Classification == KinshipClassification.BiologicalChild
            || Classification == KinshipClassification.AdoptiveParent
            || Classification == KinshipClassification.AdoptiveChild
            || Classification == KinshipClassification.FullSibling
            || Classification == KinshipClassification.HalfSibling
            || Classification == KinshipClassification.AdoptiveSibling
            || Classification == KinshipClassification.Ancestor
            || Classification == KinshipClassification.Descendant;
    }

    public sealed class FamilyTreeSnapshot
    {
        public FamilyTreeSnapshot(string focalPersonId, KinshipTraversalLimits limits, IEnumerable<RelationshipSnapshot> relationships, IEnumerable<KinshipPathResult> kinships, long sourceRelationshipRevision, bool privileged, bool truncated, IEnumerable<string> diagnostics)
        {
            FocalPersonId = focalPersonId ?? string.Empty;
            Limits = (limits ?? KinshipTraversalLimits.Default).Clone();
            Relationships = (relationships ?? Array.Empty<RelationshipSnapshot>()).Select(item => new RelationshipSnapshot(item?.Data)).ToArray();
            Kinships = (kinships ?? Array.Empty<KinshipPathResult>()).ToArray();
            SourceRelationshipRevision = sourceRelationshipRevision;
            Privileged = privileged;
            Truncated = truncated;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public string FocalPersonId { get; }
        public KinshipTraversalLimits Limits { get; }
        public IReadOnlyList<RelationshipSnapshot> Relationships { get; }
        public IReadOnlyList<KinshipPathResult> Kinships { get; }
        public long SourceRelationshipRevision { get; }
        public bool Privileged { get; }
        public bool Truncated { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    public sealed class RomanticEligibilityRequest
    {
        public string actorPersonId;
        public string targetPersonId;
        public string policyDefinitionId;
        public RomanticTransitionKind transitionKind;
        public RomanticConsentKind consentKind;
        public string consentInteractionId;
        public bool preview;
    }

    public sealed class RomanticEligibilityResult
    {
        public RomanticEligibilityResult(bool eligible, RomanticEligibilityStatus status, string actorPersonId, string targetPersonId, string policyDefinitionId, KinshipPathResult kinship, bool actorAdult, bool targetAdult, bool guardianDependent, bool existingExclusiveConflict, bool consentAccepted, int actorAttraction, int targetAttraction, int actorAffection, int targetAffection, IEnumerable<string> failureReasons, bool preview = false)
        {
            Eligible = eligible;
            Status = preview && eligible ? RomanticEligibilityStatus.Preview : status;
            ActorPersonId = actorPersonId ?? string.Empty;
            TargetPersonId = targetPersonId ?? string.Empty;
            PolicyDefinitionId = policyDefinitionId ?? string.Empty;
            Kinship = kinship;
            ActorAdult = actorAdult;
            TargetAdult = targetAdult;
            GuardianDependent = guardianDependent;
            ExistingExclusiveConflict = existingExclusiveConflict;
            ConsentAccepted = consentAccepted;
            ActorAttraction = actorAttraction;
            TargetAttraction = targetAttraction;
            ActorAffection = actorAffection;
            TargetAffection = targetAffection;
            FailureReasons = (failureReasons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Preview = preview;
        }

        public bool Eligible { get; }
        public RomanticEligibilityStatus Status { get; }
        public string ActorPersonId { get; }
        public string TargetPersonId { get; }
        public string PolicyDefinitionId { get; }
        public KinshipPathResult Kinship { get; }
        public bool ActorAdult { get; }
        public bool TargetAdult { get; }
        public bool GuardianDependent { get; }
        public bool ExistingExclusiveConflict { get; }
        public bool ConsentAccepted { get; }
        public int ActorAttraction { get; }
        public int TargetAttraction { get; }
        public int ActorAffection { get; }
        public int TargetAffection { get; }
        public IReadOnlyList<string> FailureReasons { get; }
        public bool Preview { get; }
    }

    public sealed class RomanticTransitionRequest
    {
        public string transactionId;
        public string relationshipRecordId;
        public string actorPersonId;
        public string targetPersonId;
        public string policyDefinitionId;
        public RomanticTransitionKind transitionKind;
        public RomanticConsentKind consentKind;
        public string consentInteractionId;
        public string currentRelationshipRecordId;
        public double worldTime;
        public bool preview;
    }

    public sealed class RomanticTransitionResult
    {
        public RomanticTransitionResult(bool succeeded, RomanticEligibilityStatus status, RelationshipSnapshot createdRelationship, RelationshipSnapshot endedRelationship, RomanticEligibilityResult eligibility, string message, bool preview, bool duplicate, long revisionBefore, long revisionAfter)
        {
            Succeeded = succeeded;
            Status = status;
            CreatedRelationship = createdRelationship;
            EndedRelationship = endedRelationship;
            Eligibility = eligibility;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public RomanticEligibilityStatus Status { get; }
        public RelationshipSnapshot CreatedRelationship { get; }
        public RelationshipSnapshot EndedRelationship { get; }
        public RomanticEligibilityResult Eligibility { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
    }

    [Serializable]
    public sealed class HouseholdMembershipData
    {
        public string membershipId;
        public string householdId;
        public string personId;
        public HouseholdRole role;
        public HouseholdMembershipStatus status = HouseholdMembershipStatus.Active;
        public double joinWorldTime;
        public double leaveWorldTime = -1d;
        public string sourceEventId;
        public string sourceInteractionId;
        public long revision = 1L;

        public HouseholdMembershipData Clone()
        {
            return new HouseholdMembershipData
            {
                membershipId = membershipId ?? string.Empty,
                householdId = householdId ?? string.Empty,
                personId = personId ?? string.Empty,
                role = role,
                status = status,
                joinWorldTime = joinWorldTime,
                leaveWorldTime = leaveWorldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceInteractionId = sourceInteractionId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class HouseholdRecordData
    {
        public string householdId;
        public string householdDefinitionId;
        public HouseholdLifecycleStatus status = HouseholdLifecycleStatus.Active;
        public string worldId;
        public string residencePlaceId;
        public string propertyReferenceId;
        public double createdWorldTime;
        public double endedWorldTime = -1d;
        public string sourceEventId;
        public string accessPolicyId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public HouseholdRecordData Clone()
        {
            return new HouseholdRecordData
            {
                householdId = householdId ?? string.Empty,
                householdDefinitionId = householdDefinitionId ?? string.Empty,
                status = status,
                worldId = worldId ?? string.Empty,
                residencePlaceId = residencePlaceId ?? string.Empty,
                propertyReferenceId = propertyReferenceId ?? string.Empty,
                createdWorldTime = createdWorldTime,
                endedWorldTime = endedWorldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                tags = Clean(tags),
                revision = revision
            };
        }

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class FamilyRelationshipRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<HouseholdRecordData> households = new List<HouseholdRecordData>();
        public List<HouseholdMembershipData> memberships = new List<HouseholdMembershipData>();
        public List<string> processedTransactionIds = new List<string>();

        public FamilyRelationshipRuntimeSaveData Clone()
        {
            return new FamilyRelationshipRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                households = households == null ? new List<HouseholdRecordData>() : households.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                memberships = memberships == null ? new List<HouseholdMembershipData>() : memberships.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedTransactionIds = (processedTransactionIds ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()
            };
        }
    }

    public sealed class HouseholdSnapshot
    {
        public HouseholdSnapshot(HouseholdRecordData household, IEnumerable<HouseholdMembershipData> memberships)
        {
            Data = household?.Clone() ?? new HouseholdRecordData();
            Memberships = (memberships ?? Array.Empty<HouseholdMembershipData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.personId, StringComparer.Ordinal).ThenBy(item => item.membershipId, StringComparer.Ordinal).ToArray();
        }

        public HouseholdRecordData Data { get; }
        public string HouseholdId => Data.householdId ?? string.Empty;
        public string HouseholdDefinitionId => Data.householdDefinitionId ?? string.Empty;
        public HouseholdLifecycleStatus Status => Data.status;
        public string WorldId => Data.worldId ?? string.Empty;
        public IReadOnlyList<HouseholdMembershipData> Memberships { get; }
        public IReadOnlyList<HouseholdMembershipData> ActiveMemberships => Memberships.Where(item => item.status == HouseholdMembershipStatus.Active).ToArray();
    }

    public sealed class HouseholdMutationRequest
    {
        public string transactionId;
        public string householdId;
        public string householdDefinitionId;
        public string personId;
        public HouseholdRole role = HouseholdRole.AdultMember;
        public string membershipId;
        public string residencePlaceId;
        public string propertyReferenceId;
        public string sourceEventId;
        public string sourceInteractionId;
        public double worldTime;
        public bool preview;
    }

    public sealed class HouseholdTransferRequest
    {
        public string transactionId;
        public string sourceHouseholdId;
        public string targetHouseholdId;
        public string targetHouseholdDefinitionId;
        public string[] memberPersonIds = Array.Empty<string>();
        public string residencePlaceId;
        public string propertyReferenceId;
        public double worldTime;
        public bool preview;
    }

    public sealed class HouseholdMutationResult
    {
        public HouseholdMutationResult(bool succeeded, HouseholdOperationStatus status, string message, HouseholdSnapshot household, bool preview, bool duplicate, long revisionBefore, long revisionAfter)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            Household = household;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
        }

        public bool Succeeded { get; }
        public HouseholdOperationStatus Status { get; }
        public string Message { get; }
        public HouseholdSnapshot Household { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
    }
}
