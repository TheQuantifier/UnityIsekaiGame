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
        public void DamageRepairAndItemRecoveryClosureArePersistentAndValidated()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, "durability.persist");

            ItemDurabilityOperationResult damage = fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 90f, ItemDamageChannel.Impact, "component.blade", "damage", permanent: true);
            ItemDurabilityOperationResult repair = fixture.Durability.Repair(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 20f, ItemRepairQuality.Good, "component.blade", "repair.persist");
            ItemDurabilityOperationResult recovery = fixture.Durability.MarkDestroyedByItemRecovery(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, "recovery.persist");
            ItemDurabilityRuntimeSaveData save = fixture.Durability.CreateSaveData();
            ItemDurabilityRuntime restored = new ItemDurabilityRuntime();
            ItemDurabilityOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Items, fixture.Compositions);
            ItemDurabilityRuntimeSaveData corrupt = save.Clone();
            corrupt.records[0].components.Add(corrupt.records[0].components[0].Clone());

            Assert.That(damage.Succeeded, Is.True, damage.Message);
            Assert.That(repair.Succeeded, Is.True, repair.Message);
            Assert.That(recovery.Succeeded, Is.True, recovery.Message);
            Assert.That(recovery.Snapshot.BreakageState, Is.EqualTo(ItemBreakageState.Destroyed));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetDurabilityForItem(itemId, out ItemDurabilitySnapshot restoredSnapshot), Is.True);
            Assert.That(restoredSnapshot.BreakageState, Is.EqualTo(ItemBreakageState.Destroyed));
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
            string itemId = CreateComposedItem(fixture, FindSeed(breakAtTen: true));
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);
            float healthy = fixture.Durability.GetEquipmentContributionFactor(itemId);
            fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, "component.blade", "break");

            Assert.That(healthy, Is.EqualTo(1f).Within(0.001f));
            Assert.That(fixture.Durability.GetEquipmentContributionFactor(itemId), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BreakChecksRunPerCrossedPercentAndPersistTheirOutcome()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, FindSeed(breakAtTen: true));
            ItemDurabilityOperationResult initial = fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);
            Assert.That(initial.Succeeded, Is.True, initial.Message);
            Assert.That(fixture.Policy.BreakChanceForPercent(10), Is.EqualTo(0.05f));
            Assert.That(fixture.Policy.BreakChanceForPercent(9), Is.EqualTo(0.07f));
            Assert.That(fixture.Policy.BreakChanceForPercent(8), Is.EqualTo(0.10f));
            Assert.That(fixture.Policy.BreakChanceForPercent(7), Is.EqualTo(0.14f));
            Assert.That(fixture.Policy.BreakChanceForPercent(6), Is.EqualTo(0.19f));
            Assert.That(fixture.Policy.BreakChanceForPercent(5), Is.EqualTo(0.25f));

            ItemDurabilityOperationResult breakingHit = fixture.Durability.ApplyDamage(
                fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, sourceId: "break.first-hit");

            Assert.That(breakingHit.Succeeded, Is.True, breakingHit.Message);
            Assert.That(breakingHit.Snapshot.FunctionalState, Is.EqualTo(ItemFunctionalState.Broken));
            Assert.That(breakingHit.Snapshot.BreakageState, Is.EqualTo(ItemBreakageState.Broken));
            Assert.That(breakingHit.Snapshot.NormalizedDurability, Is.EqualTo(0.10f).Within(0.00001f));
            Assert.That(breakingHit.Snapshot.LastBreakCheckPercent, Is.EqualTo(10));
            Assert.That(breakingHit.Snapshot.HasBroken, Is.True);
            Assert.That(breakingHit.Snapshot.PendingForcedDecomposition, Is.False);
            Assert.That(breakingHit.Snapshot.CurrentDurability, Is.GreaterThan(0f));

            ItemDurabilityRuntime restored = new ItemDurabilityRuntime();
            ItemDurabilityOperationResult restore = restored.RestoreFromSaveData(fixture.Durability.CreateSaveData(), fixture.Registry, fixture.Items, fixture.Compositions);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetDurabilityForItem(itemId, out ItemDurabilitySnapshot restoredSnapshot), Is.True);
            Assert.That(restoredSnapshot.HasBroken, Is.True);
            Assert.That(restoredSnapshot.LastBreakCheckPercent, Is.EqualTo(10));

            ItemDurabilityOperationResult furtherDamage = restored.ApplyDamage(
                fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, sourceId: "break.follow-up-hit");
            Assert.That(furtherDamage.Snapshot.FunctionalState, Is.EqualTo(ItemFunctionalState.Broken));
            Assert.That(furtherDamage.Snapshot.NormalizedDurability, Is.EqualTo(0.10f).Within(0.00001f));
        }

        [Test]
        public void UnbrokenItemStopsAtFivePercentAndRequestsForcedDecomposition()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, FindSeed(breakAtTen: false));
            ItemDurabilityOperationResult initial = fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);

            ItemDurabilityOperationResult result = fixture.Durability.ApplyDamage(
                fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, sourceId: "decompose.at-five");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Snapshot.HasBroken, Is.False);
            Assert.That(result.Snapshot.PendingForcedDecomposition, Is.True);
            Assert.That(result.Snapshot.NormalizedDurability, Is.EqualTo(0.05f).Within(0.00001f));
            Assert.That(result.Snapshot.LastBreakCheckPercent, Is.EqualTo(5));
        }

        [Test]
        public void SuccessfulFivePercentRollLeavesTheItemBrokenInsteadOfForcingDecomposition()
        {
            Fixture fixture = CreateFixture();
            string itemId = CreateComposedItem(fixture, FindSeedForFirstBreakAt(5));
            fixture.Durability.EnsureDefaultDurability(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId);

            ItemDurabilityOperationResult result = fixture.Durability.ApplyDamage(
                fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, sourceId: "break.at-five");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Snapshot.HasBroken, Is.True);
            Assert.That(result.Snapshot.PendingForcedDecomposition, Is.False);
            Assert.That(result.Snapshot.LastBreakCheckPercent, Is.EqualTo(5));
            Assert.That(result.Snapshot.NormalizedDurability, Is.EqualTo(0.05f).Within(0.00001f));
        }

        [Test]
        public void CatalogDegradationPolicyControlsBreakChanceAndIsRecordedOnTheItem()
        {
            Fixture fixture = CreateFixture();
            ItemBreakChanceEntryData[] authored = ItemDegradationPolicyDefinition.CreateStandardBreakChances();
            authored.Single(entry => entry.durabilityPercent == 10).breakChance = 1f;
            SetPrivate(fixture.Policy, "breakChances", authored);
            string itemId = CreateComposedItem(fixture, "durability.catalog-policy");

            ItemDurabilityOperationResult result = fixture.Durability.ApplyDamage(
                fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, itemId, 999f, ItemDamageChannel.Impact, sourceId: "policy.authored");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Snapshot.HasBroken, Is.True);
            Assert.That(result.Snapshot.LastBreakCheckPercent, Is.EqualTo(10));
            Assert.That(result.Snapshot.NormalizedDurability, Is.EqualTo(0.10f).Within(0.00001f));
            Assert.That(result.Snapshot.Data.policyId, Is.EqualTo(ItemDegradationPolicyDefinition.StandardPolicyId));
            Assert.That(fixture.Durability.DegradationPolicy, Is.SameAs(fixture.Policy));
        }

        private static string FindSeed(bool breakAtTen)
        {
            return FindSeedForFirstBreakAt(breakAtTen ? 10 : 0);
        }

        private static string FindSeedForFirstBreakAt(int expectedBreakPercent)
        {
            for (int index = 0; index < 10000; index++)
            {
                string seed = $"durability.break-roll.{expectedBreakPercent}.{index}";
                string itemId = GuidFor(seed);
                int firstBreakPercent = 0;
                long sequence = 0L;
                for (int percent = 10; percent >= 5; percent--)
                {
                    sequence++;
                    if (ItemDurabilityRuntime.DeterministicBreakRoll(itemId, percent, sequence) < ItemDegradationPolicyDefinition.StandardBreakChanceForPercent(percent))
                    {
                        firstBreakPercent = percent;
                        break;
                    }
                }

                if (firstBreakPercent == expectedBreakPercent)
                {
                    return seed;
                }
            }

            Assert.Fail("Could not find a deterministic item seed for the requested break-roll outcome.");
            return string.Empty;
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
            ItemDegradationPolicyDefinition policy = ScriptableObject.CreateInstance<ItemDegradationPolicyDefinition>();
            SetPrivate(policy, "breakChances", ItemDegradationPolicyDefinition.CreateStandardBreakChances());
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { sword, iron, policy });
            return new Fixture(sword, policy, registry, new ItemInstanceIdentityRuntime(), new ItemCompositionRuntime(), new ItemQualityAffixRuntime(), new ItemDurabilityRuntime());
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
            public Fixture(ItemDefinition sword, ItemDegradationPolicyDefinition policy, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemDurabilityRuntime durability)
            {
                Sword = sword;
                Policy = policy;
                Registry = registry;
                Items = items;
                Compositions = compositions;
                Quality = quality;
                Durability = durability;
            }

            public ItemDefinition Sword { get; }
            public ItemDegradationPolicyDefinition Policy { get; }
            public DefinitionRegistry Registry { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ItemCompositionRuntime Compositions { get; }
            public ItemQualityAffixRuntime Quality { get; }
            public ItemDurabilityRuntime Durability { get; }
        }
    }
}
