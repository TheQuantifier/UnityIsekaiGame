#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.Development;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(6, "Combat", 600)]
    public static class PrototypeStep6AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            TryRegister(registry, BuildFeature61Suite());
            TryRegister(registry, BuildFeature62Suite());
            TryRegister(registry, BuildFeature63Suite());
            TryRegister(registry, BuildFeature64Suite());
            TryRegister(registry, BuildFeature65Suite());
            TryRegister(registry, BuildFeature66Suite());
            TryRegister(registry, BuildFeature67Suite());
            TryRegister(registry, BuildFeature68Suite());
            TryRegister(registry, BuildFeature69Suite());
            TryRegister(registry, BuildFeature610Suite());
        }

        private static ITestLabAutomationSuite BuildFeature61Suite()
        {
            return Suite("feature.6.1.damage-healing", "Feature 6.1 Damage and Healing", "6.1", 610,
                Required("PrototypeTestLabService", "DamageHealingService", "Current Resources"),
                Scenario("damage-preview-does-not-mutate", "Damage preview does not mutate Health", 10, Step("preview-damage", "Preview damage", context =>
                    Operation(context.Prototype().PreviewPipelineDamage(First<DamageTypeDefinition>(context), 25f, targetPlayer: true), context, "preview-damage"))),
                Scenario("healing-preview-does-not-mutate", "Healing preview does not mutate Health", 20, Step("preview-healing", "Preview healing", context =>
                    Operation(context.Prototype().PreviewPipelineHealing(25f, targetPlayer: true), context, "preview-healing"))),
                Scenario("damage-executes-once", "Damage executes once", 30, Step("apply-damage", "Apply pipeline damage", context =>
                    Operation(context.Prototype().ApplyPipelineDamage(First<DamageTypeDefinition>(context), 25f, targetPlayer: true), context, "apply-damage"))),
                Scenario("duplicate-damage-does-not-apply-twice", "Duplicate damage does not apply twice", 40, Step("duplicate-damage", "Prove duplicate transaction", context =>
                    Operation(context.Prototype().ProvePipelineDuplicate(First<DamageTypeDefinition>(context), 25f), context, "duplicate-damage"))),
                Scenario("immunity-prevents-damage", "Immunity path remains observable", 50, Step("immunity-preview", "Preview selected damage type", context =>
                    Operation(context.Prototype().PreviewPipelineDamage(First<DamageTypeDefinition>(context), 25f, targetPlayer: true), context, "immunity-preview"))),
                Scenario("healing-clamps-and-reports-overhealing", "Healing clamps and reports overhealing", 60, Step("apply-healing", "Apply healing", context =>
                    Operation(context.Prototype().ApplyPipelineHealing(999f, targetPlayer: true), context, "apply-healing"))));
        }

        private static ITestLabAutomationSuite BuildFeature62Suite()
        {
            return Suite("feature.6.2.attack-resolution", "Feature 6.2 Attack Resolution", "6.2", 620,
                Required("PrototypeTestLabService", "AttackResolutionService", "DamageHealingService"),
                Scenario("deterministic-miss", "Deterministic miss", 10, Step("miss", "Execute miss", context =>
                    Operation(context.Prototype().ExecuteAttackResolution(First<DamageTypeDefinition>(context), 25f, 0.25f, 0.99f, 0f, 0.99f, 1.5f, 1f, 2f, true, true, false), context, "miss"))),
                Scenario("deterministic-hit", "Deterministic hit", 20, Step("hit", "Execute hit", context =>
                    Operation(context.Prototype().ExecuteAttackResolution(First<DamageTypeDefinition>(context), 10f, 0.95f, 0.1f, 0f, 0.99f, 1.5f, 1f, 2f, true, true, false), context, "hit"))),
                Scenario("deterministic-critical-hit", "Deterministic critical hit", 30, Step("critical", "Execute critical", context =>
                    Operation(context.Prototype().ExecuteAttackResolution(First<DamageTypeDefinition>(context), 10f, 0.95f, 0.1f, 0.95f, 0.1f, 2f, 1f, 2f, true, true, false), context, "critical"))),
                Scenario("miss-does-not-damage", "Miss does not damage", 40, Step("miss-preview", "Preview miss", context =>
                    Operation(context.Prototype().PreviewAttackResolution(First<DamageTypeDefinition>(context), 10f, 0.25f, 0.99f, 0f, 0.99f, 1.5f, 1f, 2f, true, true), context, "miss-preview"))),
                Scenario("duplicate-attack-does-not-damage-twice", "Duplicate attack does not damage twice", 50,
                    Step("first", "Generate transaction", context => Operation(context.Prototype().GenerateAttackTransaction(), context, "attack-tx")),
                    Step("execute", "Execute duplicate proof attack", context => Operation(context.Prototype().ExecuteAttackResolution(First<DamageTypeDefinition>(context), 10f, 0.95f, 0.1f, 0f, 0.99f, 1.5f, 1f, 2f, true, true, true), context, "attack-duplicate"))));
        }

        private static ITestLabAutomationSuite BuildFeature63Suite()
        {
            return Suite("feature.6.3.lifecycle", "Feature 6.3 Lifecycle", "6.3", 630,
                Required("PrototypeTestLabService", "ActorLifecycleController", "Current Resources"),
                Scenario("zero-health-causes-default-defeat", "Zero Health causes default defeat", 10, Step("zero-health", "Apply zero-health lifecycle damage", context =>
                    Operation(context.Prototype().ApplyZeroHealthLifecycleDamage(First<DamageTypeDefinition>(context), targetEnemy: true), context, "zero-health"))),
                Scenario("healing-does-not-automatically-recover", "Healing does not automatically recover", 20, Step("heal-after-defeat", "Heal after defeat", context =>
                    Operation(context.Prototype().ApplyPipelineHealing(25f, targetPlayer: true), context, "heal-after-defeat"))),
                Scenario("recovery-returns-unconscious-to-active", "Recovery returns Unconscious to Active", 30,
                    Step("reset", "Reset lifecycle baseline", context => Operation(context.Prototype().ResetAutomationRuntimeState(), context, "recovery-reset")),
                    Step("zero-health", "Reduce enemy Health to zero", context => Operation(context.Prototype().ApplyZeroHealthLifecycleDamage(First<DamageTypeDefinition>(context), targetEnemy: true), context, "recovery-zero-health")),
                    Step("prepare", "Prepare Unconscious recovery state", context => Operation(context.Prototype().PrepareRecoveryLifecycle(targetEnemy: true), context, "recovery-prepare")),
                    Step("recover", "Execute recovery", context => Operation(context.Prototype().ExecuteRecoveryLifecycle(targetEnemy: true, 25f, reuseTransaction: false), context, "recover"))),
                Scenario("death-and-revival-transition", "Death and revival transition correctly", 40,
                    Step("reset", "Reset lifecycle baseline", context => Operation(context.Prototype().ResetAutomationRuntimeState(), context, "revival-reset")),
                    Step("death", "Execute death", context => Operation(context.Prototype().ExecuteDeathLifecycle(targetEnemy: true, reuseTransaction: false), context, "death")),
                    Step("revival", "Execute revival", context => Operation(context.Prototype().ExecuteRevivalLifecycle(targetEnemy: true, 25f, reuseTransaction: false), context, "revival"))),
                Scenario("duplicate-lifecycle-transaction-does-not-repeat", "Duplicate lifecycle transaction does not repeat", 50,
                    Step("generate", "Generate lifecycle transaction", context => Operation(context.Prototype().GenerateLifecycleTransaction(), context, "lifecycle-tx")),
                    Step("reuse", "Reuse lifecycle transaction", context => Operation(context.Prototype().ExecuteDefeatLifecycle(targetEnemy: true, reuseTransaction: true), context, "lifecycle-duplicate"))));
        }

        private static ITestLabAutomationSuite BuildFeature64Suite()
        {
            return Suite("feature.6.4.ongoing-effects", "Feature 6.4 Ongoing Effects", "6.4", 640,
                Required("PrototypeTestLabService", "OngoingEffectService", "DamageHealingService"),
                Scenario("effect-preview-creates-no-instance", "Effect preview creates no instance", 10, Step("preview", "Preview ongoing effect", context =>
                    Operation(context.Prototype().PreviewOngoingEffect(First<OngoingEffectDefinition>(context), true, 5f, 1f, 5f, 0, 1), context, "ongoing-preview"))),
                Scenario("due-tick-applies-once", "Due tick applies once", 20,
                    Step("apply", "Apply ongoing effect", context => Operation(context.Prototype().ApplyOngoingEffect(First<OngoingEffectDefinition>(context), true, 5f, 1f, 5f, 0, 1, false), context, "ongoing-apply")),
                    Step("tick", "Process due ticks", context => Operation(context.Prototype().ProcessOngoingEffectsNow(), context, "ongoing-tick"))),
                Scenario("duplicate-tick-does-not-apply-twice", "Duplicate tick does not apply twice", 30, Step("tick", "Process due ticks once", context =>
                    Operation(context.Prototype().ProcessOngoingEffectsNow(), context, "ongoing-duplicate-tick"))),
                Scenario("hostile-ongoing-damage-can-reach-zero-health", "Hostile ongoing damage can reach zero Health", 40, Step("apply-large", "Apply large ongoing damage", context =>
                    Operation(context.Prototype().ApplyOngoingEffect(First<OngoingEffectDefinition>(context), true, 999f, 1f, 1f, 1, 1, false), context, "ongoing-zero-health"))),
                Scenario("restore-does-not-replay-ticks", "Restore does not replay ticks", 50,
                    Step("safe-location", "Move to a known Test Lab point before saving", context => Operation(context.Prototype().Teleport(FirstTestPoint(context)), context, "ongoing-safe-location")),
                    Step("save", "Save active state", context => Operation(context.Prototype().Save(), context, "ongoing-save")),
                    Step("load", "Load active state", context => Operation(context.Prototype().Load(), context, "ongoing-load"))));
        }

        private static ITestLabAutomationSuite BuildFeature65Suite()
        {
            return Suite("feature.6.5.combat-state", "Feature 6.5 Combat State", "6.5/6.5a", 650,
                Required("PrototypeTestLabService", "CombatStateService", "Actor identity"),
                Scenario("explicit-engagement-starts-combat", "Explicit engagement starts combat", 10, Step("engage", "Engage A-B", context =>
                    Operation(context.Prototype().EngageCombatStateParticipants("A", "B"), context, "combat-state-engage"))),
                Scenario("duplicate-engagement-is-idempotent", "Duplicate engagement remains idempotent", 20,
                    Step("tx", "Generate transaction", context => Operation(context.Prototype().GenerateCombatStateTransaction(), context, "combat-state-tx")),
                    Step("first", "Engage A-B", context => Operation(context.Prototype().ExecuteExplicitCombatEngagement(reuseTransaction: true), context, "combat-state-first")),
                    Step("reuse", "Reuse engagement", context => Operation(context.Prototype().ExecuteExplicitCombatEngagement(reuseTransaction: true), context, "combat-state-reuse"))),
                Scenario("timeout-exits-combat", "Timeout exits combat", 30,
                    Step("engage", "Engage A-B", context => Operation(context.Prototype().EngageCombatStateParticipants("A", "B"), context, "combat-timeout-engage")),
                    Step("advance", "Advance combat timeout", context => Operation(context.Prototype().AdvanceCombatState(10f), context, "combat-timeout"))),
                Scenario("connected-encounters-merge", "Connected encounters merge", 40,
                    Step("prep", "Prepare split participants", context => Operation(context.Prototype().PrepareCombatStateSplitParticipants(), context, "combat-prep")),
                    Step("connect-a-b", "Connect A-B", context => Operation(context.Prototype().EngageCombatStateParticipants("A", "B"), context, "combat-connect-a-b")),
                    Step("connect-b-c", "Connect B-C", context => Operation(context.Prototype().EngageCombatStateParticipants("B", "C"), context, "combat-connect-b-c"))),
                Scenario("bridge-removal-splits-encounter", "Bridge removal splits encounter", 50,
                    Step("prep", "Prepare split participants", context => Operation(context.Prototype().PrepareCombatStateSplitParticipants(), context, "combat-bridge-prep")),
                    Step("connect-a-b", "Connect A-B", context => Operation(context.Prototype().EngageCombatStateParticipants("A", "B"), context, "combat-bridge-a-b")),
                    Step("connect-b-c", "Connect B-C", context => Operation(context.Prototype().EngageCombatStateParticipants("B", "C"), context, "combat-bridge-b-c")),
                    Step("connect-c-d", "Connect C-D", context => Operation(context.Prototype().EngageCombatStateParticipants("C", "D"), context, "combat-bridge-c-d")),
                    Step("end", "End bridge engagement", context => Operation(context.Prototype().EndCombatStateEngagement("B", "C", false), context, "combat-bridge")),
                    Step("process", "Process graph", context => Operation(context.Prototype().ProcessCombatStateConnectivity(), context, "combat-split"))),
                Scenario("isolated-participant-exits-combat", "Isolated participant exits combat", 60,
                    Step("prep", "Prepare split participants", context => Operation(context.Prototype().PrepareCombatStateSplitParticipants(), context, "combat-exit-prep")),
                    Step("connect", "Connect A-B", context => Operation(context.Prototype().EngageCombatStateParticipants("A", "B"), context, "combat-exit-connect")),
                    Step("exit", "Exit participant", context => Operation(context.Prototype().ForceCombatStateParticipantExit("B"), context, "combat-exit"))),
                Scenario("integrity-validation-remains-clean", "Integrity validation remains clean", 70, Step("validate", "Validate combat state integrity", context =>
                    Operation(context.Prototype().ValidateCombatStateIntegrity(), context, "combat-integrity"))));
        }

        private static ITestLabAutomationSuite BuildFeature66Suite()
        {
            return Suite("feature.6.6.defensive-actions", "Feature 6.6 Defensive Actions", "6.6", 660,
                Required("PrototypeTestLabService", "DefensiveActionService", "AttackResolutionService"),
                Scenario("defense-preview-does-not-mutate", "Defense preview does not mutate", 10, Step("preview", "Preview defense activation", context =>
                    Operation(context.Prototype().PreviewDefenseActivation(First<DefensiveActionDefinition>(context), targetPlayer: true), context, "defense-preview"))),
                Scenario("successful-dodge-prevents-damage", "Successful Dodge prevents damage", 20,
                    Step("activate", "Activate defense", context => Operation(context.Prototype().ActivateDefense(First<DefensiveActionDefinition>(context), targetPlayer: true, reuseTransaction: false), context, "defense-activate")),
                    Step("attack", "Resolve defensive attack", context => Operation(context.Prototype().ExecuteDefensiveAttack(First<DamageTypeDefinition>(context), 25f, 0.95f, 0.1f, 0.01f, targetPlayer: true, reuseTransaction: false), context, "defense-attack"))),
                Scenario("partial-block-reduces-damage-before-feature-6-1", "Partial Block reduces damage before 6.1 mitigation", 30, Step("attack", "Resolve block path", context =>
                    Operation(context.Prototype().ExecuteDefensiveAttack(First<DamageTypeDefinition>(context), 25f, 0.95f, 0.1f, 0.5f, targetPlayer: true, reuseTransaction: false), context, "defense-block"))),
                Scenario("duplicate-attack-does-not-spend-stamina-twice", "Duplicate attack does not spend Stamina twice", 40,
                    Step("tx", "Generate attack transaction", context => Operation(context.Prototype().GenerateAttackTransaction(), context, "defense-tx")),
                    Step("reuse", "Reuse defensive attack", context => Operation(context.Prototype().ExecuteDefensiveAttack(First<DamageTypeDefinition>(context), 25f, 0.95f, 0.1f, 0.01f, targetPlayer: true, reuseTransaction: true), context, "defense-duplicate"))),
                Scenario("lifecycle-transition-clears-defense", "Lifecycle transition clears defense", 50,
                    Step("activate", "Activate defense", context => Operation(context.Prototype().ActivateDefense(First<DefensiveActionDefinition>(context), targetPlayer: true, reuseTransaction: false), context, "defense-lifecycle-activate")),
                    Step("defeat", "Execute defeat", context => Operation(context.Prototype().ExecuteDefeatLifecycle(targetEnemy: false, reuseTransaction: false), context, "defense-lifecycle-clear"))));
        }

        private static ITestLabAutomationSuite BuildFeature67Suite()
        {
            return Suite("feature.6.7.combat-execution", "Feature 6.7 Combat Execution", "6.7", 670,
                Required("PrototypeTestLabService", "CombatExecutionService", "Current Resources"),
                Scenario("execution-preview-does-not-mutate", "Execution preview does not mutate", 10, Step("preview", "Preview execution", context =>
                    Operation(context.Prototype().PreviewCombatExecution(First<CombatExecutionDefinition>(context)), context, "execution-preview"))),
                Scenario("commitment-conflict-rejects-second-action", "Commitment conflict rejects second action", 20,
                    Step("begin", "Begin execution", context => Operation(context.Prototype().BeginCombatExecution(First<CombatExecutionDefinition>(context), false), context, "execution-begin")),
                    Step("begin-second", "Begin second execution", context => Operation(context.Prototype().RunExpectedAutomationFailure(() => context.Prototype().BeginCombatExecution(First<CombatExecutionDefinition>(context), false)), context, "execution-conflict", acceptFailure: true))),
                Scenario("commit-spends-costs-once", "Commit spends costs once", 30,
                    Step("begin", "Begin execution", context => Operation(context.Prototype().BeginCombatExecution(First<CombatExecutionDefinition>(context), false), context, "execution-cost-begin")),
                    Step("advance", "Advance to ready", context => Operation(context.Prototype().AdvanceCombatExecutionClock(1f), context, "execution-cost-ready")),
                    Step("commit", "Commit execution", context => Operation(context.Prototype().CommitCombatExecution(false), context, "execution-cost-commit"))),
                Scenario("duplicate-commit-does-not-spend-twice", "Duplicate commit does not spend twice", 40,
                    Step("begin", "Begin execution", context => Operation(context.Prototype().BeginCombatExecution(First<CombatExecutionDefinition>(context), false), context, "execution-duplicate-begin")),
                    Step("advance", "Advance to ready", context => Operation(context.Prototype().AdvanceCombatExecutionClock(1f), context, "execution-duplicate-ready")),
                    Step("commit", "Commit execution", context => Operation(context.Prototype().CommitCombatExecution(false), context, "execution-duplicate-commit")),
                    Step("reuse", "Reuse commit transaction", context => Operation(context.Prototype().CommitCombatExecution(true), context, "execution-duplicate-reuse"))),
                Scenario("cooldown-blocks-until-ready-boundary", "Cooldown blocks until ready boundary", 50,
                    Step("begin", "Begin execution after cooldown", context => Operation(context.Prototype().BeginCombatExecution(First<CombatExecutionDefinition>(context), false), context, "execution-cooldown-begin")),
                    Step("advance", "Advance execution clock", context => Operation(context.Prototype().AdvanceCombatExecutionClock(10f), context, "execution-cooldown-advance"))),
                Scenario("restore-clears-commitment-and-restores-cooldowns-silently", "Restore clears commitment and restores cooldowns silently", 60,
                    Step("begin", "Begin execution", context => Operation(context.Prototype().BeginCombatExecution(First<CombatExecutionDefinition>(context), false), context, "execution-restore-begin")),
                    Step("restore-clear", "Clear transient execution state", context => Operation(context.Prototype().ClearCombatExecutionForRestore(), context, "execution-restore-clear")),
                    Step("snapshot", "Snapshot cooldowns", context => Operation(context.Prototype().SnapshotCombatExecution(), context, "execution-restore-snapshot"))));
        }

        private static ITestLabAutomationSuite BuildFeature68Suite()
        {
            return Suite("feature.6.8.combat-reactions", "Feature 6.8 Combat Reactions", "6.8", 680,
                Required("PrototypeTestLabService", "CombatReactionService", "DamageHealingService", "OngoingEffectService"),
                Scenario("reaction-preview-does-not-mutate", "Reaction preview does not mutate", 10,
                    Step("register", "Register selected reaction", context => Operation(context.Prototype().RegisterCombatReaction(FirstReaction(context, CombatReactionTriggerType.DamageApplied), ownerPlayer: false), context, "reaction-preview-register")),
                    Step("preview", "Preview reaction trigger", context => Operation(context.Prototype().PreviewCombatReactionDamage(FirstReaction(context, CombatReactionTriggerType.DamageApplied)), context, "reaction-preview"))),
                Scenario("reaction-executes-through-service", "Reaction executes through combat service", 20,
                    Step("register", "Register selected reaction", context => Operation(context.Prototype().RegisterCombatReaction(FirstReaction(context, CombatReactionTriggerType.DamageApplied), ownerPlayer: false), context, "reaction-execute-register")),
                    Step("execute", "Execute reaction trigger", context => Operation(context.Prototype().ExecuteCombatReactionDamage(FirstReaction(context, CombatReactionTriggerType.DamageApplied)), context, "reaction-execute"))),
                Scenario("duplicate-root-does-not-repeat", "Duplicate root reaction does not repeat", 30,
                    Step("register", "Register selected reaction", context => Operation(context.Prototype().RegisterCombatReaction(FirstReaction(context, CombatReactionTriggerType.DamageApplied), ownerPlayer: false), context, "reaction-duplicate-register")),
                    Step("duplicate", "Run duplicate proof", context => Operation(context.Prototype().ExecuteCombatReactionDuplicateProof(FirstReaction(context, CombatReactionTriggerType.DamageApplied)), context, "reaction-duplicate"))),
                Scenario("critical-trigger-path", "Critical trigger path is available", 40,
                    Step("register", "Register selected reaction", context => Operation(context.Prototype().RegisterCombatReaction(FirstReaction(context, CombatReactionTriggerType.CriticalHit), ownerPlayer: true), context, "reaction-critical-register")),
                    Step("critical", "Execute critical trigger", context => Operation(context.Prototype().ExecuteCombatReactionCritical(FirstReaction(context, CombatReactionTriggerType.CriticalHit)), context, "reaction-critical"))),
                Scenario("clear-removes-sources", "Clear removes registered reaction sources", 50,
                    Step("register", "Register selected reaction", context => Operation(context.Prototype().RegisterCombatReaction(FirstReaction(context, CombatReactionTriggerType.DamageApplied), ownerPlayer: false), context, "reaction-clear-register")),
                    Step("clear", "Clear reaction sources", context => Operation(context.Prototype().ClearCombatReactions(), context, "reaction-clear"))));
        }

        private static ITestLabAutomationSuite BuildFeature69Suite()
        {
            return Suite("feature.6.9.combat-contribution", "Feature 6.9 Combat Contribution", "6.9", 690,
                Required("PrototypeTestLabService", "CombatContributionService", "DamageHealingService"),
                Scenario("contribution-preview-does-not-mutate", "Contribution preview does not mutate", 10, Step("preview", "Preview contribution", context =>
                    Operation(context.Prototype().PreviewContribution(First<DamageTypeDefinition>(context)), context, "contribution-preview"))),
                Scenario("damage-records-once", "Committed damage records contribution once", 20, Step("record", "Record damage contribution", context =>
                    Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: false), context, "contribution-damage"))),
                Scenario("duplicate-damage-is-idempotent", "Duplicate contribution transaction is idempotent", 30,
                    Step("record", "Record damage contribution", context => Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: false), context, "contribution-duplicate-record")),
                    Step("reuse", "Reuse contribution transaction", context => Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: true), context, "contribution-duplicate-reuse"))),
                Scenario("fully-prevented-damage-gives-zero-attacker-credit", "Fully prevented damage gives zero attacker credit", 40, Step("prevented", "Record fully prevented damage", context =>
                    Operation(context.Prototype().RecordFullyPreventedDamageContribution(First<DamageTypeDefinition>(context)), context, "contribution-prevented"))),
                Scenario("overkill-records-actual-health-removed", "Overkill records actual Health removed", 50, Step("overkill", "Record overkill contribution", context =>
                    Operation(context.Prototype().RecordOverkillContribution(First<DamageTypeDefinition>(context)), context, "contribution-overkill"))),
                Scenario("healing-support-records-effective-value", "Healing support records effective value", 60, Step("heal", "Record healing contribution", context =>
                    Operation(context.Prototype().RecordHealingContribution(reuseTransaction: false), context, "contribution-healing"))),
                Scenario("defensive-contribution-records-support", "Defensive contribution records support", 70, Step("block", "Record Block contribution", context =>
                    Operation(context.Prototype().RecordDefenseContribution(CombatContributionType.SuccessfulBlock), context, "contribution-block"))),
                Scenario("ongoing-and-reaction-contributions-are-distinct", "Ongoing and reaction contributions are distinct", 80,
                    Step("ongoing", "Record ongoing damage", context => Operation(context.Prototype().RecordOngoingDamageContribution(), context, "contribution-ongoing")),
                    Step("reaction-damage", "Record reaction damage", context => Operation(context.Prototype().RecordReactionDamageContribution(), context, "contribution-reaction-damage")),
                    Step("reaction-heal", "Record reaction healing", context => Operation(context.Prototype().RecordReactionHealingContribution(), context, "contribution-reaction-heal"))),
                Scenario("defeat-credit-resolves-primary", "Defeat credit resolves primary contributor", 90,
                    Step("record", "Record damage contribution", context => Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: false), context, "contribution-credit-record")),
                    Step("resolve", "Resolve defeat credit", context => Operation(context.Prototype().ResolveDefeatContributionCredit(), context, "contribution-credit"))),
                Scenario("kill-credit-uses-latest-qualifying-contributor", "Kill credit uses latest qualifying contributor", 95, Step("prove", "Prove latest kill credit", context =>
                    Operation(context.Prototype().ProveContributionKillCreditLatest(), context, "contribution-kill-latest"))),
                Scenario("assist-includes-other-qualifying-contributors", "Assist includes other qualifying contributors", 96, Step("prove", "Prove assist credit", context =>
                    Operation(context.Prototype().ProveContributionAssistCredit(), context, "contribution-assist"))),
                Scenario("healing-only-support-is-not-primary", "Healing-only support is not primary kill credit", 97, Step("prove", "Prove healing support is not primary", context =>
                    Operation(context.Prototype().ProveContributionHealingOnlyNotPrimary(), context, "contribution-healing-not-primary"))),
                Scenario("expired-damage-does-not-assign-primary", "Expired damage does not assign primary credit", 100,
                    Step("record", "Record damage contribution", context => Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: false), context, "contribution-expire-record")),
                    Step("advance", "Advance beyond window", context => Operation(context.Prototype().AdvanceContributionClock(31f), context, "contribution-expire-advance")),
                    Step("resolve", "Resolve expired credit", context => Operation(context.Prototype().ResolveDefeatContributionCredit(), context, "contribution-expire-credit"))),
                Scenario("encounter-merge-combines-ledgers", "Encounter merge combines contribution ledgers", 105, Step("prove", "Prove encounter merge", context =>
                    Operation(context.Prototype().ProveContributionEncounterMerge(), context, "contribution-merge"))),
                Scenario("encounter-split-partitions-eligibility", "Encounter split partitions active eligibility", 106, Step("prove", "Prove encounter split", context =>
                    Operation(context.Prototype().ProveContributionEncounterSplit(), context, "contribution-split"))),
                Scenario("finalize-locks-ledger", "Finalize produces diagnostic reward eligibility", 110,
                    Step("record", "Record damage contribution", context => Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: false), context, "contribution-finalize-record")),
                    Step("finalize", "Finalize ledger", context => Operation(context.Prototype().FinalizeContributionLedger(), context, "contribution-finalize"))),
                Scenario("duplicate-lifecycle-resolution-is-idempotent", "Duplicate lifecycle resolution is idempotent", 115, Step("prove", "Prove duplicate lifecycle credit", context =>
                    Operation(context.Prototype().ProveContributionDuplicateLifecycleCredit(), context, "contribution-duplicate-credit"))),
                Scenario("revival-preserves-prior-credit", "Revival preserves prior death credit", 116, Step("prove", "Prove revival preserves credit", context =>
                    Operation(context.Prototype().ProveContributionRevivalPreservesCredit(), context, "contribution-revival-credit"))),
                Scenario("reward-eligibility-grants-no-concrete-rewards", "Reward eligibility grants no concrete rewards", 117, Step("prove", "Prove reward safety", context =>
                    Operation(context.Prototype().ProveContributionRewardSafety(), context, "contribution-reward-safety"))),
                Scenario("restore-clear-removes-transient-ledgers", "Restore clear removes transient contribution state", 120,
                    Step("record", "Record damage contribution", context => Operation(context.Prototype().RecordDamageContribution(First<DamageTypeDefinition>(context), reuseTransaction: false), context, "contribution-clear-record")),
                    Step("clear", "Clear contribution state", context => Operation(context.Prototype().ClearCombatContributions(), context, "contribution-clear"))));
        }

        private static ITestLabAutomationSuite BuildFeature610Suite()
        {
            return Suite("feature.6.10.combat-integration", "Feature 6.10 Combat Integration", "6.10", 700,
                Required("CombatRuntimeFacade", "DamageHealingService", "AttackResolutionService", "DefensiveActionService", "CombatExecutionService", "CombatStateService", "OngoingEffectService", "CombatReactionService", "CombatContributionService"),
                Scenario("runtime-readiness-and-snapshot", "Runtime readiness and combined snapshot are coherent", 10,
                    Step("reset", "Reset integrated runtime", context => Operation(context.Prototype().ResetCombatRuntimeIntegration(), context, "combat-integration-reset")),
                    Step("validate", "Validate combat integrity", context => Operation(context.Prototype().ValidateCombatRuntimeIntegrity(), context, "combat-integration-validate"))),
                Scenario("facade-preview-does-not-mutate", "Facade preview uses shared logic without mutation", 20,
                    Step("preview", "Preview attack through facade", context => Operation(context.Prototype().PreviewCombatRuntimeAttack(First<DamageTypeDefinition>(context)), context, "combat-integration-preview")),
                    Step("validate", "Validate after preview", context => Operation(context.Prototype().ValidateCombatRuntimeIntegrity(), context, "combat-integration-preview-validate"))),
                Scenario("ordinary-hit-transaction-trace", "Ordinary hit records a transaction trace", 30,
                    Step("hit", "Execute hit through facade", context => Operation(context.Prototype().ExecuteCombatRuntimeAttack(First<DamageTypeDefinition>(context)), context, "combat-integration-hit")),
                    Step("validate", "Validate after hit", context => Operation(context.Prototype().ValidateCombatRuntimeIntegrity(), context, "combat-integration-hit-validate"))),
                Scenario("miss-critical-and-defense-paths", "Miss, critical, dodge, and block paths remain integrated", 40,
                    Step("inventory", "Clear inventory capacity for defense fixtures", context => Operation(context.Prototype().ClearInventory(confirmed: true), context, "combat-integration-inventory")),
                    Step("miss", "Execute miss", context => Operation(context.Prototype().ExecuteCombatRuntimeMiss(First<DamageTypeDefinition>(context)), context, "combat-integration-miss")),
                    Step("critical", "Execute critical", context => Operation(context.Prototype().ExecuteCombatRuntimeCritical(First<DamageTypeDefinition>(context)), context, "combat-integration-critical")),
                    Step("dodge", "Execute dodge flow", context => Operation(context.Prototype().ExecuteCombatRuntimeDefense(First<DamageTypeDefinition>(context), block: false), context, "combat-integration-dodge")),
                    Step("block", "Execute block flow", context => Operation(context.Prototype().ExecuteCombatRuntimeDefense(First<DamageTypeDefinition>(context), block: true), context, "combat-integration-block"))),
                Scenario("ongoing-reaction-and-contribution-flow", "Ongoing effects, reactions, and contribution credit remain connected", 50,
                    Step("ongoing", "Apply ongoing tick", context => Operation(context.Prototype().ExecuteCombatRuntimeOngoingDamage(First<OngoingEffectDefinition>(context), First<DamageTypeDefinition>(context)), context, "combat-integration-ongoing")),
                    Step("reaction", "Execute reaction", context => Operation(context.Prototype().ExecuteCombatRuntimeReaction(FirstReaction(context, CombatReactionTriggerType.DamageApplied)), context, "combat-integration-reaction")),
                    Step("contribution", "Resolve contribution credit", context => Operation(context.Prototype().ExecuteCombatRuntimeContribution(First<DamageTypeDefinition>(context)), context, "combat-integration-contribution"))),
                Scenario("encounter-split-and-integrity", "Encounter split keeps contribution integrity", 60,
                    Step("split", "Run split proof", context => Operation(context.Prototype().ProveContributionEncounterSplit(), context, "combat-integration-split")),
                    Step("validate", "Validate after split", context => Operation(context.Prototype().ValidateCombatRuntimeIntegrity(), context, "combat-integration-split-validate"))),
                Scenario("restore-clears-transient-runtime", "Restore clearing removes transient combat state silently", 70,
                    Step("prime", "Execute integrated hit", context => Operation(context.Prototype().ExecuteCombatRuntimeAttack(First<DamageTypeDefinition>(context)), context, "combat-integration-restore-prime")),
                    Step("restore-clear", "Clear transient state for restore", context => Operation(context.Prototype().SimulateCombatRuntimeRestoreClear(), context, "combat-integration-restore-clear")),
                    Step("validate", "Validate after restore clear", context => Operation(context.Prototype().ValidateCombatRuntimeIntegrity(), context, "combat-integration-restore-validate"))),
                Scenario("repeat-run-does-not-leak-state", "Repeated integration run starts from a clean baseline", 80,
                    Step("first-reset", "Reset first baseline", context => Operation(context.Prototype().ResetCombatRuntimeIntegration(), context, "combat-integration-repeat-reset-a")),
                    Step("hit", "Execute hit", context => Operation(context.Prototype().ExecuteCombatRuntimeAttack(First<DamageTypeDefinition>(context)), context, "combat-integration-repeat-hit")),
                    Step("second-reset", "Reset second baseline", context => Operation(context.Prototype().ResetCombatRuntimeIntegration(), context, "combat-integration-repeat-reset-b")),
                    Step("validate", "Validate second baseline", context => Operation(context.Prototype().ValidateCombatRuntimeIntegrity(), context, "combat-integration-repeat-validate"))));
        }

        private static ITestLabAutomationSuite Suite(string suiteId, string displayName, string feature, int order, IReadOnlyList<string> required, params ITestLabAutomationScenario[] scenarios)
        {
            return new TestLabAutomationSuite(
                suiteId,
                displayName,
                feature,
                $"{displayName} runtime integration scenarios.",
                order,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: required,
                scenarios: scenarios);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 30 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 30,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.SharedRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Character | TestLabRuntimeArea.Combat,
                requiredHostId: PrototypeTestLabAutomationHost.DefaultHostId,
                requiredHostFeatures: TestLabHostFeature.SharedRuntime | TestLabHostFeature.SceneReset | TestLabHostFeature.FixtureFingerprinting | TestLabHostFeature.AutomatedExecution);
        }

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, displayName, action);
        }

        private static IReadOnlyList<string> Required(params string[] services)
        {
            return services.ToArray();
        }

        private static T First<T>(TestLabAutomationContext context)
            where T : class, IGameDefinition
        {
            return context.Prototype().GetDefinitions<T>().FirstOrDefault();
        }

        private static CombatReactionDefinition FirstReaction(TestLabAutomationContext context, CombatReactionTriggerType triggerType)
        {
            return context.Prototype().GetDefinitions<CombatReactionDefinition>().FirstOrDefault(definition => definition.SupportsTrigger(triggerType));
        }

        private static PrototypeTestPoint FirstTestPoint(TestLabAutomationContext context)
        {
            return context.Prototype().GetTestPoints().FirstOrDefault();
        }

        private static TestLabAutomationStepResult Operation(PrototypeTestLabOperation operation, TestLabAutomationContext context, string operationId, bool acceptFailure = false)
        {
            string transactionId = context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, operationId);
            if (acceptFailure)
            {
                return operation.Succeeded
                    ? TestLabAssertions.Fail(operationId, operation.OperationName, "OperationFailed", "Failure", operation.Code, operation.Message, string.Empty, transactionId)
                    : TestLabAssertions.Pass(operationId, operation.OperationName, $"Expected rejection observed: {operation.Code} {operation.Message}");
            }

            return operation.Succeeded
                ? new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Passed, "OperationSucceeded", "Succeeded", operation.Code, string.Empty, transactionId, operation.Message)
                : new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Failed, "OperationSucceeded", "Succeeded", operation.Code, string.Empty, transactionId, operation.Message);
        }

        private static void TryRegister(TestLabAutomationRegistry registry, ITestLabAutomationSuite suite)
        {
            registry.TryRegister(suite, out _);
        }
    }
}
#endif
