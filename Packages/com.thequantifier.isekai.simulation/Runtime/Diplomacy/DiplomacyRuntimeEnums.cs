using System;

namespace UnityIsekaiGame.Diplomacy
{
    public enum DiplomaticActorKind
    {
        Unknown = 0,
        Organization = 1,
        Faction = 2,
        Government = 3,
        Polity = 4,
        Coalition = 5
    }

    public enum DiplomaticOperationKind
    {
        Unknown = 0,
        RecognizeActor = 1,
        CreateRelation = 2,
        TransitionRelation = 3,
        DraftAgreement = 4,
        SignAgreement = 5,
        RatifyAgreement = 6,
        ActivateAgreement = 7,
        SuspendAgreement = 8,
        TerminateAgreement = 9,
        RecordBreach = 10,
        DeclareWar = 11,
        AddWarParticipant = 12,
        TransitionWar = 13,
        RecordIncident = 14
    }

    public enum DiplomaticOperationCode
    {
        Unknown = 0,
        Succeeded = 1,
        Preview = 2,
        Duplicate = 3,
        InvalidRequest = 4,
        MissingDefinition = 5,
        MissingActor = 6,
        ActorIneligible = 7,
        MissingRelation = 8,
        MissingAgreement = 9,
        MissingClause = 10,
        MissingParty = 11,
        MissingWar = 12,
        InvalidState = 13,
        InvalidAuthority = 14,
        ValidationFailed = 15,
        RestoreFailed = 16
    }

    public enum DiplomaticRelationCategory
    {
        Unknown = 0,
        Unrecognized = 1,
        Recognized = 2,
        Neutral = 3,
        Cooperative = 4,
        Allied = 5,
        Rival = 6,
        Hostile = 7,
        Ceasefire = 8,
        AtWar = 9,
        Custom = 10
    }

    public enum DiplomaticReciprocityPolicy
    {
        Directional = 0,
        ReciprocalRequired = 1,
        MirrorOnCreate = 2
    }

    public enum DiplomaticLifecycleState
    {
        Unknown = 0,
        Draft = 1,
        Proposed = 2,
        PendingRecognition = 3,
        Active = 4,
        Suspended = 5,
        Ended = 6,
        Superseded = 7,
        Historical = 8
    }

    public enum DiplomaticVisibility
    {
        Public = 0,
        Restricted = 1,
        Confidential = 2,
        Secret = 3,
        Hidden = 4,
        DevelopmentOnly = 5
    }

    public enum DiplomaticAgreementCategory
    {
        Unknown = 0,
        Recognition = 1,
        Cooperation = 2,
        NonAggression = 3,
        Alliance = 4,
        MutualDefense = 5,
        TradeOrResource = 6,
        InformationSharing = 7,
        Ceasefire = 8,
        Peace = 9,
        SecretProtocol = 10,
        Custom = 11
    }

    public enum DiplomaticAgreementLifecycleState
    {
        Unknown = 0,
        Draft = 1,
        Negotiating = 2,
        Signed = 3,
        Ratified = 4,
        Active = 5,
        Suspended = 6,
        Withdrawn = 7,
        Terminated = 8,
        Expired = 9,
        Superseded = 10,
        Historical = 11
    }

    public enum DiplomaticPartyRole
    {
        Unknown = 0,
        Principal = 1,
        Guarantor = 2,
        Observer = 3,
        Mediator = 4,
        Belligerent = 5,
        Ally = 6
    }

    public enum DiplomaticSignatureStatus
    {
        Unknown = 0,
        Proposed = 1,
        Signed = 2,
        Withdrawn = 3,
        Rejected = 4,
        Superseded = 5
    }

    public enum DiplomaticRatificationStatus
    {
        Unknown = 0,
        Pending = 1,
        Ratified = 2,
        Rejected = 3,
        Expired = 4,
        Waived = 5
    }

    public enum DiplomaticClauseCategory
    {
        Unknown = 0,
        Recognition = 1,
        NonAggression = 2,
        DefenseAssistance = 3,
        TradeOrResource = 4,
        InformationSharing = 5,
        ConductRestriction = 6,
        Withdrawal = 7,
        Ceasefire = 8,
        Peace = 9,
        Custom = 10
    }

    public enum DiplomaticClauseLifecycleState
    {
        Unknown = 0,
        Draft = 1,
        Proposed = 2,
        Active = 3,
        Suspended = 4,
        Fulfilled = 5,
        Breached = 6,
        Waived = 7,
        Terminated = 8,
        Expired = 9
    }

    public enum DiplomaticClauseParameterType
    {
        Unknown = 0,
        Boolean = 1,
        Integer = 2,
        Decimal = 3,
        StableId = 4,
        Text = 5,
        ActorReference = 6
    }

    public enum DiplomaticBreachState
    {
        Unknown = 0,
        Alleged = 1,
        Disputed = 2,
        Confirmed = 3,
        Waived = 4,
        Cured = 5,
        Historical = 6
    }

    public enum DiplomaticAllianceKind
    {
        None = 0,
        InformalUnderstanding = 1,
        DefensiveAlliance = 2,
        MutualDefense = 3,
        OffensiveAlliance = 4,
        Coalition = 5
    }

    public enum DiplomaticWarCategory
    {
        Unknown = 0,
        FormalWar = 1,
        LimitedWar = 2,
        FactionalConflict = 3,
        Reprisal = 4,
        Custom = 5
    }

    public enum DiplomaticWarLifecycleState
    {
        Unknown = 0,
        Threatened = 1,
        Declared = 2,
        Active = 3,
        Ceasefire = 4,
        Armistice = 5,
        PeaceNegotiation = 6,
        Ended = 7,
        Historical = 8
    }

    public enum DiplomaticWarParticipantStatus
    {
        Unknown = 0,
        Belligerent = 1,
        Ally = 2,
        Supporter = 3,
        Neutral = 4,
        Withdrawn = 5
    }

    public enum DiplomaticIncidentCategory
    {
        Unknown = 0,
        BorderIncident = 1,
        DiplomaticInsult = 2,
        TreatyViolation = 3,
        AttackReported = 4,
        MediationAttempt = 5,
        PeaceOffer = 6,
        Custom = 7
    }

    public enum DiplomaticProjectionAccess
    {
        Denied = 0,
        Concealed = 1,
        Redacted = 2,
        Full = 3,
        Privileged = 4
    }
}
