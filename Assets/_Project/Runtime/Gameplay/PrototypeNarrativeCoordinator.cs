using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Gameplay
{
    /// <summary>
    /// The live, authoritative boundary between prototype presentation and the Step 15 owner runtimes.
    /// Scene objects and UI submit requests here; they never own quest or narrative state.
    /// </summary>
    public sealed class PrototypeNarrativeCoordinator : IDialogueEffectExecutor, IDisposable
    {
        private const double SchedulerIntervalSeconds = 1d;

        private readonly PrototypePersistenceServiceBehaviour services;
        private readonly DefinitionRegistry registry;
        private readonly HashSet<string> processedObjectiveTransactions = new HashSet<string>(StringComparer.Ordinal);
        private double nextScheduledEvaluation;
        private long transactionSequence;
        private bool disposed;

        public PrototypeNarrativeCoordinator(PrototypePersistenceServiceBehaviour owner, DefinitionRegistry definitions)
        {
            services = owner ?? throw new ArgumentNullException(nameof(owner));
            registry = definitions ?? throw new ArgumentNullException(nameof(definitions));
            QuestObjectiveSignalBus.SignalReported += HandleObjectiveSignal;
            services.WorldDialogue.EventCommitted += HandleDialogueEvent;
            if (services.PlayerInventory != null) services.PlayerInventory.ItemAdded += HandleItemAdded;
            services.AdventuringParties.Changed += HandlePartyChanged;
        }

        public event Action Changed;
        public event Action<DialogueFlowSnapshot> DialogueChanged;

        public QuestRuntime Quests => services.WorldQuests;
        public QuestParticipationRuntime Participation => services.WorldQuestParticipation;
        public QuestObjectiveProgressRuntime Objectives => services.WorldQuestObjectives;
        public QuestOutcomeRuntime Outcomes => services.WorldQuestOutcomes;
        public QuestSourceRuntime Sources => services.WorldQuestSources;
        public ConversationRuntime Conversations => services.WorldConversations;
        public DialogueFlowRuntime Dialogue => services.WorldDialogue;
        public NarrativeEventRuntime NarrativeEvents => services.WorldNarrativeEvents;
        public NarrativeStateRuntime NarrativeState => services.WorldNarrativeState;
        public NarrativeArcRuntime NarrativeArcs => services.WorldNarrativeArcs;
        public AdventuringPartyService Parties => services.AdventuringParties;
        public string PlayerPersonId => services.PlayerPersonId;
        public double WorldTime => services.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble;

        public void InitializePrototypeContent()
        {
            ThrowIfDisposed();
            PrototypeQuestSourceSceneFactory.SeedPrototypeSceneQuestSources(Sources, registry, services.WorldService?.WorldId);
            EnsurePrototypeQuest(
                "quest.prototype.guild-posting",
                PrototypeQuestDefinitionFactory.GuildPostingDefinitionId,
                PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId,
                QuestIssuerType.Organization,
                "organization.prototype.adventurers-guild",
                QuestSourceChannel.QuestBoard,
                "location.prototype.adventurers-guild",
                PrototypeInteractionPointDefinitionFactory.QuestBoardPointId);
            EnsurePrototypeQuest(
                "quest.prototype.merchant-delivery",
                PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId,
                PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId,
                QuestIssuerType.Organization,
                "organization.prototype.merchant-guild",
                QuestSourceChannel.Contract,
                "location.prototype.merchant-counter",
                PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId);
            EnsurePrototypeQuest(
                "quest.prototype.civic-investigation",
                PrototypeQuestDefinitionFactory.CivicInvestigationDefinitionId,
                PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId,
                QuestIssuerType.Government,
                "government.prototype.civic",
                QuestSourceChannel.Government,
                "location.prototype.mayor-office",
                PrototypeInteractionPointDefinitionFactory.MayorDeskPointId);
            StartPrototypeArc(PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, NarrativeArcScope.Person, PlayerPersonId);
            StartPrototypeArc(PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId, NarrativeArcScope.Person, PlayerPersonId);
            StartPrototypeArc(PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, NarrativeArcScope.World, services.WorldService?.WorldId);
            Changed?.Invoke();
        }

        public void Advance()
        {
            if (disposed || WorldTime < nextScheduledEvaluation) return;
            nextScheduledEvaluation = WorldTime + SchedulerIntervalSeconds;

            long before = Participation.Revision + Sources.Revision + Outcomes.Revision;
            Participation.ExpireOffers(WorldTime, $"tx.quest.scheduler.offer.{SchedulerBucket()}");
            Sources.EvaluateExpirations(WorldTime, $"tx.quest.scheduler.listing.{SchedulerBucket()}");
            Outcomes.EvaluateDeadlines(WorldTime, $"tx.quest.scheduler.deadline.{SchedulerBucket()}");
            long after = Participation.Revision + Sources.Revision + Outcomes.Revision;
            if (after != before)
            {
                services.DirtyTracker?.MarkDirty("Quest and narrative world-time state advanced.");
                Changed?.Invoke();
            }
        }

        public QuestSourceBrowseResult BrowseSource(string questSourceId, string interactionPointId)
        {
            ThrowIfDisposed();
            QuestEligibilityContext eligibility = BuildEligibility(interactionPointId);
            QuestSourceBrowseResult result = Sources.BrowseSource(new QuestSourceBrowseRequest
            {
                questSourceId = questSourceId,
                requesterPersonId = PlayerPersonId,
                access = QuestVisibilityAccess.Recipient,
                eligibilityContext = eligibility,
                worldTime = WorldTime,
                includeIneligible = true,
                recordDiscovery = true,
                transactionId = NextTransaction("quest-source.browse")
            });
            if (result.Succeeded) Changed?.Invoke();
            return result;
        }

        public QuestSourceOperationResult AcceptListing(string questListingId, string interactionPointId)
        {
            ThrowIfDisposed();
            QuestSourceOperationResult accepted = Sources.AcceptFromSource(new QuestSourceAcceptRequest
            {
                transactionId = NextTransaction("quest-source.accept"),
                questListingId = questListingId,
                personId = PlayerPersonId,
                explicitConsent = true,
                consentRecordId = $"consent.quest.{Sanitize(questListingId)}.{Sanitize(PlayerPersonId)}",
                eligibilityContext = BuildEligibility(interactionPointId),
                worldTime = WorldTime
            });
            if (!accepted.Succeeded || accepted.Assignment == null) return accepted;

            InitializeAssignment(accepted.Assignment);
            ReportObjective(QuestObjectiveCategory.UseInteractionPoint, interactionPointId, 1, "quest-source.accept");
            if (Quests.TryGetSnapshot(accepted.Assignment.QuestId, out QuestSnapshot acceptedQuest)
                && acceptedQuest.QuestDefinitionId == PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId
                && registry.TryGet("item.prototype.merchant-parcel", out ItemDefinition parcel)
                && services.PlayerInventory != null
                && services.PlayerInventory.CountItem(parcel) == 0)
            {
                InventoryAddResult parcelGrant = services.PlayerInventory.AddItemOrInstances(parcel, 1);
                if (!parcelGrant.AddedAll)
                {
                    PrototypeHudMessageBus.Show("Make inventory space before accepting the merchant parcel.");
                }
            }
            services.DirtyTracker?.MarkDirty("Quest accepted from an authoritative source.");
            Changed?.Invoke();
            return accepted;
        }

        public QuestParticipationOperationResult AbandonAssignment(string assignmentId)
        {
            ThrowIfDisposed();
            QuestParticipationOperationResult result = Participation.AbandonAssignment(new QuestAssignmentLifecycleRequest
            {
                transactionId = NextTransaction("quest.assignment.abandon"),
                assignmentId = assignmentId,
                actingPersonId = PlayerPersonId,
                explicitConsent = true,
                worldTime = WorldTime
            });
            if (result.Succeeded)
            {
                Objectives.AbandonAssignment(assignmentId, WorldTime, NextTransaction("quest.objectives.abandon"));
                services.DirtyTracker?.MarkDirty("Quest assignment abandoned.");
                Changed?.Invoke();
            }
            return result;
        }

        public QuestOutcomeOperationResult CompleteAssignment(string assignmentId, string interactionPointId = "")
        {
            ThrowIfDisposed();
            QuestOutcomeOperationResult result = Outcomes.Complete(new QuestCompletionRequest
            {
                transactionId = NextTransaction("quest.assignment.complete"),
                assignmentId = assignmentId,
                requesterPersonId = PlayerPersonId,
                interactionPointId = interactionPointId,
                worldTime = WorldTime,
                sourceEventId = $"quest-completion.{Sanitize(assignmentId)}",
                provenanceId = "prototype.narrative.coordinator"
            });
            if (result.Succeeded)
            {
                EmitArcSignal(NarrativeArcSignalCategory.QuestOutcome, result.Outcome?.QuestDefinitionId, result.Outcome?.OutcomeKind.ToString(), result.Outcome?.TerminalOutcomeId);
                services.DirtyTracker?.MarkDirty("Quest completion committed.");
                Changed?.Invoke();
            }
            return result;
        }

        public QuestOutcomeOperationResult ClaimReward(string entitlementId)
        {
            ThrowIfDisposed();
            QuestOutcomeOperationResult result = Outcomes.ClaimReward(new QuestRewardClaimRequest
            {
                transactionId = NextTransaction("quest.reward.claim"),
                entitlementId = entitlementId,
                claimantPersonId = PlayerPersonId,
                worldTime = WorldTime
            });
            if (result.Succeeded)
            {
                services.DirtyTracker?.MarkDirty("Quest reward claimed.");
                Changed?.Invoke();
            }
            return result;
        }

        public IReadOnlyList<PrototypeQuestJournalEntry> GetJournal()
        {
            ThrowIfDisposed();
            IReadOnlyList<QuestAssignmentSnapshot> assignments = Participation.QueryAssignments(new QuestAssignmentQuery
            {
                requesterPersonId = PlayerPersonId,
                assigneePersonId = PlayerPersonId,
                access = QuestVisibilityAccess.Recipient,
                includeHistorical = true
            });

            return assignments.Select(assignment =>
            {
                Quests.TryGetSnapshot(assignment.QuestId, out QuestSnapshot quest);
                registry.TryGet(quest?.QuestDefinitionId, out QuestDefinition definition);
                QuestObjectiveSnapshot[] objectives = Objectives.QueryObjectives(new QuestObjectiveQuery
                {
                    requesterPersonId = PlayerPersonId,
                    assignmentId = assignment.AssignmentId,
                    access = QuestVisibilityAccess.Recipient,
                    includeTerminal = true
                }).ToArray();
                QuestTerminalOutcomeSnapshot outcome = Outcomes.QueryOutcomes(new QuestOutcomeQuery
                {
                    requesterPersonId = PlayerPersonId,
                    assignmentId = assignment.AssignmentId,
                    access = QuestVisibilityAccess.Recipient,
                    includeHidden = false
                }).FirstOrDefault();
                QuestRewardEntitlementSnapshot[] rewards = Outcomes.QueryRewards(new QuestRewardQuery
                {
                    requesterPersonId = PlayerPersonId,
                    assignmentId = assignment.AssignmentId,
                    recipientPersonId = PlayerPersonId,
                    access = QuestVisibilityAccess.Recipient,
                    includeTerminal = true
                }).ToArray();
                return new PrototypeQuestJournalEntry(assignment, quest, definition, objectives, outcome, rewards);
            }).OrderBy(entry => entry.IsTerminal).ThenBy(entry => entry.Title, StringComparer.Ordinal).ToArray();
        }

        public DialogueFlowOperationResult StartConversation(
            string conversationDefinitionId,
            string providerPersonId,
            string interactionPointId,
            string locationId,
            string questSourceId = "")
        {
            ThrowIfDisposed();
            string suffix = $"{Sanitize(conversationDefinitionId)}.{++transactionSequence:000000}";
            ConversationOperationResult conversation = Conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = $"tx.conversation.start.{suffix}",
                conversationId = $"conversation.runtime.{suffix}",
                conversationDefinitionId = conversationDefinitionId,
                participants = new[]
                {
                    Participant("initiator", PlayerPersonId, ConversationParticipantRole.Initiator, locationId, interactionPointId),
                    Participant("provider", providerPersonId, ConversationParticipantRole.Provider, locationId, interactionPointId)
                },
                activeSpeakerPersonId = providerPersonId,
                hostLocationId = locationId,
                hostInteractionPointId = interactionPointId,
                questSourceId = questSourceId,
                sceneBindingKey = $"prototype.dialogue.{Sanitize(providerPersonId)}",
                worldTime = WorldTime,
                provenanceId = "prototype.scene.dialogue"
            });
            if (!conversation.Succeeded)
            {
                return DialogueFlowOperationResult.Failure(DialogueFlowOperationStatus.InvalidRequest, conversation.Message, Dialogue.Revision);
            }

            DialogueFlowOperationResult flow = Dialogue.StartFlow(new DialogueFlowStartRequest
            {
                transactionId = $"tx.dialogue.start.{suffix}",
                flowId = $"dialogue-flow.runtime.{suffix}",
                conversationId = conversation.Snapshot.ConversationId,
                conditionContext = BuildDialogueContext(providerPersonId, interactionPointId, locationId),
                worldTime = WorldTime
            });
            if (flow.Succeeded)
            {
                QuestObjectiveSignalBus.ReportTalk(providerPersonId, PlayerPersonId, WorldTime);
                DialogueChanged?.Invoke(flow.Snapshot);
                Changed?.Invoke();
            }
            return flow;
        }

        public DialogueFlowOperationResult SelectDialogueChoice(string flowId, string choiceId)
        {
            ThrowIfDisposed();
            DialogueFlowOperationResult result = Dialogue.SelectChoice(new DialogueChoiceSelectionRequest
            {
                transactionId = NextTransaction("dialogue.choice"),
                flowId = flowId,
                choiceId = choiceId,
                actorPersonId = PlayerPersonId,
                conditionContext = BuildDialogueContext(string.Empty, string.Empty, string.Empty),
                worldTime = WorldTime
            });
            if (result.Succeeded)
            {
                DialogueChanged?.Invoke(result.Snapshot);
                Changed?.Invoke();
            }
            return result;
        }

        public DialogueFlowOperationResult EndDialogue(string flowId)
        {
            ThrowIfDisposed();
            DialogueFlowOperationResult flow = Dialogue.TransitionLifecycle(new DialogueFlowLifecycleRequest
            {
                transactionId = NextTransaction("dialogue.end"),
                flowId = flowId,
                targetState = DialogueFlowState.Ended,
                worldTime = WorldTime
            });
            DialogueFlowSnapshot snapshot = flow.Snapshot;
            if (flow.Succeeded && snapshot != null)
            {
                Conversations.TransitionLifecycle(new ConversationLifecycleRequest
                {
                    transactionId = NextTransaction("conversation.end"),
                    conversationId = snapshot.ConversationId,
                    targetState = ConversationLifecycleState.Completed,
                    worldTime = WorldTime,
                    provenanceId = "prototype.scene.dialogue"
                });
                Changed?.Invoke();
            }
            return flow;
        }

        public QuestObjectiveOperationResult ReportObjective(QuestObjectiveCategory category, string targetId, int amount = 1, string sourceRuntimeId = "gameplay")
        {
            return ApplyObjectiveSignal(new QuestObjectiveSignal
            {
                transactionId = NextTransaction("quest.objective.signal"),
                sourceEventId = $"event.{Sanitize(sourceRuntimeId)}.{++transactionSequence:000000}",
                sourceRuntimeId = sourceRuntimeId,
                actorPersonId = PlayerPersonId,
                participantPersonId = PlayerPersonId,
                category = category,
                target = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = targetId },
                amount = Math.Max(1, amount),
                worldTime = WorldTime,
                committed = true
            });
        }

        public void HandleInteractionPointUsed(string interactionPointId, string sourceEventId = "")
        {
            string pointId = string.IsNullOrWhiteSpace(interactionPointId) ? string.Empty : interactionPointId.Trim();
            ReportObjective(QuestObjectiveCategory.UseInteractionPoint, pointId, 1, "world.interaction-point");
            QuestObjectiveSnapshot delivery = Objectives.QueryObjectives(new QuestObjectiveQuery
            {
                requesterPersonId = PlayerPersonId,
                assigneePersonId = PlayerPersonId,
                category = QuestObjectiveCategory.DeliverItem,
                lifecycleState = QuestObjectiveLifecycleState.Active,
                includeTerminal = false,
                access = QuestVisibilityAccess.Recipient
            }).FirstOrDefault(objective => TryGetObjectiveDefinition(objective, out QuestObjectiveDefinitionData definition)
                && string.Equals(definition.target?.subjectId, pointId, StringComparison.Ordinal));
            if (delivery == null) return;

            if (!TryGetObjectiveDefinition(delivery, out QuestObjectiveDefinitionData deliveryDefinition)) return;
            string itemId = deliveryDefinition.secondaryTarget?.subjectId ?? string.Empty;
            if (!registry.TryGet(itemId, out ItemDefinition item) || services.PlayerInventory == null || services.PlayerInventory.CountItem(item) < 1)
            {
                PrototypeHudMessageBus.Show("The required delivery item is not in your inventory.");
                return;
            }

            QuestObjectiveOperationResult delivered = ApplyObjectiveSignal(new QuestObjectiveSignal
            {
                transactionId = NextTransaction("quest.objective.delivery"),
                sourceEventId = string.IsNullOrWhiteSpace(sourceEventId) ? NextTransaction("event.delivery") : sourceEventId,
                sourceRuntimeId = "world.interaction-point",
                assignmentId = delivery.AssignmentId,
                actorPersonId = PlayerPersonId,
                participantPersonId = PlayerPersonId,
                category = QuestObjectiveCategory.DeliverItem,
                target = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = pointId },
                secondaryTarget = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = itemId },
                amount = 1,
                worldTime = WorldTime,
                committed = true
            });
            if (delivered.Succeeded && delivered.Objectives.Any(objective => objective.Satisfied))
            {
                services.PlayerInventory.RemoveItem(item, 1);
                PrototypeHudMessageBus.Show($"Delivered {item.DisplayName}.");
            }
        }

        public DialogueEffectExecutionResult Execute(DialogueEffectExecutionRequest request)
        {
            if (request?.effect == null) return DialogueEffectExecutionResult.Failure("Dialogue effect is missing.");
            DialogueEffectData effect = request.effect;
            switch (effect.kind)
            {
                case DialogueEffectKind.CreateQuestOffer:
                    return CreateDialogueQuestOffer(request);
                case DialogueEffectKind.AcceptQuestOffer:
                    return AcceptDialogueQuestOffer(request);
                case DialogueEffectKind.RefuseQuestOffer:
                    return TransitionDialogueOffer(request, refuse: true);
                case DialogueEffectKind.CompleteQuest:
                    return CompleteDialogueQuest(request);
                case DialogueEffectKind.ClaimQuestReward:
                    return ClaimDialogueReward(request);
                case DialogueEffectKind.RequestNarrativeStateTransition:
                    return ApplyDialogueStateTransition(request);
                case DialogueEffectKind.SocialInteraction:
                    return ExecuteSocialDialogueEffect(request);
                case DialogueEffectKind.RevealInformation:
                case DialogueEffectKind.TransferInformation:
                    return GrantInformation(effect.targetId, request.actorPersonId, request.worldTime)
                        ? DialogueEffectExecutionResult.Success("player.knowledge", effect.targetId)
                        : DialogueEffectExecutionResult.Failure("Information could not be granted.");
                default:
                    return effect.requirement == DialogueEffectRequirement.Required
                        ? DialogueEffectExecutionResult.Failure($"Dialogue effect '{effect.kind}' is not configured for this prototype.")
                        : DialogueEffectExecutionResult.Success("dialogue.optional", effect.effectId);
            }
        }

        public bool EvaluateNarrativeStateCondition(DialogueConditionData condition, DialogueConditionContext context)
        {
            if (condition == null || NarrativeState == null) return false;
            return NarrativeState.EvaluateCondition(new NarrativeStateConditionQuery
            {
                stateDefinitionId = condition.requiredId,
                scope = NarrativeStateScope.Person,
                scopeKey = context?.actorPersonId ?? PlayerPersonId,
                variableDefinitionId = condition.secondaryId,
                expectedValue = null,
                minimumValue = condition.minimumValue,
                maximumValue = condition.maximumValue,
                negate = condition.negate
            });
        }

        public bool EvaluateNarrativeStateCondition(NarrativeConditionDefinitionData condition, NarrativeConditionContextData context)
        {
            if (condition == null || NarrativeState == null) return false;
            string scopeKey = context?.actorPersonId ?? PlayerPersonId;
            return NarrativeState.EvaluateCondition(new NarrativeStateConditionQuery
            {
                stateDefinitionId = condition.requiredId,
                variableDefinitionId = condition.secondaryId,
                scope = NarrativeStateScope.Person,
                scopeKey = scopeKey,
                expectedValue = null,
                minimumValue = condition.minimumValue,
                maximumValue = int.MaxValue,
                negate = condition.negate
            });
        }

        public bool GrantInformation(string subjectId) => GrantInformation(subjectId, PlayerPersonId, WorldTime);

        // These action payloads currently contain only a target ID. That is not enough
        // information to safely choose a membership/rank transition, legal operation,
        // travel-condition transition, or connection state. Reject them until the
        // authored action schema carries the requested operation instead of reporting a
        // false success and allowing a required narrative transaction to commit.
        public bool ExecuteOrganizationAction(string actionId) => false;
        public bool ExecuteLegalAction(string actionId) => false;
        public bool ExecuteTravelCondition(string conditionId) => false;
        public bool ExecuteConnectionChange(string connectionId) => false;

        public bool ExecuteNarrativeArcAction(NarrativeActionDefinitionData action, NarrativeArcActionContext context)
        {
            if (action == null) return false;
            if (action.category == NarrativeActionCategory.EmitNarrativeSignal)
            {
                NarrativeEventOperationResult result = NarrativeEvents.EmitSignal(new NarrativeSignalRequest
                {
                    transactionId = NextTransaction("narrative.arc.signal"),
                    signalDefinitionId = action.targetId,
                    sourceKind = NarrativeSignalSourceKind.NarrativeArcProgression,
                    sourceId = context?.ArcRecord?.narrativeArcId,
                    actorPersonId = PlayerPersonId,
                    worldTime = WorldTime
                });
                return result.Succeeded;
            }

            if (action.category == NarrativeActionCategory.GrantInformation)
            {
                return GrantInformation(action.targetId, PlayerPersonId, context?.WorldTime ?? WorldTime);
            }

            return action.requirement != NarrativeActionRequirement.Required;
        }

        public string ExecuteNarrativeStateConsequence(NarrativeActionDefinitionData action, NarrativeStateTransitionRequest request)
        {
            bool succeeded = ExecuteNarrativeArcAction(action, new NarrativeArcActionContext());
            return succeeded ? action?.targetId ?? string.Empty : string.Empty;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            QuestObjectiveSignalBus.SignalReported -= HandleObjectiveSignal;
            if (services.WorldDialogue != null) services.WorldDialogue.EventCommitted -= HandleDialogueEvent;
            if (services.PlayerInventory != null) services.PlayerInventory.ItemAdded -= HandleItemAdded;
            services.AdventuringParties.Changed -= HandlePartyChanged;
            Changed = null;
            DialogueChanged = null;
        }

        private void HandleItemAdded(ItemDefinition item, int quantity)
        {
            if (disposed || item == null || quantity <= 0) return;
            ReportObjective(QuestObjectiveCategory.ObtainItem, item.Id, quantity, "player.inventory.add");
            int possessed = services.PlayerInventory?.CountItem(item) ?? quantity;
            ReportObjective(QuestObjectiveCategory.PossessItem, item.Id, Math.Max(1, possessed), "player.inventory.state");
        }

        private void HandlePartyChanged()
        {
            if (disposed) return;
            services.DirtyTracker?.MarkDirty("Adventuring party membership changed.");
            Changed?.Invoke();
        }

        private bool TryGetObjectiveDefinition(QuestObjectiveSnapshot objective, out QuestObjectiveDefinitionData definition)
        {
            definition = null;
            if (objective == null || !registry.TryGet(objective.QuestDefinitionId, out QuestDefinition questDefinition)) return false;
            definition = questDefinition.ObjectiveDefinitions.FirstOrDefault(candidate => string.Equals(candidate.objectiveDefinitionId, objective.ObjectiveDefinitionId, StringComparison.Ordinal));
            return definition != null;
        }

        private void EnsurePrototypeQuest(string questId, string definitionId, string sourceId, QuestIssuerType issuerType, string issuerId, QuestSourceChannel channel, string locationId, string interactionPointId)
        {
            if (!Quests.TryGetSnapshot(questId, out QuestSnapshot quest))
            {
                QuestRuntimeOperationResult created = Quests.CreateQuest(new QuestCreateRequest
                {
                    transactionId = $"tx.prototype.seed.{questId}",
                    questId = questId,
                    questDefinitionId = definitionId,
                    issuer = new QuestIssuerReferenceData { issuerType = issuerType, issuerId = issuerId },
                    intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                    origin = new QuestOriginReferenceData { sourceChannel = channel, locationId = locationId, interactionPointId = interactionPointId },
                    createdWorldTime = 0d,
                    provenanceId = "prototype.narrative.seed"
                });
                quest = created.Snapshot;
            }
            if (quest == null) return;

            string listingId = $"quest-listing.prototype.{Sanitize(sourceId)}.{Sanitize(questId)}";
            if (Sources.TryGetListing(listingId, out _)) return;
            string[] authorities = Array.Empty<string>();
            if (Sources.TryGetSource(sourceId, out QuestSourceSnapshot source)
                && registry.TryGet(source.QuestSourceDefinitionId, out QuestSourceDefinition sourceDefinition))
            {
                authorities = sourceDefinition.PublicationAuthorityRequirementIds.ToArray();
            }
            Sources.PublishListing(new QuestListingPublishRequest
            {
                transactionId = $"tx.prototype.seed.{listingId}",
                questListingId = listingId,
                questSourceId = sourceId,
                questId = quest.QuestId,
                intendedAudience = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Open },
                publisherPersonId = PrototypeEntityLocationFactory.GuildMasterPersonId,
                publisherAuthorityIds = authorities,
                worldTime = 0d
            });
        }

        private void InitializeAssignment(QuestAssignmentSnapshot assignment)
        {
            Objectives.InstantiateForAssignment(assignment, transactionId: $"tx.quest.objectives.initialize.{assignment.AssignmentId}");
            Outcomes.TrackAssignment(assignment, $"tx.quest.outcome.track.{assignment.AssignmentId}");
        }

        public QuestParticipationOperationResult CreateDevelopmentAssignment(QuestDefinition definition, string provenanceId = "development")
        {
            if (definition == null)
                return QuestParticipationOperationResult.Failure(QuestParticipationOperationStatus.InvalidRequest, "Quest definition is missing.", Participation.Revision);

            string suffix = $"{Sanitize(definition.Id)}.{++transactionSequence:000000}";
            string questId = $"quest.development.{suffix}";
            if (definition.UniquePerWorld || definition.UniquePerRecipient)
            {
                foreach (QuestSnapshot existing in Quests.Query(new QuestQuery
                {
                    access = QuestVisibilityAccess.PrivilegedDiagnostic,
                    definitionId = definition.Id,
                    recipientId = definition.UniquePerRecipient ? PlayerPersonId : string.Empty,
                    worldId = definition.UniquePerWorld ? services.WorldService?.WorldId : string.Empty
                }))
                {
                    Quests.TransitionLifecycle(new QuestLifecycleTransitionRequest
                    {
                        transactionId = $"tx.quest.development.retire.{Sanitize(existing.QuestId)}.{suffix}",
                        questId = existing.QuestId,
                        targetState = QuestRuntimeLifecycleState.Historical,
                        worldTime = WorldTime,
                        provenanceId = provenanceId ?? "development"
                    });
                }
            }

            QuestIssuerType issuerType = definition.SupportedIssuerTypes
                .FirstOrDefault(type => type != QuestIssuerType.Unknown);
            if (issuerType == QuestIssuerType.Unknown)
            {
                issuerType = QuestIssuerType.System;
            }

            QuestIssuerReferenceData issuer = new QuestIssuerReferenceData
            {
                issuerType = issuerType,
                issuerId = DevelopmentIssuerId(issuerType)
            };
            QuestSourceChannel sourceChannel = definition.DefaultSourceChannel == QuestSourceChannel.Unknown
                ? QuestSourceChannel.System
                : definition.DefaultSourceChannel;
            QuestOriginReferenceData origin = DevelopmentQuestOrigin(sourceChannel, provenanceId);
            QuestRuntimeOperationResult created = Quests.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"tx.quest.development.create.{suffix}",
                questId = questId,
                questDefinitionId = definition.Id,
                issuer = issuer,
                intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = PlayerPersonId },
                origin = origin,
                createdWorldTime = WorldTime,
                provenanceId = provenanceId ?? "development"
            });
            if (!created.Succeeded)
                return QuestParticipationOperationResult.Failure(QuestParticipationOperationStatus.MissingQuest, created.Message, Participation.Revision);

            QuestEligibilityContext eligibility = BuildEligibility(origin.interactionPointId);
            eligibility.privilegedDiagnostics = true;
            eligibility.locationId = origin.locationId;
            eligibility.interactionPointId = origin.interactionPointId;
            eligibility.facts = DevelopmentEligibilityFacts(definition, PlayerPersonId);
            QuestParticipationOperationResult assigned = Participation.DirectAssign(new QuestDirectAssignmentRequest
            {
                transactionId = $"tx.quest.development.assign.{suffix}",
                assignmentId = $"quest-assignment.development.{suffix}",
                questId = questId,
                assigneePersonId = PlayerPersonId,
                institutionalIssuer = issuer,
                assignedBy = issuer,
                explicitConsent = true,
                consentRecordId = $"consent.development.{suffix}",
                eligibilityContext = eligibility,
                worldTime = WorldTime
            });
            if (assigned.Succeeded && assigned.Assignment != null)
            {
                InitializeAssignment(assigned.Assignment);
                services.DirtyTracker?.MarkDirty("Development quest assignment created.");
                Changed?.Invoke();
            }
            return assigned;
        }

        private static string DevelopmentIssuerId(QuestIssuerType issuerType)
        {
            return issuerType switch
            {
                QuestIssuerType.Person => "person.prototype.quest-giver",
                QuestIssuerType.Organization => "organization.prototype.adventurers-guild",
                QuestIssuerType.Office => "office.prototype.mayor",
                QuestIssuerType.Government => "government.prototype.civic",
                QuestIssuerType.Faction => "faction.prototype.settlement",
                QuestIssuerType.Business => "business.prototype.merchant",
                QuestIssuerType.Anonymous => string.Empty,
                QuestIssuerType.Custom => "custom.development",
                _ => "system.development"
            };
        }

        private static QuestOriginReferenceData DevelopmentQuestOrigin(QuestSourceChannel channel, string provenanceId)
        {
            return channel switch
            {
                QuestSourceChannel.QuestBoard => new QuestOriginReferenceData
                {
                    sourceChannel = channel,
                    locationId = "location.prototype.adventurers-guild",
                    interactionPointId = PrototypeInteractionPointDefinitionFactory.QuestBoardPointId,
                    provenanceId = provenanceId ?? "development"
                },
                QuestSourceChannel.Contract => new QuestOriginReferenceData
                {
                    sourceChannel = channel,
                    locationId = "location.prototype.merchant-counter",
                    interactionPointId = PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId,
                    provenanceId = provenanceId ?? "development"
                },
                QuestSourceChannel.Government => new QuestOriginReferenceData
                {
                    sourceChannel = channel,
                    locationId = "location.prototype.mayor-office",
                    interactionPointId = PrototypeInteractionPointDefinitionFactory.MayorDeskPointId,
                    provenanceId = provenanceId ?? "development"
                },
                _ => new QuestOriginReferenceData
                {
                    sourceChannel = channel,
                    locationId = channel == QuestSourceChannel.System ? string.Empty : "location.prototype.development",
                    provenanceId = provenanceId ?? "development"
                }
            };
        }

        private static QuestEligibilityFactSet DevelopmentEligibilityFacts(QuestDefinition definition, string personId)
        {
            QuestEligibilityRequirementData[] requirements = definition.EligibilityRequirementGroups
                .SelectMany(group => group.requirements ?? Array.Empty<QuestEligibilityRequirementData>())
                .Where(requirement => requirement != null && !string.IsNullOrWhiteSpace(requirement.requiredId))
                .ToArray();
            IEnumerable<string> Values(QuestEligibilityRequirementKind kind) => requirements
                .Where(requirement => requirement.kind == kind)
                .Select(requirement => requirement.requiredId);
            Dictionary<string, int> ValuesWithScores(QuestEligibilityRequirementKind kind) => requirements
                .Where(requirement => requirement.kind == kind)
                .GroupBy(requirement => requirement.requiredId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Max(requirement => Math.Max(requirement.minimumValue, 1000)), StringComparer.Ordinal);

            return new QuestEligibilityFactSet(
                activePersons: new[] { personId },
                capabilities: Values(QuestEligibilityRequirementKind.Capability).Concat(Values(QuestEligibilityRequirementKind.Custom)),
                skills: ValuesWithScores(QuestEligibilityRequirementKind.Skill),
                traits: Values(QuestEligibilityRequirementKind.Trait),
                possessedItems: Values(QuestEligibilityRequirementKind.ItemPossessed),
                equippedItems: Values(QuestEligibilityRequirementKind.ItemEquipped),
                professions: Values(QuestEligibilityRequirementKind.Profession),
                qualifications: Values(QuestEligibilityRequirementKind.Qualification),
                credentials: Values(QuestEligibilityRequirementKind.Credential),
                employments: Values(QuestEligibilityRequirementKind.Employment),
                organizationMemberships: Values(QuestEligibilityRequirementKind.OrganizationMembership),
                organizationRanks: Values(QuestEligibilityRequirementKind.OrganizationRank),
                offices: Values(QuestEligibilityRequirementKind.Office),
                authorityGrants: Values(QuestEligibilityRequirementKind.InstitutionalAuthority)
                    .Concat(Values(QuestEligibilityRequirementKind.Custom))
                    .Concat(definition.OfferingAuthorityRequirementIds),
                factions: Values(QuestEligibilityRequirementKind.FactionAffiliation),
                reputations: ValuesWithScores(QuestEligibilityRequirementKind.Reputation),
                relationships: ValuesWithScores(QuestEligibilityRequirementKind.Relationship),
                citizenships: Values(QuestEligibilityRequirementKind.Citizenship),
                residencies: Values(QuestEligibilityRequirementKind.Residency),
                legalStatuses: Values(QuestEligibilityRequirementKind.LegalStatus),
                permits: Values(QuestEligibilityRequirementKind.Permit),
                knownSubjects: Values(QuestEligibilityRequirementKind.Knowledge),
                priorQuestStates: Values(QuestEligibilityRequirementKind.PriorQuestState),
                historyFacts: Values(QuestEligibilityRequirementKind.WorldHistoryFact),
                narrativeStates: Values(QuestEligibilityRequirementKind.NarrativeState));
        }

        private QuestObjectiveOperationResult ApplyObjectiveSignal(QuestObjectiveSignal signal)
        {
            if (signal == null || string.IsNullOrWhiteSpace(signal.transactionId))
                return QuestObjectiveOperationResult.Failure(QuestObjectiveOperationStatus.InvalidRequest, "Quest objective signal is invalid.", Objectives.Revision);
            if (!processedObjectiveTransactions.Add(signal.transactionId))
                return Objectives.ApplySignal(signal);

            QuestObjectiveOperationResult result = Objectives.ApplySignal(signal);
            if (!result.Succeeded) return result;

            string contributingPerson = string.IsNullOrWhiteSpace(signal.participantPersonId) ? signal.actorPersonId : signal.participantPersonId;
            PartySnapshot contributingParty = services.AdventuringParties.GetPartyForPerson(contributingPerson);
            if (contributingParty != null)
            {
                services.PartyOperations.RecordContribution(contributingParty.PartyId, contributingPerson, Math.Max(1, signal.amount));
            }

            foreach (QuestAssignmentSnapshot assignment in Participation.QueryAssignments(new QuestAssignmentQuery
            {
                assigneePersonId = PlayerPersonId,
                lifecycleState = QuestAssignmentLifecycleState.Active,
                access = QuestVisibilityAccess.PrivilegedDiagnostic
            }))
            {
                QuestCompletionEvaluationResult ready = Outcomes.EvaluateCompletion(new QuestCompletionEvaluationRequest
                {
                    assignmentId = assignment.AssignmentId,
                    requesterPersonId = PlayerPersonId,
                    worldTime = signal.worldTime,
                    access = QuestVisibilityAccess.PrivilegedDiagnostic,
                    preview = true
                });
                if (ready.Ready) CompleteAssignment(assignment.AssignmentId);
            }

            services.DirtyTracker?.MarkDirty("Quest objective progress changed.");
            Changed?.Invoke();
            return result;
        }

        private void HandleObjectiveSignal(QuestObjectiveSignal signal)
        {
            if (signal == null) return;
            if (string.IsNullOrWhiteSpace(signal.actorPersonId)) signal.actorPersonId = PlayerPersonId;
            if (string.IsNullOrWhiteSpace(signal.participantPersonId)) signal.participantPersonId = PlayerPersonId;
            if (signal.worldTime <= 0d) signal.worldTime = WorldTime;
            if (string.IsNullOrWhiteSpace(signal.transactionId)) signal.transactionId = NextTransaction("quest.objective.bus");
            if (string.IsNullOrWhiteSpace(signal.sourceEventId)) signal.sourceEventId = $"event.quest-objective.{++transactionSequence:000000}";
            ApplyObjectiveSignal(signal);
        }

        private void HandleDialogueEvent(DialogueFlowEventData evt)
        {
            if (evt == null || evt.eventKind != DialogueFlowEventKind.ChoiceSelected) return;
            NarrativeEvents.EmitSignal(new NarrativeSignalRequest
            {
                transactionId = $"tx.narrative.dialogue.{evt.eventId}",
                signalDefinitionId = PrototypeNarrativeEventDefinitionFactory.DialogueChoiceSignalId,
                sourceKind = NarrativeSignalSourceKind.DialogueEffect,
                sourceId = evt.choiceId,
                actorPersonId = evt.actorPersonId,
                worldTime = evt.worldTime
            });
            EmitArcSignal(NarrativeArcSignalCategory.DialogueChoice, evt.choiceId, evt.choiceId, evt.eventId);
        }

        private QuestEligibilityContext BuildEligibility(string interactionPointId)
        {
            string person = PlayerPersonId;
            PartySnapshot party = services.AdventuringParties.GetPartyForPerson(person);
            string[] undertakingMembers = party?.MemberPersonIds?.ToArray() ?? new[] { person };
            string playerLocation = ResolvePlayerLocation();
            if (party != null)
            {
                services.PartyOperations.ReportMemberState(party.PartyId, person, PartyMemberReadiness.Ready, playerLocation, 0f, true, true, true);
            }
            string[] readyMembers = party == null ? new[] { person } : services.PartyOperations.GetReadyMemberIds(party.PartyId, playerLocation).ToArray();
            OrganizationMembershipSnapshot[] memberships = services.OrganizationMemberships?.QueryMemberships(person, activeOnly: true).ToArray() ?? Array.Empty<OrganizationMembershipSnapshot>();
            string[] organizations = memberships.Select(value => value.OrganizationId).ToArray();
            string[] ranks = memberships.SelectMany(value => value.RankAssignments).Where(value => value.IsActive).Select(value => value.rankDefinitionId).ToArray();
            string[] offices = memberships.SelectMany(value => value.OfficeAssignments).Where(value => value.IsActive).Select(value => value.officeId).ToArray();
            return new QuestEligibilityContext
            {
                personId = person,
                partyId = party?.PartyId ?? string.Empty,
                partyMemberPersonIds = undertakingMembers,
                readyPartyMemberPersonIds = readyMembers,
                locationId = playerLocation,
                interactionPointId = interactionPointId ?? string.Empty,
                worldTime = WorldTime,
                access = QuestVisibilityAccess.Recipient,
                facts = new QuestEligibilityFactSet(
                    activePersons: new[] { person },
                    organizationMemberships: organizations,
                    organizationRanks: ranks,
                    offices: offices,
                    knownSubjects: services.PlayerKnowledge?.CreateSnapshot()?.Beliefs.Select(value => value.Proposition?.SubjectId) ?? Array.Empty<string>())
            };
        }

        private DialogueConditionContext BuildDialogueContext(string providerPersonId, string interactionPointId, string locationId)
        {
            QuestEligibilityContext eligibility = BuildEligibility(interactionPointId);
            PartySnapshot dialogueParty = services.AdventuringParties.GetPartyForPerson(PlayerPersonId);
            QuestAssignmentSnapshot[] assignments = Participation.QueryAssignments(new QuestAssignmentQuery
            {
                assigneePersonId = PlayerPersonId,
                access = QuestVisibilityAccess.Recipient,
                includeHistorical = true
            }).ToArray();
            return new DialogueConditionContext
            {
                actorPersonId = PlayerPersonId,
                speakerPersonId = providerPersonId ?? string.Empty,
                locationId = string.IsNullOrWhiteSpace(locationId) ? eligibility.locationId : locationId,
                interactionPointId = interactionPointId ?? string.Empty,
                worldTime = WorldTime,
                facts = eligibility.facts,
                activeQuestIds = assignments.Where(value => value.LifecycleState == QuestAssignmentLifecycleState.Active).Select(value => value.QuestId),
                activeOfferIds = Participation.QueryOffers(new QuestOfferQuery { recipientPersonId = PlayerPersonId, lifecycleState = QuestOfferLifecycleState.Active, access = QuestVisibilityAccess.Recipient }).Select(value => value.OfferId),
                activeAssignmentQuestIds = assignments.Where(value => value.LifecycleState == QuestAssignmentLifecycleState.Active).Select(value => value.QuestId),
                completedQuestIds = Outcomes.QueryOutcomes(new QuestOutcomeQuery { requesterPersonId = PlayerPersonId, outcomeKind = QuestTerminalOutcomeKind.Completed, access = QuestVisibilityAccess.Recipient }).Select(value => value.QuestId),
                claimableRewardIds = Outcomes.QueryRewards(new QuestRewardQuery { requesterPersonId = PlayerPersonId, recipientPersonId = PlayerPersonId, access = QuestVisibilityAccess.Recipient }).Where(value => value.State == QuestRewardEntitlementState.Claimable).Select(value => value.EntitlementId),
                partyId = dialogueParty?.PartyId ?? string.Empty,
                partyLeaderPersonId = dialogueParty?.LeaderPersonId ?? string.Empty,
                partyMemberPersonIds = dialogueParty?.MemberPersonIds ?? Array.Empty<string>(),
                readyPartyMemberPersonIds = dialogueParty == null ? new[] { PlayerPersonId } : services.PartyOperations.GetReadyMemberIds(dialogueParty.PartyId),
                access = ConversationAccessLevel.Participant
            };
        }

        private DialogueEffectExecutionResult AcceptDialogueQuestOffer(DialogueEffectExecutionRequest request)
        {
            string target = request.effect.targetId;
            QuestOfferSnapshot offer = Participation.QueryOffers(new QuestOfferQuery
            {
                requesterPersonId = request.actorPersonId,
                recipientPersonId = request.actorPersonId,
                lifecycleState = QuestOfferLifecycleState.Active,
                access = QuestVisibilityAccess.Recipient
            }).FirstOrDefault(value => value.OfferId == target || value.QuestId == target || QuestDefinitionMatches(value.QuestId, target));

            if (offer == null)
            {
                QuestSnapshot quest = Quests.Query(new QuestQuery { definitionId = target, recipientId = request.actorPersonId }).FirstOrDefault()
                    ?? Quests.Query(new QuestQuery { definitionId = target }).FirstOrDefault();
                if (quest == null || !registry.TryGet(quest.QuestDefinitionId, out QuestDefinition definition))
                    return DialogueEffectExecutionResult.Failure($"Quest '{target}' is unavailable.");

                QuestIssuerReferenceData provider = quest.Issuer;
                provider.actingPersonId = request.conditionContext?.speakerPersonId;
                QuestParticipationOperationResult created = Participation.CreateOffer(new QuestOfferRequest
                {
                    transactionId = $"dialogue.effect.{request.effect.effectId}.{request.flowId}.offer",
                    questId = quest.QuestId,
                    recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = request.actorPersonId },
                    institutionalIssuer = quest.Issuer,
                    offeringProvider = provider,
                    channel = QuestOfferChannel.GuildCounter,
                    sourceInteractionPointId = request.conditionContext?.interactionPointId,
                    sourceLocationId = request.conditionContext?.locationId,
                    authorityBasisId = definition.OfferingAuthorityRequirementIds.FirstOrDefault(),
                    eligibilityContext = BuildEligibility(request.conditionContext?.interactionPointId),
                    worldTime = request.worldTime,
                    preview = request.preview
                });
                if (!created.Succeeded) return DialogueEffectExecutionResult.Failure(created.Message);
                if (request.preview) return DialogueEffectExecutionResult.Success("quest.offer", created.Offer?.OfferId);
                offer = created.Offer;
            }

            QuestParticipationOperationResult accepted = Participation.AcceptOffer(new QuestAcceptOfferRequest
            {
                transactionId = $"dialogue.effect.{request.effect.effectId}.{request.flowId}.accept",
                offerId = offer.OfferId,
                personId = request.actorPersonId,
                explicitConsent = true,
                consentRecordId = $"consent.dialogue.{Sanitize(request.flowId)}.{Sanitize(offer.OfferId)}",
                eligibilityContext = BuildEligibility(request.conditionContext?.interactionPointId),
                worldTime = request.worldTime,
                preview = request.preview
            });
            if (!accepted.Succeeded) return DialogueEffectExecutionResult.Failure(accepted.Message);
            if (accepted.Assignment != null) InitializeAssignment(accepted.Assignment);
            return DialogueEffectExecutionResult.Success("quest.participation", accepted.Assignment?.AssignmentId, accepted.Duplicate);
        }

        private DialogueEffectExecutionResult CreateDialogueQuestOffer(DialogueEffectExecutionRequest request)
        {
            QuestSnapshot quest = Quests.Query(new QuestQuery { definitionId = request.effect.targetId, recipientId = request.actorPersonId }).FirstOrDefault()
                ?? Quests.Query(new QuestQuery { definitionId = request.effect.targetId }).FirstOrDefault();
            if (quest == null) return DialogueEffectExecutionResult.Failure($"Quest '{request.effect.targetId}' is unavailable.");
            QuestParticipationOperationResult result = Participation.CreateOffer(new QuestOfferRequest
            {
                transactionId = $"dialogue.effect.{request.effect.effectId}.{request.flowId}",
                questId = quest.QuestId,
                recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = request.actorPersonId },
                institutionalIssuer = quest.Issuer,
                offeringProvider = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Person, issuerId = request.conditionContext?.speakerPersonId },
                channel = QuestOfferChannel.DirectPerson,
                sourceInteractionPointId = request.conditionContext?.interactionPointId,
                sourceLocationId = request.conditionContext?.locationId,
                eligibilityContext = BuildEligibility(request.conditionContext?.interactionPointId),
                worldTime = request.worldTime
            });
            return result.Succeeded ? DialogueEffectExecutionResult.Success("quest.participation", result.Offer?.OfferId, result.Duplicate) : DialogueEffectExecutionResult.Failure(result.Message);
        }

        private DialogueEffectExecutionResult TransitionDialogueOffer(DialogueEffectExecutionRequest request, bool refuse)
        {
            QuestOfferSnapshot offer = Participation.QueryOffers(new QuestOfferQuery { recipientPersonId = request.actorPersonId, lifecycleState = QuestOfferLifecycleState.Active, access = QuestVisibilityAccess.Recipient })
                .FirstOrDefault(value => value.OfferId == request.effect.targetId || value.QuestId == request.effect.targetId || QuestDefinitionMatches(value.QuestId, request.effect.targetId));
            if (offer == null) return DialogueEffectExecutionResult.Failure("No matching active quest offer exists.");
            QuestParticipationOperationResult result = refuse
                ? Participation.RefuseOffer(new QuestOfferLifecycleRequest { transactionId = $"dialogue.effect.{request.effect.effectId}.{request.flowId}", offerId = offer.OfferId, actingPersonId = request.actorPersonId, worldTime = request.worldTime })
                : null;
            return result != null && result.Succeeded ? DialogueEffectExecutionResult.Success("quest.participation", offer.OfferId, result.Duplicate) : DialogueEffectExecutionResult.Failure(result?.Message ?? "Quest offer transition failed.");
        }

        private DialogueEffectExecutionResult CompleteDialogueQuest(DialogueEffectExecutionRequest request)
        {
            QuestAssignmentSnapshot assignment = Participation.QueryAssignments(new QuestAssignmentQuery { assigneePersonId = request.actorPersonId, lifecycleState = QuestAssignmentLifecycleState.Active, access = QuestVisibilityAccess.Recipient })
                .FirstOrDefault(value => value.AssignmentId == request.effect.targetId || value.QuestId == request.effect.targetId || QuestDefinitionMatches(value.QuestId, request.effect.targetId));
            if (assignment == null) return DialogueEffectExecutionResult.Failure("No matching active quest assignment exists.");
            QuestOutcomeOperationResult result = CompleteAssignment(assignment.AssignmentId, request.conditionContext?.interactionPointId);
            return result.Succeeded ? DialogueEffectExecutionResult.Success("quest.outcome", result.Outcome?.TerminalOutcomeId, result.Duplicate) : DialogueEffectExecutionResult.Failure(result.Message);
        }

        private DialogueEffectExecutionResult ClaimDialogueReward(DialogueEffectExecutionRequest request)
        {
            QuestRewardEntitlementSnapshot reward = Outcomes.QueryRewards(new QuestRewardQuery { recipientPersonId = request.actorPersonId, requesterPersonId = request.actorPersonId, access = QuestVisibilityAccess.Recipient })
                .FirstOrDefault(value => value.EntitlementId == request.effect.targetId || value.QuestId == request.effect.targetId || QuestDefinitionMatches(value.QuestId, request.effect.targetId));
            if (reward == null) return DialogueEffectExecutionResult.Failure("No matching quest reward is claimable.");
            QuestOutcomeOperationResult result = ClaimReward(reward.EntitlementId);
            return result.Succeeded ? DialogueEffectExecutionResult.Success("quest.outcome", reward.EntitlementId, result.Duplicate) : DialogueEffectExecutionResult.Failure(result.Message);
        }

        private DialogueEffectExecutionResult ApplyDialogueStateTransition(DialogueEffectExecutionRequest request)
        {
            NarrativeStateTransitionResult result = NarrativeState.RequestTransition(new NarrativeStateTransitionRequest
            {
                transactionId = $"dialogue.effect.{request.effect.effectId}.{request.flowId}",
                transitionDefinitionId = request.effect.targetId,
                scope = NarrativeStateScope.Person,
                scopeKey = request.actorPersonId,
                sourceKind = NarrativeTransitionSourceKind.DialogueChoice,
                sourceId = request.choiceId,
                worldTime = request.worldTime,
                preview = request.preview
            });
            return result.Succeeded ? DialogueEffectExecutionResult.Success("narrative.state", result.Snapshot?.NarrativeStateId, result.Duplicate) : DialogueEffectExecutionResult.Failure(result.Message);
        }

        private DialogueEffectExecutionResult ExecuteSocialDialogueEffect(DialogueEffectExecutionRequest request)
        {
            string targetPerson = string.IsNullOrWhiteSpace(request.effect.secondaryTargetId) ? request.conditionContext?.speakerPersonId : request.effect.secondaryTargetId;
            var result = services.RecordSocialInteraction(request.effect.targetId, request.actorPersonId, targetPerson, request.conversationId, $"dialogue.effect.{request.effect.effectId}.{request.flowId}");
            return result.Succeeded ? DialogueEffectExecutionResult.Success("world.social", result.Record?.InteractionRecordId, result.Duplicate) : DialogueEffectExecutionResult.Failure(result.Message);
        }

        private bool GrantInformation(string subjectId, string personId, double worldTime)
        {
            if (services.PlayerKnowledge == null || string.IsNullOrWhiteSpace(subjectId)) return false;
            KnowledgeOperationResult result = services.PlayerKnowledge.RecordObservation(new KnowledgeObservationRequest
            {
                PersonId = personId,
                TransactionId = NextTransaction("knowledge.narrative"),
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = "knowledge.fact.aware",
                    subjectId = subjectId,
                    subjectType = KnowledgeSubjectType.Event,
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true
                },
                AcquisitionSource = KnowledgeAcquisitionSource.ScriptedRevelation,
                Provenance = KnowledgeProvenance.ScriptedDiscovery,
                Strength = KnowledgeConfidence.DefaultObservation,
                GameTimeSeconds = worldTime,
                SourceId = "narrative"
            });
            return result.Succeeded;
        }

        private void EmitArcSignal(NarrativeArcSignalCategory category, string sourceId, string value, string sourceEventId)
        {
            NarrativeArcs.ApplySignal(new NarrativeArcSignalRequest
            {
                transactionId = NextTransaction("narrative.arc.progress"),
                category = category,
                sourceId = sourceId,
                value = value,
                secondaryId = sourceEventId,
                actorPersonId = PlayerPersonId,
                worldTime = WorldTime
            });
        }

        private void StartPrototypeArc(string definitionId, NarrativeArcScope scope, string scopeKey)
        {
            if (NarrativeArcs.Query(new NarrativeArcQuery { arcDefinitionId = definitionId, scopeKey = scopeKey }).Count > 0) return;
            NarrativeArcs.StartArc(new NarrativeArcStartRequest
            {
                transactionId = $"tx.prototype.seed.arc.{Sanitize(definitionId)}.{Sanitize(scopeKey)}",
                narrativeArcId = $"narrative-arc.prototype.{Sanitize(definitionId)}.{Sanitize(scopeKey)}",
                arcDefinitionId = definitionId,
                scopeKey = scopeKey,
                actorPersonId = PlayerPersonId,
                worldTime = 0d
            });
        }

        private bool QuestDefinitionMatches(string questId, string target)
        {
            return Quests.TryGetSnapshot(questId, out QuestSnapshot quest) && string.Equals(quest.QuestDefinitionId, target, StringComparison.Ordinal);
        }

        private string ResolvePlayerLocation()
        {
            return services.WorldEntityLocations?.ResolvePhysicalLocation(PrototypeEntityLocationFactory.Person(PlayerPersonId))?.LocationId ?? string.Empty;
        }

        private ConversationParticipantRecordData Participant(string participantId, string personId, ConversationParticipantRole role, string locationId, string interactionPointId)
        {
            return new ConversationParticipantRecordData
            {
                participantId = participantId,
                personId = personId ?? string.Empty,
                role = role,
                currentLocationId = locationId ?? string.Empty,
                currentInteractionPointId = interactionPointId ?? string.Empty,
                provenanceId = "prototype.scene.dialogue"
            };
        }

        private string NextTransaction(string prefix) => $"tx.{prefix}.{++transactionSequence:000000}";
        private long SchedulerBucket() => (long)Math.Floor(WorldTime / SchedulerIntervalSeconds);
        private static string Sanitize(string value) => new string((value ?? string.Empty).ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '-' ? character : '-').ToArray()).Trim('-');
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(PrototypeNarrativeCoordinator)); }
    }

    public sealed class PrototypeQuestJournalEntry
    {
        public PrototypeQuestJournalEntry(QuestAssignmentSnapshot assignment, QuestSnapshot quest, QuestDefinition definition, IReadOnlyList<QuestObjectiveSnapshot> objectives, QuestTerminalOutcomeSnapshot outcome, IReadOnlyList<QuestRewardEntitlementSnapshot> rewards)
        {
            Assignment = assignment;
            Quest = quest;
            Definition = definition;
            Objectives = objectives ?? Array.Empty<QuestObjectiveSnapshot>();
            Outcome = outcome;
            Rewards = rewards ?? Array.Empty<QuestRewardEntitlementSnapshot>();
        }

        public QuestAssignmentSnapshot Assignment { get; }
        public QuestSnapshot Quest { get; }
        public QuestDefinition Definition { get; }
        public IReadOnlyList<QuestObjectiveSnapshot> Objectives { get; }
        public QuestTerminalOutcomeSnapshot Outcome { get; }
        public IReadOnlyList<QuestRewardEntitlementSnapshot> Rewards { get; }
        public string AssignmentId => Assignment?.AssignmentId ?? string.Empty;
        public string Title => Definition?.Title ?? Quest?.QuestDefinitionId ?? "Unknown Quest";
        public string Summary => Definition?.Summary ?? string.Empty;
        public bool IsTerminal => Outcome != null || Assignment == null || Assignment.LifecycleState != QuestAssignmentLifecycleState.Active;
        public bool CanAbandon => Assignment?.LifecycleState == QuestAssignmentLifecycleState.Active && (Definition?.AbandonmentPolicy == QuestAbandonmentPolicy.AllowedKeepsCapacityReserved || Definition?.AbandonmentPolicy == QuestAbandonmentPolicy.AllowedReleasesCapacity);
        public QuestRewardEntitlementSnapshot ClaimableReward => Rewards.FirstOrDefault(value => value.State == QuestRewardEntitlementState.Claimable);
    }
}
