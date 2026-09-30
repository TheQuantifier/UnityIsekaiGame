using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.People
{
    public static class PersonRegistry
    {
        private static readonly Dictionary<string, List<PersonIdentity>> IdentitiesById = new Dictionary<string, List<PersonIdentity>>();
        private static readonly List<PersonIdentity> RegisteredIdentities = new List<PersonIdentity>();

        public static IReadOnlyList<PersonIdentity> Registered => RegisteredIdentities;

        public static void Register(PersonIdentity identity)
        {
            if (identity == null || RegisteredIdentities.Contains(identity))
            {
                return;
            }

            RegisteredIdentities.Add(identity);

            if (!identity.HasValidIdentity)
            {
                Debug.LogWarning($"{identity.name} has no valid PersonDefinition.");
                return;
            }

            if (!IdentitiesById.TryGetValue(identity.PersonId, out List<PersonIdentity> identities))
            {
                identities = new List<PersonIdentity>();
                IdentitiesById.Add(identity.PersonId, identities);
            }

            identities.Add(identity);
            if (identities.Count > 1)
            {
                Debug.LogWarning($"Duplicate active person identity registered: {identity.PersonId}.");
            }
        }

        public static void Unregister(PersonIdentity identity)
        {
            if (identity == null)
            {
                return;
            }

            RegisteredIdentities.Remove(identity);

            if (!identity.HasValidIdentity || !IdentitiesById.TryGetValue(identity.PersonId, out List<PersonIdentity> identities))
            {
                return;
            }

            identities.Remove(identity);
            if (identities.Count == 0)
            {
                IdentitiesById.Remove(identity.PersonId);
            }
        }

        public static bool TryGetIdentity(string personId, out PersonIdentity identity)
        {
            identity = null;
            if (string.IsNullOrWhiteSpace(personId) || !IdentitiesById.TryGetValue(personId, out List<PersonIdentity> identities) || identities.Count == 0)
            {
                return false;
            }

            identity = identities[0];
            return true;
        }

        public static bool HasDuplicateActiveIdentity(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId)
                && IdentitiesById.TryGetValue(personId, out List<PersonIdentity> identities)
                && identities.Count > 1;
        }
    }
}
