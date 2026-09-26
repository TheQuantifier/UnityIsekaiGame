using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Crimes;

namespace UnityIsekaiGame.Laws
{
    public static class PrototypeLegalDefinitionFactory
    {
        public const string SovereignAuthorityId = "legal-authority.prototype.sovereign-legislative";
        public const string MunicipalAuthorityId = "legal-authority.prototype.municipal-rulemaking";
        public const string EmergencyAuthorityId = "legal-authority.prototype.emergency-rulemaking";
        public const string InternalAuthorityId = "legal-authority.prototype.organization-internal";
        public const string CentralStatuteId = "legal-instrument.prototype.central-statute";
        public const string RegionalRegulationId = "legal-instrument.prototype.regional-regulation";
        public const string MunicipalOrdinanceId = "legal-instrument.prototype.municipal-ordinance";
        public const string ExecutiveDecreeId = "legal-instrument.prototype.executive-decree";
        public const string EmergencyOrderId = "legal-instrument.prototype.emergency-order";
        public const string MilitaryCodeId = "legal-instrument.prototype.military-code";
        public const string ReligiousRuleId = "legal-instrument.prototype.religious-rule";
        public const string OrganizationRuleId = "legal-instrument.prototype.organization-rule";
        public const string TreatyImplementationId = "legal-instrument.prototype.treaty-implementation";
        public const string CharterId = "legal-instrument.prototype.charter";
        public const string RightProvisionId = "legal-provision.prototype.right";
        public const string PermissionProvisionId = "legal-provision.prototype.permission";
        public const string DutyProvisionId = "legal-provision.prototype.duty";
        public const string ProhibitionProvisionId = "legal-provision.prototype.prohibition";
        public const string ExemptionProvisionId = "legal-provision.prototype.exemption";
        public const string ImmunityProvisionId = "legal-provision.prototype.immunity";
        public const string CitizenStatusId = "legal-status.prototype.citizen";
        public const string SubjectStatusId = "legal-status.prototype.subject";
        public const string PermanentResidentStatusId = "legal-status.prototype.permanent-resident";
        public const string TemporaryResidentStatusId = "legal-status.prototype.temporary-resident";
        public const string StatelessStatusId = "legal-status.prototype.stateless";
        public const string CitizenshipId = "citizenship.prototype.general";
        public const string WorldwideNoKillingRuleId = "law-rule.world.no-killing";
        public const string WorldwideNoStealingRuleId = "law-rule.world.no-stealing";
        public const string FundamentalWorldCodeInstrumentId = "legal-instrument.world.fundamental-code";
        public const string WorldGovernmentId = "government.world.fundamental-authority";
        public const string WorldAuthorityOrganizationId = "organization.world.fundamental-authority";
        public const string WorldJurisdictionId = "jurisdiction.world.fundamental-law";

        public static DefinitionRegistry AddMissingPrototypeLegalDefinitions(DefinitionRegistry source)
        {
            List<IGameDefinition> all = source?.DefinitionsById.Values.Where(item => item != null).ToList() ?? new List<IGameDefinition>();
            HashSet<string> ids = new HashSet<string>(all.Select(item => item.Id), StringComparer.Ordinal);
            void Add(IGameDefinition definition) { if (definition != null && ids.Add(definition.Id)) all.Add(definition); }
            Add(Authority(SovereignAuthorityId, "Sovereign Legislative Authority", LegalAuthorityCategory.SovereignLegislative, new[] { GovernmentLevel.Central }, false));
            Add(Authority(MunicipalAuthorityId, "Municipal Rulemaking Authority", LegalAuthorityCategory.MunicipalRulemaking, new[] { GovernmentLevel.Municipal, GovernmentLevel.Local }, false));
            Add(Authority(EmergencyAuthorityId, "Emergency Rulemaking Authority", LegalAuthorityCategory.EmergencyRulemaking, new[] { GovernmentLevel.Central, GovernmentLevel.Regional, GovernmentLevel.Municipal }, true));
            Add(Authority(InternalAuthorityId, "Internal Legal Code Authority", LegalAuthorityCategory.OrganizationInternalLegalCode, new[] { GovernmentLevel.NonTerritorial, GovernmentLevel.Special }, false));
            Add(Instrument(CentralStatuteId, "Central Statute", LegalInstrumentCategory.Statute, 900));
            Add(Instrument(RegionalRegulationId, "Regional Regulation", LegalInstrumentCategory.Regulation, 700));
            Add(Instrument(MunicipalOrdinanceId, "Municipal Ordinance", LegalInstrumentCategory.Ordinance, 500));
            Add(Instrument(ExecutiveDecreeId, "Executive Decree", LegalInstrumentCategory.Decree, 650));
            Add(Instrument(EmergencyOrderId, "Temporary Emergency Order", LegalInstrumentCategory.EmergencyOrder, 950, 30d));
            Add(Instrument(MilitaryCodeId, "Military Internal Code", LegalInstrumentCategory.MilitaryCode, 600));
            Add(Instrument(ReligiousRuleId, "Religious Internal Rule", LegalInstrumentCategory.ReligiousInternalRule, 400));
            Add(Instrument(OrganizationRuleId, "Organization Internal Legal Rule", LegalInstrumentCategory.OrganizationInternalLegalRule, 350));
            Add(Instrument(TreatyImplementationId, "Treaty Implementation Instrument", LegalInstrumentCategory.TreatyImplementationAct, 850));
            Add(Instrument(CharterId, "Foundational Charter", LegalInstrumentCategory.Charter, 1000));
            LegalInstrumentCategory[] categories = Enum.GetValues(typeof(LegalInstrumentCategory)).Cast<LegalInstrumentCategory>().Where(item => item != LegalInstrumentCategory.Unknown).ToArray();
            Add(Provision(RightProvisionId, "Legal Right", LegalEffectCategory.Right, categories));
            Add(Provision(PermissionProvisionId, "Legal Permission", LegalEffectCategory.Permission, categories));
            Add(Provision(DutyProvisionId, "Legal Duty", LegalEffectCategory.Duty, categories));
            Add(Provision(ProhibitionProvisionId, "Legal Prohibition", LegalEffectCategory.Prohibition, categories));
            Add(Provision(ExemptionProvisionId, "Legal Exemption", LegalEffectCategory.Exemption, categories));
            Add(Provision(ImmunityProvisionId, "Legal Immunity", LegalEffectCategory.Immunity, categories));
            Add(Status(CitizenStatusId, "Citizen", LegalStatusCategory.Citizen, true, true));
            Add(Status(SubjectStatusId, "Subject", LegalStatusCategory.Subject, true, true));
            Add(Status(PermanentResidentStatusId, "Permanent Resident", LegalStatusCategory.PermanentResident, false, true));
            Add(Status(TemporaryResidentStatusId, "Temporary Resident", LegalStatusCategory.TemporaryResident, false, true));
            Add(Status(StatelessStatusId, "Stateless Person", LegalStatusCategory.StatelessPerson, false, false));
            CitizenshipDefinition citizenship = ScriptableObject.CreateInstance<CitizenshipDefinition>(); citizenship.DevelopmentConfigure(CitizenshipId, "General Citizenship", Enum.GetValues(typeof(CitizenshipAcquisitionRoute)).Cast<CitizenshipAcquisitionRoute>().Where(item => item != CitizenshipAcquisitionRoute.Unknown), true, true); Add(citizenship);
            Add(Rule(WorldwideNoKillingRuleId, "No Killing", "crime.killing", PrototypeCrimeDefinitionFactory.UnlawfulKillingOffenseId, "FWC 1"));
            Add(Rule(WorldwideNoStealingRuleId, "No Stealing", "crime.theft", PrototypeCrimeDefinitionFactory.TheftOffenseId, "FWC 2"));
            return new DefinitionRegistry(all);
        }

        private static LegalAuthorityDefinition Authority(string id, string name, LegalAuthorityCategory category, IEnumerable<GovernmentLevel> levels, bool emergency) { LegalAuthorityDefinition value = ScriptableObject.CreateInstance<LegalAuthorityDefinition>(); value.DevelopmentConfigure(id, name, category, levels, Enum.GetValues(typeof(JurisdictionCategory)).Cast<JurisdictionCategory>().Where(item => item != JurisdictionCategory.Unknown), Enum.GetValues(typeof(LegalInstrumentCategory)).Cast<LegalInstrumentCategory>().Where(item => item != LegalInstrumentCategory.Unknown), emergency: emergency); return value; }
        private static LegalInstrumentDefinition Instrument(string id, string name, LegalInstrumentCategory category, int precedence, double emergency = -1d) { LegalInstrumentDefinition value = ScriptableObject.CreateInstance<LegalInstrumentDefinition>(); value.DevelopmentConfigure(id, name, category, precedence, LegalConflictPolicy.HigherPrecedenceWins, publication: true, emergencyDuration: emergency); return value; }
        private static LegalProvisionDefinition Provision(string id, string name, LegalEffectCategory effect, IEnumerable<LegalInstrumentCategory> instruments) { LegalProvisionDefinition value = ScriptableObject.CreateInstance<LegalProvisionDefinition>(); value.DevelopmentConfigure(id, name, effect, instruments); return value; }
        private static LegalStatusDefinition Status(string id, string name, LegalStatusCategory category, bool polity, bool multiple) { LegalStatusDefinition value = ScriptableObject.CreateInstance<LegalStatusDefinition>(); value.DevelopmentConfigure(id, name, category, polity, multiple); return value; }
        private static LawRuleDefinition Rule(string id, string name, string actionId, string offenseDefinitionId, string citation)
        {
            LawRuleDefinition value = ScriptableObject.CreateInstance<LawRuleDefinition>();
            value.DevelopmentConfigure(
                id,
                name,
                actionId,
                offenseDefinitionId,
                FundamentalWorldCodeInstrumentId,
                "Fundamental World Code",
                "World Code",
                citation,
                CentralStatuteId,
                SovereignAuthorityId,
                WorldGovernmentId,
                WorldAuthorityOrganizationId,
                new[] { WorldJurisdictionId },
                LawScopeKind.Worldwide,
                minimumCredibleReportReliabilityBasisPoints: 6000,
                reportWantedDefinitionId: PrototypeCrimeDefinitionFactory.WantedForQuestioningDefinitionId,
                authorityWantedDefinitionId: PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId);
            return value;
        }
    }
}
