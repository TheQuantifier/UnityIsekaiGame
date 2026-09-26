namespace UnityIsekaiGame.Crimes
{
    public enum OffenseCategory { Unknown, ViolenceAgainstPerson, ThreatOrCoercion, PropertyDamage, TheftOrUnlawfulTaking, TrespassOrUnauthorizedEntry, FraudOrDeception, ContractOrFinancialViolation, MisuseOfOffice, ConfidentialityOrInformationViolation, PublicOrder, MilitaryInternalViolation, ReligiousInternalViolation, RegulatoryViolation, LegalStatusViolation, WarOrDiplomaticViolation, Attempt, AssistanceOrParticipation, Custom = 100 }
    public enum OffenseSeverityCategory { Unknown, Minor, Moderate, Serious, Grave, Capital, InternalDiscipline, Regulatory, Custom = 100 }
    public enum OffenseElementKind { Unknown, ActorConduct, ActorStatus, TargetStatus, TargetType, HarmOccurred, ItemPossessionChanged, PropertyDamaged, LegalDutyExisted, LegalPermissionAbsent, ConsentAbsent, JurisdictionApplied, MentalState, Circumstance, ResultOccurred, Custom = 100 }
    public enum CrimeMentalState { Unknown, NotRequired, Intentional, Knowing, Reckless, Negligent, StrictLiability }
    public enum OffenseStage { Unknown, Planned, Attempted, Interrupted, Completed, Continuing }
    public enum ParticipationCategory { Unknown, PrincipalActor, CoActor, Assistant, Organizer, Instigator, Accessory, Beneficiary, UnknownParticipant, Custom = 100 }
    public enum PotentialOffenseStatus { Unknown, Unreviewed, Plausible, ElementsPartiallySupported, ElementsSupported, ElementsContradicted, LegallyExcluded, Exempt, ImmunityRelevant, OutsideJurisdiction, TimeBarred, InsufficientInformation, Rejected, Superseded, Historical, Invalid }
    public enum CrimeIncidentCategory { Unknown, ReportedConduct, OfficiallyObservedConduct, DiscoveredHarm, MissingProperty, ViolentIncident, PropertyIncident, FinancialIncident, InformationIncident, OfficeMisconduct, InternalOrganizationViolation, UnknownConduct, Custom = 100 }
    public enum CrimeIncidentLifecycleState { Unknown, Recorded, AwaitingReview, UnderReview, ActiveInvestigation, Inactive, Suspended, Merged, Reopened, ClosedUnresolved, ClosedNoViolation, ReferredForLegalAction, Historical, Invalid }
    public enum CrimeReportCategory { Unknown, VictimReport, WitnessReport, OfficialReport, OrganizationReport, AnonymousReport, RumorBasedReport, DelayedReport, Custom = 100 }
    public enum CrimeReportLifecycleState { Unknown, Submitted, Accepted, UnderReview, Verified, Incomplete, Mistaken, FalseReport, MaliciousFalsehood, Withdrawn, Merged, Rejected, Closed, Historical, Invalid }
    public enum AllegationLifecycleState { Unknown, Recorded, Supported, Contradicted, Disputed, Withdrawn, Rejected, Superseded, Historical, Invalid }
    public enum SuspectLifecycleState { Unknown, Suspected, UnderReview, Cleared, Misidentified, NoLongerSought, Historical, Invalid }
    public enum EvidenceRelevance { Unknown, Supports, Contradicts, Neutral, ContextOnly, Exculpatory, ReliabilityConcern }
    public enum EvidenceSufficiencyState { Unknown, None, Weak, Partial, Substantial, ThresholdMet, Contradicted, Disputed }
    public enum WarrantCategory { Unknown, Arrest, Search, Seizure, Questioning, Location, InternalOrganizationProcess, MilitaryApprehension, Custom = 100 }
    public enum WarrantRequestLifecycleState { Unknown, Requested, UnderReview, Approved, Denied, Withdrawn, Superseded, Historical, Invalid }
    public enum WarrantLifecycleState { Unknown, Issued, Active, Suspended, Withdrawn, Quashed, Satisfied, Expired, Superseded, Historical, Invalid }
    public enum WarrantScopeKind { Unknown, Person, Place, Property, Inventory, Item, Record, Territory, Action, Custom = 100 }
    public enum WantedPurposeCategory { Unknown, Arrest, Questioning, Locate, InternalOrganizationProcess, MilitaryApprehension, MissingPerson, Custom = 100 }
    public enum WantedStatusLifecycleState { Unknown, Active, Suspended, Cleared, Expired, Superseded, Erroneous, Historical, Invalid }
    public enum WantedRiskAssessment { Unknown, Low, Nonviolent, PotentiallyArmed, High, ConflictingInformation }
    public enum CrimeOperationCode { Unknown, Succeeded, Preview, Duplicate, InvalidRequest, MissingDefinition, MissingRuntime, MissingIncident, MissingReport, MissingOffense, MissingWarrantRequest, MissingWarrant, MissingWantedStatus, MissingJurisdiction, MissingAuthority, MissingLegalApplicability, InvalidReference, InvalidState, ThresholdNotMet, ValidationFailed, Disposed, AccessDenied }
}
