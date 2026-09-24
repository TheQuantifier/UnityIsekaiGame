using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Equipment
{
    public sealed class PlayerStats : ActorStats
    {
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private MonoBehaviour itemQualityAffixRuntimeProvider;

        private readonly HashSet<StatModifierSource> appliedAffixModifierSources = new HashSet<StatModifierSource>();
        private IItemQualityAffixRuntimeProvider qualityAffixProvider;
        private IItemDurabilityRuntimeProvider durabilityProvider;

        protected override void Awake()
        {
            if (equipment == null)
            {
                equipment = GetComponent<PlayerEquipment>();
            }

            ResolveQualityAffixProvider();

            base.Awake();
            RecalculateEquipmentModifiers();
        }

        protected override void OnDerivedStatsConfigured()
        {
            RecalculateEquipmentModifiers();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (equipment != null)
            {
                equipment.EquipmentChanged += OnEquipmentChanged;
            }

            SubscribeQualityAffixRuntime();
            SubscribeDurabilityRuntime();
        }

        protected override void OnDisable()
        {
            UnsubscribeQualityAffixRuntime();
            UnsubscribeDurabilityRuntime();

            if (equipment != null)
            {
                equipment.EquipmentChanged -= OnEquipmentChanged;
            }

            base.OnDisable();
        }

        private void OnEquipmentChanged()
        {
            RecalculateEquipmentModifiers();
            NotifyStatsChanged();
        }

        private void OnAffixStateChanged(string itemInstanceId)
        {
            RefreshIfEquipped(itemInstanceId);
        }

        private void OnDurabilityStateChanged(string itemInstanceId)
        {
            RefreshIfEquipped(itemInstanceId);
        }

        private void RefreshIfEquipped(string itemInstanceId)
        {
            if (equipment == null || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return;
            }

            foreach (EquipmentSlotState slot in equipment.Slots)
            {
                if (slot != null
                    && !slot.IsEmpty
                    && string.Equals(slot.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    RecalculateEquipmentModifiers();
                    NotifyStatsChanged();
                    return;
                }
            }
        }

        public void RefreshEquipmentModifiers()
        {
            RecalculateEquipmentModifiers();
            NotifyStatsChanged();
        }

        private void RecalculateEquipmentModifiers()
        {
            RemoveEquipmentModifiers();

            if (equipment == null)
            {
                return;
            }

            foreach (EquipmentSlotState slot in equipment.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.Item == null || !slot.Item.IsEquippable)
                {
                    continue;
                }

                RegisterEquipmentModifiers(slot);
            }
        }

        private void RemoveEquipmentModifiers()
        {
            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < values.Length; i++)
            {
                EquipmentSlotType slotType = (EquipmentSlotType)values.GetValue(i);
                StatModifierSource source = CreateEquipmentSource(slotType);
                RemoveModifiersFromSource(source);
                RemoveResistanceModifiersFromSource(source);
            }

            foreach (StatModifierSource source in appliedAffixModifierSources)
            {
                RemoveModifiersFromSource(source);
            }

            appliedAffixModifierSources.Clear();
        }

        private void RegisterEquipmentModifiers(EquipmentSlotState slot)
        {
            float durabilityFactor = GetDurabilityContributionFactor(slot);
            if (durabilityFactor <= 0f)
            {
                return;
            }

            StatModifierSource source = CreateEquipmentSource(slot.SlotType);
            StatModifiers modifiers = slot.Item.Equipment.StatModifiers;
            AddFlatEquipmentModifier(source, StatType.MaximumHealth, modifiers.MaximumHealth * durabilityFactor);
            AddFlatEquipmentModifier(source, StatType.MaximumStamina, modifiers.MaximumStamina * durabilityFactor);
            AddFlatEquipmentModifier(source, StatType.MaximumMana, modifiers.MaximumMana * durabilityFactor);
            AddFlatEquipmentModifier(source, StatType.AttackPower, modifiers.AttackPower * durabilityFactor);
            AddFlatEquipmentModifier(source, StatType.Defense, modifiers.Defense * durabilityFactor);
            if (durabilityFactor >= 0.999f)
            {
                RegisterEquipmentResistanceModifiers(source, slot.Item.Equipment.ResistanceModifiers);
            }

            RegisterAffixModifiers(slot);
        }

        private void AddFlatEquipmentModifier(StatModifierSource source, StatType statType, float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            AddModifier(new RuntimeStatModifier(statType, StatModifierOperation.FlatAdd, value, source));
        }

        private static StatModifierSource CreateEquipmentSource(EquipmentSlotType slotType)
        {
            return new StatModifierSource(StatModifierSourceType.Equipment, $"equipment.slot.{slotType}");
        }

        private void RegisterEquipmentResistanceModifiers(StatModifierSource source, System.Collections.Generic.IReadOnlyList<ResistanceModifierDefinition> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                ResistanceModifierDefinition modifier = modifiers[i];
                if (modifier == null || !modifier.IsValid)
                {
                    continue;
                }

                AddResistanceModifier(modifier.CreateRuntimeModifier(source));
            }
        }

        private void RegisterAffixModifiers(EquipmentSlotState slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.ItemInstanceId))
            {
                return;
            }

            IItemQualityAffixRuntimeProvider provider = ResolveQualityAffixProvider();
            ItemQualityAffixRuntime qualityRuntime = provider?.ItemQualityAffixes;
            if (qualityRuntime == null)
            {
                return;
            }

            ItemQualityAffixOperationResult result = qualityRuntime.ApplyActiveAffixModifiers(
                slot.ItemInstanceId,
                provider.ItemQualityDefinitionRegistry,
                this,
                out IReadOnlyList<StatModifierSource> sources);
            if (!result.Succeeded)
            {
                Debug.LogWarning($"Equipped item affix modifiers were not applied for '{slot.ItemInstanceId}': {result.Message}");
                return;
            }

            foreach (StatModifierSource affixSource in sources)
            {
                appliedAffixModifierSources.Add(affixSource);
            }
        }

        private IItemQualityAffixRuntimeProvider ResolveQualityAffixProvider()
        {
            if (qualityAffixProvider != null)
            {
                return qualityAffixProvider;
            }

            if (itemQualityAffixRuntimeProvider is IItemQualityAffixRuntimeProvider configured)
            {
                qualityAffixProvider = configured;
                durabilityProvider = configured as IItemDurabilityRuntimeProvider;
                SubscribeQualityAffixRuntime();
                SubscribeDurabilityRuntime();
                return qualityAffixProvider;
            }

            qualityAffixProvider = GetComponent<IItemQualityAffixRuntimeProvider>();
            if (qualityAffixProvider == null)
            {
                qualityAffixProvider = GetComponentInParent<IItemQualityAffixRuntimeProvider>();
            }

            if (qualityAffixProvider is MonoBehaviour behaviour)
            {
                itemQualityAffixRuntimeProvider = behaviour;
            }

            SubscribeQualityAffixRuntime();
            durabilityProvider = qualityAffixProvider as IItemDurabilityRuntimeProvider;
            SubscribeDurabilityRuntime();
            return qualityAffixProvider;
        }

        private void SubscribeQualityAffixRuntime()
        {
            ItemQualityAffixRuntime runtime = qualityAffixProvider?.ItemQualityAffixes;
            if (runtime == null)
            {
                return;
            }

            runtime.ItemAffixStateChanged -= OnAffixStateChanged;
            runtime.ItemAffixStateChanged += OnAffixStateChanged;
        }

        private void UnsubscribeQualityAffixRuntime()
        {
            ItemQualityAffixRuntime runtime = qualityAffixProvider?.ItemQualityAffixes;
            if (runtime != null)
            {
                runtime.ItemAffixStateChanged -= OnAffixStateChanged;
            }
        }

        private void SubscribeDurabilityRuntime()
        {
            IItemDurabilityRuntimeProvider provider = durabilityProvider ?? qualityAffixProvider as IItemDurabilityRuntimeProvider;
            ItemDurabilityRuntime runtime = provider?.ItemDurability;
            if (runtime == null)
            {
                return;
            }

            durabilityProvider = provider;
            runtime.ItemDurabilityStateChanged -= OnDurabilityStateChanged;
            runtime.ItemDurabilityStateChanged += OnDurabilityStateChanged;
        }

        private void UnsubscribeDurabilityRuntime()
        {
            ItemDurabilityRuntime runtime = durabilityProvider?.ItemDurability;
            if (runtime != null)
            {
                runtime.ItemDurabilityStateChanged -= OnDurabilityStateChanged;
            }
        }

        private float GetDurabilityContributionFactor(EquipmentSlotState slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.ItemInstanceId))
            {
                return 1f;
            }

            IItemDurabilityRuntimeProvider provider = durabilityProvider ?? ResolveQualityAffixProvider() as IItemDurabilityRuntimeProvider;
            return Mathf.Clamp01(provider?.ItemDurability?.GetEquipmentContributionFactor(slot.ItemInstanceId) ?? 1f);
        }
    }
}
