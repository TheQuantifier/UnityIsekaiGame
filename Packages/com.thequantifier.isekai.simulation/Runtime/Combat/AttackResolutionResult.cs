using System;
using System.Collections.Generic;
using UnityIsekaiGame.Combat.Defense;

namespace UnityIsekaiGame.Combat
{
    public sealed class AttackResolutionResult
    {
        private AttackResolutionResult(
            bool succeeded,
            bool preview,
            bool processed,
            bool duplicate,
            AttackOutcome outcome,
            string code,
            string message,
            AttackResolutionRequest request,
            string resolvedAttackerActorId,
            string resolvedTargetActorId,
            string damageTransactionId,
            float attackerAccuracy,
            float targetEvasion,
            float normalizedAccuracyContribution,
            float normalizedEvasionContribution,
            float unclampedHitChance,
            float finalHitChance,
            bool hit,
            bool critical,
            float damageAfterCritical,
            bool rangeSupplied,
            float suppliedDistance,
            bool maximumRangeSupplied,
            float maximumRange,
            bool requirementPassed,
            string requirementSummary,
            IReadOnlyList<string> requirementFailureReasons,
            DamageApplicationResult damageResult,
            DefenseResolutionResult defenseResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Processed = processed;
            Duplicate = duplicate;
            Outcome = outcome;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? AttackResolutionResultCode.Processed : AttackResolutionResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Request = request;
            ResolvedAttackerActorId = resolvedAttackerActorId ?? string.Empty;
            ResolvedTargetActorId = resolvedTargetActorId ?? string.Empty;
            DamageTransactionId = damageTransactionId ?? string.Empty;
            AttackerAccuracy = attackerAccuracy;
            TargetEvasion = targetEvasion;
            NormalizedAccuracyContribution = normalizedAccuracyContribution;
            NormalizedEvasionContribution = normalizedEvasionContribution;
            UnclampedHitChance = unclampedHitChance;
            FinalHitChance = finalHitChance;
            Hit = hit;
            Critical = critical;
            CriticalChance = request.CriticalChance;
            CriticalRoll = request.CriticalRoll;
            CriticalMultiplier = request.CriticalMultiplier;
            BaseDamage = request.BaseDamage;
            DamageAfterCritical = damageAfterCritical;
            RangeSupplied = rangeSupplied;
            SuppliedDistance = suppliedDistance;
            MaximumRangeSupplied = maximumRangeSupplied;
            MaximumRange = maximumRange;
            RequirementPassed = requirementPassed;
            RequirementSummary = requirementSummary ?? string.Empty;
            RequirementFailureReasons = requirementFailureReasons == null ? Array.Empty<string>() : new List<string>(requirementFailureReasons);
            DamageResult = damageResult;
            DefenseResult = defenseResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Processed { get; }
        public bool Duplicate { get; }
        public AttackOutcome Outcome { get; }
        public string Code { get; }
        public string Message { get; }
        public AttackResolutionRequest Request { get; }
        public string AttackTransactionId => Request.TransactionId;
        public string ResolvedAttackerActorId { get; }
        public string ResolvedTargetActorId { get; }
        public AttackSourceType SourceType => Request.SourceType;
        public string OriginatingActionId => Request.OriginatingActionId;
        public string OriginatingAbilityId => Request.OriginatingAbilityId;
        public string OriginatingItemOrWeaponId => Request.OriginatingItemOrWeaponId;
        public string OriginatingSpellOrEffectId => Request.OriginatingSpellOrEffectId;
        public string DamageTypeId => Request.DamageType == null ? string.Empty : Request.DamageType.Id;
        public string DamageTransactionId { get; }
        public float RequestedBaseDamage => Request.BaseDamage;
        public float BaseHitChance => Request.BaseHitChance;
        public float AttackerAccuracy { get; }
        public float TargetEvasion { get; }
        public float NormalizedAccuracyContribution { get; }
        public float NormalizedEvasionContribution { get; }
        public float UnclampedHitChance { get; }
        public float FinalHitChance { get; }
        public float HitRoll => Request.HitRoll;
        public bool Hit { get; }
        public bool Critical { get; }
        public float CriticalChance { get; }
        public float CriticalRoll { get; }
        public float CriticalMultiplier { get; }
        public float BaseDamage { get; }
        public float DamageAfterCritical { get; }
        public bool RangeSupplied { get; }
        public float SuppliedDistance { get; }
        public bool MaximumRangeSupplied { get; }
        public float MaximumRange { get; }
        public bool RequirementPassed { get; }
        public string RequirementSummary { get; }
        public IReadOnlyList<string> RequirementFailureReasons { get; }
        public DamageApplicationResult DamageResult { get; }
        public DefenseResolutionResult DefenseResult { get; }
        public bool DamagePipelineSucceeded => DamageResult == null || DamageResult.Succeeded;
        public bool DamagePrevented => DefenseResult != null && DefenseResult.DamageFullyPrevented || DamageResult != null && DamageResult.Succeeded && DamageResult.FinalDamageAmount <= UnityIsekaiGame.ResourceSystem.CharacterResourceCollection.Epsilon;
        public bool DamageApplied => DamageResult != null && DamageResult.HealthChanged;
        public bool DamageDuplicate => DamageResult != null && DamageResult.Duplicate;

        public static AttackResolutionResult Create(
            bool preview,
            bool processed,
            bool duplicate,
            AttackOutcome outcome,
            string code,
            string message,
            AttackResolutionRequest request,
            string resolvedAttackerActorId,
            string resolvedTargetActorId,
            string damageTransactionId,
            float attackerAccuracy,
            float targetEvasion,
            float normalizedAccuracyContribution,
            float normalizedEvasionContribution,
            float unclampedHitChance,
            float finalHitChance,
            bool hit,
            bool critical,
            float damageAfterCritical,
            bool requirementPassed,
            string requirementSummary,
            IReadOnlyList<string> requirementFailureReasons,
            DamageApplicationResult damageResult,
            DefenseResolutionResult defenseResult = null)
        {
            bool succeeded = (outcome == AttackOutcome.Miss || outcome == AttackOutcome.Hit || outcome == AttackOutcome.CriticalHit)
                && (damageResult == null || damageResult.Succeeded);
            return new AttackResolutionResult(
                succeeded,
                preview,
                processed,
                duplicate,
                outcome,
                code,
                message,
                request,
                resolvedAttackerActorId,
                resolvedTargetActorId,
                damageTransactionId,
                attackerAccuracy,
                targetEvasion,
                normalizedAccuracyContribution,
                normalizedEvasionContribution,
                unclampedHitChance,
                finalHitChance,
                hit,
                critical,
                damageAfterCritical,
                request.HasSuppliedDistance,
                request.SuppliedDistance,
                request.HasMaximumRange,
                request.MaximumRange,
                requirementPassed,
                requirementSummary,
                requirementFailureReasons,
                damageResult,
                defenseResult);
        }

        public AttackResolutionResult AsDuplicate()
        {
            return new AttackResolutionResult(
                Succeeded,
                Preview,
                Processed,
                true,
                Outcome,
                AttackResolutionResultCode.DuplicateAttack,
                "Duplicate attack transaction ignored.",
                Request,
                ResolvedAttackerActorId,
                ResolvedTargetActorId,
                DamageTransactionId,
                AttackerAccuracy,
                TargetEvasion,
                NormalizedAccuracyContribution,
                NormalizedEvasionContribution,
                UnclampedHitChance,
                FinalHitChance,
                Hit,
                Critical,
                DamageAfterCritical,
                RangeSupplied,
                SuppliedDistance,
                MaximumRangeSupplied,
                MaximumRange,
                RequirementPassed,
                RequirementSummary,
                RequirementFailureReasons,
                DamageResult,
                DefenseResult);
        }
    }
}
