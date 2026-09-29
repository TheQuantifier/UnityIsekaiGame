using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Integration
{
    internal static class KnowledgeHistoryCollections
    {
        public static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>((values ?? Array.Empty<T>()).ToList());
        }
    }

    public enum KnowledgeHistorySubsystem
    {
        Definitions,
        Knowledge,
        Observation,
        History,
        Memory,
        Sources,
        Transfers,
        Access,
        Records
    }

    public enum KnowledgeHistoryReadinessState
    {
        Missing,
        Ready,
        Degraded
    }

    public enum KnowledgeHistoryOperationKind
    {
        Validate,
        Observe,
        RecordHistory,
        FormMemory,
        Transfer,
        AccessProjection,
        CreateRecord,
        ReadRecord,
        SaveValidation,
        Step9Contract
    }

    public enum KnowledgeHistoryFailureStage
    {
        None,
        Readiness,
        RequestValidation,
        Access,
        Knowledge,
        History,
        Memory,
        Sources,
        Transfers,
        Records,
        PersistenceValidation,
        Rollback
    }

    public sealed class KnowledgeHistoryRuntimeSet
    {
        public DefinitionRegistry DefinitionRegistry { get; set; }
        public string PersonId { get; set; }
        public string WorldId { get; set; }
        public IReadOnlyList<string> KnownPersonIds { get; set; }
        public IReadOnlyList<string> KnownBodyIds { get; set; }
        public PersonKnowledgeRuntime KnowledgeRuntime { get; set; }
        public AuthoritativeHistoryRuntime HistoryRuntime { get; set; }
        public PersonMemoryRuntime MemoryRuntime { get; set; }
        public InformationSourceRuntime SourceRuntime { get; set; }
        public InformationTransferRuntime TransferRuntime { get; set; }
        public InformationAccessRuntime AccessRuntime { get; set; }
        public KnowledgeRecordRuntime RecordRuntime { get; set; }

        public string NormalizedPersonId => PersonId ?? string.Empty;
        public string NormalizedWorldId => WorldId ?? string.Empty;
    }

    public sealed class KnowledgeHistorySubsystemReadiness
    {
        public KnowledgeHistorySubsystemReadiness(KnowledgeHistorySubsystem subsystem, KnowledgeHistoryReadinessState state, long revision, string message)
        {
            Subsystem = subsystem;
            State = state;
            Revision = revision;
            Message = message ?? string.Empty;
        }

        public KnowledgeHistorySubsystem Subsystem { get; }
        public KnowledgeHistoryReadinessState State { get; }
        public long Revision { get; }
        public string Message { get; }
        public bool Ready => State == KnowledgeHistoryReadinessState.Ready;
    }

    public sealed class KnowledgeHistoryReadinessSnapshot
    {
        public KnowledgeHistoryReadinessSnapshot(string personId, string worldId, IReadOnlyList<KnowledgeHistorySubsystemReadiness> subsystems)
        {
            PersonId = personId ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            Subsystems = KnowledgeHistoryCollections.ReadOnly((subsystems ?? Array.Empty<KnowledgeHistorySubsystemReadiness>())
                .OrderBy(item => item.Subsystem)
                .ToArray());
        }

        public string PersonId { get; }
        public string WorldId { get; }
        public IReadOnlyList<KnowledgeHistorySubsystemReadiness> Subsystems { get; }
        public bool Ready => Subsystems.Count > 0 && Subsystems.All(item => item.Ready);

        public string ToSummary()
        {
            return string.Join(" | ", Subsystems.Select(item => $"{item.Subsystem}:{item.State} rev={item.Revision} {item.Message}".Trim()));
        }
    }

    public sealed class KnowledgeHistoryValidationResult
    {
        public KnowledgeHistoryValidationResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings, KnowledgeHistoryReadinessSnapshot readiness)
        {
            Errors = KnowledgeHistoryCollections.ReadOnly((errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
            Warnings = KnowledgeHistoryCollections.ReadOnly((warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
            Readiness = readiness;
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public KnowledgeHistoryReadinessSnapshot Readiness { get; }
        public bool Succeeded => Errors.Count == 0 && Readiness != null && Readiness.Ready;

        public string ToSummary()
        {
            string ready = Readiness == null ? "Readiness=None" : $"Readiness={Readiness.Ready}";
            string errors = Errors.Count == 0 ? "Errors=None" : $"Errors={string.Join(" | ", Errors)}";
            string warnings = Warnings.Count == 0 ? "Warnings=None" : $"Warnings={string.Join(" | ", Warnings)}";
            return $"{ready}. {errors}. {warnings}.";
        }
    }

    public sealed class KnowledgeHistoryTransactionDiagnostic
    {
        public KnowledgeHistoryTransactionDiagnostic(
            string transactionId,
            KnowledgeHistoryOperationKind operationKind,
            KnowledgeHistoryFailureStage failureStage,
            bool rollbackAttempted,
            bool rollbackSucceeded,
            IReadOnlyList<KnowledgeHistorySubsystem> participants,
            IReadOnlyList<string> messages)
        {
            TransactionId = transactionId ?? string.Empty;
            OperationKind = operationKind;
            FailureStage = failureStage;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
            Participants = KnowledgeHistoryCollections.ReadOnly((participants ?? Array.Empty<KnowledgeHistorySubsystem>()).Distinct().OrderBy(value => value).ToArray());
            Messages = KnowledgeHistoryCollections.ReadOnly((messages ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
        }

        public string TransactionId { get; }
        public KnowledgeHistoryOperationKind OperationKind { get; }
        public KnowledgeHistoryFailureStage FailureStage { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackSucceeded { get; }
        public IReadOnlyList<KnowledgeHistorySubsystem> Participants { get; }
        public IReadOnlyList<string> Messages { get; }

        public string ToSummary()
        {
            string participants = Participants.Count == 0 ? "None" : string.Join(",", Participants);
            string messages = Messages.Count == 0 ? "None" : string.Join(" | ", Messages);
            return $"{OperationKind} Tx={TransactionId} Stage={FailureStage} Participants={participants} Rollback={RollbackAttempted}/{RollbackSucceeded}. {messages}";
        }
    }

    public sealed class KnowledgeHistoryOperationResult
    {
        public KnowledgeHistoryOperationResult(bool succeeded, string code, string message, KnowledgeHistoryTransactionDiagnostic diagnostic, object underlyingResult = null)
        {
            Succeeded = succeeded;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Diagnostic = diagnostic;
            UnderlyingResult = underlyingResult;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public KnowledgeHistoryTransactionDiagnostic Diagnostic { get; }
        public object UnderlyingResult { get; }

        public string ToSummary()
        {
            return $"Success={Succeeded} Code={Code} {Message} Diagnostic=[{Diagnostic?.ToSummary() ?? "None"}]";
        }
    }

    public sealed class KnowledgeHistoryDefinitionCoverageDiagnostic
    {
        public KnowledgeHistoryDefinitionCoverageDiagnostic(string definitionId, bool catalogAuthored)
        {
            DefinitionId = definitionId ?? string.Empty;
            CatalogAuthored = catalogAuthored;
        }

        public string DefinitionId { get; }
        public bool CatalogAuthored { get; }
        public bool Missing => !CatalogAuthored;

        public string ToSummary()
        {
            return $"{DefinitionId}: Catalog={CatalogAuthored} Missing={Missing}";
        }
    }

    public sealed class KnowledgeHistoryPersistenceInventory
    {
        public KnowledgeHistoryPersistenceInventory(IReadOnlyList<string> participants, IReadOnlyList<string> requiredDependencies, IReadOnlyList<string> optionalDependencies)
        {
            Participants = KnowledgeHistoryCollections.ReadOnly((participants ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            RequiredDependencies = KnowledgeHistoryCollections.ReadOnly((requiredDependencies ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            OptionalDependencies = KnowledgeHistoryCollections.ReadOnly((optionalDependencies ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        }

        public IReadOnlyList<string> Participants { get; }
        public IReadOnlyList<string> RequiredDependencies { get; }
        public IReadOnlyList<string> OptionalDependencies { get; }

        public string ToSummary()
        {
            return $"Participants=[{string.Join(",", Participants)}] Required=[{string.Join(",", RequiredDependencies)}] Optional=[{string.Join(",", OptionalDependencies)}]";
        }
    }
}
