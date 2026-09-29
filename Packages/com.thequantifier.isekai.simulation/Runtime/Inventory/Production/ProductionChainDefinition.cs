using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Recipes;

namespace UnityIsekaiGame.Inventory.Production
{
    [CreateAssetMenu(fileName = "ProductionChainDefinition", menuName = "Unity Isekai Game/Inventory/Production Chain Definition")]
    public sealed class ProductionChainDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string chainId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string category;
        [SerializeField] private string currentVersionId;
        [SerializeField] private ProductionChainLifecycleState state = ProductionChainLifecycleState.Active;
        [SerializeField] private ProductionBatchConsistencyPolicy batchConsistencyPolicy = ProductionBatchConsistencyPolicy.IdenticalAuthoritativeState;
        [SerializeField] private ProductionPartialBatchPolicy partialBatchPolicy = ProductionPartialBatchPolicy.AllOrNothing;
        [SerializeField] private ProductionInputConsumptionPolicy inputPolicy = ProductionInputConsumptionPolicy.ReservedAtStartConsumedAtCompletion;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private string secrecyClassification;
        [SerializeField] private string provenance;
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private ProductionChainVersionData[] versions = Array.Empty<ProductionChainVersionData>();

        public string Id => chainId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public string Category => category ?? string.Empty;
        public string CurrentVersionId => currentVersionId ?? string.Empty;
        public ProductionChainLifecycleState State => state;
        public ProductionBatchConsistencyPolicy BatchConsistencyPolicy => batchConsistencyPolicy;
        public ProductionPartialBatchPolicy PartialBatchPolicy => partialBatchPolicy;
        public ProductionInputConsumptionPolicy InputPolicy => inputPolicy;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public string SecrecyClassification => secrecyClassification ?? string.Empty;
        public string Provenance => provenance ?? string.Empty;
        public int SchemaVersion => Math.Max(1, schemaVersion);
        public IReadOnlyList<ProductionChainVersionData> Versions => (versions ?? Array.Empty<ProductionChainVersionData>()).Select(version => version.Clone()).ToArray();

        public bool TryGetVersion(string versionId, out ProductionChainVersionData version)
        {
            string resolved = string.IsNullOrWhiteSpace(versionId) ? CurrentVersionId : versionId;
            version = Versions.FirstOrDefault(entry => string.Equals(entry.versionId, resolved, StringComparison.Ordinal))?.Clone();
            return version != null;
        }

        private void OnValidate()
        {
            chainId = chainId?.Trim();
            currentVersionId = currentVersionId?.Trim();
            schemaVersion = Math.Max(1, schemaVersion);
            versions ??= Array.Empty<ProductionChainVersionData>();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Production Chain definition '{name}' is missing an ID.");
            }
            else if (!Id.StartsWith("production-chain.", StringComparison.Ordinal))
            {
                report.AddWarning($"Production Chain definition '{Id}' should use the 'production-chain.' namespace prefix.");
            }

            ProductionChainVersionData[] values = Versions.ToArray();
            if (values.Length == 0)
            {
                report.AddError($"Production Chain '{DisplayName}' must declare at least one version.");
                return;
            }

            HashSet<string> versionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionChainVersionData version in values)
            {
                if (string.IsNullOrWhiteSpace(version.versionId))
                {
                    report.AddError($"Production Chain '{DisplayName}' has a version without an ID.");
                    continue;
                }

                if (!versionIds.Add(version.versionId))
                {
                    report.AddError($"Production Chain '{DisplayName}' has duplicate version '{version.versionId}'.");
                }

                if (!string.IsNullOrWhiteSpace(version.chainDefinitionId) && !string.Equals(version.chainDefinitionId, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Production Chain version '{version.versionId}' belongs to '{version.chainDefinitionId}', not '{Id}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(CurrentVersionId) || !versionIds.Contains(CurrentVersionId))
            {
                report.AddError($"Production Chain '{DisplayName}' references missing current version '{CurrentVersionId}'.");
            }

            foreach (ProductionChainVersionData version in values)
            {
                if (!string.IsNullOrWhiteSpace(version.priorVersionId) && !versionIds.Contains(version.priorVersionId))
                {
                    report.AddError($"Production Chain version '{version.versionId}' references missing prior version '{version.priorVersionId}'.");
                }

                if (!string.IsNullOrWhiteSpace(version.supersedingVersionId) && !versionIds.Contains(version.supersedingVersionId))
                {
                    report.AddError($"Production Chain version '{version.versionId}' references missing superseding version '{version.supersedingVersionId}'.");
                }

                if (HasVersionCycle(version.versionId, values))
                {
                    report.AddError($"Production Chain '{DisplayName}' version lineage contains a cycle at '{version.versionId}'.");
                }

                ValidateStages(version, definitionsById, report);
            }
        }

        private void ValidateStages(ProductionChainVersionData version, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            ProductionStageDefinitionData[] stages = version.stages ?? Array.Empty<ProductionStageDefinitionData>();
            if (stages.Length == 0)
            {
                report.AddError($"Production Chain version '{version.versionId}' must declare at least one stage.");
                return;
            }

            HashSet<string> stageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionStageDefinitionData stage in stages)
            {
                if (stage == null || string.IsNullOrWhiteSpace(stage.stageId))
                {
                    report.AddError($"Production Chain version '{version.versionId}' has a stage without an ID.");
                    continue;
                }

                if (!stageIds.Add(stage.stageId))
                {
                    report.AddError($"Production Chain version '{version.versionId}' has duplicate stage '{stage.stageId}'.");
                }

                if (stage.repeatCount < 1)
                {
                    report.AddError($"Production stage '{stage.stageId}' has an invalid repeat count.");
                }

                if (!string.IsNullOrWhiteSpace(stage.recipeDefinitionId)
                    && (definitionsById == null || !definitionsById.TryGetValue(stage.recipeDefinitionId, out IGameDefinition recipe) || recipe is not RecipeDefinition))
                {
                    report.AddError($"Production stage '{stage.stageId}' references missing Recipe definition '{stage.recipeDefinitionId}'.");
                }

                foreach (string requirementId in stage.requirementIds ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(requirementId)
                        && (definitionsById == null || !definitionsById.TryGetValue(requirementId, out IGameDefinition requirement) || requirement is not ProductionRequirementDefinition))
                    {
                        report.AddError($"Production stage '{stage.stageId}' references missing Production Requirement definition '{requirementId}'.");
                    }
                }
            }

            foreach (ProductionStageDefinitionData stage in stages.Where(stage => stage != null))
            {
                foreach (string dependency in stage.dependencyStageIds ?? Array.Empty<string>())
                {
                    if (!stageIds.Contains(dependency))
                    {
                        report.AddError($"Production stage '{stage.stageId}' references missing dependency '{dependency}'.");
                    }
                }
            }

            if (HasStageCycle(stages, out string cycleStage))
            {
                report.AddError($"Production Chain version '{version.versionId}' stage graph contains a cycle at '{cycleStage}'.");
            }

            HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionStageDefinitionData root in stages.Where(stage => stage != null && (stage.dependencyStageIds == null || stage.dependencyStageIds.Length == 0)).OrderBy(stage => stage.stageId, StringComparer.Ordinal))
            {
                MarkReachable(root.stageId, stages, reachable);
            }

            foreach (ProductionStageDefinitionData required in stages.Where(stage => stage != null && !stage.optional))
            {
                if (!reachable.Contains(required.stageId))
                {
                    report.AddError($"Required production stage '{required.stageId}' is unreachable.");
                }
            }
        }

        private static bool HasVersionCycle(string start, IEnumerable<ProductionChainVersionData> values)
        {
            Dictionary<string, string> priorById = (values ?? Array.Empty<ProductionChainVersionData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.versionId))
                .ToDictionary(value => value.versionId, value => value.priorVersionId ?? string.Empty, StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string cursor = start;
            while (!string.IsNullOrWhiteSpace(cursor) && priorById.TryGetValue(cursor, out string prior))
            {
                if (!seen.Add(cursor))
                {
                    return true;
                }

                cursor = prior;
            }

            return false;
        }

        private static bool HasStageCycle(IReadOnlyList<ProductionStageDefinitionData> stages, out string cycleStage)
        {
            cycleStage = string.Empty;
            Dictionary<string, ProductionStageDefinitionData> byId = (stages ?? Array.Empty<ProductionStageDefinitionData>())
                .Where(stage => stage != null && !string.IsNullOrWhiteSpace(stage.stageId))
                .ToDictionary(stage => stage.stageId, StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in byId.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (Visit(id, byId, visiting, visited, out cycleStage))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Visit(string id, IReadOnlyDictionary<string, ProductionStageDefinitionData> byId, HashSet<string> visiting, HashSet<string> visited, out string cycleStage)
        {
            cycleStage = string.Empty;
            if (visited.Contains(id))
            {
                return false;
            }

            if (!visiting.Add(id))
            {
                cycleStage = id;
                return true;
            }

            if (byId.TryGetValue(id, out ProductionStageDefinitionData stage))
            {
                foreach (string dependency in stage.dependencyStageIds ?? Array.Empty<string>())
                {
                    if (byId.ContainsKey(dependency) && Visit(dependency, byId, visiting, visited, out cycleStage))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        private static void MarkReachable(string stageId, IReadOnlyList<ProductionStageDefinitionData> stages, HashSet<string> reachable)
        {
            if (!reachable.Add(stageId))
            {
                return;
            }

            foreach (ProductionStageDefinitionData child in stages.Where(stage => stage != null && (stage.dependencyStageIds ?? Array.Empty<string>()).Contains(stageId, StringComparer.Ordinal)).OrderBy(stage => stage.stageId, StringComparer.Ordinal))
            {
                MarkReachable(child.stageId, stages, reachable);
            }
        }
    }
}
