using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Progression
{
    public sealed class PlayerIdentityProgression : MonoBehaviour
    {
        public const string DefaultOriginAssignmentSource = "local-alpha-origin-generation";
        public const float DefaultDelayedBirthGiftSeconds = 300f;

        [SerializeField] private string accountId = PersistenceService.LocalAccountId;
        [SerializeField] private string playerId = PersistenceService.LocalPlayerId;
        [SerializeField] private string personId;
        [SerializeField] private ActorStats actorStats;
        [SerializeField] private CharacterAttributes characterAttributes;
        [SerializeField] private CalculatedStatCollection calculatedStats;
        [SerializeField] private CharacterSkillCollection skillCollection;
        [SerializeField] private CharacterAbilityCollection abilityCollection;
        [SerializeField] private WorldEntityIdentity worldEntityIdentity;
        [SerializeField] private PlayTimeTracker playTimeTracker;
        [SerializeField] private OverallLevelConfiguration overallLevelConfiguration;
        [SerializeField] private RuntimeOriginAssignmentRecord origin = new RuntimeOriginAssignmentRecord();
        [SerializeField] private RuntimeBirthGiftRecord birthGift = new RuntimeBirthGiftRecord();
        [SerializeField] private List<RuntimePermanentAttributeGrantRecord> permanentAttributeGrants = new List<RuntimePermanentAttributeGrantRecord>();
        [SerializeField] private List<RuntimeRoleRecord> roles = new List<RuntimeRoleRecord>();
        [SerializeField] private List<RuntimeSocialStatusRecord> socialStatuses = new List<RuntimeSocialStatusRecord>();
        [SerializeField] private List<RuntimeTitleRecord> titles = new List<RuntimeTitleRecord>();
        [SerializeField] private List<ActivityOutcomeRecord> activityRecords = new List<ActivityOutcomeRecord>();
        [SerializeField] private List<ParticipationRecord> participationRecords = new List<ParticipationRecord>();

        private readonly HashSet<StatModifierSource> activeStatSources = new HashSet<StatModifierSource>();
        private bool notificationsSuppressed;
        private double lastPlaytimeSample;
        private DefinitionRegistry definitionRegistry;

        public string AccountId => accountId;
        public string PlayerId => playerId;
        public string PersonId => personId;
        public string CurrentWorldEntityId => worldEntityIdentity == null ? string.Empty : worldEntityIdentity.EntityId;
        public string AccountCreatedAtUtc { get; private set; }
        public RuntimeOriginAssignmentRecord Origin => origin;
        public RuntimeBirthGiftRecord BirthGift => birthGift;
        public IReadOnlyList<RuntimeRoleRecord> Roles => roles;
        public IReadOnlyList<RuntimeSocialStatusRecord> SocialStatuses => socialStatuses;
        public IReadOnlyList<RuntimeTitleRecord> Titles => titles;
        public IReadOnlyList<ActivityOutcomeRecord> ActivityRecords => activityRecords;
        public IReadOnlyList<ParticipationRecord> ParticipationRecords => participationRecords;
        public double CumulativeActivePlaytimeSeconds => playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds;

        public event Action<PlayerIdentityProgression, bool> ProgressionChanged;
        public event Action<PlayerIdentityProgression, RuntimeOriginAssignmentRecord, bool> OriginAssigned;
        public event Action<PlayerIdentityProgression, RuntimeBirthGiftRecord, bool> BirthGiftAssigned;
        public event Action<PlayerIdentityProgression, RuntimeBirthGiftRecord, bool> BirthGiftAwakened;
        public event Action<PlayerIdentityProgression, RuntimeRoleRecord, bool> RoleAdded;
        public event Action<PlayerIdentityProgression, RuntimeRoleRecord, bool> RoleStateChanged;
        public event Action<PlayerIdentityProgression, RuntimeRoleRecord, bool> RoleAbandoned;
        public event Action<PlayerIdentityProgression, RoleConflictResult> RoleConflictDetected;
        public event Action<PlayerIdentityProgression, RuntimeRoleRecord, bool> RoleReplacementCompleted;
        public event Action<PlayerIdentityProgression, RuntimeSocialStatusRecord, bool> SocialStatusAdded;
        public event Action<PlayerIdentityProgression, RuntimeSocialStatusRecord, bool> SocialStatusResolved;
        public event Action<PlayerIdentityProgression, OverallLevelBreakdown, bool> OverallLevelChanged;
        public event Action<PlayerIdentityProgression, ActivityOutcomeRecord, bool> ActivityOutcomeRecorded;
        public event Action<PlayerIdentityProgression, ParticipationRecord, bool> ParticipationRecorded;

        private void Awake()
        {
            EnsureRuntimeReferences();
            EnsureIdentityInitialized();
            AccountCreatedAtUtc = string.IsNullOrWhiteSpace(AccountCreatedAtUtc) ? DateTime.UtcNow.ToString("O") : AccountCreatedAtUtc;
            lastPlaytimeSample = CumulativeActivePlaytimeSeconds;
        }

        private void Update()
        {
            TickDelayedBirthGift();
        }

        public void ConfigureIdentity(string account, string player, string person = null)
        {
            accountId = string.IsNullOrWhiteSpace(account) ? PersistenceService.LocalAccountId : account;
            playerId = string.IsNullOrWhiteSpace(player) ? PersistenceService.LocalPlayerId : player;
            if (!string.IsNullOrWhiteSpace(person))
            {
                personId = person;
            }

            EnsureIdentityInitialized();
            RaiseProgressionChanged(false);
        }

        public void ConfigureRuntimeReferences(ActorStats stats, WorldEntityIdentity identity, PlayTimeTracker tracker, OverallLevelConfiguration levelConfiguration)
        {
            actorStats = stats == null ? actorStats : stats;
            characterAttributes = actorStats == null ? characterAttributes : actorStats.CharacterAttributes ?? actorStats.GetComponent<CharacterAttributes>();
            calculatedStats = actorStats == null ? calculatedStats : actorStats.CalculatedStats ?? actorStats.GetComponent<CalculatedStatCollection>();
            skillCollection = actorStats == null ? skillCollection : actorStats.GetComponent<CharacterSkillCollection>();
            abilityCollection = actorStats == null ? abilityCollection : actorStats.GetComponent<CharacterAbilityCollection>();
            worldEntityIdentity = identity == null ? worldEntityIdentity : identity;
            playTimeTracker = tracker == null ? playTimeTracker : tracker;
            overallLevelConfiguration = levelConfiguration == null ? overallLevelConfiguration : levelConfiguration;
            lastPlaytimeSample = CumulativeActivePlaytimeSeconds;
            RebuildActiveEffects(false);
        }

        public bool ValidateIdentity(out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(accountId))
            {
                failureReason = "Account ID is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                failureReason = "Player ID is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(personId))
            {
                failureReason = "Person ID is missing.";
                return false;
            }

            if (string.Equals(personId, accountId, StringComparison.Ordinal)
                || string.Equals(personId, playerId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(CurrentWorldEntityId) && string.Equals(personId, CurrentWorldEntityId, StringComparison.Ordinal)))
            {
                failureReason = "Account, player, person, and world-entity IDs must remain distinct.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CurrentWorldEntityId) && !CurrentWorldEntityId.StartsWith("entity.", StringComparison.Ordinal))
            {
                failureReason = "Current world-entity ID is malformed.";
                return false;
            }

            return true;
        }

        public ProgressionOperationResult AssignRandomOrigin(DefinitionRegistry registry, int seed, bool restoring = false)
        {
            if (origin != null && origin.assigned)
            {
                return ProgressionOperationResult.Failure("OriginAlreadyAssigned", "Origin is already assigned.");
            }

            CharacterOriginGenerator generator = new CharacterOriginGenerator(registry, new SeededProgressionRandomSource(seed));
            CharacterOriginGenerationResult result = generator.Generate();
            if (!result.Succeeded)
            {
                return ProgressionOperationResult.Failure("OriginGenerationFailed", result.Message);
            }

            return AssignGeneratedOrigin(result, seed, DefaultOriginAssignmentSource, restoring);
        }

        public ProgressionOperationResult EnsureOriginAndBirthGiftAssigned(DefinitionRegistry registry, bool restoring = false)
        {
            ConfigureDefinitions(registry);
            if (restoring)
            {
                return ProgressionOperationResult.Success("Origin generation is deferred while restoring a character.");
            }

            if (origin != null && origin.assigned)
            {
                return string.IsNullOrWhiteSpace(birthGift?.giftDefinitionId)
                    ? ProgressionOperationResult.Failure("MissingBirthGift", "The assigned origin does not have a birth gift.")
                    : ProgressionOperationResult.Success("Origin and birth gift are already assigned.");
            }

            if (registry == null)
            {
                return ProgressionOperationResult.Failure("MissingDefinitionRegistry", "A definition registry is required to generate an origin and birth gift.");
            }

            return AssignRandomOrigin(registry, CreateAutomaticOriginSeed(), restoring: false);
        }

        public ProgressionOperationResult AssignGeneratedOrigin(CharacterOriginGenerationResult generated, int seed, string source, bool restoring = false)
        {
            if (generated == null || !generated.Succeeded || generated.Family == null || generated.Origin == null)
            {
                return ProgressionOperationResult.Failure("InvalidOriginGeneration", "Generated origin result is invalid.");
            }

            if (origin != null && origin.assigned)
            {
                return ProgressionOperationResult.Failure("OriginAlreadyAssigned", "Origin is already assigned.");
            }

            EnsureIdentityInitialized();
            string now = DateTime.UtcNow.ToString("O");
            double playtime = CumulativeActivePlaytimeSeconds;

            origin = new RuntimeOriginAssignmentRecord
            {
                assigned = true,
                originFamilyId = generated.Family.Id,
                originId = generated.Origin.Id,
                randomSeed = seed,
                startingGoldAmount = generated.StartingGold,
                assignedAtUtc = now,
                assignedAtPlaytimeSeconds = playtime,
                assignmentSource = string.IsNullOrWhiteSpace(source) ? DefaultOriginAssignmentSource : source
            };

            ApplyOriginStatGrants(generated.Origin, restoring);
            AssignStartingRoleStatusAndTitle(generated.Origin, restoring);
            AssignBirthGift(generated.BirthGift, generated.Family.Id, generated.Origin.Id, origin.assignmentSource, restoring);
            RebuildActiveEffects(restoring);
            RaiseOriginAssigned(restoring);
            return ProgressionOperationResult.Success($"Assigned origin {generated.Origin.DisplayName}.");
        }

        public ProgressionOperationResult ResetIdentityProgressionForDevelopment()
        {
            ClearActiveEffectSources();
            personId = CreatePersonId();
            AccountCreatedAtUtc = DateTime.UtcNow.ToString("O");
            origin = new RuntimeOriginAssignmentRecord();
            birthGift = new RuntimeBirthGiftRecord();
            permanentAttributeGrants.Clear();
            roles.Clear();
            socialStatuses.Clear();
            titles.Clear();
            activityRecords.Clear();
            participationRecords.Clear();
            RaiseProgressionChanged(false);
            return ProgressionOperationResult.Success("Identity/progression state reset for development.");
        }

        public RoleConflictResult DetectRoleConflicts(RoleDefinition role)
        {
            List<RuntimeRoleRecord> blockers = new List<RuntimeRoleRecord>();
            if (role == null)
            {
                return new RoleConflictResult(role, blockers);
            }

            HashSet<string> newIncompatibleIds = new HashSet<string>(role.IncompatibleRoles.Where(value => value != null).Select(value => value.Id), StringComparer.Ordinal);
            HashSet<string> newGroups = new HashSet<string>(role.IncompatibilityGroups.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            for (int i = 0; i < roles.Count; i++)
            {
                RuntimeRoleRecord record = roles[i];
                if (record.lifecycleState != RoleLifecycleState.Active)
                {
                    continue;
                }

                RoleDefinition activeDefinition = ResolveRoleDefinition(record.roleDefinitionId);
                if (activeDefinition == null)
                {
                    continue;
                }

                bool explicitConflict = newIncompatibleIds.Contains(activeDefinition.Id)
                    || activeDefinition.IncompatibleRoles.Any(value => value != null && string.Equals(value.Id, role.Id, StringComparison.Ordinal));
                bool groupConflict = activeDefinition.IncompatibilityGroups.Any(group => !string.IsNullOrWhiteSpace(group) && newGroups.Contains(group));
                if (explicitConflict || groupConflict)
                {
                    blockers.Add(record);
                }
            }

            RoleConflictResult result = new RoleConflictResult(role, blockers);
            if (result.HasConflict)
            {
                RoleConflictDetected?.Invoke(this, result);
            }

            return result;
        }

        public RoleAcquisitionResult AddRole(RoleDefinition role, string source = "development", string context = "", bool primary = false, bool acceptConflicts = false, bool restoring = false)
        {
            if (role == null)
            {
                return RoleAcquisitionResult.Failure("MissingRole", "Role definition is missing.");
            }

            if (roles.Any(record => record.lifecycleState == RoleLifecycleState.Active && string.Equals(record.roleDefinitionId, role.Id, StringComparison.Ordinal)))
            {
                return RoleAcquisitionResult.Failure("DuplicateActiveRole", $"Role '{role.DisplayName}' is already active.");
            }

            RoleConflictResult conflict = DetectRoleConflicts(role);
            if (conflict.HasConflict && !acceptConflicts)
            {
                return RoleAcquisitionResult.ConflictDetected(conflict, $"Role '{role.DisplayName}' conflicts with {conflict.Blockers.Count} active role(s).");
            }

            if (conflict.HasConflict && acceptConflicts)
            {
                for (int i = 0; i < conflict.Blockers.Count; i++)
                {
                    RuntimeRoleRecord blocker = conflict.Blockers[i];
                    if (blocker.lifecycleState != RoleLifecycleState.Active)
                    {
                        return RoleAcquisitionResult.Failure("ConflictValidationFailed", "A blocking role is no longer active.");
                    }
                }
            }

            RuntimeRoleRecord newRecord = CreateRoleRecord(role.Id, source, context, primary);
            if (conflict.HasConflict && acceptConflicts)
            {
                for (int i = 0; i < conflict.Blockers.Count; i++)
                {
                    MarkRoleFormer(conflict.Blockers[i], "voluntary-conflict-replacement", restoring);
                }
            }

            roles.Add(newRecord);
            RebuildActiveEffects(restoring);
            RaiseRoleAdded(newRecord, restoring);
            if (conflict.HasConflict)
            {
                RoleReplacementCompleted?.Invoke(this, newRecord, restoring);
            }

            return RoleAcquisitionResult.Success(newRecord, $"Role '{role.DisplayName}' added.");
        }

        public ProgressionOperationResult SuspendRole(string recordId)
        {
            RuntimeRoleRecord record = FindRoleRecord(recordId);
            if (record == null || record.lifecycleState != RoleLifecycleState.Active)
            {
                return ProgressionOperationResult.Failure("RoleNotActive", "Selected role is not active.");
            }

            record.lifecycleState = RoleLifecycleState.Suspended;
            record.exitReason = "suspended";
            record.endedAtUtc = DateTime.UtcNow.ToString("O");
            record.endedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds;
            RebuildActiveEffects(false);
            RaiseRoleStateChanged(record, false);
            return ProgressionOperationResult.Success("Role suspended.");
        }

        public ProgressionOperationResult RevokeRole(string recordId)
        {
            RuntimeRoleRecord record = FindRoleRecord(recordId);
            if (record == null || record.lifecycleState != RoleLifecycleState.Active)
            {
                return ProgressionOperationResult.Failure("RoleNotActive", "Selected role is not active.");
            }

            record.lifecycleState = RoleLifecycleState.Revoked;
            record.exitReason = "revoked";
            record.endedAtUtc = DateTime.UtcNow.ToString("O");
            record.endedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds;
            RebuildActiveEffects(false);
            RaiseRoleStateChanged(record, false);
            return ProgressionOperationResult.Success("Role revoked.");
        }

        public ProgressionOperationResult AbandonRole(string recordId)
        {
            RuntimeRoleRecord record = FindRoleRecord(recordId);
            if (record == null || record.lifecycleState != RoleLifecycleState.Active)
            {
                return ProgressionOperationResult.Failure("RoleNotActive", "Selected role is not active.");
            }

            MarkRoleFormer(record, "voluntary-abandonment", false);
            RebuildActiveEffects(false);
            RaiseRoleAbandoned(record, false);
            return ProgressionOperationResult.Success("Role abandoned and retained as history.");
        }

        public ProgressionOperationResult AddSocialStatus(SocialStatusDefinition status, SocialStatusContextKind contextKind, string contextTargetId, string source = "development", string reason = "Development assignment", bool restoring = false)
        {
            if (status == null)
            {
                return ProgressionOperationResult.Failure("MissingSocialStatus", "Social status definition is missing.");
            }

            contextTargetId = contextKind == SocialStatusContextKind.Global ? string.Empty : contextTargetId ?? string.Empty;
            if (contextKind != SocialStatusContextKind.Global && string.IsNullOrWhiteSpace(contextTargetId))
            {
                return ProgressionOperationResult.Failure("MissingContext", "Context target ID is required for contextual social statuses.");
            }

            if (contextKind == SocialStatusContextKind.Global && !status.AllowGlobalApplication)
            {
                return ProgressionOperationResult.Failure("GlobalNotAllowed", $"Social status '{status.DisplayName}' does not allow global application.");
            }

            if (socialStatuses.Any(record => record.lifecycleState == SocialStatusLifecycleState.Active
                && string.Equals(record.socialStatusDefinitionId, status.Id, StringComparison.Ordinal)
                && record.contextKind == contextKind
                && string.Equals(record.contextTargetId ?? string.Empty, contextTargetId, StringComparison.Ordinal)))
            {
                return ProgressionOperationResult.Failure("DuplicateSocialStatusContext", "That social status/context is already active.");
            }

            RuntimeSocialStatusRecord record = new RuntimeSocialStatusRecord
            {
                recordId = CreateRuntimeRecordId("social-status"),
                socialStatusDefinitionId = status.Id,
                lifecycleState = SocialStatusLifecycleState.Active,
                source = source ?? string.Empty,
                contextKind = contextKind,
                contextTargetId = contextTargetId,
                acquisitionReason = reason ?? string.Empty,
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                startedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds
            };

            socialStatuses.Add(record);
            RebuildActiveEffects(restoring);
            RaiseSocialStatusAdded(record, restoring);
            return ProgressionOperationResult.Success($"Social status '{status.DisplayName}' added.");
        }

        public ProgressionOperationResult ResolveSocialStatus(string recordId, string reason = "resolved")
        {
            RuntimeSocialStatusRecord record = FindSocialStatusRecord(recordId);
            if (record == null || record.lifecycleState != SocialStatusLifecycleState.Active)
            {
                return ProgressionOperationResult.Failure("SocialStatusNotActive", "Selected social status is not active.");
            }

            record.lifecycleState = SocialStatusLifecycleState.ResolvedHistorical;
            record.resolutionReason = reason ?? string.Empty;
            record.endedAtUtc = DateTime.UtcNow.ToString("O");
            record.endedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds;
            RebuildActiveEffects(false);
            RaiseSocialStatusResolved(record, false);
            return ProgressionOperationResult.Success("Social status resolved and retained as history.");
        }

        public ProgressionOperationResult RecordActivityOutcome(string activityId, ActivityType activityType, ActivityOutcome outcome, float difficulty, string sourceId = "development", string sourceSystem = "TestLab")
        {
            if (string.IsNullOrWhiteSpace(activityId))
            {
                return ProgressionOperationResult.Failure("MissingActivityId", "Activity ID is required.");
            }

            if (activityRecords.Any(record => string.Equals(record.activityId, activityId, StringComparison.Ordinal)))
            {
                return ProgressionOperationResult.Failure("DuplicateActivity", $"Activity '{activityId}' is already recorded.");
            }

            ActivityOutcomeRecord record = new ActivityOutcomeRecord
            {
                activityId = activityId,
                activityType = activityType,
                sourceId = sourceId ?? string.Empty,
                playerId = PlayerId,
                difficulty = Mathf.Clamp01(difficulty),
                acceptedAtUtc = DateTime.UtcNow.ToString("O"),
                completedAtUtc = DateTime.UtcNow.ToString("O"),
                outcome = outcome,
                contribution = 1f,
                sourceSystem = sourceSystem ?? string.Empty,
                serverAuthoritative = false
            };

            activityRecords.Add(record);
            ActivityOutcomeRecorded?.Invoke(this, record, false);
            RaiseOverallLevelChanged(false);
            RaiseProgressionChanged(false);
            return ProgressionOperationResult.Success($"Recorded activity {activityId}.");
        }

        public ProgressionOperationResult RecordParticipation(string participationId, string sourceId = "development", string sourceSystem = "TestLab")
        {
            if (string.IsNullOrWhiteSpace(participationId))
            {
                return ProgressionOperationResult.Failure("MissingParticipationId", "Participation ID is required.");
            }

            if (participationRecords.Any(record => string.Equals(record.participationId, participationId, StringComparison.Ordinal)))
            {
                return ProgressionOperationResult.Failure("DuplicateParticipation", $"Participation '{participationId}' is already recorded.");
            }

            ParticipationRecord record = new ParticipationRecord
            {
                participationId = participationId,
                sourceId = sourceId ?? string.Empty,
                playerId = PlayerId,
                sourceSystem = sourceSystem ?? string.Empty,
                contribution = 1f,
                recordedAtUtc = DateTime.UtcNow.ToString("O")
            };

            participationRecords.Add(record);
            ParticipationRecorded?.Invoke(this, record, false);
            RaiseOverallLevelChanged(false);
            RaiseProgressionChanged(false);
            return ProgressionOperationResult.Success($"Recorded participation {participationId}.");
        }

        public OverallLevelBreakdown CalculateOverallLevel()
        {
            OverallLevelConfiguration config = overallLevelConfiguration;
            float success = CalculateSuccessComponent(config);
            float playtime = Mathf.Clamp01((float)(CumulativeActivePlaytimeSeconds / (config == null ? 36000f : config.ActivePlaytimeTargetSeconds)));
            float accountAge = CalculateAccountAgeComponent(config);
            float participation = Mathf.Clamp01(participationRecords.Count / (float)(config == null ? 20 : config.ParticipationTargetCount));
            float stat = CalculatePersistentStatContribution(config);

            float successWeight = config == null ? 0.55f : config.SuccessWeight;
            float playtimeWeight = config == null ? 0.15f : config.PlaytimeWeight;
            float accountAgeWeight = config == null ? 0.10f : config.AccountAgeWeight;
            float participationWeight = config == null ? 0.20f : config.ParticipationWeight;
            float activityWeightSum = Mathf.Max(0.0001f, successWeight + playtimeWeight + accountAgeWeight + participationWeight);
            float activity = (success * successWeight + playtime * playtimeWeight + accountAge * accountAgeWeight + participation * participationWeight) / activityWeightSum;

            float totalWeightSum = Mathf.Max(0.0001f, (config == null ? 0.75f : config.ActivityWeight) + (config == null ? 0.25f : config.StatWeight));
            float total = (activity * (config == null ? 0.75f : config.ActivityWeight) + stat * (config == null ? 0.25f : config.StatWeight)) / totalWeightSum;
            int level = Mathf.Max(1, Mathf.FloorToInt(total * 100f) + 1);
            return new OverallLevelBreakdown(total, level, activity, stat, success, playtime, accountAge, participation);
        }

        public PlayerIdentityProgressionSaveData CreateSaveData()
        {
            EnsureIdentityInitialized();
            return new PlayerIdentityProgressionSaveData
            {
                schemaVersion = PlayerIdentityProgressionPersistenceParticipant.CurrentParticipantSchemaVersion,
                accountId = accountId,
                playerId = playerId,
                personId = personId,
                currentWorldEntityId = CurrentWorldEntityId,
                accountCreatedAtUtc = AccountCreatedAtUtc,
                cumulativeActivePlaytimeSeconds = CumulativeActivePlaytimeSeconds,
                origin = CloneOrigin(origin),
                birthGift = CloneBirthGift(birthGift),
                permanentAttributeGrants = permanentAttributeGrants.Select(ClonePermanentGrant).ToList(),
                roles = roles.Select(CloneRole).ToList(),
                socialStatuses = socialStatuses.Select(CloneSocialStatus).ToList(),
                titles = titles.Select(CloneTitle).ToList(),
                activityRecords = activityRecords.Select(CloneActivity).ToList(),
                participationRecords = participationRecords.Select(CloneParticipation).ToList()
            };
        }

        public ProgressionOperationResult RestoreFromSaveData(PlayerIdentityProgressionSaveData saveData, DefinitionRegistry registry, bool restoring)
        {
            if (saveData == null)
            {
                return ProgressionOperationResult.Failure("MissingSaveData", "Identity/progression save data is missing.");
            }

            ConfigureDefinitions(registry);
            ClearActiveEffectSources();
            notificationsSuppressed = true;
            try
            {
                accountId = saveData.accountId;
                playerId = saveData.playerId;
                personId = saveData.personId;
                AccountCreatedAtUtc = saveData.accountCreatedAtUtc;
                playTimeTracker?.Restore(saveData.cumulativeActivePlaytimeSeconds);
                origin = CloneOrigin(saveData.origin);
                birthGift = CloneBirthGift(saveData.birthGift);
                permanentAttributeGrants = saveData.permanentAttributeGrants == null ? new List<RuntimePermanentAttributeGrantRecord>() : saveData.permanentAttributeGrants.Select(ClonePermanentGrant).ToList();
                roles = saveData.roles == null ? new List<RuntimeRoleRecord>() : saveData.roles.Select(CloneRole).ToList();
                socialStatuses = saveData.socialStatuses == null ? new List<RuntimeSocialStatusRecord>() : saveData.socialStatuses.Select(CloneSocialStatus).ToList();
                titles = saveData.titles == null ? new List<RuntimeTitleRecord>() : saveData.titles.Select(CloneTitle).ToList();
                activityRecords = saveData.activityRecords == null ? new List<ActivityOutcomeRecord>() : saveData.activityRecords.Select(CloneActivity).ToList();
                participationRecords = saveData.participationRecords == null ? new List<ParticipationRecord>() : saveData.participationRecords.Select(CloneParticipation).ToList();
                EnsureIdentityInitialized();
                RebuildActiveEffects(restoring);
            }
            finally
            {
                notificationsSuppressed = false;
            }

            RaiseProgressionChanged(restoring);
            RaiseOverallLevelChanged(restoring);
            return ProgressionOperationResult.Success("Identity/progression state restored.");
        }

        public string BuildDiagnosticSummary()
        {
            OverallLevelBreakdown breakdown = CalculateOverallLevel();
            string rolesLine = roles.Count == 0 ? "None" : string.Join(", ", roles.Select(role => $"{role.roleDefinitionId}:{role.lifecycleState}{(role.primary ? ":Primary" : string.Empty)}"));
            string statusesLine = socialStatuses.Count == 0 ? "None" : string.Join(", ", socialStatuses.Select(status => $"{status.socialStatusDefinitionId}:{status.contextKind}:{status.contextTargetId}:{status.lifecycleState}"));
            string titlesLine = titles.Count == 0 ? "None" : string.Join(", ", titles.Select(title => title.titleDefinitionId));
            return string.Join(Environment.NewLine, new[]
            {
                "Feature 5.1 Identity / Progression",
                $"Account: {AccountId}",
                $"Player: {PlayerId}",
                $"Person: {PersonId}",
                $"World Entity: {(string.IsNullOrWhiteSpace(CurrentWorldEntityId) ? "None" : CurrentWorldEntityId)}",
                $"Account Created UTC: {AccountCreatedAtUtc}",
                $"Origin: {(origin != null && origin.assigned ? $"{origin.originFamilyId} / {origin.originId}" : "Unassigned")}",
                $"Birth Gift: {(string.IsNullOrWhiteSpace(birthGift?.giftDefinitionId) ? "None" : $"{birthGift.giftDefinitionId} {birthGift.state} Progress={birthGift.currentProgressSeconds:0.#}/{birthGift.requiredActivePlaytimeSeconds:0.#}")}",
                $"Birth Gift Roll Weights: {BuildBirthGiftWeightDiagnostic()}",
                $"Permanent Attribute Grants: {permanentAttributeGrants.Count}",
                $"Roles: {rolesLine}",
                $"Social Statuses: {statusesLine}",
                $"Titles: {titlesLine}",
                "Wallet: EconomyRuntime authoritative account",
                $"Activities: {activityRecords.Count}",
                $"Participation: {participationRecords.Count}",
                $"Overall Level: {breakdown.OverallLevel} Raw={breakdown.RawTotalScore:0.###} Activity={breakdown.NormalizedActivityScore:0.###} Stats={breakdown.NormalizedStatScore:0.###} Success={breakdown.SuccessComponent:0.###}"
            });
        }

        private void EnsureRuntimeReferences()
        {
            if (actorStats == null)
            {
                actorStats = GetComponent<ActorStats>();
            }

            if (characterAttributes == null)
            {
                characterAttributes = GetComponent<CharacterAttributes>();
            }

            if (calculatedStats == null)
            {
                calculatedStats = GetComponent<CalculatedStatCollection>();
            }

            if (skillCollection == null)
            {
                skillCollection = GetComponent<CharacterSkillCollection>();
            }

            if (abilityCollection == null)
            {
                abilityCollection = GetComponent<CharacterAbilityCollection>();
            }

            if (worldEntityIdentity == null)
            {
                worldEntityIdentity = GetComponent<WorldEntityIdentity>();
            }
        }

        private void EnsureIdentityInitialized()
        {
            accountId = string.IsNullOrWhiteSpace(accountId) ? PersistenceService.LocalAccountId : accountId;
            playerId = string.IsNullOrWhiteSpace(playerId) ? PersistenceService.LocalPlayerId : playerId;
            if (string.IsNullOrWhiteSpace(personId))
            {
                personId = CreatePersonId();
            }

            if (string.IsNullOrWhiteSpace(AccountCreatedAtUtc))
            {
                AccountCreatedAtUtc = DateTime.UtcNow.ToString("O");
            }
        }

        private string CreatePersonId()
        {
            return $"person.{playerId}.runtime.{Guid.NewGuid():N}".ToLowerInvariant();
        }

        private static int CreateAutomaticOriginSeed()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            return BitConverter.ToInt32(bytes, 0);
        }

        private void ApplyOriginStatGrants(OriginDefinition originDefinition, bool restoring)
        {
            if (originDefinition == null || origin.originStatGrantsApplied)
            {
                return;
            }

            foreach (PermanentAttributeGrantDefinition grant in originDefinition.StartingAttributeGrants)
            {
                AddPermanentAttributeGrant($"origin.{originDefinition.Id}.{grant.Attribute.Id}", originDefinition.Id, grant.Attribute.Id, grant.Amount);
            }

            origin.originStatGrantsApplied = true;
        }

        public void MarkStartingCurrencyApplied(bool restoring = false)
        {
            if (origin == null || !origin.assigned || origin.startingCurrencyApplied)
            {
                return;
            }

            origin.startingCurrencyApplied = true;
            RaiseProgressionChanged(restoring);
        }

        private void AssignStartingRoleStatusAndTitle(OriginDefinition originDefinition, bool restoring)
        {
            if (originDefinition == null)
            {
                return;
            }

            if (originDefinition.StartingRole != null)
            {
                AddRole(originDefinition.StartingRole, origin.assignmentSource, originDefinition.Id, primary: true, acceptConflicts: true, restoring: restoring);
            }

            GrantOriginSkills(originDefinition, restoring);

            foreach (SocialStatusAssignmentDefinition assignment in originDefinition.StartingSocialStatuses)
            {
                if (assignment?.SocialStatus != null)
                {
                    AddSocialStatus(assignment.SocialStatus, assignment.ContextKind, assignment.ResolveContextTargetId(), origin.assignmentSource, assignment.AcquisitionReason, restoring);
                }
            }

            if (originDefinition.StartingTitle != null && titles.All(title => !string.Equals(title.titleDefinitionId, originDefinition.StartingTitle.Id, StringComparison.Ordinal)))
            {
                titles.Add(new RuntimeTitleRecord
                {
                    titleDefinitionId = originDefinition.StartingTitle.Id,
                    source = origin.assignmentSource,
                    assignedAtUtc = DateTime.UtcNow.ToString("O"),
                    assignedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds,
                    active = true
                });
            }
        }

        private void AssignBirthGift(BirthGiftDefinition gift, string familyId, string originId, string source, bool restoring)
        {
            if (gift == null || !string.IsNullOrWhiteSpace(birthGift?.giftDefinitionId))
            {
                return;
            }

            float required = gift.AwakeningMode == BirthGiftAwakeningMode.DelayedActivePlaytime
                ? Mathf.Max(gift.RequiredActivePlaytimeSeconds, DefaultDelayedBirthGiftSeconds)
                : 0f;
            birthGift = new RuntimeBirthGiftRecord
            {
                giftDefinitionId = gift.Id,
                rarityId = gift.Rarity == null ? string.Empty : gift.Rarity.Id,
                giftType = gift.GiftType,
                originFamilyId = familyId,
                originId = originId,
                awakeningMode = gift.AwakeningMode,
                requiredActivePlaytimeSeconds = required,
                currentProgressSeconds = 0f,
                state = gift.AwakeningMode == BirthGiftAwakeningMode.ImmediateAutomatic ? BirthGiftRuntimeState.Awakened : BirthGiftRuntimeState.Dormant,
                rewardApplied = false,
                assignedAtUtc = DateTime.UtcNow.ToString("O"),
                assignedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds,
                assignmentSource = source ?? string.Empty
            };

            RaiseBirthGiftAssigned(restoring);
            if (birthGift.state == BirthGiftRuntimeState.Awakened)
            {
                AwakeBirthGift(gift, restoring);
            }
        }

        public ProgressionOperationResult ForceBirthGiftAwakening(DefinitionRegistry registry)
        {
            ConfigureDefinitions(registry);
            BirthGiftDefinition gift = ResolveBirthGiftDefinition(registry);
            if (gift == null)
            {
                return ProgressionOperationResult.Failure("MissingGift", "Birth gift definition is missing.");
            }

            if (birthGift.state == BirthGiftRuntimeState.Awakened && birthGift.rewardApplied)
            {
                return ProgressionOperationResult.Failure("GiftAlreadyAwakened", "Birth gift is already awakened.");
            }

            birthGift.currentProgressSeconds = Mathf.Max(birthGift.currentProgressSeconds, birthGift.requiredActivePlaytimeSeconds);
            AwakeBirthGift(gift, false);
            return ProgressionOperationResult.Success("Birth gift awakened.");
        }

        public ProgressionOperationResult AdvanceBirthGiftProgressForTesting(float seconds, DefinitionRegistry registry)
        {
            ConfigureDefinitions(registry);
            if (birthGift == null || birthGift.state != BirthGiftRuntimeState.Dormant)
            {
                return ProgressionOperationResult.Failure("GiftNotDormant", "Birth gift is not dormant.");
            }

            birthGift.currentProgressSeconds += Mathf.Max(0f, seconds);
            BirthGiftDefinition gift = ResolveBirthGiftDefinition(registry);
            if (gift != null && birthGift.currentProgressSeconds >= birthGift.requiredActivePlaytimeSeconds)
            {
                AwakeBirthGift(gift, false);
            }
            else
            {
                RaiseProgressionChanged(false);
            }

            return ProgressionOperationResult.Success($"Advanced birth gift progress by {seconds:0.#} seconds.");
        }

        private void TickDelayedBirthGift()
        {
            if (birthGift == null || birthGift.state != BirthGiftRuntimeState.Dormant || birthGift.awakeningMode != BirthGiftAwakeningMode.DelayedActivePlaytime)
            {
                lastPlaytimeSample = CumulativeActivePlaytimeSeconds;
                return;
            }

            double current = CumulativeActivePlaytimeSeconds;
            double delta = Math.Max(0d, current - lastPlaytimeSample);
            lastPlaytimeSample = current;
            if (delta <= 0d)
            {
                return;
            }

            birthGift.currentProgressSeconds += (float)delta;
            if (birthGift.currentProgressSeconds >= birthGift.requiredActivePlaytimeSeconds)
            {
                BirthGiftDefinition gift = ResolveBirthGiftDefinition(definitionRegistry);
                if (gift != null)
                {
                    AwakeBirthGift(gift, false);
                }
                else
                {
                    RaiseProgressionChanged(false);
                }
            }
        }

        private void AwakeBirthGift(BirthGiftDefinition gift, bool restoring)
        {
            birthGift.state = BirthGiftRuntimeState.Awakened;
            birthGift.awakenedAtUtc = DateTime.UtcNow.ToString("O");
            birthGift.awakenedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds;
            ApplyBirthGiftReward(gift, restoring);
            RaiseBirthGiftAwakened(restoring);
        }

        private void ApplyBirthGiftReward(BirthGiftDefinition gift, bool restoring)
        {
            if (gift == null || birthGift.rewardApplied)
            {
                return;
            }

            if (gift.GiftType == BirthGiftType.PermanentAttributeGrant)
            {
                foreach (PermanentAttributeGrantDefinition grant in gift.PermanentAttributeGrants)
                {
                    AddPermanentAttributeGrant($"birth-gift.{gift.Id}.{grant.Attribute.Id}", gift.Id, grant.Attribute.Id, grant.Amount);
                }
            }
            else if (gift.GiftType == BirthGiftType.GrowthAffinity)
            {
                foreach (PermanentAttributeGrantDefinition grant in gift.GrowthAffinityGrants)
                {
                    AddPermanentAttributeGrant($"birth-gift-affinity.{gift.Id}.{grant.Attribute.Id}", gift.Id, grant.Attribute.Id, grant.Amount);
                }
            }
            else if (gift.SkillGrants.Count > 0)
            {
                GrantBirthGiftSkills(gift, restoring);
            }
            else if (gift.GiftType == BirthGiftType.LatentSkill && gift.GrantedAbility != null && !string.IsNullOrWhiteSpace(gift.GrantedAbility.AbilityId))
            {
                abilityCollection?.Grant(gift.GrantedAbility.Ability, AbilityGrantSourceCategory.BirthGift, $"birth-gift.{gift.Id}", restoring);
            }

            birthGift.rewardApplied = true;
            RebuildActiveEffects(restoring);
        }

        private void GrantOriginSkills(OriginDefinition originDefinition, bool restoring)
        {
            if (skillCollection == null || originDefinition == null)
            {
                return;
            }

            foreach (SkillGrantDefinition grant in originDefinition.StartingSkillGrants)
            {
                if (grant?.Skill != null)
                {
                    skillCollection.GrantSkill(grant.Skill, grant.StartingGrade, SkillAcquisitionSource.Origin, grant.Reason, originDefinition.Id, restoring);
                }
            }
        }

        private void GrantBirthGiftSkills(BirthGiftDefinition gift, bool restoring)
        {
            if (skillCollection == null || gift == null)
            {
                return;
            }

            foreach (SkillGrantDefinition grant in gift.SkillGrants)
            {
                if (grant?.Skill != null)
                {
                    skillCollection.GrantSkill(grant.Skill, grant.StartingGrade, SkillAcquisitionSource.BirthGift, grant.Reason, gift.Id, restoring);
                }
            }
        }

        private void AddPermanentAttributeGrant(string sourceId, string definitionId, string attributeId, float amount)
        {
            if (permanentAttributeGrants.Any(grant => string.Equals(grant.sourceId, sourceId, StringComparison.Ordinal)))
            {
                return;
            }

            permanentAttributeGrants.Add(new RuntimePermanentAttributeGrantRecord
            {
                sourceId = sourceId,
                definitionId = definitionId,
                attributeId = attributeId,
                amount = Mathf.Max(0f, amount),
                applied = false
            });
        }

        private void RebuildActiveEffects(bool restoring)
        {
            ClearActiveEffectSources();

            for (int i = 0; i < permanentAttributeGrants.Count; i++)
            {
                RuntimePermanentAttributeGrantRecord grant = permanentAttributeGrants[i];
                if (characterAttributes != null && characterAttributes.TryAddPermanentSource(grant.sourceId, MapAttributeSourceCategory(grant.sourceId), grant.attributeId, grant.amount, removable: true, out _))
                {
                    grant.applied = true;
                }
            }

            BirthGiftDefinition activeGift = ResolveBirthGiftDefinition(definitionRegistry);
            if (birthGift != null && birthGift.rewardApplied && activeGift?.GiftType == BirthGiftType.LatentSkill && activeGift.GrantedAbility?.Ability != null)
            {
                abilityCollection?.Grant(activeGift.GrantedAbility.Ability, AbilityGrantSourceCategory.BirthGift, $"birth-gift.{activeGift.Id}", restoring);
            }

            foreach (RuntimeRoleRecord role in roles)
            {
                role.activeEffectsApplied = false;
                if (role.lifecycleState != RoleLifecycleState.Active)
                {
                    continue;
                }

                RoleDefinition definition = ResolveRoleDefinition(role.roleDefinitionId);
                if (definition == null)
                {
                    continue;
                }

                string roleSourceId = $"role.{role.recordId}";
                ApplyDefinitionModifiers(definition.CalculatedStatModifiers, StatModifierSourceType.Role, roleSourceId);
                foreach (ProgressionAbilityReference abilityReference in definition.GrantedAbilities)
                {
                    if (abilityReference?.Ability != null)
                    {
                        abilityCollection?.Grant(abilityReference.Ability, AbilityGrantSourceCategory.Role, roleSourceId, restoring);
                    }
                }
                foreach (ProgressionAbilityReference abilityReference in definition.BlockedAbilities)
                {
                    if (abilityReference?.Ability != null)
                    {
                        abilityCollection?.Block(abilityReference.Ability, AbilityGrantSourceCategory.Role, roleSourceId, restoring);
                    }
                }
                role.activeEffectsApplied = true;
            }

            foreach (RuntimeSocialStatusRecord status in socialStatuses)
            {
                status.activeEffectsApplied = false;
                if (status.lifecycleState != SocialStatusLifecycleState.Active)
                {
                    continue;
                }

                SocialStatusDefinition definition = ResolveSocialStatusDefinition(status.socialStatusDefinitionId);
                if (definition == null)
                {
                    continue;
                }

                string statusSourceId = $"social-status.{status.recordId}";
                ApplyDefinitionModifiers(definition.CalculatedStatModifiers, StatModifierSourceType.SocialStatus, statusSourceId);
                foreach (ProgressionAbilityReference abilityReference in definition.GrantedAbilities)
                {
                    if (abilityReference?.Ability != null)
                    {
                        abilityCollection?.Grant(abilityReference.Ability, AbilityGrantSourceCategory.SocialStatus, statusSourceId, restoring);
                    }
                }
                foreach (ProgressionAbilityReference abilityReference in definition.BlockedAbilities)
                {
                    if (abilityReference?.Ability != null)
                    {
                        abilityCollection?.Block(abilityReference.Ability, AbilityGrantSourceCategory.SocialStatus, statusSourceId, restoring);
                    }
                }
                status.activeEffectsApplied = true;
            }

            RaiseOverallLevelChanged(restoring);
        }

        private void ApplyDefinitionModifiers(IReadOnlyList<CalculatedStatModifierDefinition> modifiers, StatModifierSourceType sourceType, string sourceId)
        {
            if (actorStats == null || modifiers == null)
            {
                return;
            }

            StatModifierSource source = new StatModifierSource(sourceType, sourceId);
            for (int i = 0; i < modifiers.Count; i++)
            {
                CalculatedStatModifierDefinition modifier = modifiers[i];
                if (modifier != null && actorStats.AddCalculatedStatContribution(modifier.CreateRuntimeContribution(source, 1, $"{sourceId}.{i}.{modifier.Stat.Id}")))
                {
                    activeStatSources.Add(source);
                }
            }
        }

        private void ClearActiveEffectSources()
        {
            if (actorStats != null)
            {
                // Removing a contribution raises stat-change callbacks. Those callbacks can
                // synchronously rebuild progression and repopulate this set, so detach the
                // current batch before invoking them to keep cleanup re-entrant safe.
                StatModifierSource[] sourcesToRemove = activeStatSources.ToArray();
                activeStatSources.Clear();
                foreach (StatModifierSource source in sourcesToRemove)
                {
                    actorStats.RemoveCalculatedStatContributions(source);
                }
            }

            foreach (RuntimeRoleRecord role in roles)
            {
                abilityCollection?.RemoveSource(AbilityGrantSourceCategory.Role, $"role.{role.recordId}");
                abilityCollection?.RemoveBlockSource(AbilityGrantSourceCategory.Role, $"role.{role.recordId}");
            }

            foreach (RuntimeSocialStatusRecord status in socialStatuses)
            {
                abilityCollection?.RemoveSource(AbilityGrantSourceCategory.SocialStatus, $"social-status.{status.recordId}");
                abilityCollection?.RemoveBlockSource(AbilityGrantSourceCategory.SocialStatus, $"social-status.{status.recordId}");
            }

            if (!string.IsNullOrWhiteSpace(birthGift?.giftDefinitionId))
            {
                abilityCollection?.RemoveSource(AbilityGrantSourceCategory.BirthGift, $"birth-gift.{birthGift.giftDefinitionId}");
            }

            if (characterAttributes != null)
            {
                for (int i = 0; i < permanentAttributeGrants.Count; i++)
                {
                    characterAttributes.RemovePermanentSource(permanentAttributeGrants[i].sourceId, out _);
                    permanentAttributeGrants[i].applied = false;
                }
            }

            activeStatSources.Clear();
        }

        private RoleDefinition ResolveRoleDefinition(string roleId)
        {
            return definitionRegistry != null && definitionRegistry.TryGet(roleId, out RoleDefinition role) ? role : null;
        }

        private SocialStatusDefinition ResolveSocialStatusDefinition(string statusId)
        {
            return definitionRegistry != null && definitionRegistry.TryGet(statusId, out SocialStatusDefinition status) ? status : null;
        }

        private BirthGiftDefinition ResolveBirthGiftDefinition(DefinitionRegistry registry)
        {
            DefinitionRegistry effectiveRegistry = registry ?? definitionRegistry;
            if (birthGift == null || string.IsNullOrWhiteSpace(birthGift.giftDefinitionId) || effectiveRegistry == null)
            {
                return null;
            }

            return effectiveRegistry.TryGet(birthGift.giftDefinitionId, out BirthGiftDefinition gift) ? gift : null;
        }

        private string BuildBirthGiftWeightDiagnostic()
        {
            if (definitionRegistry == null || origin == null || !origin.assigned
                || !definitionRegistry.TryGet(origin.originFamilyId, out OriginFamilyDefinition family)
                || !definitionRegistry.TryGet(origin.originId, out OriginDefinition originDefinition))
            {
                return "Unavailable";
            }

            CharacterOriginGenerator generator = new CharacterOriginGenerator(definitionRegistry, new SeededProgressionRandomSource(origin.randomSeed));
            IReadOnlyList<BirthGiftWeightEntry> weights = generator.BuildBirthGiftWeights(family, originDefinition, out string failureReason);
            return weights.Count == 0
                ? failureReason
                : string.Join(", ", weights.Select(entry => $"{entry.Gift.Id}={entry.Probability:P1}"));
        }

        public void ConfigureDefinitions(DefinitionRegistry registry)
        {
            definitionRegistry = registry;
            if (skillCollection != null)
            {
                skillCollection.Configure(registry, calculatedStats, GetComponent<UnityIsekaiGame.Magic.PlayerSpellLoadout>());
            }
        }

        private RuntimeRoleRecord CreateRoleRecord(string roleId, string source, string context, bool primary)
        {
            return new RuntimeRoleRecord
            {
                recordId = CreateRuntimeRecordId("role"),
                roleDefinitionId = roleId,
                lifecycleState = RoleLifecycleState.Active,
                acquisitionSource = source ?? string.Empty,
                context = context ?? string.Empty,
                acquisitionReason = string.IsNullOrWhiteSpace(context) ? "Role acquired" : context,
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                startedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds,
                primary = primary
            };
        }

        private static string CreateRuntimeRecordId(string prefix)
        {
            return $"{prefix}.{Guid.NewGuid():N}".ToLowerInvariant();
        }

        private RuntimeRoleRecord FindRoleRecord(string recordId)
        {
            return roles.FirstOrDefault(record => string.Equals(record.recordId, recordId, StringComparison.Ordinal));
        }

        private RuntimeSocialStatusRecord FindSocialStatusRecord(string recordId)
        {
            return socialStatuses.FirstOrDefault(record => string.Equals(record.recordId, recordId, StringComparison.Ordinal));
        }

        private void MarkRoleFormer(RuntimeRoleRecord record, string reason, bool restoring)
        {
            record.lifecycleState = RoleLifecycleState.FormerHistorical;
            record.exitReason = reason;
            record.endedAtUtc = DateTime.UtcNow.ToString("O");
            record.endedAtPlaytimeSeconds = CumulativeActivePlaytimeSeconds;
            record.activeEffectsApplied = false;
            RaiseRoleStateChanged(record, restoring);
        }

        private float CalculateSuccessComponent(OverallLevelConfiguration config)
        {
            int total = activityRecords.Count;
            if (total == 0)
            {
                return 0f;
            }

            List<ActivityOutcomeRecord> successes = activityRecords.Where(record => record.outcome == ActivityOutcome.Success).ToList();
            float successPercentage = successes.Count / (float)total;
            float successDifficulty = successes.Count == 0 ? 0f : successes.Average(record => Mathf.Clamp01(record.difficulty));
            float sampleFactor = Mathf.Clamp01(total / (float)(config == null ? 5 : config.MinimumMeaningfulActivitySamples));
            float percentageWeight = config == null ? 0.60f : config.SuccessPercentageWeight;
            float difficultyWeight = config == null ? 0.40f : config.SuccessDifficultyWeight;
            float weightSum = Mathf.Max(0.0001f, percentageWeight + difficultyWeight);
            return ((successPercentage * percentageWeight + successDifficulty * difficultyWeight) / weightSum) * sampleFactor;
        }

        private float CalculateAccountAgeComponent(OverallLevelConfiguration config)
        {
            if (!DateTime.TryParse(AccountCreatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime createdAt))
            {
                return 0f;
            }

            DateTime now = DateTime.UtcNow;
            if (createdAt > now.AddMinutes(5))
            {
                return 0f;
            }

            double days = Math.Max(0d, (now - createdAt).TotalDays);
            return Mathf.Clamp01((float)(days / (config == null ? 30f : config.AccountAgeTargetDays)));
        }

        private float CalculatePersistentStatContribution(OverallLevelConfiguration config)
        {
            if (characterAttributes != null && characterAttributes.IsConfigured)
            {
                float normalizationConstant = config == null ? 20f : config.AttributeNormalizationConstant;
                IReadOnlyCollection<RuntimeAttributeValueRecord> values = characterAttributes.AttributeValues;
                if (values == null || values.Count == 0)
                {
                    return 0f;
                }

                float attributeTotal = 0f;
                int count = 0;
                foreach (RuntimeAttributeValueRecord record in values)
                {
                    if (record == null)
                    {
                        continue;
                    }

                    attributeTotal += record.currentValue / (record.currentValue + normalizationConstant);
                    count++;
                }

                return count == 0 ? 0f : Mathf.Clamp01(attributeTotal / count);
            }

            if (permanentAttributeGrants.Count == 0)
            {
                return 0f;
            }

            HashSet<string> eligible = config == null || config.EligiblePersistentAttributes == null || config.EligiblePersistentAttributes.Length == 0
                ? new HashSet<string>(AttributeIds.AlphaAttributeIds, StringComparer.Ordinal)
                : new HashSet<string>(config.EligiblePersistentAttributes.Where(attribute => attribute != null).Select(attribute => attribute.Id), StringComparer.Ordinal);

            float total = 0f;
            foreach (RuntimePermanentAttributeGrantRecord grant in permanentAttributeGrants)
            {
                if (eligible.Contains(grant.attributeId))
                {
                    total += Mathf.Max(0f, grant.amount);
                }
            }

            return Mathf.Clamp01(total / (config == null ? 100f : config.PersistentStatTargetTotal));
        }

        private static CalculatedStatContributionSourceCategory MapAttributeSourceCategory(string sourceId)
        {
            if (sourceId != null && sourceId.StartsWith("origin.", StringComparison.Ordinal))
            {
                return CalculatedStatContributionSourceCategory.Origin;
            }

            if (sourceId != null && sourceId.StartsWith("birth-gift.", StringComparison.Ordinal))
            {
                return CalculatedStatContributionSourceCategory.BirthGift;
            }

            return CalculatedStatContributionSourceCategory.Development;
        }

        private void RaiseProgressionChanged(bool restoring)
        {
            if (!notificationsSuppressed)
            {
                ProgressionChanged?.Invoke(this, restoring);
            }
        }

        private void RaiseOriginAssigned(bool restoring)
        {
            if (!notificationsSuppressed)
            {
                OriginAssigned?.Invoke(this, origin, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseBirthGiftAssigned(bool restoring)
        {
            if (!notificationsSuppressed)
            {
                BirthGiftAssigned?.Invoke(this, birthGift, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseBirthGiftAwakened(bool restoring)
        {
            if (!notificationsSuppressed)
            {
                BirthGiftAwakened?.Invoke(this, birthGift, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseRoleAdded(RuntimeRoleRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                RoleAdded?.Invoke(this, record, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseRoleStateChanged(RuntimeRoleRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                RoleStateChanged?.Invoke(this, record, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseRoleAbandoned(RuntimeRoleRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                RoleAbandoned?.Invoke(this, record, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseSocialStatusAdded(RuntimeSocialStatusRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                SocialStatusAdded?.Invoke(this, record, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseSocialStatusResolved(RuntimeSocialStatusRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                SocialStatusResolved?.Invoke(this, record, restoring);
                RaiseProgressionChanged(restoring);
            }
        }

        private void RaiseOverallLevelChanged(bool restoring)
        {
            if (!notificationsSuppressed)
            {
                OverallLevelChanged?.Invoke(this, CalculateOverallLevel(), restoring);
            }
        }

        private static RuntimeOriginAssignmentRecord CloneOrigin(RuntimeOriginAssignmentRecord value)
        {
            if (value == null)
            {
                return new RuntimeOriginAssignmentRecord();
            }

            return new RuntimeOriginAssignmentRecord
            {
                assigned = value.assigned,
                originFamilyId = value.originFamilyId,
                originId = value.originId,
                randomSeed = value.randomSeed,
                startingGoldAmount = value.startingGoldAmount,
                originStatGrantsApplied = value.originStatGrantsApplied,
                startingCurrencyApplied = value.startingCurrencyApplied,
                assignedAtUtc = value.assignedAtUtc,
                assignedAtPlaytimeSeconds = value.assignedAtPlaytimeSeconds,
                assignmentSource = value.assignmentSource
            };
        }

        private static RuntimeBirthGiftRecord CloneBirthGift(RuntimeBirthGiftRecord value)
        {
            if (value == null)
            {
                return new RuntimeBirthGiftRecord();
            }

            return new RuntimeBirthGiftRecord
            {
                giftDefinitionId = value.giftDefinitionId,
                rarityId = value.rarityId,
                giftType = value.giftType,
                originFamilyId = value.originFamilyId,
                originId = value.originId,
                awakeningMode = value.awakeningMode,
                requiredActivePlaytimeSeconds = value.requiredActivePlaytimeSeconds,
                currentProgressSeconds = value.currentProgressSeconds,
                state = value.state,
                rewardApplied = value.rewardApplied,
                assignedAtUtc = value.assignedAtUtc,
                assignedAtPlaytimeSeconds = value.assignedAtPlaytimeSeconds,
                awakenedAtUtc = value.awakenedAtUtc,
                awakenedAtPlaytimeSeconds = value.awakenedAtPlaytimeSeconds,
                assignmentSource = value.assignmentSource
            };
        }

        private static RuntimePermanentAttributeGrantRecord ClonePermanentGrant(RuntimePermanentAttributeGrantRecord value) => new RuntimePermanentAttributeGrantRecord
        {
            sourceId = value.sourceId,
            definitionId = value.definitionId,
            attributeId = value.attributeId,
            amount = value.amount,
            applied = value.applied
        };

        private static RuntimeRoleRecord CloneRole(RuntimeRoleRecord value) => new RuntimeRoleRecord
        {
            recordId = value.recordId,
            roleDefinitionId = value.roleDefinitionId,
            lifecycleState = value.lifecycleState,
            acquisitionSource = value.acquisitionSource,
            context = value.context,
            grantingFactionId = value.grantingFactionId,
            grantingPersonId = value.grantingPersonId,
            grantingOrganizationId = value.grantingOrganizationId,
            acquisitionReason = value.acquisitionReason,
            exitReason = value.exitReason,
            startedAtUtc = value.startedAtUtc,
            startedAtPlaytimeSeconds = value.startedAtPlaytimeSeconds,
            endedAtUtc = value.endedAtUtc,
            endedAtPlaytimeSeconds = value.endedAtPlaytimeSeconds,
            primary = value.primary,
            activeEffectsApplied = value.activeEffectsApplied
        };

        private static RuntimeSocialStatusRecord CloneSocialStatus(RuntimeSocialStatusRecord value) => new RuntimeSocialStatusRecord
        {
            recordId = value.recordId,
            socialStatusDefinitionId = value.socialStatusDefinitionId,
            lifecycleState = value.lifecycleState,
            source = value.source,
            contextKind = value.contextKind,
            contextTargetId = value.contextTargetId,
            acquisitionReason = value.acquisitionReason,
            resolutionReason = value.resolutionReason,
            startedAtUtc = value.startedAtUtc,
            startedAtPlaytimeSeconds = value.startedAtPlaytimeSeconds,
            endedAtUtc = value.endedAtUtc,
            endedAtPlaytimeSeconds = value.endedAtPlaytimeSeconds,
            activeEffectsApplied = value.activeEffectsApplied
        };

        private static RuntimeTitleRecord CloneTitle(RuntimeTitleRecord value) => new RuntimeTitleRecord
        {
            titleDefinitionId = value.titleDefinitionId,
            source = value.source,
            assignedAtUtc = value.assignedAtUtc,
            assignedAtPlaytimeSeconds = value.assignedAtPlaytimeSeconds,
            active = value.active
        };

        private static ActivityOutcomeRecord CloneActivity(ActivityOutcomeRecord value) => new ActivityOutcomeRecord
        {
            activityId = value.activityId,
            activityType = value.activityType,
            sourceId = value.sourceId,
            playerId = value.playerId,
            difficulty = value.difficulty,
            acceptedAtUtc = value.acceptedAtUtc,
            completedAtUtc = value.completedAtUtc,
            outcome = value.outcome,
            contribution = value.contribution,
            sourceSystem = value.sourceSystem,
            serverAuthoritative = value.serverAuthoritative
        };

        private static ParticipationRecord CloneParticipation(ParticipationRecord value) => new ParticipationRecord
        {
            participationId = value.participationId,
            sourceId = value.sourceId,
            playerId = value.playerId,
            sourceSystem = value.sourceSystem,
            contribution = value.contribution,
            recordedAtUtc = value.recordedAtUtc
        };

    }
}
