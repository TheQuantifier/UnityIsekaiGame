namespace UnityIsekaiGame.Quests
{
    public enum QuestDefinitionRepeatabilityPolicy
    {
        Unknown,
        Unique,
        Reusable,
        RepeatablePerRecipient,
        RepeatablePerIssuer,
        RepeatablePerWorld,
        DynamicTemplate
    }

    public enum QuestDefinitionImportance
    {
        Unknown,
        Minor,
        Standard,
        Important,
        Major,
        Critical
    }

    public enum QuestSourceChannel
    {
        Unknown,
        Manual,
        QuestBoard,
        Dialogue,
        Contract,
        Organization,
        Government,
        Discovery,
        WorldEvent,
        System,
        Custom
    }

    public enum QuestIssuerType
    {
        Unknown,
        Person,
        Organization,
        Office,
        Government,
        Faction,
        Business,
        System,
        Anonymous,
        Custom
    }

    public enum QuestRecipientScope
    {
        Unknown,
        Open,
        Person,
        OrganizationMembers,
        OrganizationRank,
        Officeholder,
        Profession,
        FactionMembers,
        Citizens,
        Party,
        MultiplePersons,
        Custom
    }

    public enum QuestRuntimeLifecycleState
    {
        Unknown,
        Draft,
        Instantiated,
        Available,
        Unavailable,
        Suspended,
        Retired,
        Historical,
        Invalid
    }

    public enum QuestVisibility
    {
        Unknown,
        Public,
        LocallyKnown,
        OrganizationKnown,
        MemberKnown,
        GovernmentKnown,
        RecipientKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic,
        Development
    }

    public enum QuestVisibilityAccess
    {
        PublicOnly,
        LocalKnowledge,
        OrganizationMember,
        Government,
        Recipient,
        PrivilegedDiagnostic
    }

    public enum QuestSubjectRole
    {
        Unknown,
        PrimaryTarget,
        Person,
        Item,
        Organization,
        Government,
        Location,
        Incident,
        Journey,
        Encounter,
        Contract,
        Profession,
        LegalMatter,
        Context,
        Custom
    }

    public enum QuestRuntimeEventKind
    {
        Unknown,
        Instantiated,
        LifecycleChanged,
        Retired,
        MetadataCorrected
    }

    public enum QuestRuntimeOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingDefinition,
        DuplicateQuestId,
        UniqueQuestAlreadyExists,
        MultipleInstancesNotAllowed,
        WrongWorld,
        MissingQuest,
        InvalidIssuer,
        InvalidRecipient,
        InvalidOrigin,
        InvalidSubjectLink,
        InvalidLifecycleTransition,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }
}
