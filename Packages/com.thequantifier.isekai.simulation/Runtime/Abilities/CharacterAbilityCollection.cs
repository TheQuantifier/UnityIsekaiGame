using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Abilities
{
    public enum AbilityGrantSourceCategory
    {
        AuthoredLoadout = 0,
        Skill = 100,
        Trait = 200,
        BirthGift = 300,
        Role = 400,
        SocialStatus = 500,
        Equipment = 600,
        Development = 900
    }

    [Serializable]
    public sealed class RuntimeAbilityGrantRecord
    {
        public string abilityId;
        public AbilityGrantSourceCategory sourceCategory;
        public string sourceId;
        public string grantedAtUtc;
    }

    public readonly struct AbilityGrantResult
    {
        private AbilityGrantResult(bool succeeded, bool duplicate, string code, string message)
        {
            Succeeded = succeeded;
            Duplicate = duplicate;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public static AbilityGrantResult Success(string message, bool duplicate = false) => new AbilityGrantResult(true, duplicate, duplicate ? "AlreadyGranted" : "Success", message);
        public static AbilityGrantResult Failure(string code, string message) => new AbilityGrantResult(false, false, code, message);
    }

    /// <summary>Canonical, source-aware ownership for abilities. Loadouts equip owned abilities; they do not own them.</summary>
    public sealed class CharacterAbilityCollection : MonoBehaviour
    {
        private readonly Dictionary<string, AbilityDefinition> definitionsById = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeAbilityGrantRecord> grantsByKey = new Dictionary<string, RuntimeAbilityGrantRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeAbilityGrantRecord> blocksByKey = new Dictionary<string, RuntimeAbilityGrantRecord>(StringComparer.Ordinal);

        public event Action<CharacterAbilityCollection, string, bool> OwnershipChanged;
        public bool IsConfigured { get; private set; }

        public void Configure(DefinitionRegistry registry)
        {
            definitionsById.Clear();
            if (registry != null)
            {
                foreach (AbilityDefinition definition in registry.DefinitionsById.Values.OfType<AbilityDefinition>())
                {
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.Id) && !definitionsById.ContainsKey(definition.Id))
                    {
                        definitionsById.Add(definition.Id, definition);
                    }
                }
            }

            IsConfigured = definitionsById.Count > 0;
            foreach (string invalidKey in grantsByKey.Where(pair => !definitionsById.ContainsKey(pair.Value.abilityId)).Select(pair => pair.Key).ToArray())
            {
                grantsByKey.Remove(invalidKey);
            }
            foreach (string invalidKey in blocksByKey.Where(pair => !definitionsById.ContainsKey(pair.Value.abilityId)).Select(pair => pair.Key).ToArray())
            {
                blocksByKey.Remove(invalidKey);
            }
        }

        public AbilityGrantResult Grant(AbilityDefinition ability, AbilityGrantSourceCategory sourceCategory, string sourceId, bool restoring = false)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.Id))
            {
                return AbilityGrantResult.Failure("MissingAbility", "Ability grant is missing an ability definition.");
            }

            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return AbilityGrantResult.Failure("MissingSource", $"Ability '{ability.Id}' grant is missing a source ID.");
            }

            if (IsConfigured && (!definitionsById.TryGetValue(ability.Id, out AbilityDefinition registered) || !ReferenceEquals(registered, ability)))
            {
                return AbilityGrantResult.Failure("UnknownAbility", $"Ability '{ability.Id}' is not registered in the active catalog.");
            }

            string key = Key(ability.Id, sourceCategory, sourceId);
            if (grantsByKey.ContainsKey(key))
            {
                return AbilityGrantResult.Success($"Ability '{ability.Id}' is already granted by '{sourceId}'.", duplicate: true);
            }

            bool previouslyOwned = HasAbility(ability.Id);
            grantsByKey.Add(key, new RuntimeAbilityGrantRecord
            {
                abilityId = ability.Id,
                sourceCategory = sourceCategory,
                sourceId = sourceId,
                grantedAtUtc = DateTime.UtcNow.ToString("O")
            });
            if (!previouslyOwned)
            {
                OwnershipChanged?.Invoke(this, ability.Id, restoring);
            }

            return AbilityGrantResult.Success($"Granted ability '{ability.Id}' from '{sourceId}'.");
        }

        public bool RemoveSource(AbilityGrantSourceCategory sourceCategory, string sourceId, bool restoring = false)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            string[] affected = grantsByKey.Values
                .Where(grant => grant.sourceCategory == sourceCategory && string.Equals(grant.sourceId, sourceId, StringComparison.Ordinal))
                .Select(grant => grant.abilityId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (affected.Length == 0)
            {
                return false;
            }

            foreach (string key in grantsByKey.Where(pair => pair.Value.sourceCategory == sourceCategory && string.Equals(pair.Value.sourceId, sourceId, StringComparison.Ordinal)).Select(pair => pair.Key).ToArray())
            {
                grantsByKey.Remove(key);
            }

            foreach (string abilityId in affected.Where(id => !HasAbility(id)))
            {
                OwnershipChanged?.Invoke(this, abilityId, restoring);
            }

            return true;
        }

        public AbilityGrantResult Block(AbilityDefinition ability, AbilityGrantSourceCategory sourceCategory, string sourceId, bool restoring = false)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.Id) || string.IsNullOrWhiteSpace(sourceId))
            {
                return AbilityGrantResult.Failure("InvalidBlock", "Ability block requires an ability and source ID.");
            }

            if (IsConfigured && (!definitionsById.TryGetValue(ability.Id, out AbilityDefinition registered) || !ReferenceEquals(registered, ability)))
            {
                return AbilityGrantResult.Failure("UnknownAbility", $"Ability '{ability.Id}' is not registered in the active catalog.");
            }

            string key = Key(ability.Id, sourceCategory, sourceId);
            if (blocksByKey.ContainsKey(key))
            {
                return AbilityGrantResult.Success($"Ability '{ability.Id}' is already blocked by '{sourceId}'.", duplicate: true);
            }

            blocksByKey.Add(key, new RuntimeAbilityGrantRecord { abilityId = ability.Id, sourceCategory = sourceCategory, sourceId = sourceId, grantedAtUtc = DateTime.UtcNow.ToString("O") });
            OwnershipChanged?.Invoke(this, ability.Id, restoring);
            return AbilityGrantResult.Success($"Blocked ability '{ability.Id}' from '{sourceId}'.");
        }

        public bool RemoveBlockSource(AbilityGrantSourceCategory sourceCategory, string sourceId, bool restoring = false)
        {
            string[] keys = blocksByKey.Where(pair => pair.Value.sourceCategory == sourceCategory && string.Equals(pair.Value.sourceId, sourceId, StringComparison.Ordinal)).Select(pair => pair.Key).ToArray();
            if (keys.Length == 0)
            {
                return false;
            }

            string[] affected = keys.Select(key => blocksByKey[key].abilityId).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string key in keys)
            {
                blocksByKey.Remove(key);
            }
            foreach (string abilityId in affected)
            {
                OwnershipChanged?.Invoke(this, abilityId, restoring);
            }
            return true;
        }

        public bool HasAbility(string abilityId) => !string.IsNullOrWhiteSpace(abilityId) && grantsByKey.Values.Any(grant => string.Equals(grant.abilityId, abilityId, StringComparison.Ordinal));
        public bool IsAbilityBlocked(string abilityId) => !string.IsNullOrWhiteSpace(abilityId) && blocksByKey.Values.Any(block => string.Equals(block.abilityId, abilityId, StringComparison.Ordinal));
        public bool CanUseAbility(string abilityId) => HasAbility(abilityId) && !IsAbilityBlocked(abilityId);

        public IReadOnlyList<AbilityDefinition> GetOwnedAbilities()
        {
            return grantsByKey.Values.Select(grant => grant.abilityId).Distinct(StringComparer.Ordinal)
                .Where(definitionsById.ContainsKey).Select(id => definitionsById[id]).OrderBy(definition => definition.DisplayName).ToArray();
        }

        public IReadOnlyList<RuntimeAbilityGrantRecord> GetGrantRecords(string abilityId = "")
        {
            return grantsByKey.Values
                .Where(grant => string.IsNullOrWhiteSpace(abilityId) || string.Equals(grant.abilityId, abilityId, StringComparison.Ordinal))
                .Select(Clone).ToArray();
        }

        private static string Key(string abilityId, AbilityGrantSourceCategory category, string sourceId) => $"{(int)category}|{sourceId}|{abilityId}";

        private static RuntimeAbilityGrantRecord Clone(RuntimeAbilityGrantRecord record) => new RuntimeAbilityGrantRecord
        {
            abilityId = record.abilityId,
            sourceCategory = record.sourceCategory,
            sourceId = record.sourceId,
            grantedAtUtc = record.grantedAtUtc
        };
    }
}
