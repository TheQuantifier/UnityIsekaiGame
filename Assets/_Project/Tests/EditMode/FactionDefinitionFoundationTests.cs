using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class FactionDefinitionFoundationTests
    {
        [Test]
        public void FactionDefinition_ExposesStableMetadataClassificationAndAuthority()
        {
            CategoryDefinition category = CreateCategory("category.faction.guild", CategoryDomain.Faction);
            TagDefinition tag = CreateTag("tag.general.adventurer", CategoryDomain.General);
            ScriptableObject faction = CreateFaction("faction.guild.adventurers", "Adventurer's Guild", "Guild", category, new[] { tag }, authority: 7);

            Assert.That(Get<string>(faction, "Id"), Is.EqualTo("faction.guild.adventurers"));
            Assert.That(Get<object>(faction, "Kind").ToString(), Is.EqualTo("Guild"));
            Assert.That(ClassificationUtility.IsInCategory((ICategorizableDefinition)faction, "category.faction.guild"), Is.True);
            Assert.That(ClassificationUtility.HasTag((ITaggedDefinition)faction, "tag.general.adventurer"), Is.True);
            Assert.That((bool)Invoke(faction, "HasAuthority", EnumValue("UnityIsekaiGame.Factions.FactionAuthorityFlags", "IssueContracts")), Is.True);
            Assert.That((bool)Invoke(faction, "HasAuthority", EnumValue("UnityIsekaiGame.Factions.FactionAuthorityFlags", "CollectTaxes")), Is.False);
        }

        [Test]
        public void CatalogValidation_ReportsDuplicateIdsSelfParentCyclesAndMissingReferences()
        {
            ScriptableObject first = CreateFaction("faction.kingdom.prototype", "Kingdom", "Nation");
            ScriptableObject duplicate = CreateFaction("faction.kingdom.prototype", "Duplicate", "Nation");
            ScriptableObject selfParent = CreateFaction("faction.self", "Self", "Guild");
            SetObject(selfParent, "parentFaction", selfParent);
            ScriptableObject parent = CreateFaction("faction.parent", "Parent", "Guild");
            ScriptableObject child = CreateFaction("faction.child", "Child", "Guild", parent: parent);
            SetObject(parent, "parentFaction", child);
            ScriptableObject missingPlace = CreatePlace("place.settlement.missing", "Missing Place", "Settlement");
            ScriptableObject placeRef = CreateFaction("faction.place-ref", "Place Ref", "Guild");
            SetObject(placeRef, "homePlace", missingPlace);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(CreateCatalog(first, duplicate, selfParent, parent, child, placeRef));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("Duplicate definition ID 'faction.kingdom.prototype'"));
            Assert.That(report.GetSummary(), Does.Contain("cannot parent itself"));
            Assert.That(report.GetSummary(), Does.Contain("circular parent hierarchy"));
            Assert.That(report.GetSummary(), Does.Contain("HomePlace"));
        }

        [Test]
        public void HierarchyUtility_ReturnsAncestorsRootSharedParentAndReadablePath()
        {
            ScriptableObject kingdom = CreateFaction("faction.kingdom.prototype", "Kingdom", "Nation");
            ScriptableObject guard = CreateFaction("faction.guard.prototype-town", "Town Guard", "Military", parent: kingdom);
            ScriptableObject patrol = CreateFaction("faction.guard.prototype-town.patrol", "Patrol", "Military", parent: guard);
            ScriptableObject reserve = CreateFaction("faction.guard.prototype-town.reserve", "Reserve", "Military", parent: guard);
            Type utility = RequiredType("UnityIsekaiGame.Factions.FactionHierarchyUtility");

            Assert.That((bool)InvokeStatic(utility, "IsDescendantOf", patrol, kingdom), Is.True);
            Assert.That(Get<string>(InvokeStatic(utility, "GetRootFaction", patrol), "Id"), Is.EqualTo("faction.kingdom.prototype"));
            Assert.That(Get<string>(InvokeStatic(utility, "FindNearestAncestorOfKind", patrol, EnumValue("UnityIsekaiGame.Factions.FactionKind", "Military"), true), "Id"), Is.EqualTo("faction.guard.prototype-town.patrol"));
            Assert.That((bool)InvokeStatic(utility, "ShareCommonParent", patrol, reserve), Is.True);
            Assert.That((string)InvokeStatic(utility, "GetHierarchyPath", patrol), Is.EqualTo("Kingdom / Town Guard / Patrol"));
        }

        [Test]
        public void CatalogValidation_AcceptsPersonPlaceQuestAndContractFactionReferences()
        {
            ScriptableObject town = CreatePlace("place.settlement.prototype-town", "Prototype Town", "Settlement");
            ScriptableObject person = CreatePerson("person.prototype.npc", "Prototype NPC");
            ScriptableObject guild = CreateFaction("faction.guild.adventurers", "Adventurer's Guild", "Guild");
            SetObject(guild, "homePlace", town);
            SetObject(guild, "defaultLeader", person);
            SetObject(person, "primaryFaction", guild);
            SetString(person, "publicRoleTitle", "Guild Representative");
            SetObject(town, "defaultGoverningFaction", guild);
            ScriptableObject quest = CreateQuest("quest.prototype", "Prototype Quest");
            SetObject(quest, "questSourceFaction", guild);
            SetObject(quest, "relatedFaction", guild);
            ScriptableObject contract = CreateContract("contract.prototype", "Prototype Contract", "Prototype Board");
            SetObject(contract, "requesterFaction", guild);
            SetObject(contract, "postingFaction", guild);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(CreateCatalog(town, person, guild, quest, contract));

            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(Get<UnityEngine.Object>(person, "PrimaryFaction"), Is.SameAs(guild));
            Assert.That(Get<UnityEngine.Object>(town, "DefaultGoverningFaction"), Is.SameAs(guild));
            Assert.That(Get<string>(contract, "RequesterDisplayName"), Is.EqualTo("Adventurer's Guild"));
            Assert.That(Get<string>(quest, "QuestSourceDisplayName"), Is.EqualTo("Adventurer's Guild"));
        }

        [Test]
        public void SaveData_StoresFactionStableIdsAndRestoreOrder()
        {
            Type referenceType = RequiredType("UnityIsekaiGame.Factions.FactionReferenceSaveData");
            object reference = Activator.CreateInstance(referenceType);
            referenceType.GetField("factionId").SetValue(reference, "faction.guild.adventurers");

            Type membershipType = RequiredType("UnityIsekaiGame.Factions.FactionMembershipSaveData");
            object membership = Activator.CreateInstance(membershipType);
            membershipType.GetField("factionId").SetValue(membership, "faction.guild.adventurers");
            membershipType.GetField("personId").SetValue(membership, "person.prototype.npc");
            membershipType.GetField("rankId").SetValue(membership, "rank.prototype");

            Assert.That(Get<bool>(reference, "HasValidId"), Is.True);
            Assert.That(membershipType.GetField("factionId").GetValue(membership), Is.EqualTo("faction.guild.adventurers"));
            Assert.That(Convert.ToInt32(EnumValue("UnityIsekaiGame.Factions.FactionRestoreOrder", "ResolveFactionDefinitions")), Is.LessThan(Convert.ToInt32(EnumValue("UnityIsekaiGame.Factions.FactionRestoreOrder", "RestoreMembershipsAndRanks"))));
        }

        private static ScriptableObject CreateFaction(
            string id,
            string displayName,
            string kind,
            CategoryDefinition category = null,
            TagDefinition[] tags = null,
            ScriptableObject parent = null,
            int authority = 0)
        {
            ScriptableObject faction = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Factions.FactionDefinition"));
            SetString(faction, "factionId", id);
            SetString(faction, "displayName", displayName);
            SetEnum(faction, "kind", "UnityIsekaiGame.Factions.FactionKind", kind);
            SetObject(faction, "primaryCategory", category);
            SetObjectArray(faction, "tags", tags ?? Array.Empty<TagDefinition>());
            SetObject(faction, "parentFaction", parent);
            SetInt(faction, "authority", authority);
            return faction;
        }

        private static ScriptableObject CreatePlace(string id, string displayName, string kind)
        {
            ScriptableObject place = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Places.PlaceDefinition"));
            SetString(place, "placeId", id);
            SetString(place, "displayName", displayName);
            SetEnum(place, "placeKind", "UnityIsekaiGame.Places.PlaceKind", kind);
            return place;
        }

        private static ScriptableObject CreatePerson(string id, string displayName)
        {
            ScriptableObject person = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.People.PersonDefinition"));
            SetString(person, "personId", id);
            SetString(person, "displayName", displayName);
            return person;
        }

        private static ScriptableObject CreateQuest(string id, string title)
        {
            ScriptableObject quest = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Quests.QuestDefinition"));
            SetString(quest, "questId", id);
            SetString(quest, "title", title);
            return quest;
        }

        private static ScriptableObject CreateContract(string id, string title, string requesterName)
        {
            ScriptableObject contract = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Contracts.ContractDefinition"));
            SetString(contract, "contractId", id);
            SetString(contract, "displayTitle", title);
            SetString(contract, "requesterName", requesterName);
            return contract;
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

        private static void SetInt(UnityEngine.Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).intValue = value;
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
