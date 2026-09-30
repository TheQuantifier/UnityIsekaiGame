using UnityEngine;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Abilities
{
    public static class AbilityActorIdentityUtility
    {
        public static string ResolveActorId(GameObject actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator character = actor.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = actor.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }
    }
}
