using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Magic;

namespace UnityIsekaiGame.Tests
{
    public sealed class AbilityEffectFoundationTests
    {
        [Test]
        public void EffectPipeline_PreflightsWholeBatchBeforeMutation()
        {
            TestEffectDefinition valid = ScriptableObject.CreateInstance<TestEffectDefinition>();
            TestEffectDefinition invalid = ScriptableObject.CreateInstance<TestEffectDefinition>();
            invalid.CanExecuteSuccessfully = false;
            EffectExecutionContext context = new EffectExecutionContext(null, null, null, Vector3.zero, Vector3.zero, Vector3.forward);

            AbilityExecutionResult result = AbilityEffectPipeline.Execute(in context, new EffectDefinition[] { valid, invalid });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(AbilityExecutionStatus.EffectValidationFailure));
            Assert.That(result.FailedEffectIndex, Is.EqualTo(1));
            Assert.That(valid.ExecuteCount, Is.Zero, "No effect may mutate state when any effect in the batch fails preflight.");
            Assert.That(invalid.ExecuteCount, Is.Zero);
            Object.DestroyImmediate(valid);
            Object.DestroyImmediate(invalid);
        }

        [Test]
        public void AbilityDefinition_UsesSharedCombatExecutionForTimingCostsAndCooldown()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            CombatExecutionDefinition execution = ScriptableObject.CreateInstance<CombatExecutionDefinition>();
            SerializedObject serializedExecution = new SerializedObject(execution);
            serializedExecution.FindProperty("executionId").stringValue = "combat-execution.test-ability";
            serializedExecution.FindProperty("displayName").stringValue = "Test Ability Execution";
            serializedExecution.FindProperty("actionType").enumValueIndex = (int)CombatExecutionActionType.Ability;
            serializedExecution.FindProperty("cooldownDuration").floatValue = 2f;
            serializedExecution.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedAbility = new SerializedObject(ability);
            serializedAbility.FindProperty("abilityId").stringValue = "ability.test-shared-execution";
            serializedAbility.FindProperty("displayName").stringValue = "Shared Execution";
            serializedAbility.FindProperty("execution").objectReferenceValue = execution;
            serializedAbility.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(ability.Execution, Is.SameAs(execution));
            Assert.That(ability.Execution.CooldownDuration, Is.EqualTo(2f));
            Assert.That(typeof(AbilityDefinition).GetField("cooldownDuration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(AbilityDefinition).GetField("resourceCosts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
            Object.DestroyImmediate(ability);
            Object.DestroyImmediate(execution);
        }

        [Test]
        public void DefinitionValidation_FlagsAbilityWithNoEffects()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            SerializedObject serialized = new SerializedObject(ability);
            serialized.FindProperty("abilityId").stringValue = "ability.no-effects";
            serialized.FindProperty("displayName").stringValue = "No Effects";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(ability));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("has no effects"));
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void SpellDefinition_ReferencesAbilityWithoutDuplicatingCombatValues()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            SpellDefinition spell = ScriptableObject.CreateInstance<SpellDefinition>();
            SerializedObject serializedSpell = new SerializedObject(spell);
            serializedSpell.FindProperty("spellId").stringValue = "spell.adapter";
            serializedSpell.FindProperty("displayName").stringValue = "Adapter";
            serializedSpell.FindProperty("ability").objectReferenceValue = ability;
            serializedSpell.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(spell.Ability, Is.SameAs(ability));
            Assert.That(typeof(SpellDefinition).GetField("manaCost", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(SpellDefinition).GetField("cooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
            Object.DestroyImmediate(spell);
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void CombatTargeting_RejectsOutOfRangeAndBlockedTargets()
        {
            AbilityDefinition ability = CreateTargetedAbility(5f);
            GameObject source = new GameObject("Targeting Source");
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Targeting Target";
            target.transform.position = new Vector3(0f, 0f, 6f);
            Physics.SyncTransforms();

            AbilityExecutionContext outOfRange = new AbilityExecutionContext(
                ability, source, target, source.transform, source.transform.position, target.transform.position, Vector3.forward, false);
            Assert.That(CombatTargetingService.Validate(in outOfRange).Status, Is.EqualTo(AbilityExecutionStatus.OutOfRange));

            target.transform.position = new Vector3(0f, 0f, 4f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Targeting Wall";
            wall.transform.position = new Vector3(0f, 0f, 2f);
            Physics.SyncTransforms();
            AbilityExecutionContext blocked = new AbilityExecutionContext(
                ability, source, target, source.transform, source.transform.position, target.transform.position, Vector3.forward, false);

            AbilityExecutionResult result = CombatTargetingService.Validate(in blocked);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("line of sight"));

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(ability);
        }

        private static AbilityDefinition CreateTargetedAbility(float range)
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            SerializedObject serialized = new SerializedObject(ability);
            serialized.FindProperty("abilityId").stringValue = "ability.test-targeting";
            serialized.FindProperty("displayName").stringValue = "Targeting Test";
            serialized.FindProperty("range").floatValue = range;
            serialized.FindProperty("targetingMode").enumValueIndex = (int)AbilityTargetingMode.DirectTarget;
            serialized.FindProperty("requiresLineOfSight").boolValue = true;
            serialized.FindProperty("targetingMask").intValue = ~0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private sealed class TestEffectDefinition : EffectDefinition
        {
            public bool CanExecuteSuccessfully { get; set; } = true;
            public int ExecuteCount { get; private set; }

            public override EffectExecutionResult CanExecute(in EffectExecutionContext context)
            {
                return CanExecuteSuccessfully
                    ? EffectExecutionResult.Success("Valid test effect.")
                    : EffectExecutionResult.Failure(EffectExecutionStatus.InvalidTarget, "Invalid test target.");
            }

            public override EffectExecutionResult Execute(in EffectExecutionContext context)
            {
                ExecuteCount++;
                return EffectExecutionResult.Success("Executed test effect.");
            }
        }
    }
}
