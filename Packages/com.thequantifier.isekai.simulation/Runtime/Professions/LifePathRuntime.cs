using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    public sealed class LifePathRuntime
    {
        private readonly Dictionary<string, LifePathRecordData> lifePathsById = new Dictionary<string, LifePathRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonAspirationData> aspirationsById = new Dictionary<string, PersonAspirationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonGoalData> goalsById = new Dictionary<string, PersonGoalData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionalIdentityData> identitiesById = new Dictionary<string, ProfessionalIdentityData>(StringComparer.Ordinal);
        private readonly Dictionary<string, IdentityConflictData> conflictsById = new Dictionary<string, IdentityConflictData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LifePathAchievementSetbackReferenceData> achievementSetbacksById = new Dictionary<string, LifePathAchievementSetbackReferenceData>(StringComparer.Ordinal);
        private readonly List<LifePathHookData> historyHooks = new List<LifePathHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private TrainingRuntime training;
        private ProfessionalActivityRuntime activities;
        private CredentialRuntime credentials;
        private ProfessionalRankRuntime ranks;
        private PositionEmploymentRuntime positions;
        private CareerHistoryRuntime careerHistory;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownOrganizationIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int LifePathCount => lifePathsById.Count;
        public int AspirationCount => aspirationsById.Count;
        public int GoalCount => goalsById.Count;
        public int IdentityCount => identitiesById.Count;
        public IReadOnlyList<LifePathHookData> HistoryHooks => historyHooks.Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, CareerHistoryRuntime careerHistoryRuntime, IEnumerable<string> persons = null, IEnumerable<string> organizations = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            training = trainingRuntime;
            activities = activityRuntime;
            credentials = credentialRuntime;
            ranks = rankRuntime;
            positions = positionRuntime;
            careerHistory = careerHistoryRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownOrganizationIds = new HashSet<string>((organizations ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public bool TryGetLifePath(string lifePathId, out LifePathRecordData record)
        {
            if (!string.IsNullOrWhiteSpace(lifePathId) && lifePathsById.TryGetValue(lifePathId, out LifePathRecordData found))
            {
                record = found.Clone();
                return true;
            }

            record = null;
            return false;
        }

        public bool TryGetAspiration(string aspirationId, out PersonAspirationData record)
        {
            if (!string.IsNullOrWhiteSpace(aspirationId) && aspirationsById.TryGetValue(aspirationId, out PersonAspirationData found))
            {
                record = found.Clone();
                return true;
            }

            record = null;
            return false;
        }

        public bool TryGetGoal(string goalId, out PersonGoalData record)
        {
            if (!string.IsNullOrWhiteSpace(goalId) && goalsById.TryGetValue(goalId, out PersonGoalData found))
            {
                record = found.Clone();
                return true;
            }

            record = null;
            return false;
        }

        public bool TryGetIdentity(string identityId, out ProfessionalIdentityData record)
        {
            if (!string.IsNullOrWhiteSpace(identityId) && identitiesById.TryGetValue(identityId, out ProfessionalIdentityData found))
            {
                record = found.Clone();
                return true;
            }

            record = null;
            return false;
        }

        public IReadOnlyList<LifePathRecordData> QueryLifePathsByPerson(string personId) => lifePathsById.Values.Where(item => Same(item.personId, personId)).OrderBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.lifePathId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PersonAspirationData> QueryAspirationsByPerson(string personId) => aspirationsById.Values.Where(item => Same(item.personId, personId)).OrderByDescending(item => item.priority).ThenBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.aspirationId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PersonGoalData> QueryGoalsByPerson(string personId) => goalsById.Values.Where(item => Same(item.personId, personId)).OrderByDescending(item => item.priority).ThenBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.goalId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProfessionalIdentityData> QueryIdentitiesByPerson(string personId) => identitiesById.Values.Where(item => Same(item.personId, personId)).OrderBy(item => item.kind).ThenByDescending(item => item.importance).ThenBy(item => item.identityId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<IdentityConflictData> QueryConflictsByPerson(string personId) => conflictsById.Values.Where(item => Same(item.personId, personId)).OrderBy(item => item.conflictId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PersonGoalData> QueryGoalsByState(PersonGoalState state) => goalsById.Values.Where(item => item.state == state).OrderBy(item => item.goalId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PersonGoalData> QueryGoalsByTargetProfession(string professionId) => goalsById.Values.Where(item => Same(item.targetProfessionId, professionId)).OrderBy(item => item.goalId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public LifePathOperationResult BuildSnapshot(string personId)
        {
            long before = revision;
            if (!KnownPerson(personId))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingPerson, $"Person '{personId}' is unknown.", before);
            }

            LifePathSnapshot snapshot = new LifePathSnapshot(personId, QueryLifePathsByPerson(personId), QueryAspirationsByPerson(personId), QueryGoalsByPerson(personId), QueryIdentitiesByPerson(personId), QueryConflictsByPerson(personId), achievementSetbacksById.Values.Where(item => Same(item.personId, personId)), revision);
            return LifePathOperationResult.Success("Life-path snapshot built.", before, before, snapshot: snapshot, preview: true);
        }

        public LifePathOperationResult CreateOrUpdateLifePath(LifePathRecordData record, string transactionId)
        {
            long before = revision;
            LifePathRecordData prepared = record?.Clone();
            LifePathOperationStatus status = ValidateLifePath(prepared, out string failure);
            if (status != LifePathOperationStatus.Succeeded)
            {
                return LifePathOperationResult.Failure(status, failure, before);
            }

            bool exists = lifePathsById.TryGetValue(prepared.lifePathId, out LifePathRecordData existing);
            if (exists && LifePathEquals(existing, prepared))
            {
                return LifePathOperationResult.Success("Life-path record already exists.", before, before, lifePath: existing, duplicate: true);
            }

            if (exists)
            {
                prepared.revision = existing.revision + 1L;
            }

            lifePathsById[prepared.lifePathId] = prepared;
            MarkMutated();
            AddHook(exists ? LifePathHookKind.LifeDirectionChanged : LifePathHookKind.LifePathCreated, prepared.personId, prepared.lifePathId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, prepared.lastRevisionWorldTime, transactionId);
            return LifePathOperationResult.Success(exists ? "Life path updated." : "Life path created.", before, revision, lifePath: prepared);
        }

        public LifePathOperationResult AddAspiration(PersonAspirationData aspiration, string transactionId)
        {
            long before = revision;
            PersonAspirationData prepared = aspiration?.Clone();
            LifePathOperationStatus status = ValidateAspiration(prepared, adding: true, out string failure);
            if (status != LifePathOperationStatus.Succeeded)
            {
                return LifePathOperationResult.Failure(status, failure, before);
            }

            if (aspirationsById.TryGetValue(prepared.aspirationId, out PersonAspirationData existing))
            {
                return AspirationEquals(existing, prepared)
                    ? LifePathOperationResult.Success("Aspiration already exists.", before, before, aspiration: existing, duplicate: true)
                    : LifePathOperationResult.Failure(LifePathOperationStatus.Duplicate, $"Aspiration '{prepared.aspirationId}' already exists with different data.", before);
            }

            if (HasExclusiveAspirationConflict(prepared, out failure))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.Conflict, failure, before);
            }

            aspirationsById[prepared.aspirationId] = prepared;
            LinkAspirationToLifePath(prepared);
            MarkMutated();
            AddHook(LifePathHookKind.AspirationAdopted, prepared.personId, string.Empty, prepared.aspirationId, string.Empty, string.Empty, string.Empty, string.Empty, prepared.startWorldTime, transactionId);
            return LifePathOperationResult.Success("Aspiration added.", before, revision, aspiration: prepared);
        }

        public LifePathOperationResult SetAspirationState(string aspirationId, PersonAspirationState state, string worldTime, string reason, string transactionId)
        {
            long before = revision;
            if (!aspirationsById.TryGetValue(aspirationId ?? string.Empty, out PersonAspirationData current))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingAspiration, $"Aspiration '{aspirationId}' does not exist.", before);
            }

            if (current.state == state)
            {
                return LifePathOperationResult.Success("Aspiration already has requested state.", before, before, aspiration: current, duplicate: true);
            }

            if (IsAspirationTerminal(current.state) && !IsAspirationTerminal(state))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.InvalidTransition, "Terminal aspiration cannot return to an active state.", before);
            }

            PersonAspirationData updated = current.Clone();
            updated.state = state;
            updated.progressSummary = reason ?? updated.progressSummary;
            updated.revision++;
            updated.revisionHistory = LifePathRecordData.Clean(updated.revisionHistory.Concat(new[] { $"{worldTime ?? string.Empty}:{state}" }));
            aspirationsById[updated.aspirationId] = updated;
            LinkAspirationToLifePath(updated);
            MarkMutated();
            LifePathHookKind hook = state == PersonAspirationState.Fulfilled ? LifePathHookKind.AspirationFulfilled : state == PersonAspirationState.Abandoned ? LifePathHookKind.AspirationAbandoned : LifePathHookKind.LifeDirectionChanged;
            AddHook(hook, updated.personId, string.Empty, updated.aspirationId, string.Empty, string.Empty, string.Empty, string.Empty, worldTime, transactionId);
            return LifePathOperationResult.Success("Aspiration state changed.", before, revision, aspiration: updated);
        }

        public LifePathOperationResult ReplaceAspiration(string oldAspirationId, PersonAspirationData replacement, string worldTime, string reason, string transactionId)
        {
            long before = revision;
            LifePathOperationResult added = AddAspiration(replacement, transactionId + ".replacement");
            if (!added.Succeeded)
            {
                return added;
            }

            if (!aspirationsById.TryGetValue(oldAspirationId ?? string.Empty, out PersonAspirationData old))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingAspiration, $"Aspiration '{oldAspirationId}' does not exist.", before);
            }

            PersonAspirationData updated = old.Clone();
            updated.state = PersonAspirationState.Replaced;
            updated.replacedByAspirationId = added.Aspiration.aspirationId;
            updated.replacementReason = reason ?? string.Empty;
            updated.revision++;
            aspirationsById[updated.aspirationId] = updated;
            MarkMutated();
            return LifePathOperationResult.Success("Aspiration replaced.", before, revision, aspiration: updated);
        }

        public LifePathOperationResult AddGoal(PersonGoalData goal, string transactionId)
        {
            long before = revision;
            PersonGoalData prepared = goal?.Clone();
            LifePathOperationStatus status = ValidateGoal(prepared, adding: true, out string failure);
            if (status != LifePathOperationStatus.Succeeded)
            {
                return LifePathOperationResult.Failure(status, failure, before);
            }

            if (goalsById.TryGetValue(prepared.goalId, out PersonGoalData existing))
            {
                return GoalEquals(existing, prepared)
                    ? LifePathOperationResult.Success("Goal already exists.", before, before, goal: existing, duplicate: true)
                    : LifePathOperationResult.Failure(LifePathOperationStatus.Duplicate, $"Goal '{prepared.goalId}' already exists with different data.", before);
            }

            if (HasGoalCycle(prepared, prepared.goalId, new HashSet<string>(StringComparer.Ordinal)))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.Cycle, "Goal dependency graph contains a cycle.", before);
            }

            if (HasActiveGoalConflict(prepared, out failure))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.Conflict, failure, before);
            }

            goalsById[prepared.goalId] = prepared;
            LinkGoal(prepared);
            MarkMutated();
            AddHook(LifePathHookKind.MajorGoalBegun, prepared.personId, string.Empty, prepared.parentAspirationId, prepared.goalId, string.Empty, string.Empty, string.Empty, prepared.startWorldTime, transactionId);
            return LifePathOperationResult.Success("Goal added.", before, revision, goal: prepared);
        }

        public LifePathOperationResult SetGoalState(string goalId, PersonGoalState state, string worldTime, string reason, string transactionId)
        {
            long before = revision;
            if (!goalsById.TryGetValue(goalId ?? string.Empty, out PersonGoalData current))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingGoal, $"Goal '{goalId}' does not exist.", before);
            }

            if (current.state == state)
            {
                return LifePathOperationResult.Success("Goal already has requested state.", before, before, goal: current, duplicate: true);
            }

            if (current.Terminal && !IsGoalTerminal(state))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.InvalidTransition, "Terminal goal cannot return to active state.", before);
            }

            PersonGoalData updated = current.Clone();
            updated.state = state;
            updated.failureOrAbandonmentReason = state == PersonGoalState.Failed || state == PersonGoalState.Abandoned || state == PersonGoalState.Cancelled || state == PersonGoalState.Expired ? reason ?? string.Empty : updated.failureOrAbandonmentReason;
            updated.revision++;
            updated.revisionHistory = LifePathRecordData.Clean(updated.revisionHistory.Concat(new[] { $"{worldTime ?? string.Empty}:{state}" }));
            goalsById[updated.goalId] = updated;
            LinkGoal(updated);
            MarkMutated();
            LifePathHookKind hook = state == PersonGoalState.Failed ? LifePathHookKind.GoalFailed : LifePathHookKind.LifeDirectionChanged;
            AddHook(hook, updated.personId, string.Empty, updated.parentAspirationId, updated.goalId, string.Empty, string.Empty, string.Empty, worldTime, transactionId);
            return LifePathOperationResult.Success("Goal state changed.", before, revision, goal: updated);
        }

        public LifePathOperationResult EvaluateGoalProgress(string goalId, LifePathProjectionAudience audience = LifePathProjectionAudience.PrivilegedDebug)
        {
            long before = revision;
            if (!goalsById.TryGetValue(goalId ?? string.Empty, out PersonGoalData goal))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingGoal, $"Goal '{goalId}' does not exist.", before);
            }

            LifeGoalProgressEvaluation evaluation = EvaluateGoal(goal, audience);
            return LifePathOperationResult.Success("Goal progress evaluated.", before, before, goal: goal, progress: evaluation, preview: true);
        }

        public LifePathOperationResult CompleteGoal(string goalId, LifeGoalProgressEvaluation progress, string worldTime, string transactionId)
        {
            long before = revision;
            if (!goalsById.TryGetValue(goalId ?? string.Empty, out PersonGoalData goal))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingGoal, $"Goal '{goalId}' does not exist.", before);
            }

            LifeGoalProgressEvaluation current = EvaluateGoal(goal, LifePathProjectionAudience.PrivilegedDebug);
            if (progress == null || !progress.SemanticallyEqualsCurrent(current.RuntimeRevision, current.EvaluationHash))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.StaleProgress, "Goal progress snapshot is stale.", before);
            }

            if (!current.AuthoritativeComplete)
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingRequirement, "Authoritative requirements are not complete.", before);
            }

            PersonGoalData updated = goal.Clone();
            updated.state = PersonGoalState.Completed;
            updated.progressState = LifeGoalProgressState.Satisfied;
            updated.completedRequirementIds = current.SatisfiedRequirements.ToArray();
            updated.remainingRequirementIds = Array.Empty<string>();
            updated.blockingReasons = Array.Empty<string>();
            updated.authoritativeReferences = current.AuthoritativeReferences.Select(item => item.Clone()).ToArray();
            updated.completionWorldTime = worldTime ?? string.Empty;
            updated.progressRevision = current.RuntimeRevision;
            updated.progressHash = current.EvaluationHash;
            updated.revision++;
            updated.revisionHistory = LifePathRecordData.Clean(updated.revisionHistory.Concat(new[] { $"{worldTime ?? string.Empty}:Completed" }));
            goalsById[updated.goalId] = updated;
            LinkGoal(updated);
            MarkMutated();
            AddHook(LifePathHookKind.GoalCompleted, updated.personId, string.Empty, updated.parentAspirationId, updated.goalId, string.Empty, string.Empty, string.Empty, worldTime, transactionId);
            return LifePathOperationResult.Success("Goal completed.", before, revision, goal: updated, progress: current);
        }

        public LifePathOperationResult SetProfessionalIdentity(ProfessionalIdentityData identity, string transactionId)
        {
            long before = revision;
            ProfessionalIdentityData prepared = identity?.Clone();
            LifePathOperationStatus status = ValidateIdentity(prepared, out string failure);
            if (status != LifePathOperationStatus.Succeeded)
            {
                return LifePathOperationResult.Failure(status, failure, before);
            }

            if (identitiesById.TryGetValue(prepared.identityId, out ProfessionalIdentityData existing) && IdentityEquals(existing, prepared))
            {
                return LifePathOperationResult.Success("Professional identity already exists.", before, before, identity: existing, duplicate: true);
            }

            if (prepared.kind == ProfessionalIdentityKind.Primary)
            {
                foreach (ProfessionalIdentityData value in identitiesById.Values.Where(item => Same(item.personId, prepared.personId) && item.kind == ProfessionalIdentityKind.Primary && item.active && !Same(item.identityId, prepared.identityId)).ToArray())
                {
                    ProfessionalIdentityData demoted = value.Clone();
                    demoted.kind = ProfessionalIdentityKind.Secondary;
                    demoted.revision++;
                    identitiesById[demoted.identityId] = demoted;
                }
            }

            identitiesById[prepared.identityId] = prepared;
            LinkIdentity(prepared);
            MarkMutated();
            AddHook(LifePathHookKind.ProfessionalIdentityChanged, prepared.personId, string.Empty, string.Empty, string.Empty, prepared.identityId, string.Empty, string.Empty, prepared.startWorldTime, transactionId);
            return LifePathOperationResult.Success("Professional identity set.", before, revision, identity: prepared);
        }

        public LifePathOperationResult RecordIdentityConflict(IdentityConflictData conflict, string transactionId)
        {
            long before = revision;
            IdentityConflictData prepared = conflict?.Clone();
            LifePathOperationStatus status = ValidateConflict(prepared, out string failure);
            if (status != LifePathOperationStatus.Succeeded)
            {
                return LifePathOperationResult.Failure(status, failure, before);
            }

            if (conflictsById.TryGetValue(prepared.conflictId, out IdentityConflictData existing))
            {
                return ConflictEquals(existing, prepared)
                    ? LifePathOperationResult.Success("Identity conflict already exists.", before, before, conflict: existing, duplicate: true)
                    : LifePathOperationResult.Failure(LifePathOperationStatus.Duplicate, $"Identity conflict '{prepared.conflictId}' already exists with different data.", before);
            }

            conflictsById[prepared.conflictId] = prepared;
            MarkMutated();
            AddHook(LifePathHookKind.IdentityConflictRecorded, prepared.personId, string.Empty, string.Empty, string.Empty, string.Empty, prepared.conflictId, string.Empty, string.Empty, transactionId);
            return LifePathOperationResult.Success("Identity conflict recorded.", before, revision, conflict: prepared);
        }

        public LifePathOperationResult ResolveIdentityConflict(string conflictId, string worldTime, string transactionId)
        {
            long before = revision;
            if (!conflictsById.TryGetValue(conflictId ?? string.Empty, out IdentityConflictData conflict))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.MissingIdentity, $"Identity conflict '{conflictId}' does not exist.", before);
            }

            if (conflict.resolved)
            {
                return LifePathOperationResult.Success("Identity conflict already resolved.", before, before, conflict: conflict, duplicate: true);
            }

            IdentityConflictData updated = conflict.Clone();
            updated.resolved = true;
            updated.state = ProfessionalIdentityAlignmentState.Aligned;
            updated.resolutionWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            conflictsById[updated.conflictId] = updated;
            MarkMutated();
            AddHook(LifePathHookKind.IdentityConflictResolved, updated.personId, string.Empty, string.Empty, string.Empty, string.Empty, updated.conflictId, string.Empty, worldTime, transactionId);
            return LifePathOperationResult.Success("Identity conflict resolved.", before, revision, conflict: updated);
        }

        public LifePathOperationResult RecordAchievementOrSetback(LifePathAchievementSetbackReferenceData record, string transactionId)
        {
            long before = revision;
            LifePathAchievementSetbackReferenceData prepared = record?.Clone();
            LifePathOperationStatus status = ValidateAchievementSetback(prepared, out string failure);
            if (status != LifePathOperationStatus.Succeeded)
            {
                return LifePathOperationResult.Failure(status, failure, before);
            }

            if (achievementSetbacksById.TryGetValue(prepared.recordId, out LifePathAchievementSetbackReferenceData existing))
            {
                return AchievementEquals(existing, prepared)
                    ? LifePathOperationResult.Success("Achievement/setback already exists.", before, before, achievementSetback: existing, duplicate: true)
                    : LifePathOperationResult.Failure(LifePathOperationStatus.Duplicate, $"Achievement/setback '{prepared.recordId}' already exists with different data.", before);
            }

            if (prepared.exclusive && achievementSetbacksById.Values.Any(item => Same(item.personId, prepared.personId) && item.sourceRecordType == prepared.sourceRecordType && Same(item.sourceRecordId, prepared.sourceRecordId)))
            {
                return LifePathOperationResult.Failure(LifePathOperationStatus.Duplicate, "Exclusive achievement/setback source is already recorded.", before);
            }

            achievementSetbacksById[prepared.recordId] = prepared;
            LinkAchievementSetback(prepared);
            MarkMutated();
            LifePathHookKind hook = prepared.kind == LifePathAchievementSetbackKind.Setback || prepared.kind == LifePathAchievementSetbackKind.Dismissal || prepared.kind == LifePathAchievementSetbackKind.MajorProfessionalFailure ? LifePathHookKind.MajorSetback : LifePathHookKind.AchievementRecorded;
            AddHook(hook, prepared.personId, prepared.lifePathId, prepared.aspirationId, prepared.goalId, string.Empty, string.Empty, prepared.recordId, prepared.worldTime, transactionId);
            return LifePathOperationResult.Success("Achievement/setback recorded.", before, revision, achievementSetback: prepared);
        }

        public LifePathProjection<LifePathSnapshot> ProjectSnapshot(string personId, LifePathProjectionAudience audience, InformationAccessDecision decision = null)
        {
            LifePathOperationResult result = BuildSnapshot(personId);
            if (!result.Succeeded || decision != null && decision.Denied)
            {
                return new LifePathProjection<LifePathSnapshot>(null, audience, decision, false, true, Array.Empty<string>(), LifePathInformationSubject.ProtectedFields);
            }

            bool privileged = IsPrivileged(audience);
            IEnumerable<PersonAspirationData> aspirations = result.Snapshot.Aspirations.Where(item => privileged || !item.Secret).Select(item => RedactAspiration(item, privileged));
            IEnumerable<PersonGoalData> goals = result.Snapshot.Goals.Where(item => privileged || !item.Secret).Select(item => RedactGoal(item, privileged));
            IEnumerable<ProfessionalIdentityData> identities = result.Snapshot.Identities.Where(item => privileged || !item.Secret).Select(item => RedactIdentity(item, privileged));
            IEnumerable<IdentityConflictData> conflicts = result.Snapshot.Conflicts.Where(item => privileged || !string.Equals(item.accessPolicyId, ProfessionContentIds.AccessSecretId, StringComparison.Ordinal)).Select(item => item.Clone());
            IEnumerable<LifePathAchievementSetbackReferenceData> achievements = result.Snapshot.AchievementSetbacks.Where(item => privileged || !item.secret).Select(item => item.Clone());
            bool redacted = !privileged && (result.Snapshot.Aspirations.Any(item => item.Secret) || result.Snapshot.Goals.Any(item => item.Secret) || result.Snapshot.Identities.Any(item => item.Secret) || result.Snapshot.AchievementSetbacks.Any(item => item.secret));
            LifePathSnapshot projection = new LifePathSnapshot(personId, result.Snapshot.LifePaths, aspirations, goals, identities, conflicts, achievements, revision);
            return new LifePathProjection<LifePathSnapshot>(projection, audience, decision, redacted, false, redacted ? new[] { "public-life-path", "public-goals", "public-identity" } : Array.Empty<string>(), redacted ? LifePathInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public LifePathRuntimeSaveData CreateSaveData()
        {
            return new LifePathRuntimeSaveData
            {
                schemaVersion = LifePathRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                lifePaths = lifePathsById.Values.OrderBy(item => item.lifePathId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                aspirations = aspirationsById.Values.OrderBy(item => item.aspirationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                goals = goalsById.Values.OrderBy(item => item.goalId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                identities = identitiesById.Values.OrderBy(item => item.identityId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                conflicts = conflictsById.Values.OrderBy(item => item.conflictId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                achievementSetbacks = achievementSetbacksById.Values.OrderBy(item => item.recordId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public LifePathOperationResult RestoreFromSaveData(LifePathRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, CareerHistoryRuntime careerHistoryRuntime, IEnumerable<string> persons, IEnumerable<string> organizations, bool restoring = true)
        {
            LifePathRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry oldRegistry = registry;
            PersonProfessionRuntime oldProfessions = professions;
            TrainingRuntime oldTraining = training;
            ProfessionalActivityRuntime oldActivities = activities;
            CredentialRuntime oldCredentials = credentials;
            ProfessionalRankRuntime oldRanks = ranks;
            PositionEmploymentRuntime oldPositions = positions;
            CareerHistoryRuntime oldCareerHistory = careerHistory;
            HashSet<string> oldPersons = new HashSet<string>(knownPersonIds, StringComparer.Ordinal);
            HashSet<string> oldOrganizations = new HashSet<string>(knownOrganizationIds, StringComparer.Ordinal);
            long before = revision;

            Configure(definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, positionRuntime, careerHistoryRuntime, persons, organizations);
            if (!ValidateSaveData(saveData, registry, professions, training, activities, credentials, ranks, positions, careerHistory, knownPersonIds, knownOrganizationIds, out string failure))
            {
                RestoreFromSaveData(rollback, oldRegistry, oldProfessions, oldTraining, oldActivities, oldCredentials, oldRanks, oldPositions, oldCareerHistory, oldPersons, oldOrganizations, restoring);
                return LifePathOperationResult.Failure(LifePathOperationStatus.CorruptSave, failure, before);
            }

            lifePathsById.Clear();
            aspirationsById.Clear();
            goalsById.Clear();
            identitiesById.Clear();
            conflictsById.Clear();
            achievementSetbacksById.Clear();
            foreach (LifePathRecordData item in saveData?.lifePaths ?? new List<LifePathRecordData>()) lifePathsById[item.lifePathId] = item.Clone();
            foreach (PersonAspirationData item in saveData?.aspirations ?? new List<PersonAspirationData>()) aspirationsById[item.aspirationId] = item.Clone();
            foreach (PersonGoalData item in saveData?.goals ?? new List<PersonGoalData>()) goalsById[item.goalId] = item.Clone();
            foreach (ProfessionalIdentityData item in saveData?.identities ?? new List<ProfessionalIdentityData>()) identitiesById[item.identityId] = item.Clone();
            foreach (IdentityConflictData item in saveData?.conflicts ?? new List<IdentityConflictData>()) conflictsById[item.conflictId] = item.Clone();
            foreach (LifePathAchievementSetbackReferenceData item in saveData?.achievementSetbacks ?? new List<LifePathAchievementSetbackReferenceData>()) achievementSetbacksById[item.recordId] = item.Clone();
            historyHooks.Clear();
            revision = Math.Max(0L, saveData?.revision ?? 0L);
            dirty = !restoring;
            return LifePathOperationResult.Success("Life-path state restored.", before, revision);
        }

        public static bool ValidateSaveData(LifePathRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, CareerHistoryRuntime careerHistoryRuntime, IEnumerable<string> persons, IEnumerable<string> organizations, out string failure)
        {
            if (saveData == null)
            {
                failure = string.Empty;
                return true;
            }

            if (saveData.schemaVersion != LifePathRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported life-path schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> knownPersons = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> knownOrganizations = new HashSet<string>((organizations ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> lifePathIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> aspirationIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> goalIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> identityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LifePathRecordData item in saveData.lifePaths ?? new List<LifePathRecordData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.lifePathId) || !lifePathIds.Add(item.lifePathId) || !Known(knownPersons, item.personId))
                {
                    failure = "Life-path save data has a missing, duplicate, or unknown-person life path.";
                    return false;
                }
            }

            foreach (PersonAspirationData item in saveData.aspirations ?? new List<PersonAspirationData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.aspirationId) || !aspirationIds.Add(item.aspirationId) || !Known(knownPersons, item.personId) || !TryDefinition(definitionRegistry, item.aspirationDefinitionId, out AspirationDefinition _))
                {
                    failure = "Aspiration save data has a missing, duplicate, unknown-person, or unknown-definition aspiration.";
                    return false;
                }
            }

            foreach (PersonGoalData item in saveData.goals ?? new List<PersonGoalData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.goalId) || !goalIds.Add(item.goalId) || !Known(knownPersons, item.personId) || !TryDefinition(definitionRegistry, item.goalDefinitionId, out LifeGoalDefinition _))
                {
                    failure = "Goal save data has a missing, duplicate, unknown-person, or unknown-definition goal.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(item.parentAspirationId) && !aspirationIds.Contains(item.parentAspirationId))
                {
                    failure = $"Goal '{item.goalId}' references missing aspiration '{item.parentAspirationId}'.";
                    return false;
                }

                if (item.state == PersonGoalState.Completed && item.remainingRequirementIds.Length > 0)
                {
                    failure = $"Completed goal '{item.goalId}' still has remaining requirements.";
                    return false;
                }
            }

            foreach (ProfessionalIdentityData item in saveData.identities ?? new List<ProfessionalIdentityData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.identityId) || !identityIds.Add(item.identityId) || !Known(knownPersons, item.personId))
                {
                    failure = "Identity save data has a missing, duplicate, or unknown-person identity.";
                    return false;
                }

                if (!ValidateIdentityReferences(item, definitionRegistry, professionRuntime, careerHistoryRuntime, out failure))
                {
                    return false;
                }
            }

            if ((saveData.identities ?? new List<ProfessionalIdentityData>()).Where(item => item != null && item.active && item.kind == ProfessionalIdentityKind.Primary).GroupBy(item => item.personId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                failure = "A Person has multiple active primary professional identities.";
                return false;
            }

            foreach (IdentityConflictData item in saveData.conflicts ?? new List<IdentityConflictData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.conflictId) || !Known(knownPersons, item.personId) || item.identityIds.Any(id => !identityIds.Contains(id)) || item.goalIds.Any(id => !goalIds.Contains(id)) || item.aspirationIds.Any(id => !aspirationIds.Contains(id)))
                {
                    failure = "Identity conflict save data references missing Person, identity, goal, or aspiration.";
                    return false;
                }
            }

            foreach (LifePathAchievementSetbackReferenceData item in saveData.achievementSetbacks ?? new List<LifePathAchievementSetbackReferenceData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.recordId) || !Known(knownPersons, item.personId) || !string.IsNullOrWhiteSpace(item.lifePathId) && !lifePathIds.Contains(item.lifePathId) || !string.IsNullOrWhiteSpace(item.aspirationId) && !aspirationIds.Contains(item.aspirationId) || !string.IsNullOrWhiteSpace(item.goalId) && !goalIds.Contains(item.goalId))
                {
                    failure = "Achievement/setback save data references missing Person, life path, aspiration, or goal.";
                    return false;
                }

                if (!ValidateSource(new LifePathSourceReferenceData { recordType = item.sourceRecordType, recordId = item.sourceRecordId }, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, positionRuntime, careerHistoryRuntime, out failure))
                {
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private LifeGoalProgressEvaluation EvaluateGoal(PersonGoalData goal, LifePathProjectionAudience audience)
        {
            LifeGoalDefinition definition = TryDefinition(registry, goal.goalDefinitionId, out LifeGoalDefinition found) ? found : null;
            List<string> satisfied = new List<string>();
            List<string> remaining = new List<string>();
            List<string> blocking = new List<string>();
            List<LifePathSourceReferenceData> references = new List<LifePathSourceReferenceData>();
            bool targetSatisfied = GoalTargetSatisfied(goal, definition, references);
            if (targetSatisfied)
            {
                satisfied.Add("target.authoritative");
            }
            else
            {
                remaining.Add("target.authoritative");
            }

            foreach (string dependencyId in goal.dependencyGoalIds)
            {
                if (goalsById.TryGetValue(dependencyId, out PersonGoalData dependency) && dependency.state == PersonGoalState.Completed)
                {
                    satisfied.Add($"dependency:{dependencyId}");
                }
                else
                {
                    remaining.Add($"dependency:{dependencyId}");
                }
            }

            if (goal.alternativeGoalIds.Any(id => goalsById.TryGetValue(id, out PersonGoalData alternative) && alternative.state == PersonGoalState.Completed))
            {
                satisfied.Add("alternative.completed");
            }

            if (HasActiveGoalConflict(goal, out string conflict))
            {
                blocking.Add(conflict);
            }

            bool complete = targetSatisfied && remaining.Count == 0 && blocking.Count == 0;
            bool perceived = complete || goal.progressState == LifeGoalProgressState.Satisfied || goal.completedRequirementIds.Contains("perceived.complete");
            int total = Math.Max(1, satisfied.Count + remaining.Count + blocking.Count);
            int percent = complete ? 1000 : Math.Max(0, satisfied.Count * 1000 / total);
            LifeGoalProgressState state = complete ? LifeGoalProgressState.Satisfied : blocking.Count > 0 ? LifeGoalProgressState.Blocked : satisfied.Count > 0 ? LifeGoalProgressState.InProgress : LifeGoalProgressState.NotStarted;
            bool redacted = !IsPrivileged(audience) && goal.Secret;
            string hash = BuildProgressHash(goal, targetSatisfied, satisfied, remaining, blocking);
            return new LifeGoalProgressEvaluation(goal.goalId, goal.personId, redacted ? LifeGoalProgressState.Redacted : state, redacted ? 0 : percent, redacted ? Array.Empty<string>() : satisfied, redacted ? Array.Empty<string>() : remaining, redacted ? new[] { "redacted" } : blocking, Array.Empty<string>(), redacted ? Array.Empty<LifePathSourceReferenceData>() : references, complete, perceived, revision + ExternalRevision(), hash, redacted);
        }

        private bool GoalTargetSatisfied(PersonGoalData goal, LifeGoalDefinition definition, List<LifePathSourceReferenceData> references)
        {
            string professionId = FirstNonEmpty(goal.targetProfessionId, definition?.RequiredProfessionIds.FirstOrDefault());
            string trainingId = FirstNonEmpty(goal.targetTrainingProgramId, definition?.RequiredTrainingProgramIds.FirstOrDefault());
            string credentialId = FirstNonEmpty(goal.targetCredentialDefinitionId, definition?.RequiredCredentialDefinitionIds.FirstOrDefault());
            string rankId = FirstNonEmpty(goal.targetRankDefinitionId, definition?.RequiredRankDefinitionIds.FirstOrDefault());
            string positionId = FirstNonEmpty(goal.targetPositionDefinitionId, definition?.RequiredPositionDefinitionIds.FirstOrDefault());
            string activityId = FirstNonEmpty(goal.targetActivityDefinitionId, definition?.RequiredActivityDefinitionIds.FirstOrDefault());
            switch (definition?.Category ?? goalCategoryFromTarget(goal))
            {
                case LifeGoalCategory.EnterProfession:
                    return !string.IsNullOrWhiteSpace(professionId) && professions != null && professions.QueryByProfession(professionId, activeOnly: true).Any(item => Same(item.PersonId, goal.personId) && AddRef(references, CareerTransitionSourceRecordType.ProfessionRelationship, item.RelationshipId, item.Revision));
                case LifeGoalCategory.CompleteProgram:
                case LifeGoalCategory.CompleteApprenticeship:
                    return !string.IsNullOrWhiteSpace(trainingId) && training != null && training.QueryByProgram(trainingId).Any(item => Same(item.PersonId, goal.personId) && item.State == TrainingEnrollmentState.Completed && AddRef(references, CareerTransitionSourceRecordType.TrainingEnrollment, item.EnrollmentId, item.Revision));
                case LifeGoalCategory.EarnCredential:
                    return !string.IsNullOrWhiteSpace(credentialId) && credentials != null && credentials.QueryByRecipient(goal.personId, activeOnly: true).Any(item => Same(item.credentialDefinitionId, credentialId) && AddRef(references, CareerTransitionSourceRecordType.Credential, item.credentialId, item.revision));
                case LifeGoalCategory.ReachRank:
                case LifeGoalCategory.GainMastery:
                    return !string.IsNullOrWhiteSpace(rankId) && ranks != null && ranks.QueryByPerson(goal.personId, currentOnly: true).Any(item => Same(item.rankDefinitionId, rankId) && AddRef(references, CareerTransitionSourceRecordType.Rank, item.rankRecordId, item.revision));
                case LifeGoalCategory.ObtainPosition:
                    return !string.IsNullOrWhiteSpace(positionId) && positions != null && positions.QueryEmploymentByPerson(goal.personId, activeOnly: true).Any(item => (Same(item.positionDefinitionId, positionId) || Same(item.positionInstanceId, positionId)) && AddRef(references, CareerTransitionSourceRecordType.Employment, item.employmentId, item.revision));
                case LifeGoalCategory.GainExperience:
                case LifeGoalCategory.CompleteWorkOrder:
                case LifeGoalCategory.ProduceQualifyingItem:
                case LifeGoalCategory.TeachLearner:
                case LifeGoalCategory.MakeDiscovery:
                    return FindValidatedActivity(goal.personId, activityId, references);
                case LifeGoalCategory.ResumeCareer:
                case LifeGoalCategory.Retire:
                    return !string.IsNullOrWhiteSpace(goal.targetCareerTransitionId) && careerHistory != null && careerHistory.TryGetTransition(goal.targetCareerTransitionId, out CareerTransitionRecordData transition) && Same(transition.personId, goal.personId) && AddRef(references, CareerTransitionSourceRecordType.CareerTransition, transition.transitionId, transition.revision);
                default:
                    return goal.authoritativeReferences.Length > 0 && goal.authoritativeReferences.All(item => ValidateSource(item, professions, training, activities, credentials, ranks, positions, careerHistory, out _));
            }
        }

        private LifeGoalCategory goalCategoryFromTarget(PersonGoalData goal)
        {
            return goal.targetSubjectType switch
            {
                LifePathTargetSubjectType.Profession => LifeGoalCategory.EnterProfession,
                LifePathTargetSubjectType.TrainingProgram => LifeGoalCategory.CompleteProgram,
                LifePathTargetSubjectType.Credential => LifeGoalCategory.EarnCredential,
                LifePathTargetSubjectType.Rank => LifeGoalCategory.ReachRank,
                LifePathTargetSubjectType.Position => LifeGoalCategory.ObtainPosition,
                LifePathTargetSubjectType.ProfessionalActivity => LifeGoalCategory.GainExperience,
                LifePathTargetSubjectType.Discovery => LifeGoalCategory.MakeDiscovery,
                _ => LifeGoalCategory.Custom
            };
        }

        private bool FindValidatedActivity(string personId, string activityDefinitionId, List<LifePathSourceReferenceData> references)
        {
            if (string.IsNullOrWhiteSpace(activityDefinitionId) || activities == null)
            {
                return false;
            }

            ProfessionalActivityRecordData activity = activities.CreateSaveData().activities
                .Where(item => item != null
                    && Same(item.personId, personId)
                    && Same(item.activityDefinitionId, activityDefinitionId)
                    && item.state == ProfessionalActivityState.Validated
                    && item.outcome != ProfessionalActivityOutcomeState.Failed)
                .OrderByDescending(item => item.revision)
                .ThenBy(item => item.activityId ?? string.Empty, StringComparer.Ordinal)
                .FirstOrDefault();
            return activity != null && AddRef(references, CareerTransitionSourceRecordType.ProfessionalActivity, activity.activityId, activity.revision);
        }

        private LifePathOperationStatus ValidateLifePath(LifePathRecordData record, out string failure)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.lifePathId) || string.IsNullOrWhiteSpace(record.personId))
            {
                failure = "Life-path ID and Person ID are required.";
                return LifePathOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(record.personId))
            {
                failure = $"Person '{record.personId}' is unknown.";
                return LifePathOperationStatus.MissingPerson;
            }

            failure = string.Empty;
            return LifePathOperationStatus.Succeeded;
        }

        private LifePathOperationStatus ValidateAspiration(PersonAspirationData aspiration, bool adding, out string failure)
        {
            if (aspiration == null || string.IsNullOrWhiteSpace(aspiration.aspirationId) || string.IsNullOrWhiteSpace(aspiration.personId) || string.IsNullOrWhiteSpace(aspiration.aspirationDefinitionId))
            {
                failure = "Aspiration ID, Person ID, and definition ID are required.";
                return LifePathOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(aspiration.personId))
            {
                failure = $"Person '{aspiration.personId}' is unknown.";
                return LifePathOperationStatus.MissingPerson;
            }

            if (!TryDefinition(registry, aspiration.aspirationDefinitionId, out AspirationDefinition definition))
            {
                failure = $"Aspiration definition '{aspiration.aspirationDefinitionId}' is missing.";
                return LifePathOperationStatus.MissingDefinition;
            }

            if (aspiration.state == PersonAspirationState.Fulfilled && aspiration.relatedGoalIds.Any(id => !goalsById.TryGetValue(id, out PersonGoalData goal) || goal.state != PersonGoalState.Completed))
            {
                failure = "Fulfilled aspiration has incomplete required goals.";
                return LifePathOperationStatus.MissingRequirement;
            }

            if (!ValidateTargetReferences(aspiration.targetProfessionId, aspiration.targetSpecializationId, aspiration.targetRankDefinitionId, aspiration.targetCredentialDefinitionId, aspiration.targetPositionDefinitionId, out failure))
            {
                return LifePathOperationStatus.MissingDefinition;
            }

            if (aspiration.Secret && !definition.SecretAllowed && string.Equals(aspiration.accessPolicyId, ProfessionContentIds.AccessSecretId, StringComparison.Ordinal))
            {
                failure = "Aspiration definition does not allow secret records.";
                return LifePathOperationStatus.InvalidRequest;
            }

            failure = string.Empty;
            return LifePathOperationStatus.Succeeded;
        }

        private LifePathOperationStatus ValidateGoal(PersonGoalData goal, bool adding, out string failure)
        {
            if (goal == null || string.IsNullOrWhiteSpace(goal.goalId) || string.IsNullOrWhiteSpace(goal.personId) || string.IsNullOrWhiteSpace(goal.goalDefinitionId))
            {
                failure = "Goal ID, Person ID, and definition ID are required.";
                return LifePathOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(goal.personId))
            {
                failure = $"Person '{goal.personId}' is unknown.";
                return LifePathOperationStatus.MissingPerson;
            }

            if (!TryDefinition(registry, goal.goalDefinitionId, out LifeGoalDefinition definition))
            {
                failure = $"Goal definition '{goal.goalDefinitionId}' is missing.";
                return LifePathOperationStatus.MissingDefinition;
            }

            if (!string.IsNullOrWhiteSpace(goal.parentAspirationId) && (!aspirationsById.TryGetValue(goal.parentAspirationId, out PersonAspirationData aspiration) || !Same(aspiration.personId, goal.personId)))
            {
                failure = "Goal references a missing or wrong-Person aspiration.";
                return LifePathOperationStatus.MissingAspiration;
            }

            if (goal.dependencyGoalIds.Any(id => !goalsById.ContainsKey(id)) || goal.alternativeGoalIds.Any(id => !goalsById.ContainsKey(id)))
            {
                failure = "Goal references a missing dependency or alternative goal.";
                return LifePathOperationStatus.MissingGoal;
            }

            if (!ValidateTargetReferences(goal.targetProfessionId, string.Empty, goal.targetRankDefinitionId, goal.targetCredentialDefinitionId, goal.targetPositionDefinitionId, out failure))
            {
                return LifePathOperationStatus.MissingDefinition;
            }

            if (!string.IsNullOrWhiteSpace(goal.targetTrainingProgramId) && !TryDefinition(registry, goal.targetTrainingProgramId, out TrainingProgramDefinition _))
            {
                failure = $"Training program '{goal.targetTrainingProgramId}' is missing.";
                return LifePathOperationStatus.MissingDefinition;
            }

            if (!string.IsNullOrWhiteSpace(goal.deadlineFoundationId) && string.CompareOrdinal(goal.deadlineFoundationId, goal.startWorldTime ?? string.Empty) < 0)
            {
                failure = "Goal deadline is before the start time.";
                return LifePathOperationStatus.InvalidDateRange;
            }

            if (goal.state == PersonGoalState.Completed && goal.remainingRequirementIds.Length > 0)
            {
                failure = "Completed goal still has remaining requirements.";
                return LifePathOperationStatus.InvalidState;
            }

            failure = string.Empty;
            return LifePathOperationStatus.Succeeded;
        }

        private LifePathOperationStatus ValidateIdentity(ProfessionalIdentityData identity, out string failure)
        {
            if (identity == null || string.IsNullOrWhiteSpace(identity.identityId) || string.IsNullOrWhiteSpace(identity.personId) || string.IsNullOrWhiteSpace(identity.professionId))
            {
                failure = "Identity ID, Person ID, and Profession ID are required.";
                return LifePathOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(identity.personId))
            {
                failure = $"Person '{identity.personId}' is unknown.";
                return LifePathOperationStatus.MissingPerson;
            }

            if (!ValidateIdentityReferences(identity, registry, professions, careerHistory, out failure))
            {
                return LifePathOperationStatus.MissingDefinition;
            }

            if (identity.kind == ProfessionalIdentityKind.Retired && identity.active && identity.alignment != ProfessionalIdentityAlignmentState.Aspirational && !CareerHistoryRequirementAdapters.IsRetired(careerHistory, identity.personId))
            {
                failure = "Active retired identity requires a retirement foundation or aspirational alignment.";
                return LifePathOperationStatus.InvalidState;
            }

            failure = string.Empty;
            return LifePathOperationStatus.Succeeded;
        }

        private LifePathOperationStatus ValidateConflict(IdentityConflictData conflict, out string failure)
        {
            if (conflict == null || string.IsNullOrWhiteSpace(conflict.conflictId) || string.IsNullOrWhiteSpace(conflict.personId))
            {
                failure = "Conflict ID and Person ID are required.";
                return LifePathOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(conflict.personId) || conflict.identityIds.Any(id => !identitiesById.ContainsKey(id)) || conflict.goalIds.Any(id => !goalsById.ContainsKey(id)) || conflict.aspirationIds.Any(id => !aspirationsById.ContainsKey(id)))
            {
                failure = "Conflict references unknown Person, identity, aspiration, or goal.";
                return LifePathOperationStatus.MissingSourceRecord;
            }

            failure = string.Empty;
            return LifePathOperationStatus.Succeeded;
        }

        private LifePathOperationStatus ValidateAchievementSetback(LifePathAchievementSetbackReferenceData record, out string failure)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.recordId) || string.IsNullOrWhiteSpace(record.personId) || string.IsNullOrWhiteSpace(record.sourceRecordId))
            {
                failure = "Achievement/setback ID, Person ID, and source record are required.";
                return LifePathOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(record.personId))
            {
                failure = $"Person '{record.personId}' is unknown.";
                return LifePathOperationStatus.MissingPerson;
            }

            if (!ValidateSource(new LifePathSourceReferenceData { recordType = record.sourceRecordType, recordId = record.sourceRecordId }, professions, training, activities, credentials, ranks, positions, careerHistory, out failure))
            {
                return LifePathOperationStatus.MissingSourceRecord;
            }

            failure = string.Empty;
            return LifePathOperationStatus.Succeeded;
        }

        private bool ValidateTargetReferences(string professionId, string specializationId, string rankId, string credentialId, string positionId, out string failure)
        {
            if (!string.IsNullOrWhiteSpace(professionId) && !TryDefinition(registry, professionId, out ProfessionDefinition _))
            {
                failure = $"Profession '{professionId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(specializationId) && !TryDefinition(registry, specializationId, out ProfessionSpecializationDefinition _))
            {
                failure = $"Specialization '{specializationId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(rankId) && !TryDefinition(registry, rankId, out ProfessionalRankDefinition _))
            {
                failure = $"Rank '{rankId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(credentialId) && !TryDefinition(registry, credentialId, out CredentialDefinition _))
            {
                failure = $"Credential '{credentialId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(positionId) && !TryDefinition(registry, positionId, out PositionDefinition _))
            {
                failure = $"Position '{positionId}' is missing.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateIdentityReferences(ProfessionalIdentityData identity, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, CareerHistoryRuntime careerHistoryRuntime, out string failure)
        {
            if (!string.IsNullOrWhiteSpace(identity.professionId) && !TryDefinition(definitionRegistry, identity.professionId, out ProfessionDefinition _))
            {
                failure = $"Profession '{identity.professionId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(identity.professionRelationshipId) && (professionRuntime == null || !professionRuntime.TryGetSnapshot(identity.professionRelationshipId, out PersonProfessionSnapshot _)))
            {
                failure = $"Profession relationship '{identity.professionRelationshipId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(identity.careerEpisodeId) && (careerHistoryRuntime == null || !careerHistoryRuntime.TryGetEpisode(identity.careerEpisodeId, out CareerEpisodeData _)))
            {
                failure = $"Career episode '{identity.careerEpisodeId}' is missing.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateSource(LifePathSourceReferenceData source, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, CareerHistoryRuntime careerHistoryRuntime, out string failure)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.recordId))
            {
                failure = string.Empty;
                return true;
            }

            bool exists = source.recordType switch
            {
                CareerTransitionSourceRecordType.ProfessionRelationship => professionRuntime != null && professionRuntime.TryGetSnapshot(source.recordId, out PersonProfessionSnapshot _),
                CareerTransitionSourceRecordType.Employment => positionRuntime != null && positionRuntime.TryGetEmployment(source.recordId, out EmploymentRecordData _),
                CareerTransitionSourceRecordType.Position => positionRuntime != null && positionRuntime.TryGetPosition(source.recordId, out PositionInstanceData _),
                CareerTransitionSourceRecordType.Rank => rankRuntime != null && rankRuntime.TryGetRank(source.recordId, out ProfessionalRankRecordData _),
                CareerTransitionSourceRecordType.Credential => credentialRuntime != null && credentialRuntime.TryGetCredential(source.recordId, out CredentialRecordData _),
                CareerTransitionSourceRecordType.TrainingEnrollment => trainingRuntime != null && trainingRuntime.TryGetEnrollment(source.recordId, out TrainingEnrollmentSnapshot _),
                CareerTransitionSourceRecordType.ProfessionalActivity => activityRuntime != null && activityRuntime.TryGetActivity(source.recordId, out ProfessionalActivityRecordData _),
                CareerTransitionSourceRecordType.ExperienceEvidence => activityRuntime != null && activityRuntime.TryGetEvidence(source.recordId, out ProfessionalExperienceEvidenceData _),
                CareerTransitionSourceRecordType.CareerEpisode => careerHistoryRuntime != null && careerHistoryRuntime.TryGetEpisode(source.recordId, out CareerEpisodeData _),
                CareerTransitionSourceRecordType.CareerTransition => careerHistoryRuntime != null && careerHistoryRuntime.TryGetTransition(source.recordId, out CareerTransitionRecordData _),
                CareerTransitionSourceRecordType.HistoricalRecordFoundation or CareerTransitionSourceRecordType.Custom => true,
                _ => true
            };

            failure = exists ? string.Empty : $"Missing source record '{source.recordType}:{source.recordId}'.";
            return exists;
        }

        private bool HasExclusiveAspirationConflict(PersonAspirationData candidate, out string failure)
        {
            AspirationDefinition definition = TryDefinition(registry, candidate.aspirationDefinitionId, out AspirationDefinition found) ? found : null;
            string[] tags = LifePathRecordData.Clean((definition?.ConflictTags ?? Array.Empty<string>()).Concat(candidate.motivationTags ?? Array.Empty<string>()));
            if (tags.Length == 0)
            {
                failure = string.Empty;
                return false;
            }

            foreach (PersonAspirationData active in aspirationsById.Values.Where(item => Same(item.personId, candidate.personId) && item.ActiveLike))
            {
                AspirationDefinition otherDefinition = TryDefinition(registry, active.aspirationDefinitionId, out AspirationDefinition other) ? other : null;
                string[] otherTags = LifePathRecordData.Clean((otherDefinition?.ConflictTags ?? Array.Empty<string>()).Concat(active.motivationTags ?? Array.Empty<string>()));
                if (tags.Intersect(otherTags, StringComparer.Ordinal).Any())
                {
                    failure = $"Active aspiration conflict on tag '{tags.Intersect(otherTags, StringComparer.Ordinal).First()}'.";
                    return true;
                }
            }

            failure = string.Empty;
            return false;
        }

        private bool HasActiveGoalConflict(PersonGoalData candidate, out string failure)
        {
            foreach (PersonGoalData active in goalsById.Values.Where(item => Same(item.personId, candidate.personId) && !item.Terminal && !Same(item.goalId, candidate.goalId)))
            {
                if (candidate.conflictTags.Intersect(active.conflictTags, StringComparer.Ordinal).Any())
                {
                    failure = $"Active goal conflict on tag '{candidate.conflictTags.Intersect(active.conflictTags, StringComparer.Ordinal).First()}'.";
                    return true;
                }
            }

            failure = string.Empty;
            return false;
        }

        private bool HasGoalCycle(PersonGoalData candidate, string root, HashSet<string> visiting)
        {
            if (!visiting.Add(candidate.goalId))
            {
                return true;
            }

            foreach (string dependency in candidate.dependencyGoalIds)
            {
                if (Same(dependency, root) || goalsById.TryGetValue(dependency, out PersonGoalData goal) && HasGoalCycle(goal, root, visiting))
                {
                    return true;
                }
            }

            visiting.Remove(candidate.goalId);
            return false;
        }

        private void LinkAspirationToLifePath(PersonAspirationData aspiration)
        {
            foreach (LifePathRecordData path in lifePathsById.Values.Where(item => Same(item.personId, aspiration.personId)).ToArray())
            {
                LifePathRecordData updated = path.Clone();
                updated.activeAspirationIds = aspiration.ActiveLike
                    ? LifePathRecordData.Clean(updated.activeAspirationIds.Concat(new[] { aspiration.aspirationId }))
                    : LifePathRecordData.Clean(updated.activeAspirationIds.Where(id => !Same(id, aspiration.aspirationId)));
                updated.revision++;
                lifePathsById[updated.lifePathId] = updated;
            }
        }

        private void LinkGoal(PersonGoalData goal)
        {
            if (!string.IsNullOrWhiteSpace(goal.parentAspirationId) && aspirationsById.TryGetValue(goal.parentAspirationId, out PersonAspirationData aspiration))
            {
                PersonAspirationData updated = aspiration.Clone();
                updated.relatedGoalIds = LifePathRecordData.Clean(updated.relatedGoalIds.Concat(new[] { goal.goalId }));
                updated.revision++;
                aspirationsById[updated.aspirationId] = updated;
            }

            foreach (LifePathRecordData path in lifePathsById.Values.Where(item => Same(item.personId, goal.personId)).ToArray())
            {
                LifePathRecordData updated = path.Clone();
                updated.activeGoalIds = goal.Terminal
                    ? LifePathRecordData.Clean(updated.activeGoalIds.Where(id => !Same(id, goal.goalId)))
                    : LifePathRecordData.Clean(updated.activeGoalIds.Concat(new[] { goal.goalId }));
                updated.revision++;
                lifePathsById[updated.lifePathId] = updated;
            }
        }

        private void LinkIdentity(ProfessionalIdentityData identity)
        {
            foreach (LifePathRecordData path in lifePathsById.Values.Where(item => Same(item.personId, identity.personId)).ToArray())
            {
                LifePathRecordData updated = path.Clone();
                if (identity.kind == ProfessionalIdentityKind.Primary && identity.active)
                {
                    updated.primaryProfessionalIdentityId = identity.identityId;
                }
                else if (identity.active)
                {
                    updated.secondaryProfessionalIdentityIds = LifePathRecordData.Clean(updated.secondaryProfessionalIdentityIds.Concat(new[] { identity.identityId }));
                }

                updated.revision++;
                lifePathsById[updated.lifePathId] = updated;
            }
        }

        private void LinkAchievementSetback(LifePathAchievementSetbackReferenceData record)
        {
            if (!string.IsNullOrWhiteSpace(record.lifePathId) && lifePathsById.TryGetValue(record.lifePathId, out LifePathRecordData path))
            {
                LifePathRecordData updated = path.Clone();
                updated.achievementAndSetbackIds = LifePathRecordData.Clean(updated.achievementAndSetbackIds.Concat(new[] { record.recordId }));
                updated.revision++;
                lifePathsById[updated.lifePathId] = updated;
            }
        }

        private PersonAspirationData RedactAspiration(PersonAspirationData source, bool privileged)
        {
            PersonAspirationData result = source.Clone();
            if (!privileged && result.Secret)
            {
                result.targetProfessionId = string.Empty;
                result.targetCredentialDefinitionId = string.Empty;
                result.targetRankDefinitionId = string.Empty;
                result.targetPositionDefinitionId = string.Empty;
                result.motivationTags = Array.Empty<string>();
                result.progressSummary = "Redacted";
            }

            return result;
        }

        private PersonGoalData RedactGoal(PersonGoalData source, bool privileged)
        {
            PersonGoalData result = source.Clone();
            if (!privileged && result.Secret)
            {
                result.targetProfessionId = string.Empty;
                result.targetCredentialDefinitionId = string.Empty;
                result.targetRankDefinitionId = string.Empty;
                result.targetPositionDefinitionId = string.Empty;
                result.completedRequirementIds = Array.Empty<string>();
                result.remainingRequirementIds = Array.Empty<string>();
                result.blockingReasons = Array.Empty<string>();
                result.authoritativeReferences = Array.Empty<LifePathSourceReferenceData>();
            }

            return result;
        }

        private ProfessionalIdentityData RedactIdentity(ProfessionalIdentityData source, bool privileged)
        {
            ProfessionalIdentityData result = source.Clone();
            if (!privileged && result.Secret)
            {
                result.professionId = string.Empty;
                result.specializationId = string.Empty;
                result.professionRelationshipId = string.Empty;
                result.beliefId = string.Empty;
                result.publicIdentityFoundationId = "redacted";
            }

            return result;
        }

        private static bool TryDefinition<T>(DefinitionRegistry registry, string id, out T definition) where T : class, IGameDefinition
        {
            if (!string.IsNullOrWhiteSpace(id) && registry != null && registry.TryGet(id, out T found))
            {
                definition = found;
                return true;
            }

            definition = null;
            return false;
        }

        private bool KnownPerson(string personId) => Known(knownPersonIds, personId);
        private static bool Known(HashSet<string> set, string value) => !string.IsNullOrWhiteSpace(value) && (set == null || set.Count == 0 || set.Contains(value));
        private static bool Same(string left, string right) => string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
        private static bool IsPrivileged(LifePathProjectionAudience audience) => audience == LifePathProjectionAudience.SubjectPerson || audience == LifePathProjectionAudience.Mentor || audience == LifePathProjectionAudience.Instructor || audience == LifePathProjectionAudience.EmployerFoundation || audience == LifePathProjectionAudience.Organization || audience == LifePathProjectionAudience.PrivilegedDebug;
        private static bool IsAspirationTerminal(PersonAspirationState state) => state == PersonAspirationState.Fulfilled || state == PersonAspirationState.Failed || state == PersonAspirationState.Abandoned || state == PersonAspirationState.Replaced || state == PersonAspirationState.Superseded;
        private static bool IsGoalTerminal(PersonGoalState state) => state == PersonGoalState.Completed || state == PersonGoalState.Failed || state == PersonGoalState.Abandoned || state == PersonGoalState.Replaced || state == PersonGoalState.Cancelled || state == PersonGoalState.Expired;
        private static string FirstNonEmpty(string first, string second) => !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        private static bool AddRef(List<LifePathSourceReferenceData> references, CareerTransitionSourceRecordType type, string id, long rev) { references.Add(new LifePathSourceReferenceData { recordType = type, recordId = id, sourceRevision = rev }); return true; }
        private long ExternalRevision() => (professions?.Revision ?? 0L) + (training?.Revision ?? 0L) + (activities?.Revision ?? 0L) + (credentials?.Revision ?? 0L) + (ranks?.Revision ?? 0L) + (positions?.Revision ?? 0L) + (careerHistory?.Revision ?? 0L);
        private string BuildProgressHash(PersonGoalData goal, bool targetSatisfied, IEnumerable<string> satisfied, IEnumerable<string> remaining, IEnumerable<string> blocking) => $"{goal.goalId}|{goal.goalDefinitionId}|{targetSatisfied}|{string.Join(",", LifePathRecordData.Clean(satisfied))}|{string.Join(",", LifePathRecordData.Clean(remaining))}|{string.Join(",", LifePathRecordData.Clean(blocking))}|{revision}|{ExternalRevision()}";
        private void MarkMutated() { revision++; dirty = true; }
        private void AddHook(LifePathHookKind kind, string personId, string lifePathId, string aspirationId, string goalId, string identityId, string conflictId, string recordId, string worldTime, string transactionId) => historyHooks.Add(new LifePathHookData { kind = kind, personId = personId ?? string.Empty, lifePathId = lifePathId ?? string.Empty, aspirationId = aspirationId ?? string.Empty, goalId = goalId ?? string.Empty, identityId = identityId ?? string.Empty, conflictId = conflictId ?? string.Empty, recordId = recordId ?? string.Empty, worldTime = worldTime ?? string.Empty, transactionId = transactionId ?? string.Empty });
        private static bool LifePathEquals(LifePathRecordData a, LifePathRecordData b)
        {
            return a != null && b != null
                && Same(a.lifePathId, b.lifePathId)
                && Same(a.personId, b.personId)
                && a.state == b.state
                && FormativeReferencesEqual(a.formativeReferences, b.formativeReferences)
                && SourceReferencesEqual(a.educationAndTrainingReferences, b.educationAndTrainingReferences)
                && StringArraysEqual(a.professionRelationshipIds, b.professionRelationshipIds)
                && StringArraysEqual(a.professionIds, b.professionIds)
                && StringArraysEqual(a.specializationIds, b.specializationIds)
                && StringArraysEqual(a.careerEpisodeIds, b.careerEpisodeIds)
                && StringArraysEqual(a.credentialIds, b.credentialIds)
                && StringArraysEqual(a.rankRecordIds, b.rankRecordIds)
                && StringArraysEqual(a.positionInstanceIds, b.positionInstanceIds)
                && StringArraysEqual(a.employmentIds, b.employmentIds)
                && StringArraysEqual(a.achievementAndSetbackIds, b.achievementAndSetbackIds)
                && StringArraysEqual(a.activeAspirationIds, b.activeAspirationIds)
                && StringArraysEqual(a.activeGoalIds, b.activeGoalIds)
                && Same(a.primaryProfessionalIdentityId, b.primaryProfessionalIdentityId)
                && StringArraysEqual(a.secondaryProfessionalIdentityIds, b.secondaryProfessionalIdentityIds)
                && Same(a.startWorldTime, b.startWorldTime)
                && Same(a.lastRevisionWorldTime, b.lastRevisionWorldTime)
                && Same(a.accessPolicyId, b.accessPolicyId)
                && Same(a.provenance, b.provenance)
                && StringArraysEqual(a.revisionHistory, b.revisionHistory);
        }

        private static bool StringArraysEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            return LifePathRecordData.Clean(left).SequenceEqual(LifePathRecordData.Clean(right), StringComparer.Ordinal);
        }

        private static bool SourceReferencesEqual(IEnumerable<LifePathSourceReferenceData> left, IEnumerable<LifePathSourceReferenceData> right)
        {
            return LifePathRecordData.CleanSources(left).Select(item => item.Signature)
                .SequenceEqual(LifePathRecordData.CleanSources(right).Select(item => item.Signature), StringComparer.Ordinal);
        }

        private static bool FormativeReferencesEqual(IEnumerable<FormativeReferenceData> left, IEnumerable<FormativeReferenceData> right)
        {
            FormativeReferenceData[] leftItems = LifePathRecordData.CleanFormative(left);
            FormativeReferenceData[] rightItems = LifePathRecordData.CleanFormative(right);
            return leftItems.Length == rightItems.Length
                && leftItems.Zip(rightItems, (a, b) => Same(a.referenceId, b.referenceId)
                    && a.kind == b.kind
                    && Same(a.subjectId, b.subjectId)
                    && Same(a.description, b.description)
                    && Same(a.worldTime, b.worldTime)
                    && a.weight == b.weight
                    && Same(a.accessPolicyId, b.accessPolicyId)
                    && Same(a.provenance, b.provenance)).All(equal => equal);
        }
        private static bool AspirationEquals(PersonAspirationData a, PersonAspirationData b) => a != null && b != null && Same(a.aspirationId, b.aspirationId) && Same(a.personId, b.personId) && Same(a.aspirationDefinitionId, b.aspirationDefinitionId) && a.state == b.state && Same(a.targetProfessionId, b.targetProfessionId);
        private static bool GoalEquals(PersonGoalData a, PersonGoalData b) => a != null && b != null && Same(a.goalId, b.goalId) && Same(a.personId, b.personId) && Same(a.goalDefinitionId, b.goalDefinitionId) && a.state == b.state && Same(a.parentAspirationId, b.parentAspirationId);
        private static bool IdentityEquals(ProfessionalIdentityData a, ProfessionalIdentityData b) => a != null && b != null && Same(a.identityId, b.identityId) && Same(a.personId, b.personId) && a.kind == b.kind && Same(a.professionId, b.professionId) && a.alignment == b.alignment;
        private static bool ConflictEquals(IdentityConflictData a, IdentityConflictData b) => a != null && b != null && Same(a.conflictId, b.conflictId) && Same(a.personId, b.personId) && a.identityIds.SequenceEqual(b.identityIds) && a.resolved == b.resolved;
        private static bool AchievementEquals(LifePathAchievementSetbackReferenceData a, LifePathAchievementSetbackReferenceData b) => a != null && b != null && Same(a.recordId, b.recordId) && Same(a.personId, b.personId) && a.kind == b.kind && Same(a.sourceRecordId, b.sourceRecordId) && a.sourceRecordType == b.sourceRecordType;
    }
}
