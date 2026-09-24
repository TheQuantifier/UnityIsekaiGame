using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;

namespace UnityIsekaiGame.Inventory
{
    [CreateAssetMenu(fileName = "NewItemDefinition", menuName = "Unity Isekai Game/Inventory/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject, IInventoryItemDefinition, IUsableItemDefinition, IEquippableItemDefinition, IHasRarity, IItemInstancePolicy, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private WorldItemPickup worldPickupPrefab;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private RarityDefinition rarity;
        [SerializeField] private ItemInstanceMode instanceMode = ItemInstanceMode.DefinitionOnly;
        [SerializeField] private bool stackable = true;
        [SerializeField, Min(1)] private int maximumStackSize = 1;
        [SerializeField] private ItemUseEffect[] useEffects;
        [SerializeField] private EquipmentData equipment;
        [SerializeField] private ItemCompositionTemplateData defaultCompositionTemplate = new ItemCompositionTemplateData();

        public string ItemId => itemId;
        public string Id => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public WorldItemPickup WorldPickupPrefab => worldPickupPrefab;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Item;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public RarityDefinition Rarity => rarity;
        public ItemInstanceMode InstanceMode => instanceMode;
        public bool Stackable => stackable;
        public int MaximumStackSize => stackable ? Mathf.Max(1, maximumStackSize) : 1;
        public IReadOnlyList<ItemUseEffect> UseEffects => useEffects;
        public bool IsUsable => useEffects != null && useEffects.Length > 0;
        public int UseEffectCount => useEffects == null ? 0 : useEffects.Length;
        public bool HasMissingUseEffect
        {
            get
            {
                if (useEffects == null)
                {
                    return false;
                }

                for (int i = 0; i < useEffects.Length; i++)
                {
                    if (useEffects[i] == null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        public EquipmentData Equipment => equipment;
        public bool IsEquippable => equipment != null && equipment.Equippable;
        public ItemCompositionTemplateData DefaultCompositionTemplate => defaultCompositionTemplate ?? new ItemCompositionTemplateData();

        public void EnableEquipmentCapability()
        {
            equipment ??= new EquipmentData();
            equipment.Enable();
        }

        public void RemoveEquipmentCapability()
        {
            equipment = null;
        }

        public void EnableMeleeWeaponCapability()
        {
            EnableEquipmentCapability();
            equipment.EnableMeleeWeapon();
        }

        public void EnableRangedWeaponCapability()
        {
            EnableEquipmentCapability();
            equipment.EnableRangedWeapon();
        }

        public void RemoveMeleeWeaponCapability()
        {
            equipment?.RemoveMeleeWeapon();
        }

        public void RemoveRangedWeaponCapability()
        {
            equipment?.RemoveRangedWeapon();
        }

        public void PruneInactiveCapabilities()
        {
            if (equipment == null) return;
            equipment.Validate();
            if (!equipment.Equippable) equipment = null;
        }

        private void OnValidate()
        {
            maximumStackSize = Mathf.Max(1, maximumStackSize);
            PruneInactiveCapabilities();
            defaultCompositionTemplate ??= new ItemCompositionTemplateData();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (worldPickupPrefab != null && worldPickupPrefab.Item != null && worldPickupPrefab.Item != this)
            {
                report.AddError($"Item definition '{DisplayName}' world pickup prefab is configured for '{worldPickupPrefab.Item.Id}', not '{Id}'.");
            }

            ValidateDefaultCompositionTemplate(definitionsById, report);

            if (equipment == null || !equipment.Equippable)
            {
                return;
            }

            ValidateDamageTypeReference(equipment.MeleeWeapon?.DamageType, "melee weapon", definitionsById, report);
            ValidateDamageTypeReference(equipment.RangedWeapon?.DamageType, "ranged weapon", definitionsById, report);
            ValidateAmmoReference(equipment.RangedWeapon?.AmmoItem, definitionsById, report);
            IReadOnlyList<ResistanceModifierDefinition> resistanceModifiers = equipment.ResistanceModifiers;
            HashSet<string> seenResistanceTypes = new HashSet<string>();
            for (int i = 0; i < resistanceModifiers.Count; i++)
            {
                ResistanceModifierDefinition modifier = resistanceModifiers[i];
                if (modifier == null)
                {
                    report.AddError($"Item definition '{DisplayName}' has a null equipment resistance modifier at index {i}.");
                    continue;
                }

                if (modifier.DamageType == null)
                {
                    report.AddError($"Item definition '{DisplayName}' has an equipment resistance modifier with no damage type at index {i}.");
                    continue;
                }

                if (!modifier.IsValid)
                {
                    report.AddError($"Item definition '{DisplayName}' has an invalid equipment resistance modifier for '{modifier.DamageType.Id}'.");
                }

                if (!seenResistanceTypes.Add($"{modifier.DamageType.Id}:{modifier.Priority}"))
                {
                    report.AddWarning($"Item definition '{DisplayName}' has duplicate-looking equipment resistance modifier for '{modifier.DamageType.Id}'.");
                }

                ValidateDamageTypeReference(modifier.DamageType, "equipment resistance", definitionsById, report);
            }
        }

        private void ValidateDefaultCompositionTemplate(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            ItemCompositionTemplateData template = DefaultCompositionTemplate;
            if (template == null || template.IsEmpty)
            {
                if (template != null && template.required)
                {
                    report.AddError($"Item definition '{DisplayName}' requires a default composition but the template is empty.");
                }

                return;
            }

            if (!System.Enum.IsDefined(typeof(ItemCompositionMassAuthority), template.massAuthority))
            {
                report.AddError($"Item definition '{DisplayName}' default composition has an invalid mass authority policy.");
                return;
            }

            DefinitionRegistry registry = definitionsById == null ? null : new DefinitionRegistry(definitionsById.Values);
            ItemCompositionRuntimeSaveData saveData = new ItemCompositionRuntimeSaveData
            {
                schemaVersion = ItemCompositionRuntimeSaveData.CurrentSchemaVersion,
                revision = 1L,
                records =
                {
                    template.Instantiate($"definition-template.{Id}", Id)
                }
            };
            if (!ItemCompositionRuntime.ValidateSaveData(saveData, registry, null, out string failure))
            {
                report.AddError($"Item definition '{DisplayName}' default composition is invalid: {failure}");
            }
        }

        private void ValidateDamageTypeReference(DamageTypeDefinition damageTypeDefinition, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (damageTypeDefinition == null)
            {
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(damageTypeDefinition.Id, out IGameDefinition found)
                || found is not DamageTypeDefinition)
            {
                report.AddError($"Item definition '{DisplayName}' {label} references damage type '{damageTypeDefinition.Id}', which is not in the configured catalog.");
            }
        }

        private void ValidateAmmoReference(ItemDefinition ammoItem, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (ammoItem == null)
            {
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(ammoItem.Id, out IGameDefinition found)
                || found is not ItemDefinition)
            {
                report.AddError($"Item definition '{DisplayName}' ranged weapon references ammo item '{ammoItem.Id}', which is not in the configured catalog.");
            }
        }
    }
}
