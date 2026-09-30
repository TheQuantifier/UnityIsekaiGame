using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Social.Integration
{
    [Serializable]
    public sealed class SocialSimulationSettings
    {
        public bool autonomousNpcDecisions = true;
        public double decisionIntervalSeconds = 2d;
        public int maximumNpcDecisionsPerTick = 1;
        public double maintenanceIntervalSeconds = 60d;
        public int maximumInteractionHistory = 2000;
        public int maximumDecisionHistoryPerPerson = 32;
        public int maximumProcessedTransactions = 4096;
        public int maximumRumorTransmissions = 2000;
        public int maximumNormAssessments = 2000;
        public int maximumInfluenceAttempts = 1000;
        public int maximumEmotionEpisodesPerPerson = 32;
    }

    /// <summary>
    /// The single orchestration boundary for the otherwise data-oriented social runtimes.
    /// It discovers people, refreshes runtime membership only when that set changes,
    /// budgets autonomous NPC decisions, and reports mutations to persistence.
    /// </summary>
    public sealed class SocialSimulationCoordinator
    {
        private readonly Func<IReadOnlyList<string>> personProvider;
        private readonly Func<string> playerPersonProvider;
        private readonly Func<string> placeProvider;
        private readonly Action<IReadOnlyList<string>> reconfigureRuntimes;
        private readonly Action<string> mutationCallback;
        private readonly SocialSimulationSettings settings;
        private readonly RelationshipRuntime relationships;
        private readonly InterpersonalAttitudeRuntime attitudes;
        private readonly ReputationRuntime reputation;
        private readonly RumorRuntime rumors;
        private readonly SocialInteractionRuntime interactions;
        private readonly SocialNormRuntime norms;
        private readonly SocialNetworkRuntime networks;
        private readonly SocialDecisionRuntime decisions;
        private readonly SocialInfluenceRuntime influence;
        private readonly SocialEmotionRuntime emotions;
        private readonly FamilyRelationshipRuntime family;
        private string peopleFingerprint = string.Empty;
        private string[] knownPeople = Array.Empty<string>();
        private double nextDecisionTime;
        private double nextMaintenanceTime;
        private int nextNpcIndex;
        private long observedMutationSignature;

        public SocialSimulationCoordinator(
            Func<IReadOnlyList<string>> people,
            Func<string> player,
            Func<string> place,
            Action<IReadOnlyList<string>> reconfigure,
            Action<string> onMutation,
            SocialSimulationSettings configuration,
            RelationshipRuntime relationshipRuntime,
            InterpersonalAttitudeRuntime attitudeRuntime,
            ReputationRuntime reputationRuntime,
            RumorRuntime rumorRuntime,
            SocialInteractionRuntime interactionRuntime,
            SocialNormRuntime normRuntime,
            SocialNetworkRuntime networkRuntime,
            SocialDecisionRuntime decisionRuntime,
            SocialInfluenceRuntime influenceRuntime,
            SocialEmotionRuntime emotionRuntime,
            FamilyRelationshipRuntime familyRuntime)
        {
            personProvider = people ?? (() => Array.Empty<string>());
            playerPersonProvider = player ?? (() => string.Empty);
            placeProvider = place ?? (() => string.Empty);
            reconfigureRuntimes = reconfigure;
            mutationCallback = onMutation;
            settings = configuration ?? new SocialSimulationSettings();
            relationships = relationshipRuntime;
            attitudes = attitudeRuntime;
            reputation = reputationRuntime;
            rumors = rumorRuntime;
            interactions = interactionRuntime;
            norms = normRuntime;
            networks = networkRuntime;
            decisions = decisionRuntime;
            influence = influenceRuntime;
            emotions = emotionRuntime;
            family = familyRuntime;
            RefreshPeople(0d, force: true);
            observedMutationSignature = MutationSignature();
        }

        public IReadOnlyList<string> KnownPeople => knownPeople;

        public void Advance(double worldTime)
        {
            RefreshPeople(worldTime, force: false);
            if (settings.autonomousNpcDecisions && worldTime >= nextDecisionTime)
            {
                RunDecisionBudget(worldTime);
                nextDecisionTime = worldTime + Math.Max(0.25d, settings.decisionIntervalSeconds);
            }

            if (worldTime >= nextMaintenanceTime)
            {
                PruneBoundedHistory();
                nextMaintenanceTime = worldTime + Math.Max(10d, settings.maintenanceIntervalSeconds);
            }

            long signature = MutationSignature();
            if (signature != observedMutationSignature)
            {
                observedMutationSignature = signature;
                mutationCallback?.Invoke("World social state changed.");
            }
        }

        public SocialInteractionResult RecordInteraction(
            string interactionDefinitionId,
            string initiatorPersonId,
            string targetPersonId,
            string transactionId,
            double worldTime,
            string originatingReferenceId = "",
            SocialInteractionVisibility? visibility = null)
        {
            RefreshPeople(worldTime, force: false);
            if (!interactions.KnowsPerson(initiatorPersonId) || !interactions.KnowsPerson(targetPersonId))
            {
                // Persistence restores and late scene binding can reconfigure an individual
                // runtime after the coordinator last refreshed its fingerprint. Repair that
                // drift at the gameplay boundary before validating the interaction.
                reconfigureRuntimes?.Invoke(knownPeople);
            }

            SocialInteractionResult result = interactions.Execute(new SocialInteractionRequest
            {
                TransactionId = transactionId,
                InteractionDefinitionId = interactionDefinitionId,
                InitiatorPersonId = initiatorPersonId,
                TargetPersonId = targetPersonId,
                PlaceId = placeProvider(),
                AudienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                WorldTime = worldTime,
                DeterministicSeed = transactionId,
                OriginatingReferenceId = originatingReferenceId,
                VisibilityOverride = visibility
            });

            if (result.Succeeded && !result.Preview && !result.Duplicate)
            {
                observedMutationSignature = MutationSignature();
                mutationCallback?.Invoke($"Social interaction recorded: {interactionDefinitionId}.");
            }

            return result;
        }

        public string BuildDebugReport(string observerPersonId, string subjectPersonId, double worldTime)
        {
            string observer = Clean(observerPersonId);
            string subject = Clean(subjectPersonId);
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = knownPeople.FirstOrDefault(person => !string.Equals(person, observer, StringComparison.Ordinal)) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(observer) || string.IsNullOrWhiteSpace(subject))
            {
                return $"Known people: {knownPeople.Length}\nNo observer/subject pair is available.";
            }

            int trust = attitudes.ResolveValue(observer, subject, PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int affection = attitudes.ResolveValue(observer, subject, PrototypeAttitudeDefinitionFactory.AffectionId).EffectiveValue;
            int respect = attitudes.ResolveValue(observer, subject, PrototypeAttitudeDefinitionFactory.RespectId).EffectiveValue;
            int fear = attitudes.ResolveValue(observer, subject, PrototypeAttitudeDefinitionFactory.FearId).EffectiveValue;
            int hostility = attitudes.ResolveValue(observer, subject, PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue;
            int relationshipCount = relationships.QueryBetween(observer, subject, activeOnly: true).Count;
            int interactionCount = interactions.Snapshots.Count(item =>
                (string.Equals(item.InitiatorPersonId, observer, StringComparison.Ordinal) && string.Equals(item.TargetPersonId, subject, StringComparison.Ordinal))
                || (string.Equals(item.InitiatorPersonId, subject, StringComparison.Ordinal) && string.Equals(item.TargetPersonId, observer, StringComparison.Ordinal)));
            int moodCount = emotions.QueryMoods(subject, worldTime).Count;
            int groupCount = networks.QueryGroupsByPerson(subject).Count;
            int familyCount = family.CreateSaveData().memberships.Count(item =>
                string.Equals(item.personId, subject, StringComparison.Ordinal)
                && item.status == HouseholdMembershipStatus.Active);

            return $"Known people: {knownPeople.Length}\nObserver: {observer}\nSubject: {subject}\nTrust {trust} | Affection {affection} | Respect {respect}\nFear {fear} | Hostility {hostility}\nRelationships {relationshipCount} | Interactions {interactionCount}\nGroups {groupCount} | Family ties {familyCount} | Moods {moodCount}";
        }

        private void RefreshPeople(double worldTime, bool force)
        {
            string[] current = (personProvider() ?? Array.Empty<string>())
                .Where(person => !string.IsNullOrWhiteSpace(person))
                .Select(Clean)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(person => person, StringComparer.Ordinal)
                .ToArray();
            string fingerprint = string.Join("\n", current);
            if (!force && string.Equals(fingerprint, peopleFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            peopleFingerprint = fingerprint;
            knownPeople = current;
            reconfigureRuntimes?.Invoke(knownPeople);
            AssignNpcProfiles(worldTime);
            nextNpcIndex = 0;
        }

        private void AssignNpcProfiles(double worldTime)
        {
            string player = Clean(playerPersonProvider());
            foreach (string person in knownPeople.Where(person => !string.Equals(person, player, StringComparison.Ordinal)))
            {
                if (decisions.TryGetState(person, out SocialDecisionPersonStateSnapshot state)
                    && !string.IsNullOrWhiteSpace(state.DecisionProfileId))
                {
                    continue;
                }

                decisions.AssignProfile(person, ResolveProfile(person), worldTime);
            }
        }

        private void RunDecisionBudget(double worldTime)
        {
            string player = Clean(playerPersonProvider());
            string[] npcs = knownPeople.Where(person => !string.Equals(person, player, StringComparison.Ordinal)).ToArray();
            if (npcs.Length == 0)
            {
                return;
            }

            int budget = Math.Min(npcs.Length, Math.Max(1, settings.maximumNpcDecisionsPerTick));
            for (int i = 0; i < budget; i++)
            {
                string actor = npcs[nextNpcIndex++ % npcs.Length];
                string[] targets = knownPeople.Where(person => !string.Equals(person, actor, StringComparison.Ordinal)).ToArray();
                if (targets.Length == 0)
                {
                    continue;
                }

                decisions.Evaluate(new SocialDecisionRequest
                {
                    ActorPersonId = actor,
                    AvailableTargetPersonIds = targets,
                    PlaceId = placeProvider(),
                    AudienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                    WorldTime = worldTime,
                    DeterministicSeed = $"social-ai.{actor}.{Math.Floor(worldTime)}",
                    ExecutionMode = SocialDecisionExecutionMode.SubmitForExecution,
                    ActorControlPolicy = SocialDecisionActorControlPolicy.AutonomousNpc,
                    CommitDecisionState = true
                });
            }
        }

        private void PruneBoundedHistory()
        {
            interactions.PruneHistory(settings.maximumInteractionHistory, settings.maximumProcessedTransactions);
            decisions.PruneHistory(settings.maximumDecisionHistoryPerPerson);
            rumors.PruneHistory(settings.maximumRumorTransmissions);
            norms.PruneHistory(settings.maximumNormAssessments);
            influence.PruneHistory(settings.maximumInfluenceAttempts);
            emotions.PruneHistory(settings.maximumEmotionEpisodesPerPerson);
        }

        private long MutationSignature()
        {
            unchecked
            {
                long value = relationships.Revision;
                value = (value * 397) ^ attitudes.Revision;
                value = (value * 397) ^ reputation.Revision;
                value = (value * 397) ^ rumors.Revision;
                value = (value * 397) ^ interactions.Revision;
                value = (value * 397) ^ norms.Revision;
                value = (value * 397) ^ networks.Revision;
                value = (value * 397) ^ decisions.Revision;
                value = (value * 397) ^ influence.Revision;
                value = (value * 397) ^ emotions.Revision;
                value = (value * 397) ^ family.Revision;
                return value;
            }
        }

        private static string ResolveProfile(string personId)
        {
            string value = Clean(personId);
            if (value.IndexOf("prisoner", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PrototypeSocialDecisionDefinitionFactory.ReservedProfileId;
            }

            if (value.IndexOf("guild", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PrototypeSocialDecisionDefinitionFactory.SupportiveGroupMemberProfileId;
            }

            return PrototypeSocialDecisionDefinitionFactory.SociableProfileId;
        }

        private static string Clean(string value) => value?.Trim() ?? string.Empty;
    }
}
