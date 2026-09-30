using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.Dialogue;

namespace UnityIsekaiGame.Narrative
{
    [CreateAssetMenu(fileName = "NarrativeEventDefinition", menuName = "Unity Isekai Game/Narrative/Narrative Event Definition")]
    public sealed class NarrativeEventDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private NarrativeEventDefinitionData data = new NarrativeEventDefinitionData();

        public string Id => data?.eventDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(data?.displayName) ? Id : data.displayName;
        public NarrativeEventDefinitionData ToRecordData() => data?.Clone() ?? new NarrativeEventDefinitionData();

        public void DevelopmentConfigure(NarrativeEventDefinitionData definitionData)
        {
            data = definitionData?.Clone() ?? new NarrativeEventDefinitionData();
            name = DisplayName;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            NarrativeEventValidationReport validation = NarrativeEventDefinitionValidator.Validate(ToRecordData(), definitionsById);
            foreach (string error in validation.Errors) report.AddError($"Narrative Event definition '{DisplayName}': {error}");
            foreach (string warning in validation.Warnings) report.AddWarning($"Narrative Event definition '{DisplayName}': {warning}");
        }
    }

    public sealed class NarrativeEventValidationReport
    {
        public NarrativeEventValidationReport()
            : this(Array.Empty<string>(), Array.Empty<string>())
        {
        }

        public NarrativeEventValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
    }

    public static class NarrativeEventDefinitionValidator
    {
        public static NarrativeEventValidationReport Validate(NarrativeEventDefinitionData definition, IReadOnlyDictionary<string, IGameDefinition> definitionsById = null)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            NarrativeEventDefinitionData data = definition?.Clone();
            if (data == null)
            {
                errors.Add("definition data is missing.");
                return new NarrativeEventValidationReport(errors, warnings);
            }

            if (string.IsNullOrWhiteSpace(data.eventDefinitionId)) errors.Add("stable NarrativeEventDefinitionId is missing.");
            else if (!data.eventDefinitionId.StartsWith("narrative-event-definition.", StringComparison.Ordinal)) warnings.Add($"'{data.eventDefinitionId}' should use the 'narrative-event-definition.' namespace prefix.");
            if (data.category == NarrativeEventCategory.Unknown) errors.Add("category is Unknown.");
            if (data.scope == NarrativeEventScope.Unknown) errors.Add("scope is Unknown.");
            if (data.repeatPolicy == NarrativeRepeatPolicy.Unknown) errors.Add("repeat policy is Unknown.");
            if (data.armingPolicy == NarrativeArmingPolicy.Unknown) errors.Add("arming policy is Unknown.");
            if (data.triggerMode == NarrativeTriggerMode.Unknown) errors.Add("trigger mode is Unknown.");
            if (data.activationStartTime >= 0d && data.activationEndTime >= 0d && data.activationEndTime < data.activationStartTime) errors.Add("activation window ends before it starts.");
            if (data.scope == NarrativeEventScope.OncePerPerson && string.IsNullOrWhiteSpace(data.scopeSelectorId)) warnings.Add("OncePerPerson definitions should document a Person scope selector.");
            if (data.scope == NarrativeEventScope.OncePerQuest && string.IsNullOrWhiteSpace(data.scopeSelectorId)) warnings.Add("OncePerQuest definitions should document a Quest scope selector.");
            if ((data.triggers == null || data.triggers.Length == 0) && data.armingPolicy != NarrativeArmingPolicy.Explicit) errors.Add("at least one trigger is required unless the event is explicitly-only armed.");

            ValidateTriggers(data, errors);
            ValidateConditions(data, errors, warnings);
            ValidateActions(data, definitionsById, errors, warnings);
            ValidateStaticCascade(data, errors);
            return new NarrativeEventValidationReport(errors, warnings);
        }

        private static void ValidateTriggers(NarrativeEventDefinitionData data, ICollection<string> errors)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeTriggerDefinitionData trigger in data.triggers ?? Array.Empty<NarrativeTriggerDefinitionData>())
            {
                if (trigger == null) continue;
                if (string.IsNullOrWhiteSpace(trigger.triggerDefinitionId)) errors.Add("trigger has no stable definition ID.");
                else if (!ids.Add(trigger.triggerDefinitionId)) errors.Add($"duplicate trigger definition '{trigger.triggerDefinitionId}'.");
                if (trigger.category == NarrativeTriggerCategory.Unknown) errors.Add($"trigger '{trigger.triggerDefinitionId}' has Unknown category.");
                if (RequiresTarget(trigger.category) && string.IsNullOrWhiteSpace(trigger.requiredSourceId) && string.IsNullOrWhiteSpace(trigger.requiredSubjectId)) errors.Add($"trigger '{trigger.triggerDefinitionId}' requires a source or subject selector.");
                if (trigger.category == NarrativeTriggerCategory.AuthoritativeTime && data.activationStartTime < 0d && data.delayDuration < 0d) errors.Add($"time trigger '{trigger.triggerDefinitionId}' requires an activation time or delay.");
            }
        }

        private static void ValidateConditions(NarrativeEventDefinitionData data, ICollection<string> errors, ICollection<string> warnings)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeConditionDefinitionData condition in data.conditions ?? Array.Empty<NarrativeConditionDefinitionData>())
            {
                if (condition == null) continue;
                if (string.IsNullOrWhiteSpace(condition.conditionDefinitionId)) errors.Add("condition has no stable definition ID.");
                else if (!ids.Add(condition.conditionDefinitionId)) errors.Add($"duplicate condition definition '{condition.conditionDefinitionId}'.");
                if (condition.category == NarrativeConditionCategory.Unknown) errors.Add($"condition '{condition.conditionDefinitionId}' has Unknown category.");
                if (condition.category != NarrativeConditionCategory.Always && condition.category != NarrativeConditionCategory.TimeState && string.IsNullOrWhiteSpace(condition.requiredId)) errors.Add($"condition '{condition.conditionDefinitionId}' requires a target ID.");
                if (condition.category == NarrativeConditionCategory.NarrativeState && string.IsNullOrWhiteSpace(condition.secondaryId)) warnings.Add($"condition '{condition.conditionDefinitionId}' should document the NarrativeVariableDefinitionId in secondaryId when it is not encoded in requiredId.");
            }

            if (data.conditionGroupPolicy == NarrativeConditionGroupPolicy.AtLeastN && data.atLeastConditionCount <= 0) errors.Add("AtLeastN condition group requires a positive count.");
        }

        private static void ValidateActions(NarrativeEventDefinitionData data, IReadOnlyDictionary<string, IGameDefinition> definitionsById, ICollection<string> errors, ICollection<string> warnings)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeActionDefinitionData action in data.actions ?? Array.Empty<NarrativeActionDefinitionData>())
            {
                if (action == null) continue;
                if (string.IsNullOrWhiteSpace(action.actionDefinitionId)) errors.Add("action has no stable definition ID.");
                else if (!ids.Add(action.actionDefinitionId)) errors.Add($"duplicate action definition '{action.actionDefinitionId}'.");
                if (action.category == NarrativeActionCategory.Unknown) errors.Add($"action '{action.actionDefinitionId}' has Unknown category.");
                if (action.category == NarrativeActionCategory.Custom) errors.Add($"action '{action.actionDefinitionId}' uses Custom; unrestricted scripting is not allowed.");
                if (RequiresActionTarget(action.category) && string.IsNullOrWhiteSpace(action.targetId) && string.IsNullOrWhiteSpace(action.inputSlotId)) errors.Add($"action '{action.actionDefinitionId}' requires a typed target or input slot.");
                if (definitionsById == null) continue;
                if (action.category == NarrativeActionCategory.InstantiateQuest && !string.IsNullOrWhiteSpace(action.targetId) && !definitionsById.ContainsKey(action.targetId)) warnings.Add($"action '{action.actionDefinitionId}' references missing Quest definition '{action.targetId}'.");
                if (action.category == NarrativeActionCategory.PublishQuestListing && !string.IsNullOrWhiteSpace(action.targetId) && !definitionsById.ContainsKey(action.targetId) && !action.targetId.StartsWith("quest-source.", StringComparison.Ordinal)) warnings.Add($"action '{action.actionDefinitionId}' target '{action.targetId}' is not a known Quest Source definition or runtime source ID.");
                if (action.category == NarrativeActionCategory.StartConversation && !string.IsNullOrWhiteSpace(action.targetId) && !definitionsById.ContainsKey(action.targetId)) warnings.Add($"action '{action.actionDefinitionId}' references missing Conversation definition '{action.targetId}'.");
                if (action.category == NarrativeActionCategory.RequestNarrativeStateTransition && !string.IsNullOrWhiteSpace(action.targetId) && !definitionsById.Values.OfType<NarrativeStateDefinition>().Any(definition => definition.ToRecordData().transitions.Any(transition => transition.transitionDefinitionId == action.targetId))) warnings.Add($"action '{action.actionDefinitionId}' references missing Narrative State transition '{action.targetId}'.");
            }
        }

        private static void ValidateStaticCascade(NarrativeEventDefinitionData data, ICollection<string> errors)
        {
            HashSet<string> triggerSignals = new HashSet<string>((data.triggers ?? Array.Empty<NarrativeTriggerDefinitionData>())
                .Where(trigger => trigger.category == NarrativeTriggerCategory.ExplicitSignal && !string.IsNullOrWhiteSpace(trigger.requiredSourceId))
                .Select(trigger => trigger.requiredSourceId), StringComparer.Ordinal);
            foreach (NarrativeActionDefinitionData action in data.actions ?? Array.Empty<NarrativeActionDefinitionData>())
            {
                if (action.category == NarrativeActionCategory.EmitNarrativeSignal && triggerSignals.Contains(action.targetId))
                {
                    errors.Add($"static self-trigger loop: action '{action.actionDefinitionId}' emits signal '{action.targetId}' consumed by the same definition.");
                }
            }
        }

        private static bool RequiresTarget(NarrativeTriggerCategory category)
        {
            return category != NarrativeTriggerCategory.DomainEvent
                && category != NarrativeTriggerCategory.CurrentStateSatisfied
                && category != NarrativeTriggerCategory.StateChanged
                && category != NarrativeTriggerCategory.Custom;
        }

        private static bool RequiresActionTarget(NarrativeActionCategory category)
        {
            return category != NarrativeActionCategory.None
                && category != NarrativeActionCategory.HistoricalEventRequest;
        }
    }
}
