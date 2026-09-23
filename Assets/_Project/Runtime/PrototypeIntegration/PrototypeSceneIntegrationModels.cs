using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public enum PrototypeSceneIntegrationIssueSeverity
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    public enum PrototypeSceneIntegrationIssueDomain
    {
        Contract,
        SceneBinding,
        QuestSourceBinding,
        RuntimeRecord,
        DuplicateBinding,
        LegacyConflict,
        MissingScript,
        Placeholder
    }

    public sealed class PrototypeSceneIntegrationIssue
    {
        public PrototypeSceneIntegrationIssue(PrototypeSceneIntegrationIssueSeverity severity, PrototypeSceneIntegrationIssueDomain domain, string subjectId, string message)
        {
            Severity = severity;
            Domain = domain;
            SubjectId = N(subjectId);
            Message = message ?? string.Empty;
        }

        public PrototypeSceneIntegrationIssueSeverity Severity { get; }
        public PrototypeSceneIntegrationIssueDomain Domain { get; }
        public string SubjectId { get; }
        public string Message { get; }
        public bool IsFailure => Severity == PrototypeSceneIntegrationIssueSeverity.Error || Severity == PrototypeSceneIntegrationIssueSeverity.Fatal;
        public override string ToString() => $"{Severity}: {Domain} '{SubjectId}' - {Message}";
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class PrototypeSceneWorldBindingExpectation
    {
        public PrototypeSceneWorldBindingExpectation(
            WorldSceneBindingCategory category,
            string logicalId,
            string bindingKey,
            string displayName,
            WorldSceneBindingRole role = WorldSceneBindingRole.Primary,
            bool required = true,
            string expectedDefinitionId = "",
            string sourceLocationId = "",
            string destinationLocationId = "")
        {
            Category = category;
            LogicalId = N(logicalId);
            BindingKey = N(bindingKey);
            DisplayName = N(displayName);
            Role = role;
            Required = required;
            ExpectedDefinitionId = N(expectedDefinitionId);
            SourceLocationId = N(sourceLocationId);
            DestinationLocationId = N(destinationLocationId);
        }

        public WorldSceneBindingCategory Category { get; }
        public string LogicalId { get; }
        public string BindingKey { get; }
        public string DisplayName { get; }
        public string WorldId => PrototypeSceneIntegrationIds.WorldId;
        public string SceneKey => PrototypeSceneIntegrationIds.SceneKey;
        public WorldSceneBindingRole Role { get; }
        public bool Required { get; }
        public string ExpectedDefinitionId { get; }
        public string SourceLocationId { get; }
        public string DestinationLocationId { get; }
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class PrototypeQuestSourceBindingExpectation
    {
        public PrototypeQuestSourceBindingExpectation(
            string questSourceId,
            string definitionId,
            string bindingKey,
            string displayName,
            string hostLocationId,
            string interactionPointId,
            string operatingOrganizationId = "",
            string operatingGovernmentId = "",
            bool required = true)
        {
            QuestSourceId = N(questSourceId);
            DefinitionId = N(definitionId);
            BindingKey = N(bindingKey);
            DisplayName = N(displayName);
            HostLocationId = N(hostLocationId);
            InteractionPointId = N(interactionPointId);
            OperatingOrganizationId = N(operatingOrganizationId);
            OperatingGovernmentId = N(operatingGovernmentId);
            Required = required;
        }

        public string QuestSourceId { get; }
        public string DefinitionId { get; }
        public string BindingKey { get; }
        public string DisplayName { get; }
        public string WorldId => PrototypeSceneIntegrationIds.WorldId;
        public string SceneKey => PrototypeSceneIntegrationIds.SceneKey;
        public string HostLocationId { get; }
        public string InteractionPointId { get; }
        public string OperatingOrganizationId { get; }
        public string OperatingGovernmentId { get; }
        public bool Required { get; }
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class PrototypeQuestSourceSceneBindingSnapshot
    {
        public PrototypeQuestSourceSceneBindingSnapshot(string questSourceId, string definitionId, string bindingKey, string sceneKey, string worldId, string displayName, string hostLocationId, string interactionPointId, bool required)
        {
            QuestSourceId = N(questSourceId);
            DefinitionId = N(definitionId);
            BindingKey = N(bindingKey);
            SceneKey = N(sceneKey);
            WorldId = N(worldId);
            DisplayName = N(displayName);
            HostLocationId = N(hostLocationId);
            InteractionPointId = N(interactionPointId);
            Required = required;
        }

        public string QuestSourceId { get; }
        public string DefinitionId { get; }
        public string BindingKey { get; }
        public string SceneKey { get; }
        public string WorldId { get; }
        public string DisplayName { get; }
        public string HostLocationId { get; }
        public string InteractionPointId { get; }
        public bool Required { get; }
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class PrototypeScenePhysicalSurfaceExpectation
    {
        public PrototypeScenePhysicalSurfaceExpectation(string surfaceId, string displayName, string hierarchyPath, string logicalBindingId, string replacementExpectation)
        {
            SurfaceId = N(surfaceId);
            DisplayName = N(displayName);
            HierarchyPath = N(hierarchyPath);
            LogicalBindingId = N(logicalBindingId);
            ReplacementExpectation = N(replacementExpectation);
        }

        public string SurfaceId { get; }
        public string DisplayName { get; }
        public string HierarchyPath { get; }
        public string LogicalBindingId { get; }
        public string ReplacementExpectation { get; }
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class PrototypeSceneIntegrationValidationReport
    {
        public PrototypeSceneIntegrationValidationReport(
            IEnumerable<WorldSceneBindingSnapshot> worldBindings,
            IEnumerable<PrototypeQuestSourceSceneBindingSnapshot> questSourceBindings,
            IEnumerable<PrototypeSceneIntegrationIssue> issues)
        {
            WorldBindings = (worldBindings ?? Array.Empty<WorldSceneBindingSnapshot>())
                .Where(item => item != null)
                .OrderBy(item => item.Category)
                .ThenBy(item => item.LogicalId, StringComparer.Ordinal)
                .ThenBy(item => item.BindingKey, StringComparer.Ordinal)
                .ToArray();
            QuestSourceBindings = (questSourceBindings ?? Array.Empty<PrototypeQuestSourceSceneBindingSnapshot>())
                .Where(item => item != null)
                .OrderBy(item => item.QuestSourceId, StringComparer.Ordinal)
                .ThenBy(item => item.BindingKey, StringComparer.Ordinal)
                .ToArray();
            Issues = (issues ?? Array.Empty<PrototypeSceneIntegrationIssue>())
                .Where(item => item != null)
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.Domain)
                .ThenBy(item => item.SubjectId, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<WorldSceneBindingSnapshot> WorldBindings { get; }
        public IReadOnlyList<PrototypeQuestSourceSceneBindingSnapshot> QuestSourceBindings { get; }
        public IReadOnlyList<PrototypeSceneIntegrationIssue> Issues { get; }
        public IReadOnlyList<PrototypeSceneIntegrationIssue> Failures => Issues.Where(item => item.IsFailure).ToArray();
        public int ErrorCount => Issues.Count(item => item.Severity == PrototypeSceneIntegrationIssueSeverity.Error);
        public int FatalCount => Issues.Count(item => item.Severity == PrototypeSceneIntegrationIssueSeverity.Fatal);
        public int WarningCount => Issues.Count(item => item.Severity == PrototypeSceneIntegrationIssueSeverity.Warning);
        public int InfoCount => Issues.Count(item => item.Severity == PrototypeSceneIntegrationIssueSeverity.Info);
        public bool Succeeded => Failures.Count == 0;
        public string Summary => $"WorldBindings={WorldBindings.Count} QuestSourceBindings={QuestSourceBindings.Count} Errors={ErrorCount} Fatal={FatalCount} Warnings={WarningCount}";
    }

    public static class PrototypeSceneIntegrationIds
    {
        public const string WorldId = PersistenceService.LocalWorldId;
        public const string SceneKey = "scene.prototype";

        public const string AdventurerGuildBoardSourceId = "quest-source.prototype.adventurers-guild-board";
        public const string AdventurerGuildCounterSourceId = "quest-source.prototype.adventurers-guild-counter";
        public const string MerchantGuildCounterSourceId = "quest-source.prototype.merchant-guild-counter";
        public const string MayorOfficeDeskSourceId = "quest-source.prototype.mayor-office-desk";
        public const string CityRecordsArchiveSourceId = "quest-source.prototype.city-records-archive";
    }
}
