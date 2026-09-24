using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class DamageHealingPipelineTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCatalog_ResolvesCanonicalDamageTypes()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            string[] expected =
            {
                "damage.physical",
                "damage.physical.slashing",
                "damage.physical.piercing",
                "damage.physical.blunt",
                "damage.magic.fire",
                "damage.magic.cold",
                "damage.magic.lightning",
                "damage.magic.poison",
                "damage.magic.arcane",
                "damage.magic.holy",
                "damage.magic.necrotic",
                "damage.true"
            };

            foreach (string id in expected)
            {
                Assert.That(registry.TryGet(id, out DamageTypeDefinition damageType), Is.True, id);
                Assert.That(damageType.CanonicalAlphaDamageType, Is.True, id);
                Assert.That(damageType.ResistanceCapabilityId, Does.StartWith("capability.damage-resistance."), id);
                Assert.That(damageType.ImmunityCapabilityId, Does.StartWith("capability.damage-immunity."), id);
            }
        }

        [Test]
        public void DamagePreview_UsesExecutionMathButDoesNotMutateOrEmitEvents()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("preview", CreateDamageType("damage.physical.slashing", DamageFamily.Physical, trueDamage: false), physicalDefense: 4f);
            target.AddResistance(target.DamageType, 0.25f);
            DamageHealingService service = new DamageHealingService();
            int resourceEvents = 0;
            int damageEvents = 0;
            target.Resources.ResourceChanged += (_, _) => resourceEvents++;
            service.DamageResolved += _ => damageEvents++;

            DamageApplicationResult preview = service.PreviewDamage(target.CreateDamageRequest(20f, "tx.preview"));

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.FinalDamageAmount, Is.EqualTo(12f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(target.MaximumHealth).Within(0.001f));
            Assert.That(resourceEvents, Is.Zero);
            Assert.That(damageEvents, Is.Zero);
        }

        [Test]
        public void DamageExecution_CommitsThroughResourceApiExactlyOnce()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("execute", CreateDamageType("damage.physical.blunt", DamageFamily.Physical, trueDamage: false), physicalDefense: 3f);
            DamageHealingService service = new DamageHealingService();
            int resourceEvents = 0;
            target.Resources.ResourceChanged += (_, _) => resourceEvents++;

            DamageApplicationResult first = service.ApplyDamage(target.CreateDamageRequest(20f, "tx.damage.once"));
            DamageApplicationResult duplicate = service.ApplyDamage(target.CreateDamageRequest(20f, "tx.damage.once"));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.HealthChanged, Is.True);
            Assert.That(first.FinalDamageAmount, Is.EqualTo(17f).Within(0.001f));
            Assert.That(resourceEvents, Is.EqualTo(1));
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(duplicate.HealthChanged, Is.False);
            Assert.That(duplicate.ResourceResult.DuplicateEvent, Is.True);
            Assert.That(resourceEvents, Is.EqualTo(1), "Duplicate resource transactions must not emit a second resource mutation event.");
        }

        [Test]
        public void DamageExecution_RevalidatesStaleTargetIdentity()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("stale", CreateDamageType("damage.physical", DamageFamily.Physical, trueDamage: false));
            DamageHealingService service = new DamageHealingService();

            DamageApplicationRequest request = new DamageApplicationRequest("tx.stale", string.Empty, null, "entity.scene.test.old-target", target.Owner, target.DamageType, 10f, "stale target", authorityValidated: true);
            DamageApplicationResult result = service.ApplyDamage(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ImmediateCombatResultCode.StaleTarget));
            Assert.That(target.Health, Is.EqualTo(target.MaximumHealth).Within(0.001f));
        }

        [Test]
        public void DamageExecution_RejectsUnvalidatedAuthorityWithoutMutation()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("authority", CreateDamageType("damage.physical", DamageFamily.Physical, trueDamage: false));
            DamageApplicationRequest request = new DamageApplicationRequest("tx.authority", "actor.test-source", null, target.ActorId, target.Owner, target.DamageType, 10f, "authority test");

            DamageApplicationResult result = new DamageHealingService().ApplyDamage(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ImmediateCombatResultCode.AuthorityRequired));
            Assert.That(target.Health, Is.EqualTo(target.MaximumHealth).Within(0.001f));
        }

        [Test]
        public void DamageExecution_RejectsMissingTargetAndInvalidAmounts()
        {
            DamageHealingService service = new DamageHealingService();
            DamageTypeDefinition damageType = CreateDamageType("damage.physical", DamageFamily.Physical, trueDamage: false);

            DamageApplicationResult missing = service.ApplyDamage(new DamageApplicationRequest("tx.missing", string.Empty, null, string.Empty, null, damageType, 10f, authorityValidated: true));
            DamageApplicationResult negative = service.ApplyDamage(new DamageApplicationRequest("tx.negative", string.Empty, null, string.Empty, new GameObject("unused"), damageType, -1f, authorityValidated: true));

            Assert.That(missing.Code, Is.EqualTo(ImmediateCombatResultCode.MissingTarget));
            Assert.That(negative.Code, Is.EqualTo(ImmediateCombatResultCode.InvalidRequest));
            UnityEngine.Object.DestroyImmediate(damageType);
            UnityEngine.Object.DestroyImmediate(negative.Request.TargetObject);
        }

        [Test]
        public void DamageExecution_AppliesPhysicalAndMagicalDefenseByFamily()
        {
            using CombatTargetFixture physical = CombatTargetFixture.Create("physical-defense", CreateDamageType("damage.physical.piercing", DamageFamily.Physical, trueDamage: false), physicalDefense: 5f, magicalDefense: 99f);
            using CombatTargetFixture magical = CombatTargetFixture.Create("magical-defense", CreateDamageType("damage.magic.fire", DamageFamily.Elemental, trueDamage: false), physicalDefense: 99f, magicalDefense: 7f);
            DamageHealingService service = new DamageHealingService();

            DamageApplicationResult physicalResult = service.ApplyDamage(physical.CreateDamageRequest(20f, "tx.physical-defense"));
            DamageApplicationResult magicalResult = service.ApplyDamage(magical.CreateDamageRequest(20f, "tx.magical-defense"));

            Assert.That(physicalResult.FinalDamageAmount, Is.EqualTo(15f).Within(0.001f));
            Assert.That(physicalResult.DefenseApplied, Is.EqualTo(5f).Within(0.001f));
            Assert.That(magicalResult.FinalDamageAmount, Is.EqualTo(13f).Within(0.001f));
            Assert.That(magicalResult.DefenseApplied, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void DamageExecution_AppliesResistanceAfterDefense()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("resistance", CreateDamageType("damage.magic.arcane", DamageFamily.Magical, trueDamage: false), magicalDefense: 4f);
            target.AddResistance(target.DamageType, 0.25f);

            DamageApplicationResult result = new DamageHealingService().ApplyDamage(target.CreateDamageRequest(20f, "tx.resistance"));

            Assert.That(result.DefenseMitigatedAmount, Is.EqualTo(4f).Within(0.001f));
            Assert.That(result.ResistanceFraction, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(result.ResistanceMitigatedAmount, Is.EqualTo(4f).Within(0.001f));
            Assert.That(result.FinalDamageAmount, Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void DamageExecution_ImmunityPreventsDamageWithoutHealthMutation()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("immune", CreateDamageType("damage.magic.cold", DamageFamily.Elemental, trueDamage: false), magicalDefense: 3f);
            target.AddImmunity(target.DamageType);
            DamageHealingService service = new DamageHealingService();
            int resourceEvents = 0;
            int preventedEvents = 0;
            target.Resources.ResourceChanged += (_, _) => resourceEvents++;
            service.DamagePrevented += _ => preventedEvents++;

            DamageApplicationResult result = service.ApplyDamage(target.CreateDamageRequest(20f, "tx.immune"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Immune, Is.True);
            Assert.That(result.FinalDamageAmount, Is.Zero);
            Assert.That(result.HealthChanged, Is.False);
            Assert.That(resourceEvents, Is.Zero);
            Assert.That(preventedEvents, Is.EqualTo(1));
            Assert.That(target.Health, Is.EqualTo(target.MaximumHealth).Within(0.001f));
        }

        [Test]
        public void TrueDamage_IgnoresDefenseResistanceAndImmunity()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("true", CreateDamageType("damage.true", DamageFamily.True, trueDamage: true), physicalDefense: 100f, magicalDefense: 100f);
            target.AddResistance(target.DamageType, 1f);
            target.AddImmunity(target.DamageType);

            DamageApplicationResult result = new DamageHealingService().ApplyDamage(target.CreateDamageRequest(20f, "tx.true"));

            Assert.That(result.TrueDamage, Is.True);
            Assert.That(result.Immune, Is.False);
            Assert.That(result.DefenseApplied, Is.Zero);
            Assert.That(result.ResistanceFraction, Is.Zero);
            Assert.That(result.FinalDamageAmount, Is.EqualTo(20f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(target.MaximumHealth - 20f).Within(0.001f));
        }

        [Test]
        public void DamageExecution_ReportsOverkillAndBecameZero()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("overkill", CreateDamageType("damage.physical", DamageFamily.Physical, trueDamage: false));

            DamageApplicationResult result = new DamageHealingService().ApplyDamage(target.CreateDamageRequest(target.MaximumHealth + 50f, "tx.overkill"));

            Assert.That(result.BecameZero, Is.True);
            Assert.That(result.OverkillAmount, Is.EqualTo(50f).Within(0.001f));
            Assert.That(target.Health, Is.Zero.Within(0.001f));
        }

        [Test]
        public void HealingPreview_DoesNotMutateOrEmitEvents()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("healing-preview", CreateDamageType("damage.physical", DamageFamily.Physical, trueDamage: false));
            target.Resources.ApplyDamage(ResourceIds.Health, 30f, "test", "setup");
            DamageHealingService service = new DamageHealingService();
            int resourceEvents = 0;
            int healingEvents = 0;
            target.Resources.ResourceChanged += (_, _) => resourceEvents++;
            service.HealingResolved += _ => healingEvents++;

            HealingApplicationResult preview = service.PreviewHealing(target.CreateHealingRequest(20f, "tx.heal.preview"));

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.FinalHealingAmount, Is.EqualTo(20f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(target.MaximumHealth - 30f).Within(0.001f));
            Assert.That(resourceEvents, Is.Zero);
            Assert.That(healingEvents, Is.Zero);
        }

        [Test]
        public void HealingExecution_ClampsOverhealAndUsesResourceDuplicateProtection()
        {
            using CombatTargetFixture target = CombatTargetFixture.Create("healing", CreateDamageType("damage.physical", DamageFamily.Physical, trueDamage: false));
            target.Resources.ApplyDamage(ResourceIds.Health, 10f, "test", "setup");
            DamageHealingService service = new DamageHealingService();
            int resourceEvents = 0;
            target.Resources.ResourceChanged += (_, _) => resourceEvents++;

            HealingApplicationResult first = service.ApplyHealing(target.CreateHealingRequest(999f, "tx.heal.once"));
            HealingApplicationResult duplicate = service.ApplyHealing(target.CreateHealingRequest(999f, "tx.heal.once"));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.FinalHealingAmount, Is.EqualTo(10f).Within(0.001f));
            Assert.That(first.OverhealAmount, Is.EqualTo(989f).Within(0.001f));
            Assert.That(first.BecameFull, Is.True);
            Assert.That(resourceEvents, Is.EqualTo(1));
            Assert.That(duplicate.Duplicate, Is.False, "A no-op full-health heal does not reach Resource API, so no transaction is consumed.");
            Assert.That(duplicate.HealthChanged, Is.False);
            Assert.That(resourceEvents, Is.EqualTo(1));
        }

        [Test]
        public void DefinitionValidation_AcceptsTrueDamageConfiguration()
        {
            DamageTypeDefinition trueDamage = CreateDamageType("damage.true", DamageFamily.True, trueDamage: true);
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(trueDamage));

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(trueDamage.IsTrueDamage, Is.True);
            Assert.That(trueDamage.GeneralDefenseApplies, Is.False);
            UnityEngine.Object.DestroyImmediate(trueDamage);
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static DamageTypeDefinition CreateDamageType(string id, DamageFamily family, bool trueDamage)
        {
            DamageTypeDefinition damageType = ScriptableObject.CreateInstance<DamageTypeDefinition>();
            SerializedObject serialized = new SerializedObject(damageType);
            serialized.FindProperty("damageTypeId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("family").enumValueIndex = (int)family;
            serialized.FindProperty("generalDefenseApplies").boolValue = !trueDamage;
            serialized.FindProperty("enforceMinimumDamage").boolValue = !trueDamage;
            serialized.FindProperty("minimumDamage").floatValue = trueDamage ? 0f : 1f;
            serialized.FindProperty("trueDamage").boolValue = trueDamage;
            serialized.FindProperty("canonicalAlphaDamageType").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return damageType;
        }

        private static CapabilityDefinition CreateCapability(string id, CapabilityValueType valueType)
        {
            CapabilityDefinition capability = ScriptableObject.CreateInstance<CapabilityDefinition>();
            SerializedObject serialized = new SerializedObject(capability);
            serialized.FindProperty("capabilityId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("valueType").enumValueIndex = (int)valueType;
            serialized.FindProperty("aggregationPolicy").enumValueIndex = valueType == CapabilityValueType.Boolean
                ? (int)CapabilityAggregationPolicy.BooleanAny
                : (int)CapabilityAggregationPolicy.Sum;
            serialized.FindProperty("minimumValue").floatValue = 0f;
            serialized.FindProperty("maximumValue").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return capability;
        }

        private sealed class CombatTargetFixture : IDisposable
        {
            private DamageTypeDefinition damageType;
            private CapabilityDefinition resistanceCapability;
            private CapabilityDefinition immunityCapability;

            public GameObject Owner { get; private set; }
            public CharacterResourceCollection Resources { get; private set; }
            public CharacterTraitCollection Traits { get; private set; }
            public CharacterCapabilityCollection Capabilities { get; private set; }
            public DamageTypeDefinition DamageType => damageType;
            public string ActorId { get; private set; }
            public float Health => Resources.GetCurrent(ResourceIds.Health);
            public float MaximumHealth => Resources.GetMaximum(ResourceIds.Health);

            public static CombatTargetFixture Create(string name, DamageTypeDefinition damageType, float physicalDefense = 0f, float magicalDefense = 0f)
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                CombatTargetFixture fixture = new CombatTargetFixture
                {
                    damageType = damageType,
                    resistanceCapability = CreateCapability(damageType.ResistanceCapabilityId, CapabilityValueType.Numeric),
                    immunityCapability = CreateCapability(damageType.ImmunityCapabilityId, CapabilityValueType.Boolean),
                    Owner = new GameObject($"Damage Healing {name}")
                };

                CharacterAttributes attributes = fixture.Owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = fixture.Owner.AddComponent<CalculatedStatCollection>();
                fixture.Resources = fixture.Owner.AddComponent<CharacterResourceCollection>();
                fixture.Traits = fixture.Owner.AddComponent<CharacterTraitCollection>();
                fixture.Capabilities = fixture.Owner.AddComponent<CharacterCapabilityCollection>();
                WorldEntityIdentity identity = fixture.Owner.AddComponent<WorldEntityIdentity>();
                string localId = $"damage-healing-{name}-{Guid.NewGuid():N}";
                Assert.That(identity.TrySetAuthoredIdentity(localId, "scene.test", PersistenceScope.RegionOrScene, "test.damage-healing", out string identityFailure), Is.True, identityFailure);
                fixture.ActorId = identity.EntityId;

                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                fixture.Resources.Configure(registry, stats, "player.local");
                fixture.Capabilities.Configure(new[] { fixture.resistanceCapability, fixture.immunityCapability });
                fixture.Traits.Configure(Array.Empty<TraitDefinition>(), stats, null, fixture.Capabilities, "player.local");
                AddStat(stats, CalculatedStatIds.PhysicalDefense, physicalDefense, $"{name}.physical-defense");
                AddStat(stats, CalculatedStatIds.MagicalDefense, magicalDefense, $"{name}.magical-defense");
                return fixture;
            }

            public DamageApplicationRequest CreateDamageRequest(float amount, string transactionId)
            {
                return new DamageApplicationRequest(transactionId, "actor.test-source", null, ActorId, Owner, damageType, amount, "test", authorityValidated: true);
            }

            public HealingApplicationRequest CreateHealingRequest(float amount, string transactionId)
            {
                return new HealingApplicationRequest(transactionId, "actor.test-source", null, ActorId, Owner, amount, "test", authorityValidated: true);
            }

            public void AddResistance(DamageTypeDefinition targetDamageType, float resistance)
            {
                Capabilities.Add(new RuntimeCapabilityContribution
                {
                    capabilityId = targetDamageType.ResistanceCapabilityId,
                    valueType = (int)CapabilityValueType.Numeric,
                    numericValue = resistance,
                    aggregationPolicy = (int)CapabilityAggregationPolicy.Sum,
                    sourceCategory = (int)CapabilitySourceCategory.Trait,
                    sourceId = "test.resistance",
                    entryId = Guid.NewGuid().ToString("N")
                });
            }

            public void AddImmunity(DamageTypeDefinition targetDamageType)
            {
                Capabilities.Add(new RuntimeCapabilityContribution
                {
                    capabilityId = targetDamageType.ImmunityCapabilityId,
                    valueType = (int)CapabilityValueType.Boolean,
                    boolValue = true,
                    aggregationPolicy = (int)CapabilityAggregationPolicy.BooleanAny,
                    sourceCategory = (int)CapabilitySourceCategory.Trait,
                    sourceId = "test.immunity",
                    entryId = Guid.NewGuid().ToString("N")
                });
            }

            public void Dispose()
            {
                if (Owner != null)
                {
                    UnityEngine.Object.DestroyImmediate(Owner);
                }

                if (damageType != null)
                {
                    UnityEngine.Object.DestroyImmediate(damageType);
                }

                if (resistanceCapability != null)
                {
                    UnityEngine.Object.DestroyImmediate(resistanceCapability);
                }

                if (immunityCapability != null)
                {
                    UnityEngine.Object.DestroyImmediate(immunityCapability);
                }
            }

            private static void AddStat(CalculatedStatCollection stats, string statId, float amount, string sourceId)
            {
                if (amount <= 0f)
                {
                    return;
                }

                bool added = stats.AddContribution(new RuntimeCalculatedStatContribution
                {
                    contributionId = sourceId,
                    statId = statId,
                    sourceId = sourceId,
                    sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                    kind = (int)CalculatedStatContributionKind.Flat,
                    direction = (int)CalculatedStatContributionDirection.Improve,
                    magnitude = amount
                }, out string failureReason);
                Assert.That(added, Is.True, failureReason);
            }
        }
    }
}
