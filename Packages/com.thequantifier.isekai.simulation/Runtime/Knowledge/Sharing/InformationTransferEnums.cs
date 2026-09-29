namespace UnityIsekaiGame.Knowledge.Sharing
{
    public enum InformationTransferMode
    {
        Unknown,
        DirectTestimony,
        ConversationStatement,
        PrivateMessage,
        PublicAnnouncement,
        WrittenMessage,
        Letter,
        BookReading,
        Report,
        FormalLesson,
        InformalTeaching,
        Demonstration,
        GuidedPractice,
        Lecture,
        QuestionAndAnswer,
        Warning,
        Instruction,
        Explanation,
        Translation,
        Summary,
        Copy,
        Recitation,
        RumorRetelling,
        MagicalCommunication,
        TechnologicalCommunication,
        Custom
    }

    public enum InformationTransferContentType
    {
        Unknown,
        FactReference,
        Proposition,
        BeliefStatement,
        EvidenceReference,
        HistoricalEventReference,
        LifeEventReference,
        MemoryStatement,
        SourceAssessment,
        SourceIdentity,
        LocationInformation,
        PersonIdentity,
        BodyIdentity,
        OrganizationInformation,
        ConditionOrDiagnosis,
        InstructionalConcept,
        ProcedureReference,
        SkillConcept,
        Warning,
        Custom
    }

    public enum InformationTransferAssertionType
    {
        Fact,
        Possibility,
        Opinion,
        Instruction,
        Warning,
        Question,
        Correction,
        Retraction
    }

    public enum InformationTransferStatus
    {
        Succeeded,
        Preview,
        PartialSuccess,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingSender,
        MissingRecipient,
        MissingContent,
        SenderAccessDenied,
        RecallFailed,
        SourceFailure,
        KnowledgeRejected,
        MemoryRejected,
        PrivacyBlocked,
        CircularChain,
        RestoreFailed,
        ValidationFailed
    }

    public enum TransferUnderstandingState
    {
        Complete,
        Partial,
        Ambiguous,
        Misinterpreted,
        Unsupported,
        DomainInsufficient,
        TranslationLimited,
        TerminologyLimited,
        ContextLimited,
        Rejected,
        Deferred
    }

    public enum TransferPrivacyScope
    {
        Public,
        RecipientOnly,
        Private,
        Confidential,
        Restricted,
        HiddenSource,
        Secret
    }

    public enum TransferMemoryPolicy
    {
        None,
        FormCommunicationMemory,
        ReinforceExisting,
        FormOrReinforce
    }

    public enum TransferEvidencePolicy
    {
        None,
        CreateRecipientEvidence,
        CreateOnlyIfUnderstood,
        CreateCorrectionEvidence
    }

    public enum TransferInheritedConfidencePolicy
    {
        RawOnly,
        SourceReliabilityAdjusted,
        UnderstandingAdjusted,
        TeachingAdjusted
    }
}
