using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Sharing
{
    public sealed class InformationTransferRuntime
    {
        private readonly Dictionary<string, InformationTransferRecordData> transfersById = new Dictionary<string, InformationTransferRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InformationTransferProcessedTransactionData> processedTransactions = new Dictionary<string, InformationTransferProcessedTransactionData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string ownerId;

        public string OwnerId => ownerId ?? string.Empty;
        public long TransferRevision { get; private set; }

        public void Configure(DefinitionRegistry definitionRegistry, string owner)
        {
            registry = definitionRegistry ?? registry;
            ownerId = owner ?? string.Empty;
        }

        public InformationTransferResult PreviewTransfer(InformationTransferRequest request)
        {
            return ExecuteTransfer(request, preview: true, restoring: false);
        }

        public InformationTransferResult ExecuteTransfer(InformationTransferRequest request, bool restoring = false)
        {
            return ExecuteTransfer(request, preview: false, restoring);
        }

        private InformationTransferResult ExecuteTransfer(InformationTransferRequest request, bool preview, bool restoring)
        {
            long priorRevision = TransferRevision;
            if (!ValidateRequest(request, out InformationTransferDefinition definition, out string failure, out InformationTransferStatus status))
            {
                return InformationTransferResult.Failure(status, failure, request?.TransactionId, preview, TransferRevision);
            }

            if (!preview && processedTransactions.TryGetValue(TransactionKey(request.TransactionId), out InformationTransferProcessedTransactionData processed))
            {
                InformationTransferRecord duplicate = transfersById.TryGetValue(processed.transferId ?? string.Empty, out InformationTransferRecordData existing)
                    ? new InformationTransferRecord(existing)
                    : null;
                return InformationTransferResult.Success("Information transfer transaction already processed.", request.TransactionId, duplicate, duplicate?.RecipientResults ?? Array.Empty<TransferRecipientResult>(), priorRevision, TransferRevision, duplicate: true);
            }

            string transferId = string.IsNullOrWhiteSpace(request.TransferId) ? StableTransferId(request) : request.TransferId.Trim();
            if (IsCircular(request.ParentTransferId, transferId) || IsCircular(request.CorrectionOfTransferId, transferId) || IsCircular(request.RetractionOfTransferId, transferId))
            {
                return InformationTransferResult.Failure(InformationTransferStatus.CircularChain, "Information transfer chains cannot be circular.", request.TransactionId, preview, TransferRevision);
            }

            string recallOutcome = "NotRequired";
            if (!ValidateSenderAccess(request, definition, out recallOutcome, out failure, out status))
            {
                return InformationTransferResult.Failure(status, failure, request.TransactionId, preview, TransferRevision);
            }

            TransferRollbackState rollback = preview ? null : CaptureRollbackState(request);
            List<TransferRecipientResultData> recipients = new List<TransferRecipientResultData>();
            string[] recipientIds = OrderedRecipients(request);
            foreach (string recipientId in recipientIds)
            {
                TransferRecipientResultData result = ProcessRecipient(request, definition, transferId, recipientId, preview, out failure, out status);
                recipients.Add(result);
                if (result.status != InformationTransferStatus.Succeeded && result.status != InformationTransferStatus.Preview)
                {
                    rollback?.Restore(registry);
                    return InformationTransferResult.Failure(status, failure, request.TransactionId, preview, TransferRevision);
                }
            }

            InformationTransferRecordData recordData = new InformationTransferRecordData
            {
                transferId = transferId,
                transactionId = request.TransactionId,
                senderPersonId = request.SenderPersonId,
                recipientPersonIds = recipientIds,
                transferDefinitionId = definition.Id,
                mode = ResolveMode(request, definition),
                worldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                locationContextId = request.LocationContextId ?? string.Empty,
                privacyScope = request.PrivacyScope,
                recallRequired = RequiresRecall(request, definition),
                summarizationRequested = request.SummarizationRequested,
                translationRequested = request.TranslationRequested,
                omissionRequested = request.OmissionRequested || request.ContentItems.Any(item => item != null && item.deliberateOmission),
                distortionRequested = request.DistortionRequested || request.ContentItems.Any(item => item != null && item.deliberateDistortion),
                teachingRequested = request.TeachingRequested,
                recipientAcknowledgmentRequired = request.RecipientAcknowledgmentRequired,
                deterministicPolicyId = request.DeterministicPolicyId ?? string.Empty,
                parentTransferId = request.ParentTransferId ?? string.Empty,
                correctionOfTransferId = request.CorrectionOfTransferId ?? string.Empty,
                retractionOfTransferId = request.RetractionOfTransferId ?? string.Empty,
                immediateSourceId = request.ImmediateSourceId ?? string.Empty,
                originalSourceId = request.OriginalSourceId ?? string.Empty,
                createdSourceId = recipients.FirstOrDefault()?.transferSourceId ?? string.Empty,
                contentItems = request.ContentItems.Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.contentItemId, StringComparer.Ordinal).ToArray(),
                recipientResults = recipients.OrderBy(result => result.recipientPersonId, StringComparer.Ordinal).Select(result => result.Clone()).ToArray(),
                senderRecallOutcome = recallOutcome,
                validationFailures = Array.Empty<string>(),
                revision = preview ? TransferRevision : TransferRevision + 1
            };
            InformationTransferRecord record = new InformationTransferRecord(recordData);
            IReadOnlyList<TransferRecipientResult> wrappedRecipients = recipients.Select(result => new TransferRecipientResult(result)).ToArray();
            if (preview)
            {
                return InformationTransferResult.Success("Information transfer preview succeeded.", request.TransactionId, record, wrappedRecipients, priorRevision, priorRevision, preview: true);
            }

            transfersById[transferId] = recordData.Clone();
            TransferRevision++;
            processedTransactions[TransactionKey(request.TransactionId)] = new InformationTransferProcessedTransactionData
            {
                transactionId = request.TransactionId,
                status = InformationTransferStatus.Succeeded,
                transferId = transferId,
                revision = TransferRevision
            };

            return InformationTransferResult.Success(restoring ? "Information transfer restored." : "Information transfer executed.", request.TransactionId, record, wrappedRecipients, priorRevision, TransferRevision);
        }

        private static TransferRollbackState CaptureRollbackState(InformationTransferRequest request)
        {
            return new TransferRollbackState(request);
        }

        public InformationTransferSnapshot CreateSnapshot()
        {
            return new InformationTransferSnapshot(OwnerId, TransferRevision, transfersById.Values.Select(data => new InformationTransferRecord(data)).ToArray());
        }

        public InformationTransferSaveData CreateSaveData()
        {
            return new InformationTransferSaveData
            {
                schemaVersion = InformationTransferSaveData.CurrentSchemaVersion,
                ownerId = OwnerId,
                transferRevision = TransferRevision,
                transfers = transfersById.Values.OrderBy(data => data.transferId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                processedTransactions = processedTransactions.Values.OrderBy(data => data.transactionId, StringComparer.Ordinal).ToArray()
            };
        }

        public InformationTransferResult RestoreFromSaveData(InformationTransferSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, expectedOwnerId, out string failureReason))
            {
                return InformationTransferResult.Failure(InformationTransferStatus.RestoreFailed, failureReason, revision: TransferRevision);
            }

            InformationTransferSaveData rollback = CreateSaveData();
            try
            {
                registry = definitionRegistry ?? registry;
                ownerId = saveData.ownerId ?? string.Empty;
                transfersById.Clear();
                processedTransactions.Clear();

                foreach (InformationTransferRecordData transfer in saveData.transfers ?? Array.Empty<InformationTransferRecordData>())
                {
                    transfersById[transfer.transferId] = transfer.Clone();
                }

                foreach (InformationTransferProcessedTransactionData transaction in saveData.processedTransactions ?? Array.Empty<InformationTransferProcessedTransactionData>())
                {
                    if (!string.IsNullOrWhiteSpace(transaction.transactionId))
                    {
                        processedTransactions[TransactionKey(transaction.transactionId)] = transaction;
                    }
                }

                TransferRevision = Math.Max(0L, saveData.transferRevision);
                return InformationTransferResult.Success("Information transfers restored without replaying sharing effects.", string.Empty, null, Array.Empty<TransferRecipientResult>(), TransferRevision, TransferRevision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, rollback.ownerId, restoring: true);
                return InformationTransferResult.Failure(InformationTransferStatus.RestoreFailed, exception.Message, revision: TransferRevision);
            }
        }

        public static bool ValidateSaveData(InformationTransferSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Information Transfer save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != InformationTransferSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Information Transfer schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedOwnerId) && !string.Equals(saveData.ownerId, expectedOwnerId, StringComparison.Ordinal))
            {
                failureReason = $"Information Transfer save owner '{saveData.ownerId}' does not match expected owner '{expectedOwnerId}'.";
                return false;
            }

            HashSet<string> transferIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, InformationTransferRecordData> transfersBySaveId = new Dictionary<string, InformationTransferRecordData>(StringComparer.Ordinal);
            foreach (InformationTransferRecordData transfer in saveData.transfers ?? Array.Empty<InformationTransferRecordData>())
            {
                if (!ValidateTransferData(transfer, definitionRegistry, out failureReason) || !transferIds.Add(transfer.transferId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Information Transfer save has duplicate transfer ID '{transfer?.transferId}'." : failureReason;
                    return false;
                }

                transfersBySaveId[transfer.transferId] = transfer;
            }

            foreach (InformationTransferRecordData transfer in saveData.transfers ?? Array.Empty<InformationTransferRecordData>())
            {
                if (!ValidateTransferReferences(transfer, transferIds, transfersBySaveId, out failureReason))
                {
                    return false;
                }
            }

            HashSet<string> transactions = new HashSet<string>(StringComparer.Ordinal);
            foreach (InformationTransferProcessedTransactionData transaction in saveData.processedTransactions ?? Array.Empty<InformationTransferProcessedTransactionData>())
            {
                if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId) || !transactions.Add(transaction.transactionId))
                {
                    failureReason = $"Information Transfer save has missing or duplicate transaction '{transaction?.transactionId}'.";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateRequest(InformationTransferRequest request, out InformationTransferDefinition definition, out string failure, out InformationTransferStatus status)
        {
            definition = null;
            failure = string.Empty;
            status = InformationTransferStatus.InvalidRequest;
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                failure = "Information transfer requires a transaction ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.SenderPersonId))
            {
                status = InformationTransferStatus.MissingSender;
                failure = "Information transfer requires a sender Person ID.";
                return false;
            }

            if (request.RecipientPersonIds == null || request.RecipientPersonIds.All(string.IsNullOrWhiteSpace))
            {
                status = InformationTransferStatus.MissingRecipient;
                failure = "Information transfer requires at least one recipient Person ID.";
                return false;
            }

            if (request.ContentItems == null || request.ContentItems.Length == 0 || request.ContentItems.All(item => item == null))
            {
                status = InformationTransferStatus.MissingContent;
                failure = "Information transfer requires structured content.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.TransferDefinitionId) && registry != null && !registry.TryGet(request.TransferDefinitionId, out definition))
            {
                status = InformationTransferStatus.MissingDefinition;
                failure = $"Information Transfer definition '{request.TransferDefinitionId}' does not resolve.";
                return false;
            }


            definition ??= ResolveDefinition(request.Mode);
            if (definition == null)
            {
                status = InformationTransferStatus.MissingDefinition;
                failure = $"No authored Information Transfer definition resolves mode '{request.Mode}'.";
                return false;
            }

            if (definition != null && !DefinitionAllowsRequest(definition, request, out failure))
            {
                status = InformationTransferStatus.InvalidRequest;
                return false;
            }

            return true;
        }

        private static bool DefinitionAllowsRequest(InformationTransferDefinition definition, InformationTransferRequest request, out string failure)
        {
            failure = string.Empty;
            InformationTransferMode mode = ResolveMode(request, definition);
            if (definition.Mode != InformationTransferMode.Unknown && definition.Mode != mode)
            {
                failure = $"Information Transfer definition '{definition.Id}' expects mode {definition.Mode}, not {mode}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredCapabilityId)
                && !(request.SenderCapabilityIds ?? Array.Empty<string>()).Contains(definition.RequiredCapabilityId, StringComparer.Ordinal))
            {
                failure = $"Information Transfer definition '{definition.Id}' requires capability '{definition.RequiredCapabilityId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredMethodId)
                && !(request.AvailableMethodIds ?? Array.Empty<string>()).Contains(definition.RequiredMethodId, StringComparer.Ordinal))
            {
                failure = $"Information Transfer definition '{definition.Id}' requires method '{definition.RequiredMethodId}'.";
                return false;
            }

            KnowledgeDomain[] requestedDomains = (request.ContentItems ?? Array.Empty<TransferContentItemData>())
                .Where(item => item != null && item.domain != KnowledgeDomain.Unknown)
                .Select(item => item.domain)
                .Distinct()
                .ToArray();
            if (definition.SupportedDomains.Count > 0 && requestedDomains.Any(domain => !definition.SupportedDomains.Contains(domain)))
            {
                failure = $"Information Transfer definition '{definition.Id}' does not support every requested Knowledge domain.";
                return false;
            }

            if ((mode == InformationTransferMode.Translation || request.TranslationRequested)
                && (request.SharedLanguageIds == null || request.SharedLanguageIds.Length == 0))
            {
                failure = $"Information Transfer definition '{definition.Id}' requires a declared shared or translation language.";
                return false;
            }

            if (request.SourceRuntime != null && !string.IsNullOrWhiteSpace(request.ImmediateSourceId)
                && request.SourceRuntime.TryGetSource(request.ImmediateSourceId, out InformationSourceRecord source)
                && definition.AllowedSourceCategories.Count > 0
                && !definition.AllowedSourceCategories.Contains(source.Category))
            {
                failure = $"Information Transfer definition '{definition.Id}' does not allow source category '{source.Category}'.";
                return false;
            }

            if (request.SummarizationRequested && !definition.SummarizationAllowed)
            {
                failure = $"Information Transfer definition '{definition.Id}' does not allow summarization.";
                return false;
            }

            if (request.TranslationRequested && !definition.TranslationAllowed)
            {
                failure = $"Information Transfer definition '{definition.Id}' does not allow translation.";
                return false;
            }

            if (mode == InformationTransferMode.Demonstration && !definition.DemonstrationAllowed)
            {
                failure = $"Information Transfer definition '{definition.Id}' does not allow demonstration.";
                return false;
            }

            if (request.PrivacyScope == TransferPrivacyScope.Public && !definition.PublicAllowed
                || (request.PrivacyScope == TransferPrivacyScope.Private || request.PrivacyScope == TransferPrivacyScope.RecipientOnly) && !definition.PrivateAllowed
                || (request.PrivacyScope == TransferPrivacyScope.Secret || request.PrivacyScope == TransferPrivacyScope.HiddenSource) && !definition.SecretAllowed)
            {
                failure = $"Information Transfer definition '{definition.Id}' blocks privacy scope {request.PrivacyScope}.";
                return false;
            }

            return true;
        }

        private InformationTransferDefinition ResolveDefinition(InformationTransferMode mode)
        {
            if (registry == null || mode == InformationTransferMode.Unknown)
            {
                return null;
            }

            InformationTransferDefinition[] matches = registry.DefinitionsById.Values
                .OfType<InformationTransferDefinition>()
                .Where(definition => definition.Mode == mode)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private bool ValidateSenderAccess(InformationTransferRequest request, InformationTransferDefinition definition, out string recallOutcome, out string failure, out InformationTransferStatus status)
        {
            recallOutcome = "NotRequired";
            failure = string.Empty;
            status = InformationTransferStatus.SenderAccessDenied;
            bool requiresRecall = RequiresRecall(request, definition);
            foreach (TransferContentItemData content in request.ContentItems.Where(item => item != null).OrderBy(item => item.contentItemId, StringComparer.Ordinal))
            {
                if (!ValidateAccessPolicyForTransfer(request, content, out failure, out status))
                {
                    return false;
                }

                if (content.deliberateFalsehood && !request.DeliberateFalsehoodAuthorized)
                {
                    status = InformationTransferStatus.SenderAccessDenied;
                    failure = $"Content '{content.contentItemId}' is a deliberate falsehood but the request did not authorize deliberate falsehood.";
                    return false;
                }

                if (content.privacyClassification >= KnowledgeVisibility.Private && !request.PrivilegedAccess && request.PrivacyScope != TransferPrivacyScope.Private && request.PrivacyScope != TransferPrivacyScope.RecipientOnly)
                {
                    status = InformationTransferStatus.PrivacyBlocked;
                    failure = $"Content '{content.contentItemId}' is private and cannot be transferred through {request.PrivacyScope}.";
                    return false;
                }

                if (!content.deliberateFalsehood && content.proposition != null && request.SenderKnowledge != null)
                {
                    if (!request.SenderKnowledge.TryGetBelief(content.proposition, out KnowledgeBeliefRecord belief) || belief.State == KnowledgeBeliefState.Forgotten)
                    {
                        failure = $"Sender '{request.SenderPersonId}' does not have accessible knowledge for content '{content.contentItemId}'.";
                        return false;
                    }

                    if (belief.Data.visibility >= KnowledgeVisibility.Private && !request.PrivilegedAccess && request.PrivacyScope == TransferPrivacyScope.Public)
                    {
                        status = InformationTransferStatus.PrivacyBlocked;
                        failure = $"Sender belief '{belief.BeliefId}' is private and cannot be publicly transferred.";
                        return false;
                    }
                }

                if (requiresRecall && !string.IsNullOrWhiteSpace(content.senderMemoryId))
                {
                    if (request.SenderMemory == null)
                    {
                        status = InformationTransferStatus.RecallFailed;
                        failure = "Recall-required transfer has no sender memory runtime.";
                        return false;
                    }

                    MemoryRecallResult recall = request.SenderMemory.Recall(new MemoryRecallRequest
                    {
                        TransactionId = $"{request.TransactionId}.recall.{content.contentItemId}",
                        RequestingPersonId = request.SenderPersonId,
                        MemoryId = content.senderMemoryId,
                        WorldTime = request.WorldTimeSeconds,
                        AttemptDifficult = true,
                        AllowCueRecovery = true,
                        ReinforceOnSuccess = false,
                        MutateMetadata = false,
                        AccessContext = MemoryAccessContext.OrdinaryRecall
                    }, preview: true);
                    recallOutcome = recall.Outcome.ToString();
                    if (!recall.Succeeded)
                    {
                        status = InformationTransferStatus.RecallFailed;
                        failure = $"Sender recall failed for content '{content.contentItemId}': {recall.Message}";
                        return false;
                    }

                    TransferContentItemData mutableContent = content;
                    mutableContent.omittedDetailIds = recall.Entries.SelectMany(entry => entry.UnavailableDetails).Select(detail => detail.detailId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                }
            }

            return true;
        }

        private static bool ValidateAccessPolicyForTransfer(InformationTransferRequest request, TransferContentItemData content, out string failure, out InformationTransferStatus status)
        {
            failure = string.Empty;
            status = InformationTransferStatus.PrivacyBlocked;
            if (request.AccessRuntime == null || request.PrivilegedAccess)
            {
                return true;
            }

            string policyId = !string.IsNullOrWhiteSpace(request.AccessPolicyId)
                ? request.AccessPolicyId
                : !string.IsNullOrWhiteSpace(content.requiredRecipientAccessId)
                    ? content.requiredRecipientAccessId
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(policyId))
            {
                return true;
            }

            InformationSubjectReferenceData subject = request.AccessSubject?.Clone() ?? SubjectFromContent(content);
            InformationAccessMode mode = string.IsNullOrWhiteSpace(request.ParentTransferId)
                ? InformationAccessMode.Share
                : InformationAccessMode.Reshare;
            InformationAccessDecision decision = request.AccessRuntime.EvaluateAccess(new InformationAccessContext
            {
                RequestingPersonId = request.SenderPersonId,
                ActingEntityId = request.SenderPersonId,
                Subject = subject,
                Purpose = InformationAccessPurpose.Transfer,
                WorldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                AccessMode = mode,
                RequestedDetailIds = content.includedDetailIds ?? Array.Empty<string>(),
                AuthorizationIds = request.SenderAuthorizationIds ?? Array.Empty<string>(),
                OrganizationIds = request.SenderOrganizationIds ?? Array.Empty<string>(),
                RoleIds = request.SenderRoleIds ?? Array.Empty<string>(),
                NeedToKnowTags = request.SenderNeedToKnowTags ?? Array.Empty<string>(),
                HasDiscoveredSubject = true,
                RedactedAccessAcceptable = request.SummarizationRequested || request.OmissionRequested || request.PrivacyScope != TransferPrivacyScope.Public,
                DeterministicPolicyId = policyId
            });

            if (decision.Denied)
            {
                failure = $"Access policy '{policyId}' denied {mode} for content '{content.contentItemId}': {decision.DenialCode}. {decision.DiagnosticReason}";
                return false;
            }

            if (mode == InformationAccessMode.Reshare && decision.ResharingOutcome == InformationResharingPolicy.NoResharing)
            {
                failure = $"Access policy '{policyId}' does not allow resharing for content '{content.contentItemId}'.";
                return false;
            }

            return true;
        }

        private static InformationSubjectReferenceData SubjectFromContent(TransferContentItemData content)
        {
            string subjectId = !string.IsNullOrWhiteSpace(content.senderMemoryId)
                ? content.senderMemoryId
                : !string.IsNullOrWhiteSpace(content.historicalEventId)
                    ? content.historicalEventId
                    : !string.IsNullOrWhiteSpace(content.lifeEventId)
                        ? content.lifeEventId
                        : !string.IsNullOrWhiteSpace(content.senderBeliefId)
                            ? content.senderBeliefId
                            : !string.IsNullOrWhiteSpace(content.senderEvidenceId)
                                ? content.senderEvidenceId
                                : content.contentItemId;

            return new InformationSubjectReferenceData
            {
                subjectType = content.contentType switch
                {
                    InformationTransferContentType.MemoryStatement => InformationSubjectType.Memory,
                    InformationTransferContentType.HistoricalEventReference => InformationSubjectType.HistoricalEvent,
                    InformationTransferContentType.LifeEventReference => InformationSubjectType.LifeEvent,
                    InformationTransferContentType.EvidenceReference => InformationSubjectType.Evidence,
                    InformationTransferContentType.SourceIdentity => InformationSubjectType.SourceIdentity,
                    InformationTransferContentType.ConditionOrDiagnosis => InformationSubjectType.Diagnosis,
                    _ => InformationSubjectType.TransferContent
                },
                subjectId = subjectId ?? string.Empty,
                tags = new[] { "transfer-content" }
            };
        }

        private TransferRecipientResultData ProcessRecipient(InformationTransferRequest request, InformationTransferDefinition definition, string transferId, string recipientId, bool preview, out string failure, out InformationTransferStatus status)
        {
            failure = string.Empty;
            status = preview ? InformationTransferStatus.Preview : InformationTransferStatus.Succeeded;
            TransferUnderstandingState understanding = ResolveUnderstanding(request, definition, recipientId);
            string sourceId = string.IsNullOrWhiteSpace(request.CreatedSourceId)
                ? $"information-source.transfer.{Sanitize(transferId)}.{Sanitize(recipientId)}"
                : request.CreatedSourceId;

            InformationSourceOperationResult sourceResult = EnsureTransferSource(request, definition, recipientId, sourceId, preview);
            if (sourceResult != null && !sourceResult.Succeeded)
            {
                status = InformationTransferStatus.SourceFailure;
                failure = sourceResult.Message;
                return RecipientFailure(recipientId, status, understanding, sourceId, failure);
            }

            SourceReliabilityResult reliability = EvaluateTransferReliability(request, recipientId, sourceId);

            List<string> delivered = new List<string>();
            List<string> omitted = new List<string>();
            List<string> misunderstood = new List<string>();
            List<string> rejected = new List<string>();
            List<string> evidenceIds = new List<string>();
            List<string> beliefIds = new List<string>();
            int rawTotal = 0;
            int effectiveTotal = 0;

            bool createEvidence = EvidencePolicy(request, definition) != TransferEvidencePolicy.None && understanding != TransferUnderstandingState.Rejected && understanding != TransferUnderstandingState.Deferred;
            if (createEvidence && request.RecipientKnowledgeRuntimes != null && request.RecipientKnowledgeRuntimes.TryGetValue(recipientId, out PersonKnowledgeRuntime recipientKnowledge))
            {
                foreach (TransferContentItemData content in request.ContentItems.Where(item => item?.proposition != null).OrderBy(item => item.contentItemId, StringComparer.Ordinal))
                {
                    delivered.Add(content.contentItemId);
                    omitted.AddRange(content.omittedDetailIds ?? Array.Empty<string>());
                    if (understanding == TransferUnderstandingState.Partial || understanding == TransferUnderstandingState.Misinterpreted)
                    {
                        misunderstood.Add(content.contentItemId);
                    }

                    int raw = KnowledgeConfidence.Clamp(content.rawEvidenceStrength > 0 ? content.rawEvidenceStrength : definition?.DefaultEvidenceStrength ?? 650);
                    int effective = request.SourceRuntime == null ? raw : request.SourceRuntime.CalculateEffectiveEvidenceStrength(raw, reliability);
                    effective = ApplyUnderstanding(effective, understanding);
                    rawTotal += raw;
                    effectiveTotal += effective;
                    KnowledgeOperationResult knowledge = preview
                        ? recipientKnowledge.PreviewObservation(ObservationRequest(request, content, recipientId, sourceId, raw, effective, reliability))
                        : recipientKnowledge.RecordObservation(ObservationRequest(request, content, recipientId, sourceId, raw, effective, reliability));
                    if (!knowledge.Succeeded)
                    {
                        status = InformationTransferStatus.KnowledgeRejected;
                        failure = knowledge.Message;
                        return RecipientFailure(recipientId, status, understanding, sourceId, failure);
                    }

                    if (knowledge.Evidence != null)
                    {
                        evidenceIds.Add(knowledge.Evidence.EvidenceId);
                    }

                    if (knowledge.ResultingBelief != null)
                    {
                        beliefIds.Add(knowledge.ResultingBelief.BeliefId);
                    }
                }
            }

            List<string> memoryIds = new List<string>();
            if (ShouldFormMemory(request, definition) && request.RecipientMemoryRuntimes != null && request.RecipientMemoryRuntimes.TryGetValue(recipientId, out PersonMemoryRuntime memory))
            {
                string memoryId = $"memory.transfer.{Sanitize(transferId)}.{Sanitize(recipientId)}";
                HistoryOperationResult memoryResult = memory.FormMemory(new FormMemoryRequest
                {
                    TransactionId = $"{request.TransactionId}.memory.{recipientId}",
                    MemoryId = memoryId,
                    OwnerPersonId = recipientId,
                    EvidenceIds = evidenceIds.ToArray(),
                    Source = request.TeachingRequested ? HistoryMemorySource.KnowledgeSharing : HistoryMemorySource.WitnessTestimony,
                    FormedAtWorldTime = Math.Max(0d, request.WorldTimeSeconds),
                    RememberedOccurredAtWorldTime = Math.Max(0d, request.WorldTimeSeconds),
                    Confidence = Math.Max(300, effectiveTotal == 0 ? 500 : effectiveTotal / Math.Max(1, evidenceIds.Count)),
                    Clarity = ApplyUnderstanding(definition?.DefaultFidelity ?? 700, understanding),
                    Salience = request.TeachingRequested ? 700 : 500,
                    FirstHand = false,
                    Visibility = PrivacyToKnowledgeVisibility(request.PrivacyScope),
                    DebugDescription = $"Communication memory for transfer {transferId}.",
                    Tags = new[] { "feature.8.7", "information-transfer", request.Mode.ToString() }
                }, null, preview);
                if (!memoryResult.Succeeded && memoryResult.Code != HistoryResultCode.Duplicate)
                {
                    status = InformationTransferStatus.MemoryRejected;
                    failure = memoryResult.Message;
                    return RecipientFailure(recipientId, status, understanding, sourceId, failure);
                }

                if (memoryResult.Memory != null)
                {
                    memoryIds.Add(memoryResult.Memory.MemoryId);
                }
            }

            return new TransferRecipientResultData
            {
                recipientPersonId = recipientId,
                status = preview ? InformationTransferStatus.Preview : InformationTransferStatus.Succeeded,
                understanding = understanding,
                inheritedConfidence = KnowledgeConfidence.Clamp(effectiveTotal == 0 ? reliability?.DerivedOverall ?? 500 : effectiveTotal / Math.Max(1, evidenceIds.Count)),
                rawEvidenceStrength = KnowledgeConfidence.Clamp(rawTotal),
                effectiveEvidenceStrength = KnowledgeConfidence.Clamp(effectiveTotal),
                reliabilityPolicyId = reliability?.Request?.PolicyId ?? request.DeterministicPolicyId,
                reliabilityEvaluationId = reliability == null ? string.Empty : $"transfer-reliability.{transferId}.{recipientId}",
                transferSourceId = sourceId,
                immediateSourceId = sourceId,
                originalSourceId = reliability?.Chain?.OriginalSourceId ?? request.OriginalSourceId ?? request.ImmediateSourceId ?? sourceId,
                deliveredContentItemIds = delivered.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                omittedContentItemIds = omitted.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                misunderstoodContentItemIds = misunderstood.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                rejectedContentItemIds = rejected.ToArray(),
                createdEvidenceIds = evidenceIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                resultingBeliefIds = beliefIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                formedMemoryIds = memoryIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                persistenceStateChanged = !preview && (evidenceIds.Count > 0 || memoryIds.Count > 0),
                message = "Recipient processed transfer."
            };
        }

        private static SourceReliabilityResult EvaluateTransferReliability(InformationTransferRequest request, string recipientId, string transferSourceId)
        {
            if (request.SourceRuntime == null)
            {
                return null;
            }

            SourceReliabilityResult transferReliability = EvaluateReliability(request, recipientId, transferSourceId);
            string immediateSourceId = request.ImmediateSourceId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(immediateSourceId) || string.Equals(immediateSourceId, transferSourceId, StringComparison.Ordinal))
            {
                return transferReliability;
            }

            SourceReliabilityResult immediateReliability = EvaluateReliability(request, recipientId, immediateSourceId);
            return immediateReliability?.Succeeded == true && immediateReliability.PersonAssessment != null && transferReliability?.PersonAssessment == null
                ? immediateReliability
                : transferReliability;
        }

        private static SourceReliabilityResult EvaluateReliability(InformationTransferRequest request, string recipientId, string sourceId)
        {
            return request.SourceRuntime?.EvaluateReliability(new SourceReliabilityRequest
            {
                EvaluatingPersonId = recipientId,
                SourceInstanceId = sourceId,
                Domain = DominantDomain(request),
                SubjectId = FirstSubject(request),
                MethodId = request.TransferDefinitionId,
                WorldTimeSeconds = request.WorldTimeSeconds,
                PrivilegedAccess = request.PrivilegedAccess,
                PolicyId = request.DeterministicPolicyId
            });
        }

        private InformationSourceOperationResult EnsureTransferSource(InformationTransferRequest request, InformationTransferDefinition definition, string recipientId, string sourceId, bool preview)
        {
            if (request.SourceRuntime == null)
            {
                return InformationSourceOperationResult.Success("No source runtime supplied; transfer source omitted.", request.TransactionId, null, null, 0, 0, preview);
            }

            string parentSourceId = request.ImmediateSourceId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(parentSourceId) && request.SourceRuntime.TryGetSource(parentSourceId, out _))
            {
                return request.SourceRuntime.TransformSource(new SourceTransformationRequest
                {
                    TransactionId = $"{request.TransactionId}.source.{recipientId}",
                    ParentSourceId = parentSourceId,
                    SourceInstanceId = sourceId,
                    TransformationType = TransformationTypeFor(request),
                    ActorPersonId = request.SenderPersonId,
                    WorldTimeSeconds = request.WorldTimeSeconds,
                    Quality = definition?.DefaultFidelity ?? 800,
                    HidesOriginal = request.PrivacyScope == TransferPrivacyScope.HiddenSource || request.PrivacyScope == TransferPrivacyScope.Secret,
                    Note = $"Information transfer {request.Mode}"
                }, preview);
            }

            return request.SourceRuntime.RegisterSource(new InformationSourceRegistrationRequest
            {
                TransactionId = $"{request.TransactionId}.source.{recipientId}",
                SourceInstanceId = sourceId,
                Category = SourceCategoryFor(request),
                ReferenceType = request.PrivacyScope == TransferPrivacyScope.HiddenSource || request.PrivacyScope == TransferPrivacyScope.Secret
                    ? InformationSourceReferenceType.None
                    : InformationSourceReferenceType.Person,
                ReferencedId = request.PrivacyScope == TransferPrivacyScope.HiddenSource || request.PrivacyScope == TransferPrivacyScope.Secret
                    ? string.Empty
                    : request.SenderPersonId,
                OriginalCreatorPersonId = request.SenderPersonId,
                ObserverPersonId = recipientId,
                HolderPersonId = recipientId,
                TransmitterPersonId = request.SenderPersonId,
                CreationWorldTimeSeconds = request.WorldTimeSeconds,
                ObservationWorldTimeSeconds = request.WorldTimeSeconds,
                TransmissionWorldTimeSeconds = request.WorldTimeSeconds,
                Domain = DominantDomain(request),
                SubjectId = FirstSubject(request),
                MethodId = request.TransferDefinitionId,
                Privacy = SourcePrivacyFor(request.PrivacyScope),
                Tags = new[] { "feature.8.7", request.Mode.ToString() }
            }, preview);
        }

        private KnowledgeObservationRequest ObservationRequest(InformationTransferRequest request, TransferContentItemData content, string recipientId, string sourceId, int raw, int effective, SourceReliabilityResult reliability)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = recipientId,
                TransactionId = $"{request.TransactionId}.knowledge.{recipientId}.{content.contentItemId}",
                Proposition = content.proposition?.Clone(),
                AcquisitionSource = request.TeachingRequested ? KnowledgeAcquisitionSource.SkillOrEducation : request.Mode == InformationTransferMode.WrittenMessage || request.Mode == InformationTransferMode.Report ? KnowledgeAcquisitionSource.WrittenSource : KnowledgeAcquisitionSource.Testimony,
                Provenance = request.TeachingRequested ? KnowledgeProvenance.SkillKnowledge : request.Mode == InformationTransferMode.Report ? KnowledgeProvenance.Document : KnowledgeProvenance.Testimony,
                Direction = content.assertionType == InformationTransferAssertionType.Correction ? KnowledgeEvidenceDirection.Corrects : KnowledgeEvidenceDirection.Supports,
                Strength = raw,
                EffectiveStrengthOverride = effective,
                Credibility = reliability?.DerivedOverall ?? effective,
                GameTimeSeconds = request.WorldTimeSeconds,
                SourceId = request.SenderPersonId,
                InformationSourceId = sourceId,
                ReliabilityPolicyId = reliability?.Request?.PolicyId ?? request.DeterministicPolicyId,
                ReliabilityEvaluationId = reliability == null ? string.Empty : $"transfer-reliability.{request.TransactionId}.{recipientId}.{content.contentItemId}",
                Visibility = PrivacyToKnowledgeVisibility(request.PrivacyScope),
                RelatedEventId = content.historicalEventId,
                Tags = new[] { "feature.8.7", "information-transfer", request.Mode.ToString() },
                PrivateAccessAuthorized = request.PrivilegedAccess || request.PrivacyScope == TransferPrivacyScope.Private || request.PrivacyScope == TransferPrivacyScope.RecipientOnly
            };
        }

        private static bool ValidateTransferData(InformationTransferRecordData transfer, DefinitionRegistry definitionRegistry, out string failureReason)
        {
            failureReason = string.Empty;
            if (transfer == null || string.IsNullOrWhiteSpace(transfer.transferId))
            {
                failureReason = "Information Transfer record is missing an ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(transfer.senderPersonId))
            {
                failureReason = $"Information Transfer '{transfer.transferId}' is missing a sender.";
                return false;
            }

            if (transfer.recipientPersonIds == null || transfer.recipientPersonIds.Length == 0 || transfer.recipientPersonIds.Any(string.IsNullOrWhiteSpace))
            {
                failureReason = $"Information Transfer '{transfer.transferId}' has no valid recipients.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(transfer.transferDefinitionId)
                && definitionRegistry != null
                && !definitionRegistry.TryGet(transfer.transferDefinitionId, out InformationTransferDefinition _))
            {
                failureReason = $"Information Transfer '{transfer.transferId}' references missing definition '{transfer.transferDefinitionId}'.";
                return false;
            }

            HashSet<string> recipients = new HashSet<string>((transfer.recipientPersonIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            foreach (TransferRecipientResultData recipient in transfer.recipientResults ?? Array.Empty<TransferRecipientResultData>())
            {
                if (recipient == null || string.IsNullOrWhiteSpace(recipient.recipientPersonId) || !recipients.Contains(recipient.recipientPersonId))
                {
                    failureReason = $"Information Transfer '{transfer.transferId}' has a recipient result for a non-recipient '{recipient?.recipientPersonId}'.";
                    return false;
                }
            }

            HashSet<string> contentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TransferContentItemData content in transfer.contentItems ?? Array.Empty<TransferContentItemData>())
            {
                if (content == null || string.IsNullOrWhiteSpace(content.contentItemId) || !contentIds.Add(content.contentItemId))
                {
                    failureReason = $"Information Transfer '{transfer.transferId}' has missing or duplicate content ID '{content?.contentItemId}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateTransferReferences(InformationTransferRecordData transfer, HashSet<string> transferIds, Dictionary<string, InformationTransferRecordData> transfersBySaveId, out string failureReason)
        {
            failureReason = string.Empty;
            if (transfer == null)
            {
                failureReason = "Information Transfer reference validation received a null record.";
                return false;
            }

            if (!ValidateReference("parent", transfer.transferId, transfer.parentTransferId, transferIds, out failureReason)
                || !ValidateReference("correction", transfer.transferId, transfer.correctionOfTransferId, transferIds, out failureReason)
                || !ValidateReference("retraction", transfer.transferId, transfer.retractionOfTransferId, transferIds, out failureReason))
            {
                return false;
            }

            if (CreatesSavedCycle(transfer, transfersBySaveId, out failureReason))
            {
                return false;
            }

            return true;
        }

        private static bool ValidateReference(string label, string transferId, string referencedTransferId, HashSet<string> transferIds, out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(referencedTransferId))
            {
                return true;
            }

            if (string.Equals(transferId, referencedTransferId, StringComparison.Ordinal))
            {
                failureReason = $"Information Transfer '{transferId}' cannot {label}-reference itself.";
                return false;
            }

            if (!transferIds.Contains(referencedTransferId))
            {
                failureReason = $"Information Transfer '{transferId}' references missing {label} transfer '{referencedTransferId}'.";
                return false;
            }

            return true;
        }

        private static bool CreatesSavedCycle(InformationTransferRecordData transfer, Dictionary<string, InformationTransferRecordData> transfersBySaveId, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            string current = transfer.transferId;
            while (!string.IsNullOrWhiteSpace(current) && transfersBySaveId.TryGetValue(current, out InformationTransferRecordData record))
            {
                if (!visited.Add(current))
                {
                    failureReason = $"Information Transfer save contains a circular chain involving '{transfer.transferId}'.";
                    return true;
                }

                current = !string.IsNullOrWhiteSpace(record.parentTransferId) ? record.parentTransferId
                    : !string.IsNullOrWhiteSpace(record.correctionOfTransferId) ? record.correctionOfTransferId
                    : record.retractionOfTransferId;
            }

            return false;
        }

        private static TransferRecipientResultData RecipientFailure(string recipientId, InformationTransferStatus status, TransferUnderstandingState understanding, string sourceId, string message)
        {
            return new TransferRecipientResultData
            {
                recipientPersonId = recipientId,
                status = status,
                understanding = understanding,
                transferSourceId = sourceId,
                message = message
            };
        }

        private bool IsCircular(string parentId, string transferId)
        {
            if (string.IsNullOrWhiteSpace(parentId))
            {
                return false;
            }

            if (string.Equals(parentId, transferId, StringComparison.Ordinal))
            {
                return true;
            }

            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { transferId };
            string current = parentId;
            while (!string.IsNullOrWhiteSpace(current) && transfersById.TryGetValue(current, out InformationTransferRecordData record))
            {
                if (!visited.Add(current))
                {
                    return true;
                }

                current = string.IsNullOrWhiteSpace(record.parentTransferId) ? record.correctionOfTransferId : record.parentTransferId;
            }

            return false;
        }

        private static string[] OrderedRecipients(InformationTransferRequest request)
        {
            return (request.RecipientPersonIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static bool RequiresRecall(InformationTransferRequest request, InformationTransferDefinition definition)
        {
            return request.SenderRecallRequired || definition != null && definition.RecallRequired;
        }

        private static TransferEvidencePolicy EvidencePolicy(InformationTransferRequest request, InformationTransferDefinition definition)
        {
            return definition?.EvidencePolicy ?? TransferEvidencePolicy.CreateRecipientEvidence;
        }

        private static bool ShouldFormMemory(InformationTransferRequest request, InformationTransferDefinition definition)
        {
            TransferMemoryPolicy policy = definition?.MemoryPolicy ?? TransferMemoryPolicy.FormCommunicationMemory;
            return policy == TransferMemoryPolicy.FormCommunicationMemory || policy == TransferMemoryPolicy.FormOrReinforce || request.TeachingRequested;
        }

        private static InformationTransferMode ResolveMode(InformationTransferRequest request, InformationTransferDefinition definition)
        {
            return request.Mode == InformationTransferMode.Unknown ? definition?.Mode ?? InformationTransferMode.DirectTestimony : request.Mode;
        }

        private static TransferUnderstandingState ResolveUnderstanding(InformationTransferRequest request, InformationTransferDefinition definition, string recipientId)
        {
            int fidelity = definition?.DefaultFidelity ?? 800;
            int completeness = definition?.DefaultCompleteness ?? 800;
            if (request.DistortionRequested)
            {
                return TransferUnderstandingState.Misinterpreted;
            }

            if (request.OmissionRequested || request.SummarizationRequested || completeness < 650)
            {
                return TransferUnderstandingState.Partial;
            }

            if (request.TranslationRequested && fidelity < 700)
            {
                return TransferUnderstandingState.TranslationLimited;
            }

            return TransferUnderstandingState.Complete;
        }

        private static int ApplyUnderstanding(int value, TransferUnderstandingState understanding)
        {
            double factor = understanding switch
            {
                TransferUnderstandingState.Complete => 1d,
                TransferUnderstandingState.Partial => 0.75d,
                TransferUnderstandingState.Ambiguous => 0.6d,
                TransferUnderstandingState.Misinterpreted => 0.45d,
                TransferUnderstandingState.DomainInsufficient => 0.5d,
                TransferUnderstandingState.TranslationLimited => 0.65d,
                TransferUnderstandingState.TerminologyLimited => 0.65d,
                TransferUnderstandingState.ContextLimited => 0.65d,
                TransferUnderstandingState.Rejected => 0d,
                TransferUnderstandingState.Deferred => 0.25d,
                _ => 0.5d
            };
            return KnowledgeConfidence.Clamp((int)Math.Round(value * factor));
        }

        private static KnowledgeDomain DominantDomain(InformationTransferRequest request)
        {
            return request.ContentItems?.FirstOrDefault(item => item != null && item.domain != KnowledgeDomain.Unknown)?.domain ?? KnowledgeDomain.Unknown;
        }

        private static string FirstSubject(InformationTransferRequest request)
        {
            return request.ContentItems?.FirstOrDefault(item => item?.proposition != null)?.proposition?.subjectId ?? string.Empty;
        }

        private static InformationSourceCategory SourceCategoryFor(InformationTransferRequest request)
        {
            return request.Mode switch
            {
                InformationTransferMode.PublicAnnouncement => InformationSourceCategory.PublicAnnouncement,
                InformationTransferMode.WrittenMessage => InformationSourceCategory.WrittenRecord,
                InformationTransferMode.Letter => InformationSourceCategory.Letter,
                InformationTransferMode.BookReading => InformationSourceCategory.Book,
                InformationTransferMode.Report => InformationSourceCategory.InstitutionalReport,
                InformationTransferMode.FormalLesson => InformationSourceCategory.ExpertTestimony,
                InformationTransferMode.Demonstration => InformationSourceCategory.DirectParticipation,
                InformationTransferMode.RumorRetelling => InformationSourceCategory.Hearsay,
                _ => InformationSourceCategory.PersonalTestimony
            };
        }

        private static InformationSourceTransformationType TransformationTypeFor(InformationTransferRequest request)
        {
            if (request.TranslationRequested || request.Mode == InformationTransferMode.Translation)
            {
                return InformationSourceTransformationType.Translation;
            }

            if (request.SummarizationRequested || request.Mode == InformationTransferMode.Summary)
            {
                return InformationSourceTransformationType.Summary;
            }

            if (request.Mode == InformationTransferMode.Copy)
            {
                return InformationSourceTransformationType.Copy;
            }

            if (!string.IsNullOrWhiteSpace(request.CorrectionOfTransferId) || request.Mode == InformationTransferMode.Explanation)
            {
                return InformationSourceTransformationType.Correction;
            }

            return InformationSourceTransformationType.Inference;
        }

        private static SourcePrivacyLevel SourcePrivacyFor(TransferPrivacyScope scope)
        {
            return scope switch
            {
                TransferPrivacyScope.Public => SourcePrivacyLevel.Public,
                TransferPrivacyScope.RecipientOnly => SourcePrivacyLevel.Personal,
                TransferPrivacyScope.Private => SourcePrivacyLevel.Private,
                TransferPrivacyScope.Confidential => SourcePrivacyLevel.Private,
                TransferPrivacyScope.Restricted => SourcePrivacyLevel.Private,
                TransferPrivacyScope.HiddenSource => SourcePrivacyLevel.Hidden,
                TransferPrivacyScope.Secret => SourcePrivacyLevel.Secret,
                _ => SourcePrivacyLevel.Shared
            };
        }

        private static KnowledgeVisibility PrivacyToKnowledgeVisibility(TransferPrivacyScope scope)
        {
            return scope switch
            {
                TransferPrivacyScope.Public => KnowledgeVisibility.Public,
                TransferPrivacyScope.RecipientOnly => KnowledgeVisibility.PersonallyObservable,
                TransferPrivacyScope.Private => KnowledgeVisibility.Private,
                TransferPrivacyScope.Confidential => KnowledgeVisibility.Confidential,
                TransferPrivacyScope.Restricted => KnowledgeVisibility.Confidential,
                TransferPrivacyScope.HiddenSource => KnowledgeVisibility.Hidden,
                TransferPrivacyScope.Secret => KnowledgeVisibility.Secret,
                _ => KnowledgeVisibility.Private
            };
        }

        private static string StableTransferId(InformationTransferRequest request)
        {
            return $"information-transfer.runtime.{Sanitize(request.SenderPersonId)}.{Sanitize(request.TransactionId)}";
        }

        private static string TransactionKey(string transactionId)
        {
            return transactionId ?? string.Empty;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "none";
            }

            return new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        }

        private sealed class TransferRollbackState
        {
            private readonly InformationSourceRuntime sourceRuntime;
            private readonly InformationSourceSaveData sourceSaveData;
            private readonly Dictionary<PersonKnowledgeRuntime, PersonKnowledgeSaveData> knowledgeSaves = new Dictionary<PersonKnowledgeRuntime, PersonKnowledgeSaveData>();
            private readonly Dictionary<PersonMemoryRuntime, PersonMemorySaveData> memorySaves = new Dictionary<PersonMemoryRuntime, PersonMemorySaveData>();

            public TransferRollbackState(InformationTransferRequest request)
            {
                sourceRuntime = request?.SourceRuntime;
                sourceSaveData = sourceRuntime?.CreateSaveData();

                foreach (PersonKnowledgeRuntime runtime in (request?.RecipientKnowledgeRuntimes?.Values ?? Array.Empty<PersonKnowledgeRuntime>()).Where(runtime => runtime != null).Distinct())
                {
                    knowledgeSaves[runtime] = runtime.CreateSaveData();
                }

                foreach (PersonMemoryRuntime runtime in (request?.RecipientMemoryRuntimes?.Values ?? Array.Empty<PersonMemoryRuntime>()).Where(runtime => runtime != null).Distinct())
                {
                    memorySaves[runtime] = runtime.CreateSaveData();
                }
            }

            public void Restore(DefinitionRegistry registry)
            {
                if (sourceRuntime != null && sourceSaveData != null)
                {
                    sourceRuntime.RestoreFromSaveData(sourceSaveData, registry, sourceSaveData.ownerId, restoring: true);
                }

                foreach (KeyValuePair<PersonKnowledgeRuntime, PersonKnowledgeSaveData> pair in knowledgeSaves)
                {
                    pair.Key.RestoreFromSaveData(pair.Value, registry, pair.Value.personId, restoring: true);
                }

                foreach (KeyValuePair<PersonMemoryRuntime, PersonMemorySaveData> pair in memorySaves)
                {
                    pair.Key.RestoreFromSaveData(pair.Value, registry, null, new[] { pair.Value.personId }, restoring: true);
                }
            }
        }
    }
}
