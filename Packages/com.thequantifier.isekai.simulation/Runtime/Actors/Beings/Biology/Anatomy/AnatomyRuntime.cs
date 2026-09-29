using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Anatomy
{
    public sealed class AnatomyRuntime
    {
        private readonly Dictionary<string, AnatomyRuntimeNode> nodesById = new Dictionary<string, AnatomyRuntimeNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, AnatomyPresenceState> presenceOverrides = new Dictionary<string, AnatomyPresenceState>(StringComparer.Ordinal);
        private string actorBodyId;
        private string anatomyDefinitionId;
        private string rootNodeId;

        public AnatomyReadinessState Readiness { get; private set; } = AnatomyReadinessState.Uninitialized;
        public long AnatomyRevision { get; private set; }
        public string ActorBodyId => actorBodyId ?? string.Empty;
        public string AnatomyDefinitionId => anatomyDefinitionId ?? string.Empty;
        public string RootNodeId => rootNodeId ?? string.Empty;
        public IReadOnlyDictionary<string, AnatomyPresenceState> PresenceOverrides => presenceOverrides;
        public bool IsReady => Readiness == AnatomyReadinessState.Ready;

        public BodyOperationResult Build(
            string exactActorBodyId,
            long bodyRevision,
            SpeciesDefinition species,
            AnatomyDefinition definition,
            IReadOnlyDictionary<string, AnatomyPresenceState> overrides,
            bool restoring,
            bool preserveRevision = false)
        {
            if (string.IsNullOrWhiteSpace(exactActorBodyId))
            {
                Readiness = AnatomyReadinessState.Invalid;
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingActorBody, "Anatomy build requires an exact Actor/body ID.");
            }

            if (species == null)
            {
                Readiness = AnatomyReadinessState.Invalid;
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingSpecies, "Anatomy build requires a resolved Species.");
            }

            if (definition == null)
            {
                Readiness = AnatomyReadinessState.Invalid;
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingAnatomyDefinition, $"Species '{species.Id}' does not resolve an Anatomy definition.");
            }

            if (!definition.IsCompatibleWith(species.BodyForm, species))
            {
                Readiness = AnatomyReadinessState.Invalid;
                return BodyOperationResult.Failure(BodyOperationResultCode.InvalidAnatomyDefinition, $"Anatomy '{definition.Id}' is not compatible with Species '{species.Id}' and body form '{species.BodyForm?.Id ?? string.Empty}'.");
            }

            Dictionary<string, AnatomyRuntimeNode> candidateNodes = new Dictionary<string, AnatomyRuntimeNode>(StringComparer.Ordinal);
            Dictionary<string, AnatomyPresenceState> candidateOverrides = new Dictionary<string, AnatomyPresenceState>(StringComparer.Ordinal);
            try
            {
                Readiness = restoring ? AnatomyReadinessState.Restoring : AnatomyReadinessState.ResolvingDefinition;
                foreach (KeyValuePair<string, AnatomyPresenceState> pair in overrides ?? new Dictionary<string, AnatomyPresenceState>())
                {
                    candidateOverrides[pair.Key] = pair.Value;
                }

                Readiness = AnatomyReadinessState.BuildingHierarchy;
                string candidateRoot = BuildNodes(exactActorBodyId, definition, candidateOverrides, candidateNodes);
                Readiness = AnatomyReadinessState.ValidatingStructure;
                string validation = ValidateRuntime(candidateRoot, candidateNodes);
                if (!string.IsNullOrWhiteSpace(validation))
                {
                    Readiness = AnatomyReadinessState.Invalid;
                    return BodyOperationResult.Failure(BodyOperationResultCode.AnatomyConstructionFailure, validation);
                }

                actorBodyId = exactActorBodyId;
                anatomyDefinitionId = definition.Id;
                rootNodeId = candidateRoot;
                nodesById.Clear();
                foreach (KeyValuePair<string, AnatomyRuntimeNode> pair in candidateNodes.OrderBy(pair => pair.Value.Order).ThenBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    nodesById[pair.Key] = pair.Value;
                }

                presenceOverrides.Clear();
                foreach (KeyValuePair<string, AnatomyPresenceState> pair in candidateOverrides.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    presenceOverrides[pair.Key] = pair.Value;
                }

                if (!preserveRevision)
                {
                    AnatomyRevision++;
                }

                Readiness = AnatomyReadinessState.Ready;
                return BodyOperationResult.Success("Anatomy built.", null);
            }
            catch (Exception exception)
            {
                Readiness = AnatomyReadinessState.Invalid;
                return BodyOperationResult.Failure(BodyOperationResultCode.AnatomyConstructionFailure, $"Anatomy build failed: {exception.Message}");
            }
        }

        public BodyOperationResult SetPresenceOverride(AnatomyDefinition definition, string nodeId, AnatomyPresenceState presence)
        {
            if (!IsReady)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.RuntimeNotReady, "Anatomy presence override requires a Ready anatomy runtime.");
            }

            if (definition == null || !definition.TryGetNode(nodeId, out AnatomyNodeDefinition node) || node == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.InvalidAnatomyPresenceOverride, $"Anatomy node '{nodeId}' does not exist.");
            }

            if (!node.Optional && presence != node.DefaultPresence)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.InvalidAnatomyPresenceOverride, $"Anatomy node '{nodeId}' is not optional and cannot be overridden.");
            }

            if (node.Vital && presence == AnatomyPresenceState.Absent)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.InvalidAnatomyPresenceOverride, $"Vital anatomy node '{nodeId}' cannot be made absent.");
            }

            AnatomyPresenceState previous = presenceOverrides.TryGetValue(nodeId, out AnatomyPresenceState existing)
                ? existing
                : node.DefaultPresence;
            if (previous == presence)
            {
                return BodyOperationResult.Success($"Anatomy node '{nodeId}' already has presence '{presence}'.", null, duplicate: true);
            }

            presenceOverrides[nodeId] = presence;
            if (nodesById.TryGetValue(nodeId, out AnatomyRuntimeNode runtimeNode))
            {
                runtimeNode.SetPresence(presence);
            }

            AnatomyRevision++;
            return BodyOperationResult.Success($"Anatomy node '{nodeId}' presence set to '{presence}'.", null);
        }

        public void SetRevision(long revision)
        {
            AnatomyRevision = Math.Max(0L, revision);
        }

        public AnatomySaveData CreateSaveData()
        {
            return new AnatomySaveData
            {
                schemaVersion = AnatomySaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                anatomyDefinitionId = AnatomyDefinitionId,
                anatomyRevision = AnatomyRevision,
                presenceOverrides = presenceOverrides
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new AnatomyPresenceOverrideData { nodeId = pair.Key, presence = pair.Value })
                    .ToArray()
            };
        }

        public AnatomySnapshot CreateSnapshot(long bodyRevision)
        {
            string validation = ValidateRuntime(rootNodeId, nodesById);
            List<AnatomyNodeSnapshot> nodes = nodesById.Values
                .OrderBy(node => node.Order)
                .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                .Select(node => node.CreateSnapshot())
                .ToList();
            return new AnatomySnapshot(
                ActorBodyId,
                AnatomyDefinitionId,
                Readiness,
                bodyRevision,
                AnatomyRevision,
                RootNodeId,
                nodes,
                string.IsNullOrWhiteSpace(validation),
                string.IsNullOrWhiteSpace(validation) ? Array.Empty<string>() : new[] { validation });
        }

        public bool TryGetNode(string nodeId, out AnatomyRuntimeNode node)
        {
            return nodesById.TryGetValue(nodeId, out node);
        }

        public void Dispose()
        {
            Readiness = AnatomyReadinessState.Disposed;
            nodesById.Clear();
            presenceOverrides.Clear();
            rootNodeId = string.Empty;
        }

        private static string BuildNodes(
            string exactActorBodyId,
            AnatomyDefinition definition,
            IReadOnlyDictionary<string, AnatomyPresenceState> overrides,
            Dictionary<string, AnatomyRuntimeNode> candidateNodes)
        {
            string root = string.Empty;
            foreach (AnatomyNodeDefinition node in definition.Nodes.Where(node => node != null).OrderBy(node => node.Order).ThenBy(node => node.NodeId, StringComparer.Ordinal))
            {
                if (candidateNodes.ContainsKey(node.NodeId))
                {
                    throw new InvalidOperationException($"Duplicate runtime anatomy node '{node.NodeId}'.");
                }

                AnatomyPresenceState presence = overrides != null && overrides.TryGetValue(node.NodeId, out AnatomyPresenceState overridden)
                    ? overridden
                    : node.DefaultPresence;
                candidateNodes[node.NodeId] = new AnatomyRuntimeNode(exactActorBodyId, definition.Id, node, presence);
                if (string.IsNullOrWhiteSpace(node.ParentNodeId))
                {
                    root = node.NodeId;
                }
            }

            foreach (AnatomyRuntimeNode node in candidateNodes.Values)
            {
                if (!string.IsNullOrWhiteSpace(node.ParentNodeId) && candidateNodes.TryGetValue(node.ParentNodeId, out AnatomyRuntimeNode parent))
                {
                    parent.AddChild(node.NodeId);
                }
            }

            return root;
        }

        private static string ValidateRuntime(string root, IReadOnlyDictionary<string, AnatomyRuntimeNode> nodes)
        {
            if (string.IsNullOrWhiteSpace(root) || nodes == null || !nodes.ContainsKey(root))
            {
                return "Anatomy runtime is missing a valid root node.";
            }

            foreach (AnatomyRuntimeNode node in nodes.Values)
            {
                if (!string.IsNullOrWhiteSpace(node.ParentNodeId) && !nodes.ContainsKey(node.ParentNodeId))
                {
                    return $"Anatomy runtime node '{node.NodeId}' is missing parent '{node.ParentNodeId}'.";
                }

                if (!string.IsNullOrWhiteSpace(node.RegionNodeId) && (!nodes.TryGetValue(node.RegionNodeId, out AnatomyRuntimeNode region) || region.Category != AnatomyStructuralCategory.Region))
                {
                    return $"Anatomy runtime node '{node.NodeId}' references invalid region '{node.RegionNodeId}'.";
                }
            }

            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Traverse(root, nodes, visited);
            return visited.Count == nodes.Count
                ? string.Empty
                : "Anatomy runtime contains orphan nodes.";
        }

        private static void Traverse(string nodeId, IReadOnlyDictionary<string, AnatomyRuntimeNode> nodes, HashSet<string> visited)
        {
            if (!visited.Add(nodeId) || !nodes.TryGetValue(nodeId, out AnatomyRuntimeNode node))
            {
                return;
            }

            foreach (string childId in node.ChildNodeIds)
            {
                Traverse(childId, nodes, visited);
            }
        }
    }

    public sealed class AnatomyRuntimeNode
    {
        private readonly List<string> childNodeIds = new List<string>();

        public AnatomyRuntimeNode(string actorBodyId, string anatomyDefinitionId, AnatomyNodeDefinition definition, AnatomyPresenceState presence)
        {
            NodeId = definition.NodeId;
            RuntimeNodeId = $"anatomy-node.{actorBodyId}.{anatomyDefinitionId}.{definition.NodeId}";
            DisplayName = definition.DisplayName;
            Category = definition.Category;
            ParentNodeId = definition.ParentNodeId;
            RegionNodeId = definition.RegionNodeId;
            BodySide = definition.BodySide;
            Presence = presence;
            InternalStructure = definition.InternalStructure;
            Corporeal = definition.Corporeal;
            Targetable = definition.Targetable;
            Vital = definition.Vital;
            Optional = definition.Optional;
            TargetingWeight = definition.RelativeTargetingWeight;
            RepeatGroup = definition.RepeatGroup;
            MirrorGroup = definition.MirrorGroup;
            Order = definition.Order;
            EquipmentTagIds = definition.EquipmentTagIds.ToArray();
            DamageTagIds = definition.DamageTagIds.ToArray();
        }

        public string NodeId { get; }
        public string RuntimeNodeId { get; }
        public string DisplayName { get; }
        public AnatomyStructuralCategory Category { get; }
        public string ParentNodeId { get; }
        public string RegionNodeId { get; }
        public AnatomyBodySide BodySide { get; }
        public AnatomyPresenceState Presence { get; private set; }
        public bool InternalStructure { get; }
        public bool Corporeal { get; }
        public bool Targetable { get; }
        public bool Vital { get; }
        public bool Optional { get; }
        public float TargetingWeight { get; }
        public string RepeatGroup { get; }
        public string MirrorGroup { get; }
        public int Order { get; }
        public IReadOnlyList<string> ChildNodeIds => childNodeIds;
        public IReadOnlyList<string> EquipmentTagIds { get; }
        public IReadOnlyList<string> DamageTagIds { get; }

        public void AddChild(string nodeId)
        {
            if (!string.IsNullOrWhiteSpace(nodeId) && !childNodeIds.Contains(nodeId, StringComparer.Ordinal))
            {
                childNodeIds.Add(nodeId);
                childNodeIds.Sort(StringComparer.Ordinal);
            }
        }

        public void SetPresence(AnatomyPresenceState presence)
        {
            Presence = presence;
        }

        public AnatomyNodeSnapshot CreateSnapshot()
        {
            return new AnatomyNodeSnapshot(
                NodeId,
                RuntimeNodeId,
                DisplayName,
                Category,
                ParentNodeId,
                RegionNodeId,
                BodySide,
                Presence,
                InternalStructure,
                Corporeal,
                Targetable,
                Vital,
                Optional,
                TargetingWeight,
                RepeatGroup,
                MirrorGroup,
                ChildNodeIds,
                EquipmentTagIds,
                DamageTagIds);
        }
    }
}
