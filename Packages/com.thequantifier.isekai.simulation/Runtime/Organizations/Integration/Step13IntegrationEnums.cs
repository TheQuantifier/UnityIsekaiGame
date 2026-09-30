namespace UnityIsekaiGame.Organizations.Integration
{
    public enum Step13IntegrationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum Step13IntegrationDiagnosticDomain
    {
        Ownership,
        Identity,
        DefinitionCatalog,
        RuntimeReadiness,
        RuntimeGraph,
        Persistence,
        Restore,
        Transaction,
        Authority,
        Jurisdiction,
        Legality,
        Domain,
        Consent,
        Visibility,
        Projection,
        Scheduler,
        Determinism,
        Snapshot,
        Boundary,
        Documentation
    }

    public enum Step13IntegrationHealthStatus
    {
        Uninitialized,
        Ready,
        Degraded,
        Failed,
        Resetting,
        Restoring,
        Disposed
    }

    public enum Step13InstitutionalSubjectType
    {
        Unknown,
        Person,
        Organization,
        Faction,
        Polity,
        Government,
        Territory,
        Place,
        Property,
        Business,
        Office,
        Membership,
        RankAssignment,
        LegalInstrument,
        LegalProvision,
        Incident,
        Warrant,
        Court,
        Case,
        Judgment,
        Sentence,
        Item,
        Inventory,
        Contract,
        HistoricalEvent
    }

    public enum Step13ProjectionVisibility
    {
        Official,
        Public,
        Participant,
        KnowledgeSafe,
        Privileged,
        Redacted,
        Concealed,
        Diagnostic
    }

    public enum Step13InstitutionalProjectionState
    {
        Authoritative,
        Current,
        Historical,
        Derived,
        Redacted,
        Concealed,
        Diagnostic
    }

    public enum Step13ActionGate
    {
        Identity,
        Authority,
        Jurisdiction,
        Legality,
        Domain,
        Consent,
        Resource,
        Timing,
        Prepared
    }

    public enum Step13TransactionStage
    {
        Preview,
        Prepare,
        Commit,
        Rollback,
        PostCommit
    }

    public enum Step13TransactionFailurePolicy
    {
        Required,
        Optional,
        DiagnosticOnly
    }
}
