using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Organizations
{
    [Serializable]
    public sealed class OrganizationMembershipRecordData
    {
        public string membershipId;
        public string organizationId;
        public string personId;
        public string membershipDefinitionId;
        public OrganizationMembershipStatus status = OrganizationMembershipStatus.Active;
        public OrganizationMembershipSourceKind sourceKind = OrganizationMembershipSourceKind.WorldSetup;
        public double appliedWorldTime = -1d;
        public double invitedWorldTime = -1d;
        public double acceptedWorldTime = -1d;
        public double activeStartWorldTime = -1d;
        public double suspendedWorldTime = -1d;
        public double endWorldTime = -1d;
        public string parentMembershipId;
        public string branchOrganizationId;
        public string employmentId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string accessPolicyId;
        public string[] rankAssignmentIds = Array.Empty<string>();
        public string[] officeAssignmentIds = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public bool IsActive => status == OrganizationMembershipStatus.Active || status == OrganizationMembershipStatus.Provisional;
        public bool IsPending => status == OrganizationMembershipStatus.Applied || status == OrganizationMembershipStatus.Invited || status == OrganizationMembershipStatus.PendingAcceptance;
        public bool IsEnded => status == OrganizationMembershipStatus.Resigned || status == OrganizationMembershipStatus.Removed || status == OrganizationMembershipStatus.Expelled || status == OrganizationMembershipStatus.Expired || status == OrganizationMembershipStatus.Historical;

        public OrganizationMembershipRecordData Clone()
        {
            return new OrganizationMembershipRecordData
            {
                membershipId = membershipId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                personId = personId ?? string.Empty,
                membershipDefinitionId = membershipDefinitionId ?? string.Empty,
                status = status,
                sourceKind = sourceKind,
                appliedWorldTime = appliedWorldTime,
                invitedWorldTime = invitedWorldTime,
                acceptedWorldTime = acceptedWorldTime,
                activeStartWorldTime = activeStartWorldTime,
                suspendedWorldTime = suspendedWorldTime,
                endWorldTime = endWorldTime,
                parentMembershipId = parentMembershipId ?? string.Empty,
                branchOrganizationId = branchOrganizationId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                visibility = visibility,
                accessPolicyId = accessPolicyId ?? string.Empty,
                rankAssignmentIds = OrganizationModelUtility.Clean(rankAssignmentIds),
                officeAssignmentIds = OrganizationModelUtility.Clean(officeAssignmentIds),
                tags = OrganizationModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationRankAssignmentRecordData
    {
        public string rankAssignmentId;
        public string membershipId;
        public string organizationId;
        public string personId;
        public string rankTrackDefinitionId;
        public string rankDefinitionId;
        public OrganizationRankAssignmentState state = OrganizationRankAssignmentState.Active;
        public double assignedWorldTime;
        public double effectiveWorldTime;
        public double endWorldTime = -1d;
        public string assignedById;
        public string replacesRankAssignmentId;
        public string replacedByRankAssignmentId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string accessPolicyId;
        public long revision = 1L;

        public bool IsActive => state == OrganizationRankAssignmentState.Active;

        public OrganizationRankAssignmentRecordData Clone()
        {
            return new OrganizationRankAssignmentRecordData
            {
                rankAssignmentId = rankAssignmentId ?? string.Empty,
                membershipId = membershipId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                personId = personId ?? string.Empty,
                rankTrackDefinitionId = rankTrackDefinitionId ?? string.Empty,
                rankDefinitionId = rankDefinitionId ?? string.Empty,
                state = state,
                assignedWorldTime = assignedWorldTime,
                effectiveWorldTime = effectiveWorldTime,
                endWorldTime = endWorldTime,
                assignedById = assignedById ?? string.Empty,
                replacesRankAssignmentId = replacesRankAssignmentId ?? string.Empty,
                replacedByRankAssignmentId = replacedByRankAssignmentId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationOfficeRecordData
    {
        public string officeId;
        public string organizationId;
        public string officeDefinitionId;
        public string displayName;
        public OrganizationOfficeState state = OrganizationOfficeState.Active;
        public int maximumActiveHolders = 1;
        public bool vacancyAllowed = true;
        public double createdWorldTime;
        public double closedWorldTime = -1d;
        public string linkedPositionInstanceId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string accessPolicyId;
        public string[] officeAssignmentIds = Array.Empty<string>();
        public long revision = 1L;

        public bool IsActive => state == OrganizationOfficeState.Active;

        public OrganizationOfficeRecordData Clone()
        {
            return new OrganizationOfficeRecordData
            {
                officeId = officeId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                officeDefinitionId = officeDefinitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                state = state,
                maximumActiveHolders = Math.Max(1, maximumActiveHolders),
                vacancyAllowed = vacancyAllowed,
                createdWorldTime = createdWorldTime,
                closedWorldTime = closedWorldTime,
                linkedPositionInstanceId = linkedPositionInstanceId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                visibility = visibility,
                accessPolicyId = accessPolicyId ?? string.Empty,
                officeAssignmentIds = OrganizationModelUtility.Clean(officeAssignmentIds),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationOfficeAssignmentRecordData
    {
        public string officeAssignmentId;
        public string officeId;
        public string membershipId;
        public string organizationId;
        public string personId;
        public OrganizationOfficeAssignmentState state = OrganizationOfficeAssignmentState.Active;
        public bool acting;
        public double assignedWorldTime;
        public double effectiveStartWorldTime;
        public double expectedEndWorldTime = -1d;
        public double endWorldTime = -1d;
        public string appointedById;
        public string linkedEmploymentId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string accessPolicyId;
        public long revision = 1L;

        public bool IsActive => state == OrganizationOfficeAssignmentState.Active || state == OrganizationOfficeAssignmentState.Acting;

        public OrganizationOfficeAssignmentRecordData Clone()
        {
            return new OrganizationOfficeAssignmentRecordData
            {
                officeAssignmentId = officeAssignmentId ?? string.Empty,
                officeId = officeId ?? string.Empty,
                membershipId = membershipId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                personId = personId ?? string.Empty,
                state = state,
                acting = acting,
                assignedWorldTime = assignedWorldTime,
                effectiveStartWorldTime = effectiveStartWorldTime,
                expectedEndWorldTime = expectedEndWorldTime,
                endWorldTime = endWorldTime,
                appointedById = appointedById ?? string.Empty,
                linkedEmploymentId = linkedEmploymentId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationMembershipTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public long revision;

        public OrganizationMembershipTransactionRecordData Clone()
        {
            return new OrganizationMembershipTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OrganizationMembershipRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<OrganizationMembershipRecordData> memberships = new List<OrganizationMembershipRecordData>();
        public List<OrganizationRankAssignmentRecordData> ranks = new List<OrganizationRankAssignmentRecordData>();
        public List<OrganizationOfficeRecordData> offices = new List<OrganizationOfficeRecordData>();
        public List<OrganizationOfficeAssignmentRecordData> officeAssignments = new List<OrganizationOfficeAssignmentRecordData>();
        public List<OrganizationMembershipTransactionRecordData> transactions = new List<OrganizationMembershipTransactionRecordData>();

        public OrganizationMembershipRuntimeSaveData Clone()
        {
            return new OrganizationMembershipRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = Math.Max(0L, revision),
                memberships = memberships == null ? new List<OrganizationMembershipRecordData>() : memberships.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                ranks = ranks == null ? new List<OrganizationRankAssignmentRecordData>() : ranks.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                offices = offices == null ? new List<OrganizationOfficeRecordData>() : offices.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                officeAssignments = officeAssignments == null ? new List<OrganizationOfficeAssignmentRecordData>() : officeAssignments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transactions = transactions == null ? new List<OrganizationMembershipTransactionRecordData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class OrganizationMembershipRequest
    {
        public string membershipId;
        public string organizationId;
        public string personId;
        public string membershipDefinitionId;
        public OrganizationMembershipStatus targetStatus = OrganizationMembershipStatus.Active;
        public OrganizationMembershipSourceKind sourceKind = OrganizationMembershipSourceKind.Application;
        public double worldTime;
        public bool explicitConsent;
        public string parentMembershipId;
        public string branchOrganizationId;
        public string employmentId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string[] tags = Array.Empty<string>();
        public OrganizationMembershipEndingPolicy endingPolicy = OrganizationMembershipEndingPolicy.FailIfActiveAssignments;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationRankAssignmentRequest
    {
        public string rankAssignmentId;
        public string membershipId;
        public string rankDefinitionId;
        public double worldTime;
        public string assignedById;
        public bool replaceCurrentTrackRank = true;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationOfficeRequest
    {
        public string officeId;
        public string organizationId;
        public string officeDefinitionId;
        public string displayName;
        public int maximumActiveHolders;
        public bool vacancyAllowed = true;
        public double worldTime;
        public string linkedPositionInstanceId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationOfficeAssignmentRequest
    {
        public string officeAssignmentId;
        public string officeId;
        public string membershipId;
        public bool acting;
        public bool proposed;
        public double worldTime;
        public double expectedEndWorldTime = -1d;
        public string appointedById;
        public string linkedEmploymentId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationOfficeAssignmentTransitionRequest
    {
        public string transactionId;
        public string officeAssignmentId;
        public OrganizationOfficeAssignmentState targetState;
        public double worldTime;
        public string sourceEventId;
        public bool preview;
    }

    public sealed class OrganizationMembershipSnapshot
    {
        public OrganizationMembershipSnapshot(OrganizationMembershipRecordData data, IEnumerable<OrganizationRankAssignmentRecordData> ranks, IEnumerable<OrganizationOfficeAssignmentRecordData> offices)
        {
            Data = data?.Clone() ?? new OrganizationMembershipRecordData();
            RankAssignments = OrganizationModelUtility.Ordered(ranks ?? Array.Empty<OrganizationRankAssignmentRecordData>(), item => item.rankAssignmentId).Select(item => item.Clone()).ToArray();
            OfficeAssignments = OrganizationModelUtility.Ordered(offices ?? Array.Empty<OrganizationOfficeAssignmentRecordData>(), item => item.officeAssignmentId).Select(item => item.Clone()).ToArray();
        }

        public OrganizationMembershipRecordData Data { get; }
        public IReadOnlyList<OrganizationRankAssignmentRecordData> RankAssignments { get; }
        public IReadOnlyList<OrganizationOfficeAssignmentRecordData> OfficeAssignments { get; }
        public string MembershipId => Data.membershipId ?? string.Empty;
        public string OrganizationId => Data.organizationId ?? string.Empty;
        public string PersonId => Data.personId ?? string.Empty;
        public OrganizationMembershipStatus Status => Data.status;
        public bool IsActive => Data.IsActive;
        public long Revision => Data.revision;
    }

    public sealed class OrganizationOfficeSnapshot
    {
        public OrganizationOfficeSnapshot(OrganizationOfficeRecordData data, IEnumerable<OrganizationOfficeAssignmentRecordData> assignments)
        {
            Data = data?.Clone() ?? new OrganizationOfficeRecordData();
            Assignments = OrganizationModelUtility.Ordered(assignments ?? Array.Empty<OrganizationOfficeAssignmentRecordData>(), item => item.officeAssignmentId).Select(item => item.Clone()).ToArray();
        }

        public OrganizationOfficeRecordData Data { get; }
        public IReadOnlyList<OrganizationOfficeAssignmentRecordData> Assignments { get; }
        public string OfficeId => Data.officeId ?? string.Empty;
        public bool IsVacant => Data.IsActive && Assignments.Count(item => item.IsActive) < Data.maximumActiveHolders;
    }

    public sealed class OrganizationMembershipProjection
    {
        public OrganizationMembershipProjection(OrganizationMembershipProjectionAccess access, InformationSubjectReferenceData subject, OrganizationMembershipSnapshot snapshot, string message)
        {
            Access = access;
            Subject = subject?.Clone() ?? new InformationSubjectReferenceData();
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public OrganizationMembershipProjectionAccess Access { get; }
        public InformationSubjectReferenceData Subject { get; }
        public OrganizationMembershipSnapshot Snapshot { get; }
        public string Message { get; }
        public bool Succeeded => Access == OrganizationMembershipProjectionAccess.Full || Access == OrganizationMembershipProjectionAccess.Redacted;
        public bool Redacted => Access == OrganizationMembershipProjectionAccess.Redacted;
    }

    public sealed class OrganizationMembershipOperationResult
    {
        private OrganizationMembershipOperationResult(bool succeeded, OrganizationMembershipOperationStatus status, OrganizationMembershipSnapshot membership, OrganizationOfficeSnapshot office, OrganizationRankAssignmentRecordData rankAssignment, OrganizationOfficeAssignmentRecordData officeAssignment, string message, long beforeRevision, long afterRevision, bool preview, bool duplicate)
        {
            Succeeded = succeeded;
            Status = status;
            Membership = membership;
            Office = office;
            RankAssignment = rankAssignment?.Clone();
            OfficeAssignment = officeAssignment?.Clone();
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public OrganizationMembershipOperationStatus Status { get; }
        public OrganizationMembershipSnapshot Membership { get; }
        public OrganizationOfficeSnapshot Office { get; }
        public OrganizationRankAssignmentRecordData RankAssignment { get; }
        public OrganizationOfficeAssignmentRecordData OfficeAssignment { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }

        public static OrganizationMembershipOperationResult Success(OrganizationMembershipSnapshot membership, OrganizationOfficeSnapshot office, OrganizationRankAssignmentRecordData rank, OrganizationOfficeAssignmentRecordData assignment, string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new OrganizationMembershipOperationResult(true, preview ? OrganizationMembershipOperationStatus.Preview : duplicate ? OrganizationMembershipOperationStatus.Duplicate : OrganizationMembershipOperationStatus.Succeeded, membership, office, rank, assignment, message, before, after, preview, duplicate);
        }

        public static OrganizationMembershipOperationResult Failure(OrganizationMembershipOperationStatus status, string message, long before)
        {
            return new OrganizationMembershipOperationResult(false, status, null, null, null, null, message, before, before, preview: false, duplicate: false);
        }
    }
}
