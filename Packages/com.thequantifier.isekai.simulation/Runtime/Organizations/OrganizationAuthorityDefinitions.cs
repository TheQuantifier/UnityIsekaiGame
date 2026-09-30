using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    [CreateAssetMenu(fileName = "OrganizationPermissionDefinition", menuName = "Unity Isekai Game/Organizations/Permission Definition")]
    public sealed class OrganizationPermissionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string permissionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationPermissionCategory category = OrganizationPermissionCategory.Custom;
        [SerializeField] private string[] supportedOrganizationDefinitionIds = Array.Empty<string>();
        [SerializeField] private OrganizationCategory[] supportedOrganizationCategories = Array.Empty<OrganizationCategory>();
        [SerializeField] private OrganizationAuthorityScopeType[] supportedScopeTypes = Array.Empty<OrganizationAuthorityScopeType>();
        [SerializeField] private string[] supportedTargetTypes = Array.Empty<string>();
        [SerializeField] private bool delegationAllowed;
        [SerializeField] private bool redelegationAllowed;
        [SerializeField] private bool jointApprovalAllowed;
        [SerializeField] private bool suspensionAllowed = true;
        [SerializeField] private bool explicitDenialAllowed = true;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => permissionDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationPermissionCategory Category => category;
        public IReadOnlyList<string> SupportedOrganizationDefinitionIds => Clean(supportedOrganizationDefinitionIds);
        public IReadOnlyList<OrganizationCategory> SupportedOrganizationCategories => CleanCategories(supportedOrganizationCategories);
        public IReadOnlyList<OrganizationAuthorityScopeType> SupportedScopeTypes => CleanScopes(supportedScopeTypes);
        public IReadOnlyList<string> SupportedTargetTypes => Clean(supportedTargetTypes);
        public bool DelegationAllowed => delegationAllowed;
        public bool RedelegationAllowed => redelegationAllowed;
        public bool JointApprovalAllowed => jointApprovalAllowed;
        public bool SuspensionAllowed => suspensionAllowed;
        public bool ExplicitDenialAllowed => explicitDenialAllowed;
        public OrganizationVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string id,
            string name,
            OrganizationPermissionCategory permissionCategory,
            IEnumerable<string> organizationDefinitions = null,
            IEnumerable<OrganizationCategory> organizationCategories = null,
            IEnumerable<OrganizationAuthorityScopeType> scopeTypes = null,
            IEnumerable<string> targetTypes = null,
            bool canDelegate = false,
            bool canRedelegate = false,
            bool jointAllowed = false,
            bool canSuspend = true,
            bool canDeny = true,
            OrganizationVisibility permissionVisibility = OrganizationVisibility.Public,
            IEnumerable<string> tagIds = null)
        {
            permissionDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = permissionCategory;
            supportedOrganizationDefinitionIds = Clean(organizationDefinitions).ToArray();
            supportedOrganizationCategories = CleanCategories(organizationCategories).ToArray();
            supportedScopeTypes = CleanScopes(scopeTypes).ToArray();
            supportedTargetTypes = Clean(targetTypes).ToArray();
            delegationAllowed = canDelegate || canRedelegate;
            redelegationAllowed = canRedelegate;
            jointApprovalAllowed = jointAllowed;
            suspensionAllowed = canSuspend;
            explicitDenialAllowed = canDeny;
            visibility = permissionVisibility;
            tags = Clean(tagIds).ToArray();
            version = 1;
        }

        public bool AppliesTo(OrganizationDefinition organizationDefinition)
        {
            if (organizationDefinition == null)
            {
                return false;
            }

            return SupportedOrganizationDefinitionIds.Count == 0 && SupportedOrganizationCategories.Count == 0
                || SupportedOrganizationDefinitionIds.Contains(organizationDefinition.Id)
                || SupportedOrganizationCategories.Contains(organizationDefinition.Category);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Permission definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-permission.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Permission definition '{DisplayName}' should use the 'organization-permission.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(OrganizationPermissionCategory), category) || category == OrganizationPermissionCategory.Unknown)
            {
                report.AddError($"Organization Permission definition '{DisplayName}' has invalid category '{category}'.");
            }

            foreach (string organizationDefinitionId in SupportedOrganizationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(organizationDefinitionId, out IGameDefinition definition) || definition is not OrganizationDefinition)
                {
                    report.AddError($"Organization Permission definition '{DisplayName}' references missing Organization Definition '{organizationDefinitionId}'.");
                }
            }

            foreach (OrganizationCategory organizationCategory in SupportedOrganizationCategories)
            {
                if (!Enum.IsDefined(typeof(OrganizationCategory), organizationCategory) || organizationCategory == OrganizationCategory.Unknown)
                {
                    report.AddError($"Organization Permission definition '{DisplayName}' references invalid Organization category '{organizationCategory}'.");
                }
            }

            foreach (OrganizationAuthorityScopeType scopeType in SupportedScopeTypes)
            {
                if (!Enum.IsDefined(typeof(OrganizationAuthorityScopeType), scopeType) || scopeType == OrganizationAuthorityScopeType.Unknown)
                {
                    report.AddError($"Organization Permission definition '{DisplayName}' references invalid scope type '{scopeType}'.");
                }
            }

            if (redelegationAllowed && !delegationAllowed)
            {
                report.AddError($"Organization Permission definition '{DisplayName}' permits redelegation but not delegation.");
            }
        }

        internal static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        internal static OrganizationCategory[] CleanCategories(IEnumerable<OrganizationCategory> values)
        {
            return (values ?? Array.Empty<OrganizationCategory>())
                .Where(value => value != OrganizationCategory.Unknown)
                .Distinct()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
        }

        internal static OrganizationAuthorityScopeType[] CleanScopes(IEnumerable<OrganizationAuthorityScopeType> values)
        {
            return (values ?? Array.Empty<OrganizationAuthorityScopeType>())
                .Where(value => value != OrganizationAuthorityScopeType.Unknown)
                .Distinct()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
    }

    [CreateAssetMenu(fileName = "InstitutionalActionDefinition", menuName = "Unity Isekai Game/Organizations/Institutional Action Definition")]
    public class InstitutionalActionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string actionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InstitutionalActionCategory category = InstitutionalActionCategory.Custom;
        [SerializeField] private string[] requiredPermissionIds = Array.Empty<string>();
        [SerializeField] private OrganizationPermissionCombinationPolicy permissionPolicy = OrganizationPermissionCombinationPolicy.AllRequiredPermissions;
        [SerializeField] private string targetType;
        [SerializeField] private OrganizationAuthorityScopeType defaultScopeType = OrganizationAuthorityScopeType.EntireOrganization;
        [SerializeField] private OrganizationMembershipStatus requiredMembershipState = OrganizationMembershipStatus.Unknown;
        [SerializeField] private int requiredApprovalCount = 1;
        [SerializeField] private string[] requiredCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] requiredQualificationIds = Array.Empty<string>();
        [SerializeField] private bool selfAuthorizationAllowed = true;
        [SerializeField] private bool delegationMaySatisfy = true;
        [SerializeField] private bool externalActorsMayBeAuthorized;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Public;
        [SerializeField] private OrganizationAuthorityAuditPolicy auditPolicy = OrganizationAuthorityAuditPolicy.SuccessfulActions;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => actionDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public InstitutionalActionCategory Category => category;
        public IReadOnlyList<string> RequiredPermissionIds => OrganizationPermissionDefinition.Clean(requiredPermissionIds);
        public OrganizationPermissionCombinationPolicy PermissionPolicy => permissionPolicy;
        public string TargetType => targetType ?? string.Empty;
        public OrganizationAuthorityScopeType DefaultScopeType => defaultScopeType;
        public OrganizationMembershipStatus RequiredMembershipState => requiredMembershipState;
        public int RequiredApprovalCount => Math.Max(1, requiredApprovalCount);
        public IReadOnlyList<string> RequiredCapabilityIds => OrganizationPermissionDefinition.Clean(requiredCapabilityIds);
        public IReadOnlyList<string> RequiredQualificationIds => OrganizationPermissionDefinition.Clean(requiredQualificationIds);
        public bool SelfAuthorizationAllowed => selfAuthorizationAllowed;
        public bool DelegationMaySatisfy => delegationMaySatisfy;
        public bool ExternalActorsMayBeAuthorized => externalActorsMayBeAuthorized;
        public OrganizationVisibility Visibility => visibility;
        public OrganizationAuthorityAuditPolicy AuditPolicy => auditPolicy;
        public IReadOnlyList<string> TagIds => OrganizationPermissionDefinition.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string id,
            string name,
            InstitutionalActionCategory actionCategory,
            IEnumerable<string> permissions,
            OrganizationPermissionCombinationPolicy policy = OrganizationPermissionCombinationPolicy.AllRequiredPermissions,
            string actionTargetType = "",
            OrganizationAuthorityScopeType scopeType = OrganizationAuthorityScopeType.EntireOrganization,
            int approvals = 1,
            IEnumerable<string> capabilities = null,
            IEnumerable<string> qualifications = null,
            bool selfAllowed = true,
            bool delegationAllowed = true,
            bool externalAllowed = false,
            OrganizationVisibility actionVisibility = OrganizationVisibility.Public,
            OrganizationAuthorityAuditPolicy audit = OrganizationAuthorityAuditPolicy.SuccessfulActions,
            IEnumerable<string> tagIds = null)
        {
            actionDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = actionCategory;
            requiredPermissionIds = OrganizationPermissionDefinition.Clean(permissions).ToArray();
            permissionPolicy = policy;
            targetType = actionTargetType ?? string.Empty;
            defaultScopeType = scopeType;
            requiredApprovalCount = Math.Max(1, approvals);
            requiredCapabilityIds = OrganizationPermissionDefinition.Clean(capabilities).ToArray();
            requiredQualificationIds = OrganizationPermissionDefinition.Clean(qualifications).ToArray();
            selfAuthorizationAllowed = selfAllowed;
            delegationMaySatisfy = delegationAllowed;
            externalActorsMayBeAuthorized = externalAllowed;
            visibility = actionVisibility;
            auditPolicy = audit;
            tags = OrganizationPermissionDefinition.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Institutional Action definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-action.", StringComparison.Ordinal))
            {
                report.AddWarning($"Institutional Action definition '{DisplayName}' should use the 'organization-action.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(InstitutionalActionCategory), category) || category == InstitutionalActionCategory.Unknown)
            {
                report.AddError($"Institutional Action definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(OrganizationPermissionCombinationPolicy), permissionPolicy) || permissionPolicy == OrganizationPermissionCombinationPolicy.Unknown)
            {
                report.AddError($"Institutional Action definition '{DisplayName}' has invalid permission policy '{permissionPolicy}'.");
            }

            if (RequiredPermissionIds.Count == 0)
            {
                report.AddError($"Institutional Action definition '{DisplayName}' must require at least one permission.");
            }

            foreach (string permissionId in RequiredPermissionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(permissionId, out IGameDefinition definition) || definition is not OrganizationPermissionDefinition)
                {
                    report.AddError($"Institutional Action definition '{DisplayName}' references missing Organization Permission '{permissionId}'.");
                }
            }

            if (permissionPolicy == OrganizationPermissionCombinationPolicy.JointApproval && RequiredApprovalCount < 2)
            {
                report.AddError($"Institutional Action definition '{DisplayName}' uses joint approval but requires fewer than two approvals.");
            }
        }
    }

    [CreateAssetMenu(fileName = "OrganizationAuthorityRoleDefinition", menuName = "Unity Isekai Game/Organizations/Authority Role Definition")]
    public class OrganizationAuthorityRoleDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string roleDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string[] grantedPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] deniedPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] supportedOrganizationDefinitionIds = Array.Empty<string>();
        [SerializeField] private OrganizationCategory[] supportedOrganizationCategories = Array.Empty<OrganizationCategory>();
        [SerializeField] private OrganizationAuthorityScopeType defaultScopeType = OrganizationAuthorityScopeType.EntireOrganization;
        [SerializeField] private OrganizationAuthorityDelegationPolicy delegationPolicy = OrganizationAuthorityDelegationPolicy.NonDelegable;
        [SerializeField] private double defaultDuration = -1d;
        [SerializeField] private int priority = 100;
        [SerializeField] private OrganizationAuthorityConflictPolicy conflictPolicy = OrganizationAuthorityConflictPolicy.DenyOverridesGrant;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => roleDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<string> GrantedPermissionIds => OrganizationPermissionDefinition.Clean(grantedPermissionIds);
        public IReadOnlyList<string> DeniedPermissionIds => OrganizationPermissionDefinition.Clean(deniedPermissionIds);
        public IReadOnlyList<string> SupportedOrganizationDefinitionIds => OrganizationPermissionDefinition.Clean(supportedOrganizationDefinitionIds);
        public IReadOnlyList<OrganizationCategory> SupportedOrganizationCategories => OrganizationPermissionDefinition.CleanCategories(supportedOrganizationCategories);
        public OrganizationAuthorityScopeType DefaultScopeType => defaultScopeType;
        public OrganizationAuthorityDelegationPolicy DelegationPolicy => delegationPolicy;
        public double DefaultDuration => defaultDuration;
        public int Priority => priority;
        public OrganizationAuthorityConflictPolicy ConflictPolicy => conflictPolicy;
        public OrganizationVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => OrganizationPermissionDefinition.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string id,
            string name,
            IEnumerable<string> grants,
            IEnumerable<string> denials = null,
            IEnumerable<string> organizationDefinitions = null,
            IEnumerable<OrganizationCategory> organizationCategories = null,
            OrganizationAuthorityScopeType scopeType = OrganizationAuthorityScopeType.EntireOrganization,
            OrganizationAuthorityDelegationPolicy delegation = OrganizationAuthorityDelegationPolicy.NonDelegable,
            double duration = -1d,
            int rolePriority = 100,
            OrganizationAuthorityConflictPolicy conflict = OrganizationAuthorityConflictPolicy.DenyOverridesGrant,
            OrganizationVisibility roleVisibility = OrganizationVisibility.Public,
            IEnumerable<string> tagIds = null)
        {
            roleDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            grantedPermissionIds = OrganizationPermissionDefinition.Clean(grants).ToArray();
            deniedPermissionIds = OrganizationPermissionDefinition.Clean(denials).ToArray();
            supportedOrganizationDefinitionIds = OrganizationPermissionDefinition.Clean(organizationDefinitions).ToArray();
            supportedOrganizationCategories = OrganizationPermissionDefinition.CleanCategories(organizationCategories).ToArray();
            defaultScopeType = scopeType;
            delegationPolicy = delegation;
            defaultDuration = duration;
            priority = rolePriority;
            conflictPolicy = conflict;
            visibility = roleVisibility;
            tags = OrganizationPermissionDefinition.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Authority Role definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-authority-role.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Authority Role definition '{DisplayName}' should use the 'organization-authority-role.' namespace prefix.");
            }

            foreach (string permissionId in GrantedPermissionIds.Concat(DeniedPermissionIds))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(permissionId, out IGameDefinition definition) || definition is not OrganizationPermissionDefinition)
                {
                    report.AddError($"Organization Authority Role definition '{DisplayName}' references missing Organization Permission '{permissionId}'.");
                }
            }

            foreach (string conflict in GrantedPermissionIds.Intersect(DeniedPermissionIds, StringComparer.Ordinal))
            {
                report.AddError($"Organization Authority Role definition '{DisplayName}' both grants and denies '{conflict}'.");
            }

            foreach (string organizationDefinitionId in SupportedOrganizationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(organizationDefinitionId, out IGameDefinition definition) || definition is not OrganizationDefinition)
                {
                    report.AddError($"Organization Authority Role definition '{DisplayName}' references missing Organization Definition '{organizationDefinitionId}'.");
                }
            }
        }
    }

    [CreateAssetMenu(fileName = "OrganizationAuthorityBindingDefinition", menuName = "Unity Isekai Game/Organizations/Authority Binding Definition")]
    public class OrganizationAuthorityBindingDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string bindingDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private OrganizationAuthorityBindingSourceType sourceType = OrganizationAuthorityBindingSourceType.MembershipDefinition;
        [SerializeField] private string sourceDefinitionId;
        [SerializeField] private string sourceRuntimeId;
        [SerializeField] private string authorityRoleDefinitionId;
        [SerializeField] private OrganizationAuthorityScopeType scopeType = OrganizationAuthorityScopeType.EntireOrganization;
        [SerializeField] private OrganizationAuthorityScopeMatch scopeMatch = OrganizationAuthorityScopeMatch.ExactOnly;
        [SerializeField] private string scopedOrganizationDefinitionId;
        [SerializeField] private double startOffset = 0d;
        [SerializeField] private double duration = -1d;
        [SerializeField] private int priority = 100;
        [SerializeField] private OrganizationAuthorityConflictPolicy conflictPolicy = OrganizationAuthorityConflictPolicy.DenyOverridesGrant;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => bindingDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public OrganizationAuthorityBindingSourceType SourceType => sourceType;
        public string SourceDefinitionId => sourceDefinitionId ?? string.Empty;
        public string SourceRuntimeId => sourceRuntimeId ?? string.Empty;
        public string AuthorityRoleDefinitionId => authorityRoleDefinitionId ?? string.Empty;
        public OrganizationAuthorityScopeType ScopeType => scopeType;
        public OrganizationAuthorityScopeMatch ScopeMatch => scopeMatch;
        public string ScopedOrganizationDefinitionId => scopedOrganizationDefinitionId ?? string.Empty;
        public double StartOffset => startOffset;
        public double Duration => duration;
        public int Priority => priority;
        public OrganizationAuthorityConflictPolicy ConflictPolicy => conflictPolicy;
        public OrganizationVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => OrganizationPermissionDefinition.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string id,
            string name,
            OrganizationAuthorityBindingSourceType bindingSourceType,
            string sourceDefinition,
            string authorityRole,
            OrganizationAuthorityScopeType authorityScopeType = OrganizationAuthorityScopeType.EntireOrganization,
            OrganizationAuthorityScopeMatch authorityScopeMatch = OrganizationAuthorityScopeMatch.ExactOnly,
            string runtimeSource = "",
            string organizationDefinitionScope = "",
            double offset = 0d,
            double bindingDuration = -1d,
            int bindingPriority = 100,
            OrganizationAuthorityConflictPolicy conflict = OrganizationAuthorityConflictPolicy.DenyOverridesGrant,
            OrganizationVisibility bindingVisibility = OrganizationVisibility.Public,
            IEnumerable<string> tagIds = null)
        {
            bindingDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            sourceType = bindingSourceType;
            sourceDefinitionId = sourceDefinition ?? string.Empty;
            sourceRuntimeId = runtimeSource ?? string.Empty;
            authorityRoleDefinitionId = authorityRole ?? string.Empty;
            scopeType = authorityScopeType;
            scopeMatch = authorityScopeMatch;
            scopedOrganizationDefinitionId = organizationDefinitionScope ?? string.Empty;
            startOffset = offset;
            duration = bindingDuration;
            priority = bindingPriority;
            conflictPolicy = conflict;
            visibility = bindingVisibility;
            tags = OrganizationPermissionDefinition.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Authority Binding definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-authority-binding.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Authority Binding definition '{DisplayName}' should use the 'organization-authority-binding.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(OrganizationAuthorityBindingSourceType), sourceType) || sourceType == OrganizationAuthorityBindingSourceType.Unknown)
            {
                report.AddError($"Organization Authority Binding definition '{DisplayName}' has invalid source type '{sourceType}'.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(AuthorityRoleDefinitionId, out IGameDefinition role) || role is not OrganizationAuthorityRoleDefinition)
            {
                report.AddError($"Organization Authority Binding definition '{DisplayName}' references missing Authority Role '{AuthorityRoleDefinitionId}'.");
            }

            if (!ValidateSourceDefinition(definitionsById))
            {
                report.AddError($"Organization Authority Binding definition '{DisplayName}' references invalid source definition '{SourceDefinitionId}' for source type '{SourceType}'.");
            }

            if (!string.IsNullOrWhiteSpace(ScopedOrganizationDefinitionId) && (definitionsById == null || !definitionsById.TryGetValue(ScopedOrganizationDefinitionId, out IGameDefinition organization) || organization is not OrganizationDefinition))
            {
                report.AddError($"Organization Authority Binding definition '{DisplayName}' references missing scoped Organization Definition '{ScopedOrganizationDefinitionId}'.");
            }
        }

        private bool ValidateSourceDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById)
        {
            if (SourceType == OrganizationAuthorityBindingSourceType.OfficeAssignment || SourceType == OrganizationAuthorityBindingSourceType.OrganizationOverride || SourceType == OrganizationAuthorityBindingSourceType.ParentBranchRelationship)
            {
                return !string.IsNullOrWhiteSpace(SourceDefinitionId) || !string.IsNullOrWhiteSpace(SourceRuntimeId);
            }

            if (definitionsById == null || string.IsNullOrWhiteSpace(SourceDefinitionId) || !definitionsById.TryGetValue(SourceDefinitionId, out IGameDefinition definition))
            {
                return false;
            }

            return SourceType switch
            {
                OrganizationAuthorityBindingSourceType.MembershipDefinition => definition is OrganizationMembershipDefinition,
                OrganizationAuthorityBindingSourceType.RankDefinition => definition is OrganizationRankDefinition,
                OrganizationAuthorityBindingSourceType.OfficeDefinition or OrganizationAuthorityBindingSourceType.ActingOfficeAssignment => definition is OrganizationOfficeDefinition,
                _ => true
            };
        }
    }
}
