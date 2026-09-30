using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Professions.Integration
{
    public enum Step10IntegrationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum Step10IntegrationDiagnosticDomain
    {
        Authority,
        DefinitionCatalog,
        RuntimeIndex,
        PersonGraph,
        Lifecycle,
        Persistence,
        SaveSchema,
        Transaction,
        Snapshot,
        Determinism,
        Access,
        RequirementAdapter,
        TestLab,
        Migration,
        Projection
    }

    public sealed class Step10IntegrationDiagnostic
    {
        public Step10IntegrationDiagnostic(
            Step10IntegrationDiagnosticSeverity severity,
            Step10IntegrationDiagnosticDomain domain,
            string code,
            string message,
            string subjectId = "",
            string graphPath = "")
        {
            Severity = severity;
            Domain = domain;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            GraphPath = graphPath ?? string.Empty;
        }

        public Step10IntegrationDiagnosticSeverity Severity { get; }
        public Step10IntegrationDiagnosticDomain Domain { get; }
        public string Code { get; }
        public string Message { get; }
        public string SubjectId { get; }
        public string GraphPath { get; }

        public override string ToString()
        {
            string path = string.IsNullOrWhiteSpace(GraphPath) ? string.Empty : $" Path={GraphPath}.";
            return string.IsNullOrWhiteSpace(SubjectId)
                ? $"{Severity}: {Domain}/{Code}: {Message}{path}"
                : $"{Severity}: {Domain}/{Code}: {SubjectId}: {Message}{path}";
        }
    }

    public sealed class Step10IntegrationValidationReport
    {
        private readonly List<Step10IntegrationDiagnostic> diagnostics = new List<Step10IntegrationDiagnostic>();

        public IReadOnlyList<Step10IntegrationDiagnostic> Diagnostics => diagnostics;
        public int ErrorCount => diagnostics.Count(diagnostic => diagnostic.Severity == Step10IntegrationDiagnosticSeverity.Error);
        public int WarningCount => diagnostics.Count(diagnostic => diagnostic.Severity == Step10IntegrationDiagnosticSeverity.Warning);
        public int InfoCount => diagnostics.Count(diagnostic => diagnostic.Severity == Step10IntegrationDiagnosticSeverity.Info);
        public bool Succeeded => ErrorCount == 0;

        public void Add(
            Step10IntegrationDiagnosticSeverity severity,
            Step10IntegrationDiagnosticDomain domain,
            string code,
            string message,
            string subjectId = "",
            string graphPath = "")
        {
            diagnostics.Add(new Step10IntegrationDiagnostic(severity, domain, code, message, subjectId, graphPath));
        }

        public void AddError(Step10IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string graphPath = "")
        {
            Add(Step10IntegrationDiagnosticSeverity.Error, domain, code, message, subjectId, graphPath);
        }

        public void AddWarning(Step10IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "", string graphPath = "")
        {
            Add(Step10IntegrationDiagnosticSeverity.Warning, domain, code, message, subjectId, graphPath);
        }

        public string ToSummary()
        {
            string[] notableDiagnostics = diagnostics
                .Where(diagnostic => diagnostic.Severity != Step10IntegrationDiagnosticSeverity.Info)
                .Take(3)
                .Select(diagnostic => diagnostic.ToString())
                .ToArray();
            string details = notableDiagnostics.Length == 0
                ? string.Empty
                : $" Details={string.Join(" | ", notableDiagnostics)}";
            return $"Errors={ErrorCount} Warnings={WarningCount} Info={InfoCount}{details}";
        }
    }

    public sealed class Step10IntegrationRuntimeSnapshot
    {
        public Step10IntegrationRuntimeSnapshot(
            PersonProfessionRuntimeSaveData professions = null,
            ProfessionEntryRuntimeSaveData entries = null,
            TrainingRuntimeSaveData training = null,
            ProfessionalActivityRuntimeSaveData activities = null,
            CredentialRuntimeSaveData credentials = null,
            ProfessionalRankRuntimeSaveData ranks = null,
            PositionEmploymentRuntimeSaveData positions = null,
            CareerHistoryRuntimeSaveData careerHistory = null,
            LifePathRuntimeSaveData lifePaths = null)
        {
            Professions = professions?.Clone() ?? new PersonProfessionRuntimeSaveData();
            Entries = entries?.Clone() ?? new ProfessionEntryRuntimeSaveData();
            Training = training?.Clone() ?? new TrainingRuntimeSaveData();
            Activities = activities?.Clone() ?? new ProfessionalActivityRuntimeSaveData();
            Credentials = credentials?.Clone() ?? new CredentialRuntimeSaveData();
            Ranks = ranks?.Clone() ?? new ProfessionalRankRuntimeSaveData();
            Positions = positions?.Clone() ?? new PositionEmploymentRuntimeSaveData();
            CareerHistory = careerHistory?.Clone() ?? new CareerHistoryRuntimeSaveData();
            LifePaths = lifePaths?.Clone() ?? new LifePathRuntimeSaveData();
        }

        public PersonProfessionRuntimeSaveData Professions { get; }
        public ProfessionEntryRuntimeSaveData Entries { get; }
        public TrainingRuntimeSaveData Training { get; }
        public ProfessionalActivityRuntimeSaveData Activities { get; }
        public CredentialRuntimeSaveData Credentials { get; }
        public ProfessionalRankRuntimeSaveData Ranks { get; }
        public PositionEmploymentRuntimeSaveData Positions { get; }
        public CareerHistoryRuntimeSaveData CareerHistory { get; }
        public LifePathRuntimeSaveData LifePaths { get; }

        public Step10IntegrationRuntimeSnapshot Clone()
        {
            return new Step10IntegrationRuntimeSnapshot(
                Professions,
                Entries,
                Training,
                Activities,
                Credentials,
                Ranks,
                Positions,
                CareerHistory,
                LifePaths);
        }
    }

    public sealed class Step10IntegrationAuthorityEntry
    {
        public Step10IntegrationAuthorityEntry(string domain, string owner, params string[] readOnlyDependents)
        {
            Domain = domain ?? string.Empty;
            Owner = owner ?? string.Empty;
            ReadOnlyDependents = (readOnlyDependents ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public string Domain { get; }
        public string Owner { get; }
        public IReadOnlyList<string> ReadOnlyDependents { get; }
    }

    public sealed class Step10IntegrationDependencyEntry
    {
        public Step10IntegrationDependencyEntry(string owner, params string[] dependsOn)
        {
            Owner = owner ?? string.Empty;
            DependsOn = (dependsOn ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public string Owner { get; }
        public IReadOnlyList<string> DependsOn { get; }
    }
}
