namespace UnityIsekaiGame.Justice
{
    public enum JusticeInstitutionCategory { Unknown, GeneralCourt, MunicipalCourt, RegionalCourt, CentralCourt, AppellateCourt, SupremeCourt, MilitaryTribunal, ReligiousTribunal, OrganizationDisciplinaryTribunal, AdministrativeTribunal, EmergencyTribunal, Custom = 100 }
    public enum JusticeCourtLifecycleState { Unknown, Proposed, Active, Suspended, EmergencyActive, InExile, OccupationTribunal, Dissolved, Superseded, Historical, Invalid }
    public enum JusticeCaseCategory { Unknown, Criminal, MilitaryDiscipline, ReligiousInternal, OrganizationDiscipline, Administrative, CivilRemedy, Custom = 100 }
    public enum JusticeDecisionProcedure { Unknown, Magistrate, Judge, Panel, Tribunal, AdministrativeOfficer, MilitaryPanel, Custom = 100 }
    public enum StandardOfProofCategory { Unknown, ReasonableSuspicion, ProbableCause, Preponderance, ClearAndConvincing, BeyondReasonableDoubt, InternalDiscipline, Custom = 100 }
    public enum ArrestCategory { Unknown, WarrantBasedArrest, VoluntarySurrender, CaughtInActArrest, EmergencyArrest, MilitaryApprehension, OrganizationInternalApprehension, CourtOrderedCustody, SentenceExecutionArrest, ProtectiveCustody, Custom = 100 }
    public enum ArrestLegalBasisKind { Unknown, ActiveArrestWarrant, VoluntarySurrender, CaughtInAct, ImmediateDanger, CourtOrder, SentenceExecution, MilitaryInternalAuthority, OrganizationInternalAuthority, Custom = 100 }
    public enum ArrestLifecycleState { Unknown, Previewed, Attempted, Completed, Failed, Superseded, Historical, Invalid }
    public enum CustodyCategory { Unknown, ArrestCustody, VoluntarySurrenderCustody, RemandDetention, SentenceImprisonment, TransferCustody, ProtectiveCustody, MilitaryCustody, OrganizationInternalCustody, Custom = 100 }
    public enum CustodyLifecycleState { Unknown, Pending, Active, Transferred, Released, Expired, Superseded, Historical, Invalid }
    public enum ReleaseCategory { Unknown, Unconditional, PendingTrial, Bail, DetentionExpired, SentenceComplete, CourtOrdered, AppealStay, Clemency, WrongfulJudgmentCorrection, Custom = 100 }
    public enum ChargeCategory { Unknown, CriminalCharge, MilitaryCharge, DisciplinaryCharge, ReligiousInternalCharge, AdministrativeViolation, Custom = 100 }
    public enum ChargeLifecycleState { Unknown, Proposed, Filed, Amended, Withdrawn, Dismissed, Superseded, Adjudicated, Historical, Invalid }
    public enum CourtCaseLifecycleState { Unknown, Filed, JurisdictionReview, Active, Stayed, Transferred, Consolidated, Severed, JudgmentEntered, Sentencing, Appealed, Closed, Historical, Invalid }
    public enum CasePartyRole { Unknown, Defendant, Prosecutor, Judge, PanelMember, DefenseRepresentative, Victim, Witness, Clerk, Plaintiff, Respondent, Custom = 100 }
    public enum PleaCategory { Unknown, NoResponse, NotGuilty, Guilty, NoContest, AdmitConductDisputeClassification, Conditional, Custom = 100 }
    public enum HearingCategory { Unknown, InitialAppearance, DetentionReview, JurisdictionReview, EvidenceHearing, Trial, Sentencing, Appeal, Remand, Custom = 100 }
    public enum HearingLifecycleState { Unknown, Scheduled, Opened, Continued, Completed, Cancelled, Historical, Invalid }
    public enum EvidenceRulingState { Unknown, Submitted, Admitted, Excluded, Limited, Disputed, Reserved, Withdrawn, Invalid }
    public enum ProceduralRulingCategory { Unknown, JurisdictionAccepted, JurisdictionRejected, TransferOrdered, ContinuanceGranted, EvidenceRuling, DetentionReview, ReleaseOrder, StayOrder, Custom = 100 }
    public enum FindingCategory { Unknown, Fact, Law, Mixed, Custom = 100 }
    public enum JudgmentOutcome { Unknown, NotResponsible, NotProven, Guilty, Liable, Dismissed, Acquitted, Vacated, Modified, Custom = 100 }
    public enum JudgmentLifecycleState { Unknown, Draft, Entered, Final, Stayed, Appealed, Modified, Reversed, Vacated, Historical, Invalid }
    public enum SentenceCategory { Unknown, Fine, Restitution, Imprisonment, Probation, Service, PermitConsequence, OfficeConsequence, MembershipConsequence, LegalStatusConsequence, Suspended, Deferred, Capital, Custom = 100 }
    public enum SentenceLifecycleState { Unknown, Imposed, Active, Stayed, Suspended, Deferred, PartiallyCompleted, Completed, Commuted, Remitted, Vacated, Historical, Invalid }
    public enum SentenceComponentState { Unknown, Pending, Active, Stayed, Completed, Suspended, Cancelled, Invalid }
    public enum RemedyCategory { Unknown, PropertyReturn, Release, StatusCorrection, PublicCorrectionNotice, DeclaratoryRuling, Restitution, Refund, Custom = 100 }
    public enum RemedyLifecycleState { Unknown, Ordered, Active, Completed, Failed, Superseded, Vacated, Historical, Invalid }
    public enum AppealCategory { Unknown, JudgmentAppeal, SentenceAppeal, ProceduralAppeal, Interlocutory, Custom = 100 }
    public enum AppealLifecycleState { Unknown, Filed, Accepted, Rejected, Pending, Decided, Withdrawn, LateRejected, Historical, Invalid }
    public enum AppealOutcome { Unknown, Affirmed, Reversed, Modified, Vacated, Remanded, Dismissed, Custom = 100 }
    public enum ClemencyCategory { Unknown, Pardon, Commutation, Remission, Reprieve, ConditionalClemency, Custom = 100 }
    public enum ClemencyLifecycleState { Unknown, Requested, Granted, Denied, Revoked, Completed, Historical, Invalid }
    public enum JusticeVisibilityDecision { Unknown, FullAccess, RedactedAccess, Denied, Concealed }
    public enum JusticeOperationCode { Unknown, Succeeded, Preview, Duplicate, InvalidRequest, InvalidDefinition, MissingDefinition, MissingRuntime, MissingIncident, MissingPotentialOffense, MissingWarrant, MissingCourt, MissingArrest, MissingCustody, MissingCharge, MissingCase, MissingHearing, MissingJudgment, MissingSentence, MissingAppeal, MissingJurisdiction, MissingAuthority, MissingLegalBasis, MissingEvidence, MissingPerson, InvalidReference, InvalidState, ThresholdNotMet, Expired, ImmunityBlocked, JurisdictionDenied, ValidationFailed, Disposed, AccessDenied }
}
