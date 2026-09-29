using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Beings;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Stats
{
    /// <summary>Calculated-stat facade plus the actor's composable damage resistances.</summary>
    public class ActorStats : MonoBehaviour, IActorStats, IDamageResistanceReceiver
    {
        [SerializeField] private ActorProfileDefinition actorProfile;
        [SerializeField] private CharacterAttributes characterAttributes;
        [SerializeField] private CalculatedStatCollection calculatedStats;

        private readonly RuntimeResistanceCollection runtimeResistances = new RuntimeResistanceCollection();
        private bool profileInitialized;

        public float MaximumHealth => GetCalculatedValue(CalculatedStatIds.MaximumHealth, 1f);
        public float MaximumStamina => GetCalculatedValue(CalculatedStatIds.MaximumStamina);
        public float MaximumMana => GetCalculatedValue(CalculatedStatIds.MaximumMana);
        public float AttackPower => GetCalculatedValue(CalculatedStatIds.PhysicalPower);
        public float Defense => GetCalculatedValue(CalculatedStatIds.PhysicalDefense);
        public float MovementSpeed => GetCalculatedValue(CalculatedStatIds.MovementSpeed);
        public ActorProfileDefinition ActorProfile => actorProfile;
        public CharacterAttributes CharacterAttributes => characterAttributes;
        public CalculatedStatCollection CalculatedStats => calculatedStats;
        public bool IsInitialized => calculatedStats != null && calculatedStats.IsConfigured;
        public ActorProfileInitializationResult LastInitializationResult { get; private set; }
        public event Action StatsChanged;
        public event Action<DamageTypeDefinition, float> ResistanceChanged;

        protected virtual void Awake()
        {
            ResolveAuthoredDependencies();
            InitializeProfileResistances();
        }

        protected virtual void OnEnable()
        {
            ResolveAuthoredDependencies();
            runtimeResistances.ResistanceChanged += OnRuntimeResistanceChanged;
            if (calculatedStats != null) calculatedStats.CalculatedStatsChanged += OnCalculatedStatsChanged;
        }

        protected virtual void OnDisable()
        {
            runtimeResistances.ResistanceChanged -= OnRuntimeResistanceChanged;
            if (calculatedStats != null) calculatedStats.CalculatedStatsChanged -= OnCalculatedStatsChanged;
        }

        public ActorProfileInitializationResult TryInitializeBaseStats()
        {
            if (profileInitialized)
            {
                LastInitializationResult = new ActorProfileInitializationResult(ActorProfileInitializationStatus.AlreadyInitialized, $"{name} actor profile is already initialized.");
                return LastInitializationResult;
            }

            InitializeProfileResistances();
            return LastInitializationResult;
        }

        public bool HasCalculatedStat(string statId) => calculatedStats != null && calculatedStats.IsConfigured && calculatedStats.HasStat(statId);

        public float GetCalculatedStatValue(string statId) => GetCalculatedValue(statId);

        public bool AddCalculatedStatContribution(RuntimeCalculatedStatContribution contribution)
        {
            return calculatedStats != null
                && calculatedStats.IsConfigured
                && contribution != null
                && calculatedStats.AddContribution(contribution, out _);
        }

        public bool RemoveCalculatedStatContributions(StatModifierSource source)
        {
            return source.IsValid
                && calculatedStats != null
                && calculatedStats.RemoveContributionsFromSource(CalculatedStatContributionSourceUtility.Map(source.SourceType), source.SourceId);
        }

        public float GetDirectResistance(DamageTypeDefinition damageType) => runtimeResistances.GetDirectResistance(damageType);
        public float GetEffectiveResistance(DamageTypeDefinition damageType) => runtimeResistances.GetEffectiveResistance(damageType);
        public bool AddResistanceModifier(RuntimeResistanceModifier modifier) => runtimeResistances.AddModifier(modifier);
        public bool RemoveResistanceModifiersFromSource(StatModifierSource source) => runtimeResistances.RemoveModifiersFromSource(source);
        public IReadOnlyList<RuntimeResistanceModifier> GetResistanceModifiers() => runtimeResistances.GetModifiers();

        protected void NotifyStatsChanged() => StatsChanged?.Invoke();

        public void ConfigureDerivedStats(DefinitionRegistry registry)
        {
            ResolveAuthoredDependencies();
            if (registry == null || characterAttributes == null || calculatedStats == null)
            {
                LastInitializationResult = new ActorProfileInitializationResult(ActorProfileInitializationStatus.InvalidProfile, $"{name} requires authored CharacterAttributes, CalculatedStatCollection, and a definition registry.");
                return;
            }

            characterAttributes.Configure(registry);
            calculatedStats.Configure(registry, characterAttributes);
            ApplyProfileStatContributions();
            InitializeProfileResistances();
            OnDerivedStatsConfigured();
            NotifyStatsChanged();
        }

        protected virtual void OnDerivedStatsConfigured() { }

        private void ResolveAuthoredDependencies()
        {
            characterAttributes ??= GetComponent<CharacterAttributes>();
            calculatedStats ??= GetComponent<CalculatedStatCollection>();
        }

        private void InitializeProfileResistances()
        {
            if (profileInitialized) return;
            runtimeResistances.ClearBaseResistances();
            if (actorProfile == null)
            {
                LastInitializationResult = new ActorProfileInitializationResult(ActorProfileInitializationStatus.InvalidProfile, $"{name} has no ActorProfileDefinition.");
                return;
            }

            foreach (ResistanceModifierDefinition resistance in actorProfile.BaseResistances)
            {
                if (resistance != null && resistance.IsValid) runtimeResistances.SetBaseResistance(resistance.DamageType, resistance.Resistance);
            }

            profileInitialized = true;
            LastInitializationResult = new ActorProfileInitializationResult(ActorProfileInitializationStatus.InitializedFromProfile, $"{name} initialized from actor profile '{actorProfile.Id}'.");
        }

        private void ApplyProfileStatContributions()
        {
            if (actorProfile == null || calculatedStats == null) return;
            calculatedStats.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Other, actorProfile.Id, restoring: true);
            for (int i = 0; i < actorProfile.StatContributions.Count; i++)
            {
                ActorProfileStatContribution authored = actorProfile.StatContributions[i];
                if (authored == null || !authored.IsValid) continue;
                calculatedStats.AddContribution(new RuntimeCalculatedStatContribution
                {
                    contributionId = $"actor-profile.{actorProfile.Id}.{authored.Stat.Id}",
                    statId = authored.Stat.Id,
                    sourceId = actorProfile.Id,
                    sourceCategory = (int)CalculatedStatContributionSourceCategory.Other,
                    kind = (int)authored.Kind,
                    direction = (int)authored.Direction,
                    magnitude = authored.Magnitude,
                    priority = authored.Priority
                }, out _, restoring: true);
            }

            calculatedStats.ForceRecalculateAll(restoring: true);
        }

        private float GetCalculatedValue(string statId, float minimum = 0f)
        {
            if (calculatedStats == null || !calculatedStats.IsConfigured || !calculatedStats.HasStat(statId)) return minimum;
            return Mathf.Max(minimum, calculatedStats.GetValue(statId));
        }

        private void OnCalculatedStatsChanged(CalculatedStatCollection source, IReadOnlyList<string> statIds, bool restoring) => NotifyStatsChanged();
        private void OnRuntimeResistanceChanged(DamageTypeDefinition damageType, float value) => ResistanceChanged?.Invoke(damageType, value);
    }
}
