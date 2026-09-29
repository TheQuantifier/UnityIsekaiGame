using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Emotions
{
    [CreateAssetMenu(fileName = "SocialMoodDimensionDefinition", menuName = "Unity Isekai Game/Social/Social Mood Dimension Definition")]
    public sealed class SocialMoodDimensionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string moodDimensionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialMoodDimensionCategory category = SocialMoodDimensionCategory.Custom;
        [SerializeField] private int minimumValue = -100;
        [SerializeField] private int maximumValue = 100;
        [SerializeField] private int neutralValue;
        [SerializeField] private double recoveryPerSecond = 0.05d;
        [SerializeField] private bool negativeValuesAllowed = true;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => moodDimensionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SocialMoodDimensionCategory Category => category;
        public int MinimumValue => minimumValue;
        public int MaximumValue => maximumValue;
        public int NeutralValue => neutralValue;
        public double RecoveryPerSecond => recoveryPerSecond;
        public bool NegativeValuesAllowed => negativeValuesAllowed;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(string id, string name, SocialMoodDimensionCategory moodCategory, int minimum, int maximum, int neutral, double recovery, bool negativeAllowed, string text, IEnumerable<string> tagIds)
        {
            moodDimensionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = text ?? string.Empty;
            category = moodCategory;
            minimumValue = negativeAllowed ? minimum : Math.Max(0, minimum);
            maximumValue = Math.Max(minimumValue, maximum);
            neutralValue = Math.Max(minimumValue, Math.Min(maximumValue, neutral));
            recoveryPerSecond = Math.Max(0d, recovery);
            negativeValuesAllowed = negativeAllowed;
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Mood Dimension '{name}' is missing a stable ID.");
            if (!Enum.IsDefined(typeof(SocialMoodDimensionCategory), category)) report.AddError($"Mood Dimension '{DisplayName}' has an invalid category.");
            if (maximumValue < minimumValue) report.AddError($"Mood Dimension '{DisplayName}' has an invalid value range.");
            if (neutralValue < minimumValue || neutralValue > maximumValue) report.AddError($"Mood Dimension '{DisplayName}' has a neutral value outside its range.");
            if (!negativeValuesAllowed && minimumValue < 0) report.AddError($"Mood Dimension '{DisplayName}' disallows negative values but has a negative minimum.");
            if (recoveryPerSecond < 0d || double.IsNaN(recoveryPerSecond) || double.IsInfinity(recoveryPerSecond)) report.AddError($"Mood Dimension '{DisplayName}' has an invalid recovery rate.");
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
