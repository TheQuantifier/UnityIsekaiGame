using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Reactions
{
    public sealed class CombatReactionSourceRegistration
    {
        public CombatReactionSourceRegistration(
            string registrationId,
            string ownerActorId,
            GameObject ownerObject,
            CombatReactionSourceKind sourceKind,
            string sourceStableId,
            string sourceInstanceId,
            int sourcePriority,
            CombatReactionDefinition definition,
            bool active = true)
        {
            RegistrationId = string.IsNullOrWhiteSpace(registrationId) ? Guid.NewGuid().ToString("N") : registrationId;
            OwnerActorId = ownerActorId ?? string.Empty;
            OwnerObject = ownerObject;
            SourceKind = sourceKind;
            SourceStableId = sourceStableId ?? string.Empty;
            SourceInstanceId = sourceInstanceId ?? string.Empty;
            SourcePriority = sourcePriority;
            Definition = definition;
            Active = active;
        }

        public string RegistrationId { get; }
        public string OwnerActorId { get; }
        public GameObject OwnerObject { get; }
        public CombatReactionSourceKind SourceKind { get; }
        public string SourceStableId { get; }
        public string SourceInstanceId { get; }
        public int SourcePriority { get; }
        public CombatReactionDefinition Definition { get; }
        public bool Active { get; }
    }

    public sealed class CombatReactionTriggerContext
    {
        private readonly IReadOnlyDictionary<string, string> metadata;
        private readonly IReadOnlyList<string> tags;

        public CombatReactionTriggerContext(
            CombatReactionTriggerType triggerType,
            string rootTransactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            float actualDamage = 0f,
            float actualHealing = 0f,
            float preventedDamage = 0f,
            bool critical = false,
            bool fullyPrevented = false,
            DamageTypeDefinition damageType = null,
            object sourceResult = null,
            IReadOnlyDictionary<string, string> metadata = null,
            IReadOnlyList<string> tags = null)
        {
            TriggerType = triggerType;
            RootTransactionId = rootTransactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            ActualDamage = Mathf.Max(0f, actualDamage);
            ActualHealing = Mathf.Max(0f, actualHealing);
            PreventedDamage = Mathf.Max(0f, preventedDamage);
            Critical = critical;
            FullyPrevented = fullyPrevented;
            DamageType = damageType;
            SourceResult = sourceResult;
            this.metadata = metadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
            this.tags = tags == null ? Array.Empty<string>() : new List<string>(tags);
        }

        public CombatReactionTriggerType TriggerType { get; }
        public string RootTransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public float ActualDamage { get; }
        public float ActualHealing { get; }
        public float PreventedDamage { get; }
        public bool Critical { get; }
        public bool FullyPrevented { get; }
        public DamageTypeDefinition DamageType { get; }
        public object SourceResult { get; }
        public IReadOnlyDictionary<string, string> Metadata => metadata;
        public IReadOnlyList<string> Tags => tags;

        public float Magnitude => ActualDamage > CharacterResourceCollection.Epsilon ? ActualDamage : ActualHealing;

        public static bool TryFromDamageResult(DamageApplicationResult result, CombatReactionTriggerType triggerType, out CombatReactionTriggerContext context)
        {
            context = null;
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded)
            {
                return false;
            }

            bool valid = triggerType == CombatReactionTriggerType.DamageApplied && result.HealthChanged
                || triggerType == CombatReactionTriggerType.DamageFullyPrevented && result.FinalDamageAmount <= CharacterResourceCollection.Epsilon
                || triggerType == CombatReactionTriggerType.HealthReachedZero && result.BecameZero;
            if (!valid)
            {
                return false;
            }

            context = new CombatReactionTriggerContext(
                triggerType,
                result.Request.TransactionId,
                result.Request.SourceActorId,
                result.Request.SourceObject,
                result.ResolvedTargetActorId,
                result.Request.TargetObject,
                result.FinalDamageAmount,
                preventedDamage: result.DefenseMitigatedAmount + result.ResistanceMitigatedAmount,
                fullyPrevented: result.FinalDamageAmount <= CharacterResourceCollection.Epsilon,
                damageType: result.Request.DamageType,
                sourceResult: result);
            return true;
        }

        public static bool TryFromHealingResult(HealingApplicationResult result, CombatReactionTriggerType triggerType, out CombatReactionTriggerContext context)
        {
            context = null;
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded)
            {
                return false;
            }

            bool valid = triggerType == CombatReactionTriggerType.HealingApplied && result.HealthChanged
                || triggerType == CombatReactionTriggerType.OverhealingOccurred && result.OverhealAmount > CharacterResourceCollection.Epsilon;
            if (!valid)
            {
                return false;
            }

            context = new CombatReactionTriggerContext(
                triggerType,
                result.Request.TransactionId,
                result.Request.SourceActorId,
                result.Request.SourceObject,
                result.ResolvedTargetActorId,
                result.Request.TargetObject,
                actualHealing: result.FinalHealingAmount,
                sourceResult: result);
            return true;
        }

        public static CombatReactionTriggerContext FromAttackResult(AttackResolutionResult result, CombatReactionTriggerType triggerType)
        {
            if (result == null)
            {
                return null;
            }

            return new CombatReactionTriggerContext(
                triggerType,
                result.AttackTransactionId,
                result.ResolvedAttackerActorId,
                result.Request.AttackerObject,
                result.ResolvedTargetActorId,
                result.Request.TargetObject,
                result.DamageResult == null ? 0f : result.DamageResult.FinalDamageAmount,
                preventedDamage: result.DefenseResult == null ? 0f : result.DefenseResult.PreventedDamage,
                critical: result.Critical,
                fullyPrevented: result.DamagePrevented,
                damageType: result.Request.DamageType,
                sourceResult: result);
        }

        public static CombatReactionTriggerContext FromDefenseResult(DefenseResolutionResult result, CombatReactionTriggerType triggerType)
        {
            if (result == null)
            {
                return null;
            }

            return new CombatReactionTriggerContext(
                triggerType,
                result.Request.ParentAttackTransactionId,
                result.Request.AttackerActorId,
                result.Request.AttackerObject,
                result.Request.DefenderActorId,
                result.Request.DefenderObject,
                preventedDamage: result.PreventedDamage,
                fullyPrevented: result.DamageFullyPrevented,
                damageType: result.Request.DamageType,
                sourceResult: result);
        }

        public static CombatReactionTriggerContext FromOngoingApplication(OngoingEffectApplicationResult result, GameObject sourceObject, GameObject targetObject)
        {
            if (result == null)
            {
                return null;
            }

            return new CombatReactionTriggerContext(
                CombatReactionTriggerType.OngoingEffectApplied,
                result.TransactionId,
                result.SourceActorId,
                sourceObject,
                result.TargetActorId,
                targetObject,
                sourceResult: result);
        }

        public static CombatReactionTriggerContext FromOngoingTick(OngoingEffectTickResult result, GameObject targetObject)
        {
            if (result == null)
            {
                return null;
            }

            return new CombatReactionTriggerContext(
                CombatReactionTriggerType.OngoingEffectTicked,
                result.TickTransactionId,
                string.Empty,
                null,
                string.Empty,
                targetObject,
                result.DamageResult == null ? 0f : result.DamageResult.FinalDamageAmount,
                result.HealingResult == null ? 0f : result.HealingResult.FinalHealingAmount,
                damageType: result.DamageResult == null ? null : result.DamageResult.Request.DamageType,
                sourceResult: result);
        }

        public static CombatReactionTriggerContext FromExecutionCommitted(CombatExecutionCommitted committed, CombatReactionTriggerType triggerType)
        {
            if (committed == null)
            {
                return null;
            }

            return new CombatReactionTriggerContext(
                triggerType,
                committed.TransactionId,
                committed.ActorId,
                null,
                committed.TargetActorId,
                null,
                sourceResult: committed);
        }
    }

    public sealed class CombatReactionExecutionResult
    {
        private CombatReactionExecutionResult(bool succeeded, bool preview, bool duplicate, string code, string message, CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, string transactionId, float roll, float finalAmount, object operationResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatReactionResultCode.Success : CombatReactionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Definition = definition;
            Source = source;
            Context = context;
            TransactionId = transactionId ?? string.Empty;
            Roll = roll;
            FinalAmount = finalAmount;
            OperationResult = operationResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public CombatReactionDefinition Definition { get; }
        public string DefinitionId => Definition == null ? string.Empty : Definition.Id;
        public CombatReactionSourceRegistration Source { get; }
        public CombatReactionTriggerContext Context { get; }
        public string TransactionId { get; }
        public float Roll { get; }
        public float FinalAmount { get; }
        public object OperationResult { get; }
        public DamageApplicationResult DamageResult => OperationResult as DamageApplicationResult;
        public HealingApplicationResult HealingResult => OperationResult as HealingApplicationResult;
        public OngoingEffectApplicationResult OngoingEffectResult => OperationResult as OngoingEffectApplicationResult;
        public ResourceChangeResult ResourceResult => OperationResult as ResourceChangeResult;

        public static CombatReactionExecutionResult Success(bool preview, string message, CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, string transactionId, float roll, float finalAmount, object operationResult)
        {
            return new CombatReactionExecutionResult(true, preview, false, preview ? CombatReactionResultCode.Preview : CombatReactionResultCode.Success, message, definition, source, context, transactionId, roll, finalAmount, operationResult);
        }

        public static CombatReactionExecutionResult Failure(bool preview, string code, string message, CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, string transactionId = "", float roll = 0f)
        {
            return new CombatReactionExecutionResult(false, preview, false, code, message, definition, source, context, transactionId, roll, 0f, null);
        }

        public static CombatReactionExecutionResult Duplicated(CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, string transactionId)
        {
            return new CombatReactionExecutionResult(true, false, true, CombatReactionResultCode.Duplicate, "Duplicate reaction operation ignored.", definition, source, context, transactionId, 0f, 0f, null);
        }
    }

    public sealed class CombatReactionChainResult
    {
        public CombatReactionChainResult(bool succeeded, bool preview, string code, string message, CombatReactionTriggerContext rootContext, IReadOnlyList<CombatReactionExecutionResult> reactions, int depth)
        {
            Succeeded = succeeded;
            Preview = preview;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatReactionResultCode.Success : CombatReactionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            RootContext = rootContext;
            Reactions = reactions == null ? Array.Empty<CombatReactionExecutionResult>() : new List<CombatReactionExecutionResult>(reactions);
            Depth = depth;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public string Code { get; }
        public string Message { get; }
        public CombatReactionTriggerContext RootContext { get; }
        public IReadOnlyList<CombatReactionExecutionResult> Reactions { get; }
        public int Depth { get; }
    }

    public static class CombatReactionResultCode
    {
        public const string Success = "Success";
        public const string Preview = "Preview";
        public const string InvalidRequest = "InvalidRequest";
        public const string Duplicate = "Duplicate";
        public const string MissingDefinition = "MissingDefinition";
        public const string MissingTarget = "MissingTarget";
        public const string ProcFailed = "ProcFailed";
        public const string UnsupportedOperation = "UnsupportedOperation";
        public const string ChainLimitReached = "ChainLimitReached";
        public const string OperationRejected = "OperationRejected";
    }
}
