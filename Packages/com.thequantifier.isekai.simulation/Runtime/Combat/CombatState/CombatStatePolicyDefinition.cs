using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Combat.CombatState
{
    [CreateAssetMenu(fileName = "CombatStatePolicyDefinition", menuName = "Unity Isekai Game/Combat/Combat State Policy")]
    public sealed class CombatStatePolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId = "combat-state-policy.prototype-alpha";
        [SerializeField] private string displayName = "Prototype Combat State Policy";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField, Min(0.01f)] private float combatTimeoutSeconds = 10f;
        [SerializeField] private bool missesStartCombat = true;
        [SerializeField] private bool preventedDamageStartsCombat = true;
        [SerializeField] private bool hostileOngoingDamageRefreshesCombat = true;
        [SerializeField] private bool retainDefeatedParticipants = true;
        [SerializeField] private bool retainUnconsciousParticipants = true;
        [SerializeField] private bool removeDeadParticipants = true;
        [SerializeField, Min(1)] private int maximumParticipants = 64;
        [SerializeField, Min(1)] private int maximumTimeoutsProcessedPerUpdate = 128;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public float CombatTimeoutSeconds => Mathf.Max(0.01f, combatTimeoutSeconds);
        public bool MissesStartCombat => missesStartCombat;
        public bool PreventedDamageStartsCombat => preventedDamageStartsCombat;
        public bool HostileOngoingDamageRefreshesCombat => hostileOngoingDamageRefreshesCombat;
        public bool RetainDefeatedParticipants => retainDefeatedParticipants;
        public bool RetainUnconsciousParticipants => retainUnconsciousParticipants;
        public bool RemoveDeadParticipants => removeDeadParticipants;
        public int MaximumParticipants => Mathf.Max(1, maximumParticipants);
        public int MaximumTimeoutsProcessedPerUpdate => Mathf.Max(1, maximumTimeoutsProcessedPerUpdate);

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            combatTimeoutSeconds = Mathf.Max(0.01f, combatTimeoutSeconds);
            maximumParticipants = Mathf.Max(1, maximumParticipants);
            maximumTimeoutsProcessedPerUpdate = Mathf.Max(1, maximumTimeoutsProcessedPerUpdate);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"CombatStatePolicy '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("combat-state-policy.", StringComparison.Ordinal))
            {
                report.AddWarning($"CombatStatePolicy '{Id}' should use the 'combat-state-policy.' namespace prefix.");
            }

            if (!IsFinite(combatTimeoutSeconds) || combatTimeoutSeconds <= 0f)
            {
                report.AddError($"CombatStatePolicy '{DisplayName}' must have a positive finite combat timeout.");
            }

            if (maximumParticipants < 1)
            {
                report.AddError($"CombatStatePolicy '{DisplayName}' must allow at least one participant.");
            }

            if (maximumTimeoutsProcessedPerUpdate < 1)
            {
                report.AddError($"CombatStatePolicy '{DisplayName}' must process at least one timeout per update.");
            }

            if (!retainDefeatedParticipants && retainUnconsciousParticipants)
            {
                report.AddWarning($"CombatStatePolicy '{DisplayName}' retains unconscious participants while removing defeated participants; verify lifecycle ordering is intentional.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
