using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;

namespace UnityIsekaiGame.Justice
{
    public static class PrototypeJusticeDefinitionFactory
    {
        public const string GeneralJusticeInstitutionId = "justice-institution.prototype.general-court-system";
        public const string MilitaryJusticeInstitutionId = "justice-institution.prototype.military-tribunal-system";
        public const string OrganizationDisciplineInstitutionId = "justice-institution.prototype.organization-disciplinary-system";
        public const string GeneralCourtDefinitionId = "court-definition.prototype.general-criminal-court";
        public const string AppellateCourtDefinitionId = "court-definition.prototype.appellate-court";
        public const string MilitaryTribunalDefinitionId = "court-definition.prototype.military-tribunal";
        public const string WarrantArrestDefinitionId = "arrest-definition.prototype.warrant-arrest";
        public const string VoluntarySurrenderDefinitionId = "arrest-definition.prototype.voluntary-surrender";
        public const string CaughtInActArrestDefinitionId = "arrest-definition.prototype.caught-in-act";
        public const string MilitaryApprehensionDefinitionId = "arrest-definition.prototype.military-apprehension";
        public const string CriminalChargeDefinitionId = "charge-definition.prototype.criminal";
        public const string MilitaryChargeDefinitionId = "charge-definition.prototype.military";
        public const string InitialHearingDefinitionId = "hearing-definition.prototype.initial-appearance";
        public const string EvidenceHearingDefinitionId = "hearing-definition.prototype.evidence";
        public const string TrialHearingDefinitionId = "hearing-definition.prototype.trial";
        public const string FineSentenceDefinitionId = "sentence-definition.prototype.fine";
        public const string RestitutionSentenceDefinitionId = "sentence-definition.prototype.restitution";
        public const string ImprisonmentSentenceDefinitionId = "sentence-definition.prototype.imprisonment";
        public const string ProbationSentenceDefinitionId = "sentence-definition.prototype.probation";
        public const string ReleaseRemedyDefinitionId = "remedy-definition.prototype.release";
        public const string PropertyReturnRemedyDefinitionId = "remedy-definition.prototype.property-return";
        public const string JudgmentAppealDefinitionId = "appeal-definition.prototype.judgment";
        public const string SentenceAppealDefinitionId = "appeal-definition.prototype.sentence";
        public const string PardonClemencyDefinitionId = "clemency-definition.prototype.pardon";
        public const string CommutationClemencyDefinitionId = "clemency-definition.prototype.commutation";

        public static DefinitionRegistry AddMissingPrototypeJusticeDefinitions(DefinitionRegistry source)
        {
            List<IGameDefinition> all = source?.DefinitionsById.Values.Where(item => item != null).ToList() ?? new List<IGameDefinition>();
            HashSet<string> ids = new HashSet<string>(all.Select(item => item.Id), StringComparer.Ordinal);
            void Add(IGameDefinition definition) { if (definition != null && ids.Add(definition.Id)) all.Add(definition); }

            Add(Institution(GeneralJusticeInstitutionId, "General Court System", JusticeInstitutionCategory.GeneralCourt, new[] { JurisdictionCategory.GeneralGovernment, JurisdictionCategory.Municipal }, new[] { JusticeCaseCategory.Criminal, JusticeCaseCategory.Administrative }, new[] { JusticeDecisionProcedure.Judge, JusticeDecisionProcedure.Magistrate }, true, true, false));
            Add(Institution(MilitaryJusticeInstitutionId, "Military Tribunal System", JusticeInstitutionCategory.MilitaryTribunal, new[] { JurisdictionCategory.Military }, new[] { JusticeCaseCategory.MilitaryDiscipline }, new[] { JusticeDecisionProcedure.MilitaryPanel }, true, true, false));
            Add(Institution(OrganizationDisciplineInstitutionId, "Organization Disciplinary System", JusticeInstitutionCategory.OrganizationDisciplinaryTribunal, new[] { JurisdictionCategory.Custom }, new[] { JusticeCaseCategory.OrganizationDiscipline }, new[] { JusticeDecisionProcedure.Tribunal }, false, false, false));

            Add(Court(GeneralCourtDefinitionId, "General Criminal Court", JusticeInstitutionCategory.GeneralCourt, new[] { JusticeCaseCategory.Criminal, JusticeCaseCategory.Administrative }, new[] { ChargeCategory.CriminalCharge, ChargeCategory.AdministrativeViolation }, true, false, StandardOfProofCategory.BeyondReasonableDoubt));
            Add(Court(AppellateCourtDefinitionId, "Appellate Court", JusticeInstitutionCategory.AppellateCourt, new[] { JusticeCaseCategory.Criminal, JusticeCaseCategory.MilitaryDiscipline, JusticeCaseCategory.OrganizationDiscipline }, new[] { ChargeCategory.CriminalCharge, ChargeCategory.MilitaryCharge, ChargeCategory.DisciplinaryCharge }, false, true, StandardOfProofCategory.ClearAndConvincing));
            Add(Court(MilitaryTribunalDefinitionId, "Military Tribunal", JusticeInstitutionCategory.MilitaryTribunal, new[] { JusticeCaseCategory.MilitaryDiscipline }, new[] { ChargeCategory.MilitaryCharge }, true, false, StandardOfProofCategory.InternalDiscipline));

            Add(Arrest(WarrantArrestDefinitionId, "Warrant-Based Arrest", ArrestCategory.WarrantBasedArrest, new[] { ArrestLegalBasisKind.ActiveArrestWarrant }, new[] { WarrantCategory.Arrest }, false, true));
            Add(Arrest(VoluntarySurrenderDefinitionId, "Voluntary Surrender", ArrestCategory.VoluntarySurrender, new[] { ArrestLegalBasisKind.VoluntarySurrender, ArrestLegalBasisKind.ActiveArrestWarrant }, new[] { WarrantCategory.Arrest }, true, true));
            Add(Arrest(CaughtInActArrestDefinitionId, "Caught-In-The-Act Arrest", ArrestCategory.CaughtInActArrest, new[] { ArrestLegalBasisKind.CaughtInAct }, Array.Empty<WarrantCategory>(), true, true));
            Add(Arrest(MilitaryApprehensionDefinitionId, "Military Apprehension", ArrestCategory.MilitaryApprehension, new[] { ArrestLegalBasisKind.MilitaryInternalAuthority, ArrestLegalBasisKind.ActiveArrestWarrant }, new[] { WarrantCategory.MilitaryApprehension }, true, true));

            Add(Charge(CriminalChargeDefinitionId, "Criminal Charge", ChargeCategory.CriminalCharge, EvidenceSufficiencyState.Substantial, StandardOfProofCategory.BeyondReasonableDoubt, new[] { OffenseCategory.ViolenceAgainstPerson, OffenseCategory.TheftOrUnlawfulTaking, OffenseCategory.PropertyDamage, OffenseCategory.FraudOrDeception, OffenseCategory.MisuseOfOffice, OffenseCategory.RegulatoryViolation }));
            Add(Charge(MilitaryChargeDefinitionId, "Military Charge", ChargeCategory.MilitaryCharge, EvidenceSufficiencyState.Partial, StandardOfProofCategory.InternalDiscipline, new[] { OffenseCategory.MilitaryInternalViolation }));

            Add(Hearing(InitialHearingDefinitionId, "Initial Appearance", HearingCategory.InitialAppearance, evidence: false, findings: false));
            Add(Hearing(EvidenceHearingDefinitionId, "Evidence Hearing", HearingCategory.EvidenceHearing, evidence: true, findings: false));
            Add(Hearing(TrialHearingDefinitionId, "Trial", HearingCategory.Trial, evidence: true, findings: true));

            Add(Sentence(FineSentenceDefinitionId, "Fine Sentence", SentenceCategory.Fine, true, false, true));
            Add(Sentence(RestitutionSentenceDefinitionId, "Restitution Sentence", SentenceCategory.Restitution, true, false, true));
            Add(Sentence(ImprisonmentSentenceDefinitionId, "Imprisonment Sentence", SentenceCategory.Imprisonment, true, true, false));
            Add(Sentence(ProbationSentenceDefinitionId, "Probation Sentence", SentenceCategory.Probation, true, false, false));

            Add(Remedy(ReleaseRemedyDefinitionId, "Release Remedy", RemedyCategory.Release));
            Add(Remedy(PropertyReturnRemedyDefinitionId, "Property Return Remedy", RemedyCategory.PropertyReturn));
            Add(Appeal(JudgmentAppealDefinitionId, "Judgment Appeal", AppealCategory.JudgmentAppeal, true, true));
            Add(Appeal(SentenceAppealDefinitionId, "Sentence Appeal", AppealCategory.SentenceAppeal, false, true));
            Add(Clemency(PardonClemencyDefinitionId, "Pardon", ClemencyCategory.Pardon));
            Add(Clemency(CommutationClemencyDefinitionId, "Commutation", ClemencyCategory.Commutation));
            return new DefinitionRegistry(all);
        }

        private static JusticeInstitutionDefinition Institution(string id, string name, JusticeInstitutionCategory category, IEnumerable<JurisdictionCategory> jurisdictions, IEnumerable<JusticeCaseCategory> cases, IEnumerable<JusticeDecisionProcedure> procedures, bool custody, bool sentencing, bool appellate)
        {
            JusticeInstitutionDefinition value = ScriptableObject.CreateInstance<JusticeInstitutionDefinition>();
            value.DevelopmentConfigure(id, name, category, jurisdictions, cases, procedures, custody, sentencing, appellate);
            return value;
        }

        private static CourtDefinition Court(string id, string name, JusticeInstitutionCategory category, IEnumerable<JusticeCaseCategory> cases, IEnumerable<ChargeCategory> charges, bool firstInstance, bool appellate, StandardOfProofCategory standard)
        {
            CourtDefinition value = ScriptableObject.CreateInstance<CourtDefinition>();
            value.DevelopmentConfigure(id, name, category, cases, charges, firstInstance, appellate, 1, standard, new[] { JudgmentOutcome.Guilty, JudgmentOutcome.Acquitted, JudgmentOutcome.NotProven, JudgmentOutcome.Dismissed, JudgmentOutcome.Liable }, new[] { SentenceCategory.Fine, SentenceCategory.Restitution, SentenceCategory.Imprisonment, SentenceCategory.Probation });
            return value;
        }

        private static ArrestDefinition Arrest(string id, string name, ArrestCategory category, IEnumerable<ArrestLegalBasisKind> bases, IEnumerable<WarrantCategory> warrants, bool warrantless, bool custody)
        {
            ArrestDefinition value = ScriptableObject.CreateInstance<ArrestDefinition>();
            value.DevelopmentConfigure(id, name, category, bases, warrants, warrantless, custody, 24d);
            return value;
        }

        private static ChargeDefinition Charge(string id, string name, ChargeCategory category, EvidenceSufficiencyState threshold, StandardOfProofCategory standard, IEnumerable<OffenseCategory> offenses)
        {
            ChargeDefinition value = ScriptableObject.CreateInstance<ChargeDefinition>();
            value.DevelopmentConfigure(id, name, category, threshold, standard, offenses);
            return value;
        }

        private static HearingDefinition Hearing(string id, string name, HearingCategory category, bool evidence, bool findings) { HearingDefinition value = ScriptableObject.CreateInstance<HearingDefinition>(); value.DevelopmentConfigure(id, name, category, evidence, findings); return value; }
        private static SentenceDefinition Sentence(string id, string name, SentenceCategory category, bool liability, bool custody, bool economy) { SentenceDefinition value = ScriptableObject.CreateInstance<SentenceDefinition>(); value.DevelopmentConfigure(id, name, category, liability, custody, economy); return value; }
        private static RemedyDefinition Remedy(string id, string name, RemedyCategory category) { RemedyDefinition value = ScriptableObject.CreateInstance<RemedyDefinition>(); value.DevelopmentConfigure(id, name, category); return value; }
        private static AppealDefinition Appeal(string id, string name, AppealCategory category, bool judgmentStay, bool sentenceStay) { AppealDefinition value = ScriptableObject.CreateInstance<AppealDefinition>(); value.DevelopmentConfigure(id, name, category, judgmentStay, sentenceStay); return value; }
        private static ClemencyDefinition Clemency(string id, string name, ClemencyCategory category) { ClemencyDefinition value = ScriptableObject.CreateInstance<ClemencyDefinition>(); value.DevelopmentConfigure(id, name, category); return value; }
    }
}
