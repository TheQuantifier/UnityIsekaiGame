using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Anatomy
{
    [Serializable]
    public sealed class AnatomyNodeDefinition
    {
        [SerializeField] private string nodeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private AnatomyStructuralCategory category;
        [SerializeField] private string parentNodeId;
        [SerializeField] private string regionNodeId;
        [SerializeField] private AnatomyBodySide bodySide;
        [SerializeField] private bool internalStructure;
        [SerializeField] private bool corporeal = true;
        [SerializeField] private bool targetable = true;
        [SerializeField] private bool vital;
        [SerializeField] private bool optional;
        [SerializeField] private AnatomyPresenceState defaultPresence = AnatomyPresenceState.Present;
        [SerializeField] private float relativeTargetingWeight = 1f;
        [SerializeField] private string repeatGroup;
        [SerializeField] private string mirrorGroup;
        [SerializeField] private string[] equipmentTagIds;
        [SerializeField] private string[] damageTagIds;
        [SerializeField] private string[] biologicalFunctionTags;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private int order;

        public string NodeId => nodeId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? NodeId : displayName;
        public string Description => description ?? string.Empty;
        public AnatomyStructuralCategory Category => category;
        public string ParentNodeId => parentNodeId ?? string.Empty;
        public string RegionNodeId => regionNodeId ?? string.Empty;
        public AnatomyBodySide BodySide => bodySide;
        public bool InternalStructure => internalStructure;
        public bool Corporeal => corporeal;
        public bool Targetable => targetable;
        public bool Vital => vital;
        public bool Optional => optional;
        public AnatomyPresenceState DefaultPresence => defaultPresence;
        public float RelativeTargetingWeight => relativeTargetingWeight;
        public string RepeatGroup => repeatGroup ?? string.Empty;
        public string MirrorGroup => mirrorGroup ?? string.Empty;
        public IReadOnlyList<string> EquipmentTagIds => equipmentTagIds ?? Array.Empty<string>();
        public IReadOnlyList<string> DamageTagIds => damageTagIds ?? Array.Empty<string>();
        public IReadOnlyList<string> BiologicalFunctionTags => biologicalFunctionTags ?? Array.Empty<string>();
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public int Order => order;
    }
}
