using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Execution
{
    public readonly struct CombatExecutionBeginRequest
    {
        public CombatExecutionBeginRequest(string transactionId, CombatExecutionDefinition definition, GameObject actorObject, string actorId = "", float now = 0f, bool authorityValidated = false, object payload = null)
        {
            TransactionId = transactionId ?? string.Empty;
            Definition = definition;
            ActorObject = actorObject;
            ActorId = actorId ?? string.Empty;
            Now = now;
            AuthorityValidated = authorityValidated;
            Payload = payload;
        }

        public string TransactionId { get; }
        public CombatExecutionDefinition Definition { get; }
        public GameObject ActorObject { get; }
        public string ActorId { get; }
        public float Now { get; }
        public bool AuthorityValidated { get; }
        public object Payload { get; }
    }

    public readonly struct CombatExecutionCommitRequest
    {
        public CombatExecutionCommitRequest(string transactionId, string executionInstanceId, GameObject actorObject, string actorId = "", float now = 0f, bool authorityValidated = false, object payload = null)
        {
            TransactionId = transactionId ?? string.Empty;
            ExecutionInstanceId = executionInstanceId ?? string.Empty;
            ActorObject = actorObject;
            ActorId = actorId ?? string.Empty;
            Now = now;
            AuthorityValidated = authorityValidated;
            Payload = payload;
        }

        public string TransactionId { get; }
        public string ExecutionInstanceId { get; }
        public GameObject ActorObject { get; }
        public string ActorId { get; }
        public float Now { get; }
        public bool AuthorityValidated { get; }
        public object Payload { get; }
    }

    public readonly struct CombatExecutionCancelRequest
    {
        public CombatExecutionCancelRequest(string transactionId, string executionInstanceId, GameObject actorObject, string actorId = "", CombatExecutionCancellationReason reason = CombatExecutionCancellationReason.PlayerOrAIRequest, float now = 0f)
        {
            TransactionId = transactionId ?? string.Empty;
            ExecutionInstanceId = executionInstanceId ?? string.Empty;
            ActorObject = actorObject;
            ActorId = actorId ?? string.Empty;
            Reason = reason;
            Now = now;
        }

        public string TransactionId { get; }
        public string ExecutionInstanceId { get; }
        public GameObject ActorObject { get; }
        public string ActorId { get; }
        public CombatExecutionCancellationReason Reason { get; }
        public float Now { get; }
    }

    public sealed class CombatExecutionStateSnapshot
    {
        public CombatExecutionStateSnapshot(
            string executionInstanceId,
            string beginTransactionId,
            string actorId,
            string actorBodyId,
            CombatExecutionDefinition definition,
            CombatExecutionPhase phase,
            float begunAt,
            float readyAt,
            float committedAt,
            float recoveryEndsAt,
            bool committed,
            bool completed,
            bool cancelled,
            bool interrupted,
            IReadOnlyList<CombatExecutionCostPreview> committedCosts)
        {
            ExecutionInstanceId = executionInstanceId ?? string.Empty;
            BeginTransactionId = beginTransactionId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            Definition = definition;
            Phase = phase;
            BegunAt = begunAt;
            ReadyAt = readyAt;
            CommittedAt = committedAt;
            RecoveryEndsAt = recoveryEndsAt;
            Committed = committed;
            Completed = completed;
            Cancelled = cancelled;
            Interrupted = interrupted;
            CommittedCosts = committedCosts == null ? Array.Empty<CombatExecutionCostPreview>() : new List<CombatExecutionCostPreview>(committedCosts);
        }

        public string ExecutionInstanceId { get; }
        public string BeginTransactionId { get; }
        public string ActorId { get; }
        public string ActorBodyId { get; }
        public CombatExecutionDefinition Definition { get; }
        public string DefinitionId => Definition == null ? string.Empty : Definition.Id;
        public CombatExecutionPhase Phase { get; }
        public float BegunAt { get; }
        public float ReadyAt { get; }
        public float CommittedAt { get; }
        public float RecoveryEndsAt { get; }
        public bool Committed { get; }
        public bool Completed { get; }
        public bool Cancelled { get; }
        public bool Interrupted { get; }
        public IReadOnlyList<CombatExecutionCostPreview> CommittedCosts { get; }
        public bool ReadyToCommit(float now) => now >= ReadyAt;
        public bool RecoveryComplete(float now) => Committed && now >= RecoveryEndsAt;
    }

    public sealed class CombatExecutionCostPreview
    {
        public CombatExecutionCostPreview(int index, CombatExecutionCostType costType, CombatExecutionCostCommitPoint commitPoint, string definitionId, float amount, bool required, bool consumed, bool supported, bool affordable, string code, string message, ResourceSnapshot resourceBefore = default, ResourceChangeResult resourceResult = null)
        {
            Index = index;
            CostType = costType;
            CommitPoint = commitPoint;
            DefinitionId = definitionId ?? string.Empty;
            Amount = amount;
            Required = required;
            Consumed = consumed;
            Supported = supported;
            Affordable = affordable;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ResourceBefore = resourceBefore;
            ResourceResult = resourceResult;
        }

        public int Index { get; }
        public CombatExecutionCostType CostType { get; }
        public CombatExecutionCostCommitPoint CommitPoint { get; }
        public string DefinitionId { get; }
        public float Amount { get; }
        public bool Required { get; }
        public bool Consumed { get; }
        public bool Supported { get; }
        public bool Affordable { get; }
        public string Code { get; }
        public string Message { get; }
        public ResourceSnapshot ResourceBefore { get; }
        public ResourceChangeResult ResourceResult { get; }
        public bool Succeeded => Supported && Affordable && (ResourceResult == null || ResourceResult.Succeeded);
    }

    public sealed class CombatExecutionCooldownSnapshot
    {
        public CombatExecutionCooldownSnapshot(string actorId, string cooldownKey, string definitionId, int currentCharges, int maximumCharges, float nextChargeReadyAt, float cooldownReadyAt)
        {
            ActorId = actorId ?? string.Empty;
            CooldownKey = cooldownKey ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            CurrentCharges = Mathf.Max(0, currentCharges);
            MaximumCharges = Mathf.Max(1, maximumCharges);
            NextChargeReadyAt = nextChargeReadyAt;
            CooldownReadyAt = cooldownReadyAt;
        }

        public string ActorId { get; }
        public string CooldownKey { get; }
        public string DefinitionId { get; }
        public int CurrentCharges { get; }
        public int MaximumCharges { get; }
        public float NextChargeReadyAt { get; }
        public float CooldownReadyAt { get; }
        public bool HasCharge => CurrentCharges > 0;
        public bool IsReady(float now) => CurrentCharges > 0 || now >= CooldownReadyAt;
    }

    public sealed class CombatExecutionResult
    {
        private CombatExecutionResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            CombatExecutionDefinition definition,
            string actorId,
            string actorBodyId,
            CombatExecutionStateSnapshot state,
            IReadOnlyList<CombatExecutionCostPreview> costs,
            CombatExecutionCooldownSnapshot cooldown,
            object underlyingResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatExecutionResultCode.Success : CombatExecutionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Definition = definition;
            ActorId = actorId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            State = state;
            Costs = costs == null ? Array.Empty<CombatExecutionCostPreview>() : new List<CombatExecutionCostPreview>(costs);
            Cooldown = cooldown;
            UnderlyingResult = underlyingResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public CombatExecutionDefinition Definition { get; }
        public string DefinitionId => Definition == null ? string.Empty : Definition.Id;
        public string ActorId { get; }
        public string ActorBodyId { get; }
        public CombatExecutionStateSnapshot State { get; }
        public IReadOnlyList<CombatExecutionCostPreview> Costs { get; }
        public CombatExecutionCooldownSnapshot Cooldown { get; }
        public object UnderlyingResult { get; }
        public AttackResolutionResult AttackResult => UnderlyingResult as AttackResolutionResult;
        public AbilityExecutionResult? AbilityResult => UnderlyingResult is AbilityExecutionResult result ? result : null;
        public DefenseActivationResult DefenseActivationResult => UnderlyingResult as DefenseActivationResult;

        public static CombatExecutionResult Success(bool preview, string code, string message, string transactionId, CombatExecutionDefinition definition, string actorId, string actorBodyId, CombatExecutionStateSnapshot state = null, IReadOnlyList<CombatExecutionCostPreview> costs = null, CombatExecutionCooldownSnapshot cooldown = null, object underlyingResult = null)
        {
            return new CombatExecutionResult(true, preview, false, preview ? CombatExecutionResultCode.Preview : code, message, transactionId, definition, actorId, actorBodyId, state, costs, cooldown, underlyingResult);
        }

        public static CombatExecutionResult Failure(bool preview, string code, string message, string transactionId, CombatExecutionDefinition definition = null, string actorId = "", string actorBodyId = "", CombatExecutionStateSnapshot state = null, IReadOnlyList<CombatExecutionCostPreview> costs = null, CombatExecutionCooldownSnapshot cooldown = null, object underlyingResult = null)
        {
            return new CombatExecutionResult(false, preview, false, code, message, transactionId, definition, actorId, actorBodyId, state, costs, cooldown, underlyingResult);
        }

        public CombatExecutionResult AsDuplicate()
        {
            return new CombatExecutionResult(Succeeded, Preview, true, CombatExecutionResultCode.Duplicate, "Duplicate combat execution transaction ignored.", TransactionId, Definition, ActorId, ActorBodyId, State, Costs, Cooldown, UnderlyingResult);
        }
    }

    public sealed class CombatExecutionCommitted
    {
        public CombatExecutionCommitted(
            string transactionId,
            string beginTransactionId,
            string executionInstanceId,
            string actorId,
            string actorBodyId,
            string targetActorId,
            CombatExecutionDefinition definition,
            CombatExecutionStateSnapshot state,
            IReadOnlyList<CombatExecutionCostPreview> costs,
            CombatExecutionCooldownSnapshot cooldown,
            object underlyingResult,
            IReadOnlyDictionary<string, string> context)
        {
            TransactionId = transactionId ?? string.Empty;
            BeginTransactionId = beginTransactionId ?? string.Empty;
            ExecutionInstanceId = executionInstanceId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            Definition = definition;
            State = state;
            Costs = costs == null ? Array.Empty<CombatExecutionCostPreview>() : new List<CombatExecutionCostPreview>(costs);
            Cooldown = cooldown;
            UnderlyingResult = underlyingResult;
            Context = context == null
                ? Array.Empty<KeyValuePair<string, string>>()
                : new List<KeyValuePair<string, string>>(context);
        }

        public string TransactionId { get; }
        public string BeginTransactionId { get; }
        public string ExecutionInstanceId { get; }
        public string ActorId { get; }
        public string ActorBodyId { get; }
        public string TargetActorId { get; }
        public CombatExecutionDefinition Definition { get; }
        public string DefinitionId => Definition == null ? string.Empty : Definition.Id;
        public CombatExecutionActionType ActionType => Definition == null ? CombatExecutionActionType.None : Definition.ActionType;
        public CombatExecutionStateSnapshot State { get; }
        public IReadOnlyList<CombatExecutionCostPreview> Costs { get; }
        public CombatExecutionCooldownSnapshot Cooldown { get; }
        public int CurrentCharges => Cooldown == null ? 0 : Cooldown.CurrentCharges;
        public int MaximumCharges => Cooldown == null ? 0 : Cooldown.MaximumCharges;
        public object UnderlyingResult { get; }
        public AttackResolutionResult AttackResult => UnderlyingResult as AttackResolutionResult;
        public AbilityExecutionResult? AbilityResult => UnderlyingResult is AbilityExecutionResult result ? result : null;
        public DefenseActivationResult DefenseActivationResult => UnderlyingResult as DefenseActivationResult;
        public IReadOnlyList<KeyValuePair<string, string>> Context { get; }
    }

    [Serializable]
    public sealed class CombatExecutionSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string personId;
        public List<CombatExecutionCooldownSaveData> cooldowns = new List<CombatExecutionCooldownSaveData>();
    }

    [Serializable]
    public sealed class CombatExecutionCooldownSaveData
    {
        public string actorId;
        public string cooldownKey;
        public string definitionId;
        public int currentCharges;
        public int maximumCharges;
        public float nextChargeReadyAt;
        public float cooldownReadyAt;
    }
}
