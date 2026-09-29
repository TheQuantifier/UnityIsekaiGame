using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Economy.Integration
{
    public sealed class EconomicIntegrationDiagnosticData
    {
        public EconomicIntegrationDiagnosticSeverity severity;
        public EconomicIntegrationDiagnosticCode code;
        public string path;
        public string owningRuntime;
        public string message;
        public string correctiveAction;
        public long[] sourceRevisions = Array.Empty<long>();

        public EconomicIntegrationDiagnosticData Clone()
        {
            return new EconomicIntegrationDiagnosticData
            {
                severity = severity,
                code = code,
                path = path ?? string.Empty,
                owningRuntime = owningRuntime ?? string.Empty,
                message = message ?? string.Empty,
                correctiveAction = correctiveAction ?? string.Empty,
                sourceRevisions = sourceRevisions == null ? Array.Empty<long>() : sourceRevisions.ToArray()
            };
        }
    }

    public sealed class EconomicAuthorityMapEntryData
    {
        public EconomicDomainAuthorityId domainId;
        public string featureId;
        public string displayName;
        public string authoritativeRuntime;
        public string[] owns = Array.Empty<string>();
        public string[] externalAuthorities = Array.Empty<string>();

        public EconomicAuthorityMapEntryData Clone()
        {
            return new EconomicAuthorityMapEntryData
            {
                domainId = domainId,
                featureId = featureId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                authoritativeRuntime = authoritativeRuntime ?? string.Empty,
                owns = CloneArray(owns),
                externalAuthorities = CloneArray(externalAuthorities)
            };
        }

        private static string[] CloneArray(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        }
    }

    public sealed class EconomicRuntimeSummaryData
    {
        public string runtimeName;
        public string persistenceKey;
        public bool present;
        public long revision;
        public int primaryRecordCount;
        public int secondaryRecordCount;
        public int tertiaryRecordCount;
        public string fingerprint;

        public EconomicRuntimeSummaryData Clone()
        {
            return new EconomicRuntimeSummaryData
            {
                runtimeName = runtimeName ?? string.Empty,
                persistenceKey = persistenceKey ?? string.Empty,
                present = present,
                revision = Math.Max(0L, revision),
                primaryRecordCount = Math.Max(0, primaryRecordCount),
                secondaryRecordCount = Math.Max(0, secondaryRecordCount),
                tertiaryRecordCount = Math.Max(0, tertiaryRecordCount),
                fingerprint = fingerprint ?? string.Empty
            };
        }
    }

    public sealed class EconomicReadinessSnapshot
    {
        private readonly EconomicIntegrationDiagnosticData[] diagnostics;

        public EconomicReadinessSnapshot(
            bool ready,
            bool definitionRegistryAvailable,
            bool sceneHostAvailable,
            bool sceneHostRequired,
            IEnumerable<EconomicRuntimeSummaryData> runtimes,
            IEnumerable<EconomicIntegrationDiagnosticData> diagnostics)
        {
            Ready = ready;
            DefinitionRegistryAvailable = definitionRegistryAvailable;
            SceneHostAvailable = sceneHostAvailable;
            SceneHostRequired = sceneHostRequired;
            RuntimeSummaries = (runtimes ?? Array.Empty<EconomicRuntimeSummaryData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<EconomicIntegrationDiagnosticData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
        }

        public bool Ready { get; }
        public bool DefinitionRegistryAvailable { get; }
        public bool SceneHostAvailable { get; }
        public bool SceneHostRequired { get; }
        public IReadOnlyList<EconomicRuntimeSummaryData> RuntimeSummaries { get; }
        public IReadOnlyList<EconomicIntegrationDiagnosticData> Diagnostics => diagnostics.Select(item => item.Clone()).ToArray();
    }

    public sealed class EconomicValidationResult
    {
        private readonly EconomicIntegrationDiagnosticData[] diagnostics;

        public EconomicValidationResult(bool succeeded, IEnumerable<EconomicIntegrationDiagnosticData> diagnostics)
        {
            Succeeded = succeeded;
            this.diagnostics = (diagnostics ?? Array.Empty<EconomicIntegrationDiagnosticData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
        }

        public bool Succeeded { get; }
        public IReadOnlyList<EconomicIntegrationDiagnosticData> Diagnostics => diagnostics.Select(item => item.Clone()).ToArray();
        public string Summary => diagnostics.Length == 0 ? "No diagnostics." : string.Join(" | ", diagnostics.Select(item => $"{item.code}: {item.message}"));
    }

    public sealed class EconomicBoundaryInvariantResult
    {
        public EconomicBoundaryInvariantId invariantId;
        public string owningRuntime;
        public string dependentRuntime;
        public bool satisfied;
        public string message;

        public EconomicBoundaryInvariantResult Clone()
        {
            return new EconomicBoundaryInvariantResult
            {
                invariantId = invariantId,
                owningRuntime = owningRuntime ?? string.Empty,
                dependentRuntime = dependentRuntime ?? string.Empty,
                satisfied = satisfied,
                message = message ?? string.Empty
            };
        }
    }

    public sealed class EconomicConservationAuditResult
    {
        public string auditId;
        public bool succeeded;
        public long monetaryLedgerNet;
        public long regionalKnownPoolUnits;
        public int checkedRuntimeCount;
        public string message;

        public EconomicConservationAuditResult Clone()
        {
            return new EconomicConservationAuditResult
            {
                auditId = auditId ?? string.Empty,
                succeeded = succeeded,
                monetaryLedgerNet = monetaryLedgerNet,
                regionalKnownPoolUnits = regionalKnownPoolUnits,
                checkedRuntimeCount = Math.Max(0, checkedRuntimeCount),
                message = message ?? string.Empty
            };
        }
    }

    public sealed class EconomicPersistenceDependencyData
    {
        public string participantKey;
        public string[] requiredDependencies = Array.Empty<string>();
        public string[] optionalDependencies = Array.Empty<string>();

        public EconomicPersistenceDependencyData Clone()
        {
            return new EconomicPersistenceDependencyData
            {
                participantKey = participantKey ?? string.Empty,
                requiredDependencies = CloneArray(requiredDependencies),
                optionalDependencies = CloneArray(optionalDependencies)
            };
        }

        private static string[] CloneArray(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    public sealed class EconomicPersistenceDependencyMapResult
    {
        private readonly EconomicPersistenceDependencyData[] participants;
        private readonly EconomicIntegrationDiagnosticData[] diagnostics;

        public EconomicPersistenceDependencyMapResult(bool succeeded, IEnumerable<EconomicPersistenceDependencyData> participants, IEnumerable<EconomicIntegrationDiagnosticData> diagnostics)
        {
            Succeeded = succeeded;
            this.participants = (participants ?? Array.Empty<EconomicPersistenceDependencyData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            this.diagnostics = (diagnostics ?? Array.Empty<EconomicIntegrationDiagnosticData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
        }

        public bool Succeeded { get; }
        public IReadOnlyList<EconomicPersistenceDependencyData> Participants => participants.Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EconomicIntegrationDiagnosticData> Diagnostics => diagnostics.Select(item => item.Clone()).ToArray();
    }

    public sealed class EconomicSignalContractData
    {
        public string signalId;
        public EconomicSignalCategory category;
        public string sourceRuntime;
        public string subjectId;
        public string valueKind;
        public long exactValue;
        public double worldTime;
        public long[] dependencyRevisions = Array.Empty<long>();
        public bool mutationFree;
        public bool step12Ready;

        public EconomicSignalContractData Clone()
        {
            return new EconomicSignalContractData
            {
                signalId = signalId ?? string.Empty,
                category = category,
                sourceRuntime = sourceRuntime ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                valueKind = valueKind ?? string.Empty,
                exactValue = exactValue,
                worldTime = Math.Max(0d, worldTime),
                dependencyRevisions = dependencyRevisions == null ? Array.Empty<long>() : dependencyRevisions.ToArray(),
                mutationFree = mutationFree,
                step12Ready = step12Ready
            };
        }
    }

    public sealed class EconomicIntegrationSnapshot
    {
        private readonly EconomicRuntimeSummaryData[] runtimes;
        private readonly EconomicSignalContractData[] signals;

        public EconomicIntegrationSnapshot(IEnumerable<EconomicRuntimeSummaryData> runtimes, IEnumerable<EconomicSignalContractData> signals, string fingerprint)
        {
            this.runtimes = (runtimes ?? Array.Empty<EconomicRuntimeSummaryData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            this.signals = (signals ?? Array.Empty<EconomicSignalContractData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            Fingerprint = fingerprint ?? string.Empty;
        }

        public IReadOnlyList<EconomicRuntimeSummaryData> RuntimeSummaries => runtimes.Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EconomicSignalContractData> Signals => signals.Select(item => item.Clone()).ToArray();
        public string Fingerprint { get; }
    }
}
