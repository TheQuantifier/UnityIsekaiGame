namespace UnityIsekaiGame.Dialogue
{
    public enum ConversationCategory
    {
        Unknown,
        General,
        QuestOffer,
        QuestTurnIn,
        QuestBoardClarification,
        MerchantService,
        GovernmentOffice,
        GuildOffice,
        RecordsInquiry,
        PrisonerInterview,
        PrivateAudience,
        GroupDiscussion,
        Diagnostic,
        Custom
    }

    public enum ConversationLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Suspended,
        Completed,
        Cancelled,
        Interrupted,
        Expired,
        Historical,
        Invalid
    }

    public enum ConversationVisibility
    {
        Unknown,
        Public,
        LocallyKnown,
        ParticipantKnown,
        OrganizationMembers,
        GovernmentOfficial,
        OfficeRestricted,
        Private,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum ConversationParticipantRole
    {
        Unknown,
        Initiator,
        Addressee,
        Speaker,
        Listener,
        Provider,
        Interpreter,
        Witness,
        Guard,
        Moderator,
        OrganizationRepresentative,
        OfficeHolder,
        QuestGiver,
        QuestRecipient,
        Merchant,
        Prisoner,
        Custom
    }

    public enum ConversationProviderRequirementKind
    {
        Unknown,
        None,
        Person,
        Organization,
        OrganizationMembership,
        Office,
        Authority,
        Government,
        Faction,
        Business,
        Custom
    }

    public enum ConversationCoLocationPolicy
    {
        Unknown,
        NotRequired,
        SameLocation,
        SameInteractionPoint,
        RemoteAllowed,
        PrivilegedBypass
    }

    public enum ConversationOverlapPolicy
    {
        Unknown,
        AllowConcurrent,
        PreventParticipantOverlap,
        PreventProviderOverlap
    }

    public enum ConversationAccessLevel
    {
        Unknown,
        Public,
        Participant,
        ControllingEntity,
        PrivilegedDiagnostic
    }

    public enum ConversationSubjectRole
    {
        Unknown,
        Quest,
        QuestSource,
        QuestListing,
        Location,
        InteractionPoint,
        Organization,
        Office,
        Person,
        Information,
        SocialContext,
        Custom
    }

    public enum ConversationEventKind
    {
        Unknown,
        ConversationStarted,
        ConversationLifecycleChanged,
        ParticipantAdded,
        ParticipantRemoved,
        ActiveSpeakerChanged,
        Restore
    }

    public enum ConversationOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingDefinition,
        MissingParticipant,
        MissingProvider,
        MissingContext,
        CoLocationRejected,
        OverlapRejected,
        VisibilityDenied,
        RevisionConflict,
        WrongWorld,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }
}
