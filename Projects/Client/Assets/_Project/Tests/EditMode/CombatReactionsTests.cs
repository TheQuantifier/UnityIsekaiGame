using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class CombatReactionsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PreviewTriggerDoesNotMutateResourcesOrEmitExecutionEvents()
        {
            using Fixture fixture = Fixture.Create();
            CombatReactionDefinition thorns = CreateReaction(
                "combat-reaction.test-preview-thorns",
                CombatReactionOperationType.ApplyDamage,
                CombatReactionTriggerType.DamageApplied,
                CombatReactionOwnershipSide.Target,
                CombatReactionTargetPolicy.OriginalSource,
                amount: 8f,
                damageType: fixture.DamageType);
            CombatReactionService service = fixture.Service;
            service.RegisterSource(new CombatReactionSourceRegistration("preview-source", fixture.DefenderActorId, fixture.Defender, CombatReactionSourceKind.Trait, "trait.test-thorns", "trait-instance", 0, thorns));
            int accepted = 0;
            int processed = 0;
            int chain = 0;
            service.TriggerAccepted += _ => accepted++;
            service.ReactionProcessed += _ => processed++;
            service.ChainProcessed += _ => chain++;
            float attackerBefore = fixture.AttackerResources.GetCurrent(ResourceIds.Health);

            CombatReactionChainResult result = service.PreviewTrigger(fixture.DamageContext(12f, "reaction.preview.root"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(result.Reactions.Count, Is.EqualTo(1));
            Assert.That(fixture.AttackerResources.GetCurrent(ResourceIds.Health), Is.EqualTo(attackerBefore).Within(0.001f));
            Assert.That(accepted + processed + chain, Is.Zero);
            UnityEngine.Object.DestroyImmediate(thorns);
        }

        [Test]
        public void ExecutionAppliesDamageThroughExistingHealthResourceOnce()
        {
            using Fixture fixture = Fixture.Create();
            CombatReactionDefinition thorns = CreateReaction(
                "combat-reaction.test-execute-thorns",
                CombatReactionOperationType.ApplyDamage,
                CombatReactionTriggerType.DamageApplied,
                CombatReactionOwnershipSide.Target,
                CombatReactionTargetPolicy.OriginalSource,
                amount: 7f,
                damageType: fixture.DamageType);
            int healthMutations = 0;
            fixture.AttackerResources.ResourceChanged += (_, result) =>
            {
                if (result.Request.ResourceId == ResourceIds.Health && result.AppliedAmount > 0f)
                {
                    healthMutations++;
                }
            };
            fixture.Service.RegisterSource(new CombatReactionSourceRegistration("execute-source", fixture.DefenderActorId, fixture.Defender, CombatReactionSourceKind.Trait, "trait.test-thorns", "trait-instance", 0, thorns));
            float attackerBefore = fixture.AttackerResources.GetCurrent(ResourceIds.Health);

            CombatReactionChainResult result = fixture.Service.ExecuteTrigger(fixture.DamageContext(12f, "reaction.execute.root"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Reactions.Count, Is.EqualTo(1));
            Assert.That(result.Reactions[0].Succeeded, Is.True, result.Reactions[0].Message);
            Assert.That(result.Reactions[0].DamageResult, Is.Not.Null);
            Assert.That(fixture.AttackerResources.GetCurrent(ResourceIds.Health), Is.EqualTo(attackerBefore - 7f).Within(0.001f));
            Assert.That(healthMutations, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(thorns);
        }

        [Test]
        public void DuplicateRootTransactionDoesNotApplySecondMutation()
        {
            using Fixture fixture = Fixture.Create();
            CombatReactionDefinition thorns = CreateReaction(
                "combat-reaction.test-duplicate",
                CombatReactionOperationType.ApplyDamage,
                CombatReactionTriggerType.DamageApplied,
                CombatReactionOwnershipSide.Target,
                CombatReactionTargetPolicy.OriginalSource,
                amount: 5f,
                damageType: fixture.DamageType);
            fixture.Service.RegisterSource(new CombatReactionSourceRegistration("duplicate-source", fixture.DefenderActorId, fixture.Defender, CombatReactionSourceKind.Status, "status.test-thorns", "status-instance", 0, thorns));
            CombatReactionTriggerContext context = fixture.DamageContext(10f, "reaction.duplicate.root");
            float before = fixture.AttackerResources.GetCurrent(ResourceIds.Health);

            CombatReactionChainResult first = fixture.Service.ExecuteTrigger(context);
            CombatReactionChainResult second = fixture.Service.ExecuteTrigger(context);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Code, Is.EqualTo(CombatReactionResultCode.Duplicate));
            Assert.That(fixture.AttackerResources.GetCurrent(ResourceIds.Health), Is.EqualTo(before - 5f).Within(0.001f));
            UnityEngine.Object.DestroyImmediate(thorns);
        }

        [Test]
        public void OrderingUsesReactionPrioritySourcePriorityAndStableIds()
        {
            using Fixture fixture = Fixture.Create();
            CombatReactionDefinition low = CreateReaction("combat-reaction.test-order-c", CombatReactionOperationType.NoOpDiagnostic, CombatReactionTriggerType.CriticalHit, CombatReactionOwnershipSide.Source, CombatReactionTargetPolicy.None, priority: 1);
            CombatReactionDefinition highA = CreateReaction("combat-reaction.test-order-a", CombatReactionOperationType.NoOpDiagnostic, CombatReactionTriggerType.CriticalHit, CombatReactionOwnershipSide.Source, CombatReactionTargetPolicy.None, priority: 10);
            CombatReactionDefinition highB = CreateReaction("combat-reaction.test-order-b", CombatReactionOperationType.NoOpDiagnostic, CombatReactionTriggerType.CriticalHit, CombatReactionOwnershipSide.Source, CombatReactionTargetPolicy.None, priority: 10);
            List<string> processed = new List<string>();
            fixture.Service.ReactionProcessed += result => processed.Add($"{result.DefinitionId}|{result.Source.SourceStableId}");
            fixture.Service.RegisterSource(new CombatReactionSourceRegistration("low", fixture.AttackerActorId, fixture.Attacker, CombatReactionSourceKind.Skill, "source.c", "1", 0, low));
            fixture.Service.RegisterSource(new CombatReactionSourceRegistration("high-b", fixture.AttackerActorId, fixture.Attacker, CombatReactionSourceKind.Skill, "source.b", "1", 0, highB));
            fixture.Service.RegisterSource(new CombatReactionSourceRegistration("high-a", fixture.AttackerActorId, fixture.Attacker, CombatReactionSourceKind.Skill, "source.a", "1", 5, highA));

            CombatReactionChainResult result = fixture.Service.ExecuteTrigger(fixture.CriticalContext("reaction.order.root"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(processed, Is.EqualTo(new[]
            {
                "combat-reaction.test-order-a|source.a",
                "combat-reaction.test-order-b|source.b",
                "combat-reaction.test-order-c|source.c"
            }));
            UnityEngine.Object.DestroyImmediate(low);
            UnityEngine.Object.DestroyImmediate(highA);
            UnityEngine.Object.DestroyImmediate(highB);
        }

        [Test]
        public void ProcChanceZeroFailsWithoutMutation()
        {
            using Fixture fixture = Fixture.Create();
            CombatReactionDefinition thorns = CreateReaction(
                "combat-reaction.test-proc-zero",
                CombatReactionOperationType.ApplyDamage,
                CombatReactionTriggerType.DamageApplied,
                CombatReactionOwnershipSide.Target,
                CombatReactionTargetPolicy.OriginalSource,
                amount: 9f,
                procChance: 0f,
                damageType: fixture.DamageType);
            fixture.Service.RegisterSource(new CombatReactionSourceRegistration("proc-source", fixture.DefenderActorId, fixture.Defender, CombatReactionSourceKind.Trait, "trait.proc", "1", 0, thorns));
            float before = fixture.AttackerResources.GetCurrent(ResourceIds.Health);

            CombatReactionChainResult result = fixture.Service.ExecuteTrigger(fixture.DamageContext(6f, "reaction.proc.root"));

            Assert.That(result.Reactions.Count, Is.EqualTo(1));
            Assert.That(result.Reactions[0].Code, Is.EqualTo(CombatReactionResultCode.ProcFailed));
            Assert.That(fixture.AttackerResources.GetCurrent(ResourceIds.Health), Is.EqualTo(before).Within(0.001f));
            UnityEngine.Object.DestroyImmediate(thorns);
        }

        [Test]
        public void UnsupportedStatusConditionAndAbilityOperationsFailExplicitly()
        {
            using Fixture fixture = Fixture.Create();
            foreach (CombatReactionOperationType operation in new[] { CombatReactionOperationType.ApplyStatusEffect, CombatReactionOperationType.RemoveStatusEffect, CombatReactionOperationType.ApplyCondition, CombatReactionOperationType.RemoveCondition, CombatReactionOperationType.TriggerImmediateAbility })
            {
                CombatReactionDefinition definition = CreateReaction($"combat-reaction.test-unsupported-{operation.ToString().ToLowerInvariant()}", operation, CombatReactionTriggerType.AttackHit, CombatReactionOwnershipSide.Source, CombatReactionTargetPolicy.OriginalTarget);
                fixture.Service.RegisterSource(new CombatReactionSourceRegistration(operation.ToString(), fixture.AttackerActorId, fixture.Attacker, CombatReactionSourceKind.Ability, $"ability.{operation}", "1", 0, definition));

                CombatReactionChainResult result = fixture.Service.ExecuteTrigger(fixture.HitContext($"reaction.unsupported.{operation}"));

                Assert.That(result.Reactions[0].Succeeded, Is.False);
                Assert.That(result.Reactions[0].Code, Is.EqualTo(CombatReactionResultCode.UnsupportedOperation));
                UnityEngine.Object.DestroyImmediate(definition);
                fixture.Service.ClearAllSources();
                fixture.Service.ClearTransientStateForRestore();
            }
        }

        private static CombatReactionDefinition CreateReaction(
            string id,
            CombatReactionOperationType operation,
            CombatReactionTriggerType trigger,
            CombatReactionOwnershipSide ownership,
            CombatReactionTargetPolicy target,
            float amount = 0f,
            float multiplier = 0f,
            float procChance = 1f,
            int priority = 0,
            DamageTypeDefinition damageType = null)
        {
            CombatReactionDefinition definition = ScriptableObject.CreateInstance<CombatReactionDefinition>();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("reactionId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            SerializedProperty triggers = serialized.FindProperty("triggerTypes");
            triggers.arraySize = 1;
            triggers.GetArrayElementAtIndex(0).enumValueIndex = (int)trigger;
            serialized.FindProperty("ownershipSide").enumValueIndex = (int)ownership;
            serialized.FindProperty("targetPolicy").enumValueIndex = (int)target;
            serialized.FindProperty("operationType").enumValueIndex = (int)operation;
            serialized.FindProperty("amount").floatValue = amount;
            serialized.FindProperty("multiplier").floatValue = multiplier;
            serialized.FindProperty("procChance").floatValue = procChance;
            serialized.FindProperty("priority").intValue = priority;
            serialized.FindProperty("damageType").objectReferenceValue = damageType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static DamageTypeDefinition CreateDamageType()
        {
            DamageTypeDefinition damageType = ScriptableObject.CreateInstance<DamageTypeDefinition>();
            SerializedObject serialized = new SerializedObject(damageType);
            serialized.FindProperty("damageTypeId").stringValue = "damage.test-reaction";
            serialized.FindProperty("displayName").stringValue = "Test Reaction";
            serialized.FindProperty("family").enumValueIndex = (int)DamageFamily.Physical;
            serialized.FindProperty("generalDefenseApplies").boolValue = true;
            serialized.FindProperty("enforceMinimumDamage").boolValue = false;
            serialized.FindProperty("minimumDamage").floatValue = 0f;
            serialized.FindProperty("canonicalAlphaDamageType").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return damageType;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly DamageTypeDefinition damageType;

            private Fixture(GameObject serviceOwner, GameObject attacker, GameObject defender, DamageTypeDefinition damageType, CharacterResourceCollection attackerResources, CharacterResourceCollection defenderResources, string attackerActorId, string defenderActorId)
            {
                ServiceOwner = serviceOwner;
                Service = serviceOwner.AddComponent<CombatReactionService>();
                Attacker = attacker;
                Defender = defender;
                this.damageType = damageType;
                AttackerResources = attackerResources;
                DefenderResources = defenderResources;
                AttackerActorId = attackerActorId;
                DefenderActorId = defenderActorId;
            }

            public GameObject ServiceOwner { get; }
            public CombatReactionService Service { get; }
            public GameObject Attacker { get; }
            public GameObject Defender { get; }
            public DamageTypeDefinition DamageType => damageType;
            public CharacterResourceCollection AttackerResources { get; }
            public CharacterResourceCollection DefenderResources { get; }
            public string AttackerActorId { get; }
            public string DefenderActorId { get; }

            public static Fixture Create()
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                DamageTypeDefinition damageType = CreateDamageType();
                GameObject serviceOwner = new GameObject("Combat Reaction Service");
                GameObject attacker = new GameObject("Combat Reaction Attacker");
                GameObject defender = new GameObject("Combat Reaction Defender");
                CharacterResourceCollection attackerResources = ConfigureActor(attacker, registry, "attacker", out string attackerActorId);
                CharacterResourceCollection defenderResources = ConfigureActor(defender, registry, "defender", out string defenderActorId);
                return new Fixture(serviceOwner, attacker, defender, damageType, attackerResources, defenderResources, attackerActorId, defenderActorId);
            }

            public CombatReactionTriggerContext DamageContext(float actualDamage, string rootTransactionId)
            {
                return new CombatReactionTriggerContext(CombatReactionTriggerType.DamageApplied, rootTransactionId, AttackerActorId, Attacker, DefenderActorId, Defender, actualDamage, damageType: damageType);
            }

            public CombatReactionTriggerContext CriticalContext(string rootTransactionId)
            {
                return new CombatReactionTriggerContext(CombatReactionTriggerType.CriticalHit, rootTransactionId, AttackerActorId, Attacker, DefenderActorId, Defender, critical: true, damageType: damageType);
            }

            public CombatReactionTriggerContext HitContext(string rootTransactionId)
            {
                return new CombatReactionTriggerContext(CombatReactionTriggerType.AttackHit, rootTransactionId, AttackerActorId, Attacker, DefenderActorId, Defender, damageType: damageType);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(ServiceOwner);
                UnityEngine.Object.DestroyImmediate(Attacker);
                UnityEngine.Object.DestroyImmediate(Defender);
                UnityEngine.Object.DestroyImmediate(damageType);
            }

            private static CharacterResourceCollection ConfigureActor(GameObject owner, DefinitionRegistry registry, string name, out string actorId)
            {
                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                CharacterResourceCollection resources = owner.AddComponent<CharacterResourceCollection>();
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                Assert.That(identity.TrySetAuthoredIdentity($"combat-reaction-{name}-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.combat-reaction", out string failureReason), Is.True, failureReason);
                actorId = identity.EntityId;
                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                resources.Configure(registry, stats, "player.local");
                return resources;
            }
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }
    }
}
