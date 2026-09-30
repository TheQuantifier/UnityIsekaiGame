using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Organizations
{
    [Serializable]
    public sealed class OrganizationAuthorityScopeData
    {
        public OrganizationAuthorityScopeType scopeType = OrganizationAuthorityScopeType.EntireOrganization;
        public OrganizationAuthorityScopeMatch scopeMatch = OrganizationAuthorityScopeMatch.ExactOnly;
        public string organizationId;
        public string branchOrganizationId;
        public string personId;
        public string officeId;
        public string rankTrackDefinitionId;
        public string membershipDefinitionId;
        public string placeId;
        public string propertyReferenceId;
        public string recordId;
        public string actionDefinitionId;
        public InformationSubjectReferenceData customSubject;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string diagnostics;

        public OrganizationAuthorityScopeData Clone()
        {
            return new OrganizationAuthorityScopeData
            {
                scopeType = scopeType,
                scopeMatch = scopeMatch,
                organizationId = organizationId ?? string.Empty,
                branchOrganizationId = branchOrganizationId ?? string.Empty,
                personId = personId ?? string.Empty,
                officeId = officeId ?? string.Empty,
                rankTrackDefinitionId = rankTrackDefinitionId ?? string.Empty,
                membershipDefinitionId = membershipDefinitionId ?? string.Empty,
                placeId = placeId ?? string.Empty,
                propertyReferenceId = propertyReferenceId ?? string.Empty,
                recordId = recordId ?? string.Empty,
                actionDefinitionId = actionDefinitionId ?? string.Empty,
                customSubject = customSubject?.Clone(),
                visibility = visibility,
                diagnostics = diagnostics ?? string.Empty
            };
        }

        public static OrganizationAuthorityScopeData ForOrganization(string organizationId, OrganizationAuthorityScopeType scopeType = OrganizationAuthorityScopeType.EntireOrganization, OrganizationAuthorityScopeMatch scopeMatch = OrganizationAuthorityScopeMatch.ExactOnly)
        {
            return new OrganizationAuthorityScopeData
            {
                scopeType = scopeType,
                scopeMatch = scopeMatch,
                organizationId = organizationId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class OrganizationAuthorityGrantRecordData
    {
        public string grantId;
        public string organizationId;
        public string granteePersonId;
        public string grantorPersonId;
        public string grantorOrganizationId;
        public string authorityRoleDefinitionId;
        public string[] permissionDefinitionIds = Array.Empty<string>();
        public OrganizationAuthoritySourceType sourceType = OrganizationAuthoritySourceType.DirectGrant;
        public string sourceMembershipId;
        public string sourceRankAssignmentId;
        public string sourceOfficeId;
        public string sourceOfficeAssignmentId;
        public string sourceGrantId;
        public OrganizationAuthorityScopeData scope = new OrganizationAuthorityScopeData();
        public double startWorldTime;
        public double expirationWorldTime = -1d;
        public OrganizationAuthorityGrantLifecycleState lifecycleState = OrganizationAuthorityGrantLifecycleState.Active;
        public int delegationDepth;
        public OrganizationAuthorityDelegationPolicy delegationPolicy = OrganizationAuthorityDelegationPolicy.NonDelegable;
        public bool redelegationAllowed;
        public string originatingInteractionId;
        public string sourceEventId;
        public string sourceRecordId;
        public string revocationReason;
        public double revokedWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public bool IsActiveAt(double worldTime)
        {
            return lifecycleState == OrganizationAuthorityGrantLifecycleState.Active
                && worldTime >= startWorldTime
                && (expirationWorldTime < 0d || worldTime < expirationWorldTime);
        }

        public OrganizationAuthorityGrantRecordData Clone()
        {
            return new OrganizationAuthorityGrantRecordData
            {
                grantId = grantId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                granteePersonId = granteePersonId ?? string.Empty,
                grantorPersonId = grantorPersonId ?? string.Empty,
                grantorOrganizationId = grantorOrganizationId ?? string.Empty,
                authorityRoleDefinitionId = authorityRoleDefinitionId ?? string.Empty,
                permissionDefinitionIds = OrganizationModelUtility.Clean(permissionDefinitionIds),
                sourceType = sourceType,
                sourceMembershipId = sourceMembershipId ?? string.Empty,
                sourceRankAssignmentId = sourceRankAssignmentId ?? string.Empty,
                sourceOfficeId = sourceOfficeId ?? string.Empty,
                sourceOfficeAssignmentId = sourceOfficeAssignmentId ?? string.Empty,
                sourceGrantId = sourceGrantId ?? string.Empty,
                scope = scope?.Clone() ?? new OrganizationAuthorityScopeData(),
                startWorldTime = startWorldTime,
                expirationWorldTime = expirationWorldTime,
                lifecycleState = lifecycleState,
                delegationDepth = Math.Max(0, delegationDepth),
                delegationPolicy = delegationPolicy,
                redelegationAllowed = redelegationAllowed,
                originatingInteractionId = originatingInteractionId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                revocationReason = revocationReason ?? string.Empty,
                revokedWorldTime = revokedWorldTime,
                visibility = visibility,
                provenanceId = provenanceId ?? string.Empty,
                tags = OrganizationModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationAuthorityApprovalRecordData
    {
        public string approvalId;
        public string operationId;
        public string actionDefinitionId;
        public string organizationId;
        public string approverPersonId;
        public string targetPersonId;
        public OrganizationAuthorityScopeData scope = new OrganizationAuthorityScopeData();
        public string[] approvedPermissionIds = Array.Empty<string>();
        public double approvedWorldTime;
        public double expirationWorldTime = -1d;
        public double consumedWorldTime = -1d;
        public OrganizationApprovalLifecycleState lifecycleState = OrganizationApprovalLifecycleState.Active;
        public string sourceAuthorityId;
        public string sourceEventId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime)
        {
            return lifecycleState == OrganizationApprovalLifecycleState.Active
                && worldTime >= approvedWorldTime
                && (expirationWorldTime < 0d || worldTime < expirationWorldTime);
        }

        public OrganizationAuthorityApprovalRecordData Clone()
        {
            return new OrganizationAuthorityApprovalRecordData
            {
                approvalId = approvalId ?? string.Empty,
                operationId = operationId ?? string.Empty,
                actionDefinitionId = actionDefinitionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                approverPersonId = approverPersonId ?? string.Empty,
                targetPersonId = targetPersonId ?? string.Empty,
                scope = scope?.Clone() ?? new OrganizationAuthorityScopeData(),
                approvedPermissionIds = OrganizationModelUtility.Clean(approvedPermissionIds),
                approvedWorldTime = approvedWorldTime,
                expirationWorldTime = expirationWorldTime,
                consumedWorldTime = consumedWorldTime,
                lifecycleState = lifecycleState,
                sourceAuthorityId = sourceAuthorityId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationAuthorityAuditRecordData
    {
        public string auditId;
        public string operationId;
        public string actionDefinitionId;
        public string organizationId;
        public string actorPersonId;
        public OrganizationAuthorizationStatus status = OrganizationAuthorizationStatus.Unknown;
        public string[] requiredPermissionIds = Array.Empty<string>();
        public string[] sourceAuthorityIds = Array.Empty<string>();
        public double worldTime;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string message;
        public long revision = 1L;

        public OrganizationAuthorityAuditRecordData Clone()
        {
            return new OrganizationAuthorityAuditRecordData
            {
                auditId = auditId ?? string.Empty,
                operationId = operationId ?? string.Empty,
                actionDefinitionId = actionDefinitionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                status = status,
                requiredPermissionIds = OrganizationModelUtility.Clean(requiredPermissionIds),
                sourceAuthorityIds = OrganizationModelUtility.Clean(sourceAuthorityIds),
                worldTime = worldTime,
                visibility = visibility,
                message = message ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class OrganizationAuthorityTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public long revision;

        public OrganizationAuthorityTransactionRecordData Clone()
        {
            return new OrganizationAuthorityTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OrganizationAuthorityRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<OrganizationAuthorityGrantRecordData> grants = new List<OrganizationAuthorityGrantRecordData>();
        public List<OrganizationAuthorityApprovalRecordData> approvals = new List<OrganizationAuthorityApprovalRecordData>();
        public List<OrganizationAuthorityAuditRecordData> audits = new List<OrganizationAuthorityAuditRecordData>();
        public List<OrganizationAuthorityTransactionRecordData> transactions = new List<OrganizationAuthorityTransactionRecordData>();

        public OrganizationAuthorityRuntimeSaveData Clone()
        {
            return new OrganizationAuthorityRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = Math.Max(0L, revision),
                grants = grants == null ? new List<OrganizationAuthorityGrantRecordData>() : grants.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                approvals = approvals == null ? new List<OrganizationAuthorityApprovalRecordData>() : approvals.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                audits = audits == null ? new List<OrganizationAuthorityAuditRecordData>() : audits.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transactions = transactions == null ? new List<OrganizationAuthorityTransactionRecordData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class OrganizationAuthorityGrantRequest
    {
        public string grantId;
        public string organizationId;
        public string granteePersonId;
        public string grantorPersonId;
        public string grantorOrganizationId;
        public string authorityRoleDefinitionId;
        public string[] permissionDefinitionIds = Array.Empty<string>();
        public OrganizationAuthoritySourceType sourceType = OrganizationAuthoritySourceType.DirectGrant;
        public string sourceGrantId;
        public OrganizationAuthorityScopeData scope;
        public double startWorldTime;
        public double expirationWorldTime = -1d;
        public OrganizationAuthorityDelegationPolicy delegationPolicy = OrganizationAuthorityDelegationPolicy.NonDelegable;
        public bool redelegationAllowed;
        public string originatingInteractionId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string[] tags = Array.Empty<string>();
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationDelegationRequest
    {
        public string delegationGrantId;
        public string organizationId;
        public string delegatorPersonId;
        public string recipientPersonId;
        public string sourceAuthorityId;
        public string authorityRoleDefinitionId;
        public string[] permissionDefinitionIds = Array.Empty<string>();
        public OrganizationAuthorityScopeData scope;
        public double startWorldTime;
        public double expirationWorldTime = -1d;
        public OrganizationAuthorityDelegationPolicy delegationPolicy = OrganizationAuthorityDelegationPolicy.NonDelegable;
        public bool redelegationAllowed;
        public string originatingInteractionId;
        public string reason;
        public string sourceEventId;
        public string provenanceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationAuthorityLifecycleRequest
    {
        public string grantId;
        public OrganizationAuthorityGrantLifecycleState targetState;
        public double worldTime;
        public string reason;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationAuthorizationRequest
    {
        public string operationId;
        public string actorPersonId;
        public string organizationId;
        public string actionDefinitionId;
        public string[] requiredPermissionIds = Array.Empty<string>();
        public OrganizationPermissionCombinationPolicy permissionPolicy = OrganizationPermissionCombinationPolicy.Unknown;
        public OrganizationAuthorityScopeData scope;
        public string targetPersonId;
        public string targetRecordId;
        public string[] actorCapabilityIds = Array.Empty<string>();
        public string[] actorQualificationIds = Array.Empty<string>();
        public string[] approvalPersonIds = Array.Empty<string>();
        public bool consumeApprovals;
        public bool allowDelegatedAuthority = true;
        public bool privilegedDiagnostics;
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationApprovalRequest
    {
        public string approvalId;
        public string operationId;
        public string actionDefinitionId;
        public string organizationId;
        public string approverPersonId;
        public string targetPersonId;
        public OrganizationAuthorityScopeData scope;
        public double approvedWorldTime;
        public double expirationWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string sourceEventId;
        public string provenanceId;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationAuthoritySnapshot
    {
        public OrganizationAuthoritySnapshot(OrganizationAuthorityGrantRecordData data)
        {
            Data = data?.Clone() ?? new OrganizationAuthorityGrantRecordData();
        }

        public OrganizationAuthorityGrantRecordData Data { get; }
        public string GrantId => Data.grantId ?? string.Empty;
        public string OrganizationId => Data.organizationId ?? string.Empty;
        public string GranteePersonId => Data.granteePersonId ?? string.Empty;
        public OrganizationAuthorityGrantLifecycleState LifecycleState => Data.lifecycleState;
        public long Revision => Data.revision;
    }

    public sealed class OrganizationApprovalSnapshot
    {
        public OrganizationApprovalSnapshot(OrganizationAuthorityApprovalRecordData data)
        {
            Data = data?.Clone() ?? new OrganizationAuthorityApprovalRecordData();
        }

        public OrganizationAuthorityApprovalRecordData Data { get; }
        public string ApprovalId => Data.approvalId ?? string.Empty;
        public OrganizationApprovalLifecycleState LifecycleState => Data.lifecycleState;
    }

    public sealed class OrganizationAuthorityAuditSnapshot
    {
        public OrganizationAuthorityAuditSnapshot(OrganizationAuthorityAuditRecordData data)
        {
            Data = data?.Clone() ?? new OrganizationAuthorityAuditRecordData();
        }

        public OrganizationAuthorityAuditRecordData Data { get; }
        public string AuditId => Data.auditId ?? string.Empty;
        public OrganizationAuthorizationStatus Status => Data.status;
        public long Revision => Data.revision;
    }

    public sealed class OrganizationEffectivePermissionSourceData
    {
        public string sourceAuthorityId;
        public OrganizationAuthoritySourceType sourceType = OrganizationAuthoritySourceType.Unknown;
        public string sourceRecordId;
        public string authorityRoleDefinitionId;
        public string permissionDefinitionId;
        public OrganizationAuthorityScopeData scope = new OrganizationAuthorityScopeData();
        public bool delegated;
        public bool denied;
        public int priority;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string message;

        public OrganizationEffectivePermissionSourceData Clone()
        {
            return new OrganizationEffectivePermissionSourceData
            {
                sourceAuthorityId = sourceAuthorityId ?? string.Empty,
                sourceType = sourceType,
                sourceRecordId = sourceRecordId ?? string.Empty,
                authorityRoleDefinitionId = authorityRoleDefinitionId ?? string.Empty,
                permissionDefinitionId = permissionDefinitionId ?? string.Empty,
                scope = scope?.Clone() ?? new OrganizationAuthorityScopeData(),
                delegated = delegated,
                denied = denied,
                priority = priority,
                visibility = visibility,
                message = message ?? string.Empty
            };
        }
    }

    public sealed class OrganizationEffectiveAuthoritySnapshot
    {
        public OrganizationEffectiveAuthoritySnapshot(string personId, string organizationId, IEnumerable<OrganizationEffectivePermissionSourceData> sources, long runtimeRevision)
        {
            PersonId = personId ?? string.Empty;
            OrganizationId = organizationId ?? string.Empty;
            Sources = (sources ?? Array.Empty<OrganizationEffectivePermissionSourceData>())
                .Select(item => item?.Clone())
                .Where(item => item != null)
                .OrderBy(item => item.permissionDefinitionId, StringComparer.Ordinal)
                .ThenByDescending(item => item.priority)
                .ThenBy(item => item.sourceAuthorityId, StringComparer.Ordinal)
                .ToArray();
            RuntimeRevision = runtimeRevision;
        }

        public string PersonId { get; }
        public string OrganizationId { get; }
        public IReadOnlyList<OrganizationEffectivePermissionSourceData> Sources { get; }
        public long RuntimeRevision { get; }
    }

    public sealed class OrganizationAuthorizationResult
    {
        public OrganizationAuthorizationResult(
            bool succeeded,
            OrganizationAuthorizationStatus status,
            string operationId,
            string actionDefinitionId,
            string actorPersonId,
            string organizationId,
            IEnumerable<string> requiredPermissionIds,
            IEnumerable<OrganizationEffectivePermissionSourceData> matchedSources,
            IEnumerable<string> missingPermissionIds,
            IEnumerable<string> approvalIds,
            string message,
            long beforeRevision,
            long afterRevision,
            bool preview = false,
            bool duplicate = false)
        {
            Succeeded = succeeded;
            Status = status;
            OperationId = operationId ?? string.Empty;
            ActionDefinitionId = actionDefinitionId ?? string.Empty;
            ActorPersonId = actorPersonId ?? string.Empty;
            OrganizationId = organizationId ?? string.Empty;
            RequiredPermissionIds = OrganizationModelUtility.Clean(requiredPermissionIds);
            MatchedSources = (matchedSources ?? Array.Empty<OrganizationEffectivePermissionSourceData>())
                .Select(item => item?.Clone())
                .Where(item => item != null)
                .OrderBy(item => item.permissionDefinitionId, StringComparer.Ordinal)
                .ThenByDescending(item => item.priority)
                .ThenBy(item => item.sourceAuthorityId, StringComparer.Ordinal)
                .ToArray();
            MissingPermissionIds = OrganizationModelUtility.Clean(missingPermissionIds);
            ApprovalIds = OrganizationModelUtility.Clean(approvalIds);
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public OrganizationAuthorizationStatus Status { get; }
        public string OperationId { get; }
        public string ActionDefinitionId { get; }
        public string ActorPersonId { get; }
        public string OrganizationId { get; }
        public IReadOnlyList<string> RequiredPermissionIds { get; }
        public IReadOnlyList<OrganizationEffectivePermissionSourceData> MatchedSources { get; }
        public IReadOnlyList<string> MissingPermissionIds { get; }
        public IReadOnlyList<string> ApprovalIds { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
    }

    public sealed class OrganizationAuthorityOperationResult
    {
        private OrganizationAuthorityOperationResult(bool succeeded, OrganizationAuthorizationStatus status, OrganizationAuthoritySnapshot grant, OrganizationApprovalSnapshot approval, OrganizationAuthorizationResult authorization, string message, long beforeRevision, long afterRevision, bool preview, bool duplicate)
        {
            Succeeded = succeeded;
            Status = status;
            Grant = grant;
            Approval = approval;
            Authorization = authorization;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public OrganizationAuthorizationStatus Status { get; }
        public OrganizationAuthoritySnapshot Grant { get; }
        public OrganizationApprovalSnapshot Approval { get; }
        public OrganizationAuthorizationResult Authorization { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }

        public static OrganizationAuthorityOperationResult Success(OrganizationAuthoritySnapshot grant, OrganizationApprovalSnapshot approval, OrganizationAuthorizationResult authorization, string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new OrganizationAuthorityOperationResult(true, preview ? OrganizationAuthorizationStatus.Preview : duplicate ? OrganizationAuthorizationStatus.Duplicate : OrganizationAuthorizationStatus.Authorized, grant, approval, authorization, message, before, after, preview, duplicate);
        }

        public static OrganizationAuthorityOperationResult Failure(OrganizationAuthorizationStatus status, string message, long before)
        {
            return new OrganizationAuthorityOperationResult(false, status, null, null, null, message, before, before, false, false);
        }
    }

    public sealed class OrganizationAuthorityProjection
    {
        public OrganizationAuthorityProjection(OrganizationAuthorityProjectionAccess access, OrganizationAuthoritySnapshot snapshot, string message)
        {
            Access = access;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public OrganizationAuthorityProjectionAccess Access { get; }
        public OrganizationAuthoritySnapshot Snapshot { get; }
        public string Message { get; }
        public bool Succeeded => Access == OrganizationAuthorityProjectionAccess.Full || Access == OrganizationAuthorityProjectionAccess.Redacted;
    }
}
