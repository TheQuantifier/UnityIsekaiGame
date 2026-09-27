using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Quests
{
    public sealed class QuestRuntime : IDisposable
    {
        private readonly Dictionary<string, QuestRecordData> questsById = new Dictionary<string, QuestRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestRuntimeTransactionData> transactionsById = new Dictionary<string, QuestRuntimeTransactionData>(StringComparer.Ordinal);
        private readonly List<QuestRuntimeEventData> events = new List<QuestRuntimeEventData>();
        private readonly Dictionary<string, List<string>> byDefinition = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> byTag = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> byIssuer = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> byRecipient = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> byOriginLocation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> byOriginInteraction = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> bySubject = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private string worldId;
        private bool disposed;
        private long revision;

        public QuestRuntime(DefinitionRegistry definitionRegistry = null, string worldId = PersistenceService.LocalWorldId)
        {
            Configure(definitionRegistry, worldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int Count => questsById.Count;

        public void Configure(DefinitionRegistry definitionRegistry, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId;
        }

        public QuestRuntimeOperationResult CreateQuest(QuestCreateRequest request)
        {
            if (disposed)
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.Disposed, "Quest runtime is disposed.", revision);
            }

            if (request == null || string.IsNullOrWhiteSpace(request.questDefinitionId))
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.InvalidRequest, "Quest creation requires a definition ID.", revision);
            }

            if (registry == null)
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.MissingDefinitionRegistry, "Quest runtime has no definition registry.", revision);
            }

            if (!registry.TryGet(request.questDefinitionId, out QuestDefinition definition))
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.MissingDefinition, $"Quest definition '{request.questDefinitionId}' is missing.", revision);
            }

            string transactionId = Clean(request.transactionId);
            if (!string.IsNullOrWhiteSpace(transactionId) && transactionsById.TryGetValue(transactionId, out QuestRuntimeTransactionData duplicateTransaction))
            {
                QuestSnapshot duplicateSnapshot = TryGetSnapshot(duplicateTransaction.questId, out QuestSnapshot found) ? found : null;
                return QuestRuntimeOperationResult.Success(duplicateSnapshot, "Duplicate quest transaction ignored.", revision, revision, duplicate: true);
            }

            if (request.expectedRevision >= 0L && request.expectedRevision != revision)
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.RevisionConflict, $"Expected revision {request.expectedRevision}, actual {revision}.", revision);
            }

            string questId = Clean(request.questId);
            if (string.IsNullOrWhiteSpace(questId))
            {
                questId = BuildQuestId(definition, request);
            }

            if (questsById.ContainsKey(questId))
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.DuplicateQuestId, $"Quest '{questId}' already exists.", revision);
            }

            QuestIssuerReferenceData issuer = request.issuer?.Clone() ?? new QuestIssuerReferenceData { issuerType = QuestIssuerType.System, issuerId = "system.quest" };
            QuestRecipientReferenceData recipient = request.intendedRecipient?.Clone() ?? new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open };
            QuestOriginReferenceData origin = request.origin?.Clone() ?? new QuestOriginReferenceData { sourceChannel = definition.DefaultSourceChannel };
            if (origin.sourceChannel == QuestSourceChannel.Unknown)
            {
                origin.sourceChannel = definition.DefaultSourceChannel;
            }

            QuestRuntimeOperationStatus referenceStatus = ValidateReferences(definition, issuer, recipient, origin, request.subjectLinks, out string referenceFailure);
            if (referenceStatus != QuestRuntimeOperationStatus.Succeeded)
            {
                return QuestRuntimeOperationResult.Failure(referenceStatus, referenceFailure, revision);
            }

            QuestRuntimeOperationStatus uniquenessStatus = ValidateUniqueness(definition, request, recipient, out string uniquenessFailure);
            if (uniquenessStatus != QuestRuntimeOperationStatus.Succeeded)
            {
                return QuestRuntimeOperationResult.Failure(uniquenessStatus, uniquenessFailure, revision);
            }

            QuestRecordData data = new QuestRecordData
            {
                questId = questId,
                questDefinitionId = definition.Id,
                worldId = worldId,
                saveSlotId = Clean(request.saveSlotId),
                lifecycleState = request.initialLifecycleState == QuestRuntimeLifecycleState.Unknown ? QuestRuntimeLifecycleState.Available : request.initialLifecycleState,
                issuer = issuer,
                intendedRecipient = recipient,
                origin = origin,
                subjectLinks = NormalizeSubjectLinks(request.subjectLinks),
                tagIds = QuestRuntimeModelUtility.Clean((request.tagIds ?? Array.Empty<string>()).Concat(definition.DefaultTagIds ?? Array.Empty<string>())),
                visibility = request.visibility ?? definition.DefaultVisibility,
                createdWorldTime = request.createdWorldTime,
                repeatInstanceKey = Clean(request.repeatInstanceKey),
                sourceEventId = Clean(request.sourceEventId),
                sourceRecordId = Clean(request.sourceRecordId),
                provenanceId = Clean(request.provenanceId),
                revision = 1L
            };

            if (request.preview)
            {
                return QuestRuntimeOperationResult.Success(new QuestSnapshot(data), "Quest creation previewed.", revision, revision, preview: true);
            }

            long before = revision;
            questsById.Add(data.questId, data.Clone());
            revision++;
            RecordTransaction(transactionId, "CreateQuest", data.questId);
            RecordEvent(transactionId, data.questId, QuestRuntimeEventKind.Instantiated, QuestRuntimeLifecycleState.Unknown, data.lifecycleState, data.createdWorldTime, data.sourceEventId, data.provenanceId);
            RebuildIndexes();
            return QuestRuntimeOperationResult.Success(new QuestSnapshot(data), "Quest created.", before, revision);
        }

        public QuestRuntimeOperationResult TransitionLifecycle(QuestLifecycleTransitionRequest request)
        {
            if (disposed)
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.Disposed, "Quest runtime is disposed.", revision);
            }

            if (request == null || string.IsNullOrWhiteSpace(request.questId))
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.InvalidRequest, "Quest lifecycle transition requires a quest ID.", revision);
            }

            string transactionId = Clean(request.transactionId);
            if (!string.IsNullOrWhiteSpace(transactionId) && transactionsById.TryGetValue(transactionId, out QuestRuntimeTransactionData duplicateTransaction))
            {
                QuestSnapshot duplicateSnapshot = TryGetSnapshot(duplicateTransaction.questId, out QuestSnapshot found) ? found : null;
                return QuestRuntimeOperationResult.Success(duplicateSnapshot, "Duplicate quest lifecycle transaction ignored.", revision, revision, duplicate: true);
            }

            if (request.expectedRevision >= 0L && request.expectedRevision != revision)
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.RevisionConflict, $"Expected revision {request.expectedRevision}, actual {revision}.", revision);
            }

            string questId = Clean(request.questId);
            if (!questsById.TryGetValue(questId, out QuestRecordData existing))
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.MissingQuest, $"Quest '{questId}' is missing.", revision);
            }

            if (request.targetState == QuestRuntimeLifecycleState.Unknown || request.targetState == QuestRuntimeLifecycleState.Draft)
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.InvalidLifecycleTransition, $"Quest cannot transition to '{request.targetState}'.", revision);
            }

            QuestRecordData updated = existing.Clone();
            QuestRuntimeLifecycleState beforeState = updated.lifecycleState;
            updated.lifecycleState = request.targetState;
            if (request.targetState == QuestRuntimeLifecycleState.Retired || request.targetState == QuestRuntimeLifecycleState.Historical || request.targetState == QuestRuntimeLifecycleState.Invalid)
            {
                updated.retiredWorldTime = request.worldTime;
            }

            updated.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? updated.sourceEventId : request.sourceEventId;
            updated.provenanceId = string.IsNullOrWhiteSpace(request.provenanceId) ? updated.provenanceId : request.provenanceId;
            updated.revision++;

            if (request.preview)
            {
                return QuestRuntimeOperationResult.Success(new QuestSnapshot(updated), "Quest lifecycle transition previewed.", revision, revision, preview: true);
            }

            long beforeRevision = revision;
            questsById[questId] = updated;
            revision++;
            RecordTransaction(transactionId, "TransitionLifecycle", questId);
            RecordEvent(transactionId, questId, QuestRuntimeEventKind.LifecycleChanged, beforeState, updated.lifecycleState, request.worldTime, request.sourceEventId, request.provenanceId);
            RebuildIndexes();
            return QuestRuntimeOperationResult.Success(new QuestSnapshot(updated), "Quest lifecycle changed.", beforeRevision, revision);
        }

        public bool TryGetSnapshot(string questId, out QuestSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(questId) || !questsById.TryGetValue(questId, out QuestRecordData record))
            {
                return false;
            }

            snapshot = new QuestSnapshot(record);
            return true;
        }

        public IReadOnlyList<QuestSnapshot> Query(QuestQuery query = null)
        {
            QuestQuery actual = query ?? new QuestQuery();
            IEnumerable<QuestRecordData> records = questsById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId))
            {
                records = records.Where(record => string.Equals(record.worldId, actual.worldId, StringComparison.Ordinal));
            }

            if (!actual.includeRetired)
            {
                records = records.Where(record => record.lifecycleState != QuestRuntimeLifecycleState.Retired && record.lifecycleState != QuestRuntimeLifecycleState.Historical && record.lifecycleState != QuestRuntimeLifecycleState.Invalid);
            }

            if (!string.IsNullOrWhiteSpace(actual.definitionId))
            {
                records = records.Where(record => string.Equals(record.questDefinitionId, actual.definitionId, StringComparison.Ordinal));
            }

            if (actual.category.HasValue)
            {
                records = records.Where(record => registry != null && registry.TryGet(record.questDefinitionId, out QuestDefinition definition) && definition.Category == actual.category.Value);
            }

            if (!string.IsNullOrWhiteSpace(actual.tagId))
            {
                records = records.Where(record => (record.tagIds ?? Array.Empty<string>()).Contains(actual.tagId, StringComparer.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(actual.issuerId))
            {
                records = records.Where(record => string.Equals(record.issuer?.issuerId, actual.issuerId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(actual.recipientId))
            {
                records = records.Where(record => string.Equals(record.intendedRecipient?.recipientId, actual.recipientId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(actual.originLocationId))
            {
                records = records.Where(record => string.Equals(record.origin?.locationId, actual.originLocationId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(actual.originInteractionPointId))
            {
                records = records.Where(record => string.Equals(record.origin?.interactionPointId, actual.originInteractionPointId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(actual.subjectId))
            {
                records = records.Where(record => (record.subjectLinks ?? Array.Empty<QuestSubjectLinkData>()).Any(link => string.Equals(link?.subject?.subjectId, actual.subjectId, StringComparison.Ordinal)));
            }

            records = records.Where(record => CanSee(record, actual.access, actual.requesterPersonId));
            return records
                .OrderBy(record => record.createdWorldTime)
                .ThenBy(record => record.questId, StringComparer.Ordinal)
                .Select(record => new QuestSnapshot(record))
                .ToArray();
        }

        public IReadOnlyList<QuestRuntimeEventData> Events => events.Select(value => value.Clone()).ToArray();

        public QuestRuntimeSaveData CreateSaveData()
        {
            return new QuestRuntimeSaveData
            {
                worldId = worldId,
                revision = revision,
                quests = questsById.Values.OrderBy(record => record.questId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                events = events.OrderBy(value => value.runtimeRevision).ThenBy(value => value.eventId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(value => value.transactionId, StringComparer.Ordinal).Select(value => value.Clone()).ToList()
            };
        }

        public QuestRuntimeOperationResult RestoreFromSaveData(QuestRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, expectedWorldId, out string failure))
            {
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.PersistenceInvalid, failure, revision);
            }

            QuestRuntimeSaveData rollback = CreateSaveData();
            try
            {
                Configure(definitionRegistry ?? registry, string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId);
                questsById.Clear();
                transactionsById.Clear();
                events.Clear();
                foreach (QuestRecordData record in saveData.quests ?? new List<QuestRecordData>())
                {
                    questsById[record.questId] = record.Clone();
                }

                foreach (QuestRuntimeTransactionData transaction in saveData.transactions ?? new List<QuestRuntimeTransactionData>())
                {
                    transactionsById[transaction.transactionId] = transaction.Clone();
                }

                events.AddRange((saveData.events ?? new List<QuestRuntimeEventData>()).Select(value => value.Clone()));
                revision = saveData.revision;
                RebuildIndexes();
                return QuestRuntimeOperationResult.Success(null, restoring ? "Quests restored." : "Quests loaded.", revision, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, worldId, restoring: true);
                return QuestRuntimeOperationResult.Failure(QuestRuntimeOperationStatus.RestoreFailed, $"Quest restore failed: {exception.Message}", revision);
            }
        }

        public QuestRuntimeValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), registry, worldId, out _, out QuestRuntimeValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(QuestRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, registry, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(QuestRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure, out QuestRuntimeValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (saveData == null)
            {
                errors.Add("Quest save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != QuestRuntimeSaveData.CurrentSchemaVersion)
                {
                    errors.Add($"Unsupported quest save schema version {saveData.schemaVersion}.");
                }

                string world = string.IsNullOrWhiteSpace(expectedWorldId) ? saveData.worldId : expectedWorldId;
                if (!string.IsNullOrWhiteSpace(world) && !string.Equals(saveData.worldId, world, StringComparison.Ordinal))
                {
                    errors.Add($"Quest save world '{saveData.worldId}' does not match expected world '{world}'.");
                }

                if (registry == null)
                {
                    errors.Add("Quest validation requires a definition registry.");
                }

                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> transactions = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestRecordData record in saveData.quests ?? new List<QuestRecordData>())
                {
                    ValidateRecord(record, registry, ids, errors);
                }

                foreach (QuestRuntimeTransactionData transaction in saveData.transactions ?? new List<QuestRuntimeTransactionData>())
                {
                    if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId))
                    {
                        errors.Add("Quest transaction is missing a transaction ID.");
                    }
                    else if (!transactions.Add(transaction.transactionId))
                    {
                        errors.Add($"Duplicate quest transaction ID '{transaction.transactionId}'.");
                    }
                    else if (!ids.Contains(transaction.questId))
                    {
                        errors.Add($"Quest transaction '{transaction.transactionId}' references missing quest '{transaction.questId}'.");
                    }
                }

                foreach (QuestRuntimeEventData evt in saveData.events ?? new List<QuestRuntimeEventData>())
                {
                    if (evt == null || string.IsNullOrWhiteSpace(evt.eventId))
                    {
                        errors.Add("Quest runtime event is missing an event ID.");
                    }
                    else if (!ids.Contains(evt.questId))
                    {
                        errors.Add($"Quest event '{evt.eventId}' references missing quest '{evt.questId}'.");
                    }
                }
            }

            report = new QuestRuntimeValidationReport(errors, warnings);
            failure = report.Succeeded ? string.Empty : string.Join(" | ", report.Errors);
            return report.Succeeded;
        }

        public void Clear()
        {
            questsById.Clear();
            transactionsById.Clear();
            events.Clear();
            revision = 0L;
            RebuildIndexes();
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
        }

        private static void ValidateRecord(QuestRecordData record, DefinitionRegistry registry, ISet<string> ids, ICollection<string> errors)
        {
            if (record == null)
            {
                errors.Add("Quest record is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(record.questId))
            {
                errors.Add("Quest record is missing a quest ID.");
            }
            else if (!ids.Add(record.questId))
            {
                errors.Add($"Duplicate quest ID '{record.questId}'.");
            }

            if (string.IsNullOrWhiteSpace(record.questDefinitionId))
            {
                errors.Add($"Quest '{record.questId}' is missing a definition ID.");
            }
            else if (registry != null && !registry.TryGet(record.questDefinitionId, out QuestDefinition _))
            {
                errors.Add($"Quest '{record.questId}' references missing quest definition '{record.questDefinitionId}'.");
            }

            if (string.IsNullOrWhiteSpace(record.worldId))
            {
                errors.Add($"Quest '{record.questId}' is missing a world ID.");
            }

            if (record.lifecycleState == QuestRuntimeLifecycleState.Unknown)
            {
                errors.Add($"Quest '{record.questId}' has an unknown lifecycle state.");
            }

            if (record.visibility == QuestVisibility.Unknown)
            {
                errors.Add($"Quest '{record.questId}' has unknown visibility.");
            }

            ValidateIssuer(record, errors);
            ValidateRecipient(record, errors);
            ValidateOrigin(record, errors);
            foreach (QuestSubjectLinkData link in record.subjectLinks ?? Array.Empty<QuestSubjectLinkData>())
            {
                ValidateSubjectLink(record.questId, link, errors);
            }
        }

        private QuestRuntimeOperationStatus ValidateReferences(QuestDefinition definition, QuestIssuerReferenceData issuer, QuestRecipientReferenceData recipient, QuestOriginReferenceData origin, IEnumerable<QuestSubjectLinkData> subjectLinks, out string failure)
        {
            if (issuer.issuerType == QuestIssuerType.Unknown)
            {
                failure = "Quest issuer type is unknown.";
                return QuestRuntimeOperationStatus.InvalidIssuer;
            }

            if (definition.SupportedIssuerTypes.Count > 0 && !definition.SupportedIssuerTypes.Contains(issuer.issuerType))
            {
                failure = $"Quest definition '{definition.Id}' does not support issuer type '{issuer.issuerType}'.";
                return QuestRuntimeOperationStatus.InvalidIssuer;
            }

            if (RequiresIssuerId(issuer.issuerType) && string.IsNullOrWhiteSpace(issuer.issuerId))
            {
                failure = $"Quest issuer type '{issuer.issuerType}' requires an issuer ID.";
                return QuestRuntimeOperationStatus.InvalidIssuer;
            }

            if (recipient.recipientScope == QuestRecipientScope.Unknown)
            {
                failure = "Quest recipient scope is unknown.";
                return QuestRuntimeOperationStatus.InvalidRecipient;
            }

            if (definition.SupportedRecipientScopes.Count > 0 && !definition.SupportedRecipientScopes.Contains(recipient.recipientScope))
            {
                failure = $"Quest definition '{definition.Id}' does not support recipient scope '{recipient.recipientScope}'.";
                return QuestRuntimeOperationStatus.InvalidRecipient;
            }

            if (RequiresRecipientId(recipient.recipientScope) && string.IsNullOrWhiteSpace(recipient.recipientId))
            {
                failure = $"Quest recipient scope '{recipient.recipientScope}' requires a recipient ID.";
                return QuestRuntimeOperationStatus.InvalidRecipient;
            }

            if (origin.sourceChannel == QuestSourceChannel.Unknown)
            {
                failure = "Quest origin source channel is unknown.";
                return QuestRuntimeOperationStatus.InvalidOrigin;
            }

            foreach (QuestSubjectLinkData link in subjectLinks ?? Array.Empty<QuestSubjectLinkData>())
            {
                if (link == null || link.role == QuestSubjectRole.Unknown || link.subject == null || link.subject.subjectType == InformationSubjectType.Unknown || string.IsNullOrWhiteSpace(link.subject.subjectId))
                {
                    failure = "Quest subject links require a role and concrete information subject.";
                    return QuestRuntimeOperationStatus.InvalidSubjectLink;
                }
            }

            failure = string.Empty;
            return QuestRuntimeOperationStatus.Succeeded;
        }

        private QuestRuntimeOperationStatus ValidateUniqueness(QuestDefinition definition, QuestCreateRequest request, QuestRecipientReferenceData recipient, out string failure)
        {
            bool activeSameDefinition = questsById.Values.Any(record => IsActive(record) && string.Equals(record.questDefinitionId, definition.Id, StringComparison.Ordinal));
            if (definition.UniquePerWorld && activeSameDefinition)
            {
                failure = $"Quest definition '{definition.Id}' is unique per world and already has an active quest.";
                return QuestRuntimeOperationStatus.UniqueQuestAlreadyExists;
            }

            if (!definition.AllowMultipleSimultaneousInstances && activeSameDefinition)
            {
                failure = $"Quest definition '{definition.Id}' does not allow multiple simultaneous instances.";
                return QuestRuntimeOperationStatus.MultipleInstancesNotAllowed;
            }

            if (definition.UniquePerRecipient && questsById.Values.Any(record => IsActive(record) && string.Equals(record.questDefinitionId, definition.Id, StringComparison.Ordinal) && string.Equals(record.intendedRecipient?.StableKey, recipient.StableKey, StringComparison.Ordinal)))
            {
                failure = $"Quest definition '{definition.Id}' is unique per recipient '{recipient.StableKey}'.";
                return QuestRuntimeOperationStatus.UniqueQuestAlreadyExists;
            }

            failure = string.Empty;
            return QuestRuntimeOperationStatus.Succeeded;
        }

        private static bool CanSee(QuestRecordData record, QuestVisibilityAccess access, string requesterPersonId)
        {
            if (access == QuestVisibilityAccess.PrivilegedDiagnostic)
            {
                return true;
            }

            if (record.visibility == QuestVisibility.Hidden || record.visibility == QuestVisibility.Diagnostic || record.visibility == QuestVisibility.Development)
            {
                return false;
            }

            if (record.visibility == QuestVisibility.Secret)
            {
                return access == QuestVisibilityAccess.Government || access == QuestVisibilityAccess.OrganizationMember;
            }

            if (record.visibility == QuestVisibility.Restricted || record.visibility == QuestVisibility.OrganizationKnown || record.visibility == QuestVisibility.MemberKnown)
            {
                return access == QuestVisibilityAccess.OrganizationMember || access == QuestVisibilityAccess.Government;
            }

            if (record.visibility == QuestVisibility.GovernmentKnown)
            {
                return access == QuestVisibilityAccess.Government;
            }

            if (record.visibility == QuestVisibility.RecipientKnown)
            {
                return access == QuestVisibilityAccess.Recipient && string.Equals(record.intendedRecipient?.recipientId, requesterPersonId, StringComparison.Ordinal);
            }

            return true;
        }

        private static bool IsActive(QuestRecordData record)
        {
            return record != null && record.lifecycleState != QuestRuntimeLifecycleState.Retired && record.lifecycleState != QuestRuntimeLifecycleState.Historical && record.lifecycleState != QuestRuntimeLifecycleState.Invalid;
        }

        private static string BuildQuestId(QuestDefinition definition, QuestCreateRequest request)
        {
            string baseId = definition.Id.Replace("quest-definition.", "quest.").Replace("quest.", "quest.");
            if (definition.AllowMultipleSimultaneousInstances || definition.AllowDynamicInstances)
            {
                string key = Clean(request.repeatInstanceKey);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return $"{baseId}.{key}";
                }

                return $"{baseId}.{Guid.NewGuid():N}";
            }

            return baseId;
        }

        private static QuestSubjectLinkData[] NormalizeSubjectLinks(IEnumerable<QuestSubjectLinkData> links)
        {
            List<QuestSubjectLinkData> normalized = new List<QuestSubjectLinkData>();
            int index = 0;
            foreach (QuestSubjectLinkData link in links ?? Array.Empty<QuestSubjectLinkData>())
            {
                if (link == null)
                {
                    continue;
                }

                QuestSubjectLinkData clone = link.Clone();
                if (string.IsNullOrWhiteSpace(clone.linkId))
                {
                    clone.linkId = $"subject.{index:000}.{clone.role}.{clone.subject.subjectId}";
                }

                normalized.Add(clone);
                index++;
            }

            return normalized
                .GroupBy(value => value.StableKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.role)
                .ThenBy(value => value.subject.subjectType)
                .ThenBy(value => value.subject.subjectId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool RequiresIssuerId(QuestIssuerType issuerType)
        {
            return issuerType != QuestIssuerType.System && issuerType != QuestIssuerType.Anonymous;
        }

        private static bool RequiresRecipientId(QuestRecipientScope recipientScope)
        {
            return recipientScope != QuestRecipientScope.Open && recipientScope != QuestRecipientScope.Party && recipientScope != QuestRecipientScope.MultiplePersons;
        }

        private static bool RequiresOriginId(QuestOriginReferenceData origin)
        {
            return origin != null && (origin.sourceChannel == QuestSourceChannel.QuestBoard || origin.sourceChannel == QuestSourceChannel.Dialogue || origin.sourceChannel == QuestSourceChannel.Organization || origin.sourceChannel == QuestSourceChannel.Government);
        }

        private static void ValidateIssuer(QuestRecordData record, ICollection<string> errors)
        {
            if (record.issuer == null || record.issuer.issuerType == QuestIssuerType.Unknown)
            {
                errors.Add($"Quest '{record.questId}' has invalid issuer metadata.");
            }
            else if (RequiresIssuerId(record.issuer.issuerType) && string.IsNullOrWhiteSpace(record.issuer.issuerId))
            {
                errors.Add($"Quest '{record.questId}' issuer type '{record.issuer.issuerType}' requires an issuer ID.");
            }
        }

        private static void ValidateRecipient(QuestRecordData record, ICollection<string> errors)
        {
            if (record.intendedRecipient == null || record.intendedRecipient.recipientScope == QuestRecipientScope.Unknown)
            {
                errors.Add($"Quest '{record.questId}' has invalid recipient metadata.");
            }
            else if (RequiresRecipientId(record.intendedRecipient.recipientScope) && string.IsNullOrWhiteSpace(record.intendedRecipient.recipientId))
            {
                errors.Add($"Quest '{record.questId}' recipient scope '{record.intendedRecipient.recipientScope}' requires a recipient ID.");
            }
        }

        private static void ValidateOrigin(QuestRecordData record, ICollection<string> errors)
        {
            if (record.origin == null || record.origin.sourceChannel == QuestSourceChannel.Unknown)
            {
                errors.Add($"Quest '{record.questId}' has invalid origin metadata.");
            }
            else if (RequiresOriginId(record.origin) && string.IsNullOrWhiteSpace(record.origin.locationId) && string.IsNullOrWhiteSpace(record.origin.interactionPointId))
            {
                errors.Add($"Quest '{record.questId}' origin channel '{record.origin.sourceChannel}' requires a location or interaction point reference.");
            }
        }

        private static void ValidateSubjectLink(string questId, QuestSubjectLinkData link, ICollection<string> errors)
        {
            if (link == null)
            {
                errors.Add($"Quest '{questId}' has a null subject link.");
                return;
            }

            if (link.role == QuestSubjectRole.Unknown || link.subject == null || link.subject.subjectType == InformationSubjectType.Unknown || string.IsNullOrWhiteSpace(link.subject.subjectId))
            {
                errors.Add($"Quest '{questId}' has an invalid subject link.");
            }
        }

        private void RecordTransaction(string transactionId, string operation, string questId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            transactionsById[transactionId] = new QuestRuntimeTransactionData { transactionId = transactionId, operation = operation, questId = questId, runtimeRevision = revision };
        }

        private void RecordEvent(string transactionId, string questId, QuestRuntimeEventKind kind, QuestRuntimeLifecycleState before, QuestRuntimeLifecycleState after, double worldTime, string sourceEventId, string provenanceId)
        {
            events.Add(new QuestRuntimeEventData
            {
                eventId = $"quest-event.{revision:000000}.{questId}.{kind}",
                transactionId = transactionId ?? string.Empty,
                questId = questId,
                eventKind = kind,
                beforeState = before,
                afterState = after,
                worldTime = worldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                runtimeRevision = revision
            });
        }

        private void RebuildIndexes()
        {
            byDefinition.Clear();
            byTag.Clear();
            byIssuer.Clear();
            byRecipient.Clear();
            byOriginLocation.Clear();
            byOriginInteraction.Clear();
            bySubject.Clear();
            foreach (QuestRecordData record in questsById.Values.OrderBy(value => value.questId, StringComparer.Ordinal))
            {
                AddIndex(byDefinition, record.questDefinitionId, record.questId);
                AddIndex(byIssuer, record.issuer?.StableKey, record.questId);
                AddIndex(byRecipient, record.intendedRecipient?.StableKey, record.questId);
                AddIndex(byOriginLocation, record.origin?.locationId, record.questId);
                AddIndex(byOriginInteraction, record.origin?.interactionPointId, record.questId);
                foreach (string tag in record.tagIds ?? Array.Empty<string>())
                {
                    AddIndex(byTag, tag, record.questId);
                }

                foreach (QuestSubjectLinkData link in record.subjectLinks ?? Array.Empty<QuestSubjectLinkData>())
                {
                    AddIndex(bySubject, link?.subject?.subjectId, record.questId);
                }
            }
        }

        private static void AddIndex(IDictionary<string, List<string>> index, string key, string questId)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(questId))
            {
                return;
            }

            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }

            if (!values.Contains(questId))
            {
                values.Add(questId);
            }
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
