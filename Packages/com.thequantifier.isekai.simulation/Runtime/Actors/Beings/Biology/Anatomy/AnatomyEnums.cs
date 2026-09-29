namespace UnityIsekaiGame.Beings.Biology.Anatomy
{
    public enum AnatomyReadinessState
    {
        Uninitialized,
        ResolvingDefinition,
        BuildingHierarchy,
        ValidatingStructure,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum AnatomyStructuralCategory
    {
        Unknown,
        Region,
        Part,
        Limb,
        Appendage,
        Organ,
        Core,
        Surface,
        InternalStructure,
        Essence,
        DistributedStructure
    }

    public enum AnatomyBodySide
    {
        None,
        Center,
        Left,
        Right,
        Front,
        Rear,
        Upper,
        Lower,
        Dorsal,
        Ventral,
        Unknown
    }

    public enum AnatomyPresenceState
    {
        Present,
        Absent,
        Optional,
        Suppressed,
        Inactive,
        Unknown
    }
}
