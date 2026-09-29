using System;
using UnityEngine;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Combat.Execution;

namespace UnityIsekaiGame.Combat
{
    [Serializable]
    public sealed class RangedWeaponData
    {
        [SerializeField] private bool weapon;
        [SerializeField] private string attackName = "Ranged Attack";
        [SerializeField, Min(0f)] private float baseDamage = 5f;
        [SerializeField] private CombatExecutionDefinition execution;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 18f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0.01f)] private float projectileHitRadius = 0.08f;
        [SerializeField] private Vector3 launchOffset = new Vector3(0.18f, -0.12f, 0.55f);
        [SerializeField] private ItemDefinition ammoItem;
        [SerializeField] private SpellProjectile projectilePrefab;
        [SerializeField] private GameObject projectileVisualPrefab;
        [SerializeField] private DamageTypeDefinition damageType;

        public bool IsWeapon => weapon;
        public string AttackName => string.IsNullOrWhiteSpace(attackName) ? "Ranged Attack" : attackName;
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public CombatExecutionDefinition Execution => execution;
        public float AttackCooldown => execution == null ? 0f : execution.CooldownDuration;
        public float StaminaCost => MeleeWeaponData.ResolveStaminaCost(execution);
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public float ProjectileLifetime => Mathf.Max(0.1f, projectileLifetime);
        public float ProjectileHitRadius => Mathf.Max(0.01f, projectileHitRadius);
        public Vector3 LaunchOffset => launchOffset;
        public ItemDefinition AmmoItem => ammoItem;
        public SpellProjectile ProjectilePrefab => projectilePrefab;
        public GameObject ProjectileVisualPrefab => projectileVisualPrefab;
        public DamageTypeDefinition DamageType => damageType;

        public void SetEnabled(bool value) => weapon = value;

        public void Validate()
        {
            baseDamage = Mathf.Max(0f, baseDamage);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            projectileHitRadius = Mathf.Max(0.01f, projectileHitRadius);
        }
    }
}
