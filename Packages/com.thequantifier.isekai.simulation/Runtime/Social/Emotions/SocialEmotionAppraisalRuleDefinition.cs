using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Influence;

namespace UnityIsekaiGame.Social.Emotions
{
    [CreateAssetMenu(fileName = "SocialEmotionAppraisalRuleDefinition", menuName = "Unity Isekai Game/Social/Social Emotion Appraisal Rule Definition")]
    public sealed class SocialEmotionAppraisalRuleDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string appraisalRuleId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string emotionDefinitionId;
        [SerializeField] private SocialEmotionCauseCategory causeCategory = SocialEmotionCauseCategory.Custom;
        [SerializeField] private SocialEmotionResponsibility responsibility = SocialEmotionResponsibility.Unknown;
        [SerializeField] private SocialInfluenceTruthStatus requiredBeliefTruthStatus = SocialInfluenceTruthStatus.Unknown;
        [SerializeField] private SocialInfluenceDetectionOutcome minimumDetectionOutcome = SocialInfluenceDetectionOutcome.NotApplicable;
        [SerializeField] private int priority = 100;
        [SerializeField] private int baseIntensity = 40;
        [SerializeField] private double durationSeconds = 120d;
        [SerializeField] private string targetMoodDimensionId;
        [SerializeField] private int moodContributionOverride;
        [SerializeField] private int decisionModifierOverride;
        [SerializeField] private string[] requiredTags = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => appraisalRuleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public string EmotionDefinitionId => emotionDefinitionId ?? string.Empty;
        public SocialEmotionCauseCategory CauseCategory => causeCategory;
        public SocialEmotionResponsibility Responsibility => responsibility;
        public SocialInfluenceTruthStatus RequiredBeliefTruthStatus => requiredBeliefTruthStatus;
        public SocialInfluenceDetectionOutcome MinimumDetectionOutcome => minimumDetectionOutcome;
        public int Priority => priority;
        public int BaseIntensity => baseIntensity;
        public double DurationSeconds => durationSeconds;
        public string TargetMoodDimensionId => targetMoodDimensionId ?? string.Empty;
        public int MoodContributionOverride => moodContributionOverride;
        public int DecisionModifierOverride => decisionModifierOverride;
        public IReadOnlyList<string> RequiredTags => requiredTags ?? Array.Empty<string>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(
            string id,
            string name,
            string emotionId,
            SocialEmotionCauseCategory cause,
            SocialEmotionResponsibility causeResponsibility,
            SocialInfluenceTruthStatus requiredTruth,
            SocialInfluenceDetectionOutcome minimumDetection,
            int rulePriority,
            int intensity,
            double duration,
            string moodDimensionId,
            int moodOverride,
            int decisionOverride,
            IEnumerable<string> requiredTagIds,
            IEnumerable<string> tagIds)
        {
            appraisalRuleId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            emotionDefinitionId = emotionId?.Trim();
            causeCategory = cause;
            responsibility = causeResponsibility;
            requiredBeliefTruthStatus = requiredTruth;
            minimumDetectionOutcome = minimumDetection;
            priority = rulePriority;
            baseIntensity = Math.Max(0, intensity);
            durationSeconds = Math.Max(0d, duration);
            targetMoodDimensionId = moodDimensionId?.Trim();
            moodContributionOverride = moodOverride;
            decisionModifierOverride = decisionOverride;
            requiredTags = Clean(requiredTagIds);
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Emotion Appraisal Rule '{name}' is missing a stable ID.");
            if (string.IsNullOrWhiteSpace(emotionDefinitionId) || definitionsById == null || !definitionsById.ContainsKey(emotionDefinitionId))
            {
                report.AddError($"Emotion Appraisal Rule '{DisplayName}' references missing Emotion Definition '{emotionDefinitionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(targetMoodDimensionId) && definitionsById != null && !definitionsById.ContainsKey(targetMoodDimensionId))
            {
                report.AddError($"Emotion Appraisal Rule '{DisplayName}' references missing Mood Dimension '{targetMoodDimensionId}'.");
            }

            if (!Enum.IsDefined(typeof(SocialEmotionCauseCategory), causeCategory)) report.AddError($"Emotion Appraisal Rule '{DisplayName}' has an invalid cause category.");
            if (!Enum.IsDefined(typeof(SocialEmotionResponsibility), responsibility)) report.AddError($"Emotion Appraisal Rule '{DisplayName}' has an invalid responsibility.");
            if (durationSeconds < 0d || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds)) report.AddError($"Emotion Appraisal Rule '{DisplayName}' has an invalid duration.");
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
