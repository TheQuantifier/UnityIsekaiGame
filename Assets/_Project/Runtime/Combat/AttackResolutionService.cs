using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat
{
    public sealed class AttackResolutionService : IAttackResolutionService
    {
        public const float DefaultMinimumHitChance = 0.05f;
        public const float DefaultMaximumHitChance = 0.95f;
        public const float WholeNumberStatScaleDivisor = 100f;

        private const int DefaultProcessedAttackLimit = 1024;
        private readonly IDamageHealingService damageHealingService;
        private readonly IDefensiveActionService defensiveActionService;
        private readonly CombatStateService combatStateService;
        private readonly Dictionary<string, AttackResolutionResult> processedAttackResults = new Dictionary<string, AttackResolutionResult>(StringComparer.Ordinal);
        private readonly Queue<string> processedAttackOrder = new Queue<string>();
        private readonly int processedAttackLimit;

        public event Action<AttackResolutionResult> AttackProcessed;
        public event Action<AttackResolutionResult> AttackBlocked;
        public event Action<AttackResolutionResult> AttackMissed;
        public event Action<AttackResolutionResult> AttackHit;
        public event Action<AttackResolutionResult> CriticalHit;
        public event Action<AttackResolutionResult> AttackDamagePrevented;
        public event Action<AttackResolutionResult> AttackDamageApplied;

        public AttackResolutionService()
            : this(new DamageHealingService(), null, DefaultProcessedAttackLimit)
        {
        }

        public AttackResolutionService(IDamageHealingService damageHealingService, int processedAttackLimit = DefaultProcessedAttackLimit)
            : this(damageHealingService, null, processedAttackLimit)
        {
        }

        public AttackResolutionService(IDamageHealingService damageHealingService, IDefensiveActionService defensiveActionService, int processedAttackLimit = DefaultProcessedAttackLimit)
            : this(damageHealingService, defensiveActionService, null, processedAttackLimit)
        {
        }

        public AttackResolutionService(IDamageHealingService damageHealingService, IDefensiveActionService defensiveActionService, CombatStateService combatStateService, int processedAttackLimit = DefaultProcessedAttackLimit)
        {
            this.damageHealingService = damageHealingService ?? new DamageHealingService();
            this.defensiveActionService = defensiveActionService;
            this.combatStateService = combatStateService;
            this.processedAttackLimit = Mathf.Max(16, processedAttackLimit);
        }

        public AttackResolutionResult PreviewAttack(AttackResolutionRequest request)
        {
            return Resolve(request, execute: false);
        }

        public AttackResolutionResult ExecuteAttack(AttackResolutionRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && processedAttackResults.TryGetValue(request.TransactionId, out AttackResolutionResult processed))
            {
                return processed.AsDuplicate();
            }

            AttackResolutionResult result = Resolve(request, execute: true);
            if (!string.IsNullOrWhiteSpace(request.TransactionId) && result.Processed)
            {
                RememberProcessedAttack(request.TransactionId, result);
            }

            RecordCombatActivity(result);
            EmitExecutionEvents(result);
            return result;
        }

        public static string DeriveDamageTransactionId(string attackTransactionId)
        {
            return string.IsNullOrWhiteSpace(attackTransactionId)
                ? string.Empty
                : $"{attackTransactionId}.damage";
        }

        private AttackResolutionResult Resolve(AttackResolutionRequest request, bool execute)
        {
            if (!ValidateRequestShape(request, out AttackOutcome shapeOutcome, out string shapeCode, out string shapeMessage))
            {
                return CreateTerminalResult(request, execute, shapeOutcome, shapeCode, shapeMessage);
            }

            if (execute && !request.AuthorityValidated)
            {
                return CreateTerminalResult(request, execute, AttackOutcome.Invalid, AttackResolutionResultCode.AuthorityRequired, "Executing an attack requires validated game/server authority.");
            }

            if (!TryResolveActor(request.TargetObject, request.TargetActorId, resolvingAttacker: false, requireReady: true, out ActorRuntime target, out string targetCode, out string targetMessage))
            {
                return CreateTerminalResult(request, execute, AttackOutcome.Invalid, targetCode, targetMessage);
            }

            ActorRuntime attacker = default;
            if (request.RequiresAttacker)
            {
                if (!TryResolveActor(request.AttackerObject, request.AttackerActorId, resolvingAttacker: true, requireReady: true, out attacker, out string attackerCode, out string attackerMessage))
                {
                    AttackOutcome outcome = attackerCode == AttackResolutionResultCode.AttackerNotReady ? AttackOutcome.Blocked : AttackOutcome.Invalid;
                    return CreateTerminalResult(request, execute, outcome, attackerCode, attackerMessage, target.ActorId);
                }
            }
            else if (request.AttackerObject != null)
            {
                TryResolveActor(request.AttackerObject, request.AttackerActorId, resolvingAttacker: true, requireReady: false, out attacker, out _, out _);
            }

            if (!TryValidateContext(request, out string contextCode, out string contextMessage))
            {
                return CreateTerminalResult(request, execute, AttackOutcome.Blocked, contextCode, contextMessage, target.ActorId, attacker.ActorId);
            }

            RequirementSummary requirement = EvaluateRequirements(request, attacker);
            if (!requirement.Passed)
            {
                return CreateTerminalResult(request, execute, AttackOutcome.Blocked, AttackResolutionResultCode.RequirementFailed, requirement.Summary, target.ActorId, attacker.ActorId, requirement);
            }

            float accuracy = 0f;
            if (request.RequiresAttacker && !TryReadStat(attacker.Stats, CalculatedStatIds.Accuracy, out accuracy))
            {
                return CreateTerminalResult(request, execute, AttackOutcome.Invalid, AttackResolutionResultCode.MissingAccuracy, "Attacker Accuracy stat is missing.", target.ActorId, attacker.ActorId, requirement);
            }

            if (!TryReadStat(target.Stats, CalculatedStatIds.Evasion, out float evasion))
            {
                return CreateTerminalResult(request, execute, AttackOutcome.Invalid, AttackResolutionResultCode.MissingEvasion, "Target Evasion stat is missing.", target.ActorId, attacker.ActorId, requirement);
            }

            float normalizedAccuracy = request.RequiresAttacker ? accuracy / WholeNumberStatScaleDivisor : 0f;
            float normalizedEvasion = evasion / WholeNumberStatScaleDivisor;
            float unclampedHitChance = request.BaseHitChance + normalizedAccuracy - normalizedEvasion;
            float finalHitChance = Mathf.Clamp(unclampedHitChance, DefaultMinimumHitChance, DefaultMaximumHitChance);
            bool hit = request.HitRoll < finalHitChance;
            if (!hit)
            {
                return AttackResolutionResult.Create(
                    preview: !execute,
                    processed: execute,
                    duplicate: false,
                    outcome: AttackOutcome.Miss,
                    code: execute ? AttackResolutionResultCode.Processed : AttackResolutionResultCode.Preview,
                    message: $"Attack missed. Roll {request.HitRoll:0.###} >= hit chance {finalHitChance:0.###}.",
                    request,
                    attacker.ActorId,
                    target.ActorId,
                    string.Empty,
                    accuracy,
                    evasion,
                    normalizedAccuracy,
                    normalizedEvasion,
                    unclampedHitChance,
                    finalHitChance,
                    hit: false,
                    critical: false,
                    damageAfterCritical: 0f,
                    requirement.Passed,
                    requirement.Summary,
                    requirement.FailureReasons,
                    damageResult: null);
            }

            bool critical = request.CriticalChance > 0f && request.CriticalRoll < Mathf.Clamp01(request.CriticalChance);
            float damageAmount = critical ? request.BaseDamage * request.CriticalMultiplier : request.BaseDamage;
            DefenseResolutionResult defenseResult = ResolveActiveDefense(request, attacker.ActorId, target.ActorId, damageAmount, critical, execute);
            if (defenseResult != null && !defenseResult.Succeeded && IsTerminalDefenseFailure(defenseResult.Code))
            {
                return AttackResolutionResult.Create(
                    preview: !execute,
                    processed: execute,
                    duplicate: false,
                    AttackOutcome.Blocked,
                    defenseResult.Code,
                    defenseResult.Message,
                    request,
                    attacker.ActorId,
                    target.ActorId,
                    string.Empty,
                    accuracy,
                    evasion,
                    normalizedAccuracy,
                    normalizedEvasion,
                    unclampedHitChance,
                    finalHitChance,
                    hit: true,
                    critical,
                    damageAmount,
                    requirement.Passed,
                    requirement.Summary,
                    requirement.FailureReasons,
                    damageResult: null,
                    defenseResult);
            }

            float damageAfterDefense = defenseResult == null ? damageAmount : defenseResult.RemainingDamage;
            if (defenseResult != null && defenseResult.DamageFullyPrevented)
            {
                AttackOutcome preventedOutcome = critical ? AttackOutcome.CriticalHit : AttackOutcome.Hit;
                return AttackResolutionResult.Create(
                    preview: !execute,
                    processed: execute,
                    duplicate: false,
                    preventedOutcome,
                    execute ? AttackResolutionResultCode.Processed : AttackResolutionResultCode.Preview,
                    $"{preventedOutcome}: active defense prevented all damage.",
                    request,
                    attacker.ActorId,
                    target.ActorId,
                    string.Empty,
                    accuracy,
                    evasion,
                    normalizedAccuracy,
                    normalizedEvasion,
                    unclampedHitChance,
                    finalHitChance,
                    hit: true,
                    critical,
                    damageAmount,
                    requirement.Passed,
                    requirement.Summary,
                    requirement.FailureReasons,
                    damageResult: null,
                    defenseResult);
            }

            string damageTransactionId = DeriveDamageTransactionId(request.TransactionId);
            DamageApplicationRequest damageRequest = new DamageApplicationRequest(
                damageTransactionId,
                attacker.ActorId,
                request.AttackerObject,
                target.ActorId,
                request.TargetObject,
                request.DamageType,
                damageAfterDefense,
                BuildDamageReason(request, critical),
                request.AuthorityValidated);
            DamageApplicationResult damageResult = execute
                ? damageHealingService.ApplyDamage(damageRequest)
                : damageHealingService.PreviewDamage(damageRequest);
            AttackOutcome logicalOutcome = critical ? AttackOutcome.CriticalHit : AttackOutcome.Hit;
            string code = damageResult.Succeeded
                ? execute ? AttackResolutionResultCode.Processed : AttackResolutionResultCode.Preview
                : AttackResolutionResultCode.DamageFailed;
            string message = damageResult.Succeeded
                ? $"{logicalOutcome}: final attack damage {damageAmount:0.###}; active defense {damageAfterDefense:0.###}; damage pipeline {damageResult.Code}."
                : $"Attack hit, but damage pipeline failed: {damageResult.Message}";

            return AttackResolutionResult.Create(
                preview: !execute,
                processed: execute,
                duplicate: false,
                logicalOutcome,
                code,
                message,
                request,
                attacker.ActorId,
                target.ActorId,
                damageTransactionId,
                accuracy,
                evasion,
                normalizedAccuracy,
                normalizedEvasion,
                unclampedHitChance,
                finalHitChance,
                hit: true,
                critical,
                damageAmount,
                requirement.Passed,
                requirement.Summary,
                requirement.FailureReasons,
                damageResult,
                defenseResult);
        }

        private DefenseResolutionResult ResolveActiveDefense(AttackResolutionRequest request, string attackerActorId, string targetActorId, float damageAmount, bool critical, bool execute)
        {
            if (defensiveActionService == null)
            {
                return null;
            }

            string defenseTransactionId = DefensiveActionService.ReadString(request.Metadata, "defense.transaction-id", DefensiveActionService.DeriveDefenseTransactionId(request.TransactionId));
            DefenseResolutionRequest defenseRequest = new DefenseResolutionRequest(
                defenseTransactionId,
                request.TransactionId,
                attackerActorId,
                request.AttackerObject,
                targetActorId,
                request.TargetObject,
                request.DamageType,
                request.SourceType,
                damageAmount,
                DefensiveActionService.ReadDefenseRoll(request.Metadata),
                critical,
                DefensiveActionService.ReadBool(request.Metadata, "defense.blockable", true),
                DefensiveActionService.ReadBool(request.Metadata, "defense.parryable", true),
                DefensiveActionService.ReadBool(request.Metadata, "defense.dodgeable", true),
                request.DamageType != null && request.DamageType.IsTrueDamage,
                DefensiveActionService.ReadBool(request.Metadata, "defense.allow-true-active", true),
                DefensiveActionService.ReadString(request.Metadata, "defense.expected-state-id"),
                DefensiveActionService.ReadDefenseNow(request.Metadata, Time.time),
                request.AuthorityValidated);
            return execute ? defensiveActionService.Resolve(defenseRequest) : defensiveActionService.PreviewResolve(defenseRequest);
        }

        private static bool IsTerminalDefenseFailure(string code)
        {
            return string.Equals(code, DefensiveActionResultCode.InvalidRoll, StringComparison.Ordinal)
                || string.Equals(code, DefensiveActionResultCode.InvalidRequest, StringComparison.Ordinal)
                || string.Equals(code, DefensiveActionResultCode.MissingActor, StringComparison.Ordinal);
        }

        private static bool ValidateRequestShape(AttackResolutionRequest request, out AttackOutcome outcome, out string code, out string message)
        {
            outcome = AttackOutcome.Invalid;
            code = AttackResolutionResultCode.InvalidRequest;
            message = string.Empty;

            if (request.TargetObject == null)
            {
                code = AttackResolutionResultCode.MissingTarget;
                message = "Attack target is missing.";
                return false;
            }

            if (request.RequiresAttacker && request.AttackerObject == null)
            {
                code = AttackResolutionResultCode.MissingAttacker;
                message = "Attack source type requires an attacker.";
                return false;
            }

            if (request.DamageType == null)
            {
                code = AttackResolutionResultCode.UnknownDamageType;
                message = "Damage Type is missing.";
                return false;
            }

            if (!IsValidTransactionId(request.TransactionId))
            {
                message = "Attack transaction ID is malformed.";
                return false;
            }

            if (!IsFinite(request.BaseDamage) || request.BaseDamage < 0f)
            {
                message = "Base damage must be finite and non-negative.";
                return false;
            }

            if (!IsFinite(request.BaseHitChance) || request.BaseHitChance < 0f || request.BaseHitChance > 1f)
            {
                message = "Base hit chance must be finite and within [0, 1].";
                return false;
            }

            if (!IsFinite(request.CriticalChance) || request.CriticalChance < 0f || request.CriticalChance > 1f)
            {
                message = "Critical chance must be finite, non-negative, and within [0, 1].";
                return false;
            }

            if (!IsFinite(request.CriticalMultiplier) || request.CriticalMultiplier < 1f)
            {
                message = "Critical multiplier must be finite and at least 1.";
                return false;
            }

            if (!IsValidRoll(request.HitRoll) || !IsValidRoll(request.CriticalRoll))
            {
                code = AttackResolutionResultCode.InvalidRoll;
                message = "Hit and critical rolls must be finite values in [0, 1).";
                return false;
            }

            if (request.HasSuppliedDistance && (!IsFinite(request.SuppliedDistance) || request.SuppliedDistance < 0f))
            {
                message = "Supplied distance must be finite and non-negative.";
                return false;
            }

            if (request.HasMaximumRange && (!IsFinite(request.MaximumRange) || request.MaximumRange < 0f))
            {
                message = "Maximum range must be finite and non-negative.";
                return false;
            }

            outcome = AttackOutcome.Hit;
            code = string.Empty;
            message = string.Empty;
            return true;
        }

        private static bool TryValidateContext(AttackResolutionRequest request, out string code, out string message)
        {
            code = string.Empty;
            message = string.Empty;
            if (request.HasSuppliedLineOfSight && !request.SuppliedLineOfSight)
            {
                code = AttackResolutionResultCode.RequirementFailed;
                message = "Supplied line-of-sight context blocks the attack.";
                return false;
            }

            if (request.HasSuppliedTargetValidity && !request.SuppliedTargetValid)
            {
                code = AttackResolutionResultCode.RequirementFailed;
                message = "Supplied target-validity context blocks the attack.";
                return false;
            }

            if (request.HasSuppliedDistance && request.HasMaximumRange && request.SuppliedDistance > request.MaximumRange)
            {
                code = AttackResolutionResultCode.OutOfRange;
                message = $"Attack distance {request.SuppliedDistance:0.###} exceeds maximum range {request.MaximumRange:0.###}.";
                return false;
            }

            return true;
        }

        private static RequirementSummary EvaluateRequirements(AttackResolutionRequest request, ActorRuntime attacker)
        {
            if (request.Requirements == null)
            {
                return RequirementSummary.PassedEmpty;
            }

            if (attacker.Character == null)
            {
                return new RequirementSummary(false, "Requirement evaluation requires a character attacker.", new[] { "Missing character attacker." });
            }

            RequirementEvaluationResult result = attacker.Character.Query.EvaluateRequirement(request.Requirements);
            IReadOnlyList<string> failures = result.VisibleFailureReasons.Count > 0
                ? result.VisibleFailureReasons
                : result.NodeResults.Where(node => !node.Passed && !string.IsNullOrWhiteSpace(node.InternalReason)).Select(node => node.InternalReason).ToList();
            string summary = result.Passed
                ? $"Requirement set '{result.RequirementSetId}' passed."
                : $"Requirement set '{result.RequirementSetId}' failed: {string.Join("; ", failures)}";
            return new RequirementSummary(result.Passed, summary, failures);
        }

        private static bool TryResolveActor(GameObject actorObject, string expectedActorId, bool resolvingAttacker, bool requireReady, out ActorRuntime actor, out string code, out string message)
        {
            actor = default;
            code = string.Empty;
            message = string.Empty;
            if (actorObject == null)
            {
                code = resolvingAttacker ? AttackResolutionResultCode.MissingAttacker : AttackResolutionResultCode.MissingTarget;
                message = "Actor object is missing.";
                return false;
            }

            CharacterSystemCoordinator character = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            string actorId = ResolveActorId(actorObject, character);
            if (string.IsNullOrWhiteSpace(actorId))
            {
                code = resolvingAttacker ? AttackResolutionResultCode.MissingAttacker : AttackResolutionResultCode.MissingTarget;
                message = "Actor identity is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedActorId) && !string.Equals(expectedActorId, actorId, StringComparison.Ordinal))
            {
                code = resolvingAttacker ? AttackResolutionResultCode.StaleAttacker : AttackResolutionResultCode.StaleTarget;
                message = $"Actor identity '{expectedActorId}' no longer resolves to '{actorId}'.";
                return false;
            }

            if (requireReady && character != null && !character.IsReady)
            {
                code = resolvingAttacker ? AttackResolutionResultCode.AttackerNotReady : AttackResolutionResultCode.TargetNotReady;
                message = $"Character '{actorObject.name}' is not Ready.";
                return false;
            }

            CalculatedStatCollection stats = character == null
                ? actorObject.GetComponentInParent<CalculatedStatCollection>()
                : character.CalculatedStats;
            actor = new ActorRuntime(actorObject, actorId, character, stats);
            return true;
        }

        private static string ResolveActorId(GameObject actorObject, CharacterSystemCoordinator character)
        {
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private static bool TryReadStat(CalculatedStatCollection stats, string statId, out float value)
        {
            value = 0f;
            if (stats == null || !stats.HasStat(statId))
            {
                return false;
            }

            value = stats.GetValue(statId);
            return IsFinite(value);
        }

        private AttackResolutionResult CreateTerminalResult(
            AttackResolutionRequest request,
            bool execute,
            AttackOutcome outcome,
            string code,
            string message,
            string resolvedTargetActorId = "",
            string resolvedAttackerActorId = "",
            RequirementSummary requirement = default)
        {
            if (requirement.Equals(default(RequirementSummary)))
            {
                requirement = RequirementSummary.PassedEmpty;
            }

            return AttackResolutionResult.Create(
                preview: !execute,
                processed: execute,
                duplicate: false,
                outcome,
                code,
                message,
                request,
                resolvedAttackerActorId,
                resolvedTargetActorId,
                string.Empty,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                hit: false,
                critical: false,
                damageAfterCritical: 0f,
                requirement.Passed,
                requirement.Summary,
                requirement.FailureReasons,
                damageResult: null);
        }

        private void EmitExecutionEvents(AttackResolutionResult result)
        {
            if (result == null || result.Preview || result.Duplicate)
            {
                return;
            }

            if (result.Outcome == AttackOutcome.Blocked)
            {
                AttackBlocked?.Invoke(result);
            }
            else if (result.Outcome == AttackOutcome.Miss)
            {
                AttackMissed?.Invoke(result);
            }
            else if (result.Outcome == AttackOutcome.Hit || result.Outcome == AttackOutcome.CriticalHit)
            {
                AttackHit?.Invoke(result);
                if (result.Outcome == AttackOutcome.CriticalHit)
                {
                    CriticalHit?.Invoke(result);
                }

                if (result.DefenseResult != null && result.DefenseResult.DamageFullyPrevented)
                {
                    AttackDamagePrevented?.Invoke(result);
                }
                else if (result.DamageResult != null && result.DamageResult.Succeeded)
                {
                    if (result.DamageResult.HealthChanged)
                    {
                        AttackDamageApplied?.Invoke(result);
                    }
                    else if (result.DamageResult.FinalDamageAmount <= CharacterResourceCollection.Epsilon)
                    {
                        AttackDamagePrevented?.Invoke(result);
                    }
                }
            }

            AttackProcessed?.Invoke(result);
        }

        private void RecordCombatActivity(AttackResolutionResult result)
        {
            if (combatStateService == null || result == null || result.Preview || result.Duplicate || !result.Processed)
            {
                return;
            }

            combatStateService.RecordAttackResult(result);
        }

        private void RememberProcessedAttack(string transactionId, AttackResolutionResult result)
        {
            if (processedAttackResults.ContainsKey(transactionId))
            {
                return;
            }

            processedAttackResults.Add(transactionId, result);
            processedAttackOrder.Enqueue(transactionId);
            while (processedAttackResults.Count > processedAttackLimit && processedAttackOrder.Count > 0)
            {
                processedAttackResults.Remove(processedAttackOrder.Dequeue());
            }
        }

        private static string BuildDamageReason(AttackResolutionRequest request, bool critical)
        {
            string source = !string.IsNullOrWhiteSpace(request.OriginatingActionId)
                ? request.OriginatingActionId
                : !string.IsNullOrWhiteSpace(request.OriginatingAbilityId)
                    ? request.OriginatingAbilityId
                    : !string.IsNullOrWhiteSpace(request.OriginatingItemOrWeaponId)
                        ? request.OriginatingItemOrWeaponId
                        : request.SourceType.ToString();
            return critical ? $"Critical attack from {source}." : $"Attack from {source}.";
        }

        private static bool IsValidTransactionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidRoll(float value)
        {
            return IsFinite(value) && value >= 0f && value < 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct ActorRuntime
        {
            public ActorRuntime(GameObject gameObject, string actorId, CharacterSystemCoordinator character, CalculatedStatCollection stats)
            {
                GameObject = gameObject;
                ActorId = actorId ?? string.Empty;
                Character = character;
                Stats = stats;
            }

            public GameObject GameObject { get; }
            public string ActorId { get; }
            public CharacterSystemCoordinator Character { get; }
            public CalculatedStatCollection Stats { get; }
        }

        private readonly struct RequirementSummary
        {
            public static readonly RequirementSummary PassedEmpty = new RequirementSummary(true, "No requirement set.", Array.Empty<string>());

            public RequirementSummary(bool passed, string summary, IReadOnlyList<string> failureReasons)
            {
                Passed = passed;
                Summary = summary ?? string.Empty;
                FailureReasons = failureReasons == null ? Array.Empty<string>() : new List<string>(failureReasons);
            }

            public bool Passed { get; }
            public string Summary { get; }
            public IReadOnlyList<string> FailureReasons { get; }
        }
    }
}
