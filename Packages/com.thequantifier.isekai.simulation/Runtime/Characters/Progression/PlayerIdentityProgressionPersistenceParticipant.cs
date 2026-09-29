using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Progression
{
    public sealed class PlayerIdentityProgressionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.identity-progression";
        public const int CurrentParticipantSchemaVersion = 5;

        private readonly PlayerIdentityProgression progression;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;
        private readonly string accountId;

        public PlayerIdentityProgressionPersistenceParticipant(
            PlayerIdentityProgression progression,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId,
            string accountId = PersistenceService.LocalAccountId)
        {
            this.progression = progression;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
            this.accountId = string.IsNullOrWhiteSpace(accountId) ? PersistenceService.LocalAccountId : accountId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 0;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (progression == null)
            {
                return PersistenceParticipantSaveResult.Failure("Player identity/progression component is missing.");
            }

            if (!progression.ValidateIdentity(out string failureReason))
            {
                return PersistenceParticipantSaveResult.Failure(failureReason);
            }

            PlayerIdentityProgressionSaveData saveData = progression.CreateSaveData();
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Identity/progression snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported identity/progression participant schema version {payloadSchemaVersion}. Development saves from earlier Step 5 schemas are intentionally rejected.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Identity/progression payload is empty.");
            }

            PlayerIdentityProgressionSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerIdentityProgressionSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Identity/progression payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Identity/progression payload did not parse.");
            }

            if (saveData.schemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported identity/progression payload schema version {saveData.schemaVersion}. Development saves from earlier Step 5 schemas are intentionally rejected.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Definition registry is not available for identity/progression restore.");
            }

            if (!ValidateSaveData(saveData, registry, out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (progression == null)
            {
                return PersistenceParticipantCommitResult.Failure("Player identity/progression component is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared || prepared.SaveData == null)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared identity/progression payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for identity/progression commit.");
            }

            PlayerIdentityProgressionSaveData rollback = progression.CreateSaveData();
            ProgressionOperationResult result = progression.RestoreFromSaveData(prepared.SaveData, registry, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Player identity/progression restored.");
            }

            ProgressionOperationResult rollbackResult = progression.RestoreFromSaveData(rollback, registry, restoring: true);
            return PersistenceParticipantCommitResult.Failure(
                rollbackResult.Succeeded
                    ? $"Identity/progression commit failed after preparation; rollback succeeded: {result.Message}"
                    : $"Identity/progression commit failed after preparation and rollback failed: {result.Message} / {rollbackResult.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateSaveData(PlayerIdentityProgressionSaveData saveData, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;

            if (!string.Equals(saveData.accountId, accountId, StringComparison.Ordinal))
            {
                failureReason = $"Saved account ID '{saveData.accountId}' does not match current account '{accountId}'.";
                return false;
            }

            if (!string.Equals(saveData.playerId, ownerId, StringComparison.Ordinal))
            {
                failureReason = $"Saved player ID '{saveData.playerId}' does not match participant owner '{ownerId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveData.personId))
            {
                failureReason = "Saved person ID is missing.";
                return false;
            }

            if (string.Equals(saveData.personId, saveData.accountId, StringComparison.Ordinal)
                || string.Equals(saveData.personId, saveData.playerId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(saveData.currentWorldEntityId) && string.Equals(saveData.personId, saveData.currentWorldEntityId, StringComparison.Ordinal)))
            {
                failureReason = "Saved account, player, person, and world-entity IDs must remain distinct.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.currentWorldEntityId) && !saveData.currentWorldEntityId.StartsWith("entity.", StringComparison.Ordinal))
            {
                failureReason = $"Saved world-entity ID '{saveData.currentWorldEntityId}' is malformed.";
                return false;
            }

            if (!IsValidUtc(saveData.accountCreatedAtUtc))
            {
                failureReason = "Saved account creation timestamp is missing or malformed.";
                return false;
            }

            if (saveData.cumulativeActivePlaytimeSeconds < 0d || double.IsNaN(saveData.cumulativeActivePlaytimeSeconds) || double.IsInfinity(saveData.cumulativeActivePlaytimeSeconds))
            {
                failureReason = "Saved active playtime must be finite and non-negative.";
                return false;
            }

            return ValidateOrigin(saveData.origin, registry, out failureReason)
                && ValidateBirthGift(saveData.birthGift, registry, out failureReason)
                && ValidatePermanentGrants(saveData.permanentAttributeGrants, registry, out failureReason)
                && ValidateRoles(saveData.roles, registry, out failureReason)
                && ValidateSocialStatuses(saveData.socialStatuses, registry, out failureReason)
                && ValidateTitles(saveData.titles, registry, out failureReason)
                && ValidateActivityRecords(saveData.activityRecords, out failureReason)
                && ValidateParticipationRecords(saveData.participationRecords, out failureReason);
        }

        private static bool ValidateOrigin(RuntimeOriginAssignmentRecord origin, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (origin == null || !origin.assigned)
            {
                failureReason = "Saved identity/progression data does not contain an assigned origin.";
                return false;
            }

            if (!registry.TryGet(origin.originFamilyId, out OriginFamilyDefinition family))
            {
                failureReason = $"Origin family definition '{origin.originFamilyId}' was not found.";
                return false;
            }

            if (!registry.TryGet(origin.originId, out OriginDefinition originDefinition))
            {
                failureReason = $"Origin definition '{origin.originId}' was not found.";
                return false;
            }

            if (originDefinition.Family != null && !string.Equals(originDefinition.Family.Id, family.Id, StringComparison.Ordinal))
            {
                failureReason = $"Saved origin '{origin.originId}' does not belong to saved family '{origin.originFamilyId}'.";
                return false;
            }

            if (origin.startingGoldAmount < 0L)
            {
                failureReason = "Saved starting Gold amount cannot be negative.";
                return false;
            }

            if (!IsValidUtc(origin.assignedAtUtc))
            {
                failureReason = "Saved origin assignment timestamp is malformed.";
                return false;
            }

            return true;
        }

        private static bool ValidateBirthGift(RuntimeBirthGiftRecord gift, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (gift == null || string.IsNullOrWhiteSpace(gift.giftDefinitionId))
            {
                failureReason = "Saved identity/progression data does not contain an assigned birth gift.";
                return false;
            }

            if (!registry.TryGet(gift.giftDefinitionId, out BirthGiftDefinition definition))
            {
                failureReason = $"Birth gift definition '{gift.giftDefinitionId}' was not found.";
                return false;
            }

            if (definition.GiftType != gift.giftType)
            {
                failureReason = $"Saved birth gift '{gift.giftDefinitionId}' has type '{gift.giftType}' but the definition is '{definition.GiftType}'.";
                return false;
            }

            if (gift.requiredActivePlaytimeSeconds < 0f || gift.currentProgressSeconds < 0f)
            {
                failureReason = "Saved birth gift playtime progress must be non-negative.";
                return false;
            }

            if (!IsValidUtc(gift.assignedAtUtc))
            {
                failureReason = "Saved birth gift assignment timestamp is malformed.";
                return false;
            }

            if (gift.state == BirthGiftRuntimeState.Awakened && !IsValidUtc(gift.awakenedAtUtc))
            {
                failureReason = "Saved awakened birth gift timestamp is malformed.";
                return false;
            }

            return true;
        }

        private static bool ValidatePermanentGrants(IReadOnlyList<RuntimePermanentAttributeGrantRecord> grants, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<RuntimePermanentAttributeGrantRecord> entries = grants ?? Array.Empty<RuntimePermanentAttributeGrantRecord>();
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimePermanentAttributeGrantRecord grant = entries[i];
                if (grant == null || string.IsNullOrWhiteSpace(grant.sourceId) || !ids.Add(grant.sourceId))
                {
                    failureReason = "Saved permanent attribute grant IDs must be present and unique.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(grant.attributeId) || registry == null || !registry.TryGet(grant.attributeId, out AttributeDefinition _))
                {
                    failureReason = $"Saved permanent attribute grant '{grant.sourceId}' references unknown attribute '{grant.attributeId}'.";
                    return false;
                }

                if (grant.amount <= 0f || float.IsNaN(grant.amount) || float.IsInfinity(grant.amount))
                {
                    failureReason = $"Saved permanent attribute grant '{grant.sourceId}' has an invalid amount.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateRoles(IReadOnlyList<RuntimeRoleRecord> roles, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<RuntimeRoleRecord> entries = roles ?? Array.Empty<RuntimeRoleRecord>();
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeRoleRecord role = entries[i];
                if (role == null || string.IsNullOrWhiteSpace(role.recordId) || !recordIds.Add(role.recordId))
                {
                    failureReason = "Saved role record IDs must be present and unique.";
                    return false;
                }

                if (!registry.TryGet(role.roleDefinitionId, out RoleDefinition _))
                {
                    failureReason = $"Role definition '{role.roleDefinitionId}' was not found.";
                    return false;
                }

                if (role.lifecycleState == RoleLifecycleState.Active && !activeDefinitionIds.Add(role.roleDefinitionId))
                {
                    failureReason = $"Role definition '{role.roleDefinitionId}' appears more than once as active.";
                    return false;
                }

                if (!IsValidUtc(role.startedAtUtc))
                {
                    failureReason = $"Role record '{role.recordId}' has a malformed start timestamp.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateSocialStatuses(IReadOnlyList<RuntimeSocialStatusRecord> statuses, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeKeys = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<RuntimeSocialStatusRecord> entries = statuses ?? Array.Empty<RuntimeSocialStatusRecord>();
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeSocialStatusRecord status = entries[i];
                if (status == null || string.IsNullOrWhiteSpace(status.recordId) || !recordIds.Add(status.recordId))
                {
                    failureReason = "Saved social status record IDs must be present and unique.";
                    return false;
                }

                if (!registry.TryGet(status.socialStatusDefinitionId, out SocialStatusDefinition definition))
                {
                    failureReason = $"Social status definition '{status.socialStatusDefinitionId}' was not found.";
                    return false;
                }

                if (status.lifecycleState == SocialStatusLifecycleState.Active)
                {
                    string key = $"{status.socialStatusDefinitionId}|{status.contextKind}|{status.contextTargetId}";
                    if (!definition.AllowMultipleContexts && !activeKeys.Add(key))
                    {
                        failureReason = $"Social status '{status.socialStatusDefinitionId}' is duplicated in the same active context.";
                        return false;
                    }
                }

                if (!IsValidUtc(status.startedAtUtc))
                {
                    failureReason = $"Social status record '{status.recordId}' has a malformed start timestamp.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateTitles(IReadOnlyList<RuntimeTitleRecord> titles, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> titleIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<RuntimeTitleRecord> entries = titles ?? Array.Empty<RuntimeTitleRecord>();
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeTitleRecord title = entries[i];
                if (title == null || string.IsNullOrWhiteSpace(title.titleDefinitionId) || !titleIds.Add(title.titleDefinitionId))
                {
                    failureReason = "Saved title definition IDs must be present and unique.";
                    return false;
                }

                if (!registry.TryGet(title.titleDefinitionId, out TitleDefinition _))
                {
                    failureReason = $"Title definition '{title.titleDefinitionId}' was not found.";
                    return false;
                }

                if (!IsValidUtc(title.assignedAtUtc))
                {
                    failureReason = $"Title record '{title.titleDefinitionId}' has a malformed grant timestamp.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateStringList(IReadOnlyList<string> values, string label, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<string> entries = values ?? Array.Empty<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(entries[i]) || !ids.Add(entries[i]))
                {
                    failureReason = $"Saved {label} IDs must be present and unique.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateActivityRecords(IReadOnlyList<ActivityOutcomeRecord> records, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<ActivityOutcomeRecord> entries = records ?? Array.Empty<ActivityOutcomeRecord>();
            for (int i = 0; i < entries.Count; i++)
            {
                ActivityOutcomeRecord record = entries[i];
                if (record == null || string.IsNullOrWhiteSpace(record.activityId) || !ids.Add(record.activityId))
                {
                    failureReason = "Saved activity IDs must be present and unique.";
                    return false;
                }

                if (float.IsNaN(record.difficulty) || float.IsInfinity(record.difficulty) || record.difficulty < 0f)
                {
                    failureReason = $"Activity record '{record.activityId}' has invalid difficulty.";
                    return false;
                }

                if (!IsValidUtc(record.completedAtUtc))
                {
                    failureReason = $"Activity record '{record.activityId}' has a malformed completion timestamp.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateParticipationRecords(IReadOnlyList<ParticipationRecord> records, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<ParticipationRecord> entries = records ?? Array.Empty<ParticipationRecord>();
            for (int i = 0; i < entries.Count; i++)
            {
                ParticipationRecord record = entries[i];
                if (record == null || string.IsNullOrWhiteSpace(record.participationId) || !ids.Add(record.participationId))
                {
                    failureReason = "Saved participation IDs must be present and unique.";
                    return false;
                }

                if (float.IsNaN(record.contribution) || float.IsInfinity(record.contribution) || record.contribution < 0f)
                {
                    failureReason = $"Participation record '{record.participationId}' has invalid contribution.";
                    return false;
                }

                if (!IsValidUtc(record.recordedAtUtc))
                {
                    failureReason = $"Participation record '{record.participationId}' has a malformed timestamp.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidUtc(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out _);
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerIdentityProgressionSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerIdentityProgressionSaveData SaveData { get; }
        }
    }
}
