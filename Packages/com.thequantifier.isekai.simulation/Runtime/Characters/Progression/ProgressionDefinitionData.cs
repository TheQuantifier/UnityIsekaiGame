using System;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Progression
{
    [Serializable]
    public sealed class PermanentAttributeGrantDefinition
    {
        [SerializeField] private AttributeDefinition attribute;
        [SerializeField, Min(0f)] private float amount;

        public AttributeDefinition Attribute => attribute;
        public float Amount => Mathf.Max(0f, amount);
        public bool IsValid => attribute != null && amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);
    }

    [Serializable]
    public sealed class ProgressionCurrencyGrantDefinition
    {
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField, Min(0)] private long baseAmount;
        [SerializeField, Min(0)] private long randomVariation;

        public CurrencyDefinition Currency => currency;
        public long BaseAmount => Math.Max(0L, baseAmount);
        public long RandomVariation => Math.Max(0L, randomVariation);
    }

    [Serializable]
    public sealed class RarityWeightModifierDefinition
    {
        [SerializeField] private RarityDefinition rarity;
        [SerializeField, Min(0f)] private float weightMultiplier = 1f;

        public RarityDefinition Rarity => rarity;
        public float WeightMultiplier => Mathf.Max(0f, weightMultiplier);
        public float RawWeightMultiplier => weightMultiplier;
    }

    [Serializable]
    public sealed class BirthGiftWeightModifierDefinition
    {
        [SerializeField] private BirthGiftDefinition gift;
        [SerializeField, Min(0f)] private float weightMultiplier = 1f;

        public BirthGiftDefinition Gift => gift;
        public float WeightMultiplier => Mathf.Max(0f, weightMultiplier);
        public float RawWeightMultiplier => weightMultiplier;
    }

    [Serializable]
    public sealed class SocialStatusAssignmentDefinition
    {
        [SerializeField] private SocialStatusDefinition socialStatus;
        [SerializeField] private SocialStatusContextKind contextKind;
        [SerializeField] private FactionDefinition factionContext;
        [SerializeField] private PlaceDefinition placeContext;
        [SerializeField] private PersonDefinition personContext;
        [SerializeField] private string customContextTargetId;
        [SerializeField] private string acquisitionReason = "Origin assignment";

        public SocialStatusDefinition SocialStatus => socialStatus;
        public SocialStatusContextKind ContextKind => contextKind;
        public FactionDefinition FactionContext => factionContext;
        public PlaceDefinition PlaceContext => placeContext;
        public PersonDefinition PersonContext => personContext;
        public string CustomContextTargetId => customContextTargetId ?? string.Empty;
        public string AcquisitionReason => string.IsNullOrWhiteSpace(acquisitionReason) ? "Origin assignment" : acquisitionReason;

        public string ResolveContextTargetId()
        {
            return contextKind switch
            {
                SocialStatusContextKind.Faction or SocialStatusContextKind.Government or SocialStatusContextKind.Organization => factionContext == null ? CustomContextTargetId : factionContext.Id,
                SocialStatusContextKind.Place or SocialStatusContextKind.Jurisdiction => placeContext == null ? CustomContextTargetId : placeContext.Id,
                SocialStatusContextKind.Person => personContext == null ? CustomContextTargetId : personContext.Id,
                _ => string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ProgressionAbilityReference
    {
        [SerializeField] private AbilityDefinition ability;

        public AbilityDefinition Ability => ability;
        public string AbilityId => ability == null ? string.Empty : ability.Id;
    }

    [Serializable]
    public sealed class ProgressionPolicyPayload
    {
        [SerializeField] private string key;
        [SerializeField] private string value;

        public string Key => key ?? string.Empty;
        public string Value => value ?? string.Empty;
    }

    [Serializable]
    public sealed class ProgressionResistanceModifierDefinition
    {
        [SerializeField] private ResistanceModifierDefinition modifier;

        public ResistanceModifierDefinition Modifier => modifier;
    }
}
