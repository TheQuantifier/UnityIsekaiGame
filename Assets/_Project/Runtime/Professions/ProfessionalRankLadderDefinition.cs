using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Professional Rank Ladder Definition")]
    public sealed class ProfessionalRankLadderDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string professionId;
        [SerializeField] private string specializationId;
        [SerializeField] private string[] orderedRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] terminalRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] lateralRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] demotionPathRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private bool allowMultipleRoots;
        [SerializeField] private bool allowRankSkipping;
        [SerializeField] private bool formalTrack = true;
        [SerializeField] private bool informalTrack = true;
        [SerializeField] private bool restricted;
        [SerializeField] private bool secret;
        [SerializeField] private InformationVisibilityClassification visibility = InformationVisibilityClassification.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string ProfessionId => professionId ?? string.Empty;
        public string SpecializationId => specializationId ?? string.Empty;
        public IReadOnlyList<string> OrderedRankDefinitionIds => CredentialDefinition.Clean(orderedRankDefinitionIds);
        public IReadOnlyList<string> TerminalRankDefinitionIds => CredentialDefinition.Clean(terminalRankDefinitionIds);
        public IReadOnlyList<string> LateralRankDefinitionIds => CredentialDefinition.Clean(lateralRankDefinitionIds);
        public IReadOnlyList<string> DemotionPathRankDefinitionIds => CredentialDefinition.Clean(demotionPathRankDefinitionIds);
        public bool AllowMultipleRoots => allowMultipleRoots;
        public bool AllowRankSkipping => allowRankSkipping;
        public bool FormalTrack => formalTrack;
        public bool InformalTrack => informalTrack;
        public bool Restricted => restricted;
        public bool Secret => secret;
        public InformationVisibilityClassification Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            string profession,
            IEnumerable<string> orderedRanks,
            string specialization = "",
            IEnumerable<string> terminalRanks = null,
            IEnumerable<string> lateralRanks = null,
            IEnumerable<string> demotionPathRanks = null,
            bool multipleRoots = false,
            bool rankSkipping = false,
            bool supportsFormalTrack = true,
            bool supportsInformalTrack = true,
            bool isRestricted = false,
            bool isSecret = false,
            InformationVisibilityClassification classification = InformationVisibilityClassification.Public,
            string policyId = "",
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            professionId = profession ?? string.Empty;
            specializationId = specialization ?? string.Empty;
            orderedRankDefinitionIds = CredentialDefinition.Clean(orderedRanks);
            terminalRankDefinitionIds = CredentialDefinition.Clean(terminalRanks);
            lateralRankDefinitionIds = CredentialDefinition.Clean(lateralRanks);
            demotionPathRankDefinitionIds = CredentialDefinition.Clean(demotionPathRanks);
            allowMultipleRoots = multipleRoots;
            allowRankSkipping = rankSkipping;
            formalTrack = supportsFormalTrack;
            informalTrack = supportsInformalTrack;
            restricted = isRestricted;
            secret = isSecret;
            visibility = classification;
            accessPolicyId = policyId ?? string.Empty;
            version = Math.Max(1, definitionVersion);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Professional Rank Ladder definition has no stable ID.");
            }
            else if (!Id.StartsWith("profession.rank-ladder.", StringComparison.Ordinal))
            {
                report.AddWarning($"Professional Rank Ladder definition '{DisplayName}' should use the 'profession.rank-ladder.' namespace prefix.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(ProfessionId, out IGameDefinition professionDefinition) || professionDefinition is not ProfessionDefinition)
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' references missing Profession '{ProfessionId}'.");
            }

            if (OrderedRankDefinitionIds.Count == 0)
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' has no ranks.");
            }

            Dictionary<string, ProfessionalRankDefinition> ranks = new Dictionary<string, ProfessionalRankDefinition>(StringComparer.Ordinal);
            foreach (string rankId in OrderedRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(rankId, out IGameDefinition definition) || definition is not ProfessionalRankDefinition rank)
                {
                    report.AddError($"Professional Rank Ladder definition '{DisplayName}' references missing Rank '{rankId}'.");
                    continue;
                }

                ranks[rankId] = rank;
                if (!string.Equals(rank.ProfessionId, ProfessionId, StringComparison.Ordinal) || !string.Equals(rank.SpecializationId, SpecializationId, StringComparison.Ordinal))
                {
                    report.AddError($"Professional Rank Ladder definition '{DisplayName}' contains cross-profession or cross-specialization Rank '{rankId}'.");
                }
            }

            var duplicateOrders = ranks.Values.GroupBy(rank => rank.RankOrder).Where(group => group.Count() > 1).ToArray();
            foreach (var group in duplicateOrders)
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' has duplicate rank order '{group.Key}'.");
            }

            int rootCount = ranks.Values.Count(rank => !rank.PriorRankDefinitionIds.Any(ranks.ContainsKey));
            if (!AllowMultipleRoots && rootCount > 1)
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' has multiple roots.");
            }

            foreach (ProfessionalRankDefinition rank in ranks.Values)
            {
                foreach (string priorId in rank.PriorRankDefinitionIds)
                {
                    if (ranks.ContainsKey(priorId))
                    {
                        continue;
                    }

                    if (definitionsById == null || !definitionsById.TryGetValue(priorId, out IGameDefinition definition) || definition is not ProfessionalRankDefinition externalPrior)
                    {
                        continue;
                    }

                    bool sameProfession = string.Equals(externalPrior.ProfessionId, ProfessionId, StringComparison.Ordinal);
                    bool compatibleSpecialization = string.IsNullOrWhiteSpace(externalPrior.SpecializationId)
                        || string.Equals(externalPrior.SpecializationId, SpecializationId, StringComparison.Ordinal);
                    if (!sameProfession || !compatibleSpecialization)
                    {
                        report.AddError($"Professional Rank Ladder definition '{DisplayName}' links Rank '{rank.Id}' to incompatible external prior Rank '{priorId}'.");
                    }
                }
            }

            if (HasCycle(ranks))
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' has a circular advancement path.");
            }

            HashSet<string> reachable = new HashSet<string>(ranks.Values.Where(rank => !rank.PriorRankDefinitionIds.Any(ranks.ContainsKey)).Select(rank => rank.Id), StringComparer.Ordinal);
            bool changed;
            do
            {
                changed = false;
                foreach (ProfessionalRankDefinition rank in ranks.Values.OrderBy(rank => rank.RankOrder).ThenBy(rank => rank.Id, StringComparer.Ordinal))
                {
                    if (!reachable.Contains(rank.Id) && rank.PriorRankDefinitionIds.Where(ranks.ContainsKey).Any(prior => reachable.Contains(prior)))
                    {
                        reachable.Add(rank.Id);
                        changed = true;
                    }
                }
            }
            while (changed);

            foreach (string rankId in ranks.Keys.Where(rankId => !reachable.Contains(rankId)).OrderBy(rankId => rankId, StringComparer.Ordinal))
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' has unreachable Rank '{rankId}'.");
            }

            if (Version <= 0)
            {
                report.AddError($"Professional Rank Ladder definition '{DisplayName}' has invalid version.");
            }
        }

        private static bool HasCycle(Dictionary<string, ProfessionalRankDefinition> ranks)
        {
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

            bool Visit(string rankId)
            {
                if (visited.Contains(rankId))
                {
                    return false;
                }

                if (!visiting.Add(rankId))
                {
                    return true;
                }

                if (ranks.TryGetValue(rankId, out ProfessionalRankDefinition rank))
                {
                    foreach (string priorId in rank.PriorRankDefinitionIds.Where(ranks.ContainsKey))
                    {
                        if (Visit(priorId))
                        {
                            return true;
                        }
                    }
                }

                visiting.Remove(rankId);
                visited.Add(rankId);
                return false;
            }

            return ranks.Keys.OrderBy(rankId => rankId, StringComparer.Ordinal).Any(Visit);
        }
    }
}
