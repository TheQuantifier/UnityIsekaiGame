namespace UnityIsekaiGame.Social.Integration
{
    public enum Step12IntegrationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum Step12IntegrationDiagnosticDomain
    {
        Authority,
        DefinitionCatalog,
        RuntimeReadiness,
        RuntimeGraph,
        Persistence,
        Transaction,
        Context,
        Visibility,
        Projection,
        Scheduler,
        Recursion,
        Determinism,
        Snapshot,
        Boundary,
        Documentation
    }

    public enum Step12SocialVisibility
    {
        Public,
        Observable,
        ParticipantKnown,
        FamilyKnown,
        MemberVisible,
        Confidential,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum Step12SocialProjectionState
    {
        AuthoritativeFact,
        KnownFact,
        BelievedClaim,
        InferredState,
        RumoredState,
        DisputedState,
        HiddenState,
        UnknownState,
        DiagnosticOnly
    }

    public enum Step12TransactionStage
    {
        Preview,
        Prepare,
        Commit,
        Rollback,
        PostCommit
    }

    public enum Step12TransactionFailurePolicy
    {
        Required,
        Optional,
        DiagnosticOnly
    }

    public enum Step12HealthStatus
    {
        Ready,
        Degraded,
        Failed
    }
}
