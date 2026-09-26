using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Tests
{
    public sealed class SkillsProgressionTests
    {
        private static int configureEventCount;
        private static Component reentrantSkillCollection;
        private static DefinitionRegistry reentrantRegistry;

        [Test]
        public void SkillGrades_AreOrderedAndAaaIsMastery()
        {
            Type gradeType = RequiredType("UnityIsekaiGame.Skills.SkillGrade");
            Type utilityType = RequiredType("UnityIsekaiGame.Skills.SkillGradeUtility");

            Assert.That((int)Enum.Parse(gradeType, "F"), Is.EqualTo(0));
            Assert.That((int)Enum.Parse(gradeType, "AAA"), Is.EqualTo(7));
            Assert.That((bool)InvokeStatic(utilityType, "IsMastered", Enum.Parse(gradeType, "AAA")), Is.True);
            object[] args = { Enum.Parse(gradeType, "A"), null };
            Assert.That((bool)InvokeStaticByRef(utilityType, "TryGetNext", args), Is.True);
            Assert.That(args[1].ToString(), Is.EqualTo("AA"));
        }

        [Test]
        public void PrototypeCatalog_ResolvesAlphaSkills()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();

            AssertResolves(registry, "skill.swordsmanship");
            AssertResolves(registry, "skill.unarmed-combat");
            AssertResolves(registry, "skill.arcane-magic");
            AssertResolves(registry, "skill.healing-magic");
            AssertResolves(registry, "skill.appraisal");
            AssertResolves(registry, "skill.trading");
            AssertResolves(registry, "skill.smithing");
        }

        [Test]
        public void PrototypeSkills_DeclareSemanticTypesForEligibilityRules()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            Assert.That(registry.DefinitionsById.Values.OfType<SkillDefinition>().All(skill => skill.SkillTypes != SkillType.Unknown), Is.True);
            Assert.That(registry.TryGet("skill.swordsmanship", out SkillDefinition swordsmanship), Is.True);
            Assert.That(registry.TryGet("skill.unarmed-combat", out SkillDefinition unarmed), Is.True);
            Assert.That(registry.TryGet("skill.arcane-magic", out SkillDefinition arcane), Is.True);
            Assert.That(registry.TryGet("skill.healing-magic", out SkillDefinition healing), Is.True);
            Assert.That(registry.TryGet("skill.appraisal", out SkillDefinition appraisal), Is.True);
            Assert.That(swordsmanship.HasAnyType(SkillType.Combat), Is.True);
            Assert.That(unarmed.HasAnyType(SkillType.Combat), Is.True);
            Assert.That(arcane.HasAnyType(SkillType.Combat), Is.True);
            Assert.That(arcane.HasAnyType(SkillType.Magic), Is.True);
            Assert.That(healing.HasAnyType(SkillType.Combat), Is.True, "Combat-support skills should qualify adventurers.");
            Assert.That(appraisal.HasAnyType(SkillType.Combat), Is.False);
        }

        [Test]
        public void HiddenLearning_CountsExecutedMissesRejectsBlockedAndDeduplicates()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = CreateSkillCollection(registry, out Component skills);
            try
            {
                object appraisal = Resolve(registry, "skill.appraisal");

                Invoke(skills, "RecordQualifyingAction", CreateAction("event.one", appraisal, true, true));
                AssertHiddenProgress(skills, "skill.appraisal", 1);

                Invoke(skills, "RecordQualifyingAction", CreateAction("event.miss", appraisal, true, false));
                AssertHiddenProgress(skills, "skill.appraisal", 2);

                Invoke(skills, "RecordQualifyingAction", CreateAction("event.blocked", appraisal, false, false));
                AssertHiddenProgress(skills, "skill.appraisal", 2);

                Invoke(skills, "RecordQualifyingAction", CreateAction("event.dup", appraisal, true, true));
                Invoke(skills, "RecordQualifyingAction", CreateAction("event.dup", appraisal, true, true));
                AssertHiddenProgress(skills, "skill.appraisal", 3);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RecordQualifyingAction_SurvivesReentrantConfigureFromSkillEvents()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = CreateSkillCollection(registry, out Component skills);
            try
            {
                object appraisal = Resolve(registry, "skill.appraisal");
                EventInfo hiddenProgressChanged = skills.GetType().GetEvent("HiddenProgressChanged", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(hiddenProgressChanged, Is.Not.Null);

                reentrantSkillCollection = skills;
                reentrantRegistry = registry;
                Delegate handler = CreateReconfigureHandler(hiddenProgressChanged.EventHandlerType);
                hiddenProgressChanged.AddEventHandler(skills, handler);

                Assert.DoesNotThrow(() => Invoke(skills, "RecordQualifyingAction", CreateAction("event.reentrant-configure", appraisal, true, true)));
                AssertHiddenProgress(skills, "skill.appraisal", 1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                reentrantSkillCollection = null;
                reentrantRegistry = null;
            }
        }

        [Test]
        public void NaturalLearningThreshold_LearnsAtFAndNextUseGrantsXp()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = CreateSkillCollection(registry, out Component skills);
            try
            {
                object appraisal = Resolve(registry, "skill.appraisal");

                for (int i = 0; i < 30; i++)
                {
                    Invoke(skills, "RecordQualifyingAction", CreateAction($"appraisal.{i}", appraisal, true, true));
                }

                object learned = FirstLearned(skills, "skill.appraisal");
                Assert.That(Get<int>(learned, "currentGrade"), Is.EqualTo(0));
                Assert.That(Get<int>(learned, "currentXp"), Is.EqualTo(0));

                Invoke(skills, "RecordQualifyingAction", CreateAction("appraisal.next", appraisal, true, false));
                learned = FirstLearned(skills, "skill.appraisal");
                Assert.That(Get<int>(learned, "currentXp"), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DirectGrant_DeduplicatesPromotesAndPreservesXp()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = CreateSkillCollection(registry, out Component skills);
            try
            {
                object sword = Resolve(registry, "skill.swordsmanship");
                Type gradeType = RequiredType("UnityIsekaiGame.Skills.SkillGrade");
                Type sourceType = RequiredType("UnityIsekaiGame.Skills.SkillAcquisitionSource");

                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "C"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);
                Invoke(skills, "AwardSkillUse", "skill.swordsmanship", "xp.one", 1);
                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "F"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);
                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "A"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);

                IList learned = (IList)GetProperty(skills, "LearnedSkills");
                Assert.That(learned.Count, Is.EqualTo(1));
                object record = learned[0];
                Assert.That(Get<int>(record, "currentGrade"), Is.EqualTo(5));
                Assert.That(Get<int>(record, "currentXp"), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void XpPromotion_CarriesExcessAndStopsAtAaa()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = CreateSkillCollection(registry, out Component skills);
            try
            {
                object sword = Resolve(registry, "skill.swordsmanship");
                Type gradeType = RequiredType("UnityIsekaiGame.Skills.SkillGrade");
                Type sourceType = RequiredType("UnityIsekaiGame.Skills.SkillAcquisitionSource");

                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "F"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);
                Invoke(skills, "AwardSkillUse", "skill.swordsmanship", "xp.promote", 26);
                object record = FirstLearned(skills, "skill.swordsmanship");
                Assert.That(Get<int>(record, "currentGrade"), Is.EqualTo(1));
                Assert.That(Get<int>(record, "currentXp"), Is.EqualTo(1));

                Invoke(skills, "AwardSkillUse", "skill.swordsmanship", "xp.master", 5000);
                record = FirstLearned(skills, "skill.swordsmanship");
                Assert.That(Get<int>(record, "currentGrade"), Is.EqualTo(7));
                Assert.That(Get<int>(record, "currentXp"), Is.EqualTo(0));
                int lifetime = Get<int>(record, "lifetimeXp");

                Invoke(skills, "AwardSkillUse", "skill.swordsmanship", "xp.after-master", 10);
                record = FirstLearned(skills, "skill.swordsmanship");
                Assert.That(Get<int>(record, "lifetimeXp"), Is.EqualTo(lifetime));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CumulativeGradeEffects_RebuildWithoutDuplicatingContributions()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = new GameObject("Skill Cumulative Fixture");
            try
            {
                Component attributes = root.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                Component calculated = root.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
                Component skills = root.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"));
                Invoke(attributes, "Configure", registry);
                Invoke(calculated, "Configure", registry, attributes);
                Invoke(skills, "Configure", registry, calculated, null);

                object sword = Resolve(registry, "skill.swordsmanship");
                Type gradeType = RequiredType("UnityIsekaiGame.Skills.SkillGrade");
                Type sourceType = RequiredType("UnityIsekaiGame.Skills.SkillAcquisitionSource");
                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "E"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);

                Assert.That((float)Invoke(calculated, "GetValue", "calculated-stat.physical-power"), Is.EqualTo(10f).Within(0.001f));
                Invoke(skills, "RebuildSkillEffects", false);
                Assert.That((float)Invoke(calculated, "GetValue", "calculated-stat.physical-power"), Is.EqualTo(10f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Configure_DoesNotRaiseSkillsChangedForUiRefreshSafety()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = new GameObject("Skill Configure Event Fixture");
            try
            {
                Component skills = root.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"));
                EventInfo changed = skills.GetType().GetEvent("SkillsChanged", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(changed, Is.Not.Null);

                configureEventCount = 0;
                Delegate handler = CreateSkillsChangedCounter(changed.EventHandlerType);
                changed.AddEventHandler(skills, handler);

                Invoke(skills, "Configure", registry, null, null);
                Assert.That(configureEventCount, Is.EqualTo(0), "Configure must not raise SkillsChanged or menu Refresh can recurse into Configure.");

                object sword = Resolve(registry, "skill.swordsmanship");
                Type gradeType = RequiredType("UnityIsekaiGame.Skills.SkillGrade");
                Type sourceType = RequiredType("UnityIsekaiGame.Skills.SkillAcquisitionSource");
                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "F"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);
                Assert.That(configureEventCount, Is.EqualTo(1), "The test event handler should still observe real Skill state changes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                configureEventCount = 0;
            }
        }

        [Test]
        public void PlayerSkillsSaveData_RoundTripsAndRejectsDuplicateLearnedRecords()
        {
            DefinitionRegistry registry = LoadPrototypeRegistry();
            GameObject root = CreateSkillCollection(registry, out Component skills);
            try
            {
                object sword = Resolve(registry, "skill.swordsmanship");
                Type gradeType = RequiredType("UnityIsekaiGame.Skills.SkillGrade");
                Type sourceType = RequiredType("UnityIsekaiGame.Skills.SkillAcquisitionSource");
                Invoke(skills, "GrantSkill", sword, Enum.Parse(gradeType, "F"), Enum.Parse(sourceType, "Development"), "test", "test-lab", false);

                object saveData = Invoke(skills, "CreateSaveData", "player.local", "person.local");
                object[] validArgs = { saveData, registry, null };
                Assert.That((bool)InvokeStaticByRef(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"), "ValidateSaveData", validArgs), Is.True, validArgs[2]?.ToString());

                IList learned = (IList)GetField(saveData, "learnedSkills");
                learned.Add(learned[0]);
                object[] args = { saveData, registry, null };
                bool valid = (bool)InvokeStaticByRef(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"), "ValidateSaveData", args);
                string failure = args[2]?.ToString();
                Assert.That(valid, Is.False);
                Assert.That(failure, Does.Contain("Duplicate learned Skill"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static DefinitionRegistry LoadPrototypeRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }

        private static GameObject CreateSkillCollection(DefinitionRegistry registry, out Component component)
        {
            GameObject root = new GameObject("Skill Test Fixture");
            component = root.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"));
            Invoke(component, "Configure", registry, null, null);
            return root;
        }

        private static object CreateAction(string eventId, object skillDefinition, bool executed, bool succeeded)
        {
            object learning = GetProperty(skillDefinition, "NaturalLearning");
            object category = GetProperty(learning, "ActionCategory");
            string qualifyingEvent = GetProperty(learning, "QualifyingEventId")?.ToString();
            return InvokeStatic(RequiredType("UnityIsekaiGame.Skills.SkillActionExecutionEvent"), "Development", eventId, category, qualifyingEvent, executed, succeeded);
        }

        private static object Resolve(DefinitionRegistry registry, string id)
        {
            Assert.That(registry.TryGet(id, out IGameDefinition definition), Is.True, id);
            return definition;
        }

        private static Delegate CreateSkillsChangedCounter(Type eventHandlerType)
        {
            MethodInfo invoke = eventHandlerType.GetMethod("Invoke");
            ParameterExpression[] parameters = invoke.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            MethodInfo increment = typeof(SkillsProgressionTests).GetMethod(nameof(IncrementConfigureEventCount), BindingFlags.NonPublic | BindingFlags.Static);
            return Expression.Lambda(eventHandlerType, Expression.Call(increment), parameters).Compile();
        }

        private static Delegate CreateReconfigureHandler(Type eventHandlerType)
        {
            MethodInfo invoke = eventHandlerType.GetMethod("Invoke");
            ParameterExpression[] parameters = invoke.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            MethodInfo reconfigure = typeof(SkillsProgressionTests).GetMethod(nameof(ReconfigureSkillCollectionDuringEvent), BindingFlags.NonPublic | BindingFlags.Static);
            return Expression.Lambda(eventHandlerType, Expression.Call(reconfigure), parameters).Compile();
        }

        private static void IncrementConfigureEventCount()
        {
            configureEventCount++;
        }

        private static void ReconfigureSkillCollectionDuringEvent()
        {
            if (reentrantSkillCollection != null && reentrantRegistry != null)
            {
                Invoke(reentrantSkillCollection, "Configure", reentrantRegistry, null, null);
            }
        }

        private static void AssertResolves(DefinitionRegistry registry, string id)
        {
            Assert.That(registry.TryGet(id, out IGameDefinition definition), Is.True, id);
            Assert.That(definition.GetType().FullName, Is.EqualTo("UnityIsekaiGame.Skills.SkillDefinition"));
        }

        private static void AssertHiddenProgress(Component skills, string skillId, int expected)
        {
            object progress = Invoke(skills, "GetLearningProgressForDevelopment", skillId);
            Assert.That(progress, Is.Not.Null);
            Assert.That(Get<int>(progress, "currentHiddenCount"), Is.EqualTo(expected));
        }

        private static object FirstLearned(Component skills, string skillId)
        {
            IList learned = (IList)GetProperty(skills, "LearnedSkills");
            foreach (object record in learned)
            {
                if (Get<string>(record, "skillDefinitionId") == skillId)
                {
                    return record;
                }
            }

            Assert.Fail($"Missing learned Skill {skillId}");
            return null;
        }

        private static Type RequiredType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(found => found != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .First(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            return method.Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .First(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            return method.Invoke(null, args);
        }

        private static object InvokeStaticByRef(Type type, string methodName, object[] args)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .First(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            return method.Invoke(null, args);
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
        }

        private static T Get<T>(object target, string name)
        {
            object value = GetField(target, name) ?? GetProperty(target, name);
            return value == null ? default : (T)value;
        }
    }
}
