namespace UnityIsekaiGame.Networking
{
    public enum NarrativeAuthorityCommandType : byte
    {
        None = 0,
        Interact = 1,
        BrowseQuestSource = 2,
        AcceptQuestListing = 3,
        AbandonQuest = 4,
        ClaimQuestReward = 5,
        SelectDialogueChoice = 6,
        EndDialogue = 7,
        CreateParty = 20,
        InvitePartyMember = 21,
        AcceptPartyInvitation = 22,
        DeclinePartyInvitation = 23,
        RemovePartyMember = 24,
        TransferPartyLeadership = 25,
        SetPartyReady = 26,
        SetPartySettings = 27,
        LeaveParty = 28,
        DissolveParty = 29
    }

    public enum NarrativeAuthorityFailure : byte
    {
        None = 0,
        InvalidCommand = 1,
        ReplayedCommand = 2,
        InvalidIdentifier = 3,
        OutOfRange = 4,
        InteractionUnavailable = 5,
        UnauthorizedContext = 6,
        QuestRejected = 7,
        DialogueRejected = 8,
        PartyRejected = 9,
        DeferredTransaction = 10,
        ServerRejected = 11
    }

    public enum NarrativePresentationAction : byte
    {
        None = 0,
        InteractionConfirmed = 1,
        OpenQuestSource = 2,
        OpenConversation = 3,
        OpenGuildDesk = 4,
        RefreshQuestJournal = 5,
        RefreshParty = 6,
        CloseConversation = 7
    }

    public static class NarrativeAuthorityLimits
    {
        public const int MaximumIdentifierBytes = 128;
        public const int MaximumDisplayNameBytes = 128;
        public const int MaximumResultMessageBytes = 480;
        public const float InteractionRangeTolerance = 0.35f;
        public const double InteractionAuthorizationSeconds = 20d;
    }
}
