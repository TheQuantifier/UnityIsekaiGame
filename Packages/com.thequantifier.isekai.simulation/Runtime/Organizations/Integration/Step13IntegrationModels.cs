using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Organizations.Integration
{
    public sealed class Step13IntegrationDiagnostic
    {
        public Step13IntegrationDiagnostic(
            Step13IntegrationDiagnosticSeverity severity,
            Step13IntegrationDiagnosticDomain domain,
            string code,
            string message,
            string subjectId = "",
            string correctiveAction = "")
        {
            Severity = severity;
            Domain = domain;
            Code = Clean(code);
            Message = Clean(message);
            SubjectId = Clean(subjectId);
            CorrectiveAction = Clean(correctiveAction);
        }

        public Step13IntegrationDiagnosticSeverity Severity { get; }
        public Step13IntegrationDiagnosticDomain Domain { get; }
        public string Code { get; }
        public string Message { get; }
        public string SubjectId { get; }
        public string CorrectiveAction { get; }

        public override string ToString()
        {
            string subject = string.IsNullOrWhiteSpace(SubjectId) ? string.Empty : $"{SubjectId}: ";
            string action = string.IsNullOrWhiteSpace(CorrectiveAction) ? string.Empty : $" Action={CorrectiveAction}";
            return $"{Severity}: {Domain}/{Code}: {subject}{Message}{action}";
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13IntegrationValidationReport
    {
        private readonly List<Step13IntegrationDiagnostic> diagnostics = new List<Step13IntegrationDiagnostic>();

        public IReadOnlyList<Step13IntegrationDiagnostic> Diagnostics => diagnostics.ToArray();
        public int ErrorCount => diagnostics.Count(item => item.Severity == Step13IntegrationDiagnosticSeverity.Error);
        public int WarningCount => diagnostics.Count(item => item.Severity == Step13IntegrationDiagnosticSeverity.Warning);
        public int InfoCount => diagnostics.Count(item => item.Severity == Step13IntegrationDiagnosticSeverity.Info);
        public bool Succeeded => ErrorCount == 0;

        public void Add(Step13IntegrationDiagnosticSeverity severity, Step13IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string correctiveAction = "")
        {
            diagnostics.Add(new Step13IntegrationDiagnostic(severity, domain, code, message, subjectId, correctiveAction));
        }

        public void AddError(Step13IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string correctiveAction = "")
        {
            Add(Step13IntegrationDiagnosticSeverity.Error, domain, code, message, subjectId, correctiveAction);
        }

        public void AddWarning(Step13IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string correctiveAction = "")
        {
            Add(Step13IntegrationDiagnosticSeverity.Warning, domain, code, message, subjectId, correctiveAction);
        }

        public void AddInfo(Step13IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "")
        {
            Add(Step13IntegrationDiagnosticSeverity.Info, domain, code, message, subjectId);
        }

        public string ToSummary()
        {
            return $"Errors={ErrorCount} Warnings={WarningCount} Info={InfoCount}";
        }
    }

    public sealed class Step13OwnershipEntry
    {
        public Step13OwnershipEntry(string domainId, string featureId, string displayName, string authoritativeRuntime, bool derived = false, params string[] readOnlyDependents)
        {
            DomainId = Clean(domainId);
            FeatureId = Clean(featureId);
            DisplayName = Clean(displayName);
            AuthoritativeRuntime = Clean(authoritativeRuntime);
            Derived = derived;
            ReadOnlyDependents = Ordered(readOnlyDependents);
        }

        public string DomainId { get; }
        public string FeatureId { get; }
        public string DisplayName { get; }
        public string AuthoritativeRuntime { get; }
        public bool Derived { get; }
        public IReadOnlyList<string> ReadOnlyDependents { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Ordered(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class Step13PersistenceDependencyEntry
    {
        public Step13PersistenceDependencyEntry(string participantKey, params string[] dependsOn)
        {
            ParticipantKey = Clean(participantKey);
            DependsOn = Ordered(dependsOn);
        }

        public string ParticipantKey { get; }
        public IReadOnlyList<string> DependsOn { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Ordered(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class Step13RuntimeSummary
    {
        public Step13RuntimeSummary(string runtimeName, string persistenceKey, bool present, bool ready, long revision, int primaryCount, int secondaryCount = 0, int tertiaryCount = 0)
        {
            RuntimeName = Clean(runtimeName);
            PersistenceKey = Clean(persistenceKey);
            Present = present;
            Ready = ready;
            Revision = Math.Max(0L, revision);
            PrimaryCount = Math.Max(0, primaryCount);
            SecondaryCount = Math.Max(0, secondaryCount);
            TertiaryCount = Math.Max(0, tertiaryCount);
        }

        public string RuntimeName { get; }
        public string PersistenceKey { get; }
        public bool Present { get; }
        public bool Ready { get; }
        public long Revision { get; }
        public int PrimaryCount { get; }
        public int SecondaryCount { get; }
        public int TertiaryCount { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13InstitutionalSubjectReference
    {
        public Step13InstitutionalSubjectReference(Step13InstitutionalSubjectType subjectType, string stableId, string worldId = "", string sourceRuntime = "", Step13ProjectionVisibility visibility = Step13ProjectionVisibility.Public)
        {
            SubjectType = subjectType;
            StableId = Clean(stableId);
            WorldId = Clean(worldId);
            SourceRuntime = Clean(sourceRuntime);
            Visibility = visibility;
        }

        public Step13InstitutionalSubjectType SubjectType { get; }
        public string StableId { get; }
        public string WorldId { get; }
        public string SourceRuntime { get; }
        public Step13ProjectionVisibility Visibility { get; }
        public bool IsValid => SubjectType != Step13InstitutionalSubjectType.Unknown && !string.IsNullOrWhiteSpace(StableId);

        public override string ToString() => $"{SubjectType}:{StableId}";

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13InstitutionalActionContext
    {
        public Step13InstitutionalActionContext(
            string actingPersonId,
            string representedOrganizationId,
            string representedGovernmentId,
            string officeAssignmentId,
            string authorityGrantId,
            Step13InstitutionalSubjectReference target,
            string actionDefinitionId,
            string placeId,
            string territoryId,
            string jurisdictionId,
            string legalSubjectMatterId,
            string sourceProposalId,
            string sourceTreatyId,
            string sourceIncidentId,
            string sourceWarrantId,
            double worldTime,
            string provenanceId = "",
            Step13ProjectionVisibility visibility = Step13ProjectionVisibility.Official)
        {
            ActingPersonId = Clean(actingPersonId);
            RepresentedOrganizationId = Clean(representedOrganizationId);
            RepresentedGovernmentId = Clean(representedGovernmentId);
            OfficeAssignmentId = Clean(officeAssignmentId);
            AuthorityGrantId = Clean(authorityGrantId);
            Target = target ?? new Step13InstitutionalSubjectReference(Step13InstitutionalSubjectType.Unknown, string.Empty);
            ActionDefinitionId = Clean(actionDefinitionId);
            PlaceId = Clean(placeId);
            TerritoryId = Clean(territoryId);
            JurisdictionId = Clean(jurisdictionId);
            LegalSubjectMatterId = Clean(legalSubjectMatterId);
            SourceProposalId = Clean(sourceProposalId);
            SourceTreatyId = Clean(sourceTreatyId);
            SourceIncidentId = Clean(sourceIncidentId);
            SourceWarrantId = Clean(sourceWarrantId);
            WorldTime = Math.Max(0d, worldTime);
            ProvenanceId = Clean(provenanceId);
            Visibility = visibility;
        }

        public string ActingPersonId { get; }
        public string RepresentedOrganizationId { get; }
        public string RepresentedGovernmentId { get; }
        public string OfficeAssignmentId { get; }
        public string AuthorityGrantId { get; }
        public Step13InstitutionalSubjectReference Target { get; }
        public string ActionDefinitionId { get; }
        public string PlaceId { get; }
        public string TerritoryId { get; }
        public string JurisdictionId { get; }
        public string LegalSubjectMatterId { get; }
        public string SourceProposalId { get; }
        public string SourceTreatyId { get; }
        public string SourceIncidentId { get; }
        public string SourceWarrantId { get; }
        public double WorldTime { get; }
        public string ProvenanceId { get; }
        public Step13ProjectionVisibility Visibility { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13ActionGateResult
    {
        public Step13ActionGateResult(Step13ActionGate gate, bool succeeded, string code, string message, string sourceRuntime = "", string sourceRecordId = "", long revision = 0)
        {
            Gate = gate;
            Succeeded = succeeded;
            Code = Clean(code);
            Message = Clean(message);
            SourceRuntime = Clean(sourceRuntime);
            SourceRecordId = Clean(sourceRecordId);
            Revision = Math.Max(0L, revision);
        }

        public Step13ActionGate Gate { get; }
        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public string SourceRuntime { get; }
        public string SourceRecordId { get; }
        public long Revision { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13ActionEvaluationResult
    {
        private readonly Step13ActionGateResult[] gates;

        public Step13ActionEvaluationResult(IEnumerable<Step13ActionGateResult> gates, string fingerprint)
        {
            this.gates = (gates ?? Array.Empty<Step13ActionGateResult>()).Where(item => item != null).OrderBy(item => item.Gate).ThenBy(item => item.SourceRuntime, StringComparer.Ordinal).ThenBy(item => item.SourceRecordId, StringComparer.Ordinal).ToArray();
            Fingerprint = Clean(fingerprint);
        }

        public IReadOnlyList<Step13ActionGateResult> Gates => gates.ToArray();
        public IReadOnlyList<Step13ActionGateResult> FailedGates => gates.Where(item => !item.Succeeded).ToArray();
        public bool Executable => gates.Length > 0 && gates.All(item => item.Succeeded);
        public string Fingerprint { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13ContextRecordReference
    {
        public Step13ContextRecordReference(string runtimeName, string recordId, Step13InstitutionalProjectionState projectionState, Step13ProjectionVisibility visibility, string summary = "")
        {
            RuntimeName = Clean(runtimeName);
            RecordId = Clean(recordId);
            ProjectionState = projectionState;
            Visibility = visibility;
            Summary = Clean(summary);
        }

        public string RuntimeName { get; }
        public string RecordId { get; }
        public Step13InstitutionalProjectionState ProjectionState { get; }
        public Step13ProjectionVisibility Visibility { get; }
        public string Summary { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13InstitutionalContextOptions
    {
        public int MaxOrganizations { get; set; } = 12;
        public int MaxMemberships { get; set; } = 12;
        public int MaxAuthority { get; set; } = 12;
        public int MaxResources { get; set; } = 12;
        public int MaxDecisions { get; set; } = 12;
        public int MaxFactions { get; set; } = 12;
        public int MaxDiplomacy { get; set; } = 12;
        public int MaxGovernments { get; set; } = 12;
        public int MaxLaws { get; set; } = 12;
        public int MaxCrimes { get; set; } = 12;
        public int MaxJustice { get; set; } = 12;
        public bool Privileged { get; set; }

        public Step13InstitutionalContextOptions Clone()
        {
            return new Step13InstitutionalContextOptions
            {
                MaxOrganizations = Math.Max(0, MaxOrganizations),
                MaxMemberships = Math.Max(0, MaxMemberships),
                MaxAuthority = Math.Max(0, MaxAuthority),
                MaxResources = Math.Max(0, MaxResources),
                MaxDecisions = Math.Max(0, MaxDecisions),
                MaxFactions = Math.Max(0, MaxFactions),
                MaxDiplomacy = Math.Max(0, MaxDiplomacy),
                MaxGovernments = Math.Max(0, MaxGovernments),
                MaxLaws = Math.Max(0, MaxLaws),
                MaxCrimes = Math.Max(0, MaxCrimes),
                MaxJustice = Math.Max(0, MaxJustice),
                Privileged = Privileged
            };
        }
    }

    public sealed class Step13InstitutionalContextSnapshot
    {
        private readonly Step13ContextRecordReference[] records;
        private readonly Step13RuntimeSummary[] sourceRuntimes;
        private readonly string[] diagnostics;

        public Step13InstitutionalContextSnapshot(
            string requesterPersonId,
            string actorPersonId,
            string organizationId,
            string governmentId,
            string placeId,
            double worldTime,
            IEnumerable<Step13ContextRecordReference> records,
            IEnumerable<Step13RuntimeSummary> sourceRuntimes,
            IEnumerable<string> diagnostics,
            bool truncated,
            string fingerprint)
        {
            RequesterPersonId = Clean(requesterPersonId);
            ActorPersonId = Clean(actorPersonId);
            OrganizationId = Clean(organizationId);
            GovernmentId = Clean(governmentId);
            PlaceId = Clean(placeId);
            WorldTime = Math.Max(0d, worldTime);
            this.records = (records ?? Array.Empty<Step13ContextRecordReference>()).Where(item => item != null).OrderBy(item => item.RuntimeName, StringComparer.Ordinal).ThenBy(item => item.RecordId, StringComparer.Ordinal).ToArray();
            this.sourceRuntimes = (sourceRuntimes ?? Array.Empty<Step13RuntimeSummary>()).Where(item => item != null).OrderBy(item => item.RuntimeName, StringComparer.Ordinal).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Truncated = truncated;
            Fingerprint = Clean(fingerprint);
        }

        public string RequesterPersonId { get; }
        public string ActorPersonId { get; }
        public string OrganizationId { get; }
        public string GovernmentId { get; }
        public string PlaceId { get; }
        public double WorldTime { get; }
        public IReadOnlyList<Step13ContextRecordReference> Records => records.ToArray();
        public IReadOnlyList<Step13RuntimeSummary> SourceRuntimes => sourceRuntimes.ToArray();
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();
        public bool Truncated { get; }
        public string Fingerprint { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13ReadinessSnapshot
    {
        private readonly Step13RuntimeSummary[] runtimes;
        private readonly Step13OwnershipEntry[] ownershipMap;
        private readonly Step13PersistenceDependencyEntry[] persistenceDependencies;
        private readonly Step13IntegrationDiagnostic[] diagnostics;

        public Step13ReadinessSnapshot(
            Step13IntegrationHealthStatus status,
            IEnumerable<Step13RuntimeSummary> runtimes,
            IEnumerable<Step13OwnershipEntry> ownershipMap,
            IEnumerable<Step13PersistenceDependencyEntry> persistenceDependencies,
            IEnumerable<Step13IntegrationDiagnostic> diagnostics,
            string worldId,
            long revision,
            string fingerprint)
        {
            Status = status;
            this.runtimes = (runtimes ?? Array.Empty<Step13RuntimeSummary>()).Where(item => item != null).OrderBy(item => item.RuntimeName, StringComparer.Ordinal).ToArray();
            this.ownershipMap = (ownershipMap ?? Array.Empty<Step13OwnershipEntry>()).Where(item => item != null).OrderBy(item => item.DomainId, StringComparer.Ordinal).ToArray();
            this.persistenceDependencies = (persistenceDependencies ?? Array.Empty<Step13PersistenceDependencyEntry>()).Where(item => item != null).OrderBy(item => item.ParticipantKey, StringComparer.Ordinal).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<Step13IntegrationDiagnostic>()).Where(item => item != null).ToArray();
            WorldId = Clean(worldId);
            Revision = Math.Max(0L, revision);
            Fingerprint = Clean(fingerprint);
        }

        public Step13IntegrationHealthStatus Status { get; }
        public IReadOnlyList<Step13RuntimeSummary> Runtimes => runtimes.ToArray();
        public IReadOnlyList<Step13OwnershipEntry> OwnershipMap => ownershipMap.ToArray();
        public IReadOnlyList<Step13PersistenceDependencyEntry> PersistenceDependencies => persistenceDependencies.ToArray();
        public IReadOnlyList<Step13IntegrationDiagnostic> Diagnostics => diagnostics.ToArray();
        public string WorldId { get; }
        public long Revision { get; }
        public string Fingerprint { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13TransactionParticipantPlan
    {
        public Step13TransactionParticipantPlan(string runtimeName, Step13TransactionFailurePolicy failurePolicy, Func<bool> preview, Func<bool> prepare, Func<bool> commit, Func<bool> rollback, Func<bool> postCommit = null)
        {
            RuntimeName = Clean(runtimeName);
            FailurePolicy = failurePolicy;
            Preview = preview;
            Prepare = prepare;
            Commit = commit;
            Rollback = rollback;
            PostCommit = postCommit;
        }

        public string RuntimeName { get; }
        public Step13TransactionFailurePolicy FailurePolicy { get; }
        public Func<bool> Preview { get; }
        public Func<bool> Prepare { get; }
        public Func<bool> Commit { get; }
        public Func<bool> Rollback { get; }
        public Func<bool> PostCommit { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13TransactionParticipantResult
    {
        public Step13TransactionParticipantResult(string runtimeName, Step13TransactionStage stage, bool succeeded, Step13TransactionFailurePolicy failurePolicy, string message = "")
        {
            RuntimeName = string.IsNullOrWhiteSpace(runtimeName) ? string.Empty : runtimeName.Trim();
            Stage = stage;
            Succeeded = succeeded;
            FailurePolicy = failurePolicy;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public string RuntimeName { get; }
        public Step13TransactionStage Stage { get; }
        public bool Succeeded { get; }
        public Step13TransactionFailurePolicy FailurePolicy { get; }
        public string Message { get; }
    }

    public sealed class Step13TransactionResult
    {
        private readonly Step13TransactionParticipantResult[] participants;
        private readonly string[] diagnostics;

        public Step13TransactionResult(bool succeeded, string transactionId, bool preview, bool duplicate, IEnumerable<Step13TransactionParticipantResult> participants, IEnumerable<string> diagnostics)
        {
            Succeeded = succeeded;
            TransactionId = string.IsNullOrWhiteSpace(transactionId) ? string.Empty : transactionId.Trim();
            Preview = preview;
            Duplicate = duplicate;
            this.participants = (participants ?? Array.Empty<Step13TransactionParticipantResult>()).Where(item => item != null).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        }

        public bool Succeeded { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public IReadOnlyList<Step13TransactionParticipantResult> Participants => participants.ToArray();
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();
    }

    public sealed class Step13SchedulerBudget
    {
        public int MaximumEvaluationsPerTick { get; set; } = 64;
        public int MaximumTraversalDepth { get; set; } = 6;
        public int MaximumQueuedInstitutionalConsequences { get; set; } = 128;
        public bool UseSystemTime { get; set; }
        public bool AllowImmediateRecursiveDispatch { get; set; }
    }
}
