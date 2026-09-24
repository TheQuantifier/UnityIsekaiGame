using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class DefensiveActionsReactiveCombatTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCatalog_ResolvesDefensiveActionsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            DefinitionRegistry registry = catalog.CreateRegistry();

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());
            AssertDefense(registry, "defense-action.basic-guard", DefensiveActionType.Guard);
            AssertDefense(registry, "defense-action.shield-block", DefensiveActionType.Block);
            AssertDefense(registry, "defense-action.weapon-parry", DefensiveActionType.Parry);
            AssertDefense(registry, "defense-action.basic-dodge", DefensiveActionType.Dodge);
            Assert.That(registry.TryGet("item.prototype-shield", out ItemDefinition shield), Is.True);
            Assert.That(shield.Tags, Has.Some.Matches<TagDefinition>(tag => tag != null && tag.Id == "tag.general.shield-compatible"));
        }

        [Test]
        public void PreviewActivationDoesNotSpendStaminaOrCreateStateOrEmitEvents()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();
            int activated = 0;
            service.DefenseActivated += _ => activated++;
            float staminaBefore = fixture.GetTargetStamina();

            DefenseActivationResult result = service.PreviewActivate(fixture.CreateActivationRequest("defense.preview.activate", "defense-action.basic-dodge"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(activated, Is.Zero);
            Assert.That(fixture.GetTargetStamina(), Is.EqualTo(staminaBefore).Within(0.001f));
            Assert.That(service.TryGetActiveDefense(fixture.TargetActorId, out _), Is.False);
        }

        [Test]
        public void ActivationSpendsStaminaOnceAndDuplicateDoesNotSpendAgain()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();
            float staminaBefore = fixture.GetTargetStamina();
            DefenseActivationRequest request = fixture.CreateActivationRequest("defense.activate.duplicate", "defense-action.basic-guard");

            DefenseActivationResult first = service.Activate(request);
            DefenseActivationResult second = service.Activate(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(fixture.GetTargetStamina(), Is.EqualTo(staminaBefore - first.State.Definition.ActivationStaminaCost).Within(0.001f));
            Assert.That(service.TryGetActiveDefense(fixture.TargetActorId, out DefensiveActionStateSnapshot active), Is.True);
            Assert.That(active.DefinitionId, Is.EqualTo("defense-action.basic-guard"));
        }

        [Test]
        public void ShieldBlockRejectsMissingAndNonShieldEquipment()
        {
            using DefenseFixture missing = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();

            DefenseActivationResult noShield = service.Activate(missing.CreateActivationRequest("defense.block.no-shield", "defense-action.shield-block"));

            Assert.That(noShield.Succeeded, Is.False);
            Assert.That(noShield.Code, Is.EqualTo(DefensiveActionResultCode.MissingEquipment));

            using DefenseFixture wrong = DefenseFixture.Create();
            wrong.EquipTarget("item.prototype-sword");

            DefenseActivationResult sword = service.Activate(wrong.CreateActivationRequest("defense.block.sword", "defense-action.shield-block"));

            Assert.That(sword.Succeeded, Is.False);
            Assert.That(sword.Code, Is.EqualTo(DefensiveActionResultCode.IncompatibleEquipment));
        }

        [Test]
        public void WeaponParryRejectsMissingAndNonParryEquipment()
        {
            using DefenseFixture missing = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();

            DefenseActivationResult noWeapon = service.Activate(missing.CreateActivationRequest("defense.parry.no-weapon", "defense-action.weapon-parry"));

            Assert.That(noWeapon.Succeeded, Is.False);
            Assert.That(noWeapon.Code, Is.EqualTo(DefensiveActionResultCode.MissingEquipment));

            using DefenseFixture wrong = DefenseFixture.Create();
            wrong.EquipTarget("item.prototype-shield");

            DefenseActivationResult shield = service.Activate(wrong.CreateActivationRequest("defense.parry.shield", "defense-action.weapon-parry"));

            Assert.That(shield.Succeeded, Is.False);
            Assert.That(shield.Code, Is.EqualTo(DefensiveActionResultCode.IncompatibleEquipment));
        }

        [Test]
        public void EquipmentRemovedBetweenPreviewAndExecutionRejectsDefenseAndAllowsDamage()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            fixture.EquipTarget("item.prototype-shield");
            DefensiveActionService defense = new DefensiveActionService();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService attacks = new AttackResolutionService(damage, defense);

            DefenseActivationResult activation = defense.Activate(fixture.CreateActivationRequest("defense.block.stale.activate", "defense-action.shield-block"));
            Assert.That(activation.Succeeded, Is.True, activation.Message);
            AttackResolutionResult preview = attacks.PreviewAttack(fixture.CreateAttackRequest("attack.block.stale.preview", baseDamage: 30f, defenseRoll: 0.1f));
            fixture.ClearTargetEquipment();

            AttackResolutionResult execute = attacks.ExecuteAttack(fixture.CreateAttackRequest("attack.block.stale.execute", baseDamage: 30f, defenseRoll: 0.1f));

            Assert.That(preview.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.BlockSucceeded));
            Assert.That(execute.DefenseResult.Succeeded, Is.False);
            Assert.That(execute.DefenseResult.Code, Is.EqualTo(DefensiveActionResultCode.UnequippedEquipment));
            Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
        }

        [Test]
        public void SkillContributionChangesFinalDefenseChance()
        {
            using DefenseFixture baseline = DefenseFixture.Create(targetEvasion: 0f);
            baseline.EquipTarget("item.prototype-sword");
            DefensiveActionService baselineDefense = new DefensiveActionService();
            baselineDefense.Activate(baseline.CreateActivationRequest("defense.skill.baseline.activate", "defense-action.weapon-parry"));
            DefenseResolutionResult withoutSkill = baselineDefense.PreviewResolve(baseline.CreateDefenseResolutionRequest("defense.skill.baseline.resolve", defenseRoll: 0.5f));

            using DefenseFixture skilled = DefenseFixture.Create(targetEvasion: 0f);
            skilled.EquipTarget("item.prototype-sword");
            skilled.GrantTargetSkill("skill.swordsmanship", SkillGrade.A);
            DefensiveActionService skilledDefense = new DefensiveActionService();
            skilledDefense.Activate(skilled.CreateActivationRequest("defense.skill.skilled.activate", "defense-action.weapon-parry"));
            DefenseResolutionResult withSkill = skilledDefense.PreviewResolve(skilled.CreateDefenseResolutionRequest("defense.skill.skilled.resolve", defenseRoll: 0.5f));

            Assert.That(withSkill.FinalDefenseChance, Is.GreaterThan(withoutSkill.FinalDefenseChance));
        }

        [Test]
        public void InsufficientStaminaAtActivationFailsWithoutState()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            fixture.SetTargetStamina(1f);
            DefensiveActionService service = new DefensiveActionService();

            DefenseActivationResult result = service.Activate(fixture.CreateActivationRequest("defense.activate.no-stamina", "defense-action.basic-guard"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(DefensiveActionResultCode.InsufficientStamina));
            Assert.That(service.TryGetActiveDefense(fixture.TargetActorId, out _), Is.False);
        }

        [Test]
        public void InsufficientStaminaAtResolutionConsumesDodgeAndDoesNotReportDefenseSuccess()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService defense = new DefensiveActionService();
            defense.Activate(fixture.CreateActivationRequest("defense.resolve.no-stamina.activate", "defense-action.basic-dodge"));
            fixture.SetTargetStamina(1f);

            DefenseResolutionResult result = defense.Resolve(fixture.CreateDefenseResolutionRequest("defense.resolve.no-stamina", defenseRoll: 0.1f));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(DefenseResolutionOutcome.InsufficientStamina));
            Assert.That(result.Consumed, Is.True);
            Assert.That(defense.TryGetActiveDefense(fixture.TargetActorId, out _), Is.False);
        }

        [Test]
        public void SuccessfulAndFailedDodgeWindowsConsumeConsistently()
        {
            using DefenseFixture success = DefenseFixture.Create(targetEvasion: 0f);
            DefensiveActionService successDefense = new DefensiveActionService();
            successDefense.Activate(success.CreateActivationRequest("defense.dodge.success.activate", "defense-action.basic-dodge"));
            DefenseResolutionResult dodge = successDefense.Resolve(success.CreateDefenseResolutionRequest("defense.dodge.success", defenseRoll: 0.1f));

            Assert.That(dodge.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeSucceeded));
            Assert.That(dodge.Consumed, Is.True);
            Assert.That(successDefense.TryGetActiveDefense(success.TargetActorId, out _), Is.False);

            using DefenseFixture fail = DefenseFixture.Create(targetEvasion: 0f);
            DefensiveActionService failDefense = new DefensiveActionService();
            failDefense.Activate(fail.CreateActivationRequest("defense.dodge.fail.activate", "defense-action.basic-dodge"));
            DefenseResolutionResult failed = failDefense.Resolve(fail.CreateDefenseResolutionRequest("defense.dodge.fail", defenseRoll: 0.9f));

            Assert.That(failed.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeFailed));
            Assert.That(failed.Consumed, Is.True);
            Assert.That(failDefense.TryGetActiveDefense(fail.TargetActorId, out _), Is.False);
        }

        [Test]
        public void SuccessfulAndFailedParryWindowsConsumeConsistently()
        {
            using DefenseFixture success = DefenseFixture.Create(targetEvasion: 0f);
            success.EquipTarget("item.prototype-sword");
            DefensiveActionService successDefense = new DefensiveActionService();
            successDefense.Activate(success.CreateActivationRequest("defense.parry.success.activate", "defense-action.weapon-parry"));
            DefenseResolutionResult parry = successDefense.Resolve(success.CreateDefenseResolutionRequest("defense.parry.success", defenseRoll: 0.1f));

            Assert.That(parry.Outcome, Is.EqualTo(DefenseResolutionOutcome.ParrySucceeded));
            Assert.That(parry.Consumed, Is.True);
            Assert.That(successDefense.TryGetActiveDefense(success.TargetActorId, out _), Is.False);

            using DefenseFixture fail = DefenseFixture.Create(targetEvasion: 0f);
            fail.EquipTarget("item.prototype-sword");
            DefensiveActionService failDefense = new DefensiveActionService();
            failDefense.Activate(fail.CreateActivationRequest("defense.parry.fail.activate", "defense-action.weapon-parry"));
            DefenseResolutionResult failed = failDefense.Resolve(fail.CreateDefenseResolutionRequest("defense.parry.fail", defenseRoll: 0.9f));

            Assert.That(failed.Outcome, Is.EqualTo(DefenseResolutionOutcome.ParryFailed));
            Assert.That(failed.Consumed, Is.True);
            Assert.That(failDefense.TryGetActiveDefense(fail.TargetActorId, out _), Is.False);
        }

        [Test]
        public void UnparryableUnblockableAndUndodgeableAttacksRejectDefenseWithoutConsumption()
        {
            using DefenseFixture parry = DefenseFixture.Create();
            parry.EquipTarget("item.prototype-sword");
            DefensiveActionService parryDefense = new DefensiveActionService();
            parryDefense.Activate(parry.CreateActivationRequest("defense.unparryable.activate", "defense-action.weapon-parry"));
            DefenseResolutionResult unparryable = parryDefense.Resolve(parry.CreateDefenseResolutionRequest("defense.unparryable", parryable: false));

            Assert.That(unparryable.Succeeded, Is.False);
            Assert.That(unparryable.Code, Is.EqualTo(DefensiveActionResultCode.Ineligible));
            Assert.That(unparryable.Consumed, Is.False);
            Assert.That(parryDefense.TryGetActiveDefense(parry.TargetActorId, out _), Is.True);

            using DefenseFixture block = DefenseFixture.Create();
            block.EquipTarget("item.prototype-shield");
            DefensiveActionService blockDefense = new DefensiveActionService();
            blockDefense.Activate(block.CreateActivationRequest("defense.unblockable.activate", "defense-action.shield-block"));
            DefenseResolutionResult unblockable = blockDefense.Resolve(block.CreateDefenseResolutionRequest("defense.unblockable", blockable: false));

            Assert.That(unblockable.Succeeded, Is.False);
            Assert.That(unblockable.Code, Is.EqualTo(DefensiveActionResultCode.Ineligible));
            Assert.That(unblockable.Consumed, Is.False);
            Assert.That(blockDefense.TryGetActiveDefense(block.TargetActorId, out _), Is.True);

            using DefenseFixture dodge = DefenseFixture.Create();
            DefensiveActionService dodgeDefense = new DefensiveActionService();
            dodgeDefense.Activate(dodge.CreateActivationRequest("defense.undodgeable.activate", "defense-action.basic-dodge"));
            DefenseResolutionResult undodgeable = dodgeDefense.Resolve(dodge.CreateDefenseResolutionRequest("defense.undodgeable", dodgeable: false));

            Assert.That(undodgeable.Succeeded, Is.False);
            Assert.That(undodgeable.Code, Is.EqualTo(DefensiveActionResultCode.Ineligible));
            Assert.That(undodgeable.Consumed, Is.False);
            Assert.That(dodgeDefense.TryGetActiveDefense(dodge.TargetActorId, out _), Is.True);
        }

        [Test]
        public void InvalidDefenseRollAndRollBoundaryAreDeterministic()
        {
            using DefenseFixture invalid = DefenseFixture.Create(targetEvasion: 0f);
            DefensiveActionService invalidDefense = new DefensiveActionService();
            invalidDefense.Activate(invalid.CreateActivationRequest("defense.roll.invalid.activate", "defense-action.basic-dodge"));

            DefenseResolutionResult bad = invalidDefense.Resolve(invalid.CreateDefenseResolutionRequest("defense.roll.invalid", defenseRoll: 1f));

            Assert.That(bad.Succeeded, Is.False);
            Assert.That(bad.Code, Is.EqualTo(DefensiveActionResultCode.InvalidRoll));

            using DefenseFixture below = DefenseFixture.Create(targetEvasion: 0f);
            DefensiveActionService belowDefense = new DefensiveActionService();
            belowDefense.Activate(below.CreateActivationRequest("defense.roll.below.activate", "defense-action.basic-dodge"));
            DefenseResolutionResult succeeds = belowDefense.Resolve(below.CreateDefenseResolutionRequest("defense.roll.below", defenseRoll: 0.549f));

            Assert.That(succeeds.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeSucceeded));

            using DefenseFixture equal = DefenseFixture.Create(targetEvasion: 0f);
            DefensiveActionService equalDefense = new DefensiveActionService();
            equalDefense.Activate(equal.CreateActivationRequest("defense.roll.equal.activate", "defense-action.basic-dodge"));
            DefenseResolutionResult preview = equalDefense.PreviewResolve(equal.CreateDefenseResolutionRequest("defense.roll.equal.preview", defenseRoll: 0.1f));
            DefenseResolutionResult fails = equalDefense.Resolve(equal.CreateDefenseResolutionRequest("defense.roll.equal", defenseRoll: preview.FinalDefenseChance));

            Assert.That(fails.FinalDefenseChance, Is.EqualTo(preview.FinalDefenseChance).Within(0.001f));
            Assert.That(fails.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeFailed));
        }

        [Test]
        public void PreviewAttackWithDodgeUsesDefenseCalculationButDoesNotMutateOrEmit()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService defense = new DefensiveActionService();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService attacks = new AttackResolutionService(damage, defense);
            defense.Activate(fixture.CreateActivationRequest("defense.preview.attack.activate", "defense-action.basic-dodge"));
            float staminaBefore = fixture.GetTargetStamina();
            int dodged = 0;
            defense.AttackDodged += _ => dodged++;

            AttackResolutionResult result = attacks.PreviewAttack(fixture.CreateAttackRequest("attack.preview.defense", defenseRoll: 0.1f));

            Assert.That(result.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeSucceeded));
            Assert.That(result.DamagePrevented, Is.True);
            Assert.That(damage.PreviewDamageCalls, Is.Zero);
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
            Assert.That(dodged, Is.Zero);
            Assert.That(fixture.GetTargetStamina(), Is.EqualTo(staminaBefore).Within(0.001f));
            Assert.That(defense.TryGetActiveDefense(fixture.TargetActorId, out _), Is.True);
        }

        [Test]
        public void FullPreventionPerformsNoDamageExecution()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService defense = new DefensiveActionService();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService attacks = new AttackResolutionService(damage, defense);
            defense.Activate(fixture.CreateActivationRequest("defense.execute.dodge.activate", "defense-action.basic-dodge"));

            AttackResolutionResult result = attacks.ExecuteAttack(fixture.CreateAttackRequest("attack.execute.dodge", defenseRoll: 0.1f));

            Assert.That(result.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeSucceeded));
            Assert.That(result.DamageResult, Is.Null);
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
        }

        [Test]
        public void PartialBlockPerformsExactlyOneDamageExecution()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            fixture.EquipTarget("item.prototype-shield");
            DefensiveActionService defense = new DefensiveActionService();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService attacks = new AttackResolutionService(damage, defense);
            defense.Activate(fixture.CreateActivationRequest("defense.block.activate", "defense-action.shield-block"));

            AttackResolutionResult result = attacks.ExecuteAttack(fixture.CreateAttackRequest("attack.block", baseDamage: 30f, defenseRoll: 0.1f));

            Assert.That(result.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.BlockSucceeded));
            Assert.That(result.DefenseResult.PreventedDamage, Is.EqualTo(20f).Within(0.001f));
            Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
            Assert.That(damage.LastDamageRequest.RequestedAmount, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void DuplicateAttackSpendsNoAdditionalStaminaAndConsumesNoAdditionalState()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService defense = new DefensiveActionService();
            AttackResolutionService attacks = new AttackResolutionService(new FakeDamageHealingService(), defense);
            defense.Activate(fixture.CreateActivationRequest("defense.duplicate.attack.activate", "defense-action.basic-dodge"));
            float staminaBefore = fixture.GetTargetStamina();
            AttackResolutionRequest request = fixture.CreateAttackRequest("attack.duplicate.defense", defenseRoll: 0.1f);

            AttackResolutionResult first = attacks.ExecuteAttack(request);
            AttackResolutionResult second = attacks.ExecuteAttack(request);

            Assert.That(first.DefenseResult.Consumed, Is.True);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(fixture.GetTargetStamina(), Is.EqualTo(staminaBefore - fixture.GetDefense("defense-action.basic-dodge").SuccessStaminaCost).Within(0.001f));
            Assert.That(defense.TryGetActiveDefense(fixture.TargetActorId, out _), Is.False);
        }

        [Test]
        public void Feature61MitigationStillAppliesAfterBlock()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            fixture.EquipTarget("item.prototype-shield");
            fixture.AddTargetStat(CalculatedStatIds.PhysicalDefense, 5f, "test.physical-defense");
            DefensiveActionService defense = new DefensiveActionService();
            AttackResolutionService attacks = new AttackResolutionService(new DamageHealingService(), defense);
            defense.Activate(fixture.CreateActivationRequest("defense.block.mitigation.activate", "defense-action.shield-block"));
            float healthBefore = fixture.TargetResources.GetCurrent(ResourceIds.Health);

            AttackResolutionResult result = attacks.ExecuteAttack(fixture.CreateAttackRequest("attack.block.mitigation", baseDamage: 30f, defenseRoll: 0.1f));

            Assert.That(result.DefenseResult.PreventedDamage, Is.EqualTo(20f).Within(0.001f));
            Assert.That(result.DamageResult, Is.Not.Null);
            Assert.That(result.DamageResult.RequestedAmount, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.DamageResult.FinalDamageAmount, Is.LessThan(10f));
            Assert.That(fixture.TargetResources.GetCurrent(ResourceIds.Health), Is.EqualTo(healthBefore - result.DamageResult.FinalDamageAmount).Within(0.001f));
        }

        [Test]
        public void TrueDamageAuthoredRulesForDodgeParryBlockAndGuard()
        {
            using DefenseFixture dodgeFixture = DefenseFixture.Create();
            DefensiveActionService dodgeDefense = new DefensiveActionService();
            FakeDamageHealingService dodgeDamage = new FakeDamageHealingService();
            dodgeDefense.Activate(dodgeFixture.CreateActivationRequest("defense.true.dodge.activate", "defense-action.basic-dodge"));
            AttackResolutionResult dodge = new AttackResolutionService(dodgeDamage, dodgeDefense).ExecuteAttack(dodgeFixture.CreateAttackRequest("attack.true.dodge", damageTypeId: "damage.true", defenseRoll: 0.1f));
            Assert.That(dodge.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.DodgeSucceeded));
            Assert.That(dodgeDamage.ApplyDamageCalls, Is.Zero);

            using DefenseFixture parryFixture = DefenseFixture.Create();
            parryFixture.EquipTarget("item.prototype-sword");
            DefensiveActionService parryDefense = new DefensiveActionService();
            FakeDamageHealingService parryDamage = new FakeDamageHealingService();
            parryDefense.Activate(parryFixture.CreateActivationRequest("defense.true.parry.activate", "defense-action.weapon-parry"));
            AttackResolutionResult parry = new AttackResolutionService(parryDamage, parryDefense).ExecuteAttack(parryFixture.CreateAttackRequest("attack.true.parry", damageTypeId: "damage.true", defenseRoll: 0.1f));
            Assert.That(parry.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.ParrySucceeded));
            Assert.That(parryDamage.ApplyDamageCalls, Is.Zero);

            using DefenseFixture blockFixture = DefenseFixture.Create();
            blockFixture.EquipTarget("item.prototype-shield");
            DefensiveActionService blockDefense = new DefensiveActionService();
            FakeDamageHealingService blockDamage = new FakeDamageHealingService();
            blockDefense.Activate(blockFixture.CreateActivationRequest("defense.true.block.activate", "defense-action.shield-block"));
            AttackResolutionResult block = new AttackResolutionService(blockDamage, blockDefense).ExecuteAttack(blockFixture.CreateAttackRequest("attack.true.block", baseDamage: 30f, damageTypeId: "damage.true", defenseRoll: 0.1f));
            Assert.That(block.DefenseResult.Code, Is.EqualTo(DefensiveActionResultCode.Ineligible));
            Assert.That(blockDamage.ApplyDamageCalls, Is.EqualTo(1));

            using DefenseFixture guardFixture = DefenseFixture.Create();
            DefensiveActionService guardDefense = new DefensiveActionService();
            FakeDamageHealingService guardDamage = new FakeDamageHealingService();
            guardDefense.Activate(guardFixture.CreateActivationRequest("defense.true.guard.activate", "defense-action.basic-guard"));
            AttackResolutionResult guard = new AttackResolutionService(guardDamage, guardDefense).ExecuteAttack(guardFixture.CreateAttackRequest("attack.true.guard", baseDamage: 30f, damageTypeId: "damage.true", defenseRoll: 0.1f));
            Assert.That(guard.DefenseResult.Code, Is.EqualTo(DefensiveActionResultCode.Ineligible));
            Assert.That(guardDamage.ApplyDamageCalls, Is.EqualTo(1));
        }

        [Test]
        public void DeadUnconsciousAndDefeatedActorsCannotActivateOrResolveDefense()
        {
            AssertCannotActivateWhen(ActorLifecycleState.Dead);
            AssertCannotActivateWhen(ActorLifecycleState.Unconscious);
            AssertCannotActivateWhen(ActorLifecycleState.Defeated);

            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService defense = new DefensiveActionService();
            defense.Activate(fixture.CreateActivationRequest("defense.lifecycle.resolve.activate", "defense-action.basic-dodge"));
            fixture.SetLifecycleState(ActorLifecycleState.Dead);

            DefenseResolutionResult result = defense.Resolve(fixture.CreateDefenseResolutionRequest("defense.lifecycle.resolve", defenseRoll: 0.1f));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(DefensiveActionResultCode.ActorCannotAct));
            Assert.That(defense.TryGetActiveDefense(fixture.TargetActorId, out _), Is.False);
        }

        [Test]
        public void LifecycleTransitionsClearDefenseAndRecoveryRevivalDoNotRestoreIt()
        {
            using DefenseFixture unconscious = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();
            service.Activate(unconscious.CreateActivationRequest("defense.lifecycle.defeat.activate", "defense-action.basic-guard"));
            unconscious.TargetLifecycle.ExecuteDefeat(new DefeatResolutionRequest("defense.lifecycle.defeat", "test", null, unconscious.TargetActorId, unconscious.Target, LifecycleTriggerKind.ExplicitDefeat));

            Assert.That(service.TryGetActiveDefense(unconscious.TargetActorId, out _), Is.False);

            unconscious.SetTargetHealth(0f);
            unconscious.TargetLifecycle.ExecuteRecovery(new LifecycleRecoveryRequest("defense.lifecycle.recover", "test", null, unconscious.TargetActorId, unconscious.Target, 25f));
            Assert.That(unconscious.TargetLifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(service.TryGetActiveDefense(unconscious.TargetActorId, out _), Is.False);

            using DefenseFixture dead = DefenseFixture.Create();
            DefensiveActionService deathService = new DefensiveActionService();
            deathService.Activate(dead.CreateActivationRequest("defense.lifecycle.death.activate", "defense-action.basic-guard"));
            dead.TargetLifecycle.ExecuteDeath(new LifecycleDeathRequest("defense.lifecycle.death", "test", null, dead.TargetActorId, dead.Target, LifecycleTriggerKind.ExplicitDeath));

            Assert.That(deathService.TryGetActiveDefense(dead.TargetActorId, out _), Is.False);

            dead.SetTargetHealth(0f);
            dead.TargetLifecycle.ExecuteRevival(new LifecycleRevivalRequest("defense.lifecycle.revive", "test", null, dead.TargetActorId, dead.Target, 25f));
            Assert.That(dead.TargetLifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(deathService.TryGetActiveDefense(dead.TargetActorId, out _), Is.False);
        }

        [Test]
        public void ReplacementBodyDoesNotInheritDefense()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService defense = new DefensiveActionService();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            defense.Activate(fixture.CreateActivationRequest("defense.replacement.activate", "defense-action.basic-dodge"));
            GameObject replacement = fixture.CreateReplacementTargetBody();
            try
            {
                AttackResolutionRequest request = fixture.CreateAttackRequest("attack.replacement", targetObject: replacement, defenseRoll: 0.1f);
                AttackResolutionResult result = new AttackResolutionService(damage, defense).ExecuteAttack(request);

                Assert.That(result.DefenseResult.Succeeded, Is.False);
                Assert.That(result.DefenseResult.Code, Is.EqualTo(DefensiveActionResultCode.StaleBody));
                Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void SaveLoadRestoreClearsTransientDefenseWithoutEvents()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();
            int activated = 0;
            int cancelled = 0;
            int attempted = 0;
            service.DefenseActivated += _ => activated++;
            service.DefenseCancelled += _ => cancelled++;
            service.DefenseAttempted += _ => attempted++;
            service.Activate(fixture.CreateActivationRequest("defense.restore.activate", "defense-action.basic-guard"));
            Assert.That(activated, Is.EqualTo(1));
            object saveData = fixture.TargetResources.CreateSaveData("player.local", "person.prototype-player");

            service.ClearTransientStateForRestore(fixture.TargetActorId);

            Assert.That(saveData, Is.Not.Null);
            Assert.That(service.TryGetActiveDefense(fixture.TargetActorId, out _), Is.False);
            Assert.That(cancelled, Is.Zero);
            Assert.That(attempted, Is.Zero);
        }

        [Test]
        public void CombatActivityRefreshesExactlyOncePerLogicalAttack()
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            fixture.EquipTarget("item.prototype-shield");
            DefensiveActionService defense = new DefensiveActionService();
            CombatStateService combat = fixture.Target.AddComponent<CombatStateService>();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService attacks = new AttackResolutionService(damage, defense, combat);
            int entered = 0;
            int refreshed = 0;
            combat.ActorEnteredCombat += _ => entered++;
            combat.ActorCombatActivityRefreshed += _ => refreshed++;
            defense.Activate(fixture.CreateActivationRequest("defense.combat.activate", "defense-action.shield-block"));

            AttackResolutionResult result = attacks.ExecuteAttack(fixture.CreateAttackRequest("attack.combat.once", baseDamage: 30f, defenseRoll: 0.1f));

            Assert.That(result.DefenseResult.Outcome, Is.EqualTo(DefenseResolutionOutcome.BlockSucceeded));
            Assert.That(entered + refreshed, Is.EqualTo(1));
            Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeAssemblyDoesNotReferenceDevelopmentOrTests()
        {
            AssemblyName[] references = typeof(DefensiveActionService).Assembly.GetReferencedAssemblies();

            Assert.That(Array.Exists(references, reference => reference.Name.Contains("Development") || reference.Name.Contains("Tests")), Is.False);
        }

        private static void AssertCannotActivateWhen(ActorLifecycleState state)
        {
            using DefenseFixture fixture = DefenseFixture.Create();
            DefensiveActionService service = new DefensiveActionService();
            fixture.SetLifecycleState(state);

            DefenseActivationResult result = service.Activate(fixture.CreateActivationRequest($"defense.lifecycle.activate.{state}", "defense-action.basic-dodge"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(DefensiveActionResultCode.ActorCannotAct));
        }

        private static void AssertDefense(DefinitionRegistry registry, string id, DefensiveActionType expectedType)
        {
            Assert.That(registry.TryGet(id, out DefensiveActionDefinition definition), Is.True, id);
            Assert.That(definition.ActionType, Is.EqualTo(expectedType));
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private sealed class FakeDamageHealingService : IDamageHealingService
        {
            public int PreviewDamageCalls { get; private set; }
            public int ApplyDamageCalls { get; private set; }
            public DamageApplicationRequest LastDamageRequest { get; private set; }

            public DamageApplicationResult PreviewDamage(DamageApplicationRequest request)
            {
                PreviewDamageCalls++;
                LastDamageRequest = request;
                return CreateResult(request, preview: true);
            }

            public DamageApplicationResult ApplyDamage(DamageApplicationRequest request)
            {
                ApplyDamageCalls++;
                LastDamageRequest = request;
                return CreateResult(request, preview: false);
            }

            public HealingApplicationResult PreviewHealing(HealingApplicationRequest request)
            {
                return HealingApplicationResult.Failure(request, "Unsupported", "Healing is not used by defense tests.");
            }

            public HealingApplicationResult ApplyHealing(HealingApplicationRequest request)
            {
                return HealingApplicationResult.Failure(request, "Unsupported", "Healing is not used by defense tests.");
            }

            private static DamageApplicationResult CreateResult(DamageApplicationRequest request, bool preview)
            {
                return DamageApplicationResult.Create(
                    preview,
                    preview ? ImmediateCombatResultCode.Preview : ImmediateCombatResultCode.Applied,
                    "Fake damage result.",
                    request,
                    request.TargetActorId,
                    request.RequestedAmount,
                    0f,
                    0f,
                    0f,
                    0f,
                    request.RequestedAmount,
                    100f,
                    100f - request.RequestedAmount,
                    0f,
                    100f,
                    false,
                    false,
                    false,
                    request.RequestedAmount > 0f,
                    false,
                    0f,
                    null);
            }
        }

        private sealed class DefenseFixture : IDisposable
        {
            private readonly string targetLocalId;
            private DefinitionRegistry registry;
            private PlayerEquipment targetEquipment;

            public GameObject Attacker { get; private set; }
            public GameObject Target { get; private set; }
            public string AttackerActorId { get; private set; }
            public string TargetActorId { get; private set; }
            public CalculatedStatCollection AttackerStats { get; private set; }
            public CalculatedStatCollection TargetStats { get; private set; }
            public CharacterResourceCollection TargetResources { get; private set; }
            public CharacterSkillCollection TargetSkills { get; private set; }
            public ActorLifecycleController TargetLifecycle { get; private set; }

            private DefenseFixture(string targetLocalId)
            {
                this.targetLocalId = targetLocalId;
            }

            public static DefenseFixture Create(float targetEvasion = 10f)
            {
                string localId = $"defense-test-target-{Guid.NewGuid():N}";
                DefenseFixture fixture = new DefenseFixture(localId)
                {
                    registry = LoadCatalog().CreateRegistry(),
                    Attacker = new GameObject($"Defense Attacker {Guid.NewGuid():N}"),
                    Target = new GameObject($"Defense Target {Guid.NewGuid():N}")
                };
                fixture.AttackerStats = ConfigureStats(fixture.registry, fixture.Attacker, CalculatedStatIds.Accuracy, 10f);
                fixture.TargetStats = ConfigureStats(fixture.registry, fixture.Target, CalculatedStatIds.Evasion, targetEvasion);
                AddIdentity(fixture.Attacker, $"defense-test-attacker-{Guid.NewGuid():N}", out string attackerId);
                AddIdentity(fixture.Target, localId, out string targetId);
                fixture.AttackerActorId = attackerId;
                fixture.TargetActorId = targetId;
                fixture.TargetResources = fixture.Target.AddComponent<CharacterResourceCollection>();
                fixture.TargetResources.Configure(fixture.registry, fixture.TargetStats, "player.local");
                fixture.TargetSkills = fixture.Target.AddComponent<CharacterSkillCollection>();
                fixture.TargetSkills.Configure(fixture.registry, fixture.TargetStats, null);
                fixture.TargetLifecycle = fixture.Target.AddComponent<ActorLifecycleController>();
                fixture.TargetLifecycle.Configure(null, fixture.TargetResources, null, null);
                fixture.targetEquipment = fixture.Target.AddComponent<PlayerEquipment>();
                fixture.targetEquipment.GetSlot(EquipmentSlotType.MainHand);
                return fixture;
            }

            public DefenseActivationRequest CreateActivationRequest(string transactionId, string definitionId)
            {
                return new DefenseActivationRequest(transactionId, TargetActorId, Target, GetDefense(definitionId), now: 1f, authorityValidated: true);
            }

            public DefenseResolutionRequest CreateDefenseResolutionRequest(string transactionId, float baseDamage = 10f, string damageTypeId = "damage.physical", float defenseRoll = 0.1f, bool blockable = true, bool parryable = true, bool dodgeable = true)
            {
                Assert.That(registry.TryGet(damageTypeId, out DamageTypeDefinition damageType), Is.True, damageTypeId);
                return new DefenseResolutionRequest(
                    transactionId,
                    "attack.direct",
                    AttackerActorId,
                    Attacker,
                    TargetActorId,
                    Target,
                    damageType,
                    AttackSourceType.Weapon,
                    baseDamage,
                    defenseRoll,
                    critical: false,
                    blockable,
                    parryable,
                    dodgeable,
                    trueDamage: damageType.IsTrueDamage,
                    allowTrueDamageActiveDefense: true,
                    now: 1.1f,
                    authorityValidated: true);
            }

            public AttackResolutionRequest CreateAttackRequest(string transactionId, float baseDamage = 10f, string damageTypeId = "damage.physical", float defenseRoll = 0.1f, bool blockable = true, bool parryable = true, bool dodgeable = true, GameObject targetObject = null)
            {
                Assert.That(registry.TryGet(damageTypeId, out DamageTypeDefinition damageType), Is.True, damageTypeId);
                Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["defense.now"] = "1.1",
                    ["defense.roll"] = defenseRoll.ToString("0.###"),
                    ["defense.blockable"] = blockable.ToString(),
                    ["defense.parryable"] = parryable.ToString(),
                    ["defense.dodgeable"] = dodgeable.ToString(),
                    ["defense.allow-true-active"] = "true"
                };
                GameObject resolvedTarget = targetObject == null ? Target : targetObject;
                return new AttackResolutionRequest(
                    transactionId,
                    AttackSourceType.Weapon,
                    Attacker,
                    AttackerActorId,
                    resolvedTarget,
                    TargetActorId,
                    damageType,
                    baseDamage,
                    hitRoll: 0.1f,
                    criticalRoll: 0.5f,
                    baseHitChance: 0.95f,
                    criticalChance: 0f,
                    criticalMultiplier: AttackResolutionRequest.DefaultCriticalMultiplier,
                    hasSuppliedDistance: true,
                    suppliedDistance: 1f,
                    hasMaximumRange: true,
                    maximumRange: 2f,
                    metadata: metadata,
                    authorityValidated: true);
            }

            public DefensiveActionDefinition GetDefense(string definitionId)
            {
                Assert.That(registry.TryGet(definitionId, out DefensiveActionDefinition definition), Is.True, definitionId);
                return definition;
            }

            public float GetTargetStamina()
            {
                return TargetResources.GetCurrent(ResourceIds.Stamina);
            }

            public void SetTargetStamina(float value)
            {
                ResourceChangeResult result = TargetResources.SetCurrent(ResourceIds.Stamina, value, "test.defense", "Set stamina.", restoration: true);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void SetTargetHealth(float value)
            {
                ResourceChangeResult result = TargetResources.SetCurrent(ResourceIds.Health, value, "test.defense", "Set health.", restoration: true);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void EquipTarget(string itemId)
            {
                Assert.That(registry.TryGet(itemId, out ItemDefinition item), Is.True, itemId);
                EquipmentSlotState slot = targetEquipment.GetSlot(item.Equipment.SlotType);
                typeof(EquipmentSlotState).GetMethod("SetItem", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(slot, new object[] { item });
            }

            public void ClearTargetEquipment()
            {
                IReadOnlyList<EquipmentSlotState> slots = targetEquipment.Slots;
                MethodInfo clear = typeof(EquipmentSlotState).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < slots.Count; i++)
                {
                    clear.Invoke(slots[i], Array.Empty<object>());
                }
            }

            public void GrantTargetSkill(string skillId, SkillGrade grade)
            {
                Assert.That(registry.TryGet(skillId, out SkillDefinition skill), Is.True, skillId);
                SkillOperationResult result = TargetSkills.GrantSkill(skill, grade, SkillAcquisitionSource.Development, "Defense test.");
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void AddTargetStat(string statId, float amount, string sourceId)
            {
                AddStat(TargetStats, statId, amount, sourceId);
            }

            public void SetLifecycleState(ActorLifecycleState state)
            {
                typeof(ActorLifecycleController).GetField("lifecycleState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(TargetLifecycle, state);
            }

            public GameObject CreateReplacementTargetBody()
            {
                GameObject replacement = new GameObject($"Defense Replacement {Guid.NewGuid():N}");
                ConfigureStats(registry, replacement, CalculatedStatIds.Evasion, 0f);
                AddIdentity(replacement, targetLocalId, out _);
                CharacterResourceCollection resources = replacement.AddComponent<CharacterResourceCollection>();
                resources.Configure(registry, replacement.GetComponent<CalculatedStatCollection>(), "player.local");
                return replacement;
            }

            public void Dispose()
            {
                if (Attacker != null)
                {
                    UnityEngine.Object.DestroyImmediate(Attacker);
                }

                if (Target != null)
                {
                    UnityEngine.Object.DestroyImmediate(Target);
                }
            }

            private static CalculatedStatCollection ConfigureStats(DefinitionRegistry registry, GameObject owner, string statId, float amount)
            {
                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                if (amount > 0f)
                {
                    AddStat(stats, statId, amount, $"{owner.name}.{statId}");
                }

                return stats;
            }

            private static void AddStat(CalculatedStatCollection stats, string statId, float amount, string sourceId)
            {
                Assert.That(stats.AddContribution(new RuntimeCalculatedStatContribution
                {
                    contributionId = sourceId,
                    statId = statId,
                    sourceId = sourceId,
                    sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                    kind = (int)CalculatedStatContributionKind.Flat,
                    direction = (int)CalculatedStatContributionDirection.Improve,
                    magnitude = amount
                }, out string failureReason), Is.True, failureReason);
            }

            private static void AddIdentity(GameObject owner, string localId, out string actorId)
            {
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                Assert.That(identity.TrySetAuthoredIdentity(localId, "scene.test", PersistenceScope.RegionOrScene, "test.defense", out string failureReason), Is.True, failureReason);
                actorId = identity.EntityId;
            }
        }
    }
}
