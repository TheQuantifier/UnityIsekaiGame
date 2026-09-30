using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    public sealed class CareerHistoryRuntime
    {
        private readonly Dictionary<string, CareerEpisodeData> episodesById = new Dictionary<string, CareerEpisodeData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerTransitionRecordData> transitionsById = new Dictionary<string, CareerTransitionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerMilestoneRecordData> milestonesById = new Dictionary<string, CareerMilestoneRecordData>(StringComparer.Ordinal);
        private readonly List<CareerHistoryHookData> historyHooks = new List<CareerHistoryHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private TrainingRuntime training;
        private ProfessionalActivityRuntime activities;
        private CredentialRuntime credentials;
        private ProfessionalRankRuntime ranks;
        private PositionEmploymentRuntime positions;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownOrganizationIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownAuthorityIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int EpisodeCount => episodesById.Count;
        public int TransitionCount => transitionsById.Count;
        public int MilestoneCount => milestonesById.Count;
        public IReadOnlyList<CareerEpisodeData> Episodes => episodesById.Values.OrderBy(item => item.episodeId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CareerTransitionRecordData> Transitions => transitionsById.Values.OrderBy(item => item.transitionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CareerMilestoneRecordData> Milestones => milestonesById.Values.OrderBy(item => item.milestoneId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CareerHistoryHookData> HistoryHooks => historyHooks.Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, IEnumerable<string> persons = null, IEnumerable<string> organizations = null, IEnumerable<string> authorities = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            training = trainingRuntime;
            activities = activityRuntime;
            credentials = credentialRuntime;
            ranks = rankRuntime;
            positions = positionRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownOrganizationIds = new HashSet<string>((organizations ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownAuthorityIds = new HashSet<string>((authorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public bool TryGetEpisode(string episodeId, out CareerEpisodeData episode)
        {
            if (!string.IsNullOrWhiteSpace(episodeId) && episodesById.TryGetValue(episodeId, out CareerEpisodeData found))
            {
                episode = found.Clone();
                return true;
            }

            episode = null;
            return false;
        }

        public bool TryGetTransition(string transitionId, out CareerTransitionRecordData transition)
        {
            if (!string.IsNullOrWhiteSpace(transitionId) && transitionsById.TryGetValue(transitionId, out CareerTransitionRecordData found))
            {
                transition = found.Clone();
                return true;
            }

            transition = null;
            return false;
        }

        public bool TryGetMilestone(string milestoneId, out CareerMilestoneRecordData milestone)
        {
            if (!string.IsNullOrWhiteSpace(milestoneId) && milestonesById.TryGetValue(milestoneId, out CareerMilestoneRecordData found))
            {
                milestone = found.Clone();
                return true;
            }

            milestone = null;
            return false;
        }

        public IReadOnlyList<CareerEpisodeData> QueryEpisodesByPerson(string personId)
        {
            return episodesById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal))
                .OrderBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.episodeId ?? string.Empty, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public IReadOnlyList<CareerTransitionRecordData> QueryTransitionsByPerson(string personId)
        {
            return transitionsById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal))
                .OrderBy(item => item.transitionWorldTime ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.transitionId ?? string.Empty, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public IReadOnlyList<CareerEpisodeData> QueryByProfession(string professionId)
        {
            return episodesById.Values.Where(item => string.Equals(item.professionId, professionId ?? string.Empty, StringComparison.Ordinal)).OrderBy(item => item.episodeId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        }

        public IReadOnlyList<CareerEpisodeData> QueryByEmployment(string employmentId)
        {
            return episodesById.Values.Where(item => string.Equals(item.employmentId, employmentId ?? string.Empty, StringComparison.Ordinal)).OrderBy(item => item.episodeId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        }

        public IReadOnlyList<CareerEpisodeData> QueryByOrganization(string organizationId)
        {
            return episodesById.Values.Where(item => string.Equals(item.organizationId, organizationId ?? string.Empty, StringComparison.Ordinal)).OrderBy(item => item.episodeId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        }

        public CareerHistoryOperationResult BuildTimeline(string personId)
        {
            long before = revision;
            if (!KnownPerson(personId))
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.MissingPerson, $"Person '{personId}' is unknown.", before);
            }

            CareerTimelineSnapshot timeline = new CareerTimelineSnapshot(personId, QueryEpisodesByPerson(personId), QueryTransitionsByPerson(personId), milestonesById.Values.Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal)), revision);
            return CareerHistoryOperationResult.Success("Career timeline built.", before, before, timeline: timeline, preview: true);
        }

        public CareerHistoryOperationResult PreviewTransition(CareerTransitionRecordData transition)
        {
            long before = revision;
            CareerTransitionRecordData prepared = PrepareTransitionForCommit(transition, before, out CareerHistoryOperationStatus status, out string failure);
            return status == CareerHistoryOperationStatus.Succeeded
                ? CareerHistoryOperationResult.Success("Career transition preview succeeded.", before, before, transition: prepared, preview: true)
                : CareerHistoryOperationResult.Failure(status, failure, before);
        }

        public CareerHistoryOperationResult StartCareerEpisode(CareerEpisodeData episode, string transactionId)
        {
            long before = revision;
            CareerEpisodeData prepared = episode?.Clone();
            CareerHistoryOperationStatus status = ValidateEpisodeForCommit(prepared, adding: true, out string failure);
            if (status != CareerHistoryOperationStatus.Succeeded)
            {
                return CareerHistoryOperationResult.Failure(status, failure, before);
            }

            prepared.state = prepared.state == CareerEpisodeState.Planned ? CareerEpisodeState.Active : prepared.state;
            if (episodesById.TryGetValue(prepared.episodeId, out CareerEpisodeData existing))
            {
                return EpisodeSemanticallyEquals(existing, prepared)
                    ? CareerHistoryOperationResult.Success("Career episode already exists.", before, before, episode: existing, duplicate: true)
                    : CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.Duplicate, $"Career episode '{prepared.episodeId}' already exists with different data.", before);
            }

            episodesById[prepared.episodeId] = prepared;
            MarkMutated();
            AddHook(prepared.category == CareerEpisodeCategory.CareerGap ? CareerHistoryHookKind.CareerGapStarted : CareerHistoryHookKind.EpisodeStarted, prepared.episodeId, string.Empty, string.Empty, prepared.personId, prepared.professionId, prepared.employmentId, prepared.organizationId, prepared.startWorldTime, transactionId);
            return CareerHistoryOperationResult.Success("Career episode started.", before, revision, episode: prepared);
        }

        public CareerHistoryOperationResult EndCareerEpisode(string episodeId, string endWorldTime, string reason, string transactionId)
        {
            long before = revision;
            if (!episodesById.TryGetValue(episodeId ?? string.Empty, out CareerEpisodeData episode))
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.MissingEpisode, $"Career episode '{episodeId}' does not exist.", before);
            }

            if (episode.state == CareerEpisodeState.Ended || episode.state == CareerEpisodeState.Retired)
            {
                return CareerHistoryOperationResult.Success("Career episode already ended.", before, before, episode: episode, duplicate: true);
            }

            CareerEpisodeData updated = episode.Clone();
            updated.endWorldTime = endWorldTime ?? string.Empty;
            updated.reasonEnded = reason ?? string.Empty;
            updated.state = CareerEpisodeState.Ended;
            updated.primaryCareer = false;
            updated.revision++;
            CareerHistoryOperationStatus status = ValidateEpisodeForCommit(updated, adding: false, out string failure);
            if (status != CareerHistoryOperationStatus.Succeeded)
            {
                return CareerHistoryOperationResult.Failure(status, failure, before);
            }

            episodesById[updated.episodeId] = updated;
            MarkMutated();
            AddHook(updated.category == CareerEpisodeCategory.CareerGap ? CareerHistoryHookKind.CareerGapEnded : CareerHistoryHookKind.EpisodeEnded, updated.episodeId, string.Empty, string.Empty, updated.personId, updated.professionId, updated.employmentId, updated.organizationId, updated.endWorldTime, transactionId);
            return CareerHistoryOperationResult.Success("Career episode ended.", before, revision, episode: updated);
        }

        public CareerHistoryOperationResult SetPrimaryCareer(string episodeId, string transactionId)
        {
            long before = revision;
            if (!episodesById.TryGetValue(episodeId ?? string.Empty, out CareerEpisodeData episode))
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.MissingEpisode, $"Career episode '{episodeId}' does not exist.", before);
            }

            if (episode.state != CareerEpisodeState.Active)
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.InvalidState, "Only an active career episode may be primary.", before);
            }

            foreach (CareerEpisodeData value in episodesById.Values.Where(item => string.Equals(item.personId, episode.personId, StringComparison.Ordinal)).ToArray())
            {
                CareerEpisodeData updated = value.Clone();
                updated.primaryCareer = string.Equals(updated.episodeId, episode.episodeId, StringComparison.Ordinal);
                updated.careerClassification = updated.primaryCareer ? CareerClassification.Primary : updated.careerClassification == CareerClassification.Primary ? CareerClassification.Secondary : updated.careerClassification;
                updated.revision++;
                episodesById[updated.episodeId] = updated;
            }

            MarkMutated();
            AddHook(CareerHistoryHookKind.PrimaryCareerChanged, episode.episodeId, string.Empty, string.Empty, episode.personId, episode.professionId, episode.employmentId, episode.organizationId, string.Empty, transactionId);
            return CareerHistoryOperationResult.Success("Primary career changed.", before, revision, episode: episodesById[episode.episodeId]);
        }

        public CareerHistoryOperationResult RecordTransition(CareerTransitionRecordData transition, string transactionId)
        {
            long before = revision;
            if (transition != null
                && !string.IsNullOrWhiteSpace(transition.transitionId)
                && transitionsById.TryGetValue(transition.transitionId, out CareerTransitionRecordData existingTransition))
            {
                return TransitionSemanticallyEquals(existingTransition, transition, registry)
                    ? CareerHistoryOperationResult.Success("Career transition already exists.", before, before, transition: existingTransition, duplicate: true)
                    : CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.Duplicate, $"Career transition '{transition.transitionId}' already exists with different data.", before);
            }

            CareerTransitionRecordData prepared = PrepareTransitionForCommit(transition, before, out CareerHistoryOperationStatus status, out string failure);
            if (status != CareerHistoryOperationStatus.Succeeded)
            {
                return CareerHistoryOperationResult.Failure(status, failure, before);
            }

            CareerHistoryRuntimeSaveData rollback = CreateSaveData();
            transitionsById[prepared.transitionId] = prepared;
            foreach (string episodeId in prepared.sourceEpisodeIds.Concat(prepared.destinationEpisodeIds).Distinct(StringComparer.Ordinal))
            {
                if (episodesById.TryGetValue(episodeId, out CareerEpisodeData episode))
                {
                    CareerEpisodeData updated = episode.Clone();
                    updated.transitionIds = CareerEpisodeData.Clean(updated.transitionIds.Concat(new[] { prepared.transitionId }));
                    updated.revision++;
                    episodesById[episodeId] = updated;
                }
            }

            if (!ValidateSaveData(CreateSaveData(), registry, professions, training, activities, credentials, ranks, positions, knownPersonIds, knownOrganizationIds, knownAuthorityIds, out string validationFailure))
            {
                RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, positions, knownPersonIds, knownOrganizationIds, knownAuthorityIds, restoring: true);
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.ValidationFailed, validationFailure, before);
            }

            MarkMutated();
            AddHook(HookFor(prepared.category), string.Empty, prepared.transitionId, string.Empty, prepared.personId, prepared.professionId, prepared.newEmploymentId, prepared.organizationId, prepared.transitionWorldTime, transactionId);
            return CareerHistoryOperationResult.Success("Career transition recorded.", before, revision, transition: prepared);
        }

        public CareerHistoryOperationResult BeginCareerGap(string episodeId, string personId, string worldTime, string reason, string transactionId, bool primary = false)
        {
            return StartCareerEpisode(new CareerEpisodeData
            {
                episodeId = episodeId,
                personId = personId,
                category = CareerEpisodeCategory.CareerGap,
                state = CareerEpisodeState.Active,
                careerClassification = CareerClassification.Gap,
                startWorldTime = worldTime,
                reasonStarted = reason,
                primaryCareer = primary,
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, transactionId);
        }

        public CareerHistoryOperationResult EndCareerGap(string episodeId, string worldTime, string reason, string transactionId)
        {
            return EndCareerEpisode(episodeId, worldTime, reason, transactionId);
        }

        public CareerHistoryOperationResult RecordMilestone(CareerMilestoneRecordData milestone, string transactionId)
        {
            long before = revision;
            CareerMilestoneRecordData prepared = milestone?.Clone();
            CareerHistoryOperationStatus status = ValidateMilestoneForCommit(prepared, out string failure);
            if (status != CareerHistoryOperationStatus.Succeeded)
            {
                return CareerHistoryOperationResult.Failure(status, failure, before);
            }

            if (milestonesById.ContainsKey(prepared.milestoneId))
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.Duplicate, $"Career milestone '{prepared.milestoneId}' already exists.", before);
            }

            if (prepared.exclusive && milestonesById.Values.Any(item => item.kind == prepared.kind && string.Equals(item.sourceRecordId, prepared.sourceRecordId, StringComparison.Ordinal)))
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.InvalidRequest, "Exclusive career milestone source is already recorded.", before);
            }

            milestonesById[prepared.milestoneId] = prepared;
            MarkMutated();
            AddHook(prepared.kind == CareerMilestoneKind.Setback ? CareerHistoryHookKind.SetbackRecorded : CareerHistoryHookKind.AchievementRecorded, prepared.episodeId, prepared.transitionId, prepared.milestoneId, prepared.personId, prepared.professionId, string.Empty, prepared.organizationId, prepared.worldTime, transactionId);
            return CareerHistoryOperationResult.Success("Career milestone recorded.", before, revision, milestone: prepared);
        }

        public CareerHistoryOperationResult CorrectHistoricalClassification(string episodeId, CareerClassification classification, string transactionId)
        {
            long before = revision;
            if (!episodesById.TryGetValue(episodeId ?? string.Empty, out CareerEpisodeData episode))
            {
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.MissingEpisode, $"Career episode '{episodeId}' does not exist.", before);
            }

            CareerEpisodeData updated = episode.Clone();
            updated.careerClassification = classification;
            updated.state = updated.state == CareerEpisodeState.Invalid ? CareerEpisodeState.Corrected : updated.state;
            updated.revision++;
            episodesById[updated.episodeId] = updated;
            MarkMutated();
            AddHook(CareerHistoryHookKind.Corrected, updated.episodeId, string.Empty, string.Empty, updated.personId, updated.professionId, updated.employmentId, updated.organizationId, string.Empty, transactionId);
            return CareerHistoryOperationResult.Success("Career episode classification corrected.", before, revision, episode: updated);
        }

        public CareerHistoryProjection<CareerEpisodeData> ProjectEpisode(string episodeId, CareerHistoryProjectionAudience audience, InformationAccessDecision decision = null)
        {
            if (!TryGetEpisode(episodeId, out CareerEpisodeData episode))
            {
                return new CareerHistoryProjection<CareerEpisodeData>(null, audience, decision, false, true, Array.Empty<string>(), Array.Empty<string>());
            }

            bool privileged = audience == CareerHistoryProjectionAudience.SubjectPerson || audience == CareerHistoryProjectionAudience.PrivilegedDebug || audience == CareerHistoryProjectionAudience.Employer || audience == CareerHistoryProjectionAudience.ProfessionAuthority;
            bool denied = decision != null && decision.Denied;
            bool redacted = !denied && (episode.secret || !string.IsNullOrWhiteSpace(episode.accessPolicyId) && episode.accessPolicyId == ProfessionContentIds.AccessSecretId) && !privileged;
            CareerEpisodeData projected = episode.Clone();
            List<string> redactedFields = new List<string>();
            if (denied)
            {
                return new CareerHistoryProjection<CareerEpisodeData>(null, audience, decision, false, true, Array.Empty<string>(), CareerHistoryInformationSubject.ProtectedFields);
            }

            if (redacted)
            {
                projected.employmentId = string.Empty;
                projected.positionInstanceId = string.Empty;
                projected.organizationId = string.Empty;
                projected.reasonStarted = string.Empty;
                projected.reasonEnded = string.Empty;
                projected.sourceRecords = Array.Empty<CareerSourceRecordReferenceData>();
                redactedFields.AddRange(CareerHistoryInformationSubject.ProtectedFields);
            }

            return new CareerHistoryProjection<CareerEpisodeData>(projected, audience, decision, redacted, false, redacted ? new[] { "episode-id", "person-id", "category", "state" } : Array.Empty<string>(), redactedFields);
        }

        public CareerHistoryProjection<CareerTimelineSnapshot> ProjectTimeline(string personId, CareerHistoryProjectionAudience audience, InformationAccessDecision decision = null)
        {
            CareerHistoryOperationResult timelineResult = BuildTimeline(personId);
            if (!timelineResult.Succeeded)
            {
                return new CareerHistoryProjection<CareerTimelineSnapshot>(null, audience, decision, false, true, Array.Empty<string>(), Array.Empty<string>());
            }

            if (decision != null && decision.Denied)
            {
                return new CareerHistoryProjection<CareerTimelineSnapshot>(null, audience, decision, false, true, Array.Empty<string>(), CareerHistoryInformationSubject.ProtectedFields);
            }

            bool privileged = audience == CareerHistoryProjectionAudience.SubjectPerson || audience == CareerHistoryProjectionAudience.PrivilegedDebug || audience == CareerHistoryProjectionAudience.Employer || audience == CareerHistoryProjectionAudience.ProfessionAuthority;
            IEnumerable<CareerEpisodeData> episodes = timelineResult.Timeline.Episodes.Select(item => ProjectEpisode(item.episodeId, audience, decision).Record).Where(item => item != null && (privileged || !item.secret));
            IEnumerable<CareerTransitionRecordData> transitions = timelineResult.Timeline.Transitions.Select(item => RedactTransition(item, privileged));
            IEnumerable<CareerMilestoneRecordData> milestones = timelineResult.Timeline.Milestones.Where(item => privileged || !item.secret).Select(item => item.Clone());
            bool redacted = timelineResult.Timeline.Episodes.Any(item => item.secret) && !privileged || timelineResult.Timeline.Transitions.Any(item => item.secret) && !privileged;
            return new CareerHistoryProjection<CareerTimelineSnapshot>(new CareerTimelineSnapshot(personId, episodes, transitions, milestones, revision), audience, decision, redacted, false, redacted ? new[] { "timeline", "public-career-periods" } : Array.Empty<string>(), redacted ? CareerHistoryInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public CareerHistoryRuntimeSaveData CreateSaveData()
        {
            return new CareerHistoryRuntimeSaveData
            {
                schemaVersion = CareerHistoryRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                episodes = episodesById.Values.OrderBy(item => item.episodeId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                transitions = transitionsById.Values.OrderBy(item => item.transitionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                milestones = milestonesById.Values.OrderBy(item => item.milestoneId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public CareerHistoryOperationResult RestoreFromSaveData(CareerHistoryRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, IEnumerable<string> persons, IEnumerable<string> organizations, IEnumerable<string> authorities, bool restoring = true)
        {
            if (saveData == null)
            {
                episodesById.Clear();
                transitionsById.Clear();
                milestonesById.Clear();
                historyHooks.Clear();
                revision = 0L;
                dirty = false;
                Configure(definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, positionRuntime, persons, organizations, authorities);
                return CareerHistoryOperationResult.Success("Empty career history state restored.", 0L, 0L);
            }

            CareerHistoryRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry previousRegistry = registry;
            PersonProfessionRuntime previousProfessions = professions;
            TrainingRuntime previousTraining = training;
            ProfessionalActivityRuntime previousActivities = activities;
            CredentialRuntime previousCredentials = credentials;
            ProfessionalRankRuntime previousRanks = ranks;
            PositionEmploymentRuntime previousPositions = positions;
            HashSet<string> previousPersons = new HashSet<string>(knownPersonIds, StringComparer.Ordinal);
            HashSet<string> previousOrganizations = new HashSet<string>(knownOrganizationIds, StringComparer.Ordinal);
            HashSet<string> previousAuthorities = new HashSet<string>(knownAuthorityIds, StringComparer.Ordinal);
            long before = revision;

            Configure(definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, positionRuntime, persons, organizations, authorities);
            if (!ValidateSaveData(saveData, registry, professions, training, activities, credentials, ranks, positions, knownPersonIds, knownOrganizationIds, knownAuthorityIds, out string failure))
            {
                RestoreFromSaveData(rollback, previousRegistry, previousProfessions, previousTraining, previousActivities, previousCredentials, previousRanks, previousPositions, previousPersons, previousOrganizations, previousAuthorities, restoring);
                return CareerHistoryOperationResult.Failure(CareerHistoryOperationStatus.CorruptSave, failure, before);
            }

            episodesById.Clear();
            transitionsById.Clear();
            milestonesById.Clear();
            foreach (CareerEpisodeData episode in saveData.episodes ?? new List<CareerEpisodeData>())
            {
                episodesById[episode.episodeId] = episode.Clone();
            }

            foreach (CareerTransitionRecordData transition in saveData.transitions ?? new List<CareerTransitionRecordData>())
            {
                transitionsById[transition.transitionId] = transition.Clone();
            }

            foreach (CareerMilestoneRecordData milestone in saveData.milestones ?? new List<CareerMilestoneRecordData>())
            {
                milestonesById[milestone.milestoneId] = milestone.Clone();
            }

            historyHooks.Clear();
            revision = Math.Max(0L, saveData.revision);
            dirty = !restoring;
            return CareerHistoryOperationResult.Success("Career history restored.", before, revision);
        }

        public static bool ValidateSaveData(CareerHistoryRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, IEnumerable<string> persons, IEnumerable<string> organizations, IEnumerable<string> authorities, out string failure)
        {
            if (saveData == null)
            {
                failure = string.Empty;
                return true;
            }

            if (saveData.schemaVersion != CareerHistoryRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported career history schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> knownPersons = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> knownOrganizations = new HashSet<string>((organizations ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> episodeIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> transitionIds = new HashSet<string>(StringComparer.Ordinal);
            List<CareerEpisodeData> episodes = (saveData.episodes ?? new List<CareerEpisodeData>()).Select(item => item?.Clone()).Where(item => item != null).ToList();
            List<CareerTransitionRecordData> transitions = (saveData.transitions ?? new List<CareerTransitionRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToList();
            foreach (CareerEpisodeData episode in episodes)
            {
                if (string.IsNullOrWhiteSpace(episode.episodeId) || !episodeIds.Add(episode.episodeId))
                {
                    failure = "Career episode save data has a missing or duplicate episode ID.";
                    return false;
                }
            }

            foreach (CareerTransitionRecordData transition in transitions)
            {
                if (string.IsNullOrWhiteSpace(transition.transitionId) || !transitionIds.Add(transition.transitionId))
                {
                    failure = "Career transition save data has a missing or duplicate transition ID.";
                    return false;
                }
            }

            foreach (CareerEpisodeData episode in episodes)
            {
                if (!Known(knownPersons, episode.personId))
                {
                    failure = $"Career episode '{episode.episodeId}' references unknown Person '{episode.personId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(episode.organizationId) && !Known(knownOrganizations, episode.organizationId))
                {
                    failure = $"Career episode '{episode.episodeId}' references unknown Organization '{episode.organizationId}'.";
                    return false;
                }

                if (!ValidateReferencedDefinitions(episode, definitionRegistry, out failure) || !ValidateSourceRecords(episode.sourceRecords, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, positionRuntime, episodeIds, transitionIds, out failure))
                {
                    failure = $"Career episode '{episode.episodeId}' is invalid: {failure}";
                    return false;
                }

                if (!DateRangeValid(episode.startWorldTime, episode.endWorldTime))
                {
                    failure = $"Career episode '{episode.episodeId}' starts after it ends.";
                    return false;
                }
            }

            foreach (CareerTransitionRecordData transition in transitions)
            {
                if (!Known(knownPersons, transition.personId))
                {
                    failure = $"Career transition '{transition.transitionId}' references unknown Person '{transition.personId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(transition.transitionDefinitionId) && !TryDefinition(definitionRegistry, transition.transitionDefinitionId, out CareerTransitionDefinition _))
                {
                    failure = $"Career transition '{transition.transitionId}' references missing transition definition '{transition.transitionDefinitionId}'.";
                    return false;
                }

                if (transition.sourceEpisodeIds.Concat(transition.destinationEpisodeIds).Any(id => !episodeIds.Contains(id)))
                {
                    failure = $"Career transition '{transition.transitionId}' references a missing episode.";
                    return false;
                }

                if (!ValidateSourceRecords(transition.supportingRecords, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, positionRuntime, episodeIds, transitionIds, out failure))
                {
                    failure = $"Career transition '{transition.transitionId}' is invalid: {failure}";
                    return false;
                }
            }

            foreach (IGrouping<string, CareerEpisodeData> group in episodes.Where(item => item.primaryCareer && item.state == CareerEpisodeState.Active).GroupBy(item => item.personId, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    failure = $"Person '{group.Key}' has multiple active primary careers.";
                    return false;
                }
            }

            foreach (CareerMilestoneRecordData milestone in saveData.milestones ?? new List<CareerMilestoneRecordData>())
            {
                if (milestone == null || string.IsNullOrWhiteSpace(milestone.milestoneId))
                {
                    failure = "Career milestone save data has a missing milestone ID.";
                    return false;
                }

                if (!Known(knownPersons, milestone.personId) || !string.IsNullOrWhiteSpace(milestone.episodeId) && !episodeIds.Contains(milestone.episodeId) || !string.IsNullOrWhiteSpace(milestone.transitionId) && !transitionIds.Contains(milestone.transitionId))
                {
                    failure = $"Career milestone '{milestone.milestoneId}' references missing Person, episode, or transition.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private CareerTransitionRecordData PrepareTransitionForCommit(CareerTransitionRecordData transition, long before, out CareerHistoryOperationStatus status, out string failure)
        {
            CareerTransitionRecordData prepared = transition?.Clone();
            if (prepared == null || string.IsNullOrWhiteSpace(prepared.transitionId) || string.IsNullOrWhiteSpace(prepared.personId))
            {
                status = CareerHistoryOperationStatus.InvalidRequest;
                failure = "Transition ID and Person ID are required.";
                return null;
            }

            if (!KnownPerson(prepared.personId))
            {
                status = CareerHistoryOperationStatus.MissingPerson;
                failure = $"Person '{prepared.personId}' is unknown.";
                return null;
            }

            CareerTransitionDefinition definition = null;
            if (!string.IsNullOrWhiteSpace(prepared.transitionDefinitionId) && !TryDefinition(registry, prepared.transitionDefinitionId, out definition))
            {
                status = CareerHistoryOperationStatus.MissingDefinition;
                failure = $"Career transition definition '{prepared.transitionDefinitionId}' is missing.";
                return null;
            }

            if (definition != null)
            {
                prepared.category = definition.Category;
                if (definition.AuthorityApprovalRequired && !KnownAuthority(prepared.decidingAuthorityId))
                {
                    status = CareerHistoryOperationStatus.UnauthorizedAuthority;
                    failure = "Transition requires a known deciding authority.";
                    return null;
                }

                foreach (CareerTransitionSourceRecordType required in definition.RequiredSourceRecordTypes)
                {
                    if (!prepared.supportingRecords.Any(item => item.recordType == required))
                    {
                        status = CareerHistoryOperationStatus.MissingSourceRecord;
                        failure = $"Transition requires source record type '{required}'.";
                        return null;
                    }
                }
            }

            foreach (string id in prepared.sourceEpisodeIds.Concat(prepared.destinationEpisodeIds))
            {
                if (!episodesById.TryGetValue(id, out CareerEpisodeData episode))
                {
                    status = CareerHistoryOperationStatus.MissingEpisode;
                    failure = $"Transition references missing episode '{id}'.";
                    return null;
                }

                if (definition != null && prepared.sourceEpisodeIds.Contains(id) && definition.AllowedSourceStates.Count > 0 && !definition.AllowedSourceStates.Contains(episode.state))
                {
                    status = CareerHistoryOperationStatus.InvalidTransition;
                    failure = $"Source episode '{id}' is not in an allowed state.";
                    return null;
                }

                if (definition != null && prepared.destinationEpisodeIds.Contains(id) && definition.AllowedDestinationStates.Count > 0 && !definition.AllowedDestinationStates.Contains(episode.state))
                {
                    status = CareerHistoryOperationStatus.InvalidTransition;
                    failure = $"Destination episode '{id}' is not in an allowed state.";
                    return null;
                }
            }

            if (!ValidateSourceRecords(prepared.supportingRecords, professions, training, activities, credentials, ranks, positions, episodesById.Keys, transitionsById.Keys, out failure))
            {
                status = CareerHistoryOperationStatus.MissingSourceRecord;
                return null;
            }

            if (prepared.category == CareerTransitionCategory.Promotion && string.Equals(prepared.previousRankRecordId, prepared.newRankRecordId, StringComparison.Ordinal))
            {
                status = CareerHistoryOperationStatus.InvalidTransition;
                failure = "Promotion transition requires a rank change.";
                return null;
            }

            if (prepared.category == CareerTransitionCategory.Transfer && string.Equals(prepared.previousPositionInstanceId, prepared.newPositionInstanceId, StringComparison.Ordinal))
            {
                status = CareerHistoryOperationStatus.InvalidTransition;
                failure = "Transfer transition requires a position change.";
                return null;
            }

            if (prepared.category == CareerTransitionCategory.Retirement && !episodesById.Values.Any(item => string.Equals(item.personId, prepared.personId, StringComparison.Ordinal) && item.state == CareerEpisodeState.Active))
            {
                status = CareerHistoryOperationStatus.InvalidTransition;
                failure = "Retirement requires an active career.";
                return null;
            }

            if (prepared.category == CareerTransitionCategory.ReturnFromRetirement && !episodesById.Values.Any(item => string.Equals(item.personId, prepared.personId, StringComparison.Ordinal) && item.category == CareerEpisodeCategory.Retirement))
            {
                status = CareerHistoryOperationStatus.InvalidTransition;
                failure = "Return from retirement requires prior retirement.";
                return null;
            }

            prepared.professionRevision = professions?.Revision ?? 0L;
            prepared.trainingRevision = training?.Revision ?? 0L;
            prepared.activityRevision = activities?.Revision ?? 0L;
            prepared.credentialRevision = credentials?.Revision ?? 0L;
            prepared.rankRevision = ranks?.Revision ?? 0L;
            prepared.employmentRevision = positions?.Revision ?? 0L;
            if (prepared.careerRevisionAtEvaluation == 0L && string.IsNullOrWhiteSpace(prepared.evaluationHash))
            {
                prepared.careerRevisionAtEvaluation = before;
                prepared.evaluationHash = CareerTransitionRecordData.BuildEvaluationHash(prepared.personId, prepared.transitionDefinitionId, prepared.category, prepared.sourceEpisodeIds, prepared.destinationEpisodeIds, prepared.professionId, prepared.previousRankRecordId, prepared.newRankRecordId, prepared.previousEmploymentId, prepared.newEmploymentId, prepared.previousPositionInstanceId, prepared.newPositionInstanceId, prepared.organizationId, before, prepared.professionRevision, prepared.trainingRevision, prepared.activityRevision, prepared.credentialRevision, prepared.rankRevision, prepared.employmentRevision);
            }
            else if (!prepared.SemanticallyEqualsCurrent(before, prepared.professionRevision, prepared.trainingRevision, prepared.activityRevision, prepared.credentialRevision, prepared.rankRevision, prepared.employmentRevision))
            {
                status = CareerHistoryOperationStatus.StaleState;
                failure = "Transition snapshot is stale.";
                return null;
            }

            status = CareerHistoryOperationStatus.Succeeded;
            failure = string.Empty;
            return prepared;
        }

        private CareerHistoryOperationStatus ValidateEpisodeForCommit(CareerEpisodeData episode, bool adding, out string failure)
        {
            if (episode == null || string.IsNullOrWhiteSpace(episode.episodeId) || string.IsNullOrWhiteSpace(episode.personId))
            {
                failure = "Episode ID and Person ID are required.";
                return CareerHistoryOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(episode.personId))
            {
                failure = $"Person '{episode.personId}' is unknown.";
                return CareerHistoryOperationStatus.MissingPerson;
            }

            if (!string.IsNullOrWhiteSpace(episode.organizationId) && !KnownOrganization(episode.organizationId))
            {
                failure = $"Organization '{episode.organizationId}' is unknown.";
                return CareerHistoryOperationStatus.MissingSourceRecord;
            }

            if (!DateRangeValid(episode.startWorldTime, episode.endWorldTime))
            {
                failure = "Episode starts after it ends.";
                return CareerHistoryOperationStatus.InvalidDateRange;
            }

            if (!ValidateReferencedDefinitions(episode, registry, out failure))
            {
                return CareerHistoryOperationStatus.MissingDefinition;
            }

            if (!ValidateSourceRecords(episode.sourceRecords, professions, training, activities, credentials, ranks, positions, episodesById.Keys, transitionsById.Keys, out failure))
            {
                return CareerHistoryOperationStatus.MissingSourceRecord;
            }

            if (adding && episode.primaryCareer && episode.state == CareerEpisodeState.Active && episodesById.Values.Any(item => string.Equals(item.personId, episode.personId, StringComparison.Ordinal) && item.primaryCareer && item.state == CareerEpisodeState.Active))
            {
                failure = "A Person cannot have multiple active primary careers.";
                return CareerHistoryOperationStatus.PrimaryCareerConflict;
            }

            if (adding && episode.exclusiveCareer && episode.state == CareerEpisodeState.Active && episodesById.Values.Any(item => string.Equals(item.personId, episode.personId, StringComparison.Ordinal) && item.exclusiveCareer && item.state == CareerEpisodeState.Active && Overlaps(item, episode)))
            {
                failure = "Exclusive career episodes cannot overlap.";
                return CareerHistoryOperationStatus.ExclusiveCareerConflict;
            }

            if (adding && episode.sourceRecords.Length > 0 && episodesById.Values.Any(item => string.Equals(item.personId, episode.personId, StringComparison.Ordinal) && item.sourceRecords.Any(source => episode.sourceRecords.Any(candidate => string.Equals(candidate.Signature, source.Signature, StringComparison.Ordinal)))))
            {
                failure = "A career episode already exists for an exclusive source record.";
                return CareerHistoryOperationStatus.InvalidRequest;
            }

            failure = string.Empty;
            return CareerHistoryOperationStatus.Succeeded;
        }

        private CareerHistoryOperationStatus ValidateMilestoneForCommit(CareerMilestoneRecordData milestone, out string failure)
        {
            if (milestone == null || string.IsNullOrWhiteSpace(milestone.milestoneId) || string.IsNullOrWhiteSpace(milestone.personId))
            {
                failure = "Milestone ID and Person ID are required.";
                return CareerHistoryOperationStatus.InvalidRequest;
            }

            if (!KnownPerson(milestone.personId))
            {
                failure = $"Person '{milestone.personId}' is unknown.";
                return CareerHistoryOperationStatus.MissingPerson;
            }

            if (!string.IsNullOrWhiteSpace(milestone.episodeId) && !episodesById.ContainsKey(milestone.episodeId) || !string.IsNullOrWhiteSpace(milestone.transitionId) && !transitionsById.ContainsKey(milestone.transitionId))
            {
                failure = "Milestone references a missing episode or transition.";
                return CareerHistoryOperationStatus.MissingSourceRecord;
            }

            CareerSourceRecordReferenceData source = new CareerSourceRecordReferenceData { recordId = milestone.sourceRecordId, recordType = milestone.sourceRecordType };
            if (!string.IsNullOrWhiteSpace(source.recordId) && !ValidateSourceRecords(new[] { source }, professions, training, activities, credentials, ranks, positions, episodesById.Keys, transitionsById.Keys, out failure))
            {
                return CareerHistoryOperationStatus.MissingSourceRecord;
            }

            failure = string.Empty;
            return CareerHistoryOperationStatus.Succeeded;
        }

        private static bool ValidateReferencedDefinitions(CareerEpisodeData episode, DefinitionRegistry definitionRegistry, out string failure)
        {
            if (!string.IsNullOrWhiteSpace(episode.professionId) && !TryDefinition(definitionRegistry, episode.professionId, out ProfessionDefinition _))
            {
                failure = $"Profession '{episode.professionId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(episode.rankDefinitionId) && !TryDefinition(definitionRegistry, episode.rankDefinitionId, out ProfessionalRankDefinition _))
            {
                failure = $"Rank definition '{episode.rankDefinitionId}' is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(episode.credentialDefinitionId) && !TryDefinition(definitionRegistry, episode.credentialDefinitionId, out CredentialDefinition _))
            {
                failure = $"Credential definition '{episode.credentialDefinitionId}' is missing.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateSourceRecords(IEnumerable<CareerSourceRecordReferenceData> sources, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, PositionEmploymentRuntime positionRuntime, IEnumerable<string> careerEpisodeIds, IEnumerable<string> careerTransitionIds, out string failure)
        {
            HashSet<string> knownCareerEpisodeIds = new HashSet<string>((careerEpisodeIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> knownCareerTransitionIds = new HashSet<string>((careerTransitionIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            foreach (CareerSourceRecordReferenceData source in CareerEpisodeData.CleanSources(sources))
            {
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
                    CareerTransitionSourceRecordType.CareerEpisode => knownCareerEpisodeIds.Contains(source.recordId),
                    CareerTransitionSourceRecordType.CareerTransition => knownCareerTransitionIds.Contains(source.recordId),
                    CareerTransitionSourceRecordType.HistoricalRecordFoundation or CareerTransitionSourceRecordType.Custom => true,
                    _ => true
                };

                if (!exists)
                {
                    failure = $"Missing source record '{source.recordType}:{source.recordId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool EpisodeSemanticallyEquals(CareerEpisodeData left, CareerEpisodeData right)
        {
            CareerEpisodeData a = left?.Clone();
            CareerEpisodeData b = right?.Clone();
            if (a == null || b == null)
            {
                return a == b;
            }

            return string.Equals(a.episodeId, b.episodeId, StringComparison.Ordinal)
                && string.Equals(a.personId, b.personId, StringComparison.Ordinal)
                && a.category == b.category
                && string.Equals(a.professionId, b.professionId, StringComparison.Ordinal)
                && string.Equals(a.specializationId, b.specializationId, StringComparison.Ordinal)
                && string.Equals(a.employmentId, b.employmentId, StringComparison.Ordinal)
                && string.Equals(a.positionInstanceId, b.positionInstanceId, StringComparison.Ordinal)
                && string.Equals(a.organizationId, b.organizationId, StringComparison.Ordinal)
                && string.Equals(a.trainingEnrollmentId, b.trainingEnrollmentId, StringComparison.Ordinal)
                && string.Equals(a.rankRecordId, b.rankRecordId, StringComparison.Ordinal)
                && string.Equals(a.rankDefinitionId, b.rankDefinitionId, StringComparison.Ordinal)
                && string.Equals(a.credentialId, b.credentialId, StringComparison.Ordinal)
                && string.Equals(a.credentialDefinitionId, b.credentialDefinitionId, StringComparison.Ordinal)
                && string.Equals(a.startWorldTime, b.startWorldTime, StringComparison.Ordinal)
                && string.Equals(a.endWorldTime, b.endWorldTime, StringComparison.Ordinal)
                && a.state == b.state
                && a.careerClassification == b.careerClassification
                && a.workClassification == b.workClassification
                && string.Equals(a.locationFoundationId, b.locationFoundationId, StringComparison.Ordinal)
                && string.Equals(a.reasonStarted, b.reasonStarted, StringComparison.Ordinal)
                && string.Equals(a.reasonEnded, b.reasonEnded, StringComparison.Ordinal)
                && a.primaryCareer == b.primaryCareer
                && a.exclusiveCareer == b.exclusiveCareer
                && a.secret == b.secret
                && a.disputed == b.disputed
                && string.Equals(a.accessPolicyId, b.accessPolicyId, StringComparison.Ordinal)
                && string.Equals(a.provenance, b.provenance, StringComparison.Ordinal)
                && a.sourceRecords.Select(source => source.Signature).SequenceEqual(b.sourceRecords.Select(source => source.Signature))
                && a.transitionIds.SequenceEqual(b.transitionIds);
        }

        private static bool TransitionSemanticallyEquals(CareerTransitionRecordData left, CareerTransitionRecordData right, DefinitionRegistry definitionRegistry)
        {
            CareerTransitionRecordData a = NormalizeTransitionForComparison(left, definitionRegistry);
            CareerTransitionRecordData b = NormalizeTransitionForComparison(right, definitionRegistry);
            if (a == null || b == null)
            {
                return a == b;
            }

            return string.Equals(a.transitionId, b.transitionId, StringComparison.Ordinal)
                && string.Equals(a.personId, b.personId, StringComparison.Ordinal)
                && string.Equals(a.transitionDefinitionId, b.transitionDefinitionId, StringComparison.Ordinal)
                && a.category == b.category
                && a.sourceEpisodeIds.SequenceEqual(b.sourceEpisodeIds)
                && a.destinationEpisodeIds.SequenceEqual(b.destinationEpisodeIds)
                && string.Equals(a.professionId, b.professionId, StringComparison.Ordinal)
                && string.Equals(a.specializationId, b.specializationId, StringComparison.Ordinal)
                && string.Equals(a.previousRankRecordId, b.previousRankRecordId, StringComparison.Ordinal)
                && string.Equals(a.newRankRecordId, b.newRankRecordId, StringComparison.Ordinal)
                && string.Equals(a.previousCredentialId, b.previousCredentialId, StringComparison.Ordinal)
                && string.Equals(a.newCredentialId, b.newCredentialId, StringComparison.Ordinal)
                && string.Equals(a.previousEmploymentId, b.previousEmploymentId, StringComparison.Ordinal)
                && string.Equals(a.newEmploymentId, b.newEmploymentId, StringComparison.Ordinal)
                && string.Equals(a.previousPositionInstanceId, b.previousPositionInstanceId, StringComparison.Ordinal)
                && string.Equals(a.newPositionInstanceId, b.newPositionInstanceId, StringComparison.Ordinal)
                && string.Equals(a.organizationId, b.organizationId, StringComparison.Ordinal)
                && string.Equals(a.transitionWorldTime, b.transitionWorldTime, StringComparison.Ordinal)
                && string.Equals(a.decidingAuthorityId, b.decidingAuthorityId, StringComparison.Ordinal)
                && a.voluntary == b.voluntary
                && a.involuntary == b.involuntary
                && a.secret == b.secret
                && a.disputed == b.disputed
                && string.Equals(a.reason, b.reason, StringComparison.Ordinal)
                && a.supportingRecords.Select(source => source.Signature).SequenceEqual(b.supportingRecords.Select(source => source.Signature))
                && string.Equals(a.accessPolicyId, b.accessPolicyId, StringComparison.Ordinal)
                && string.Equals(a.provenance, b.provenance, StringComparison.Ordinal);
        }

        private static CareerTransitionRecordData NormalizeTransitionForComparison(CareerTransitionRecordData transition, DefinitionRegistry definitionRegistry)
        {
            CareerTransitionRecordData clone = transition?.Clone();
            if (clone != null
                && !string.IsNullOrWhiteSpace(clone.transitionDefinitionId)
                && TryDefinition(definitionRegistry, clone.transitionDefinitionId, out CareerTransitionDefinition definition))
            {
                clone.category = definition.Category;
            }

            return clone;
        }

        private static bool TryDefinition<TDefinition>(DefinitionRegistry definitionRegistry, string id, out TDefinition definition)
            where TDefinition : class, IGameDefinition
        {
            if (definitionRegistry != null && !string.IsNullOrWhiteSpace(id) && definitionRegistry.TryGet(id, out TDefinition found))
            {
                definition = found;
                return true;
            }

            definition = null;
            return false;
        }

        private bool KnownPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        }

        private bool KnownOrganization(string organizationId)
        {
            return !string.IsNullOrWhiteSpace(organizationId) && (knownOrganizationIds.Count == 0 || knownOrganizationIds.Contains(organizationId));
        }

        private bool KnownAuthority(string authorityId)
        {
            return !string.IsNullOrWhiteSpace(authorityId) && (knownAuthorityIds.Count == 0 || knownAuthorityIds.Contains(authorityId));
        }

        private static bool Known(HashSet<string> knownIds, string id)
        {
            return !string.IsNullOrWhiteSpace(id) && (knownIds == null || knownIds.Count == 0 || knownIds.Contains(id));
        }

        private static bool DateRangeValid(string start, string end)
        {
            return string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end) || string.CompareOrdinal(start, end) <= 0;
        }

        private static bool Overlaps(CareerEpisodeData left, CareerEpisodeData right)
        {
            string leftEnd = string.IsNullOrWhiteSpace(left.endWorldTime) ? "~" : left.endWorldTime;
            string rightEnd = string.IsNullOrWhiteSpace(right.endWorldTime) ? "~" : right.endWorldTime;
            return string.CompareOrdinal(left.startWorldTime ?? string.Empty, rightEnd) <= 0
                && string.CompareOrdinal(right.startWorldTime ?? string.Empty, leftEnd) <= 0;
        }

        private static CareerTransitionRecordData RedactTransition(CareerTransitionRecordData transition, bool privileged)
        {
            CareerTransitionRecordData projected = transition.Clone();
            if (projected.secret && !privileged)
            {
                projected.previousEmploymentId = string.Empty;
                projected.newEmploymentId = string.Empty;
                projected.previousPositionInstanceId = string.Empty;
                projected.newPositionInstanceId = string.Empty;
                projected.organizationId = string.Empty;
                projected.reason = string.Empty;
                projected.supportingRecords = Array.Empty<CareerSourceRecordReferenceData>();
            }

            return projected;
        }

        private static CareerHistoryHookKind HookFor(CareerTransitionCategory category)
        {
            return category switch
            {
                CareerTransitionCategory.CareerGapStarted => CareerHistoryHookKind.CareerGapStarted,
                CareerTransitionCategory.CareerGapEnded => CareerHistoryHookKind.CareerGapEnded,
                CareerTransitionCategory.Retirement => CareerHistoryHookKind.RetirementRecorded,
                CareerTransitionCategory.ReturnFromRetirement => CareerHistoryHookKind.ReturnRecorded,
                CareerTransitionCategory.Achievement => CareerHistoryHookKind.AchievementRecorded,
                CareerTransitionCategory.Setback => CareerHistoryHookKind.SetbackRecorded,
                CareerTransitionCategory.Correction => CareerHistoryHookKind.Corrected,
                _ => CareerHistoryHookKind.TransitionRecorded
            };
        }

        private void MarkMutated()
        {
            revision++;
            dirty = true;
        }

        private void AddHook(CareerHistoryHookKind kind, string episodeId, string transitionId, string milestoneId, string personId, string professionId, string employmentId, string organizationId, string worldTime, string transactionId)
        {
            historyHooks.Add(new CareerHistoryHookData
            {
                kind = kind,
                episodeId = episodeId ?? string.Empty,
                transitionId = transitionId ?? string.Empty,
                milestoneId = milestoneId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            });
        }
    }
}
