using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Rumors
{
    [CreateAssetMenu(fileName = "RumorDefinition", menuName = "Unity Isekai Game/Social/Rumor Definition")]
    public sealed class RumorDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string rumorDefinitionId = "rumor.definition.prototype";
        [SerializeField] private string displayName = "Prototype Rumor";
        [SerializeField, TextArea] private string description;
        [SerializeField] private RumorCategory category = RumorCategory.Custom;
        [SerializeField] private RumorDisclosure defaultDisclosure = RumorDisclosure.Shareable;
        [SerializeField, Range(0, 1000)] private int defaultSalience = 500;
        [SerializeField, Range(0, 1000)] private int defaultMemorability = 500;
        [SerializeField, Range(0, 1000)] private int defaultTransmissionDifficulty = 250;
        [SerializeField] private RumorDistortionPolicy defaultDistortionPolicy = RumorDistortionPolicy.None;
        [SerializeField] private bool retransmissionAllowed = true;
        [SerializeField] private bool anonymousSourcingAllowed = true;
        [SerializeField] private bool originalSourceMayBeConcealed = true;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField, Min(1)] private int version = 1;

        public string Id => RumorDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RumorDefinitionId : displayName;
        public string RumorDefinitionId => rumorDefinitionId ?? string.Empty;
        public RumorCategory Category => category;
        public RumorDisclosure DefaultDisclosure => defaultDisclosure;
        public int DefaultSalience => Clamp(defaultSalience);
        public int DefaultMemorability => Clamp(defaultMemorability);
        public int DefaultTransmissionDifficulty => Clamp(defaultTransmissionDifficulty);
        public RumorDistortionPolicy DefaultDistortionPolicy => defaultDistortionPolicy;
        public bool RetransmissionAllowed => retransmissionAllowed;
        public bool AnonymousSourcingAllowed => anonymousSourcingAllowed;
        public bool OriginalSourceMayBeConcealed => originalSourceMayBeConcealed;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => Math.Max(1, version);

        // Prototype definitions are also created by the runtime registry in release players,
        // so this configurator must remain available outside editor/development builds.
        public void DevelopmentConfigure(
            string id,
            string name,
            RumorCategory rumorCategory,
            RumorDisclosure disclosure,
            RumorDistortionPolicy distortionPolicy,
            bool allowRetransmission,
            bool allowAnonymousSource,
            bool allowSourceConcealment,
            int salience = 500,
            int memorability = 500,
            int transmissionDifficulty = 250,
            string text = "",
            string[] tagIds = null)
        {
            rumorDefinitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            category = rumorCategory;
            defaultDisclosure = disclosure;
            defaultDistortionPolicy = distortionPolicy;
            retransmissionAllowed = allowRetransmission;
            anonymousSourcingAllowed = allowAnonymousSource;
            originalSourceMayBeConcealed = allowSourceConcealment;
            defaultSalience = salience;
            defaultMemorability = memorability;
            defaultTransmissionDifficulty = transmissionDifficulty;
            tags = tagIds ?? Array.Empty<string>();
        }
        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(RumorDefinitionId))
            {
                report.AddError("Rumor definition must declare a stable ID.");
            }
            else if (!RumorDefinitionId.StartsWith("rumor.", StringComparison.Ordinal))
            {
                report.AddError($"Rumor definition '{DisplayName}' ID '{RumorDefinitionId}' must start with 'rumor.'.");
            }

            if (!Enum.IsDefined(typeof(RumorCategory), category) || category == RumorCategory.Unknown)
            {
                report.AddError($"Rumor definition '{DisplayName}' must declare a concrete category.");
            }

            if (!Enum.IsDefined(typeof(RumorDisclosure), defaultDisclosure))
            {
                report.AddError($"Rumor definition '{DisplayName}' has invalid disclosure.");
            }

            if (!Enum.IsDefined(typeof(RumorDistortionPolicy), defaultDistortionPolicy))
            {
                report.AddError($"Rumor definition '{DisplayName}' has invalid distortion policy.");
            }

            if (DefaultSalience != defaultSalience || DefaultMemorability != defaultMemorability || DefaultTransmissionDifficulty != defaultTransmissionDifficulty)
            {
                report.AddError($"Rumor definition '{DisplayName}' has out-of-range numeric defaults.");
            }

            if (defaultDisclosure >= RumorDisclosure.Secret && !OriginalSourceMayBeConcealed && AnonymousSourcingAllowed)
            {
                report.AddError($"Rumor definition '{DisplayName}' cannot allow anonymous sourcing while forcing original source disclosure for secret information.");
            }

            foreach (string tag in Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.AddError($"Rumor definition '{DisplayName}' contains a blank tag.");
                    break;
                }
            }
        }

        private static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(1000, value));
        }
    }
}
