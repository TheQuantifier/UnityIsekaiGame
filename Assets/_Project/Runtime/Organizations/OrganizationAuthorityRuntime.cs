using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    public sealed class OrganizationAuthorityRuntime
    {
        private const int MaximumHierarchyTraversal = 64;
        private const int MaximumDelegationDepth = 8;

        private readonly Dictionary<string, OrganizationAuthorityGrantRecordData> grantsById = new Dictionary<string, OrganizationAuthorityGrantRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationAuthorityApprovalRecordData> approvalsById = new Dictionary<string, OrganizationAuthorityApprovalRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationAuthorityAuditRecordData> auditsById = new Dictionary<string, OrganizationAuthorityAuditRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationAuthorityTransactionRecordData> transactionsById = new Dictionary<string, OrganizationAuthorityTransactionRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private OrganizationMembershipRuntime memberships;
        private string worldId = string.Empty;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownOrganizationIds = new HashSet<string>(StringComparer.Ordinal);

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public int GrantCount => grantsById.Count;
        public int ApprovalCount => approvalsById.Count;
        public IReadOnlyList<OrganizationAuthoritySnapshot> Grants => grantsById.Values.OrderBy(item => item.grantId, StringComparer.Ordinal).Select(item => new OrganizationAuthoritySnapshot(item)).ToArray();
        public IReadOnlyList<OrganizationApprovalSnapshot> Approvals => approvalsById.Values.OrderBy(item => item.approvalId, StringComparer.Ordinal).Select(item => new OrganizationApprovalSnapshot(item)).ToArray();
        public IReadOnlyList<OrganizationAuthorityAuditSnapshot> Audits => auditsById.Values.OrderBy(item => item.auditId, StringComparer.Ordinal).Select(item => new OrganizationAuthorityAuditSnapshot(item)).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, string world, IEnumerable<string> persons = null, IEnumerable<string> organizationIds = null)
        {
            registry = definitionRegistry ?? registry;
            organizations = organizationRuntime ?? organizations;
            memberships = membershipRuntime ?? memberships;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            knownPersonIds = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            knownOrganizationIds = new HashSet<string>(Clean(organizationIds).Concat((organizations?.Snapshots ?? Array.Empty<OrganizationSnapshot>()).Select(item => item.OrganizationId)), StringComparer.Ordinal);
        }

        public OrganizationAuthorityOperationResult CreateDirectGrant(OrganizationAuthorityGrantRequest request)
        {
            request ??= new OrganizationAuthorityGrantRequest();
            long before = Revision;
            string grantId = Normalize(request.grantId);
            if (string.IsNullOrWhiteSpace(grantId))
            {
                grantId = $"organization-authority-grant.{Normalize(request.organizationId)}.{Normalize(request.granteePersonId)}.{Normalize(request.authorityRoleDefinitionId)}";
            }

            if (TryDuplicate(Normalize(request.transactionId), grantId, "grant", before, out OrganizationAuthorityOperationResult duplicate))
            {
                return duplicate;
            }

            if (grantsById.ContainsKey(grantId))
            {
                return Fail(OrganizationAuthorizationStatus.InvalidRequest, $"Authority grant '{grantId}' already exists.", before);
            }

            if (request.sourceType == OrganizationAuthoritySourceType.Delegation)
            {
                return Fail(OrganizationAuthorizationStatus.InvalidRequest, "Delegation grants must be created through DelegateAuthority.", before);
            }

            return CreateGrantRecord(request, grantId, before, completeTransaction: true, "Authority grant created.", "Authority grant previewed.");
        }

        public OrganizationAuthorityOperationResult DelegateAuthority(OrganizationDelegationRequest request)
        {
            request ??= new OrganizationDelegationRequest();
            long before = Revision;
            string grantId = Normalize(request.delegationGrantId);
            if (string.IsNullOrWhiteSpace(grantId))
            {
                grantId = $"organization-authority-delegation.{Normalize(request.organizationId)}.{Normalize(request.delegatorPersonId)}.{Normalize(request.recipientPersonId)}";
            }

            if (TryDuplicate(Normalize(request.transactionId), grantId, "delegation", before, out OrganizationAuthorityOperationResult duplicate))
            {
                return duplicate;
            }

            if (grantsById.ContainsKey(grantId))
            {
                return Fail(OrganizationAuthorizationStatus.InvalidRequest, $"Delegation grant '{grantId}' already exists.", before);
            }

            string[] requestedPermissions = Clean(request.permissionDefinitionIds);
            if (requestedPermissions.Length == 0 && !string.IsNullOrWhiteSpace(request.authorityRoleDefinitionId) && TryGetDefinition(request.authorityRoleDefinitionId, out OrganizationAuthorityRoleDefinition requestedRole))
            {
                requestedPermissions = requestedRole.GrantedPermissionIds.ToArray();
            }

            if (requestedPermissions.Length == 0)
            {
                return Fail(OrganizationAuthorizationStatus.MissingPermission, "Delegation requires a role or explicit permission set.", before);
            }

            OrganizationAuthorizationResult authority = EvaluateAuthorization(new OrganizationAuthorizationRequest
            {
                actorPersonId = request.delegatorPersonId,
                organizationId = request.organizationId,
                requiredPermissionIds = requestedPermissions,
                permissionPolicy = OrganizationPermissionCombinationPolicy.AllRequiredPermissions,
                scope = request.scope,
                allowDelegatedAuthority = true,
                worldTime = request.startWorldTime,
                privilegedDiagnostics = true
            });

            if (!authority.Succeeded)
            {
                return Fail(authority.Status, $"Delegator lacks delegated permissions: {authority.Message}", before);
            }

            foreach (OrganizationEffectivePermissionSourceData source in authority.MatchedSources)
            {
                if (!SourceAllowsDelegation(source, request.permissionDefinitionIds, out string delegationFailure))
                {
                    return Fail(OrganizationAuthorizationStatus.InvalidDependency, delegationFailure, before);
                }
            }

            if (!IsNarrowerOrEqualScope(request.scope, authority.MatchedSources.Select(item => item.scope), request.organizationId))
            {
                return Fail(OrganizationAuthorizationStatus.ScopeMismatch, "Delegated scope must be equal to or narrower than source authority.", before);
            }

            if (!ValidateDelegationChain(request.sourceAuthorityId, request.delegatorPersonId, request.recipientPersonId, out int depth, out string chainFailure))
            {
                return Fail(OrganizationAuthorizationStatus.InvalidDependency, chainFailure, before);
            }

            OrganizationAuthorityGrantRequest grant = new OrganizationAuthorityGrantRequest
            {
                grantId = grantId,
                organizationId = request.organizationId,
                granteePersonId = request.recipientPersonId,
                grantorPersonId = request.delegatorPersonId,
                authorityRoleDefinitionId = request.authorityRoleDefinitionId,
                permissionDefinitionIds = requestedPermissions,
                sourceType = OrganizationAuthoritySourceType.Delegation,
                sourceGrantId = request.sourceAuthorityId,
                scope = request.scope,
                startWorldTime = request.startWorldTime,
                expirationWorldTime = request.expirationWorldTime,
                delegationPolicy = request.delegationPolicy,
                redelegationAllowed = request.redelegationAllowed,
                originatingInteractionId = request.originatingInteractionId,
                sourceEventId = request.sourceEventId,
                provenanceId = request.provenanceId,
                visibility = request.visibility,
                transactionId = string.Empty,
                preview = request.preview
            };

            OrganizationAuthorityOperationResult result = CreateGrantRecord(grant, grantId, before, completeTransaction: false, "Authority delegation created.", "Authority delegation previewed.");
            if (!result.Succeeded)
            {
                return result;
            }

            if (!request.preview && grantsById.TryGetValue(grantId, out OrganizationAuthorityGrantRecordData record))
            {
                record.sourceGrantId = Normalize(request.sourceAuthorityId);
                record.delegationDepth = depth + 1;
                CompleteTransaction(request.transactionId, "delegation", grantId);
            }

            return result;
        }

        private OrganizationAuthorityOperationResult CreateGrantRecord(OrganizationAuthorityGrantRequest request, string grantId, long before, bool completeTransaction, string successMessage, string previewMessage)
        {
            if (!ValidateGrantRequest(request, grantId, before, out OrganizationAuthorityOperationResult failure))
            {
                return failure;
            }

            OrganizationAuthorityRuntimeSaveData rollback = CreateSaveData();
            OrganizationAuthorityGrantRecordData record = new OrganizationAuthorityGrantRecordData
            {
                grantId = grantId,
                organizationId = Normalize(request.organizationId),
                granteePersonId = Normalize(request.granteePersonId),
                grantorPersonId = Normalize(request.grantorPersonId),
                grantorOrganizationId = Normalize(request.grantorOrganizationId),
                authorityRoleDefinitionId = Normalize(request.authorityRoleDefinitionId),
                permissionDefinitionIds = Clean(request.permissionDefinitionIds),
                sourceType = request.sourceType == OrganizationAuthoritySourceType.Unknown ? OrganizationAuthoritySourceType.DirectGrant : request.sourceType,
                sourceGrantId = Normalize(request.sourceGrantId),
                scope = NormalizeScope(request.scope, request.organizationId),
                startWorldTime = request.startWorldTime,
                expirationWorldTime = request.expirationWorldTime,
                lifecycleState = OrganizationAuthorityGrantLifecycleState.Active,
                delegationPolicy = request.delegationPolicy,
                redelegationAllowed = request.redelegationAllowed || request.delegationPolicy == OrganizationAuthorityDelegationPolicy.Redelegable,
                originatingInteractionId = Normalize(request.originatingInteractionId),
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                visibility = request.visibility,
                tags = Clean(request.tags),
                revision = 1L
            };

            grantsById.Add(record.grantId, record);
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationAuthorizationStatus.PersistenceInvalid, validationFailure, before);
            }

            OrganizationAuthoritySnapshot snapshot = new OrganizationAuthoritySnapshot(record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Succeed(snapshot, null, null, previewMessage, before, before, preview: true);
            }

            if (completeTransaction)
            {
                CompleteTransaction(request.transactionId, "grant", grantId);
            }

            Touch();
            return Succeed(new OrganizationAuthoritySnapshot(record), null, null, successMessage, before, Revision);
        }

        public OrganizationAuthorityOperationResult ChangeGrantLifecycle(OrganizationAuthorityLifecycleRequest request)
        {
            request ??= new OrganizationAuthorityLifecycleRequest();
            long before = Revision;
            string grantId = Normalize(request.grantId);
            if (TryDuplicate(Normalize(request.transactionId), grantId, "grant-lifecycle", before, out OrganizationAuthorityOperationResult duplicate))
            {
                return duplicate;
            }

            if (!grantsById.TryGetValue(grantId, out OrganizationAuthorityGrantRecordData record))
            {
                return Fail(OrganizationAuthorizationStatus.InvalidRequest, $"Authority grant '{grantId}' does not exist.", before);
            }

            if (request.targetState != OrganizationAuthorityGrantLifecycleState.Suspended
                && request.targetState != OrganizationAuthorityGrantLifecycleState.Active
                && request.targetState != OrganizationAuthorityGrantLifecycleState.Revoked
                && request.targetState != OrganizationAuthorityGrantLifecycleState.Expired
                && request.targetState != OrganizationAuthorityGrantLifecycleState.Ended)
            {
                return Fail(OrganizationAuthorizationStatus.InvalidRequest, $"Unsupported authority grant lifecycle target '{request.targetState}'.", before);
            }

            OrganizationAuthorityRuntimeSaveData rollback = CreateSaveData();
            record.lifecycleState = request.targetState;
            record.revocationReason = request.targetState == OrganizationAuthorityGrantLifecycleState.Revoked ? Normalize(request.reason) : record.revocationReason;
            record.revokedWorldTime = request.targetState == OrganizationAuthorityGrantLifecycleState.Revoked ? request.worldTime : record.revokedWorldTime;
            record.revision++;
            InvalidateDependentDelegations(record.grantId, request.worldTime, request.targetState);

            if (!ValidateCurrent(out string failure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationAuthorizationStatus.PersistenceInvalid, failure, before);
            }

            if (request.preview)
            {
                OrganizationAuthoritySnapshot preview = new OrganizationAuthoritySnapshot(record);
                RestoreInternal(rollback);
                return Succeed(preview, null, null, "Authority lifecycle previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "grant-lifecycle", grantId);
            Touch();
            return Succeed(new OrganizationAuthoritySnapshot(record), null, null, "Authority lifecycle changed.", before, Revision);
        }

        public OrganizationAuthorityOperationResult RecordApproval(OrganizationApprovalRequest request)
        {
            request ??= new OrganizationApprovalRequest();
            long before = Revision;
            string approvalId = Normalize(request.approvalId);
            if (string.IsNullOrWhiteSpace(approvalId))
            {
                approvalId = $"organization-authority-approval.{Normalize(request.operationId)}.{Normalize(request.approverPersonId)}";
            }

            if (TryDuplicate(Normalize(request.transactionId), approvalId, "approval", before, out OrganizationAuthorityOperationResult duplicate))
            {
                return duplicate;
            }

            if (approvalsById.ContainsKey(approvalId) || approvalsById.Values.Any(item => item.IsActiveAt(request.approvedWorldTime) && string.Equals(item.operationId, Normalize(request.operationId), StringComparison.Ordinal) && string.Equals(item.approverPersonId, Normalize(request.approverPersonId), StringComparison.Ordinal)))
            {
                return Fail(OrganizationAuthorizationStatus.DuplicateApproval, "The approver already has an active approval for this operation.", before);
            }

            string[] requiredApprovalPermissions = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(request.actionDefinitionId) && TryGetDefinition(request.actionDefinitionId, out InstitutionalActionDefinition action))
            {
                requiredApprovalPermissions = action.RequiredPermissionIds.ToArray();
            }

            OrganizationAuthorizationResult authorization = EvaluateAuthorization(new OrganizationAuthorizationRequest
            {
                operationId = request.operationId,
                actorPersonId = request.approverPersonId,
                organizationId = request.organizationId,
                requiredPermissionIds = requiredApprovalPermissions,
                permissionPolicy = OrganizationPermissionCombinationPolicy.AllRequiredPermissions,
                scope = request.scope,
                targetPersonId = request.targetPersonId,
                worldTime = request.approvedWorldTime,
                privilegedDiagnostics = true
            });

            if (!authorization.Succeeded)
            {
                return Fail(authorization.Status, authorization.Message, before);
            }

            OrganizationAuthorityRuntimeSaveData rollback = CreateSaveData();
            OrganizationAuthorityApprovalRecordData approval = new OrganizationAuthorityApprovalRecordData
            {
                approvalId = approvalId,
                operationId = Normalize(request.operationId),
                actionDefinitionId = Normalize(request.actionDefinitionId),
                organizationId = Normalize(request.organizationId),
                approverPersonId = Normalize(request.approverPersonId),
                targetPersonId = Normalize(request.targetPersonId),
                scope = NormalizeScope(request.scope, request.organizationId),
                approvedPermissionIds = authorization.RequiredPermissionIds.ToArray(),
                approvedWorldTime = request.approvedWorldTime,
                expirationWorldTime = request.expirationWorldTime,
                lifecycleState = OrganizationApprovalLifecycleState.Active,
                sourceAuthorityId = authorization.MatchedSources.FirstOrDefault()?.sourceAuthorityId ?? string.Empty,
                sourceEventId = Normalize(request.sourceEventId),
                provenanceId = Normalize(request.provenanceId),
                visibility = request.visibility,
                revision = 1L
            };
            approvalsById.Add(approval.approvalId, approval);

            if (!ValidateCurrent(out string failure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationAuthorizationStatus.PersistenceInvalid, failure, before);
            }

            if (request.preview)
            {
                OrganizationApprovalSnapshot preview = new OrganizationApprovalSnapshot(approval);
                RestoreInternal(rollback);
                return Succeed(null, preview, authorization, "Approval previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "approval", approvalId);
            Touch();
            return Succeed(null, new OrganizationApprovalSnapshot(approval), authorization, "Approval recorded.", before, Revision);
        }

        public OrganizationAuthorizationResult EvaluateAuthorization(OrganizationAuthorizationRequest request)
        {
            request ??= new OrganizationAuthorizationRequest();
            long before = Revision;
            string actor = Normalize(request.actorPersonId);
            string organizationId = Normalize(request.organizationId);
            double worldTime = request.worldTime;

            if (string.IsNullOrWhiteSpace(actor) || !knownPersonIds.Contains(actor))
            {
                return Auth(false, OrganizationAuthorizationStatus.MissingActor, request, Array.Empty<string>(), Array.Empty<OrganizationEffectivePermissionSourceData>(), Array.Empty<string>(), "Known actor is required.", before, before);
            }

            if (!TryActiveOrganization(organizationId, out OrganizationSnapshot organization))
            {
                return Auth(false, OrganizationAuthorizationStatus.MissingOrganization, request, Array.Empty<string>(), Array.Empty<OrganizationEffectivePermissionSourceData>(), Array.Empty<string>(), $"Organization '{organizationId}' is not active.", before, before);
            }

            InstitutionalActionDefinition action = null;
            string[] requiredPermissions = Clean(request.requiredPermissionIds);
            OrganizationPermissionCombinationPolicy policy = request.permissionPolicy;
            OrganizationAuthorityScopeData scope = NormalizeScope(request.scope, organizationId);
            if (!string.IsNullOrWhiteSpace(request.actionDefinitionId))
            {
                if (!TryGetDefinition(request.actionDefinitionId, out action))
                {
                    return Auth(false, OrganizationAuthorizationStatus.MissingAction, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, $"Action definition '{request.actionDefinitionId}' is missing.", before, before);
                }

                requiredPermissions = requiredPermissions.Length == 0 ? action.RequiredPermissionIds.ToArray() : requiredPermissions;
                policy = policy == OrganizationPermissionCombinationPolicy.Unknown ? action.PermissionPolicy : policy;
                if (scope.scopeType == OrganizationAuthorityScopeType.Unknown)
                {
                    scope.scopeType = action.DefaultScopeType;
                }

                if (action.RequiredCapabilityIds.Except(Clean(request.actorCapabilityIds), StringComparer.Ordinal).Any())
                {
                    return Auth(false, OrganizationAuthorizationStatus.CapabilityMissing, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, "Required capability is missing.", before, before);
                }

                if (action.RequiredQualificationIds.Except(Clean(request.actorQualificationIds), StringComparer.Ordinal).Any())
                {
                    return Auth(false, OrganizationAuthorizationStatus.QualificationMissing, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, "Required qualification is missing.", before, before);
                }

                if (!action.ExternalActorsMayBeAuthorized && memberships != null && !memberships.QueryMemberships(actor, organizationId, activeOnly: true).Any())
                {
                    return Auth(false, OrganizationAuthorizationStatus.MissingPermission, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, "Action requires an active organization member.", before, before);
                }

                if (action.RequiredMembershipState != OrganizationMembershipStatus.Unknown
                    && memberships != null
                    && !memberships.QueryMemberships(actor, organizationId).Any(item => item.Status == action.RequiredMembershipState))
                {
                    return Auth(false, OrganizationAuthorizationStatus.MissingPermission, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, $"Action requires membership state '{action.RequiredMembershipState}'.", before, before);
                }
            }

            requiredPermissions = Clean(requiredPermissions);
            if (requiredPermissions.Length == 0)
            {
                return Auth(false, OrganizationAuthorizationStatus.MissingPermission, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, "Authorization requires at least one permission.", before, before);
            }

            foreach (string permissionId in requiredPermissions)
            {
                if (!TryGetDefinition(permissionId, out OrganizationPermissionDefinition _))
                {
                    return Auth(false, OrganizationAuthorizationStatus.MissingPermission, request, requiredPermissions, Array.Empty<OrganizationEffectivePermissionSourceData>(), requiredPermissions, $"Permission definition '{permissionId}' is missing.", before, before);
                }
            }

            OrganizationEffectivePermissionSourceData[] sources = ResolveEffectivePermissionSources(actor, organizationId, worldTime, request.allowDelegatedAuthority, request.privilegedDiagnostics)
                .Where(source => SourceScopeMatches(source.scope, scope, organizationId))
                .ToArray();

            OrganizationEffectivePermissionSourceData[] denied = sources
                .Where(source => source.denied && requiredPermissions.Contains(source.permissionDefinitionId))
                .OrderByDescending(source => source.priority)
                .ThenBy(source => source.sourceAuthorityId, StringComparer.Ordinal)
                .ToArray();
            if (denied.Length > 0)
            {
                return Auth(false, OrganizationAuthorizationStatus.DeniedPermission, request, requiredPermissions, denied, requiredPermissions, $"Permission '{denied[0].permissionDefinitionId}' is explicitly denied.", before, before);
            }

            OrganizationEffectivePermissionSourceData[] grants = sources.Where(source => !source.denied && requiredPermissions.Contains(source.permissionDefinitionId)).ToArray();
            string[] present = grants.Select(source => source.permissionDefinitionId).Distinct(StringComparer.Ordinal).ToArray();
            string[] missing = requiredPermissions.Except(present, StringComparer.Ordinal).ToArray();
            bool authorized = policy switch
            {
                OrganizationPermissionCombinationPolicy.AnyRequiredPermission => present.Length > 0,
                OrganizationPermissionCombinationPolicy.JointApproval => missing.Length == 0 && HasJointApproval(request, action, requiredPermissions, worldTime, out _),
                _ => missing.Length == 0
            };

            if (policy == OrganizationPermissionCombinationPolicy.JointApproval && missing.Length == 0 && !HasJointApproval(request, action, requiredPermissions, worldTime, out string[] approvalIds))
            {
                return Auth(false, OrganizationAuthorizationStatus.JointApprovalMissing, request, requiredPermissions, grants, missing, "Joint approval requirement is not satisfied.", before, before);
            }

            if (!authorized)
            {
                return Auth(false, missing.Length == 0 ? OrganizationAuthorizationStatus.JointApprovalMissing : OrganizationAuthorizationStatus.MissingPermission, request, requiredPermissions, grants, missing.Length == 0 ? requiredPermissions : missing, "Required authority is missing.", before, before);
            }

            string[] activeApprovalIds = policy == OrganizationPermissionCombinationPolicy.JointApproval && HasJointApproval(request, action, requiredPermissions, worldTime, out string[] ids) ? ids : Array.Empty<string>();
            if (request.consumeApprovals && activeApprovalIds.Length > 0)
            {
                foreach (string approvalId in activeApprovalIds)
                {
                    if (approvalsById.TryGetValue(approvalId, out OrganizationAuthorityApprovalRecordData approval))
                    {
                        approval.lifecycleState = OrganizationApprovalLifecycleState.Consumed;
                        approval.consumedWorldTime = worldTime;
                        approval.revision++;
                    }
                }

                Touch();
            }

            return Auth(true, request.preview ? OrganizationAuthorizationStatus.Preview : OrganizationAuthorizationStatus.Authorized, request, requiredPermissions, grants, Array.Empty<string>(), "Authorized.", before, Revision, activeApprovalIds, preview: request.preview);
        }

        public OrganizationAuthorityOperationResult RecordAuthorizationAudit(OrganizationAuthorizationResult authorization, string auditId = "", double worldTime = 0d, OrganizationVisibility visibility = OrganizationVisibility.Restricted)
        {
            long before = Revision;
            if (authorization == null)
            {
                return Fail(OrganizationAuthorizationStatus.InvalidRequest, "Authorization result is required for an audit record.", before);
            }

            string id = Normalize(auditId);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"organization-authority-audit.{Normalize(authorization.OperationId)}.{Normalize(authorization.ActorPersonId)}.{before + 1L}";
            }

            if (auditsById.ContainsKey(id))
            {
                return Succeed(null, null, authorization, "Authority audit already recorded.", before, before, duplicate: true);
            }

            OrganizationAuthorityRuntimeSaveData rollback = CreateSaveData();
            OrganizationAuthorityAuditRecordData record = new OrganizationAuthorityAuditRecordData
            {
                auditId = id,
                operationId = Normalize(authorization.OperationId),
                actionDefinitionId = Normalize(authorization.ActionDefinitionId),
                organizationId = Normalize(authorization.OrganizationId),
                actorPersonId = Normalize(authorization.ActorPersonId),
                status = authorization.Status,
                requiredPermissionIds = Clean(authorization.RequiredPermissionIds),
                sourceAuthorityIds = Clean(authorization.MatchedSources.Select(item => item.sourceAuthorityId)),
                worldTime = worldTime,
                visibility = visibility,
                message = Normalize(authorization.Message),
                revision = 1L
            };

            auditsById.Add(record.auditId, record);
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationAuthorizationStatus.PersistenceInvalid, validationFailure, before);
            }

            Touch();
            return Succeed(null, null, authorization, "Authority audit recorded.", before, Revision);
        }

        public OrganizationEffectiveAuthoritySnapshot QueryEffectiveAuthority(string personId, string organizationId, double worldTime, bool includeDelegated = true, bool privileged = false)
        {
            return new OrganizationEffectiveAuthoritySnapshot(Normalize(personId), Normalize(organizationId), ResolveEffectivePermissionSources(personId, organizationId, worldTime, includeDelegated, privileged), Revision);
        }

        public IReadOnlyList<OrganizationAuthoritySnapshot> QueryGrants(string personId = "", string organizationId = "", bool activeOnly = false, double worldTime = 0d)
        {
            string person = Normalize(personId);
            string organization = Normalize(organizationId);
            return grantsById.Values
                .Where(item => (string.IsNullOrWhiteSpace(person) || string.Equals(item.granteePersonId, person, StringComparison.Ordinal))
                    && (string.IsNullOrWhiteSpace(organization) || string.Equals(item.organizationId, organization, StringComparison.Ordinal))
                    && (!activeOnly || item.IsActiveAt(worldTime)))
                .OrderBy(item => item.organizationId, StringComparer.Ordinal)
                .ThenBy(item => item.granteePersonId, StringComparer.Ordinal)
                .ThenBy(item => item.grantId, StringComparer.Ordinal)
                .Select(item => new OrganizationAuthoritySnapshot(item))
                .ToArray();
        }

        public bool TryGetGrant(string grantId, out OrganizationAuthoritySnapshot snapshot)
        {
            if (grantsById.TryGetValue(Normalize(grantId), out OrganizationAuthorityGrantRecordData record))
            {
                snapshot = new OrganizationAuthoritySnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public OrganizationAuthorityProjection ProjectGrant(string grantId, string requesterPersonId, bool privileged = false)
        {
            if (!grantsById.TryGetValue(Normalize(grantId), out OrganizationAuthorityGrantRecordData record))
            {
                return new OrganizationAuthorityProjection(OrganizationAuthorityProjectionAccess.Denied, null, "Grant does not exist.");
            }

            string requester = Normalize(requesterPersonId);
            if (privileged || string.Equals(record.granteePersonId, requester, StringComparison.Ordinal) || string.Equals(record.grantorPersonId, requester, StringComparison.Ordinal) || record.visibility == OrganizationVisibility.Public)
            {
                return new OrganizationAuthorityProjection(OrganizationAuthorityProjectionAccess.Full, new OrganizationAuthoritySnapshot(record), "Authority grant visible.");
            }

            if (record.visibility == OrganizationVisibility.Hidden)
            {
                return new OrganizationAuthorityProjection(OrganizationAuthorityProjectionAccess.Concealed, null, "Authority grant concealed.");
            }

            OrganizationAuthorityGrantRecordData redacted = record.Clone();
            redacted.grantorPersonId = string.Empty;
            redacted.sourceGrantId = string.Empty;
            redacted.sourceMembershipId = string.Empty;
            redacted.sourceRankAssignmentId = string.Empty;
            redacted.sourceOfficeAssignmentId = string.Empty;
            redacted.provenanceId = string.Empty;
            return new OrganizationAuthorityProjection(OrganizationAuthorityProjectionAccess.Redacted, new OrganizationAuthoritySnapshot(redacted), "Authority grant redacted.");
        }

        public OrganizationAuthorityRuntimeSaveData CreateSaveData()
        {
            return new OrganizationAuthorityRuntimeSaveData
            {
                schemaVersion = OrganizationAuthorityRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                grants = grantsById.Values.OrderBy(item => item.grantId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                approvals = approvalsById.Values.OrderBy(item => item.approvalId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                audits = auditsById.Values.OrderBy(item => item.auditId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToList()
            };
        }

        public OrganizationAuthorityOperationResult RestoreFromSaveData(OrganizationAuthorityRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, string world, IEnumerable<string> persons = null, IEnumerable<string> organizationIds = null, bool restoring = true)
        {
            long before = Revision;
            OrganizationAuthorityRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry previousRegistry = registry;
            OrganizationRuntime previousOrganizations = organizations;
            OrganizationMembershipRuntime previousMemberships = memberships;
            string previousWorld = worldId;
            HashSet<string> previousPersons = new HashSet<string>(knownPersonIds, StringComparer.Ordinal);
            HashSet<string> previousOrganizationIds = new HashSet<string>(knownOrganizationIds, StringComparer.Ordinal);

            Configure(definitionRegistry, organizationRuntime, membershipRuntime, world, persons, organizationIds);
            if (!ValidateSaveData(saveData, registry, organizations, memberships, worldId, knownPersonIds, knownOrganizationIds, out string failure))
            {
                registry = previousRegistry;
                organizations = previousOrganizations;
                memberships = previousMemberships;
                worldId = previousWorld;
                knownPersonIds = previousPersons;
                knownOrganizationIds = previousOrganizationIds;
                return Fail(OrganizationAuthorizationStatus.RestoreFailed, failure, before);
            }

            try
            {
                RestoreInternal(saveData);
                IsDirty = false;
                return Succeed(null, null, null, "Organization authority state restored.", before, Revision);
            }
            catch (Exception ex)
            {
                RestoreInternal(rollback);
                registry = previousRegistry;
                organizations = previousOrganizations;
                memberships = previousMemberships;
                worldId = previousWorld;
                knownPersonIds = previousPersons;
                knownOrganizationIds = previousOrganizationIds;
                return Fail(OrganizationAuthorizationStatus.RestoreFailed, ex.Message, before);
            }
        }

        public OrganizationAuthorityOperationResult RestoreCheckpoint(OrganizationAuthorityRuntimeSaveData saveData)
        {
            return RestoreFromSaveData(saveData, registry, organizations, memberships, worldId, knownPersonIds, knownOrganizationIds, restoring: true);
        }

        public static bool ValidateSaveData(OrganizationAuthorityRuntimeSaveData saveData, DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationMembershipRuntime memberships, string world, IEnumerable<string> persons, IEnumerable<string> organizationIds, out string failure)
        {
            failure = string.Empty;
            if (!ValidateSaveShape(saveData, out failure))
            {
                return false;
            }

            OrganizationAuthorityRuntime runtime = new OrganizationAuthorityRuntime();
            runtime.Configure(registry, organizations, memberships, world, persons, organizationIds);
            runtime.RestoreInternal((saveData ?? new OrganizationAuthorityRuntimeSaveData()).Clone());
            return runtime.ValidateCurrent(out failure);
        }

        private static bool ValidateSaveShape(OrganizationAuthorityRuntimeSaveData saveData, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Organization authority save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != OrganizationAuthorityRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported organization authority schema version {saveData.schemaVersion}.";
                return false;
            }

            return !HasDuplicateIds(saveData.grants?.Select(item => item?.grantId), "grant", out failure)
                && !HasDuplicateIds(saveData.approvals?.Select(item => item?.approvalId), "approval", out failure)
                && !HasDuplicateIds(saveData.audits?.Select(item => item?.auditId), "audit", out failure)
                && !HasDuplicateIds(saveData.transactions?.Select(item => item?.transactionId), "transaction", out failure);
        }

        private bool ValidateCurrent(out string failure)
        {
            failure = string.Empty;
            foreach (OrganizationAuthorityGrantRecordData grant in grantsById.Values)
            {
                if (grant == null || string.IsNullOrWhiteSpace(grant.grantId))
                {
                    failure = "Organization authority grants require stable IDs.";
                    return false;
                }

                if (!knownPersonIds.Contains(grant.granteePersonId))
                {
                    failure = $"Authority grant '{grant.grantId}' references missing grantee Person '{grant.granteePersonId}'.";
                    return false;
                }

                if (!TryKnownOrganization(grant.organizationId, out _))
                {
                    failure = $"Authority grant '{grant.grantId}' references missing Organization '{grant.organizationId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(grant.authorityRoleDefinitionId) && !TryGetDefinition(grant.authorityRoleDefinitionId, out OrganizationAuthorityRoleDefinition _))
                {
                    failure = $"Authority grant '{grant.grantId}' references missing Authority Role '{grant.authorityRoleDefinitionId}'.";
                    return false;
                }

                foreach (string permissionId in Clean(grant.permissionDefinitionIds))
                {
                    if (!TryGetDefinition(permissionId, out OrganizationPermissionDefinition _))
                    {
                        failure = $"Authority grant '{grant.grantId}' references missing Permission '{permissionId}'.";
                        return false;
                    }
                }

                if (grant.sourceType == OrganizationAuthoritySourceType.Delegation && !string.IsNullOrWhiteSpace(grant.sourceGrantId) && !grantsById.ContainsKey(grant.sourceGrantId))
                {
                    failure = $"Delegation grant '{grant.grantId}' references missing source grant '{grant.sourceGrantId}'.";
                    return false;
                }
            }

            foreach (OrganizationAuthorityApprovalRecordData approval in approvalsById.Values)
            {
                if (approval == null || string.IsNullOrWhiteSpace(approval.approvalId) || string.IsNullOrWhiteSpace(approval.operationId))
                {
                    failure = "Organization authority approvals require stable approval and operation IDs.";
                    return false;
                }

                if (!knownPersonIds.Contains(approval.approverPersonId))
                {
                    failure = $"Approval '{approval.approvalId}' references missing approver Person '{approval.approverPersonId}'.";
                    return false;
                }

                if (!TryKnownOrganization(approval.organizationId, out _))
                {
                    failure = $"Approval '{approval.approvalId}' references missing Organization '{approval.organizationId}'.";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateGrantRequest(OrganizationAuthorityGrantRequest request, string grantId, long before, out OrganizationAuthorityOperationResult failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(grantId) || string.IsNullOrWhiteSpace(request.organizationId) || string.IsNullOrWhiteSpace(request.granteePersonId))
            {
                failure = Fail(OrganizationAuthorizationStatus.InvalidRequest, "Grant, organization, and grantee IDs are required.", before);
                return false;
            }

            if (!knownPersonIds.Contains(Normalize(request.granteePersonId)))
            {
                failure = Fail(OrganizationAuthorizationStatus.MissingActor, $"Grantee Person '{request.granteePersonId}' is not known.", before);
                return false;
            }

            if (!TryActiveOrganization(request.organizationId, out _))
            {
                failure = Fail(OrganizationAuthorizationStatus.MissingOrganization, $"Organization '{request.organizationId}' is not active.", before);
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.authorityRoleDefinitionId) && Clean(request.permissionDefinitionIds).Length == 0)
            {
                failure = Fail(OrganizationAuthorizationStatus.MissingPermission, "Grant requires an authority role or explicit permissions.", before);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.authorityRoleDefinitionId) && !TryGetDefinition(request.authorityRoleDefinitionId, out OrganizationAuthorityRoleDefinition _))
            {
                failure = Fail(OrganizationAuthorizationStatus.MissingPermission, $"Authority role '{request.authorityRoleDefinitionId}' is missing.", before);
                return false;
            }

            foreach (string permissionId in Clean(request.permissionDefinitionIds))
            {
                if (!TryGetDefinition(permissionId, out OrganizationPermissionDefinition _))
                {
                    failure = Fail(OrganizationAuthorizationStatus.MissingPermission, $"Permission '{permissionId}' is missing.", before);
                    return false;
                }
            }

            if (request.expirationWorldTime >= 0d && request.expirationWorldTime <= request.startWorldTime)
            {
                failure = Fail(OrganizationAuthorizationStatus.InvalidRequest, "Grant expiration must be after its start time.", before);
                return false;
            }

            return true;
        }

        private IEnumerable<OrganizationEffectivePermissionSourceData> ResolveEffectivePermissionSources(string personId, string organizationId, double worldTime, bool includeDelegated, bool privileged)
        {
            string person = Normalize(personId);
            string organization = Normalize(organizationId);
            List<OrganizationEffectivePermissionSourceData> sources = new List<OrganizationEffectivePermissionSourceData>();

            if (memberships != null)
            {
                foreach (OrganizationMembershipSnapshot membership in memberships.QueryMemberships(person, string.Empty, activeOnly: true))
                {
                    if (!TryKnownOrganization(membership.OrganizationId, out OrganizationSnapshot membershipOrganization))
                    {
                        continue;
                    }

                    AddBoundSources(sources, membership.PersonId, membership.OrganizationId, membership.MembershipId, OrganizationAuthorityBindingSourceType.MembershipDefinition, membership.Data.membershipDefinitionId, OrganizationAuthoritySourceType.MembershipDefinition, membershipOrganization, worldTime, delegated: false);
                    foreach (OrganizationRankAssignmentRecordData rank in membership.RankAssignments.Where(item => item.IsActive))
                    {
                        AddBoundSources(sources, membership.PersonId, membership.OrganizationId, rank.rankAssignmentId, OrganizationAuthorityBindingSourceType.RankDefinition, rank.rankDefinitionId, OrganizationAuthoritySourceType.RankDefinition, membershipOrganization, worldTime, delegated: false);
                    }

                    foreach (OrganizationOfficeAssignmentRecordData assignment in membership.OfficeAssignments.Where(item => item.IsActive))
                    {
                        OrganizationOfficeSnapshot office = memberships.Offices.FirstOrDefault(item => string.Equals(item.OfficeId, assignment.officeId, StringComparison.Ordinal));
                        if (office != null)
                        {
                            AddBoundSources(sources, membership.PersonId, membership.OrganizationId, assignment.officeAssignmentId, assignment.acting ? OrganizationAuthorityBindingSourceType.ActingOfficeAssignment : OrganizationAuthorityBindingSourceType.OfficeDefinition, office.Data.officeDefinitionId, assignment.acting ? OrganizationAuthoritySourceType.OfficeAssignment : OrganizationAuthoritySourceType.OfficeDefinition, membershipOrganization, worldTime, delegated: false, officeId: office.OfficeId, officeAssignmentId: assignment.officeAssignmentId);
                        }
                    }
                }
            }

            foreach (OrganizationAuthorityGrantRecordData grant in grantsById.Values.Where(item => string.Equals(item.granteePersonId, person, StringComparison.Ordinal)))
            {
                if (grant.sourceType == OrganizationAuthoritySourceType.Delegation && !includeDelegated)
                {
                    continue;
                }

                if (!grant.IsActiveAt(worldTime))
                {
                    continue;
                }

                if (!SourceScopeMatches(grant.scope, OrganizationAuthorityScopeData.ForOrganization(organization), organization))
                {
                    continue;
                }

                AddRoleAndPermissionSources(sources, grant.grantId, grant.sourceType, grant.grantId, grant.authorityRoleDefinitionId, grant.permissionDefinitionIds, grant.scope, grant.delegationPolicy, grant.visibility, delegated: grant.sourceType == OrganizationAuthoritySourceType.Delegation, priorityOverride: null);
            }

            return sources
                .OrderBy(item => item.permissionDefinitionId, StringComparer.Ordinal)
                .ThenByDescending(item => item.priority)
                .ThenBy(item => item.sourceAuthorityId, StringComparer.Ordinal)
                .ToArray();
        }

        private void AddBoundSources(List<OrganizationEffectivePermissionSourceData> sources, string personId, string organizationId, string sourceRecordId, OrganizationAuthorityBindingSourceType bindingType, string sourceDefinitionId, OrganizationAuthoritySourceType sourceType, OrganizationSnapshot sourceOrganization, double worldTime, bool delegated, string officeId = "", string officeAssignmentId = "")
        {
            foreach (OrganizationAuthorityBindingDefinition binding in Definitions<OrganizationAuthorityBindingDefinition>()
                .Where(item => item.SourceType == bindingType && string.Equals(item.SourceDefinitionId, sourceDefinitionId, StringComparison.Ordinal))
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(binding.ScopedOrganizationDefinitionId) && !string.Equals(binding.ScopedOrganizationDefinitionId, sourceOrganization.DefinitionId, StringComparison.Ordinal))
                {
                    continue;
                }

                OrganizationAuthorityScopeData scope = new OrganizationAuthorityScopeData
                {
                    scopeType = binding.ScopeType,
                    scopeMatch = binding.ScopeMatch,
                    organizationId = organizationId,
                    officeId = officeId,
                    actionDefinitionId = string.Empty,
                    visibility = binding.Visibility
                };
                AddRoleAndPermissionSources(sources, binding.Id, sourceType, sourceRecordId, binding.AuthorityRoleDefinitionId, Array.Empty<string>(), scope, OrganizationAuthorityDelegationPolicy.NonDelegable, binding.Visibility, delegated, binding.Priority);
            }
        }

        private void AddRoleAndPermissionSources(List<OrganizationEffectivePermissionSourceData> sources, string sourceAuthorityId, OrganizationAuthoritySourceType sourceType, string sourceRecordId, string roleDefinitionId, IEnumerable<string> directPermissionIds, OrganizationAuthorityScopeData scope, OrganizationAuthorityDelegationPolicy delegationPolicy, OrganizationVisibility visibility, bool delegated, int? priorityOverride)
        {
            int priority = priorityOverride ?? 100;
            OrganizationAuthorityDelegationPolicy roleDelegation = delegationPolicy;
            if (!string.IsNullOrWhiteSpace(roleDefinitionId) && TryGetDefinition(roleDefinitionId, out OrganizationAuthorityRoleDefinition role))
            {
                priority = priorityOverride ?? role.Priority;
                roleDelegation = role.DelegationPolicy;
                foreach (string permissionId in role.GrantedPermissionIds)
                {
                    AddSource(sources, sourceAuthorityId, sourceType, sourceRecordId, role.Id, permissionId, scope, delegated, denied: false, priority, visibility, roleDelegation);
                }

                foreach (string permissionId in role.DeniedPermissionIds)
                {
                    AddSource(sources, sourceAuthorityId, sourceType, sourceRecordId, role.Id, permissionId, scope, delegated, denied: true, priority, visibility, roleDelegation);
                }
            }

            foreach (string permissionId in Clean(directPermissionIds))
            {
                AddSource(sources, sourceAuthorityId, sourceType, sourceRecordId, roleDefinitionId, permissionId, scope, delegated, denied: false, priority, visibility, roleDelegation);
            }
        }

        private static void AddSource(List<OrganizationEffectivePermissionSourceData> sources, string sourceAuthorityId, OrganizationAuthoritySourceType sourceType, string sourceRecordId, string roleId, string permissionId, OrganizationAuthorityScopeData scope, bool delegated, bool denied, int priority, OrganizationVisibility visibility, OrganizationAuthorityDelegationPolicy delegationPolicy)
        {
            sources.Add(new OrganizationEffectivePermissionSourceData
            {
                sourceAuthorityId = sourceAuthorityId ?? string.Empty,
                sourceType = sourceType,
                sourceRecordId = sourceRecordId ?? string.Empty,
                authorityRoleDefinitionId = roleId ?? string.Empty,
                permissionDefinitionId = Normalize(permissionId),
                scope = scope?.Clone() ?? new OrganizationAuthorityScopeData(),
                delegated = delegated,
                denied = denied,
                priority = priority,
                visibility = visibility,
                message = delegationPolicy.ToString()
            });
        }

        private bool HasJointApproval(OrganizationAuthorizationRequest request, InstitutionalActionDefinition action, string[] requiredPermissions, double worldTime, out string[] approvalIds)
        {
            approvalIds = Array.Empty<string>();
            int required = Math.Max(2, action?.RequiredApprovalCount ?? 2);
            string operation = Normalize(request.operationId);
            string actor = Normalize(request.actorPersonId);
            string[] explicitApprovers = Clean(request.approvalPersonIds);
            IEnumerable<string> candidateIds = explicitApprovers.Length > 0
                ? explicitApprovers
                : approvalsById.Values.Where(item => item.IsActiveAt(worldTime) && string.Equals(item.operationId, operation, StringComparison.Ordinal)).Select(item => item.approverPersonId);
            string[] distinct = candidateIds.Where(item => !string.Equals(item, actor, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();

            List<string> matchingApprovalIds = new List<string>();
            foreach (string approver in distinct)
            {
                OrganizationAuthorizationResult approverResult = EvaluateAuthorization(new OrganizationAuthorizationRequest
                {
                    actorPersonId = approver,
                    organizationId = request.organizationId,
                    requiredPermissionIds = requiredPermissions,
                    permissionPolicy = OrganizationPermissionCombinationPolicy.AllRequiredPermissions,
                    scope = request.scope,
                    allowDelegatedAuthority = request.allowDelegatedAuthority,
                    worldTime = worldTime,
                    privilegedDiagnostics = request.privilegedDiagnostics
                });

                if (!approverResult.Succeeded)
                {
                    continue;
                }

                OrganizationAuthorityApprovalRecordData approval = approvalsById.Values
                    .Where(item => item.IsActiveAt(worldTime)
                        && string.Equals(item.operationId, operation, StringComparison.Ordinal)
                        && string.Equals(item.approverPersonId, approver, StringComparison.Ordinal))
                    .OrderBy(item => item.approvalId, StringComparer.Ordinal)
                    .FirstOrDefault();
                matchingApprovalIds.Add(approval?.approvalId ?? $"inline:{approver}");
                if (matchingApprovalIds.Count >= required)
                {
                    approvalIds = matchingApprovalIds.OrderBy(item => item, StringComparer.Ordinal).ToArray();
                    return true;
                }
            }

            return false;
        }

        private bool SourceAllowsDelegation(OrganizationEffectivePermissionSourceData source, IEnumerable<string> requestedPermissionIds, out string failure)
        {
            failure = string.Empty;
            if (source == null)
            {
                failure = "Source authority is missing.";
                return false;
            }

            foreach (string permissionId in Clean(requestedPermissionIds))
            {
                if (TryGetDefinition(permissionId, out OrganizationPermissionDefinition permission) && !permission.DelegationAllowed)
                {
                    failure = $"Permission '{permissionId}' does not allow delegation.";
                    return false;
                }
            }

            if (string.Equals(source.message, OrganizationAuthorityDelegationPolicy.NonDelegable.ToString(), StringComparison.Ordinal))
            {
                failure = $"Authority source '{source.sourceAuthorityId}' is non-delegable.";
                return false;
            }

            if (source.delegated && !string.Equals(source.message, OrganizationAuthorityDelegationPolicy.Redelegable.ToString(), StringComparison.Ordinal))
            {
                failure = $"Delegated authority source '{source.sourceAuthorityId}' may not be redelegated.";
                return false;
            }

            return true;
        }

        private bool ValidateDelegationChain(string sourceGrantId, string delegatorId, string recipientId, out int depth, out string failure)
        {
            depth = 0;
            failure = string.Empty;
            if (string.Equals(Normalize(delegatorId), Normalize(recipientId), StringComparison.Ordinal))
            {
                failure = "Delegation cycles to the delegator are not allowed.";
                return false;
            }

            string current = Normalize(sourceGrantId);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && grantsById.TryGetValue(current, out OrganizationAuthorityGrantRecordData grant))
            {
                if (!visited.Add(current))
                {
                    failure = "Delegation chain contains a cycle.";
                    return false;
                }

                if (string.Equals(grant.granteePersonId, Normalize(recipientId), StringComparison.Ordinal))
                {
                    failure = "Delegation would create a grantee cycle.";
                    return false;
                }

                depth = Math.Max(depth, grant.delegationDepth);
                if (visited.Count > MaximumDelegationDepth)
                {
                    failure = "Delegation chain exceeds the supported depth.";
                    return false;
                }

                current = Normalize(grant.sourceGrantId);
            }

            return true;
        }

        private bool IsNarrowerOrEqualScope(OrganizationAuthorityScopeData requested, IEnumerable<OrganizationAuthorityScopeData> sourceScopes, string organizationId)
        {
            OrganizationAuthorityScopeData requestScope = NormalizeScope(requested, organizationId);
            return (sourceScopes ?? Array.Empty<OrganizationAuthorityScopeData>()).Any(source => SourceScopeMatches(source, requestScope, organizationId));
        }

        private bool SourceScopeMatches(OrganizationAuthorityScopeData source, OrganizationAuthorityScopeData requested, string fallbackOrganizationId)
        {
            source = NormalizeScope(source, fallbackOrganizationId);
            requested = NormalizeScope(requested, fallbackOrganizationId);
            if (source.scopeType == OrganizationAuthorityScopeType.EntireOrganization)
            {
                return string.Equals(source.organizationId, requested.organizationId, StringComparison.Ordinal);
            }

            if (source.scopeType == OrganizationAuthorityScopeType.OrganizationBranch)
            {
                return string.Equals(source.organizationId, requested.organizationId, StringComparison.Ordinal)
                    || string.Equals(source.branchOrganizationId, requested.organizationId, StringComparison.Ordinal);
            }

            if (source.scopeType == OrganizationAuthorityScopeType.SpecificOrganizationSubtree)
            {
                return string.Equals(source.organizationId, requested.organizationId, StringComparison.Ordinal)
                    || IsDescendantOrganization(requested.organizationId, source.organizationId);
            }

            if (!string.Equals(source.organizationId, requested.organizationId, StringComparison.Ordinal))
            {
                return false;
            }

            return source.scopeType switch
            {
                OrganizationAuthorityScopeType.SpecificOffice => string.IsNullOrWhiteSpace(source.officeId) || string.Equals(source.officeId, requested.officeId, StringComparison.Ordinal),
                OrganizationAuthorityScopeType.SpecificRankTrack => string.IsNullOrWhiteSpace(source.rankTrackDefinitionId) || string.Equals(source.rankTrackDefinitionId, requested.rankTrackDefinitionId, StringComparison.Ordinal),
                OrganizationAuthorityScopeType.SpecificMembershipType => string.IsNullOrWhiteSpace(source.membershipDefinitionId) || string.Equals(source.membershipDefinitionId, requested.membershipDefinitionId, StringComparison.Ordinal),
                OrganizationAuthorityScopeType.SpecificPerson => string.IsNullOrWhiteSpace(source.personId) || string.Equals(source.personId, requested.personId, StringComparison.Ordinal),
                OrganizationAuthorityScopeType.SpecificRecord => string.IsNullOrWhiteSpace(source.recordId) || string.Equals(source.recordId, requested.recordId, StringComparison.Ordinal),
                OrganizationAuthorityScopeType.SpecificAction => string.IsNullOrWhiteSpace(source.actionDefinitionId) || string.Equals(source.actionDefinitionId, requested.actionDefinitionId, StringComparison.Ordinal),
                _ => true
            };
        }

        private bool IsDescendantOrganization(string candidateChildId, string ancestorId)
        {
            string child = Normalize(candidateChildId);
            string ancestor = Normalize(ancestorId);
            if (string.IsNullOrWhiteSpace(child) || string.IsNullOrWhiteSpace(ancestor) || organizations == null)
            {
                return false;
            }

            Queue<string> queue = new Queue<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(child);
            while (queue.Count > 0 && visited.Count <= MaximumHierarchyTraversal)
            {
                string current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                OrganizationSnapshot snapshot = organizations.Snapshots.FirstOrDefault(item => string.Equals(item.OrganizationId, current, StringComparison.Ordinal));
                if (snapshot == null)
                {
                    continue;
                }

                foreach (OrganizationLinkRecordData link in snapshot.Links.Where(item => item.IsActive && item.kind == OrganizationLinkKind.Parent))
                {
                    if (string.Equals(link.targetOrganizationId, ancestor, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    queue.Enqueue(link.targetOrganizationId);
                }
            }

            return false;
        }

        private void InvalidateDependentDelegations(string sourceGrantId, double worldTime, OrganizationAuthorityGrantLifecycleState sourceState)
        {
            foreach (OrganizationAuthorityGrantRecordData grant in grantsById.Values.Where(item => item.lifecycleState == OrganizationAuthorityGrantLifecycleState.Active && string.Equals(item.sourceGrantId, sourceGrantId, StringComparison.Ordinal)))
            {
                grant.lifecycleState = sourceState == OrganizationAuthorityGrantLifecycleState.Suspended ? OrganizationAuthorityGrantLifecycleState.Suspended : OrganizationAuthorityGrantLifecycleState.Revoked;
                grant.revokedWorldTime = sourceState == OrganizationAuthorityGrantLifecycleState.Revoked ? worldTime : grant.revokedWorldTime;
                grant.revocationReason = $"Source authority '{sourceGrantId}' became {sourceState}.";
                grant.revision++;
                InvalidateDependentDelegations(grant.grantId, worldTime, grant.lifecycleState);
            }
        }

        private bool TryActiveOrganization(string organizationId, out OrganizationSnapshot snapshot)
        {
            if (!TryKnownOrganization(organizationId, out snapshot))
            {
                return false;
            }

            return snapshot.LifecycleState == OrganizationLifecycleState.Active;
        }

        private bool TryKnownOrganization(string organizationId, out OrganizationSnapshot snapshot)
        {
            string id = Normalize(organizationId);
            if (organizations != null && organizations.TryGetSnapshot(id, out snapshot))
            {
                return true;
            }

            snapshot = null;
            return knownOrganizationIds.Contains(id);
        }

        private bool TryGetDefinition<TDefinition>(string definitionId, out TDefinition definition)
            where TDefinition : class, IGameDefinition
        {
            definition = null;
            return registry != null && registry.TryGet(Normalize(definitionId), out definition);
        }

        private IEnumerable<TDefinition> Definitions<TDefinition>()
            where TDefinition : class, IGameDefinition
        {
            return registry?.DefinitionsById?.Values?.OfType<TDefinition>() ?? Array.Empty<TDefinition>();
        }

        private void RestoreInternal(OrganizationAuthorityRuntimeSaveData saveData)
        {
            grantsById.Clear();
            approvalsById.Clear();
            auditsById.Clear();
            transactionsById.Clear();

            foreach (OrganizationAuthorityGrantRecordData grant in saveData?.grants ?? new List<OrganizationAuthorityGrantRecordData>())
            {
                grantsById[Normalize(grant.grantId)] = grant.Clone();
            }

            foreach (OrganizationAuthorityApprovalRecordData approval in saveData?.approvals ?? new List<OrganizationAuthorityApprovalRecordData>())
            {
                approvalsById[Normalize(approval.approvalId)] = approval.Clone();
            }

            foreach (OrganizationAuthorityAuditRecordData audit in saveData?.audits ?? new List<OrganizationAuthorityAuditRecordData>())
            {
                auditsById[Normalize(audit.auditId)] = audit.Clone();
            }

            foreach (OrganizationAuthorityTransactionRecordData transaction in saveData?.transactions ?? new List<OrganizationAuthorityTransactionRecordData>())
            {
                transactionsById[Normalize(transaction.transactionId)] = transaction.Clone();
            }

            Revision = Math.Max(0L, saveData?.revision ?? 0L);
            worldId = saveData?.worldId ?? worldId;
        }

        private bool TryDuplicate(string transactionId, string subjectId, string operation, long before, out OrganizationAuthorityOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!transactionsById.TryGetValue(transactionId, out OrganizationAuthorityTransactionRecordData previous))
            {
                return false;
            }

            if (!string.Equals(previous.operation, operation, StringComparison.Ordinal) || !string.Equals(previous.subjectId, subjectId, StringComparison.Ordinal))
            {
                result = Fail(OrganizationAuthorizationStatus.InvalidRequest, $"Transaction '{transactionId}' was already used for a different {previous.operation} operation.", before);
                return true;
            }

            OrganizationAuthoritySnapshot grant = grantsById.TryGetValue(previous.subjectId, out OrganizationAuthorityGrantRecordData grantRecord) ? new OrganizationAuthoritySnapshot(grantRecord) : null;
            OrganizationApprovalSnapshot approval = approvalsById.TryGetValue(previous.subjectId, out OrganizationAuthorityApprovalRecordData approvalRecord) ? new OrganizationApprovalSnapshot(approvalRecord) : null;
            result = Succeed(grant, approval, null, "Duplicate transaction ignored.", before, before, duplicate: true);
            return true;
        }

        private void CompleteTransaction(string transactionId, string operation, string subjectId)
        {
            transactionId = Normalize(transactionId);
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                transactionsById[transactionId] = new OrganizationAuthorityTransactionRecordData
                {
                    transactionId = transactionId,
                    operation = operation ?? string.Empty,
                    subjectId = subjectId ?? string.Empty,
                    revision = Revision + 1L
                };
            }
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private static OrganizationAuthorityOperationResult Succeed(OrganizationAuthoritySnapshot grant, OrganizationApprovalSnapshot approval, OrganizationAuthorizationResult authorization, string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return OrganizationAuthorityOperationResult.Success(grant, approval, authorization, message, before, after, preview, duplicate);
        }

        private static OrganizationAuthorityOperationResult Fail(OrganizationAuthorizationStatus status, string message, long before)
        {
            return OrganizationAuthorityOperationResult.Failure(status, message, before);
        }

        private static OrganizationAuthorizationResult Auth(bool succeeded, OrganizationAuthorizationStatus status, OrganizationAuthorizationRequest request, IEnumerable<string> requiredPermissions, IEnumerable<OrganizationEffectivePermissionSourceData> sources, IEnumerable<string> missing, string message, long before, long after, IEnumerable<string> approvals = null, bool preview = false)
        {
            return new OrganizationAuthorizationResult(succeeded, status, request?.operationId, request?.actionDefinitionId, request?.actorPersonId, request?.organizationId, requiredPermissions, sources, missing, approvals ?? Array.Empty<string>(), message, before, after, preview);
        }

        private static OrganizationAuthorityScopeData NormalizeScope(OrganizationAuthorityScopeData scope, string fallbackOrganizationId)
        {
            OrganizationAuthorityScopeData result = scope?.Clone() ?? OrganizationAuthorityScopeData.ForOrganization(fallbackOrganizationId);
            result.organizationId = Normalize(string.IsNullOrWhiteSpace(result.organizationId) ? fallbackOrganizationId : result.organizationId);
            result.branchOrganizationId = Normalize(result.branchOrganizationId);
            result.personId = Normalize(result.personId);
            result.officeId = Normalize(result.officeId);
            result.rankTrackDefinitionId = Normalize(result.rankTrackDefinitionId);
            result.membershipDefinitionId = Normalize(result.membershipDefinitionId);
            result.placeId = Normalize(result.placeId);
            result.propertyReferenceId = Normalize(result.propertyReferenceId);
            result.recordId = Normalize(result.recordId);
            result.actionDefinitionId = Normalize(result.actionDefinitionId);
            return result;
        }

        private static bool HasDuplicateIds(IEnumerable<string> ids, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                string normalized = Normalize(id);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!seen.Add(normalized))
                {
                    failure = $"Duplicate organization authority {label} ID '{normalized}'.";
                    return true;
                }
            }

            return false;
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return OrganizationModelUtility.Clean(values);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
