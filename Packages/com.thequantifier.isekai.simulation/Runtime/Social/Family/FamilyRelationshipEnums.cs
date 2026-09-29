namespace UnityIsekaiGame.Social.Family
{
    public enum ParentageKind
    {
        Unknown = 0,
        Biological = 1,
        Adoptive = 2,
        Legal = 3,
        Foster = 4,
        SocialOrRecognized = 5,
        Custom = 100
    }

    public enum ParentageEvidenceStatus
    {
        Unknown = 0,
        Confirmed = 1,
        LegallyRecognized = 2,
        Disputed = 3,
        Alleged = 4,
        Hidden = 5
    }

    public enum FamilyVisibility
    {
        Public = 0,
        FamilyKnown = 1,
        ParticipantKnown = 2,
        Confidential = 3,
        Secret = 4,
        Hidden = 5,
        Diagnostic = 6
    }

    public enum KinshipLineageKind
    {
        None = 0,
        Biological = 1,
        Adoptive = 2,
        Legal = 3,
        Foster = 4,
        Step = 5,
        Mixed = 6,
        Custom = 100
    }

    public enum KinshipClassification
    {
        Parent = 0,
        Child = 1,
        BiologicalParent = 2,
        BiologicalChild = 3,
        AdoptiveParent = 4,
        AdoptiveChild = 5,
        Guardian = 6,
        Dependent = 7,
        FullSibling = 8,
        HalfSibling = 9,
        AdoptiveSibling = 10,
        StepSibling = 11,
        Grandparent = 12,
        Grandchild = 13,
        Ancestor = 14,
        Descendant = 15,
        AuntOrUncle = 16,
        NieceOrNephew = 17,
        FirstCousin = 18,
        MoreDistantCousin = 19,
        Spouse = 20,
        Partner = 21,
        FormerPartner = 22,
        ParentInLaw = 23,
        ChildInLaw = 24,
        SiblingInLaw = 25,
        Unrelated = 90,
        Indeterminate = 91,
        Truncated = 92
    }

    public enum RomanticRelationshipState
    {
        None = 0,
        Courtship = 1,
        Engagement = 2,
        Partnership = 3,
        Marriage = 4,
        Separation = 5,
        FormerPartnership = 6
    }

    public enum RomanticTransitionKind
    {
        ProposeCourtship = 0,
        AcceptCourtship = 1,
        RejectCourtship = 2,
        EndCourtship = 3,
        ProposeEngagement = 4,
        AcceptEngagement = 5,
        RejectEngagement = 6,
        EstablishPartnership = 7,
        EstablishMarriage = 8,
        RequestSeparation = 9,
        ConfirmSeparation = 10,
        EndPartnership = 11,
        Reconcile = 12,
        RecordWidowhood = 13
    }

    public enum RomanticConsentKind
    {
        None = 0,
        ExplicitAcceptedInteraction = 1,
        PlayerChoice = 2,
        ScriptedAuthority = 3,
        RejectedInteraction = 4,
        Compliance = 5,
        Influence = 6,
        InferredFromAttraction = 7
    }

    public enum RomanticEligibilityStatus
    {
        Eligible = 0,
        Preview = 1,
        Ineligible = 2,
        MissingPolicy = 3,
        MissingRuntime = 4,
        MissingParticipant = 5,
        UnknownPerson = 6,
        NonAdult = 7,
        UnresolvedLifeStage = 8,
        ProhibitedKinship = 9,
        GuardianDependent = 10,
        ExistingExclusivePartnership = 11,
        MissingConsent = 12,
        InvalidConsent = 13,
        InvalidRequest = 14,
        Duplicate = 15,
        RestoreFailed = 20
    }

    public enum HouseholdRole
    {
        Head = 0,
        CoHead = 1,
        AdultMember = 2,
        Dependent = 3,
        Guardian = 4,
        ChildMember = 5,
        Guest = 6,
        Servant = 7,
        Ward = 8,
        Custom = 100
    }

    public enum HouseholdLifecycleStatus
    {
        Active = 0,
        Dissolved = 1,
        Merged = 2,
        Split = 3
    }

    public enum HouseholdMembershipStatus
    {
        Active = 0,
        Ended = 1
    }

    public enum HouseholdOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingRuntime = 11,
        MissingDefinition = 12,
        MissingHousehold = 13,
        DuplicateHousehold = 14,
        DuplicateMembership = 15,
        DuplicateActiveMembership = 16,
        MissingParticipant = 17,
        UnknownPerson = 18,
        InvalidRole = 19,
        InvalidLifecycle = 20,
        CrossWorldReference = 21,
        RestoreFailed = 30,
        ValidationFailed = 31
    }
}
