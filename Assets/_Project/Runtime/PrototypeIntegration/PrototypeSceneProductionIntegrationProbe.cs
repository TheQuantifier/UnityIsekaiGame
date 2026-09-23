using System;
using System.Linq;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public sealed class PrototypeSceneProductionIntegrationProbe
    {
        private readonly string suffix;

        private PrototypeSceneProductionIntegrationProbe(string suffix, DefinitionRegistry registry)
        {
            this.suffix = string.IsNullOrWhiteSpace(suffix) ? "phase2" : suffix.Trim();
            Registry = registry;
            Quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            Participation = new QuestParticipationRuntime(Quests, registry, PersistenceService.LocalWorldId);
            Sources = new QuestSourceRuntime(Quests, Participation, registry, PersistenceService.LocalWorldId);
            Conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            Dialogue = new DialogueFlowRuntime(registry, Conversations);
            Organizations = new OrganizationRuntime();
            Memberships = new OrganizationMembershipRuntime();
            States = new NarrativeStateRuntime(registry);
            Arcs = new NarrativeArcRuntime(registry, new NarrativeArcRuntimeIntegrations { QuestRuntime = Quests, QuestSourceRuntime = Sources, NarrativeStateRuntime = States });

            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(Sources, registry, PersistenceService.LocalWorldId);
            SeedOrganizations();
        }

        public DefinitionRegistry Registry { get; }
        public QuestRuntime Quests { get; }
        public QuestParticipationRuntime Participation { get; }
        public QuestSourceRuntime Sources { get; }
        public ConversationRuntime Conversations { get; }
        public DialogueFlowRuntime Dialogue { get; }
        public OrganizationRuntime Organizations { get; }
        public OrganizationMembershipRuntime Memberships { get; }
        public NarrativeStateRuntime States { get; }
        public NarrativeArcRuntime Arcs { get; }

        public static DefinitionRegistry BuildRegistry(DefinitionRegistry baseRegistry = null)
        {
            DefinitionRegistry registry = baseRegistry ?? new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(registry);
            registry = PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(registry);
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(registry);
            registry = PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(registry);
            registry = PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(registry);
            registry = PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(registry);
            registry = PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(registry);
            registry = PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(registry);
            registry = PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(registry);
            return registry;
        }

        public static PrototypeSceneProductionIntegrationProbe Create(string suffix, DefinitionRegistry baseRegistry = null)
        {
            return new PrototypeSceneProductionIntegrationProbe(suffix, BuildRegistry(baseRegistry));
        }

        public PrototypeSceneProductionProbeResult RunGuildFlow()
        {
            QuestRuntimeOperationResult quest = CreateQuest("guild", PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, "organization.prototype.guild", "location.prototype.adventurers-guild", PrototypeInteractionPointDefinitionFactory.QuestBoardPointId, QuestSourceChannel.QuestBoard);
            if (!quest.Succeeded) return Fail($"GuildQuest={quest.Status} {quest.Message}");

            QuestSourceOperationResult listing = Publish(PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId, quest.Snapshot.QuestId, "guild-board");
            Sources.BrowseSource(new QuestSourceBrowseRequest { questSourceId = PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId, requesterPersonId = PrototypeEntityLocationFactory.PlayerPersonId, eligibilityContext = Eligibility(PrototypeEntityLocationFactory.PlayerPersonId, "location.prototype.adventurers-guild", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId), recordDiscovery = true, transactionId = $"tx.{suffix}.guild.browse", worldTime = 2d });
            QuestSourceOperationResult accept = listing.Listing == null
                ? QuestSourceOperationResult.Failure(QuestSourceOperationStatus.InvalidRequest, "Listing was not created.", Sources.Revision)
                : Sources.AcceptFromSource(new QuestSourceAcceptRequest { transactionId = $"tx.{suffix}.guild.accept", questListingId = listing.Listing.QuestListingId, personId = PrototypeEntityLocationFactory.PlayerPersonId, explicitConsent = true, eligibilityContext = Eligibility(PrototypeEntityLocationFactory.PlayerPersonId, "location.prototype.adventurers-guild", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId), worldTime = 3d });
            OrganizationMembershipOperationResult membership = Memberships.ApplyMembership(new OrganizationMembershipRequest
            {
                transactionId = $"tx.{suffix}.guild.membership",
                membershipId = $"organization-membership.{suffix}.guild.player",
                organizationId = "organization.prototype.guild",
                personId = PrototypeEntityLocationFactory.PlayerPersonId,
                membershipDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                sourceKind = OrganizationMembershipSourceKind.WorldSetup,
                targetStatus = OrganizationMembershipStatus.Active,
                explicitConsent = true,
                worldTime = 4d
            });
            ConversationOperationResult conversation = StartConversation(
                $"conversation.{suffix}.guild",
                PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                PrototypeSceneIntegrationIds.AdventurerGuildCounterSourceId,
                PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                "location.prototype.adventurers-guild",
                "organization.prototype.adventurers-guild",
                string.Empty,
                string.Empty,
                quest.Snapshot.QuestId);
            DialogueFlowOperationResult flow = Dialogue.StartFlow(new DialogueFlowStartRequest { transactionId = $"tx.{suffix}.guild.flow", flowId = $"dialogue-flow.{suffix}.guild", conversationId = conversation.Snapshot?.ConversationId, worldTime = 5d });
            DialogueFlowOperationResult choice = Dialogue.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = $"tx.{suffix}.guild.choice", flowId = flow.Snapshot?.FlowId, choiceId = "guild.choice.ask-work", actorPersonId = PrototypeEntityLocationFactory.PlayerPersonId, conditionContext = DialogueContext(PrototypeEntityLocationFactory.PlayerPersonId), worldTime = 6d });
            NarrativeArcOperationResult arc = Arcs.StartArc(StartArc(PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, PrototypeEntityLocationFactory.PlayerPersonId, $"tx.{suffix}.guild.arc"));
            NarrativeStateTransitionResult state = States.RequestTransition(StateTransition(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, PrototypeEntityLocationFactory.PlayerPersonId, $"tx.{suffix}.guild.state"));

            bool valid = listing.Succeeded && accept.Succeeded && membership.Succeeded && conversation.Succeeded && flow.Succeeded && choice.Succeeded && arc.Succeeded && state.Succeeded;
            return new PrototypeSceneProductionProbeResult(valid, $"Listing={listing.Status} Accept={accept.Status} Membership={membership.Status} Conversation={conversation.Status} Flow={flow.Status} Choice={choice.Status} Arc={arc.Status} State={state.Status}", guildQuestId: quest.Snapshot.QuestId, guildConversationId: conversation.Snapshot?.ConversationId);
        }

        public PrototypeSceneProductionProbeResult RunMerchantAndCivicFlow()
        {
            QuestRuntimeOperationResult merchantQuest = CreateQuest("merchant", PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, "organization.prototype.merchant-guild", "location.prototype.merchant-counter", PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, QuestSourceChannel.Contract, QuestRecipientScope.Person);
            if (!merchantQuest.Succeeded) return Fail($"MerchantQuest={merchantQuest.Status}");

            QuestSourceOperationResult merchantListing = Publish(PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId, merchantQuest.Snapshot.QuestId, "merchant-counter");
            QuestSourceOperationResult acceptMerchant = merchantListing.Listing == null
                ? QuestSourceOperationResult.Failure(QuestSourceOperationStatus.InvalidRequest, "Merchant listing was not created.", Sources.Revision)
                : Sources.AcceptFromSource(new QuestSourceAcceptRequest { transactionId = $"tx.{suffix}.merchant.accept", questListingId = merchantListing.Listing.QuestListingId, personId = PrototypeEntityLocationFactory.PlayerPersonId, explicitConsent = true, eligibilityContext = Eligibility(PrototypeEntityLocationFactory.PlayerPersonId, "location.prototype.merchant-counter", PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId), worldTime = 3d });
            ConversationOperationResult merchantConversation = StartConversation($"conversation.{suffix}.merchant", PrototypeConversationDefinitionFactory.MerchantGuildCounterDefinitionId, PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId, PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, "location.prototype.merchant-counter", "organization.prototype.merchant-guild", string.Empty, string.Empty, merchantQuest.Snapshot.QuestId);
            EndConversation(merchantConversation.Snapshot?.ConversationId, "merchant", 4.5d);
            NarrativeArcOperationResult arc = Arcs.StartArc(StartArc(PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, string.Empty, $"tx.{suffix}.mayor.arc", NarrativeArcScope.World));
            string civicQuestId = arc.Snapshot?.Stages.SelectMany(stage => stage.BoundQuests).FirstOrDefault(quest => string.Equals(quest.questDefinitionId, PrototypeQuestDefinitionFactory.CivicInvestigationDefinitionId, StringComparison.Ordinal))?.questId ?? string.Empty;
            QuestSourceOperationResult civicListing = Publish(PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId, civicQuestId, "mayor-desk");
            ConversationOperationResult mayorConversation = StartConversation($"conversation.{suffix}.mayor", PrototypeConversationDefinitionFactory.MayorDeskDefinitionId, PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId, PrototypeInteractionPointDefinitionFactory.MayorDeskPointId, "location.prototype.mayor-office", string.Empty, "office.prototype.mayor", "government.prototype.civic", civicQuestId);
            EndConversation(mayorConversation.Snapshot?.ConversationId, "mayor", 5d);
            ConversationOperationResult recordsConversation = StartConversation($"conversation.{suffix}.records", PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId, PrototypeSceneIntegrationIds.CityRecordsArchiveSourceId, PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId, "location.prototype.civic-office", string.Empty, string.Empty, "government.prototype.civic", civicQuestId, "authority.prototype.records.read");
            NarrativeStateTransitionResult state = States.RequestTransition(new NarrativeStateTransitionRequest
            {
                transactionId = $"tx.{suffix}.mayor.state",
                transitionDefinitionId = PrototypeNarrativeStateDefinitionFactory.OpenInvestigationTransitionId,
                scope = NarrativeStateScope.World,
                scopeKey = PersistenceService.LocalWorldId,
                sourceKind = NarrativeTransitionSourceKind.Development,
                sourceId = "phase2.production",
                worldTime = 6d
            });

            Quests.TryGetSnapshot(civicQuestId, out QuestSnapshot civicQuest);
            bool valid = merchantListing.Succeeded && civicListing.Succeeded && acceptMerchant.Succeeded && merchantConversation.Succeeded && mayorConversation.Succeeded && recordsConversation.Succeeded && arc.Succeeded && state.Succeeded;
            return new PrototypeSceneProductionProbeResult(valid, $"MerchantListing={merchantListing.Status} CivicListing={civicListing.Status} Accept={acceptMerchant.Status} MerchantEligibility={EligibilityDiagnostics(merchantQuest.Snapshot.QuestId, Eligibility(PrototypeEntityLocationFactory.PlayerPersonId, "location.prototype.merchant-counter", PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId))} MerchantConversation={merchantConversation.Status} MayorConversation={mayorConversation.Status} RecordsConversation={recordsConversation.Status} Arc={arc.Status} State={state.Status} CivicQuest={civicQuestId} CivicIssuer={civicQuest?.Issuer.issuerType}:{civicQuest?.Issuer.issuerId} CivicTags={string.Join(",", civicQuest?.TagIds ?? Array.Empty<string>())}", merchantQuestId: merchantQuest.Snapshot.QuestId);
        }

        public PrototypeSceneProductionIntegrationProbe Restore()
        {
            PrototypeSceneProductionIntegrationProbe restored = new PrototypeSceneProductionIntegrationProbe($"{suffix}.restored", Registry);
            restored.Quests.RestoreFromSaveData(Quests.CreateSaveData(), Registry);
            restored.Participation.RestoreFromSaveData(Participation.CreateSaveData(), restored.Quests, Registry);
            restored.Sources.RestoreFromSaveData(Sources.CreateSaveData(), restored.Quests, restored.Participation, Registry);
            restored.Conversations.RestoreFromSaveData(Conversations.CreateSaveData(), Registry);
            restored.Dialogue.RestoreFromSaveData(Dialogue.CreateSaveData(), Registry, restored.Conversations);
            restored.Memberships.RestoreFromSaveData(
                Memberships.CreateSaveData(),
                Registry,
                restored.Organizations,
                PersistenceService.LocalWorldId,
                new[] { PrototypeEntityLocationFactory.PlayerPersonId, PrototypeEntityLocationFactory.GuildMasterPersonId, PrototypeEntityLocationFactory.MerchantPersonId },
                restored.Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            restored.States.RestoreFromSaveData(States.CreateSaveData(), Registry);
            restored.Arcs.RestoreFromSaveData(Arcs.CreateSaveData(), Registry, new NarrativeArcRuntimeIntegrations { QuestRuntime = restored.Quests, QuestSourceRuntime = restored.Sources, NarrativeStateRuntime = restored.States });
            return restored;
        }

        private void SeedOrganizations()
        {
            PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(Organizations, Registry, PersistenceService.LocalWorldId);
            Organizations.Configure(Registry, PersistenceService.LocalWorldId);
            Memberships.Configure(Registry, Organizations, PersistenceService.LocalWorldId, new[] { PrototypeEntityLocationFactory.PlayerPersonId, PrototypeEntityLocationFactory.GuildMasterPersonId, PrototypeEntityLocationFactory.MerchantPersonId }, Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
        }

        private QuestRuntimeOperationResult CreateQuest(string key, string definitionId, string issuerId, string locationId, string interactionPointId, QuestSourceChannel channel, QuestRecipientScope recipientScope = QuestRecipientScope.Open)
        {
            return Quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"tx.{suffix}.{key}.quest",
                questId = $"quest.{suffix}.{key}",
                questDefinitionId = definitionId,
                issuer = new QuestIssuerReferenceData { issuerType = issuerId.StartsWith("government.", StringComparison.Ordinal) ? QuestIssuerType.Government : QuestIssuerType.Organization, issuerId = issuerId },
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = recipientScope, recipientId = recipientScope == QuestRecipientScope.Person ? PrototypeEntityLocationFactory.PlayerPersonId : string.Empty },
                origin = new QuestOriginReferenceData { sourceChannel = channel, locationId = locationId, interactionPointId = interactionPointId },
                createdWorldTime = 1d
            });
        }

        private QuestSourceOperationResult Publish(string sourceId, string questId, string key)
        {
            return Sources.PublishListing(new QuestListingPublishRequest
            {
                transactionId = $"tx.{suffix}.{key}.publish",
                questListingId = $"quest-listing.{suffix}.{key}",
                questSourceId = sourceId,
                questId = questId,
                intendedAudience = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                publisherPersonId = PrototypeEntityLocationFactory.GuildMasterPersonId,
                publisherAuthorityIds = PublisherAuthoritiesForSource(sourceId),
                worldTime = 2d
            });
        }

        private string[] PublisherAuthoritiesForSource(string sourceId)
        {
            if (!Sources.TryGetSource(sourceId, out QuestSourceSnapshot source) || !Registry.TryGet(source.QuestSourceDefinitionId, out QuestSourceDefinition definition))
            {
                return Array.Empty<string>();
            }

            return definition.PublicationAuthorityRequirementIds.ToArray();
        }

        private string EligibilityDiagnostics(string questId, QuestEligibilityContext context)
        {
            QuestEligibilityResult eligibility = Participation.EvaluateEligibility(questId, context);
            return $"{eligibility.Eligible}:{string.Join(",", eligibility.VisibleFailureReasons)}:{eligibility.HiddenFailureCount}";
        }

        private ConversationOperationResult StartConversation(string conversationId, string definitionId, string questSourceId, string interactionPointId, string locationId, string organizationId, string officeId, string governmentId, string questId, string authorityTag = "")
        {
            ConversationParticipantRecordData[] participants =
            {
                Participant("initiator", PrototypeEntityLocationFactory.PlayerPersonId, ConversationParticipantRole.Initiator, locationId, interactionPointId),
                Participant("provider", PrototypeEntityLocationFactory.GuildMasterPersonId, ConversationParticipantRole.Provider, locationId, interactionPointId, organizationId, officeId, governmentId, authorityTag),
                Participant("recipient", PrototypeEntityLocationFactory.PlayerPersonId, ConversationParticipantRole.QuestRecipient, locationId, interactionPointId),
                Participant("merchant", PrototypeEntityLocationFactory.MerchantPersonId, ConversationParticipantRole.Merchant, locationId, interactionPointId, organizationId),
                Participant("office", PrototypeEntityLocationFactory.GuildMasterPersonId, ConversationParticipantRole.OfficeHolder, locationId, interactionPointId, organizationId, officeId, governmentId),
                Participant("listener", PrototypeEntityLocationFactory.PlayerPersonId, ConversationParticipantRole.Listener, locationId, interactionPointId)
            };

            return Conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = $"tx.{suffix}.{conversationId}",
                conversationId = conversationId,
                conversationDefinitionId = definitionId,
                participants = participants,
                activeSpeakerPersonId = PrototypeEntityLocationFactory.GuildMasterPersonId,
                hostLocationId = locationId,
                hostInteractionPointId = interactionPointId,
                questSourceId = questSourceId,
                questId = questId,
                operatingOrganizationId = organizationId,
                operatingOfficeId = officeId,
                operatingGovernmentId = governmentId,
                sceneBindingKey = PrototypeSceneIntegrationContract.QuestSourceBindings.FirstOrDefault(binding => binding.QuestSourceId == questSourceId)?.BindingKey ?? string.Empty,
                tagIds = string.IsNullOrWhiteSpace(authorityTag) ? Array.Empty<string>() : new[] { authorityTag },
                worldTime = 4d
            });
        }

        private void EndConversation(string conversationId, string key, double worldTime)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return;
            }

            Conversations.TransitionLifecycle(new ConversationLifecycleRequest
            {
                transactionId = $"tx.{suffix}.{key}.conversation.complete",
                conversationId = conversationId,
                targetState = ConversationLifecycleState.Completed,
                worldTime = worldTime,
                provenanceId = "phase2.production"
            });
        }

        private static ConversationParticipantRecordData Participant(string id, string personId, ConversationParticipantRole role, string locationId, string interactionPointId, string organizationId = "", string officeId = "", string governmentId = "", string provenanceId = "")
        {
            return new ConversationParticipantRecordData
            {
                participantId = id,
                personId = personId,
                role = role,
                representedOrganizationId = organizationId,
                representedOfficeId = officeId,
                representedGovernmentId = governmentId,
                currentLocationId = locationId,
                currentInteractionPointId = interactionPointId,
                provenanceId = provenanceId
            };
        }

        private static QuestEligibilityContext Eligibility(string personId, string locationId, string interactionPointId)
        {
            return new QuestEligibilityContext
            {
                personId = personId,
                locationId = locationId,
                interactionPointId = interactionPointId,
                privilegedDiagnostics = true,
                worldTime = 2d,
                facts = new QuestEligibilityFactSet(
                    organizationMemberships: new[] { "organization.prototype.adventurers-guild", "organization.prototype.guild" },
                    offices: new[] { "office.prototype.city-investigator", "office.prototype.mayor" },
                    authorityGrants: new[] { "authority.prototype.guild.quest-offer", "authority.prototype.city.quest-assign", "authority.prototype.merchant.quest-offer" },
                    citizenships: new[] { "government.prototype.city" })
            };
        }

        private static DialogueConditionContext DialogueContext(string personId)
        {
            return new DialogueConditionContext
            {
                actorPersonId = personId,
                locationId = "location.prototype.adventurers-guild",
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                privilegedDiagnostics = true,
                worldTime = 5d
            };
        }

        private static NarrativeArcStartRequest StartArc(string arcDefinitionId, string actor, string tx, NarrativeArcScope scope = NarrativeArcScope.Person)
        {
            return new NarrativeArcStartRequest
            {
                transactionId = tx,
                arcDefinitionId = arcDefinitionId,
                actorPersonId = actor,
                scopeKey = scope == NarrativeArcScope.World ? PersistenceService.LocalWorldId : actor,
                subjectId = actor,
                worldTime = 5d
            };
        }

        private static NarrativeStateTransitionRequest StateTransition(string transitionId, string actor, string tx)
        {
            return new NarrativeStateTransitionRequest
            {
                transactionId = tx,
                transitionDefinitionId = transitionId,
                scope = NarrativeStateScope.Person,
                scopeKey = actor,
                sourceKind = NarrativeTransitionSourceKind.Development,
                sourceId = "phase2.production",
                actorPersonId = actor,
                worldTime = 5d
            };
        }

        private static PrototypeSceneProductionProbeResult Fail(string diagnostics)
        {
            return new PrototypeSceneProductionProbeResult(false, diagnostics ?? string.Empty);
        }
    }

    public sealed class PrototypeSceneProductionProbeResult
    {
        public PrototypeSceneProductionProbeResult(bool succeeded, string diagnostics, string guildQuestId = "", string guildConversationId = "", string merchantQuestId = "")
        {
            Succeeded = succeeded;
            Diagnostics = diagnostics ?? string.Empty;
            GuildQuestId = guildQuestId ?? string.Empty;
            GuildConversationId = guildConversationId ?? string.Empty;
            MerchantQuestId = merchantQuestId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Diagnostics { get; }
        public string GuildQuestId { get; }
        public string GuildConversationId { get; }
        public string MerchantQuestId { get; }
    }
}
