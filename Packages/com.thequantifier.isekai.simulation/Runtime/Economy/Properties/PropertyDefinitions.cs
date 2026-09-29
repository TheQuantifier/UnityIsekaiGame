using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Economy.Properties
{
    [Serializable]
    public sealed class PropertySharePolicyData
    {
        public bool requireExactTotalActiveShares = true;
        public long requiredTotalUnits = 10000L;
        public bool allowSeparateChildOwnership = true;
        public bool transferParentTransfersChildrenByDefault;

        public PropertySharePolicyData Clone()
        {
            return new PropertySharePolicyData
            {
                requireExactTotalActiveShares = requireExactTotalActiveShares,
                requiredTotalUnits = Math.Max(1L, requiredTotalUnits),
                allowSeparateChildOwnership = allowSeparateChildOwnership,
                transferParentTransfersChildrenByDefault = transferParentTransfersChildrenByDefault
            };
        }
    }

    [Serializable]
    public sealed class PropertyAccessPolicyData
    {
        public bool ownerGetsDefaultManageAccess = true;
        public bool ownerGetsDefaultTransferAccess = true;
        public bool inheritPublicAccessToChildren;
        public bool publicProjectionHidesOwners = true;
        public bool publicProjectionHidesTenants = true;
        public bool publicProjectionHidesAccounts = true;

        public PropertyAccessPolicyData Clone()
        {
            return new PropertyAccessPolicyData
            {
                ownerGetsDefaultManageAccess = ownerGetsDefaultManageAccess,
                ownerGetsDefaultTransferAccess = ownerGetsDefaultTransferAccess,
                inheritPublicAccessToChildren = inheritPublicAccessToChildren,
                publicProjectionHidesOwners = publicProjectionHidesOwners,
                publicProjectionHidesTenants = publicProjectionHidesTenants,
                publicProjectionHidesAccounts = publicProjectionHidesAccounts
            };
        }
    }

    [Serializable]
    public sealed class PropertyMaintenancePolicyData
    {
        public bool allowMaintenance = true;
        public bool requiresAuthorizedMaintainer = true;
        public bool requiresToolsWhenDeclared = true;
        public bool requiresMaterialsWhenDeclared = true;

        public PropertyMaintenancePolicyData Clone()
        {
            return new PropertyMaintenancePolicyData
            {
                allowMaintenance = allowMaintenance,
                requiresAuthorizedMaintainer = requiresAuthorizedMaintainer,
                requiresToolsWhenDeclared = requiresToolsWhenDeclared,
                requiresMaterialsWhenDeclared = requiresMaterialsWhenDeclared
            };
        }
    }

    [CreateAssetMenu(fileName = "PropertyDefinition", menuName = "Unity Isekai Game/Economy/Property Definition")]
    public class PropertyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string propertyDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private PropertyCategory category = PropertyCategory.LandParcel;
        [SerializeField] private PropertyCategory parentCategory = PropertyCategory.Unknown;
        [SerializeField] private PropertyCategory[] permittedChildCategories = Array.Empty<PropertyCategory>();
        [SerializeField] private PropertyOwnershipModel[] permittedOwnershipModels = { PropertyOwnershipModel.Sole, PropertyOwnershipModel.SharedFractional };
        [SerializeField] private OccupancyCategory[] permittedOccupancyCategories = { OccupancyCategory.Residence, OccupancyCategory.BusinessOperation, OccupancyCategory.Storage };
        [SerializeField] private TenancyModel[] permittedTenancyModels = { TenancyModel.None, TenancyModel.FixedTerm, TenancyModel.OpenEnded };
        [SerializeField] private PropertyUseCategory[] supportedUseCategories = { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Storage };
        [SerializeField] private string defaultCurrencyId;
        [SerializeField] private bool requiresSpatialReference;
        [SerializeField] private string propertyValuePolicyId;
        [SerializeField] private string transferPolicyId;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private PropertySharePolicyData ownershipPolicy = new PropertySharePolicyData();
        [SerializeField] private PropertyAccessPolicyData accessPolicy = new PropertyAccessPolicyData();
        [SerializeField] private PropertyMaintenancePolicyData maintenancePolicy = new PropertyMaintenancePolicyData();
        [SerializeField, Min(1)] private int definitionVersion = 1;

        public string Id => propertyDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public PropertyCategory Category => category;
        public PropertyCategory ParentCategory => parentCategory;
        public IReadOnlyList<PropertyCategory> PermittedChildCategories => Normalize(permittedChildCategories);
        public IReadOnlyList<PropertyOwnershipModel> PermittedOwnershipModels => Normalize(permittedOwnershipModels);
        public IReadOnlyList<OccupancyCategory> PermittedOccupancyCategories => Normalize(permittedOccupancyCategories);
        public IReadOnlyList<TenancyModel> PermittedTenancyModels => Normalize(permittedTenancyModels);
        public IReadOnlyList<PropertyUseCategory> SupportedUseCategories => Normalize(supportedUseCategories);
        public string DefaultCurrencyId => defaultCurrencyId ?? string.Empty;
        public bool RequiresSpatialReference => requiresSpatialReference;
        public string PropertyValuePolicyId => propertyValuePolicyId ?? string.Empty;
        public string TransferPolicyId => transferPolicyId ?? string.Empty;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public PropertySharePolicyData OwnershipPolicy => ownershipPolicy?.Clone() ?? new PropertySharePolicyData();
        public PropertyAccessPolicyData AccessPolicy => accessPolicy?.Clone() ?? new PropertyAccessPolicyData();
        public PropertyMaintenancePolicyData MaintenancePolicy => maintenancePolicy?.Clone() ?? new PropertyMaintenancePolicyData();
        public int DefinitionVersion => Math.Max(1, definitionVersion);

        public void Initialize(string id, string display, PropertyCategory propertyCategory)
        {
            propertyDefinitionId = id ?? string.Empty;
            displayName = display ?? string.Empty;
            category = propertyCategory;
            definitionVersion = Math.Max(1, definitionVersion);
        }

        public void SetPolicies(
            PropertyCategory[] childCategories = null,
            PropertyOwnershipModel[] ownershipModels = null,
            PropertyUseCategory[] useCategories = null,
            string currencyId = "")
        {
            permittedChildCategories = childCategories ?? permittedChildCategories ?? Array.Empty<PropertyCategory>();
            permittedOwnershipModels = ownershipModels ?? permittedOwnershipModels ?? Array.Empty<PropertyOwnershipModel>();
            supportedUseCategories = useCategories ?? supportedUseCategories ?? Array.Empty<PropertyUseCategory>();
            defaultCurrencyId = currencyId ?? defaultCurrencyId ?? string.Empty;
        }

        private void OnValidate()
        {
            definitionVersion = Math.Max(1, definitionVersion);
            ownershipPolicy ??= new PropertySharePolicyData();
            accessPolicy ??= new PropertyAccessPolicyData();
            maintenancePolicy ??= new PropertyMaintenancePolicyData();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("property.", StringComparison.Ordinal))
            {
                report.AddError($"Property definition '{DisplayName}' must use the 'property.' namespace.");
            }

            if (!Enum.IsDefined(typeof(PropertyCategory), category) || category == PropertyCategory.Unknown)
            {
                report.AddError($"Property definition '{DisplayName}' has an invalid category.");
            }

            if (PermittedOwnershipModels.Count == 0 || PermittedOwnershipModels.Contains(PropertyOwnershipModel.Unknown))
            {
                report.AddError($"Property definition '{DisplayName}' must declare permitted ownership models.");
            }

            if (SupportedUseCategories.Count == 0 || SupportedUseCategories.Contains(PropertyUseCategory.Unknown))
            {
                report.AddError($"Property definition '{DisplayName}' must declare supported use categories.");
            }

            PropertySharePolicyData sharePolicy = OwnershipPolicy;
            if (sharePolicy.requiredTotalUnits <= 0L)
            {
                report.AddError($"Property definition '{DisplayName}' has an invalid ownership-share policy.");
            }

            if (!string.IsNullOrWhiteSpace(DefaultCurrencyId) && definitionsById != null && !definitionsById.ContainsKey(DefaultCurrencyId))
            {
                report.AddWarning($"Property definition '{DisplayName}' references optional currency '{DefaultCurrencyId}' that is not in the current catalog.");
            }
        }

        private static IReadOnlyList<T> Normalize<T>(T[] values)
            where T : Enum
        {
            return (values ?? Array.Empty<T>())
                .Where(value => Convert.ToInt32(value) != 0)
                .Distinct()
                .OrderBy(value => Convert.ToInt32(value))
                .ToArray();
        }
    }
}
