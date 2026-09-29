using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Production;

namespace UnityIsekaiGame.Inventory.Experimentation
{
    [CreateAssetMenu(fileName = "ExperimentDefinition", menuName = "Unity Isekai Game/Inventory/Experiment Definition")]
    public sealed class ExperimentDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string experimentId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ExperimentCategory category = ExperimentCategory.Unknown;
        [SerializeField] private ExperimentPlanMode defaultPlanMode = ExperimentPlanMode.Controlled;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private string[] supportedTargetTypes = Array.Empty<string>();
        [SerializeField] private ExperimentVariableDefinitionData[] variables = Array.Empty<ExperimentVariableDefinitionData>();
        [SerializeField] private ExperimentControlDefinitionData[] requiredControls = Array.Empty<ExperimentControlDefinitionData>();
        [SerializeField] private string[] requiredObservationMethodIds = Array.Empty<string>();
        [SerializeField] private string[] productionRequirementIds = Array.Empty<string>();
        [SerializeField] private string procedureTemplateId;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private string secrecyClassification;
        [SerializeField] private string provenance;
        [SerializeField] private ExperimentPolicyData evidencePolicy = new ExperimentPolicyData();
        [SerializeField] private ExperimentPolicyData reproducibilityPolicy = new ExperimentPolicyData { minimumTrials = 2, independentReproductionThreshold = 2 };
        [SerializeField] private ExperimentPolicyData confirmationPolicy = new ExperimentPolicyData { minimumTrials = 2, confirmationEvidenceThreshold = 2 };
        [SerializeField] private int schemaVersion = 1;

        public string Id => experimentId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public ExperimentCategory Category => category;
        public ExperimentPlanMode DefaultPlanMode => defaultPlanMode;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public IReadOnlyList<string> SupportedTargetTypes => supportedTargetTypes ?? Array.Empty<string>();
        public IReadOnlyList<ExperimentVariableDefinitionData> Variables => (variables ?? Array.Empty<ExperimentVariableDefinitionData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToArray();
        public IReadOnlyList<ExperimentControlDefinitionData> RequiredControls => (requiredControls ?? Array.Empty<ExperimentControlDefinitionData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToArray();
        public IReadOnlyList<string> RequiredObservationMethodIds => requiredObservationMethodIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ProductionRequirementIds => productionRequirementIds ?? Array.Empty<string>();
        public string ProcedureTemplateId => procedureTemplateId ?? string.Empty;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public string SecrecyClassification => secrecyClassification ?? string.Empty;
        public string Provenance => provenance ?? string.Empty;
        public ExperimentPolicyData EvidencePolicy => evidencePolicy == null ? new ExperimentPolicyData() : evidencePolicy.Clone();
        public ExperimentPolicyData ReproducibilityPolicy => reproducibilityPolicy == null ? new ExperimentPolicyData() : reproducibilityPolicy.Clone();
        public ExperimentPolicyData ConfirmationPolicy => confirmationPolicy == null ? new ExperimentPolicyData() : confirmationPolicy.Clone();
        public int SchemaVersion => Math.Max(1, schemaVersion);

        private void OnValidate()
        {
            experimentId = experimentId?.Trim();
            procedureTemplateId = procedureTemplateId?.Trim();
            accessPolicyId = accessPolicyId?.Trim();
            schemaVersion = Math.Max(1, schemaVersion);
            tags = ExperimentVariableDefinitionData.NormalizeIds(tags);
            supportedTargetTypes = ExperimentVariableDefinitionData.NormalizeIds(supportedTargetTypes);
            requiredObservationMethodIds = ExperimentVariableDefinitionData.NormalizeIds(requiredObservationMethodIds);
            productionRequirementIds = ExperimentVariableDefinitionData.NormalizeIds(productionRequirementIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Experiment definition '{name}' is missing an ID.");
            }
            else if (!Id.StartsWith("experiment.", StringComparison.Ordinal))
            {
                report.AddWarning($"Experiment definition '{Id}' should use the 'experiment.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(ExperimentCategory), category) || category == ExperimentCategory.Unknown)
            {
                report.AddError($"Experiment '{DisplayName}' must declare a concrete category.");
            }

            if (!Enum.IsDefined(typeof(ExperimentPlanMode), defaultPlanMode))
            {
                report.AddError($"Experiment '{DisplayName}' has an invalid plan mode.");
            }

            if (SupportedTargetTypes.Count == 0)
            {
                report.AddError($"Experiment '{DisplayName}' must declare at least one supported target type.");
            }

            ValidateVariables(report);
            ValidateControls(report);
            ValidateRequirements(definitionsById, report);
            ValidatePolicies(report);
        }

        private void ValidateVariables(DefinitionValidationReport report)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExperimentVariableDefinitionData variable in Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.variableId))
                {
                    report.AddError($"Experiment '{DisplayName}' has a variable without an ID.");
                    continue;
                }

                if (!ids.Add(variable.variableId))
                {
                    report.AddError($"Experiment '{DisplayName}' has duplicate variable '{variable.variableId}'.");
                }

                if (variable.category == ExperimentVariableCategory.Unknown)
                {
                    report.AddError($"Experiment variable '{variable.variableId}' must declare a concrete category.");
                }

                if (variable.valueType == ExperimentValueType.None)
                {
                    report.AddError($"Experiment variable '{variable.variableId}' must declare a concrete value type.");
                }

                if (variable.valueType == ExperimentValueType.Range && variable.maximumValue < variable.minimumValue)
                {
                    report.AddError($"Experiment variable '{variable.variableId}' has an invalid range.");
                }
            }
        }

        private void ValidateControls(DefinitionValidationReport report)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> variableIds = new HashSet<string>(Variables.Select(variable => variable.variableId), StringComparer.Ordinal);
            foreach (ExperimentControlDefinitionData control in RequiredControls)
            {
                if (string.IsNullOrWhiteSpace(control.controlId))
                {
                    report.AddError($"Experiment '{DisplayName}' has a control without an ID.");
                    continue;
                }

                if (!ids.Add(control.controlId))
                {
                    report.AddError($"Experiment '{DisplayName}' has duplicate control '{control.controlId}'.");
                }

                if (string.IsNullOrWhiteSpace(control.baselineType) && string.IsNullOrWhiteSpace(control.baselineReferenceId))
                {
                    report.AddError($"Experiment control '{control.controlId}' must declare a baseline.");
                }

                foreach (string variableId in control.heldVariableIds ?? Array.Empty<string>())
                {
                    if (!variableIds.Contains(variableId))
                    {
                        report.AddError($"Experiment control '{control.controlId}' references missing variable '{variableId}'.");
                    }
                }
            }
        }

        private void ValidateRequirements(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (string requirementId in ProductionRequirementIds)
            {
                if (!string.IsNullOrWhiteSpace(requirementId)
                    && (definitionsById == null || !definitionsById.TryGetValue(requirementId, out IGameDefinition definition) || definition is not ProductionRequirementDefinition))
                {
                    report.AddError($"Experiment '{DisplayName}' references missing Production Requirement definition '{requirementId}'.");
                }
            }
        }

        private void ValidatePolicies(DefinitionValidationReport report)
        {
            ValidatePolicy(EvidencePolicy, "evidence", report);
            ValidatePolicy(ReproducibilityPolicy, "reproducibility", report);
            ValidatePolicy(ConfirmationPolicy, "confirmation", report);
            if (Category == ExperimentCategory.DestructiveTesting && !ConfirmationPolicy.allowDestructiveTesting)
            {
                report.AddError($"Destructive Experiment '{DisplayName}' must explicitly allow destructive testing in its confirmation policy.");
            }
        }

        private void ValidatePolicy(ExperimentPolicyData policy, string label, DefinitionValidationReport report)
        {
            if (policy == null)
            {
                report.AddError($"Experiment '{DisplayName}' is missing a {label} policy.");
                return;
            }

            if (policy.minimumTrials < 1 || policy.independentReproductionThreshold < 1 || policy.confirmationEvidenceThreshold < 1)
            {
                report.AddError($"Experiment '{DisplayName}' has an invalid {label} policy threshold.");
            }
        }
    }
}
