using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    [CreateAssetMenu(fileName = "OrganizationResourceTypeDefinition", menuName = "Unity Isekai Game/Organizations/Resource Type Definition")]
    public sealed class OrganizationResourceTypeDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string resourceTypeDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationResourceCategory category = OrganizationResourceCategory.Custom;
        [SerializeField] private OrganizationAssetReferenceKind[] supportedReferenceKinds = Array.Empty<OrganizationAssetReferenceKind>();
        [SerializeField] private bool ownershipAllowed = true;
        [SerializeField] private bool controlAllowed = true;
        [SerializeField] private bool custodyAllowed = true;
        [SerializeField] private bool transferAllowed = true;
        [SerializeField] private string valuationPolicyId;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Public;
        [SerializeField] private OrganizationAuthorityAuditPolicy auditPolicy = OrganizationAuthorityAuditPolicy.SuccessfulActions;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => resourceTypeDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationResourceCategory Category => category;
        public IReadOnlyList<OrganizationAssetReferenceKind> SupportedReferenceKinds => (supportedReferenceKinds ?? Array.Empty<OrganizationAssetReferenceKind>()).Where(value => value != OrganizationAssetReferenceKind.Unknown).Distinct().OrderBy(value => (int)value).ToArray();
        public bool OwnershipAllowed => ownershipAllowed;
        public bool ControlAllowed => controlAllowed;
        public bool CustodyAllowed => custodyAllowed;
        public bool TransferAllowed => transferAllowed;
        public string ValuationPolicyId => valuationPolicyId ?? string.Empty;
        public OrganizationVisibility Visibility => visibility;
        public OrganizationAuthorityAuditPolicy AuditPolicy => auditPolicy;
        public IReadOnlyList<string> TagIds => OrganizationModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, OrganizationResourceCategory resourceCategory, IEnumerable<OrganizationAssetReferenceKind> referenceKinds, bool canOwn = true, bool canControl = true, bool canCustody = true, bool canTransfer = true, OrganizationVisibility resourceVisibility = OrganizationVisibility.Public, OrganizationAuthorityAuditPolicy audit = OrganizationAuthorityAuditPolicy.SuccessfulActions, IEnumerable<string> tagIds = null)
        {
            resourceTypeDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = resourceCategory;
            supportedReferenceKinds = (referenceKinds ?? Array.Empty<OrganizationAssetReferenceKind>()).Where(value => value != OrganizationAssetReferenceKind.Unknown).Distinct().OrderBy(value => (int)value).ToArray();
            ownershipAllowed = canOwn;
            controlAllowed = canControl;
            custodyAllowed = canCustody;
            transferAllowed = canTransfer;
            valuationPolicyId = string.Empty;
            visibility = resourceVisibility;
            auditPolicy = audit;
            tags = OrganizationModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Organization Resource Type definition has no stable ID.");
            else if (!Id.StartsWith("organization-resource-type.", StringComparison.Ordinal)) report.AddWarning($"Organization Resource Type definition '{DisplayName}' should use the 'organization-resource-type.' namespace prefix.");
            if (!Enum.IsDefined(typeof(OrganizationResourceCategory), category) || category == OrganizationResourceCategory.Unknown) report.AddError($"Organization Resource Type definition '{DisplayName}' has an invalid category.");
            if (SupportedReferenceKinds.Count == 0) report.AddError($"Organization Resource Type definition '{DisplayName}' must support at least one underlying reference kind.");
            if (!ownershipAllowed && !controlAllowed && !custodyAllowed) report.AddError($"Organization Resource Type definition '{DisplayName}' cannot be owned, controlled, or held in custody.");
            if (!Enum.IsDefined(typeof(OrganizationVisibility), visibility)) report.AddError($"Organization Resource Type definition '{DisplayName}' has invalid visibility.");
        }
    }
}
