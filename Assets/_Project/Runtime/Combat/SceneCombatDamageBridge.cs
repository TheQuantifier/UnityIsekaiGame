using System;
using UnityEngine;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat
{
    public static class SceneCombatDamageBridge
    {
        private static readonly DamageHealingService DamageHealing = new DamageHealingService();

        public static bool CanUseCurrentResourcePipeline(GameObject target, in DamageInfo damageInfo)
        {
            return TryResolvePipelineRequest(target, in damageInfo, string.Empty, string.Empty, out _, out _);
        }

        public static bool TryApplyCurrentResourceDamage(
            GameObject target,
            in DamageInfo damageInfo,
            string transactionPrefix,
            string reason,
            out DamageResult result)
        {
            if (!TryResolvePipelineRequest(target, in damageInfo, transactionPrefix, reason, out DamageApplicationRequest request, out string failure))
            {
                result = DamageResult.Failure(damageInfo.RawAmount, failure);
                return false;
            }

            DamageApplicationResult application = DamageHealing.ApplyDamage(request);
            result = ToDamageResult(application);
            return true;
        }

        public static DamageResult ApplyDamage(
            GameObject target,
            in DamageInfo damageInfo,
            string transactionPrefix,
            string reason)
        {
            if (TryApplyCurrentResourceDamage(target, in damageInfo, transactionPrefix, reason, out DamageResult pipelineResult))
            {
                return pipelineResult;
            }

            return pipelineResult;
        }

        private static bool TryResolvePipelineRequest(
            GameObject target,
            in DamageInfo damageInfo,
            string transactionPrefix,
            string reason,
            out DamageApplicationRequest request,
            out string failure)
        {
            request = default;
            failure = string.Empty;
            if (target == null)
            {
                failure = "Damage target is missing.";
                return false;
            }

            CharacterSystemCoordinator targetCharacter = target.GetComponentInParent<CharacterSystemCoordinator>();
            CharacterResourceCollection targetResources = targetCharacter == null
                ? target.GetComponentInParent<CharacterResourceCollection>()
                : targetCharacter.Resources;
            if (targetResources == null || !targetResources.HasResource(ResourceIds.Health))
            {
                failure = "Target does not expose current Health resources.";
                return false;
            }

            if (!damageInfo.DamagePacket.HasComponents)
            {
                failure = "Damage packet has no canonical typed components.";
                return false;
            }

            string targetActorId = ResolveActorId(target, targetCharacter);
            if (string.IsNullOrWhiteSpace(targetActorId))
            {
                failure = "Target actor identity is missing.";
                return false;
            }

            CharacterSystemCoordinator sourceCharacter = damageInfo.Source == null ? null : damageInfo.Source.GetComponentInParent<CharacterSystemCoordinator>();
            string sourceActorId = ResolveActorId(damageInfo.Source, sourceCharacter);
            request = new DamageApplicationRequest(
                CreateTransactionId(transactionPrefix),
                sourceActorId,
                damageInfo.Source,
                targetActorId,
                target,
                damageInfo.DamagePacket,
                string.IsNullOrWhiteSpace(reason) ? "Scene combat damage" : reason,
                authorityValidated: true);
            return true;
        }

        private static DamageResult ToDamageResult(DamageApplicationResult result)
        {
            if (result == null)
            {
                return DamageResult.Failure(0f, "Damage application produced no result.");
            }

            if (!result.Succeeded)
            {
                return DamageResult.Failure(result.RequestedAmount, result.Message);
            }

            string message = result.HealthChanged
                ? $"Damage applied through current Health resources. Health: {result.NewHealth:0.#} / {result.HealthMaximum:0.#}."
                : result.Message;
            return new DamageResult(
                result.HealthChanged,
                result.RequestedAmount,
                result.RequestedAmount,
                result.DefenseApplied,
                result.DefenseMitigatedAmount,
                result.ResistanceMitigatedAmount,
                0f,
                result.HealthChanged ? result.ResourceResult?.AppliedAmount ?? result.FinalDamageAmount : 0f,
                result.NewHealth,
                result.BecameZero,
                message,
                Array.Empty<DamageComponentResult>());
        }

        private static string ResolveActorId(GameObject actor, CharacterSystemCoordinator character)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private static string CreateTransactionId(string prefix)
        {
            string normalized = string.IsNullOrWhiteSpace(prefix) ? "scene-combat.damage" : prefix.Trim();
            return $"{normalized}.{Guid.NewGuid():N}".ToLowerInvariant();
        }
    }
}
