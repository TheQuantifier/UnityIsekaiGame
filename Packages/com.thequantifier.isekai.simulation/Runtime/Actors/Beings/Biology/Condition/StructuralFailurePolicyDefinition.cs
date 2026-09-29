using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Condition
{
    [CreateAssetMenu(fileName = "StructuralFailurePolicy", menuName = "Unity Isekai Game/Beings/Biology/Structural Failure Policy")]
    public sealed class StructuralFailurePolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private StructureDamageState failureState = StructureDamageState.Destroyed;
        [SerializeField] private StructureFunctionalState functionalState = StructureFunctionalState.Destroyed;
        [SerializeField] private RuntimeStructurePresenceState runtimePresence = RuntimeStructurePresenceState.Destroyed;
        [SerializeField] private bool lifecycleHook;

        public string Id => policyId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public StructureDamageState FailureState => failureState;
        public StructureFunctionalState FunctionalState => functionalState;
        public RuntimeStructurePresenceState RuntimePresence => runtimePresence;
        public bool LifecycleHook => lifecycleHook;

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            displayName = displayName?.Trim();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"StructuralFailurePolicyDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("structural-failure.", StringComparison.Ordinal))
            {
                report.AddWarning($"StructuralFailurePolicyDefinition '{Id}' should use the 'structural-failure.' namespace prefix.");
            }
        }
    }
}
