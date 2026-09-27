using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.WorldLocations
{
    [CreateAssetMenu(fileName = "TravelModeDefinition", menuName = "Unity Isekai Game/World/Travel Mode Definition")]
    public class TravelModeDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string travelModeDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private TravelModeCategory category = TravelModeCategory.Walking;
        [SerializeField] private RouteSegmentCategory[] supportedRouteCategories = Array.Empty<RouteSegmentCategory>();
        [SerializeField] private double distanceMultiplier = 1d;
        [SerializeField] private double costMultiplier = 1d;
        [SerializeField] private double baseSpeedMetersPerSecond = 1.4d;
        [SerializeField] private string[] requiredCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] requiredEquipmentDefinitionIds = Array.Empty<string>();
        [SerializeField] private RouteVisibility visibility = RouteVisibility.Public;
        [SerializeField] private int version = 1;

        public string Id => travelModeDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public TravelModeCategory Category => category;
        public IReadOnlyList<RouteSegmentCategory> SupportedRouteCategories => supportedRouteCategories ?? Array.Empty<RouteSegmentCategory>();
        public double DistanceMultiplier => distanceMultiplier;
        public double CostMultiplier => costMultiplier;
        public double BaseSpeedMetersPerSecond => baseSpeedMetersPerSecond;
        public IReadOnlyList<string> RequiredCapabilityIds => requiredCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredEquipmentDefinitionIds => requiredEquipmentDefinitionIds ?? Array.Empty<string>();
        public RouteVisibility Visibility => visibility;
        public int Version => version;

        private void OnValidate()
        {
            travelModeDefinitionId = N(travelModeDefinitionId);
            displayName = N(displayName);
            supportedRouteCategories = CleanCategories(supportedRouteCategories);
            requiredCapabilityIds = Clean(requiredCapabilityIds);
            requiredEquipmentDefinitionIds = Clean(requiredEquipmentDefinitionIds);
            distanceMultiplier = ValidPositive(distanceMultiplier) ? distanceMultiplier : 1d;
            costMultiplier = ValidPositive(costMultiplier) ? costMultiplier : 1d;
            baseSpeedMetersPerSecond = ValidPositive(baseSpeedMetersPerSecond) ? baseSpeedMetersPerSecond : 1.4d;
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(string id, string display, TravelModeCategory modeCategory, IEnumerable<RouteSegmentCategory> routeCategories, double distanceScale = 1d, double costScale = 1d, IEnumerable<string> capabilities = null, IEnumerable<string> equipment = null, RouteVisibility modeVisibility = RouteVisibility.Public, double baseSpeed = 1.4d)
        {
            travelModeDefinitionId = N(id);
            displayName = string.IsNullOrWhiteSpace(display) ? travelModeDefinitionId : display.Trim();
            description = string.Empty;
            category = modeCategory;
            supportedRouteCategories = CleanCategories(routeCategories);
            distanceMultiplier = distanceScale;
            costMultiplier = costScale;
            baseSpeedMetersPerSecond = baseSpeed;
            requiredCapabilityIds = Clean(capabilities);
            requiredEquipmentDefinitionIds = Clean(equipment);
            visibility = modeVisibility;
            version = 1;
        }

        public bool SupportsCategory(RouteSegmentCategory routeCategory)
        {
            RouteSegmentCategory[] supported = CleanCategories(supportedRouteCategories);
            return supported.Length == 0 || supported.Contains(routeCategory);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Travel Mode Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("travel-mode-definition.", StringComparison.Ordinal)) report.AddWarning($"Travel Mode Definition '{Id}' should use the 'travel-mode-definition.' namespace prefix.");
            if (!Enum.IsDefined(typeof(TravelModeCategory), category) || category == TravelModeCategory.Unknown) report.AddError($"Travel Mode Definition '{DisplayName}' must declare a concrete category.");
            if (!ValidPositive(distanceMultiplier)) report.AddError($"Travel Mode Definition '{DisplayName}' has invalid distance multiplier.");
            if (!ValidPositive(costMultiplier)) report.AddError($"Travel Mode Definition '{DisplayName}' has invalid cost multiplier.");
            if (!ValidPositive(baseSpeedMetersPerSecond)) report.AddError($"Travel Mode Definition '{DisplayName}' has invalid base speed.");
        }

        private static bool ValidPositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static RouteSegmentCategory[] CleanCategories(IEnumerable<RouteSegmentCategory> values) => (values ?? Array.Empty<RouteSegmentCategory>()).Where(value => Enum.IsDefined(typeof(RouteSegmentCategory), value) && value != RouteSegmentCategory.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
    }

    [CreateAssetMenu(fileName = "RouteSegmentDefinition", menuName = "Unity Isekai Game/World/Route Segment Definition")]
    public class RouteSegmentDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string routeSegmentDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private RouteSegmentCategory category = RouteSegmentCategory.Path;
        [SerializeField] private LocationConnectionDirectionality defaultDirectionality = LocationConnectionDirectionality.Bidirectional;
        [SerializeField] private double defaultDistanceMeters = 10d;
        [SerializeField] private double defaultCostUnits = 10d;
        [SerializeField] private double costMultiplier = 1d;
        [SerializeField] private bool allowZeroDistance;
        [SerializeField] private bool supportsAccessPolicies = true;
        [SerializeField] private bool supportsNetworkMembership = true;
        [SerializeField] private bool supportsSceneBinding;
        [SerializeField] private bool mayBeHidden = true;
        [SerializeField] private RouteVisibility defaultVisibility = RouteVisibility.Public;
        [SerializeField] private string[] supportedTravelModeDefinitionIds = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => routeSegmentDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public RouteSegmentCategory Category => category;
        public LocationConnectionDirectionality DefaultDirectionality => defaultDirectionality;
        public double DefaultDistanceMeters => defaultDistanceMeters;
        public double DefaultCostUnits => defaultCostUnits;
        public double CostMultiplier => costMultiplier;
        public bool AllowZeroDistance => allowZeroDistance;
        public bool SupportsAccessPolicies => supportsAccessPolicies;
        public bool SupportsNetworkMembership => supportsNetworkMembership;
        public bool SupportsSceneBinding => supportsSceneBinding;
        public bool MayBeHidden => mayBeHidden;
        public RouteVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> SupportedTravelModeDefinitionIds => supportedTravelModeDefinitionIds ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            routeSegmentDefinitionId = N(routeSegmentDefinitionId);
            displayName = N(displayName);
            supportedTravelModeDefinitionIds = Clean(supportedTravelModeDefinitionIds);
            defaultDistanceMeters = ValidNonNegative(defaultDistanceMeters) ? defaultDistanceMeters : 0d;
            defaultCostUnits = ValidNonNegative(defaultCostUnits) ? defaultCostUnits : 0d;
            costMultiplier = ValidPositive(costMultiplier) ? costMultiplier : 1d;
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(string id, string display, RouteSegmentCategory segmentCategory, LocationConnectionDirectionality directionality, double distanceMeters, double costUnits, IEnumerable<string> travelModes, bool zeroDistance = false, bool accessPolicies = true, bool networkMembership = true, bool sceneBinding = false, bool hidden = true, RouteVisibility visibility = RouteVisibility.Public, double multiplier = 1d)
        {
            routeSegmentDefinitionId = N(id);
            displayName = string.IsNullOrWhiteSpace(display) ? routeSegmentDefinitionId : display.Trim();
            description = string.Empty;
            category = segmentCategory;
            defaultDirectionality = directionality;
            defaultDistanceMeters = distanceMeters;
            defaultCostUnits = costUnits;
            costMultiplier = multiplier;
            supportedTravelModeDefinitionIds = Clean(travelModes);
            allowZeroDistance = zeroDistance;
            supportsAccessPolicies = accessPolicies;
            supportsNetworkMembership = networkMembership;
            supportsSceneBinding = sceneBinding;
            mayBeHidden = hidden;
            defaultVisibility = visibility;
            version = 1;
        }

        public bool SupportsTravelMode(string travelModeDefinitionId)
        {
            string[] modes = Clean(supportedTravelModeDefinitionIds);
            return modes.Length == 0 || modes.Contains(N(travelModeDefinitionId), StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Route Segment Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("route-segment-definition.", StringComparison.Ordinal)) report.AddWarning($"Route Segment Definition '{Id}' should use the 'route-segment-definition.' namespace prefix.");
            if (!Enum.IsDefined(typeof(RouteSegmentCategory), category) || category == RouteSegmentCategory.Unknown) report.AddError($"Route Segment Definition '{DisplayName}' must declare a concrete category.");
            if (!Enum.IsDefined(typeof(LocationConnectionDirectionality), defaultDirectionality) || defaultDirectionality == LocationConnectionDirectionality.Unknown) report.AddError($"Route Segment Definition '{DisplayName}' must declare concrete directionality.");
            if (!ValidNonNegative(defaultDistanceMeters) || (!allowZeroDistance && defaultDistanceMeters <= 0d)) report.AddError($"Route Segment Definition '{DisplayName}' has invalid default distance.");
            if (!ValidNonNegative(defaultCostUnits)) report.AddError($"Route Segment Definition '{DisplayName}' has invalid default cost.");
            if (!ValidPositive(costMultiplier)) report.AddError($"Route Segment Definition '{DisplayName}' has invalid cost multiplier.");
            foreach (string modeId in Clean(supportedTravelModeDefinitionIds))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(modeId, out IGameDefinition definition) || definition is not TravelModeDefinition) report.AddError($"Route Segment Definition '{DisplayName}' references missing Travel Mode '{modeId}'.");
            }
            if (!mayBeHidden && (defaultVisibility == RouteVisibility.Secret || defaultVisibility == RouteVisibility.Hidden)) report.AddError($"Route Segment Definition '{DisplayName}' forbids hidden visibility but defaults to {defaultVisibility}.");
        }

        private static bool ValidNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        private static bool ValidPositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
