using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Knowledge.Integration
{
    public sealed class KnowledgeHistoryFacade
    {
        private readonly KnowledgeHistoryRuntimeSet runtimes;

        public KnowledgeHistoryFacade(KnowledgeHistoryRuntimeSet runtimeSet)
        {
            runtimes = runtimeSet ?? new KnowledgeHistoryRuntimeSet();
        }

        public KnowledgeHistoryReadinessSnapshot CreateReadinessSnapshot()
        {
            return new KnowledgeHistoryReadinessSnapshot(
                runtimes.NormalizedPersonId,
                runtimes.NormalizedWorldId,
                new[]
                {
                    DefinitionReadiness(),
                    RuntimeReadiness(KnowledgeHistorySubsystem.Knowledge, runtimes.KnowledgeRuntime != null && runtimes.KnowledgeRuntime.IsReady, runtimes.KnowledgeRuntime?.KnowledgeRevision ?? 0L, runtimes.KnowledgeRuntime == null ? "Missing PersonKnowledgeRuntime." : runtimes.KnowledgeRuntime.Readiness.ToString()),
                    RuntimeReadiness(KnowledgeHistorySubsystem.History, runtimes.HistoryRuntime != null, runtimes.HistoryRuntime?.HistoryRevision ?? 0L, runtimes.HistoryRuntime == null ? "Missing AuthoritativeHistoryRuntime." : "Ready"),
                    RuntimeReadiness(KnowledgeHistorySubsystem.Memory, runtimes.MemoryRuntime != null, runtimes.MemoryRuntime?.MemoryRevision ?? 0L, runtimes.MemoryRuntime == null ? "Missing PersonMemoryRuntime." : "Ready"),
                    RuntimeReadiness(KnowledgeHistorySubsystem.Sources, runtimes.SourceRuntime != null, runtimes.SourceRuntime?.SourceRevision ?? 0L, runtimes.SourceRuntime == null ? "Missing InformationSourceRuntime." : "Ready"),
                    RuntimeReadiness(KnowledgeHistorySubsystem.Transfers, runtimes.TransferRuntime != null, runtimes.TransferRuntime?.TransferRevision ?? 0L, runtimes.TransferRuntime == null ? "Missing InformationTransferRuntime." : "Ready"),
                    RuntimeReadiness(KnowledgeHistorySubsystem.Access, runtimes.AccessRuntime != null, runtimes.AccessRuntime?.AccessRevision ?? 0L, runtimes.AccessRuntime == null ? "Missing InformationAccessRuntime." : "Ready"),
                    RuntimeReadiness(KnowledgeHistorySubsystem.Records, runtimes.RecordRuntime != null, runtimes.RecordRuntime?.RecordRevision ?? 0L, runtimes.RecordRuntime == null ? "Missing KnowledgeRecordRuntime." : "Ready")
                });
        }

        public KnowledgeHistoryValidationResult ValidateCurrentState()
        {
            KnowledgeHistoryReadinessSnapshot readiness = CreateReadinessSnapshot();
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            foreach (KnowledgeHistorySubsystemReadiness item in readiness.Subsystems.Where(item => !item.Ready))
            {
                errors.Add($"{item.Subsystem}: {item.Message}");
            }

            if (readiness.Ready)
            {
                ValidateSavePayloads(errors);
                ValidateCrossRuntimeReferences(errors);
            }

            if (runtimes.KnowledgeRuntime != null)
            {
                KnowledgeValidationResult knowledgeValidation = runtimes.KnowledgeRuntime.ValidateKnowledge();
                foreach (string issue in knowledgeValidation.Errors)
                {
                    errors.Add($"Knowledge: {issue}");
                }

                foreach (string issue in knowledgeValidation.Warnings)
                {
                    warnings.Add($"Knowledge: {issue}");
                }
            }

            return new KnowledgeHistoryValidationResult(errors, warnings, readiness);
        }

        public KnowledgeHistoryOperationResult RecordObservation(KnowledgeObservationRequest request, bool preview = false)
        {
            if (runtimes.KnowledgeRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.Observe, KnowledgeHistoryFailureStage.Readiness, request?.TransactionId, "MissingKnowledgeRuntime", "Person Knowledge runtime is missing.", KnowledgeHistorySubsystem.Knowledge);
            }

            KnowledgeOperationResult result = preview
                ? runtimes.KnowledgeRuntime.PreviewObservation(request)
                : runtimes.KnowledgeRuntime.RecordObservation(request);
            return Wrap(result.Succeeded, result.Code.ToString(), result.Message, KnowledgeHistoryOperationKind.Observe, result.TransactionId, result.Succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.Knowledge, result, KnowledgeHistorySubsystem.Knowledge);
        }

        public KnowledgeHistoryOperationResult RecordHistoricalEvent(RecordHistoricalEventRequest request, bool preview = false)
        {
            if (runtimes.HistoryRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.RecordHistory, KnowledgeHistoryFailureStage.Readiness, request?.TransactionId, "MissingHistoryRuntime", "Authoritative History runtime is missing.", KnowledgeHistorySubsystem.History);
            }

            HistoryOperationResult result = preview
                ? runtimes.HistoryRuntime.PreviewRecordEvent(request)
                : runtimes.HistoryRuntime.RecordEvent(request);
            return Wrap(result.Succeeded, result.Code.ToString(), result.Message, KnowledgeHistoryOperationKind.RecordHistory, result.TransactionId, result.Succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.History, result, KnowledgeHistorySubsystem.History);
        }

        public KnowledgeHistoryOperationResult FormMemory(FormMemoryRequest request, bool preview = false)
        {
            if (runtimes.MemoryRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.FormMemory, KnowledgeHistoryFailureStage.Readiness, request?.TransactionId, "MissingMemoryRuntime", "Person Memory runtime is missing.", KnowledgeHistorySubsystem.Memory);
            }

            HistoryOperationResult result = preview
                ? runtimes.MemoryRuntime.PreviewFormMemory(request, runtimes.KnowledgeRuntime)
                : runtimes.MemoryRuntime.FormMemory(request, runtimes.KnowledgeRuntime);
            return Wrap(result.Succeeded, result.Code.ToString(), result.Message, KnowledgeHistoryOperationKind.FormMemory, result.TransactionId, result.Succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.Memory, result, KnowledgeHistorySubsystem.Memory, KnowledgeHistorySubsystem.Knowledge);
        }

        public KnowledgeHistoryOperationResult ExecuteTransfer(InformationTransferRequest request, bool preview = false)
        {
            if (runtimes.TransferRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.Transfer, KnowledgeHistoryFailureStage.Readiness, request?.TransactionId, "MissingTransferRuntime", "Information Transfer runtime is missing.", KnowledgeHistorySubsystem.Transfers);
            }

            InformationTransferResult result = preview
                ? runtimes.TransferRuntime.PreviewTransfer(request)
                : runtimes.TransferRuntime.ExecuteTransfer(request);
            return Wrap(result.Succeeded, result.Status.ToString(), result.Message, KnowledgeHistoryOperationKind.Transfer, result.TransactionId, result.Succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.Transfers, result, KnowledgeHistorySubsystem.Transfers, KnowledgeHistorySubsystem.Knowledge, KnowledgeHistorySubsystem.Memory, KnowledgeHistorySubsystem.Sources);
        }

        public KnowledgeHistoryOperationResult EvaluateAccess(InformationAccessContext context)
        {
            if (runtimes.AccessRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.AccessProjection, KnowledgeHistoryFailureStage.Readiness, string.Empty, "MissingAccessRuntime", "Information Access runtime is missing.", KnowledgeHistorySubsystem.Access);
            }

            InformationAccessDecision decision = runtimes.AccessRuntime.EvaluateAccess(context);
            bool succeeded = decision != null && !decision.Denied;
            string message = decision == null
                ? "Access decision was unavailable."
                : string.IsNullOrWhiteSpace(decision.DiagnosticReason) ? decision.VisibleReason : decision.DiagnosticReason;
            return Wrap(succeeded, decision == null ? "MissingDecision" : decision.Decision.ToString(), message, KnowledgeHistoryOperationKind.AccessProjection, string.Empty, succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.Access, decision, KnowledgeHistorySubsystem.Access);
        }

        public KnowledgeHistoryOperationResult CreateRecord(KnowledgeRecordCreateRequest request)
        {
            if (runtimes.RecordRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.CreateRecord, KnowledgeHistoryFailureStage.Readiness, request?.TransactionId, "MissingRecordRuntime", "Knowledge Record runtime is missing.", KnowledgeHistorySubsystem.Records);
            }

            KnowledgeRecordOperationResult result = runtimes.RecordRuntime.CreateRecord(request);
            return Wrap(result.Succeeded, result.Code.ToString(), result.Message, KnowledgeHistoryOperationKind.CreateRecord, result.TransactionId, result.Succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.Records, result, KnowledgeHistorySubsystem.Records);
        }

        public KnowledgeHistoryOperationResult ReadRecordAsPerson(KnowledgeRecordReadRequest request)
        {
            if (runtimes.RecordRuntime == null)
            {
                return Failure(KnowledgeHistoryOperationKind.ReadRecord, KnowledgeHistoryFailureStage.Readiness, request?.TransactionId, "MissingRecordRuntime", "Knowledge Record runtime is missing.", KnowledgeHistorySubsystem.Records);
            }

            KnowledgeRecordReadResult result = runtimes.RecordRuntime.ReadRecordAsPerson(request, runtimes.AccessRuntime, runtimes.SourceRuntime, runtimes.KnowledgeRuntime, runtimes.MemoryRuntime);
            KnowledgeHistoryFailureStage stage = result.Succeeded ? KnowledgeHistoryFailureStage.None : KnowledgeHistoryFailureStage.Records;
            return Wrap(result.Succeeded, result.Code.ToString(), result.Message, KnowledgeHistoryOperationKind.ReadRecord, result.TransactionId, stage, result, KnowledgeHistorySubsystem.Records, KnowledgeHistorySubsystem.Access, KnowledgeHistorySubsystem.Sources, KnowledgeHistorySubsystem.Knowledge, KnowledgeHistorySubsystem.Memory);
        }

        public IReadOnlyList<InformationAccessProjection<KnowledgeBeliefRecord>> QueryKnowledgeForPerson(
            InformationAccessContext accessContext,
            KnowledgeDomain? domain = null,
            string subjectId = null,
            string policyId = "")
        {
            if (runtimes.KnowledgeRuntime == null)
            {
                return Array.Empty<InformationAccessProjection<KnowledgeBeliefRecord>>();
            }

            return runtimes.KnowledgeRuntime.QueryKnowledgeProjections(
                runtimes.AccessRuntime,
                accessContext,
                domain,
                subjectId,
                policyId);
        }

        public IReadOnlyList<InformationAccessProjection<HistoricalEventRecord>> QueryHistoryForPerson(
            string personId,
            InformationAccessContext accessContext,
            bool publicOnly = false,
            string policyId = "")
        {
            if (runtimes.HistoryRuntime == null)
            {
                return Array.Empty<InformationAccessProjection<HistoricalEventRecord>>();
            }

            return runtimes.HistoryRuntime.QueryHistoryProjectionsByPerson(
                personId,
                runtimes.AccessRuntime,
                accessContext,
                runtimes.MemoryRuntime,
                publicOnly,
                policyId);
        }

        public IReadOnlyList<InformationAccessProjection<BiographyTimelineEntry>> QueryBiographyForPerson(
            string personId,
            InformationAccessContext accessContext,
            bool publicOnly = false,
            bool personKnown = false,
            bool personRemembered = false,
            string policyId = "")
        {
            if (runtimes.HistoryRuntime == null)
            {
                return Array.Empty<InformationAccessProjection<BiographyTimelineEntry>>();
            }

            return runtimes.HistoryRuntime.GetBiographyProjection(
                personId,
                runtimes.AccessRuntime,
                accessContext,
                runtimes.MemoryRuntime,
                publicOnly,
                personKnown,
                personRemembered,
                policyId);
        }

        public IReadOnlyList<KnowledgeRecordProjection> SearchRecords(
            KnowledgeRecordSearchQuery query,
            KnowledgeRecordProjectionContext projectionContext)
        {
            return runtimes.RecordRuntime?.Search(query, projectionContext, runtimes.AccessRuntime)
                ?? Array.Empty<KnowledgeRecordProjection>();
        }

        public IReadOnlyList<KnowledgeHistoryDefinitionCoverageDiagnostic> CreateDefinitionCoverageDiagnostics(IEnumerable<string> expectedDefinitionIds)
        {
            HashSet<string> expected = new HashSet<string>((expectedDefinitionIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            return expected
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(id => new KnowledgeHistoryDefinitionCoverageDiagnostic(id, runtimes.DefinitionRegistry != null && runtimes.DefinitionRegistry.Contains(id)))
                .ToArray();
        }

        public KnowledgeHistoryPersistenceInventory CreatePersistenceInventory()
        {
            return new KnowledgeHistoryPersistenceInventory(
                new[]
                {
                    PersonKnowledgePersistenceParticipant.Key,
                    PersonMemoryPersistenceParticipant.Key,
                    InformationSourcePersistenceParticipant.WorldKey,
                    InformationTransferPersistenceParticipant.WorldKey,
                    InformationAccessPersistenceParticipant.WorldKey,
                    KnowledgeRecordPersistenceParticipant.WorldKey,
                    AuthoritativeHistoryPersistenceParticipant.Key
                },
                new[]
                {
                    $"{PersonMemoryPersistenceParticipant.Key} -> {AuthoritativeHistoryPersistenceParticipant.Key}"
                },
                new[]
                {
                    $"{PersonKnowledgePersistenceParticipant.Key} -> {PlayerBodyPersistenceParticipant.Key}",
                    $"{InformationSourcePersistenceParticipant.WorldKey} -> {PersonKnowledgePersistenceParticipant.Key}",
                    $"{InformationTransferPersistenceParticipant.WorldKey} -> {PersonKnowledgePersistenceParticipant.Key}/{PersonMemoryPersistenceParticipant.Key}/{InformationSourcePersistenceParticipant.WorldKey}",
                    $"{InformationAccessPersistenceParticipant.WorldKey} -> {PersonKnowledgePersistenceParticipant.Key}/{PersonMemoryPersistenceParticipant.Key}/{InformationSourcePersistenceParticipant.WorldKey}/{InformationTransferPersistenceParticipant.WorldKey}",
                    $"{KnowledgeRecordPersistenceParticipant.WorldKey} -> {PersonKnowledgePersistenceParticipant.Key}/{AuthoritativeHistoryPersistenceParticipant.Key}/{PersonMemoryPersistenceParticipant.Key}/{InformationSourcePersistenceParticipant.WorldKey}/{InformationTransferPersistenceParticipant.WorldKey}/{InformationAccessPersistenceParticipant.WorldKey}"
                });
        }

        private void ValidateSavePayloads(List<string> errors)
        {
            if (runtimes.KnowledgeRuntime != null && !PersonKnowledgeRuntime.ValidateSaveData(runtimes.KnowledgeRuntime.CreateSaveData(), runtimes.DefinitionRegistry, runtimes.NormalizedPersonId, out string knowledgeFailure))
            {
                errors.Add($"Knowledge save payload: {knowledgeFailure}");
            }

            if (runtimes.HistoryRuntime != null && !AuthoritativeHistoryRuntime.ValidateSaveData(runtimes.HistoryRuntime.CreateSaveData(), runtimes.DefinitionRegistry, KnownPersons(), KnownBodies(), out string historyFailure))
            {
                errors.Add($"History save payload: {historyFailure}");
            }

            if (runtimes.MemoryRuntime != null && !PersonMemoryRuntime.ValidateSaveData(runtimes.MemoryRuntime.CreateSaveData(), runtimes.HistoryRuntime, KnownPersons(), out string memoryFailure))
            {
                errors.Add($"Memory save payload: {memoryFailure}");
            }

            if (runtimes.SourceRuntime != null && !InformationSourceRuntime.ValidateSaveData(runtimes.SourceRuntime.CreateSaveData(), runtimes.DefinitionRegistry, runtimes.NormalizedPersonId, out string sourceFailure))
            {
                errors.Add($"Information Source save payload: {sourceFailure}");
            }

            if (runtimes.TransferRuntime != null && !InformationTransferRuntime.ValidateSaveData(runtimes.TransferRuntime.CreateSaveData(), runtimes.DefinitionRegistry, runtimes.NormalizedPersonId, out string transferFailure))
            {
                errors.Add($"Information Transfer save payload: {transferFailure}");
            }

            if (runtimes.AccessRuntime != null && !InformationAccessRuntime.ValidateSaveData(runtimes.AccessRuntime.CreateSaveData(), runtimes.DefinitionRegistry, runtimes.NormalizedPersonId, out string accessFailure))
            {
                errors.Add($"Information Access save payload: {accessFailure}");
            }

            if (runtimes.RecordRuntime != null && !KnowledgeRecordRuntime.ValidateSaveData(runtimes.RecordRuntime.CreateSaveData(), runtimes.DefinitionRegistry, runtimes.NormalizedPersonId, out string recordFailure))
            {
                errors.Add($"Knowledge Record save payload: {recordFailure}");
            }
        }

        private void ValidateCrossRuntimeReferences(List<string> errors)
        {
            KnowledgeSnapshot knowledge = runtimes.KnowledgeRuntime?.CreateSnapshot();
            HistorySnapshot history = runtimes.HistoryRuntime?.CreateSnapshot();
            PersonMemorySnapshot memory = runtimes.MemoryRuntime?.CreateSnapshot();
            InformationSourceSnapshot sources = runtimes.SourceRuntime?.CreateSnapshot();
            InformationTransferSnapshot transfers = runtimes.TransferRuntime?.CreateSnapshot();
            InformationAccessSnapshot access = runtimes.AccessRuntime?.CreateSnapshot();
            KnowledgeRecordSnapshot records = runtimes.RecordRuntime?.CreateSnapshot();

            HashSet<string> evidenceIds = IdSet(knowledge?.Evidence.Select(item => item.EvidenceId));
            HashSet<string> beliefIds = IdSet(knowledge?.Beliefs.Select(item => item.BeliefId));
            HashSet<string> eventIds = IdSet(history?.Events.Select(item => item.EventId));
            HashSet<string> lifeEventIds = IdSet(history?.Events.Where(item => item.Data.isLifeEvent).Select(item => item.EventId));
            HashSet<string> memoryIds = IdSet(memory?.Memories.Select(item => item.MemoryId));
            HashSet<string> sourceIds = IdSet(sources?.Sources.Select(item => item.SourceInstanceId));
            HashSet<string> transferIds = IdSet(transfers?.Transfers.Select(item => item.TransferId));
            HashSet<string> accessPolicyIds = IdSet(access?.Policies.Select(item => item.PolicyId));
            HashSet<string> recordIds = IdSet(records?.Records.Select(item => item.RecordId));

            foreach (HistoryMemoryRecord record in memory?.Memories ?? Array.Empty<HistoryMemoryRecord>())
            {
                RequireReference(errors, "Memory", record.MemoryId, "historicalEventId", record.HistoricalEventId, eventIds, "History");
                RequireReference(errors, "Memory", record.MemoryId, "beliefId", record.BeliefId, beliefIds, "Knowledge");
                RequireReferences(errors, "Memory", record.MemoryId, "evidenceIds", record.EvidenceIds, evidenceIds, "Knowledge");
            }

            foreach (InformationSourceRecord source in sources?.Sources ?? Array.Empty<InformationSourceRecord>())
            {
                RequireReference(errors, "Information Source", source.SourceInstanceId, "parentSourceId", source.Data.parentSourceId, sourceIds, "Information Source");
                RequireReference(errors, "Information Source", source.SourceInstanceId, "originalSourceId", source.Data.originalSourceId, sourceIds, "Information Source");
                RequireReference(errors, "Information Source", source.SourceInstanceId, "supersedesSourceId", source.Data.supersedesSourceId, sourceIds, "Information Source");
                RequireReference(errors, "Information Source", source.SourceInstanceId, "correctedBySourceId", source.Data.correctedBySourceId, sourceIds, "Information Source");
            }

            foreach (PersonSourceAssessmentRecord assessment in sources?.Assessments ?? Array.Empty<PersonSourceAssessmentRecord>())
            {
                RequireReference(errors, "Source Assessment", assessment.AssessmentId, "sourceInstanceId", assessment.SourceInstanceId, sourceIds, "Information Source");
                RequireReferences(errors, "Source Assessment", assessment.AssessmentId, "supportingEvidenceIds", assessment.Data.supportingEvidenceIds, evidenceIds, "Knowledge");
                RequireReferences(errors, "Source Assessment", assessment.AssessmentId, "priorExperienceIds", assessment.Data.priorExperienceIds, memoryIds, "Memory");
                RequireReference(errors, "Source Assessment", assessment.AssessmentId, "supersedesAssessmentId", assessment.Data.supersedesAssessmentId, IdSet(sources?.Assessments.Select(item => item.AssessmentId)), "Source Assessment");
            }

            foreach (SourceTransformationData transformation in sources?.Transformations ?? Array.Empty<SourceTransformationData>())
            {
                RequireReference(errors, "Source Transformation", transformation.transformationId, "fromSourceId", transformation.fromSourceId, sourceIds, "Information Source");
                RequireReference(errors, "Source Transformation", transformation.transformationId, "toSourceId", transformation.toSourceId, sourceIds, "Information Source");
            }

            foreach (InformationTransferRecord transfer in transfers?.Transfers ?? Array.Empty<InformationTransferRecord>())
            {
                RequireReference(errors, "Information Transfer", transfer.TransferId, "parentTransferId", transfer.Data.parentTransferId, transferIds, "Information Transfer");
                RequireReference(errors, "Information Transfer", transfer.TransferId, "correctionOfTransferId", transfer.Data.correctionOfTransferId, transferIds, "Information Transfer");
                RequireReference(errors, "Information Transfer", transfer.TransferId, "retractionOfTransferId", transfer.Data.retractionOfTransferId, transferIds, "Information Transfer");
                RequireReference(errors, "Information Transfer", transfer.TransferId, "immediateSourceId", transfer.Data.immediateSourceId, sourceIds, "Information Source");
                RequireReference(errors, "Information Transfer", transfer.TransferId, "originalSourceId", transfer.Data.originalSourceId, sourceIds, "Information Source");
                RequireReference(errors, "Information Transfer", transfer.TransferId, "createdSourceId", transfer.Data.createdSourceId, sourceIds, "Information Source");

                foreach (TransferContentItemData content in transfer.ContentItems)
                {
                    string contentId = $"{transfer.TransferId}/{content.contentItemId}";
                    RequireReference(errors, "Transfer Content", contentId, "senderEvidenceId", content.senderEvidenceId, evidenceIds, "Knowledge");
                    RequireReference(errors, "Transfer Content", contentId, "senderBeliefId", content.senderBeliefId, beliefIds, "Knowledge");
                    RequireReference(errors, "Transfer Content", contentId, "senderMemoryId", content.senderMemoryId, memoryIds, "Memory");
                    RequireReference(errors, "Transfer Content", contentId, "historicalEventId", content.historicalEventId, eventIds, "History");
                    RequireReference(errors, "Transfer Content", contentId, "lifeEventId", content.lifeEventId, lifeEventIds, "History");
                    RequireReference(errors, "Transfer Content", contentId, "immediateSourceId", content.immediateSourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Transfer Content", contentId, "originalSourceId", content.originalSourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Transfer Content", contentId, "claimedSourceId", content.claimedSourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Transfer Content", contentId, "actualKnownSourceId", content.actualKnownSourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Transfer Content", contentId, "requiredRecipientAccessId", content.requiredRecipientAccessId, accessPolicyIds, "Information Access");
                }

                foreach (TransferRecipientResult recipient in transfer.RecipientResults)
                {
                    string recipientId = $"{transfer.TransferId}/{recipient.RecipientPersonId}";
                    RequireReference(errors, "Transfer Recipient", recipientId, "transferSourceId", recipient.Data.transferSourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Transfer Recipient", recipientId, "immediateSourceId", recipient.Data.immediateSourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Transfer Recipient", recipientId, "originalSourceId", recipient.Data.originalSourceId, sourceIds, "Information Source");
                    if (string.Equals(recipient.RecipientPersonId, runtimes.NormalizedPersonId, StringComparison.Ordinal))
                    {
                        RequireReferences(errors, "Transfer Recipient", recipientId, "createdEvidenceIds", recipient.CreatedEvidenceIds, evidenceIds, "Knowledge");
                        RequireReferences(errors, "Transfer Recipient", recipientId, "resultingBeliefIds", recipient.ResultingBeliefIds, beliefIds, "Knowledge");
                        RequireReferences(errors, "Transfer Recipient", recipientId, "formedMemoryIds", recipient.FormedMemoryIds, memoryIds, "Memory");
                    }
                }
            }

            foreach (InformationAccessGrantRecord grant in access?.Grants ?? Array.Empty<InformationAccessGrantRecord>())
            {
                RequireReference(errors, "Access Grant", grant.GrantId, "policyId", grant.PolicyId, accessPolicyIds, "Information Access");
            }

            foreach (InformationConcealmentRecord concealment in access?.Concealments ?? Array.Empty<InformationConcealmentRecord>())
            {
                RequireReference(errors, "Access Concealment", concealment.ConcealmentId, "policyId", concealment.Data.policyId, accessPolicyIds, "Information Access");
            }

            foreach (KnowledgeRecord record in records?.Records ?? Array.Empty<KnowledgeRecord>())
            {
                KnowledgeRecordData data = record.Data;
                if (!string.IsNullOrWhiteSpace(data.definitionId) && runtimes.DefinitionRegistry != null && !runtimes.DefinitionRegistry.Contains(data.definitionId))
                {
                    errors.Add($"Records: record '{record.RecordId}' references missing definition '{data.definitionId}'.");
                }

                RequireReferences(errors, "Knowledge Record", record.RecordId, "sourceIds", data.sourceIds, sourceIds, "Information Source");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "evidenceIds", data.evidenceIds, evidenceIds, "Knowledge");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "beliefIds", data.beliefIds, beliefIds, "Knowledge");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "memoryIds", data.memoryIds, memoryIds, "Memory");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "historicalEventIds", data.historicalEventIds, eventIds, "History");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "lifeEventIds", data.lifeEventIds, lifeEventIds, "History");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "transferIds", data.transferIds, transferIds, "Information Transfer");
                RequireReferences(errors, "Knowledge Record", record.RecordId, "relatedRecordIds", data.relatedRecordIds, recordIds, "Records");
                RequireReference(errors, "Knowledge Record", record.RecordId, "parentRecordId", data.parentRecordId, recordIds, "Records");
                RequireReference(errors, "Knowledge Record", record.RecordId, "supersedesRecordId", data.supersedesRecordId, recordIds, "Records");
                RequireReference(errors, "Knowledge Record", record.RecordId, "correctedByRecordId", data.correctedByRecordId, recordIds, "Records");
                RequireReference(errors, "Knowledge Record", record.RecordId, "accessPolicyId", data.accessPolicyId, accessPolicyIds, "Information Access");

                foreach (KnowledgeRecordDetailData detail in data.details ?? Array.Empty<KnowledgeRecordDetailData>())
                {
                    string detailId = $"{record.RecordId}/{detail.detailId}";
                    RequireReference(errors, "Knowledge Record Detail", detailId, "sourceId", detail.sourceId, sourceIds, "Information Source");
                    RequireReference(errors, "Knowledge Record Detail", detailId, "evidenceId", detail.evidenceId, evidenceIds, "Knowledge");
                }
            }

            foreach (KnowledgeRecordCollection collection in records?.Collections ?? Array.Empty<KnowledgeRecordCollection>())
            {
                RequireReferences(errors, "Knowledge Record Collection", collection.CollectionId, "recordIds", collection.RecordIds, recordIds, "Records");
            }
        }

        private static HashSet<string> IdSet(IEnumerable<string> values)
        {
            return new HashSet<string>((values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }

        private static void RequireReferences(List<string> errors, string ownerKind, string ownerId, string field, IEnumerable<string> references, HashSet<string> validIds, string targetSubsystem)
        {
            foreach (string reference in references ?? Array.Empty<string>())
            {
                RequireReference(errors, ownerKind, ownerId, field, reference, validIds, targetSubsystem);
            }
        }

        private static void RequireReference(List<string> errors, string ownerKind, string ownerId, string field, string reference, HashSet<string> validIds, string targetSubsystem)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return;
            }

            if (validIds == null || !validIds.Contains(reference))
            {
                errors.Add($"{ownerKind}: '{ownerId}' field '{field}' references missing {targetSubsystem} record '{reference}'.");
            }
        }

        private IReadOnlyList<string> KnownPersons()
        {
            return (runtimes.KnownPersonIds ?? Array.Empty<string>())
                .Concat(new[] { runtimes.NormalizedPersonId })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private IReadOnlyList<string> KnownBodies()
        {
            string bodyId = runtimes.KnowledgeRuntime == null ? string.Empty : runtimes.KnowledgeRuntime.CurrentBodyId;
            return (runtimes.KnownBodyIds ?? Array.Empty<string>())
                .Concat(new[] { bodyId })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private KnowledgeHistorySubsystemReadiness DefinitionReadiness()
        {
            return RuntimeReadiness(KnowledgeHistorySubsystem.Definitions, runtimes.DefinitionRegistry != null, runtimes.DefinitionRegistry?.Count ?? 0, runtimes.DefinitionRegistry == null ? "Missing DefinitionRegistry." : "Ready");
        }

        private static KnowledgeHistorySubsystemReadiness RuntimeReadiness(KnowledgeHistorySubsystem subsystem, bool ready, long revision, string message)
        {
            return new KnowledgeHistorySubsystemReadiness(subsystem, ready ? KnowledgeHistoryReadinessState.Ready : KnowledgeHistoryReadinessState.Missing, revision, message);
        }

        private static KnowledgeHistoryOperationResult Failure(KnowledgeHistoryOperationKind kind, KnowledgeHistoryFailureStage stage, string transactionId, string code, string message, params KnowledgeHistorySubsystem[] participants)
        {
            return Wrap(false, code, message, kind, transactionId, stage, null, participants);
        }

        private static KnowledgeHistoryOperationResult Wrap(bool succeeded, string code, string message, KnowledgeHistoryOperationKind kind, string transactionId, KnowledgeHistoryFailureStage stage, object underlying, params KnowledgeHistorySubsystem[] participants)
        {
            KnowledgeHistoryTransactionDiagnostic diagnostic = new KnowledgeHistoryTransactionDiagnostic(
                transactionId,
                kind,
                stage,
                rollbackAttempted: !succeeded && (kind == KnowledgeHistoryOperationKind.Transfer || kind == KnowledgeHistoryOperationKind.ReadRecord),
                rollbackSucceeded: !succeeded && (kind == KnowledgeHistoryOperationKind.Transfer || kind == KnowledgeHistoryOperationKind.ReadRecord),
                participants,
                new[] { message });
            return new KnowledgeHistoryOperationResult(succeeded, code, message, diagnostic, underlying);
        }
    }
}
