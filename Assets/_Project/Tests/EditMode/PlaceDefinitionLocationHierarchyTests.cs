using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class PlaceDefinitionLocationHierarchyTests
    {
        [Test]
        public void PlaceDefinition_ExposesStableMetadataAndClassification()
        {
            CategoryDefinition category = CreateCategory("category.place.settlement", CategoryDomain.Place);
            TagDefinition settlement = CreateTag("tag.place.settlement", CategoryDomain.Place);
            ScriptableObject place = CreatePlace("place.settlement.prototype-town", "Prototype Town", "Settlement", category, new[] { settlement });

            Assert.That(Get<string>(place, "Id"), Is.EqualTo("place.settlement.prototype-town"));
            Assert.That(Get<object>(place, "PlaceKind").ToString(), Is.EqualTo("Settlement"));
            Assert.That(ClassificationUtility.IsInCategory((ICategorizableDefinition)place, "category.place.settlement"), Is.True);
            Assert.That(ClassificationUtility.HasTag((ITaggedDefinition)place, "tag.place.settlement"), Is.True);
        }

        [Test]
        public void Catalog_RegistersPlaceForTypedLookupAndMissingLookupFails()
        {
            ScriptableObject world = CreatePlace("place.world.prototype", "World", "World");
            DefinitionCatalog catalog = CreateCatalog(world);
            DefinitionRegistry registry = catalog.CreateRegistry();

            Assert.That(registry.TryGet("place.world.prototype", out IGameDefinition found), Is.True);
            Assert.That(found, Is.SameAs(world));
            Assert.That(registry.TryGet("place.missing", out IGameDefinition missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void CatalogValidation_ReportsDuplicateIdsSelfParentAndCycles()
        {
            ScriptableObject first = CreatePlace("place.region.prototype", "Region", "Region");
            ScriptableObject duplicate = CreatePlace("place.region.prototype", "Duplicate Region", "Region");
            ScriptableObject selfParent = CreatePlace("place.poi.self", "Self", "PointOfInterest");
            SetObject(selfParent, "parentPlace", selfParent);
            ScriptableObject parent = CreatePlace("place.region.parent", "Parent", "Region");
            ScriptableObject child = CreatePlace("place.poi.child", "Child", "PointOfInterest", parent: parent);
            SetObject(parent, "parentPlace", child);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(CreateCatalog(first, duplicate, selfParent, parent, child));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("Duplicate definition ID 'place.region.prototype'"));
            Assert.That(report.GetSummary(), Does.Contain("cannot parent itself"));
            Assert.That(report.GetSummary(), Does.Contain("circular parent hierarchy"));
        }

        [Test]
        public void HierarchyUtility_ReturnsAncestorsDescendantsAndReadablePath()
        {
            ScriptableObject nation = CreatePlace("place.nation.prototype", "Nation", "Nation");
            ScriptableObject region = CreatePlace("place.region.prototype", "Region", "Region", parent: nation);
            ScriptableObject settlement = CreatePlace("place.settlement.prototype-town", "Town", "Settlement", parent: region);
            ScriptableObject poi = CreatePlace("place.poi.disturbance-site", "Disturbance Site", "PointOfInterest", parent: settlement);
            Type utility = RequiredType("UnityIsekaiGame.Places.PlaceHierarchyUtility");

            Assert.That((bool)InvokeStatic(utility, "IsDescendantOf", poi, region), Is.True);
            Assert.That((bool)InvokeStatic(utility, "ContainsOrIs", poi, poi), Is.True);
            Assert.That(Get<string>(InvokeStatic(utility, "GetContainingSettlement", poi), "Id"), Is.EqualTo("place.settlement.prototype-town"));
            Assert.That(Get<string>(InvokeStatic(utility, "GetContainingRegion", poi), "Id"), Is.EqualTo("place.region.prototype"));
            Assert.That(Get<string>(InvokeStatic(utility, "GetContainingNation", poi), "Id"), Is.EqualTo("place.nation.prototype"));
            Assert.That((string)InvokeStatic(utility, "GetHierarchyPath", poi), Is.EqualTo("Nation / Region / Town / Disturbance Site"));
        }

        [Test]
        public void CatalogValidation_ReportsMissingParentAndWarnsForSuspiciousHierarchy()
        {
            ScriptableObject building = CreatePlace("place.building.prototype", "Building", "Building");
            ScriptableObject nationInsideBuilding = CreatePlace("place.nation.inside-building", "Odd Nation", "Nation", parent: building);

            DefinitionValidationReport missingReport = DefinitionCatalogValidator.Validate(CreateCatalog(nationInsideBuilding));
            DefinitionValidationReport warningReport = DefinitionCatalogValidator.Validate(CreateCatalog(building, nationInsideBuilding));

            Assert.That(missingReport.HasErrors, Is.True);
            Assert.That(missingReport.GetSummary(), Does.Contain("parent place"));
            Assert.That(warningReport.HasErrors, Is.False, warningReport.GetSummary());
            Assert.That(warningReport.WarningCount, Is.GreaterThan(0));
            Assert.That(warningReport.GetSummary(), Does.Contain("suspicious hierarchy"));
        }

        [Test]
        public void PlaceIdentity_ExposesDefinitionWithoutRegistry()
        {
            ScriptableObject place = CreatePlace("place.poi.disturbance-site", "Disturbance Site", "PointOfInterest");
            GameObject gameObject = new GameObject("Place Identity");
            Component identity = gameObject.AddComponent(RequiredType("UnityIsekaiGame.Places.PlaceIdentity"));
            SetObject(identity, "definition", place);

            Assert.That(Get<string>(identity, "PlaceId"), Is.EqualTo("place.poi.disturbance-site"));
            Assert.That(Get<bool>(identity, "HasValidDefinition"), Is.True);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ReachLocationObjective_MatchesTypedPlaceAndCompletesOnceOnlyWhileActive()
        {
            ScriptableObject place = CreatePlace("place.poi.disturbance-site", "Disturbance Site", "PointOfInterest");
            ScriptableObject objective = CreateReachLocationObjective(place, "legacy_disturbance");
            object context = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Contracts.ContractObjectiveContext"), new object[] { null });
            object instance = Invoke(objective, "CreateInstance", context);

            InvokeStatic(RequiredType("UnityIsekaiGame.Quests.QuestObjectiveSignalBus"), "ReportReachLocation", "place.poi.disturbance-site");
            Assert.That(Get<int>(instance, "CurrentProgress"), Is.EqualTo(0));

            Invoke(instance, "Activate");
            InvokeStatic(RequiredType("UnityIsekaiGame.Quests.QuestObjectiveSignalBus"), "ReportReachLocation", "place.poi.disturbance-site");
            InvokeStatic(RequiredType("UnityIsekaiGame.Quests.QuestObjectiveSignalBus"), "ReportReachLocation", "place.poi.disturbance-site");

            Assert.That(Get<int>(instance, "CurrentProgress"), Is.EqualTo(1));
            Assert.That(Get<bool>(instance, "IsComplete"), Is.True);
            Invoke(instance, "Dispose");
        }

        [Test]
        public void ReachLocationObjective_UsesLegacyStringFallbackWhenNoPlaceAssigned()
        {
            ScriptableObject objective = CreateReachLocationObjective(null, "prototype_disturbance_site");
            object context = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Contracts.ContractObjectiveContext"), new object[] { null });
            object instance = Invoke(objective, "CreateInstance", context);

            Invoke(instance, "Activate");
            InvokeStatic(RequiredType("UnityIsekaiGame.Quests.QuestObjectiveSignalBus"), "ReportReachLocation", "prototype_disturbance_site");

            Assert.That(Get<int>(instance, "CurrentProgress"), Is.EqualTo(1));
            Assert.That(Get<bool>(instance, "IsComplete"), Is.True);
            Invoke(instance, "Dispose");
        }

        [Test]
        public void PersonDefinition_CanReferenceHomePlace()
        {
            ScriptableObject home = CreatePlace("place.settlement.prototype-town", "Prototype Town", "Settlement");
            ScriptableObject person = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.People.PersonDefinition"));
            SetString(person, "personId", "person.prototype.npc");
            SetString(person, "displayName", "Prototype NPC");
            SetObject(person, "homePlace", home);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(CreateCatalog(home, person));

            Assert.That(Get<UnityEngine.Object>(person, "HomePlace"), Is.SameAs(home));
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
        }

        [Test]
        public void SaveData_StoresPlaceStableIdsAndRestoreOrder()
        {
            Type referenceType = RequiredType("UnityIsekaiGame.Places.PlaceReferenceSaveData");
            object reference = Activator.CreateInstance(referenceType);
            referenceType.GetField("placeId").SetValue(reference, "place.poi.disturbance-site");

            Type locationType = RequiredType("UnityIsekaiGame.Places.ActorLocationSaveData");
            object location = Activator.CreateInstance(locationType);
            locationType.GetField("currentPlaceId").SetValue(location, "place.settlement.prototype-town");
            locationType.GetField("sceneKey").SetValue(location, "scene.prototype");

            Assert.That(referenceType.GetField("placeId").GetValue(reference), Is.EqualTo("place.poi.disturbance-site"));
            Assert.That(locationType.GetField("currentPlaceId").GetValue(location), Is.EqualTo("place.settlement.prototype-town"));
            Assert.That(Convert.ToInt32(EnumValue("UnityIsekaiGame.Places.PlaceRestoreOrder", "ResolvePlaceDefinition")), Is.LessThan(Convert.ToInt32(EnumValue("UnityIsekaiGame.Places.PlaceRestoreOrder", "RestoreActorPosition"))));
        }

        private static ScriptableObject CreatePlace(
            string id,
            string displayName,
            string kind,
            CategoryDefinition category = null,
            TagDefinition[] tags = null,
            ScriptableObject parent = null)
        {
            ScriptableObject place = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Places.PlaceDefinition"));
            SetString(place, "placeId", id);
            SetString(place, "displayName", displayName);
            SetEnum(place, "placeKind", "UnityIsekaiGame.Places.PlaceKind", kind);
            SetObject(place, "primaryCategory", category);
            SetObjectArray(place, "tags", tags ?? Array.Empty<TagDefinition>());
            SetObject(place, "parentPlace", parent);
            return place;
        }

        private static ScriptableObject CreateReachLocationObjective(ScriptableObject targetPlace, string legacyLocationId)
        {
            ScriptableObject objective = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Quests.ReachLocationObjectiveDefinition"));
            SetString(objective, "description", "Reach test location.");
            SetObject(objective, "targetPlace", targetPlace);
            SetString(objective, "locationId", legacyLocationId);
            return objective;
        }

        private static CategoryDefinition CreateCategory(string id, CategoryDomain domain)
        {
            CategoryDefinition category = ScriptableObject.CreateInstance<CategoryDefinition>();
            SetString(category, "categoryId", id);
            SetString(category, "displayName", id);
            SetEnumValue(category, "domain", (int)domain);
            return category;
        }

        private static TagDefinition CreateTag(string id, CategoryDomain domain)
        {
            TagDefinition tag = ScriptableObject.CreateInstance<TagDefinition>();
            SetString(tag, "tagId", id);
            SetString(tag, "displayName", id);
            SetEnumValue(tag, "domain", (int)domain);
            return tag;
        }

        private static DefinitionCatalog CreateCatalog(params ScriptableObject[] definitions)
        {
            return ClassificationTestFactory.CreateCatalog(definitions);
        }

        private static void SetString(UnityEngine.Object target, string fieldName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(UnityEngine.Object target, string fieldName, string fullName, string enumValue)
        {
            SetEnumValue(target, fieldName, Convert.ToInt32(EnumValue(fullName, enumValue)));
        }

        private static void SetEnumValue(UnityEngine.Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Get<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType().GetMethod(methodName).Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            foreach (System.Reflection.MethodInfo method in type.GetMethods())
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return method.Invoke(null, args);
                }
            }

            Assert.Fail($"Expected static method {type.FullName}.{methodName} with {args.Length} argument(s).");
            return null;
        }

        private static object EnumValue(string fullName, string name)
        {
            return Enum.Parse(RequiredType(fullName), name);
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, $"Expected runtime type {fullName} to exist in loaded project assemblies.");
            return type;
        }
    }
}
