using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.WorldLocations.Integration
{
    public enum Step14IntegrationReadinessState
    {
        Uninitialized,
        Initializing,
        Restoring,
        Ready,
        Degraded,
        Failed,
        Resetting,
        Disposed
    }

    public enum Step14IntegrationDiagnosticSeverity
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    public enum Step14IntegrationDiagnosticDomain
    {
        Authority,
        Dependency,
        Persistence,
        WorldScope,
        StableIdentity,
        Hierarchy,
        SpatialRelationship,
        EntityPlacement,
        PersonBody,
        InteractionPoint,
        ConnectionAccess,
        RouteGraph,
        Journey,
        Scheduler,
        TravelCondition,
        Hazard,
        Encounter,
        PoliticalTravel,
        SceneBinding,
        MovementHistory,
        Determinism,
        Visibility,
        Step15Contract,
        PrototypeFixture
    }

    public sealed class Step14IntegrationDiagnostic
    {
        public Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity severity, Step14IntegrationDiagnosticDomain domain, string subjectId, string message)
        {
            Severity = severity;
            Domain = domain;
            SubjectId = N(subjectId);
            Message = message ?? string.Empty;
        }

        public Step14IntegrationDiagnosticSeverity Severity { get; }
        public Step14IntegrationDiagnosticDomain Domain { get; }
        public string SubjectId { get; }
        public string Message { get; }
        public bool IsFailure => Severity == Step14IntegrationDiagnosticSeverity.Error || Severity == Step14IntegrationDiagnosticSeverity.Fatal;
        public override string ToString() => $"{Severity}: {Domain} '{SubjectId}' - {Message}";

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step14IntegrationAuthorityEntry
    {
        public Step14IntegrationAuthorityEntry(string domain, string authoritativeRuntime, bool authoritative, bool persisted, bool derived, bool external, string notes)
        {
            Domain = N(domain);
            AuthoritativeRuntime = N(authoritativeRuntime);
            Authoritative = authoritative;
            Persisted = persisted;
            Derived = derived;
            External = external;
            Notes = notes ?? string.Empty;
        }

        public string Domain { get; }
        public string AuthoritativeRuntime { get; }
        public bool Authoritative { get; }
        public bool Persisted { get; }
        public bool Derived { get; }
        public bool External { get; }
        public string Notes { get; }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step14IntegrationDependencyEntry
    {
        public Step14IntegrationDependencyEntry(string participantId, IEnumerable<string> requiredDependencies, IEnumerable<string> optionalDependencies)
        {
            ParticipantId = N(participantId);
            RequiredDependencies = C(requiredDependencies);
            OptionalDependencies = C(optionalDependencies);
        }

        public string ParticipantId { get; }
        public IReadOnlyList<string> RequiredDependencies { get; }
        public IReadOnlyList<string> OptionalDependencies { get; }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static IReadOnlyList<string> C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class Step14IntegrationSnapshot
    {
        public Step14IntegrationSnapshot(
            Step14PersistenceSnapshotSource persistenceSource,
            WorldSceneBindingValidationReport sceneBindingValidation = null,
            bool authoritativeTimeAvailable = true,
            bool schedulerAvailable = true,
            bool prototypeFixtureAvailable = true)
        {
            PersistenceSource = (persistenceSource ?? new Step14PersistenceSnapshotSource
            {
                worldId = PersistenceService.LocalWorldId,
                saveSlotId = string.Empty
            }).Clone();
            SceneBindingValidation = sceneBindingValidation;
            AuthoritativeTimeAvailable = authoritativeTimeAvailable;
            SchedulerAvailable = schedulerAvailable;
            PrototypeFixtureAvailable = prototypeFixtureAvailable;
        }

        public Step14PersistenceSnapshotSource PersistenceSource { get; }
        public WorldSceneBindingValidationReport SceneBindingValidation { get; }
        public bool AuthoritativeTimeAvailable { get; }
        public bool SchedulerAvailable { get; }
        public bool PrototypeFixtureAvailable { get; }
        public string WorldId => string.IsNullOrWhiteSpace(PersistenceSource.worldId) ? PersistenceService.LocalWorldId : PersistenceSource.worldId.Trim();
        public string SaveSlotId => PersistenceSource.saveSlotId ?? string.Empty;
        public double AuthoritativeWorldTime => PersistenceSource.authoritativeWorldTime;
    }

    public sealed class Step14IntegrationValidationReport
    {
        public Step14IntegrationValidationReport(Step14IntegrationReadinessState readiness, Step14PersistenceManifest persistenceManifest, IEnumerable<Step14IntegrationAuthorityEntry> authorityMap, IEnumerable<Step14IntegrationDependencyEntry> dependencies, IEnumerable<Step14IntegrationDiagnostic> diagnostics, Step14Step15HandoffContract step15Contract, string fingerprint)
        {
            Readiness = readiness;
            PersistenceManifest = persistenceManifest;
            AuthorityMap = (authorityMap ?? Array.Empty<Step14IntegrationAuthorityEntry>()).Where(item => item != null).OrderBy(item => item.Domain, StringComparer.Ordinal).ToArray();
            Dependencies = (dependencies ?? Array.Empty<Step14IntegrationDependencyEntry>()).Where(item => item != null).OrderBy(item => item.ParticipantId, StringComparer.Ordinal).ToArray();
            Diagnostics = (diagnostics ?? Array.Empty<Step14IntegrationDiagnostic>())
                .Where(item => item != null)
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.Domain)
                .ThenBy(item => item.SubjectId, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToArray();
            Step15Contract = step15Contract ?? Step14Step15HandoffContract.Empty;
            Fingerprint = fingerprint ?? string.Empty;
        }

        public Step14IntegrationReadinessState Readiness { get; }
        public Step14PersistenceManifest PersistenceManifest { get; }
        public IReadOnlyList<Step14IntegrationAuthorityEntry> AuthorityMap { get; }
        public IReadOnlyList<Step14IntegrationDependencyEntry> Dependencies { get; }
        public IReadOnlyList<Step14IntegrationDiagnostic> Diagnostics { get; }
        public Step14Step15HandoffContract Step15Contract { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<Step14IntegrationDiagnostic> Failures => Diagnostics.Where(item => item.IsFailure).ToArray();
        public IReadOnlyList<Step14IntegrationDiagnostic> Warnings => Diagnostics.Where(item => item.Severity == Step14IntegrationDiagnosticSeverity.Warning).ToArray();
        public bool Succeeded => Failures.Count == 0 && Readiness != Step14IntegrationReadinessState.Failed;
        public string Summary => Succeeded ? $"Step 14 integration ready. Fingerprint={Fingerprint}" : string.Join(" | ", Failures.Select(item => item.Message));
    }

    public sealed class Step14Step15HandoffContract
    {
        public static readonly Step14Step15HandoffContract Empty = new Step14Step15HandoffContract(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        public Step14Step15HandoffContract(IEnumerable<string> stableReferenceTypes, IEnumerable<string> queryCapabilities, IEnumerable<string> commandCapabilities, IEnumerable<string> deferredBoundaries)
        {
            StableReferenceTypes = C(stableReferenceTypes);
            QueryCapabilities = C(queryCapabilities);
            CommandCapabilities = C(commandCapabilities);
            DeferredBoundaries = C(deferredBoundaries);
        }

        public IReadOnlyList<string> StableReferenceTypes { get; }
        public IReadOnlyList<string> QueryCapabilities { get; }
        public IReadOnlyList<string> CommandCapabilities { get; }
        public IReadOnlyList<string> DeferredBoundaries { get; }
        public bool Succeeded => StableReferenceTypes.Count >= 8 && QueryCapabilities.Count >= 8 && CommandCapabilities.Count >= 6;

        private static IReadOnlyList<string> C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
