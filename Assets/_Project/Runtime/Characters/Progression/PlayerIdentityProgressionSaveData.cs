using System.Collections.Generic;

namespace UnityIsekaiGame.Progression
{
    [System.Serializable]
    public sealed class PlayerIdentityProgressionSaveData
    {
        public int schemaVersion = PlayerIdentityProgressionPersistenceParticipant.CurrentParticipantSchemaVersion;
        public string accountId;
        public string playerId;
        public string personId;
        public string currentWorldEntityId;
        public string accountCreatedAtUtc;
        public double cumulativeActivePlaytimeSeconds;
        public RuntimeOriginAssignmentRecord origin;
        public RuntimeBirthGiftRecord birthGift;
        public List<RuntimePermanentAttributeGrantRecord> permanentAttributeGrants = new List<RuntimePermanentAttributeGrantRecord>();
        public List<RuntimeRoleRecord> roles = new List<RuntimeRoleRecord>();
        public List<RuntimeSocialStatusRecord> socialStatuses = new List<RuntimeSocialStatusRecord>();
        public List<RuntimeTitleRecord> titles = new List<RuntimeTitleRecord>();
        public List<WalletBalanceRecord> walletBalances = new List<WalletBalanceRecord>();
        public List<string> learnedCapabilityIds = new List<string>();
        public List<ActivityOutcomeRecord> activityRecords = new List<ActivityOutcomeRecord>();
        public List<ParticipationRecord> participationRecords = new List<ParticipationRecord>();
    }
}
