using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.Execution
{
    public sealed class CombatExecutionService
    {
        private const int DefaultProcessedTransactionLimit = 1024;

        private readonly List<ICombatExecutionHandler> handlers = new List<ICombatExecutionHandler>();
        private readonly Dictionary<string, List<ActiveExecutionRecord>> activeByActorId = new Dictionary<string, List<ActiveExecutionRecord>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, RuntimeCooldownRecord>> cooldownsByActorId = new Dictionary<string, Dictionary<string, RuntimeCooldownRecord>>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombatExecutionResult> processedBegins = new Dictionary<string, CombatExecutionResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombatExecutionResult> processedCommits = new Dictionary<string, CombatExecutionResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombatExecutionResult> processedCancellations = new Dictionary<string, CombatExecutionResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActorLifecycleController> lifecycleSubscriptionsByActorId = new Dictionary<string, ActorLifecycleController>(StringComparer.Ordinal);
        private readonly Queue<string> processedBeginOrder = new Queue<string>();
        private readonly Queue<string> processedCommitOrder = new Queue<string>();
        private readonly Queue<string> processedCancellationOrder = new Queue<string>();
        private readonly int processedTransactionLimit;

        public CombatExecutionService(IEnumerable<ICombatExecutionHandler> executionHandlers = null, int processedTransactionLimit = DefaultProcessedTransactionLimit)
        {
            this.processedTransactionLimit = Mathf.Max(16, processedTransactionLimit);
            if (executionHandlers != null)
            {
                foreach (ICombatExecutionHandler handler in executionHandlers)
                {
                    RegisterHandler(handler);
                }
            }

            RegisterHandler(new NoOpCombatExecutionHandler());
        }

        public event Action<CombatExecutionResult> ExecutionBegan;
        public event Action<CombatExecutionResult> ExecutionCommitted;
        public event Action<CombatExecutionResult> ExecutionCompleted;
        public event Action<CombatExecutionResult> ExecutionCancelled;
        public event Action<CombatExecutionResult> ExecutionInterrupted;
        public event Action<CombatExecutionResult> CostCommitted;
        public event Action<CombatExecutionResult> CooldownChanged;
        public event Action<CombatExecutionCommitted> CombatExecutionCommitted;

        public void RegisterHandler(ICombatExecutionHandler handler)
        {
            if (handler == null || handlers.Any(existing => existing.GetType() == handler.GetType() || string.Equals(existing.HandlerId, handler.HandlerId, StringComparison.Ordinal)))
            {
                return;
            }

            handlers.Add(handler);
        }

        public CombatExecutionResult PreviewBeginExecution(CombatExecutionBeginRequest request)
        {
            return ResolveBegin(request, execute: false);
        }

        public CombatExecutionResult BeginExecution(CombatExecutionBeginRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedBegins.TryGetValue(request.TransactionId, out CombatExecutionResult processed))
            {
                return processed.AsDuplicate();
            }

            CombatExecutionResult result = ResolveBegin(request, execute: true);
            if (result.Succeeded)
            {
                Remember(processedBegins, processedBeginOrder, request.TransactionId, result);
                ExecutionBegan?.Invoke(result);
            }

            return result;
        }

        public CombatExecutionResult PreviewCommitExecution(CombatExecutionCommitRequest request)
        {
            return ResolveCommit(request, execute: false);
        }

        public CombatExecutionResult CommitExecution(CombatExecutionCommitRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedCommits.TryGetValue(request.TransactionId, out CombatExecutionResult processed))
            {
                return processed.AsDuplicate();
            }

            CombatExecutionResult result = ResolveCommit(request, execute: true);
            if (result.Succeeded)
            {
                Remember(processedCommits, processedCommitOrder, request.TransactionId, result);
                ExecutionCommitted?.Invoke(result);
            }

            return result;
        }

        public CombatExecutionResult PreviewCancelExecution(CombatExecutionCancelRequest request)
        {
            return ResolveCancel(request, execute: false, interrupted: false);
        }

        public CombatExecutionResult CancelExecution(CombatExecutionCancelRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedCancellations.TryGetValue(request.TransactionId, out CombatExecutionResult processed))
            {
                return processed.AsDuplicate();
            }

            CombatExecutionResult result = ResolveCancel(request, execute: true, interrupted: false);
            if (result.Succeeded)
            {
                Remember(processedCancellations, processedCancellationOrder, request.TransactionId, result);
                ExecutionCancelled?.Invoke(result);
            }

            return result;
        }

        public CombatExecutionResult InterruptExecution(CombatExecutionCancelRequest request)
        {
            CombatExecutionResult result = ResolveCancel(request, execute: true, interrupted: true);
            if (result.Succeeded)
            {
                ExecutionInterrupted?.Invoke(result);
            }

            return result;
        }

        public IReadOnlyList<CombatExecutionResult> ProcessExecutionTime(float authoritativeTime)
        {
            if (!IsFinite(authoritativeTime) || authoritativeTime < 0f)
            {
                return Array.Empty<CombatExecutionResult>();
            }

            List<CombatExecutionResult> results = new List<CombatExecutionResult>();
            foreach (string actorId in activeByActorId.Keys.ToList())
            {
                List<ActiveExecutionRecord> active = activeByActorId[actorId];
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    ActiveExecutionRecord record = active[i];
                    if (!record.Committed || authoritativeTime < record.RecoveryEndsAt)
                    {
                        continue;
                    }

                    List<CombatExecutionCostPreview> completionCosts = CommitCosts(record, CombatExecutionCostCommitPoint.OnCompletion, execute: true, authoritativeTime, out CombatExecutionResult failure);
                    if (failure != null)
                    {
                        results.Add(failure);
                        continue;
                    }

                    if (record.Definition.CooldownStartPoint == CombatExecutionCooldownStartPoint.OnCompletion)
                    {
                        ConsumeCooldownCharge(actorId, record.Definition, authoritativeTime);
                    }

                    active.RemoveAt(i);
                    CombatExecutionStateSnapshot snapshot = record.ToSnapshot(CombatExecutionPhase.Completed, completionCosts);
                    CombatExecutionResult result = CombatExecutionResult.Success(false, CombatExecutionResultCode.Success, $"Completed {record.Definition.DisplayName}.", record.BeginTransactionId, record.Definition, actorId, record.ActorBodyId, snapshot, completionCosts, GetCooldownState(actorId, record.Definition.ResolveCooldownKey()));
                    results.Add(result);
                    ExecutionCompleted?.Invoke(result);
                }

                if (active.Count == 0)
                {
                    RemoveActiveList(actorId);
                }
            }

            RecoverCooldownCharges(authoritativeTime);
            return results;
        }

        public CombatExecutionStateSnapshot GetExecutionState(string actorId, string executionInstanceId = "")
        {
            if (string.IsNullOrWhiteSpace(actorId) || !activeByActorId.TryGetValue(actorId, out List<ActiveExecutionRecord> active))
            {
                return null;
            }

            ActiveExecutionRecord record = string.IsNullOrWhiteSpace(executionInstanceId)
                ? active.FirstOrDefault()
                : active.FirstOrDefault(candidate => string.Equals(candidate.ExecutionInstanceId, executionInstanceId, StringComparison.Ordinal));
            return record == null ? null : record.ToSnapshot(record.Phase, record.CommittedCosts);
        }

        public CombatExecutionCooldownSnapshot GetCooldownState(string actorId, string cooldownKey)
        {
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(cooldownKey) || !cooldownsByActorId.TryGetValue(actorId, out Dictionary<string, RuntimeCooldownRecord> actorCooldowns) || !actorCooldowns.TryGetValue(cooldownKey, out RuntimeCooldownRecord record))
            {
                return null;
            }

            return record.ToSnapshot(actorId, cooldownKey);
        }

        public int GetAvailableCharges(string actorId, string cooldownKey)
        {
            CombatExecutionCooldownSnapshot snapshot = GetCooldownState(actorId, cooldownKey);
            return snapshot == null ? 0 : snapshot.CurrentCharges;
        }

        public void ClearTransientStateForRestore(string actorId = "")
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                foreach (string key in lifecycleSubscriptionsByActorId.Keys.ToList())
                {
                    UnsubscribeLifecycle(key);
                }

                activeByActorId.Clear();
                return;
            }

            RemoveActiveList(actorId);
        }

        public CombatExecutionSaveData CreateSaveData(string playerId, string personId)
        {
            List<CombatExecutionCooldownSaveData> cooldowns = new List<CombatExecutionCooldownSaveData>();
            foreach (KeyValuePair<string, Dictionary<string, RuntimeCooldownRecord>> actorPair in cooldownsByActorId)
            {
                foreach (KeyValuePair<string, RuntimeCooldownRecord> cooldownPair in actorPair.Value)
                {
                    cooldowns.Add(cooldownPair.Value.ToSaveData(actorPair.Key, cooldownPair.Key));
                }
            }

            return new CombatExecutionSaveData
            {
                schemaVersion = CombatExecutionSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                cooldowns = cooldowns
            };
        }

        public bool RestoreFromSaveData(CombatExecutionSaveData saveData, string expectedPlayerId, out string failureReason, bool restoring)
        {
            if (!ValidateSaveData(saveData, expectedPlayerId, out failureReason))
            {
                return false;
            }

            ClearTransientStateForRestore();
            cooldownsByActorId.Clear();
            foreach (CombatExecutionCooldownSaveData cooldown in saveData.cooldowns ?? new List<CombatExecutionCooldownSaveData>())
            {
                GetOrCreateActorCooldowns(cooldown.actorId)[cooldown.cooldownKey] = RuntimeCooldownRecord.FromSaveData(cooldown);
            }

            return true;
        }

        public static bool ValidateSaveData(CombatExecutionSaveData saveData, string expectedPlayerId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Combat execution save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CombatExecutionSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported combat execution schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPlayerId) && !string.Equals(saveData.playerId, expectedPlayerId, StringComparison.Ordinal))
            {
                failureReason = $"Saved combat execution owner '{saveData.playerId}' does not match current player '{expectedPlayerId}'.";
                return false;
            }

            foreach (CombatExecutionCooldownSaveData cooldown in saveData.cooldowns ?? new List<CombatExecutionCooldownSaveData>())
            {
                if (cooldown == null || string.IsNullOrWhiteSpace(cooldown.actorId) || string.IsNullOrWhiteSpace(cooldown.cooldownKey) || cooldown.maximumCharges < 1 || cooldown.currentCharges < 0 || cooldown.currentCharges > cooldown.maximumCharges || !IsFinite(cooldown.nextChargeReadyAt) || !IsFinite(cooldown.cooldownReadyAt))
                {
                    failureReason = "Combat execution cooldown save data is invalid.";
                    return false;
                }
            }

            return true;
        }

        private CombatExecutionResult ResolveBegin(CombatExecutionBeginRequest request, bool execute)
        {
            if (!ValidateRequestShape(request.TransactionId, request.Now, out string shapeCode, out string shapeMessage))
            {
                return CombatExecutionResult.Failure(!execute, shapeCode, shapeMessage, request.TransactionId, request.Definition);
            }

            if (execute && !request.AuthorityValidated)
            {
                return CombatExecutionResult.Failure(false, CombatExecutionResultCode.AuthorityRequired, "Combat execution begin requires validated game/server authority.", request.TransactionId, request.Definition);
            }

            if (!ResolveActor(request.ActorObject, request.ActorId, out ActorRuntime actor, out string actorCode, out string actorMessage))
            {
                return CombatExecutionResult.Failure(!execute, actorCode, actorMessage, request.TransactionId, request.Definition);
            }

            if (request.Definition == null)
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.MissingDefinition, "Combat execution definition is missing.", request.TransactionId, null, actor.ActorId, actor.BodyId);
            }

            if (!ActorLifecycleUtility.CanAct(request.ActorObject))
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.ActorCannotAct, $"Actor '{actor.ActorId}' cannot begin execution while {ActorLifecycleUtility.GetState(request.ActorObject)}.", request.TransactionId, request.Definition, actor.ActorId, actor.BodyId);
            }

            RequirementEvaluationResult requirement = request.Definition.Requirements == null || actor.Character == null
                ? null
                : actor.Character.Query.EvaluateRequirement(request.Definition.Requirements);
            if (requirement != null && !requirement.Passed)
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.RequirementFailed, BuildRequirementMessage(requirement), request.TransactionId, request.Definition, actor.ActorId, actor.BodyId);
            }

            if (HasConflictingCommitment(actor.ActorId, request.Definition, out string conflictMessage))
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.CommitmentConflict, conflictMessage, request.TransactionId, request.Definition, actor.ActorId, actor.BodyId);
            }

            if (!HasAvailableCharge(actor.ActorId, request.Definition, request.Now, createIfMissing: execute, out CombatExecutionCooldownSnapshot cooldown, out string cooldownCode, out string cooldownMessage))
            {
                return CombatExecutionResult.Failure(!execute, cooldownCode, cooldownMessage, request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, cooldown: cooldown);
            }

            List<CombatExecutionCostPreview> beginCosts = CommitCostsPreviewOnly(request.Definition, actor.Resources, CombatExecutionCostCommitPoint.OnBegin);
            if (!AllCostsSucceeded(beginCosts, out string costCode, out string costMessage))
            {
                return CombatExecutionResult.Failure(!execute, costCode, costMessage, request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, costs: beginCosts, cooldown: cooldown);
            }

            if (!TryResolveHandler(request.Definition, request.Payload, out ICombatExecutionHandler handler))
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.FailedUnderlyingAction, $"No combat execution handler accepts payload for '{request.Definition.Id}'.", request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, costs: beginCosts, cooldown: cooldown);
            }

            CombatExecutionHandlerResult handlerPreview = handler.Preview(request.Definition, request.Payload, request.TransactionId);
            if (!handlerPreview.Succeeded)
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.FailedUnderlyingAction, handlerPreview.Message, request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, costs: beginCosts, cooldown: cooldown, underlyingResult: handlerPreview.PayloadResult);
            }

            if (!execute)
            {
                CombatExecutionStateSnapshot previewState = new CombatExecutionStateSnapshot(string.Empty, request.TransactionId, actor.ActorId, actor.BodyId, request.Definition, request.Definition.WindUpDuration > 0f ? CombatExecutionPhase.WindUp : CombatExecutionPhase.ReadyToCommit, request.Now, request.Now + request.Definition.WindUpDuration, 0f, 0f, false, false, false, false, Array.Empty<CombatExecutionCostPreview>());
                return CombatExecutionResult.Success(true, CombatExecutionResultCode.Preview, $"Previewed begin for {request.Definition.DisplayName}.", request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, previewState, beginCosts, cooldown, handlerPreview.PayloadResult);
            }

            ActiveExecutionRecord record = new ActiveExecutionRecord(CreateExecutionInstanceId(request), request.TransactionId, actor.ActorId, actor.BodyId, request.ActorObject, request.Definition, request.Now, request.Now + request.Definition.WindUpDuration, handler, request.Payload);
            List<CombatExecutionCostPreview> committedBeginCosts = CommitCosts(record, CombatExecutionCostCommitPoint.OnBegin, execute: true, request.Now, out CombatExecutionResult beginFailure);
            if (beginFailure != null)
            {
                return beginFailure;
            }

            record.CommittedCosts.AddRange(committedBeginCosts);
            if (request.Definition.CooldownStartPoint == CombatExecutionCooldownStartPoint.OnBegin)
            {
                ConsumeCooldownCharge(actor.ActorId, request.Definition, request.Now);
                cooldown = GetCooldownState(actor.ActorId, request.Definition.ResolveCooldownKey());
                CooldownChanged?.Invoke(CombatExecutionResult.Success(false, CombatExecutionResultCode.Success, "Cooldown charge consumed on begin.", request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts), committedBeginCosts, cooldown));
            }

            GetOrCreateActiveList(actor.ActorId).Add(record);
            SubscribeLifecycle(actor.ActorId, request.ActorObject.GetComponentInParent<ActorLifecycleController>());
            CombatExecutionStateSnapshot snapshot = record.ToSnapshot(record.Phase, record.CommittedCosts);
            return CombatExecutionResult.Success(false, CombatExecutionResultCode.Success, $"Began {request.Definition.DisplayName}.", request.TransactionId, request.Definition, actor.ActorId, actor.BodyId, snapshot, committedBeginCosts, cooldown, handlerPreview.PayloadResult);
        }

        private CombatExecutionResult ResolveCommit(CombatExecutionCommitRequest request, bool execute)
        {
            if (!ValidateRequestShape(request.TransactionId, request.Now, out string shapeCode, out string shapeMessage))
            {
                return CombatExecutionResult.Failure(!execute, shapeCode, shapeMessage, request.TransactionId);
            }

            if (execute && !request.AuthorityValidated)
            {
                return CombatExecutionResult.Failure(false, CombatExecutionResultCode.AuthorityRequired, "Combat execution commit requires validated game/server authority.", request.TransactionId);
            }

            if (!ResolveActor(request.ActorObject, request.ActorId, out ActorRuntime actor, out string actorCode, out string actorMessage))
            {
                return CombatExecutionResult.Failure(!execute, actorCode, actorMessage, request.TransactionId);
            }

            if (!TryFindActive(actor.ActorId, request.ExecutionInstanceId, out ActiveExecutionRecord record))
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.MissingExecution, $"Actor '{actor.ActorId}' has no matching active execution.", request.TransactionId, actorId: actor.ActorId, actorBodyId: actor.BodyId);
            }

            if (!string.Equals(record.ActorBodyId, actor.BodyId, StringComparison.Ordinal))
            {
                if (execute)
                {
                    RemoveActive(record);
                }

                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.StaleBody, $"Execution '{record.ExecutionInstanceId}' belongs to a different actor body.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts));
            }

            if (!ActorLifecycleUtility.CanAct(request.ActorObject))
            {
                if (execute)
                {
                    RemoveActive(record);
                }

                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.ActorCannotAct, $"Actor '{actor.ActorId}' cannot commit execution while {ActorLifecycleUtility.GetState(request.ActorObject)}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts));
            }

            if (request.Now < record.ReadyAt)
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.ExecutionTooEarly, $"Execution '{record.ExecutionInstanceId}' is not ready until {record.ReadyAt:0.###}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts));
            }

            List<CombatExecutionCostPreview> executionCosts = CommitCostsPreviewOnly(record.Definition, actor.Resources, CombatExecutionCostCommitPoint.OnExecution);
            if (!AllCostsSucceeded(executionCosts, out string costCode, out string costMessage))
            {
                return CombatExecutionResult.Failure(!execute, costCode, costMessage, request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts), executionCosts, GetCooldownState(actor.ActorId, record.Definition.ResolveCooldownKey()));
            }

            CombatExecutionHandlerResult handlerPreview = record.Handler.Preview(record.Definition, request.Payload ?? record.Payload, request.TransactionId);
            if (!handlerPreview.Succeeded)
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.FailedUnderlyingAction, handlerPreview.Message, request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts), executionCosts, GetCooldownState(actor.ActorId, record.Definition.ResolveCooldownKey()), handlerPreview.PayloadResult);
            }

            if (!execute)
            {
                CombatExecutionStateSnapshot previewState = record.ToSnapshot(CombatExecutionPhase.ReadyToCommit, record.CommittedCosts);
                return CombatExecutionResult.Success(true, CombatExecutionResultCode.Preview, $"Previewed commit for {record.Definition.DisplayName}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, previewState, executionCosts, GetCooldownState(actor.ActorId, record.Definition.ResolveCooldownKey()), handlerPreview.PayloadResult);
            }

            List<CombatExecutionCostPreview> committedCosts = CommitCosts(record, CombatExecutionCostCommitPoint.OnExecution, execute: true, request.Now, out CombatExecutionResult commitFailure);
            if (commitFailure != null)
            {
                return commitFailure;
            }

            record.CommittedCosts.AddRange(committedCosts);
            CombatExecutionCooldownSnapshot cooldown = GetCooldownState(actor.ActorId, record.Definition.ResolveCooldownKey());
            if (record.Definition.CooldownStartPoint == CombatExecutionCooldownStartPoint.OnExecution)
            {
                ConsumeCooldownCharge(actor.ActorId, record.Definition, request.Now);
                cooldown = GetCooldownState(actor.ActorId, record.Definition.ResolveCooldownKey());
            }

            CombatExecutionHandlerResult handlerResult = record.Handler.Execute(record.Definition, request.Payload ?? record.Payload, request.TransactionId);
            if (!handlerResult.Succeeded)
            {
                TryRefund(record, committedCosts.Where(cost => cost.ResourceResult != null).ToList(), $"{request.TransactionId}.refund", out _);
                return CombatExecutionResult.Failure(false, CombatExecutionResultCode.FailedUnderlyingAction, handlerResult.Message, request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts), committedCosts, cooldown, handlerResult.PayloadResult);
            }

            List<CombatExecutionCostPreview> successCosts = CommitCosts(record, CombatExecutionCostCommitPoint.OnSuccessfulExecution, execute: true, request.Now, out CombatExecutionResult successCostFailure);
            if (successCostFailure != null)
            {
                return successCostFailure;
            }

            record.CommittedCosts.AddRange(successCosts);
            record.MarkCommitted(request.Now);
            CombatExecutionStateSnapshot snapshot = record.ToSnapshot(record.Phase, record.CommittedCosts);
            CombatExecutionResult result = CombatExecutionResult.Success(false, CombatExecutionResultCode.Success, $"Committed {record.Definition.DisplayName}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, snapshot, committedCosts.Concat(successCosts).ToList(), cooldown, handlerResult.PayloadResult);
            CostCommitted?.Invoke(result);
            CombatExecutionCommitted?.Invoke(BuildCommittedEvent(request, record, snapshot, result, request.Payload ?? record.Payload));
            return result;
        }

        private CombatExecutionResult ResolveCancel(CombatExecutionCancelRequest request, bool execute, bool interrupted)
        {
            if (!ResolveActor(request.ActorObject, request.ActorId, out ActorRuntime actor, out string actorCode, out string actorMessage))
            {
                return CombatExecutionResult.Failure(!execute, actorCode, actorMessage, request.TransactionId);
            }

            if (!TryFindActive(actor.ActorId, request.ExecutionInstanceId, out ActiveExecutionRecord record))
            {
                return CombatExecutionResult.Failure(!execute, CombatExecutionResultCode.MissingExecution, $"Actor '{actor.ActorId}' has no matching active execution.", request.TransactionId, actorId: actor.ActorId, actorBodyId: actor.BodyId);
            }

            if (interrupted && !CanInterrupt(record, request.Now))
            {
                return CombatExecutionResult.Failure(false, CombatExecutionResultCode.Interrupted, $"Execution '{record.ExecutionInstanceId}' is not interruptible in phase {record.Phase}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(record.Phase, record.CommittedCosts));
            }

            if (!execute)
            {
                CombatExecutionPhase previewPhase = interrupted ? CombatExecutionPhase.Interrupted : CombatExecutionPhase.Cancelled;
                return CombatExecutionResult.Success(true, CombatExecutionResultCode.Preview, $"Previewed {previewPhase} for {record.Definition.DisplayName}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(previewPhase, record.CommittedCosts));
            }

            if (!record.Committed && record.Definition.BeginCostRefundPolicy == CombatExecutionRefundPolicy.RefundIfCancelledBeforeExecution)
            {
                TryRefund(record, record.CommittedCosts.Where(cost => cost.ResourceResult != null).ToList(), $"{request.TransactionId}.refund", out _);
            }

            RemoveActive(record);
            CombatExecutionPhase phase = interrupted ? CombatExecutionPhase.Interrupted : CombatExecutionPhase.Cancelled;
            string code = interrupted ? CombatExecutionResultCode.Interrupted : CombatExecutionResultCode.Cancelled;
            return CombatExecutionResult.Success(false, code, $"{phase} {record.Definition.DisplayName}.", request.TransactionId, record.Definition, actor.ActorId, actor.BodyId, record.ToSnapshot(phase, record.CommittedCosts));
        }

        private List<CombatExecutionCostPreview> CommitCostsPreviewOnly(CombatExecutionDefinition definition, CharacterResourceCollection resources, CombatExecutionCostCommitPoint commitPoint)
        {
            return PreviewCosts(definition, resources, commitPoint, null, execute: false, 0f, out _);
        }

        private List<CombatExecutionCostPreview> CommitCosts(ActiveExecutionRecord record, CombatExecutionCostCommitPoint commitPoint, bool execute, float now, out CombatExecutionResult failure)
        {
            failure = null;
            List<CombatExecutionCostPreview> results = PreviewCosts(record.Definition, ResolveResources(record.ActorObject), commitPoint, record, execute, now, out List<CombatExecutionCostPreview> committed);
            if (!AllCostsSucceeded(results, out string code, out string message))
            {
                failure = CombatExecutionResult.Failure(!execute, code, message, record.BeginTransactionId, record.Definition, record.ActorId, record.ActorBodyId, record.ToSnapshot(record.Phase, record.CommittedCosts), results, GetCooldownState(record.ActorId, record.Definition.ResolveCooldownKey()));
                if (execute)
                {
                    TryRefund(record, committed, $"{record.BeginTransactionId}.cost-failure-refund", out _);
                }
            }

            return results;
        }

        private List<CombatExecutionCostPreview> PreviewCosts(CombatExecutionDefinition definition, CharacterResourceCollection resources, CombatExecutionCostCommitPoint commitPoint, ActiveExecutionRecord record, bool execute, float now, out List<CombatExecutionCostPreview> committed)
        {
            committed = new List<CombatExecutionCostPreview>();
            List<CombatExecutionCostPreview> results = new List<CombatExecutionCostPreview>();
            IReadOnlyList<CombatExecutionCostDefinition> costs = definition.Costs;
            for (int i = 0; i < costs.Count; i++)
            {
                CombatExecutionCostDefinition cost = costs[i];
                if (cost == null || cost.CommitPoint != commitPoint || !cost.Consumed)
                {
                    continue;
                }

                if (cost.CostType != CombatExecutionCostType.Resource)
                {
                    results.Add(new CombatExecutionCostPreview(i, cost.CostType, cost.CommitPoint, cost.DefinitionId, cost.Amount, cost.Required, cost.Consumed, false, false, CombatExecutionResultCode.UnsupportedCostType, $"{cost.CostType} costs are intentionally rejected until a production transactional API exists."));
                    continue;
                }

                if (resources == null || !resources.TryGetResource(cost.DefinitionId, out ResourceSnapshot snapshot))
                {
                    results.Add(new CombatExecutionCostPreview(i, cost.CostType, cost.CommitPoint, cost.DefinitionId, cost.Amount, cost.Required, cost.Consumed, true, false, CombatExecutionResultCode.MissingResource, $"Resource '{cost.DefinitionId}' is not configured."));
                    continue;
                }

                bool affordable = snapshot.Current - cost.Amount >= Mathf.Max(snapshot.Minimum, cost.MinimumRemaining) - CharacterResourceCollection.Epsilon;
                if (!affordable)
                {
                    results.Add(new CombatExecutionCostPreview(i, cost.CostType, cost.CommitPoint, cost.DefinitionId, cost.Amount, cost.Required, cost.Consumed, true, false, CombatExecutionResultCode.InsufficientResource, $"Not enough {cost.DefinitionId}.", snapshot));
                    continue;
                }

                if (!execute)
                {
                    results.Add(new CombatExecutionCostPreview(i, cost.CostType, cost.CommitPoint, cost.DefinitionId, cost.Amount, cost.Required, cost.Consumed, true, true, CombatExecutionResultCode.Preview, $"Would spend {cost.Amount:0.###} {cost.DefinitionId}.", snapshot));
                    continue;
                }

                string transactionId = DeriveCostTransactionId(record.BeginTransactionId, commitPoint, i);
                ResourceChangeResult resourceResult = resources.ApplyChange(new ResourceChangeRequest(cost.DefinitionId, ResourceChangeOperation.Spend, cost.Amount, ResourceChangeSourceCategory.Combat, definition.Id, $"Combat execution {definition.DisplayName}.", transactionId, allowPartial: false, authorityValidated: true));
                CombatExecutionCostPreview committedCost = new CombatExecutionCostPreview(i, cost.CostType, cost.CommitPoint, cost.DefinitionId, cost.Amount, cost.Required, cost.Consumed, true, resourceResult.Succeeded, resourceResult.Succeeded ? CombatExecutionResultCode.Success : resourceResult.Code, resourceResult.Message, snapshot, resourceResult);
                results.Add(committedCost);
                if (resourceResult.Succeeded && !resourceResult.DuplicateEvent)
                {
                    committed.Add(committedCost);
                }
            }

            return results;
        }

        private bool TryRefund(ActiveExecutionRecord record, IReadOnlyList<CombatExecutionCostPreview> committed, string refundTransactionPrefix, out string failureReason)
        {
            failureReason = string.Empty;
            CharacterResourceCollection resources = ResolveResources(record.ActorObject);
            if (resources == null)
            {
                failureReason = "Resource collection missing during refund.";
                return false;
            }

            for (int i = 0; i < committed.Count; i++)
            {
                CombatExecutionCostPreview cost = committed[i];
                ResourceChangeResult refund = resources.ApplyChange(new ResourceChangeRequest(cost.DefinitionId, ResourceChangeOperation.Gain, cost.ResourceResult.AppliedAmount, ResourceChangeSourceCategory.Combat, record.Definition.Id, $"Refund {record.Definition.DisplayName}.", $"{refundTransactionPrefix}.{cost.Index}", allowPartial: true, authorityValidated: true));
                if (!refund.Succeeded)
                {
                    failureReason = refund.Message;
                    return false;
                }
            }

            return true;
        }

        private bool HasAvailableCharge(string actorId, CombatExecutionDefinition definition, float now, bool createIfMissing, out CombatExecutionCooldownSnapshot snapshot, out string code, out string message)
        {
            string cooldownKey = definition.ResolveCooldownKey();
            if (!cooldownsByActorId.TryGetValue(actorId, out Dictionary<string, RuntimeCooldownRecord> actorCooldowns) || !actorCooldowns.TryGetValue(cooldownKey, out RuntimeCooldownRecord record))
            {
                if (!createIfMissing)
                {
                    snapshot = new CombatExecutionCooldownSnapshot(actorId, cooldownKey, definition.Id, definition.MaximumCharges, definition.MaximumCharges, now, now);
                    code = CombatExecutionResultCode.Success;
                    message = string.Empty;
                    return true;
                }

                record = GetOrCreateCooldown(actorId, definition, now);
            }

            RecoverCooldown(record, definition, now);
            snapshot = record.ToSnapshot(actorId, definition.ResolveCooldownKey());
            if (record.CurrentCharges > 0)
            {
                code = CombatExecutionResultCode.Success;
                message = string.Empty;
                return true;
            }

            code = definition.MaximumCharges > 1 ? CombatExecutionResultCode.NoChargesAvailable : CombatExecutionResultCode.CooldownActive;
            message = $"Combat execution '{definition.Id}' is not ready until {record.CooldownReadyAt:0.###}.";
            return false;
        }

        private void ConsumeCooldownCharge(string actorId, CombatExecutionDefinition definition, float now)
        {
            RuntimeCooldownRecord record = GetOrCreateCooldown(actorId, definition, now);
            RecoverCooldown(record, definition, now);
            record.CurrentCharges = Mathf.Max(0, record.CurrentCharges - 1);
            float duration = definition.ChargeRecoveryDuration > 0f ? definition.ChargeRecoveryDuration : definition.CooldownDuration;
            record.NextChargeReadyAt = duration > 0f ? now + duration : now;
            record.CooldownReadyAt = record.CurrentCharges > 0 ? now : record.NextChargeReadyAt;
        }

        private RuntimeCooldownRecord GetOrCreateCooldown(string actorId, CombatExecutionDefinition definition, float now)
        {
            Dictionary<string, RuntimeCooldownRecord> actorCooldowns = GetOrCreateActorCooldowns(actorId);
            string cooldownKey = definition.ResolveCooldownKey();
            if (!actorCooldowns.TryGetValue(cooldownKey, out RuntimeCooldownRecord record))
            {
                record = new RuntimeCooldownRecord(definition.Id, definition.MaximumCharges, definition.MaximumCharges, now, now);
                actorCooldowns[cooldownKey] = record;
            }

            return record;
        }

        private Dictionary<string, RuntimeCooldownRecord> GetOrCreateActorCooldowns(string actorId)
        {
            if (!cooldownsByActorId.TryGetValue(actorId, out Dictionary<string, RuntimeCooldownRecord> actorCooldowns))
            {
                actorCooldowns = new Dictionary<string, RuntimeCooldownRecord>(StringComparer.Ordinal);
                cooldownsByActorId[actorId] = actorCooldowns;
            }

            return actorCooldowns;
        }

        private void RecoverCooldownCharges(float now)
        {
            foreach (Dictionary<string, RuntimeCooldownRecord> actorCooldowns in cooldownsByActorId.Values)
            {
                foreach (RuntimeCooldownRecord record in actorCooldowns.Values)
                {
                    RecoverCooldown(record, record.DefinitionId, now);
                }
            }
        }

        private void RecoverCooldown(RuntimeCooldownRecord record, CombatExecutionDefinition definition, float now)
        {
            RecoverCooldown(record, definition.Id, now);
        }

        private void RecoverCooldown(RuntimeCooldownRecord record, string definitionId, float now)
        {
            if (record == null || record.CurrentCharges >= record.MaximumCharges || now < record.NextChargeReadyAt)
            {
                return;
            }

            record.CurrentCharges = record.MaximumCharges;
            record.CooldownReadyAt = now;
            record.NextChargeReadyAt = now;
        }

        private bool TryResolveHandler(CombatExecutionDefinition definition, object payload, out ICombatExecutionHandler handler)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                if (handlers[i].CanHandle(definition, payload))
                {
                    handler = handlers[i];
                    return true;
                }
            }

            handler = null;
            return false;
        }

        private static CombatExecutionCommitted BuildCommittedEvent(CombatExecutionCommitRequest request, ActiveExecutionRecord record, CombatExecutionStateSnapshot snapshot, CombatExecutionResult result, object payload)
        {
            Dictionary<string, string> context = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["execution.transaction"] = request.TransactionId,
                ["execution.beginTransaction"] = record.BeginTransactionId,
                ["execution.instance"] = record.ExecutionInstanceId,
                ["execution.definition"] = record.Definition.Id,
                ["execution.actionType"] = record.Definition.ActionType.ToString(),
                ["execution.commitmentCategory"] = record.Definition.CommitmentCategory.ToString()
            };

            string targetActorId = ResolveTargetActorId(payload, result.UnderlyingResult, context);
            return new CombatExecutionCommitted(
                request.TransactionId,
                record.BeginTransactionId,
                record.ExecutionInstanceId,
                record.ActorId,
                record.ActorBodyId,
                targetActorId,
                record.Definition,
                snapshot,
                result.Costs,
                result.Cooldown,
                result.UnderlyingResult,
                context);
        }

        private static string ResolveTargetActorId(object payload, object underlyingResult, Dictionary<string, string> context)
        {
            if (underlyingResult is AttackResolutionResult attackResult)
            {
                AddContext(context, "attack.transaction", attackResult.AttackTransactionId);
                AddContext(context, "attack.damageTransaction", attackResult.DamageTransactionId);
                AddContext(context, "attack.outcome", attackResult.Outcome.ToString());
                AddContext(context, "attack.damageType", attackResult.DamageTypeId);
                AddContext(context, "attack.originatingAction", attackResult.OriginatingActionId);
                AddContext(context, "attack.originatingAbility", attackResult.OriginatingAbilityId);
                AddContext(context, "attack.originatingItemOrWeapon", attackResult.OriginatingItemOrWeaponId);
                AddContext(context, "attack.originatingSpellOrEffect", attackResult.OriginatingSpellOrEffectId);
                return attackResult.ResolvedTargetActorId;
            }

            if (underlyingResult is DefenseActivationResult defenseResult)
            {
                AddContext(context, "defense.transaction", defenseResult.Request.TransactionId);
                AddContext(context, "defense.definition", defenseResult.State == null ? string.Empty : defenseResult.State.DefinitionId);
                AddContext(context, "defense.sourceEquipment", defenseResult.Request.SourceEquipmentId);
                AddContext(context, "defense.sourceAction", defenseResult.Request.SourceActionId);
                return defenseResult.Request.DefenderActorId;
            }

            if (underlyingResult is AbilityExecutionResult abilityResult)
            {
                AddContext(context, "ability.status", abilityResult.Status.ToString());
                AddContext(context, "ability.failedEffectIndex", abilityResult.FailedEffectIndex.ToString());
            }

            if (payload is AttackResolutionRequest attackRequest)
            {
                AddContext(context, "attack.transaction", attackRequest.TransactionId);
                AddContext(context, "attack.damageType", attackRequest.DamageType == null ? string.Empty : attackRequest.DamageType.Id);
                return attackRequest.TargetActorId;
            }

            if (payload is AbilityExecutionContext abilityContext)
            {
                AddContext(context, "ability.definition", abilityContext.Ability == null ? string.Empty : abilityContext.Ability.Id);
                return ResolveActorId(abilityContext.Target);
            }

            if (payload is DefenseActivationRequest defenseRequest)
            {
                AddContext(context, "defense.transaction", defenseRequest.TransactionId);
                AddContext(context, "defense.definition", defenseRequest.Definition == null ? string.Empty : defenseRequest.Definition.Id);
                AddContext(context, "defense.sourceEquipment", defenseRequest.SourceEquipmentId);
                AddContext(context, "defense.sourceAction", defenseRequest.SourceActionId);
                return defenseRequest.DefenderActorId;
            }

            return string.Empty;
        }

        private static void AddContext(Dictionary<string, string> context, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                context[key] = value;
            }
        }

        private static bool AllCostsSucceeded(IReadOnlyList<CombatExecutionCostPreview> costs, out string code, out string message)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                CombatExecutionCostPreview cost = costs[i];
                if (cost.Required && !cost.Succeeded)
                {
                    code = cost.Code;
                    message = cost.Message;
                    return false;
                }
            }

            code = CombatExecutionResultCode.Success;
            message = string.Empty;
            return true;
        }

        private bool HasConflictingCommitment(string actorId, CombatExecutionDefinition definition, out string message)
        {
            message = string.Empty;
            if (!activeByActorId.TryGetValue(actorId, out List<ActiveExecutionRecord> active))
            {
                return false;
            }

            for (int i = 0; i < active.Count; i++)
            {
                CombatExecutionDefinition other = active[i].Definition;
                if (!definition.CanOverlapWith(other.CommitmentCategory) || !other.CanOverlapWith(definition.CommitmentCategory))
                {
                    message = $"Execution '{definition.Id}' conflicts with active '{other.Id}'.";
                    return true;
                }
            }

            return false;
        }

        private List<ActiveExecutionRecord> GetOrCreateActiveList(string actorId)
        {
            if (!activeByActorId.TryGetValue(actorId, out List<ActiveExecutionRecord> active))
            {
                active = new List<ActiveExecutionRecord>();
                activeByActorId[actorId] = active;
            }

            return active;
        }

        private bool TryFindActive(string actorId, string executionInstanceId, out ActiveExecutionRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(actorId) || !activeByActorId.TryGetValue(actorId, out List<ActiveExecutionRecord> active))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(executionInstanceId))
            {
                record = active.Count == 1 ? active[0] : null;
                return record != null;
            }

            record = active.FirstOrDefault(candidate => string.Equals(candidate.ExecutionInstanceId, executionInstanceId, StringComparison.Ordinal));
            return record != null;
        }

        private void RemoveActive(ActiveExecutionRecord record)
        {
            if (record == null || !activeByActorId.TryGetValue(record.ActorId, out List<ActiveExecutionRecord> active))
            {
                return;
            }

            active.Remove(record);
            if (active.Count == 0)
            {
                RemoveActiveList(record.ActorId);
            }
        }

        private void RemoveActiveList(string actorId)
        {
            activeByActorId.Remove(actorId);
            UnsubscribeLifecycle(actorId);
        }

        private static bool CanInterrupt(ActiveExecutionRecord record, float now)
        {
            return record.Definition.InterruptionPolicy switch
            {
                CombatExecutionInterruptionPolicy.NotInterruptible => false,
                CombatExecutionInterruptionPolicy.InterruptDuringWindUp => !record.Committed && now < record.ReadyAt,
                CombatExecutionInterruptionPolicy.InterruptBeforeExecution => !record.Committed,
                CombatExecutionInterruptionPolicy.InterruptAnytimeBeforeCompletion => !record.Completed,
                _ => false
            };
        }

        private static bool ValidateRequestShape(string transactionId, float now, out string code, out string message)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Length > 160 || transactionId.Any(char.IsWhiteSpace))
            {
                code = CombatExecutionResultCode.InvalidRequest;
                message = "Combat execution transaction ID is missing or malformed.";
                return false;
            }

            if (!IsFinite(now) || now < 0f)
            {
                code = CombatExecutionResultCode.InvalidClock;
                message = "Combat execution clock must be finite and non-negative.";
                return false;
            }

            code = CombatExecutionResultCode.Success;
            message = string.Empty;
            return true;
        }

        private static bool ResolveActor(GameObject actorObject, string expectedActorId, out ActorRuntime actor, out string code, out string message)
        {
            actor = default;
            if (actorObject == null)
            {
                code = CombatExecutionResultCode.MissingActor;
                message = "Actor object is missing.";
                return false;
            }

            CharacterSystemCoordinator character = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            string actorId = character == null ? string.Empty : character.ActorId;
            if (string.IsNullOrWhiteSpace(actorId))
            {
                WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
                actorId = identity == null ? string.Empty : identity.EntityId;
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                actorId = $"actor.runtime.{RuntimeHelpers.GetHashCode(actorObject):x8}";
            }

            if (!string.IsNullOrWhiteSpace(expectedActorId) && !string.Equals(expectedActorId, actorId, StringComparison.Ordinal))
            {
                code = CombatExecutionResultCode.StaleActor;
                message = $"Actor identity '{expectedActorId}' no longer resolves to '{actorId}'.";
                return false;
            }

            string bodyId = ResolveBodyId(actorObject);
            if (string.IsNullOrWhiteSpace(bodyId))
            {
                code = CombatExecutionResultCode.MissingBody;
                message = "Actor body identity is missing.";
                return false;
            }

            actor = new ActorRuntime(actorObject, actorId, bodyId, character, character == null ? actorObject.GetComponentInParent<CharacterResourceCollection>() : character.Resources);
            code = CombatExecutionResultCode.Success;
            message = string.Empty;
            return true;
        }

        private static string ResolveBodyId(GameObject actorObject)
        {
            WorldEntityIdentity identity = actorObject == null ? null : actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity != null && !string.IsNullOrWhiteSpace(identity.EntityId)
                ? identity.EntityId
                : $"body.runtime.{RuntimeHelpers.GetHashCode(actorObject):x8}";
        }

        private static string ResolveActorId(GameObject actorObject)
        {
            if (actorObject == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator character = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity != null ? identity.EntityId : string.Empty;
        }

        private static CharacterResourceCollection ResolveResources(GameObject actorObject)
        {
            CharacterSystemCoordinator character = actorObject == null ? null : actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            return character == null ? actorObject == null ? null : actorObject.GetComponentInParent<CharacterResourceCollection>() : character.Resources;
        }

        private static string BuildRequirementMessage(RequirementEvaluationResult result)
        {
            IReadOnlyList<string> failures = result.VisibleFailureReasons.Count > 0 ? result.VisibleFailureReasons : result.TestLabFailureReasons;
            return failures.Count == 0 ? $"Requirement set '{result.RequirementSetId}' failed." : $"Requirement set '{result.RequirementSetId}' failed: {string.Join("; ", failures)}";
        }

        private static string CreateExecutionInstanceId(CombatExecutionBeginRequest request)
        {
            return string.IsNullOrWhiteSpace(request.TransactionId)
                ? $"combat-execution.runtime.{Guid.NewGuid():N}"
                : $"{request.TransactionId}.execution";
        }

        private static string DeriveCostTransactionId(string beginTransactionId, CombatExecutionCostCommitPoint commitPoint, int index)
        {
            return $"{beginTransactionId}.cost.{commitPoint}.{index}";
        }

        private void SubscribeLifecycle(string actorId, ActorLifecycleController lifecycle)
        {
            if (string.IsNullOrWhiteSpace(actorId) || lifecycle == null)
            {
                return;
            }

            UnsubscribeLifecycle(actorId);
            lifecycle.ActorDefeated += OnLifecycleInvalidated;
            lifecycle.ActorBecameUnconscious += OnLifecycleInvalidated;
            lifecycle.ActorDied += OnLifecycleInvalidated;
            lifecycleSubscriptionsByActorId[actorId] = lifecycle;
        }

        private void UnsubscribeLifecycle(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId) || !lifecycleSubscriptionsByActorId.TryGetValue(actorId, out ActorLifecycleController lifecycle) || lifecycle == null)
            {
                lifecycleSubscriptionsByActorId.Remove(actorId);
                return;
            }

            lifecycle.ActorDefeated -= OnLifecycleInvalidated;
            lifecycle.ActorBecameUnconscious -= OnLifecycleInvalidated;
            lifecycle.ActorDied -= OnLifecycleInvalidated;
            lifecycleSubscriptionsByActorId.Remove(actorId);
        }

        private void OnLifecycleInvalidated(ActorLifecycleResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.TargetActorId))
            {
                return;
            }

            if (!activeByActorId.TryGetValue(result.TargetActorId, out List<ActiveExecutionRecord> active))
            {
                return;
            }

            foreach (ActiveExecutionRecord record in active.ToList())
            {
                CombatExecutionResult interrupted = CombatExecutionResult.Success(false, CombatExecutionResultCode.Interrupted, $"Interrupted {record.Definition.DisplayName} because actor lifecycle changed.", result.TransactionId, record.Definition, record.ActorId, record.ActorBodyId, record.ToSnapshot(CombatExecutionPhase.Interrupted, record.CommittedCosts));
                ExecutionInterrupted?.Invoke(interrupted);
            }

            RemoveActiveList(result.TargetActorId);
        }

        private void Remember(Dictionary<string, CombatExecutionResult> results, Queue<string> order, string transactionId, CombatExecutionResult result)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || results.ContainsKey(transactionId))
            {
                return;
            }

            results.Add(transactionId, result);
            order.Enqueue(transactionId);
            while (results.Count > processedTransactionLimit && order.Count > 0)
            {
                results.Remove(order.Dequeue());
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct ActorRuntime
        {
            public ActorRuntime(GameObject gameObject, string actorId, string bodyId, CharacterSystemCoordinator character, CharacterResourceCollection resources)
            {
                GameObject = gameObject;
                ActorId = actorId ?? string.Empty;
                BodyId = bodyId ?? string.Empty;
                Character = character;
                Resources = resources;
            }

            public GameObject GameObject { get; }
            public string ActorId { get; }
            public string BodyId { get; }
            public CharacterSystemCoordinator Character { get; }
            public CharacterResourceCollection Resources { get; }
        }

        private sealed class ActiveExecutionRecord
        {
            public ActiveExecutionRecord(string executionInstanceId, string beginTransactionId, string actorId, string actorBodyId, GameObject actorObject, CombatExecutionDefinition definition, float begunAt, float readyAt, ICombatExecutionHandler handler, object payload)
            {
                ExecutionInstanceId = executionInstanceId ?? string.Empty;
                BeginTransactionId = beginTransactionId ?? string.Empty;
                ActorId = actorId ?? string.Empty;
                ActorBodyId = actorBodyId ?? string.Empty;
                ActorObject = actorObject;
                Definition = definition;
                BegunAt = begunAt;
                ReadyAt = readyAt;
                Handler = handler;
                Payload = payload;
                Phase = readyAt > begunAt ? CombatExecutionPhase.WindUp : CombatExecutionPhase.ReadyToCommit;
            }

            public string ExecutionInstanceId { get; }
            public string BeginTransactionId { get; }
            public string ActorId { get; }
            public string ActorBodyId { get; }
            public CombatExecutionDefinition Definition { get; }
            public GameObject ActorObject { get; private set; }
            public float BegunAt { get; }
            public float ReadyAt { get; }
            public float CommittedAt { get; private set; }
            public float RecoveryEndsAt { get; private set; }
            public bool Committed { get; private set; }
            public bool Completed { get; private set; }
            public CombatExecutionPhase Phase { get; private set; }
            public ICombatExecutionHandler Handler { get; }
            public object Payload { get; }
            public List<CombatExecutionCostPreview> CommittedCosts { get; } = new List<CombatExecutionCostPreview>();

            public void MarkCommitted(float now)
            {
                Committed = true;
                CommittedAt = now;
                RecoveryEndsAt = now + Definition.RecoveryDuration;
                Phase = Definition.RecoveryDuration > 0f ? CombatExecutionPhase.Recovery : CombatExecutionPhase.Completed;
                Completed = Phase == CombatExecutionPhase.Completed;
            }

            public CombatExecutionStateSnapshot ToSnapshot(CombatExecutionPhase phase, IReadOnlyList<CombatExecutionCostPreview> committedCosts)
            {
                return new CombatExecutionStateSnapshot(ExecutionInstanceId, BeginTransactionId, ActorId, ActorBodyId, Definition, phase, BegunAt, ReadyAt, CommittedAt, RecoveryEndsAt, Committed, phase == CombatExecutionPhase.Completed, phase == CombatExecutionPhase.Cancelled, phase == CombatExecutionPhase.Interrupted, committedCosts);
            }
        }

        private sealed class RuntimeCooldownRecord
        {
            public RuntimeCooldownRecord(string definitionId, int maximumCharges, int currentCharges, float nextChargeReadyAt, float cooldownReadyAt)
            {
                DefinitionId = definitionId ?? string.Empty;
                MaximumCharges = Mathf.Max(1, maximumCharges);
                CurrentCharges = Mathf.Clamp(currentCharges, 0, MaximumCharges);
                NextChargeReadyAt = nextChargeReadyAt;
                CooldownReadyAt = cooldownReadyAt;
            }

            public string DefinitionId { get; }
            public int MaximumCharges { get; }
            public int CurrentCharges { get; set; }
            public float NextChargeReadyAt { get; set; }
            public float CooldownReadyAt { get; set; }

            public CombatExecutionCooldownSnapshot ToSnapshot(string actorId, string cooldownKey)
            {
                return new CombatExecutionCooldownSnapshot(actorId, cooldownKey, DefinitionId, CurrentCharges, MaximumCharges, NextChargeReadyAt, CooldownReadyAt);
            }

            public CombatExecutionCooldownSaveData ToSaveData(string actorId, string cooldownKey)
            {
                return new CombatExecutionCooldownSaveData
                {
                    actorId = actorId,
                    cooldownKey = cooldownKey,
                    definitionId = DefinitionId,
                    currentCharges = CurrentCharges,
                    maximumCharges = MaximumCharges,
                    nextChargeReadyAt = NextChargeReadyAt,
                    cooldownReadyAt = CooldownReadyAt
                };
            }

            public static RuntimeCooldownRecord FromSaveData(CombatExecutionCooldownSaveData saveData)
            {
                return new RuntimeCooldownRecord(saveData.definitionId, saveData.maximumCharges, saveData.currentCharges, saveData.nextChargeReadyAt, saveData.cooldownReadyAt);
            }
        }
    }
}
