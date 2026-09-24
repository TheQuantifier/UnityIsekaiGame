using System;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Traits
{
    [Serializable]
    public sealed class TraitCalculatedStatContributionDefinition
    {
        [SerializeField] private string entryId;
        [SerializeField] private CalculatedStatDefinition calculatedStat;
        [SerializeField] private CalculatedStatContributionKind kind;
        [SerializeField] private CalculatedStatContributionDirection direction = CalculatedStatContributionDirection.Improve;
        [SerializeField] private float magnitude;
        [SerializeField] private int priority;

        public string EntryId => string.IsNullOrWhiteSpace(entryId) ? calculatedStat == null ? string.Empty : calculatedStat.Id : entryId;
        public CalculatedStatDefinition CalculatedStat => calculatedStat;
        public CalculatedStatContributionKind Kind => kind;
        public CalculatedStatContributionDirection Direction => direction;
        public float Magnitude => magnitude;
        public int Priority => priority;
    }

    [Serializable]
    public sealed class TraitAbilityGrantDefinition
    {
        [SerializeField] private string entryId;
        [SerializeField] private AbilityDefinition ability;
        [SerializeField] private TraitLifecycleState requiredLifecycle = TraitLifecycleState.Active;
        [SerializeField] private bool requireDiscovered;
        [SerializeField] private bool alphaEnabled = true;

        public string EntryId => string.IsNullOrWhiteSpace(entryId) ? AbilityId : entryId;
        public AbilityDefinition Ability => ability;
        public string AbilityId => ability == null ? string.Empty : ability.Id;
        public TraitLifecycleState RequiredLifecycle => requiredLifecycle;
        public bool RequireDiscovered => requireDiscovered;
        public bool AlphaEnabled => alphaEnabled;
    }

    [Serializable]
    public sealed class TraitCapabilityGrantDefinition
    {
        [SerializeField] private string entryId;
        [SerializeField] private CapabilityDefinition capability;
        [SerializeField] private bool booleanValue = true;
        [SerializeField] private float numericValue;
        [SerializeField] private bool blocker;
        [SerializeField] private int priority;
        [SerializeField] private bool alphaEnabled = true;

        public string EntryId => string.IsNullOrWhiteSpace(entryId) ? capability == null ? string.Empty : capability.Id : entryId;
        public CapabilityDefinition Capability => capability;
        public bool BooleanValue => booleanValue;
        public float NumericValue => numericValue;
        public bool Blocker => blocker;
        public int Priority => priority;
        public bool AlphaEnabled => alphaEnabled;
    }

    [Serializable]
    public sealed class TraitResistanceGrantDefinition
    {
        [SerializeField] private string entryId;
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField, Range(0f, 1f)] private float resistanceFraction;
        [SerializeField] private bool immunity;
        [SerializeField] private bool alphaEnabled = true;

        public string EntryId => string.IsNullOrWhiteSpace(entryId) ? damageType == null ? string.Empty : damageType.Id : entryId;
        public DamageTypeDefinition DamageType => damageType;
        public float ResistanceFraction => Mathf.Clamp01(resistanceFraction);
        public bool Immunity => immunity;
        public bool AlphaEnabled => alphaEnabled;
    }

    [Serializable]
    public sealed class TraitLinkedGrantDefinition
    {
        [SerializeField] private TraitDefinition trait;
        [SerializeField] private TraitLifecycleState lifecycle = TraitLifecycleState.Active;
        [SerializeField] private TraitDiscoveryState discovery = TraitDiscoveryState.Undiscovered;
        [SerializeField] private bool childEffectsDependOnParentActive = true;
        [SerializeField] private bool revokeOnParentRemoval = true;
        [SerializeField] private bool alphaEnabled = true;

        public TraitDefinition Trait => trait;
        public TraitLifecycleState Lifecycle => lifecycle;
        public TraitDiscoveryState Discovery => discovery;
        public bool ChildEffectsDependOnParentActive => childEffectsDependOnParentActive;
        public bool RevokeOnParentRemoval => revokeOnParentRemoval;
        public bool AlphaEnabled => alphaEnabled;
    }

    [Serializable]
    public sealed class TraitSkillGrantDefinition
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private SkillGrade startingGrade = SkillGrade.F;
        [SerializeField] private bool alphaEnabled = true;

        public SkillDefinition Skill => skill;
        public SkillGrade StartingGrade => SkillGradeUtility.Clamp(startingGrade);
        public bool AlphaEnabled => alphaEnabled;
    }
}
