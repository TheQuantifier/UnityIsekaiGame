using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step3IntegrationCloseoutTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCatalog_ValidatesAndRegistersRepresentativeStep3Definitions()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.False, report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());

            AssertRegistered(registry, "item.health-potion", "UnityIsekaiGame.Inventory.ItemDefinition");
            AssertRegistered(registry, "item.prototype-sword", "UnityIsekaiGame.Inventory.ItemDefinition");
            AssertRegistered(registry, "item.prototype-bow", "UnityIsekaiGame.Inventory.ItemDefinition");
            AssertRegistered(registry, "item.prototype-arrow", "UnityIsekaiGame.Inventory.ItemDefinition");
            AssertRegistered(registry, "item.prototype-iron-ore", "UnityIsekaiGame.Inventory.ItemDefinition");
            AssertRegistered(registry, "category.item.weapon", "UnityIsekaiGame.GameData.CategoryDefinition");
            AssertRegistered(registry, "category.item.ammunition", "UnityIsekaiGame.GameData.CategoryDefinition");
            AssertRegistered(registry, "tag.general.arcane", "UnityIsekaiGame.GameData.TagDefinition");
            AssertRegistered(registry, "rarity.common", "UnityIsekaiGame.GameData.RarityDefinition");
            AssertRegistered(registry, "quality.common", "UnityIsekaiGame.Inventory.Quality.QualityTierDefinition");
            AssertRegistered(registry, "condition.scale.standard", "UnityIsekaiGame.Inventory.Durability.ItemConditionScaleDefinition");
            AssertRegistered(registry, "ability.arcane-bolt", "UnityIsekaiGame.Abilities.AbilityDefinition");
            AssertRegistered(registry, "effect.arcane-damage", "UnityIsekaiGame.Abilities.DamageEffectDefinition");
            AssertRegistered(registry, "status.prototype-might", "UnityIsekaiGame.StatusEffects.StatusEffectDefinition");
            AssertRegistered(registry, "being.prototype-enemy", "UnityIsekaiGame.Beings.BeingDefinition");
            AssertRegistered(registry, "actor-profile.enemy-prototype", "UnityIsekaiGame.Beings.ActorProfileDefinition");
            AssertRegistered(registry, "place.building.prototype-guild-board-area", "UnityIsekaiGame.Places.PlaceDefinition");
            AssertRegistered(registry, "faction.guild.adventurers", "UnityIsekaiGame.Factions.FactionDefinition");
            AssertRegistered(registry, "contract.prototype-enemy-elimination", "UnityIsekaiGame.Contracts.ContractDefinition");
            AssertRegistered(registry, "quest.prototype-strange-disturbance", "UnityIsekaiGame.Quests.QuestDefinition");
            AssertRegistered(registry, "person.prototype.npc", "UnityIsekaiGame.People.PersonDefinition");
            AssertRegistered(registry, "damage.magic.arcane", "UnityIsekaiGame.Combat.DamageTypeDefinition");
        }

        [Test]
        public void PrototypeCatalog_HasNoDuplicateGlobalDefinitionIds()
        {
            DefinitionCatalog catalog = LoadCatalog();
            string[] duplicateIds = catalog.GetDefinitions()
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
                .GroupBy(definition => definition.Id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.That(duplicateIds, Is.Empty, $"Duplicate definition IDs: {string.Join(", ", duplicateIds)}");
        }

        [Test]
        public void PrototypeCatalog_PreservesRepresentativeTypedCrossSystemReferences()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();

            IGameDefinition healthPotion = Required(registry, "item.health-potion");
            Assert.That(Get<WorldItemPickup>(healthPotion, "WorldPickupPrefab"), Is.Not.Null);

            IGameDefinition sword = Required(registry, "item.prototype-sword");
            Assert.That(Get<WorldItemPickup>(sword, "WorldPickupPrefab"), Is.Not.Null);
            object equipment = Get<object>(sword, "Equipment");
            object swordView = Get<object>(equipment, "View");
            Assert.That(Get<GameObject>(swordView, "FirstPersonPrefab"), Is.Not.Null);
            Assert.That(Get<Vector3>(swordView, "FirstPersonLocalScale"), Is.EqualTo(Vector3.one));
            object meleeWeapon = Get<object>(equipment, "MeleeWeapon");
            Assert.That(Get<bool>(meleeWeapon, "IsWeapon"), Is.True);
            Assert.That(Get<IGameDefinition>(meleeWeapon, "DamageType").Id, Is.EqualTo("damage.physical.slashing"));

            IGameDefinition bow = Required(registry, "item.prototype-bow");
            Assert.That(Get<WorldItemPickup>(bow, "WorldPickupPrefab"), Is.Not.Null);
            object bowEquipment = Get<object>(bow, "Equipment");
            object bowView = Get<object>(bowEquipment, "View");
            Assert.That(Get<GameObject>(bowView, "FirstPersonPrefab"), Is.Not.Null);
            Assert.That(Get<Vector3>(bowView, "FirstPersonLocalScale"), Is.EqualTo(Vector3.one));
            object rangedWeapon = Get<object>(bowEquipment, "RangedWeapon");
            Assert.That(Get<bool>(rangedWeapon, "IsWeapon"), Is.True);
            Assert.That(Get<IGameDefinition>(rangedWeapon, "AmmoItem").Id, Is.EqualTo("item.prototype-arrow"));
            Assert.That(Get<IGameDefinition>(rangedWeapon, "DamageType").Id, Is.EqualTo("damage.physical.piercing"));
            Assert.That(Get<UnityEngine.GameObject>(rangedWeapon, "ProjectileVisualPrefab"), Is.Not.Null);

            IGameDefinition arrow = Required(registry, "item.prototype-arrow");
            Assert.That(Get<WorldItemPickup>(arrow, "WorldPickupPrefab"), Is.Not.Null);
            Assert.That(Get<IGameDefinition>(arrow, "PrimaryCategory").Id, Is.EqualTo("category.item.ammunition"));

            IGameDefinition arcaneEffect = Required(registry, "effect.arcane-damage");
            Assert.That(Get<IGameDefinition>(arcaneEffect, "TypedDamageType").Id, Is.EqualTo("damage.magic.arcane"));

            IGameDefinition heavyArcaneEffect = Required(registry, "effect.heavy-arcane-damage");
            Assert.That(Get<IGameDefinition>(heavyArcaneEffect, "TypedDamageType").Id, Is.EqualTo("damage.magic.arcane"));

            IGameDefinition mightApplication = Required(registry, "effect.apply-prototype-might");
            Assert.That(Get<IGameDefinition>(mightApplication, "StatusEffect").Id, Is.EqualTo("status.prototype-might"));

            IGameDefinition enemyProfile = Required(registry, "actor-profile.enemy-prototype");
            Assert.That(Get<IGameDefinition>(enemyProfile, "BeingDefinition").Id, Is.EqualTo("being.prototype-enemy"));

            IGameDefinition guildBoardArea = Required(registry, "place.building.prototype-guild-board-area");
            Assert.That(Get<IGameDefinition>(guildBoardArea, "ParentPlace").Id, Is.EqualTo("place.settlement.prototype-town"));

            IGameDefinition adventurersGuild = Required(registry, "faction.guild.adventurers");
            Assert.That(Get<IGameDefinition>(adventurersGuild, "HeadquartersPlace").Id, Is.EqualTo("place.building.prototype-guild-board-area"));

            IGameDefinition enemyContract = Required(registry, "contract.prototype-enemy-elimination");
            Assert.That(Get<IGameDefinition>(enemyContract, "RequesterFaction").Id, Is.EqualTo("faction.guild.adventurers"));
            Assert.That(Get<IGameDefinition>(enemyContract, "PostingFaction").Id, Is.EqualTo("faction.guild.adventurers"));

            IGameDefinition quest = Required(registry, "quest.prototype-strange-disturbance");
            Assert.That(Get<IGameDefinition>(quest, "QuestGiver").Id, Is.EqualTo("person.prototype.npc"));
            Assert.That(Get<IGameDefinition>(quest, "QuestSourceFaction").Id, Is.EqualTo("faction.guild.adventurers"));
            Assert.That(Get<IGameDefinition>(quest, "RelatedFaction").Id, Is.EqualTo("faction.guild.adventurers"));
        }

        [Test]
        public void WorldPickupFactory_UsesItemPickupPrefabAndRuntimeDropIdentity()
        {
            WorldEntityRegistry.ClearForTests();
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            ItemDefinition sword = Required<ItemDefinition>(registry, "item.prototype-sword");

            WorldItemPickup pickup = null;
            try
            {
                pickup = WorldItemPickupFactory.Create(sword, 2, new Vector3(1f, 0f, 2f), Quaternion.identity);

                Assert.That(pickup, Is.Not.Null);
                Assert.That(pickup.Item, Is.SameAs(sword));
                Assert.That(pickup.Quantity, Is.EqualTo(2));
                Assert.That(pickup.transform.Find("Longsword"), Is.Not.Null, "The item pickup prefab visual should be reused instead of spawning the cube fallback.");

                WorldEntitySpawnResult identityResult = WorldEntityIdentityFactory.CreateRuntimeIdentity(
                    pickup.gameObject,
                    "scene.prototype",
                    PersistenceService.LocalWorldId,
                    sword.Id);

                Assert.That(identityResult.Succeeded, Is.True, identityResult.Message);
                Assert.That(identityResult.Identity.IdentityKind, Is.EqualTo(WorldEntityIdentityKind.RuntimeSpawned));
                Assert.That(identityResult.Identity.EntityId, Does.StartWith("entity.local-world.runtime."));
            }
            finally
            {
                if (pickup != null)
                {
                    Object.DestroyImmediate(pickup.gameObject);
                }

                WorldEntityRegistry.ClearForTests();
            }
        }

        [Test]
        public void PrototypeBow_EquipsThroughInventoryAndFiresArrowProjectile()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            ItemDefinition bow = Required<ItemDefinition>(registry, "item.prototype-bow");
            ItemDefinition arrow = Required<ItemDefinition>(registry, "item.prototype-arrow");

            GameObject player = new GameObject("Prototype Bow Test Player");
            GameObject origin = new GameObject("Prototype Bow Test Origin");
            GameObject serviceHost = new GameObject("Prototype Bow Test Services");
            try
            {
                serviceHost.SetActive(false);
                origin.transform.SetParent(player.transform);
                origin.transform.localPosition = Vector3.zero;
                origin.transform.localRotation = Quaternion.identity;

                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                PlayerEquipment equipment = player.AddComponent<PlayerEquipment>();
                CharacterAttributes attributes = player.AddComponent<CharacterAttributes>();
                CalculatedStatCollection calculatedStats = player.AddComponent<CalculatedStatCollection>();
                CharacterResourceCollection resources = player.AddComponent<CharacterResourceCollection>();
                WorldEntityIdentity identity = player.AddComponent<WorldEntityIdentity>();
                Assert.That(identity.TrySetAuthoredIdentity("prototype-bow-test-player", "scene.test", PersistenceScope.RegionOrScene, "test.bow", out string identityFailure), Is.True, identityFailure);
                attributes.Configure(registry);
                calculatedStats.Configure(registry, attributes);
                resources.Configure(registry, calculatedStats, identity.EntityId);
                PrototypePersistenceServiceBehaviour runtimeServices = serviceHost.AddComponent<PrototypePersistenceServiceBehaviour>();
                PlayerMeleeCombat combat = player.AddComponent<PlayerMeleeCombat>();
                SetPrivateField(equipment, "inventory", inventory);
                SetPrivateField(combat, "equipment", equipment);
                SetPrivateField(combat, "inventory", inventory);
                SetPrivateField(combat, "attackOrigin", origin.transform);
                SetPrivateField(combat, "runtimeServices", runtimeServices);

                Assert.That(inventory.AddItemOrInstances(bow, 1).AddedAll, Is.True);
                Assert.That(inventory.AddItem(arrow, 2).AddedAll, Is.True);

                int bowSlot = FindSlot(inventory, bow);
                Assert.That(bowSlot, Is.GreaterThanOrEqualTo(0), "Bow should be present in inventory before equipping.");
                Assert.That(equipment.EquipFromInventorySlot(bowSlot).Succeeded, Is.True);
                Assert.That(inventory.CountItem(arrow), Is.EqualTo(2));

                int beforeProjectiles = CountSceneProjectiles();
                MeleeAttackResult result = combat.TryAttack();
                int afterProjectiles = CountSceneProjectiles();

                Assert.That(result.Started, Is.True, result.Message);
                Assert.That(result.AttackName, Is.EqualTo("Prototype Arrow Shot"));
                Assert.That(inventory.CountItem(arrow), Is.EqualTo(1), "Firing the bow should consume one Prototype Arrow.");
                Assert.That(afterProjectiles, Is.EqualTo(beforeProjectiles + 1), "Firing the bow should spawn one projectile.");
            }
            finally
            {
                foreach (SpellProjectile projectile in Object.FindObjectsByType<SpellProjectile>())
                {
                    if (projectile.gameObject.scene.IsValid())
                    {
                        Object.DestroyImmediate(projectile.gameObject);
                    }
                }

                Object.DestroyImmediate(player);
                Object.DestroyImmediate(serviceHost);
            }
        }

        [Test]
        public void FirstPersonWeaponPresenter_FlattensSingleChildVisualPrefab()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            ItemDefinition sword = Required<ItemDefinition>(registry, "item.prototype-sword");

            GameObject player = new GameObject("Prototype Sword Presenter Test Player");
            GameObject weaponRoot = new GameObject("PrototypeSwordView");
            try
            {
                weaponRoot.transform.SetParent(player.transform);

                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                PlayerEquipment equipment = player.AddComponent<PlayerEquipment>();
                SetPrivateField(equipment, "inventory", inventory);
                FirstPersonWeaponPresenter presenter = player.AddComponent<FirstPersonWeaponPresenter>();
                SetPrivateField(presenter, "equipment", equipment);
                SetPrivateField(presenter, "weaponRoot", weaponRoot);
                SetPrivateField(presenter, "swingRoot", weaponRoot.transform);

                Assert.That(inventory.AddItemOrInstances(sword, 1).AddedAll, Is.True);
                Assert.That(equipment.EquipFromInventorySlot(FindSlot(inventory, sword)).Succeeded, Is.True);
                InvokePrivateMethod(presenter, "RefreshVisibility");

                Assert.That(weaponRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(weaponRoot.transform.GetChild(0).name, Is.EqualTo("Longsword"));
                Assert.That(weaponRoot.transform.Find("Prototype Sword View"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Expected prototype definition catalog at {CatalogPath}.");
            return catalog;
        }

        private static void AssertRegistered(DefinitionRegistry registry, string id, string expectedTypeName)
        {
            IGameDefinition definition = Required(registry, id);
            Assert.That(definition.GetType().FullName, Is.EqualTo(expectedTypeName), $"Definition '{id}' resolved to an unexpected type.");
        }

        private static IGameDefinition Required(DefinitionRegistry registry, string id)
        {
            Assert.That(registry.TryGet(id, out IGameDefinition definition), Is.True, $"Expected definition '{id}' to be registered.");
            return definition;
        }

        private static TDefinition Required<TDefinition>(DefinitionRegistry registry, string id)
            where TDefinition : class, IGameDefinition
        {
            IGameDefinition definition = Required(registry, id);
            Assert.That(definition, Is.TypeOf<TDefinition>());
            return (TDefinition)definition;
        }

        private static T Get<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null, $"Cannot read '{propertyName}' from a null target.");
            object value = target.GetType().GetProperty(propertyName).GetValue(target);
            Assert.That(value, Is.Not.Null, $"Expected '{target.GetType().Name}.{propertyName}' to be assigned.");
            return (T)value;
        }

        private static int FindSlot(PlayerInventory inventory, ItemDefinition item)
        {
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.Item == item)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountSceneProjectiles()
        {
            return Object.FindObjectsByType<SpellProjectile>().Count(projectile => projectile.gameObject.scene.IsValid());
        }

        private static void SetPrivateField<TTarget>(TTarget target, string fieldName, object value)
        {
            FieldInfo field = typeof(TTarget).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {typeof(TTarget).Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod<TTarget>(TTarget target, string methodName)
        {
            MethodInfo method = typeof(TTarget).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected private method '{methodName}' on {typeof(TTarget).Name}.");
            method.Invoke(target, null);
        }
    }
}
