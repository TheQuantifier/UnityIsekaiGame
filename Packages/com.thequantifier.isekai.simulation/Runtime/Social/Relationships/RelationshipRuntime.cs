using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Relationships
{
    public sealed class RelationshipRuntime
    {
        private readonly Dictionary<string, RelationshipRecordData> recordsById = new Dictionary<string, RelationshipRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int Count => recordsById.Count;
        public IReadOnlyList<RelationshipSnapshot> Snapshots => Ordered(recordsById.Values).Select(record => new RelationshipSnapshot(record)).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, IEnumerable<string> persons)
        {
            registry = definitionRegistry;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
        }

        public RelationshipOperationResult CreateRelationship(RelationshipCreateRequest request)
        {
            request ??= new RelationshipCreateRequest();
            long before = revision;
            string recordId = string.IsNullOrWhiteSpace(request.recordId)
                ? BuildDefaultRecordId(request.relationshipDefinitionId, request.firstPersonId, request.secondPersonId)
                : request.recordId.Trim();

            if (recordsById.TryGetValue(recordId, out RelationshipRecordData existing))
            {
                if (IsEquivalent(existing, request, recordId))
                {
                    return RelationshipOperationResult.Success(new RelationshipSnapshot(existing), "Relationship record already exists.", before, before, duplicate: true);
                }

                return RelationshipOperationResult.Failure(RelationshipOperationStatus.DuplicateRecordId, $"Relationship record ID '{recordId}' already exists with different data.", before);
            }

            if (!TryBuildRecord(request, recordId, out RelationshipRecordData record, out RelationshipOperationResult failure))
            {
                return failure;
            }

            RelationshipRuntimeSaveData rollback = request.preview ? CreateSaveData() : null;
            recordsById[record.recordId] = record;
            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, out string validationFailure))
            {
                if (request.preview)
                {
                    RestoreInternal(rollback);
                }
                else
                {
                    recordsById.Remove(record.recordId);
                }

                return RelationshipOperationResult.Failure(MapValidationStatus(validationFailure), validationFailure, before);
            }

            RelationshipSnapshot snapshot = new RelationshipSnapshot(record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return RelationshipOperationResult.Success(snapshot, "Relationship creation previewed.", before, before, preview: true);
            }

            revision++;
            dirty = true;
            record.revision = Math.Max(1L, record.revision);
            return RelationshipOperationResult.Success(new RelationshipSnapshot(record), "Relationship created.", before, revision);
        }

        public RelationshipOperationResult EndRelationship(RelationshipEndRequest request)
        {
            request ??= new RelationshipEndRequest();
            long before = revision;
            string recordId = request.recordId ?? string.Empty;
            if (!recordsById.TryGetValue(recordId, out RelationshipRecordData record))
            {
                return RelationshipOperationResult.Failure(RelationshipOperationStatus.MissingRelationship, $"Relationship record '{recordId}' does not exist.", before);
            }

            if (!TryGetDefinition(record.relationshipDefinitionId, out RelationshipDefinition definition, out RelationshipOperationResult failure))
            {
                return failure;
            }

            if (!definition.MayEnd)
            {
                return RelationshipOperationResult.Failure(RelationshipOperationStatus.CannotEndRelationship, $"Relationship Definition '{definition.Id}' cannot be ended by ordinary lifecycle mutation.", before);
            }

            if (record.status == RelationshipLifecycleStatus.Ended)
            {
                return RelationshipOperationResult.Success(new RelationshipSnapshot(record), "Relationship already ended.", before, before, duplicate: true);
            }

            if (request.endWorldTime < record.startWorldTime)
            {
                return RelationshipOperationResult.Failure(RelationshipOperationStatus.InvalidTimeRange, "Relationship end time cannot be before start time.", before);
            }

            RelationshipRuntimeSaveData rollback = CreateSaveData();
            record.status = RelationshipLifecycleStatus.Ended;
            record.endWorldTime = request.endWorldTime;
            record.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? record.sourceEventId : request.sourceEventId.Trim();
            record.sourceRecordId = string.IsNullOrWhiteSpace(request.sourceRecordId) ? record.sourceRecordId : request.sourceRecordId.Trim();
            record.revision++;

            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, out string validationFailure))
            {
                RestoreInternal(rollback);
                return RelationshipOperationResult.Failure(MapValidationStatus(validationFailure), validationFailure, before);
            }

            RelationshipSnapshot snapshot = new RelationshipSnapshot(record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return RelationshipOperationResult.Success(snapshot, "Relationship ending previewed.", before, before, preview: true);
            }

            revision++;
            dirty = true;
            return RelationshipOperationResult.Success(snapshot, "Relationship ended.", before, revision);
        }

        public void Clear()
        {
            if (recordsById.Count == 0)
            {
                return;
            }

            recordsById.Clear();
            revision++;
            dirty = true;
        }

        public bool TryGetSnapshot(string recordId, out RelationshipSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(recordId) && recordsById.TryGetValue(recordId, out RelationshipRecordData record))
            {
                snapshot = new RelationshipSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<RelationshipSnapshot> QueryByPerson(string personId, bool activeOnly = false)
        {
            return Query(record => IncludesPerson(record, personId) && (!activeOnly || record.status == RelationshipLifecycleStatus.Active));
        }

        public IReadOnlyList<RelationshipSnapshot> QueryBetween(string firstPersonId, string secondPersonId, bool activeOnly = false)
        {
            return Query(record =>
            {
                if (activeOnly && record.status != RelationshipLifecycleStatus.Active)
                {
                    return false;
                }

                return IncludesPerson(record, firstPersonId) && IncludesPerson(record, secondPersonId);
            });
        }

        public IReadOnlyList<RelationshipSnapshot> QueryByDefinition(string relationshipDefinitionId, bool activeOnly = false)
        {
            return Query(record => string.Equals(record.relationshipDefinitionId, relationshipDefinitionId, StringComparison.Ordinal) && (!activeOnly || record.status == RelationshipLifecycleStatus.Active));
        }

        public IReadOnlyList<RelationshipSnapshot> QueryByCategory(RelationshipCategory category, bool activeOnly = false)
        {
            return Query(record =>
            {
                if (activeOnly && record.status != RelationshipLifecycleStatus.Active)
                {
                    return false;
                }

                return registry != null
                    && registry.TryGet(record.relationshipDefinitionId, out RelationshipDefinition definition)
                    && definition.Category == category;
            });
        }

        public IReadOnlyList<RelationshipSnapshot> QueryByStatus(RelationshipLifecycleStatus status)
        {
            return Query(record => record.status == status);
        }

        public IReadOnlyList<RelationshipSnapshot> QueryByRole(string roleId, bool activeOnly = false)
        {
            return Query(record => (!activeOnly || record.status == RelationshipLifecycleStatus.Active)
                && (record.participants ?? Array.Empty<RelationshipEndpointData>()).Any(endpoint => string.Equals(endpoint.roleId, roleId, StringComparison.Ordinal)));
        }

        public RelationshipRuntimeSaveData CreateSaveData()
        {
            return new RelationshipRuntimeSaveData
            {
                schemaVersion = RelationshipRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = Ordered(recordsById.Values).Select(record => record.Clone()).ToList()
            };
        }

        public RelationshipOperationResult RestoreFromSaveData(RelationshipRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoring = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failureReason))
            {
                return RelationshipOperationResult.Failure(RelationshipOperationStatus.RestoreFailed, failureReason, before);
            }

            Configure(definitionRegistry, persons);
            RestoreInternal(saveData);
            dirty = !restoring;
            return RelationshipOperationResult.Success(null, "Relationships restored.", before, revision);
        }

        public static bool ValidateSaveData(RelationshipRuntimeSaveData saveData, DefinitionRegistry registry, IEnumerable<string> knownPersons, out string failureReason)
        {
            failureReason = string.Empty;
            RelationshipRuntimeSaveData effective = saveData ?? new RelationshipRuntimeSaveData();
            if (effective.schemaVersion != RelationshipRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Relationship save schema version {effective.schemaVersion}.";
                return false;
            }

            HashSet<string> persons = new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            return ValidateAll(effective, registry, persons, out failureReason);
        }

        private bool TryBuildRecord(RelationshipCreateRequest request, string recordId, out RelationshipRecordData record, out RelationshipOperationResult failure)
        {
            record = null;
            failure = null;
            long before = revision;
            if (!TryGetDefinition(request.relationshipDefinitionId, out RelationshipDefinition definition, out failure))
            {
                return false;
            }

            RelationshipEndpointData first = new RelationshipEndpointData { personId = request.firstPersonId?.Trim(), roleId = request.firstRoleId?.Trim() };
            RelationshipEndpointData second = new RelationshipEndpointData { personId = request.secondPersonId?.Trim(), roleId = request.secondRoleId?.Trim() };
            RelationshipEndpointData[] endpoints = NormalizeEndpoints(definition, first, second);
            record = new RelationshipRecordData
            {
                recordId = recordId,
                relationshipDefinitionId = definition.Id,
                status = RelationshipLifecycleStatus.Active,
                participants = endpoints,
                startWorldTime = request.startWorldTime,
                endWorldTime = -1d,
                sourceEventId = request.sourceEventId ?? string.Empty,
                sourceRecordId = request.sourceRecordId ?? string.Empty,
                accessPolicyId = string.IsNullOrWhiteSpace(request.accessPolicyId) ? definition.DefaultAccessPolicyId : request.accessPolicyId.Trim(),
                tags = RelationshipRecordData.Clean((request.tags ?? Array.Empty<string>()).Concat(definition.Tags)),
                revision = 1L
            };

            if (string.IsNullOrWhiteSpace(record.recordId))
            {
                failure = RelationshipOperationResult.Failure(RelationshipOperationStatus.InvalidRequest, "Relationship record ID is required.", before);
                return false;
            }

            return true;
        }

        private bool TryGetDefinition(string relationshipDefinitionId, out RelationshipDefinition definition, out RelationshipOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry == null)
            {
                failure = RelationshipOperationResult.Failure(RelationshipOperationStatus.MissingDefinitionRegistry, "Relationship runtime has no definition registry.", revision);
                return false;
            }

            if (string.IsNullOrWhiteSpace(relationshipDefinitionId) || !registry.TryGet(relationshipDefinitionId.Trim(), out definition))
            {
                failure = RelationshipOperationResult.Failure(RelationshipOperationStatus.MissingDefinition, $"Relationship Definition '{relationshipDefinitionId}' is missing.", revision);
                return false;
            }

            return true;
        }

        private IReadOnlyList<RelationshipSnapshot> Query(Func<RelationshipRecordData, bool> predicate)
        {
            return Ordered(recordsById.Values.Where(predicate)).Select(record => new RelationshipSnapshot(record)).ToArray();
        }

        private void RestoreInternal(RelationshipRuntimeSaveData saveData)
        {
            recordsById.Clear();
            foreach (RelationshipRecordData record in saveData?.records ?? new List<RelationshipRecordData>())
            {
                if (record != null)
                {
                    RelationshipRecordData clone = record.Clone();
                    recordsById[clone.recordId] = clone;
                }
            }

            revision = saveData?.revision ?? 0L;
        }

        private static bool ValidateAll(RelationshipRuntimeSaveData saveData, DefinitionRegistry registry, HashSet<string> knownPersons, out string failure)
        {
            failure = string.Empty;
            if (registry == null)
            {
                failure = "Relationship runtime requires a definition registry.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> activeKeys = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RelationshipRecordData raw in saveData.records ?? new List<RelationshipRecordData>())
            {
                RelationshipRecordData record = raw?.Clone();
                if (record == null)
                {
                    failure = "Relationship save contains a null record.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.recordId) || !ids.Add(record.recordId))
                {
                    failure = $"Relationship save contains duplicate or empty record ID '{record.recordId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.relationshipDefinitionId)
                    || !registry.TryGet(record.relationshipDefinitionId, out RelationshipDefinition definition))
                {
                    failure = $"Relationship record '{record.recordId}' references missing Relationship Definition '{record.relationshipDefinitionId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(RelationshipLifecycleStatus), record.status))
                {
                    failure = $"Relationship record '{record.recordId}' has invalid lifecycle status '{record.status}'.";
                    return false;
                }

                RelationshipEndpointData[] endpoints = record.participants ?? Array.Empty<RelationshipEndpointData>();
                if (endpoints.Length != 2)
                {
                    failure = $"Relationship record '{record.recordId}' must contain exactly two participants.";
                    return false;
                }

                foreach (RelationshipEndpointData endpoint in endpoints)
                {
                    if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.personId))
                    {
                        failure = $"Relationship record '{record.recordId}' has a missing participant person ID.";
                        return false;
                    }

                    if (knownPersons != null && knownPersons.Count > 0 && !knownPersons.Contains(endpoint.personId))
                    {
                        failure = $"Relationship record '{record.recordId}' references unknown Person '{endpoint.personId}'.";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(endpoint.roleId) || !definition.HasRole(endpoint.roleId))
                    {
                        failure = $"Relationship record '{record.recordId}' uses invalid role '{endpoint.roleId}' for Relationship Definition '{definition.Id}'.";
                        return false;
                    }
                }

                if (!definition.AllowSelfRelationship && string.Equals(endpoints[0].personId, endpoints[1].personId, StringComparison.Ordinal))
                {
                    failure = $"Relationship record '{record.recordId}' cannot relate Person '{endpoints[0].personId}' to themself.";
                    return false;
                }

                if (record.status == RelationshipLifecycleStatus.Active && record.endWorldTime >= 0d)
                {
                    failure = $"Relationship record '{record.recordId}' is active but has an end time.";
                    return false;
                }

                if (record.status == RelationshipLifecycleStatus.Ended && record.endWorldTime < record.startWorldTime)
                {
                    failure = $"Relationship record '{record.recordId}' has end time before start time.";
                    return false;
                }

                if (record.status == RelationshipLifecycleStatus.Active && definition.DuplicatePolicy != RelationshipDuplicatePolicy.AllowMultipleActive)
                {
                    string key = ActiveDuplicateKey(definition, record);
                    activeKeys.TryGetValue(key, out int count);
                    activeKeys[key] = count + 1;
                    if (activeKeys[key] > 1)
                    {
                        failure = $"Relationship runtime contains duplicate active relationship for key '{key}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static RelationshipEndpointData[] NormalizeEndpoints(RelationshipDefinition definition, RelationshipEndpointData first, RelationshipEndpointData second)
        {
            RelationshipEndpointData[] endpoints = { first?.Clone() ?? new RelationshipEndpointData(), second?.Clone() ?? new RelationshipEndpointData() };
            if (definition.Directionality != RelationshipDirectionality.Symmetric || !definition.CanonicalizeSymmetricParticipants)
            {
                return endpoints;
            }

            return endpoints
                .OrderBy(endpoint => endpoint.personId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.roleId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<RelationshipRecordData> Ordered(IEnumerable<RelationshipRecordData> records)
        {
            return (records ?? Array.Empty<RelationshipRecordData>())
                .OrderBy(record => record.startWorldTime)
                .ThenBy(record => record.relationshipDefinitionId, StringComparer.Ordinal)
                .ThenBy(record => PairKey(record), StringComparer.Ordinal)
                .ThenBy(record => record.recordId, StringComparer.Ordinal);
        }

        private static bool IncludesPerson(RelationshipRecordData record, string personId)
        {
            return !string.IsNullOrWhiteSpace(personId)
                && (record.participants ?? Array.Empty<RelationshipEndpointData>()).Any(endpoint => string.Equals(endpoint.personId, personId, StringComparison.Ordinal));
        }

        private static string ActiveDuplicateKey(RelationshipDefinition definition, RelationshipRecordData record)
        {
            string pair = PairKey(record);
            if (definition.DuplicatePolicy == RelationshipDuplicatePolicy.OneActiveBetweenParticipants)
            {
                return $"{definition.Id}|{pair}";
            }

            string roleKey = string.Join(",", (record.participants ?? Array.Empty<RelationshipEndpointData>())
                .OrderBy(endpoint => definition.Directionality == RelationshipDirectionality.Symmetric ? endpoint.personId : endpoint.roleId, StringComparer.Ordinal)
                .Select(endpoint => $"{endpoint.roleId}:{endpoint.personId}"));
            return $"{definition.Id}|{roleKey}";
        }

        private static string PairKey(RelationshipRecordData record)
        {
            return string.Join("|", (record.participants ?? Array.Empty<RelationshipEndpointData>())
                .Select(endpoint => endpoint.personId ?? string.Empty)
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static string BuildDefaultRecordId(string relationshipDefinitionId, string firstPersonId, string secondPersonId)
        {
            string first = firstPersonId ?? string.Empty;
            string second = secondPersonId ?? string.Empty;
            string[] ordered = new[] { first, second }.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return $"relationship-record.{relationshipDefinitionId}.{ordered[0]}.{ordered[1]}";
        }

        private static RelationshipOperationStatus MapValidationStatus(string validationFailure)
        {
            if (validationFailure == null)
            {
                return RelationshipOperationStatus.ValidationFailed;
            }

            if (validationFailure.Contains("duplicate active", StringComparison.Ordinal))
            {
                return RelationshipOperationStatus.DuplicateActiveRelationship;
            }

            if (validationFailure.Contains("unknown Person", StringComparison.Ordinal))
            {
                return RelationshipOperationStatus.UnknownPerson;
            }

            if (validationFailure.Contains("missing Relationship Definition", StringComparison.Ordinal))
            {
                return RelationshipOperationStatus.MissingDefinition;
            }

            if (validationFailure.Contains("invalid role", StringComparison.Ordinal))
            {
                return RelationshipOperationStatus.InvalidRole;
            }

            if (validationFailure.Contains("themself", StringComparison.Ordinal))
            {
                return RelationshipOperationStatus.SelfRelationshipNotAllowed;
            }

            if (validationFailure.Contains("end time before start time", StringComparison.Ordinal))
            {
                return RelationshipOperationStatus.InvalidTimeRange;
            }

            return RelationshipOperationStatus.ValidationFailed;
        }

        private static bool IsEquivalent(RelationshipRecordData existing, RelationshipCreateRequest request, string recordId)
        {
            if (existing == null)
            {
                return false;
            }

            string[] existingPersons = (existing.participants ?? Array.Empty<RelationshipEndpointData>()).Select(endpoint => endpoint.personId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] requestedPersons = new[] { request.firstPersonId ?? string.Empty, request.secondPersonId ?? string.Empty }.Select(value => value.Trim()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return string.Equals(existing.recordId, recordId, StringComparison.Ordinal)
                && string.Equals(existing.relationshipDefinitionId, request.relationshipDefinitionId?.Trim() ?? string.Empty, StringComparison.Ordinal)
                && existing.status == RelationshipLifecycleStatus.Active
                && Math.Abs(existing.startWorldTime - request.startWorldTime) < 0.0001d
                && existingPersons.SequenceEqual(requestedPersons, StringComparer.Ordinal);
        }
    }
}
