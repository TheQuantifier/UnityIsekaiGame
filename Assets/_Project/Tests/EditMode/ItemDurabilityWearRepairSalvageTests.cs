using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Tests
{
    public sealed class ItemDurabilityWearRepairSalvageTests
    {
        [Test]
        public void DefaultDurabilityUsesDedicatedAuthoritativeRecord()
        {
            Fixture fixture = CreateFixture();
            string itemId = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: GuidFor("durability.migration")).Snapshot.ItemInstanceId;
            fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, Composition(itemId));

            ItemDurabilityOperationResult result = fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Snapshot.NormalizedDurability, Is.EqualTo(1f).Within(0.001f));
            Assert.That(result.Snapshot.Data.source, Is.EqualTo(ItemDurabilityRecordSource.DefinitionDefault));
            Assert.That(result.Snapshot.CurrentDurability, Is.GreaterThan(0f));
            Assert.That(fixture.Items.TryGetSnapshot(itemId, out ItemInstanceSnapshot identity), Is.True);
            Assert.That(identity.ItemInstanceId, Is.EqualTo(itemId));
        }

        [Test]
        public void DamageRepairAndSalvageArePersistentAndValidated()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, "durability.persist");

            ItemDurabilityOperationResult damage = fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 90f, ItemDamageChannel.Impact, "component.blade", "damage", permanent: true);
            ItemDurabilityOperationResult repair = fixture.Durability.Repair(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 20f, ItemRepairQuality.Good, "component.blade", "repair.persist");
            ItemDurabilityOperationResult salvage = fixture.Durability.ExecuteSalvage(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, "salvage.persist");
            ItemDurabilityRuntimeSaveData save = fixture.Durability.CreateSaveData();
            ItemDurabilityRuntime restored = new ItemDurabilityRuntime();
            ItemDurabilityOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Items, fixture.Compositions);
            ItemDurabilityRuntimeSaveData corrupt = save.Clone();
            corrupt.records[0].components.Add(corrupt.records[0].components[0].Clone());

            Assert.That(damage.Succeeded, Is.True, damage.Message);
            Assert.That(repair.Succeeded, Is.True, repair.Message);
            Assert.That(salvage.Succeeded, Is.True, salvage.Message);
            Assert.That(salvage.SalvageOutputs.Count, Is.GreaterThan(0));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetDurabilityForItem(itemId, out ItemDurabilitySnapshot restoredSnapshot), Is.True);
            Assert.That(restoredSnapshot.Data.salvageState, Is.EqualTo(ItemSalvageState.Salvaged));
            Assert.That(ItemDurabilityRuntime.ValidateSaveData(corrupt, fixture.Registry, fixture.Items, fixture.Compositions, out _), Is.False);
        }

        [Test]
        public void AccessProjectionRedactsProtectedDurabilityDetails()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, "durability.projection");
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);
            InformationAccessDecision decision = new InformationAccessDecision(
                "person.viewer",
                ItemDurabilityInformationSubject.Create(itemId, $"item-durability.{itemId}", fixture.Sword.Id),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.None,
                true,
                InformationResharingPolicy.NoResharing,
                Array.Empty<string>(),
                ItemDurabilityInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { "policy.test.durability" },
                0d,
                "Redacted",
                "Test",
                false);

            ItemDurabilityProjection projection = fixture.Durability.Project(itemId, decision);

            Assert.That(projection.Denied, Is.False);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Snapshot.CreateInformationSubject().tags, Does.Contain(ItemDurabilityInformationSubject.DurabilitySubjectTag));
            Assert.That(projection.RedactedFields, Does.Contain("repair-history"));
        }

        [Test]
        public void BrokenDurabilityDisablesEquipmentContribution()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, "durability.equipment");
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);
            float healthy = fixture.Durability.GetEquipmentContributionFactor(itemId);
            fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, "component.blade", "break");

            Assert.That(healthy, Is.EqualTo(1f).Within(0.001f));
            Assert.That(fixture.Durability.GetEquipmentContributionFactor(itemId), Is.EqualTo(0f).Within(0.001f));
        }

        private static string CreateComposedItem(Fixture fixture, string seed)
        {
            string itemId = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: GuidFor(seed)).Snapshot.ItemInstanceId;
            fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, Composition(itemId));
            return itemId;
        }

        private static ItemCompositionRecordData Composition(string itemInstanceId)
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
                    new ItemMaterialEntryData
                    {
                        entryId = "entry.blade",
                        materialDefinitionId = "material.test.iron",
                        role = MaterialEntryRole.PrimaryStructure,
                        quantity = new MaterialQuantityData { value = 1f, unit = MaterialQuantityUnit.Kilogram },
                        purity = 1f
                    }
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

        private static Fixture CreateFixture()
        {
            ItemDefinition sword = ScriptableObject.CreateInstance<ItemDefinition>();
            SetPrivate(sword, "itemId", "item.prototype-sword");
            SetPrivate(sword, "displayName", "Prototype Sword");
            SetPrivate(sword, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            SetPrivate(sword, "stackable", false);
            MaterialDefinition iron = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivate(iron, "materialId", "material.test.iron");
            SetPrivate(iron, "displayName", "Iron");
            SetPrivate(iron, "category", MaterialCategory.Metal);
            SetPrivate(iron, "physicalProperties", new MaterialPhysicalPropertySet
            {
                densityKgPerLiter = 7.8f,
                hardness = 0.8f,
                durability = 0.75f,
                flexibility = 0.2f,
                conductivity = 0.2f,
                flammability = 0.1f,
                biologicalCompatibility = 0.5f
            });
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { sword, iron });
            return new Fixture(sword, registry, new ItemInstanceIdentityRuntime(), new ItemCompositionRuntime(), new ItemQualityAffixRuntime(), new ItemDurabilityRuntime());
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static string GuidFor(string seed)
        {
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return new Guid(bytes).ToString("D");
        }

        private sealed class Fixture
        {
            public Fixture(ItemDefinition sword, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemDurabilityRuntime durability)
            {
                Sword = sword;
                Registry = registry;
                Items = items;
                Compositions = compositions;
                Quality = quality;
                Durability = durability;
            }

            public ItemDefinition Sword { get; }
            public DefinitionRegistry Registry { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ItemCompositionRuntime Compositions { get; }
            public ItemQualityAffixRuntime Quality { get; }
            public ItemDurabilityRuntime Durability { get; }
        }
    }
}
