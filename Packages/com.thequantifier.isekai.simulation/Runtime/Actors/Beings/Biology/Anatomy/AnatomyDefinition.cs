using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Anatomy
{
    [CreateAssetMenu(fileName = "Anatomy", menuName = "Unity Isekai Game/Beings/Biology/Anatomy")]
    public sealed class AnatomyDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string anatomyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private BodyFormDefinition[] compatibleBodyForms;
        [SerializeField] private SpeciesDefinition[] compatibleSpecies;
        [SerializeField] private AnatomyNodeDefinition[] nodes;
        [SerializeField] private TagDefinition[] tags;

        public string Id => anatomyId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public int SchemaVersion => schemaVersion;
        public IReadOnlyList<BodyFormDefinition> CompatibleBodyForms => compatibleBodyForms ?? Array.Empty<BodyFormDefinition>();
        public IReadOnlyList<SpeciesDefinition> CompatibleSpecies => compatibleSpecies ?? Array.Empty<SpeciesDefinition>();
        public IReadOnlyList<AnatomyNodeDefinition> Nodes => nodes ?? Array.Empty<AnatomyNodeDefinition>();
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();

        private void OnValidate()
        {
            anatomyId = anatomyId?.Trim();
            displayName = displayName?.Trim();
            schemaVersion = Math.Max(1, schemaVersion);
        }

        public bool IsCompatibleWith(BodyFormDefinition bodyForm, SpeciesDefinition species)
        {
            bool bodyFormAllowed = CompatibleBodyForms.Count == 0
                || (bodyForm != null && CompatibleBodyForms.Any(candidate => candidate != null && string.Equals(candidate.Id, bodyForm.Id, StringComparison.Ordinal)));
            bool speciesAllowed = CompatibleSpecies.Count == 0
                || (species != null && CompatibleSpecies.Any(candidate => candidate != null && string.Equals(candidate.Id, species.Id, StringComparison.Ordinal)));
            return bodyFormAllowed && speciesAllowed;
        }

        public bool TryGetNode(string nodeId, out AnatomyNodeDefinition node)
        {
            node = Nodes.FirstOrDefault(candidate => candidate != null && string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            return node != null;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"AnatomyDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("anatomy.", StringComparison.Ordinal))
            {
                report.AddWarning($"AnatomyDefinition '{Id}' should use the 'anatomy.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"AnatomyDefinition '{Id}' is missing a display name.");
            }

            ValidateDefinitionReferences(definitionsById, report);
            ValidateNodes(report);
            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private void ValidateDefinitionReferences(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (BodyFormDefinition bodyForm in CompatibleBodyForms)
            {
                if (bodyForm == null)
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' has a missing compatible body form reference.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(bodyForm.Id, out IGameDefinition found) || found is not BodyFormDefinition))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' references body form '{bodyForm.Id}', which is not in the configured catalog.");
                }
            }

            foreach (SpeciesDefinition species in CompatibleSpecies)
            {
                if (species == null)
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' has a missing compatible Species reference.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(species.Id, out IGameDefinition found) || found is not SpeciesDefinition))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' references Species '{species.Id}', which is not in the configured catalog.");
                }
            }

            foreach (TagDefinition tag in Tags)
            {
                if (tag == null)
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' has a missing tag reference.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(tag.Id, out IGameDefinition found) || found is not TagDefinition))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' references tag '{tag.Id}', which is not in the configured catalog.");
                }
            }
        }

        private void ValidateNodes(DefinitionValidationReport report)
        {
            IReadOnlyList<AnatomyNodeDefinition> nodeList = Nodes;
            if (nodeList.Count == 0)
            {
                report.AddError($"AnatomyDefinition '{DisplayName}' has no structural nodes.");
                return;
            }

            Dictionary<string, AnatomyNodeDefinition> byId = new Dictionary<string, AnatomyNodeDefinition>(StringComparer.Ordinal);
            Dictionary<string, string> childParents = new Dictionary<string, string>(StringComparer.Ordinal);
            int rootCount = 0;

            foreach (AnatomyNodeDefinition node in nodeList)
            {
                if (node == null)
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' has a missing node entry.");
                    continue;
                }

                DefinitionIdValidationResult nodeIdResult = DefinitionIdValidator.Validate(node.NodeId, $"AnatomyDefinition '{DisplayName}' node ID");
                foreach (DefinitionIdValidationMessage message in nodeIdResult.Messages)
                {
                    report.Add(message.Severity, message.Message);
                }

                if (!string.IsNullOrWhiteSpace(node.NodeId) && !byId.TryAdd(node.NodeId, node))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' has duplicate node ID '{node.NodeId}'.");
                }

                if (string.IsNullOrWhiteSpace(node.DisplayName))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' is missing a display name.");
                }

                if (string.IsNullOrWhiteSpace(node.ParentNodeId))
                {
                    rootCount++;
                }
                else if (string.Equals(node.NodeId, node.ParentNodeId, StringComparison.Ordinal))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' parents itself.");
                }
                else if (!childParents.TryAdd(node.NodeId, node.ParentNodeId))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' has multiple parents.");
                }

                if (!string.IsNullOrWhiteSpace(node.RegionNodeId) && string.Equals(node.NodeId, node.RegionNodeId, StringComparison.Ordinal))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' uses itself as a region reference.");
                }

                if (node.Vital && node.DefaultPresence == AnatomyPresenceState.Absent)
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' vital node '{node.NodeId}' cannot default to Absent.");
                }

                if (node.InternalStructure && string.IsNullOrWhiteSpace(node.ParentNodeId))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' internal node '{node.NodeId}' must have a container parent.");
                }

                if (node.RelativeTargetingWeight < 0f)
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' has a negative targeting weight.");
                }
            }

            if (rootCount != 1)
            {
                report.AddError($"AnatomyDefinition '{DisplayName}' must have exactly one root node but has {rootCount}.");
            }

            foreach (AnatomyNodeDefinition node in nodeList.Where(node => node != null))
            {
                if (!string.IsNullOrWhiteSpace(node.ParentNodeId) && !byId.ContainsKey(node.ParentNodeId))
                {
                    report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' references missing parent '{node.ParentNodeId}'.");
                }

                if (!string.IsNullOrWhiteSpace(node.RegionNodeId))
                {
                    if (!byId.TryGetValue(node.RegionNodeId, out AnatomyNodeDefinition region) || region.Category != AnatomyStructuralCategory.Region)
                    {
                        report.AddError($"AnatomyDefinition '{DisplayName}' node '{node.NodeId}' references invalid region '{node.RegionNodeId}'.");
                    }
                }
            }

            ValidateCycles(byId, report);
            ValidateMirrorGroups(nodeList, report);
        }

        private void ValidateCycles(Dictionary<string, AnatomyNodeDefinition> byId, DefinitionValidationReport report)
        {
            foreach (AnatomyNodeDefinition node in byId.Values)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                string cursor = node.NodeId;
                while (byId.TryGetValue(cursor, out AnatomyNodeDefinition current) && !string.IsNullOrWhiteSpace(current.ParentNodeId))
                {
                    if (!seen.Add(cursor))
                    {
                        report.AddError($"AnatomyDefinition '{DisplayName}' has a circular parent chain involving '{node.NodeId}'.");
                        break;
                    }

                    cursor = current.ParentNodeId;
                }
            }
        }

        private void ValidateMirrorGroups(IReadOnlyList<AnatomyNodeDefinition> nodeList, DefinitionValidationReport report)
        {
            foreach (IGrouping<string, AnatomyNodeDefinition> group in nodeList
                         .Where(node => node != null && !string.IsNullOrWhiteSpace(node.MirrorGroup))
                         .GroupBy(node => node.MirrorGroup, StringComparer.Ordinal))
            {
                HashSet<AnatomyBodySide> sides = new HashSet<AnatomyBodySide>();
                foreach (AnatomyNodeDefinition node in group)
                {
                    if (node.BodySide == AnatomyBodySide.None || node.BodySide == AnatomyBodySide.Unknown)
                    {
                        report.AddError($"AnatomyDefinition '{DisplayName}' mirrored node '{node.NodeId}' must specify a deterministic side.");
                    }
                    else if (!sides.Add(node.BodySide))
                    {
                        report.AddError($"AnatomyDefinition '{DisplayName}' mirror group '{group.Key}' duplicates side '{node.BodySide}'.");
                    }
                }
            }
        }

        private void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            AnatomyDefinition firstAnatomy = definitionsById.Values.OfType<AnatomyDefinition>().OrderBy(anatomy => anatomy.Id, StringComparer.Ordinal).FirstOrDefault();
            if (!ReferenceEquals(firstAnatomy, this))
            {
                return;
            }

            RequireCanonical(definitionsById, "anatomy.human", "Human Anatomy", report);
            RequireCanonical(definitionsById, "anatomy.basic-construct", "Basic Construct Anatomy", report);
            RequireCanonical(definitionsById, "anatomy.basic-spirit", "Basic Spirit Anatomy", report);
        }

        private static void RequireCanonical(IReadOnlyDictionary<string, IGameDefinition> definitionsById, string id, string label, DefinitionValidationReport report)
        {
            if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not AnatomyDefinition)
            {
                report.AddError($"{label} '{id}' must be registered in the canonical alpha definition catalog.");
            }
        }
    }
}
