using System;
using System.Collections.Generic;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypeEntityLocationFactory
    {
        public const string PlayerPersonId = "person.prototype.player";
        public const string GuildMasterPersonId = "person.prototype.guildmaster";
        public const string MerchantPersonId = "person.prototype.merchant";
        public const string AdventurersGuildReceptionistPersonId = "person.prototype.adventurers-guild-receptionist";
        public const string MerchantGuildReceptionistPersonId = "person.prototype.merchant-guild-receptionist";
        public const string PrisonerPersonId = "person.prototype.prisoner";
        public const string RecordsClerkPersonId = "person.prototype.records-clerk";
        public const string PlayerBodyId = "body.prototype.player";
        public const string GuildMasterBodyId = "body.prototype.guildmaster";
        public const string MerchantBodyId = "body.prototype.merchant";
        public const string AdventurersGuildReceptionistBodyId = "body.prototype.adventurers-guild-receptionist";
        public const string MerchantGuildReceptionistBodyId = "body.prototype.merchant-guild-receptionist";
        public const string PrisonerBodyId = "body.prototype.prisoner";
        public const string RecordsClerkBodyId = "body.prototype.records-clerk";
        public const string MayorBodyId = "body.prototype.mayor";
        public const string GuardBodyId = "body.prototype.guard";
        public const string MagistrateBodyId = "body.prototype.magistrate";
        public const string MerchantGuildmasterBodyId = "body.prototype.merchant-guildmaster";
        public const string TemplePriestBodyId = "body.prototype.temple-priest";
        public const string UniversityHeadmasterBodyId = "body.prototype.university-headmaster";
        public const string ManorLordBodyId = "body.prototype.manor-lord";
        public const string DukeBodyId = "body.prototype.duke";
        public const string MonarchBodyId = "body.prototype.monarch";
        public const string SwordItemInstanceId = "item-instance.prototype.sword.world";
        public const string ArrowItemInstanceId = "item-instance.prototype.arrow.world";
        public const string GuildChestEntityId = "world-entity.prototype.guild-chest";
        public const string DungeonDoorEntityId = "world-entity.prototype.dungeon-door";

        public static IReadOnlyList<EntityLocationReferenceData> CreateKnownEntities(string worldId = PersistenceService.LocalWorldId)
        {
            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            return new[]
            {
                Person(PlayerPersonId, world),
                Person(GuildMasterPersonId, world),
                Person(MerchantPersonId, world),
                Person(AdventurersGuildReceptionistPersonId, world),
                Person(MerchantGuildReceptionistPersonId, world),
                Person(PrisonerPersonId, world),
                Person(RecordsClerkPersonId, world),
                Person(PrototypeInstitutionalContentIds.MayorPerson, world),
                Person(PrototypeInstitutionalContentIds.GuardPerson, world),
                Person(PrototypeInstitutionalContentIds.MagistratePerson, world),
                Person(PrototypeInstitutionalContentIds.MerchantGuildmasterPerson, world),
                Person(PrototypeInstitutionalContentIds.TemplePriestPerson, world),
                Person(PrototypeInstitutionalContentIds.UniversityHeadmasterPerson, world),
                Person(PrototypeInstitutionalContentIds.ManorLordPerson, world),
                Person(PrototypeInstitutionalContentIds.DukePerson, world),
                Person(PrototypeInstitutionalContentIds.MonarchPerson, world),
                Body(PlayerBodyId, world),
                Body(GuildMasterBodyId, world),
                Body(MerchantBodyId, world),
                Body(AdventurersGuildReceptionistBodyId, world),
                Body(MerchantGuildReceptionistBodyId, world),
                Body(PrisonerBodyId, world),
                Body(RecordsClerkBodyId, world),
                Body(MayorBodyId, world),
                Body(GuardBodyId, world),
                Body(MagistrateBodyId, world),
                Body(MerchantGuildmasterBodyId, world),
                Body(TemplePriestBodyId, world),
                Body(UniversityHeadmasterBodyId, world),
                Body(ManorLordBodyId, world),
                Body(DukeBodyId, world),
                Body(MonarchBodyId, world),
                Item(SwordItemInstanceId, world),
                Item(ArrowItemInstanceId, world),
                WorldEntity(GuildChestEntityId, world),
                WorldEntity(DungeonDoorEntityId, world)
            };
        }

        public static IReadOnlyList<EntityPersonBodyBindingData> CreatePersonBodyBindings()
        {
            return new[]
            {
                new EntityPersonBodyBindingData { personId = PlayerPersonId, activeBodyId = PlayerBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = GuildMasterPersonId, activeBodyId = GuildMasterBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = MerchantPersonId, activeBodyId = MerchantBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = AdventurersGuildReceptionistPersonId, activeBodyId = AdventurersGuildReceptionistBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = MerchantGuildReceptionistPersonId, activeBodyId = MerchantGuildReceptionistBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrisonerPersonId, activeBodyId = PrisonerBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = RecordsClerkPersonId, activeBodyId = RecordsClerkBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.MayorPerson, activeBodyId = MayorBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.GuardPerson, activeBodyId = GuardBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.MagistratePerson, activeBodyId = MagistrateBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.MerchantGuildmasterPerson, activeBodyId = MerchantGuildmasterBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.TemplePriestPerson, activeBodyId = TemplePriestBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.UniversityHeadmasterPerson, activeBodyId = UniversityHeadmasterBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.ManorLordPerson, activeBodyId = ManorLordBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.DukePerson, activeBodyId = DukeBodyId, sourceId = "prototype.entity-location.bootstrap" },
                new EntityPersonBodyBindingData { personId = PrototypeInstitutionalContentIds.MonarchPerson, activeBodyId = MonarchBodyId, sourceId = "prototype.entity-location.bootstrap" }
            };
        }

        public static void SeedPrototypePlacements(EntityLocationRuntime runtime, LocationRuntime locations, string worldId = PersistenceService.LocalWorldId)
        {
            if (runtime == null)
            {
                return;
            }

            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            runtime.Configure(locations, world, CreateKnownEntities(world), null, CreatePersonBodyBindings());
            Place(runtime, Body(PlayerBodyId, world), "location.prototype.town", EntityPlacementCategory.Present, 0d);
            Place(runtime, Body(GuildMasterBodyId, world), "location.prototype.guildmaster-office", EntityPlacementCategory.WorkingPlaceholder, 1d);
            Place(runtime, Body(MerchantBodyId, world), "location.prototype.merchant-counter", EntityPlacementCategory.WorkingPlaceholder, 1d);
            Place(runtime, Body(AdventurersGuildReceptionistBodyId, world), "location.prototype.adventurers-guild", EntityPlacementCategory.WorkingPlaceholder, 1d);
            Place(runtime, Body(MerchantGuildReceptionistBodyId, world), "location.prototype.merchant-counter", EntityPlacementCategory.WorkingPlaceholder, 1d);
            Place(runtime, Body(PrisonerBodyId, world), "location.prototype.basement-prison", EntityPlacementCategory.Detained, 1d);
            Place(runtime, Body(RecordsClerkBodyId, world), "location.prototype.records-office", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(MayorBodyId, world), "location.prototype.mayor-office", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(GuardBodyId, world), "location.prototype.guard-station", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(MagistrateBodyId, world), "location.prototype.courthouse", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(MerchantGuildmasterBodyId, world), "location.prototype.merchant-guild", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(TemplePriestBodyId, world), "location.prototype.temple", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(UniversityHeadmasterBodyId, world), "location.prototype.university", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(ManorLordBodyId, world), "location.prototype.civic-office", EntityPlacementCategory.WorkingPlaceholder, 0d);
            Place(runtime, Body(DukeBodyId, world), "location.prototype.civic-office", EntityPlacementCategory.Present, 0d);
            Place(runtime, Body(MonarchBodyId, world), "location.prototype.civic-office", EntityPlacementCategory.Present, 0d);
            Place(runtime, Item(SwordItemInstanceId, world), "location.prototype.dungeon-entry", EntityPlacementCategory.Dropped, 1d);
            Place(runtime, Item(ArrowItemInstanceId, world), "location.prototype.market-district", EntityPlacementCategory.Dropped, 1d);
            Place(runtime, WorldEntity(GuildChestEntityId, world), "location.prototype.adventurers-guild", EntityPlacementCategory.Stored, 1d);
            Place(runtime, WorldEntity(DungeonDoorEntityId, world), "location.prototype.dungeon-entry", EntityPlacementCategory.Present, 1d);
        }

        public static EntityLocationReferenceData Person(string id, string worldId = PersistenceService.LocalWorldId)
        {
            return Reference(LocationOccupantEntityType.Person, id, worldId);
        }

        public static EntityLocationReferenceData Body(string id, string worldId = PersistenceService.LocalWorldId)
        {
            return Reference(LocationOccupantEntityType.Body, id, worldId);
        }

        public static EntityLocationReferenceData Item(string id, string worldId = PersistenceService.LocalWorldId)
        {
            return Reference(LocationOccupantEntityType.ItemInstance, id, worldId);
        }

        public static EntityLocationReferenceData WorldEntity(string id, string worldId = PersistenceService.LocalWorldId)
        {
            return Reference(LocationOccupantEntityType.WorldEntity, id, worldId);
        }

        private static EntityLocationReferenceData Reference(LocationOccupantEntityType type, string id, string worldId)
        {
            return new EntityLocationReferenceData
            {
                entityType = type,
                entityId = id ?? string.Empty,
                worldId = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim()
            };
        }

        private static void Place(EntityLocationRuntime runtime, EntityLocationReferenceData entity, string locationId, EntityPlacementCategory category, double worldTime)
        {
            runtime.Place(new EntityPlacementRequest
            {
                transactionId = $"prototype.entity-location.place.{entity.entityId}",
                placementId = $"placement.prototype.{entity.entityType.ToString().ToLowerInvariant()}.{entity.entityId}",
                entity = entity,
                exactLocationId = locationId,
                category = category,
                worldTime = worldTime,
                sourceEventId = "prototype.entity-location.bootstrap",
                provenanceId = "prototype.entity-location.factory"
            });
        }
    }
}
