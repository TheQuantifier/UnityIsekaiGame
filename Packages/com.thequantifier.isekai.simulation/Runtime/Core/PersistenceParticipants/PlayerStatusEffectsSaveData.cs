using System;
using System.Collections.Generic;
using UnityIsekaiGame.StatusEffects;

namespace UnityIsekaiGame.Persistence
{
    [Serializable]
    public sealed class PlayerStatusEffectsSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string actorProfileId = string.Empty;
        public List<StatusEffectSaveData> statuses = new List<StatusEffectSaveData>();
    }
}
