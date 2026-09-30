using System;
using System.Collections.Generic;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Parties;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PartyOperationalPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.party-operations";
        private readonly PartyOperationalRuntime runtime;
        private readonly string ownerId;
        public PartyOperationalPersistenceParticipant(PartyOperationalRuntime runtime, string ownerId) { this.runtime = runtime; this.ownerId = ownerId; }
        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => 1;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 106;
        public IReadOnlyList<string> RequiredDependencies => new[] { SocialNetworkPersistenceParticipant.Key };
        public IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload() => runtime == null ? PersistenceParticipantSaveResult.Failure("Party operational runtime is missing.") : PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(runtime.CreateSaveData()));
        public PersistenceParticipantPrepareResult PreparePayload(string json, int version)
        {
            if (version != ParticipantSchemaVersion || string.IsNullOrWhiteSpace(json)) return PersistenceParticipantPrepareResult.Failure("Party operational payload is empty or has an unsupported participant schema.");
            try
            {
                PartyOperationalSaveData data = PersistenceSerialization.Deserialize<PartyOperationalSaveData>(json);
                return PartyOperationalRuntime.TryMigrate(data, out PartyOperationalSaveData migrated, out string message) && PartyOperationalRuntime.Validate(migrated, out message)
                    ? PersistenceParticipantPrepareResult.Success(migrated.Clone())
                    : PersistenceParticipantPrepareResult.Failure(message);
            }
            catch { return PersistenceParticipantPrepareResult.Failure("Party operational payload is malformed JSON."); }
        }
        public PersistenceParticipantCommitResult CommitPreparedPayload(object prepared)
        {
            if (prepared is not PartyOperationalSaveData data || runtime == null) return PersistenceParticipantCommitResult.Failure("Prepared party operational payload has the wrong type.");
            PartyOperationalSaveData rollback = runtime.CreateSaveData();
            if (runtime.Restore(data, out string message)) return PersistenceParticipantCommitResult.Success(message);
            runtime.Restore(rollback, out _); return PersistenceParticipantCommitResult.Failure(message);
        }
        public void DiscardPreparedPayload(object preparedPayload) { }
    }
}
