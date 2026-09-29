using System;
using UnityEngine;
using UnityIsekaiGame.Magic;

namespace UnityIsekaiGame.Abilities
{
    [Serializable]
    public sealed class AbilityProjectileDelivery
    {
        [SerializeField] private SpellProjectile projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 16f;
        [SerializeField, Min(0.1f)] private float maximumLifetime = 3f;
        [SerializeField] private Vector3 castPointOffset = new Vector3(0.2f, -0.15f, 0.4f);

        public SpellProjectile ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public float MaximumLifetime => maximumLifetime;
        public Vector3 CastPointOffset => castPointOffset;

        public void Validate()
        {
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            maximumLifetime = Mathf.Max(0.1f, maximumLifetime);
        }
    }
}
