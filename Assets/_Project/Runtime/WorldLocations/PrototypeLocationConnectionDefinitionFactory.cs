using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypeLocationConnectionDefinitionFactory
    {
        public const string PublicDoorwayDefinitionId = "location-connection-definition.prototype.public-doorway";
        public const string StandardDoorDefinitionId = "location-connection-definition.prototype.standard-door";
        public const string LockableDoorDefinitionId = "location-connection-definition.prototype.lockable-door";
        public const string PrisonCellDoorDefinitionId = "location-connection-definition.prototype.prison-cell-door";
        public const string RestrictedOfficeDoorDefinitionId = "location-connection-definition.prototype.restricted-office-door";
        public const string PublicBuildingEntranceDefinitionId = "location-connection-definition.prototype.public-building-entrance";
        public const string GuildMemberDoorDefinitionId = "location-connection-definition.prototype.guild-member-door";
        public const string StorageDoorDefinitionId = "location-connection-definition.prototype.storage-door";
        public const string DungeonEntranceDefinitionId = "location-connection-definition.prototype.dungeon-entrance";
        public const string HiddenPassageDefinitionId = "location-connection-definition.prototype.hidden-passage";
        public const string OneWayConnectionDefinitionId = "location-connection-definition.prototype.one-way-connection";

        public const string PublicAccessPolicyId = "location-access-policy-definition.prototype.public";
        public const string GuildMemberAccessPolicyId = "location-access-policy-definition.prototype.guild-member";
        public const string GuildRankAccessPolicyId = "location-access-policy-definition.prototype.guild-rank-iron";
        public const string GuildHeadOfficeAccessPolicyId = "location-access-policy-definition.prototype.guild-head-office";
        public const string MayorOfficeAccessPolicyId = "location-access-policy-definition.prototype.mayor-office";
        public const string RecordsAuthorityAccessPolicyId = "location-access-policy-definition.prototype.records-authority";
        public const string RecordsEmploymentAccessPolicyId = "location-access-policy-definition.prototype.records-clerk-employment";
        public const string StorageOwnershipAccessPolicyId = "location-access-policy-definition.prototype.storage-property-owner";
        public const string StorageKeyAccessPolicyId = "location-access-policy-definition.prototype.storage-key";
        public const string LegalPermitAccessPolicyId = "location-access-policy-definition.prototype.legal-permit";
        public const string WarrantAccessPolicyId = "location-access-policy-definition.prototype.search-warrant";
        public const string CustodyGuardAccessPolicyId = "location-access-policy-definition.prototype.custody-guard";
        public const string PrisonerCustodyAccessPolicyId = "location-access-policy-definition.prototype.prisoner-custody";
        public const string ExplicitWhitelistAccessPolicyId = "location-access-policy-definition.prototype.explicit-whitelist";

        public const string VillageGuildEntranceConnectionId = "location-connection.prototype.village-guild-entrance";
        public const string VillageCivicEntranceConnectionId = "location-connection.prototype.village-civic-entrance";
        public const string MarketMerchantCounterConnectionId = "location-connection.prototype.market-merchant-counter";
        public const string GuildHeadOfficeConnectionId = "location-connection.prototype.guild-head-office";
        public const string MayorOfficeConnectionId = "location-connection.prototype.mayor-office";
        public const string RecordsOfficeConnectionId = "location-connection.prototype.records-office";
        public const string GuildStorageConnectionId = "location-connection.prototype.guild-storage";
        public const string PrisonCellConnectionId = "location-connection.prototype.prison-cell-door";
        public const string WildernessDungeonConnectionId = "location-connection.prototype.wilderness-dungeon-entrance";
        public const string DungeonOneWayDropConnectionId = "location-connection.prototype.dungeon-one-way-drop";
        public const string HiddenPassageConnectionId = "location-connection.prototype.hidden-guild-prison-passage";

        public static DefinitionRegistry AddMissingPrototypeConnectionDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingConnectionDefinitions(ids));
            definitions.AddRange(CreateMissingAccessPolicyDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<LocationConnectionDefinition> CreateMissingConnectionDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<LocationConnectionDefinition> definitions = new List<LocationConnectionDefinition>();
            LocationCategory[] anyInterior = { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea, LocationCategory.Dungeon };
            LocationCategory[] publicAreas = { LocationCategory.Settlement, LocationCategory.District, LocationCategory.Wilderness };

            AddConnection(definitions, ids, PublicDoorwayDefinitionId, "Prototype Public Doorway", LocationConnectionCategory.Doorway, LocationConnectionDirectionality.Bidirectional, publicAreas.Concat(anyInterior), publicAreas.Concat(anyInterior), open: false, supportsLock: false, key: false);
            AddConnection(definitions, ids, StandardDoorDefinitionId, "Prototype Standard Door", LocationConnectionCategory.Door, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior, open: true, supportsLock: false, key: false);
            AddConnection(definitions, ids, LockableDoorDefinitionId, "Prototype Lockable Door", LocationConnectionCategory.LockedDoor, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior, open: true, supportsLock: true, key: true);
            AddConnection(definitions, ids, PrisonCellDoorDefinitionId, "Prototype Prison Cell Door", LocationConnectionCategory.CellDoor, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior, open: true, supportsLock: true, key: true, visibility: LocationConnectionVisibility.Restricted);
            AddConnection(definitions, ids, RestrictedOfficeDoorDefinitionId, "Prototype Restricted Office Door", LocationConnectionCategory.Door, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior, open: true, supportsLock: true, key: true, visibility: LocationConnectionVisibility.Restricted);
            AddConnection(definitions, ids, PublicBuildingEntranceDefinitionId, "Prototype Public Building Entrance", LocationConnectionCategory.BuildingEntrance, LocationConnectionDirectionality.Bidirectional, publicAreas, anyInterior, open: false, supportsLock: false, key: false);
            AddConnection(definitions, ids, GuildMemberDoorDefinitionId, "Prototype Guild Member Door", LocationConnectionCategory.Door, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior, open: true, supportsLock: true, key: true);
            AddConnection(definitions, ids, StorageDoorDefinitionId, "Prototype Storage Door", LocationConnectionCategory.LockedDoor, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior, open: true, supportsLock: true, key: true, visibility: LocationConnectionVisibility.Restricted);
            AddConnection(definitions, ids, DungeonEntranceDefinitionId, "Prototype Dungeon Entrance", LocationConnectionCategory.DungeonEntrance, LocationConnectionDirectionality.Bidirectional, new[] { LocationCategory.Wilderness, LocationCategory.Settlement, LocationCategory.District }, new[] { LocationCategory.Dungeon }, open: false, supportsLock: false, key: false, visibility: LocationConnectionVisibility.Secret);
            AddConnection(definitions, ids, HiddenPassageDefinitionId, "Prototype Hidden Passage", LocationConnectionCategory.HiddenPassage, LocationConnectionDirectionality.Bidirectional, anyInterior, anyInterior.Concat(new[] { LocationCategory.Dungeon }), open: false, supportsLock: false, key: false, visibility: LocationConnectionVisibility.Hidden);
            AddConnection(definitions, ids, OneWayConnectionDefinitionId, "Prototype One-Way Connection", LocationConnectionCategory.OneWayDropPlaceholder, LocationConnectionDirectionality.SourceToDestinationOnly, publicAreas.Concat(anyInterior), publicAreas.Concat(anyInterior), open: false, supportsLock: false, key: false);
            return definitions;
        }

        public static IReadOnlyList<LocationAccessPolicyDefinition> CreateMissingAccessPolicyDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<LocationAccessPolicyDefinition> definitions = new List<LocationAccessPolicyDefinition>();
            AddPolicy(definitions, ids, PublicAccessPolicyId, "Prototype Public Access", LocationAccessPolicyCategory.Public, allow: true);
            AddPolicy(definitions, ids, GuildMemberAccessPolicyId, "Prototype Guild Member Access", LocationAccessPolicyCategory.OrganizationMembers, organizations: new[] { "organization.prototype.adventurers-guild" });
            AddPolicy(definitions, ids, GuildRankAccessPolicyId, "Prototype Guild Rank Access", LocationAccessPolicyCategory.MinimumRank, organizations: new[] { "organization.prototype.adventurers-guild" }, ranks: new[] { "rank.prototype.guild.iron" });
            AddPolicy(definitions, ids, GuildHeadOfficeAccessPolicyId, "Prototype Guild Head Office Access", LocationAccessPolicyCategory.SpecificOffice, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" });
            AddPolicy(definitions, ids, MayorOfficeAccessPolicyId, "Prototype Mayor Office Access", LocationAccessPolicyCategory.SpecificOffice, offices: new[] { "office.prototype.mayor" }, authorities: new[] { "authority.government.prototype" });
            AddPolicy(definitions, ids, RecordsAuthorityAccessPolicyId, "Prototype Records Authority Access", LocationAccessPolicyCategory.AuthorizedStaff, authorities: new[] { "permission.prototype.records.restricted-read" });
            AddPolicy(definitions, ids, RecordsEmploymentAccessPolicyId, "Prototype Records Clerk Employment Access", LocationAccessPolicyCategory.AuthorizedStaff, employments: new[] { "employment.prototype.records-clerk" });
            AddPolicy(definitions, ids, StorageOwnershipAccessPolicyId, "Prototype Storage Owner Access", LocationAccessPolicyCategory.PrivateOwnerOnly, properties: new[] { "property.prototype.guild-storage" });
            AddPolicy(definitions, ids, StorageKeyAccessPolicyId, "Prototype Storage Key Access", LocationAccessPolicyCategory.KeyRequired, keyDefinitions: new[] { "item.prototype-storage-key" });
            AddPolicy(definitions, ids, LegalPermitAccessPolicyId, "Prototype Legal Permit Access", LocationAccessPolicyCategory.LegalPermitRequired, permits: new[] { "legal-right.prototype.records.restricted-read" });
            AddPolicy(definitions, ids, WarrantAccessPolicyId, "Prototype Search Warrant Access", LocationAccessPolicyCategory.WarrantRequired, warrants: new[] { "warrant.prototype.search" });
            AddPolicy(definitions, ids, CustodyGuardAccessPolicyId, "Prototype Custody Guard Access", LocationAccessPolicyCategory.CustodyAuthorized, custodyRoles: new[] { "custody-role.prototype.guard" });
            AddPolicy(definitions, ids, PrisonerCustodyAccessPolicyId, "Prototype Prisoner Custody Access", LocationAccessPolicyCategory.CustodyAuthorized, custodyRoles: new[] { "custody-role.prototype.prisoner" });
            AddPolicy(definitions, ids, ExplicitWhitelistAccessPolicyId, "Prototype Explicit Whitelist Access", LocationAccessPolicyCategory.ExplicitWhitelist, whitelist: new[] { PrototypeEntityLocationFactory.GuildMasterPersonId });
            return definitions;
        }

        public static void SeedPrototypeConnections(LocationConnectionRuntime runtime, DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactionPoints, string worldId)
        {
            if (runtime == null)
            {
                return;
            }

            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            runtime.Configure(registry, locations, entityLocations, interactionPoints, world);
            Seed(runtime, VillageGuildEntranceConnectionId, PublicBuildingEntranceDefinitionId, "Village to Adventurers Guild Entrance", "location.prototype.village", "location.prototype.adventurers-guild", policies: new[] { PublicAccessPolicyId }, binding: "prototype.connection.village-guild");
            Seed(runtime, VillageCivicEntranceConnectionId, PublicBuildingEntranceDefinitionId, "Village to Civic Office Entrance", "location.prototype.village", "location.prototype.civic-office", policies: new[] { PublicAccessPolicyId }, binding: "prototype.connection.village-civic");
            Seed(runtime, MarketMerchantCounterConnectionId, PublicDoorwayDefinitionId, "Market to Merchant Counter", "location.prototype.market-district", "location.prototype.merchant-counter", policies: new[] { PublicAccessPolicyId }, binding: "prototype.connection.market-merchant");
            Seed(runtime, GuildHeadOfficeConnectionId, RestrictedOfficeDoorDefinitionId, "Guild Hall to Guild Head Office", "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", policies: new[] { GuildHeadOfficeAccessPolicyId, GuildMemberAccessPolicyId }, open: LocationConnectionOpenState.Closed, locked: LocationConnectionLockState.Locked, binding: "prototype.connection.guild-head-door");
            Seed(runtime, MayorOfficeConnectionId, RestrictedOfficeDoorDefinitionId, "Civic Office to Mayor Office", "location.prototype.civic-office", "location.prototype.mayor-office", policies: new[] { MayorOfficeAccessPolicyId }, open: LocationConnectionOpenState.Closed, locked: LocationConnectionLockState.Locked, binding: "prototype.connection.mayor-door");
            Seed(runtime, RecordsOfficeConnectionId, RestrictedOfficeDoorDefinitionId, "Civic Office to Restricted Records", "location.prototype.civic-office", "location.prototype.mayor-office", policies: new[] { RecordsAuthorityAccessPolicyId, RecordsEmploymentAccessPolicyId, LegalPermitAccessPolicyId, WarrantAccessPolicyId }, open: LocationConnectionOpenState.Closed, locked: LocationConnectionLockState.Locked, binding: "prototype.connection.records-door");
            Seed(runtime, GuildStorageConnectionId, StorageDoorDefinitionId, "Guild Hall to Guild Storage", "location.prototype.adventurers-guild", "location.prototype.merchant-counter", policies: new[] { StorageOwnershipAccessPolicyId, StorageKeyAccessPolicyId }, open: LocationConnectionOpenState.Closed, locked: LocationConnectionLockState.Locked, visibility: LocationConnectionVisibility.Restricted, binding: "prototype.connection.guild-storage");
            Seed(runtime, PrisonCellConnectionId, PrisonCellDoorDefinitionId, "Detention Area to Prison Cell", "location.prototype.civic-office", "location.prototype.basement-prison", policies: new[] { CustodyGuardAccessPolicyId }, open: LocationConnectionOpenState.Closed, locked: LocationConnectionLockState.Locked, visibility: LocationConnectionVisibility.Restricted, binding: "prototype.connection.prison-cell");
            Seed(runtime, WildernessDungeonConnectionId, DungeonEntranceDefinitionId, "Wilderness to Dungeon Entrance", "location.prototype.wilderness-ring", "location.prototype.dungeon-entry", policies: new[] { PublicAccessPolicyId }, visibility: LocationConnectionVisibility.Secret, binding: "prototype.connection.dungeon-entrance");
            Seed(runtime, DungeonOneWayDropConnectionId, OneWayConnectionDefinitionId, "Dungeon One-Way Drop", "location.prototype.wilderness-ring", "location.prototype.dungeon-entry", directionality: LocationConnectionDirectionality.SourceToDestinationOnly, policies: new[] { PublicAccessPolicyId }, visibility: LocationConnectionVisibility.Restricted, binding: "prototype.connection.dungeon-drop");
            Seed(runtime, HiddenPassageConnectionId, HiddenPassageDefinitionId, "Hidden Guild-Prison Passage", "location.prototype.guildmaster-office", "location.prototype.basement-prison", policies: new[] { ExplicitWhitelistAccessPolicyId }, visibility: LocationConnectionVisibility.Hidden, binding: "prototype.connection.hidden-passage");
        }

        private static void AddConnection(ICollection<LocationConnectionDefinition> definitions, ISet<string> ids, string id, string display, LocationConnectionCategory category, LocationConnectionDirectionality directionality, IEnumerable<LocationCategory> sourceCategories, IEnumerable<LocationCategory> destinationCategories, bool open, bool supportsLock, bool key, LocationConnectionVisibility visibility = LocationConnectionVisibility.Public)
        {
            if (ids.Contains(id)) return;
            LocationConnectionDefinition definition = ScriptableObject.CreateInstance<LocationConnectionDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, directionality, sourceCategories, destinationCategories, accessPoint: false, openState: open, lockState: supportsLock, blockageState: true, destructionState: true, sceneBinding: true, keyAccess: key, institutionalAccess: true, visibility: visibility);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddPolicy(ICollection<LocationAccessPolicyDefinition> definitions, ISet<string> ids, string id, string display, LocationAccessPolicyCategory category, int priority = 0, bool allow = false, bool deny = false, IEnumerable<string> organizations = null, IEnumerable<string> ranks = null, IEnumerable<string> offices = null, IEnumerable<string> authorities = null, IEnumerable<string> employments = null, IEnumerable<string> properties = null, IEnumerable<string> permits = null, IEnumerable<string> warrants = null, IEnumerable<string> custodyRoles = null, IEnumerable<string> keyInstances = null, IEnumerable<string> keyDefinitions = null, IEnumerable<string> credentials = null, IEnumerable<string> whitelist = null, IEnumerable<string> blacklist = null)
        {
            if (ids.Contains(id)) return;
            LocationAccessPolicyDefinition definition = ScriptableObject.CreateInstance<LocationAccessPolicyDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, priority, allow, deny, organizations, ranks, offices, authorities, employments, properties, permits, warrants, custodyRoles, keyInstances, keyDefinitions, credentials, whitelist, blacklist);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void Seed(LocationConnectionRuntime runtime, string id, string definitionId, string display, string source, string destination, LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Unknown, IEnumerable<string> policies = null, LocationConnectionOpenState open = LocationConnectionOpenState.Open, LocationConnectionLockState locked = LocationConnectionLockState.NotLockable, LocationConnectionVisibility visibility = LocationConnectionVisibility.Public, string binding = null)
        {
            runtime.CreateConnection(new LocationConnectionCreateRequest
            {
                transactionId = $"prototype.seed.{id}",
                connectionId = id,
                connectionDefinitionId = definitionId,
                displayName = display,
                sourceLocationId = source,
                destinationLocationId = destination,
                directionality = directionality,
                openState = open,
                lockState = locked,
                blockageState = LocationConnectionBlockageState.Clear,
                accessPolicyDefinitionIds = (policies ?? Array.Empty<string>()).ToArray(),
                visibility = visibility,
                sceneBindingKey = binding,
                sceneBindingCategory = LocationConnectionSceneBindingCategory.PrototypeMarker,
                worldTime = 0d,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.location-connection.seed"
            });
        }
    }
}
