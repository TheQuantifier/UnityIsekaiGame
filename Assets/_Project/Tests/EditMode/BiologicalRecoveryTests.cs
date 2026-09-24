using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BiologicalRecoveryTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in createdObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void PrototypeCatalog_ResolvesCanonicalBiologicalRecoveryAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<RecoveryMethodDefinition>(registry, "recovery.natural.wound-closure");
            AssertResolves<RecoveryMethodDefinition>(registry, "recovery.natural.blood-restoration");
            AssertResolves<RecoveryMethodDefinition>(registry, "recovery.repair.construct");
            AssertResolves<RecoveryMethodDefinition>(registry, "recovery.restoration.spirit");
            AssertResolves<BiologicalRecoveryProfileDefinition>(registry, "recovery-profile.species.human");
            AssertResolves<BiologicalRecoveryProfileDefinition>(registry, "recovery-profile.species.basic-construct");
            AssertResolves<BiologicalRecoveryProfileDefinition>(registry, "recovery-profile.species.basic-spirit");
        }

        [Test]
        public void PreviewStartAndTick_DoNotMutateRecoveryConditionVitalOrEmitEvents()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.preview");
            InjuryRecordSnapshot injury = ApplyInjury(body, "tx.recovery.preview.damage", "injury.laceration", "part.hand.left", 40);
            RecoveryProcessStartRequest start = StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", injury.InjuryId, string.Empty, "tx.recovery.preview.start");
            Assert.That(body.BiologicalRecovery.StartProcess(start, body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);

            long recoveryRevision = body.BiologicalRecovery.RecoveryRevision;
            long conditionRevision = body.Condition.ConditionRevision;
            long vitalRevision = body.VitalProcesses.VitalRevision;
            int recoveryEvents = 0;
            int conditionEvents = 0;
            int vitalEvents = 0;
            body.BiologicalRecovery.RecoveryChanged += (_, _, _) => recoveryEvents++;
            body.Condition.ConditionChanged += (_, _, _) => conditionEvents++;
            body.VitalProcesses.VitalResourceChanged += (_, _, _) => vitalEvents++;
            int integrityBefore = GetStructure(body.Condition.CreateSnapshot(), "part.hand.left").CurrentIntegrity;

            BiologicalRecoveryResult previewStart = body.BiologicalRecovery.PreviewStartProcess(
                StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", injury.InjuryId, string.Empty, "tx.recovery.preview.start.only"),
                body.CreateSnapshot(),
                body.BiologicalCompatibility);
            BiologicalRecoveryResult previewTick = body.BiologicalRecovery.PreviewTick(
                Tick(body, "tx.recovery.preview.tick", 3600f),
                body.CreateSnapshot(),
                body.BiologicalCompatibility,
                body.Condition,
                body.VitalProcesses);

            Assert.That(previewStart.Succeeded, Is.True, previewStart.Message);
            Assert.That(previewStart.Preview, Is.True);
            Assert.That(previewTick.Succeeded, Is.True, previewTick.Message);
            Assert.That(previewTick.Preview, Is.True);
            Assert.That(body.BiologicalRecovery.RecoveryRevision, Is.EqualTo(recoveryRevision));
            Assert.That(body.Condition.ConditionRevision, Is.EqualTo(conditionRevision));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(vitalRevision));
            Assert.That(GetStructure(body.Condition.CreateSnapshot(), "part.hand.left").CurrentIntegrity, Is.EqualTo(integrityBefore));
            Assert.That(recoveryEvents, Is.EqualTo(0));
            Assert.That(conditionEvents, Is.EqualTo(0));
            Assert.That(vitalEvents, Is.EqualTo(0));
        }

        [Test]
        public void StructuralRecovery_UsesConditionRuntimeAndDuplicateTickIsIdempotent()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.structure");
            InjuryRecordSnapshot injury = ApplyInjury(body, "tx.recovery.structure.damage", "injury.laceration", "part.hand.left", 40);
            int integrityBefore = GetStructure(body.Condition.CreateSnapshot(), "part.hand.left").CurrentIntegrity;

            BiologicalRecoveryResult start = body.BiologicalRecovery.StartProcess(
                StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", injury.InjuryId, string.Empty, "tx.recovery.structure.start"),
                body.CreateSnapshot(),
                body.BiologicalCompatibility);
            BiologicalRecoveryResult first = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.structure.tick", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            long recoveryRevisionAfterFirst = body.BiologicalRecovery.RecoveryRevision;
            long conditionRevisionAfterFirst = body.Condition.ConditionRevision;
            BiologicalRecoveryResult duplicate = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.structure.tick", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            BodyConditionSnapshot condition = body.Condition.CreateSnapshot();

            Assert.That(start.Succeeded, Is.True, start.Message);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.StructuralRecovery, Is.Not.Null);
            Assert.That(first.StructuralRecovery.Succeeded, Is.True, first.StructuralRecovery.Message);
            Assert.That(GetStructure(condition, "part.hand.left").CurrentIntegrity, Is.GreaterThan(integrityBefore));
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(body.BiologicalRecovery.RecoveryRevision, Is.EqualTo(recoveryRevisionAfterFirst));
            Assert.That(body.Condition.ConditionRevision, Is.EqualTo(conditionRevisionAfterFirst));
        }

        [Test]
        public void VitalRecovery_RestoresBloodThroughVitalRuntime()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.vital");
            Assert.That(body.VitalProcesses.ApplyMutation(VitalRequest(body, BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 30f, "tx.recovery.vital.consume"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);
            float bloodBefore = GetResource(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Blood).CurrentValue;

            BiologicalRecoveryResult start = body.BiologicalRecovery.StartProcess(
                StartRequest(body, "recovery.natural.blood-restoration", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.Blood, "tx.recovery.vital.start"),
                body.CreateSnapshot(),
                body.BiologicalCompatibility);
            BiologicalRecoveryResult tick = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.vital.tick", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            VitalResourceSnapshot bloodAfter = GetResource(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Blood);

            Assert.That(start.Succeeded, Is.True, start.Message);
            Assert.That(tick.Succeeded, Is.True, tick.Message);
            Assert.That(tick.VitalResourceMutation, Is.Not.Null);
            Assert.That(tick.VitalResourceMutation.Succeeded, Is.True, tick.VitalResourceMutation.Message);
            Assert.That(bloodAfter.CurrentValue, Is.GreaterThan(bloodBefore));
        }

        [Test]
        public void IncompatibleMethodRejectedThroughCompatibilityWithoutMutation()
        {
            ActorBodyRuntime construct = CreateBodyRuntime(LoadRegistry(), "actor.runtime.recovery.compat.construct", "person.recovery");
            Assert.That(construct.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            LocalizedStructuralDamageResult damage = construct.Condition.ApplyLocalizedDamage(ConditionRequest(construct, "tx.recovery.compat.damage", "injury.core-damage", "core.power", 40), construct.CreateAnatomySnapshot(), compatibility: construct.BiologicalCompatibility, body: construct.CreateSnapshot());
            Assert.That(damage.Succeeded, Is.True, damage.Message);

            long revisionBefore = construct.BiologicalRecovery.RecoveryRevision;
            BiologicalRecoveryResult natural = construct.BiologicalRecovery.PreviewStartProcess(
                StartRequest(construct, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "core.power", damage.InjuryId, string.Empty, "tx.recovery.compat.natural"),
                construct.CreateSnapshot(),
                construct.BiologicalCompatibility);

            Assert.That(natural.Succeeded, Is.False);
            Assert.That(natural.Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed).Or.EqualTo(BiologicalRecoveryResultCode.Incompatible).Or.EqualTo(BiologicalRecoveryResultCode.Suppressed));
            Assert.That(construct.BiologicalRecovery.RecoveryRevision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void PrincipalRecoveryMethods_EvaluateCompatibilityAndAuthoredProfileAccess()
        {
            ActorBodyRuntime human = CreateHumanBody("actor.runtime.recovery.methods.human");
            InjuryRecordSnapshot laceration = ApplyInjury(human, "tx.recovery.methods.laceration", "injury.laceration", "part.hand.left", 15);
            InjuryRecordSnapshot tissue = ApplyInjury(human, "tx.recovery.methods.tissue", "injury.blunt-trauma", "part.arm.left", 15);
            InjuryRecordSnapshot fracture = ApplyInjury(human, "tx.recovery.methods.fracture", "injury.fracture", "part.leg.left", 20);
            InjuryRecordSnapshot organ = ApplyInjury(human, "tx.recovery.methods.organ", "injury.organ-trauma", "organ.lung.left", 20);

            Assert.That(PreviewStart(human, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", laceration.InjuryId, string.Empty, "tx.recovery.methods.wound").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.natural.tissue-healing", RecoveryTargetCategory.Injury, "part.arm.left", tissue.InjuryId, string.Empty, "tx.recovery.methods.tissue.start").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.natural.fracture-healing", RecoveryTargetCategory.Injury, "part.leg.left", fracture.InjuryId, string.Empty, "tx.recovery.methods.fracture.start").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.natural.organ-recovery", RecoveryTargetCategory.Injury, "organ.lung.left", organ.InjuryId, string.Empty, "tx.recovery.methods.organ.start").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.natural.blood-restoration", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.Blood, "tx.recovery.methods.blood").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.natural.breath-restoration", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.Breath, "tx.recovery.methods.breath").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.magical.biological-healing", RecoveryTargetCategory.StructuralIntegrity, "part.hand.left", string.Empty, string.Empty, "tx.recovery.methods.magical").Succeeded, Is.True);
            Assert.That(PreviewStart(human, "recovery.magical.holy-healing", RecoveryTargetCategory.StructuralIntegrity, "part.hand.left", string.Empty, string.Empty, "tx.recovery.methods.holy").Succeeded, Is.True);

            ActorBodyRuntime spirit = CreateBodyRuntime(LoadRegistry(), "actor.runtime.recovery.methods.spirit", "person.recovery");
            Assert.That(spirit.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            BiologicalRecoveryResult spiritNatural = PreviewStart(spirit, "recovery.natural.tissue-healing", RecoveryTargetCategory.StructuralIntegrity, "core.spiritual", string.Empty, string.Empty, "tx.recovery.methods.spirit.natural");
            BiologicalRecoveryResult spiritRestoration = PreviewStart(spirit, "recovery.restoration.spirit", RecoveryTargetCategory.StructuralIntegrity, "core.spiritual", string.Empty, string.Empty, "tx.recovery.methods.spirit.restoration");
            BiologicalRecoveryResult necroticRestoration = PreviewStart(spirit, "recovery.magical.necrotic-restoration", RecoveryTargetCategory.StructuralIntegrity, "core.spiritual", string.Empty, string.Empty, "tx.recovery.methods.necrotic");

            Assert.That(spiritNatural.Succeeded, Is.False);
            Assert.That(spiritNatural.Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed).Or.EqualTo(BiologicalRecoveryResultCode.Incompatible).Or.EqualTo(BiologicalRecoveryResultCode.Suppressed));
            Assert.That(spiritRestoration.Succeeded, Is.False, "Spirit Restoration requires an authored SpiritSanctuary rest context.");
            Assert.That(spiritRestoration.Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed));
            Assert.That(necroticRestoration.Succeeded, Is.True, necroticRestoration.Message);

            ActorBodyRuntime construct = CreateBodyRuntime(LoadRegistry(), "actor.runtime.recovery.methods.construct", "person.recovery");
            Assert.That(construct.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            Assert.That(PreviewStart(construct, "recovery.magical.biological-healing", RecoveryTargetCategory.StructuralIntegrity, "core.power", string.Empty, string.Empty, "tx.recovery.methods.construct.bio").Succeeded, Is.False);
            Assert.That(PreviewStart(construct, "recovery.repair.construct", RecoveryTargetCategory.StructuralIntegrity, "core.power", string.Empty, string.Empty, "tx.recovery.methods.construct.repair").Succeeded, Is.False);
            Assert.That(PreviewStart(construct, "recovery.regeneration.structure", RecoveryTargetCategory.StructuralIntegrity, "core.power", string.Empty, string.Empty, "tx.recovery.methods.construct.regen").Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed));
        }

        [Test]
        public void RecoveryLimits_StopNaturalHealingAndRejectDestroyedOrSeveredTargets()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.limits");
            InjuryRecordSnapshot wound = ApplyInjury(body, "tx.recovery.limits.damage", "injury.laceration", "part.hand.left", 40);
            Assert.That(body.BiologicalRecovery.StartProcess(StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", wound.InjuryId, string.Empty, "tx.recovery.limits.start"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);

            BiologicalRecoveryResult first = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.limits.tick", 24f * 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            StructureConditionSnapshot hand = GetStructure(body.Condition.CreateSnapshot(), "part.hand.left");
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(hand.CurrentIntegrity, Is.EqualTo(Mathf.RoundToInt(hand.MaximumIntegrity * 0.75f)));

            ActorBodyRuntime destroyed = CreateHumanBody("actor.runtime.recovery.limits.destroyed");
            InjuryRecordSnapshot destroyedInjury = ApplyInjury(destroyed, "tx.recovery.limits.destroyed.damage", "injury.crush", "part.foot.left", 500);
            Assert.That(destroyed.BiologicalRecovery.StartProcess(StartRequest(destroyed, "recovery.natural.tissue-healing", RecoveryTargetCategory.Injury, "part.foot.left", destroyedInjury.InjuryId, string.Empty, "tx.recovery.limits.destroyed.start"), destroyed.CreateSnapshot(), destroyed.BiologicalCompatibility).Succeeded, Is.True);
            BiologicalRecoveryResult destroyedTick = destroyed.BiologicalRecovery.ApplyTick(Tick(destroyed, "tx.recovery.limits.destroyed.tick", 3600f), destroyed.CreateSnapshot(), destroyed.BiologicalCompatibility, destroyed.Condition, destroyed.VitalProcesses);
            Assert.That(destroyedTick.Succeeded, Is.False);
            Assert.That(destroyedTick.Code, Is.EqualTo(BiologicalRecoveryResultCode.OwningSystemFailure));

            ActorBodyRuntime severed = CreateHumanBody("actor.runtime.recovery.limits.severed");
            InjuryRecordSnapshot severedInjury = ApplyInjury(severed, "tx.recovery.limits.severed.damage", "injury.severing", "part.hand.right", 500);
            Assert.That(severed.BiologicalRecovery.StartProcess(StartRequest(severed, "recovery.natural.tissue-healing", RecoveryTargetCategory.Injury, "part.hand.right", severedInjury.InjuryId, string.Empty, "tx.recovery.limits.severed.start"), severed.CreateSnapshot(), severed.BiologicalCompatibility).Succeeded, Is.True);
            BiologicalRecoveryResult severedTick = severed.BiologicalRecovery.ApplyTick(Tick(severed, "tx.recovery.limits.severed.tick", 3600f), severed.CreateSnapshot(), severed.BiologicalCompatibility, severed.Condition, severed.VitalProcesses);
            Assert.That(severedTick.Succeeded, Is.False);
            Assert.That(severedTick.Code, Is.EqualTo(BiologicalRecoveryResultCode.OwningSystemFailure));
        }

        [Test]
        public void RestContextControlsSleepAndFatigueWithoutRealTime()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.rest.context");
            Assert.That(body.VitalProcesses.ApplyMutation(VitalRequest(body, BiologicalResourceIds.SleepNeed, VitalResourceMutationOperation.Consume, 40f, "tx.recovery.rest.sleep.consume"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(body.VitalProcesses.ApplyMutation(VitalRequest(body, BiologicalResourceIds.Fatigue, VitalResourceMutationOperation.Consume, 30f, "tx.recovery.rest.fatigue.consume"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);

            BiologicalRecoveryResult sleepBlocked = PreviewStart(body, "recovery.natural.sleep-need-reduction", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.SleepNeed, "tx.recovery.rest.sleep.blocked");
            BiologicalRecoveryResult fatigueBlocked = PreviewStart(body, "recovery.natural.fatigue-reduction", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.Fatigue, "tx.recovery.rest.fatigue.blocked");
            Assert.That(sleepBlocked.Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed));
            Assert.That(fatigueBlocked.Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed));

            Assert.That(SetRest(body, RecoveryRestType.Resting, "tx.recovery.rest.resting").Succeeded, Is.True);
            Assert.That(PreviewStart(body, "recovery.natural.fatigue-reduction", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.Fatigue, "tx.recovery.rest.fatigue.start").Succeeded, Is.True);
            Assert.That(PreviewStart(body, "recovery.natural.sleep-need-reduction", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.SleepNeed, "tx.recovery.rest.sleep.still.blocked").Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed));

            Assert.That(SetRest(body, RecoveryRestType.Sleeping, "tx.recovery.rest.sleeping").Succeeded, Is.True);
            BiologicalRecoveryResult sleepStart = body.BiologicalRecovery.StartProcess(StartRequest(body, "recovery.natural.sleep-need-reduction", RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, BiologicalResourceIds.SleepNeed, "tx.recovery.rest.sleep.start"), body.CreateSnapshot(), body.BiologicalCompatibility);
            float sleepNeedBefore = GetResource(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.SleepNeed).CurrentValue;
            BiologicalRecoveryResult sleepTick = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.rest.sleep.tick", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            float sleepNeedAfter = GetResource(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.SleepNeed).CurrentValue;

            Assert.That(sleepStart.Succeeded, Is.True, sleepStart.Message);
            Assert.That(sleepTick.Succeeded, Is.True, sleepTick.Message);
            Assert.That(sleepNeedAfter, Is.LessThan(sleepNeedBefore));

            Assert.That(SetRest(body, RecoveryRestType.NotResting, "tx.recovery.rest.clear").Succeeded, Is.True);
            BiologicalRecoveryResult paused = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.rest.after-clear", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            Assert.That(paused.Succeeded, Is.False);
            Assert.That(paused.Code, Is.EqualTo(BiologicalRecoveryResultCode.Blocked));
        }

        [Test]
        public void VitalResourceRecovery_UsesVitalProcessRuntimeForAllBiologicalResources()
        {
            AssertResourceRecovery(CreateHumanBody("actor.runtime.recovery.vital.blood"), "recovery.natural.blood-restoration", BiologicalResourceIds.Blood, "tx.recovery.vital.all.blood", RecoveryRestType.NotResting);
            AssertResourceRecovery(CreateHumanBody("actor.runtime.recovery.vital.breath"), "recovery.natural.breath-restoration", BiologicalResourceIds.Breath, "tx.recovery.vital.all.breath", RecoveryRestType.NotResting);
            AssertResourceRecovery(CreateHumanBody("actor.runtime.recovery.vital.nutrition"), "recovery.natural.nutrition-recovery", BiologicalResourceIds.Nutrition, "tx.recovery.vital.all.nutrition", RecoveryRestType.NotResting);
            AssertResourceRecovery(CreateHumanBody("actor.runtime.recovery.vital.hydration"), "recovery.natural.hydration-recovery", BiologicalResourceIds.Hydration, "tx.recovery.vital.all.hydration", RecoveryRestType.NotResting);
            AssertResourceRecovery(CreateHumanBody("actor.runtime.recovery.vital.fatigue"), "recovery.natural.fatigue-reduction", BiologicalResourceIds.Fatigue, "tx.recovery.vital.all.fatigue", RecoveryRestType.Sleeping);
            AssertResourceRecovery(CreateHumanBody("actor.runtime.recovery.vital.sleep"), "recovery.natural.sleep-need-reduction", BiologicalResourceIds.SleepNeed, "tx.recovery.vital.all.sleep", RecoveryRestType.Sleeping);
        }

        [Test]
        public void SuppressionPausesRecoveryAndSourceRemovalResumesWithoutDuplicatingProcess()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.interrupt.compat");
            InjuryRecordSnapshot wound = ApplyInjury(body, "tx.recovery.interrupt.damage", "injury.laceration", "part.hand.left", 40);
            BiologicalRecoveryResult start = body.BiologicalRecovery.StartProcess(StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", wound.InjuryId, string.Empty, "tx.recovery.interrupt.start"), body.CreateSnapshot(), body.BiologicalCompatibility);
            Assert.That(start.Succeeded, Is.True, start.Message);
            string processId = start.ProcessId;

            RuntimeBiologicalInteractionRule suppression = new RuntimeBiologicalInteractionRule(
                "test.recovery.suppression",
                BiologicalCompatibilitySourceKind.Development,
                "test.recovery",
                BiologicalInteractionIds.NaturalHealing,
                BiologicalInteractionCategory.Recovery,
                BiologicalInteractionRuleKind.Suppression,
                BiologicalCompatibilityState.Compatible,
                1f,
                1f,
                1f,
                0f,
                999f,
                1,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Edit Mode recovery suppression");
            Assert.That(body.BiologicalCompatibility.AddOrUpdateContribution(suppression).Succeeded, Is.True);

            BiologicalRecoveryResult suppressed = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.interrupt.suppressed", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            RecoveryProcessSnapshot pausedProcess = body.BiologicalRecovery.CreateSnapshot().Processes.FirstOrDefault(process => process.ProcessId == processId);
            Assert.That(suppressed.Succeeded, Is.False);
            Assert.That(suppressed.Code, Is.EqualTo(BiologicalRecoveryResultCode.Suppressed));
            Assert.That(pausedProcess, Is.Not.Null);
            Assert.That(pausedProcess.State, Is.EqualTo(RecoveryProcessState.Suppressed));

            Assert.That(body.BiologicalCompatibility.RemoveContribution("test.recovery", "test.recovery.suppression").Succeeded, Is.True);
            BiologicalRecoveryResult resumed = body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.interrupt.resumed", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            BiologicalRecoverySnapshot after = body.BiologicalRecovery.CreateSnapshot();
            Assert.That(resumed.Succeeded, Is.True, resumed.Message);
            Assert.That(after.Processes.Count(process => process.ProcessId == processId), Is.EqualTo(1));
            Assert.That(after.Processes.First(process => process.ProcessId == processId).State, Is.EqualTo(RecoveryProcessState.Active).Or.EqualTo(RecoveryProcessState.Completed));
        }

        [Test]
        public void TickRejectsStaleDependencyRevisionsAndReevaluatesCurrentCompatibility()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.stale");
            InjuryRecordSnapshot wound = ApplyInjury(body, "tx.recovery.stale.damage", "injury.laceration", "part.hand.left", 40);
            Assert.That(body.BiologicalRecovery.StartProcess(StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", wound.InjuryId, string.Empty, "tx.recovery.stale.start"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);

            RecoveryTickRequest staleRequest = Tick(body, "tx.recovery.stale.tick", 3600f);
            Assert.That(body.Condition.ApplyLocalizedDamage(ConditionRequest(body, "tx.recovery.stale.second-damage", "injury.blunt-trauma", "part.arm.right", 5), body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()).Succeeded, Is.True);
            BiologicalRecoveryResult stale = body.BiologicalRecovery.ApplyTick(staleRequest, body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);

            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Code, Is.EqualTo(BiologicalRecoveryResultCode.StaleDependency));
        }

        [Test]
        public void RecoverySaveRestore_IsSilentStableAndDoesNotReplayProgress()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.restore.replay");
            InjuryRecordSnapshot wound = ApplyInjury(body, "tx.recovery.restore.replay.damage", "injury.laceration", "part.hand.left", 40);
            Assert.That(body.BiologicalRecovery.StartProcess(StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", wound.InjuryId, string.Empty, "tx.recovery.restore.replay.start"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            Assert.That(body.BiologicalRecovery.ApplyTick(Tick(body, "tx.recovery.restore.replay.tick", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses).Succeeded, Is.True);
            BodySaveData saveData = body.CreateSaveData();
            int integrityAfterSave = GetStructure(body.Condition.CreateSnapshot(), "part.hand.left").CurrentIntegrity;

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.recovery.restore.replay", "person.recovery");
            int recoveryEvents = 0;
            int conditionEvents = 0;
            int vitalEvents = 0;
            restored.BiologicalRecovery.RecoveryChanged += (_, _, _) => recoveryEvents++;
            restored.Condition.ConditionChanged += (_, _, _) => conditionEvents++;
            restored.VitalProcesses.VitalResourceChanged += (_, _, _) => vitalEvents++;
            Assert.That(restored.RestoreFromSaveData(saveData, registry, "actor.runtime.recovery.restore.replay", "person.recovery", restoring: true).Succeeded, Is.True);
            Assert.That(restored.RestoreFromSaveData(saveData, registry, "actor.runtime.recovery.restore.replay", "person.recovery", restoring: true).Succeeded, Is.True);

            BodyConditionSnapshot condition = restored.Condition.CreateSnapshot();
            BiologicalRecoverySnapshot recovery = restored.BiologicalRecovery.CreateSnapshot();
            Assert.That(GetStructure(condition, "part.hand.left").CurrentIntegrity, Is.EqualTo(integrityAfterSave));
            Assert.That(recovery.ActiveProcesses.Select(process => process.ProcessId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(recovery.ActiveProcesses.Count));
            Assert.That(recoveryEvents, Is.EqualTo(0));
            Assert.That(conditionEvents, Is.EqualTo(0));
            Assert.That(vitalEvents, Is.EqualTo(0));
        }

        [Test]
        public void ReplacementBodyDoesNotInheritSavedRecoveryProcesses()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.replace.original");
            InjuryRecordSnapshot wound = ApplyInjury(body, "tx.recovery.replace.damage", "injury.laceration", "part.hand.left", 40);
            Assert.That(body.BiologicalRecovery.StartProcess(StartRequest(body, "recovery.natural.wound-closure", RecoveryTargetCategory.Injury, "part.hand.left", wound.InjuryId, string.Empty, "tx.recovery.replace.start"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            BodySaveData saveData = body.CreateSaveData();

            ActorBodyRuntime replacement = CreateHumanBody("actor.runtime.recovery.replace.new");
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, LoadRegistry(), replacement.ActorBodyId, "person.recovery", out string failureReason), Is.False);
            Assert.That(failureReason, Does.Contain("does not match"));
            Assert.That(replacement.BiologicalRecovery.CreateSnapshot().ActiveProcesses, Is.Empty);
        }

        [Test]
        public void RestContextEnablesConstructRepairAndIsPersisted()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime construct = CreateBodyRuntime(registry, "actor.runtime.recovery.construct", "person.recovery");
            Assert.That(construct.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            LocalizedStructuralDamageResult damage = construct.Condition.ApplyLocalizedDamage(ConditionRequest(construct, "tx.recovery.construct.damage", "injury.core-damage", "core.power", 40), construct.CreateAnatomySnapshot(), compatibility: construct.BiologicalCompatibility, body: construct.CreateSnapshot());
            Assert.That(damage.Succeeded, Is.True, damage.Message);

            BiologicalRecoveryResult blocked = construct.BiologicalRecovery.PreviewStartProcess(
                StartRequest(construct, "recovery.repair.construct", RecoveryTargetCategory.Injury, "core.power", damage.InjuryId, string.Empty, "tx.recovery.construct.blocked"),
                construct.CreateSnapshot(),
                construct.BiologicalCompatibility);
            BiologicalRecoveryResult rest = construct.BiologicalRecovery.SetRestContext(new RecoveryRestContextRequest
            {
                ActorBodyId = construct.ActorBodyId,
                RestType = RecoveryRestType.RepairStation,
                SourceId = "edit-mode-test",
                TransactionId = "tx.recovery.construct.rest",
                Quality = 1f
            });
            BiologicalRecoveryResult start = construct.BiologicalRecovery.StartProcess(
                StartRequest(construct, "recovery.repair.construct", RecoveryTargetCategory.Injury, "core.power", damage.InjuryId, string.Empty, "tx.recovery.construct.start"),
                construct.CreateSnapshot(),
                construct.BiologicalCompatibility);
            BodySaveData saveData = construct.CreateSaveData();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.recovery.construct", "person.recovery");
            int events = 0;
            restored.BiologicalRecovery.RecoveryChanged += (_, _, _) => events++;
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.recovery.construct", "person.recovery", restoring: true);
            BiologicalRecoverySnapshot snapshot = restored.BiologicalRecovery.CreateSnapshot();

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Code, Is.EqualTo(BiologicalRecoveryResultCode.RequirementFailed));
            Assert.That(rest.Succeeded, Is.True, rest.Message);
            Assert.That(start.Succeeded, Is.True, start.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(events, Is.EqualTo(0));
            Assert.That(snapshot.RestContext.RestType, Is.EqualTo(RecoveryRestType.RepairStation));
            Assert.That(snapshot.ActiveProcesses.Select(process => process.RecoveryMethodId), Does.Contain("recovery.repair.construct"));
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.recovery.construct", "person.recovery", out string failureReason), Is.True, failureReason);
        }

        [Test]
        public void SchemaFiveBodySaveLoadsAndInitializesRecovery()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.recovery.schema5");
            BodySaveData oldSave = body.CreateSaveData();
            oldSave.schemaVersion = 5;
            oldSave.biologicalRecovery = null;
            PlayerBodyPersistenceParticipant participant = new PlayerBodyPersistenceParticipant(body, () => registry, "person.recovery");

            UnityIsekaiGame.GameData.Persistence.PersistenceParticipantPrepareResult prepare = participant.PreparePayload(JsonUtility.ToJson(oldSave), 5);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);

            UnityIsekaiGame.GameData.Persistence.PersistenceParticipantCommitResult commit = participant.CommitPreparedPayload(prepare.PreparedPayload);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(body.BiologicalRecovery.IsReady, Is.True);
            Assert.That(body.CreateSaveData().schemaVersion, Is.EqualTo(BodySaveData.CurrentSchemaVersion));
            Assert.That(body.CreateSaveData().biologicalRecovery, Is.Not.Null);
        }

        [Test]
        public void RuntimeRecoveryCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/Recovery";
            foreach (string file in Directory.GetFiles(runtimeFolder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.Development"), file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.UI"), file);
                Assert.That(text, Does.Not.Contain("UnityEditor"), file);
                Assert.That(text, Does.Not.Contain("Prototype"), file);
            }
        }

        private ActorBodyRuntime CreateHumanBody(string actorBodyId)
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), actorBodyId, "person.recovery");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            return body;
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Biological Recovery Test Body");
            createdObjects.Add(owner);
            owner.AddComponent<CharacterAttributes>();
            owner.AddComponent<CalculatedStatCollection>();
            owner.AddComponent<CharacterTraitCollection>();
            owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            owner.AddComponent<ActorBodyRuntime>();

            CharacterAttributes attributes = owner.GetComponent<CharacterAttributes>();
            CalculatedStatCollection stats = owner.GetComponent<CalculatedStatCollection>();
            CharacterTraitCollection traits = owner.GetComponent<CharacterTraitCollection>();
            UnityIsekaiGame.Capabilities.CharacterCapabilityCollection capabilities = owner.GetComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();

            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            capabilities.Configure(registry);
            traits.Configure(registry, stats, null, capabilities, personId);
            body.Configure(registry, actorBodyId, personId, traits, stats, capabilityCollection: capabilities);
            return body;
        }

        private static RecoveryProcessStartRequest StartRequest(ActorBodyRuntime body, string methodId, RecoveryTargetCategory targetCategory, string nodeId, string injuryId, string resourceId, string transactionId)
        {
            return new RecoveryProcessStartRequest
            {
                ActorBodyId = body.ActorBodyId,
                RecoveryMethodId = methodId,
                SourceId = "edit-mode-test",
                TransactionId = transactionId,
                AuthorityContext = "Edit Mode biological recovery test",
                ExpectedBodyRevision = body.BodyRevision,
                Target = new RecoveryTargetReference
                {
                    ActorBodyId = body.ActorBodyId,
                    TargetCategory = targetCategory,
                    AnatomyNodeId = nodeId,
                    InjuryId = injuryId,
                    ResourceDefinitionId = resourceId,
                    OwningSystemRevision = targetCategory == RecoveryTargetCategory.VitalResource ? body.VitalProcesses.VitalRevision : body.Condition.ConditionRevision
                }
            };
        }

        private static RecoveryTickRequest Tick(ActorBodyRuntime body, string tickId, float seconds)
        {
            return new RecoveryTickRequest
            {
                ActorBodyId = body.ActorBodyId,
                TickId = tickId,
                ElapsedGameSeconds = seconds,
                AuthorityContext = "Edit Mode biological recovery test",
                ExpectedRecoveryRevision = body.BiologicalRecovery.RecoveryRevision,
                ExpectedBodyRevision = body.BodyRevision,
                ExpectedConditionRevision = body.Condition.ConditionRevision,
                ExpectedVitalRevision = body.VitalProcesses.VitalRevision,
                ExpectedHazardRevision = body.BiologicalHazards.HazardRevision,
                ExpectedCompatibilityRevision = body.BiologicalCompatibility.CompatibilityRevision
            };
        }

        private static BiologicalRecoveryResult PreviewStart(ActorBodyRuntime body, string methodId, RecoveryTargetCategory targetCategory, string nodeId, string injuryId, string resourceId, string transactionId)
        {
            return body.BiologicalRecovery.PreviewStartProcess(StartRequest(body, methodId, targetCategory, nodeId, injuryId, resourceId, transactionId), body.CreateSnapshot(), body.BiologicalCompatibility);
        }

        private static BiologicalRecoveryResult SetRest(ActorBodyRuntime body, RecoveryRestType restType, string transactionId)
        {
            return body.BiologicalRecovery.SetRestContext(new RecoveryRestContextRequest
            {
                ActorBodyId = body.ActorBodyId,
                RestType = restType,
                SourceId = "edit-mode-test",
                TransactionId = transactionId,
                Quality = 1f
            });
        }

        private static void AssertResourceRecovery(ActorBodyRuntime body, string methodId, string resourceId, string transactionPrefix, RecoveryRestType restType)
        {
            VitalResourceSnapshot beforeMutation = GetResource(body.VitalProcesses.CreateSnapshot(), resourceId);
            if (restType != RecoveryRestType.NotResting)
            {
                Assert.That(SetRest(body, restType, $"{transactionPrefix}.rest").Succeeded, Is.True);
            }

            Assert.That(body.VitalProcesses.ApplyMutation(VitalRequest(body, resourceId, VitalResourceMutationOperation.Consume, 20f, $"{transactionPrefix}.consume"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);
            VitalResourceSnapshot depleted = GetResource(body.VitalProcesses.CreateSnapshot(), resourceId);
            Assert.That(body.BiologicalRecovery.StartProcess(StartRequest(body, methodId, RecoveryTargetCategory.VitalResource, string.Empty, string.Empty, resourceId, $"{transactionPrefix}.start"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            BiologicalRecoveryResult tick = body.BiologicalRecovery.ApplyTick(Tick(body, $"{transactionPrefix}.tick", 3600f), body.CreateSnapshot(), body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
            VitalResourceSnapshot after = GetResource(body.VitalProcesses.CreateSnapshot(), resourceId);

            Assert.That(tick.Succeeded, Is.True, tick.Message);
            if (beforeMutation.ModelType == BiologicalResourceModelType.AccumulatingNeed)
            {
                Assert.That(after.CurrentValue, Is.LessThan(depleted.CurrentValue), resourceId);
            }
            else
            {
                Assert.That(after.CurrentValue, Is.GreaterThan(depleted.CurrentValue), resourceId);
                Assert.That(after.CurrentValue, Is.LessThanOrEqualTo(after.EffectiveMaximumValue + 0.001f), resourceId);
            }
        }

        private static InjuryRecordSnapshot ApplyInjury(ActorBodyRuntime body, string transactionId, string injuryDefinitionId, string nodeId, int structuralDamage)
        {
            LocalizedStructuralDamageResult damage = body.Condition.ApplyLocalizedDamage(ConditionRequest(body, transactionId, injuryDefinitionId, nodeId, structuralDamage), body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            Assert.That(damage.Succeeded, Is.True, damage.Message);
            InjuryRecordSnapshot injury = body.Condition.CreateSnapshot().ActiveInjuries.FirstOrDefault(candidate => candidate.InjuryId == damage.InjuryId);
            Assert.That(injury, Is.Not.Null, $"Damage result did not create expected injury '{damage.InjuryId}'.");
            return injury;
        }

        private static LocalizedStructuralDamageRequest ConditionRequest(ActorBodyRuntime body, string transactionId, string injuryDefinitionId, string nodeId, int structuralDamage)
        {
            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            return new LocalizedStructuralDamageRequest
            {
                TransactionId = transactionId,
                SourceActorBodyId = body.ActorBodyId,
                TargetActorBodyId = body.ActorBodyId,
                TargetNodeId = nodeId,
                InjuryDefinitionId = injuryDefinitionId,
                StructuralDamage = structuralDamage,
                ExpectedBodyRevision = anatomy.BodyRevision,
                ExpectedAnatomyRevision = anatomy.AnatomyRevision,
                Context = "Edit Mode biological recovery test"
            };
        }

        private static VitalResourceMutationRequest VitalRequest(ActorBodyRuntime body, string resourceId, VitalResourceMutationOperation operation, float amount, string transactionId)
        {
            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            BodyConditionSnapshot condition = body.Condition.CreateSnapshot();
            return new VitalResourceMutationRequest(
                body.ActorBodyId,
                resourceId,
                operation,
                amount,
                transactionId,
                "edit-mode-test",
                "Edit Mode biological recovery test",
                body.BodyRevision,
                anatomy.AnatomyRevision,
                condition.ConditionRevision);
        }

        private static StructureConditionSnapshot GetStructure(BodyConditionSnapshot snapshot, string nodeId)
        {
            StructureConditionSnapshot structure = snapshot.Structures.FirstOrDefault(candidate => candidate.NodeId == nodeId);
            Assert.That(structure, Is.Not.Null, $"Missing condition structure '{nodeId}'.");
            return structure;
        }

        private static VitalResourceSnapshot GetResource(VitalProcessSnapshot snapshot, string resourceId)
        {
            VitalResourceSnapshot resource = snapshot.Resources.FirstOrDefault(candidate => candidate.ResourceId == resourceId);
            Assert.That(resource, Is.Not.Null, $"Missing vital resource '{resourceId}'.");
            return resource;
        }

        private static DefinitionRegistry LoadRegistry()
        {
            return LoadCatalog().CreateRegistry();
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static void AssertResolves<TDefinition>(DefinitionRegistry registry, string id)
            where TDefinition : class, IGameDefinition
        {
            Assert.That(registry.TryGet(id, out TDefinition definition), Is.True, $"Definition '{id}' did not resolve as {typeof(TDefinition).Name}.");
            Assert.That(definition, Is.Not.Null);
        }
    }
}
