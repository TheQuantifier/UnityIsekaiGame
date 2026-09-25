using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Integration;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Gameplay
{
    public sealed partial class PrototypePersistenceServiceBehaviour
    {
        [Header("Social Simulation")]
        [SerializeField] private SocialSimulationSettings socialSimulationSettings = new SocialSimulationSettings();
        [SerializeField] private bool enableSocialDebugPanel = true;

        private SocialSimulationCoordinator socialSimulation;
        private PrototypeSocialDebugPanel socialDebugPanel;
        private WorldSocialCognitionPersistenceParticipant worldSocialCognitionParticipant;
        private bool socialCombatSubscribed;
        private bool socialCrimeSubscribed;
        private bool socialOrganizationSubscribed;
        private readonly Dictionary<string, PersonKnowledgeRuntime> npcKnowledgeByPerson = new Dictionary<string, PersonKnowledgeRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonMemoryRuntime> npcMemoryByPerson = new Dictionary<string, PersonMemoryRuntime>(StringComparer.Ordinal);
        private Transform npcCognitionRoot;

        public IReadOnlyList<string> KnownSocialPersonIds => socialSimulation?.KnownPeople ?? GetPrototypeSocialPersonIds(ResolvePlayerPersonId());

        public SocialInteractionResult RecordSocialInteraction(
            string interactionDefinitionId,
            string initiatorPersonId,
            string targetPersonId,
            string originatingReferenceId = "",
            string transactionId = "")
        {
            EnsureGroup9SocialRuntime();
            string tx = string.IsNullOrWhiteSpace(transactionId)
                ? $"social.gameplay.{Guid.NewGuid():N}"
                : transactionId.Trim();
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            return socialSimulation.RecordInteraction(
                interactionDefinitionId,
                initiatorPersonId,
                targetPersonId,
                tx,
                worldTime,
                originatingReferenceId);
        }

        public string BuildSocialDebugReport(string targetPersonId = "")
        {
            EnsureGroup9SocialRuntime();
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            return socialSimulation.BuildDebugReport(ResolvePlayerPersonId(), targetPersonId, worldTime);
        }

        private void EnsureGroup9SocialRuntime()
        {
            EnsureNpcCognition(GetPrototypeSocialPersonIds(ResolvePlayerPersonId()));
            socialSimulation ??= new SocialSimulationCoordinator(
                () => GetPrototypeSocialPersonIds(ResolvePlayerPersonId()),
                ResolvePlayerPersonId,
                () => currentPlaceTracker == null ? string.Empty : currentPlaceTracker.CurrentPlaceId,
                ReconfigureSocialRuntimes,
                reason => dirtyTracker?.MarkDirty(reason),
                socialSimulationSettings,
                Relationships,
                InterpersonalAttitudes,
                Reputation,
                Rumors,
                SocialInteractions,
                SocialNorms,
                SocialNetworks,
                SocialDecisions,
                SocialInfluence,
                SocialEmotions,
                FamilyRelationships);

            if (!socialCombatSubscribed)
            {
                SceneCombatDamageBridge.DamageApplied += HandleSceneCombatDamageApplied;
                socialCombatSubscribed = true;
            }

            if (!socialCrimeSubscribed)
            {
                Crimes.StateChanged += HandleCrimeStateChanged;
                socialCrimeSubscribed = true;
            }

            if (!socialOrganizationSubscribed)
            {
                OrganizationDecisions.OperationCommitted += HandleOrganizationDecisionCommitted;
                socialOrganizationSubscribed = true;
            }

            if (enableSocialDebugPanel && socialDebugPanel == null)
            {
                socialDebugPanel = GetComponent<PrototypeSocialDebugPanel>();
                if (socialDebugPanel == null)
                {
                    socialDebugPanel = gameObject.AddComponent<PrototypeSocialDebugPanel>();
                }
                socialDebugPanel.Configure(this);
            }
        }

        private void DisableGroup9SocialRuntime()
        {
            if (socialCombatSubscribed)
            {
                SceneCombatDamageBridge.DamageApplied -= HandleSceneCombatDamageApplied;
                socialCombatSubscribed = false;
            }

            if (worldSocialCognitionParticipant != null)
            {
                UnregisterParticipant(worldSocialCognitionParticipant);
                worldSocialCognitionParticipant = null;
            }

            if (socialCrimeSubscribed && worldCrimes != null)
            {
                worldCrimes.StateChanged -= HandleCrimeStateChanged;
                socialCrimeSubscribed = false;
            }
            if (socialOrganizationSubscribed && worldOrganizationDecisions != null)
            {
                worldOrganizationDecisions.OperationCommitted -= HandleOrganizationDecisionCommitted;
                socialOrganizationSubscribed = false;
            }
        }

        private void EnsureWorldSocialCognitionParticipant()
        {
            if (worldSocialCognitionParticipant != null || definitionCatalog == null)
            {
                return;
            }

            EnsureNpcCognition(GetPrototypeSocialPersonIds(ResolvePlayerPersonId()));
            worldSocialCognitionParticipant = new WorldSocialCognitionPersistenceParticipant(
                () => npcKnowledgeByPerson,
                () => npcMemoryByPerson,
                EnsureNpcCognition,
                GetDefinitionRegistry,
                () => AuthoritativeHistory,
                () => GetPrototypeSocialPersonIds(ResolvePlayerPersonId()),
                worldService == null ? PersistenceService.LocalWorldId : worldService.WorldId);
            RegisterParticipant(worldSocialCognitionParticipant, out string failure);
            if (!string.IsNullOrWhiteSpace(failure))
            {
                Debug.LogWarning(failure);
                worldSocialCognitionParticipant = null;
            }
        }

        private void AdvanceGroup9SocialSimulation()
        {
            if (socialSimulation == null)
            {
                return;
            }

            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            socialSimulation.Advance(worldTime);
        }

        private void ReconfigureSocialRuntimes(IReadOnlyList<string> people)
        {
            string[] known = (people ?? Array.Empty<string>()).ToArray();
            EnsureNpcCognition(known);
            DefinitionRegistry registry = GetDefinitionRegistry();
            Relationships.Configure(registry, known);
            InterpersonalAttitudes.Configure(registry, known);
            Reputation.Configure(registry, known);
            Rumors.Configure(registry, known, ResolveKnowledgeRuntimeForPerson, ResolveMemoryRuntimeForPerson);
            SocialInteractions.Configure(registry, known, Relationships, InterpersonalAttitudes, Reputation, Rumors);
            SocialNorms.Configure(registry, known, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions);
            SocialNetworks.Configure(registry, known, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions, SocialNorms);
            SocialInfluence.Configure(registry, known, InterpersonalAttitudes, Reputation, SocialInteractions,
                known.Select(ResolveKnowledgeRuntimeForPerson).Where(runtime => runtime != null));
            SocialEmotions.Configure(registry, known, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialInteractions, SocialNorms, SocialNetworks, SocialInfluence);
            SocialDecisions.Configure(registry, known, SocialInteractions, Relationships, InterpersonalAttitudes, Reputation, Rumors, SocialNorms, SocialNetworks, SocialDecisionModifierSourceCollection.Compose(SocialInfluence, SocialEmotions));
            FamilyRelationships.Configure(registry, known, Relationships, InterpersonalAttitudes, SocialInteractions,
                worldService == null ? PersistenceService.LocalWorldId : worldService.WorldId,
                GetPrototypeAdultPersonIds(ResolvePlayerPersonId()));
        }

        private void EnsureNpcCognition(IEnumerable<string> people)
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            string player = ResolvePlayerPersonId();
            string[] known = (people ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string person in known.Where(person => !string.Equals(person, player, StringComparison.Ordinal)))
            {
                if (!npcKnowledgeByPerson.TryGetValue(person, out PersonKnowledgeRuntime knowledge))
                {
                    knowledge = CreateNpcKnowledgeRuntime(person);
                    npcKnowledgeByPerson.Add(person, knowledge);
                }
                knowledge.Configure(registry, person, person, string.Empty, restoring: false);

                if (!npcMemoryByPerson.TryGetValue(person, out PersonMemoryRuntime memory))
                {
                    memory = new PersonMemoryRuntime();
                    npcMemoryByPerson.Add(person, memory);
                }
                memory.Configure(person, registry, AuthoritativeHistory, known);
            }
        }

        private PersonKnowledgeRuntime CreateNpcKnowledgeRuntime(string personId)
        {
            if (npcCognitionRoot == null)
            {
                GameObject root = new GameObject("[Runtime NPC Social Cognition]");
                root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                root.transform.SetParent(transform, false);
                npcCognitionRoot = root.transform;
            }

            GameObject host = new GameObject($"Knowledge - {personId}");
            host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            host.transform.SetParent(npcCognitionRoot, false);
            return host.AddComponent<PersonKnowledgeRuntime>();
        }

        private void HandleSceneCombatDamageApplied(GameObject source, GameObject target, DamageApplicationResult result)
        {
            if (result == null || !result.Succeeded || !result.HealthChanged)
            {
                return;
            }

            string sourcePerson = ResolveScenePersonId(source);
            string targetPerson = ResolveScenePersonId(target);
            if (string.IsNullOrWhiteSpace(sourcePerson) || string.IsNullOrWhiteSpace(targetPerson) || string.Equals(sourcePerson, targetPerson, StringComparison.Ordinal))
            {
                return;
            }

            RecordSocialInteraction(
                PrototypeSocialInteractionDefinitionFactory.AttackId,
                sourcePerson,
                targetPerson,
                "combat.damage",
                $"social.combat.{result.Request.TransactionId}");
        }

        private void HandleCrimeStateChanged(CrimeMutationEvent change)
        {
            if (change == null || change.Result == null || !change.Result.Succeeded || change.Result.Preview || change.Result.Duplicate)
            {
                return;
            }

            string personId = string.Empty;
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            if (string.Equals(change.Operation, "create-wanted-status", StringComparison.Ordinal)
                && Crimes.TryGetWantedStatus(change.SubjectId, out WantedStatusRecordData wanted))
            {
                personId = wanted.subjectId;
                worldTime = wanted.activeWorldTime;
            }
            else if (string.Equals(change.Operation, "publish-wanted-notice", StringComparison.Ordinal))
            {
                WantedNoticeRecordData notice = Crimes.WantedNotices.FirstOrDefault(item => string.Equals(item.noticeId, change.SubjectId, StringComparison.Ordinal));
                if (notice != null && Crimes.TryGetWantedStatus(notice.wantedStatusId, out WantedStatusRecordData noticeStatus))
                {
                    personId = noticeStatus.subjectId;
                    worldTime = notice.publishedWorldTime;
                }
            }

            if (string.IsNullOrWhiteSpace(personId))
            {
                return;
            }

            Reputation.Mutate(new ReputationMutationRequest
            {
                transactionId = $"social.crime.{change.Operation}.{change.SubjectId}.{change.Revision}",
                subjectPersonId = personId,
                audienceId = PrototypeReputationDefinitionFactory.CityGuardAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                delta = string.Equals(change.Operation, "publish-wanted-notice", StringComparison.Ordinal) ? 12 : 8,
                sourceId = change.SubjectId,
                sourceCategory = ReputationContributionSourceCategory.Conviction,
                authenticity = ReputationAuthenticity.Verified,
                supportingReferenceId = change.SubjectId,
                worldTime = worldTime
            });
        }

        private void HandleOrganizationDecisionCommitted(OrganizationDecisionCommittedEvent change)
        {
            OrganizationDecisionTransactionRecordData transaction = change?.Transaction;
            if (transaction == null || !string.Equals(transaction.operation, "submit-proposal", StringComparison.Ordinal)
                || !string.Equals(transaction.organizationId, "organization.prototype.adventurers-guild", StringComparison.Ordinal))
            {
                return;
            }

            OrganizationProposalRecordData proposal = OrganizationDecisions.CreateSaveData().proposals
                .FirstOrDefault(item => string.Equals(item.proposalId, transaction.subjectId, StringComparison.Ordinal));
            if (proposal == null || string.IsNullOrWhiteSpace(proposal.proposerPersonId))
            {
                return;
            }

            Reputation.Mutate(new ReputationMutationRequest
            {
                transactionId = $"social.organization.{transaction.transactionId}",
                subjectPersonId = proposal.proposerPersonId,
                audienceId = PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.RenownId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                delta = 1,
                sourceId = proposal.proposalId,
                sourceCategory = ReputationContributionSourceCategory.Scripted,
                authenticity = ReputationAuthenticity.Verified,
                supportingReferenceId = transaction.organizationId,
                worldTime = transaction.worldTime
            });
        }

        private static string ResolveScenePersonId(GameObject actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            PersonIdentity person = actor.GetComponentInParent<PersonIdentity>();
            if (person != null && person.HasValidIdentity)
            {
                return person.PersonId;
            }

            WorldEntityIdentity entity = actor.GetComponentInParent<WorldEntityIdentity>();
            return entity != null && entity.EntityId.StartsWith("person.", StringComparison.Ordinal) ? entity.EntityId : string.Empty;
        }
    }
}
