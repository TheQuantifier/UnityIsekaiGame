using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Persistence
{
    [Serializable]
    public sealed class WorldSocialCognitionSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public List<PersonKnowledgeSaveData> knowledge = new List<PersonKnowledgeSaveData>();
        public List<PersonMemorySaveData> memories = new List<PersonMemorySaveData>();
    }

    public sealed class WorldSocialCognitionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.social-cognition";
        private readonly Func<IReadOnlyDictionary<string, PersonKnowledgeRuntime>> knowledgeProvider;
        private readonly Func<IReadOnlyDictionary<string, PersonMemoryRuntime>> memoryProvider;
        private readonly Action<IEnumerable<string>> ensurePeople;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<AuthoritativeHistoryRuntime> historyProvider;
        private readonly Func<IEnumerable<string>> knownPeopleProvider;
        private readonly string ownerId;

        public WorldSocialCognitionPersistenceParticipant(
            Func<IReadOnlyDictionary<string, PersonKnowledgeRuntime>> knowledge,
            Func<IReadOnlyDictionary<string, PersonMemoryRuntime>> memories,
            Action<IEnumerable<string>> ensure,
            Func<DefinitionRegistry> registry,
            Func<AuthoritativeHistoryRuntime> history,
            Func<IEnumerable<string>> knownPeople,
            string owner)
        {
            knowledgeProvider = knowledge;
            memoryProvider = memories;
            ensurePeople = ensure;
            registryProvider = registry;
            historyProvider = history;
            knownPeopleProvider = knownPeople;
            ownerId = string.IsNullOrWhiteSpace(owner) ? PersistenceService.LocalWorldId : owner;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => WorldSocialCognitionSaveData.CurrentSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Notification;
        public int LoadPriority => 96;
        public IReadOnlyList<string> RequiredDependencies => new[] { AuthoritativeHistoryPersistenceParticipant.Key };
        public IReadOnlyList<string> OptionalDependencies => new[] { RumorPersistenceParticipant.Key, SocialInteractionPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            WorldSocialCognitionSaveData save = new WorldSocialCognitionSaveData
            {
                knowledge = (knowledgeProvider?.Invoke() ?? EmptyKnowledge()).Values
                    .Where(value => value != null)
                    .OrderBy(value => value.PersonId, StringComparer.Ordinal)
                    .Select(value => value.CreateSaveData())
                    .ToList(),
                memories = (memoryProvider?.Invoke() ?? EmptyMemories()).Values
                    .Where(value => value != null)
                    .OrderBy(value => value.PersonId, StringComparer.Ordinal)
                    .Select(value => value.CreateSaveData())
                    .ToList()
            };
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(save));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != WorldSocialCognitionSaveData.CurrentSchemaVersion || string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("World social cognition payload has an unsupported schema or is empty.");
            }

            WorldSocialCognitionSaveData save;
            try
            {
                save = PersistenceSerialization.Deserialize<WorldSocialCognitionSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("World social cognition payload is malformed JSON.");
            }

            if (save == null || save.schemaVersion != WorldSocialCognitionSaveData.CurrentSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure("World social cognition payload is invalid.");
            }

            string[] personIds = (save.knowledge ?? new List<PersonKnowledgeSaveData>()).Select(item => item?.personId)
                .Concat((save.memories ?? new List<PersonMemorySaveData>()).Select(item => item?.personId))
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            if (personIds.Length != (save.knowledge ?? new List<PersonKnowledgeSaveData>()).Count
                || personIds.Length != (save.memories ?? new List<PersonMemorySaveData>()).Count)
            {
                return PersistenceParticipantPrepareResult.Failure("World social cognition must contain exactly one knowledge and memory record per NPC.");
            }

            return PersistenceParticipantPrepareResult.Success(save);
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (preparedPayload is not WorldSocialCognitionSaveData save)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared world social cognition payload has the wrong type.");
            }

            string[] savedPeople = save.knowledge.Select(item => item.personId).ToArray();
            ensurePeople?.Invoke(savedPeople.Concat(knownPeopleProvider?.Invoke() ?? Array.Empty<string>()));
            IReadOnlyDictionary<string, PersonKnowledgeRuntime> knowledge = knowledgeProvider?.Invoke() ?? EmptyKnowledge();
            IReadOnlyDictionary<string, PersonMemoryRuntime> memories = memoryProvider?.Invoke() ?? EmptyMemories();
            DefinitionRegistry registry = registryProvider?.Invoke();
            AuthoritativeHistoryRuntime history = historyProvider?.Invoke();
            string[] known = (knownPeopleProvider?.Invoke() ?? Array.Empty<string>()).Concat(savedPeople).Distinct(StringComparer.Ordinal).ToArray();

            foreach (PersonKnowledgeSaveData record in save.knowledge)
            {
                if (!knowledge.TryGetValue(record.personId, out PersonKnowledgeRuntime runtime)
                    || !runtime.RestoreFromSaveData(record, registry, record.personId, restoring: true).Succeeded)
                {
                    return PersistenceParticipantCommitResult.Failure($"Failed to restore NPC knowledge for '{record.personId}'.");
                }
            }

            foreach (PersonMemorySaveData record in save.memories)
            {
                if (!memories.TryGetValue(record.personId, out PersonMemoryRuntime runtime)
                    || !runtime.RestoreFromSaveData(record, registry, history, known, restoring: true).Succeeded)
                {
                    return PersistenceParticipantCommitResult.Failure($"Failed to restore NPC memory for '{record.personId}'.");
                }
            }

            return PersistenceParticipantCommitResult.Success("NPC social knowledge and memory restored.");
        }

        public void DiscardPreparedPayload(object preparedPayload) { }

        private static IReadOnlyDictionary<string, PersonKnowledgeRuntime> EmptyKnowledge() => new Dictionary<string, PersonKnowledgeRuntime>();
        private static IReadOnlyDictionary<string, PersonMemoryRuntime> EmptyMemories() => new Dictionary<string, PersonMemoryRuntime>();
    }
}
