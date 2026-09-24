using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class BiologicalConditionTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string ViralId = "condition.biology.prototype-viral-malaise";
        private const string WoundInfectionId = "condition.biology.prototype-bacterial-wound-infection";
        private const string PoisonId = "condition.biology.prototype-poison";
        private const string FeverId = "condition.biology.prototype-fever-response";
        private const string MedicineId = "treatment.biology.prototype-medicine";
        private const string AntidoteId = "treatment.biology.prototype-antidote";
        private const string AirborneTransmissionId = "transmission.biology.prototype-viral-airborne";

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
        public void PrototypeCatalog_ResolvesBiologicalConditionsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<BiologicalConditionDefinition>(registry, ViralId);
            AssertResolves<BiologicalConditionDefinition>(registry, WoundInfectionId);
            AssertResolves<BiologicalConditionDefinition>(registry, PoisonId);
            AssertResolves<BiologicalConditionDefinition>(registry, "condition.biology.prototype-venom");
            AssertResolves<BiologicalConditionDefinition>(registry, "condition.biology.prototype-systemic-toxin");
            AssertResolves<BiologicalConditionDefinition>(registry, "condition.biology.prototype-alcohol-intoxication");
            AssertResolves<BiologicalConditionDefinition>(registry, FeverId);
            AssertResolves<BiologicalConditionTreatmentDefinition>(registry, MedicineId);
            AssertResolves<BiologicalConditionTreatmentDefinition>(registry, AntidoteId);
            AssertResolves<BiologicalTransmissionProfileDefinition>(registry, AirborneTransmissionId);
        }

        [Test]
        public void ExposurePreview_UsesRuntimeCalculationWithoutMutation()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.preview", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            BiologicalConditionRuntimeSnapshot before = body.BiologicalConditions.CreateSnapshot();

            BiologicalConditionResult result = body.BiologicalConditions.PreviewExposure(Exposure(body, "tx.condition.preview", ViralId, BiologicalExposureRoute.Inhalation, 16f), body.CreateSnapshot(), body.BiologicalCompatibility);
            BiologicalConditionRuntimeSnapshot after = body.BiologicalConditions.CreateSnapshot();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(result.EffectiveDose, Is.GreaterThan(0f));
            Assert.That(result.Snapshot.ActiveInstances.Count, Is.EqualTo(1));
            Assert.That(after.ActiveInstances, Is.Empty);
            Assert.That(after.BiologicalConditionRevision, Is.EqualTo(before.BiologicalConditionRevision));
            Assert.That(after.ProcessedTransactionIds, Is.Empty);
            Assert.That(after.IsDirty, Is.EqualTo(before.IsDirty));
        }

        [Test]
        public void ExposureExecution_EstablishesStableInstanceAndDuplicateIsIdempotent()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.duplicate", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            BiologicalConditionExposureRequest request = Exposure(body, "tx.condition.duplicate", ViralId, BiologicalExposureRoute.Inhalation, 16f);

            BiologicalConditionResult first = body.BiologicalConditions.ApplyExposure(request, body.CreateSnapshot(), body.BiologicalCompatibility);
            BiologicalConditionRuntimeSnapshot afterFirst = body.BiologicalConditions.CreateSnapshot();
            BiologicalConditionResult duplicate = body.BiologicalConditions.ApplyExposure(request, body.CreateSnapshot(), body.BiologicalCompatibility);
            BiologicalConditionRuntimeSnapshot afterDuplicate = body.BiologicalConditions.CreateSnapshot();

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.InstanceId, Does.StartWith("condition-instance.actor.runtime.biological-condition.duplicate."));
            Assert.That(afterFirst.ActiveInstances.Count, Is.EqualTo(1));
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(duplicate.Code, Is.EqualTo(BiologicalConditionResultCode.Duplicate));
            Assert.That(afterDuplicate.ActiveInstances.Count, Is.EqualTo(1));
            Assert.That(afterDuplicate.BiologicalConditionRevision, Is.EqualTo(afterFirst.BiologicalConditionRevision));
            Assert.That(afterDuplicate.ActiveInstances[0].Load, Is.EqualTo(afterFirst.ActiveInstances[0].Load).Within(0.001f));
        }

        [Test]
        public void WoundInfectionRequiresActiveCompatibleInjury()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.wound", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);

            BiologicalConditionResult missingWound = body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.no-wound", WoundInfectionId, BiologicalExposureRoute.Wound, 12f, "part.hand.left"), body.CreateSnapshot(), body.BiologicalCompatibility);
            Assert.That(missingWound.Succeeded, Is.False);
            Assert.That(missingWound.Code, Is.EqualTo(BiologicalConditionResultCode.MissingRequiredInjury));

            Assert.That(body.Condition.ApplyLocalizedDamage(Damage(body, "tx.condition.create-wound", "injury.laceration", "part.hand.left", 10), body.CreateAnatomySnapshot(), body.BiologicalCompatibility, body.CreateSnapshot()).Succeeded, Is.True);
            BiologicalConditionResult infection = body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.wound", WoundInfectionId, BiologicalExposureRoute.Wound, 12f, "part.hand.left"), body.CreateSnapshot(), body.BiologicalCompatibility);

            Assert.That(infection.Succeeded, Is.True, infection.Message);
            Assert.That(body.BiologicalConditions.CreateSnapshot().ActiveInstances[0].TargetAnatomyNodeId, Is.EqualTo("part.hand.left"));
        }

        [Test]
        public void CompatibilityRejectsSpiritDiseaseAndConstructPoisonWithoutMutation()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime spirit = CreateBodyRuntime(registry, "actor.runtime.biological-condition.spirit", "person.biological-condition");
            Assert.That(spirit.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            BiologicalConditionResult spiritDisease = spirit.BiologicalConditions.ApplyExposure(Exposure(spirit, "tx.condition.spirit-disease", ViralId, BiologicalExposureRoute.Inhalation, 16f), spirit.CreateSnapshot(), spirit.BiologicalCompatibility);
            Assert.That(spiritDisease.Succeeded, Is.False);
            Assert.That(spiritDisease.Code == BiologicalConditionResultCode.Incompatible || spiritDisease.Code == BiologicalConditionResultCode.Immune, Is.True, spiritDisease.Message);
            Assert.That(spirit.BiologicalConditions.CreateSnapshot().ActiveInstances, Is.Empty);

            ActorBodyRuntime construct = CreateBodyRuntime(registry, "actor.runtime.biological-condition.construct", "person.biological-condition");
            Assert.That(construct.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            BiologicalConditionResult constructPoison = construct.BiologicalConditions.ApplyExposure(Exposure(construct, "tx.condition.construct-poison", PoisonId, BiologicalExposureRoute.Ingestion, 12f), construct.CreateSnapshot(), construct.BiologicalCompatibility);
            Assert.That(constructPoison.Succeeded, Is.False);
            Assert.That(constructPoison.Code == BiologicalConditionResultCode.Incompatible || constructPoison.Code == BiologicalConditionResultCode.Immune, Is.True, constructPoison.Message);
            Assert.That(construct.BiologicalConditions.CreateSnapshot().ActiveInstances, Is.Empty);
        }

        [Test]
        public void TickConsequencePreviewDoesNotMutateOwningSystems()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.tick", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.fever", FeverId, BiologicalExposureRoute.Scripted, 10f), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            BodySnapshot snapshot = body.CreateSnapshot();
            float temperatureBefore = GetVital(body, BiologicalResourceIds.Temperature).CurrentValue;
            long vitalRevisionBefore = snapshot.VitalProcesses.VitalRevision;
            long hazardRevisionBefore = snapshot.BiologicalHazards.HazardRevision;
            long recoveryRevisionBefore = snapshot.BiologicalRecovery.RecoveryRevision;

            BiologicalConditionConsequenceExecutionResult preview = body.BiologicalConditions.PreviewTickConsequences(TickConsequences(body, "tx.condition.tick.preview"));

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(preview.ConditionTick.Consequences.Any(plan => plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.VitalPressure)), Is.True);
            Assert.That(preview.VitalResults.Any(result => result.Preview), Is.True);
            Assert.That(preview.HazardResults.Any(result => result.Preview), Is.True);
            Assert.That(preview.RecoveryResults.Any(result => result.Preview), Is.True);
            Assert.That(GetVital(body, BiologicalResourceIds.Temperature).CurrentValue, Is.EqualTo(temperatureBefore).Within(0.001f));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(vitalRevisionBefore));
            Assert.That(body.BiologicalHazards.HazardRevision, Is.EqualTo(hazardRevisionBefore));
            Assert.That(body.BiologicalRecovery.RecoveryRevision, Is.EqualTo(recoveryRevisionBefore));
        }

        [Test]
        public void TickConsequencesCommitThroughVitalsHazardsAndRecoveryOwners()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.consequences", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.consequence.fever", FeverId, BiologicalExposureRoute.Scripted, 10f), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            float temperatureBefore = GetVital(body, BiologicalResourceIds.Temperature).CurrentValue;

            BiologicalConditionConsequenceExecutionResult result = body.BiologicalConditions.ApplyTickConsequences(TickConsequences(body, "tx.condition.consequence.tick"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.False);
            Assert.That(result.ConditionTick.Consequences.Any(plan => plan.Flags.HasFlag(BiologicalConditionConsequenceFlags.Fever)), Is.True);
            Assert.That(GetVital(body, BiologicalResourceIds.Temperature).CurrentValue, Is.GreaterThan(temperatureBefore));
            Assert.That(result.VitalResults.Any(mutation => mutation.Succeeded && !mutation.Preview), Is.True);
            Assert.That(body.BiologicalHazards.CreateSnapshot().ActiveHazards.Any(hazard => hazard.HazardDefinitionId == BiologicalHazardIds.Overheating && hazard.Sources.Any(source => source.SourceCategory == BiologicalHazardSourceCategory.Condition)), Is.True);
            Assert.That(body.BiologicalRecovery.CreateSnapshot().RateModifiers.Any(modifier => modifier.RateMultiplier < 1f), Is.True);
        }

        [Test]
        public void DuplicateTickConsequencesDoNotApplyOwnerMutationsTwice()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.duplicate-tick", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.duplicate.fever", FeverId, BiologicalExposureRoute.Scripted, 10f), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);

            BiologicalConditionConsequenceExecutionResult first = body.BiologicalConditions.ApplyTickConsequences(TickConsequences(body, "tx.condition.duplicate.tick"));
            float temperatureAfterFirst = GetVital(body, BiologicalResourceIds.Temperature).CurrentValue;
            long vitalRevisionAfterFirst = body.VitalProcesses.VitalRevision;
            long hazardRevisionAfterFirst = body.BiologicalHazards.HazardRevision;
            long recoveryRevisionAfterFirst = body.BiologicalRecovery.RecoveryRevision;
            BiologicalConditionConsequenceExecutionResult duplicate = body.BiologicalConditions.ApplyTickConsequences(TickConsequences(body, "tx.condition.duplicate.tick"));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(GetVital(body, BiologicalResourceIds.Temperature).CurrentValue, Is.EqualTo(temperatureAfterFirst).Within(0.001f));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(vitalRevisionAfterFirst));
            Assert.That(body.BiologicalHazards.HazardRevision, Is.EqualTo(hazardRevisionAfterFirst));
            Assert.That(body.BiologicalRecovery.RecoveryRevision, Is.EqualTo(recoveryRevisionAfterFirst));
        }

        [Test]
        public void DamageConsequencesCommitThroughDamageHealingService()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.biological-condition.damage", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.poison.damage", PoisonId, BiologicalExposureRoute.Ingestion, 16f), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            CharacterResourceCollection resources = body.GetComponent<CharacterResourceCollection>();
            WorldEntityIdentity identity = body.GetComponent<WorldEntityIdentity>();
            float healthBefore = resources.GetCurrent(ResourceIds.Health);

            BiologicalConditionConsequenceExecutionResult result = body.BiologicalConditions.ApplyTickConsequences(TickConsequences(body, "tx.condition.poison.damage.tick", new DamageHealingService(), body.gameObject, identity.EntityId));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.DamageResults.Count, Is.EqualTo(1));
            Assert.That(result.DamageResults[0].Succeeded, Is.True, result.DamageResults[0].Message);
            Assert.That(result.DamageResults[0].HealthChanged, Is.True);
            Assert.That(resources.GetCurrent(ResourceIds.Health), Is.LessThan(healthBefore));
        }

        [Test]
        public void TreatmentReducesLoadAndCanCreateImmunityMemory()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.treatment", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            BiologicalConditionResult exposure = body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.treatment.expose", ViralId, BiologicalExposureRoute.Inhalation, 16f), body.CreateSnapshot(), body.BiologicalCompatibility);
            Assert.That(exposure.Succeeded, Is.True, exposure.Message);
            BiologicalConditionInstanceSnapshot before = body.BiologicalConditions.CreateSnapshot().ActiveInstances.Single();

            BiologicalConditionResult treatment = body.BiologicalConditions.ApplyTreatment(new BiologicalConditionTreatmentRequest(body.ActorBodyId, before.InstanceId, MedicineId, "tx.condition.treatment", dose: 3f, sourceId: "test.medicine"), body.CreateSnapshot(), body.BiologicalCompatibility);
            BiologicalConditionRuntimeSnapshot after = body.BiologicalConditions.CreateSnapshot();

            Assert.That(treatment.Succeeded, Is.True, treatment.Message);
            Assert.That(after.Instances.Single().Load, Is.LessThan(before.Load));
            Assert.That(after.ImmunityMemory.Any(memory => memory.ConditionDefinitionId == ViralId), Is.True);
        }

        [Test]
        public void TransmissionBuildsExposurePlanOnly()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.biological-condition.transmission", "person.biological-condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            BiologicalConditionResult exposure = body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.condition.transmission.expose", ViralId, BiologicalExposureRoute.Inhalation, 16f), body.CreateSnapshot(), body.BiologicalCompatibility);
            Assert.That(exposure.Succeeded, Is.True, exposure.Message);
            BiologicalConditionRuntimeSnapshot before = body.BiologicalConditions.CreateSnapshot();

            BiologicalConditionTransmissionPlan plan = body.BiologicalConditions.PreviewTransmission(new BiologicalConditionTransmissionRequest(body.ActorBodyId, "actor.runtime.biological-condition.target", before.ActiveInstances.Single().InstanceId, AirborneTransmissionId, "tx.condition.transmission"));
            BiologicalConditionRuntimeSnapshot after = body.BiologicalConditions.CreateSnapshot();

            Assert.That(plan.ExposureRequest, Is.Not.Null);
            Assert.That(plan.ExposureRequest.ActorBodyId, Is.EqualTo("actor.runtime.biological-condition.target"));
            Assert.That(plan.ExposureRequest.ConditionDefinitionId, Is.EqualTo(ViralId));
            Assert.That(plan.ExposureRequest.Preview, Is.True);
            Assert.That(after.BiologicalConditionRevision, Is.EqualTo(before.BiologicalConditionRevision));
            Assert.That(after.ActiveInstances.Count, Is.EqualTo(1));
        }

        [Test]
        public void SaveRestorePreservesConditionsWithoutReplay()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime original = CreateBodyRuntime(registry, "actor.runtime.biological-condition.restore", "person.biological-condition");
            Assert.That(original.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(original.BiologicalConditions.ApplyExposure(Exposure(original, "tx.condition.restore.expose", ViralId, BiologicalExposureRoute.Inhalation, 16f), original.CreateSnapshot(), original.BiologicalCompatibility).Succeeded, Is.True);
            Assert.That(original.BiologicalConditions.ApplyTick(new BiologicalConditionTickRequest(original.ActorBodyId, 600f, "tx.condition.restore.tick"), original.CreateSnapshot(), original.BiologicalCompatibility).Succeeded, Is.True);
            BodySaveData saveData = original.CreateSaveData();
            BiologicalConditionInstanceSnapshot before = original.BiologicalConditions.CreateSnapshot().ActiveInstances.Single();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.biological-condition.restore", "person.biological-condition");
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.biological-condition.restore", "person.biological-condition", restoring: true);
            BiologicalConditionRuntimeSnapshot after = restored.BiologicalConditions.CreateSnapshot();

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(after.ActiveInstances.Count, Is.EqualTo(1));
            Assert.That(after.ActiveInstances[0].InstanceId, Is.EqualTo(before.InstanceId));
            Assert.That(after.ActiveInstances[0].Family, Is.EqualTo(BiologicalConditionFamily.ViralInfection));
            Assert.That(after.ActiveInstances[0].Load, Is.EqualTo(before.Load).Within(0.001f));
            Assert.That(after.IsDirty, Is.False);
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.biological-condition.restore", "person.biological-condition", out string failure), Is.True, failure);
        }

        [Test]
        public void BodyReplacementDoesNotTransferOrdinaryBiologicalConditionsByDefault()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime infectedBody = CreateBodyRuntime(registry, "actor.runtime.biological-condition.old-body", "person.biological-condition.replacement");
            Assert.That(infectedBody.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(infectedBody.BiologicalConditions.ApplyExposure(Exposure(infectedBody, "tx.condition.replacement.expose", ViralId, BiologicalExposureRoute.Inhalation, 16f), infectedBody.CreateSnapshot(), infectedBody.BiologicalCompatibility).Succeeded, Is.True);

            ActorBodyRuntime replacementBody = CreateBodyRuntime(registry, "actor.runtime.biological-condition.new-body", "person.biological-condition.replacement");
            Assert.That(replacementBody.AssignSpecies("species.human").Succeeded, Is.True);

            Assert.That(infectedBody.BiologicalConditions.CreateSnapshot().ActiveInstances.Count, Is.EqualTo(1));
            Assert.That(replacementBody.BiologicalConditions.CreateSnapshot().ActiveInstances, Is.Empty);
        }

        [Test]
        public void RuntimeBiologicalConditionCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/BiologicalConditions";
            foreach (string file in Directory.GetFiles(runtimeFolder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.Development"), file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.UI"), file);
                Assert.That(text, Does.Not.Contain("UnityEditor"), file);
                Assert.That(text, Does.Not.Contain("PrototypeTestLab"), file);
            }
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Biological Condition Test Body");
            createdObjects.Add(owner);
            owner.AddComponent<CharacterAttributes>();
            owner.AddComponent<CalculatedStatCollection>();
            owner.AddComponent<CharacterResourceCollection>();
            owner.AddComponent<CharacterTraitCollection>();
            owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
            owner.AddComponent<ActorBodyRuntime>();

            CharacterAttributes attributes = owner.GetComponent<CharacterAttributes>();
            CalculatedStatCollection stats = owner.GetComponent<CalculatedStatCollection>();
            CharacterResourceCollection resources = owner.GetComponent<CharacterResourceCollection>();
            CharacterTraitCollection traits = owner.GetComponent<CharacterTraitCollection>();
            UnityIsekaiGame.Capabilities.CharacterCapabilityCollection capabilities = owner.GetComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();

            Assert.That(identity.TrySetAuthoredIdentity(actorBodyId.Replace('.', '-'), "scene.test", PersistenceScope.RegionOrScene, "test.biological-condition", out string identityFailure), Is.True, identityFailure);
            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            resources.Configure(registry, stats, personId);
            capabilities.Configure(registry);
            traits.Configure(registry, stats, null, capabilities, personId);
            body.Configure(registry, actorBodyId, personId, traits, stats, capabilityCollection: capabilities);
            return body;
        }

        private static BiologicalConditionConsequenceExecutionRequest TickConsequences(ActorBodyRuntime body, string transactionId, IDamageHealingService damageHealing = null, GameObject targetObject = null, string targetActorId = "")
        {
            BodySnapshot snapshot = body.CreateSnapshot();
            return new BiologicalConditionConsequenceExecutionRequest(
                new BiologicalConditionTickRequest(body.ActorBodyId, 600f, transactionId),
                snapshot,
                body.BiologicalCompatibility,
                body.VitalProcesses,
                body.BiologicalHazards,
                body.BiologicalRecovery,
                damageHealing,
                targetObject,
                targetObject,
                targetActorId,
                targetActorId);
        }

        private static VitalResourceSnapshot GetVital(ActorBodyRuntime body, string resourceId)
        {
            Assert.That(body.VitalProcesses.TryGetResource(resourceId, out VitalResourceSnapshot resource), Is.True, $"Vital resource '{resourceId}' was not configured.");
            return resource;
        }

        private static BiologicalConditionExposureRequest Exposure(ActorBodyRuntime body, string transactionId, string conditionDefinitionId, BiologicalExposureRoute route, float dose, string nodeId = "")
        {
            BodySnapshot snapshot = body.CreateSnapshot();
            return new BiologicalConditionExposureRequest(
                body.ActorBodyId,
                conditionDefinitionId,
                transactionId,
                route,
                dose,
                sourceId: "test.biological-condition",
                sourceBodyId: body.ActorBodyId,
                sourceEventId: transactionId,
                sourceCategory: BiologicalConditionSourceCategory.Development,
                targetAnatomyNodeId: nodeId,
                expectedBodyRevision: snapshot.BodyRevision,
                expectedAnatomyRevision: snapshot.Anatomy == null ? 0L : snapshot.Anatomy.AnatomyRevision,
                expectedConditionRevision: snapshot.Condition == null ? 0L : snapshot.Condition.ConditionRevision,
                expectedVitalRevision: snapshot.VitalProcesses == null ? 0L : snapshot.VitalProcesses.VitalRevision,
                expectedHazardRevision: snapshot.BiologicalHazards == null ? 0L : snapshot.BiologicalHazards.HazardRevision,
                expectedCompatibilityRevision: snapshot.BiologicalCompatibility == null ? 0L : snapshot.BiologicalCompatibility.CompatibilityRevision);
        }

        private static LocalizedStructuralDamageRequest Damage(ActorBodyRuntime body, string transactionId, string injuryDefinitionId, string nodeId, int structuralDamage)
        {
            return new LocalizedStructuralDamageRequest
            {
                TransactionId = transactionId,
                SourceActorBodyId = body.ActorBodyId,
                TargetActorBodyId = body.ActorBodyId,
                TargetNodeId = nodeId,
                InjuryDefinitionId = injuryDefinitionId,
                StructuralDamage = structuralDamage,
                ExpectedBodyRevision = body.CreateAnatomySnapshot().BodyRevision,
                ExpectedAnatomyRevision = body.CreateAnatomySnapshot().AnatomyRevision,
                Context = "Edit Mode biological condition test"
            };
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
