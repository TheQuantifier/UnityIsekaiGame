using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Magic
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpellProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float hitRadius = 0.12f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        private readonly List<Collider> ignoredCasterColliders = new List<Collider>();
        private Collider projectileCollider;
        private Rigidbody projectileRigidbody;
        private GameObject caster;
        private Vector3 direction;
        private float speed;
        private AbilityDefinition ability;
        private Action<GameObject, Vector3> impactCallback;
        private float expireAtTime;
        private bool initialized;
        private bool completed;
        private bool payloadExecuted;

        public event Action<SpellProjectile> Completed;

        private void Awake()
        {
            projectileCollider = GetComponent<Collider>();
            projectileRigidbody = GetComponent<Rigidbody>();
            projectileRigidbody.isKinematic = true;
            projectileRigidbody.useGravity = false;
            projectileRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void FixedUpdate()
        {
            if (!initialized || completed)
            {
                return;
            }

            if (Time.time >= expireAtTime)
            {
                Complete();
                return;
            }

            float distance = speed * Time.fixedDeltaTime;
            Vector3 origin = transform.position;
            if (TryGetFirstValidHit(origin, distance, out RaycastHit hit))
            {
                transform.position = hit.point;
                ResolveImpact(hit);
                return;
            }

            projectileRigidbody.MovePosition(origin + direction * distance);
        }

        public void Initialize(GameObject spellCaster, Vector3 travelDirection, float projectileSpeed, AbilityDefinition payloadAbility, float lifetime)
        {
            caster = spellCaster;
            ability = payloadAbility;
            impactCallback = null;
            direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : transform.forward;
            speed = Mathf.Max(0.1f, projectileSpeed);
            expireAtTime = Time.time + Mathf.Max(0.1f, lifetime);
            initialized = true;
            completed = false;
            payloadExecuted = false;
            IgnoreCasterColliders();
        }

        public void Initialize(GameObject projectileOwner, Vector3 travelDirection, float projectileSpeed, float lifetime, Action<GameObject, Vector3> onImpact)
        {
            caster = projectileOwner;
            ability = null;
            impactCallback = onImpact;
            direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : transform.forward;
            speed = Mathf.Max(0.1f, projectileSpeed);
            expireAtTime = Time.time + Mathf.Max(0.1f, lifetime);
            initialized = true;
            completed = false;
            payloadExecuted = false;
            IgnoreCasterColliders();
        }

        private void ResolveImpact(RaycastHit hit)
        {
            if (hit.collider == null || IsCasterCollider(hit.collider))
            {
                return;
            }

            if (ability != null)
            {
                ExecuteAbilityPayload(hit);
            }
            else
            {
                impactCallback?.Invoke(ResolveAbilityTarget(hit.collider), hit.point);
            }
            Complete();
        }

        private void ExecuteAbilityPayload(RaycastHit hit)
        {
            if (payloadExecuted || ability == null)
            {
                return;
            }

            payloadExecuted = true;
            GameObject target = ResolveAbilityTarget(hit.collider);
            EffectExecutionContext context = new EffectExecutionContext(
                ability,
                caster,
                target,
                caster == null ? transform.position : caster.transform.position,
                hit.point,
                direction);
            AbilityExecutionResult result = AbilityEffectPipeline.Execute(in context, ability.Effects);
            Debug.Log(result.Succeeded ? result.Message : result.Message);
        }

        private static GameObject ResolveAbilityTarget(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return null;
            }

            CharacterResourceCollection resources = hitCollider.GetComponentInParent<CharacterResourceCollection>();
            return resources == null ? hitCollider.gameObject : resources.gameObject;
        }

        private void IgnoreCasterColliders()
        {
            ignoredCasterColliders.Clear();
            if (caster == null || projectileCollider == null)
            {
                return;
            }

            caster.GetComponentsInChildren(ignoredCasterColliders);
            for (int i = 0; i < ignoredCasterColliders.Count; i++)
            {
                if (ignoredCasterColliders[i] != null)
                {
                    Physics.IgnoreCollision(projectileCollider, ignoredCasterColliders[i], true);
                }
            }
        }

        private bool IsCasterCollider(Collider hitCollider)
        {
            return caster != null && hitCollider.transform.IsChildOf(caster.transform);
        }

        private bool IsProjectileCollider(Collider hitCollider)
        {
            return hitCollider == projectileCollider || hitCollider.transform.IsChildOf(transform);
        }

        private bool TryGetFirstValidHit(Vector3 origin, float distance, out RaycastHit validHit)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, hitRadius, direction, distance, hitMask, triggerInteraction);
            foreach (RaycastHit hit in hits.OrderBy(candidate => candidate.distance))
            {
                if (hit.collider == null || IsProjectileCollider(hit.collider) || IsCasterCollider(hit.collider))
                {
                    continue;
                }

                validHit = hit;
                return true;
            }

            validHit = default;
            return false;
        }

        private void Complete()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            Completed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
