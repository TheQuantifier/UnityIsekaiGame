using System;

namespace UnityIsekaiGame.Beings.Biology.Anatomy
{
    [Serializable]
    public sealed class AnatomyPresenceOverrideData
    {
        public string nodeId;
        public AnatomyPresenceState presence;
    }

    [Serializable]
    public sealed class AnatomySaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public string anatomyDefinitionId;
        public long anatomyRevision;
        public AnatomyPresenceOverrideData[] presenceOverrides;
    }
}
