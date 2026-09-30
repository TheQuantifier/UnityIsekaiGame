using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Emotions
{
    public sealed class SocialEmotionRuntime : ISocialDecisionModifierSource, IDisposable
    {
        private readonly Dictionary<string, SocialEmotionEpisodeData> episodesById = new Dictionary<string, SocialEmotionEpisodeData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialMoodStateData> moodsByKey = new Dictionary<string, SocialMoodStateData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialEmotionDecisionModifierData> decisionModifiersById = new Dictionary<string, SocialEmotionDecisionModifierData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SocialEmotionProcessedTransactionData> processedTransactions = new Dictionary<string, SocialEmotionProcessedTransactionData>(StringComparer.Ordinal);
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private bool disposed;
        private bool restoring;
        private long sequence;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => registry != null && !disposed;
        public int Count => episodesById.Count;

        public void Configure(
            DefinitionRegistry definitionRegistry,
            IEnumerable<string> persons,
            RelationshipRuntime relationships = null,
            InterpersonalAttitudeRuntime attitudes = null,
            ReputationRuntime reputation = null,
            RumorRuntime rumors = null,
            SocialInteractionRuntime interactions = null,
            SocialNormRuntime norms = null,
            SocialNetworkRuntime networks = null,
            SocialInfluenceRuntime influence = null)
        {
            registry = definitionRegistry ?? registry;
            knownPersonIds = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            disposed = false;
        }

        public SocialEmotionResult Preview(SocialEmotionTriggerRequest request)
        {
            SocialEmotionTriggerRequest clone = request?.Clone() ?? new SocialEmotionTriggerRequest();
            clone.Preview = true;
            return Resolve(clone);
        }

        public SocialEmotionResult Execute(SocialEmotionTriggerRequest request)
        {
            SocialEmotionTriggerRequest clone = request?.Clone() ?? new SocialEmotionTriggerRequest();
            clone.Preview = false;
            return Resolve(clone);
        }

        public IReadOnlyList<SocialEmotionEpisodeSnapshot> CreateSnapshot(double worldTime = 0d)
        {
            return episodesById.Values
                .OrderBy(item => item.personId, StringComparer.Ordinal)
                .ThenBy(item => item.startWorldTime)
                .ThenBy(item => item.episodeId, StringComparer.Ordinal)
                .Select(item => new SocialEmotionEpisodeSnapshot(item.Clone(), CurrentIntensity(item, worldTime)))
                .ToArray();
        }

        public IReadOnlyList<SocialEmotionEpisodeSnapshot> QueryActiveEpisodes(string personId, double worldTime)
        {
            string person = Clean(personId);
            return episodesById.Values
                .Where(item => item.IsActiveAt(worldTime))
                .Where(item => string.IsNullOrWhiteSpace(person) || string.Equals(item.personId, person, StringComparison.Ordinal))
                .OrderByDescending(item => CurrentIntensity(item, worldTime))
                .ThenBy(item => item.emotionDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.episodeId, StringComparer.Ordinal)
                .Select(item => new SocialEmotionEpisodeSnapshot(item.Clone(), CurrentIntensity(item, worldTime)))
                .ToArray();
        }

        public IReadOnlyList<SocialMoodSnapshot> QueryMoods(string personId, double worldTime)
        {
            EvaluateMoods(personId, worldTime, commit: false, out List<SocialMoodStateData> states);
            return states.OrderBy(item => item.moodDimensionId, StringComparer.Ordinal).Select(item => new SocialMoodSnapshot(item.Clone())).ToArray();
        }

        public SocialEmotionProjection GetProjection(string requesterPersonId, string episodeId, bool privileged = false, double worldTime = 0d)
        {
            if (string.IsNullOrWhiteSpace(episodeId) || !episodesById.TryGetValue(episodeId.Trim(), out SocialEmotionEpisodeData episode))
            {
                return new SocialEmotionProjection(SocialEmotionProjectionAccess.Denied, null, "Emotion episode is missing.");
            }

            if (privileged || string.Equals(Clean(requesterPersonId), episode.personId, StringComparison.Ordinal))
            {
                return new SocialEmotionProjection(SocialEmotionProjectionAccess.Full, new SocialEmotionEpisodeSnapshot(episode.Clone(), CurrentIntensity(episode, worldTime)), "Full access granted.");
            }

            if (episode.concealed || episode.visibility == SocialEmotionVisibility.Internal)
            {
                return new SocialEmotionProjection(SocialEmotionProjectionAccess.Concealed, null, "Emotion episode is concealed from requester.");
            }

            SocialEmotionEpisodeData clone = episode.Clone();
            clone.cause = new SocialEmotionCauseReferenceData { category = clone.cause?.category ?? SocialEmotionCauseCategory.Custom, sourceRuntime = clone.cause?.sourceRuntime ?? string.Empty };
            clone.targetPersonId = string.Empty;
            clone.subjectId = string.Empty;
            return new SocialEmotionProjection(SocialEmotionProjectionAccess.Redacted, new SocialEmotionEpisodeSnapshot(clone, CurrentIntensity(episode, worldTime)), "Emotion episode was redacted.");
        }

        public int ResolveSocialDecisionScoreModifier(string actorPersonId, string targetPersonId, string intentionDefinitionId, string interactionDefinitionId, double worldTime, out string sourceModifierId)
        {
            string actor = Clean(actorPersonId);
            string target = Clean(targetPersonId);
            string intention = Clean(intentionDefinitionId);
            string interaction = Clean(interactionDefinitionId);
            SocialEmotionDecisionModifierData[] active = decisionModifiersById.Values
                .Where(item => item.IsActiveAt(worldTime))
                .Where(item => string.IsNullOrWhiteSpace(item.actorPersonId) || string.Equals(item.actorPersonId, actor, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.targetPersonId) || string.Equals(item.targetPersonId, target, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.intentionDefinitionId) || string.Equals(item.intentionDefinitionId, intention, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.interactionDefinitionId) || string.Equals(item.interactionDefinitionId, interaction, StringComparison.Ordinal))
                .OrderBy(item => item.modifierId, StringComparer.Ordinal)
                .ToArray();
            sourceModifierId = string.Join(",", active.Select(item => item.modifierId));
            return Math.Max(-250, Math.Min(250, active.Sum(item => item.scoreDelta)));
        }

        public SocialEmotionRuntimeSaveData CreateSaveData()
        {
            return new SocialEmotionRuntimeSaveData
            {
                revision = Revision,
                episodeSequence = sequence,
                episodes = episodesById.Values.OrderBy(item => item.episodeId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                moods = moodsByKey.Values.OrderBy(item => item.personId, StringComparer.Ordinal).ThenBy(item => item.moodDimensionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                decisionModifiers = decisionModifiersById.Values.OrderBy(item => item.modifierId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                processedTransactions = processedTransactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public int PruneHistory(int maximumEpisodesPerPerson)
        {
            string[] remove = episodesById.Values.GroupBy(item => item.personId, StringComparer.Ordinal)
                .SelectMany(group => group.OrderByDescending(item => item.startWorldTime).ThenByDescending(item => item.revision).Skip(Math.Max(0, maximumEpisodesPerPerson)))
                .Select(item => item.episodeId).ToArray();
            foreach (string id in remove) episodesById.Remove(id);
            HashSet<string> retained = new HashSet<string>(episodesById.Keys, StringComparer.Ordinal);
            foreach (string tx in processedTransactions.Values.Where(item => !retained.Contains(item.episodeId)).Select(item => item.transactionId).ToArray()) processedTransactions.Remove(tx);
            foreach (string id in decisionModifiersById.Values.Where(item => !retained.Contains(item.sourceEpisodeId)).Select(item => item.modifierId).ToArray()) decisionModifiersById.Remove(id);
            foreach (SocialMoodStateData mood in moodsByKey.Values) mood.sourceEpisodeIds = (mood.sourceEpisodeIds ?? Array.Empty<string>()).Where(retained.Contains).ToArray();
            if (remove.Length > 0) { Revision++; IsDirty = true; }
            return remove.Length;
        }

        public SocialEmotionResult RestoreFromSaveData(SocialEmotionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failure))
            {
                return SocialEmotionResult.Failure(SocialEmotionStatus.RestoreFailed, failure, before);
            }

            restoring = restoringState;
            try
            {
                RestoreInternal(saveData ?? new SocialEmotionRuntimeSaveData());
            }
            finally
            {
                restoring = false;
            }

            return new SocialEmotionResult(true, SocialEmotionStatus.Restored, "Social emotions restored.", null, null, false, false, before, Revision);
        }

        public static bool ValidateSaveData(SocialEmotionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            SocialEmotionRuntimeSaveData effective = saveData ?? new SocialEmotionRuntimeSaveData();
            if (effective.schemaVersion != SocialEmotionRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported Social Emotion schema version {effective.schemaVersion}.";
                return false;
            }

            HashSet<string> known = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            HashSet<string> episodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialEmotionEpisodeData episode in effective.episodes ?? new List<SocialEmotionEpisodeData>())
            {
                if (episode == null || string.IsNullOrWhiteSpace(episode.episodeId) || !episodeIds.Add(episode.episodeId))
                {
                    failure = "Social Emotion payload contains a missing or duplicate episode ID.";
                    return false;
                }

                if (!KnownOrEmpty(known, episode.personId) || !KnownOrEmpty(known, episode.targetPersonId))
                {
                    failure = $"Social Emotion episode '{episode.episodeId}' references an unknown Person.";
                    return false;
                }

                if (definitionRegistry != null && !definitionRegistry.TryGet(episode.emotionDefinitionId, out SocialEmotionDefinition _))
                {
                    failure = $"Social Emotion episode '{episode.episodeId}' references missing emotion '{episode.emotionDefinitionId}'.";
                    return false;
                }

                if (definitionRegistry != null && !string.IsNullOrWhiteSpace(episode.appraisalRuleId) && !definitionRegistry.TryGet(episode.appraisalRuleId, out SocialEmotionAppraisalRuleDefinition _))
                {
                    failure = $"Social Emotion episode '{episode.episodeId}' references missing appraisal rule '{episode.appraisalRuleId}'.";
                    return false;
                }
            }

            HashSet<string> moodKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialMoodStateData mood in effective.moods ?? new List<SocialMoodStateData>())
            {
                if (mood == null || string.IsNullOrWhiteSpace(mood.personId) || string.IsNullOrWhiteSpace(mood.moodDimensionId) || !moodKeys.Add(MoodKey(mood.personId, mood.moodDimensionId)))
                {
                    failure = "Social Emotion payload contains a missing or duplicate mood state.";
                    return false;
                }

                if (!known.Contains(mood.personId))
                {
                    failure = $"Mood state references unknown Person '{mood.personId}'.";
                    return false;
                }

                if (definitionRegistry != null && !definitionRegistry.TryGet(mood.moodDimensionId, out SocialMoodDimensionDefinition _))
                {
                    failure = $"Mood state references missing Mood Dimension '{mood.moodDimensionId}'.";
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            episodesById.Clear();
            moodsByKey.Clear();
            decisionModifiersById.Clear();
            processedTransactions.Clear();
        }

        private SocialEmotionResult Resolve(SocialEmotionTriggerRequest request)
        {
            long before = Revision;
            if (!IsReady || restoring)
            {
                return SocialEmotionResult.Failure(SocialEmotionStatus.RuntimeNotReady, "Social Emotion runtime is not ready.", before);
            }

            string tx = Clean(request.TransactionId);
            if (string.IsNullOrWhiteSpace(tx))
            {
                return SocialEmotionResult.Failure(SocialEmotionStatus.InvalidRequest, "Social emotion requires a transaction ID.", before);
            }

            if (!request.Preview && processedTransactions.TryGetValue(tx, out SocialEmotionProcessedTransactionData processed))
            {
                episodesById.TryGetValue(processed.episodeId, out SocialEmotionEpisodeData existing);
                return new SocialEmotionResult(true, SocialEmotionStatus.Duplicate, "Social emotion transaction already processed.", existing == null ? null : new SocialEmotionEpisodeSnapshot(existing.Clone(), CurrentIntensity(existing, request.WorldTime)), null, false, true, before, before);
            }

            string person = Clean(request.PersonId);
            if (!ValidateKnownPerson(person) || !KnownOrEmpty(knownPersonIds, request.TargetPersonId))
            {
                return SocialEmotionResult.Failure(SocialEmotionStatus.MissingPerson, "Social emotion actor and target must be known Persons.", before);
            }

            string emotionId = Clean(request.EmotionDefinitionId);
            string ruleId = Clean(request.AppraisalRuleId);
            SocialEmotionAppraisalRuleDefinition rule = null;
            if (string.IsNullOrWhiteSpace(emotionId))
            {
                rule = ResolveRule(request);
                if (rule == null)
                {
                    return SocialEmotionResult.Failure(SocialEmotionStatus.MissingAppraisalRule, "No active emotion appraisal rule matched the request.", before);
                }

                emotionId = rule.EmotionDefinitionId;
                ruleId = rule.Id;
            }
            else if (!string.IsNullOrWhiteSpace(ruleId))
            {
                registry.TryGet(ruleId, out rule);
            }

            if (!registry.TryGet(emotionId, out SocialEmotionDefinition emotion))
            {
                return SocialEmotionResult.Failure(SocialEmotionStatus.MissingEmotionDefinition, $"Social Emotion Definition '{emotionId}' is missing.", before);
            }

            int intensity = Math.Max(emotion.MinimumIntensity, Math.Min(emotion.MaximumIntensity, request.IntensityOverride ?? rule?.BaseIntensity ?? emotion.DefaultIntensity));
            double duration = Math.Max(0d, request.DurationOverrideSeconds ?? rule?.DurationSeconds ?? emotion.DefaultDurationSeconds);
            string subject = Clean(string.IsNullOrWhiteSpace(request.SubjectId) ? request.Cause?.subjectId : request.SubjectId);
            string target = Clean(string.IsNullOrWhiteSpace(request.TargetPersonId) ? request.Cause?.targetPersonId : request.TargetPersonId);
            SocialEmotionEpisodeData episode = BuildEpisode(request, tx, person, target, subject, emotion, ruleId, intensity, duration);
            SocialMoodStateData mood = BuildMood(person, emotion, rule, episode, request.WorldTime);

            if (request.Preview)
            {
                return new SocialEmotionResult(true, SocialEmotionStatus.Preview, "Social emotion preview succeeded.", new SocialEmotionEpisodeSnapshot(episode, CurrentIntensity(episode, request.WorldTime)), new SocialMoodSnapshot(mood), true, false, before, before);
            }

            SocialEmotionEpisodeData committed = ApplyStacking(episode, emotion, request.WorldTime);
            SocialMoodStateData committedMood = CommitMood(mood, committed, emotion, request.WorldTime);
            SocialEmotionDecisionModifierData modifier = BuildDecisionModifier(committed, emotion, rule, request.WorldTime);
            if (modifier != null)
            {
                committed.decisionModifierId = modifier.modifierId;
                decisionModifiersById[modifier.modifierId] = modifier;
            }

            episodesById[committed.episodeId] = committed;
            processedTransactions[tx] = new SocialEmotionProcessedTransactionData { transactionId = tx, episodeId = committed.episodeId, status = SocialEmotionStatus.Succeeded };
            Revision++;
            committed.revision = Revision;
            committedMood.revision = Revision;
            episodesById[committed.episodeId] = committed.Clone();
            moodsByKey[MoodKey(committedMood.personId, committedMood.moodDimensionId)] = committedMood.Clone();
            IsDirty = true;

            return new SocialEmotionResult(true, SocialEmotionStatus.Succeeded, "Social emotion recorded.", new SocialEmotionEpisodeSnapshot(committed.Clone(), CurrentIntensity(committed, request.WorldTime)), new SocialMoodSnapshot(committedMood.Clone()), false, false, before, Revision);
        }

        private SocialEmotionEpisodeData ApplyStacking(SocialEmotionEpisodeData episode, SocialEmotionDefinition definition, double worldTime)
        {
            if (definition.StackingPolicy == SocialEmotionStackingPolicy.KeepSeparate)
            {
                return episode;
            }

            SocialEmotionEpisodeData existing = episodesById.Values
                .Where(item => item.IsActiveAt(worldTime))
                .Where(item => string.Equals(item.personId, episode.personId, StringComparison.Ordinal))
                .Where(item => string.Equals(item.emotionDefinitionId, episode.emotionDefinitionId, StringComparison.Ordinal))
                .Where(item => string.Equals(item.targetPersonId, episode.targetPersonId, StringComparison.Ordinal))
                .OrderBy(item => item.episodeId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (existing == null)
            {
                return episode;
            }

            SocialEmotionEpisodeData merged = existing.Clone();
            if (definition.StackingPolicy == SocialEmotionStackingPolicy.ReinforceExisting)
            {
                merged.baseIntensity = Math.Min(definition.MaximumIntensity, Math.Max(CurrentIntensity(merged, worldTime), episode.baseIntensity) + Math.Max(1, episode.baseIntensity / 3));
                merged.reinforcementCount++;
                merged.expirationWorldTime = Math.Max(merged.expirationWorldTime, episode.expirationWorldTime);
            }
            else if (episode.baseIntensity > CurrentIntensity(merged, worldTime))
            {
                merged.baseIntensity = episode.baseIntensity;
                merged.expirationWorldTime = episode.expirationWorldTime;
                merged.cause = episode.cause.Clone();
                merged.subjectId = episode.subjectId;
            }

            return merged;
        }

        private SocialEmotionAppraisalRuleDefinition ResolveRule(SocialEmotionTriggerRequest request)
        {
            SocialEmotionCauseReferenceData cause = request.Cause ?? new SocialEmotionCauseReferenceData();
            string[] requestTags = Clean(cause.tags);
            return registry.DefinitionsById.Values
                .OfType<SocialEmotionAppraisalRuleDefinition>()
                .Where(rule => rule.CauseCategory == cause.category)
                .Where(rule => rule.RequiredBeliefTruthStatus == SocialInfluenceTruthStatus.Unknown || rule.RequiredBeliefTruthStatus == cause.believedTruthStatus)
                .Where(rule => cause.detectionOutcome >= rule.MinimumDetectionOutcome)
                .Where(rule => rule.RequiredTags.All(tag => requestTags.Contains(tag)))
                .OrderByDescending(rule => rule.Priority)
                .ThenBy(rule => rule.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private SocialEmotionEpisodeData BuildEpisode(SocialEmotionTriggerRequest request, string tx, string person, string target, string subject, SocialEmotionDefinition emotion, string ruleId, int intensity, double duration)
        {
            string episodeId = string.IsNullOrWhiteSpace(request.EpisodeId)
                ? BuildStableId("social-emotion-episode", $"{tx}.{person}.{emotion.Id}.{target}.{subject}")
                : Clean(request.EpisodeId);
            return new SocialEmotionEpisodeData
            {
                episodeId = episodeId,
                transactionId = tx,
                personId = person,
                emotionDefinitionId = emotion.Id,
                appraisalRuleId = ruleId,
                targetPersonId = target,
                subjectId = subject,
                cause = request.Cause?.Clone() ?? new SocialEmotionCauseReferenceData(),
                baseIntensity = intensity,
                startWorldTime = request.WorldTime,
                expirationWorldTime = duration <= 0d || emotion.DecayPolicy == SocialEmotionDecayPolicy.None ? -1d : request.WorldTime + duration,
                visibility = emotion.DefaultVisibility,
                suppressed = request.Suppressed && emotion.CanBeSuppressed,
                concealed = request.Concealed && emotion.CanBeConcealed,
                active = true
            };
        }

        private SocialMoodStateData BuildMood(string person, SocialEmotionDefinition emotion, SocialEmotionAppraisalRuleDefinition rule, SocialEmotionEpisodeData episode, double worldTime)
        {
            string moodId = Clean(string.IsNullOrWhiteSpace(rule?.TargetMoodDimensionId) ? emotion.PrimaryMoodDimensionId : rule.TargetMoodDimensionId);
            int contribution = rule != null && rule.MoodContributionOverride != 0 ? rule.MoodContributionOverride : emotion.MoodContribution;
            if (string.IsNullOrWhiteSpace(moodId) || !registry.TryGet(moodId, out SocialMoodDimensionDefinition dimension))
            {
                return new SocialMoodStateData { personId = person, moodDimensionId = moodId, value = 0, lastEvaluatedWorldTime = worldTime, sourceEpisodeIds = new[] { episode.episodeId } };
            }

            SocialMoodStateData current = moodsByKey.TryGetValue(MoodKey(person, moodId), out SocialMoodStateData existing) ? DecayMood(existing, dimension, worldTime) : new SocialMoodStateData { personId = person, moodDimensionId = moodId, value = dimension.NeutralValue, lastEvaluatedWorldTime = worldTime };
            current.value = Math.Max(dimension.MinimumValue, Math.Min(dimension.MaximumValue, current.value + (contribution * episode.baseIntensity / 100)));
            current.sourceEpisodeIds = Clean((current.sourceEpisodeIds ?? Array.Empty<string>()).Concat(new[] { episode.episodeId }));
            current.lastEvaluatedWorldTime = worldTime;
            return current;
        }

        private SocialMoodStateData CommitMood(SocialMoodStateData mood, SocialEmotionEpisodeData episode, SocialEmotionDefinition emotion, double worldTime)
        {
            if (string.IsNullOrWhiteSpace(mood.moodDimensionId) || !registry.TryGet(mood.moodDimensionId, out SocialMoodDimensionDefinition _))
            {
                return mood;
            }

            return mood;
        }

        private SocialEmotionDecisionModifierData BuildDecisionModifier(SocialEmotionEpisodeData episode, SocialEmotionDefinition emotion, SocialEmotionAppraisalRuleDefinition rule, double worldTime)
        {
            int score = rule != null && rule.DecisionModifierOverride != 0 ? rule.DecisionModifierOverride : emotion.DecisionScoreModifier;
            if (score == 0)
            {
                return null;
            }

            return new SocialEmotionDecisionModifierData
            {
                modifierId = BuildStableId("social-emotion-modifier", episode.episodeId),
                sourceEpisodeId = episode.episodeId,
                actorPersonId = episode.personId,
                targetPersonId = episode.targetPersonId,
                scoreDelta = Math.Max(-250, Math.Min(250, score)),
                createdWorldTime = worldTime,
                expirationWorldTime = episode.expirationWorldTime
            };
        }

        private void EvaluateMoods(string personId, double worldTime, bool commit, out List<SocialMoodStateData> states)
        {
            string person = Clean(personId);
            states = new List<SocialMoodStateData>();
            foreach (SocialMoodStateData mood in moodsByKey.Values.Where(item => string.IsNullOrWhiteSpace(person) || string.Equals(item.personId, person, StringComparison.Ordinal)).OrderBy(item => item.moodDimensionId, StringComparer.Ordinal))
            {
                SocialMoodStateData resolved = registry.TryGet(mood.moodDimensionId, out SocialMoodDimensionDefinition dimension)
                    ? DecayMood(mood, dimension, worldTime)
                    : mood.Clone();
                states.Add(resolved);
                if (commit)
                {
                    moodsByKey[MoodKey(resolved.personId, resolved.moodDimensionId)] = resolved.Clone();
                }
            }
        }

        private SocialMoodStateData DecayMood(SocialMoodStateData mood, SocialMoodDimensionDefinition dimension, double worldTime)
        {
            SocialMoodStateData clone = mood.Clone();
            double elapsed = Math.Max(0d, worldTime - clone.lastEvaluatedWorldTime);
            int delta = (int)Math.Floor(elapsed * dimension.RecoveryPerSecond);
            if (delta > 0)
            {
                if (clone.value > dimension.NeutralValue)
                {
                    clone.value = Math.Max(dimension.NeutralValue, clone.value - delta);
                }
                else if (clone.value < dimension.NeutralValue)
                {
                    clone.value = Math.Min(dimension.NeutralValue, clone.value + delta);
                }

                clone.lastEvaluatedWorldTime = worldTime;
            }

            return clone;
        }

        private int CurrentIntensity(SocialEmotionEpisodeData episode, double worldTime)
        {
            if (episode == null || !episode.active || episode.suppressed)
            {
                return 0;
            }

            if (!registry.TryGet(episode.emotionDefinitionId, out SocialEmotionDefinition definition) || definition.DecayPolicy == SocialEmotionDecayPolicy.None || episode.expirationWorldTime < 0d)
            {
                return episode.baseIntensity;
            }

            if (worldTime >= episode.expirationWorldTime)
            {
                return 0;
            }

            if (definition.DecayPolicy == SocialEmotionDecayPolicy.StepAtExpiration)
            {
                return episode.baseIntensity;
            }

            double duration = Math.Max(0.0001d, episode.expirationWorldTime - episode.startWorldTime);
            double remaining = Math.Max(0d, episode.expirationWorldTime - worldTime);
            return Math.Max(0, Math.Min(definition.MaximumIntensity, (int)Math.Round(episode.baseIntensity * (remaining / duration))));
        }

        private void RestoreInternal(SocialEmotionRuntimeSaveData saveData)
        {
            SocialEmotionRuntimeSaveData effective = saveData ?? new SocialEmotionRuntimeSaveData();
            episodesById.Clear();
            moodsByKey.Clear();
            decisionModifiersById.Clear();
            processedTransactions.Clear();
            foreach (SocialEmotionEpisodeData episode in effective.episodes ?? new List<SocialEmotionEpisodeData>())
            {
                episodesById[episode.episodeId] = episode.Clone();
            }

            foreach (SocialMoodStateData mood in effective.moods ?? new List<SocialMoodStateData>())
            {
                moodsByKey[MoodKey(mood.personId, mood.moodDimensionId)] = mood.Clone();
            }

            foreach (SocialEmotionDecisionModifierData modifier in effective.decisionModifiers ?? new List<SocialEmotionDecisionModifierData>())
            {
                decisionModifiersById[modifier.modifierId] = modifier.Clone();
            }

            foreach (SocialEmotionProcessedTransactionData processed in effective.processedTransactions ?? new List<SocialEmotionProcessedTransactionData>())
            {
                processedTransactions[processed.transactionId] = processed.Clone();
            }

            sequence = Math.Max(0L, effective.episodeSequence);
            Revision = Math.Max(0L, effective.revision);
            IsDirty = false;
        }

        private bool ValidateKnownPerson(string personId) => !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        private static bool KnownOrEmpty(HashSet<string> known, string personId) => string.IsNullOrWhiteSpace(personId) || known.Count == 0 || known.Contains(personId.Trim());
        private static string MoodKey(string personId, string moodDimensionId) => $"{Clean(personId)}|{Clean(moodDimensionId)}";
        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string BuildStableId(string prefix, string seed)
        {
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return $"{prefix}.{BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant()}";
        }
    }
}
