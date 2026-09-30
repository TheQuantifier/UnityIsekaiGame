using System;
using System.Collections.Generic;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Gameplay
{
    [Serializable]
    public sealed class NarrativeSessionSnapshotData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string personId;
        public long revision;
        public string activeDialogueFlowId;
        public List<NarrativeQuestReplicaData> quests = new List<NarrativeQuestReplicaData>();
        public NarrativePartyReplicaData party;
        public List<NarrativePartyInvitationReplicaData> invitations = new List<NarrativePartyInvitationReplicaData>();
    }

    [Serializable]
    public sealed class NarrativeQuestReplicaData
    {
        public QuestAssignmentRecordData assignment;
        public QuestRecordData quest;
        public List<QuestObjectiveRecordData> objectives = new List<QuestObjectiveRecordData>();
        public QuestTerminalOutcomeRecordData outcome;
        public List<QuestRewardEntitlementRecordData> rewards = new List<QuestRewardEntitlementRecordData>();
    }

    [Serializable]
    public sealed class NarrativePartyReplicaData
    {
        public string partyId;
        public string displayName;
        public int minimumOperationalMembers;
        public int maximumMembers;
        public int formation;
        public int lootPolicy;
        public int friendlyFirePolicy;
        public int groupCommand;
        public List<NarrativePartyMemberReplicaData> members = new List<NarrativePartyMemberReplicaData>();
    }

    [Serializable]
    public sealed class NarrativePartyMemberReplicaData
    {
        public string personId;
        public string membershipId;
        public bool isLeader;
        public int readiness;
        public string locationId;
        public float distanceToLeader;
        public bool connected;
        public bool alive;
        public bool conscious;
    }

    [Serializable]
    public sealed class NarrativePartyInvitationReplicaData
    {
        public string invitationId;
        public string partyId;
        public string inviterPersonId;
        public string invitedPersonId;
        public int status;
    }
}
