using System;

namespace UnityIsekaiGame.Factions
{
    [Serializable]
    public struct FactionReferenceSaveData
    {
        public string factionId;

        public bool HasValidId => !string.IsNullOrWhiteSpace(factionId) && factionId.StartsWith("faction.");
    }
}
