using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public static class PrototypeNarrativeArcDefinitionFactory
    {
        public const string GuildIntroArcDefinitionId = "narrative-arc-definition.prototype.guild-intro";
        public const string MerchantGuildArcDefinitionId = "narrative-arc-definition.prototype.merchant-guild";
        public const string MayorInvestigationArcDefinitionId = "narrative-arc-definition.prototype.mayor-investigation";
        public const string RoyalSuccessionArcDefinitionId = "narrative-arc-definition.prototype.royal-succession";
        public const string ParallelSupportArcDefinitionId = "narrative-arc-definition.prototype.parallel-support";
        public const string CrossArcDependentDefinitionId = "narrative-arc-definition.prototype.cross-arc-dependent";

        public const string GuildIntroJoinStageId = "narrative-arc-stage-definition.prototype.guild-intro.join";
        public const string GuildIntroPostingStageId = "narrative-arc-stage-definition.prototype.guild-intro.guild-posting";
        public const string GuildIntroFollowUpStageId = "narrative-arc-stage-definition.prototype.guild-intro.follow-up";
        public const string MerchantDeliveryStageId = "narrative-arc-stage-definition.prototype.merchant-guild.delivery";
        public const string MayorHiddenStageId = "narrative-arc-stage-definition.prototype.mayor-investigation.hidden";
        public const string RoyalHeirStageId = "narrative-arc-stage-definition.prototype.royal-succession.heir";
        public const string RoyalRivalStageId = "narrative-arc-stage-definition.prototype.royal-succession.rival";
        public const string ParallelAStageId = "narrative-arc-stage-definition.prototype.parallel-support.a";
        public const string ParallelBStageId = "narrative-arc-stage-definition.prototype.parallel-support.b";
        public const string ParallelCStageId = "narrative-arc-stage-definition.prototype.parallel-support.c";
        public const string ParallelJoinStageId = "narrative-arc-stage-definition.prototype.parallel-support.join";
        public const string CrossArcStageId = "narrative-arc-stage-definition.prototype.cross-arc-dependent.follow-up";

        public static readonly string[] PrototypeDefinitionIds =
        {
            GuildIntroArcDefinitionId,
            MerchantGuildArcDefinitionId,
            MayorInvestigationArcDefinitionId,
            RoyalSuccessionArcDefinitionId,
            ParallelSupportArcDefinitionId,
            CrossArcDependentDefinitionId
        };

        public static DefinitionRegistry AddMissingPrototypeNarrativeArcDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            foreach (NarrativeArcDefinition definition in CreateMissingNarrativeArcDefinitions(ids)) definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<NarrativeArcDefinition> CreateMissingNarrativeArcDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<NarrativeArcDefinition> definitions = new List<NarrativeArcDefinition>();

            Add(definitions, ids, new NarrativeArcDefinitionData
            {
                arcDefinitionId = GuildIntroArcDefinitionId,
                displayName = "Prototype Adventurers Guild Introduction",
                scope = NarrativeArcScope.Person,
                visibility = NarrativeEventVisibility.ParticipantKnown,
                stages = new[]
                {
                    Stage(GuildIntroJoinStageId, "Choose guild allegiance", order: 10, initial: true, complete: new[]
                    {
                        StateDependency("dependency.guild-loyal", PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId)
                    }),
                    Stage(GuildIntroPostingStageId, "Guild posting quest", order: 20, entry: new[] { StageDependency("dependency.guild-choice-resolved", NarrativeArcDependencyKind.StageCompleted, GuildIntroJoinStageId) }, complete: new[]
                    {
                        QuestDependency("dependency.guild-posting-completed", PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, QuestTerminalOutcomeKind.Completed)
                    }, quests: new[]
                    {
                        QuestBinding("binding.guild-posting", NarrativeArcQuestBindingMode.InstantiateOnStageActivation, PrototypeQuestDefinitionFactory.GuildPostingDefinitionId)
                    }),
                    Stage(GuildIntroFollowUpStageId, "Follow-up rumor", order: 30, terminal: true, entry: new[] { StageDependency("dependency.guild-posting-resolved", NarrativeArcDependencyKind.StageCompleted, GuildIntroPostingStageId) }, complete: new[]
                    {
                        SignalDependency("dependency.follow-up-signal", NarrativeArcDependencyKind.NarrativeEvent, PrototypeNarrativeEventDefinitionFactory.CascadeFollowUpSignalId)
                    }, actions: new[]
                    {
                        Action("narrative-action.prototype.guild-intro.follow-up-signal", NarrativeActionCategory.EmitNarrativeSignal, PrototypeNarrativeEventDefinitionFactory.CascadeStartSignalId, order: 10)
                    })
                },
                tagIds = new[] { "prototype", "guild", "quest-chain" }
            });

            Add(definitions, ids, new NarrativeArcDefinitionData
            {
                arcDefinitionId = MerchantGuildArcDefinitionId,
                displayName = "Prototype Merchant Guild Delivery",
                scope = NarrativeArcScope.Person,
                visibility = NarrativeEventVisibility.ParticipantKnown,
                stages = new[]
                {
                    Stage(MerchantDeliveryStageId, "Merchant delivery", order: 10, initial: true, terminal: true, complete: new[]
                    {
                        QuestDependency("dependency.merchant-delivery-completed", PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, QuestTerminalOutcomeKind.Completed)
                    }, skip: new[]
                    {
                        QuestDependency("dependency.merchant-delivery-failed", PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, QuestTerminalOutcomeKind.Failed)
                    }, quests: new[]
                    {
                        QuestBinding("binding.merchant-delivery", NarrativeArcQuestBindingMode.InstantiateAndDirectOffer, PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId)
                    })
                },
                tagIds = new[] { "prototype", "merchant", "quest-chain" }
            });

            Add(definitions, ids, new NarrativeArcDefinitionData
            {
                arcDefinitionId = MayorInvestigationArcDefinitionId,
                displayName = "Prototype Mayor Investigation Arc",
                scope = NarrativeArcScope.World,
                visibility = NarrativeEventVisibility.Hidden,
                stages = new[]
                {
                    Stage(MayorHiddenStageId, "Hidden mayor investigation", order: 10, initial: true, hidden: true, complete: new[]
                    {
                        StateDependency("dependency.mayor-exposed", PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, PrototypeNarrativeStateDefinitionFactory.MayorStageVariableId, PrototypeNarrativeStateDefinitionFactory.InvestigationExposedValueId)
                    }, quests: new[]
                    {
                        QuestBinding("binding.civic-investigation", NarrativeArcQuestBindingMode.InstantiateOnStageActivation, PrototypeQuestDefinitionFactory.CivicInvestigationDefinitionId, "quest-source.prototype.mayor-office-desk")
                    })
                },
                tagIds = new[] { "prototype", "mayor", "hidden" }
            });

            Add(definitions, ids, new NarrativeArcDefinitionData
            {
                arcDefinitionId = RoyalSuccessionArcDefinitionId,
                displayName = "Prototype Royal Succession Arc",
                scope = NarrativeArcScope.World,
                visibility = NarrativeEventVisibility.Restricted,
                stages = new[]
                {
                    Stage(RoyalHeirStageId, "Support the heir", order: 10, initial: true, complete: new[]
                    {
                        StateDependency("dependency.heir-branch", PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, PrototypeNarrativeStateDefinitionFactory.RoyalSupportHeirValueId)
                    }),
                    Stage(RoyalRivalStageId, "Support the rival", order: 20, initial: true, complete: new[]
                    {
                        StateDependency("dependency.rival-branch", PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, PrototypeNarrativeStateDefinitionFactory.RoyalSupportRivalValueId)
                    }),
                    Stage("narrative-arc-stage-definition.prototype.royal-succession.reconcile", "Reconcile succession", order: 30, terminal: true, entry: new[]
                    {
                        StageDependency("dependency.any-royal-branch", NarrativeArcDependencyKind.AnyStageResolved, string.Empty, new[] { RoyalHeirStageId, RoyalRivalStageId })
                    }, complete: new[]
                    {
                        StateDependency("dependency.royal-reconciled", PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, PrototypeNarrativeStateDefinitionFactory.RoyalReconciledValueId)
                    })
                },
                tagIds = new[] { "prototype", "royal", "branch" }
            });

            Add(definitions, ids, new NarrativeArcDefinitionData
            {
                arcDefinitionId = ParallelSupportArcDefinitionId,
                displayName = "Prototype Parallel Support Arc",
                scope = NarrativeArcScope.Person,
                visibility = NarrativeEventVisibility.ParticipantKnown,
                stages = new[]
                {
                    Stage(ParallelAStageId, "Parallel support A", order: 10, initial: true, complete: new[] { SignalDependency("dependency.parallel-a", NarrativeArcDependencyKind.Custom, "signal.parallel.a") }),
                    Stage(ParallelBStageId, "Parallel support B", order: 20, initial: true, complete: new[] { SignalDependency("dependency.parallel-b", NarrativeArcDependencyKind.Custom, "signal.parallel.b") }),
                    Stage(ParallelCStageId, "Parallel support C", order: 30, initial: true, complete: new[] { SignalDependency("dependency.parallel-c", NarrativeArcDependencyKind.Custom, "signal.parallel.c") }),
                    Stage(ParallelJoinStageId, "Parallel join", order: 40, terminal: true, entry: new[]
                    {
                        StageDependency("dependency.parallel-two-of-three", NarrativeArcDependencyKind.AtLeastNStagesResolved, string.Empty, new[] { ParallelAStageId, ParallelBStageId, ParallelCStageId }, min: 2)
                    }, complete: new[] { SignalDependency("dependency.parallel-join", NarrativeArcDependencyKind.Custom, "signal.parallel.join") })
                },
                tagIds = new[] { "prototype", "parallel", "merge" }
            });

            Add(definitions, ids, new NarrativeArcDefinitionData
            {
                arcDefinitionId = CrossArcDependentDefinitionId,
                displayName = "Prototype Cross Arc Follow Up",
                scope = NarrativeArcScope.Person,
                visibility = NarrativeEventVisibility.ParticipantKnown,
                stages = new[]
                {
                    Stage(CrossArcStageId, "Cross arc follow-up", order: 10, initial: true, terminal: true, entry: new[]
                    {
                        new NarrativeArcDependencyDefinitionData
                        {
                            dependencyDefinitionId = "dependency.guild-arc-completed",
                            kind = NarrativeArcDependencyKind.ArcCompleted,
                            requiredId = GuildIntroArcDefinitionId
                        }
                    }, complete: new[] { SignalDependency("dependency.cross-finished", NarrativeArcDependencyKind.Custom, "signal.cross.finished") })
                },
                tagIds = new[] { "prototype", "cross-arc" }
            });

            return definitions;
        }

        private static NarrativeArcStageDefinitionData Stage(
            string id,
            string name,
            int order,
            bool initial = false,
            bool terminal = false,
            bool hidden = false,
            NarrativeArcDependencyDefinitionData[] entry = null,
            NarrativeArcDependencyDefinitionData[] complete = null,
            NarrativeArcDependencyDefinitionData[] skip = null,
            NarrativeArcDependencyDefinitionData[] failure = null,
            NarrativeArcQuestBindingDefinitionData[] quests = null,
            NarrativeActionDefinitionData[] actions = null)
        {
            return new NarrativeArcStageDefinitionData
            {
                stageDefinitionId = id,
                displayName = name,
                order = order,
                initial = initial,
                terminalOnCompletion = terminal,
                hidden = hidden,
                entryDependencies = entry ?? Array.Empty<NarrativeArcDependencyDefinitionData>(),
                completionDependencies = complete ?? Array.Empty<NarrativeArcDependencyDefinitionData>(),
                skipDependencies = skip ?? Array.Empty<NarrativeArcDependencyDefinitionData>(),
                failureDependencies = failure ?? Array.Empty<NarrativeArcDependencyDefinitionData>(),
                questBindings = quests ?? Array.Empty<NarrativeArcQuestBindingDefinitionData>(),
                entryActions = actions ?? Array.Empty<NarrativeActionDefinitionData>()
            };
        }

        private static NarrativeArcDependencyDefinitionData StageDependency(string id, NarrativeArcDependencyKind kind, string stageId, IEnumerable<string> stageIds = null, int min = 1)
        {
            return new NarrativeArcDependencyDefinitionData
            {
                dependencyDefinitionId = id,
                kind = kind,
                requiredId = stageId,
                stageDefinitionIds = NarrativeModelUtility.Clean(stageIds),
                minimumCount = min
            };
        }

        private static NarrativeArcDependencyDefinitionData QuestDependency(string id, string questDefinitionId, QuestTerminalOutcomeKind outcome)
        {
            return new NarrativeArcDependencyDefinitionData
            {
                dependencyDefinitionId = id,
                kind = NarrativeArcDependencyKind.QuestOutcome,
                requiredId = questDefinitionId,
                requiredValue = outcome.ToString()
            };
        }

        private static NarrativeArcDependencyDefinitionData StateDependency(string id, string stateId, string variableId, string valueId)
        {
            return new NarrativeArcDependencyDefinitionData
            {
                dependencyDefinitionId = id,
                kind = NarrativeArcDependencyKind.NarrativeState,
                requiredId = $"{stateId}|{variableId}|{valueId}",
                secondaryId = variableId,
                requiredValue = valueId
            };
        }

        private static NarrativeArcDependencyDefinitionData SignalDependency(string id, NarrativeArcDependencyKind kind, string signalId)
        {
            return new NarrativeArcDependencyDefinitionData
            {
                dependencyDefinitionId = id,
                kind = kind,
                requiredId = signalId
            };
        }

        private static NarrativeArcQuestBindingDefinitionData QuestBinding(string id, NarrativeArcQuestBindingMode mode, string questDefinitionId, string questSourceId = "")
        {
            return new NarrativeArcQuestBindingDefinitionData
            {
                bindingDefinitionId = id,
                mode = mode,
                questDefinitionId = questDefinitionId,
                questSourceId = questSourceId ?? string.Empty,
                required = true
            };
        }

        private static NarrativeActionDefinitionData Action(string id, NarrativeActionCategory category, string target, int order)
        {
            return new NarrativeActionDefinitionData
            {
                actionDefinitionId = id,
                category = category,
                targetId = target,
                requirement = NarrativeActionRequirement.OptionalBestEffort,
                order = order
            };
        }

        private static void Add(ICollection<NarrativeArcDefinition> definitions, ISet<string> existingIds, NarrativeArcDefinitionData data)
        {
            if (data == null || existingIds.Contains(data.arcDefinitionId)) return;
            NarrativeArcDefinition definition = ScriptableObject.CreateInstance<NarrativeArcDefinition>();
            definition.DevelopmentConfigure(data);
            definitions.Add(definition);
            existingIds.Add(data.arcDefinitionId);
        }
    }
}
