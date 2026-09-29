using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;

namespace UnityIsekaiGame.Inventory.Integration
{
    public enum Step9IntegrationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum Step9IntegrationDiagnosticDomain
    {
        Authority,
        DefinitionCatalog,
        RuntimeIndex,
        ItemGraph,
        Location,
        Persistence,
        SaveSchema,
        Transaction,
        Snapshot,
        Determinism,
        Access,
        TestLab,
        PrototypeContent,
        Projection
    }

    public sealed class Step9IntegrationDiagnostic
    {
        public Step9IntegrationDiagnostic(
            Step9IntegrationDiagnosticSeverity severity,
            Step9IntegrationDiagnosticDomain domain,
            string code,
            string message,
            string subjectId = "")
        {
            Severity = severity;
            Domain = domain;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
        }

        public Step9IntegrationDiagnosticSeverity Severity { get; }
        public Step9IntegrationDiagnosticDomain Domain { get; }
        public string Code { get; }
        public string Message { get; }
        public string SubjectId { get; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(SubjectId)
                ? $"{Severity}: {Domain}/{Code}: {Message}"
                : $"{Severity}: {Domain}/{Code}: {SubjectId}: {Message}";
        }
    }

    public sealed class Step9IntegrationValidationReport
    {
        private readonly List<Step9IntegrationDiagnostic> diagnostics = new List<Step9IntegrationDiagnostic>();

        public IReadOnlyList<Step9IntegrationDiagnostic> Diagnostics => diagnostics;
        public int ErrorCount => diagnostics.Count(diagnostic => diagnostic.Severity == Step9IntegrationDiagnosticSeverity.Error);
        public int WarningCount => diagnostics.Count(diagnostic => diagnostic.Severity == Step9IntegrationDiagnosticSeverity.Warning);
        public int InfoCount => diagnostics.Count(diagnostic => diagnostic.Severity == Step9IntegrationDiagnosticSeverity.Info);
        public bool Succeeded => ErrorCount == 0;

        public void Add(
            Step9IntegrationDiagnosticSeverity severity,
            Step9IntegrationDiagnosticDomain domain,
            string code,
            string message,
            string subjectId = "")
        {
            diagnostics.Add(new Step9IntegrationDiagnostic(severity, domain, code, message, subjectId));
        }

        public void AddError(Step9IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "")
        {
            Add(Step9IntegrationDiagnosticSeverity.Error, domain, code, message, subjectId);
        }

        public void AddWarning(Step9IntegrationDiagnosticDomain domain, string code, string message, string subjectId = "")
        {
            Add(Step9IntegrationDiagnosticSeverity.Warning, domain, code, message, subjectId);
        }

        public string ToSummary()
        {
            return $"Errors={ErrorCount} Warnings={WarningCount} Info={InfoCount}";
        }
    }

    public sealed class Step9IntegrationRuntimeSnapshot
    {
        public Step9IntegrationRuntimeSnapshot(
            ItemInstanceRuntimeSaveData itemInstances = null,
            ItemCompositionRuntimeSaveData itemCompositions = null,
            ItemQualityAffixRuntimeSaveData itemQualityAffixes = null,
            ItemDurabilityRuntimeSaveData itemDurability = null,
            ProductionRequirementRuntimeSaveData productionRequirements = null,
            RecipeKnowledgeSaveData recipeKnowledge = null,
            CraftingExecutionRuntimeSaveData craftingExecution = null,
            ProductionWorkflowRuntimeSaveData productionWorkflow = null,
            ExperimentationRuntimeSaveData experimentation = null)
        {
            ItemInstances = itemInstances?.Clone() ?? new ItemInstanceRuntimeSaveData();
            ItemCompositions = itemCompositions?.Clone() ?? new ItemCompositionRuntimeSaveData();
            ItemQualityAffixes = itemQualityAffixes?.Clone() ?? new ItemQualityAffixRuntimeSaveData();
            ItemDurability = itemDurability?.Clone() ?? new ItemDurabilityRuntimeSaveData();
            ProductionRequirements = productionRequirements?.Clone() ?? new ProductionRequirementRuntimeSaveData();
            RecipeKnowledge = recipeKnowledge?.Clone() ?? new RecipeKnowledgeSaveData();
            CraftingExecution = craftingExecution?.Clone() ?? new CraftingExecutionRuntimeSaveData();
            ProductionWorkflow = productionWorkflow?.Clone() ?? new ProductionWorkflowRuntimeSaveData();
            Experimentation = experimentation?.Clone() ?? new ExperimentationRuntimeSaveData();
        }

        public ItemInstanceRuntimeSaveData ItemInstances { get; }
        public ItemCompositionRuntimeSaveData ItemCompositions { get; }
        public ItemQualityAffixRuntimeSaveData ItemQualityAffixes { get; }
        public ItemDurabilityRuntimeSaveData ItemDurability { get; }
        public ProductionRequirementRuntimeSaveData ProductionRequirements { get; }
        public RecipeKnowledgeSaveData RecipeKnowledge { get; }
        public CraftingExecutionRuntimeSaveData CraftingExecution { get; }
        public ProductionWorkflowRuntimeSaveData ProductionWorkflow { get; }
        public ExperimentationRuntimeSaveData Experimentation { get; }

        public Step9IntegrationRuntimeSnapshot Clone()
        {
            return new Step9IntegrationRuntimeSnapshot(
                ItemInstances,
                ItemCompositions,
                ItemQualityAffixes,
                ItemDurability,
                ProductionRequirements,
                RecipeKnowledge,
                CraftingExecution,
                ProductionWorkflow,
                Experimentation);
        }
    }

    public sealed class Step9IntegrationAuthorityEntry
    {
        public Step9IntegrationAuthorityEntry(string domain, string owner, params string[] readOnlyDependents)
        {
            Domain = domain ?? string.Empty;
            Owner = owner ?? string.Empty;
            ReadOnlyDependents = (readOnlyDependents ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public string Domain { get; }
        public string Owner { get; }
        public IReadOnlyList<string> ReadOnlyDependents { get; }
    }

    public sealed class Step9IntegrationDependencyEntry
    {
        public Step9IntegrationDependencyEntry(string owner, params string[] dependsOn)
        {
            Owner = owner ?? string.Empty;
            DependsOn = (dependsOn ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public string Owner { get; }
        public IReadOnlyList<string> DependsOn { get; }
    }
}
