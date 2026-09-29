using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Decisions
{
    [CreateAssetMenu(fileName = "SocialIntentionDefinition", menuName = "Unity Isekai Game/Social/Social Intention Definition")]
    public sealed class SocialIntentionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string intentionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialIntentionCategory category = SocialIntentionCategory.Custom;
        [SerializeField] private string[] eligibleInteractionDefinitionIds = Array.Empty<string>();
        [SerializeField] private int basePriority = 100;
        [SerializeField] private double cooldownSeconds = 10d;
        [SerializeField] private bool requiresTarget = true;
        [SerializeField] private bool allowNoInteractionSelection;
        [SerializeField] private string[] considerationIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => intentionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SocialIntentionCategory Category => category;
        public IReadOnlyList<string> EligibleInteractionDefinitionIds => eligibleInteractionDefinitionIds ?? Array.Empty<string>();
        public int BasePriority => basePriority;
        public double CooldownSeconds => Math.Max(0d, cooldownSeconds);
        public bool RequiresTarget => requiresTarget;
        public bool AllowNoInteractionSelection => allowNoInteractionSelection;
        public IReadOnlyList<string> ConsiderationIds => considerationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(string id, string name, SocialIntentionCategory intentionCategory, IEnumerable<string> interactionIds, int priority, double cooldown, bool targetRequired, bool noInteraction, IEnumerable<string> considerations, IEnumerable<string> tagIds)
        {
            intentionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = intentionCategory;
            eligibleInteractionDefinitionIds = Clean(interactionIds);
            basePriority = priority;
            cooldownSeconds = Math.Max(0d, cooldown);
            requiresTarget = targetRequired;
            allowNoInteractionSelection = noInteraction;
            considerationIds = Clean(considerations);
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Social Intention '{name}' is missing a stable ID.");
            if (!Enum.IsDefined(typeof(SocialIntentionCategory), category)) report.AddError($"Social Intention '{DisplayName}' has an invalid category.");
            if (basePriority < 0) report.AddError($"Social Intention '{DisplayName}' has a negative base priority.");
            if (double.IsNaN(cooldownSeconds) || double.IsInfinity(cooldownSeconds) || cooldownSeconds < 0d) report.AddError($"Social Intention '{DisplayName}' has an invalid cooldown.");
            if (!allowNoInteractionSelection && (eligibleInteractionDefinitionIds == null || eligibleInteractionDefinitionIds.Length == 0)) report.AddError($"Social Intention '{DisplayName}' must declare an interaction or allow no-interaction selection.");
            foreach (string interactionId in eligibleInteractionDefinitionIds ?? Array.Empty<string>())
            {
                if (!definitionsById.ContainsKey(interactionId)) report.AddError($"Social Intention '{DisplayName}' references missing Social Interaction '{interactionId}'.");
            }
            foreach (string considerationId in considerationIds ?? Array.Empty<string>())
            {
                if (!definitionsById.ContainsKey(considerationId)) report.AddError($"Social Intention '{DisplayName}' references missing Social Consideration '{considerationId}'.");
            }
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

}
