using System;

namespace UnityIsekaiGame.Laws
{
    public enum LawScopeKind
    {
        Unknown = 0,
        Worldwide = 1,
        Government = 2,
        Territory = 3,
        Place = 4,
        Property = 5,
        Organization = 6,
        Custom = 100
    }

    public enum LegalAuthorityCategory { Unknown, SovereignLegislative, CentralGovernmentRulemaking, RegionalRulemaking, MunicipalRulemaking, ExecutiveRegulation, EmergencyRulemaking, AdministrativeRulemaking, MilitaryInternalLaw, ReligiousInternalLaw, OrganizationInternalLegalCode, TreatyImplementation, Custom = 100 }
    public enum LegalInstrumentCategory { Unknown, Constitution, Charter, Statute, Decree, Ordinance, Regulation, AdministrativeRule, EmergencyOrder, MilitaryCode, ReligiousInternalRule, OrganizationInternalLegalRule, TreatyImplementationAct, JudicialPrecedent, Custom = 100 }
    public enum LegalInstrumentLifecycleState { Unknown, Draft, Enacted, Scheduled, Active, PartiallySuspended, Suspended, Expired, Repealed, Superseded, Invalidated, Historical, Invalid }
    public enum LegalEffectCategory { Unknown, Right, Permission, Duty, Prohibition, Exemption, Immunity, Eligibility, StatusGrant, StatusRestriction, PropertyRestriction, ContractCapacity, Custom = 100 }
    public enum LegalProvisionLifecycleState { Unknown, Scheduled, Active, Suspended, Repealed, Superseded, Expired, Historical }
    public enum LegalConditionKind { Unknown, Person, Organization, Territory, Place, Property, Office, Profession, Item, Activity, LegalStatus, Membership, Custom = 100 }
    public enum LegalConflictPolicy { Unknown, HigherPrecedenceWins, SpecificOverridesGeneral, LaterInstrumentWins, Shared, Unresolved }
    public enum LegalApplicabilityStatus { Unknown, NoApplicableLaw, Permitted, Required, Prohibited, Exempt, Immune, Applicable, Conflict, AccessDenied, InvalidRequest }
    public enum LegalStatusCategory { Unknown, Citizen, Subject, National, PermanentResident, TemporaryResident, ProtectedPerson, StatelessPerson, ForeignVisitor, Custom = 100 }
    public enum LegalStatusLifecycleState { Unknown, Proposed, Active, Suspended, Disputed, Renounced, Revoked, Lost, Restored, Superseded, Historical }
    public enum CitizenshipAcquisitionRoute { Unknown, Birth, Grant, Naturalization, Succession, Adoption, Marriage, ExplicitScript, Restoration, Custom = 100 }
    public enum LegalEntitlementLifecycleState { Unknown, Proposed, Active, Suspended, Expired, Revoked, Superseded, Historical }
    public enum LegalOperationCode { Unknown, Succeeded, Preview, Duplicate, InvalidRequest, MissingDefinition, MissingInstrument, MissingProvision, MissingGovernment, MissingJurisdiction, MissingPerson, MissingStatus, MissingAuthority, MissingResolution, InvalidReference, InvalidState, Conflict, ValidationFailed, Disposed, AccessDenied }
    public enum LegalTransitionKind { Unknown, Amendment, Suspension, Repeal, Supersession, EmergencyExpiration, GovernmentSuccession, TerritorialTransfer, PolityDissolution, CitizenshipTransition, OccupationTransition, TreatyImplementation }
}
