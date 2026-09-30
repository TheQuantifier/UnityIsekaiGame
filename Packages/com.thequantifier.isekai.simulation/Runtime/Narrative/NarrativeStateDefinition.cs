using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Narrative
{
    [CreateAssetMenu(fileName = "NarrativeStateDefinition", menuName = "Unity Isekai Game/Narrative/Narrative State Definition")]
    public sealed class NarrativeStateDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private NarrativeStateDefinitionData data = new NarrativeStateDefinitionData();

        public string Id => data?.stateDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(data?.displayName) ? Id : data.displayName;
        public NarrativeStateDefinitionData ToRecordData() => data?.Clone() ?? new NarrativeStateDefinitionData();

        public void DevelopmentConfigure(NarrativeStateDefinitionData definitionData)
        {
            data = definitionData?.Clone() ?? new NarrativeStateDefinitionData();
            name = DisplayName;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            NarrativeStateValidationReport validation = NarrativeStateDefinitionValidator.Validate(ToRecordData(), definitionsById);
            foreach (string error in validation.Errors) report.AddError($"Narrative State definition '{DisplayName}': {error}");
            foreach (string warning in validation.Warnings) report.AddWarning($"Narrative State definition '{DisplayName}': {warning}");
        }
    }

    public static class NarrativeStateDefinitionValidator
    {
        public static NarrativeStateValidationReport Validate(NarrativeStateDefinitionData definition, IReadOnlyDictionary<string, IGameDefinition> definitionsById = null)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            NarrativeStateDefinitionData data = definition?.Clone();
            if (data == null)
            {
                errors.Add("definition data is missing.");
                return new NarrativeStateValidationReport(errors, warnings);
            }

            if (string.IsNullOrWhiteSpace(data.stateDefinitionId)) errors.Add("stable NarrativeStateDefinitionId is missing.");
            else if (!data.stateDefinitionId.StartsWith("narrative-state-definition.", StringComparison.Ordinal)) warnings.Add($"'{data.stateDefinitionId}' should use the 'narrative-state-definition.' namespace prefix.");
            if (data.scope == NarrativeStateScope.Unknown) errors.Add("scope is Unknown.");
            if (data.visibility == NarrativeStateVisibility.Unknown) errors.Add("visibility is Unknown.");
            if (data.variables == null || data.variables.Length == 0) errors.Add("at least one variable definition is required.");

            Dictionary<string, NarrativeVariableDefinitionData> variables = new Dictionary<string, NarrativeVariableDefinitionData>(StringComparer.Ordinal);
            foreach (NarrativeVariableDefinitionData variable in data.variables ?? Array.Empty<NarrativeVariableDefinitionData>())
            {
                ValidateVariable(data, variable, variables, errors, warnings);
            }

            HashSet<string> transitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeStateTransitionDefinitionData transition in data.transitions ?? Array.Empty<NarrativeStateTransitionDefinitionData>())
            {
                ValidateTransition(data, transition, variables, transitionIds, definitionsById, errors, warnings);
            }

            return new NarrativeStateValidationReport(errors, warnings);
        }

        private static void ValidateVariable(NarrativeStateDefinitionData state, NarrativeVariableDefinitionData variable, IDictionary<string, NarrativeVariableDefinitionData> variables, ICollection<string> errors, ICollection<string> warnings)
        {
            if (variable == null)
            {
                errors.Add("variable definition is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(variable.variableDefinitionId)) errors.Add("variable has no stable NarrativeVariableDefinitionId.");
            else if (!variable.variableDefinitionId.StartsWith("narrative-variable.", StringComparison.Ordinal)) warnings.Add($"variable '{variable.variableDefinitionId}' should use the 'narrative-variable.' namespace prefix.");
            else if (variables.ContainsKey(variable.variableDefinitionId)) errors.Add($"duplicate variable definition '{variable.variableDefinitionId}'.");
            else variables[variable.variableDefinitionId] = variable.Clone();

            if (variable.kind == NarrativeVariableKind.Unknown) errors.Add($"variable '{variable.variableDefinitionId}' kind is Unknown.");
            if (variable.scope == NarrativeStateScope.Unknown) errors.Add($"variable '{variable.variableDefinitionId}' scope is Unknown.");
            if (variable.scope != state.scope) warnings.Add($"variable '{variable.variableDefinitionId}' scope differs from state scope; runtime uses the state scope for record identity.");
            if (variable.mutabilityPolicy == NarrativeVariableMutabilityPolicy.Unknown) errors.Add($"variable '{variable.variableDefinitionId}' mutability policy is Unknown.");
            if (!ValueMatchesKind(variable.defaultValue, variable.kind, variable, out string defaultFailure)) errors.Add($"variable '{variable.variableDefinitionId}' default value is invalid: {defaultFailure}");
            if ((variable.kind == NarrativeVariableKind.Integer || variable.kind == NarrativeVariableKind.SmallCounter) && variable.minimumValue > variable.maximumValue) errors.Add($"variable '{variable.variableDefinitionId}' numeric range is invalid.");

            if (variable.kind == NarrativeVariableKind.StateToken)
            {
                HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
                foreach (NarrativeStateValueDefinitionData value in variable.allowedValues ?? Array.Empty<NarrativeStateValueDefinitionData>())
                {
                    if (string.IsNullOrWhiteSpace(value.valueDefinitionId)) errors.Add($"variable '{variable.variableDefinitionId}' has an allowed state value without a stable ID.");
                    else if (!value.valueDefinitionId.StartsWith("narrative-state-value.", StringComparison.Ordinal)) warnings.Add($"state value '{value.valueDefinitionId}' should use the 'narrative-state-value.' namespace prefix.");
                    else if (!values.Add(value.valueDefinitionId)) errors.Add($"variable '{variable.variableDefinitionId}' has duplicate state value '{value.valueDefinitionId}'.");
                }

                if (!string.IsNullOrWhiteSpace(variable.defaultValue?.tokenValue) && !values.Contains(variable.defaultValue.tokenValue)) errors.Add($"variable '{variable.variableDefinitionId}' default state value '{variable.defaultValue.tokenValue}' is not allowed.");
            }
        }

        private static void ValidateTransition(NarrativeStateDefinitionData state, NarrativeStateTransitionDefinitionData transition, IReadOnlyDictionary<string, NarrativeVariableDefinitionData> variables, ISet<string> transitionIds, IReadOnlyDictionary<string, IGameDefinition> definitionsById, ICollection<string> errors, ICollection<string> warnings)
        {
            if (transition == null)
            {
                errors.Add("transition definition is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(transition.transitionDefinitionId)) errors.Add("transition has no stable NarrativeStateTransitionDefinitionId.");
            else if (!transition.transitionDefinitionId.StartsWith("narrative-transition-definition.", StringComparison.Ordinal)) warnings.Add($"transition '{transition.transitionDefinitionId}' should use the 'narrative-transition-definition.' namespace prefix.");
            else if (!transitionIds.Add(transition.transitionDefinitionId)) errors.Add($"duplicate transition definition '{transition.transitionDefinitionId}'.");

            if (string.IsNullOrWhiteSpace(transition.variableDefinitionId) || !variables.TryGetValue(transition.variableDefinitionId, out NarrativeVariableDefinitionData variable))
            {
                errors.Add($"transition '{transition.transitionDefinitionId}' references missing variable '{transition.variableDefinitionId}'.");
                return;
            }

            if (!ValueMatchesKind(transition.targetValue, variable.kind, variable, out string targetFailure)) errors.Add($"transition '{transition.transitionDefinitionId}' target value is invalid: {targetFailure}");
            foreach (NarrativeVariableValueData source in transition.allowedSourceValues ?? Array.Empty<NarrativeVariableValueData>())
            {
                if (!ValueMatchesKind(source, variable.kind, variable, out string sourceFailure)) errors.Add($"transition '{transition.transitionDefinitionId}' source value is invalid: {sourceFailure}");
            }

            HashSet<string> conditionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeConditionDefinitionData condition in transition.conditions ?? Array.Empty<NarrativeConditionDefinitionData>())
            {
                if (condition == null) continue;
                if (string.IsNullOrWhiteSpace(condition.conditionDefinitionId)) errors.Add($"transition '{transition.transitionDefinitionId}' has a condition without a stable ID.");
                else if (!conditionIds.Add(condition.conditionDefinitionId)) errors.Add($"transition '{transition.transitionDefinitionId}' has duplicate condition '{condition.conditionDefinitionId}'.");
                if (condition.category == NarrativeConditionCategory.Unknown) errors.Add($"transition '{transition.transitionDefinitionId}' condition '{condition.conditionDefinitionId}' has Unknown category.");
            }

            HashSet<string> actionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeActionDefinitionData action in transition.consequences ?? Array.Empty<NarrativeActionDefinitionData>())
            {
                if (action == null) continue;
                if (string.IsNullOrWhiteSpace(action.actionDefinitionId)) errors.Add($"transition '{transition.transitionDefinitionId}' has a consequence without a stable ID.");
                else if (!actionIds.Add(action.actionDefinitionId)) errors.Add($"transition '{transition.transitionDefinitionId}' has duplicate consequence '{action.actionDefinitionId}'.");
                if (action.category == NarrativeActionCategory.Unknown || action.category == NarrativeActionCategory.Custom) errors.Add($"transition '{transition.transitionDefinitionId}' consequence '{action.actionDefinitionId}' has unsupported category '{action.category}'.");
                if (RequiresActionTarget(action.category) && string.IsNullOrWhiteSpace(action.targetId) && string.IsNullOrWhiteSpace(action.inputSlotId)) errors.Add($"transition '{transition.transitionDefinitionId}' consequence '{action.actionDefinitionId}' requires a target.");
                if (definitionsById == null) continue;
                if (action.category == NarrativeActionCategory.RequestNarrativeStateTransition && !string.IsNullOrWhiteSpace(action.targetId) && !definitionsById.Values.OfType<NarrativeStateDefinition>().Any(definition => definition.ToRecordData().transitions.Any(candidate => candidate.transitionDefinitionId == action.targetId))) warnings.Add($"transition consequence '{action.actionDefinitionId}' references narrative transition '{action.targetId}' that is not in the registry.");
            }
        }

        public static bool ValueMatchesKind(NarrativeVariableValueData value, NarrativeVariableKind expected, NarrativeVariableDefinitionData variable, out string failure)
        {
            failure = string.Empty;
            value = value?.Clone();
            if (value == null)
            {
                failure = "value is missing.";
                return false;
            }

            if (value.kind != expected)
            {
                failure = $"expected {expected}, got {value.kind}.";
                return false;
            }

            if ((expected == NarrativeVariableKind.Integer || expected == NarrativeVariableKind.SmallCounter) && variable != null && (value.intValue < variable.minimumValue || value.intValue > variable.maximumValue))
            {
                failure = $"integer {value.intValue} is outside range {variable.minimumValue}..{variable.maximumValue}.";
                return false;
            }

            if (expected == NarrativeVariableKind.SmallCounter && value.intValue < 0)
            {
                failure = "small counters cannot be negative.";
                return false;
            }

            if (expected == NarrativeVariableKind.StateToken)
            {
                if (string.IsNullOrWhiteSpace(value.tokenValue))
                {
                    failure = "state token is empty.";
                    return false;
                }

                if (variable != null && (variable.allowedValues ?? Array.Empty<NarrativeStateValueDefinitionData>()).Length > 0 && !(variable.allowedValues ?? Array.Empty<NarrativeStateValueDefinitionData>()).Any(allowed => string.Equals(allowed.valueDefinitionId, value.tokenValue, StringComparison.Ordinal)))
                {
                    failure = $"state token '{value.tokenValue}' is not allowed.";
                    return false;
                }
            }

            if (expected == NarrativeVariableKind.StableSubjectReference && string.IsNullOrWhiteSpace(value.subjectReference?.subjectId))
            {
                failure = "required subject reference is empty.";
                return false;
            }

            return true;
        }

        private static bool RequiresActionTarget(NarrativeActionCategory category)
        {
            return category != NarrativeActionCategory.None
                && category != NarrativeActionCategory.HistoricalEventRequest;
        }
    }
}
