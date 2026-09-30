using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Career Transition Definition")]
    public sealed class CareerTransitionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CareerTransitionCategory category = CareerTransitionCategory.Custom;
        [SerializeField] private CareerEpisodeState[] allowedSourceStates = Array.Empty<CareerEpisodeState>();
        [SerializeField] private CareerEpisodeState[] allowedDestinationStates = Array.Empty<CareerEpisodeState>();
        [SerializeField] private CareerTransitionSourceRecordType[] requiredSourceRecordTypes = Array.Empty<CareerTransitionSourceRecordType>();
        [SerializeField] private string[] requiredProfessionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private bool voluntary = true;
        [SerializeField] private bool authorityApprovalRequired;
        [SerializeField] private bool mayBeDisputed;
        [SerializeField] private bool secretAllowed;
        [SerializeField] private string historySignificancePolicyId;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public CareerTransitionCategory Category => category;
        public IReadOnlyList<CareerEpisodeState> AllowedSourceStates => allowedSourceStates ?? Array.Empty<CareerEpisodeState>();
        public IReadOnlyList<CareerEpisodeState> AllowedDestinationStates => allowedDestinationStates ?? Array.Empty<CareerEpisodeState>();
        public IReadOnlyList<CareerTransitionSourceRecordType> RequiredSourceRecordTypes => requiredSourceRecordTypes ?? Array.Empty<CareerTransitionSourceRecordType>();
        public IReadOnlyList<string> RequiredProfessionIds => Clean(requiredProfessionIds);
        public IReadOnlyList<string> RequiredRankDefinitionIds => Clean(requiredRankDefinitionIds);
        public IReadOnlyList<string> RequiredCredentialDefinitionIds => Clean(requiredCredentialDefinitionIds);
        public bool Voluntary => voluntary;
        public bool AuthorityApprovalRequired => authorityApprovalRequired;
        public bool MayBeDisputed => mayBeDisputed;
        public bool SecretAllowed => secretAllowed;
        public string HistorySignificancePolicyId => historySignificancePolicyId ?? string.Empty;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string definitionId,
            string name,
            CareerTransitionCategory transitionCategory,
            IEnumerable<CareerEpisodeState> sourceStates = null,
            IEnumerable<CareerEpisodeState> destinationStates = null,
            IEnumerable<CareerTransitionSourceRecordType> requiredSources = null,
            IEnumerable<string> professions = null,
            IEnumerable<string> ranks = null,
            IEnumerable<string> credentials = null,
            bool isVoluntary = true,
            bool requiresAuthority = false,
            bool disputed = false,
            bool allowSecret = false,
            string historyPolicy = "",
            string policyId = "",
            int definitionVersion = 1)
        {
            id = definitionId ?? string.Empty;
            displayName = name ?? string.Empty;
            category = transitionCategory;
            allowedSourceStates = (sourceStates ?? Array.Empty<CareerEpisodeState>()).Distinct().OrderBy(value => value).ToArray();
            allowedDestinationStates = (destinationStates ?? Array.Empty<CareerEpisodeState>()).Distinct().OrderBy(value => value).ToArray();
            requiredSourceRecordTypes = (requiredSources ?? Array.Empty<CareerTransitionSourceRecordType>()).Distinct().OrderBy(value => value).ToArray();
            requiredProfessionIds = Clean(professions).ToArray();
            requiredRankDefinitionIds = Clean(ranks).ToArray();
            requiredCredentialDefinitionIds = Clean(credentials).ToArray();
            voluntary = isVoluntary;
            authorityApprovalRequired = requiresAuthority;
            mayBeDisputed = disputed;
            secretAllowed = allowSecret;
            historySignificancePolicyId = historyPolicy ?? string.Empty;
            accessPolicyId = policyId ?? string.Empty;
            version = Math.Max(1, definitionVersion);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("career.transition.", StringComparison.Ordinal))
            {
                report.AddError($"{nameof(CareerTransitionDefinition)} '{DisplayName}' must use the 'career.transition.' namespace.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"{nameof(CareerTransitionDefinition)} '{Id}' must declare a display name.");
            }

            foreach (string professionId in RequiredProfessionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(professionId, out IGameDefinition definition) || definition is not ProfessionDefinition)
                {
                    report.AddError($"Career Transition '{DisplayName}' references missing Profession '{professionId}'.");
                }
            }

            foreach (string rankId in RequiredRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(rankId, out IGameDefinition definition) || definition is not ProfessionalRankDefinition)
                {
                    report.AddError($"Career Transition '{DisplayName}' references missing Rank '{rankId}'.");
                }
            }

            foreach (string credentialId in RequiredCredentialDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(credentialId, out IGameDefinition definition) || definition is not CredentialDefinition)
                {
                    report.AddError($"Career Transition '{DisplayName}' references missing Credential '{credentialId}'.");
                }
            }

            if (Version <= 0)
            {
                report.AddError($"Career Transition '{DisplayName}' must have a positive version.");
            }
        }

        private static IReadOnlyList<string> Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
