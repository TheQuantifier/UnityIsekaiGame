using System;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Beings.Biology
{
    [Serializable]
    public sealed class BiologicalTraitGrantDefinition
    {
        [SerializeField] private TraitDefinition trait;
        [SerializeField] private TraitLifecycleState lifecycle = TraitLifecycleState.Active;
        [SerializeField] private TraitDiscoveryState discovery = TraitDiscoveryState.Discovered;
        [SerializeField] private bool alphaEnabled = true;

        public TraitDefinition Trait => trait;
        public TraitLifecycleState Lifecycle => lifecycle;
        public TraitDiscoveryState Discovery => discovery;
        public bool AlphaEnabled => alphaEnabled;
    }

    [Serializable]
    public sealed class BiologicalCapabilityGrantDefinition
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
    public sealed class BiologicalStatContributionDefinition
    {
        [SerializeField] private string contributionId;
        [SerializeField] private CalculatedStatDefinition calculatedStat;
        [SerializeField] private CalculatedStatContributionKind kind = CalculatedStatContributionKind.Flat;
        [SerializeField] private CalculatedStatContributionDirection direction = CalculatedStatContributionDirection.Improve;
        [SerializeField] private float magnitude;
        [SerializeField] private int priority;
        [SerializeField] private bool alphaEnabled = true;

        public string ContributionId => string.IsNullOrWhiteSpace(contributionId) ? calculatedStat == null ? string.Empty : calculatedStat.Id : contributionId;
        public CalculatedStatDefinition CalculatedStat => calculatedStat;
        public CalculatedStatContributionKind Kind => kind;
        public CalculatedStatContributionDirection Direction => direction;
        public float Magnitude => magnitude;
        public int Priority => priority;
        public bool AlphaEnabled => alphaEnabled;
    }
}
