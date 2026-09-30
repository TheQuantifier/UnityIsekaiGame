using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Decisions
{
    [CreateAssetMenu(fileName = "SocialConsiderationDefinition", menuName = "Unity Isekai Game/Social/Social Consideration Definition")]
    public sealed class SocialConsiderationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string considerationId;
        [SerializeField] private string displayName;
        [SerializeField] private SocialDecisionConsiderationInput input = SocialDecisionConsiderationInput.Constant;
        [SerializeField] private SocialDecisionResponseCurve responseCurve = SocialDecisionResponseCurve.Linear;
        [SerializeField] private SocialDecisionMissingDataPolicy missingDataPolicy = SocialDecisionMissingDataPolicy.Neutral;
        [SerializeField] private int inputMinimum;
        [SerializeField] private int inputMaximum = 100;
        [SerializeField] private int weight = 100;
        [SerializeField] private bool required;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => considerationId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public SocialDecisionConsiderationInput Input => input;
        public SocialDecisionResponseCurve ResponseCurve => responseCurve;
        public SocialDecisionMissingDataPolicy MissingDataPolicy => missingDataPolicy;
        public int InputMinimum => inputMinimum;
        public int InputMaximum => inputMaximum;
        public int Weight => weight;
        public bool Required => required;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(string id, string name, SocialDecisionConsiderationInput source, SocialDecisionResponseCurve curve, int minimum, int maximum, int authoredWeight, SocialDecisionMissingDataPolicy missingPolicy, bool requiredInput, IEnumerable<string> tagIds)
        {
            considerationId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            input = source;
            responseCurve = curve;
            inputMinimum = minimum;
            inputMaximum = maximum;
            weight = authoredWeight;
            missingDataPolicy = missingPolicy;
            required = requiredInput;
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Social Consideration '{name}' is missing a stable ID.");
            if (!Enum.IsDefined(typeof(SocialDecisionConsiderationInput), input)) report.AddError($"Social Consideration '{DisplayName}' has an unknown input source.");
            if (!Enum.IsDefined(typeof(SocialDecisionResponseCurve), responseCurve)) report.AddError($"Social Consideration '{DisplayName}' has an unknown response curve.");
            if (!Enum.IsDefined(typeof(SocialDecisionMissingDataPolicy), missingDataPolicy)) report.AddError($"Social Consideration '{DisplayName}' has an unknown missing-data policy.");
            if (inputMaximum <= inputMinimum) report.AddError($"Social Consideration '{DisplayName}' has an invalid input range.");
            if (weight < -1000 || weight > 1000) report.AddError($"Social Consideration '{DisplayName}' has an out-of-range weight.");
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
