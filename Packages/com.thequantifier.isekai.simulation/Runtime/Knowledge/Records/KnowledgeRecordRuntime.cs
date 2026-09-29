using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Records
{
    public sealed class KnowledgeRecordRuntime
    {
        private readonly Dictionary<string, KnowledgeRecordData> recordsById = new Dictionary<string, KnowledgeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, KnowledgeRecordCollectionData> collectionsById = new Dictionary<string, KnowledgeRecordCollectionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, KnowledgeRecordProcessedTransactionData> processedTransactions = new Dictionary<string, KnowledgeRecordProcessedTransactionData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string ownerId;

        public string OwnerId => ownerId ?? string.Empty;
        public long RecordRevision { get; private set; }

        public void Configure(DefinitionRegistry definitionRegistry, string owner)
        {
            registry = definitionRegistry ?? registry;
            ownerId = owner ?? string.Empty;
        }

        public bool ValidateConfiguredSaveData(KnowledgeRecordSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, out string failure)
        {
            return ValidateSaveData(saveData, definitionRegistry ?? registry, expectedOwnerId, out failure);
        }

        public KnowledgeRecordOperationResult CreateRecord(KnowledgeRecordCreateRequest request)
        {
            long prior = RecordRevision;
            if (request != null && !request.Preview && IsDuplicate(request.TransactionId ?? string.Empty, "create", request.RecordId ?? string.Empty, out KnowledgeRecordOperationResult earlyDuplicate))
            {
                return earlyDuplicate;
            }

            if (!ValidateCreateRequest(request, out KnowledgeRecordDefinition definition, out string failure, out KnowledgeRecordResultCode code))
            {
                return KnowledgeRecordOperationResult.Failure(code, failure, request?.TransactionId, request?.Preview ?? false, RecordRevision);
            }

            string transactionId = request.TransactionId ?? string.Empty;
            string recordId = request.RecordId.Trim();
            KnowledgeRecordData data = BuildRecordData(request, definition);
            KnowledgeRecord record = new KnowledgeRecord(data);
            if (request.Preview)
            {
                return KnowledgeRecordOperationResult.Success("Knowledge record preview succeeded.", transactionId, prior, prior, record, preview: true);
            }

            recordsById[recordId] = data;
            RecordRevision++;
            Remember(transactionId, "create", recordId);
            return KnowledgeRecordOperationResult.Success("Knowledge record created.", transactionId, prior, RecordRevision, new KnowledgeRecord(data));
        }

        public KnowledgeRecordOperationResult CorrectRecord(KnowledgeRecordCreateRequest request, string supersededRecordId)
        {
            long prior = RecordRevision;
            if (string.IsNullOrWhiteSpace(supersededRecordId) || !recordsById.TryGetValue(supersededRecordId, out KnowledgeRecordData superseded))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.MissingRecord, $"Superseded record '{supersededRecordId}' was not found.", request?.TransactionId, request?.Preview ?? false, RecordRevision);
            }

            if (CreatesCircularCorrection(request?.RecordId, supersededRecordId))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.CircularCorrection, "Knowledge record correction would create a circular correction chain.", request?.TransactionId, request?.Preview ?? false, RecordRevision);
            }

            KnowledgeRecordOperationResult created = CreateRecord(request);
            if (!created.Succeeded || request?.Preview == true)
            {
                return created;
            }

            KnowledgeRecordData correction = recordsById[created.Record.RecordId];
            correction.supersedesRecordId = supersededRecordId;
            correction.revision++;
            superseded.status = KnowledgeRecordStatus.Corrected;
            superseded.correctedByRecordId = correction.recordId;
            superseded.revision++;
            RecordRevision++;
            return KnowledgeRecordOperationResult.Success("Knowledge record correction created; original remains auditable.", request.TransactionId, prior, RecordRevision, new KnowledgeRecord(correction));
        }

        public KnowledgeRecordProjection ProjectRecord(string recordId, KnowledgeRecordProjectionContext projectionContext, InformationAccessRuntime accessRuntime = null)
        {
            if (!recordsById.TryGetValue(recordId ?? string.Empty, out KnowledgeRecordData data))
            {
                return new KnowledgeRecordProjection(null, null, new Dictionary<string, InformationRedactionState>(), Array.Empty<KnowledgeRecordDetailData>(), string.Empty, $"Knowledge record '{recordId}' was not found.");
            }

            return ProjectRecordData(data, projectionContext, accessRuntime);
        }

        public IReadOnlyList<KnowledgeRecordProjection> Search(KnowledgeRecordSearchQuery query, KnowledgeRecordProjectionContext projectionContext, InformationAccessRuntime accessRuntime = null)
        {
            query ??= new KnowledgeRecordSearchQuery();
            int offset = Math.Max(0, query.Offset);
            int limit = Math.Max(1, Math.Min(500, query.Limit));
            return recordsById.Values
                .Where(data => MatchesSearch(data, query))
                .OrderBy(data => data.occurredStartWorldTime)
                .ThenBy(data => data.createdWorldTime)
                .ThenBy(data => data.orderingToken ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(data => data.recordId ?? string.Empty, StringComparer.Ordinal)
                .Skip(offset)
                .Take(limit)
                .Select(data => ProjectRecordData(data, projectionContext, accessRuntime))
                .Where(projection => projection.Succeeded)
                .ToArray();
        }

        public KnowledgeRecordOperationResult ReadRecord(string recordId, KnowledgeRecordProjectionContext projectionContext, InformationAccessRuntime accessRuntime = null)
        {
            long prior = RecordRevision;
            KnowledgeRecordProjection projection = ProjectRecord(recordId, projectionContext, accessRuntime);
            if (projection.Denied)
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.AccessDenied, projection.Message, revision: RecordRevision);
            }

            return KnowledgeRecordOperationResult.Success("Knowledge record read as projected information. Reading does not force belief or mutate authoritative truth.", string.Empty, prior, prior, projection.Record, projection, preview: true);
        }

        public KnowledgeRecordReadResult ReadRecordAsPerson(
            KnowledgeRecordReadRequest request,
            InformationAccessRuntime accessRuntime,
            InformationSourceRuntime sourceRuntime,
            PersonKnowledgeRuntime knowledgeRuntime,
            PersonMemoryRuntime memoryRuntime)
        {
            if (!ValidateReadRequest(request, out string failure))
            {
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.InvalidRequest, failure, request?.TransactionId);
            }

            KnowledgeRecordProjectionContext projectionContext = request.ProjectionContext ?? new KnowledgeRecordProjectionContext
            {
                RequesterPersonId = request.ReaderPersonId,
                ContextKind = KnowledgeRecordProjectionContextKind.Public,
                AccessContext = new InformationAccessContext
                {
                    RequestingPersonId = request.ReaderPersonId,
                    ActingEntityId = request.ReaderPersonId,
                    AccessMode = InformationAccessMode.Read,
                    Purpose = InformationAccessPurpose.Journal,
                    HasDiscoveredSubject = true,
                    RedactedAccessAcceptable = true,
                    WorldTimeSeconds = request.WorldTimeSeconds
                }
            };

            KnowledgeRecordProjection projection = ProjectRecord(request.RecordId, projectionContext, accessRuntime);
            if (projection.Denied)
            {
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.AccessDenied, projection.Message, request.TransactionId, projection);
            }

            if (request.Preview || request.PrivilegedInspection)
            {
                return KnowledgeRecordReadResult.Success("Knowledge record read preview/inspection resolved without side effects.", request.TransactionId, projection, null, null, null, preview: true);
            }

            KnowledgeRecord record = projection.Record;
            bool canCreateEvidence = TryBuildReadProposition(record, request, out KnowledgePropositionData proposition);
            if (request.CreateKnowledgeEvidence && !canCreateEvidence && request.RequireEvidenceProposition)
            {
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.InvalidRequest, "Knowledge Record read requires an evidence proposition, but none could be derived.", request.TransactionId, projection);
            }

            if (request.CreateInformationSource && sourceRuntime == null)
            {
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.InvalidRequest, "Knowledge Record read requires an Information Source runtime.", request.TransactionId, projection);
            }

            if (request.CreateKnowledgeEvidence && canCreateEvidence && knowledgeRuntime == null)
            {
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.InvalidRequest, "Knowledge Record read requires a Person Knowledge runtime for evidence creation.", request.TransactionId, projection);
            }

            if (request.CreateMemory && memoryRuntime == null)
            {
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.InvalidRequest, "Knowledge Record read requires a Person Memory runtime for memory creation.", request.TransactionId, projection);
            }

            InformationSourceSaveData sourceRollback = sourceRuntime?.CreateSaveData();
            PersonKnowledgeSaveData knowledgeRollback = knowledgeRuntime?.CreateSaveData();
            PersonMemorySaveData memoryRollback = memoryRuntime?.CreateSaveData();
            InformationSourceOperationResult sourceResult = null;
            KnowledgeOperationResult knowledgeResult = null;
            HistoryOperationResult memoryResult = null;

            try
            {
                string sourceId = string.Empty;
                if (request.CreateInformationSource)
                {
                    sourceResult = RegisterReadSource(request, record, sourceRuntime);
                    if (!sourceResult.Succeeded && !sourceResult.Duplicate)
                    {
                        RestoreReadSideEffects(sourceRuntime, sourceRollback, knowledgeRuntime, knowledgeRollback, memoryRuntime, memoryRollback);
                        return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.PartialMutationRejected, sourceResult.Message, request.TransactionId, projection);
                    }

                    sourceId = sourceResult.Source?.SourceInstanceId ?? string.Empty;
                }

                if (request.CreateKnowledgeEvidence && canCreateEvidence)
                {
                    knowledgeResult = knowledgeRuntime.RecordObservation(new KnowledgeObservationRequest
                    {
                        PersonId = request.ReaderPersonId,
                        TransactionId = $"{request.TransactionId}.knowledge",
                        Proposition = proposition,
                        AcquisitionSource = KnowledgeAcquisitionSource.WrittenSource,
                        Provenance = KnowledgeProvenance.Document,
                        Direction = DetailDirection(record),
                        Strength = KnowledgeConfidence.Clamp(request.EvidenceStrength),
                        Credibility = KnowledgeConfidence.Clamp(request.EvidenceCredibility <= 0 ? record.Data.reliability : request.EvidenceCredibility),
                        EffectiveStrengthOverride = KnowledgeConfidence.Clamp(request.EvidenceStrength),
                        GameTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                        SourceId = record.RecordId,
                        InformationSourceId = sourceId,
                        EvidenceId = $"evidence.record-read.{StableHash(request.ReaderPersonId + "|" + request.RecordId + "|" + request.TransactionId)}",
                        Visibility = request.EvidenceVisibility,
                        PrivateAccessAuthorized = request.EvidenceVisibility >= KnowledgeVisibility.Private || projectionContext.Privileged,
                        RelatedEventId = record.Subject.SubjectType == InformationSubjectType.HistoricalEvent ? record.Subject.SubjectId : string.Empty,
                        Tags = new[] { "knowledge-record-read", record.Category.ToString(), record.RecordId }
                    });
                    if (!knowledgeResult.Succeeded && !knowledgeResult.Duplicate)
                    {
                        RestoreReadSideEffects(sourceRuntime, sourceRollback, knowledgeRuntime, knowledgeRollback, memoryRuntime, memoryRollback);
                        return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.PartialMutationRejected, knowledgeResult.Message, request.TransactionId, projection);
                    }
                }

                if (request.CreateMemory)
                {
                    string memoryId = string.IsNullOrWhiteSpace(request.MemoryId)
                        ? $"memory.record-read.{StableHash(request.ReaderPersonId + "|" + request.RecordId)}"
                        : request.MemoryId.Trim();
                    if (memoryRuntime.TryGetMemory(memoryId, out HistoryMemoryRecord existingMemory))
                    {
                        memoryResult = existingMemory.State == MemoryState.Forgotten
                            ? memoryRuntime.RecoverMemory(memoryId, $"{request.TransactionId}.memory-recover", Math.Max(0d, request.WorldTimeSeconds), MemoryState.Accessible, sourceId, restoring: false)
                            : memoryRuntime.ReinforceMemory(new MemoryReinforcementRequest
                            {
                                TransactionId = $"{request.TransactionId}.memory-reinforce",
                                OwnerPersonId = request.ReaderPersonId,
                                MemoryId = memoryId,
                                WorldTime = Math.Max(0d, request.WorldTimeSeconds),
                                Source = MemoryReinforcementSource.Reading,
                                ConfidenceDelta = 50,
                                ClarityDelta = 80,
                                SalienceDelta = 25,
                                ImproveAccessibility = true,
                                SourceId = sourceId
                            });
                    }
                    else
                    {
                        memoryResult = memoryRuntime.FormMemory(new FormMemoryRequest
                        {
                            TransactionId = $"{request.TransactionId}.memory",
                            MemoryId = memoryId,
                            OwnerPersonId = request.ReaderPersonId,
                            BeliefId = knowledgeResult?.ResultingBelief?.BeliefId ?? string.Empty,
                            EvidenceIds = string.IsNullOrWhiteSpace(knowledgeResult?.Evidence?.EvidenceId) ? Array.Empty<string>() : new[] { knowledgeResult.Evidence.EvidenceId },
                            Source = HistoryMemorySource.WrittenRecord,
                            FormedAtWorldTime = Math.Max(0d, request.WorldTimeSeconds),
                            RememberedOccurredAtWorldTime = Math.Max(0d, record.Data.occurredStartWorldTime),
                            Confidence = KnowledgeRecordData.ClampScore(Math.Max(500, record.Data.confidence)),
                            Clarity = projection.Redacted ? 500 : 750,
                            Salience = record.Category == KnowledgeRecordCategory.PersonalJournal ? 650 : 500,
                            FirstHand = false,
                            Visibility = request.EvidenceVisibility,
                            DebugDescription = $"Read Knowledge Record {record.RecordId}.",
                            Tags = new[] { "knowledge-record-read", record.RecordId, record.Category.ToString() }
                        });
                    }

                    if (!memoryResult.Succeeded && !memoryResult.Duplicate)
                    {
                        RestoreReadSideEffects(sourceRuntime, sourceRollback, knowledgeRuntime, knowledgeRollback, memoryRuntime, memoryRollback);
                        return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.PartialMutationRejected, memoryResult.Message, request.TransactionId, projection);
                    }
                }
            }
            catch (Exception exception)
            {
                RestoreReadSideEffects(sourceRuntime, sourceRollback, knowledgeRuntime, knowledgeRollback, memoryRuntime, memoryRollback);
                return KnowledgeRecordReadResult.Failure(KnowledgeRecordResultCode.PartialMutationRejected, exception.Message, request.TransactionId, projection);
            }

            bool duplicate = sourceResult?.Duplicate == true || knowledgeResult?.Duplicate == true || memoryResult?.Duplicate == true;
            return KnowledgeRecordReadResult.Success("Knowledge record read applied permitted source, evidence, and memory effects.", request.TransactionId, projection, sourceResult, knowledgeResult, memoryResult, duplicate: duplicate);
        }

        public KnowledgeRecordOperationResult CreateCollection(string collectionId, string displayName, string owner, IEnumerable<string> recordIds, string transactionId = "")
        {
            long prior = RecordRevision;
            if (string.IsNullOrWhiteSpace(collectionId))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.InvalidRequest, "Knowledge record collection requires a stable ID.", transactionId, revision: RecordRevision);
            }

            if (IsDuplicate(transactionId, "collection", collectionId, out KnowledgeRecordOperationResult duplicate))
            {
                return duplicate;
            }

            string[] ids = KnowledgeRecordData.CloneArray(recordIds);
            string missing = ids.FirstOrDefault(id => !recordsById.ContainsKey(id));
            if (!string.IsNullOrWhiteSpace(missing))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.MissingRecord, $"Collection references missing record '{missing}'.", transactionId, revision: RecordRevision);
            }

            collectionsById[collectionId] = new KnowledgeRecordCollectionData
            {
                collectionId = collectionId,
                ownerId = owner ?? OwnerId,
                displayName = displayName ?? string.Empty,
                recordIds = ids,
                revision = collectionsById.TryGetValue(collectionId, out KnowledgeRecordCollectionData existing) ? existing.revision + 1L : 1L
            };
            RecordRevision++;
            Remember(transactionId, "collection", collectionId);
            return KnowledgeRecordOperationResult.Success("Knowledge record collection created.", transactionId, prior, RecordRevision);
        }

        public KnowledgeRecordOperationResult AddRecordToCollection(string collectionId, string recordId, string transactionId = "")
        {
            long prior = RecordRevision;
            if (!collectionsById.TryGetValue(collectionId ?? string.Empty, out KnowledgeRecordCollectionData collection))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.MissingCollection, $"Collection '{collectionId}' was not found.", transactionId, revision: RecordRevision);
            }

            if (!recordsById.ContainsKey(recordId ?? string.Empty))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.MissingRecord, $"Record '{recordId}' was not found.", transactionId, revision: RecordRevision);
            }

            if ((collection.recordIds ?? Array.Empty<string>()).Contains(recordId, StringComparer.Ordinal))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.DuplicateMembership, $"Record '{recordId}' is already in collection '{collectionId}'.", transactionId, revision: RecordRevision);
            }

            collection.recordIds = KnowledgeRecordData.CloneArray((collection.recordIds ?? Array.Empty<string>()).Concat(new[] { recordId }));
            collection.revision++;
            RecordRevision++;
            Remember(transactionId, "collection-add", collectionId);
            return KnowledgeRecordOperationResult.Success("Record added to collection.", transactionId, prior, RecordRevision);
        }

        public KnowledgeRecordOperationResult RemoveRecordFromCollection(string collectionId, string recordId, string transactionId = "")
        {
            long prior = RecordRevision;
            if (!collectionsById.TryGetValue(collectionId ?? string.Empty, out KnowledgeRecordCollectionData collection))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.MissingCollection, $"Collection '{collectionId}' was not found.", transactionId, revision: RecordRevision);
            }

            collection.recordIds = KnowledgeRecordData.CloneArray((collection.recordIds ?? Array.Empty<string>()).Where(id => !string.Equals(id, recordId, StringComparison.Ordinal)));
            collection.revision++;
            RecordRevision++;
            Remember(transactionId, "collection-remove", collectionId);
            return KnowledgeRecordOperationResult.Success("Record removed from collection. The record itself was not deleted.", transactionId, prior, RecordRevision);
        }

        public KnowledgeRecordSnapshot CreateSnapshot()
        {
            return new KnowledgeRecordSnapshot(
                OwnerId,
                RecordRevision,
                recordsById.Values.OrderBy(data => data.recordId, StringComparer.Ordinal).Select(data => new KnowledgeRecord(data)).ToArray(),
                collectionsById.Values.OrderBy(data => data.collectionId, StringComparer.Ordinal).Select(data => new KnowledgeRecordCollection(data)).ToArray());
        }

        public KnowledgeRecordSaveData CreateSaveData()
        {
            return new KnowledgeRecordSaveData
            {
                schemaVersion = KnowledgeRecordSaveData.CurrentSchemaVersion,
                ownerId = OwnerId,
                recordRevision = RecordRevision,
                records = recordsById.Values.OrderBy(data => data.recordId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                collections = collectionsById.Values.OrderBy(data => data.collectionId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                processedTransactions = processedTransactions.Values.OrderBy(data => data.transactionId, StringComparer.Ordinal).ToArray()
            };
        }

        public KnowledgeRecordOperationResult RestoreFromSaveData(KnowledgeRecordSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, bool restoring = false)
        {
            long prior = RecordRevision;
            if (!ValidateSaveData(saveData, definitionRegistry, expectedOwnerId, out string failure))
            {
                return KnowledgeRecordOperationResult.Failure(KnowledgeRecordResultCode.CorruptPayload, failure, revision: RecordRevision);
            }

            Dictionary<string, KnowledgeRecordData> preparedRecords = saveData.records.ToDictionary(data => data.recordId, data => data.Clone(), StringComparer.Ordinal);
            Dictionary<string, KnowledgeRecordCollectionData> preparedCollections = saveData.collections.ToDictionary(data => data.collectionId, data => data.Clone(), StringComparer.Ordinal);
            Dictionary<string, KnowledgeRecordProcessedTransactionData> preparedTransactions = (saveData.processedTransactions ?? Array.Empty<KnowledgeRecordProcessedTransactionData>())
                .Where(data => !string.IsNullOrWhiteSpace(data.transactionId))
                .ToDictionary(data => data.transactionId, data => new KnowledgeRecordProcessedTransactionData { transactionId = data.transactionId, operation = data.operation ?? string.Empty, recordId = data.recordId ?? string.Empty, revision = data.revision }, StringComparer.Ordinal);

            recordsById.Clear();
            foreach (KeyValuePair<string, KnowledgeRecordData> pair in preparedRecords)
            {
                recordsById[pair.Key] = pair.Value;
            }

            collectionsById.Clear();
            foreach (KeyValuePair<string, KnowledgeRecordCollectionData> pair in preparedCollections)
            {
                collectionsById[pair.Key] = pair.Value;
            }

            processedTransactions.Clear();
            foreach (KeyValuePair<string, KnowledgeRecordProcessedTransactionData> pair in preparedTransactions)
            {
                processedTransactions[pair.Key] = pair.Value;
            }

            ownerId = saveData.ownerId ?? expectedOwnerId ?? string.Empty;
            registry = definitionRegistry ?? registry;
            RecordRevision = Math.Max(0L, saveData.recordRevision);
            return KnowledgeRecordOperationResult.Success(restoring ? "Knowledge records restored." : "Knowledge records loaded.", string.Empty, prior, RecordRevision);
        }

        public static bool ValidateSaveData(KnowledgeRecordSaveData saveData, DefinitionRegistry registry, string expectedOwnerId, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Knowledge Record save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != KnowledgeRecordSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Knowledge Record schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedOwnerId) && !string.IsNullOrWhiteSpace(saveData.ownerId) && !string.Equals(saveData.ownerId, expectedOwnerId, StringComparison.Ordinal))
            {
                failure = $"Knowledge Record owner mismatch. Expected '{expectedOwnerId}', got '{saveData.ownerId}'.";
                return false;
            }

            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeRecordData record in saveData.records ?? Array.Empty<KnowledgeRecordData>())
            {
                if (!ValidateRecordData(record, registry, out failure))
                {
                    return false;
                }

                if (!recordIds.Add(record.recordId))
                {
                    failure = $"Duplicate Knowledge Record ID '{record.recordId}'.";
                    return false;
                }
            }

            foreach (KnowledgeRecordData record in saveData.records ?? Array.Empty<KnowledgeRecordData>())
            {
                if (!string.IsNullOrWhiteSpace(record.correctedByRecordId) && !recordIds.Contains(record.correctedByRecordId))
                {
                    failure = $"Knowledge Record '{record.recordId}' references missing correction '{record.correctedByRecordId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(record.supersedesRecordId) && !recordIds.Contains(record.supersedesRecordId))
                {
                    failure = $"Knowledge Record '{record.recordId}' references missing superseded record '{record.supersedesRecordId}'.";
                    return false;
                }
            }

            HashSet<string> collectionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeRecordCollectionData collection in saveData.collections ?? Array.Empty<KnowledgeRecordCollectionData>())
            {
                if (collection == null || string.IsNullOrWhiteSpace(collection.collectionId))
                {
                    failure = "Knowledge Record collection is missing a stable ID.";
                    return false;
                }

                if (!collectionIds.Add(collection.collectionId))
                {
                    failure = $"Duplicate Knowledge Record collection ID '{collection.collectionId}'.";
                    return false;
                }

                foreach (string recordId in collection.recordIds ?? Array.Empty<string>())
                {
                    if (!recordIds.Contains(recordId))
                    {
                        failure = $"Knowledge Record collection '{collection.collectionId}' references missing record '{recordId}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        private KnowledgeRecordProjection ProjectRecordData(KnowledgeRecordData data, KnowledgeRecordProjectionContext projectionContext, InformationAccessRuntime accessRuntime)
        {
            projectionContext ??= new KnowledgeRecordProjectionContext();
            string[] detailIds = (data.details ?? Array.Empty<KnowledgeRecordDetailData>())
                .Select(detail => detail?.detailId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            RedactedInformationProjection redaction = null;
            if (!projectionContext.Privileged && accessRuntime != null)
            {
                InformationAccessContext context = BuildAccessContext(data, projectionContext, detailIds);
                redaction = accessRuntime.Project(context, detailIds);
                if (redaction?.Decision?.Denied == true)
                {
                    return new KnowledgeRecordProjection(null, redaction.Decision, redaction.Details, Array.Empty<KnowledgeRecordDetailData>(), string.Empty, redaction.Decision.VisibleReason);
                }
            }

            IReadOnlyDictionary<string, InformationRedactionState> states = redaction?.Details ?? detailIds.ToDictionary(id => id, _ => InformationRedactionState.Visible, StringComparer.Ordinal);
            KnowledgeRecordData projected = data.Clone();
            projected.details = (projected.details ?? Array.Empty<KnowledgeRecordDetailData>())
                .Where(detail => detail != null && IsDetailVisible(states, detail.detailId, projectionContext.IncludeRedactedDetails))
                .Select(detail =>
                {
                    KnowledgeRecordDetailData clone = detail.Clone();
                    if (states.TryGetValue(clone.detailId ?? string.Empty, out InformationRedactionState state) && state != InformationRedactionState.Visible)
                    {
                        clone.value = string.Empty;
                    }

                    return clone;
                })
                .ToArray();

            string visibleRecordId = redaction?.Decision?.Denied == true ? string.Empty : projected.recordId;
            return new KnowledgeRecordProjection(new KnowledgeRecord(projected), redaction?.Decision, states, projected.details, visibleRecordId, redaction?.Decision?.VisibleReason ?? "Knowledge record projected.");
        }

        private InformationAccessContext BuildAccessContext(KnowledgeRecordData data, KnowledgeRecordProjectionContext projectionContext, string[] detailIds)
        {
            InformationAccessContext source = projectionContext.AccessContext;
            return new InformationAccessContext
            {
                RequestingPersonId = source?.RequestingPersonId ?? projectionContext.RequesterPersonId ?? string.Empty,
                ActingEntityId = source?.ActingEntityId ?? projectionContext.RequesterPersonId ?? string.Empty,
                Subject = data.subject?.Clone() ?? new InformationSubjectReferenceData { subjectType = InformationSubjectType.KnowledgeRecord, subjectId = data.recordId },
                Purpose = source?.Purpose ?? InformationAccessPurpose.Journal,
                WorldTimeSeconds = source?.WorldTimeSeconds ?? Math.Max(0d, data.updatedWorldTime),
                AccessMode = source?.AccessMode ?? InformationAccessMode.Inspect,
                RequestedDetailIds = detailIds,
                AuthorizationIds = source?.AuthorizationIds ?? Array.Empty<string>(),
                OrganizationIds = source?.OrganizationIds ?? Array.Empty<string>(),
                RoleIds = source?.RoleIds ?? Array.Empty<string>(),
                TitleOrStatusIds = source?.TitleOrStatusIds ?? Array.Empty<string>(),
                NeedToKnowTags = source?.NeedToKnowTags ?? Array.Empty<string>(),
                HasDiscoveredSubject = source?.HasDiscoveredSubject ?? true,
                KnowsSource = source?.KnowsSource ?? false,
                ContextKind = source?.ContextKind ?? InformationContextKind.Gameplay,
                RedactedAccessAcceptable = source?.RedactedAccessAcceptable ?? true,
                RevealDenialReasons = source?.RevealDenialReasons ?? false,
                DeterministicPolicyId = string.IsNullOrWhiteSpace(data.accessPolicyId) ? source?.DeterministicPolicyId ?? string.Empty : data.accessPolicyId
            };
        }

        private bool ValidateReadRequest(KnowledgeRecordReadRequest request, out string failure)
        {
            failure = string.Empty;
            if (request == null)
            {
                failure = "Knowledge Record read request is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                failure = "Knowledge Record read requires a transaction ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.RecordId))
            {
                failure = "Knowledge Record read requires a record ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ReaderPersonId))
            {
                failure = "Knowledge Record read requires a reader Person ID.";
                return false;
            }

            if (!recordsById.ContainsKey(request.RecordId))
            {
                failure = $"Knowledge Record '{request.RecordId}' was not found.";
                return false;
            }

            return true;
        }

        private InformationSourceOperationResult RegisterReadSource(KnowledgeRecordReadRequest request, KnowledgeRecord record, InformationSourceRuntime sourceRuntime)
        {
            string sourceId = string.IsNullOrWhiteSpace(request.SourceInstanceId)
                ? $"information-source.record-read.{StableHash(request.ReaderPersonId + "|" + request.RecordId)}"
                : request.SourceInstanceId.Trim();
            if (sourceRuntime.TryGetSource(sourceId, out InformationSourceRecord existingSource))
            {
                return InformationSourceOperationResult.Success("Knowledge Record read source already exists.", $"{request.TransactionId}.source", existingSource, null, sourceRuntime.SourceRevision, sourceRuntime.SourceRevision, duplicate: true);
            }

            string parentSourceId = (record.Data.sourceIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault(id => sourceRuntime.TryGetSource(id, out _));

            if (!string.IsNullOrWhiteSpace(parentSourceId))
            {
                return sourceRuntime.TransformSource(new SourceTransformationRequest
                {
                    TransactionId = $"{request.TransactionId}.source",
                    SourceInstanceId = sourceId,
                    ParentSourceId = parentSourceId,
                    TransformationType = InformationSourceTransformationType.Copy,
                    ActorPersonId = request.ReaderPersonId,
                    WorldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                    Quality = KnowledgeRecordData.ClampScore(record.Data.reliability),
                    HidesOriginal = false,
                    Note = $"Read Knowledge Record {record.RecordId}."
                });
            }

            return sourceRuntime.RegisterSource(new InformationSourceRegistrationRequest
            {
                TransactionId = $"{request.TransactionId}.source",
                SourceInstanceId = sourceId,
                SourceDefinitionId = request.InformationSourceDefinitionId ?? string.Empty,
                Category = SourceCategoryFor(record.Category),
                ReferenceType = SourceReferenceTypeFor(record.Subject.SubjectType),
                ReferencedId = record.Subject.SubjectId,
                OriginalCreatorPersonId = record.AuthorPersonId,
                ObserverPersonId = request.ReaderPersonId,
                HolderPersonId = request.ReaderPersonId,
                TransmitterPersonId = record.AuthorPersonId,
                CreationWorldTimeSeconds = Math.Max(0d, record.Data.createdWorldTime),
                ObservationWorldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                TransmissionWorldTimeSeconds = Math.Max(0d, request.WorldTimeSeconds),
                Domain = KnowledgeDomain.Historical,
                SubjectId = record.Subject.SubjectId,
                MethodId = "method.knowledge-record.read",
                Privacy = SourcePrivacyFor(record.Classification),
                Tags = (record.Data.tags ?? Array.Empty<string>()).Concat(new[] { "knowledge-record-read", record.RecordId }).ToArray()
            });
        }

        private bool TryBuildReadProposition(KnowledgeRecord record, KnowledgeRecordReadRequest request, out KnowledgePropositionData proposition)
        {
            proposition = request.Proposition?.Clone();
            if (proposition != null)
            {
                return true;
            }

            InformationSubjectReference subject = record.Subject;
            if (subject.SubjectType == InformationSubjectType.HistoricalEvent || subject.SubjectType == InformationSubjectType.LifeEvent)
            {
                proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                    subjectType = KnowledgeSubjectType.Event,
                    subjectId = subject.SubjectId,
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true,
                    sourceContextId = record.RecordId,
                    sourceRevision = record.Revision
                };
                return true;
            }

            if (subject.SubjectType == InformationSubjectType.PersonIdentity)
            {
                proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.PersonIdentity,
                    subjectType = KnowledgeSubjectType.Person,
                    subjectId = subject.SubjectId,
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = subject.SubjectId,
                    sourceContextId = record.RecordId,
                    sourceRevision = record.Revision
                };
                return true;
            }

            return false;
        }

        private static KnowledgeEvidenceDirection DetailDirection(KnowledgeRecord record)
        {
            return record?.Status == KnowledgeRecordStatus.Corrected || record?.Status == KnowledgeRecordStatus.Verified
                ? KnowledgeEvidenceDirection.Corrects
                : KnowledgeEvidenceDirection.Supports;
        }

        private static InformationSourceCategory SourceCategoryFor(KnowledgeRecordCategory category)
        {
            return category switch
            {
                KnowledgeRecordCategory.PersonalJournal => InformationSourceCategory.Journal,
                KnowledgeRecordCategory.KnowledgeJournal => InformationSourceCategory.Journal,
                KnowledgeRecordCategory.HistoricalRecord => InformationSourceCategory.HistoricalRecord,
                KnowledgeRecordCategory.Biography => InformationSourceCategory.WrittenRecord,
                KnowledgeRecordCategory.PublicBiography => InformationSourceCategory.PublicAnnouncement,
                KnowledgeRecordCategory.Bestiary => InformationSourceCategory.Book,
                KnowledgeRecordCategory.LocationRecord => InformationSourceCategory.Map,
                KnowledgeRecordCategory.MapRecord => InformationSourceCategory.Map,
                KnowledgeRecordCategory.MedicalRecord => InformationSourceCategory.InstitutionalReport,
                KnowledgeRecordCategory.DiagnosisRecord => InformationSourceCategory.InstitutionalReport,
                KnowledgeRecordCategory.InvestigationRecord => InformationSourceCategory.InstitutionalReport,
                KnowledgeRecordCategory.EvidenceRecord => InformationSourceCategory.PhysicalEvidence,
                KnowledgeRecordCategory.SourceRecord => InformationSourceCategory.WrittenRecord,
                _ => InformationSourceCategory.WrittenRecord
            };
        }

        private static InformationSourceReferenceType SourceReferenceTypeFor(InformationSubjectType subjectType)
        {
            return subjectType switch
            {
                InformationSubjectType.PersonIdentity => InformationSourceReferenceType.Person,
                InformationSubjectType.BodyIdentity => InformationSourceReferenceType.Body,
                InformationSubjectType.HistoricalEvent => InformationSourceReferenceType.HistoricalEvent,
                InformationSubjectType.LifeEvent => InformationSourceReferenceType.HistoricalEvent,
                InformationSubjectType.Memory => InformationSourceReferenceType.Memory,
                InformationSubjectType.Evidence => InformationSourceReferenceType.Evidence,
                InformationSubjectType.Source => InformationSourceReferenceType.Custom,
                InformationSubjectType.SourceChain => InformationSourceReferenceType.Custom,
                InformationSubjectType.Location => InformationSourceReferenceType.Location,
                InformationSubjectType.Organization => InformationSourceReferenceType.Organization,
                _ => InformationSourceReferenceType.Document
            };
        }

        private static SourcePrivacyLevel SourcePrivacyFor(InformationVisibilityClassification classification)
        {
            return classification switch
            {
                InformationVisibilityClassification.Public => SourcePrivacyLevel.Public,
                InformationVisibilityClassification.Open => SourcePrivacyLevel.Public,
                InformationVisibilityClassification.Personal => SourcePrivacyLevel.Personal,
                InformationVisibilityClassification.Private => SourcePrivacyLevel.Private,
                InformationVisibilityClassification.Confidential => SourcePrivacyLevel.Private,
                InformationVisibilityClassification.Restricted => SourcePrivacyLevel.Private,
                InformationVisibilityClassification.Secret => SourcePrivacyLevel.Secret,
                InformationVisibilityClassification.HighlySecret => SourcePrivacyLevel.Secret,
                InformationVisibilityClassification.Hidden => SourcePrivacyLevel.Hidden,
                InformationVisibilityClassification.Sealed => SourcePrivacyLevel.Hidden,
                _ => SourcePrivacyLevel.Shared
            };
        }

        private void RestoreReadSideEffects(
            InformationSourceRuntime sourceRuntime,
            InformationSourceSaveData sourceRollback,
            PersonKnowledgeRuntime knowledgeRuntime,
            PersonKnowledgeSaveData knowledgeRollback,
            PersonMemoryRuntime memoryRuntime,
            PersonMemorySaveData memoryRollback)
        {
            if (sourceRuntime != null && sourceRollback != null)
            {
                sourceRuntime.RestoreFromSaveData(sourceRollback, registry, sourceRollback.ownerId, restoring: true);
            }

            if (knowledgeRuntime != null && knowledgeRollback != null)
            {
                knowledgeRuntime.RestoreFromSaveData(knowledgeRollback, registry, knowledgeRollback.personId, restoring: true);
            }

            if (memoryRuntime != null && memoryRollback != null)
            {
                memoryRuntime.RestoreFromSaveData(memoryRollback, registry, null, new[] { memoryRollback.personId }, restoring: true);
            }
        }

        private bool ValidateCreateRequest(KnowledgeRecordCreateRequest request, out KnowledgeRecordDefinition definition, out string failure, out KnowledgeRecordResultCode code)
        {
            definition = null;
            failure = string.Empty;
            code = KnowledgeRecordResultCode.InvalidRequest;
            if (request == null)
            {
                failure = "Knowledge Record create request is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.RecordId))
            {
                failure = "Knowledge Record create request requires a stable record ID.";
                return false;
            }

            if (!request.RecordId.StartsWith("record.", StringComparison.Ordinal))
            {
                failure = $"Knowledge Record ID '{request.RecordId}' must use the 'record.' namespace prefix.";
                return false;
            }

            if (!request.Preview && recordsById.ContainsKey(request.RecordId))
            {
                failure = $"Knowledge Record '{request.RecordId}' already exists.";
                return false;
            }

            if (registry == null || string.IsNullOrWhiteSpace(request.DefinitionId) || !registry.TryGet(request.DefinitionId, out definition))
            {
                failure = $"Knowledge Record definition '{request.DefinitionId}' was not found.";
                code = KnowledgeRecordResultCode.MissingDefinition;
                return false;
            }

            InformationSubjectType subjectType = request.Subject?.subjectType ?? InformationSubjectType.Unknown;
            if (!definition.AllowedSubjectTypes.Contains(subjectType))
            {
                failure = $"Knowledge Record definition '{definition.Id}' does not allow subject type '{subjectType}'.";
                return false;
            }

            if (!definition.AllowedOwnerKinds.Contains(request.OwnerKind))
            {
                failure = $"Knowledge Record definition '{definition.Id}' does not allow owner kind '{request.OwnerKind}'.";
                return false;
            }

            if (request.Subject == null || request.Subject.subjectType == InformationSubjectType.Unknown || string.IsNullOrWhiteSpace(request.Subject.subjectId))
            {
                failure = "Knowledge Record create request requires a typed subject reference.";
                return false;
            }

            return true;
        }

        private static bool ValidateRecordData(KnowledgeRecordData record, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (record == null || string.IsNullOrWhiteSpace(record.recordId))
            {
                failure = "Knowledge Record is missing a stable record ID.";
                return false;
            }

            if (!record.recordId.StartsWith("record.", StringComparison.Ordinal))
            {
                failure = $"Knowledge Record ID '{record.recordId}' must use the 'record.' namespace prefix.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.definitionId) || registry == null || !registry.TryGet<KnowledgeRecordDefinition>(record.definitionId, out _))
            {
                failure = $"Knowledge Record '{record.recordId}' references missing definition '{record.definitionId}'.";
                return false;
            }

            if (record.subject == null || record.subject.subjectType == InformationSubjectType.Unknown || string.IsNullOrWhiteSpace(record.subject.subjectId))
            {
                failure = $"Knowledge Record '{record.recordId}' requires a typed subject reference.";
                return false;
            }

            if (!Enum.IsDefined(typeof(KnowledgeRecordCategory), record.category) || record.category == KnowledgeRecordCategory.Unknown)
            {
                failure = $"Knowledge Record '{record.recordId}' has an invalid category.";
                return false;
            }

            return true;
        }

        private KnowledgeRecordData BuildRecordData(KnowledgeRecordCreateRequest request, KnowledgeRecordDefinition definition)
        {
            double now = Math.Max(0d, request.WorldTimeSeconds);
            return new KnowledgeRecordData
            {
                recordId = request.RecordId.Trim(),
                definitionId = definition.Id,
                category = request.Category == KnowledgeRecordCategory.Unknown ? definition.Category : request.Category,
                ownerKind = request.OwnerKind,
                ownerId = request.OwnerId ?? OwnerId,
                subject = request.Subject?.Clone() ?? new InformationSubjectReferenceData(),
                authorPersonId = request.AuthorPersonId ?? string.Empty,
                creatorSystemId = "knowledge-record-runtime",
                createdWorldTime = now,
                updatedWorldTime = now,
                occurredStartWorldTime = request.OccurredWorldTimeSeconds <= 0d ? now : request.OccurredWorldTimeSeconds,
                preservedProjectionContext = request.ProjectionContext,
                knowledgeOwnerPersonId = request.KnowledgeOwnerPersonId ?? string.Empty,
                sourceIds = KnowledgeRecordData.CloneArray(request.SourceIds),
                evidenceIds = KnowledgeRecordData.CloneArray(request.EvidenceIds),
                memoryIds = KnowledgeRecordData.CloneArray(request.MemoryIds),
                historicalEventIds = KnowledgeRecordData.CloneArray(request.HistoricalEventIds),
                lifeEventIds = KnowledgeRecordData.CloneArray(request.LifeEventIds),
                transferIds = KnowledgeRecordData.CloneArray(request.TransferIds),
                relatedRecordIds = KnowledgeRecordData.CloneArray(request.RelatedRecordIds),
                status = request.Status,
                completeness = request.Completeness,
                confidence = KnowledgeRecordData.ClampScore(request.Confidence),
                reliability = KnowledgeRecordData.ClampScore(request.Reliability),
                accessPolicyId = string.IsNullOrWhiteSpace(request.AccessPolicyId) ? definition.DefaultAccessPolicyId : request.AccessPolicyId,
                classification = request.Classification,
                tags = KnowledgeRecordData.CloneArray((request.Tags ?? Array.Empty<string>()).Concat(definition.Tags)),
                details = request.Details == null ? Array.Empty<KnowledgeRecordDetailData>() : request.Details.Select(detail => detail?.Clone()).Where(detail => detail != null).ToArray(),
                orderingToken = $"{now:0000000000.000}:{request.RecordId}",
                revision = 1L
            };
        }

        private bool CreatesCircularCorrection(string correctionRecordId, string supersededRecordId)
        {
            if (string.IsNullOrWhiteSpace(correctionRecordId) || string.IsNullOrWhiteSpace(supersededRecordId))
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string cursor = supersededRecordId;
            while (!string.IsNullOrWhiteSpace(cursor) && recordsById.TryGetValue(cursor, out KnowledgeRecordData current))
            {
                if (!seen.Add(cursor) || string.Equals(cursor, correctionRecordId, StringComparison.Ordinal))
                {
                    return true;
                }

                cursor = current.supersedesRecordId;
            }

            return false;
        }

        private static bool MatchesSearch(KnowledgeRecordData data, KnowledgeRecordSearchQuery query)
        {
            return data != null
                && (!query.Category.HasValue || data.category == query.Category.Value)
                && (!query.Status.HasValue || data.status == query.Status.Value)
                && (!query.OwnerKind.HasValue || data.ownerKind == query.OwnerKind.Value)
                && (string.IsNullOrWhiteSpace(query.OwnerId) || string.Equals(data.ownerId, query.OwnerId, StringComparison.Ordinal))
                && (!query.SubjectType.HasValue || data.subject?.subjectType == query.SubjectType.Value)
                && (string.IsNullOrWhiteSpace(query.SubjectId) || string.Equals(data.subject?.subjectId, query.SubjectId, StringComparison.Ordinal))
                && (!query.Classification.HasValue || data.classification == query.Classification.Value)
                && (string.IsNullOrWhiteSpace(query.Tag) || (data.tags ?? Array.Empty<string>()).Contains(query.Tag, StringComparer.Ordinal))
                && (query.IncludeCorrected || data.status != KnowledgeRecordStatus.Corrected && data.status != KnowledgeRecordStatus.Superseded)
                && (query.IncludeArchived || data.status != KnowledgeRecordStatus.Archived);
        }

        private static bool IsDetailVisible(IReadOnlyDictionary<string, InformationRedactionState> states, string detailId, bool includeRedacted)
        {
            if (states == null || string.IsNullOrWhiteSpace(detailId) || !states.TryGetValue(detailId, out InformationRedactionState state))
            {
                return true;
            }

            return state == InformationRedactionState.Visible || includeRedacted && state == InformationRedactionState.Redacted;
        }

        private bool IsDuplicate(string transactionId, string operation, string recordId, out KnowledgeRecordOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId) || !processedTransactions.TryGetValue(transactionId.Trim(), out KnowledgeRecordProcessedTransactionData processed))
            {
                return false;
            }

            recordsById.TryGetValue(processed.recordId ?? recordId ?? string.Empty, out KnowledgeRecordData data);
            result = KnowledgeRecordOperationResult.Success($"Duplicate Knowledge Record {processed.operation} transaction ignored.", transactionId, RecordRevision, RecordRevision, data == null ? null : new KnowledgeRecord(data), duplicate: true);
            return true;
        }

        private void Remember(string transactionId, string operation, string recordId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedTransactions[transactionId.Trim()] = new KnowledgeRecordProcessedTransactionData
            {
                transactionId = transactionId.Trim(),
                operation = operation ?? string.Empty,
                recordId = recordId ?? string.Empty,
                revision = RecordRevision
            };
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("x16");
            }
        }
    }
}
