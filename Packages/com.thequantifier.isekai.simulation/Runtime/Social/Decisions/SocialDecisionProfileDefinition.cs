using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Decisions
{
    [CreateAssetMenu(fileName = "SocialDecisionProfileDefinition", menuName = "Unity Isekai Game/Social/Social Decision Profile Definition")]
    public sealed class SocialDecisionProfileDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField] private string[] enabledIntentionIds = Array.Empty<string>();
        [SerializeField] private string[] considerationIds = Array.Empty<string>();
        [SerializeField] private double evaluationIntervalSeconds = 15d;
        [SerializeField] private int maximumTargets = 8;
        [SerializeField] private int maximumIntentions = 8;
        [SerializeField] private int maximumCandidates = 24;
        [SerializeField] private int maximumDiagnostics = 24;
        [SerializeField] private int scoreThreshold = 100;
        [SerializeField] private int maximumActionsPerWindow = 1;
        [SerializeField] private double actionWindowSeconds = 30d;
        [SerializeField] private bool allowPlayerControlled;
        [SerializeField] private SocialDecisionExecutionMode defaultExecutionMode = SocialDecisionExecutionMode.EvaluateOnly;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => profileId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public IReadOnlyList<string> EnabledIntentionIds => enabledIntentionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ConsiderationIds => considerationIds ?? Array.Empty<string>();
        public double EvaluationIntervalSeconds => Math.Max(0d, evaluationIntervalSeconds);
        public int MaximumTargets => Math.Max(0, maximumTargets);
        public int MaximumIntentions => Math.Max(0, maximumIntentions);
        public int MaximumCandidates => Math.Max(0, maximumCandidates);
        public int MaximumDiagnostics => Math.Max(0, maximumDiagnostics);
        public int ScoreThreshold => scoreThreshold;
        public int MaximumActionsPerWindow => Math.Max(0, maximumActionsPerWindow);
        public double ActionWindowSeconds => Math.Max(0d, actionWindowSeconds);
        public bool AllowPlayerControlled => allowPlayerControlled;
        public SocialDecisionExecutionMode DefaultExecutionMode => defaultExecutionMode;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(string id, string name, IEnumerable<string> intentions, IEnumerable<string> considerations, double interval, int maxTargets, int maxIntentions, int maxCandidates, int threshold, SocialDecisionExecutionMode mode, bool playersAllowed, IEnumerable<string> tagIds)
        {
            profileId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            enabledIntentionIds = Clean(intentions);
            considerationIds = Clean(considerations);
            evaluationIntervalSeconds = Math.Max(0d, interval);
            maximumTargets = Math.Max(0, maxTargets);
            maximumIntentions = Math.Max(0, maxIntentions);
            maximumCandidates = Math.Max(0, maxCandidates);
            maximumDiagnostics = Math.Max(maxCandidates, 1);
            scoreThreshold = threshold;
            defaultExecutionMode = mode;
            allowPlayerControlled = playersAllowed;
            maximumActionsPerWindow = 1;
            actionWindowSeconds = Math.Max(interval, 1d);
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Social Decision Profile '{name}' is missing a stable ID.");
            if (evaluationIntervalSeconds < 0d || double.IsNaN(evaluationIntervalSeconds) || double.IsInfinity(evaluationIntervalSeconds)) report.AddError($"Social Decision Profile '{DisplayName}' has an invalid evaluation interval.");
            if (maximumTargets < 0 || maximumIntentions < 0 || maximumCandidates < 0 || maximumActionsPerWindow < 0) report.AddError($"Social Decision Profile '{DisplayName}' has invalid limits.");
            foreach (string intentionId in enabledIntentionIds ?? Array.Empty<string>())
            {
                if (!definitionsById.ContainsKey(intentionId)) report.AddError($"Social Decision Profile '{DisplayName}' references missing Social Intention '{intentionId}'.");
            }
            foreach (string considerationId in considerationIds ?? Array.Empty<string>())
            {
                if (!definitionsById.ContainsKey(considerationId)) report.AddError($"Social Decision Profile '{DisplayName}' references missing Social Consideration '{considerationId}'.");
            }
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
