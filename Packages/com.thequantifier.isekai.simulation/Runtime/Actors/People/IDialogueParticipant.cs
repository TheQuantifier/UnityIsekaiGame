namespace UnityIsekaiGame.People
{
    public interface IDialogueParticipant : IPersonCapability
    {
        string DialogueDisplayName { get; }
    }
}
