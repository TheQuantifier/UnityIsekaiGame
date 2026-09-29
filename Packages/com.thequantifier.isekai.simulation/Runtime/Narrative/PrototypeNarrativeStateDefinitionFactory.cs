using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Narrative
{
    public static class PrototypeNarrativeStateDefinitionFactory
    {
        public const string GuildLoyaltyDefinitionId = "narrative-state-definition.prototype.guild-loyalty";
        public const string MayorInvestigationDefinitionId = "narrative-state-definition.prototype.mayor-investigation";
        public const string RoyalSuccessionDefinitionId = "narrative-state-definition.prototype.royal-succession";

        public const string GuildLoyaltyVariableId = "narrative-variable.prototype.guild-loyalty.path";
        public const string MayorStageVariableId = "narrative-variable.prototype.mayor-investigation.stage";
        public const string RoyalBranchVariableId = "narrative-variable.prototype.royal-succession.branch";
        public const string RoyalSuccessorVariableId = "narrative-variable.prototype.royal-succession.chosen-successor";

        public const string GuildUncommittedValueId = "narrative-state-value.prototype.guild-loyalty.uncommitted";
        public const string GuildLoyalValueId = "narrative-state-value.prototype.guild-loyalty.loyal-to-guild";
        public const string GuildMerchantValueId = "narrative-state-value.prototype.guild-loyalty.supported-merchant";
        public const string GuildMergedValueId = "narrative-state-value.prototype.guild-loyalty.city-service";

        public const string InvestigationUnknownValueId = "narrative-state-value.prototype.mayor-investigation.unknown";
        public const string InvestigationOpenedValueId = "narrative-state-value.prototype.mayor-investigation.opened";
        public const string InvestigationExposedValueId = "narrative-state-value.prototype.mayor-investigation.exposed";

        public const string RoyalUnresolvedValueId = "narrative-state-value.prototype.royal-succession.unresolved";
        public const string RoyalSupportHeirValueId = "narrative-state-value.prototype.royal-succession.support-heir";
        public const string RoyalSupportRivalValueId = "narrative-state-value.prototype.royal-succession.support-rival";
        public const string RoyalReconciledValueId = "narrative-state-value.prototype.royal-succession.reconciled";
        public const string RoyalTerminalValueId = "narrative-state-value.prototype.royal-succession.crowned";

        public const string ChooseGuildTransitionId = "narrative-transition-definition.prototype.guild-loyalty.choose-guild";
        public const string ChooseMerchantTransitionId = "narrative-transition-definition.prototype.guild-loyalty.choose-merchant";
        public const string GuildMergeTransitionId = "narrative-transition-definition.prototype.guild-loyalty.merge-city-service";
        public const string OpenInvestigationTransitionId = "narrative-transition-definition.prototype.mayor-investigation.open";
        public const string ExposeMayorTransitionId = "narrative-transition-definition.prototype.mayor-investigation.expose";
        public const string SupportHeirTransitionId = "narrative-transition-definition.prototype.royal-succession.support-heir";
        public const string SupportRivalTransitionId = "narrative-transition-definition.prototype.royal-succession.support-rival";
        public const string ReconcileSuccessionTransitionId = "narrative-transition-definition.prototype.royal-succession.reconcile";
        public const string CrownHeirTransitionId = "narrative-transition-definition.prototype.royal-succession.crown-heir";
        public const string ChooseSuccessorSubjectTransitionId = "narrative-transition-definition.prototype.royal-succession.choose-successor";

        public static readonly string[] PrototypeDefinitionIds =
        {
            GuildLoyaltyDefinitionId,
            MayorInvestigationDefinitionId,
            RoyalSuccessionDefinitionId
        };

        public static DefinitionRegistry AddMissingPrototypeNarrativeStateDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            foreach (NarrativeStateDefinition definition in CreateMissingNarrativeStateDefinitions(ids)) definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<NarrativeStateDefinition> CreateMissingNarrativeStateDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<NarrativeStateDefinition> definitions = new List<NarrativeStateDefinition>();

            Add(definitions, ids, new NarrativeStateDefinitionData
            {
                stateDefinitionId = GuildLoyaltyDefinitionId,
                displayName = "Prototype Guild Loyalty",
                domainId = "narrative-domain.prototype.guild",
                scope = NarrativeStateScope.Person,
                visibility = NarrativeStateVisibility.ParticipantKnown,
                variables = new[]
                {
                    StateVariable(GuildLoyaltyVariableId, "Guild loyalty path", GuildUncommittedValueId, new[]
                    {
                        Value(GuildUncommittedValueId, "Uncommitted"),
                        Value(GuildLoyalValueId, "Loyal to Adventurers Guild", branch: "narrative-branch.prototype.guild.loyal"),
                        Value(GuildMerchantValueId, "Supported Merchant Guild", branch: "narrative-branch.prototype.guild.merchant"),
                        Value(GuildMergedValueId, "City service merge", branch: "narrative-branch.prototype.guild.city-service", merge: "narrative-merge.prototype.guild.city-service")
                    }, exclusion: "narrative-exclusion.prototype.guild-loyalty")
                },
                transitions = new[]
                {
                    Transition(ChooseGuildTransitionId, GuildLoyaltyVariableId, GuildLoyalValueId, source: new[] { GuildUncommittedValueId }, order: 10, signal: "narrative-signal-definition.prototype.state.guild-loyal"),
                    Transition(ChooseMerchantTransitionId, GuildLoyaltyVariableId, GuildMerchantValueId, source: new[] { GuildUncommittedValueId }, order: 20),
                    Transition(GuildMergeTransitionId, GuildLoyaltyVariableId, GuildMergedValueId, source: new[] { GuildLoyalValueId, GuildMerchantValueId }, order: 30, repeat: NarrativeTransitionRepeatPolicy.Repeatable, reentry: NarrativeTransitionReentryPolicy.Allow)
                },
                tagIds = new[] { "prototype", "guild", "branch" }
            });

            Add(definitions, ids, new NarrativeStateDefinitionData
            {
                stateDefinitionId = MayorInvestigationDefinitionId,
                displayName = "Prototype Mayor Investigation",
                domainId = "narrative-domain.prototype.mayor",
                scope = NarrativeStateScope.World,
                visibility = NarrativeStateVisibility.Hidden,
                variables = new[]
                {
                    StateVariable(MayorStageVariableId, "Mayor investigation stage", InvestigationUnknownValueId, new[]
                    {
                        Value(InvestigationUnknownValueId, "Unknown"),
                        Value(InvestigationOpenedValueId, "Investigation opened"),
                        Value(InvestigationExposedValueId, "Mayor exposed", terminal: true)
                    }, exclusion: "narrative-exclusion.prototype.mayor-investigation", scope: NarrativeStateScope.World, visibility: NarrativeStateVisibility.Hidden)
                },
                transitions = new[]
                {
                    Transition(OpenInvestigationTransitionId, MayorStageVariableId, InvestigationOpenedValueId, source: new[] { InvestigationUnknownValueId }, order: 10, visibility: NarrativeStateVisibility.Hidden),
                    Transition(ExposeMayorTransitionId, MayorStageVariableId, InvestigationExposedValueId, source: new[] { InvestigationOpenedValueId }, order: 20, visibility: NarrativeStateVisibility.Hidden)
                },
                tagIds = new[] { "prototype", "mayor", "hidden" }
            });

            Add(definitions, ids, new NarrativeStateDefinitionData
            {
                stateDefinitionId = RoyalSuccessionDefinitionId,
                displayName = "Prototype Royal Succession",
                domainId = "narrative-domain.prototype.royal-succession",
                scope = NarrativeStateScope.World,
                visibility = NarrativeStateVisibility.Restricted,
                variables = new[]
                {
                    StateVariable(RoyalBranchVariableId, "Royal succession branch", RoyalUnresolvedValueId, new[]
                    {
                        Value(RoyalUnresolvedValueId, "Unresolved"),
                        Value(RoyalSupportHeirValueId, "Support heir", branch: "narrative-branch.prototype.royal.heir"),
                        Value(RoyalSupportRivalValueId, "Support rival", branch: "narrative-branch.prototype.royal.rival"),
                        Value(RoyalReconciledValueId, "Reconciled", merge: "narrative-merge.prototype.royal.reconciled"),
                        Value(RoyalTerminalValueId, "Crowned", terminal: true)
                    }, exclusion: "narrative-exclusion.prototype.royal-succession", scope: NarrativeStateScope.World, visibility: NarrativeStateVisibility.Restricted),
                    new NarrativeVariableDefinitionData
                    {
                        variableDefinitionId = RoyalSuccessorVariableId,
                        displayName = "Chosen successor",
                        kind = NarrativeVariableKind.OptionalStableSubjectReference,
                        scope = NarrativeStateScope.World,
                        visibility = NarrativeStateVisibility.Restricted,
                        defaultValue = NarrativeVariableValueData.OptionalSubject(InformationSubjectType.PersonIdentity, string.Empty)
                    }
                },
                transitions = new[]
                {
                    Transition(SupportHeirTransitionId, RoyalBranchVariableId, RoyalSupportHeirValueId, source: new[] { RoyalUnresolvedValueId }, order: 10),
                    Transition(SupportRivalTransitionId, RoyalBranchVariableId, RoyalSupportRivalValueId, source: new[] { RoyalUnresolvedValueId }, order: 20),
                    Transition(ReconcileSuccessionTransitionId, RoyalBranchVariableId, RoyalReconciledValueId, source: new[] { RoyalSupportHeirValueId, RoyalSupportRivalValueId }, order: 30, repeat: NarrativeTransitionRepeatPolicy.Repeatable, reentry: NarrativeTransitionReentryPolicy.Allow),
                    Transition(CrownHeirTransitionId, RoyalBranchVariableId, RoyalTerminalValueId, source: new[] { RoyalReconciledValueId }, order: 40),
                    new NarrativeStateTransitionDefinitionData
                    {
                        transitionDefinitionId = ChooseSuccessorSubjectTransitionId,
                        displayName = "Choose successor subject",
                        variableDefinitionId = RoyalSuccessorVariableId,
                        targetValue = NarrativeVariableValueData.OptionalSubject(InformationSubjectType.PersonIdentity, "person.prototype.heir"),
                        repeatPolicy = NarrativeTransitionRepeatPolicy.IdempotentSameTarget,
                        reentryPolicy = NarrativeTransitionReentryPolicy.Allow,
                        order = 50
                    }
                },
                tagIds = new[] { "prototype", "royal", "branch" }
            });

            return definitions;
        }

        private static NarrativeVariableDefinitionData StateVariable(string id, string name, string defaultValue, NarrativeStateValueDefinitionData[] values, string exclusion, NarrativeStateScope scope = NarrativeStateScope.Person, NarrativeStateVisibility visibility = NarrativeStateVisibility.ParticipantKnown)
        {
            return new NarrativeVariableDefinitionData
            {
                variableDefinitionId = id,
                displayName = name,
                kind = NarrativeVariableKind.StateToken,
                scope = scope,
                visibility = visibility,
                defaultValue = NarrativeVariableValueData.Token(defaultValue),
                allowedValues = values,
                exclusionGroupId = exclusion
            };
        }

        private static NarrativeStateValueDefinitionData Value(string id, string name, bool terminal = false, string branch = "", string merge = "")
        {
            return new NarrativeStateValueDefinitionData
            {
                valueDefinitionId = id,
                displayName = name,
                terminal = terminal,
                branchDefinitionId = branch,
                mergeGroupId = merge
            };
        }

        private static NarrativeStateTransitionDefinitionData Transition(
            string id,
            string variableId,
            string target,
            IEnumerable<string> source,
            int order,
            string signal = "",
            NarrativeStateVisibility visibility = NarrativeStateVisibility.Public,
            NarrativeTransitionRepeatPolicy repeat = NarrativeTransitionRepeatPolicy.IdempotentSameTarget,
            NarrativeTransitionReentryPolicy reentry = NarrativeTransitionReentryPolicy.RejectAfterTerminal)
        {
            return new NarrativeStateTransitionDefinitionData
            {
                transitionDefinitionId = id,
                variableDefinitionId = variableId,
                targetValue = NarrativeVariableValueData.Token(target),
                allowedSourceValues = (source ?? Array.Empty<string>()).Select(NarrativeVariableValueData.Token).ToArray(),
                repeatPolicy = repeat,
                reentryPolicy = reentry,
                visibility = visibility,
                order = order,
                consequences = string.IsNullOrWhiteSpace(signal)
                    ? Array.Empty<NarrativeActionDefinitionData>()
                    : new[]
                    {
                        new NarrativeActionDefinitionData
                        {
                            actionDefinitionId = $"narrative-action.prototype.{NarrativeModelUtility.SanitizeForId(id)}.signal",
                            category = NarrativeActionCategory.EmitNarrativeSignal,
                            targetId = signal,
                            requirement = NarrativeActionRequirement.OptionalBestEffort,
                            order = 10
                        }
                    }
            };
        }

        private static void Add(ICollection<NarrativeStateDefinition> definitions, ISet<string> existingIds, NarrativeStateDefinitionData data)
        {
            if (data == null || existingIds.Contains(data.stateDefinitionId)) return;
            NarrativeStateDefinition definition = ScriptableObject.CreateInstance<NarrativeStateDefinition>();
            definition.DevelopmentConfigure(data);
            definitions.Add(definition);
            existingIds.Add(data.stateDefinitionId);
        }
    }
}
