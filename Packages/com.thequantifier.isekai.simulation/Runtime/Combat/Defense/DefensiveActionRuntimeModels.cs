using UnityEngine;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Defense
{
    public readonly struct DefenseActivationRequest
    {
        public DefenseActivationRequest(
            string transactionId,
            string defenderActorId,
            GameObject defenderObject,
            DefensiveActionDefinition definition,
            string sourceEquipmentId = "",
            string sourceActionId = "",
            float now = 0f,
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            DefenderActorId = defenderActorId ?? string.Empty;
            DefenderObject = defenderObject;
            Definition = definition;
            SourceEquipmentId = sourceEquipmentId ?? string.Empty;
            SourceActionId = sourceActionId ?? string.Empty;
            Now = now;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string DefenderActorId { get; }
        public GameObject DefenderObject { get; }
        public DefensiveActionDefinition Definition { get; }
        public string SourceEquipmentId { get; }
        public string SourceActionId { get; }
        public float Now { get; }
        public bool AuthorityValidated { get; }
    }

    public readonly struct DefenseCancellationRequest
    {
        public DefenseCancellationRequest(
            string transactionId,
            string defenderActorId,
            GameObject defenderObject,
            DefenseCancellationReason reason = DefenseCancellationReason.Explicit,
            string expectedStateId = "",
            string expectedDefinitionId = "",
            float now = 0f)
        {
            TransactionId = transactionId ?? string.Empty;
            DefenderActorId = defenderActorId ?? string.Empty;
            DefenderObject = defenderObject;
            Reason = reason;
            ExpectedStateId = expectedStateId ?? string.Empty;
            ExpectedDefinitionId = expectedDefinitionId ?? string.Empty;
            Now = now;
        }

        public string TransactionId { get; }
        public string DefenderActorId { get; }
        public GameObject DefenderObject { get; }
        public DefenseCancellationReason Reason { get; }
        public string ExpectedStateId { get; }
        public string ExpectedDefinitionId { get; }
        public float Now { get; }
    }

    public readonly struct DefenseResolutionRequest
    {
        public DefenseResolutionRequest(
            string transactionId,
            string parentAttackTransactionId,
            string attackerActorId,
            GameObject attackerObject,
            string defenderActorId,
            GameObject defenderObject,
            DamageTypeDefinition damageType,
            AttackSourceType sourceType,
            float incomingDamage,
            float roll,
            bool critical = false,
            bool blockable = true,
            bool parryable = true,
            bool dodgeable = true,
            bool trueDamage = false,
            bool allowTrueDamageActiveDefense = true,
            string expectedStateId = "",
            float now = 0f,
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            ParentAttackTransactionId = parentAttackTransactionId ?? string.Empty;
            AttackerActorId = attackerActorId ?? string.Empty;
            AttackerObject = attackerObject;
            DefenderActorId = defenderActorId ?? string.Empty;
            DefenderObject = defenderObject;
            DamageType = damageType;
            SourceType = sourceType;
            IncomingDamage = incomingDamage;
            Roll = roll;
            Critical = critical;
            Blockable = blockable;
            Parryable = parryable;
            Dodgeable = dodgeable;
            TrueDamage = trueDamage;
            AllowTrueDamageActiveDefense = allowTrueDamageActiveDefense;
            ExpectedStateId = expectedStateId ?? string.Empty;
            Now = now;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string ParentAttackTransactionId { get; }
        public string AttackerActorId { get; }
        public GameObject AttackerObject { get; }
        public string DefenderActorId { get; }
        public GameObject DefenderObject { get; }
        public DamageTypeDefinition DamageType { get; }
        public AttackSourceType SourceType { get; }
        public float IncomingDamage { get; }
        public float Roll { get; }
        public bool Critical { get; }
        public bool Blockable { get; }
        public bool Parryable { get; }
        public bool Dodgeable { get; }
        public bool TrueDamage { get; }
        public bool AllowTrueDamageActiveDefense { get; }
        public string ExpectedStateId { get; }
        public float Now { get; }
        public bool AuthorityValidated { get; }
    }

    public sealed class DefensiveActionStateSnapshot
    {
        public DefensiveActionStateSnapshot(
            string stateId,
            string defenderActorId,
            DefensiveActionDefinition definition,
            DefensiveActionState state,
            float activatedAt,
            float expiresAt,
            string sourceEquipmentId,
            string sourceEquipmentInstanceId,
            string defenderBodyId,
            string sourceActionId)
        {
            StateId = stateId ?? string.Empty;
            DefenderActorId = defenderActorId ?? string.Empty;
            Definition = definition;
            State = state;
            ActivatedAt = activatedAt;
            ExpiresAt = expiresAt;
            SourceEquipmentId = sourceEquipmentId ?? string.Empty;
            SourceEquipmentInstanceId = sourceEquipmentInstanceId ?? string.Empty;
            DefenderBodyId = defenderBodyId ?? string.Empty;
            SourceActionId = sourceActionId ?? string.Empty;
        }

        public string StateId { get; }
        public string DefenderActorId { get; }
        public DefensiveActionDefinition Definition { get; }
        public string DefinitionId => Definition == null ? string.Empty : Definition.Id;
        public DefensiveActionType ActionType => Definition == null ? DefensiveActionType.None : Definition.ActionType;
        public DefensiveActionState State { get; }
        public float ActivatedAt { get; }
        public float ExpiresAt { get; }
        public string SourceEquipmentId { get; }
        public string SourceEquipmentInstanceId { get; }
        public string DefenderBodyId { get; }
        public string SourceActionId { get; }
        public bool HasExpiration => ExpiresAt > ActivatedAt;

        public bool IsExpired(float now)
        {
            return HasExpiration && now >= ExpiresAt;
        }
    }

    public sealed class DefenseActivationResult
    {
        private DefenseActivationResult(bool succeeded, bool preview, bool duplicate, string code, string message, DefenseActivationRequest request, DefensiveActionStateSnapshot state, ResourceChangeResult staminaResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? DefensiveActionResultCode.Success : DefensiveActionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Request = request;
            State = state;
            StaminaResult = staminaResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public DefenseActivationRequest Request { get; }
        public DefensiveActionStateSnapshot State { get; }
        public ResourceChangeResult StaminaResult { get; }
        public bool StaminaSpent => StaminaResult != null && StaminaResult.Succeeded && StaminaResult.AppliedAmount > CharacterResourceCollection.Epsilon;

        public static DefenseActivationResult Success(DefenseActivationRequest request, bool preview, bool duplicate, string message, DefensiveActionStateSnapshot state, ResourceChangeResult staminaResult = null)
        {
            return new DefenseActivationResult(true, preview, duplicate, preview ? DefensiveActionResultCode.Preview : DefensiveActionResultCode.Success, message, request, state, staminaResult);
        }

        public static DefenseActivationResult Failure(DefenseActivationRequest request, bool preview, string code, string message, DefensiveActionStateSnapshot state = null, ResourceChangeResult staminaResult = null)
        {
            return new DefenseActivationResult(false, preview, false, code, message, request, state, staminaResult);
        }

        public DefenseActivationResult AsDuplicate()
        {
            return new DefenseActivationResult(Succeeded, Preview, true, DefensiveActionResultCode.DuplicateTransaction, "Duplicate defensive activation transaction ignored.", Request, State, StaminaResult);
        }
    }

    public sealed class DefenseCancellationResult
    {
        private DefenseCancellationResult(bool succeeded, bool preview, bool duplicate, string code, string message, DefenseCancellationRequest request, DefensiveActionStateSnapshot removedState)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? DefensiveActionResultCode.Success : DefensiveActionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Request = request;
            RemovedState = removedState;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public DefenseCancellationRequest Request { get; }
        public DefensiveActionStateSnapshot RemovedState { get; }

        public static DefenseCancellationResult Success(DefenseCancellationRequest request, bool preview, bool duplicate, string message, DefensiveActionStateSnapshot removedState)
        {
            return new DefenseCancellationResult(true, preview, duplicate, preview ? DefensiveActionResultCode.Preview : DefensiveActionResultCode.Success, message, request, removedState);
        }

        public static DefenseCancellationResult Failure(DefenseCancellationRequest request, bool preview, string code, string message, DefensiveActionStateSnapshot removedState = null)
        {
            return new DefenseCancellationResult(false, preview, false, code, message, request, removedState);
        }

        public DefenseCancellationResult AsDuplicate()
        {
            return new DefenseCancellationResult(Succeeded, Preview, true, DefensiveActionResultCode.DuplicateTransaction, "Duplicate defensive cancellation transaction ignored.", Request, RemovedState);
        }
    }

    public sealed class DefenseResolutionResult
    {
        private DefenseResolutionResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            string code,
            string message,
            DefenseResolutionOutcome outcome,
            DefenseResolutionRequest request,
            DefensiveActionStateSnapshot state,
            float finalDefenseChance,
            bool attempted,
            bool defenseSucceeded,
            float preventedDamage,
            float remainingDamage,
            bool consumed,
            ResourceChangeResult staminaResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? DefensiveActionResultCode.Success : DefensiveActionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Outcome = outcome;
            Request = request;
            State = state;
            FinalDefenseChance = finalDefenseChance;
            Attempted = attempted;
            DefenseSucceeded = defenseSucceeded;
            PreventedDamage = preventedDamage;
            RemainingDamage = Mathf.Max(0f, remainingDamage);
            Consumed = consumed;
            StaminaResult = staminaResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public DefenseResolutionOutcome Outcome { get; }
        public DefenseResolutionRequest Request { get; }
        public DefensiveActionStateSnapshot State { get; }
        public string DefensiveActionId => State == null ? string.Empty : State.DefinitionId;
        public DefensiveActionType DefensiveActionType => State == null ? DefensiveActionType.None : State.ActionType;
        public float FinalDefenseChance { get; }
        public bool Attempted { get; }
        public bool DefenseSucceeded { get; }
        public float PreventedDamage { get; }
        public float RemainingDamage { get; }
        public bool DamageFullyPrevented => RemainingDamage <= CharacterResourceCollection.Epsilon && PreventedDamage > CharacterResourceCollection.Epsilon;
        public bool Consumed { get; }
        public ResourceChangeResult StaminaResult { get; }
        public bool StaminaSpent => StaminaResult != null && StaminaResult.Succeeded && StaminaResult.AppliedAmount > CharacterResourceCollection.Epsilon;

        public static DefenseResolutionResult Create(bool preview, bool duplicate, DefenseResolutionOutcome outcome, string code, string message, DefenseResolutionRequest request, DefensiveActionStateSnapshot state, float finalChance, bool attempted, bool defenseSucceeded, float preventedDamage, float remainingDamage, bool consumed, ResourceChangeResult staminaResult = null)
        {
            return new DefenseResolutionResult(true, preview, duplicate, code, message, outcome, request, state, finalChance, attempted, defenseSucceeded, preventedDamage, remainingDamage, consumed, staminaResult);
        }

        public static DefenseResolutionResult Rejected(bool preview, DefenseResolutionOutcome outcome, string code, string message, DefenseResolutionRequest request, DefensiveActionStateSnapshot state, float finalChance = 0f, bool attempted = false, bool consumed = false, ResourceChangeResult staminaResult = null)
        {
            return new DefenseResolutionResult(false, preview, false, code, message, outcome, request, state, finalChance, attempted, false, 0f, request.IncomingDamage, consumed, staminaResult);
        }

        public static DefenseResolutionResult Failure(bool preview, string code, string message, DefenseResolutionRequest request, DefensiveActionStateSnapshot state = null)
        {
            return new DefenseResolutionResult(false, preview, false, code, message, DefenseResolutionOutcome.None, request, state, 0f, false, false, 0f, request.IncomingDamage, false, null);
        }

        public DefenseResolutionResult AsDuplicate()
        {
            return new DefenseResolutionResult(Succeeded, Preview, true, DefensiveActionResultCode.DuplicateTransaction, "Duplicate defense resolution transaction ignored.", Outcome, Request, State, FinalDefenseChance, Attempted, DefenseSucceeded, PreventedDamage, RemainingDamage, Consumed, StaminaResult);
        }
    }
}
