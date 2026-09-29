using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class ProductionToolsRequirementsTests
    {
        [Test]
        public void ProductionDefinitions_ValidateAndResolveSharedContracts()
        {
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" });
            ProductionStationDefinition forge = Station("production-station.prototype.forge", ProductionStationCategory.Forge, new[] { "station.capability.heat" }, new[] { ProductionToolRole.Primary });
            ProductionRequirementDefinition toolRequirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary, category: ProductionToolCategory.Hammering, capabilityId: "tool.capability.strike");
            ProductionRequirementDefinition stationRequirement = Requirement("production-requirement.prototype.forge", ProductionRequirementType.Station, station: forge, stationCategory: ProductionStationCategory.Forge, stationCapabilityId: "station.capability.heat");
            DefinitionValidationReport report = new DefinitionValidationReport();

            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { hammer, forge, toolRequirement, stationRequirement }, report);

            Assert.That(report.ErrorCount, Is.Zero, string.Join(Environment.NewLine, report.Messages.Select(message => message.Message)));
            Assert.That(registry.TryGet("production-tool.prototype.hammer", out ProductionToolDefinition _), Is.True);
            Assert.That(registry.TryGet("production-requirement.prototype.forge", out ProductionRequirementDefinition _), Is.True);
        }

        [Test]
        public void ExactToolStationAndAlternativeSelection_AreDeterministic()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" });
            ProductionToolDefinition mallet = Tool("production-tool.prototype.mallet", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" }, substitutesFor: new[] { hammer.Id }, priority: 5);
            ProductionStationDefinition forge = Station("production-station.prototype.forge", ProductionStationCategory.Forge, new[] { "station.capability.heat" }, new[] { ProductionToolRole.Primary });
            DefinitionRegistry registry = Registry(fixture.Sword, hammer, mallet, forge);
            string malletItem = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("100"), ownerPersonId: "person.smith").Snapshot.ItemInstanceId;
            fixture.Production.RegisterStation(forge, "station.prototype.forge.a", "location.smithy");
            ProductionRequirementDefinition toolRequirement = Requirement("production-requirement.prototype.exact-hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary, category: ProductionToolCategory.Hammering, capabilityId: "tool.capability.strike");
            ProductionRequirementDefinition stationRequirement = Requirement("production-requirement.prototype.forge", ProductionRequirementType.Station, station: forge, stationCategory: ProductionStationCategory.Forge, stationCapabilityId: "station.capability.heat");

            ProductionContextData context = new ProductionContextData
            {
                actorPersonId = "person.smith",
                locationId = "location.smithy",
                toolCandidates =
                {
                    Candidate(malletItem, mallet)
                }
            };
            ProductionRequirementEvaluationResult first = fixture.Production.EvaluateRequirements(new[] { toolRequirement, stationRequirement }, context, registry, fixture.Items, productionJobId: "job.prototype.sword");
            ProductionRequirementEvaluationResult second = fixture.Production.EvaluateRequirements(new[] { stationRequirement, toolRequirement }, context, registry, fixture.Items, productionJobId: "job.prototype.sword");

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(first.Plan.signature, Is.EqualTo(second.Plan.signature));
            Assert.That(first.Plan.selections.Any(selection => selection.selectedToolDefinitionId == mallet.Id), Is.True);
            Assert.That(first.Plan.selections.Any(selection => selection.selectedStationInstanceId == "station.prototype.forge.a"), Is.True);
        }

        [Test]
        public void PerceivedAndAuthoritativeEligibility_DoNotSelectHiddenOrFalseCandidates()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" });
            DefinitionRegistry registry = Registry(fixture.Sword, hammer);
            string perceived = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("101")).Snapshot.ItemInstanceId;
            string hidden = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("102")).Snapshot.ItemInstanceId;
            ProductionRequirementDefinition requirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);

            ProductionContextData context = new ProductionContextData
            {
                toolCandidates =
                {
                    Candidate(hidden, hammer, perceived: false),
                    Candidate(perceived, hammer)
                }
            };
            ProductionRequirementEvaluationResult result = fixture.Production.EvaluateRequirements(new[] { requirement }, context, registry, fixture.Items);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Plan.selections.Single().selectedToolItemInstanceId, Is.EqualTo(perceived));
        }

        [Test]
        public void PerceivedPlanningCanSucceedWhenAuthoritativePlanningRejects()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" });
            DefinitionRegistry registry = Registry(fixture.Sword, hammer);
            string damaged = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("107")).Snapshot.ItemInstanceId;
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, registry, damaged);
            fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, registry, damaged, 1000f);
            ProductionRequirementDefinition requirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);

            ProductionContextData perceived = new ProductionContextData
            {
                perspective = ProductionEvaluationPerspective.Perceived,
                toolCandidates = { Candidate(damaged, hammer, perceived: true, authoritative: false, durability: 1f) }
            };
            ProductionContextData authoritative = new ProductionContextData
            {
                perspective = ProductionEvaluationPerspective.Authoritative,
                toolCandidates = { Candidate(damaged, hammer, perceived: true, authoritative: false, durability: 1f) }
            };

            ProductionRequirementEvaluationResult perceivedPlan = fixture.Production.EvaluateRequirements(new[] { requirement }, perceived, registry, fixture.Items, fixture.Durability, productionJobId: "job.perceived");
            ProductionRequirementEvaluationResult authoritativePlan = fixture.Production.EvaluateRequirements(new[] { requirement }, authoritative, registry, fixture.Items, fixture.Durability, productionJobId: "job.authoritative");

            Assert.That(perceivedPlan.Succeeded, Is.True, perceivedPlan.Message);
            Assert.That(authoritativePlan.Succeeded, Is.False);
            Assert.That(authoritativePlan.Status, Is.EqualTo(ProductionRequirementEvaluationStatus.MissingTool));
        }

        [Test]
        public void ResourceItemMaterialSkillKnowledgeAndAccessRequirements_ArePlannedWithoutMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            MaterialDefinition iron = Material("material.prototype.iron", MaterialCategory.Metal);
            DefinitionRegistry registry = Registry(fixture.Sword, iron);
            ProductionRequirementDefinition skill = Requirement("production-requirement.prototype.skill", ProductionRequirementType.SkillCapability, capabilityId: "capability.production.blacksmithing");
            ProductionRequirementDefinition knowledge = Requirement("production-requirement.prototype.knowledge", ProductionRequirementType.Knowledge, knowledgeId: "fact.prototype.sword-pattern");
            ProductionRequirementDefinition resource = Requirement("production-requirement.prototype.heat", ProductionRequirementType.Resource, resourceId: "resource.production.heat", quantity: 5f);
            ProductionRequirementDefinition item = Requirement("production-requirement.prototype.blank", ProductionRequirementType.Item, item: fixture.Sword, quantity: 1f);
            ProductionRequirementDefinition material = Requirement("production-requirement.prototype.iron", ProductionRequirementType.Material, material: iron, quantity: 2f);
            ProductionRequirementDefinition access = Requirement("production-requirement.prototype.access", ProductionRequirementType.Access, accessKey: "access.workshop.private");

            ProductionContextData context = new ProductionContextData
            {
                capabilityIds = new[] { "capability.production.blacksmithing" },
                knownFactDefinitionIds = new[] { "fact.prototype.sword-pattern" },
                accessKeys = new[] { "access.workshop.private" },
                resourceQuantities = { Quantity("resource.production.heat", 5f) },
                itemQuantities = { Quantity(fixture.Sword.Id, 1f) },
                materialQuantities = { Quantity(iron.Id, 2f) }
            };
            ProductionRequirementEvaluationResult preview = fixture.Production.EvaluateRequirements(new[] { skill, knowledge, resource, item, material, access }, context, registry, fixture.Items, preview: true);

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(fixture.Production.PlanCount, Is.Zero);
            Assert.That(preview.Plan.selections.Count, Is.EqualTo(6));
            Assert.That(preview.Plan.selections.Where(selection => selection.allocations.Count > 0).SelectMany(selection => selection.allocations).All(allocation => allocation.quantity > 0f), Is.True);
        }

        [Test]
        public void KnowledgeRequirementUsesAuthoritativeKnowledgeSnapshotWhenAvailable()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionRequirementDefinition knowledge = Requirement("production-requirement.prototype.knowledge", ProductionRequirementType.Knowledge, knowledgeId: "fact.prototype.sword-pattern");
            ProductionContextData context = new ProductionContextData
            {
                actorPersonId = "person.smith",
                knowledgeSnapshot = KnowledgeSnapshot("person.smith", "fact.prototype.sword-pattern", revision: 42L)
            };

            ProductionRequirementEvaluationResult result = fixture.Production.EvaluateRequirements(new[] { knowledge }, context, Registry(), productionJobId: "job.knowledge");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Plan.selections.Single().allocations.Single().expectedRuntimeRevision, Is.EqualTo(42L));
            Assert.That(result.Plan.dependencies.Any(dependency => dependency.dependencyType == "allocation.Knowledge" && dependency.revision == 42L), Is.True);
        }

        [Test]
        public void ExactQuantityAllocationsAndReservationsPreventDoubleSpend()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionRequirementDefinition iron = Requirement("production-requirement.prototype.iron", ProductionRequirementType.Material, material: Material("material.prototype.iron", MaterialCategory.Metal), quantity: 7f);
            ProductionRequirementDefinition moreIron = Requirement("production-requirement.prototype.more-iron", ProductionRequirementType.Material, material: Material("material.prototype.iron", MaterialCategory.Metal), quantity: 5f);
            ProductionRequirementDefinition remainingIron = Requirement("production-requirement.prototype.remaining-iron", ProductionRequirementType.Material, material: Material("material.prototype.iron", MaterialCategory.Metal), quantity: 3f);
            ProductionContextData context = new ProductionContextData
            {
                materialQuantities = { Quantity("material.prototype.iron", 10f, itemInstanceId: "item-instance.iron-stack", containerId: "container.smithy", locationId: "location.smithy", revision: 5L, stackRevision: 9L) }
            };
            ProductionRequirementEvaluationResult first = fixture.Production.EvaluateRequirements(new[] { iron }, context, Registry(), productionJobId: "job.iron.a");
            ProductionReservationResult reserved = fixture.Production.ReservePlan(first.Plan.planId);
            ProductionRequirementEvaluationResult overAllocated = fixture.Production.EvaluateRequirements(new[] { moreIron }, context, Registry(), productionJobId: "job.iron.b");
            ProductionRequirementEvaluationResult remaining = fixture.Production.EvaluateRequirements(new[] { remainingIron }, context, Registry(), productionJobId: "job.iron.c");
            fixture.Production.ReleasePlanReservations(first.Plan.planId);
            ProductionRequirementEvaluationResult afterRelease = fixture.Production.EvaluateRequirements(new[] { moreIron }, context, Registry(), productionJobId: "job.iron.d");

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Plan.selections.Single().allocations.Single().itemInstanceId, Is.EqualTo("item-instance.iron-stack"));
            Assert.That(first.Plan.selections.Single().allocations.Single().sourceContainerId, Is.EqualTo("container.smithy"));
            Assert.That(reserved.Succeeded, Is.True, reserved.Message);
            Assert.That(overAllocated.Succeeded, Is.False);
            Assert.That(overAllocated.Status, Is.EqualTo(ProductionRequirementEvaluationStatus.MissingMaterial));
            Assert.That(remaining.Succeeded, Is.True, remaining.Message);
            Assert.That(afterRelease.Succeeded, Is.True, afterRelease.Message);
        }

        [Test]
        public void Reservations_PreventConflictsAndExpireDeterministically()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" });
            ProductionStationDefinition anvil = Station("production-station.prototype.anvil", ProductionStationCategory.Anvil, new[] { "station.capability.impact" }, new[] { ProductionToolRole.Primary });
            DefinitionRegistry registry = Registry(fixture.Sword, hammer, anvil);
            string hammerItem = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("103")).Snapshot.ItemInstanceId;
            fixture.Production.RegisterStation(anvil, "station.prototype.anvil.a", "location.smithy");
            ProductionRequirementDefinition toolRequirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);
            ProductionRequirementDefinition stationRequirement = Requirement("production-requirement.prototype.anvil", ProductionRequirementType.Station, station: anvil, stationCategory: ProductionStationCategory.Anvil);
            ProductionContextData context = new ProductionContextData
            {
                locationId = "location.smithy",
                worldTime = "10",
                toolCandidates = { Candidate(hammerItem, hammer) }
            };
            ProductionRequirementEvaluationResult firstPlan = fixture.Production.EvaluateRequirements(new[] { toolRequirement, stationRequirement }, context, registry, fixture.Items, productionJobId: "job.a");
            ProductionReservationResult firstReservation = fixture.Production.ReservePlan(firstPlan.Plan.planId, "15");
            ProductionRequirementEvaluationResult conflicted = fixture.Production.EvaluateRequirements(new[] { toolRequirement }, context, registry, fixture.Items, productionJobId: "job.b");
            ProductionReservationResult release = fixture.Production.ReleasePlanReservations(firstPlan.Plan.planId);
            ProductionRequirementEvaluationResult afterRelease = fixture.Production.EvaluateRequirements(new[] { toolRequirement }, context, registry, fixture.Items, productionJobId: "job.c");

            Assert.That(firstReservation.Succeeded, Is.True, firstReservation.Message);
            Assert.That(conflicted.Succeeded, Is.False);
            Assert.That(conflicted.Status, Is.EqualTo(ProductionRequirementEvaluationStatus.MissingTool));
            Assert.That(release.Succeeded, Is.True, release.Message);
            Assert.That(afterRelease.Succeeded, Is.True, afterRelease.Message);
        }

        [Test]
        public void PlanInvalidation_DetectsChangedToolDurabilityDependency()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" }, wear: 4f);
            ProductionStationDefinition anvil = Station("production-station.prototype.anvil", ProductionStationCategory.Anvil, new[] { "station.capability.impact" }, new[] { ProductionToolRole.Primary });
            DefinitionRegistry registry = Registry(fixture.Sword, hammer, anvil);
            string hammerItem = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("104")).Snapshot.ItemInstanceId;
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, registry, hammerItem);
            ProductionRequirementDefinition requirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);
            ProductionRequirementDefinition stationRequirement = Requirement("production-requirement.prototype.anvil", ProductionRequirementType.Station, station: anvil, stationCategory: ProductionStationCategory.Anvil);
            fixture.Production.RegisterStation(anvil, "station.prototype.anvil.invalidate", "location.smithy");
            ProductionContextData context = new ProductionContextData
            {
                locationId = "location.smithy",
                toolCandidates = { Candidate(hammerItem, hammer) }
            };
            ProductionRequirementEvaluationResult planned = fixture.Production.EvaluateRequirements(new[] { requirement }, context, registry, fixture.Items, fixture.Durability, productionJobId: "job.invalidate");
            ProductionRequirementEvaluationResult stationPlanned = fixture.Production.EvaluateRequirements(new[] { stationRequirement }, context, registry, fixture.Items, fixture.Durability, productionJobId: "job.invalidate.station");
            fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, registry, hammerItem, 1f);
            fixture.Production.RegisterStation(anvil, "station.prototype.anvil.invalidate", "location.reconfigured");

            ProductionRequirementEvaluationResult current = fixture.Production.ValidatePlanCurrent(planned.Plan.planId, fixture.Items, fixture.Durability);
            ProductionRequirementEvaluationResult stationCurrent = fixture.Production.ValidatePlanCurrent(stationPlanned.Plan.planId, fixture.Items, fixture.Durability);

            Assert.That(planned.Succeeded, Is.True, planned.Message);
            Assert.That(stationPlanned.Succeeded, Is.True, stationPlanned.Message);
            Assert.That(current.Succeeded, Is.False);
            Assert.That(current.Status, Is.EqualTo(ProductionRequirementEvaluationStatus.StalePlan));
            Assert.That(stationCurrent.Succeeded, Is.False);
            Assert.That(stationCurrent.Status, Is.EqualTo(ProductionRequirementEvaluationStatus.StalePlan));
        }

        [Test]
        public void ToolWearAndPersistence_RoundTripWithoutReservationDrift()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" }, wear: 3f);
            DefinitionRegistry registry = Registry(fixture.Sword, hammer);
            string hammerItem = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("105")).Snapshot.ItemInstanceId;
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, registry, hammerItem);
            ProductionRequirementDefinition requirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);
            ProductionContextData context = new ProductionContextData { toolCandidates = { Candidate(hammerItem, hammer) } };
            ProductionRequirementEvaluationResult planned = fixture.Production.EvaluateRequirements(new[] { requirement }, context, registry, fixture.Items, fixture.Durability, productionJobId: "job.persist");
            float beforeWear = fixture.Durability.TryGetDurabilityForItem(hammerItem, out ItemDurabilitySnapshot before) ? before.NormalizedDurability : -1f;
            ProductionRequirementEvaluationResult wear = fixture.Production.ApplyToolWearForPlan(planned.Plan.planId, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability, registry);
            float afterWear = fixture.Durability.TryGetDurabilityForItem(hammerItem, out ItemDurabilitySnapshot after) ? after.NormalizedDurability : -2f;
            ProductionReservationResult reservedPlan = fixture.Production.ReservePlan(planned.Plan.planId);
            ProductionRequirementRuntimeSaveData save = fixture.Production.CreateSaveData();
            ProductionRequirementRuntime restored = new ProductionRequirementRuntime();
            ProductionRequirementEvaluationResult restore = restored.RestoreFromSaveData(save);

            Assert.That(wear.Succeeded, Is.True, wear.Message);
            Assert.That(planned.Plan.selections.Single().expectedToolWear, Is.EqualTo(3f));
            Assert.That(afterWear, Is.EqualTo(beforeWear));
            Assert.That(reservedPlan.Succeeded, Is.True, reservedPlan.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.PlanCount, Is.EqualTo(1));
            Assert.That(restored.ReservationCount, Is.EqualTo(1));
            Assert.That(restored.Plans.Single().signature, Is.EqualTo(planned.Plan.signature));
        }

        [Test]
        public void PersistenceParticipant_PreparesBeforeCommitAndRollsBackOnInvalidPayload()
        {
            RuntimeFixture fixture = CreateFixture();
            ProductionToolDefinition hammer = Tool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, Array.Empty<string>());
            DefinitionRegistry registry = Registry(fixture.Sword, hammer);
            string hammerItem = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: Id("106")).Snapshot.ItemInstanceId;
            ProductionRequirementDefinition requirement = Requirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);
            fixture.Production.EvaluateRequirements(new[] { requirement }, new ProductionContextData { toolCandidates = { Candidate(hammerItem, hammer) } }, registry, fixture.Items, productionJobId: "job.participant");
            ProductionRequirementPersistenceParticipant participant = new ProductionRequirementPersistenceParticipant(fixture.Production);
            PersistenceParticipantSaveResult capture = participant.CapturePayload();
            PersistenceParticipantPrepareResult invalid = participant.PreparePayload("{\"schemaVersion\":999}", ProductionRequirementPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(capture.Succeeded, Is.True, capture.Message);
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(fixture.Production.PlanCount, Is.EqualTo(1));
        }

        private static RuntimeFixture CreateFixture()
        {
            return new RuntimeFixture();
        }

        private static DefinitionRegistry Registry(params IGameDefinition[] definitions)
        {
            return new DefinitionRegistry(definitions);
        }

        private static ProductionToolCandidateData Candidate(string itemInstanceId, ProductionToolDefinition tool, bool perceived = true, bool authoritative = true, float durability = 1f)
        {
            return new ProductionToolCandidateData
            {
                itemInstanceId = itemInstanceId,
                toolDefinitionId = tool.Id,
                role = tool.Roles.FirstOrDefault(),
                category = tool.Category,
                capabilityIds = tool.CapabilityIds.ToArray(),
                quality = 1f,
                durability = durability,
                perceived = perceived,
                authoritative = authoritative
            };
        }

        private static ProductionQuantityData Quantity(string definitionId, float quantity, string itemInstanceId = "", string containerId = "", string locationId = "", long revision = 0L, long stackRevision = 0L)
        {
            return new ProductionQuantityData
            {
                definitionId = definitionId,
                itemInstanceId = itemInstanceId,
                sourceContainerId = containerId,
                locationId = locationId,
                quantity = quantity,
                unit = ProductionQuantityUnit.Count,
                expectedRuntimeRevision = revision,
                expectedStackRevision = stackRevision,
                accessDecisionId = string.IsNullOrWhiteSpace(itemInstanceId) ? string.Empty : $"access.{itemInstanceId}",
                perceived = true,
                authoritative = true
            };
        }

        private static ProductionToolDefinition Tool(string id, ProductionToolCategory category, ProductionToolRole[] roles, string[] capabilities, string[] substitutesFor = null, int priority = 0, float wear = 0f)
        {
            ProductionToolDefinition tool = ScriptableObject.CreateInstance<ProductionToolDefinition>();
            SetPrivate(tool, "toolId", id);
            SetPrivate(tool, "displayName", id);
            SetPrivate(tool, "category", category);
            SetPrivate(tool, "roles", roles);
            SetPrivate(tool, "capabilityIds", capabilities);
            SetPrivate(tool, "substitutesForToolIds", substitutesFor ?? Array.Empty<string>());
            SetPrivate(tool, "minimumQuality", 0f);
            SetPrivate(tool, "minimumDurability", 0.01f);
            SetPrivate(tool, "durabilityWearPerUse", wear);
            SetPrivate(tool, "priority", priority);
            return tool;
        }

        private static ProductionStationDefinition Station(string id, ProductionStationCategory category, string[] capabilities, ProductionToolRole[] supportedRoles)
        {
            ProductionStationDefinition station = ScriptableObject.CreateInstance<ProductionStationDefinition>();
            SetPrivate(station, "stationId", id);
            SetPrivate(station, "displayName", id);
            SetPrivate(station, "category", category);
            SetPrivate(station, "capabilityIds", capabilities);
            SetPrivate(station, "supportedToolRoles", supportedRoles);
            SetPrivate(station, "concurrentReservationLimit", 1);
            return station;
        }

        private static ProductionRequirementDefinition Requirement(
            string id,
            ProductionRequirementType type,
            ProductionToolDefinition tool = null,
            ProductionToolRole role = ProductionToolRole.Unknown,
            ProductionToolCategory category = ProductionToolCategory.Unknown,
            string capabilityId = "",
            ProductionStationDefinition station = null,
            ProductionStationCategory stationCategory = ProductionStationCategory.Unknown,
            string stationCapabilityId = "",
            string knowledgeId = "",
            string resourceId = "",
            ItemDefinition item = null,
            MaterialDefinition material = null,
            float quantity = 1f,
            string accessKey = "")
        {
            ProductionRequirementDefinition requirement = ScriptableObject.CreateInstance<ProductionRequirementDefinition>();
            SetPrivate(requirement, "requirementId", id);
            SetPrivate(requirement, "displayName", id);
            SetPrivate(requirement, "requirementGroupId", "requirement-group.prototype");
            SetPrivate(requirement, "requirementType", type);
            SetPrivate(requirement, "strictness", ProductionRequirementStrictness.Required);
            SetPrivate(requirement, "allowSubstitution", true);
            SetPrivate(requirement, "toolDefinition", tool);
            SetPrivate(requirement, "toolRole", role);
            SetPrivate(requirement, "toolCategory", category);
            SetPrivate(requirement, "toolCapabilityId", capabilityId);
            SetPrivate(requirement, "stationDefinition", station);
            SetPrivate(requirement, "stationCategory", stationCategory);
            SetPrivate(requirement, "stationCapabilityId", stationCapabilityId);
            SetPrivate(requirement, "capabilityId", capabilityId);
            SetPrivate(requirement, "knowledgeFactDefinitionId", knowledgeId);
            SetPrivate(requirement, "resourceId", resourceId);
            SetPrivate(requirement, "itemDefinition", item);
            SetPrivate(requirement, "materialDefinition", material);
            SetPrivate(requirement, "quantity", quantity);
            SetPrivate(requirement, "accessKey", accessKey);
            return requirement;
        }

        private static MaterialDefinition Material(string id, MaterialCategory category)
        {
            MaterialDefinition material = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivate(material, "materialId", id);
            SetPrivate(material, "displayName", id);
            SetPrivate(material, "category", category);
            SetPrivate(material, "physicalProperties", new MaterialPhysicalPropertySet
            {
                densityKgPerLiter = 7f,
                hardness = 0.5f,
                durability = 0.5f,
                flexibility = 0.2f,
                conductivity = 0.1f,
                flammability = 0.1f,
                biologicalCompatibility = 0.5f
            });
            return material;
        }

        private static ItemDefinition Item(string id)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetPrivate(item, "itemId", id);
            SetPrivate(item, "displayName", id);
            SetPrivate(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            SetPrivate(item, "stackable", false);
            return item;
        }

        private static KnowledgeSnapshot KnowledgeSnapshot(string personId, string factDefinitionId, long revision)
        {
            KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            SetPrivate(definition, "factId", factDefinitionId);
            SetPrivate(definition, "displayName", factDefinitionId);
            SetPrivate(definition, "domain", KnowledgeDomain.Crafting);
            SetPrivate(definition, "propositionType", KnowledgePropositionType.Capability);
            SetPrivate(definition, "subjectType", KnowledgeSubjectType.Person);
            SetPrivate(definition, "valueType", KnowledgeValueType.Boolean);
            KnowledgeBeliefRecord belief = new KnowledgeBeliefRecord(new KnowledgeBeliefRecordData
            {
                beliefId = $"belief.{factDefinitionId}",
                personId = personId,
                proposition = new KnowledgePropositionData
                {
                    factDefinitionId = factDefinitionId,
                    subjectId = personId,
                    subjectType = KnowledgeSubjectType.Person,
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true
                },
                confidence = 1000,
                freshness = KnowledgeFreshnessState.Current,
                truthState = KnowledgeTruthState.Aligned,
                visibility = KnowledgeVisibility.Public,
                beliefRevision = revision
            }, definition);
            return new KnowledgeSnapshot(personId, string.Empty, string.Empty, revision, KnowledgeReadinessState.Ready, new[] { belief }, Array.Empty<KnowledgeEvidenceRecord>(), Array.Empty<string>());
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static string Id(string suffix)
        {
            return $"00000000-0000-0000-0000-000000000{suffix}";
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture()
            {
                Sword = Item("item.prototype-production-sword");
            }

            public ItemDefinition Sword { get; }
            public ItemInstanceIdentityRuntime Items { get; } = new ItemInstanceIdentityRuntime();
            public ItemCompositionRuntime Compositions { get; } = new ItemCompositionRuntime();
            public ItemQualityAffixRuntime Quality { get; } = new ItemQualityAffixRuntime();
            public ItemDurabilityRuntime Durability { get; } = new ItemDurabilityRuntime();
            public ProductionRequirementRuntime Production { get; } = new ProductionRequirementRuntime();
        }
    }
}
