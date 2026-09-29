using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Influence
{
    [CreateAssetMenu(fileName = "SocialInfluenceMethodDefinition", menuName = "Unity Isekai Game/Social/Social Influence Method Definition")]
    public sealed class SocialInfluenceMethodDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SocialInfluenceCategory category = SocialInfluenceCategory.Custom;
        [SerializeField] private SocialInfluenceIntent[] supportedIntents = Array.Empty<SocialInfluenceIntent>();
        [SerializeField] private SocialInfluenceSubjectKind[] supportedSubjectKinds = Array.Empty<SocialInfluenceSubjectKind>();
        [SerializeField] private int baseInfluence = 500;
        [SerializeField] private int baseResistance = 400;
        [SerializeField] private int evidenceWeight = 120;
        [SerializeField] private int relationshipWeight = 80;
        [SerializeField] private int reputationWeight = 60;
        [SerializeField] private int deceptionDetectionBase = 250;
        [SerializeField] private int maximumDecisionModifier = 120;
        [SerializeField] private double cooldownSeconds = 8d;
        [SerializeField] private double modifierDurationSeconds = 60d;
        [SerializeField] private bool deceptionAllowed;
        [SerializeField] private bool createsBeliefEvidence = true;
        [SerializeField] private bool allowsCompliance = true;
        [SerializeField] private bool allowsDecisionModifier = true;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => methodId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SocialInfluenceCategory Category => category;
        public IReadOnlyList<SocialInfluenceIntent> SupportedIntents => supportedIntents ?? Array.Empty<SocialInfluenceIntent>();
        public IReadOnlyList<SocialInfluenceSubjectKind> SupportedSubjectKinds => supportedSubjectKinds ?? Array.Empty<SocialInfluenceSubjectKind>();
        public int BaseInfluence => baseInfluence;
        public int BaseResistance => baseResistance;
        public int EvidenceWeight => evidenceWeight;
        public int RelationshipWeight => relationshipWeight;
        public int ReputationWeight => reputationWeight;
        public int DeceptionDetectionBase => deceptionDetectionBase;
        public int MaximumDecisionModifier => Math.Max(0, maximumDecisionModifier);
        public double CooldownSeconds => Math.Max(0d, cooldownSeconds);
        public double ModifierDurationSeconds => Math.Max(0d, modifierDurationSeconds);
        public bool DeceptionAllowed => deceptionAllowed;
        public bool CreatesBeliefEvidence => createsBeliefEvidence;
        public bool AllowsCompliance => allowsCompliance;
        public bool AllowsDecisionModifier => allowsDecisionModifier;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

        public void DevelopmentConfigure(
            string id,
            string name,
            SocialInfluenceCategory methodCategory,
            IEnumerable<SocialInfluenceIntent> intents,
            IEnumerable<SocialInfluenceSubjectKind> subjects,
            int influence,
            int resistance,
            int evidence,
            int relationship,
            int reputation,
            int detection,
            int maxDecisionModifier,
            double cooldown,
            double modifierDuration,
            bool deception,
            bool beliefEvidence,
            bool compliance,
            bool decisionModifier,
            IEnumerable<string> tagIds)
        {
            methodId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = methodCategory;
            supportedIntents = Clean(intents);
            supportedSubjectKinds = Clean(subjects);
            baseInfluence = influence;
            baseResistance = resistance;
            evidenceWeight = evidence;
            relationshipWeight = relationship;
            reputationWeight = reputation;
            deceptionDetectionBase = detection;
            maximumDecisionModifier = Math.Max(0, maxDecisionModifier);
            cooldownSeconds = Math.Max(0d, cooldown);
            modifierDurationSeconds = Math.Max(0d, modifierDuration);
            deceptionAllowed = deception;
            createsBeliefEvidence = beliefEvidence;
            allowsCompliance = compliance;
            allowsDecisionModifier = decisionModifier;
            tags = Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Social Influence Method '{name}' is missing a stable ID.");
            if (!Enum.IsDefined(typeof(SocialInfluenceCategory), category)) report.AddError($"Social Influence Method '{DisplayName}' has an invalid category.");
            if (supportedIntents == null || supportedIntents.Length == 0) report.AddError($"Social Influence Method '{DisplayName}' must declare at least one supported intent.");
            if (supportedSubjectKinds == null || supportedSubjectKinds.Length == 0) report.AddError($"Social Influence Method '{DisplayName}' must declare at least one supported subject kind.");
            if (cooldownSeconds < 0d || double.IsNaN(cooldownSeconds) || double.IsInfinity(cooldownSeconds)) report.AddError($"Social Influence Method '{DisplayName}' has an invalid cooldown.");
            if (modifierDurationSeconds < 0d || double.IsNaN(modifierDurationSeconds) || double.IsInfinity(modifierDurationSeconds)) report.AddError($"Social Influence Method '{DisplayName}' has an invalid modifier duration.");
            if (maximumDecisionModifier < 0 || maximumDecisionModifier > 1000) report.AddError($"Social Influence Method '{DisplayName}' has an out-of-range decision modifier cap.");
        }

        private static T[] Clean<T>(IEnumerable<T> values) where T : struct, Enum => (values ?? Array.Empty<T>()).Where(value => Enum.IsDefined(typeof(T), value)).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
