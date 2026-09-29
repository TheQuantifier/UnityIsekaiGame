using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypePoliticalTravelFactory
    {
        public const string DungeonEntryCheckpointId = "border-checkpoint.prototype.dungeon-entry";

        public static void SeedPrototypeCheckpoints(PoliticalTravelRuntime runtime)
        {
            runtime?.CreateCheckpoint(new BorderCheckpointCreateRequest
            {
                transactionId = "prototype.seed.border-checkpoint.dungeon-entry",
                checkpointId = DungeonEntryCheckpointId,
                displayName = "Dungeon Entry Checkpoint",
                locationId = "location.prototype.dungeon-entry",
                governingGovernmentId = PrototypeInstitutionalContentIds.TownGovernment,
                jurisdictionId = PrototypeInstitutionalContentIds.TownJurisdiction,
                policy = BorderCheckpointPolicy.RequireAuthorization,
                requiredActionIds = new[] { "government.action.enter-restricted-route" },
                requiredPermitIds = new[] { PrototypeGovernmentDefinitionFactory.TravelWritDefinitionId },
                visibility = PoliticalVisibility.Public,
                worldTime = 0d,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.political-travel.seed"
            });
        }
    }
}
