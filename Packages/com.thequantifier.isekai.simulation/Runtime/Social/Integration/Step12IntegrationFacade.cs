using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Persistence;
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
    public sealed class Step12SocialSimulationFacade
    {
        private readonly DefinitionRegistry registry;
        private readonly string worldId;
        private readonly string[] knownPersonIds;
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

        public Step12SocialSimulationFacade(
            DefinitionRegistry registry,
            IEnumerable<string> knownPersonIds,
            string worldId,
            RelationshipRuntime relationships,
            InterpersonalAttitudeRuntime attitudes,
            ReputationRuntime reputation,
            RumorRuntime rumors,
            SocialInteractionRuntime interactions,
            SocialNormRuntime norms,
            SocialNetworkRuntime networks,
            SocialDecisionRuntime decisions,
            SocialInfluenceRuntime influence,
            SocialEmotionRuntime emotions,
            FamilyRelationshipRuntime family)
        {
            this.registry = registry;
            this.knownPersonIds = Clean(knownPersonIds);
            this.worldId = Clean(worldId);
            this.relationships = relationships;
            this.attitudes = attitudes;
            this.reputation = reputation;
            this.rumors = rumors;
            this.interactions = interactions;
            this.norms = norms;
            this.networks = networks;
            this.decisions = decisions;
            this.influence = influence;
            this.emotions = emotions;
            this.family = family;
        }

        public IReadOnlyList<Step12AuthorityEntry> AuthorityMap => CreateAuthorityMap();
        public IReadOnlyList<Step12PersistenceDependencyEntry> PersistenceDependencies => CreatePersistenceDependencyGraph();

        public IReadOnlyList<Step12RuntimeSummary> CreateRuntimeSummaries()
        {
            RelationshipRuntimeSaveData relationshipSave = relationships?.CreateSaveData();
            InterpersonalAttitudeRuntimeSaveData attitudeSave = attitudes?.CreateSaveData();
            ReputationRuntimeSaveData reputationSave = reputation?.CreateSaveData();
            RumorRuntimeSaveData rumorSave = rumors?.CreateSaveData();
            SocialInteractionRuntimeSaveData interactionSave = interactions?.CreateSaveData();
            SocialNormRuntimeSaveData normSave = norms?.CreateSaveData();
            SocialNetworkRuntimeSaveData networkSave = networks?.CreateSaveData();
            SocialDecisionRuntimeSaveData decisionSave = decisions?.CreateSaveData();
            SocialInfluenceRuntimeSaveData influenceSave = influence?.CreateSaveData();
            SocialEmotionRuntimeSaveData emotionSave = emotions?.CreateSaveData();
            FamilyRelationshipRuntimeSaveData familySave = family?.CreateSaveData();

            return new[]
            {
                new Step12RuntimeSummary(nameof(RelationshipRuntime), RelationshipPersistenceParticipant.Key, relationships != null, relationships != null && relationshipSave != null, relationships?.Revision ?? 0L, relationshipSave?.records?.Count ?? 0),
                new Step12RuntimeSummary(nameof(InterpersonalAttitudeRuntime), InterpersonalAttitudePersistenceParticipant.Key, attitudes != null, attitudes != null && attitudeSave != null, attitudes?.Revision ?? 0L, attitudeSave?.records?.Count ?? 0),
                new Step12RuntimeSummary(nameof(ReputationRuntime), ReputationPersistenceParticipant.Key, reputation != null, reputation != null && reputationSave != null, reputation?.Revision ?? 0L, reputationSave?.records?.Count ?? 0),
                new Step12RuntimeSummary(nameof(RumorRuntime), RumorPersistenceParticipant.Key, rumors != null, rumors != null && rumorSave != null, rumors?.Revision ?? 0L, rumorSave?.rumors?.Length ?? 0, rumorSave?.transmissions?.Length ?? 0),
                new Step12RuntimeSummary(nameof(SocialInteractionRuntime), SocialInteractionPersistenceParticipant.Key, interactions != null, interactions != null && interactionSave != null, interactions?.Revision ?? 0L, interactionSave?.records?.Count ?? 0, interactionSave?.pendingInteractions?.Count ?? 0, interactionSave?.promises?.Count ?? 0),
                new Step12RuntimeSummary(nameof(SocialNormRuntime), SocialNormPersistenceParticipant.Key, norms != null, norms != null && normSave != null, norms?.Revision ?? 0L, normSave?.assessments?.Count ?? 0),
                new Step12RuntimeSummary(nameof(SocialNetworkRuntime), SocialNetworkPersistenceParticipant.Key, networks != null, networks != null && networkSave != null, networks?.Revision ?? 0L, networkSave?.groups?.Count ?? 0, networkSave?.memberships?.Count ?? 0),
                new Step12RuntimeSummary(nameof(SocialDecisionRuntime), SocialDecisionPersistenceParticipant.Key, decisions != null, decisions != null && decisionSave != null, decisions?.Revision ?? 0L, decisionSave?.personStates?.Count ?? 0),
                new Step12RuntimeSummary(nameof(SocialInfluenceRuntime), SocialInfluencePersistenceParticipant.Key, influence != null, influence != null && influenceSave != null, influence?.Revision ?? 0L, influenceSave?.attempts?.Count ?? 0, influenceSave?.decisionModifiers?.Count ?? 0),
                new Step12RuntimeSummary(nameof(SocialEmotionRuntime), SocialEmotionPersistenceParticipant.Key, emotions != null, emotions != null && emotionSave != null, emotions?.Revision ?? 0L, emotionSave?.episodes?.Count ?? 0, emotionSave?.moods?.Count ?? 0),
                new Step12RuntimeSummary(nameof(FamilyRelationshipRuntime), FamilyRelationshipPersistenceParticipant.Key, family != null, family != null && familySave != null, family?.Revision ?? 0L, familySave?.households?.Count ?? 0, familySave?.memberships?.Count ?? 0)
            };
        }

        public Step12IntegrationValidationReport ValidateComplete()
        {
            Step12IntegrationValidationReport report = new Step12IntegrationValidationReport();
            Step12SocialSimulationValidator.ValidateAuthorityMap(AuthorityMap, report);
            Step12SocialSimulationValidator.ValidatePersistenceDependencies(PersistenceDependencies, report);
            ValidateRuntimeReadiness(report);
            ValidateRuntimeSaveGraphs(report);
            Step12SocialSimulationValidator.ValidateSchedulerBudget(new Step12SchedulerBudget(), report);
            return report;
        }

        public Step12HealthSnapshot CreateHealthSnapshot()
        {
            Step12IntegrationValidationReport report = ValidateComplete();
            IReadOnlyList<Step12RuntimeSummary> runtimes = CreateRuntimeSummaries();
            Step12HealthStatus status = report.ErrorCount > 0
                ? Step12HealthStatus.Failed
                : report.WarningCount > 0 || runtimes.Any(item => !item.Ready)
                    ? Step12HealthStatus.Degraded
                    : Step12HealthStatus.Ready;
            string fingerprint = Fingerprint(runtimes.Select(item => $"{item.RuntimeName}:{item.Revision}:{item.PrimaryCount}:{item.SecondaryCount}:{item.TertiaryCount}")
                .Concat(report.Diagnostics.Select(item => item.ToString())));
            return new Step12HealthSnapshot(status, runtimes, report.Diagnostics, fingerprint);
        }

        public Step12SocialContextSnapshot CreateContextSnapshot(string requesterPersonId, string actorPersonId, string targetPersonId, double worldTime, Step12SocialContextOptions options = null)
        {
            Step12SocialContextOptions resolved = (options ?? new Step12SocialContextOptions()).Clone();
            List<Step12ContextRecordReference> records = new List<Step12ContextRecordReference>();
            List<string> diagnostics = new List<string>();
            bool truncated = false;
            string actor = Clean(actorPersonId);
            string target = Clean(targetPersonId);

            AddLimited(records, diagnostics, ref truncated, "relationships", resolved.MaxRelationships,
                relationships?.Snapshots
                    .Where(item => IncludesPerson(item.Participants.Select(participant => participant.personId), actor, target))
                    .Select(item => Reference(nameof(RelationshipRuntime), item.RecordId, Step12SocialProjectionState.AuthoritativeFact, MapVisibility(item.AccessPolicyId), item.RelationshipDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "attitudes", resolved.MaxAttitudes,
                attitudes?.CreateSaveData()?.records
                    .Where(item => MatchesPair(item.observerPersonId, item.subjectPersonId, actor, target))
                    .Select(item => Reference(nameof(InterpersonalAttitudeRuntime), item.recordId, Step12SocialProjectionState.InferredState, Step12SocialVisibility.ParticipantKnown, $"{item.observerPersonId}->{item.subjectPersonId}")));

            AddLimited(records, diagnostics, ref truncated, "reputation", resolved.MaxReputations,
                reputation?.CreateSaveData()?.records
                    .Where(item => string.Equals(item.subjectPersonId, actor, StringComparison.Ordinal) || string.Equals(item.subjectPersonId, target, StringComparison.Ordinal))
                    .Select(item => Reference(nameof(ReputationRuntime), item.recordId, Step12SocialProjectionState.InferredState, Step12SocialVisibility.Public, item.audienceId)));

            AddLimited(records, diagnostics, ref truncated, "rumors", resolved.MaxRumors,
                rumors?.CreateSaveData()?.rumors
                    .Where(item => IncludesPerson(item.subjectIds, actor, target) || MatchesPerson(item.originatorPersonId, actor, target))
                    .Select(item => Reference(nameof(RumorRuntime), item.rumorId, Step12SocialProjectionState.RumoredState, MapVisibility(item.disclosure), item.claimIdentity)));

            AddLimited(records, diagnostics, ref truncated, "interactions", resolved.MaxInteractions,
                interactions?.CreateSaveData()?.records
                    .Where(item => MatchesPair(item.initiatorPersonId, item.targetPersonId, actor, target) || IncludesPerson(item.participants?.Select(participant => participant.personId), actor, target))
                    .Select(item => Reference(nameof(SocialInteractionRuntime), item.interactionRecordId, Step12SocialProjectionState.AuthoritativeFact, MapVisibility(item.visibility), item.interactionDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "norms", resolved.MaxNorms,
                norms?.CreateSaveData()?.assessments
                    .Where(item => MatchesPair(item.actorPersonId, item.targetPersonId, actor, target) || IncludesPerson(item.witnessPersonIds, actor, target))
                    .Select(item => Reference(nameof(SocialNormRuntime), item.assessmentRecordId, Step12SocialProjectionState.AuthoritativeFact, MapVisibility(item.visibility), item.normDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "groups", resolved.MaxGroups,
                networks?.CreateSaveData()?.memberships
                    .Where(item => MatchesPerson(item.personId, actor, target))
                    .Select(item => Reference(nameof(SocialNetworkRuntime), item.membershipId, Step12SocialProjectionState.InferredState, Step12SocialVisibility.MemberVisible, item.groupId)));

            AddLimited(records, diagnostics, ref truncated, "decisions", resolved.MaxInteractions,
                decisions?.CreateSaveData()?.personStates
                    .Where(item => MatchesPair(item.personId, item.activeTargetPersonId, actor, target))
                    .Select(item => Reference(nameof(SocialDecisionRuntime), item.activeDecisionId, Step12SocialProjectionState.DiagnosticOnly, Step12SocialVisibility.Diagnostic, item.activeIntentionDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "influence", resolved.MaxInfluenceAttempts,
                influence?.CreateSaveData()?.attempts
                    .Where(item => MatchesPair(item.speakerPersonId, item.targetPersonId, actor, target))
                    .Select(item => Reference(nameof(SocialInfluenceRuntime), item.attemptId, Step12SocialProjectionState.AuthoritativeFact, MapVisibility(item.visibility), item.methodDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "emotions", resolved.MaxEmotions,
                emotions?.CreateSaveData()?.episodes
                    .Where(item => MatchesPair(item.personId, item.targetPersonId, actor, target))
                    .Select(item => Reference(nameof(SocialEmotionRuntime), item.episodeId, Step12SocialProjectionState.InferredState, MapVisibility(item.visibility), item.emotionDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "households", resolved.MaxHouseholds,
                family?.CreateSaveData()?.memberships
                    .Where(item => MatchesPerson(item.personId, actor, target))
                    .Select(item => Reference(nameof(FamilyRelationshipRuntime), item.membershipId, Step12SocialProjectionState.AuthoritativeFact, Step12SocialVisibility.FamilyKnown, item.householdId)));

            string fingerprint = Fingerprint(records
                .OrderBy(item => item.RuntimeName, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .Select(item => $"{item.RuntimeName}:{item.RecordId}:{item.ProjectionState}:{item.Visibility}:{item.Summary}")
                .Concat(CreateRuntimeSummaries().Select(item => $"{item.RuntimeName}:{item.Revision}:{item.PrimaryCount}:{item.SecondaryCount}:{item.TertiaryCount}")));

            return new Step12SocialContextSnapshot(requesterPersonId, actor, target, worldTime, records, CreateRuntimeSummaries(), diagnostics, truncated, fingerprint);
        }

        public Step12ConsequenceReference CreateConsequenceReference(string sourceFeature, string sourceRecordId, string sourceTransactionId, string destinationRuntime, string destinationRecordId, string operation, double worldTime, long revision, Step12SocialVisibility visibility = Step12SocialVisibility.Public, bool active = true)
        {
            return new Step12ConsequenceReference(sourceFeature, sourceRecordId, sourceTransactionId, destinationRuntime, destinationRecordId, operation, worldTime, revision, visibility, active);
        }

        private void ValidateRuntimeReadiness(Step12IntegrationValidationReport report)
        {
            if (registry == null)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.DefinitionCatalog, "missing-registry", "The social integration facade requires a DefinitionRegistry.");
            }

            foreach (Step12RuntimeSummary summary in CreateRuntimeSummaries())
            {
                if (!summary.Present)
                {
                    report.AddError(Step12IntegrationDiagnosticDomain.RuntimeReadiness, "missing-runtime", $"{summary.RuntimeName} is not present.", summary.RuntimeName);
                }
            }
        }

        private void ValidateRuntimeSaveGraphs(Step12IntegrationValidationReport report)
        {
            if (registry == null)
            {
                return;
            }

            if (relationships != null && !RelationshipRuntime.ValidateSaveData(relationships.CreateSaveData(), registry, knownPersonIds, out string relationshipFailure))
            {
                ValidateSave(report, RelationshipPersistenceParticipant.Key, relationshipFailure);
            }

            if (attitudes != null && !InterpersonalAttitudeRuntime.ValidateSaveData(attitudes.CreateSaveData(), registry, knownPersonIds, out string attitudeFailure))
            {
                ValidateSave(report, InterpersonalAttitudePersistenceParticipant.Key, attitudeFailure);
            }

            if (reputation != null && !ReputationRuntime.ValidateSaveData(reputation.CreateSaveData(), registry, knownPersonIds, out string reputationFailure))
            {
                ValidateSave(report, ReputationPersistenceParticipant.Key, reputationFailure);
            }

            if (rumors != null && !RumorRuntime.ValidateSaveData(rumors.CreateSaveData(), registry, knownPersonIds, out string rumorFailure))
            {
                ValidateSave(report, RumorPersistenceParticipant.Key, rumorFailure);
            }

            if (interactions != null && !SocialInteractionRuntime.ValidateSaveData(interactions.CreateSaveData(), registry, knownPersonIds, out string interactionFailure))
            {
                ValidateSave(report, SocialInteractionPersistenceParticipant.Key, interactionFailure);
            }

            if (norms != null && !SocialNormRuntime.ValidateSaveData(norms.CreateSaveData(), registry, knownPersonIds, out string normFailure))
            {
                ValidateSave(report, SocialNormPersistenceParticipant.Key, normFailure);
            }

            if (networks != null && !SocialNetworkRuntime.ValidateSaveData(networks.CreateSaveData(), registry, knownPersonIds, out string networkFailure))
            {
                ValidateSave(report, SocialNetworkPersistenceParticipant.Key, networkFailure);
            }

            if (decisions != null && !SocialDecisionRuntime.ValidateSaveData(decisions.CreateSaveData(), registry, knownPersonIds, out string decisionFailure))
            {
                ValidateSave(report, SocialDecisionPersistenceParticipant.Key, decisionFailure);
            }

            if (influence != null && !SocialInfluenceRuntime.ValidateSaveData(influence.CreateSaveData(), registry, knownPersonIds, out string influenceFailure))
            {
                ValidateSave(report, SocialInfluencePersistenceParticipant.Key, influenceFailure);
            }

            if (emotions != null && !SocialEmotionRuntime.ValidateSaveData(emotions.CreateSaveData(), registry, knownPersonIds, out string emotionFailure))
            {
                ValidateSave(report, SocialEmotionPersistenceParticipant.Key, emotionFailure);
            }

            if (family != null && !FamilyRelationshipRuntime.ValidateSaveData(family.CreateSaveData(), registry, knownPersonIds, worldId, out string familyFailure))
            {
                ValidateSave(report, FamilyRelationshipPersistenceParticipant.Key, familyFailure);
            }
        }

        private static void ValidateSave(Step12IntegrationValidationReport report, string participantKey, string failure)
        {
            report.AddError(Step12IntegrationDiagnosticDomain.Persistence, "invalid-save-graph", failure, participantKey);
        }

        public static IReadOnlyList<Step12AuthorityEntry> CreateAuthorityMap()
        {
            return new[]
            {
                new Step12AuthorityEntry("relationships", "12.1", "Relationship Records", nameof(RelationshipRuntime), false, nameof(SocialInteractionRuntime), nameof(FamilyRelationshipRuntime), nameof(SocialNetworkRuntime)),
                new Step12AuthorityEntry("attitudes", "12.2", "Interpersonal Attitudes", nameof(InterpersonalAttitudeRuntime), false, nameof(SocialDecisionRuntime), nameof(SocialInfluenceRuntime), nameof(SocialEmotionRuntime), nameof(SocialNetworkRuntime)),
                new Step12AuthorityEntry("reputation", "12.3", "Audience Reputation", nameof(ReputationRuntime), false, nameof(SocialDecisionRuntime), nameof(SocialInfluenceRuntime), nameof(SocialNetworkRuntime)),
                new Step12AuthorityEntry("rumors", "12.4", "Rumors and Transmissions", nameof(RumorRuntime), false, nameof(SocialInteractionRuntime), nameof(SocialNetworkRuntime)),
                new Step12AuthorityEntry("interactions", "12.5", "Social Interactions", nameof(SocialInteractionRuntime), false, nameof(SocialNormRuntime), nameof(SocialDecisionRuntime), nameof(SocialInfluenceRuntime)),
                new Step12AuthorityEntry("norms", "12.6", "Social Norm Assessments", nameof(SocialNormRuntime), false, nameof(SocialInteractionRuntime), nameof(SocialDecisionRuntime)),
                new Step12AuthorityEntry("informal-groups", "12.7", "Informal Groups", nameof(SocialNetworkRuntime), false, nameof(SocialDecisionRuntime)),
                new Step12AuthorityEntry("social-graph", "12.7", "Derived Social Graph", nameof(SocialNetworkRuntime), true, nameof(RelationshipRuntime), nameof(InterpersonalAttitudeRuntime), nameof(ReputationRuntime), nameof(RumorRuntime), nameof(SocialInteractionRuntime), nameof(SocialNormRuntime)),
                new Step12AuthorityEntry("decisions", "12.8", "Social Decisions", nameof(SocialDecisionRuntime), false, nameof(SocialInteractionRuntime)),
                new Step12AuthorityEntry("influence", "12.9", "Influence Attempts", nameof(SocialInfluenceRuntime), false, nameof(SocialDecisionRuntime), nameof(SocialEmotionRuntime)),
                new Step12AuthorityEntry("emotions", "12.10", "Social Emotions and Moods", nameof(SocialEmotionRuntime), false, nameof(SocialDecisionRuntime)),
                new Step12AuthorityEntry("kinship", "12.11", "Derived Kinship", nameof(FamilyRelationshipRuntime), true, nameof(RelationshipRuntime), nameof(InterpersonalAttitudeRuntime)),
                new Step12AuthorityEntry("households", "12.11", "Households", nameof(FamilyRelationshipRuntime), false, nameof(RelationshipRuntime))
            };
        }

        public static IReadOnlyList<Step12PersistenceDependencyEntry> CreatePersistenceDependencyGraph()
        {
            return new[]
            {
                new Step12PersistenceDependencyEntry(RelationshipPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(InterpersonalAttitudePersistenceParticipant.Key, RelationshipPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(ReputationPersistenceParticipant.Key, AuthoritativeHistoryPersistenceParticipant.Key, KnowledgeRecordPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(RumorPersistenceParticipant.Key, PersonKnowledgePersistenceParticipant.Key, PersonMemoryPersistenceParticipant.Key, AuthoritativeHistoryPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(SocialInteractionPersistenceParticipant.Key, RelationshipPersistenceParticipant.Key, InterpersonalAttitudePersistenceParticipant.Key, ReputationPersistenceParticipant.Key, RumorPersistenceParticipant.Key, AuthoritativeHistoryPersistenceParticipant.Key, PersonMemoryPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(SocialNormPersistenceParticipant.Key, SocialInteractionPersistenceParticipant.Key, InterpersonalAttitudePersistenceParticipant.Key, ReputationPersistenceParticipant.Key, RumorPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(SocialNetworkPersistenceParticipant.Key, RelationshipPersistenceParticipant.Key, InterpersonalAttitudePersistenceParticipant.Key, ReputationPersistenceParticipant.Key, RumorPersistenceParticipant.Key, SocialInteractionPersistenceParticipant.Key, SocialNormPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(SocialDecisionPersistenceParticipant.Key, RelationshipPersistenceParticipant.Key, InterpersonalAttitudePersistenceParticipant.Key, ReputationPersistenceParticipant.Key, SocialInteractionPersistenceParticipant.Key, SocialInfluencePersistenceParticipant.Key, SocialEmotionPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(SocialInfluencePersistenceParticipant.Key, PersonKnowledgePersistenceParticipant.Key, SocialInteractionPersistenceParticipant.Key, InterpersonalAttitudePersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(SocialEmotionPersistenceParticipant.Key, SocialInfluencePersistenceParticipant.Key, SocialInteractionPersistenceParticipant.Key),
                new Step12PersistenceDependencyEntry(FamilyRelationshipPersistenceParticipant.Key, RelationshipPersistenceParticipant.Key, InterpersonalAttitudePersistenceParticipant.Key)
            };
        }

        private static Step12ContextRecordReference Reference(string runtimeName, string recordId, Step12SocialProjectionState projectionState, Step12SocialVisibility visibility, string summary)
        {
            return new Step12ContextRecordReference(runtimeName, recordId, projectionState, visibility, summary);
        }

        private static void AddLimited(List<Step12ContextRecordReference> records, List<string> diagnostics, ref bool truncated, string label, int limit, IEnumerable<Step12ContextRecordReference> candidates)
        {
            if (limit <= 0 || candidates == null)
            {
                return;
            }

            Step12ContextRecordReference[] ordered = candidates
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.RecordId))
                .OrderBy(item => item.RuntimeName, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .ToArray();
            records.AddRange(ordered.Take(limit));
            if (ordered.Length > limit)
            {
                truncated = true;
                diagnostics.Add($"{label} truncated {ordered.Length}->{limit}");
            }
        }

        private static bool MatchesPair(string first, string second, string actor, string target)
        {
            return MatchesPerson(first, actor, target) || MatchesPerson(second, actor, target);
        }

        private static bool MatchesPerson(string personId, string actor, string target)
        {
            return (!string.IsNullOrWhiteSpace(actor) && string.Equals(personId, actor, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(target) && string.Equals(personId, target, StringComparison.Ordinal));
        }

        private static bool IncludesPerson(IEnumerable<string> personIds, string actor, string target)
        {
            return (personIds ?? Array.Empty<string>()).Any(person => MatchesPerson(person, actor, target));
        }

        private static Step12SocialVisibility MapVisibility(string accessPolicyId)
        {
            string value = Clean(accessPolicyId).ToLowerInvariant();
            if (value.Contains("hidden"))
            {
                return Step12SocialVisibility.Hidden;
            }

            if (value.Contains("secret"))
            {
                return Step12SocialVisibility.Secret;
            }

            if (value.Contains("confidential") || value.Contains("private"))
            {
                return Step12SocialVisibility.Confidential;
            }

            return Step12SocialVisibility.Public;
        }

        private static Step12SocialVisibility MapVisibility(RumorDisclosure disclosure)
        {
            return disclosure switch
            {
                RumorDisclosure.Secret => Step12SocialVisibility.Secret,
                RumorDisclosure.Private => Step12SocialVisibility.Confidential,
                RumorDisclosure.Public => Step12SocialVisibility.Public,
                _ => Step12SocialVisibility.Observable
            };
        }

        private static Step12SocialVisibility MapVisibility(SocialInteractionVisibility visibility)
        {
            return visibility switch
            {
                SocialInteractionVisibility.Private => Step12SocialVisibility.Confidential,
                SocialInteractionVisibility.Public => Step12SocialVisibility.Public,
                SocialInteractionVisibility.Witnessed => Step12SocialVisibility.Observable,
                _ => Step12SocialVisibility.ParticipantKnown
            };
        }

        private static Step12SocialVisibility MapVisibility(SocialNormVisibility visibility)
        {
            return visibility switch
            {
                SocialNormVisibility.Private => Step12SocialVisibility.Confidential,
                SocialNormVisibility.Public => Step12SocialVisibility.Public,
                _ => Step12SocialVisibility.Observable
            };
        }

        private static Step12SocialVisibility MapVisibility(SocialInfluenceVisibility visibility)
        {
            return visibility switch
            {
                SocialInfluenceVisibility.Private => Step12SocialVisibility.Confidential,
                SocialInfluenceVisibility.Public => Step12SocialVisibility.Public,
                _ => Step12SocialVisibility.Observable
            };
        }

        private static Step12SocialVisibility MapVisibility(SocialEmotionVisibility visibility)
        {
            return visibility switch
            {
                SocialEmotionVisibility.Public => Step12SocialVisibility.Public,
                SocialEmotionVisibility.Internal => Step12SocialVisibility.Hidden,
                _ => Step12SocialVisibility.Observable
            };
        }

        private static string Fingerprint(IEnumerable<string> parts)
        {
            string joined = string.Join("\n", parts ?? Array.Empty<string>());
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step12SocialSimulationTransactionCoordinator
    {
        private readonly HashSet<string> completedTransactionIds = new HashSet<string>(StringComparer.Ordinal);

        public Step12TransactionResult Execute(string transactionId, IEnumerable<Step12TransactionParticipantPlan> participants, bool preview = false)
        {
            string tx = Clean(transactionId);
            List<Step12TransactionParticipantResult> results = new List<Step12TransactionParticipantResult>();
            List<string> diagnostics = new List<string>();

            if (string.IsNullOrWhiteSpace(tx))
            {
                return new Step12TransactionResult(false, tx, preview, false, results, new[] { "Transaction ID is required." });
            }

            if (!preview && completedTransactionIds.Contains(tx))
            {
                return new Step12TransactionResult(true, tx, false, true, results, new[] { "Duplicate transaction ignored." });
            }

            Step12TransactionParticipantPlan[] ordered = (participants ?? Array.Empty<Step12TransactionParticipantPlan>())
                .Where(item => item != null)
                .OrderBy(item => item.RuntimeName, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length == 0)
            {
                return new Step12TransactionResult(false, tx, preview, false, results, new[] { "At least one participant is required." });
            }

            Step12TransactionStage firstStage = preview ? Step12TransactionStage.Preview : Step12TransactionStage.Prepare;
            if (!RunStage(ordered, firstStage, results, diagnostics, out Step12TransactionParticipantPlan failed))
            {
                return new Step12TransactionResult(false, tx, preview, false, results, diagnostics);
            }

            if (preview)
            {
                return new Step12TransactionResult(true, tx, true, false, results, diagnostics);
            }

            if (!RunStage(ordered, Step12TransactionStage.Commit, results, diagnostics, out failed))
            {
                Rollback(ordered, results);
                return new Step12TransactionResult(false, tx, false, false, results, diagnostics);
            }

            RunStage(ordered, Step12TransactionStage.PostCommit, results, diagnostics, out failed, failRequired: false);
            completedTransactionIds.Add(tx);
            return new Step12TransactionResult(true, tx, false, false, results, diagnostics);
        }

        private static bool RunStage(IReadOnlyList<Step12TransactionParticipantPlan> participants, Step12TransactionStage stage, List<Step12TransactionParticipantResult> results, List<string> diagnostics, out Step12TransactionParticipantPlan failed, bool failRequired = true)
        {
            failed = null;
            foreach (Step12TransactionParticipantPlan participant in participants)
            {
                Func<bool> action = stage switch
                {
                    Step12TransactionStage.Preview => participant.Preview,
                    Step12TransactionStage.Prepare => participant.Prepare,
                    Step12TransactionStage.Commit => participant.Commit,
                    Step12TransactionStage.PostCommit => participant.PostCommit,
                    _ => null
                };

                bool succeeded = action == null || action();
                results.Add(new Step12TransactionParticipantResult(participant.RuntimeName, stage, succeeded, participant.FailurePolicy));
                if (!succeeded && failRequired && participant.FailurePolicy == Step12TransactionFailurePolicy.Required)
                {
                    failed = participant;
                    diagnostics.Add($"{stage} failed for required participant {participant.RuntimeName}.");
                    return false;
                }
            }

            return true;
        }

        private static void Rollback(IReadOnlyList<Step12TransactionParticipantPlan> participants, List<Step12TransactionParticipantResult> results)
        {
            foreach (Step12TransactionParticipantPlan participant in participants.Reverse())
            {
                bool succeeded = participant.Rollback == null || participant.Rollback();
                results.Add(new Step12TransactionParticipantResult(participant.RuntimeName, Step12TransactionStage.Rollback, succeeded, participant.FailurePolicy));
            }
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step12SchedulerBudget
    {
        public int MaximumEvaluationsPerTick { get; set; } = 64;
        public int MaximumRecursionDepth { get; set; } = 3;
        public int MaximumQueuedConsequences { get; set; } = 128;
        public bool UseSystemTime { get; set; }
        public bool AllowImmediateRecursiveDispatch { get; set; }
    }

    public static class Step12SocialSimulationValidator
    {
        public static void ValidateAuthorityMap(IEnumerable<Step12AuthorityEntry> authorityMap, Step12IntegrationValidationReport report)
        {
            Step12AuthorityEntry[] entries = (authorityMap ?? Array.Empty<Step12AuthorityEntry>()).Where(item => item != null).ToArray();
            if (entries.Length == 0)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Authority, "missing-authority-map", "Step 12 authority map is empty.");
                return;
            }

            foreach (IGrouping<string, Step12AuthorityEntry> duplicate in entries.GroupBy(item => item.DomainId, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Authority, "duplicate-domain", $"Domain '{duplicate.Key}' has multiple authority entries.", duplicate.Key);
            }

            foreach (Step12AuthorityEntry entry in entries.Where(item => string.IsNullOrWhiteSpace(item.AuthoritativeRuntime) || string.IsNullOrWhiteSpace(item.FeatureId)))
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Authority, "incomplete-authority-entry", "Authority entries require a feature ID and authoritative runtime.", entry.DomainId);
            }
        }

        public static void ValidatePersistenceDependencies(IEnumerable<Step12PersistenceDependencyEntry> dependencies, Step12IntegrationValidationReport report)
        {
            Step12PersistenceDependencyEntry[] entries = (dependencies ?? Array.Empty<Step12PersistenceDependencyEntry>()).Where(item => item != null).ToArray();
            HashSet<string> keys = new HashSet<string>(entries.Select(item => item.ParticipantKey), StringComparer.Ordinal);
            foreach (Step12PersistenceDependencyEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.ParticipantKey))
                {
                    report.AddError(Step12IntegrationDiagnosticDomain.Persistence, "empty-participant-key", "Persistence dependency participant key is empty.");
                }

                foreach (string dependency in entry.DependsOn)
                {
                    if (string.Equals(dependency, entry.ParticipantKey, StringComparison.Ordinal))
                    {
                        report.AddError(Step12IntegrationDiagnosticDomain.Persistence, "self-dependency", $"Participant '{entry.ParticipantKey}' depends on itself.", entry.ParticipantKey);
                    }
                    else if (keys.Contains(dependency) && HasPath(dependency, entry.ParticipantKey, entries, new HashSet<string>(StringComparer.Ordinal)))
                    {
                        report.AddError(Step12IntegrationDiagnosticDomain.Persistence, "dependency-cycle", $"Participant '{entry.ParticipantKey}' participates in a dependency cycle through '{dependency}'.", entry.ParticipantKey);
                    }
                }
            }
        }

        public static void ValidateSchedulerBudget(Step12SchedulerBudget budget, Step12IntegrationValidationReport report)
        {
            if (budget == null)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Scheduler, "missing-budget", "Scheduler budget is required.");
                return;
            }

            if (budget.MaximumEvaluationsPerTick <= 0 || budget.MaximumQueuedConsequences <= 0)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Scheduler, "invalid-limit", "Scheduler evaluation and queue limits must be positive.");
            }

            if (budget.MaximumRecursionDepth < 0 || budget.MaximumRecursionDepth > 8)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Recursion, "invalid-recursion-limit", "Scheduler recursion depth must be bounded between 0 and 8.");
            }

            if (budget.UseSystemTime)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Determinism, "system-time", "Social integration scheduling must use explicit world time, not system time.");
            }

            if (budget.AllowImmediateRecursiveDispatch)
            {
                report.AddError(Step12IntegrationDiagnosticDomain.Recursion, "immediate-recursion", "Immediate recursive social dispatch is not allowed.");
            }
        }

        private static bool HasPath(string start, string target, IReadOnlyList<Step12PersistenceDependencyEntry> entries, HashSet<string> visited)
        {
            if (!visited.Add(start))
            {
                return false;
            }

            Step12PersistenceDependencyEntry entry = entries.FirstOrDefault(item => string.Equals(item.ParticipantKey, start, StringComparison.Ordinal));
            if (entry == null)
            {
                return false;
            }

            return entry.DependsOn.Any(dependency => string.Equals(dependency, target, StringComparison.Ordinal) || HasPath(dependency, target, entries, visited));
        }
    }
}
