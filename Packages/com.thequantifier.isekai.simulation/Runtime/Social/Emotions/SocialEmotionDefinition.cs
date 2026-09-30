using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Emotions
{
    [CreateAssetMenu(fileName = "SocialEmotionDefinition", menuName = "Unity Isekai Game/Social/Social Emotion Definition")]
    public sealed class SocialEmotionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string emotionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialEmotionCategory category = SocialEmotionCategory.Custom;
        [SerializeField] private SocialEmotionValence valence;
        [SerializeField] private SocialEmotionArousal arousal = SocialEmotionArousal.Medium;
        [SerializeField] private SocialEmotionDecayPolicy decayPolicy = SocialEmotionDecayPolicy.Linear;
        [SerializeField] private SocialEmotionStackingPolicy stackingPolicy = SocialEmotionStackingPolicy.ReinforceExisting;
        [SerializeField] private SocialEmotionTargetPolicy targetPolicy = SocialEmotionTargetPolicy.PersonOrSubject;
        [SerializeField] private SocialEmotionVisibility defaultVisibility = SocialEmotionVisibility.Internal;
        [SerializeField] private string primaryMoodDimensionId;
        [SerializeField] private int minimumIntensity = 0;
        [SerializeField] private int maximumIntensity = 100;
        [SerializeField] private int defaultIntensity = 40;
        [SerializeField] private double defaultDurationSeconds = 120d;
        [SerializeField] private int moodContribution = 20;
        [SerializeField] private int decisionScoreModifier = 0;
        [SerializeField] private bool canBeSuppressed = true;
        [SerializeField] private bool canBeConcealed = true;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => emotionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SocialEmotionCategory Category => category;
        public SocialEmotionValence Valence => valence;
        public SocialEmotionArousal Arousal => arousal;
        public SocialEmotionDecayPolicy DecayPolicy => decayPolicy;
        public SocialEmotionStackingPolicy StackingPolicy => stackingPolicy;
        public SocialEmotionTargetPolicy TargetPolicy => targetPolicy;
        public SocialEmotionVisibility DefaultVisibility => defaultVisibility;
        public string PrimaryMoodDimensionId => primaryMoodDimensionId ?? string.Empty;
        public int MinimumIntensity => minimumIntensity;
        public int MaximumIntensity => maximumIntensity;
        public int DefaultIntensity => defaultIntensity;
        public double DefaultDurationSeconds => defaultDurationSeconds;
        public int MoodContribution => moodContribution;
        public int DecisionScoreModifier => decisionScoreModifier;
        public bool CanBeSuppressed => canBeSuppressed;
        public bool CanBeConcealed => canBeConcealed;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(
            string id,
            string name,
            SocialEmotionCategory emotionCategory,
            SocialEmotionValence emotionValence,
            SocialEmotionArousal emotionArousal,
            SocialEmotionDecayPolicy emotionDecayPolicy,
            SocialEmotionStackingPolicy emotionStackingPolicy,
            SocialEmotionTargetPolicy emotionTargetPolicy,
            SocialEmotionVisibility visibility,
            string moodDimensionId,
            int minimum,
            int maximum,
            int defaultValue,
            double durationSeconds,
            int moodDelta,
            int decisionModifier,
            bool suppressible,
            bool concealable,
            IEnumerable<string> tagIds)
        {
            emotionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = emotionCategory;
            valence = emotionValence;
            arousal = emotionArousal;
            decayPolicy = emotionDecayPolicy;
            stackingPolicy = emotionStackingPolicy;
            targetPolicy = emotionTargetPolicy;
            defaultVisibility = visibility;
            primaryMoodDimensionId = moodDimensionId?.Trim();
            minimumIntensity = Math.Max(0, minimum);
            maximumIntensity = Math.Max(minimumIntensity, maximum);
            defaultIntensity = Math.Max(minimumIntensity, Math.Min(maximumIntensity, defaultValue));
            defaultDurationSeconds = Math.Max(0d, durationSeconds);
            moodContribution = moodDelta;
            decisionScoreModifier = decisionModifier;
            canBeSuppressed = suppressible;
            canBeConcealed = concealable;
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Social Emotion '{name}' is missing a stable ID.");
            if (!Enum.IsDefined(typeof(SocialEmotionCategory), category)) report.AddError($"Social Emotion '{DisplayName}' has an invalid category.");
            if (!Enum.IsDefined(typeof(SocialEmotionValence), valence)) report.AddError($"Social Emotion '{DisplayName}' has an invalid valence.");
            if (!Enum.IsDefined(typeof(SocialEmotionArousal), arousal)) report.AddError($"Social Emotion '{DisplayName}' has an invalid arousal.");
            if (!Enum.IsDefined(typeof(SocialEmotionDecayPolicy), decayPolicy)) report.AddError($"Social Emotion '{DisplayName}' has an invalid decay policy.");
            if (!Enum.IsDefined(typeof(SocialEmotionStackingPolicy), stackingPolicy)) report.AddError($"Social Emotion '{DisplayName}' has an invalid stacking policy.");
            if (!Enum.IsDefined(typeof(SocialEmotionTargetPolicy), targetPolicy)) report.AddError($"Social Emotion '{DisplayName}' has an invalid target policy.");
            if (maximumIntensity < minimumIntensity) report.AddError($"Social Emotion '{DisplayName}' has an invalid intensity range.");
            if (defaultIntensity < minimumIntensity || defaultIntensity > maximumIntensity) report.AddError($"Social Emotion '{DisplayName}' has a default intensity outside its range.");
            if (defaultDurationSeconds < 0d || double.IsNaN(defaultDurationSeconds) || double.IsInfinity(defaultDurationSeconds)) report.AddError($"Social Emotion '{DisplayName}' has an invalid duration.");
            if (!string.IsNullOrWhiteSpace(primaryMoodDimensionId) && definitionsById != null && !definitionsById.ContainsKey(primaryMoodDimensionId))
            {
                report.AddError($"Social Emotion '{DisplayName}' references missing Mood Dimension '{primaryMoodDimensionId}'.");
            }
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
