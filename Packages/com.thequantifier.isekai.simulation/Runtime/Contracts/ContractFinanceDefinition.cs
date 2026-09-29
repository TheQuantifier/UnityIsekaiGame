using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Contracts
{
    [CreateAssetMenu(fileName = "Contract Finance Definition", menuName = "Unity Isekai Game/Contracts/Economic Contract Definition")]
    public sealed class ContractFinanceDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField] private EconomicContractCategory category = EconomicContractCategory.General;
        [SerializeField] private ContractTermData[] requiredTerms;
        [SerializeField] private ContractPartyRole[] requiredPartyRoles;

        public string Id => definitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? definitionId : displayName;
        public EconomicContractCategory Category => category;
        public IReadOnlyList<ContractTermData> RequiredTerms => requiredTerms ?? System.Array.Empty<ContractTermData>();
        public IReadOnlyList<ContractPartyRole> RequiredPartyRoles => requiredPartyRoles ?? System.Array.Empty<ContractPartyRole>();

        public void Initialize(
            string id,
            string name,
            EconomicContractCategory contractCategory,
            IEnumerable<ContractPartyRole> roles = null,
            IEnumerable<ContractTermData> terms = null)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = contractCategory;
            requiredPartyRoles = (roles ?? System.Array.Empty<ContractPartyRole>()).Distinct().OrderBy(role => role).ToArray();
            requiredTerms = ContractFinanceModelHelpers.CloneArray(terms, term => term.Clone());
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (RequiredPartyRoles.Count == 0)
            {
                report.AddWarning($"Economic Contract Definition '{DisplayName}' has no required party roles.");
            }

            HashSet<string> termIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (ContractTermData term in RequiredTerms)
            {
                if (term == null || string.IsNullOrWhiteSpace(term.termId))
                {
                    report.AddError($"Economic Contract Definition '{DisplayName}' has a required term without a stable term ID.");
                    continue;
                }

                if (!termIds.Add(term.termId))
                {
                    report.AddError($"Economic Contract Definition '{DisplayName}' has duplicate required term '{term.termId}'.");
                }

                if ((term.category == ContractTermCategory.Payment || term.category == ContractTermCategory.Repayment)
                    && (string.IsNullOrWhiteSpace(term.currencyId) || term.amountUnits <= 0L))
                {
                    report.AddError($"Economic Contract Definition '{DisplayName}' payment term '{term.termId}' must declare currency and a positive amount.");
                }
            }
        }
    }
}
