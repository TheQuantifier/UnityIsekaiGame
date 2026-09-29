using System.Collections.Generic;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class LocationValidationService
    {
        private readonly DefinitionRegistry registry;
        private readonly string worldId;

        public LocationValidationService(DefinitionRegistry registry, string worldId = PersistenceService.LocalWorldId)
        {
            this.registry = registry;
            this.worldId = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
        }

        public LocationValidationReport ValidateSaveData(LocationRuntimeSaveData saveData, IEnumerable<string> knownProperties = null, IEnumerable<string> knownOrganizations = null, IEnumerable<string> knownGovernments = null, IEnumerable<string> knownTerritories = null)
        {
            LocationRuntime.ValidateSaveData(saveData, registry, worldId, knownProperties, knownOrganizations, knownGovernments, knownTerritories, out _, out LocationValidationReport report);
            return report;
        }

        public LocationValidationReport ValidateRuntime(LocationRuntime runtime)
        {
            return runtime == null ? ValidateSaveData(new LocationRuntimeSaveData()) : runtime.ValidateRuntime();
        }
    }
}
