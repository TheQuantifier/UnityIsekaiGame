using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "CurrencyDefinition", menuName = "Unity Isekai Game/Progression/Currency Definition")]
    public sealed class CurrencyDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string currencyId;
        [SerializeField] private string displayName;
        [SerializeField] private string symbol = "G";
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField, Min(0)] private int minorUnitPrecision;
        [SerializeField] private string denominationRelationshipMetadata;
        [SerializeField] private string regionFactionMetadata;
        [SerializeField] private bool enabledForAlpha = true;
        [SerializeField] private string stackDisplayPolicy = "WholeUnits";
        [SerializeField] private bool abstractBalancesAllowed = true;
        [SerializeField] private bool physicalCurrencyAllowed = true;
        [SerializeField, Min(1)] private long unitsPerPhysicalItem = 1L;
        [SerializeField] private ItemDefinition physicalCurrencyItem;
        [SerializeField] private string issuerId;

        public string CurrencyId => currencyId;
        public string Id => currencyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Symbol => symbol ?? string.Empty;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Currency;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public int MinorUnitPrecision => Mathf.Max(0, minorUnitPrecision);
        public string DenominationRelationshipMetadata => denominationRelationshipMetadata ?? string.Empty;
        public string RegionFactionMetadata => regionFactionMetadata ?? string.Empty;
        public bool EnabledForAlpha => enabledForAlpha;
        public string StackDisplayPolicy => stackDisplayPolicy ?? string.Empty;
        public bool AbstractBalancesAllowed => abstractBalancesAllowed;
        public bool PhysicalCurrencyAllowed => physicalCurrencyAllowed;
        public long UnitsPerPhysicalItem => unitsPerPhysicalItem <= 0L ? 1L : unitsPerPhysicalItem;
        public ItemDefinition PhysicalCurrencyItem => physicalCurrencyItem;
        public string IssuerId => issuerId ?? string.Empty;

        public void Initialize(
            string id,
            string display,
            string currencySymbol = "G",
            int precision = 0,
            ItemDefinition physicalItem = null,
            long physicalUnits = 1L,
            bool allowAbstractBalances = true,
            bool allowPhysicalCurrency = true,
            string issuer = "")
        {
            currencyId = id ?? string.Empty;
            displayName = display ?? string.Empty;
            symbol = currencySymbol ?? string.Empty;
            minorUnitPrecision = Mathf.Max(0, precision);
            physicalCurrencyItem = physicalItem;
            unitsPerPhysicalItem = System.Math.Max(1L, physicalUnits);
            abstractBalancesAllowed = allowAbstractBalances;
            physicalCurrencyAllowed = allowPhysicalCurrency;
            issuerId = issuer ?? string.Empty;
        }

        private void OnValidate()
        {
            minorUnitPrecision = Mathf.Max(0, minorUnitPrecision);
            unitsPerPhysicalItem = System.Math.Max(1L, unitsPerPhysicalItem);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Id.StartsWith("currency."))
            {
                report.AddWarning($"Currency '{DisplayName}' should use the 'currency.' namespace prefix.");
            }

            if (MinorUnitPrecision < 0)
            {
                report.AddError($"Currency '{DisplayName}' has invalid precision.");
            }

            if (UnitsPerPhysicalItem <= 0L)
            {
                report.AddError($"Currency '{DisplayName}' has invalid physical item unit conversion.");
            }

            if (physicalCurrencyItem != null
                && (definitionsById == null
                    || !definitionsById.TryGetValue(physicalCurrencyItem.Id, out IGameDefinition found)
                    || found is not ItemDefinition))
            {
                report.AddError($"Currency '{DisplayName}' physical currency item '{physicalCurrencyItem.Id}' is not in the configured catalog.");
            }
        }
    }
}
