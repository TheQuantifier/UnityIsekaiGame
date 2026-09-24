using System;
using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    [Serializable]
    public sealed class MeleeWeaponData
    {
        [SerializeField] private bool weapon;
        [SerializeField] private string attackName = "Attack";
        [SerializeField, Min(0f)] private float baseDamage = 5f;
        [SerializeField, Min(0.1f)] private float attackRange = 2f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.5f;
        [SerializeField, Min(0f)] private float staminaCost;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.35f;
        [SerializeField] private DamageTypeDefinition damageType;

        public bool IsWeapon => weapon;
        public string AttackName => string.IsNullOrWhiteSpace(attackName) ? "Attack" : attackName;
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public float AttackRange => Mathf.Max(0.1f, attackRange);
        public float AttackCooldown => Mathf.Max(0f, attackCooldown);
        public float StaminaCost => Mathf.Max(0f, staminaCost);
        public float HitRadius => Mathf.Max(0.01f, hitRadius);
        public DamageTypeDefinition DamageType => damageType;

        public void SetEnabled(bool value) => weapon = value;

        public void Validate()
        {
            baseDamage = Mathf.Max(0f, baseDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            staminaCost = Mathf.Max(0f, staminaCost);
            hitRadius = Mathf.Max(0.01f, hitRadius);
        }
    }
}
