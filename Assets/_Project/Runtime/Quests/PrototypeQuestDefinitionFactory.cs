using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Quests
{
    public static class PrototypeQuestDefinitionFactory
    {
        public const string GuildPostingDefinitionId = "quest-definition.prototype.guild-posting";
        public const string MerchantDeliveryDefinitionId = "quest-definition.prototype.merchant-delivery";
        public const string CivicInvestigationDefinitionId = "quest-definition.prototype.civic-investigation";
        public const string HiddenDungeonRumorDefinitionId = "quest-definition.prototype.hidden-dungeon-rumor";
        public const string DynamicBountyDefinitionId = "quest-definition.prototype.dynamic-bounty";

        public static readonly string[] PrototypeDefinitionIds =
        {
            GuildPostingDefinitionId,
            MerchantDeliveryDefinitionId,
            CivicInvestigationDefinitionId,
            HiddenDungeonRumorDefinitionId,
            DynamicBountyDefinitionId
        };

        public static DefinitionRegistry AddMissingPrototypeQuestDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            foreach (QuestDefinition definition in CreateMissingQuestDefinitions(ids))
            {
                definitions.Add(definition);
            }

            return PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(new DefinitionRegistry(definitions));
        }

        public static IReadOnlyList<QuestDefinition> CreateMissingQuestDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<QuestDefinition> definitions = new List<QuestDefinition>();
            Add(definitions, ids, GuildPostingDefinitionId, "Guild Posting", QuestCategory.GuildQuest, QuestDefinitionImportance.Standard, QuestDefinitionRepeatabilityPolicy.Unique, QuestVisibility.Public, QuestSourceChannel.QuestBoard, new[] { QuestIssuerType.Organization }, new[] { QuestRecipientScope.Open, QuestRecipientScope.Person }, new[] { "guild", "posting", "public" });
            Add(definitions, ids, MerchantDeliveryDefinitionId, "Merchant Delivery", QuestCategory.Delivery, QuestDefinitionImportance.Standard, QuestDefinitionRepeatabilityPolicy.RepeatablePerRecipient, QuestVisibility.LocallyKnown, QuestSourceChannel.Contract, new[] { QuestIssuerType.Organization, QuestIssuerType.Business }, new[] { QuestRecipientScope.Person }, new[] { "merchant", "delivery", "contract" }, multiple: true, perWorldUnique: false, perRecipientUnique: true);
            Add(definitions, ids, CivicInvestigationDefinitionId, "Civic Investigation", QuestCategory.Investigation, QuestDefinitionImportance.Important, QuestDefinitionRepeatabilityPolicy.Unique, QuestVisibility.Restricted, QuestSourceChannel.Government, new[] { QuestIssuerType.Government, QuestIssuerType.Office }, new[] { QuestRecipientScope.Person, QuestRecipientScope.Officeholder }, new[] { "civic", "investigation", "restricted" });
            Add(definitions, ids, HiddenDungeonRumorDefinitionId, "Hidden Dungeon Rumor", QuestCategory.Discovery, QuestDefinitionImportance.Minor, QuestDefinitionRepeatabilityPolicy.Unique, QuestVisibility.Hidden, QuestSourceChannel.Discovery, new[] { QuestIssuerType.Anonymous, QuestIssuerType.System }, new[] { QuestRecipientScope.Open }, new[] { "hidden", "dungeon", "rumor" });
            Add(definitions, ids, DynamicBountyDefinitionId, "Dynamic Bounty", QuestCategory.BountyPlaceholder, QuestDefinitionImportance.Standard, QuestDefinitionRepeatabilityPolicy.DynamicTemplate, QuestVisibility.Public, QuestSourceChannel.QuestBoard, new[] { QuestIssuerType.Organization, QuestIssuerType.Government }, new[] { QuestRecipientScope.Open, QuestRecipientScope.Person }, new[] { "bounty", "dynamic" }, dynamic: true, multiple: true, perWorldUnique: false);
            return definitions;
        }

        private static void Add(
            ICollection<QuestDefinition> definitions,
            ISet<string> existingIds,
            string id,
            string title,
            QuestCategory category,
            QuestDefinitionImportance importance,
            QuestDefinitionRepeatabilityPolicy repeatability,
            QuestVisibility visibility,
            QuestSourceChannel source,
            IEnumerable<QuestIssuerType> issuerTypes,
            IEnumerable<QuestRecipientScope> recipientScopes,
            IEnumerable<string> tags,
            bool dynamic = false,
            bool multiple = false,
            bool perWorldUnique = true,
            bool perRecipientUnique = false)
        {
            if (existingIds.Contains(id))
            {
                return;
            }

            QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
            definition.name = title;
            definition.DevelopmentConfigureIdentity(id, title, category, importance, repeatability, visibility, source, issuerTypes, recipientScopes, tags, dynamic, multiple, perWorldUnique, perRecipientUnique);
            ConfigureParticipation(definition, id);
            ConfigureObjectives(definition, id);
            ConfigureOutcomes(definition, id);
            definitions.Add(definition);
            existingIds.Add(id);
        }

        private static void ConfigureParticipation(QuestDefinition definition, string id)
        {
            switch (id)
            {
                case GuildPostingDefinitionId:
                    definition.DevelopmentConfigureParticipation(
                        assignment: QuestAssignmentPolicy.Exclusive,
                        consent: QuestConsentPolicy.ExplicitRecipientConsentRequired,
                        refusal: QuestRefusalPolicy.MayReoffer,
                        abandonment: QuestAbandonmentPolicy.AllowedReleasesCapacity,
                        capacity: 1,
                        offerDuration: 7d,
                        authorityRequirements: new[] { "authority.prototype.guild.quest-offer" },
                        eligibilityGroups: new[] { All("guild-context", Membership("organization.prototype.adventurers-guild"), Interaction(UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId)) });
                    break;
                case MerchantDeliveryDefinitionId:
                    definition.DevelopmentConfigureParticipation(
                        assignment: QuestAssignmentPolicy.Nonexclusive,
                        consent: QuestConsentPolicy.ExplicitRecipientConsentRequired,
                        refusal: QuestRefusalPolicy.MayReoffer,
                        abandonment: QuestAbandonmentPolicy.AllowedReleasesCapacity,
                        capacity: 0,
                        offerDuration: 3d,
                        authorityRequirements: new[] { "authority.prototype.merchant.quest-offer" },
                        eligibilityGroups: new[] { All("merchant-counter", Interaction(UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId)) });
                    break;
                case CivicInvestigationDefinitionId:
                    definition.DevelopmentConfigureParticipation(
                        assignment: QuestAssignmentPolicy.CapacityLimited,
                        consent: QuestConsentPolicy.DirectInstitutionalAssignmentAllowed,
                        refusal: QuestRefusalPolicy.RefusalClosesOffer,
                        abandonment: QuestAbandonmentPolicy.AllowedKeepsCapacityReserved,
                        capacity: 2,
                        offerDuration: 5d,
                        authorityRequirements: new[] { "authority.prototype.city.quest-assign" },
                        eligibilityGroups: new[] { All("civic-authorized", Office("office.prototype.city-investigator"), Citizenship("government.prototype.city")) });
                    break;
                case HiddenDungeonRumorDefinitionId:
                    definition.DevelopmentConfigureParticipation(
                        assignment: QuestAssignmentPolicy.Exclusive,
                        consent: QuestConsentPolicy.ExplicitRecipientConsentRequired,
                        refusal: QuestRefusalPolicy.MayReoffer,
                        abandonment: QuestAbandonmentPolicy.AllowedReleasesCapacity,
                        capacity: 1,
                        offerDuration: -1d,
                        prevalidateOffers: false,
                        eligibilityGroups: new[] { Any("rumor-discovered", Knowledge("subject.prototype.hidden-dungeon"), History("history.prototype.heard-dungeon-rumor")) });
                    break;
                case DynamicBountyDefinitionId:
                    definition.DevelopmentConfigureParticipation(
                        assignment: QuestAssignmentPolicy.CapacityLimited,
                        consent: QuestConsentPolicy.ExplicitRecipientConsentRequired,
                        refusal: QuestRefusalPolicy.MayReoffer,
                        abandonment: QuestAbandonmentPolicy.AllowedReleasesCapacity,
                        capacity: 4,
                        offerDuration: 2d,
                        authorityRequirements: new[] { "authority.prototype.bounty-board.post" },
                        eligibilityGroups: new[] { All("bounty-board", Interaction("interaction-point.prototype.bounty-board")) });
                    break;
                default:
                    definition.DevelopmentConfigureParticipation();
                    break;
            }
        }

        private static void ConfigureObjectives(QuestDefinition definition, string id)
        {
            switch (id)
            {
                case GuildPostingDefinitionId:
                    definition.DevelopmentConfigureObjectives(
                        new[]
                        {
                            Objective("quest-objective-definition.prototype.guild.use-counter", "Use Adventurer Guild Counter", QuestObjectiveCategory.UseInteractionPoint, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId), tags: new[] { "required", "guild", "interaction" }, order: 10),
                            Objective("quest-objective-definition.prototype.guild.enter-dungeon", "Enter the Dungeon", QuestObjectiveCategory.VisitLocation, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Location, "location.prototype.dungeon-entry"), prerequisites: new[] { "quest-objective-definition.prototype.guild.use-counter" }, tags: new[] { "required", "exploration" }, order: 20),
                            Objective("quest-objective-definition.prototype.guild.defeat-monsters", "Defeat Three Monsters", QuestObjectiveCategory.DefeatCount, QuestObjectiveProgressModel.Counter, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, "enemy-family.prototype.monster"), amount: 3, prerequisites: new[] { "quest-objective-definition.prototype.guild.enter-dungeon" }, tags: new[] { "required", "combat" }, order: 30),
                            Objective("quest-objective-definition.prototype.guild.report-return", "Report Back to the Guild", QuestObjectiveCategory.UseInteractionPoint, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId), prerequisites: new[] { "quest-objective-definition.prototype.guild.defeat-monsters" }, tags: new[] { "required", "guild" }, order: 40)
                        },
                        new[] { Group("quest-objective-group.prototype.guild.required", QuestObjectiveGroupPolicy.OrderedAll, 4, "quest-objective-definition.prototype.guild.use-counter", "quest-objective-definition.prototype.guild.enter-dungeon", "quest-objective-definition.prototype.guild.defeat-monsters", "quest-objective-definition.prototype.guild.report-return") });
                    break;
                case MerchantDeliveryDefinitionId:
                    definition.DevelopmentConfigureObjectives(
                        new[]
                        {
                            Objective("quest-objective-definition.prototype.delivery.collect-parcel", "Collect Merchant Parcel", QuestObjectiveCategory.ObtainItem, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, "item.prototype.merchant-parcel"), tags: new[] { "required", "delivery", "item" }, order: 10),
                            Objective("quest-objective-definition.prototype.delivery.possess-parcel", "Possess Merchant Parcel", QuestObjectiveCategory.PossessItem, QuestObjectiveProgressModel.QuantityCurrent, QuestObjectiveProgressSource.CurrentStateQuery, Target(InformationSubjectType.Custom, "item.prototype.merchant-parcel"), amount: 1, tags: new[] { "required", "delivery", "item" }, order: 20),
                            Objective("quest-objective-definition.prototype.delivery.deliver-parcel", "Deliver Parcel to Merchant Counter", QuestObjectiveCategory.DeliverItem, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId), secondaryTarget: Target(InformationSubjectType.Custom, "item.prototype.merchant-parcel"), prerequisites: new[] { "quest-objective-definition.prototype.delivery.collect-parcel" }, tags: new[] { "required", "delivery" }, order: 30)
                        },
                        new[] { Group("quest-objective-group.prototype.delivery.required", QuestObjectiveGroupPolicy.All, 3, "quest-objective-definition.prototype.delivery.collect-parcel", "quest-objective-definition.prototype.delivery.possess-parcel", "quest-objective-definition.prototype.delivery.deliver-parcel") });
                    break;
                case CivicInvestigationDefinitionId:
                    definition.DevelopmentConfigureObjectives(
                        new[]
                        {
                            Objective("quest-objective-definition.prototype.investigation.visit-office", "Visit Mayor's Office", QuestObjectiveCategory.VisitLocation, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Location, "location.prototype.mayor-office"), tags: new[] { "required", "civic" }, order: 10),
                            Objective("quest-objective-definition.prototype.investigation.learn-fact", "Learn Incident Fact", QuestObjectiveCategory.LearnFact, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.KnowledgeRecord, "fact.prototype.civic-incident"), prerequisites: new[] { "quest-objective-definition.prototype.investigation.visit-office" }, tags: new[] { "required", "knowledge" }, order: 20),
                            Objective("quest-objective-definition.prototype.investigation.report-incident", "Report Incident", QuestObjectiveCategory.ReportIncident, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, "incident.prototype.civic"), prerequisites: new[] { "quest-objective-definition.prototype.investigation.learn-fact" }, tags: new[] { "required", "legal" }, order: 30)
                        },
                        new[] { Group("quest-objective-group.prototype.investigation.required", QuestObjectiveGroupPolicy.OrderedAll, 3, "quest-objective-definition.prototype.investigation.visit-office", "quest-objective-definition.prototype.investigation.learn-fact", "quest-objective-definition.prototype.investigation.report-incident") });
                    break;
                case HiddenDungeonRumorDefinitionId:
                    definition.DevelopmentConfigureObjectives(
                        new[]
                        {
                            Objective("quest-objective-definition.prototype.hidden.discover-dungeon", "Discover Hidden Dungeon", QuestObjectiveCategory.DiscoverLocation, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Location, "location.prototype.secret-dungeon-entry"), classification: QuestObjectiveRequirementClassification.HiddenRequired, visibility: QuestObjectiveVisibility.Hidden, tags: new[] { "hidden", "discovery" }, order: 10),
                            Objective("quest-objective-definition.prototype.hidden.reach-shrine", "Reach Forgotten Shrine", QuestObjectiveCategory.VisitLocation, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Location, "location.prototype.forgotten-shrine"), classification: QuestObjectiveRequirementClassification.Optional, visibility: QuestObjectiveVisibility.Hidden, prerequisites: new[] { "quest-objective-definition.prototype.hidden.discover-dungeon" }, tags: new[] { "optional", "hidden" }, order: 20)
                        },
                        new[] { Group("quest-objective-group.prototype.hidden.required", QuestObjectiveGroupPolicy.All, 1, "quest-objective-definition.prototype.hidden.discover-dungeon") });
                    break;
                case DynamicBountyDefinitionId:
                    definition.DevelopmentConfigureObjectives(
                        new[]
                        {
                            Objective("quest-objective-definition.prototype.bounty.defeat-target", "Defeat Bounty Target", QuestObjectiveCategory.DefeatTarget, QuestObjectiveProgressModel.UniqueTargetCount, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, "encounter.prototype.dynamic-bounty-target"), amount: 1, tags: new[] { "required", "bounty", "combat" }, order: 10),
                            Objective("quest-objective-definition.prototype.bounty.survive-combat", "Survive the Encounter", QuestObjectiveCategory.SurviveCombat, QuestObjectiveProgressModel.BooleanEvent, QuestObjectiveProgressSource.DomainEvent, Target(InformationSubjectType.Custom, "encounter.prototype.dynamic-bounty-target"), classification: QuestObjectiveRequirementClassification.Optional, tags: new[] { "optional", "combat" }, order: 20)
                        },
                        new[] { Group("quest-objective-group.prototype.bounty.required", QuestObjectiveGroupPolicy.All, 1, "quest-objective-definition.prototype.bounty.defeat-target") });
                    break;
                default:
                    definition.DevelopmentConfigureObjectives(Array.Empty<QuestObjectiveDefinitionData>());
                    break;
            }
        }

        private static void ConfigureOutcomes(QuestDefinition definition, string id)
        {
            switch (id)
            {
                case GuildPostingDefinitionId:
                    definition.DevelopmentConfigureOutcomes(
                        completion: TurnIn(UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId),
                        deadlines: new[] { Deadline("quest-deadline-definition.prototype.guild.three-days", 3d) },
                        failures: new[] { Failure("quest-failure-condition.prototype.guild.deadline", QuestFailureReasonCode.DeadlineExpired, QuestFailureTriggerKind.Deadline) },
                        rewards: new[] { RewardPackage("quest-reward-package.prototype.guild.base", QuestRewardDeliveryPolicy.ClaimAfterCompletion, Reward("quest-reward.prototype.guild.gold", QuestRewardCategory.Currency, "currency.gold", 50), Reward("quest-reward.prototype.guild.reputation", QuestRewardCategory.Reputation, "reputation.prototype.adventurers-guild", 5)) },
                        consequences: new[] { Consequence("quest-consequence.prototype.guild.missed", QuestTerminalOutcomeKind.Expired, QuestRewardCategory.Reputation, "reputation.prototype.adventurers-guild", -2) });
                    break;
                case MerchantDeliveryDefinitionId:
                    definition.DevelopmentConfigureOutcomes(
                        completion: TurnIn(UnityIsekaiGame.WorldLocations.PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId),
                        deadlines: new[] { Deadline("quest-deadline-definition.prototype.delivery.one-day", 1d) },
                        failures: new[] { Failure("quest-failure-condition.prototype.delivery.parcel-lost", QuestFailureReasonCode.RequiredItemLost, QuestFailureTriggerKind.StateEvaluation) },
                        rewards: new[] { RewardPackage("quest-reward-package.prototype.delivery.base", QuestRewardDeliveryPolicy.ClaimAfterCompletion, Reward("quest-reward.prototype.delivery.gold", QuestRewardCategory.Currency, "currency.gold", 25), Reward("quest-reward.prototype.delivery.item", QuestRewardCategory.Item, "item.health-potion", 1)) });
                    break;
                case CivicInvestigationDefinitionId:
                    definition.DevelopmentConfigureOutcomes(
                        completion: new QuestCompletionPolicyData { policy = QuestCompletionPolicy.RequireIssuerVerification, requiredIssuerId = "office.prototype.mayor", allowOptionalBonusRewards = true },
                        deadlines: new[] { Deadline("quest-deadline-definition.prototype.investigation.five-days", 5d) },
                        failures: new[] { Failure("quest-failure-condition.prototype.investigation.protected-target", QuestFailureReasonCode.ProtectedTargetLost, QuestFailureTriggerKind.DomainEvent) },
                        rewards: new[] { RewardPackage("quest-reward-package.prototype.investigation.base", QuestRewardDeliveryPolicy.ClaimAfterCompletion, Reward("quest-reward.prototype.investigation.legal-permit", QuestRewardCategory.LegalPermitStatus, "permit.prototype.city-investigator", 1), Reward("quest-reward.prototype.investigation.knowledge", QuestRewardCategory.Knowledge, "knowledge.prototype.civic-incident-resolution", 1)) });
                    break;
                case HiddenDungeonRumorDefinitionId:
                    definition.DevelopmentConfigureOutcomes(
                        completion: new QuestCompletionPolicyData { policy = QuestCompletionPolicy.AutoCompleteWhenRequiredObjectivesSatisfied, allowOptionalBonusRewards = true },
                        rewards: new[] { RewardPackage("quest-reward-package.prototype.hidden.base", QuestRewardDeliveryPolicy.GrantOnCompletion, Reward("quest-reward.prototype.hidden.knowledge", QuestRewardCategory.Knowledge, "knowledge.prototype.hidden-dungeon-confirmed", 1, hidden: true)) });
                    break;
                case DynamicBountyDefinitionId:
                    definition.DevelopmentConfigureOutcomes(
                        completion: TurnIn("interaction-point.prototype.bounty-board"),
                        deadlines: new[] { Deadline("quest-deadline-definition.prototype.bounty.two-days", 2d) },
                        failures: new[] { Failure("quest-failure-condition.prototype.bounty.actor-died", QuestFailureReasonCode.ActorDied, QuestFailureTriggerKind.DomainEvent) },
                        rewards: new[] { RewardPackage("quest-reward-package.prototype.bounty.base", QuestRewardDeliveryPolicy.ClaimAfterCompletion, Reward("quest-reward.prototype.bounty.gold", QuestRewardCategory.Currency, "currency.gold", 75), Reward("quest-reward.prototype.bounty.reputation", QuestRewardCategory.Reputation, "reputation.prototype.city-guard", 8)) });
                    break;
                default:
                    definition.DevelopmentConfigureOutcomes();
                    break;
            }
        }

        private static QuestEligibilityRequirementGroupData All(string id, params QuestEligibilityRequirementData[] requirements)
        {
            return Group(id, QuestEligibilityGroupPolicy.All, requirements);
        }

        private static QuestEligibilityRequirementGroupData Any(string id, params QuestEligibilityRequirementData[] requirements)
        {
            return Group(id, QuestEligibilityGroupPolicy.Any, requirements);
        }

        private static QuestEligibilityRequirementGroupData Group(string id, QuestEligibilityGroupPolicy policy, params QuestEligibilityRequirementData[] requirements)
        {
            return new QuestEligibilityRequirementGroupData
            {
                groupId = id,
                policy = policy,
                requirements = requirements ?? Array.Empty<QuestEligibilityRequirementData>()
            };
        }

        private static QuestEligibilityRequirementData Membership(string id) => Requirement(QuestEligibilityRequirementKind.OrganizationMembership, id);
        private static QuestEligibilityRequirementData Interaction(string id) => Requirement(QuestEligibilityRequirementKind.InteractionPointPresence, id);
        private static QuestEligibilityRequirementData Office(string id) => Requirement(QuestEligibilityRequirementKind.Office, id);
        private static QuestEligibilityRequirementData Citizenship(string id) => Requirement(QuestEligibilityRequirementKind.Citizenship, id);
        private static QuestEligibilityRequirementData Knowledge(string id) => Requirement(QuestEligibilityRequirementKind.Knowledge, id);
        private static QuestEligibilityRequirementData History(string id) => Requirement(QuestEligibilityRequirementKind.WorldHistoryFact, id);

        private static QuestEligibilityRequirementData Requirement(QuestEligibilityRequirementKind kind, string id)
        {
            return new QuestEligibilityRequirementData
            {
                requirementId = $"{kind}.{id}",
                kind = kind,
                requiredId = id,
                comparison = QuestRequirementComparison.Exists
            };
        }

        private static QuestObjectiveDefinitionData Objective(
            string id,
            string label,
            QuestObjectiveCategory category,
            QuestObjectiveProgressModel progressModel,
            QuestObjectiveProgressSource source,
            InformationSubjectReferenceData target,
            InformationSubjectReferenceData secondaryTarget = null,
            int amount = 1,
            IEnumerable<string> prerequisites = null,
            IEnumerable<string> tags = null,
            QuestObjectiveRequirementClassification classification = QuestObjectiveRequirementClassification.Required,
            QuestObjectiveVisibility visibility = QuestObjectiveVisibility.Public,
            QuestObjectiveSatisfactionPolicy satisfactionPolicy = QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied,
            int order = 0)
        {
            return new QuestObjectiveDefinitionData
            {
                objectiveDefinitionId = id,
                label = label,
                description = label,
                category = category,
                progressModel = progressModel,
                progressSource = source,
                classification = classification,
                visibility = visibility,
                satisfactionPolicy = satisfactionPolicy,
                repetitionPolicy = progressModel == QuestObjectiveProgressModel.UniqueTargetCount ? QuestObjectiveRepetitionPolicy.CountUniqueTargetOnce : QuestObjectiveRepetitionPolicy.CountSourceEventOnce,
                beforeActivationPolicy = progressModel == QuestObjectiveProgressModel.QuantityCurrent || progressModel == QuestObjectiveProgressModel.BooleanState || progressModel == QuestObjectiveProgressModel.Threshold
                    ? QuestObjectiveProgressBeforeActivationPolicy.EvaluateCurrentStateOnActivation
                    : QuestObjectiveProgressBeforeActivationPolicy.Ignore,
                ownershipScope = QuestObjectiveOwnershipScope.PerAssignment,
                target = target,
                secondaryTarget = secondaryTarget ?? new InformationSubjectReferenceData(),
                targetAmount = Math.Max(1, amount),
                thresholdValue = Math.Max(1, amount),
                prerequisiteObjectiveDefinitionIds = QuestRuntimeModelUtility.Clean(prerequisites),
                tagIds = QuestRuntimeModelUtility.Clean(tags),
                requiredForCompletion = classification == QuestObjectiveRequirementClassification.Required || classification == QuestObjectiveRequirementClassification.HiddenRequired,
                sequenceOrder = order
            };
        }

        private static QuestObjectiveGroupDefinitionData Group(string id, QuestObjectiveGroupPolicy policy, int threshold, params string[] objectiveIds)
        {
            return new QuestObjectiveGroupDefinitionData
            {
                groupDefinitionId = id,
                label = id,
                policy = policy,
                thresholdCount = threshold,
                objectiveDefinitionIds = QuestRuntimeModelUtility.Clean(objectiveIds),
                requiredForCompletion = true
            };
        }

        private static InformationSubjectReferenceData Target(InformationSubjectType type, string id)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = type,
                subjectId = id,
                tags = Array.Empty<string>()
            };
        }

        private static QuestCompletionPolicyData TurnIn(string interactionPointId)
        {
            return new QuestCompletionPolicyData
            {
                policy = QuestCompletionPolicy.RequireTurnIn,
                requiredInteractionPointId = interactionPointId,
                allowOptionalBonusRewards = true
            };
        }

        private static QuestDeadlineDefinitionData Deadline(string id, double duration)
        {
            return new QuestDeadlineDefinitionData
            {
                deadlineDefinitionId = id,
                startKind = QuestDeadlineStartKind.AssignmentAccepted,
                expirationPolicy = QuestDeadlineExpirationPolicy.FailAssignment,
                durationFromStart = duration
            };
        }

        private static QuestFailureConditionDefinitionData Failure(string id, QuestFailureReasonCode reason, QuestFailureTriggerKind trigger)
        {
            return new QuestFailureConditionDefinitionData
            {
                failureConditionId = id,
                reasonCode = reason,
                triggerKind = trigger,
                subject = Target(InformationSubjectType.Custom, id)
            };
        }

        private static QuestRewardPackageDefinitionData RewardPackage(string id, QuestRewardDeliveryPolicy delivery, params QuestRewardDefinitionData[] rewards)
        {
            return new QuestRewardPackageDefinitionData
            {
                rewardPackageId = id,
                deliveryPolicy = delivery,
                atomicityPolicy = QuestRewardPackageAtomicityPolicy.AllOrNothing,
                rewards = rewards ?? Array.Empty<QuestRewardDefinitionData>()
            };
        }

        private static QuestRewardDefinitionData Reward(string id, QuestRewardCategory category, string targetId, int quantity, bool optional = false, bool hidden = false)
        {
            return new QuestRewardDefinitionData
            {
                rewardDefinitionId = id,
                category = category,
                targetDefinitionId = targetId,
                quantity = quantity,
                optional = optional,
                hidden = hidden
            };
        }

        private static QuestConsequenceDefinitionData Consequence(string id, QuestTerminalOutcomeKind appliesTo, QuestRewardCategory category, string targetId, int magnitude)
        {
            return new QuestConsequenceDefinitionData
            {
                consequenceDefinitionId = id,
                appliesTo = appliesTo,
                category = category,
                targetDefinitionId = targetId,
                magnitude = magnitude
            };
        }
    }
}
