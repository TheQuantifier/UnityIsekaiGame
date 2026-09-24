using System;

namespace UnityIsekaiGame.ActorLifecycle
{
    [Serializable]
    public sealed class ActorLifecycleSaveData
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string personId;
        public string actorId;
        public string policyId;
        public string lifecycleState;
        public string diedAtUtc;
        public string revivalAvailableAtUtc;
    }
}
