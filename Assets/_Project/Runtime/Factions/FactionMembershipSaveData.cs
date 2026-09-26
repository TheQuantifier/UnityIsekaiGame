using System;

namespace UnityIsekaiGame.Factions
{
    [Serializable]
    public struct FactionMembershipSaveData
    {
        public string factionId;
        public string personId;
        public string rankId;
        public string[] roleIds;
        public string membershipState;
        public int reputation;
    }
}
