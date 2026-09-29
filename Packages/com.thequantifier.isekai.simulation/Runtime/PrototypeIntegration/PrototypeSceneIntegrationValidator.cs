using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public sealed class PrototypeSceneIntegrationRuntimeContext
    {
        public LocationRuntime Locations { get; set; }
        public InteractionPointRuntime InteractionPoints { get; set; }
        public LocationConnectionRuntime Connections { get; set; }
        public EntityLocationRuntime EntityLocations { get; set; }
        public QuestSourceRuntime QuestSources { get; set; }
    }

    public static class PrototypeSceneIntegrationValidator
    {
        public static PrototypeSceneIntegrationValidationReport Validate(
            IEnumerable<WorldSceneBindingSnapshot> worldBindings,
            IEnumerable<PrototypeQuestSourceSceneBindingSnapshot> questSourceBindings,
            PrototypeSceneIntegrationRuntimeContext runtimeContext = null,
            IEnumerable<PrototypeSceneIntegrationIssue> precomputedIssues = null)
        {
            WorldSceneBindingSnapshot[] world = (worldBindings ?? Array.Empty<WorldSceneBindingSnapshot>())
                .Where(item => item != null)
                .ToArray();
            PrototypeQuestSourceSceneBindingSnapshot[] sources = (questSourceBindings ?? Array.Empty<PrototypeQuestSourceSceneBindingSnapshot>())
                .Where(item => item != null)
                .ToArray();
            List<PrototypeSceneIntegrationIssue> issues = new List<PrototypeSceneIntegrationIssue>();
            if (precomputedIssues != null)
            {
                issues.AddRange(precomputedIssues.Where(item => item != null));
            }

            ValidateExpectedWorldBindings(world, runtimeContext, issues);
            ValidateExpectedQuestSourceBindings(sources, runtimeContext, issues);
            ValidateDuplicateWorldBindings(world, issues);
            ValidateDuplicateQuestSources(sources, issues);
            ValidateWorldScope(world, sources, issues);

            return new PrototypeSceneIntegrationValidationReport(world, sources, issues);
        }

        public static PrototypeSceneIntegrationValidationReport ValidateComponents(
            IEnumerable<WorldSceneBindingComponent> worldBindings,
            IEnumerable<QuestSourceSceneBinding> questSourceBindings,
            PrototypeSceneIntegrationRuntimeContext runtimeContext = null,
            IEnumerable<PrototypeSceneIntegrationIssue> precomputedIssues = null)
        {
            return Validate(
                (worldBindings ?? Array.Empty<WorldSceneBindingComponent>()).Where(item => item != null).Select(item => item.CreateSnapshot()),
                (questSourceBindings ?? Array.Empty<QuestSourceSceneBinding>()).Where(item => item != null).Select(item => item.CreateSnapshot()),
                runtimeContext,
                precomputedIssues);
        }

        private static void ValidateExpectedWorldBindings(IReadOnlyList<WorldSceneBindingSnapshot> bindings, PrototypeSceneIntegrationRuntimeContext runtimeContext, ICollection<PrototypeSceneIntegrationIssue> issues)
        {
            foreach (PrototypeSceneWorldBindingExpectation expected in PrototypeSceneIntegrationContract.WorldBindings.Where(item => item.Required))
            {
                WorldSceneBindingSnapshot match = bindings.FirstOrDefault(binding =>
                    binding.Category == expected.Category
                    && string.Equals(binding.LogicalId, expected.LogicalId, StringComparison.Ordinal)
                    && string.Equals(binding.BindingKey, expected.BindingKey, StringComparison.Ordinal));

                if (match == null)
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.SceneBinding, expected.LogicalId, $"Required {expected.Category} binding '{expected.DisplayName}' is missing from the prototype scene."));
                    continue;
                }

                if (match.Role != expected.Role)
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.SceneBinding, expected.LogicalId, $"Binding '{match.BindingKey}' has role {match.Role}, expected {expected.Role}."));
                }

                if (runtimeContext != null && !LogicalRecordExists(expected, runtimeContext))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.RuntimeRecord, expected.LogicalId, $"Required logical {expected.Category} record is missing from its authoritative runtime."));
                }
            }
        }

        private static void ValidateExpectedQuestSourceBindings(IReadOnlyList<PrototypeQuestSourceSceneBindingSnapshot> bindings, PrototypeSceneIntegrationRuntimeContext runtimeContext, ICollection<PrototypeSceneIntegrationIssue> issues)
        {
            foreach (PrototypeQuestSourceBindingExpectation expected in PrototypeSceneIntegrationContract.QuestSourceBindings.Where(item => item.Required))
            {
                PrototypeQuestSourceSceneBindingSnapshot match = bindings.FirstOrDefault(binding =>
                    string.Equals(binding.QuestSourceId, expected.QuestSourceId, StringComparison.Ordinal)
                    && string.Equals(binding.BindingKey, expected.BindingKey, StringComparison.Ordinal));

                if (match == null)
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.QuestSourceBinding, expected.QuestSourceId, $"Required Quest Source binding '{expected.DisplayName}' is missing from the prototype scene."));
                    continue;
                }

                if (!string.Equals(match.DefinitionId, expected.DefinitionId, StringComparison.Ordinal))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.QuestSourceBinding, expected.QuestSourceId, $"Quest Source binding uses definition '{match.DefinitionId}', expected '{expected.DefinitionId}'."));
                }

                if (!string.Equals(match.HostLocationId, expected.HostLocationId, StringComparison.Ordinal))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.QuestSourceBinding, expected.QuestSourceId, $"Quest Source binding host location '{match.HostLocationId}' does not match '{expected.HostLocationId}'."));
                }

                if (!string.Equals(match.InteractionPointId, expected.InteractionPointId, StringComparison.Ordinal))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.QuestSourceBinding, expected.QuestSourceId, $"Quest Source binding interaction point '{match.InteractionPointId}' does not match '{expected.InteractionPointId}'."));
                }

                if (runtimeContext?.QuestSources != null && !runtimeContext.QuestSources.TryGetSource(expected.QuestSourceId, out QuestSourceSnapshot source))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.RuntimeRecord, expected.QuestSourceId, "Required Quest Source record is missing from QuestSourceRuntime."));
                    continue;
                }

                if (runtimeContext?.QuestSources != null && runtimeContext.QuestSources.TryGetSource(expected.QuestSourceId, out source))
                {
                    if (!string.Equals(source.QuestSourceDefinitionId, expected.DefinitionId, StringComparison.Ordinal))
                    {
                        issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.RuntimeRecord, expected.QuestSourceId, $"Quest Source runtime record uses definition '{source.QuestSourceDefinitionId}', expected '{expected.DefinitionId}'."));
                    }
                }
            }
        }

        private static void ValidateDuplicateWorldBindings(IReadOnlyList<WorldSceneBindingSnapshot> bindings, ICollection<PrototypeSceneIntegrationIssue> issues)
        {
            foreach (IGrouping<string, WorldSceneBindingSnapshot> group in bindings
                .Where(item => item.Role == WorldSceneBindingRole.Primary && item.Category != WorldSceneBindingCategory.PresentationOnly)
                .GroupBy(item => $"{item.WorldId}:{item.SceneKey}:{item.Category}:{item.LogicalId}", StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
            {
                issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.DuplicateBinding, group.First().LogicalId, $"Duplicate primary world scene bindings: {string.Join(", ", group.Select(item => item.DisplayName).OrderBy(value => value, StringComparer.Ordinal))}."));
            }
        }

        private static void ValidateDuplicateQuestSources(IReadOnlyList<PrototypeQuestSourceSceneBindingSnapshot> bindings, ICollection<PrototypeSceneIntegrationIssue> issues)
        {
            foreach (IGrouping<string, PrototypeQuestSourceSceneBindingSnapshot> group in bindings
                .Where(item => !string.IsNullOrWhiteSpace(item.QuestSourceId))
                .GroupBy(item => $"{item.WorldId}:{item.SceneKey}:{item.QuestSourceId}", StringComparer.Ordinal)
                .Where(group => group.Count() > 1))
            {
                issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.DuplicateBinding, group.First().QuestSourceId, $"Duplicate Quest Source scene bindings: {string.Join(", ", group.Select(item => item.DisplayName).OrderBy(value => value, StringComparer.Ordinal))}."));
            }
        }

        private static void ValidateWorldScope(IReadOnlyList<WorldSceneBindingSnapshot> world, IReadOnlyList<PrototypeQuestSourceSceneBindingSnapshot> sources, ICollection<PrototypeSceneIntegrationIssue> issues)
        {
            foreach (WorldSceneBindingSnapshot binding in world)
            {
                if (!string.Equals(binding.WorldId, PrototypeSceneIntegrationIds.WorldId, StringComparison.Ordinal) || !string.Equals(binding.SceneKey, PrototypeSceneIntegrationIds.SceneKey, StringComparison.Ordinal))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.SceneBinding, binding.LogicalId, $"World scene binding scope is '{binding.WorldId}/{binding.SceneKey}', expected '{PrototypeSceneIntegrationIds.WorldId}/{PrototypeSceneIntegrationIds.SceneKey}'."));
                }
            }

            foreach (PrototypeQuestSourceSceneBindingSnapshot binding in sources)
            {
                if (!string.Equals(binding.WorldId, PrototypeSceneIntegrationIds.WorldId, StringComparison.Ordinal) || !string.Equals(binding.SceneKey, PrototypeSceneIntegrationIds.SceneKey, StringComparison.Ordinal))
                {
                    issues.Add(Issue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.QuestSourceBinding, binding.QuestSourceId, $"Quest Source binding scope is '{binding.WorldId}/{binding.SceneKey}', expected '{PrototypeSceneIntegrationIds.WorldId}/{PrototypeSceneIntegrationIds.SceneKey}'."));
                }
            }
        }

        private static bool LogicalRecordExists(PrototypeSceneWorldBindingExpectation expected, PrototypeSceneIntegrationRuntimeContext runtimeContext)
        {
            return expected.Category switch
            {
                WorldSceneBindingCategory.Location => runtimeContext.Locations != null && runtimeContext.Locations.TryGetSnapshot(expected.LogicalId, out _),
                WorldSceneBindingCategory.InteractionPoint => runtimeContext.InteractionPoints != null && runtimeContext.InteractionPoints.TryGetPoint(expected.LogicalId, out _),
                WorldSceneBindingCategory.Connection => runtimeContext.Connections != null && runtimeContext.Connections.TryGetConnection(expected.LogicalId, out _),
                WorldSceneBindingCategory.Entity => runtimeContext.EntityLocations != null && runtimeContext.EntityLocations.ResolvePhysicalLocation(ParseEntity(expected.LogicalId)).Succeeded,
                _ => true
            };
        }

        private static EntityLocationReferenceData ParseEntity(string logicalId)
        {
            string[] parts = (logicalId ?? string.Empty).Split(':');
            if (parts.Length >= 3 && Enum.TryParse(parts[0], out LocationOccupantEntityType type))
            {
                return new EntityLocationReferenceData
                {
                    entityType = type,
                    worldId = parts[1],
                    entityId = string.Join(":", parts.Skip(2))
                };
            }

            return new EntityLocationReferenceData();
        }

        private static PrototypeSceneIntegrationIssue Issue(PrototypeSceneIntegrationIssueSeverity severity, PrototypeSceneIntegrationIssueDomain domain, string subjectId, string message)
        {
            return new PrototypeSceneIntegrationIssue(severity, domain, subjectId, message);
        }
    }
}
