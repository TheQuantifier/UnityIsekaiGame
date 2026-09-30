using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.Defense
{
    public sealed class DefensiveActionService : IDefensiveActionService
    {
        public const float DefaultDefenseRoll = 0.5f;
        private const int DefaultProcessedTransactionLimit = 1024;

        private readonly Dictionary<string, ActiveDefenseRecord> activeDefensesByActorId = new Dictionary<string, ActiveDefenseRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, DefenseActivationResult> processedActivations = new Dictionary<string, DefenseActivationResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, DefenseCancellationResult> processedCancellations = new Dictionary<string, DefenseCancellationResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, DefenseResolutionResult> processedResolutions = new Dictionary<string, DefenseResolutionResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActorLifecycleController> lifecycleSubscriptionsByActorId = new Dictionary<string, ActorLifecycleController>(StringComparer.Ordinal);
        private readonly Queue<string> processedActivationOrder = new Queue<string>();
        private readonly Queue<string> processedCancellationOrder = new Queue<string>();
        private readonly Queue<string> processedResolutionOrder = new Queue<string>();
        private readonly int processedTransactionLimit;

        public DefensiveActionService(int processedTransactionLimit = DefaultProcessedTransactionLimit)
        {
            this.processedTransactionLimit = Mathf.Max(16, processedTransactionLimit);
        }

        public event Action<DefenseActivationResult> DefenseActivated;
        public event Action<DefenseCancellationResult> DefenseCancelled;
        public event Action<DefenseResolutionResult> DefenseAttempted;
        public event Action<DefenseResolutionResult> AttackDodged;
        public event Action<DefenseResolutionResult> AttackParried;
        public event Action<DefenseResolutionResult> AttackBlockedByDefense;
        public event Action<DefenseResolutionResult> AttackGuardReduced;
        public event Action<DefenseResolutionResult> DefenseConsumed;

        public DefenseActivationResult PreviewActivate(DefenseActivationRequest request)
        {
            return ResolveActivation(request, execute: false);
        }

        public DefenseActivationResult Activate(DefenseActivationRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedActivations.TryGetValue(request.TransactionId, out DefenseActivationResult processed))
            {
                return processed.AsDuplicate();
            }

            DefenseActivationResult result = ResolveActivation(request, execute: true);
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && result.Succeeded)
            {
                Remember(processedActivations, processedActivationOrder, request.TransactionId, result);
            }

            if (result.Succeeded && !result.Duplicate)
            {
                DefenseActivated?.Invoke(result);
            }

            return result;
        }

        public DefenseCancellationResult PreviewCancel(DefenseCancellationRequest request)
        {
            return ResolveCancellation(request, execute: false);
        }

        public DefenseCancellationResult Cancel(DefenseCancellationRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedCancellations.TryGetValue(request.TransactionId, out DefenseCancellationResult processed))
            {
                return processed.AsDuplicate();
            }

            DefenseCancellationResult result = ResolveCancellation(request, execute: true);
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && result.Succeeded)
            {
                Remember(processedCancellations, processedCancellationOrder, request.TransactionId, result);
            }

            if (result.Succeeded && !result.Duplicate)
            {
                DefenseCancelled?.Invoke(result);
            }

            return result;
        }

        public DefenseResolutionResult PreviewResolve(DefenseResolutionRequest request)
        {
            return ResolveDefense(request, execute: false);
        }

        public DefenseResolutionResult Resolve(DefenseResolutionRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedResolutions.TryGetValue(request.TransactionId, out DefenseResolutionResult processed))
            {
                return processed.AsDuplicate();
            }

            DefenseResolutionResult result = ResolveDefense(request, execute: true);
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && result.Succeeded)
            {
                Remember(processedResolutions, processedResolutionOrder, request.TransactionId, result);
            }

            EmitResolutionEvents(result);
            return result;
        }

        public bool TryGetActiveDefense(string defenderActorId, out DefensiveActionStateSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(defenderActorId) && activeDefensesByActorId.TryGetValue(defenderActorId, out ActiveDefenseRecord record))
            {
                snapshot = record.ToSnapshot();
                return true;
            }

            snapshot = null;
            return false;
        }

        public void ClearTransientStateForRestore(string defenderActorId = "")
        {
            if (string.IsNullOrWhiteSpace(defenderActorId))
            {
                foreach (string actorId in new List<string>(lifecycleSubscriptionsByActorId.Keys))
                {
                    UnsubscribeLifecycle(actorId);
                }

                activeDefensesByActorId.Clear();
                return;
            }

            RemoveActiveDefense(defenderActorId);
        }

        public static string DeriveDefenseTransactionId(string attackTransactionId)
        {
            return string.IsNullOrWhiteSpace(attackTransactionId) ? string.Empty : $"{attackTransactionId}.defense";
        }

        public static float ReadDefenseRoll(IReadOnlyList<KeyValuePair<string, string>> metadata)
        {
            return TryReadFloat(metadata, "defense.roll", out float roll) ? roll : DefaultDefenseRoll;
        }

        public static bool ReadBool(IReadOnlyList<KeyValuePair<string, string>> metadata, string key, bool defaultValue)
        {
            if (metadata == null)
            {
                return defaultValue;
            }

            for (int i = 0; i < metadata.Count; i++)
            {
                if (string.Equals(metadata[i].Key, key, StringComparison.OrdinalIgnoreCase) && bool.TryParse(metadata[i].Value, out bool value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static string ReadString(IReadOnlyList<KeyValuePair<string, string>> metadata, string key, string defaultValue = "")
        {
            if (metadata == null)
            {
                return defaultValue ?? string.Empty;
            }

            for (int i = 0; i < metadata.Count; i++)
            {
                if (string.Equals(metadata[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return metadata[i].Value ?? string.Empty;
                }
            }

            return defaultValue ?? string.Empty;
        }

        public static float ReadDefenseNow(IReadOnlyList<KeyValuePair<string, string>> metadata, float defaultValue)
        {
            return TryReadFloat(metadata, "defense.now", out float now) && IsFinite(now) ? now : defaultValue;
        }

        private DefenseActivationResult ResolveActivation(DefenseActivationRequest request, bool execute)
        {
            if (request.DefenderObject == null)
            {
                return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.MissingActor, "Defender object is missing.");
            }

            if (request.Definition == null)
            {
                return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.MissingDefinition, "Defensive action definition is missing.");
            }

            if (request.Definition.ActionType == DefensiveActionType.None)
            {
                return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.InvalidRequest, "Defensive action type is None.");
            }

            if (!TryResolveActorId(request.DefenderObject, request.DefenderActorId, out string actorId, out string actorMessage))
            {
                return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.MissingActor, actorMessage);
            }

            if (!ActorLifecycleUtility.CanAct(request.DefenderObject))
            {
                return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.ActorCannotAct, $"Actor '{actorId}' cannot activate defenses while {ActorLifecycleUtility.GetState(request.DefenderObject)}.");
            }

            if (!TryResolveEquippedRequirement(request.DefenderObject, request.Definition, request.SourceEquipmentId, out EquippedDefenseSource equipmentSource, out string equipmentCode, out string equipmentMessage))
            {
                return DefenseActivationResult.Failure(request, !execute, equipmentCode, equipmentMessage);
            }

            ResourceChangeResult staminaResult = null;
            if (request.Definition.ActivationStaminaCost > CharacterResourceCollection.Epsilon)
            {
                CharacterResourceCollection resources = request.DefenderObject.GetComponentInParent<CharacterResourceCollection>();
                if (resources == null || !resources.HasResource(ResourceIds.Stamina))
                {
                    return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.MissingResource, "Defender Stamina resource is not configured.");
                }

                if (!resources.CanSpend(ResourceIds.Stamina, request.Definition.ActivationStaminaCost))
                {
                    return DefenseActivationResult.Failure(request, !execute, DefensiveActionResultCode.InsufficientStamina, "Not enough Stamina to activate defense.");
                }

                if (execute)
                {
                    staminaResult = resources.ApplyChange(new ResourceChangeRequest(
                        ResourceIds.Stamina,
                        ResourceChangeOperation.Spend,
                        request.Definition.ActivationStaminaCost,
                        ResourceChangeSourceCategory.Combat,
                        request.Definition.Id,
                        $"Activate {request.Definition.DisplayName}.",
                        DeriveResourceTransactionId(request.TransactionId, "activate"),
                        allowPartial: false,
                        authorityValidated: request.AuthorityValidated));
                    if (!staminaResult.Succeeded)
                    {
                        return DefenseActivationResult.Failure(request, false, staminaResult.Code, staminaResult.Message, null, staminaResult);
                    }
                }
            }

            string stateId = string.IsNullOrWhiteSpace(request.TransactionId)
                ? $"defense-state.{actorId}.{Guid.NewGuid():N}"
                : $"{request.TransactionId}.state";
            float expiresAt = request.Definition.IsTimedWindow ? request.Now + request.Definition.TimingWindowSeconds : 0f;
            DefensiveActionStateSnapshot snapshot = new DefensiveActionStateSnapshot(stateId, actorId, request.Definition, request.Definition.RuntimeState, request.Now, expiresAt, equipmentSource.ItemDefinitionId, equipmentSource.ItemInstanceId, request.DefenderObject.GetEntityId().ToString(), request.SourceActionId);

            if (execute)
            {
                RemoveActiveDefense(actorId);
                activeDefensesByActorId[actorId] = new ActiveDefenseRecord(snapshot);
                SubscribeLifecycle(actorId, request.DefenderObject.GetComponentInParent<ActorLifecycleController>());
            }

            return DefenseActivationResult.Success(request, !execute, false, execute ? $"Activated {request.Definition.DisplayName}." : $"Previewed {request.Definition.DisplayName} activation.", snapshot, staminaResult);
        }

        private DefenseCancellationResult ResolveCancellation(DefenseCancellationRequest request, bool execute)
        {
            if (request.DefenderObject == null && string.IsNullOrWhiteSpace(request.DefenderActorId))
            {
                return DefenseCancellationResult.Failure(request, !execute, DefensiveActionResultCode.MissingActor, "Defender object or actor ID is required.");
            }

            string actorId = request.DefenderActorId;
            if (string.IsNullOrWhiteSpace(actorId) && !TryResolveActorId(request.DefenderObject, string.Empty, out actorId, out string actorMessage))
            {
                return DefenseCancellationResult.Failure(request, !execute, DefensiveActionResultCode.MissingActor, actorMessage);
            }

            if (!activeDefensesByActorId.TryGetValue(actorId, out ActiveDefenseRecord record))
            {
                return DefenseCancellationResult.Failure(request, !execute, DefensiveActionResultCode.NoActiveDefense, $"Actor '{actorId}' has no active defense.");
            }

            DefensiveActionStateSnapshot snapshot = record.ToSnapshot();
            if (!string.IsNullOrWhiteSpace(request.ExpectedStateId) && !string.Equals(request.ExpectedStateId, snapshot.StateId, StringComparison.Ordinal))
            {
                return DefenseCancellationResult.Failure(request, !execute, DefensiveActionResultCode.Ineligible, $"Expected defense state '{request.ExpectedStateId}' but found '{snapshot.StateId}'.", snapshot);
            }

            if (!string.IsNullOrWhiteSpace(request.ExpectedDefinitionId) && !string.Equals(request.ExpectedDefinitionId, snapshot.DefinitionId, StringComparison.Ordinal))
            {
                return DefenseCancellationResult.Failure(request, !execute, DefensiveActionResultCode.Ineligible, $"Expected defense definition '{request.ExpectedDefinitionId}' but found '{snapshot.DefinitionId}'.", snapshot);
            }

            if (execute)
            {
                RemoveActiveDefense(actorId);
            }

            return DefenseCancellationResult.Success(request, !execute, false, execute ? $"Cancelled {snapshot.DefinitionId}." : $"Previewed cancellation of {snapshot.DefinitionId}.", snapshot);
        }

        private DefenseResolutionResult ResolveDefense(DefenseResolutionRequest request, bool execute)
        {
            if (request.DefenderObject == null)
            {
                return DefenseResolutionResult.Failure(!execute, DefensiveActionResultCode.MissingActor, "Defender object is missing.", request);
            }

            if (!IsFinite(request.IncomingDamage) || request.IncomingDamage < 0f)
            {
                return DefenseResolutionResult.Failure(!execute, DefensiveActionResultCode.InvalidRequest, "Incoming damage must be finite and non-negative.", request);
            }

            if (!IsValidRoll(request.Roll))
            {
                return DefenseResolutionResult.Failure(!execute, DefensiveActionResultCode.InvalidRoll, "Defense roll must be finite and within [0, 1).", request);
            }

            if (!TryResolveActorId(request.DefenderObject, request.DefenderActorId, out string actorId, out string actorMessage))
            {
                return DefenseResolutionResult.Failure(!execute, DefensiveActionResultCode.MissingActor, actorMessage, request);
            }

            if (!activeDefensesByActorId.TryGetValue(actorId, out ActiveDefenseRecord record))
            {
                return DefenseResolutionResult.Create(!execute, false, DefenseResolutionOutcome.NoDefense, execute ? DefensiveActionResultCode.Success : DefensiveActionResultCode.Preview, "No active defense.", request, null, 0f, false, false, 0f, request.IncomingDamage, false);
            }

            DefensiveActionStateSnapshot snapshot = record.ToSnapshot();
            if (!string.Equals(snapshot.DefenderBodyId, request.DefenderObject.GetEntityId().ToString(), StringComparison.Ordinal))
            {
                if (execute)
                {
                    RemoveActiveDefense(actorId);
                }

                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Ineligible, DefensiveActionResultCode.StaleBody, $"Active defense '{snapshot.StateId}' belongs to a different actor body.", request, snapshot);
            }

            if (!string.IsNullOrWhiteSpace(request.ExpectedStateId) && !string.Equals(request.ExpectedStateId, snapshot.StateId, StringComparison.Ordinal))
            {
                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Ineligible, DefensiveActionResultCode.Ineligible, $"Expected defense state '{request.ExpectedStateId}' but found '{snapshot.StateId}'.", request, snapshot);
            }

            if (snapshot.IsExpired(request.Now))
            {
                if (execute)
                {
                    RemoveActiveDefense(actorId);
                }

                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Expired, DefensiveActionResultCode.Expired, $"Defense '{snapshot.DefinitionId}' expired.", request, snapshot, consumed: execute);
            }

            if (!ActorLifecycleUtility.CanAct(request.DefenderObject))
            {
                if (execute)
                {
                    RemoveActiveDefense(actorId);
                }

                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Ineligible, DefensiveActionResultCode.ActorCannotAct, $"Actor '{actorId}' cannot use defenses while {ActorLifecycleUtility.GetState(request.DefenderObject)}.", request, snapshot, consumed: execute);
            }

            DefensiveActionDefinition definition = snapshot.Definition;
            if (definition == null || definition.ActionType == DefensiveActionType.None)
            {
                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Ineligible, DefensiveActionResultCode.MissingDefinition, "Active defense definition is missing.", request, snapshot);
            }

            if (!TryRevalidateEquippedRequirement(request.DefenderObject, snapshot, out string equipmentCode, out string equipmentMessage))
            {
                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Ineligible, equipmentCode, equipmentMessage, request, snapshot);
            }

            if (!IsEligibleForAttack(definition, request, out string eligibilityReason))
            {
                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.Ineligible, DefensiveActionResultCode.Ineligible, eligibilityReason, request, snapshot);
            }

            bool validAttempt = true;
            float finalChance = CalculateFinalChance(definition, request.DefenderObject);
            bool chanceSucceeded = definition.ActionType == DefensiveActionType.Guard || request.Roll < finalChance;
            DefenseResolutionOutcome failedOutcome = definition.ActionType switch
            {
                DefensiveActionType.Dodge => DefenseResolutionOutcome.DodgeFailed,
                DefensiveActionType.Parry => DefenseResolutionOutcome.ParryFailed,
                DefensiveActionType.Block => DefenseResolutionOutcome.BlockFailed,
                _ => DefenseResolutionOutcome.NoDefense
            };
            bool shouldConsume = definition.ConsumedAfterAttempt && validAttempt;

            if (!TrySpendResolutionStamina(definition, request, execute, chanceSucceeded, out ResourceChangeResult staminaResult, out string staminaFailure))
            {
                if (execute && shouldConsume)
                {
                    RemoveActiveDefense(actorId);
                }

                return DefenseResolutionResult.Rejected(!execute, DefenseResolutionOutcome.InsufficientStamina, DefensiveActionResultCode.InsufficientStamina, staminaFailure, request, snapshot, finalChance, attempted: true, consumed: execute && shouldConsume, staminaResult);
            }

            float prevented = 0f;
            DefenseResolutionOutcome outcome = failedOutcome;
            bool defenseSucceeded = false;
            if (chanceSucceeded)
            {
                prevented = CalculatePreventedDamage(definition, request.IncomingDamage);
                defenseSucceeded = prevented > CharacterResourceCollection.Epsilon || definition.ActionType == DefensiveActionType.Dodge || definition.ActionType == DefensiveActionType.Parry;
                outcome = definition.ActionType switch
                {
                    DefensiveActionType.Dodge => DefenseResolutionOutcome.DodgeSucceeded,
                    DefensiveActionType.Parry => DefenseResolutionOutcome.ParrySucceeded,
                    DefensiveActionType.Block => prevented >= request.IncomingDamage - CharacterResourceCollection.Epsilon ? DefenseResolutionOutcome.Prevented : DefenseResolutionOutcome.BlockSucceeded,
                    DefensiveActionType.Guard => prevented >= request.IncomingDamage - CharacterResourceCollection.Epsilon ? DefenseResolutionOutcome.Prevented : DefenseResolutionOutcome.GuardReduced,
                    _ => DefenseResolutionOutcome.None
                };
            }

            float remaining = Mathf.Max(0f, request.IncomingDamage - prevented);
            if (execute && shouldConsume)
            {
                RemoveActiveDefense(actorId);
            }

            return DefenseResolutionResult.Create(!execute, false, outcome, execute ? DefensiveActionResultCode.Success : DefensiveActionResultCode.Preview, BuildResolutionMessage(definition, outcome, prevented, remaining), request, snapshot, finalChance, true, defenseSucceeded, prevented, remaining, execute && shouldConsume, staminaResult);
        }

        private bool TrySpendResolutionStamina(DefensiveActionDefinition definition, DefenseResolutionRequest request, bool execute, bool chanceSucceeded, out ResourceChangeResult result, out string failureReason)
        {
            result = null;
            failureReason = string.Empty;
            float cost = chanceSucceeded ? definition.SuccessStaminaCost : definition.FailureStaminaCost;
            if (cost <= CharacterResourceCollection.Epsilon)
            {
                return true;
            }

            CharacterResourceCollection resources = request.DefenderObject.GetComponentInParent<CharacterResourceCollection>();
            if (resources == null || !resources.HasResource(ResourceIds.Stamina))
            {
                failureReason = "Defender Stamina resource is not configured.";
                return false;
            }

            if (!resources.CanSpend(ResourceIds.Stamina, cost))
            {
                failureReason = $"Not enough Stamina to resolve {definition.DisplayName}.";
                return false;
            }

            if (!execute)
            {
                return true;
            }

            result = resources.ApplyChange(new ResourceChangeRequest(
                ResourceIds.Stamina,
                ResourceChangeOperation.Spend,
                cost,
                ResourceChangeSourceCategory.Combat,
                definition.Id,
                $"Resolve {definition.DisplayName}.",
                DeriveResourceTransactionId(request.TransactionId, chanceSucceeded ? "success" : "failure"),
                allowPartial: false,
                authorityValidated: request.AuthorityValidated));
            if (result.Succeeded)
            {
                return true;
            }

            failureReason = result.Message;
            return false;
        }

        private static bool IsEligibleForAttack(DefensiveActionDefinition definition, DefenseResolutionRequest request, out string reason)
        {
            reason = string.Empty;
            if (request.TrueDamage && (!request.AllowTrueDamageActiveDefense || !definition.AllowsTrueDamageDefense))
            {
                reason = $"Defensive action '{definition.Id}' cannot affect true damage.";
                return false;
            }

            if (request.Critical && !definition.AllowsCriticalDefense)
            {
                reason = $"Defensive action '{definition.Id}' cannot affect critical attacks.";
                return false;
            }

            if (!definition.AppliesToDamageType(request.DamageType))
            {
                reason = $"Defensive action '{definition.Id}' does not apply to damage type '{(request.DamageType == null ? string.Empty : request.DamageType.Id)}'.";
                return false;
            }

            if (definition.ActionType == DefensiveActionType.Dodge && !request.Dodgeable)
            {
                reason = "Attack is not dodgeable.";
                return false;
            }

            if (definition.ActionType == DefensiveActionType.Parry && !request.Parryable)
            {
                reason = "Attack is not parryable.";
                return false;
            }

            if ((definition.ActionType == DefensiveActionType.Block || definition.ActionType == DefensiveActionType.Guard) && !request.Blockable)
            {
                reason = "Attack is not blockable.";
                return false;
            }

            return true;
        }

        private static float CalculatePreventedDamage(DefensiveActionDefinition definition, float incomingDamage)
        {
            switch (definition.ActionType)
            {
                case DefensiveActionType.Dodge:
                case DefensiveActionType.Parry:
                    return incomingDamage;
                case DefensiveActionType.Block:
                case DefensiveActionType.Guard:
                    return definition.ReductionMode switch
                    {
                        DefensiveDamageReductionMode.FlatReduction => Mathf.Min(incomingDamage, definition.FlatReduction),
                        DefensiveDamageReductionMode.PercentageReduction => Mathf.Clamp01(definition.PercentageReduction) * incomingDamage,
                        DefensiveDamageReductionMode.FullPrevention => incomingDamage <= definition.FullPreventionThreshold + CharacterResourceCollection.Epsilon ? incomingDamage : 0f,
                        _ => 0f
                    };
                default:
                    return 0f;
            }
        }

        private static float CalculateFinalChance(DefensiveActionDefinition definition, GameObject defenderObject)
        {
            float chance = definition.BaseChance;
            if (!string.IsNullOrWhiteSpace(definition.ContributingStatId))
            {
                CalculatedStatCollection stats = defenderObject == null ? null : defenderObject.GetComponentInParent<CalculatedStatCollection>();
                if (stats != null && stats.HasStat(definition.ContributingStatId))
                {
                    chance += (stats.GetValue(definition.ContributingStatId) / AttackResolutionService.WholeNumberStatScaleDivisor) * definition.StatContributionScale;
                }
            }

            if (!string.IsNullOrWhiteSpace(definition.ContributingSkillId))
            {
                CharacterSkillCollection skills = defenderObject == null ? null : defenderObject.GetComponentInParent<CharacterSkillCollection>();
                if (skills != null)
                {
                    SkillGrade grade = skills.GetGrade(definition.ContributingSkillId);
                    chance += (SkillGradeUtility.ToIndex(grade) / (float)SkillGradeUtility.MaximumIndex) * definition.SkillContributionScale;
                }
            }

            return Mathf.Clamp(chance, definition.MinimumChance, definition.MaximumChance);
        }

        private static bool TryResolveEquippedRequirement(GameObject defenderObject, DefensiveActionDefinition definition, string requestedEquipmentId, out EquippedDefenseSource source, out string code, out string message)
        {
            source = default;
            code = string.Empty;
            message = string.Empty;
            bool requiresEquipment = definition.RequiresEquipmentSource
                || !string.IsNullOrWhiteSpace(definition.RequiredEquipmentCategoryId)
                || !string.IsNullOrWhiteSpace(definition.RequiredEquipmentTagId);
            if (!requiresEquipment)
            {
                return true;
            }

            PlayerEquipment equipment = defenderObject == null ? null : defenderObject.GetComponentInParent<PlayerEquipment>();
            if (equipment == null)
            {
                code = DefensiveActionResultCode.MissingEquipment;
                message = $"Defensive action '{definition.Id}' requires equipped compatible gear.";
                return false;
            }

            IReadOnlyList<EquipmentSlotState> slots = equipment.Slots;
            bool sawCandidate = false;
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotState slot = slots[i];
                if (slot == null || slot.IsEmpty)
                {
                    continue;
                }

                ItemDefinition item = slot.Item;
                sawCandidate = true;
                string itemId = item == null ? string.Empty : item.Id;
                string instanceId = slot.ItemInstanceId;
                if (!string.IsNullOrWhiteSpace(requestedEquipmentId)
                    && !string.Equals(requestedEquipmentId, itemId, StringComparison.Ordinal)
                    && !string.Equals(requestedEquipmentId, instanceId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsCompatibleEquipment(item, definition))
                {
                    source = new EquippedDefenseSource(itemId, instanceId, slot.SlotType);
                    return true;
                }
            }

            code = string.IsNullOrWhiteSpace(requestedEquipmentId) && !sawCandidate ? DefensiveActionResultCode.MissingEquipment : DefensiveActionResultCode.IncompatibleEquipment;
            message = string.IsNullOrWhiteSpace(requestedEquipmentId)
                ? $"No equipped item satisfies defensive action '{definition.Id}'."
                : $"Equipped item '{requestedEquipmentId}' does not satisfy defensive action '{definition.Id}'.";
            return false;
        }

        private static bool TryRevalidateEquippedRequirement(GameObject defenderObject, DefensiveActionStateSnapshot snapshot, out string code, out string message)
        {
            code = string.Empty;
            message = string.Empty;
            DefensiveActionDefinition definition = snapshot.Definition;
            bool requiresEquipment = definition.RequiresEquipmentSource
                || !string.IsNullOrWhiteSpace(definition.RequiredEquipmentCategoryId)
                || !string.IsNullOrWhiteSpace(definition.RequiredEquipmentTagId);
            if (!requiresEquipment)
            {
                return true;
            }

            PlayerEquipment equipment = defenderObject == null ? null : defenderObject.GetComponentInParent<PlayerEquipment>();
            if (equipment == null)
            {
                code = DefensiveActionResultCode.MissingEquipment;
                message = $"Defensive action '{definition.Id}' requires equipped compatible gear.";
                return false;
            }

            IReadOnlyList<EquipmentSlotState> slots = equipment.Slots;
            bool sawStoredItem = false;
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotState slot = slots[i];
                if (slot == null || slot.IsEmpty)
                {
                    continue;
                }

                ItemDefinition item = slot.Item;
                string itemId = item == null ? string.Empty : item.Id;
                string instanceId = slot.ItemInstanceId;
                bool sameDefinition = string.Equals(itemId, snapshot.SourceEquipmentId, StringComparison.Ordinal);
                bool sameInstance = string.IsNullOrWhiteSpace(snapshot.SourceEquipmentInstanceId)
                    ? string.IsNullOrWhiteSpace(instanceId) && sameDefinition
                    : string.Equals(instanceId, snapshot.SourceEquipmentInstanceId, StringComparison.Ordinal);
                if (!sameDefinition || !sameInstance)
                {
                    continue;
                }

                sawStoredItem = true;
                if (!IsCompatibleEquipment(item, definition))
                {
                    code = DefensiveActionResultCode.IncompatibleEquipment;
                    message = $"Equipped item '{itemId}' is no longer compatible with defensive action '{definition.Id}'.";
                    return false;
                }

                return true;
            }

            code = sawStoredItem ? DefensiveActionResultCode.IncompatibleEquipment : DefensiveActionResultCode.UnequippedEquipment;
            message = $"Required equipment '{snapshot.SourceEquipmentId}' is no longer equipped for defensive action '{definition.Id}'.";
            return false;
        }

        private static bool IsCompatibleEquipment(ItemDefinition item, DefensiveActionDefinition definition)
        {
            if (item == null || !item.IsEquippable)
            {
                return false;
            }

            if (definition.ActionType == DefensiveActionType.Parry && item.Equipment?.MeleeWeapon?.IsWeapon != true)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredEquipmentCategoryId)
                && !string.Equals(item.PrimaryCategory?.Id, definition.RequiredEquipmentCategoryId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredEquipmentTagId) && !HasTag(item, definition.RequiredEquipmentTagId))
            {
                return false;
            }

            return true;
        }

        private static bool HasTag(ItemDefinition item, string tagId)
        {
            IReadOnlyList<UnityIsekaiGame.GameData.TagDefinition> tags = item.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null && string.Equals(tags[i].Id, tagId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveActorId(GameObject actorObject, string expectedActorId, out string actorId, out string message)
        {
            actorId = string.Empty;
            message = string.Empty;
            if (actorObject == null)
            {
                message = "Actor object is missing.";
                return false;
            }

            CharacterSystemCoordinator character = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                actorId = character.ActorId;
            }
            else
            {
                WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
                actorId = identity == null ? string.Empty : identity.EntityId;
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                message = "Actor identity is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedActorId) && !string.Equals(expectedActorId, actorId, StringComparison.Ordinal))
            {
                message = $"Actor identity '{expectedActorId}' no longer resolves to '{actorId}'.";
                return false;
            }

            return true;
        }

        private void EmitResolutionEvents(DefenseResolutionResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            if (result.Attempted)
            {
                DefenseAttempted?.Invoke(result);
            }

            if (result.Outcome == DefenseResolutionOutcome.DodgeSucceeded)
            {
                AttackDodged?.Invoke(result);
            }
            else if (result.Outcome == DefenseResolutionOutcome.ParrySucceeded)
            {
                AttackParried?.Invoke(result);
            }
            else if (result.Outcome == DefenseResolutionOutcome.BlockSucceeded || result.Outcome == DefenseResolutionOutcome.Prevented)
            {
                AttackBlockedByDefense?.Invoke(result);
            }
            else if (result.Outcome == DefenseResolutionOutcome.GuardReduced)
            {
                AttackGuardReduced?.Invoke(result);
            }

            if (result.Consumed)
            {
                DefenseConsumed?.Invoke(result);
            }
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

            RemoveActiveDefense(result.TargetActorId);
        }

        private void RemoveActiveDefense(string actorId)
        {
            activeDefensesByActorId.Remove(actorId);
            UnsubscribeLifecycle(actorId);
        }

        private void Remember<T>(Dictionary<string, T> results, Queue<string> order, string transactionId, T result)
        {
            if (results.ContainsKey(transactionId))
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

        private static string DeriveResourceTransactionId(string transactionId, string suffix)
        {
            return string.IsNullOrWhiteSpace(transactionId) ? string.Empty : $"{transactionId}.stamina.{suffix}";
        }

        private static string BuildResolutionMessage(DefensiveActionDefinition definition, DefenseResolutionOutcome outcome, float prevented, float remaining)
        {
            return $"{definition.DisplayName} {outcome}: prevented {prevented:0.###}, remaining {remaining:0.###}.";
        }

        private static bool TryReadFloat(IReadOnlyList<KeyValuePair<string, string>> metadata, string key, out float value)
        {
            value = 0f;
            if (metadata == null)
            {
                return false;
            }

            for (int i = 0; i < metadata.Count; i++)
            {
                if (string.Equals(metadata[i].Key, key, StringComparison.OrdinalIgnoreCase) && float.TryParse(metadata[i].Value, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidRoll(float value)
        {
            return IsFinite(value) && value >= 0f && value < 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct ActiveDefenseRecord
        {
            private readonly DefensiveActionStateSnapshot snapshot;

            public ActiveDefenseRecord(DefensiveActionStateSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public DefensiveActionStateSnapshot ToSnapshot()
            {
                return snapshot;
            }
        }

        private readonly struct EquippedDefenseSource
        {
            public EquippedDefenseSource(string itemDefinitionId, string itemInstanceId, EquipmentSlotType slotType)
            {
                ItemDefinitionId = itemDefinitionId ?? string.Empty;
                ItemInstanceId = itemInstanceId ?? string.Empty;
                SlotType = slotType;
            }

            public string ItemDefinitionId { get; }
            public string ItemInstanceId { get; }
            public EquipmentSlotType SlotType { get; }
        }
    }
}
