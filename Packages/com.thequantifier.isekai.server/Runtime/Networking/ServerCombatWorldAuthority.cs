using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    public sealed class ServerCombatWorldAuthority : MonoBehaviour
    {
        private readonly List<CombatantBinding> bindings = new List<CombatantBinding>();
        private readonly List<NetworkCombatantState> snapshot = new List<NetworkCombatantState>();
        private readonly Dictionary<ulong, Transform> playerTargets = new Dictionary<ulong, Transform>();
        private NetworkCombatWorldState networkState;
        private double nextPublishAt;
        private bool configured;
        private bool combatSmokeMode;

        public NetworkCombatWorldState NetworkState => networkState;

        public void Configure(NetworkCombatWorldState replicatedState, bool smokeMode = false)
        {
            if (configured) throw new InvalidOperationException("Server combat world authority is already configured.");
            networkState = replicatedState ?? throw new ArgumentNullException(nameof(replicatedState));
            combatSmokeMode = smokeMode;
            DiscoverCombatants();
            configured = true;
        }

        private void Update()
        {
            if (!configured || networkState == null || !networkState.IsSpawned || !networkState.IsServer) return;
            RefreshEnemyTargets();
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextPublishAt) return;
            nextPublishAt = now + 0.1d;
            PublishSnapshot();
        }

        public void PublishSnapshotNow()
        {
            if (configured && networkState != null && networkState.IsSpawned && networkState.IsServer) PublishSnapshot();
        }

        public void RegisterPlayerTarget(ulong clientId, Transform target)
        {
            if (target != null) playerTargets[clientId] = target;
            if (combatSmokeMode && target != null) PrepareSmokeTarget(target);
            RefreshEnemyTargets();
        }

        public void UnregisterPlayerTarget(ulong clientId)
        {
            playerTargets.Remove(clientId);
            RefreshEnemyTargets();
        }

        public bool TryResolveCombatant(Collider collider, out EnemyHealth health, out string entityId)
        {
            health = collider == null ? null : collider.GetComponentInParent<EnemyHealth>();
            if (health == null)
            {
                entityId = string.Empty;
                return false;
            }

            WorldEntityIdentity identity = health.GetComponentInParent<WorldEntityIdentity>();
            entityId = identity == null ? string.Empty : identity.EntityId;
            return !string.IsNullOrWhiteSpace(entityId);
        }

        private void DiscoverCombatants()
        {
            bindings.Clear();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealth health = enemies[i];
                WorldEntityIdentity identity = health == null ? null : health.GetComponentInParent<WorldEntityIdentity>();
                string id = identity == null ? string.Empty : identity.EntityId;
                CharacterResourceCollection resources = health == null ? null : health.GetComponent<CharacterResourceCollection>();
                if (health == null || resources == null || string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning($"[Network Combat] Ignored combatant '{health?.name ?? "missing"}' because it lacks Health resources or a stable world entity identity.", health);
                    continue;
                }

                if (!ids.Add(id)) throw new InvalidOperationException($"Duplicate authoritative combatant entity ID '{id}'.");
                bindings.Add(new CombatantBinding(id, health, resources, health.GetComponent<PrototypeEnemyController>()));
            }

            bindings.Sort((left, right) => string.CompareOrdinal(left.EntityId, right.EntityId));
        }

        private void PublishSnapshot()
        {
            snapshot.Clear();
            for (int i = bindings.Count - 1; i >= 0; i--)
            {
                CombatantBinding binding = bindings[i];
                if (binding.Health == null || binding.Resources == null)
                {
                    bindings.RemoveAt(i);
                    continue;
                }

                snapshot.Add(new NetworkCombatantState(
                    binding.EntityId,
                    binding.Health.transform.position,
                    binding.Health.transform.rotation,
                    binding.Health.CurrentHealth,
                    binding.Health.MaximumHealth,
                    binding.Health.IsDefeated));
            }

            snapshot.Sort((left, right) => string.CompareOrdinal(left.EntityIdText, right.EntityIdText));
            networkState.PublishServerSnapshot(snapshot);
        }

        private void RefreshEnemyTargets()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                CombatantBinding binding = bindings[i];
                if (binding.Controller == null || binding.Health == null) continue;
                Transform nearest = null;
                float nearestDistance = float.PositiveInfinity;
                foreach (Transform candidate in playerTargets.Values)
                {
                    if (candidate == null) continue;
                    float distance = (candidate.position - binding.Health.transform.position).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = candidate;
                    }
                }
                binding.Controller.SetTarget(nearest);
            }
        }

        private void PrepareSmokeTarget(Transform player)
        {
            if (bindings.Count == 0) return;
            CombatantBinding binding = bindings[0];
            if (binding.Health == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
            binding.Health.transform.SetPositionAndRotation(player.position + forward * 4f, Quaternion.LookRotation(-forward, Vector3.up));
            PrototypeEnemyPatrolController patrol = binding.Health.GetComponent<PrototypeEnemyPatrolController>();
            EnemyMeleeAttack attack = binding.Health.GetComponent<EnemyMeleeAttack>();
            if (binding.Controller != null) binding.Controller.enabled = false;
            if (patrol != null) patrol.enabled = false;
            if (attack != null) attack.enabled = false;
            PublishSnapshotNow();
        }

        private readonly struct CombatantBinding
        {
            public CombatantBinding(string entityId, EnemyHealth health, CharacterResourceCollection resources, PrototypeEnemyController controller)
            {
                EntityId = entityId;
                Health = health;
                Resources = resources;
                Controller = controller;
            }

            public string EntityId { get; }
            public EnemyHealth Health { get; }
            public CharacterResourceCollection Resources { get; }
            public PrototypeEnemyController Controller { get; }
        }
    }
}
