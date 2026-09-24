using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Capabilities;

namespace UnityIsekaiGame.ActorLifecycle
{
    [CreateAssetMenu(fileName = "DefeatPolicyDefinition", menuName = "Unity Isekai Game/Actors/Defeat Policy")]
    public sealed class DefeatPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private DefeatPolicyOutcome zeroHealthOutcome = DefeatPolicyOutcome.BecomeUnconscious;
        [SerializeField] private bool allowUnconsciousness = true;
        [SerializeField] private bool allowDeath = true;
        [SerializeField] private bool allowRecovery = true;
        [SerializeField] private bool allowRevival = true;
        [SerializeField, Min(0f)] private float recoveryMinimumHealth = 1f;
        [SerializeField, Min(0f)] private float revivalMinimumHealth = 1f;
        [SerializeField, Min(0f), Tooltip("Real-world seconds after death before revival is allowed. The deadline continues while the game is closed.")]
        private float revivalWaitRealSeconds = 3600f;
        [SerializeField] private RequirementSetDefinition recoveryRequirements;
        [SerializeField] private RequirementSetDefinition revivalRequirements;
        [SerializeField] private RequirementSetDefinition deathRequirements;
        [SerializeField] private CapabilityDefinition canBecomeUnconsciousCapability;
        [SerializeField] private CapabilityDefinition canDieCapability;
        [SerializeField] private CapabilityDefinition canRecoverCapability;
        [SerializeField] private CapabilityDefinition canBeRevivedCapability;
        [SerializeField] private CapabilityDefinition deathImmunityCapability;
        [SerializeField] private bool alphaEnabled = true;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public DefeatPolicyOutcome ZeroHealthOutcome => zeroHealthOutcome;
        public bool AllowUnconsciousness => allowUnconsciousness;
        public bool AllowDeath => allowDeath;
        public bool AllowRecovery => allowRecovery;
        public bool AllowRevival => allowRevival;
        public float RecoveryMinimumHealth => Mathf.Max(0f, recoveryMinimumHealth);
        public float RevivalMinimumHealth => Mathf.Max(0f, revivalMinimumHealth);
        public double RevivalWaitRealSeconds => Math.Max(0d, revivalWaitRealSeconds);
        public RequirementSetDefinition RecoveryRequirements => recoveryRequirements;
        public RequirementSetDefinition RevivalRequirements => revivalRequirements;
        public RequirementSetDefinition DeathRequirements => deathRequirements;
        public string CanBecomeUnconsciousCapabilityId => canBecomeUnconsciousCapability == null ? ActorLifecycleCapabilityIds.CanBecomeUnconscious : canBecomeUnconsciousCapability.Id;
        public string CanDieCapabilityId => canDieCapability == null ? ActorLifecycleCapabilityIds.CanDie : canDieCapability.Id;
        public string CanRecoverCapabilityId => canRecoverCapability == null ? ActorLifecycleCapabilityIds.CanRecover : canRecoverCapability.Id;
        public string CanBeRevivedCapabilityId => canBeRevivedCapability == null ? ActorLifecycleCapabilityIds.CanBeRevived : canBeRevivedCapability.Id;
        public string DeathImmunityCapabilityId => deathImmunityCapability == null ? ActorLifecycleCapabilityIds.DeathImmunity : deathImmunityCapability.Id;
        public bool AlphaEnabled => alphaEnabled;

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            recoveryMinimumHealth = Mathf.Max(0f, recoveryMinimumHealth);
            revivalMinimumHealth = Mathf.Max(0f, revivalMinimumHealth);
            revivalWaitRealSeconds = float.IsNaN(revivalWaitRealSeconds) || float.IsInfinity(revivalWaitRealSeconds)
                ? 0f
                : Mathf.Max(0f, revivalWaitRealSeconds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"DefeatPolicy '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("defeat-policy.", StringComparison.Ordinal))
            {
                report.AddWarning($"DefeatPolicy '{Id}' should use the 'defeat-policy.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(DefeatPolicyOutcome), zeroHealthOutcome))
            {
                report.AddError($"DefeatPolicy '{DisplayName}' has an invalid zero-health outcome.");
            }

            if (zeroHealthOutcome == DefeatPolicyOutcome.BecomeUnconscious && !allowUnconsciousness)
            {
                report.AddError($"DefeatPolicy '{DisplayName}' cannot become unconscious while unconsciousness is disallowed.");
            }

            if (zeroHealthOutcome == DefeatPolicyOutcome.DieImmediately && !allowDeath)
            {
                report.AddError($"DefeatPolicy '{DisplayName}' cannot die immediately while death is disallowed.");
            }

            if (allowRecovery && recoveryMinimumHealth <= 0f)
            {
                report.AddWarning($"DefeatPolicy '{DisplayName}' allows recovery but restores no Health by default.");
            }

            if (allowRevival && revivalMinimumHealth <= 0f)
            {
                report.AddWarning($"DefeatPolicy '{DisplayName}' allows revival but restores no Health by default.");
            }

            if (float.IsNaN(revivalWaitRealSeconds) || float.IsInfinity(revivalWaitRealSeconds) || revivalWaitRealSeconds < 0f)
            {
                report.AddError($"DefeatPolicy '{DisplayName}' has an invalid real-world revival wait.");
            }

            ValidateCapabilityReference(canBecomeUnconsciousCapability, "become unconscious", definitionsById, report);
            ValidateCapabilityReference(canDieCapability, "die", definitionsById, report);
            ValidateCapabilityReference(canRecoverCapability, "recover", definitionsById, report);
            ValidateCapabilityReference(canBeRevivedCapability, "be revived", definitionsById, report);
            ValidateCapabilityReference(deathImmunityCapability, "death immunity", definitionsById, report);
            ValidateRequirementReference(recoveryRequirements, "recovery", definitionsById, report);
            ValidateRequirementReference(revivalRequirements, "revival", definitionsById, report);
            ValidateRequirementReference(deathRequirements, "death", definitionsById, report);
        }

        private static void ValidateCapabilityReference(CapabilityDefinition capability, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (capability == null)
            {
                report.AddError($"DefeatPolicy capability for {label} is missing.");
                return;
            }
            if (capability.ValueType != CapabilityValueType.Boolean)
            {
                report.AddError($"DefeatPolicy capability '{capability.Id}' for {label} must be Boolean.");
            }
            if (definitionsById == null || !definitionsById.TryGetValue(capability.Id, out IGameDefinition found) || !ReferenceEquals(found, capability))
            {
                report.AddError($"DefeatPolicy capability '{capability.Id}' for {label} is not registered in the catalog.");
            }
        }

        private void ValidateRequirementReference(RequirementSetDefinition requirement, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (requirement == null)
            {
                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(requirement.Id, out IGameDefinition found) || !ReferenceEquals(found, requirement))
            {
                report.AddError($"DefeatPolicy '{DisplayName}' {label} requirement '{requirement.Id}' is not registered in the catalog.");
            }
        }
    }
}
