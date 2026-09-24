using System;
using UnityEngine;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat
{
    [Serializable]
    public sealed class MeleeWeaponData
    {
        [SerializeField] private bool weapon;
        [SerializeField] private string attackName = "Attack";
        [SerializeField, Min(0f)] private float baseDamage = 5f;
        [SerializeField, Min(0.1f)] private float attackRange = 2f;
        [SerializeField] private CombatExecutionDefinition execution;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.35f;
        [SerializeField] private DamageTypeDefinition damageType;

        public bool IsWeapon => weapon;
        public string AttackName => string.IsNullOrWhiteSpace(attackName) ? "Attack" : attackName;
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public float AttackRange => Mathf.Max(0.1f, attackRange);
        public CombatExecutionDefinition Execution => execution;
        public float AttackCooldown => execution == null ? 0f : execution.CooldownDuration;
        public float StaminaCost => ResolveStaminaCost(execution);
        public float HitRadius => Mathf.Max(0.01f, hitRadius);
        public DamageTypeDefinition DamageType => damageType;

        public void SetEnabled(bool value) => weapon = value;

        public void Validate()
        {
            baseDamage = Mathf.Max(0f, baseDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            hitRadius = Mathf.Max(0.01f, hitRadius);
        }

        internal static float ResolveStaminaCost(CombatExecutionDefinition definition)
        {
            float total = 0f;
            if (definition == null) return total;
            foreach (CombatExecutionCostDefinition cost in definition.Costs)
            {
                if (cost.CostType == CombatExecutionCostType.Resource && cost.Resource != null && cost.Resource.Id == ResourceIds.Stamina) total += cost.Amount;
            }
            return total;
        }
    }
}
