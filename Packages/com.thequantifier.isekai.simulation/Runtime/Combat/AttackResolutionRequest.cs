using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Requirements;

namespace UnityIsekaiGame.Combat
{
    public readonly struct AttackResolutionRequest
    {
        public const float DefaultBaseHitChance = 0.75f;
        public const float DefaultCriticalChance = 0f;
        public const float DefaultCriticalMultiplier = 1.5f;

        public AttackResolutionRequest(
            string transactionId,
            AttackSourceType sourceType,
            GameObject attackerObject,
            string attackerActorId,
            GameObject targetObject,
            string targetActorId,
            DamageTypeDefinition damageType,
            float baseDamage,
            float hitRoll,
            float criticalRoll,
            float baseHitChance = DefaultBaseHitChance,
            float criticalChance = DefaultCriticalChance,
            float criticalMultiplier = DefaultCriticalMultiplier,
            bool hasSuppliedDistance = false,
            float suppliedDistance = 0f,
            bool hasMaximumRange = false,
            float maximumRange = 0f,
            bool suppliedLineOfSight = true,
            bool hasSuppliedLineOfSight = false,
            bool suppliedTargetValid = true,
            bool hasSuppliedTargetValidity = false,
            RequirementSetDefinition requirements = null,
            string originatingActionId = "",
            string originatingAbilityId = "",
            string originatingItemOrWeaponId = "",
            string originatingSpellOrEffectId = "",
            IReadOnlyDictionary<string, string> metadata = null,
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceType = sourceType;
            AttackerObject = attackerObject;
            AttackerActorId = attackerActorId ?? string.Empty;
            TargetObject = targetObject;
            TargetActorId = targetActorId ?? string.Empty;
            DamageType = damageType;
            BaseDamage = baseDamage;
            BaseHitChance = baseHitChance;
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            HasSuppliedDistance = hasSuppliedDistance;
            SuppliedDistance = suppliedDistance;
            HasMaximumRange = hasMaximumRange;
            MaximumRange = maximumRange;
            HasSuppliedLineOfSight = hasSuppliedLineOfSight;
            SuppliedLineOfSight = suppliedLineOfSight;
            HasSuppliedTargetValidity = hasSuppliedTargetValidity;
            SuppliedTargetValid = suppliedTargetValid;
            HitRoll = hitRoll;
            CriticalRoll = criticalRoll;
            Requirements = requirements;
            OriginatingActionId = originatingActionId ?? string.Empty;
            OriginatingAbilityId = originatingAbilityId ?? string.Empty;
            OriginatingItemOrWeaponId = originatingItemOrWeaponId ?? string.Empty;
            OriginatingSpellOrEffectId = originatingSpellOrEffectId ?? string.Empty;
            Metadata = metadata == null
                ? Array.Empty<KeyValuePair<string, string>>()
                : new List<KeyValuePair<string, string>>(metadata);
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public AttackSourceType SourceType { get; }
        public GameObject AttackerObject { get; }
        public string AttackerActorId { get; }
        public GameObject TargetObject { get; }
        public string TargetActorId { get; }
        public DamageTypeDefinition DamageType { get; }
        public float BaseDamage { get; }
        public float BaseHitChance { get; }
        public float CriticalChance { get; }
        public float CriticalMultiplier { get; }
        public bool HasSuppliedDistance { get; }
        public float SuppliedDistance { get; }
        public bool HasMaximumRange { get; }
        public float MaximumRange { get; }
        public bool HasSuppliedLineOfSight { get; }
        public bool SuppliedLineOfSight { get; }
        public bool HasSuppliedTargetValidity { get; }
        public bool SuppliedTargetValid { get; }
        public float HitRoll { get; }
        public float CriticalRoll { get; }
        public RequirementSetDefinition Requirements { get; }
        public string OriginatingActionId { get; }
        public string OriginatingAbilityId { get; }
        public string OriginatingItemOrWeaponId { get; }
        public string OriginatingSpellOrEffectId { get; }
        public IReadOnlyList<KeyValuePair<string, string>> Metadata { get; }
        public bool AuthorityValidated { get; }

        public bool RequiresAttacker => SourceType != AttackSourceType.Environmental && SourceType != AttackSourceType.Scripted;
    }
}
