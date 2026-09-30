using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Social.Integration
{
    public sealed class Step12IntegrationDiagnostic
    {
        public Step12IntegrationDiagnostic(
            Step12IntegrationDiagnosticSeverity severity,
            Step12IntegrationDiagnosticDomain domain,
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

        public Step12IntegrationDiagnosticSeverity Severity { get; }
        public Step12IntegrationDiagnosticDomain Domain { get; }
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

    public sealed class Step12IntegrationValidationReport
    {
        private readonly List<Step12IntegrationDiagnostic> diagnostics = new List<Step12IntegrationDiagnostic>();

        public IReadOnlyList<Step12IntegrationDiagnostic> Diagnostics => diagnostics.ToArray();
        public int ErrorCount => diagnostics.Count(item => item.Severity == Step12IntegrationDiagnosticSeverity.Error);
        public int WarningCount => diagnostics.Count(item => item.Severity == Step12IntegrationDiagnosticSeverity.Warning);
        public int InfoCount => diagnostics.Count(item => item.Severity == Step12IntegrationDiagnosticSeverity.Info);
        public bool Succeeded => ErrorCount == 0;

        public void Add(Step12IntegrationDiagnosticSeverity severity, Step12IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string correctiveAction = "")
        {
            diagnostics.Add(new Step12IntegrationDiagnostic(severity, domain, code, message, subjectId, correctiveAction));
        }

        public void AddError(Step12IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string correctiveAction = "")
        {
            Add(Step12IntegrationDiagnosticSeverity.Error, domain, code, message, subjectId, correctiveAction);
        }

        public void AddWarning(Step12IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string correctiveAction = "")
        {
            Add(Step12IntegrationDiagnosticSeverity.Warning, domain, code, message, subjectId, correctiveAction);
        }

        public string ToSummary()
        {
            return $"Errors={ErrorCount} Warnings={WarningCount} Info={InfoCount}";
        }
    }

    public sealed class Step12AuthorityEntry
    {
        public Step12AuthorityEntry(string domainId, string featureId, string displayName, string authoritativeRuntime, bool derived = false, params string[] readOnlyDependents)
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

    public sealed class Step12PersistenceDependencyEntry
    {
        public Step12PersistenceDependencyEntry(string participantKey, params string[] dependsOn)
        {
            ParticipantKey = string.IsNullOrWhiteSpace(participantKey) ? string.Empty : participantKey.Trim();
            DependsOn = (dependsOn ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public string ParticipantKey { get; }
        public IReadOnlyList<string> DependsOn { get; }
    }

    public sealed class Step12RuntimeSummary
    {
        public Step12RuntimeSummary(string runtimeName, string persistenceKey, bool present, bool ready, long revision, int primaryCount, int secondaryCount = 0, int tertiaryCount = 0)
        {
            RuntimeName = string.IsNullOrWhiteSpace(runtimeName) ? string.Empty : runtimeName.Trim();
            PersistenceKey = string.IsNullOrWhiteSpace(persistenceKey) ? string.Empty : persistenceKey.Trim();
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
    }

    public sealed class Step12SocialContextOptions
    {
        public int MaxRelationships { get; set; } = 12;
        public int MaxAttitudes { get; set; } = 12;
        public int MaxReputations { get; set; } = 12;
        public int MaxRumors { get; set; } = 12;
        public int MaxInteractions { get; set; } = 12;
        public int MaxNorms { get; set; } = 12;
        public int MaxGroups { get; set; } = 12;
        public int MaxInfluenceAttempts { get; set; } = 12;
        public int MaxEmotions { get; set; } = 12;
        public int MaxHouseholds { get; set; } = 6;
        public bool Privileged { get; set; }

        public Step12SocialContextOptions Clone()
        {
            return new Step12SocialContextOptions
            {
                MaxRelationships = Math.Max(0, MaxRelationships),
                MaxAttitudes = Math.Max(0, MaxAttitudes),
                MaxReputations = Math.Max(0, MaxReputations),
                MaxRumors = Math.Max(0, MaxRumors),
                MaxInteractions = Math.Max(0, MaxInteractions),
                MaxNorms = Math.Max(0, MaxNorms),
                MaxGroups = Math.Max(0, MaxGroups),
                MaxInfluenceAttempts = Math.Max(0, MaxInfluenceAttempts),
                MaxEmotions = Math.Max(0, MaxEmotions),
                MaxHouseholds = Math.Max(0, MaxHouseholds),
                Privileged = Privileged
            };
        }
    }

    public sealed class Step12ContextRecordReference
    {
        public Step12ContextRecordReference(string runtimeName, string recordId, Step12SocialProjectionState projectionState, Step12SocialVisibility visibility = Step12SocialVisibility.Public, string summary = "")
        {
            RuntimeName = Clean(runtimeName);
            RecordId = Clean(recordId);
            ProjectionState = projectionState;
            Visibility = visibility;
            Summary = Clean(summary);
        }

        public string RuntimeName { get; }
        public string RecordId { get; }
        public Step12SocialProjectionState ProjectionState { get; }
        public Step12SocialVisibility Visibility { get; }
        public string Summary { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step12SocialContextSnapshot
    {
        private readonly Step12ContextRecordReference[] records;
        private readonly Step12RuntimeSummary[] sourceRuntimes;
        private readonly string[] diagnostics;

        public Step12SocialContextSnapshot(
            string requesterPersonId,
            string actorPersonId,
            string targetPersonId,
            double worldTime,
            IEnumerable<Step12ContextRecordReference> records,
            IEnumerable<Step12RuntimeSummary> sourceRuntimes,
            IEnumerable<string> diagnostics,
            bool truncated,
            string fingerprint)
        {
            RequesterPersonId = Clean(requesterPersonId);
            ActorPersonId = Clean(actorPersonId);
            TargetPersonId = Clean(targetPersonId);
            WorldTime = Math.Max(0d, worldTime);
            this.records = (records ?? Array.Empty<Step12ContextRecordReference>()).Where(item => item != null).OrderBy(item => item.RuntimeName, StringComparer.Ordinal).ThenBy(item => item.RecordId, StringComparer.Ordinal).ToArray();
            this.sourceRuntimes = (sourceRuntimes ?? Array.Empty<Step12RuntimeSummary>()).Where(item => item != null).OrderBy(item => item.RuntimeName, StringComparer.Ordinal).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Truncated = truncated;
            Fingerprint = Clean(fingerprint);
        }

        public string RequesterPersonId { get; }
        public string ActorPersonId { get; }
        public string TargetPersonId { get; }
        public double WorldTime { get; }
        public IReadOnlyList<Step12ContextRecordReference> Records => records.ToArray();
        public IReadOnlyList<Step12RuntimeSummary> SourceRuntimes => sourceRuntimes.ToArray();
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();
        public bool Truncated { get; }
        public string Fingerprint { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step12ConsequenceReference
    {
        public Step12ConsequenceReference(string sourceFeature, string sourceRecordId, string sourceTransactionId, string destinationRuntime, string destinationRecordId, string operation, double worldTime, long revision, Step12SocialVisibility visibility = Step12SocialVisibility.Public, bool active = true)
        {
            SourceFeature = Clean(sourceFeature);
            SourceRecordId = Clean(sourceRecordId);
            SourceTransactionId = Clean(sourceTransactionId);
            DestinationRuntime = Clean(destinationRuntime);
            DestinationRecordId = Clean(destinationRecordId);
            Operation = Clean(operation);
            WorldTime = Math.Max(0d, worldTime);
            Revision = Math.Max(0L, revision);
            Visibility = visibility;
            Active = active;
        }

        public string SourceFeature { get; }
        public string SourceRecordId { get; }
        public string SourceTransactionId { get; }
        public string DestinationRuntime { get; }
        public string DestinationRecordId { get; }
        public string Operation { get; }
        public double WorldTime { get; }
        public long Revision { get; }
        public Step12SocialVisibility Visibility { get; }
        public bool Active { get; }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step12TransactionParticipantPlan
    {
        public Step12TransactionParticipantPlan(string runtimeName, Step12TransactionFailurePolicy failurePolicy, Func<bool> preview, Func<bool> prepare, Func<bool> commit, Func<bool> rollback, Func<bool> postCommit = null)
        {
            RuntimeName = string.IsNullOrWhiteSpace(runtimeName) ? string.Empty : runtimeName.Trim();
            FailurePolicy = failurePolicy;
            Preview = preview;
            Prepare = prepare;
            Commit = commit;
            Rollback = rollback;
            PostCommit = postCommit;
        }

        public string RuntimeName { get; }
        public Step12TransactionFailurePolicy FailurePolicy { get; }
        public Func<bool> Preview { get; }
        public Func<bool> Prepare { get; }
        public Func<bool> Commit { get; }
        public Func<bool> Rollback { get; }
        public Func<bool> PostCommit { get; }
    }

    public sealed class Step12TransactionParticipantResult
    {
        public Step12TransactionParticipantResult(string runtimeName, Step12TransactionStage stage, bool succeeded, Step12TransactionFailurePolicy failurePolicy, string message = "")
        {
            RuntimeName = string.IsNullOrWhiteSpace(runtimeName) ? string.Empty : runtimeName.Trim();
            Stage = stage;
            Succeeded = succeeded;
            FailurePolicy = failurePolicy;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public string RuntimeName { get; }
        public Step12TransactionStage Stage { get; }
        public bool Succeeded { get; }
        public Step12TransactionFailurePolicy FailurePolicy { get; }
        public string Message { get; }
    }

    public sealed class Step12TransactionResult
    {
        private readonly Step12TransactionParticipantResult[] participants;
        private readonly string[] diagnostics;

        public Step12TransactionResult(bool succeeded, string transactionId, bool preview, bool duplicate, IEnumerable<Step12TransactionParticipantResult> participants, IEnumerable<string> diagnostics)
        {
            Succeeded = succeeded;
            TransactionId = string.IsNullOrWhiteSpace(transactionId) ? string.Empty : transactionId.Trim();
            Preview = preview;
            Duplicate = duplicate;
            this.participants = (participants ?? Array.Empty<Step12TransactionParticipantResult>()).Where(item => item != null).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        }

        public bool Succeeded { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public IReadOnlyList<Step12TransactionParticipantResult> Participants => participants.ToArray();
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();
    }

    public sealed class Step12HealthSnapshot
    {
        private readonly Step12RuntimeSummary[] runtimes;
        private readonly Step12IntegrationDiagnostic[] diagnostics;

        public Step12HealthSnapshot(Step12HealthStatus status, IEnumerable<Step12RuntimeSummary> runtimes, IEnumerable<Step12IntegrationDiagnostic> diagnostics, string fingerprint)
        {
            Status = status;
            this.runtimes = (runtimes ?? Array.Empty<Step12RuntimeSummary>()).Where(item => item != null).OrderBy(item => item.RuntimeName, StringComparer.Ordinal).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<Step12IntegrationDiagnostic>()).Where(item => item != null).ToArray();
            Fingerprint = string.IsNullOrWhiteSpace(fingerprint) ? string.Empty : fingerprint.Trim();
        }

        public Step12HealthStatus Status { get; }
        public IReadOnlyList<Step12RuntimeSummary> Runtimes => runtimes.ToArray();
        public IReadOnlyList<Step12IntegrationDiagnostic> Diagnostics => diagnostics.ToArray();
        public string Fingerprint { get; }
    }
}
