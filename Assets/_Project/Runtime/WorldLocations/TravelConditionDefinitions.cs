using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.WorldLocations
{
    [CreateAssetMenu(fileName = "TravelConditionDefinition", menuName = "Unity Isekai Game/World/Travel Condition Definition")]
    public class TravelConditionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string travelConditionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private TravelConditionCategory category = TravelConditionCategory.Terrain;
        [SerializeField] private TravelConditionTargetScope[] validTargetScopes = Array.Empty<TravelConditionTargetScope>();
        [SerializeField] private TravelConditionSeverity defaultSeverity = TravelConditionSeverity.Minor;
        [SerializeField] private TravelConditionStackingPolicy stackingPolicy = TravelConditionStackingPolicy.Multiplicative;
        [SerializeField] private TravelConditionVisibility defaultVisibility = TravelConditionVisibility.Public;
        [SerializeField] private double movementRateMultiplier = 1d;
        [SerializeField] private double routeCostMultiplier = 1d;
        [SerializeField] private bool hardBlocksTravel;
        [SerializeField] private string[] restrictedTravelModeDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] requiredEquipmentDefinitionIds = Array.Empty<string>();
        [SerializeField] private bool requiresEquippedEquipment;
        [SerializeField] private string[] hazardDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] encounterDefinitionIds = Array.Empty<string>();
        [SerializeField] private double defaultDurationSeconds = -1d;
        [SerializeField] private int priority;
        [SerializeField] private int version = 1;

        public string Id => travelConditionDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public TravelConditionCategory Category => category;
        public IReadOnlyList<TravelConditionTargetScope> ValidTargetScopes => validTargetScopes ?? Array.Empty<TravelConditionTargetScope>();
        public TravelConditionSeverity DefaultSeverity => defaultSeverity;
        public TravelConditionStackingPolicy StackingPolicy => stackingPolicy == TravelConditionStackingPolicy.Unknown ? TravelConditionStackingPolicy.Multiplicative : stackingPolicy;
        public TravelConditionVisibility DefaultVisibility => defaultVisibility;
        public double MovementRateMultiplier => movementRateMultiplier;
        public double RouteCostMultiplier => routeCostMultiplier;
        public bool HardBlocksTravel => hardBlocksTravel;
        public IReadOnlyList<string> RestrictedTravelModeDefinitionIds => restrictedTravelModeDefinitionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredCapabilityIds => requiredCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredEquipmentDefinitionIds => requiredEquipmentDefinitionIds ?? Array.Empty<string>();
        public bool RequiresEquippedEquipment => requiresEquippedEquipment;
        public IReadOnlyList<string> HazardDefinitionIds => hazardDefinitionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> EncounterDefinitionIds => encounterDefinitionIds ?? Array.Empty<string>();
        public double DefaultDurationSeconds => defaultDurationSeconds;
        public int Priority => priority;
        public int Version => version;

        private void OnValidate()
        {
            travelConditionDefinitionId = N(travelConditionDefinitionId);
            displayName = N(displayName);
            validTargetScopes = CleanScopes(validTargetScopes);
            restrictedTravelModeDefinitionIds = Clean(restrictedTravelModeDefinitionIds);
            requiredCapabilityIds = Clean(requiredCapabilityIds);
            requiredEquipmentDefinitionIds = Clean(requiredEquipmentDefinitionIds);
            hazardDefinitionIds = Clean(hazardDefinitionIds);
            encounterDefinitionIds = Clean(encounterDefinitionIds);
            movementRateMultiplier = ValidPositive(movementRateMultiplier) ? movementRateMultiplier : 1d;
            routeCostMultiplier = ValidPositive(routeCostMultiplier) ? routeCostMultiplier : 1d;
            defaultDurationSeconds = defaultDurationSeconds < 0d ? -1d : defaultDurationSeconds;
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            TravelConditionCategory conditionCategory,
            IEnumerable<TravelConditionTargetScope> scopes,
            TravelConditionSeverity severity = TravelConditionSeverity.Minor,
            double movementMultiplier = 1d,
            double costMultiplier = 1d,
            bool blocksTravel = false,
            IEnumerable<string> restrictedModes = null,
            IEnumerable<string> capabilities = null,
            IEnumerable<string> equipment = null,
            bool equippedEquipment = false,
            IEnumerable<string> hazards = null,
            IEnumerable<string> encounters = null,
            TravelConditionVisibility visibility = TravelConditionVisibility.Public,
            TravelConditionStackingPolicy stacking = TravelConditionStackingPolicy.Multiplicative,
            double durationSeconds = -1d,
            int conditionPriority = 0)
        {
            travelConditionDefinitionId = N(id);
            displayName = string.IsNullOrWhiteSpace(display) ? travelConditionDefinitionId : display.Trim();
            description = string.Empty;
            category = conditionCategory;
            validTargetScopes = CleanScopes(scopes);
            defaultSeverity = severity == TravelConditionSeverity.Unknown ? TravelConditionSeverity.Minor : severity;
            movementRateMultiplier = movementMultiplier;
            routeCostMultiplier = costMultiplier;
            hardBlocksTravel = blocksTravel;
            restrictedTravelModeDefinitionIds = Clean(restrictedModes);
            requiredCapabilityIds = Clean(capabilities);
            requiredEquipmentDefinitionIds = Clean(equipment);
            requiresEquippedEquipment = equippedEquipment;
            hazardDefinitionIds = Clean(hazards);
            encounterDefinitionIds = Clean(encounters);
            defaultVisibility = visibility;
            stackingPolicy = stacking == TravelConditionStackingPolicy.Unknown ? TravelConditionStackingPolicy.Multiplicative : stacking;
            defaultDurationSeconds = durationSeconds;
            priority = conditionPriority;
            version = 1;
            OnValidate();
        }

        public bool SupportsScope(TravelConditionTargetScope scope)
        {
            TravelConditionTargetScope[] scopes = CleanScopes(validTargetScopes);
            return scopes.Length == 0 || scopes.Contains(scope);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Travel Condition Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("travel-condition-definition.", StringComparison.Ordinal)) report.AddWarning($"Travel Condition Definition '{Id}' should use the 'travel-condition-definition.' namespace prefix.");
            if (!Enum.IsDefined(typeof(TravelConditionCategory), category) || category == TravelConditionCategory.Unknown) report.AddError($"Travel Condition Definition '{DisplayName}' must declare a concrete category.");
            if (!Enum.IsDefined(typeof(TravelConditionSeverity), defaultSeverity) || defaultSeverity == TravelConditionSeverity.Unknown) report.AddError($"Travel Condition Definition '{DisplayName}' must declare a concrete severity.");
            if (!Enum.IsDefined(typeof(TravelConditionStackingPolicy), stackingPolicy) || stackingPolicy == TravelConditionStackingPolicy.Unknown) report.AddError($"Travel Condition Definition '{DisplayName}' must declare a concrete stacking policy.");
            if (CleanScopes(validTargetScopes).Length == 0) report.AddError($"Travel Condition Definition '{DisplayName}' must declare at least one target scope.");
            if (!ValidPositive(movementRateMultiplier)) report.AddError($"Travel Condition Definition '{DisplayName}' has invalid movement multiplier.");
            if (!ValidPositive(routeCostMultiplier)) report.AddError($"Travel Condition Definition '{DisplayName}' has invalid route-cost multiplier.");
            foreach (string modeId in Clean(restrictedTravelModeDefinitionIds))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(modeId, out IGameDefinition definition) || definition is not TravelModeDefinition) report.AddError($"Travel Condition Definition '{DisplayName}' references missing Travel Mode '{modeId}'.");
            }
            foreach (string hazardId in Clean(hazardDefinitionIds))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(hazardId, out IGameDefinition definition) || definition is not TravelHazardDefinition) report.AddError($"Travel Condition Definition '{DisplayName}' references missing Travel Hazard '{hazardId}'.");
            }
            foreach (string encounterId in Clean(encounterDefinitionIds))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(encounterId, out IGameDefinition definition) || definition is not TravelEncounterDefinition) report.AddError($"Travel Condition Definition '{DisplayName}' references missing Travel Encounter '{encounterId}'.");
            }
        }

        private static bool ValidPositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static TravelConditionTargetScope[] CleanScopes(IEnumerable<TravelConditionTargetScope> values) => (values ?? Array.Empty<TravelConditionTargetScope>()).Where(value => Enum.IsDefined(typeof(TravelConditionTargetScope), value) && value != TravelConditionTargetScope.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
    }

    [CreateAssetMenu(fileName = "TravelHazardDefinition", menuName = "Unity Isekai Game/World/Travel Hazard Definition")]
    public class TravelHazardDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string travelHazardDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private TravelHazardCategory category = TravelHazardCategory.Terrain;
        [SerializeField] private TravelHazardTriggerPolicy triggerPolicy = TravelHazardTriggerPolicy.ExplicitOnly;
        [SerializeField] private string biologyConditionDefinitionId;
        [SerializeField] private string combatDamageDefinitionId;
        [SerializeField] private string[] consequenceTags = Array.Empty<string>();
        [SerializeField] private TravelConditionVisibility visibility = TravelConditionVisibility.Public;
        [SerializeField] private int priority;
        [SerializeField] private int version = 1;

        public string Id => travelHazardDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public TravelHazardCategory Category => category;
        public TravelHazardTriggerPolicy TriggerPolicy => triggerPolicy == TravelHazardTriggerPolicy.Unknown ? TravelHazardTriggerPolicy.ExplicitOnly : triggerPolicy;
        public string BiologyConditionDefinitionId => biologyConditionDefinitionId ?? string.Empty;
        public string CombatDamageDefinitionId => combatDamageDefinitionId ?? string.Empty;
        public IReadOnlyList<string> ConsequenceTags => consequenceTags ?? Array.Empty<string>();
        public TravelConditionVisibility Visibility => visibility;
        public int Priority => priority;
        public int Version => version;

        public void DevelopmentConfigure(string id, string display, TravelHazardCategory hazardCategory, TravelHazardTriggerPolicy policy = TravelHazardTriggerPolicy.ExplicitOnly, string biologyReference = "", string combatReference = "", IEnumerable<string> tags = null, TravelConditionVisibility hazardVisibility = TravelConditionVisibility.Public, int hazardPriority = 0)
        {
            travelHazardDefinitionId = N(id);
            displayName = string.IsNullOrWhiteSpace(display) ? travelHazardDefinitionId : display.Trim();
            description = string.Empty;
            category = hazardCategory;
            triggerPolicy = policy == TravelHazardTriggerPolicy.Unknown ? TravelHazardTriggerPolicy.ExplicitOnly : policy;
            biologyConditionDefinitionId = N(biologyReference);
            combatDamageDefinitionId = N(combatReference);
            consequenceTags = Clean(tags);
            visibility = hazardVisibility;
            priority = hazardPriority;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Travel Hazard Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("travel-hazard-definition.", StringComparison.Ordinal)) report.AddWarning($"Travel Hazard Definition '{Id}' should use the 'travel-hazard-definition.' namespace prefix.");
            if (!Enum.IsDefined(typeof(TravelHazardCategory), category) || category == TravelHazardCategory.Unknown) report.AddError($"Travel Hazard Definition '{DisplayName}' must declare a concrete category.");
            if (!Enum.IsDefined(typeof(TravelHazardTriggerPolicy), triggerPolicy) || triggerPolicy == TravelHazardTriggerPolicy.Unknown) report.AddError($"Travel Hazard Definition '{DisplayName}' must declare a concrete trigger policy.");
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [CreateAssetMenu(fileName = "TravelEncounterDefinition", menuName = "Unity Isekai Game/World/Travel Encounter Definition")]
    public class TravelEncounterDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string travelEncounterDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private TravelEncounterCategory category = TravelEncounterCategory.Neutral;
        [SerializeField] private TravelEncounterTriggerPolicy triggerPolicy = TravelEncounterTriggerPolicy.ExplicitOnly;
        [SerializeField] private TravelEncounterRepeatPolicy repeatPolicy = TravelEncounterRepeatPolicy.OncePerJourney;
        [SerializeField] private TravelEncounterInterruptionPolicy interruptionPolicy = TravelEncounterInterruptionPolicy.None;
        [SerializeField] private string combatEncounterDefinitionId;
        [SerializeField] private string[] participantRoleIds = Array.Empty<string>();
        [SerializeField] private TravelConditionVisibility visibility = TravelConditionVisibility.Public;
        [SerializeField] private int priority;
        [SerializeField] private int version = 1;

        public string Id => travelEncounterDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public TravelEncounterCategory Category => category;
        public TravelEncounterTriggerPolicy TriggerPolicy => triggerPolicy == TravelEncounterTriggerPolicy.Unknown ? TravelEncounterTriggerPolicy.ExplicitOnly : triggerPolicy;
        public TravelEncounterRepeatPolicy RepeatPolicy => repeatPolicy == TravelEncounterRepeatPolicy.Unknown ? TravelEncounterRepeatPolicy.OncePerJourney : repeatPolicy;
        public TravelEncounterInterruptionPolicy InterruptionPolicy => interruptionPolicy;
        public string CombatEncounterDefinitionId => combatEncounterDefinitionId ?? string.Empty;
        public IReadOnlyList<string> ParticipantRoleIds => participantRoleIds ?? Array.Empty<string>();
        public TravelConditionVisibility Visibility => visibility;
        public int Priority => priority;
        public int Version => version;

        public void DevelopmentConfigure(string id, string display, TravelEncounterCategory encounterCategory, TravelEncounterTriggerPolicy policy = TravelEncounterTriggerPolicy.ExplicitOnly, TravelEncounterRepeatPolicy repeat = TravelEncounterRepeatPolicy.OncePerJourney, TravelEncounterInterruptionPolicy interruption = TravelEncounterInterruptionPolicy.None, string combatReference = "", IEnumerable<string> roles = null, TravelConditionVisibility encounterVisibility = TravelConditionVisibility.Public, int encounterPriority = 0)
        {
            travelEncounterDefinitionId = N(id);
            displayName = string.IsNullOrWhiteSpace(display) ? travelEncounterDefinitionId : display.Trim();
            description = string.Empty;
            category = encounterCategory;
            triggerPolicy = policy == TravelEncounterTriggerPolicy.Unknown ? TravelEncounterTriggerPolicy.ExplicitOnly : policy;
            repeatPolicy = repeat == TravelEncounterRepeatPolicy.Unknown ? TravelEncounterRepeatPolicy.OncePerJourney : repeat;
            interruptionPolicy = interruption;
            combatEncounterDefinitionId = N(combatReference);
            participantRoleIds = Clean(roles);
            visibility = encounterVisibility;
            priority = encounterPriority;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Travel Encounter Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("travel-encounter-definition.", StringComparison.Ordinal)) report.AddWarning($"Travel Encounter Definition '{Id}' should use the 'travel-encounter-definition.' namespace prefix.");
            if (!Enum.IsDefined(typeof(TravelEncounterCategory), category) || category == TravelEncounterCategory.Unknown) report.AddError($"Travel Encounter Definition '{DisplayName}' must declare a concrete category.");
            if (!Enum.IsDefined(typeof(TravelEncounterTriggerPolicy), triggerPolicy) || triggerPolicy == TravelEncounterTriggerPolicy.Unknown) report.AddError($"Travel Encounter Definition '{DisplayName}' must declare a concrete trigger policy.");
            if (!Enum.IsDefined(typeof(TravelEncounterRepeatPolicy), repeatPolicy) || repeatPolicy == TravelEncounterRepeatPolicy.Unknown) report.AddError($"Travel Encounter Definition '{DisplayName}' must declare a concrete repeat policy.");
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
