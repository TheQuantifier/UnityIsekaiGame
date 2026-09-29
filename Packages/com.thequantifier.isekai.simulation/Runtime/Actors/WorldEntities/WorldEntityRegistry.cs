using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityIsekaiGame.WorldEntities
{
    public static class WorldEntityRegistry
    {
        private static readonly Dictionary<string, WorldEntityIdentity> EntitiesById = new Dictionary<string, WorldEntityIdentity>(StringComparer.Ordinal);

        public static int Count
        {
            get
            {
                CleanupDestroyedEntries();
                return EntitiesById.Count;
            }
        }

        public static IReadOnlyList<WorldEntityIdentity> RegisteredEntities
        {
            get
            {
                CleanupDestroyedEntries();
                return EntitiesById.Values.OrderBy(identity => identity.EntityId, StringComparer.Ordinal).ToList();
            }
        }

        public static WorldEntityRegistryResult Register(WorldEntityIdentity identity)
        {
            CleanupDestroyedEntries();
            if (identity == null)
            {
                return WorldEntityRegistryResult.Failure("MissingIdentity", "Cannot register a null world entity identity.");
            }

            if (!identity.ValidateIdentity(out string failureReason))
            {
                return WorldEntityRegistryResult.Failure("InvalidIdentity", failureReason);
            }

            if (identity.IdentityKind == WorldEntityIdentityKind.Transient)
            {
                return WorldEntityRegistryResult.Failure("Transient", "Transient world entities are not registered for persistence.");
            }

            string entityId = identity.EntityId;
            if (EntitiesById.TryGetValue(entityId, out WorldEntityIdentity existing) && existing != null && !ReferenceEquals(existing, identity))
            {
                return WorldEntityRegistryResult.Failure("DuplicateId", $"World entity ID '{entityId}' is already registered by '{existing.name}'.");
            }

            EntitiesById[entityId] = identity;
            identity.MarkRegisteredForRegistry();
            return WorldEntityRegistryResult.Success($"Registered world entity '{entityId}'.");
        }

        public static void Unregister(WorldEntityIdentity identity)
        {
            if (identity == null)
            {
                return;
            }

            string entityId = identity.EntityId;
            if (EntitiesById.TryGetValue(entityId, out WorldEntityIdentity found) && ReferenceEquals(found, identity))
            {
                EntitiesById.Remove(entityId);
            }

            identity.MarkUnregisteredForRegistry();
        }

        public static bool TryResolve(string entityId, out WorldEntityIdentity identity)
        {
            CleanupDestroyedEntries();
            identity = null;
            string normalized = WorldEntityIdUtility.Normalize(entityId);
            return !string.IsNullOrWhiteSpace(normalized)
                && EntitiesById.TryGetValue(normalized, out identity)
                && identity != null;
        }

        public static bool TryResolveComponent<TComponent>(string entityId, out TComponent component)
            where TComponent : Component
        {
            component = null;
            if (!TryResolve(entityId, out WorldEntityIdentity identity))
            {
                return false;
            }

            component = identity.GetComponent<TComponent>();
            return component != null;
        }

        public static IReadOnlyList<WorldEntityIdentity> QueryByScene(string sceneKey)
        {
            string normalized = WorldEntityIdUtility.Normalize(sceneKey);
            return RegisteredEntities.Where(identity => identity.SceneKey == normalized).ToList();
        }

        public static IReadOnlyList<WorldEntityIdentity> QueryByKind(WorldEntityIdentityKind kind)
        {
            return RegisteredEntities.Where(identity => identity.IdentityKind == kind).ToList();
        }

        public static IReadOnlyList<WorldEntityIdentity> QueryPersistenceEligible()
        {
            return RegisteredEntities.Where(identity => identity.PersistenceEligible).ToList();
        }

        public static void ClearScene(string sceneKey)
        {
            string normalized = WorldEntityIdUtility.Normalize(sceneKey);
            foreach (WorldEntityIdentity identity in QueryByScene(normalized))
            {
                Unregister(identity);
            }
        }

        public static IReadOnlyList<WorldEntityRegistrySnapshot> BuildDiagnosticSnapshot()
        {
            return RegisteredEntities
                .Select(identity => new WorldEntityRegistrySnapshot(
                    identity.EntityId,
                    identity.SceneKey,
                    identity.WorldId,
                    identity.IdentityKind,
                    identity.gameObject.activeInHierarchy,
                    identity.PersistenceEligible,
                    identity.DefinitionId,
                    identity.ExpectedEntityType))
                .ToList();
        }

        public static string BuildDiagnosticReport()
        {
            List<string> lines = new List<string> { $"Registered world entities: {Count}" };
            foreach (WorldEntityRegistrySnapshot snapshot in BuildDiagnosticSnapshot())
            {
                lines.Add($"{snapshot.EntityId} | {snapshot.IdentityKind} | scene={snapshot.SceneKey} | active={snapshot.ActiveInHierarchy} | def={snapshot.DefinitionId}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static void ClearForTests()
        {
            foreach (WorldEntityIdentity identity in EntitiesById.Values)
            {
                if (identity != null)
                {
                    identity.MarkUnregisteredForRegistry();
                }
            }

            EntitiesById.Clear();
        }

        private static void CleanupDestroyedEntries()
        {
            List<string> destroyed = null;
            foreach (KeyValuePair<string, WorldEntityIdentity> pair in EntitiesById)
            {
                if (pair.Value == null)
                {
                    destroyed ??= new List<string>();
                    destroyed.Add(pair.Key);
                }
            }

            if (destroyed == null)
            {
                return;
            }

            for (int i = 0; i < destroyed.Count; i++)
            {
                EntitiesById.Remove(destroyed[i]);
            }
        }
    }
}
