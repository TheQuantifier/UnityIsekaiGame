using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Skills
{
    [Serializable]
    public sealed class SkillNaturalLearningDefinition
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string qualifyingEventId;
        [SerializeField] private SkillActionEventCategory actionCategory;
        [SerializeField] private CategoryDefinition requiredItemCategory;
        [SerializeField] private TagDefinition requiredItemTag;
        [SerializeField] private TagDefinition requiredMagicTag;
        [SerializeField] private TagDefinition requiredActionTag;
        [SerializeField, Min(1)] private int requiredCount = 100;
        [SerializeField] private SkillGrade grantedStartingGrade = SkillGrade.F;

        public bool Enabled => enabled;
        public string QualifyingEventId => qualifyingEventId ?? string.Empty;
        public SkillActionEventCategory ActionCategory => actionCategory;
        public CategoryDefinition RequiredItemCategory => requiredItemCategory;
        public TagDefinition RequiredItemTag => requiredItemTag;
        public TagDefinition RequiredMagicTag => requiredMagicTag;
        public TagDefinition RequiredActionTag => requiredActionTag;
        public int RequiredCount => Math.Max(1, requiredCount);
        public SkillGrade GrantedStartingGrade => SkillGradeUtility.Clamp(grantedStartingGrade);

        public void Validate()
        {
            requiredCount = Math.Max(1, requiredCount);
            grantedStartingGrade = SkillGradeUtility.Clamp(grantedStartingGrade);
        }
    }

    [Serializable]
    public sealed class SkillXpThresholdDefinition
    {
        [SerializeField] private SkillGrade fromGrade;
        [SerializeField, Min(1)] private int xpRequired = 25;

        public SkillGrade FromGrade => SkillGradeUtility.Clamp(fromGrade);
        public int XpRequired => Math.Max(1, xpRequired);

        public void Validate()
        {
            fromGrade = SkillGradeUtility.Clamp(fromGrade);
            xpRequired = Math.Max(1, xpRequired);
        }
    }

    [Serializable]
    public sealed class SkillCalculatedStatContributionDefinition
    {
        [SerializeField] private CalculatedStatDefinition calculatedStat;
        [SerializeField] private CalculatedStatContributionKind kind = CalculatedStatContributionKind.Flat;
        [SerializeField] private CalculatedStatContributionDirection direction = CalculatedStatContributionDirection.Improve;
        [SerializeField, Min(0f)] private float magnitude;
        [SerializeField] private int priority;

        public CalculatedStatDefinition CalculatedStat => calculatedStat;
        public CalculatedStatContributionKind Kind => kind;
        public CalculatedStatContributionDirection Direction => direction;
        public float Magnitude => Mathf.Max(0f, magnitude);
        public int Priority => priority;

        public void Validate()
        {
            magnitude = Mathf.Max(0f, magnitude);
        }
    }

    [Serializable]
    public sealed class SkillAbilityUnlockDefinition
    {
        [SerializeField] private AbilityDefinition ability;
        [SerializeField] private SkillGrade requiredGrade;
        [SerializeField] private string sourceIdentity;
        [SerializeField] private bool alphaAvailable = true;
        [SerializeField] private string futureMetadata;

        public AbilityDefinition Ability => ability;
        public string AbilityId => ability == null ? string.Empty : ability.Id;
        public SkillGrade RequiredGrade => SkillGradeUtility.Clamp(requiredGrade);
        public string SourceIdentity => sourceIdentity ?? string.Empty;
        public bool AlphaAvailable => alphaAvailable;
        public string FutureMetadata => futureMetadata ?? string.Empty;

        public void Validate()
        {
            requiredGrade = SkillGradeUtility.Clamp(requiredGrade);
        }
    }

    [Serializable]
    public sealed class SkillGradeEffectPackageDefinition
    {
        [SerializeField] private SkillGrade grade;
        [SerializeField] private SkillCalculatedStatContributionDefinition[] calculatedStatContributions;
        [SerializeField] private SkillAbilityUnlockDefinition[] abilityUnlocks;
        [SerializeField] private string[] capabilityUnlockIds;
        [SerializeField] private string futureInteractionMetadata;

        public SkillGrade Grade => SkillGradeUtility.Clamp(grade);
        public IReadOnlyList<SkillCalculatedStatContributionDefinition> CalculatedStatContributions => calculatedStatContributions ?? Array.Empty<SkillCalculatedStatContributionDefinition>();
        public IReadOnlyList<SkillAbilityUnlockDefinition> AbilityUnlocks => abilityUnlocks ?? Array.Empty<SkillAbilityUnlockDefinition>();
        public IReadOnlyList<string> CapabilityUnlockIds => capabilityUnlockIds ?? Array.Empty<string>();
        public string FutureInteractionMetadata => futureInteractionMetadata ?? string.Empty;

        public void Validate()
        {
            grade = SkillGradeUtility.Clamp(grade);
            if (calculatedStatContributions != null)
            {
                for (int i = 0; i < calculatedStatContributions.Length; i++)
                {
                    calculatedStatContributions[i]?.Validate();
                }
            }

            if (abilityUnlocks != null)
            {
                for (int i = 0; i < abilityUnlocks.Length; i++)
                {
                    abilityUnlocks[i]?.Validate();
                }
            }
        }
    }

    [Serializable]
    public sealed class SkillGrantDefinition
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private SkillGrade startingGrade = SkillGrade.F;
        [SerializeField] private string reason = "Direct Skill grant";

        public SkillDefinition Skill => skill;
        public SkillGrade StartingGrade => SkillGradeUtility.Clamp(startingGrade);
        public string Reason => string.IsNullOrWhiteSpace(reason) ? "Direct Skill grant" : reason;

        public void Validate()
        {
            startingGrade = SkillGradeUtility.Clamp(startingGrade);
        }
    }
}
