using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Combat.OngoingEffects
{
    [Serializable]
    public sealed class OngoingEffectInstanceSaveData
    {
        public string instanceId;
        public string definitionId;
        public string sourceActorId;
        public string targetActorId;
        public string originId;
        public string applicationTransactionId;
        public float amountPerTick;
        public float tickInterval;
        public float totalDuration;
        public int finiteTickCount;
        public float elapsedSeconds;
        public float nextTickElapsedSeconds;
        public int completedTicks;
        public int stackCount;
        public string state;
        public long revision;
    }

    [Serializable]
    public sealed class OngoingEffectsSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string targetActorId;
        public List<OngoingEffectInstanceSaveData> instances = new List<OngoingEffectInstanceSaveData>();
    }
}
