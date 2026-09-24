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
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Tests
{
    public sealed class ItemQualityAffixesTests
    {
        [Test]
        public void QualityRecordStoresWorkmanshipUnknownAndNotApplicableSeparately()
        {
            RuntimeFixture fixture = CreateFixture();
            string item = CreateComposedItem(fixture, "quality");

            ItemQualityAffixOperationResult result = fixture.Quality.SetQualityRecord(fixture.Items, fixture.Compositions, fixture.Registry, new ItemQualityRecordData
            {
                itemInstanceId = item,
                itemDefinitionId = SwordId,
                workmanship =
                {
                    new ItemWorkmanshipEntryData { entryId = "workmanship.unknown", dimension = WorkmanshipDimension.Decoration, value = new ItemQualityValueData { state = QualityValueState.Unknown, value = -1f } },
                    new ItemWorkmanshipEntryData { entryId = "workmanship.na", dimension = WorkmanshipDimension.MagicalInscription, value = new ItemQualityValueData { state = QualityValueState.NotApplicable, value = -1f } },
                    new ItemWorkmanshipEntryData { entryId = "workmanship.overall", dimension = WorkmanshipDimension.Overall, value = new ItemQualityValueData { state = QualityValueState.Known, value = 0.9f } }
                },
                dimensions =
                {
                    Dimension("quality.functional", ItemQualityDimension.Functional, 0.9f)
                }
            });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Quality.QualityTierId, Is.EqualTo("quality.masterwork"));
            Assert.That(result.Quality.Data.workmanship.Any(entry => entry.value.state == QualityValueState.Unknown), Is.True);
            Assert.That(result.Quality.Data.workmanship.Any(entry => entry.value.state == QualityValueState.NotApplicable), Is.True);
        }

        [Test]
        public void QualitySnapshotsAreImmutableAndProjectionRedactsHiddenDefectsAndAffixes()
        {
            RuntimeFixture fixture = CreateFixture();
            string item = CreateComposedItem(fixture, "redaction");
            Assert.That(fixture.Quality.SetQualityRecord(fixture.Items, fixture.Compositions, fixture.Registry, QualityRecord(item, 0.8f)).Succeeded, Is.True);
            Assert.That(fixture.Quality.AddDefect(fixture.Items, fixture.Compositions, fixture.Registry, item, new ItemDefectEntryData { defectId = "defect.visible", category = ItemDefectCategory.Dull, severity = 0.1f }).Succeeded, Is.True);
            Assert.That(fixture.Quality.AddDefect(fixture.Items, fixture.Compositions, fixture.Registry, item, new ItemDefectEntryData { defectId = "defect.hidden", category = ItemDefectCategory.HiddenDefect, severity = 0.3f, hidden = true }).Succeeded, Is.True);
            Assert.That(fixture.Quality.ApplyAffix(fixture.Items, fixture.Compositions, fixture.Registry, item, fixture.HiddenAffix, seed: "hidden").Succeeded, Is.True);

            fixture.Quality.TryGetQualityForItem(item, out ItemQualitySnapshot snapshot);
            snapshot.Data.defects.Clear();
            fixture.Quality.TryGetQualityForItem(item, out ItemQualitySnapshot reread);
            Assert.That(reread.Data.defects.Count, Is.EqualTo(2));

            InformationAccessDecision decision = RedactedDecision(item);
            ItemQualityProjection projection = fixture.Quality.Project(item, decision);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Snapshot.Data.defects.Count, Is.EqualTo(1));
            Assert.That(projection.Affixes, Is.Empty);
            Assert.That(fixture.Quality.GetAffixesForItem(item).Count, Is.EqualTo(1));
        }

        [Test]
        public void AffixGenerationIsDeterministicPreviewDoesNotMutateAndRestoreDoesNotReroll()
        {
            RuntimeFixture fixture = CreateFixture();
            string item = CreateComposedItem(fixture, "generation");
            fixture.Quality.SetQualityRecord(fixture.Items, fixture.Compositions, fixture.Registry, QualityRecord(item, 0.8f));
            ItemAffixGenerationRequest request = new ItemAffixGenerationRequest { ItemInstanceId = item, Seed = "fixed-seed", RequestedAffixCount = 1, Preview = true };

            ItemQualityAffixOperationResult preview = fixture.Quality.GenerateAffixes(fixture.Items, fixture.Compositions, fixture.Registry, request);
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(fixture.Quality.AffixCount, Is.EqualTo(0));
            request.Preview = false;
            ItemQualityAffixOperationResult execute = fixture.Quality.GenerateAffixes(fixture.Items, fixture.Compositions, fixture.Registry, request);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(preview.Affixes[0].AffixDefinitionId, Is.EqualTo(execute.Affixes[0].AffixDefinitionId));
            Assert.That(preview.Affixes[0].Data.rolledValues[0].value, Is.EqualTo(execute.Affixes[0].Data.rolledValues[0].value).Within(0.0001f));

            ItemQualityAffixRuntime restored = new ItemQualityAffixRuntime();
            Assert.That(restored.RestoreFromSaveData(fixture.Quality.CreateSaveData(), fixture.Registry, fixture.Items).Succeeded, Is.True);
            Assert.That(restored.GetAffixesForItem(item)[0].Data.rolledValues[0].value, Is.EqualTo(execute.Affixes[0].Data.rolledValues[0].value).Within(0.0001f));
        }

        [Test]
        public void AffixConflictStackEquivalenceAndModifierContributionsAreSourceSafe()
        {
            RuntimeFixture fixture = CreateFixture();
            string first = CreateComposedItem(fixture, "first");
            string second = CreateComposedItem(fixture, "second");
            fixture.Quality.SetQualityRecord(fixture.Items, fixture.Compositions, fixture.Registry, QualityRecord(first, 0.8f));
            fixture.Quality.SetQualityRecord(fixture.Items, fixture.Compositions, fixture.Registry, QualityRecord(second, 0.4f));

            ItemQualityAffixOperationResult applied = fixture.Quality.ApplyAffix(fixture.Items, fixture.Compositions, fixture.Registry, first, fixture.KeenAffix, seed: "a");
            ItemQualityAffixOperationResult conflict = fixture.Quality.ApplyAffix(fixture.Items, fixture.Compositions, fixture.Registry, first, fixture.KeenAffix, seed: "b");
            Assert.That(applied.Succeeded, Is.True, applied.Message);
            Assert.That(conflict.Succeeded, Is.False);
            Assert.That(fixture.Quality.CanShareQualityAffixStack(first, second), Is.False);

            TestRuntimeStatReceiver stats = new TestRuntimeStatReceiver(10f);
            Assert.That(fixture.Quality.ApplyActiveAffixModifiers(first, fixture.Registry, stats, out IReadOnlyList<StatModifierSource> sources).Succeeded, Is.True);
            Assert.That(stats.GetStatValue(StatType.AttackPower), Is.EqualTo(12f).Within(0.001f));
            foreach (StatModifierSource source in sources)
            {
                stats.RemoveModifiersFromSource(source);
            }
            Assert.That(stats.GetStatValue(StatType.AttackPower), Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void PersistenceRejectsCorruptAffixesBeforeCommitAndLeavesLiveStateUntouched()
        {
            RuntimeFixture fixture = CreateFixture();
            string item = CreateComposedItem(fixture, "persist");
            fixture.Quality.SetQualityRecord(fixture.Items, fixture.Compositions, fixture.Registry, QualityRecord(item, 0.8f));
            fixture.Quality.ApplyAffix(fixture.Items, fixture.Compositions, fixture.Registry, item, fixture.KeenAffix, seed: "save");

            ItemQualityAffixPersistenceParticipant participant = new ItemQualityAffixPersistenceParticipant(fixture.Quality, fixture.Items, () => fixture.Registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            Assert.That(save.Succeeded, Is.True, save.Message);

            ItemQualityAffixRuntimeSaveData corrupt = fixture.Quality.CreateSaveData();
            corrupt.affixInstances[0].affixDefinitionId = "affix.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), ItemQualityAffixPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Quality.GetAffixesForItem(item).Count, Is.EqualTo(1));
        }

        [Test]
        public void DefaultQualityCreatesOneAuthoritativeRecordWithoutAffixes()
        {
            RuntimeFixture fixture = CreateFixture();
            string item = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: ItemInstanceId.Generate()).Snapshot.ItemInstanceId;
            ItemQualityAffixOperationResult migrated = fixture.Quality.EnsureDefaultQuality(fixture.Items, fixture.Compositions, fixture.Registry, item);
            ItemQualityAffixOperationResult migratedAgain = fixture.Quality.EnsureDefaultQuality(fixture.Items, fixture.Compositions, fixture.Registry, item);

            Assert.That(migrated.Succeeded, Is.True, migrated.Message);
            Assert.That(migrated.Quality.OverallQuality, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(migratedAgain.Succeeded, Is.True);
            Assert.That(fixture.Quality.QualityRecordCount, Is.EqualTo(1));
            Assert.That(fixture.Quality.AffixCount, Is.EqualTo(0));
        }

        private const string SwordId = "item.prototype-sword";

        private static string CreateComposedItem(RuntimeFixture fixture, string slug)
        {
            string item = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: ItemInstanceId.Generate(), creationSourceId: slug).Snapshot.ItemInstanceId;
            Assert.That(fixture.Compositions.SetComposition(fixture.Items, fixture.Registry, Composition(item)).Succeeded, Is.True);
            return item;
        }

        private static ItemQualityRecordData QualityRecord(string itemInstanceId, float quality)
        {
            return new ItemQualityRecordData
            {
                itemInstanceId = itemInstanceId,
                itemDefinitionId = SwordId,
                overallQuality = quality,
                source = ItemQualityRecordSource.TestLab,
                workmanship =
                {
                    new ItemWorkmanshipEntryData { entryId = "workmanship.overall", dimension = WorkmanshipDimension.Overall, value = new ItemQualityValueData { state = QualityValueState.Known, value = quality } }
                },
                dimensions =
                {
                    Dimension("quality.functional", ItemQualityDimension.Functional, quality)
                }
            };
        }

        private static ItemQualityDimensionEntryData Dimension(string id, ItemQualityDimension dimension, float value)
        {
            return new ItemQualityDimensionEntryData
            {
                entryId = id,
                dimension = dimension,
                value = new ItemQualityValueData { state = QualityValueState.Known, value = value },
                weight = 1f
            };
        }

        private static ItemCompositionRecordData Composition(string itemInstanceId)
        {
            return new ItemCompositionRecordData
            {
                itemInstanceId = itemInstanceId,
                sourceItemDefinitionId = SwordId,
                completeness = ItemCompositionCompleteness.Complete,
                materials =
                {
                    new ItemMaterialEntryData
                    {
                        entryId = "entry.blade",
                        materialDefinitionId = "material.prototype.iron",
                        role = MaterialEntryRole.PrimaryStructure,
                        quantity = new MaterialQuantityData { value = 1f, unit = MaterialQuantityUnit.Kilogram },
                        purity = 1f
                    }
                },
                components =
                {
                    new ItemComponentEntryData { componentEntryId = "component.blade", kind = ItemComponentKind.AbstractComponent, materialEntryIds = new[] { "entry.blade" } }
                }
            };
        }

        private static RuntimeFixture CreateFixture()
        {
            ItemDefinition sword = Item(SwordId, "Prototype Sword");
            MaterialDefinition iron = Material("material.prototype.iron", MaterialCategory.Metal);
            QualityTierDefinition common = Tier("quality.common", "Common", 0.35f, 0.65f, 30);
            QualityTierDefinition fine = Tier("quality.fine", "Fine", 0.65f, 0.85f, 60);
            QualityTierDefinition masterwork = Tier("quality.masterwork", "Masterwork", 0.85f, 1f, 90);
            ItemAffixDefinition keen = Affix("affix.prototype.keen-edge", ItemAffixClassification.Prefix, hidden: false);
            ItemAffixDefinition hidden = Affix("affix.prototype.hidden-edge", ItemAffixClassification.Hidden, hidden: true);
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { sword, iron, common, fine, masterwork, keen, hidden });
            return new RuntimeFixture(sword, registry, new ItemInstanceIdentityRuntime(), new ItemCompositionRuntime(), new ItemQualityAffixRuntime(), keen, hidden);
        }

        private static ItemDefinition Item(string id, string displayName)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "itemId", id);
            SetField(item, "displayName", displayName);
            SetField(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            SetField(item, "stackable", false);
            return item;
        }

        private static MaterialDefinition Material(string id, MaterialCategory category)
        {
            MaterialDefinition material = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetField(material, "materialId", id);
            SetField(material, "displayName", id);
            SetField(material, "category", category);
            SetField(material, "physicalProperties", new MaterialPhysicalPropertySet { densityKgPerLiter = 7.8f, hardness = 0.8f, durability = 0.8f });
            return material;
        }

        private static QualityTierDefinition Tier(string id, string name, float min, float max, int order)
        {
            QualityTierDefinition tier = ScriptableObject.CreateInstance<QualityTierDefinition>();
            SetField(tier, "tierId", id);
            SetField(tier, "displayName", name);
            SetField(tier, "minimumQuality", min);
            SetField(tier, "maximumQuality", max);
            SetField(tier, "sortOrder", order);
            return tier;
        }

        private static ItemAffixDefinition Affix(string id, ItemAffixClassification classification, bool hidden)
        {
            ItemAffixDefinition definition = ScriptableObject.CreateInstance<ItemAffixDefinition>();
            SetField(definition, "affixId", id);
            SetField(definition, "displayName", id);
            SetField(definition, "classification", classification);
            SetField(definition, "hiddenByDefault", hidden);
            SetField(definition, "exclusiveGroups", new[] { "affix-group.edge" });
            SetField(definition, "rarityContribution", 0.05f);
            SetField(definition, "tiers", new[]
            {
                new ItemAffixTierData
                {
                    tierId = $"{id}.tier.fine",
                    sortOrder = 10,
                    minimumItemQuality = 0.5f,
                    maximumItemQuality = 1f,
                    valueMinimum = 1f,
                    valueMaximum = 1f,
                    rarityContribution = 0.05f,
                    modifierTemplates = new[] { StatModifier(StatType.AttackPower, StatModifierOperation.FlatAdd, 2f) }
                }
            });
            return definition;
        }

        private static StatModifierDefinition StatModifier(StatType statType, StatModifierOperation operation, float value)
        {
            StatModifierDefinition modifier = new StatModifierDefinition();
            SetField(modifier, "statType", statType);
            SetField(modifier, "operation", operation);
            SetField(modifier, "value", value);
            SetField(modifier, "scaleWithStacks", false);
            return modifier;
        }

        private static InformationAccessDecision RedactedDecision(string itemInstanceId)
        {
            return new InformationAccessDecision("person.viewer", ItemQualityAffixInformationSubject.Quality(itemInstanceId, $"item-quality.{itemInstanceId}", SwordId), InformationAccessMode.Inspect, InformationAccessDecisionKind.RedactedAccess, InformationAccessDenialCode.None, true, InformationResharingPolicy.NoResharing, Array.Empty<string>(), ItemQualityAffixInformationSubject.ProtectedFields, Array.Empty<string>(), new[] { "policy.prototype.quality" }, 0d, "Redacted", "test", false);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class TestRuntimeStatReceiver : IRuntimeStatReceiver
        {
            private readonly float baseAttackPower;
            private readonly Dictionary<StatModifierSource, List<RuntimeStatModifier>> modifiers = new Dictionary<StatModifierSource, List<RuntimeStatModifier>>();

            public TestRuntimeStatReceiver(float baseAttackPower)
            {
                this.baseAttackPower = baseAttackPower;
            }

            public bool HasStat(StatType statType) => statType == StatType.AttackPower;

            public float GetStatValue(StatType statType)
            {
                float value = statType == StatType.AttackPower ? baseAttackPower : 0f;
                foreach (List<RuntimeStatModifier> source in modifiers.Values)
                {
                    foreach (RuntimeStatModifier modifier in source)
                    {
                        if (modifier.StatType == statType && modifier.Operation == StatModifierOperation.FlatAdd)
                        {
                            value += modifier.Value;
                        }
                    }
                }

                return value;
            }

            public bool AddModifier(RuntimeStatModifier modifier)
            {
                if (!modifier.IsValid || !HasStat(modifier.StatType))
                {
                    return false;
                }

                if (!modifiers.TryGetValue(modifier.Source, out List<RuntimeStatModifier> source))
                {
                    source = new List<RuntimeStatModifier>();
                    modifiers.Add(modifier.Source, source);
                }

                source.Add(modifier);
                return true;
            }

            public bool RemoveModifiersFromSource(StatModifierSource source) => modifiers.Remove(source);
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(ItemDefinition sword, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemAffixDefinition keenAffix, ItemAffixDefinition hiddenAffix)
            {
                Sword = sword;
                Registry = registry;
                Items = items;
                Compositions = compositions;
                Quality = quality;
                KeenAffix = keenAffix;
                HiddenAffix = hiddenAffix;
            }

            public ItemDefinition Sword { get; }
            public DefinitionRegistry Registry { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ItemCompositionRuntime Compositions { get; }
            public ItemQualityAffixRuntime Quality { get; }
            public ItemAffixDefinition KeenAffix { get; }
            public ItemAffixDefinition HiddenAffix { get; }
        }
    }
}
