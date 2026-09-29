using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Anatomy
{
    public sealed class AnatomyNodeSnapshot
    {
        public AnatomyNodeSnapshot(
            string nodeId,
            string runtimeNodeId,
            string displayName,
            AnatomyStructuralCategory category,
            string parentNodeId,
            string regionNodeId,
            AnatomyBodySide bodySide,
            AnatomyPresenceState presence,
            bool internalStructure,
            bool corporeal,
            bool targetable,
            bool vital,
            bool optional,
            float targetingWeight,
            string repeatGroup,
            string mirrorGroup,
            IReadOnlyList<string> childNodeIds,
            IReadOnlyList<string> equipmentTagIds,
            IReadOnlyList<string> damageTagIds)
        {
            NodeId = nodeId ?? string.Empty;
            RuntimeNodeId = runtimeNodeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            ParentNodeId = parentNodeId ?? string.Empty;
            RegionNodeId = regionNodeId ?? string.Empty;
            BodySide = bodySide;
            Presence = presence;
            InternalStructure = internalStructure;
            Corporeal = corporeal;
            Targetable = targetable;
            Vital = vital;
            Optional = optional;
            TargetingWeight = targetingWeight;
            RepeatGroup = repeatGroup ?? string.Empty;
            MirrorGroup = mirrorGroup ?? string.Empty;
            ChildNodeIds = childNodeIds == null ? Array.Empty<string>() : childNodeIds.ToArray();
            EquipmentTagIds = equipmentTagIds == null ? Array.Empty<string>() : equipmentTagIds.ToArray();
            DamageTagIds = damageTagIds == null ? Array.Empty<string>() : damageTagIds.ToArray();
        }

        public string NodeId { get; }
        public string RuntimeNodeId { get; }
        public string DisplayName { get; }
        public AnatomyStructuralCategory Category { get; }
        public string ParentNodeId { get; }
        public string RegionNodeId { get; }
        public AnatomyBodySide BodySide { get; }
        public AnatomyPresenceState Presence { get; }
        public bool InternalStructure { get; }
        public bool Corporeal { get; }
        public bool Targetable { get; }
        public bool Vital { get; }
        public bool Optional { get; }
        public float TargetingWeight { get; }
        public string RepeatGroup { get; }
        public string MirrorGroup { get; }
        public IReadOnlyList<string> ChildNodeIds { get; }
        public IReadOnlyList<string> EquipmentTagIds { get; }
        public IReadOnlyList<string> DamageTagIds { get; }
        public bool Present => Presence == AnatomyPresenceState.Present || Presence == AnatomyPresenceState.Optional;
    }

    public sealed class AnatomySnapshot
    {
        public AnatomySnapshot(
            string actorBodyId,
            string anatomyDefinitionId,
            AnatomyReadinessState readiness,
            long bodyRevision,
            long anatomyRevision,
            string rootNodeId,
            IReadOnlyList<AnatomyNodeSnapshot> nodes,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            AnatomyDefinitionId = anatomyDefinitionId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            RootNodeId = rootNodeId ?? string.Empty;
            Nodes = nodes == null ? Array.Empty<AnatomyNodeSnapshot>() : nodes.ToArray();
            Regions = Nodes.Where(node => node.Category == AnatomyStructuralCategory.Region).ToArray();
            BodyParts = Nodes.Where(node => node.Category == AnatomyStructuralCategory.Part
                || node.Category == AnatomyStructuralCategory.Limb
                || node.Category == AnatomyStructuralCategory.Appendage
                || node.Category == AnatomyStructuralCategory.Surface).ToArray();
            OrgansAndInternalStructures = Nodes.Where(node => node.Category == AnatomyStructuralCategory.Organ
                || node.Category == AnatomyStructuralCategory.InternalStructure
                || node.Category == AnatomyStructuralCategory.Core
                || node.Category == AnatomyStructuralCategory.Essence
                || node.Category == AnatomyStructuralCategory.DistributedStructure).ToArray();
            VitalStructures = Nodes.Where(node => node.Vital && node.Present).ToArray();
            TargetableRegions = Regions.Where(node => node.Targetable && node.Present).ToArray();
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public string AnatomyDefinitionId { get; }
        public AnatomyReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public string RootNodeId { get; }
        public IReadOnlyList<AnatomyNodeSnapshot> Nodes { get; }
        public IReadOnlyList<AnatomyNodeSnapshot> Regions { get; }
        public IReadOnlyList<AnatomyNodeSnapshot> BodyParts { get; }
        public IReadOnlyList<AnatomyNodeSnapshot> OrgansAndInternalStructures { get; }
        public IReadOnlyList<AnatomyNodeSnapshot> VitalStructures { get; }
        public IReadOnlyList<AnatomyNodeSnapshot> TargetableRegions { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
