using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    [CreateAssetMenu(fileName = "NarrativeArcDefinition", menuName = "Unity Isekai Game/Narrative/Narrative Arc Definition")]
    public sealed class NarrativeArcDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private NarrativeArcDefinitionData data = new NarrativeArcDefinitionData();

        public string Id => data?.arcDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(data?.displayName) ? Id : data.displayName;
        public NarrativeArcDefinitionData ToRecordData() => data?.Clone() ?? new NarrativeArcDefinitionData();

        public void DevelopmentConfigure(NarrativeArcDefinitionData definitionData)
        {
            data = definitionData?.Clone() ?? new NarrativeArcDefinitionData();
            name = DisplayName;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            NarrativeArcValidationReport validation = NarrativeArcDefinitionValidator.Validate(ToRecordData(), definitionsById);
            foreach (string error in validation.Errors) report.AddError($"Narrative Arc definition '{DisplayName}': {error}");
            foreach (string warning in validation.Warnings) report.AddWarning($"Narrative Arc definition '{DisplayName}': {warning}");
        }
    }

    public sealed class NarrativeArcValidationReport
    {
        public NarrativeArcValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
    }

    public static class NarrativeArcDefinitionValidator
    {
        public static NarrativeArcValidationReport Validate(NarrativeArcDefinitionData definition, IReadOnlyDictionary<string, IGameDefinition> definitionsById = null)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            NarrativeArcDefinitionData data = definition?.Clone();
            if (data == null)
            {
                errors.Add("definition data is missing.");
                return new NarrativeArcValidationReport(errors, warnings);
            }

            if (string.IsNullOrWhiteSpace(data.arcDefinitionId)) errors.Add("stable NarrativeArcDefinitionId is missing.");
            else if (!data.arcDefinitionId.StartsWith("narrative-arc-definition.", StringComparison.Ordinal)) warnings.Add($"'{data.arcDefinitionId}' should use the 'narrative-arc-definition.' namespace prefix.");
            if (data.scope == NarrativeArcScope.Unknown) errors.Add("scope is Unknown.");
            if (data.visibility == NarrativeEventVisibility.Unknown) errors.Add("visibility is Unknown.");
            if (data.stages == null || data.stages.Length == 0) errors.Add("at least one stage definition is required.");

            Dictionary<string, NarrativeArcStageDefinitionData> stages = new Dictionary<string, NarrativeArcStageDefinitionData>(StringComparer.Ordinal);
            foreach (NarrativeArcStageDefinitionData stage in data.stages ?? Array.Empty<NarrativeArcStageDefinitionData>())
            {
                ValidateStage(stage, stages, definitionsById, errors, warnings);
            }

            if (stages.Count > 0 && !stages.Values.Any(value => value.initial || (value.entryDependencies == null || value.entryDependencies.Length == 0))) errors.Add("at least one initial or dependency-free entry stage is required.");
            ValidateStageGraph(data, stages, errors);
            return new NarrativeArcValidationReport(errors, warnings);
        }

        public static NarrativeArcValidationReport ValidateGraph(IEnumerable<NarrativeArcDefinitionData> definitions)
        {
            Dictionary<string, NarrativeArcDefinitionData> byId = (definitions ?? Array.Empty<NarrativeArcDefinitionData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.arcDefinitionId))
                .ToDictionary(value => value.arcDefinitionId, value => value.Clone(), StringComparer.Ordinal);
            Dictionary<string, string[]> edges = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (NarrativeArcDefinitionData definition in byId.Values)
            {
                edges[definition.arcDefinitionId] = definition.stages
                    .SelectMany(stage => AllDependencies(stage))
                    .Where(dep => dep.kind == NarrativeArcDependencyKind.ArcCompleted || dep.kind == NarrativeArcDependencyKind.ArcResolved)
                    .Select(dep => dep.requiredId)
                    .Where(id => !string.IsNullOrWhiteSpace(id) && byId.ContainsKey(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            List<string> errors = new List<string>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in edges.Keys.OrderBy(value => value, StringComparer.Ordinal)) VisitArc(id, edges, visiting, visited, errors);
            return new NarrativeArcValidationReport(errors, Array.Empty<string>());
        }

        private static void ValidateStage(NarrativeArcStageDefinitionData stage, IDictionary<string, NarrativeArcStageDefinitionData> stages, IReadOnlyDictionary<string, IGameDefinition> definitionsById, ICollection<string> errors, ICollection<string> warnings)
        {
            if (stage == null)
            {
                errors.Add("stage definition is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(stage.stageDefinitionId)) errors.Add("stage has no stable NarrativeArcStageDefinitionId.");
            else if (!stage.stageDefinitionId.StartsWith("narrative-arc-stage-definition.", StringComparison.Ordinal)) warnings.Add($"stage '{stage.stageDefinitionId}' should use the 'narrative-arc-stage-definition.' namespace prefix.");
            else if (stages.ContainsKey(stage.stageDefinitionId)) errors.Add($"duplicate stage definition '{stage.stageDefinitionId}'.");
            else stages[stage.stageDefinitionId] = stage.Clone();

            ValidateDependencies("entry", stage.entryDependencies, errors);
            ValidateDependencies("completion", stage.completionDependencies, errors);
            ValidateDependencies("skip", stage.skipDependencies, errors);
            ValidateDependencies("failure", stage.failureDependencies, errors);
            ValidateActions(stage.entryActions, errors);
            ValidateActions(stage.completionActions, errors);
            ValidateQuestBindings(stage.questBindings, definitionsById, errors, warnings);
        }

        private static void ValidateDependencies(string group, IEnumerable<NarrativeArcDependencyDefinitionData> dependencies, ICollection<string> errors)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeArcDependencyDefinitionData dependency in dependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>())
            {
                if (dependency == null) continue;
                if (string.IsNullOrWhiteSpace(dependency.dependencyDefinitionId)) errors.Add($"{group} dependency has no stable ID.");
                else if (!ids.Add(dependency.dependencyDefinitionId)) errors.Add($"duplicate {group} dependency '{dependency.dependencyDefinitionId}'.");
                if (dependency.kind == NarrativeArcDependencyKind.Unknown) errors.Add($"dependency '{dependency.dependencyDefinitionId}' has Unknown kind.");
                if (RequiresRequiredId(dependency.kind) && string.IsNullOrWhiteSpace(dependency.requiredId)) errors.Add($"dependency '{dependency.dependencyDefinitionId}' requires requiredId.");
                if (RequiresStageIds(dependency.kind) && (dependency.stageDefinitionIds == null || dependency.stageDefinitionIds.Length == 0)) errors.Add($"dependency '{dependency.dependencyDefinitionId}' requires stageDefinitionIds.");
                if (dependency.kind == NarrativeArcDependencyKind.AtLeastNStagesResolved && dependency.minimumCount <= 0) errors.Add($"dependency '{dependency.dependencyDefinitionId}' requires a positive minimumCount.");
            }
        }

        private static void ValidateActions(IEnumerable<NarrativeActionDefinitionData> actions, ICollection<string> errors)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeActionDefinitionData action in actions ?? Array.Empty<NarrativeActionDefinitionData>())
            {
                if (action == null) continue;
                if (string.IsNullOrWhiteSpace(action.actionDefinitionId)) errors.Add("stage action has no stable ID.");
                else if (!ids.Add(action.actionDefinitionId)) errors.Add($"duplicate stage action '{action.actionDefinitionId}'.");
                if (action.category == NarrativeActionCategory.Unknown || action.category == NarrativeActionCategory.Custom) errors.Add($"stage action '{action.actionDefinitionId}' has unsupported category '{action.category}'.");
            }
        }

        private static void ValidateQuestBindings(IEnumerable<NarrativeArcQuestBindingDefinitionData> bindings, IReadOnlyDictionary<string, IGameDefinition> definitionsById, ICollection<string> errors, ICollection<string> warnings)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeArcQuestBindingDefinitionData binding in bindings ?? Array.Empty<NarrativeArcQuestBindingDefinitionData>())
            {
                if (binding == null) continue;
                if (string.IsNullOrWhiteSpace(binding.bindingDefinitionId)) errors.Add("quest binding has no stable ID.");
                else if (!ids.Add(binding.bindingDefinitionId)) errors.Add($"duplicate quest binding '{binding.bindingDefinitionId}'.");
                if (binding.mode == NarrativeArcQuestBindingMode.Unknown) errors.Add($"quest binding '{binding.bindingDefinitionId}' has Unknown mode.");
                if (binding.mode != NarrativeArcQuestBindingMode.ReferenceExistingQuest && string.IsNullOrWhiteSpace(binding.questDefinitionId)) errors.Add($"quest binding '{binding.bindingDefinitionId}' requires a QuestDefinitionId.");
                if (definitionsById != null && !string.IsNullOrWhiteSpace(binding.questDefinitionId) && !definitionsById.ContainsKey(binding.questDefinitionId)) warnings.Add($"quest binding '{binding.bindingDefinitionId}' references missing Quest definition '{binding.questDefinitionId}'.");
                if (definitionsById != null && !string.IsNullOrWhiteSpace(binding.questSourceId) && !definitionsById.ContainsKey(binding.questSourceId) && !binding.questSourceId.StartsWith("quest-source.", StringComparison.Ordinal)) warnings.Add($"quest binding '{binding.bindingDefinitionId}' references unknown Quest Source '{binding.questSourceId}'.");
            }
        }

        private static void ValidateStageGraph(NarrativeArcDefinitionData data, IReadOnlyDictionary<string, NarrativeArcStageDefinitionData> stages, ICollection<string> errors)
        {
            foreach (NarrativeArcStageDefinitionData stage in stages.Values)
            {
                foreach (NarrativeArcDependencyDefinitionData dependency in AllDependencies(stage))
                {
                    foreach (string stageId in DependencyStageReferences(dependency))
                    {
                        if (!stages.ContainsKey(stageId)) errors.Add($"stage '{stage.stageDefinitionId}' dependency '{dependency.dependencyDefinitionId}' references missing stage '{stageId}'.");
                    }
                }
            }

            Dictionary<string, string[]> edges = stages.Values.ToDictionary(
                value => value.stageDefinitionId,
                value => value.entryDependencies.SelectMany(DependencyStageReferences).Where(stages.ContainsKey).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in edges.Keys.OrderBy(value => value, StringComparer.Ordinal)) VisitStage(data.arcDefinitionId, id, edges, visiting, visited, errors);
        }

        private static bool VisitStage(string arcId, string id, IReadOnlyDictionary<string, string[]> edges, ISet<string> visiting, ISet<string> visited, ICollection<string> errors)
        {
            if (visited.Contains(id)) return true;
            if (!visiting.Add(id))
            {
                errors.Add($"stage dependency cycle detected in arc '{arcId}' at '{id}'.");
                return false;
            }

            foreach (string next in edges.TryGetValue(id, out string[] outbound) ? outbound : Array.Empty<string>()) VisitStage(arcId, next, edges, visiting, visited, errors);
            visiting.Remove(id);
            visited.Add(id);
            return true;
        }

        private static bool VisitArc(string id, IReadOnlyDictionary<string, string[]> edges, ISet<string> visiting, ISet<string> visited, ICollection<string> errors)
        {
            if (visited.Contains(id)) return true;
            if (!visiting.Add(id))
            {
                errors.Add($"cross-arc dependency cycle detected at '{id}'.");
                return false;
            }

            foreach (string next in edges.TryGetValue(id, out string[] outbound) ? outbound : Array.Empty<string>()) VisitArc(next, edges, visiting, visited, errors);
            visiting.Remove(id);
            visited.Add(id);
            return true;
        }

        private static IEnumerable<NarrativeArcDependencyDefinitionData> AllDependencies(NarrativeArcStageDefinitionData stage)
        {
            if (stage == null) return Array.Empty<NarrativeArcDependencyDefinitionData>();
            return (stage.entryDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>())
                .Concat(stage.completionDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>())
                .Concat(stage.skipDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>())
                .Concat(stage.failureDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>())
                .Where(value => value != null)
                .Select(value => value.Clone());
        }

        private static IEnumerable<string> DependencyStageReferences(NarrativeArcDependencyDefinitionData dependency)
        {
            if (dependency == null) return Array.Empty<string>();
            if (RequiresStageIds(dependency.kind)) return NarrativeModelUtility.Clean(dependency.stageDefinitionIds);
            if (dependency.kind == NarrativeArcDependencyKind.StageCompleted || dependency.kind == NarrativeArcDependencyKind.StageSkipped || dependency.kind == NarrativeArcDependencyKind.StageResolved) return new[] { dependency.requiredId };
            return Array.Empty<string>();
        }

        private static bool RequiresRequiredId(NarrativeArcDependencyKind kind)
        {
            return kind == NarrativeArcDependencyKind.StageCompleted
                || kind == NarrativeArcDependencyKind.StageSkipped
                || kind == NarrativeArcDependencyKind.StageResolved
                || kind == NarrativeArcDependencyKind.QuestOutcome
                || kind == NarrativeArcDependencyKind.NarrativeState
                || kind == NarrativeArcDependencyKind.DialogueChoice
                || kind == NarrativeArcDependencyKind.NarrativeEvent
                || kind == NarrativeArcDependencyKind.CurrentWorldCondition
                || kind == NarrativeArcDependencyKind.ArcCompleted
                || kind == NarrativeArcDependencyKind.ArcResolved
                || kind == NarrativeArcDependencyKind.Custom;
        }

        private static bool RequiresStageIds(NarrativeArcDependencyKind kind)
        {
            return kind == NarrativeArcDependencyKind.AllStagesResolved
                || kind == NarrativeArcDependencyKind.AnyStageResolved
                || kind == NarrativeArcDependencyKind.AtLeastNStagesResolved;
        }
    }
}
