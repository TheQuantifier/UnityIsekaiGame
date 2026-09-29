using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class MaterialsItemCompositionTests
    {
        [Test]
        public void MaterialDefinitionsValidatePhysicalProfilesAndCompositeReferences()
        {
            MaterialDefinition iron = Material("material.prototype.iron", MaterialCategory.Metal, density: 7.8f, hardness: 0.8f, durability: 0.75f);
            MaterialDefinition steel = Material("material.prototype.steel", MaterialCategory.Composite, density: 7.7f, hardness: 0.9f, durability: 0.9f);
            SetField(steel, "constituents", new[] { Constituent(iron, 1f) });
            DefinitionCatalog catalog = Catalog(iron, steel);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            DefinitionRegistry registry = catalog.CreateRegistry();
            ItemCompositionRuntime runtime = new ItemCompositionRuntime();
            Assert.That(runtime.ExpandCompositeMaterial("material.prototype.steel", registry), Does.Contain("material.prototype.iron"));
        }

        [Test]
        public void CompositionSnapshotsAreImmutableAndDoNotMutateRuntime()
        {
            RuntimeFixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            ItemCompositionOperationResult set = fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, SwordComposition(itemId, "material.prototype.iron"));
            Assert.That(set.Succeeded, Is.True, set.Message);

            Assert.That(fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot snapshot), Is.True);
            snapshot.Data.materials[0].materialDefinitionId = "material.prototype.wood";

            Assert.That(fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot reread), Is.True);
            Assert.That(reread.Materials[0].materialDefinitionId, Is.EqualTo("material.prototype.iron"));
        }

        [Test]
        public void CompositionRequiresExistingItemAndMaterialWithoutPartialMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            ItemCompositionRecordData invalid = SwordComposition(itemId, "material.prototype.missing");

            ItemCompositionOperationResult result = fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, invalid);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(ItemCompositionOperationStatus.MissingMaterial));
            Assert.That(fixture.Compositions.Count, Is.EqualTo(0));
        }

        [Test]
        public void EnsureCompositionUsesItemDefinitionTemplateOrUnknownFallback()
        {
            RuntimeFixture fixture = CreateFixture();
            ItemCompositionTemplateData template = new ItemCompositionTemplateData
            {
                completeness = ItemCompositionCompleteness.Complete,
                materials =
                {
                    MaterialEntry("entry.template.blade", "material.prototype.iron", MaterialEntryRole.PrimaryStructure, 1f, MaterialQuantityUnit.Kilogram)
                }
            };
            SetField(fixture.Sword, "defaultCompositionTemplate", template);
            string swordId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            string potionId = fixture.Items.CreateItem(fixture.Potion).Snapshot.ItemInstanceId;

            ItemCompositionOperationResult sword = fixture.Compositions.EnsureCompositionForItem(fixture.Items, fixture.Registry, swordId);
            ItemCompositionOperationResult potion = fixture.Compositions.EnsureCompositionForItem(fixture.Items, fixture.Registry, potionId);

            Assert.That(sword.Succeeded, Is.True, sword.Message);
            Assert.That(sword.Snapshot.Materials[0].materialDefinitionId, Is.EqualTo("material.prototype.iron"));
            Assert.That(sword.Snapshot.Data.revisionHistory.Count, Is.EqualTo(1));
            Assert.That(potion.Succeeded, Is.True, potion.Message);
            Assert.That(potion.Snapshot.Completeness, Is.EqualTo(ItemCompositionCompleteness.Unknown));
        }

        [Test]
        public void AtomicCreateWithRequiredCompositionLeavesNoItemBehindWhenCompositionFails()
        {
            RuntimeFixture fixture = CreateFixture();
            ItemCompositionCreationRequest request = new ItemCompositionCreationRequest
            {
                Definition = fixture.Sword,
                ItemInstanceId = ItemInstanceId.Generate(),
                RequireComposition = true,
                ExplicitComposition = SwordComposition("will-be-replaced", "material.prototype.missing"),
                Purpose = ItemCompositionMutationPurpose.AuthoredSetup
            };

            ItemCompositionCreationResult result = ItemCompositionCoordinator.CreateItem(fixture.Items, fixture.Compositions, fixture.Registry, request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Items.Count, Is.EqualTo(0));
            Assert.That(fixture.Compositions.Count, Is.EqualTo(0));
        }

        [Test]
        public void AtomicCreateCommitsItemAndDefaultCompositionTogether()
        {
            RuntimeFixture fixture = CreateFixture();
            SetField(fixture.Sword, "defaultCompositionTemplate", new ItemCompositionTemplateData
            {
                required = true,
                templateVersionId = "template.prototype.sword.v1",
                completeness = ItemCompositionCompleteness.Complete,
                materials =
                {
                    MaterialEntry("entry.template.blade", "material.prototype.iron", MaterialEntryRole.PrimaryStructure, 1f, MaterialQuantityUnit.Kilogram)
                }
            });

            ItemCompositionCreationResult result = ItemCompositionCoordinator.CreateItem(fixture.Items, fixture.Compositions, fixture.Registry, new ItemCompositionCreationRequest
            {
                Definition = fixture.Sword,
                ItemInstanceId = ItemInstanceId.Generate(),
                RequireComposition = true,
                Purpose = ItemCompositionMutationPurpose.AuthoredSetup
            });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Items.TryGetSnapshot(result.Item.ItemInstanceId, out _), Is.True);
            Assert.That(fixture.Compositions.TryGetSnapshotForItem(result.Item.ItemInstanceId, out ItemCompositionSnapshot snapshot), Is.True);
            Assert.That(snapshot.Data.templateVersionId, Is.EqualTo("template.prototype.sword.v1"));
        }

        [Test]
        public void ComponentGraphRejectsCyclesAndDestroyedTrackedComponents()
        {
            RuntimeFixture fixture = CreateFixture();
            string parent = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            string pommel = fixture.Items.CreateItem(fixture.Potion).Snapshot.ItemInstanceId;
            Assert.That(fixture.Items.DestroyOrConsume(pommel, consumed: false).Succeeded, Is.True);

            ItemCompositionRecordData destroyedComponent = SwordComposition(parent, "material.prototype.iron");
            destroyedComponent.components.Add(new ItemComponentEntryData
            {
                componentEntryId = "component.pommel",
                kind = ItemComponentKind.TrackedItemInstance,
                componentItemInstanceId = pommel
            });
            ItemCompositionOperationResult rejectedDestroyed = fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, destroyedComponent);
            Assert.That(rejectedDestroyed.Succeeded, Is.False, rejectedDestroyed.Message);

            string activePommel = fixture.Items.CreateItem(fixture.Potion).Snapshot.ItemInstanceId;
            Assert.That(fixture.Items.ReserveAsComponent(activePommel, parent, "component.b").Succeeded, Is.True);
            ItemCompositionRecordData cycle = SwordComposition(parent, "material.prototype.iron");
            cycle.components.Add(new ItemComponentEntryData { componentEntryId = "component.a", parentComponentEntryId = "component.b", kind = ItemComponentKind.AbstractComponent });
            cycle.components.Add(new ItemComponentEntryData { componentEntryId = "component.b", parentComponentEntryId = "component.a", kind = ItemComponentKind.TrackedItemInstance, componentItemInstanceId = activePommel });
            ItemCompositionOperationResult rejectedCycle = fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, cycle);
            Assert.That(rejectedCycle.Succeeded, Is.False);
            Assert.That(rejectedCycle.Status, Is.EqualTo(ItemCompositionOperationStatus.InvalidGraph));
        }

        [Test]
        public void TrackedComponentsMustBeReservedForOneParentAndCanDetachAtomically()
        {
            RuntimeFixture fixture = CreateFixture();
            string parent = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            string child = fixture.Items.CreateItem(fixture.Potion, custodianPersonId: "person.owner").Snapshot.ItemInstanceId;
            ItemCompositionRecordData invalid = SwordComposition(parent, "material.prototype.iron");
            invalid.components.Add(new ItemComponentEntryData
            {
                componentEntryId = "component.gem",
                kind = ItemComponentKind.TrackedItemInstance,
                componentItemInstanceId = child
            });

            ItemCompositionOperationResult rejected = fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, invalid);
            Assert.That(rejected.Status, Is.EqualTo(ItemCompositionOperationStatus.InvalidComponentLocation));

            ItemCompositionOperationResult attached = ItemCompositionCoordinator.AttachTrackedComponent(
                fixture.Items,
                fixture.Compositions,
                fixture.Registry,
                parent,
                child,
                new ItemComponentEntryData { componentEntryId = "component.gem" });
            Assert.That(attached.Succeeded, Is.True, attached.Message);
            Assert.That(fixture.Items.TryGetSnapshot(child, out ItemInstanceSnapshot reserved), Is.True);
            Assert.That(reserved.LocationKind, Is.EqualTo(ItemLocationKind.ProductionReserved));

            ItemCompositionOperationResult detached = ItemCompositionCoordinator.DetachTrackedComponentToInventory(
                fixture.Items,
                fixture.Compositions,
                fixture.Registry,
                parent,
                "component.gem",
                "person.owner");
            Assert.That(detached.Succeeded, Is.True, detached.Message);
            Assert.That(fixture.Items.TryGetSnapshot(child, out ItemInstanceSnapshot released), Is.True);
            Assert.That(released.LocationKind, Is.EqualTo(ItemLocationKind.Inventory));
        }

        [Test]
        public void QuantitySemanticsRejectInvalidConversionsAndOverfullProportions()
        {
            RuntimeFixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            ItemCompositionRecordData fractionalCount = SwordComposition(itemId, "material.prototype.iron");
            fractionalCount.materials[0].quantity = new MaterialQuantityData { value = 1.5f, unit = MaterialQuantityUnit.Count };
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, fractionalCount).Status, Is.EqualTo(ItemCompositionOperationStatus.InvalidQuantity));

            ItemCompositionRecordData overfull = SwordComposition(itemId, "material.prototype.iron");
            overfull.materials[0].quantity = new MaterialQuantityData { value = 70f, unit = MaterialQuantityUnit.Percent };
            overfull.materials.Add(MaterialEntry("entry.extra", "material.prototype.wood", MaterialEntryRole.Decoration, 40f, MaterialQuantityUnit.Percent));
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, overfull).Status, Is.EqualTo(ItemCompositionOperationStatus.InvalidQuantity));

            ItemCompositionRecordData volumeWithoutDensity = SwordComposition(itemId, "material.prototype.air");
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, volumeWithoutDensity).Status, Is.EqualTo(ItemCompositionOperationStatus.MissingMaterial));
        }

        [Test]
        public void CanonicalCompositionSignatureIgnoresEquivalentUnitAndInsertionOrder()
        {
            RuntimeFixture fixture = CreateFixture();
            string first = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            string second = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            ItemCompositionRecordData a = SwordComposition(first, "material.prototype.iron");
            a.materials.Add(MaterialEntry("entry.oil", "material.prototype.oil", MaterialEntryRole.Coating, 100f, MaterialQuantityUnit.Milliliter));
            ItemCompositionRecordData b = SwordComposition(second, "material.prototype.iron");
            b.materials[0].quantity = new MaterialQuantityData { value = 1200f, unit = MaterialQuantityUnit.Gram };
            b.materials.Insert(0, MaterialEntry("entry.oil", "material.prototype.oil", MaterialEntryRole.Coating, 0.1f, MaterialQuantityUnit.Liter));

            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, a).Succeeded, Is.True);
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, b).Succeeded, Is.True);

            Assert.That(fixture.Compositions.CanShareStack(first, second), Is.True);
        }

        [Test]
        public void CompositeExpansionAggregatesNestedConstituentsAndRejectsCycles()
        {
            RuntimeFixture fixture = CreateFixture(includeComposite: true);
            CompositeMaterialExpansionResult expansion = fixture.Compositions.ExpandCompositeMaterialConstituents("material.prototype.pattern-weld", fixture.Registry);

            Assert.That(expansion.Succeeded, Is.True, expansion.Message);
            Assert.That(expansion.Entries.Count(entry => entry.MaterialDefinitionId == "material.prototype.iron"), Is.EqualTo(1));
            Assert.That(expansion.Entries[0].Ratio, Is.EqualTo(1f).Within(0.0001f));

            MaterialDefinition cycleA = Material("material.prototype.cycle-a", MaterialCategory.Composite, 1f, 0.1f, 0.1f);
            MaterialDefinition cycleB = Material("material.prototype.cycle-b", MaterialCategory.Composite, 1f, 0.1f, 0.1f);
            SetField(cycleA, "constituents", new[] { Constituent(cycleB, 1f) });
            SetField(cycleB, "constituents", new[] { Constituent(cycleA, 1f) });
            DefinitionRegistry cyclicRegistry = new DefinitionRegistry(new IGameDefinition[] { cycleA, cycleB });
            CompositeMaterialExpansionResult cyclic = fixture.Compositions.ExpandCompositeMaterialConstituents("material.prototype.cycle-a", cyclicRegistry);
            Assert.That(cyclic.Succeeded, Is.False);
        }


        [Test]
        public void DerivedPropertiesAndCompatibilityResolveDeterministically()
        {
            RuntimeFixture fixture = CreateFixture(includeRule: true);
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            ItemCompositionRecordData composition = SwordComposition(itemId, "material.prototype.iron");
            composition.materials.Add(MaterialEntry("entry.oil", "material.prototype.oil", MaterialEntryRole.Coating, 100f, MaterialQuantityUnit.Milliliter));
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, composition).Succeeded, Is.True);

            fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot snapshot);
            DerivedItemMaterialProperties properties = fixture.Compositions.ComputeDerivedProperties(snapshot, fixture.Registry);
            MaterialCompatibilityEvaluation evaluation = fixture.Compositions.EvaluateCompatibility(snapshot.Materials[0], snapshot.Materials[1], fixture.Registry);

            Assert.That(properties.MaterialCount, Is.EqualTo(2));
            Assert.That(properties.KnownMassKg, Is.GreaterThan(0f));
            Assert.That(properties.GameplayMassAuthoritative, Is.False);
            Assert.That(evaluation.Outcome, Is.EqualTo(MaterialCompatibilityOutcome.Degrades));
            Assert.That(evaluation.RuleId, Is.EqualTo("material-rule.prototype.oil-on-iron"));
        }

        [Test]
        public void StackCompatibilityIncludesCompositionSignature()
        {
            RuntimeFixture fixture = CreateFixture();
            string first = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            string second = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, SwordComposition(first, "material.prototype.iron")).Succeeded, Is.True);
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, SwordComposition(second, "material.prototype.wood")).Succeeded, Is.True);

            Assert.That(fixture.Compositions.CanShareStack(first, second), Is.False);
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, SwordComposition(second, "material.prototype.iron")).Succeeded, Is.True);
            Assert.That(fixture.Compositions.CanShareStack(first, second), Is.True);
        }

        [Test]
        public void CompositionProjectionRedactsProtectedDetailsWithoutMutatingRuntime()
        {
            RuntimeFixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            ItemCompositionRecordData composition = SwordComposition(itemId, "material.prototype.iron");
            composition.materials[0].purity = 0.6f;
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, composition).Succeeded, Is.True);

            InformationAccessDecision decision = new InformationAccessDecision(
                "person.viewer",
                new InformationSubjectReferenceData(),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.None,
                sourceVisible: true,
                InformationResharingPolicy.NoResharing,
                Array.Empty<string>(),
                new[] { "material-purity" },
                Array.Empty<string>(),
                new[] { "policy.prototype" },
                0d,
                "Redacted",
                "test",
                auditRequired: false);
            ItemCompositionProjection projection = fixture.Compositions.Project(itemId, decision);
            projection.VisibleMaterials[0].purity = 0.1f;

            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Snapshot.Data.provenanceIds, Is.Empty);
            Assert.That(projection.Snapshot.Data.revisionHistory, Is.Empty);
            Assert.That(fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot reread), Is.True);
            Assert.That(reread.Materials[0].purity, Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void PersistenceRoundTripRejectsCorruptCompositionBeforeCommit()
        {
            RuntimeFixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, SwordComposition(itemId, "material.prototype.iron")).Succeeded, Is.True);

            ItemCompositionRuntimeSaveData saveData = fixture.Compositions.CreateSaveData();
            ItemCompositionRuntime restored = new ItemCompositionRuntime();
            Assert.That(restored.RestoreFromSaveData(saveData, fixture.Registry, fixture.Items).Succeeded, Is.True);
            Assert.That(restored.TryGetSnapshotForItem(itemId, out _), Is.True);

            ItemCompositionRuntimeSaveData corrupt = saveData.Clone();
            corrupt.records[0].materials[0].materialDefinitionId = "material.prototype.missing";
            ItemCompositionPersistenceParticipant participant = new ItemCompositionPersistenceParticipant(restored, fixture.Items, () => fixture.Registry);
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), ItemCompositionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot stillPresent), Is.True);
            Assert.That(stillPresent.Materials[0].materialDefinitionId, Is.EqualTo("material.prototype.iron"));
        }

        [Test]
        public void RequiredHistoryCompositionMutationReferencesItemAndRollsBackWhenHistoryFails()
        {
            RuntimeFixture fixture = CreateFixture();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            history.Configure(fixture.Registry, "world.prototype", new[] { "person.smith" });
            string itemId = fixture.Items.CreateItem(fixture.Sword, ownerPersonId: "person.smith").Snapshot.ItemInstanceId;
            ItemCompositionRecordData first = SwordComposition(itemId, "material.prototype.iron");
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, first).Succeeded, Is.True);
            ItemCompositionRecordData replacement = SwordComposition(itemId, "material.prototype.wood");

            ItemCompositionHistoryResult rejected = ItemCompositionHistoryIntegration.SetCompositionWithRequiredHistory(
                fixture.Compositions,
                fixture.Items,
                fixture.Registry,
                history,
                replacement,
                "tx.composition.rejected",
                "history-event.missing",
                "event.composition.rejected",
                "person.smith",
                ItemCompositionHistoryEventKind.CompositionCorrected,
                5d);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot afterReject), Is.True);
            Assert.That(afterReject.Materials[0].materialDefinitionId, Is.EqualTo("material.prototype.iron"));

            ItemCompositionHistoryResult accepted = ItemCompositionHistoryIntegration.SetCompositionWithRequiredHistory(
                fixture.Compositions,
                fixture.Items,
                fixture.Registry,
                history,
                replacement,
                "tx.composition.accepted",
                "history-event.person-participation",
                "event.composition.accepted",
                "person.smith",
                ItemCompositionHistoryEventKind.CompositionCorrected,
                6d);

            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.HistoryResult.Event.Payload.itemId, Is.EqualTo(itemId));
            Assert.That(accepted.HistoryResult.Event.Payload.claimValueId, Is.EqualTo(replacement.compositionId));
            Assert.That(fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot afterAccept), Is.True);
            Assert.That(afterAccept.Materials[0].materialDefinitionId, Is.EqualTo("material.prototype.wood"));
        }

        [Test]
        public void CompositionRevisionDoesNotChangeOnProjectionSaveOrRestore()
        {
            RuntimeFixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword).Snapshot.ItemInstanceId;
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, SwordComposition(itemId, "material.prototype.iron")).Succeeded, Is.True);
            Assert.That(fixture.Compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot before), Is.True);
            long runtimeRevision = fixture.Compositions.Revision;

            fixture.Compositions.Project(itemId);
            ItemCompositionRuntimeSaveData saveData = fixture.Compositions.CreateSaveData();
            ItemCompositionRuntime restored = new ItemCompositionRuntime();
            Assert.That(restored.RestoreFromSaveData(saveData, fixture.Registry, fixture.Items).Succeeded, Is.True);

            Assert.That(fixture.Compositions.Revision, Is.EqualTo(runtimeRevision));
            Assert.That(restored.Revision, Is.EqualTo(saveData.revision));
            Assert.That(restored.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot after), Is.True);
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Data.revisionHistory.Count, Is.EqualTo(before.Data.revisionHistory.Count));
        }

        private static ItemCompositionRecordData SwordComposition(string itemInstanceId, string materialId)
        {
            return new ItemCompositionRecordData
            {
                compositionId = $"item-composition.{itemInstanceId}",
                itemInstanceId = itemInstanceId,
                sourceItemDefinitionId = "item.prototype-sword",
                completeness = ItemCompositionCompleteness.Complete,
                source = "test",
                materials =
                {
                    MaterialEntry("entry.blade", materialId, MaterialEntryRole.PrimaryStructure, 1.2f, MaterialQuantityUnit.Kilogram)
                },
                components =
                {
                    new ItemComponentEntryData
                    {
                        componentEntryId = "component.blade",
                        kind = ItemComponentKind.AbstractComponent,
                        materialEntryIds = new[] { "entry.blade" }
                    }
                }
            };
        }

        private static ItemMaterialEntryData MaterialEntry(string id, string materialId, MaterialEntryRole role, float value, MaterialQuantityUnit unit)
        {
            return new ItemMaterialEntryData
            {
                entryId = id,
                materialDefinitionId = materialId,
                role = role,
                quantity = new MaterialQuantityData { value = value, unit = unit },
                purity = 1f
            };
        }

        private static RuntimeFixture CreateFixture(bool includeRule = false, bool includeComposite = false)
        {
            ItemDefinition sword = Item("item.prototype-sword", "Prototype Sword");
            ItemDefinition potion = Item("item.health-potion", "Health Potion");
            MaterialDefinition iron = Material("material.prototype.iron", MaterialCategory.Metal, density: 7.8f, hardness: 0.8f, durability: 0.75f);
            MaterialDefinition wood = Material("material.prototype.wood", MaterialCategory.Wood, density: 0.7f, hardness: 0.25f, durability: 0.45f);
            MaterialDefinition oil = Material("material.prototype.oil", MaterialCategory.Liquid, density: 0.9f, hardness: 0.02f, durability: 0.1f);
            MaterialDefinition steel = Material("material.prototype.steel", MaterialCategory.Composite, density: 7.7f, hardness: 0.9f, durability: 0.9f);
            MaterialDefinition pattern = Material("material.prototype.pattern-weld", MaterialCategory.Composite, density: 7.75f, hardness: 0.9f, durability: 0.95f);
            HistoricalEventDefinition historyEvent = HistoryEvent("history-event.person-participation");
            SetField(steel, "constituents", new[] { Constituent(iron, 1f) });
            SetField(pattern, "constituents", new[] { Constituent(steel, 0.5f), Constituent(iron, 0.5f) });
            IGameDefinition[] baseDefinitions = includeComposite
                ? new IGameDefinition[] { sword, potion, iron, wood, oil, steel, pattern, historyEvent }
                : new IGameDefinition[] { sword, potion, iron, wood, oil, historyEvent };
            if (includeRule)
            {
                MaterialCompatibilityRuleDefinition rule = ScriptableObject.CreateInstance<MaterialCompatibilityRuleDefinition>();
                SetField(rule, "ruleId", "material-rule.prototype.oil-on-iron");
                SetField(rule, "displayName", "Oil on Iron");
                SetField(rule, "sourceMaterial", iron);
                SetField(rule, "targetMaterial", oil);
                SetField(rule, "outcome", MaterialCompatibilityOutcome.Degrades);
                SetField(rule, "priority", 100);
                return new RuntimeFixture(sword, potion, new DefinitionRegistry(baseDefinitions.Concat(new IGameDefinition[] { rule })), new ItemInstanceIdentityRuntime(), new ItemCompositionRuntime());
            }

            return new RuntimeFixture(sword, potion, new DefinitionRegistry(baseDefinitions), new ItemInstanceIdentityRuntime(), new ItemCompositionRuntime());
        }

        private static ItemDefinition Item(string id, string displayName)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "itemId", id);
            SetField(item, "displayName", displayName);
            SetField(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            SetField(item, "stackable", false);
            SetField(item, "maximumStackSize", 1);
            return item;
        }

        private static MaterialDefinition Material(string id, MaterialCategory category, float density, float hardness, float durability)
        {
            MaterialDefinition material = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetField(material, "materialId", id);
            SetField(material, "displayName", id);
            SetField(material, "category", category);
            SetField(material, "physicalProperties", new MaterialPhysicalPropertySet
            {
                densityKgPerLiter = density,
                hardness = hardness,
                durability = durability,
                flexibility = 0.2f,
                conductivity = 0.2f,
                flammability = 0.1f,
                biologicalCompatibility = 0.5f
            });
            return material;
        }

        private static CompositeMaterialConstituentDefinition Constituent(MaterialDefinition material, float ratio)
        {
            CompositeMaterialConstituentDefinition constituent = new CompositeMaterialConstituentDefinition();
            SetField(constituent, "material", material);
            SetField(constituent, "ratio", ratio);
            return constituent;
        }

        private static HistoricalEventDefinition HistoryEvent(string id)
        {
            HistoricalEventDefinition definition = ScriptableObject.CreateInstance<HistoricalEventDefinition>();
            SetField(definition, "eventDefinitionId", id);
            SetField(definition, "displayName", id);
            SetField(definition, "category", HistoricalEventCategory.CustomWorldEvent);
            SetField(definition, "defaultVisibility", KnowledgeVisibility.Private);
            SetField(definition, "payloadKind", HistoricalEventPayloadKind.Generic);
            return definition;
        }

        private static DefinitionCatalog Catalog(params IGameDefinition[] definitions)
        {
            return ClassificationTestFactory.CreateCatalog(Array.ConvertAll(definitions, definition => definition as ScriptableObject));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(ItemDefinition sword, ItemDefinition potion, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions)
            {
                Sword = sword;
                Potion = potion;
                Registry = registry;
                Items = items;
                Compositions = compositions;
            }

            public ItemDefinition Sword { get; }
            public ItemDefinition Potion { get; }
            public DefinitionRegistry Registry { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ItemCompositionRuntime Compositions { get; }
        }
    }
}
