using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Organizations
{
    public sealed class OrganizationRuntime
    {
        private readonly Dictionary<string, OrganizationRecordData> recordsById = new Dictionary<string, OrganizationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationNameRecordData> namesById = new Dictionary<string, OrganizationNameRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationLinkRecordData> linksById = new Dictionary<string, OrganizationLinkRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationTransactionRecordData> transactionsById = new Dictionary<string, OrganizationTransactionRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string worldId = string.Empty;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownPlaceIds = new HashSet<string>(StringComparer.Ordinal);

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public int Count => recordsById.Count;
        public IReadOnlyList<OrganizationSnapshot> Snapshots => recordsById.Values
            .OrderBy(record => record.organizationId, StringComparer.Ordinal)
            .Select(BuildSnapshot)
            .ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string world, IEnumerable<string> persons = null, IEnumerable<string> places = null)
        {
            registry = definitionRegistry ?? registry;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            knownPersonIds = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            knownPlaceIds = new HashSet<string>(Clean(places), StringComparer.Ordinal);
        }

        public OrganizationOperationResult CreateOrganization(OrganizationCreateRequest request)
        {
            request ??= new OrganizationCreateRequest();
            long before = Revision;
            string organizationId = Normalize(request.organizationId);
            string tx = Normalize(request.transactionId);

            if (TryDuplicate(tx, organizationId, "create", before, out OrganizationOperationResult duplicate))
            {
                return duplicate;
            }

            if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(request.organizationDefinitionId))
            {
                return Fail(OrganizationOperationStatus.InvalidRequest, "Organization ID and definition ID are required.", before);
            }

            if (recordsById.TryGetValue(organizationId, out OrganizationRecordData existing))
            {
                if (string.Equals(existing.organizationDefinitionId, Normalize(request.organizationDefinitionId), StringComparison.Ordinal)
                    && string.Equals(existing.currentName, Normalize(request.officialName), StringComparison.Ordinal))
                {
                    return Succeed(BuildSnapshot(existing), "Organization already exists.", before, before, duplicate: true);
                }

                return Fail(OrganizationOperationStatus.DuplicateOrganizationId, $"Organization '{organizationId}' already exists with different data.", before);
            }

            if (!TryGetDefinition(request.organizationDefinitionId, out OrganizationDefinition definition, before, out OrganizationOperationResult failure))
            {
                return failure;
            }

            OrganizationLifecycleState lifecycle = request.initialLifecycleState == OrganizationLifecycleState.Unknown ? definition.DefaultLifecycleState : request.initialLifecycleState;
            if (lifecycle == OrganizationLifecycleState.Unknown || lifecycle == OrganizationLifecycleState.Dissolved || lifecycle == OrganizationLifecycleState.Archived)
            {
                return Fail(OrganizationOperationStatus.InvalidLifecycleTransition, "Organizations must be created forming, active, or dormant.", before);
            }

            string officialName = NormalizeName(request.officialName);
            if (string.IsNullOrWhiteSpace(officialName))
            {
                return Fail(OrganizationOperationStatus.InvalidName, "Organization official name is required.", before);
            }

            if (!definition.SupportsVisibility(request.visibility))
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{definition.Id}' does not support visibility '{request.visibility}'.", before);
            }

            if (definition.RequireHeadquarters && string.IsNullOrWhiteSpace(request.headquartersPlaceId))
            {
                return Fail(OrganizationOperationStatus.InvalidReference, $"Organization definition '{definition.Id}' requires a headquarters place.", before);
            }

            if (!string.IsNullOrWhiteSpace(request.headquartersPlaceId) && !definition.AllowHeadquarters)
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{definition.Id}' does not support headquarters.", before);
            }

            if ((request.operatingAreaPlaceIds ?? Array.Empty<string>()).Length > 0 && !definition.AllowOperatingAreas)
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{definition.Id}' does not support operating areas.", before);
            }

            if (!ValidateFounders(request.founders, before, out failure)
                || !ValidatePlaces(new[] { request.headquartersPlaceId }.Concat(request.operatingAreaPlaceIds ?? Array.Empty<string>()), before, out failure)
                || !ValidateKnownOrganizations(request.predecessorOrganizationIds, before, out failure))
            {
                return failure;
            }

            OrganizationRuntimeSaveData rollback = CreateSaveData();
            OrganizationRecordData record = new OrganizationRecordData
            {
                organizationId = organizationId,
                organizationDefinitionId = Normalize(request.organizationDefinitionId),
                currentOfficialNameRecordId = BuildNameId(organizationId, "official", 1),
                currentName = officialName,
                shortName = Normalize(request.shortName),
                abbreviation = Normalize(request.abbreviation),
                lifecycleState = lifecycle,
                foundingWorldTime = request.foundingWorldTime,
                activationWorldTime = request.activationWorldTime >= 0d ? request.activationWorldTime : lifecycle == OrganizationLifecycleState.Active ? request.foundingWorldTime : -1d,
                visibility = request.visibility,
                founders = CloneFounders(request.founders),
                predecessorOrganizationIds = Clean(request.predecessorOrganizationIds),
                headquartersPlaceId = Normalize(request.headquartersPlaceId),
                operatingAreaPlaceIds = Clean(request.operatingAreaPlaceIds),
                publicDescription = Normalize(request.publicDescription),
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                tags = Clean(request.tags),
                revision = 1L
            };

            recordsById.Add(record.organizationId, record);
            namesById.Add(record.currentOfficialNameRecordId, new OrganizationNameRecordData
            {
                nameRecordId = record.currentOfficialNameRecordId,
                organizationId = record.organizationId,
                value = record.currentName,
                category = OrganizationNameCategory.Official,
                effectiveStartWorldTime = request.foundingWorldTime,
                visibility = request.visibility,
                sourceEventId = record.sourceEventId,
                sourceRecordId = record.sourceRecordId,
                provenanceId = record.provenanceId,
                revision = 1L
            });

            AddAliasNames(record, request.aliases, definition, request.foundingWorldTime);

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            OrganizationSnapshot snapshot = BuildSnapshot(record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Succeed(snapshot, "Organization creation previewed.", before, before, preview: true);
            }

            CompleteTransaction(tx, "create", organizationId);
            Touch(record);
            return Succeed(BuildSnapshot(record), "Organization created.", before, Revision);
        }

        public OrganizationOperationResult RenameOrganization(OrganizationRenameRequest request)
        {
            request ??= new OrganizationRenameRequest();
            long before = Revision;
            string organizationId = Normalize(request.organizationId);
            if (TryDuplicate(Normalize(request.transactionId), organizationId, "rename", before, out OrganizationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!recordsById.TryGetValue(organizationId, out OrganizationRecordData record))
            {
                return Fail(OrganizationOperationStatus.MissingOrganization, $"Organization '{organizationId}' does not exist.", before);
            }

            string newName = NormalizeName(request.newOfficialName);
            if (string.IsNullOrWhiteSpace(newName))
            {
                return Fail(OrganizationOperationStatus.InvalidName, "New official name is required.", before);
            }

            if (string.Equals(record.currentName, newName, StringComparison.Ordinal))
            {
                return Succeed(BuildSnapshot(record), "Organization already has that official name.", before, before, duplicate: true);
            }

            OrganizationRuntimeSaveData rollback = CreateSaveData();
            if (namesById.TryGetValue(record.currentOfficialNameRecordId, out OrganizationNameRecordData oldName))
            {
                oldName.category = OrganizationNameCategory.FormerOfficial;
                oldName.effectiveEndWorldTime = request.effectiveWorldTime;
                oldName.revision++;
            }

            string nameId = BuildNameId(record.organizationId, "official", namesById.Values.Count(name => string.Equals(name.organizationId, record.organizationId, StringComparison.Ordinal)) + 1);
            namesById[nameId] = new OrganizationNameRecordData
            {
                nameRecordId = nameId,
                organizationId = record.organizationId,
                value = newName,
                category = OrganizationNameCategory.Official,
                effectiveStartWorldTime = request.effectiveWorldTime,
                visibility = record.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                revision = 1L
            };
            record.currentOfficialNameRecordId = nameId;
            record.currentName = newName;

            if (!ValidateCurrent(out string failure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationOperationStatus.PersistenceInvalid, failure, before);
            }

            if (request.preview)
            {
                OrganizationSnapshot preview = BuildSnapshot(record);
                RestoreInternal(rollback);
                return Succeed(preview, "Organization rename previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "rename", organizationId);
            Touch(record);
            return Succeed(BuildSnapshot(record), "Organization renamed.", before, Revision);
        }

        public OrganizationOperationResult TransitionLifecycle(OrganizationLifecycleTransitionRequest request)
        {
            request ??= new OrganizationLifecycleTransitionRequest();
            long before = Revision;
            string organizationId = Normalize(request.organizationId);
            if (TryDuplicate(Normalize(request.transactionId), organizationId, "lifecycle", before, out OrganizationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!recordsById.TryGetValue(organizationId, out OrganizationRecordData record))
            {
                return Fail(OrganizationOperationStatus.MissingOrganization, $"Organization '{organizationId}' does not exist.", before);
            }

            if (!TryGetDefinition(record.organizationDefinitionId, out OrganizationDefinition definition, before, out OrganizationOperationResult failure))
            {
                return failure;
            }

            if (record.lifecycleState == request.targetState)
            {
                return Succeed(BuildSnapshot(record), "Organization lifecycle already has that state.", before, before, duplicate: true);
            }

            if (!IsAllowedTransition(record.lifecycleState, request.targetState, definition))
            {
                return Fail(OrganizationOperationStatus.InvalidLifecycleTransition, $"Cannot transition organization from {record.lifecycleState} to {request.targetState}.", before);
            }

            OrganizationRuntimeSaveData rollback = CreateSaveData();
            record.lifecycleState = request.targetState;
            if (request.targetState == OrganizationLifecycleState.Active)
            {
                record.activationWorldTime = request.worldTime;
            }
            else if (request.targetState == OrganizationLifecycleState.Dormant)
            {
                record.dormancyWorldTime = request.worldTime;
            }
            else if (request.targetState == OrganizationLifecycleState.Dissolved)
            {
                record.dissolutionWorldTime = request.worldTime;
            }

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            if (request.preview)
            {
                OrganizationSnapshot preview = BuildSnapshot(record);
                RestoreInternal(rollback);
                return Succeed(preview, "Organization lifecycle transition previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "lifecycle", organizationId);
            Touch(record);
            return Succeed(BuildSnapshot(record), "Organization lifecycle changed.", before, Revision);
        }

        public OrganizationOperationResult SetPlace(OrganizationPlaceRequest request)
        {
            request ??= new OrganizationPlaceRequest();
            long before = Revision;
            string organizationId = Normalize(request.organizationId);
            string operation = request.kind == OrganizationReferenceKind.OperatingArea ? "operating-area" : "headquarters";
            if (TryDuplicate(Normalize(request.transactionId), organizationId, operation, before, out OrganizationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!recordsById.TryGetValue(organizationId, out OrganizationRecordData record))
            {
                return Fail(OrganizationOperationStatus.MissingOrganization, $"Organization '{organizationId}' does not exist.", before);
            }

            if (!TryGetDefinition(record.organizationDefinitionId, out OrganizationDefinition definition, before, out OrganizationOperationResult failure))
            {
                return failure;
            }

            string placeId = Normalize(request.placeId);
            if (string.IsNullOrWhiteSpace(placeId) || !ValidatePlaces(new[] { placeId }, before, out failure))
            {
                return failure ?? Fail(OrganizationOperationStatus.InvalidReference, "Place ID is required.", before);
            }

            if (request.kind == OrganizationReferenceKind.Headquarters && !definition.AllowHeadquarters)
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{definition.Id}' does not support headquarters.", before);
            }

            if (request.kind == OrganizationReferenceKind.OperatingArea && !definition.AllowOperatingAreas)
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{definition.Id}' does not support operating areas.", before);
            }

            OrganizationRuntimeSaveData rollback = CreateSaveData();
            if (request.kind == OrganizationReferenceKind.OperatingArea)
            {
                record.operatingAreaPlaceIds = Clean((record.operatingAreaPlaceIds ?? Array.Empty<string>()).Concat(new[] { placeId }));
            }
            else
            {
                record.headquartersPlaceId = placeId;
            }

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            if (request.preview)
            {
                OrganizationSnapshot preview = BuildSnapshot(record);
                RestoreInternal(rollback);
                return Succeed(preview, "Organization place update previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, operation, organizationId);
            Touch(record);
            return Succeed(BuildSnapshot(record), "Organization place updated.", before, Revision);
        }

        public OrganizationOperationResult LinkOrganizations(OrganizationLinkRequest request)
        {
            request ??= new OrganizationLinkRequest();
            long before = Revision;
            string sourceId = Normalize(request.sourceOrganizationId);
            string targetId = Normalize(request.targetOrganizationId);
            if (TryDuplicate(Normalize(request.transactionId), sourceId, "link", before, out OrganizationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!recordsById.TryGetValue(sourceId, out OrganizationRecordData source) || !recordsById.ContainsKey(targetId))
            {
                return Fail(OrganizationOperationStatus.MissingOrganization, "Both source and target organizations must exist.", before);
            }

            if (string.Equals(sourceId, targetId, StringComparison.Ordinal))
            {
                return Fail(OrganizationOperationStatus.InvalidReference, "Organization links cannot target themselves.", before);
            }

            if (!TryGetDefinition(source.organizationDefinitionId, out OrganizationDefinition sourceDefinition, before, out OrganizationOperationResult failure))
            {
                return failure;
            }

            if (!SupportsLink(sourceDefinition, request.kind))
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{sourceDefinition.Id}' does not support link kind '{request.kind}'.", before);
            }

            if (!sourceDefinition.AllowMultipleParents
                && request.kind == OrganizationLinkKind.Parent
                && linksById.Values.Any(link => link.IsActive && link.kind == OrganizationLinkKind.Parent && string.Equals(link.sourceOrganizationId, sourceId, StringComparison.Ordinal)))
            {
                return Fail(OrganizationOperationStatus.UnsupportedByDefinition, $"Organization definition '{sourceDefinition.Id}' allows only one active parent.", before);
            }

            OrganizationLinkRecordData existing = linksById.Values.FirstOrDefault(link => link.IsActive
                && link.kind == request.kind
                && string.Equals(link.sourceOrganizationId, sourceId, StringComparison.Ordinal)
                && string.Equals(link.targetOrganizationId, targetId, StringComparison.Ordinal));
            if (existing != null)
            {
                return Succeed(BuildSnapshot(source), "Organization link already exists.", before, before, duplicate: true);
            }

            if (IsHierarchyKind(request.kind) && HasPath(targetId, sourceId, IsHierarchyKind))
            {
                return Fail(OrganizationOperationStatus.CycleDetected, "Organization hierarchy link would create a cycle.", before);
            }

            OrganizationRuntimeSaveData rollback = CreateSaveData();
            string linkId = string.IsNullOrWhiteSpace(request.linkRecordId) ? BuildLinkId(sourceId, request.kind, targetId) : Normalize(request.linkRecordId);
            if (linksById.ContainsKey(linkId))
            {
                return Fail(OrganizationOperationStatus.DuplicateRecordId, $"Organization link '{linkId}' already exists with different data.", before);
            }

            linksById[linkId] = new OrganizationLinkRecordData
            {
                linkRecordId = linkId,
                sourceOrganizationId = sourceId,
                targetOrganizationId = targetId,
                kind = request.kind,
                startWorldTime = request.startWorldTime,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                tags = Clean(request.tags),
                revision = 1L
            };

            RebuildDerivedLinks();
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            if (request.preview)
            {
                OrganizationSnapshot preview = BuildSnapshot(source);
                RestoreInternal(rollback);
                return Succeed(preview, "Organization link previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "link", sourceId);
            Touch(source);
            return Succeed(BuildSnapshot(source), "Organization link created.", before, Revision);
        }

        public bool TryGetSnapshot(string organizationId, out OrganizationSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(organizationId) && recordsById.TryGetValue(organizationId, out OrganizationRecordData record))
            {
                snapshot = BuildSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<OrganizationSnapshot> QueryByCategory(OrganizationCategory category, bool activeOnly = false)
        {
            return recordsById.Values
                .Where(record => (!activeOnly || record.lifecycleState == OrganizationLifecycleState.Active)
                    && registry != null
                    && registry.TryGet(record.organizationDefinitionId, out OrganizationDefinition definition)
                    && definition.Category == category)
                .OrderBy(record => record.organizationId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .ToArray();
        }

        public IReadOnlyList<OrganizationSnapshot> QueryByParent(string parentOrganizationId)
        {
            string parent = Normalize(parentOrganizationId);
            return linksById.Values
                .Where(link => link.IsActive && IsHierarchyKind(link.kind) && string.Equals(link.targetOrganizationId, parent, StringComparison.Ordinal))
                .OrderBy(link => link.sourceOrganizationId, StringComparer.Ordinal)
                .Select(link => recordsById.TryGetValue(link.sourceOrganizationId, out OrganizationRecordData record) ? BuildSnapshot(record) : null)
                .Where(snapshot => snapshot != null)
                .ToArray();
        }

        public OrganizationProjection ProjectOrganization(string organizationId, string requesterPersonId, bool privileged = false)
        {
            string id = Normalize(organizationId);
            InformationSubjectReferenceData subject = CreateInformationSubject(id);
            if (!recordsById.TryGetValue(id, out OrganizationRecordData record))
            {
                return new OrganizationProjection(OrganizationProjectionAccess.Denied, subject, null, string.Empty, $"Organization '{organizationId}' does not exist.");
            }

            if (privileged || record.visibility == OrganizationVisibility.Public)
            {
                return new OrganizationProjection(OrganizationProjectionAccess.Full, subject, BuildSnapshot(record), record.currentName, "Organization projection returned.");
            }

            if (record.visibility == OrganizationVisibility.Hidden)
            {
                return new OrganizationProjection(OrganizationProjectionAccess.Concealed, subject, null, string.Empty, "Organization is concealed.");
            }

            OrganizationRecordData redacted = record.Clone();
            redacted.headquartersPlaceId = string.Empty;
            redacted.operatingAreaPlaceIds = Array.Empty<string>();
            redacted.externalReferences = Array.Empty<OrganizationExternalReferenceData>();
            redacted.founders = Array.Empty<OrganizationFounderReferenceData>();
            redacted.sourceEventId = string.Empty;
            redacted.sourceRecordId = string.Empty;
            redacted.provenanceId = string.Empty;
            return new OrganizationProjection(
                OrganizationProjectionAccess.Redacted,
                subject,
                new OrganizationSnapshot(redacted, VisibleNames(record.organizationId, privileged: false), VisibleLinks(record.organizationId, privileged: false)),
                redacted.currentName,
                "Organization projection redacted.");
        }

        public InformationSubjectReferenceData CreateInformationSubject(string organizationId)
        {
            string id = Normalize(organizationId);
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Organization,
                subjectId = id,
                controllingEntityId = id,
                tags = new[] { "organization" }
            };
        }

        public OrganizationRuntimeSaveData CreateSaveData()
        {
            return new OrganizationRuntimeSaveData
            {
                schemaVersion = OrganizationRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId ?? string.Empty,
                revision = Revision,
                records = recordsById.Values.OrderBy(record => record.organizationId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                names = namesById.Values.OrderBy(name => name.nameRecordId, StringComparer.Ordinal).Select(name => name.Clone()).ToList(),
                links = linksById.Values.OrderBy(link => link.linkRecordId, StringComparer.Ordinal).Select(link => link.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(tx => tx.revision).ThenBy(tx => tx.transactionId, StringComparer.Ordinal).Select(tx => tx.Clone()).RetainNewestTransactions().ToList()
            };
        }

        public OrganizationOperationResult RestoreFromSaveData(OrganizationRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, string world, IEnumerable<string> persons = null, IEnumerable<string> places = null, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, world, persons, places, out string failure))
            {
                return Fail(OrganizationOperationStatus.RestoreFailed, failure, before);
            }

            Configure(definitionRegistry, world, persons, places);
            RestoreInternal(saveData);
            IsDirty = !restoring;
            return Succeed(null, "Organizations restored.", before, Revision);
        }

        public static bool ValidateSaveData(OrganizationRuntimeSaveData saveData, DefinitionRegistry registry, string world, IEnumerable<string> persons, IEnumerable<string> places, out string failure)
        {
            failure = string.Empty;
            OrganizationRuntimeSaveData effective = saveData ?? new OrganizationRuntimeSaveData();
            if (effective.schemaVersion != OrganizationRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Organization save schema version {effective.schemaVersion}.";
                return false;
            }

            if (HasDuplicateIds(effective.records?.Select(record => record?.organizationId), out string duplicateRecord))
            {
                failure = $"Duplicate Organization record ID '{duplicateRecord}'.";
                return false;
            }

            if (HasDuplicateIds(effective.names?.Select(record => record?.nameRecordId), out string duplicateName))
            {
                failure = $"Duplicate Organization name record ID '{duplicateName}'.";
                return false;
            }

            if (HasDuplicateIds(effective.links?.Select(record => record?.linkRecordId), out string duplicateLink))
            {
                failure = $"Duplicate Organization link record ID '{duplicateLink}'.";
                return false;
            }

            if (HasDuplicateIds(effective.transactions?.Select(record => record?.transactionId), out string duplicateTransaction))
            {
                failure = $"Duplicate Organization transaction ID '{duplicateTransaction}'.";
                return false;
            }

            OrganizationRuntime runtime = new OrganizationRuntime();
            runtime.Configure(registry, world, persons, places);
            runtime.RestoreInternal(effective);
            return runtime.ValidateCurrent(out failure);
        }

        private bool ValidateCurrent(out string failure)
        {
            failure = string.Empty;
            OrganizationRuntimeSaveData saveData = CreateSaveData();
            if (saveData.schemaVersion != OrganizationRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Organization save schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.IsNullOrWhiteSpace(worldId) && !string.Equals(saveData.worldId, worldId, StringComparison.Ordinal))
            {
                failure = $"Organization save world '{saveData.worldId}' does not match runtime world '{worldId}'.";
                return false;
            }

            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OrganizationRecordData record in saveData.records ?? new List<OrganizationRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.organizationId))
                {
                    failure = "Organization record has no stable organization ID.";
                    return false;
                }

                if (!recordIds.Add(record.organizationId))
                {
                    failure = $"Duplicate Organization record ID '{record.organizationId}'.";
                    return false;
                }

                OrganizationDefinition definition = null;
                if (registry != null && !registry.TryGet(record.organizationDefinitionId, out definition))
                {
                    failure = $"Organization '{record.organizationId}' references missing Organization Definition '{record.organizationDefinitionId}'.";
                    return false;
                }

                if (definition != null)
                {
                    if (!definition.SupportsVisibility(record.visibility))
                    {
                        failure = $"Organization '{record.organizationId}' visibility '{record.visibility}' is not supported by definition '{definition.Id}'.";
                        return false;
                    }

                    if (definition.RequireHeadquarters && string.IsNullOrWhiteSpace(record.headquartersPlaceId))
                    {
                        failure = $"Organization '{record.organizationId}' requires a headquarters place.";
                        return false;
                    }

                    if (!definition.AllowHeadquarters && !string.IsNullOrWhiteSpace(record.headquartersPlaceId))
                    {
                        failure = $"Organization '{record.organizationId}' has headquarters but definition '{definition.Id}' disallows headquarters.";
                        return false;
                    }

                    if (!definition.AllowOperatingAreas && (record.operatingAreaPlaceIds ?? Array.Empty<string>()).Length > 0)
                    {
                        failure = $"Organization '{record.organizationId}' has operating areas but definition '{definition.Id}' disallows operating areas.";
                        return false;
                    }
                }

                if (string.IsNullOrWhiteSpace(record.currentName) || string.IsNullOrWhiteSpace(record.currentOfficialNameRecordId))
                {
                    failure = $"Organization '{record.organizationId}' is missing current official name data.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(OrganizationLifecycleState), record.lifecycleState) || record.lifecycleState == OrganizationLifecycleState.Unknown)
                {
                    failure = $"Organization '{record.organizationId}' has invalid lifecycle state '{record.lifecycleState}'.";
                    return false;
                }

                if (!ValidateFounders(record.founders, Revision, out OrganizationOperationResult founderFailure))
                {
                    failure = founderFailure.Message;
                    return false;
                }

                if (!ValidatePlaces(new[] { record.headquartersPlaceId }.Concat(record.operatingAreaPlaceIds ?? Array.Empty<string>()), Revision, out OrganizationOperationResult placeFailure))
                {
                    failure = placeFailure.Message;
                    return false;
                }
            }

            foreach (OrganizationNameRecordData name in saveData.names ?? new List<OrganizationNameRecordData>())
            {
                if (name == null || string.IsNullOrWhiteSpace(name.nameRecordId) || string.IsNullOrWhiteSpace(name.organizationId))
                {
                    failure = "Organization name record has missing IDs.";
                    return false;
                }

                if (!recordIds.Contains(name.organizationId))
                {
                    failure = $"Organization name '{name.nameRecordId}' references missing organization '{name.organizationId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(name.value))
                {
                    failure = $"Organization name '{name.nameRecordId}' has no value.";
                    return false;
                }
            }

            foreach (OrganizationRecordData record in saveData.records ?? new List<OrganizationRecordData>())
            {
                List<OrganizationNameRecordData> officialNames = (saveData.names ?? new List<OrganizationNameRecordData>())
                    .Where(name => name != null
                        && string.Equals(name.organizationId, record.organizationId, StringComparison.Ordinal)
                        && name.category == OrganizationNameCategory.Official
                        && name.IsActive)
                    .ToList();
                OrganizationNameRecordData official = officialNames.FirstOrDefault(name => string.Equals(name.nameRecordId, record.currentOfficialNameRecordId, StringComparison.Ordinal));
                if (official == null || official.category != OrganizationNameCategory.Official || !official.IsActive)
                {
                    failure = $"Organization '{record.organizationId}' current official name is missing or inactive.";
                    return false;
                }

                if (officialNames.Count != 1)
                {
                    failure = $"Organization '{record.organizationId}' must have exactly one active official name.";
                    return false;
                }
            }

            foreach (OrganizationLinkRecordData link in saveData.links ?? new List<OrganizationLinkRecordData>())
            {
                if (link == null || string.IsNullOrWhiteSpace(link.linkRecordId) || string.IsNullOrWhiteSpace(link.sourceOrganizationId) || string.IsNullOrWhiteSpace(link.targetOrganizationId))
                {
                    failure = "Organization link record has missing IDs.";
                    return false;
                }

                if (!recordIds.Contains(link.sourceOrganizationId) || !recordIds.Contains(link.targetOrganizationId))
                {
                    failure = $"Organization link '{link.linkRecordId}' references missing organization.";
                    return false;
                }

                if (string.Equals(link.sourceOrganizationId, link.targetOrganizationId, StringComparison.Ordinal))
                {
                    failure = $"Organization link '{link.linkRecordId}' targets itself.";
                    return false;
                }

                if (recordsById.TryGetValue(link.sourceOrganizationId, out OrganizationRecordData source)
                    && registry != null
                    && registry.TryGet(source.organizationDefinitionId, out OrganizationDefinition sourceDefinition)
                    && !SupportsLink(sourceDefinition, link.kind))
                {
                    failure = $"Organization link '{link.linkRecordId}' uses unsupported link kind '{link.kind}' for definition '{sourceDefinition.Id}'.";
                    return false;
                }
            }

            foreach (OrganizationLinkRecordData link in saveData.links ?? new List<OrganizationLinkRecordData>())
            {
                if (link.IsActive && IsHierarchyKind(link.kind) && HasPath(link.targetOrganizationId, link.sourceOrganizationId, IsHierarchyKind))
                {
                    failure = $"Organization hierarchy link '{link.linkRecordId}' creates a cycle.";
                    return false;
                }
            }

            return true;
        }

        private OrganizationSnapshot BuildSnapshot(OrganizationRecordData record)
        {
            return new OrganizationSnapshot(record, NamesFor(record.organizationId), LinksFor(record.organizationId));
        }

        private IEnumerable<OrganizationNameRecordData> NamesFor(string organizationId)
        {
            return namesById.Values.Where(name => string.Equals(name.organizationId, organizationId, StringComparison.Ordinal));
        }

        private IEnumerable<OrganizationLinkRecordData> LinksFor(string organizationId)
        {
            return linksById.Values.Where(link => string.Equals(link.sourceOrganizationId, organizationId, StringComparison.Ordinal) || string.Equals(link.targetOrganizationId, organizationId, StringComparison.Ordinal));
        }

        private IEnumerable<OrganizationNameRecordData> VisibleNames(string organizationId, bool privileged)
        {
            return NamesFor(organizationId).Where(name => privileged || name.visibility == OrganizationVisibility.Public || name.visibility == OrganizationVisibility.Restricted);
        }

        private IEnumerable<OrganizationLinkRecordData> VisibleLinks(string organizationId, bool privileged)
        {
            return LinksFor(organizationId).Where(link => privileged || link.visibility == OrganizationVisibility.Public || link.visibility == OrganizationVisibility.Restricted);
        }

        private bool TryGetDefinition(string definitionId, out OrganizationDefinition definition, long before, out OrganizationOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry == null || !registry.TryGet(Normalize(definitionId), out definition))
            {
                failure = Fail(OrganizationOperationStatus.MissingDefinition, $"Organization Definition '{definitionId}' was not found.", before);
                return false;
            }

            return true;
        }

        private bool TryDuplicate(string transactionId, string organizationId, string operation, long before, out OrganizationOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out OrganizationTransactionRecordData transaction))
            {
                return false;
            }

            if (string.Equals(transaction.operation, operation, StringComparison.Ordinal)
                && string.Equals(transaction.organizationId, organizationId, StringComparison.Ordinal)
                && recordsById.TryGetValue(organizationId, out OrganizationRecordData record))
            {
                result = Succeed(BuildSnapshot(record), "Organization transaction already applied.", before, before, duplicate: true);
                return true;
            }

            result = Fail(OrganizationOperationStatus.DuplicateRecordId, $"Transaction '{transactionId}' was already used for a different organization mutation.", before);
            return true;
        }

        private void CompleteTransaction(string transactionId, string operation, string organizationId)
        {
            string tx = Normalize(transactionId);
            if (string.IsNullOrWhiteSpace(tx))
            {
                return;
            }

            transactionsById[tx] = new OrganizationTransactionRecordData
            {
                transactionId = tx,
                operation = operation ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                revision = Revision + 1L
            };
        }

        private void AddAliasNames(OrganizationRecordData record, IEnumerable<string> aliases, OrganizationDefinition definition, double worldTime)
        {
            if (!definition.AllowAliases)
            {
                return;
            }

            int next = namesById.Values.Count(name => string.Equals(name.organizationId, record.organizationId, StringComparison.Ordinal)) + 1;
            foreach (string alias in Clean(aliases))
            {
                string id = BuildNameId(record.organizationId, "alias", next++);
                namesById[id] = new OrganizationNameRecordData
                {
                    nameRecordId = id,
                    organizationId = record.organizationId,
                    value = alias,
                    category = OrganizationNameCategory.Alias,
                    effectiveStartWorldTime = worldTime,
                    visibility = record.visibility,
                    revision = 1L
                };
            }
        }

        private bool ValidateFounders(IEnumerable<OrganizationFounderReferenceData> founders, long before, out OrganizationOperationResult failure)
        {
            failure = null;
            foreach (OrganizationFounderReferenceData founder in founders ?? Array.Empty<OrganizationFounderReferenceData>())
            {
                if (founder == null || string.IsNullOrWhiteSpace(founder.subjectId))
                {
                    failure = Fail(OrganizationOperationStatus.InvalidReference, "Founder references must have a subject ID.", before);
                    return false;
                }

                if (founder.kind == OrganizationFounderKind.Person && knownPersonIds.Count > 0 && !knownPersonIds.Contains(founder.subjectId))
                {
                    failure = Fail(OrganizationOperationStatus.InvalidReference, $"Founder person '{founder.subjectId}' is not known to the organization runtime.", before);
                    return false;
                }

                if (founder.kind == OrganizationFounderKind.Organization && !recordsById.ContainsKey(founder.subjectId))
                {
                    failure = Fail(OrganizationOperationStatus.InvalidReference, $"Founder organization '{founder.subjectId}' does not exist.", before);
                    return false;
                }
            }

            return true;
        }

        private bool ValidatePlaces(IEnumerable<string> places, long before, out OrganizationOperationResult failure)
        {
            failure = null;
            foreach (string raw in places ?? Array.Empty<string>())
            {
                string place = Normalize(raw);
                if (string.IsNullOrWhiteSpace(place))
                {
                    continue;
                }

                if (knownPlaceIds.Count > 0 && !knownPlaceIds.Contains(place))
                {
                    failure = Fail(OrganizationOperationStatus.InvalidReference, $"Place '{place}' is not known to the organization runtime.", before);
                    return false;
                }
            }

            return true;
        }

        private bool ValidateKnownOrganizations(IEnumerable<string> organizationIds, long before, out OrganizationOperationResult failure)
        {
            failure = null;
            foreach (string organizationId in Clean(organizationIds))
            {
                if (!recordsById.ContainsKey(organizationId))
                {
                    failure = Fail(OrganizationOperationStatus.InvalidReference, $"Referenced organization '{organizationId}' does not exist.", before);
                    return false;
                }
            }

            return true;
        }

        private void RebuildDerivedLinks()
        {
            foreach (OrganizationRecordData record in recordsById.Values)
            {
                string id = record.organizationId;
                record.parentOrganizationIds = Clean(linksById.Values.Where(link => link.IsActive && IsHierarchyKind(link.kind) && string.Equals(link.sourceOrganizationId, id, StringComparison.Ordinal)).Select(link => link.targetOrganizationId));
                record.predecessorOrganizationIds = Clean(linksById.Values.Where(link => link.IsActive && (link.kind == OrganizationLinkKind.Predecessor || link.kind == OrganizationLinkKind.SplitFrom || link.kind == OrganizationLinkKind.MergedFrom) && string.Equals(link.sourceOrganizationId, id, StringComparison.Ordinal)).Select(link => link.targetOrganizationId));
                record.successorOrganizationIds = Clean(linksById.Values.Where(link => link.IsActive && link.kind == OrganizationLinkKind.Successor && string.Equals(link.sourceOrganizationId, id, StringComparison.Ordinal)).Select(link => link.targetOrganizationId));
            }
        }

        private bool HasPath(string startOrganizationId, string targetOrganizationId, Func<OrganizationLinkKind, bool> linkFilter)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Stack<string> pending = new Stack<string>();
            pending.Push(startOrganizationId);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (string.Equals(current, targetOrganizationId, StringComparison.Ordinal))
                {
                    return true;
                }

                foreach (OrganizationLinkRecordData link in linksById.Values.Where(link => link.IsActive && linkFilter(link.kind) && string.Equals(link.sourceOrganizationId, current, StringComparison.Ordinal)))
                {
                    pending.Push(link.targetOrganizationId);
                }
            }

            return false;
        }

        private static bool SupportsLink(OrganizationDefinition definition, OrganizationLinkKind kind)
        {
            return kind switch
            {
                OrganizationLinkKind.Parent or OrganizationLinkKind.Branch => definition.AllowBranches,
                OrganizationLinkKind.Affiliate => definition.AllowAffiliates,
                OrganizationLinkKind.Predecessor or OrganizationLinkKind.Successor or OrganizationLinkKind.SplitFrom or OrganizationLinkKind.MergedFrom => definition.AllowSuccessors,
                _ => true
            };
        }

        private static bool IsHierarchyKind(OrganizationLinkKind kind)
        {
            return kind == OrganizationLinkKind.Parent || kind == OrganizationLinkKind.Branch;
        }

        private static bool IsAllowedTransition(OrganizationLifecycleState from, OrganizationLifecycleState to, OrganizationDefinition definition)
        {
            if (to == OrganizationLifecycleState.Dissolved && !definition.AllowDissolution)
            {
                return false;
            }

            return from switch
            {
                OrganizationLifecycleState.Forming => to == OrganizationLifecycleState.Active || to == OrganizationLifecycleState.Dissolved,
                OrganizationLifecycleState.Active => to == OrganizationLifecycleState.Dormant || to == OrganizationLifecycleState.Dissolved,
                OrganizationLifecycleState.Dormant => to == OrganizationLifecycleState.Active || to == OrganizationLifecycleState.Dissolved,
                OrganizationLifecycleState.Dissolved => to == OrganizationLifecycleState.Archived,
                _ => false
            };
        }

        private void RestoreInternal(OrganizationRuntimeSaveData saveData)
        {
            recordsById.Clear();
            namesById.Clear();
            linksById.Clear();
            transactionsById.Clear();
            OrganizationRuntimeSaveData effective = saveData?.Clone() ?? new OrganizationRuntimeSaveData { worldId = worldId };
            worldId = effective.worldId ?? worldId ?? string.Empty;
            Revision = effective.revision;
            foreach (OrganizationRecordData record in effective.records ?? new List<OrganizationRecordData>())
            {
                recordsById[record.organizationId] = record.Clone();
            }

            foreach (OrganizationNameRecordData name in effective.names ?? new List<OrganizationNameRecordData>())
            {
                namesById[name.nameRecordId] = name.Clone();
            }

            foreach (OrganizationLinkRecordData link in effective.links ?? new List<OrganizationLinkRecordData>())
            {
                linksById[link.linkRecordId] = link.Clone();
            }

            foreach (OrganizationTransactionRecordData transaction in effective.transactions ?? new List<OrganizationTransactionRecordData>())
            {
                transactionsById[transaction.transactionId] = transaction.Clone();
            }
        }

        private void Touch(OrganizationRecordData record)
        {
            Revision++;
            IsDirty = true;
            if (record != null)
            {
                record.revision++;
            }
        }

        private static OrganizationFounderReferenceData[] CloneFounders(IEnumerable<OrganizationFounderReferenceData> founders)
        {
            return (founders ?? Array.Empty<OrganizationFounderReferenceData>())
                .Where(founder => founder != null)
                .Select(founder => founder.Clone())
                .OrderBy(founder => founder.kind.ToString(), StringComparer.Ordinal)
                .ThenBy(founder => founder.subjectId, StringComparer.Ordinal)
                .ToArray();
        }

        private static string BuildNameId(string organizationId, string category, int sequence)
        {
            return $"{Normalize(organizationId)}.name.{Normalize(category)}.{Math.Max(1, sequence):000}";
        }

        private static string BuildLinkId(string sourceId, OrganizationLinkKind kind, string targetId)
        {
            return $"{Normalize(sourceId)}.link.{kind.ToString().ToLowerInvariant()}.{Normalize(targetId).Replace("organization.", string.Empty)}";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeName(string value)
        {
            return Normalize(value);
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return OrganizationModelUtility.Clean(values);
        }

        private static bool HasDuplicateIds(IEnumerable<string> values, out string duplicate)
        {
            duplicate = string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                string normalized = Normalize(value);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!seen.Add(normalized))
                {
                    duplicate = normalized;
                    return true;
                }
            }

            return false;
        }

        private static OrganizationOperationResult Succeed(OrganizationSnapshot snapshot, string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return OrganizationOperationResult.Success(snapshot, message, before, after, preview, duplicate);
        }

        private static OrganizationOperationResult Fail(OrganizationOperationStatus status, string message, long before)
        {
            return OrganizationOperationResult.Failure(status, message, before);
        }
    }
}
