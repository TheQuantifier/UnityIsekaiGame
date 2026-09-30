using System;

namespace UnityIsekaiGame.StatusEffects
{
    [Serializable]
    public sealed class StatusEffectSaveData
    {
        public string statusDefinitionId;
        public string applicationId;
        public string sourceId;
        public float remainingDuration;
        public float elapsedDuration;
        public int stackCount;
        public StatusDurationModel durationModel;
        public StatusPersistencePolicy persistencePolicy;
        public float tickProgress;
    }
}
