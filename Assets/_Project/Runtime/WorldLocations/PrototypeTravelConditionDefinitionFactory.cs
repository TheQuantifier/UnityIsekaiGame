using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypeTravelConditionDefinitionFactory
    {
        public const string MuddyRoadConditionId = "travel-condition-definition.prototype.muddy-road";
        public const string CollapsedPassConditionId = "travel-condition-definition.prototype.collapsed-pass";
        public const string ClimbingRequiredConditionId = "travel-condition-definition.prototype.climbing-required";
        public const string TorchRequiredConditionId = "travel-condition-definition.prototype.torch-required";
        public const string HiddenAmbushRiskConditionId = "travel-condition-definition.prototype.hidden-ambush-risk";
        public const string HeatConditionId = "travel-condition-definition.prototype.extreme-heat";

        public const string FallRiskHazardId = "travel-hazard-definition.prototype.fall-risk";
        public const string HeatExposureHazardId = "travel-hazard-definition.prototype.heat-exposure";

        public const string RoadsideEncounterId = "travel-encounter-definition.prototype.neutral-roadside";
        public const string HiddenAmbushEncounterId = "travel-encounter-definition.prototype.hidden-ambush";
        public const string DiscoveryEncounterId = "travel-encounter-definition.prototype.discovery";

        public const string ClimbCapabilityId = "capability.prototype.movement.climb";
        public const string TorchEquipmentDefinitionId = "item.prototype-torch";

        public static DefinitionRegistry AddMissingPrototypeTravelConditionDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingHazardDefinitions(ids));
            definitions.AddRange(CreateMissingEncounterDefinitions(ids));
            definitions.AddRange(CreateMissingConditionDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<TravelConditionDefinition> CreateMissingConditionDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<TravelConditionDefinition> definitions = new List<TravelConditionDefinition>();
            AddCondition(definitions, ids, MuddyRoadConditionId, "Prototype Muddy Road", TravelConditionCategory.Terrain, new[] { TravelConditionTargetScope.RouteSegment, TravelConditionTargetScope.Connection, TravelConditionTargetScope.RouteEdge }, TravelConditionSeverity.Moderate, 0.6d, 1.4d, false, priority: 10);
            AddCondition(definitions, ids, CollapsedPassConditionId, "Prototype Collapsed Pass", TravelConditionCategory.Obstruction, new[] { TravelConditionTargetScope.RouteSegment, TravelConditionTargetScope.Connection, TravelConditionTargetScope.RouteEdge }, TravelConditionSeverity.Severe, 1d, 1d, true, priority: 100);
            AddCondition(definitions, ids, ClimbingRequiredConditionId, "Prototype Climbing Required", TravelConditionCategory.Requirement, new[] { TravelConditionTargetScope.RouteSegment, TravelConditionTargetScope.Location }, TravelConditionSeverity.Major, 0.8d, 1.25d, false, capabilities: new[] { ClimbCapabilityId }, priority: 50);
            AddCondition(definitions, ids, TorchRequiredConditionId, "Prototype Torch Required", TravelConditionCategory.Requirement, new[] { TravelConditionTargetScope.Location, TravelConditionTargetScope.RouteSegment, TravelConditionTargetScope.Journey }, TravelConditionSeverity.Moderate, 0.85d, 1.1d, false, equipment: new[] { TorchEquipmentDefinitionId }, priority: 40);
            AddCondition(definitions, ids, HiddenAmbushRiskConditionId, "Prototype Hidden Ambush Risk", TravelConditionCategory.EncounterRisk, new[] { TravelConditionTargetScope.RouteSegment, TravelConditionTargetScope.RouteEdge, TravelConditionTargetScope.Journey }, TravelConditionSeverity.Major, 0.9d, 1.3d, false, encounters: new[] { HiddenAmbushEncounterId }, visibility: TravelConditionVisibility.Hidden, priority: 75);
            AddCondition(definitions, ids, HeatConditionId, "Prototype Extreme Heat", TravelConditionCategory.Weather, new[] { TravelConditionTargetScope.Location, TravelConditionTargetScope.RouteNetwork, TravelConditionTargetScope.Journey }, TravelConditionSeverity.Major, 0.7d, 1.5d, false, hazards: new[] { HeatExposureHazardId }, priority: 60);
            return definitions;
        }

        public static IReadOnlyList<TravelHazardDefinition> CreateMissingHazardDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<TravelHazardDefinition> definitions = new List<TravelHazardDefinition>();
            AddHazard(definitions, ids, FallRiskHazardId, "Prototype Fall Risk", TravelHazardCategory.Terrain, TravelHazardTriggerPolicy.ExplicitOnly, tags: new[] { "hazard.travel.fall", "delegates.injury" }, priority: 20);
            AddHazard(definitions, ids, HeatExposureHazardId, "Prototype Heat Exposure", TravelHazardCategory.Environmental, TravelHazardTriggerPolicy.ExplicitOnly, tags: new[] { "hazard.travel.heat", "delegates.biological-condition" }, priority: 15);
            return definitions;
        }

        public static IReadOnlyList<TravelEncounterDefinition> CreateMissingEncounterDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<TravelEncounterDefinition> definitions = new List<TravelEncounterDefinition>();
            AddEncounter(definitions, ids, RoadsideEncounterId, "Prototype Roadside Encounter", TravelEncounterCategory.Neutral, TravelEncounterTriggerPolicy.ExplicitOnly, TravelEncounterRepeatPolicy.OncePerJourney, TravelEncounterInterruptionPolicy.PauseJourney, roles: new[] { "traveler", "roadside-actor" }, priority: 10);
            AddEncounter(definitions, ids, HiddenAmbushEncounterId, "Prototype Hidden Ambush", TravelEncounterCategory.Hostile, TravelEncounterTriggerPolicy.JourneyCheckpoint, TravelEncounterRepeatPolicy.OncePerJourney, TravelEncounterInterruptionPolicy.BlockJourney, combatReference: "combat-encounter.prototype.ambush", roles: new[] { "traveler", "ambusher" }, visibility: TravelConditionVisibility.Hidden, priority: 90);
            AddEncounter(definitions, ids, DiscoveryEncounterId, "Prototype Discovery Encounter", TravelEncounterCategory.Discovery, TravelEncounterTriggerPolicy.ExplicitOnly, TravelEncounterRepeatPolicy.OncePerRouteEdge, TravelEncounterInterruptionPolicy.None, roles: new[] { "traveler" }, priority: 5);
            return definitions;
        }

        public static void SeedPrototypeTravelConditions(TravelConditionRuntime runtime, DefinitionRegistry registry, LocationRouteRuntime routes, TravelJourneyRuntime journeys, string worldId)
        {
            if (runtime == null) return;
            runtime.Configure(registry, routes, journeys, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim());
            runtime.CreateCondition(new TravelConditionCreateRequest
            {
                transactionId = "prototype.seed.travel-condition.wilderness-trail-mud",
                conditionId = "travel-condition.prototype.wilderness-trail-mud",
                conditionDefinitionId = MuddyRoadConditionId,
                target = new TravelConditionTargetReferenceData
                {
                    scope = TravelConditionTargetScope.RouteSegment,
                    targetId = PrototypeLocationRouteDefinitionFactory.TownWildernessTrailSegmentId,
                    sourceLocationId = "location.prototype.town",
                    destinationLocationId = "location.prototype.wilderness-ring",
                    edgeKind = RouteEdgeKind.RouteSegment
                },
                startsWorldTime = 0d,
                endsWorldTime = -1d,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.travel-condition.seed"
            });
        }

        private static void AddCondition(ICollection<TravelConditionDefinition> definitions, ISet<string> ids, string id, string display, TravelConditionCategory category, IEnumerable<TravelConditionTargetScope> scopes, TravelConditionSeverity severity, double movementMultiplier, double costMultiplier, bool blocksTravel, IEnumerable<string> restrictedModes = null, IEnumerable<string> capabilities = null, IEnumerable<string> equipment = null, IEnumerable<string> hazards = null, IEnumerable<string> encounters = null, TravelConditionVisibility visibility = TravelConditionVisibility.Public, int priority = 0)
        {
            if (ids.Contains(id)) return;
            TravelConditionDefinition definition = ScriptableObject.CreateInstance<TravelConditionDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, scopes, severity, movementMultiplier, costMultiplier, blocksTravel, restrictedModes, capabilities, equipment, false, hazards, encounters, visibility, TravelConditionStackingPolicy.Multiplicative, -1d, priority);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddHazard(ICollection<TravelHazardDefinition> definitions, ISet<string> ids, string id, string display, TravelHazardCategory category, TravelHazardTriggerPolicy policy, IEnumerable<string> tags = null, int priority = 0)
        {
            if (ids.Contains(id)) return;
            TravelHazardDefinition definition = ScriptableObject.CreateInstance<TravelHazardDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, policy, tags: tags, hazardPriority: priority);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddEncounter(ICollection<TravelEncounterDefinition> definitions, ISet<string> ids, string id, string display, TravelEncounterCategory category, TravelEncounterTriggerPolicy policy, TravelEncounterRepeatPolicy repeat, TravelEncounterInterruptionPolicy interruption, string combatReference = "", IEnumerable<string> roles = null, TravelConditionVisibility visibility = TravelConditionVisibility.Public, int priority = 0)
        {
            if (ids.Contains(id)) return;
            TravelEncounterDefinition definition = ScriptableObject.CreateInstance<TravelEncounterDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, policy, repeat, interruption, combatReference, roles, visibility, priority);
            definitions.Add(definition);
            ids.Add(id);
        }
    }
}
