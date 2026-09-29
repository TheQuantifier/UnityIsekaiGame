namespace UnityIsekaiGame.Gameplay
{
    public interface INarrativeAuthorityClientBridge
    {
        bool IsServerAuthorityActive { get; }
        bool IsApplyingReplica { get; }
        bool RequestBrowseQuestSource(string sourceId);
        bool RequestAcceptQuest(string listingId, string sourceId);
        bool RequestDialogueChoice(string choiceId);
        bool RequestEndDialogue();
    }

    public static class NarrativeAuthorityBridgeRegistry
    {
        public static INarrativeAuthorityClientBridge Active { get; set; }
    }
}
