using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    public static class PrototypeOrganizationResourceDefinitionFactory
    {
        public const string CurrencyResourceTypeId = "organization-resource-type.prototype.currency";
        public const string InventoryResourceTypeId = "organization-resource-type.prototype.inventory";
        public const string PropertyResourceTypeId = "organization-resource-type.prototype.property";
        public const string BusinessResourceTypeId = "organization-resource-type.prototype.business";
        public const string CustodyResourceTypeId = "organization-resource-type.prototype.custody";

        public static DefinitionRegistry AddMissingPrototypeOrganizationResourceDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            definitions.AddRange(CreateMissingDefinitions(ids));
            return PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(new DefinitionRegistry(definitions));
        }

        public static IReadOnlyList<OrganizationResourceTypeDefinition> CreateMissingDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>((existingIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            List<OrganizationResourceTypeDefinition> definitions = new List<OrganizationResourceTypeDefinition>();
            Add(definitions, ids, CurrencyResourceTypeId, "Organization Currency", OrganizationResourceCategory.Currency, new[] { OrganizationAssetReferenceKind.Treasury, OrganizationAssetReferenceKind.Account, OrganizationAssetReferenceKind.CurrencyBalance });
            Add(definitions, ids, InventoryResourceTypeId, "Organization Inventory", OrganizationResourceCategory.ItemInventory, new[] { OrganizationAssetReferenceKind.Inventory, OrganizationAssetReferenceKind.ItemInstance });
            Add(definitions, ids, PropertyResourceTypeId, "Organization Property", OrganizationResourceCategory.Property, new[] { OrganizationAssetReferenceKind.Property, OrganizationAssetReferenceKind.Building, OrganizationAssetReferenceKind.LandParcel });
            Add(definitions, ids, BusinessResourceTypeId, "Organization Business Interest", OrganizationResourceCategory.BusinessInterest, new[] { OrganizationAssetReferenceKind.Business });
            Add(definitions, ids, CustodyResourceTypeId, "Organization Custodied Asset", OrganizationResourceCategory.CustodiedAsset, new[] { OrganizationAssetReferenceKind.ItemInstance, OrganizationAssetReferenceKind.Property, OrganizationAssetReferenceKind.Custom }, canOwn: false, canControl: true, canCustody: true);
            return definitions;
        }

        private static void Add(ICollection<OrganizationResourceTypeDefinition> definitions, ISet<string> ids, string id, string name, OrganizationResourceCategory category, IEnumerable<OrganizationAssetReferenceKind> kinds, bool canOwn = true, bool canControl = true, bool canCustody = true)
        {
            if (ids.Contains(id)) return;
            OrganizationResourceTypeDefinition definition = ScriptableObject.CreateInstance<OrganizationResourceTypeDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, kinds, canOwn, canControl, canCustody, canTransfer: true, tagIds: new[] { "prototype", "organization", "resources" });
            definitions.Add(definition);
            ids.Add(id);
        }
    }
}
