namespace UnityIsekaiGame.Traits
{
    public enum TraitPolarity
    {
        Positive,
        Negative,
        Mixed,
        Neutral
    }

    public enum TraitPermanenceClass
    {
        Innate,
        AcquiredPermanent,
        Removable,
        Transformational,
        Narrative
    }

    public enum TraitLifecycleState
    {
        Dormant,
        Active,
        Suppressed,
        Removed,
        Historical
    }

    public enum TraitDiscoveryState
    {
        Undiscovered,
        Suspected,
        Discovered
    }

    public enum TraitVisibility
    {
        Public,
        Known,
        Hidden,
        Secret
    }

    public enum TraitSourceCategory
    {
        Origin,
        BirthGift,
        Species,
        BiologicalClassification,
        Lineage,
        Quest,
        Achievement,
        Transformation,
        Curse,
        Blessing,
        InjuryOutcome,
        Item,
        Role,
        SocialStatus,
        Trait,
        Administrative,
        Migration,
        Development
    }

    public enum TraitFinalSourcePolicy
    {
        Remove,
        KeepDormant,
        KeepHistorical
    }
}
