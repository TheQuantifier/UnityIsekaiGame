using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    public sealed class PersonProfessionRuntime
    {
        private static readonly string[] AllProjectionFields =
        {
            "relationship-id",
            "person-id",
            "profession-id",
            "state",
            "practice-form",
            "self-declared",
            "recognized",
            "primary",
            "active",
            "world-time",
            "specializations",
            "recognition-authority",
            "access-policy",
            "provenance",
            "tags"
        };

        private readonly Dictionary<string, PersonProfessionRelationshipData> relationshipsById = new Dictionary<string, PersonProfessionRelationshipData>(StringComparer.Ordinal);
        private readonly List<ProfessionHistoryHookData> historyHooks = new List<ProfessionHistoryHookData>();
        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int Count => relationshipsById.Count;
        public IReadOnlyList<ProfessionHistoryHookData> HistoryHooks => historyHooks.Select(hook => hook.Clone()).ToArray();
        public IReadOnlyList<PersonProfessionSnapshot> Snapshots => relationshipsById.Values
            .OrderBy(record => record.personId, StringComparer.Ordinal)
            .ThenBy(record => record.professionId, StringComparer.Ordinal)
            .ThenBy(record => record.relationshipId, StringComparer.Ordinal)
            .Select(record => new PersonProfessionSnapshot(record))
            .ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, IEnumerable<string> persons = null)
        {
            registry = definitionRegistry;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }

        public ProfessionOperationResult AddRelationship(AddProfessionRelationshipRequest request)
        {
            request ??= new AddProfessionRelationshipRequest();
            long beforeRevision = revision;
            string relationshipId = string.IsNullOrWhiteSpace(request.relationshipId)
                ? $"profession-relationship.{request.personId}.{request.professionId}"
                : request.relationshipId.Trim();

            if (relationshipsById.TryGetValue(relationshipId, out PersonProfessionRelationshipData existing))
            {
                if (!IsEquivalent(existing, request, relationshipId))
                {
                    return ProfessionOperationResult.Failure(ProfessionOperationStatus.DuplicateRelationshipId, $"Profession relationship ID '{relationshipId}' already exists with different data.", beforeRevision);
                }

                return ProfessionOperationResult.Success(new PersonProfessionSnapshot(existing), "Profession relationship already exists.", beforeRevision, beforeRevision, duplicate: true);
            }

            PersonProfessionRelationshipData record = new PersonProfessionRelationshipData
            {
                relationshipId = relationshipId,
                personId = request.personId?.Trim(),
                professionId = request.professionId?.Trim(),
                state = request.recognized ? ProfessionRelationshipState.RecognizedPractitioner : request.state,
                formalPractice = request.formalPractice || request.recognized,
                informalPractice = request.informalPractice,
                selfDeclared = request.selfDeclared,
                recognized = request.recognized,
                primary = request.primary || ShouldBecomeDefaultPrimary(request.personId, request.active),
                active = request.active,
                startWorldTime = request.startWorldTime ?? string.Empty,
                endWorldTime = request.endWorldTime ?? string.Empty,
                specializationIds = PersonProfessionRelationshipData.Clean(request.specializationIds),
                recognizingAuthorityId = request.recognizingAuthorityId ?? string.Empty,
                recognitionReferenceId = request.recognitionReferenceId ?? string.Empty,
                accessPolicyId = request.accessPolicyId ?? string.Empty,
                provenanceId = request.provenanceId ?? string.Empty,
                tags = PersonProfessionRelationshipData.Clean(request.tags),
                disputed = request.state == ProfessionRelationshipState.Disputed,
                revision = 1L
            };

            if (!ValidateNewRecord(record, out string failure, out ProfessionOperationStatus status))
            {
                return ProfessionOperationResult.Failure(status, failure, beforeRevision);
            }

            PersonProfessionRuntimeSaveData rollback = request.preview ? CreateSaveData() : null;
            if (request.preview)
            {
                relationshipsById[record.relationshipId] = record;
                if (record.primary)
                {
                    ClearOtherPrimaries(record.personId, record.relationshipId);
                }

                PersonProfessionSnapshot snapshot = new PersonProfessionSnapshot(record);
                RestoreInternal(rollback);
                return ProfessionOperationResult.Success(snapshot, "Profession relationship previewed.", beforeRevision, beforeRevision, preview: true);
            }

            relationshipsById[record.relationshipId] = record;
            if (record.primary)
            {
                ClearOtherPrimaries(record.personId, record.relationshipId);
            }

            revision++;
            dirty = true;
            AddHook(ProfessionHistoryHookKind.BeganPracticing, record, request.transactionId);
            if (record.recognized)
            {
                AddHook(ProfessionHistoryHookKind.Recognized, record, request.transactionId);
            }

            if (record.primary)
            {
                AddHook(ProfessionHistoryHookKind.MadePrimary, record, request.transactionId);
            }

            foreach (string specializationId in record.specializationIds)
            {
                AddHook(ProfessionHistoryHookKind.SpecializationAdopted, record, request.transactionId, specializationId);
            }

            return ProfessionOperationResult.Success(new PersonProfessionSnapshot(record), "Profession relationship added.", beforeRevision, revision);
        }

        public ProfessionOperationResult Recognize(string relationshipId, string authorityId, string recognitionReferenceId = "", string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                if (string.IsNullOrWhiteSpace(authorityId))
                {
                    return ProfessionOperationResult.Failure(ProfessionOperationStatus.MissingRecognitionAuthority, "Formal recognition requires a recognizing authority.", revision);
                }

                record.recognized = true;
                record.formalPractice = true;
                record.recognizingAuthorityId = authorityId.Trim();
                record.recognitionReferenceId = recognitionReferenceId ?? string.Empty;
                record.state = ProfessionRelationshipState.RecognizedPractitioner;
                return null;
            }, ProfessionHistoryHookKind.Recognized, "Profession relationship recognized.");
        }

        public ProfessionOperationResult RemoveRecognition(string relationshipId, string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                record.recognized = false;
                record.formalPractice = false;
                record.recognizingAuthorityId = string.Empty;
                record.recognitionReferenceId = string.Empty;
                if (record.state == ProfessionRelationshipState.RecognizedPractitioner)
                {
                    record.state = record.active ? ProfessionRelationshipState.Practicing : ProfessionRelationshipState.Inactive;
                }

                return null;
            }, ProfessionHistoryHookKind.RecognitionRevoked, "Profession recognition removed.");
        }

        public ProfessionOperationResult SetPrimary(string relationshipId, string transactionId = "", bool preview = false)
        {
            if (!relationshipsById.TryGetValue(relationshipId ?? string.Empty, out PersonProfessionRelationshipData record))
            {
                return ProfessionOperationResult.Failure(ProfessionOperationStatus.MissingRelationship, $"Profession relationship '{relationshipId}' does not exist.", revision);
            }

            if (!record.active)
            {
                return ProfessionOperationResult.Failure(ProfessionOperationStatus.InvalidState, "Inactive profession relationships cannot become primary.", revision);
            }

            PersonProfessionRuntimeSaveData rollback = CreateSaveData();
            long before = revision;
            ClearOtherPrimaries(record.personId, record.relationshipId);
            record.primary = true;
            record.revision++;
            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, out string failure))
            {
                RestoreInternal(rollback);
                return ProfessionOperationResult.Failure(ProfessionOperationStatus.ValidationFailed, failure, before);
            }

            if (preview)
            {
                PersonProfessionSnapshot previewSnapshot = new PersonProfessionSnapshot(record);
                RestoreInternal(rollback);
                return ProfessionOperationResult.Success(previewSnapshot, "Profession primary change previewed.", before, before, preview: true);
            }

            revision++;
            dirty = true;
            AddHook(ProfessionHistoryHookKind.MadePrimary, record, transactionId);
            return ProfessionOperationResult.Success(new PersonProfessionSnapshot(record), "Primary profession changed.", before, revision);
        }

        public ProfessionOperationResult AddSpecialization(string relationshipId, string specializationId, string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                if (!ValidateSpecialization(record.professionId, specializationId, out string failure))
                {
                    return ProfessionOperationResult.Failure(ProfessionOperationStatus.InvalidSpecialization, failure, revision);
                }

                record.specializationIds = PersonProfessionRelationshipData.Clean(record.specializationIds.Concat(new[] { specializationId }));
                return null;
            }, ProfessionHistoryHookKind.SpecializationAdopted, "Profession specialization added.", specializationId);
        }

        public ProfessionOperationResult RemoveSpecialization(string relationshipId, string specializationId, string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                record.specializationIds = PersonProfessionRelationshipData.Clean(record.specializationIds.Where(id => !string.Equals(id, specializationId, StringComparison.Ordinal)));
                return null;
            }, ProfessionHistoryHookKind.Corrected, "Profession specialization removed.", specializationId);
        }

        public ProfessionOperationResult Activate(string relationshipId, bool active, string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                record.active = active;
                if (!active)
                {
                    record.primary = false;
                    record.state = ProfessionRelationshipState.Inactive;
                }
                else if (record.state == ProfessionRelationshipState.Inactive)
                {
                    record.state = ProfessionRelationshipState.Practicing;
                }

                return null;
            }, active ? ProfessionHistoryHookKind.Corrected : ProfessionHistoryHookKind.Stopped, active ? "Profession relationship activated." : "Profession relationship deactivated.");
        }

        public ProfessionOperationResult Retire(string relationshipId, string endWorldTime, string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                record.active = false;
                record.primary = false;
                record.state = ProfessionRelationshipState.Retired;
                record.endWorldTime = endWorldTime ?? string.Empty;
                return null;
            }, ProfessionHistoryHookKind.Retired, "Profession relationship retired.");
        }

        public ProfessionOperationResult MarkDisputed(string relationshipId, bool disputed, string transactionId = "", bool preview = false)
        {
            return Mutate(relationshipId, transactionId, preview, record =>
            {
                record.disputed = disputed;
                record.state = disputed ? ProfessionRelationshipState.Disputed : (record.active ? ProfessionRelationshipState.Practicing : ProfessionRelationshipState.Inactive);
                return null;
            }, ProfessionHistoryHookKind.Corrected, "Profession dispute state changed.");
        }

        public bool TryGetSnapshot(string relationshipId, out PersonProfessionSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(relationshipId) && relationshipsById.TryGetValue(relationshipId, out PersonProfessionRelationshipData record))
            {
                snapshot = new PersonProfessionSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryByPerson(string personId, bool activeOnly = false)
        {
            return Query(record => string.Equals(record.personId, personId, StringComparison.Ordinal) && (!activeOnly || record.active));
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryByProfession(string professionId, bool activeOnly = false)
        {
            return Query(record => string.Equals(record.professionId, professionId, StringComparison.Ordinal) && (!activeOnly || record.active));
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryByCategory(ProfessionCategory category, DefinitionRegistry definitionRegistry = null, bool activeOnly = false)
        {
            DefinitionRegistry effectiveRegistry = definitionRegistry ?? registry;
            return Query(record =>
            {
                if (activeOnly && !record.active)
                {
                    return false;
                }

                return effectiveRegistry != null
                    && effectiveRegistry.TryGet(record.professionId, out ProfessionDefinition profession)
                    && profession.Category == category;
            });
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryPrimary(string personId)
        {
            return Query(record => string.Equals(record.personId, personId, StringComparison.Ordinal) && record.primary);
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryByAuthority(string authorityId)
        {
            return Query(record => string.Equals(record.recognizingAuthorityId, authorityId, StringComparison.Ordinal));
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryByState(ProfessionRelationshipState state)
        {
            return Query(record => record.state == state);
        }

        public IReadOnlyList<PersonProfessionSnapshot> QueryBySpecialization(string specializationId)
        {
            return Query(record => (record.specializationIds ?? Array.Empty<string>()).Contains(specializationId, StringComparer.Ordinal));
        }

        public PersonProfessionProjection Project(string relationshipId, ProfessionProjectionAudience audience, InformationAccessDecision decision = null)
        {
            if (!TryGetSnapshot(relationshipId, out PersonProfessionSnapshot snapshot))
            {
                return new PersonProfessionProjection(null, audience, decision, redacted: false, denied: true, Array.Empty<string>(), Array.Empty<string>());
            }

            if (audience == ProfessionProjectionAudience.AuthoritativeInternal || audience == ProfessionProjectionAudience.PrivilegedDebug || decision == null)
            {
                return new PersonProfessionProjection(snapshot, audience, decision, redacted: false, denied: false, AllProjectionFields, Array.Empty<string>());
            }

            bool denied = decision.Denied;
            bool redacted = !denied && !decision.FullAccess;
            PersonProfessionSnapshot projected = denied
                ? null
                : redacted
                    ? new PersonProfessionSnapshot(Redacted(snapshot.Data))
                    : snapshot;
            return new PersonProfessionProjection(projected, audience, decision, redacted, denied, decision.AllowedDetails, decision.RedactedDetails.Concat(decision.HiddenDetails).ToArray());
        }

        public PersonProfessionRuntimeSaveData CreateSaveData()
        {
            return new PersonProfessionRuntimeSaveData
            {
                schemaVersion = PersonProfessionRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                relationships = relationshipsById.Values
                    .OrderBy(record => record.personId, StringComparer.Ordinal)
                    .ThenBy(record => record.professionId, StringComparer.Ordinal)
                    .ThenBy(record => record.relationshipId, StringComparer.Ordinal)
                    .Select(record => record.Clone())
                    .ToList()
            };
        }

        public ProfessionOperationResult RestoreFromSaveData(PersonProfessionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoring = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failure))
            {
                return ProfessionOperationResult.Failure(ProfessionOperationStatus.RestoreFailed, failure, before);
            }

            Configure(definitionRegistry, persons);
            RestoreInternal(saveData, clearHistoryHooks: true);
            dirty = !restoring;
            return ProfessionOperationResult.Success(null, "Profession relationships restored.", before, revision);
        }

        public static bool ValidateSaveData(PersonProfessionRuntimeSaveData saveData, DefinitionRegistry registry, IEnumerable<string> knownPersons, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Profession save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > PersonProfessionRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported profession save schema version {saveData.schemaVersion}.";
                return false;
            }

            return ValidateAll(saveData, registry, new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal), out failureReason);
        }

        public void MarkClean()
        {
            dirty = false;
        }

        private ProfessionOperationResult Mutate(string relationshipId, string transactionId, bool preview, Func<PersonProfessionRelationshipData, ProfessionOperationResult> mutation, ProfessionHistoryHookKind hookKind, string successMessage, string specializationId = "")
        {
            relationshipId ??= string.Empty;
            if (!relationshipsById.TryGetValue(relationshipId, out PersonProfessionRelationshipData record))
            {
                return ProfessionOperationResult.Failure(ProfessionOperationStatus.MissingRelationship, $"Profession relationship '{relationshipId}' does not exist.", revision);
            }

            PersonProfessionRuntimeSaveData rollback = CreateSaveData();
            long before = revision;
            ProfessionOperationResult failure = mutation(record);
            if (failure != null)
            {
                RestoreInternal(rollback);
                return failure;
            }

            EnsureDefaultPrimary(record.personId);
            record.revision++;
            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, out string validationFailure))
            {
                RestoreInternal(rollback);
                return ProfessionOperationResult.Failure(ProfessionOperationStatus.ValidationFailed, validationFailure, before);
            }

            PersonProfessionSnapshot snapshot = new PersonProfessionSnapshot(record);
            if (preview)
            {
                RestoreInternal(rollback);
                return ProfessionOperationResult.Success(snapshot, $"{successMessage} Preview only.", before, before, preview: true);
            }

            revision++;
            dirty = true;
            AddHook(hookKind, record, transactionId, specializationId);
            return ProfessionOperationResult.Success(snapshot, successMessage, before, revision);
        }

        private IReadOnlyList<PersonProfessionSnapshot> Query(Func<PersonProfessionRelationshipData, bool> predicate)
        {
            return relationshipsById.Values
                .Where(predicate)
                .OrderBy(record => record.personId, StringComparer.Ordinal)
                .ThenBy(record => record.professionId, StringComparer.Ordinal)
                .ThenBy(record => record.relationshipId, StringComparer.Ordinal)
                .Select(record => new PersonProfessionSnapshot(record))
                .ToArray();
        }

        private bool ValidateNewRecord(PersonProfessionRelationshipData record, out string failure, out ProfessionOperationStatus status)
        {
            status = ProfessionOperationStatus.ValidationFailed;
            PersonProfessionRuntimeSaveData save = CreateSaveData();
            save.relationships.Add(record.Clone());
            save.revision = revision + 1L;
            if (ValidateAll(save, registry, knownPersonIds, out failure))
            {
                return true;
            }

            if (failure.Contains("unknown Person", StringComparison.Ordinal))
            {
                status = ProfessionOperationStatus.MissingPerson;
            }
            else if (failure.Contains("missing Profession", StringComparison.Ordinal))
            {
                status = ProfessionOperationStatus.MissingDefinition;
            }
            else if (failure.Contains("duplicate active", StringComparison.Ordinal))
            {
                status = ProfessionOperationStatus.DuplicateActiveRelationship;
            }
            else if (failure.Contains("recognizing authority", StringComparison.Ordinal))
            {
                status = ProfessionOperationStatus.MissingRecognitionAuthority;
            }
            else if (failure.Contains("specialization", StringComparison.Ordinal))
            {
                status = ProfessionOperationStatus.InvalidSpecialization;
            }

            return false;
        }

        private static bool ValidateAll(PersonProfessionRuntimeSaveData saveData, DefinitionRegistry registry, HashSet<string> knownPersons, out string failure)
        {
            failure = string.Empty;
            List<PersonProfessionRelationshipData> records = saveData.relationships ?? new List<PersonProfessionRelationshipData>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> activeByPersonProfession = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> primaryByPerson = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (PersonProfessionRelationshipData record in records)
            {
                if (record == null)
                {
                    failure = "Profession save contains a null relationship.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.relationshipId) || !ids.Add(record.relationshipId))
                {
                    failure = $"Profession relationship has duplicate or empty relationship ID '{record.relationshipId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.personId) || (knownPersons != null && knownPersons.Count > 0 && !knownPersons.Contains(record.personId)))
                {
                    failure = $"Profession relationship '{record.relationshipId}' references unknown Person '{record.personId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.professionId)
                    || registry == null
                    || !registry.TryGet(record.professionId, out ProfessionDefinition profession))
                {
                    failure = $"Profession relationship '{record.relationshipId}' references missing Profession '{record.professionId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(ProfessionRelationshipState), record.state))
                {
                    failure = $"Profession relationship '{record.relationshipId}' has invalid state '{record.state}'.";
                    return false;
                }

                if (record.recognized && string.IsNullOrWhiteSpace(record.recognizingAuthorityId))
                {
                    failure = $"Profession relationship '{record.relationshipId}' is recognized but has no recognizing authority.";
                    return false;
                }

                if (record.recognized && !record.formalPractice)
                {
                    failure = $"Profession relationship '{record.relationshipId}' is recognized but is not formal practice.";
                    return false;
                }

                if (record.recognized && !profession.FormalRecognitionPossible)
                {
                    failure = $"Profession relationship '{record.relationshipId}' recognizes Profession '{record.professionId}', but that profession cannot be formally recognized.";
                    return false;
                }

                if (record.recognized
                    && profession.RecognizingAuthorityIds.Count > 0
                    && !profession.RecognizingAuthorityIds.Contains(record.recognizingAuthorityId, StringComparer.Ordinal))
                {
                    failure = $"Profession relationship '{record.relationshipId}' is recognized by authority '{record.recognizingAuthorityId}', which is not valid for Profession '{record.professionId}'.";
                    return false;
                }

                if (record.selfDeclared && !profession.SelfDeclarationAllowed)
                {
                    failure = $"Profession relationship '{record.relationshipId}' is self-declared, but Profession '{record.professionId}' does not allow self declaration.";
                    return false;
                }

                if ((record.state == ProfessionRelationshipState.Secret || (record.tags ?? Array.Empty<string>()).Contains("profession.secret", StringComparer.Ordinal)) && !profession.SecretAllowed)
                {
                    failure = $"Profession relationship '{record.relationshipId}' is secret, but Profession '{record.professionId}' does not allow secret practice.";
                    return false;
                }

                if (record.state == ProfessionRelationshipState.Retired && record.active)
                {
                    failure = $"Profession relationship '{record.relationshipId}' is retired but still active.";
                    return false;
                }

                if (record.state == ProfessionRelationshipState.Revoked && record.recognized)
                {
                    failure = $"Profession relationship '{record.relationshipId}' is revoked but still recognized.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(record.endWorldTime) && string.CompareOrdinal(record.endWorldTime, record.startWorldTime ?? string.Empty) < 0)
                {
                    failure = $"Profession relationship '{record.relationshipId}' has end time before start time.";
                    return false;
                }

                if (!ValidateSpecializationList(record, profession, registry, out failure))
                {
                    return false;
                }

                if (record.active)
                {
                    string activeKey = $"{record.personId}|{record.professionId}";
                    activeByPersonProfession.TryGetValue(activeKey, out int activeCount);
                    activeByPersonProfession[activeKey] = activeCount + 1;
                    if (activeByPersonProfession[activeKey] > 1)
                    {
                        failure = $"Profession runtime contains duplicate active relationship for Person '{record.personId}' and Profession '{record.professionId}'.";
                        return false;
                    }
                }

                if (record.primary)
                {
                    if (!record.active)
                    {
                        failure = $"Profession relationship '{record.relationshipId}' is primary but inactive.";
                        return false;
                    }

                    primaryByPerson.TryGetValue(record.personId, out int primaryCount);
                    primaryByPerson[record.personId] = primaryCount + 1;
                    if (primaryByPerson[record.personId] > 1)
                    {
                        failure = $"Profession runtime contains multiple primary professions for Person '{record.personId}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ValidateSpecialization(string professionId, string specializationId, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(specializationId)
                || registry == null
                || !registry.TryGet(specializationId, out ProfessionSpecializationDefinition specialization))
            {
                failure = $"Profession specialization '{specializationId}' is missing.";
                return false;
            }

            if (!string.Equals(specialization.ParentProfessionId, professionId, StringComparison.Ordinal))
            {
                failure = $"Profession specialization '{specializationId}' belongs to '{specialization.ParentProfessionId}', not '{professionId}'.";
                return false;
            }

            if (registry.TryGet(professionId, out ProfessionDefinition profession)
                && profession.AllowedSpecializationIds.Count > 0
                && !profession.AllowsSpecialization(specializationId))
            {
                failure = $"Profession '{professionId}' does not allow specialization '{specializationId}'.";
                return false;
            }

            return true;
        }

        private static bool ValidateSpecializationList(PersonProfessionRelationshipData record, ProfessionDefinition profession, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string specializationId in record.specializationIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(specializationId) || !seen.Add(specializationId))
                {
                    failure = $"Profession relationship '{record.relationshipId}' has duplicate or empty specialization ID '{specializationId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(specializationId, out ProfessionSpecializationDefinition specialization))
                {
                    failure = $"Profession relationship '{record.relationshipId}' references missing specialization '{specializationId}'.";
                    return false;
                }

                if (!string.Equals(specialization.ParentProfessionId, record.professionId, StringComparison.Ordinal))
                {
                    failure = $"Profession relationship '{record.relationshipId}' references specialization '{specializationId}' for parent '{specialization.ParentProfessionId}', expected '{record.professionId}'.";
                    return false;
                }

                if (profession.AllowedSpecializationIds.Count > 0 && !profession.AllowsSpecialization(specializationId))
                {
                    failure = $"Profession relationship '{record.relationshipId}' uses specialization '{specializationId}' not allowed by Profession '{profession.Id}'.";
                    return false;
                }
            }

            return true;
        }

        private void ClearOtherPrimaries(string personId, string exceptRelationshipId)
        {
            foreach (PersonProfessionRelationshipData other in relationshipsById.Values)
            {
                if (string.Equals(other.personId, personId, StringComparison.Ordinal)
                    && !string.Equals(other.relationshipId, exceptRelationshipId, StringComparison.Ordinal)
                    && other.primary)
                {
                    other.primary = false;
                    other.revision++;
                }
            }
        }

        private void RestoreInternal(PersonProfessionRuntimeSaveData saveData, bool clearHistoryHooks = false)
        {
            relationshipsById.Clear();
            foreach (PersonProfessionRelationshipData relationship in saveData?.relationships ?? new List<PersonProfessionRelationshipData>())
            {
                if (relationship != null)
                {
                    relationshipsById[relationship.relationshipId] = relationship.Clone();
                }
            }

            revision = saveData?.revision ?? 0L;
            if (clearHistoryHooks)
            {
                historyHooks.Clear();
            }
        }

        private bool ShouldBecomeDefaultPrimary(string personId, bool active)
        {
            return active
                && !string.IsNullOrWhiteSpace(personId)
                && !relationshipsById.Values.Any(record => string.Equals(record.personId, personId, StringComparison.Ordinal) && record.active && record.primary);
        }

        private void EnsureDefaultPrimary(string personId)
        {
            if (string.IsNullOrWhiteSpace(personId)
                || relationshipsById.Values.Any(record => string.Equals(record.personId, personId, StringComparison.Ordinal) && record.active && record.primary))
            {
                return;
            }

            PersonProfessionRelationshipData replacement = relationshipsById.Values
                .Where(record => string.Equals(record.personId, personId, StringComparison.Ordinal) && record.active)
                .OrderBy(record => record.professionId, StringComparer.Ordinal)
                .ThenBy(record => record.relationshipId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (replacement != null)
            {
                replacement.primary = true;
                replacement.revision++;
            }
        }

        private static bool IsEquivalent(PersonProfessionRelationshipData existing, AddProfessionRelationshipRequest request, string relationshipId)
        {
            ProfessionRelationshipState requestedState = request.recognized ? ProfessionRelationshipState.RecognizedPractitioner : request.state;
            bool requestedFormalPractice = request.formalPractice || request.recognized;

            return existing != null
                && string.Equals(existing.relationshipId, relationshipId, StringComparison.Ordinal)
                && string.Equals(existing.personId, request.personId?.Trim() ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.professionId, request.professionId?.Trim() ?? string.Empty, StringComparison.Ordinal)
                && existing.state == requestedState
                && existing.formalPractice == requestedFormalPractice
                && existing.informalPractice == request.informalPractice
                && existing.selfDeclared == request.selfDeclared
                && existing.recognized == request.recognized
                && existing.active == request.active
                && string.Equals(existing.startWorldTime ?? string.Empty, request.startWorldTime ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.endWorldTime ?? string.Empty, request.endWorldTime ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.recognizingAuthorityId ?? string.Empty, request.recognizingAuthorityId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.recognitionReferenceId ?? string.Empty, request.recognitionReferenceId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.accessPolicyId ?? string.Empty, request.accessPolicyId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.provenanceId ?? string.Empty, request.provenanceId ?? string.Empty, StringComparison.Ordinal)
                && PersonProfessionRelationshipData.Clean(existing.specializationIds).SequenceEqual(PersonProfessionRelationshipData.Clean(request.specializationIds), StringComparer.Ordinal)
                && PersonProfessionRelationshipData.Clean(existing.tags).SequenceEqual(PersonProfessionRelationshipData.Clean(request.tags), StringComparer.Ordinal);
        }

        private void AddHook(ProfessionHistoryHookKind kind, PersonProfessionRelationshipData record, string transactionId, string specializationId = "")
        {
            if (record == null)
            {
                return;
            }

            historyHooks.Add(new ProfessionHistoryHookData
            {
                kind = kind,
                relationshipId = record.relationshipId,
                personId = record.personId,
                professionId = record.professionId,
                specializationId = specializationId ?? string.Empty,
                authorityId = record.recognizingAuthorityId,
                worldTime = string.IsNullOrWhiteSpace(record.endWorldTime) ? record.startWorldTime : record.endWorldTime,
                transactionId = transactionId ?? string.Empty
            });
        }

        private static PersonProfessionRelationshipData Redacted(PersonProfessionRelationshipData source)
        {
            PersonProfessionRelationshipData data = source.Clone();
            data.relationshipId = string.Empty;
            data.personId = string.Empty;
            data.specializationIds = Array.Empty<string>();
            data.recognizingAuthorityId = string.Empty;
            data.recognitionReferenceId = string.Empty;
            data.accessPolicyId = string.Empty;
            data.provenanceId = string.Empty;
            data.tags = data.tags.Where(tag => !tag.Contains("secret", StringComparison.OrdinalIgnoreCase)).ToArray();
            return data;
        }
    }
}
