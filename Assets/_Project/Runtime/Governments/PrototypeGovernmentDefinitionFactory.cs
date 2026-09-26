using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Governments
{
    public static class PrototypeGovernmentDefinitionFactory
    {
        public const string KingdomPolityDefinitionId = "polity.prototype.hereditary-kingdom";
        public const string EmpirePolityDefinitionId = "polity.prototype.centralized-empire";
        public const string RepublicPolityDefinitionId = "polity.prototype.republic";
        public const string CityStatePolityDefinitionId = "polity.prototype.city-state";
        public const string ConfederationPolityDefinitionId = "polity.prototype.confederation";
        public const string TribalPolityDefinitionId = "polity.prototype.tribal-polity";
        public const string ReligiousPolityDefinitionId = "polity.prototype.religious-polity";
        public const string AutonomousPolityDefinitionId = "polity.prototype.autonomous-region";
        public const string DisputedPolityDefinitionId = "polity.prototype.disputed-polity";
        public const string ExiledCommunityPolityDefinitionId = "polity.prototype.exiled-community";

        public const string RoyalGovernmentDefinitionId = "government.prototype.central-royal";
        public const string RepublicanCouncilDefinitionId = "government.prototype.republican-council";
        public const string MunicipalCouncilDefinitionId = "government.prototype.municipal-council";
        public const string VillageAdministrationDefinitionId = "government.prototype.village-administration";
        public const string TownAdministrationDefinitionId = "government.prototype.town-administration";
        public const string ManorAdministrationDefinitionId = "government.prototype.manor-administration";
        public const string DucalAdministrationDefinitionId = "government.prototype.ducal-administration";
        public const string ProvincialAdministrationDefinitionId = "government.prototype.provincial-administration";
        public const string TribalCouncilDefinitionId = "government.prototype.tribal-council";
        public const string ReligiousGovernmentDefinitionId = "government.prototype.religious-government";
        public const string ProvisionalGovernmentDefinitionId = "government.prototype.provisional-government";
        public const string OccupationAdministrationDefinitionId = "government.prototype.occupation-administration";
        public const string ExileGovernmentDefinitionId = "government.prototype.government-in-exile";
        public const string ClaimantGovernmentDefinitionId = "government.prototype.claimant-government";

        public const string RealmTerritoryDefinitionId = "political-territory.prototype.realm";
        public const string ProvinceTerritoryDefinitionId = "political-territory.prototype.province";
        public const string MunicipalityTerritoryDefinitionId = "political-territory.prototype.municipality";
        public const string VillageTerritoryDefinitionId = "political-territory.prototype.village";
        public const string OccupiedTerritoryDefinitionId = "political-territory.prototype.occupied-area";
        public const string AutonomousTerritoryDefinitionId = "political-territory.prototype.autonomous-region";
        public const string ReligiousTerritoryDefinitionId = "political-territory.prototype.religious-jurisdiction";
        public const string MilitaryDistrictTerritoryDefinitionId = "political-territory.prototype.military-district";

        public const string SovereigntyClaimDefinitionId = "territorial-claim.prototype.sovereignty";
        public const string AdministrativeClaimDefinitionId = "territorial-claim.prototype.administration";
        public const string TreatyClaimDefinitionId = "territorial-claim.prototype.treaty-basis";
        public const string OccupationClaimDefinitionId = "territorial-claim.prototype.occupation";
        public const string AutonomyClaimDefinitionId = "territorial-claim.prototype.autonomy";

        public const string GeneralJurisdictionDefinitionId = "jurisdiction.prototype.general-government";
        public const string MunicipalJurisdictionDefinitionId = "jurisdiction.prototype.municipal";
        public const string MilitaryJurisdictionDefinitionId = "jurisdiction.prototype.military";
        public const string ReligiousJurisdictionDefinitionId = "jurisdiction.prototype.religious-internal";
        public const string CommercialJurisdictionDefinitionId = "jurisdiction.prototype.commercial";
        public const string PropertyJurisdictionDefinitionId = "jurisdiction.prototype.property";
        public const string EmergencyJurisdictionDefinitionId = "jurisdiction.prototype.emergency";

        public const string CityGuardCharterDefinitionId = "government-charter.prototype.city-guard";
        public const string RoyalForgeCharterDefinitionId = "government-charter.prototype.royal-forge";
        public const string AdventurersGuildCharterDefinitionId = "government-charter.prototype.adventurers-guild";
        public const string MerchantGuildCharterDefinitionId = "government-charter.prototype.merchant-guild";
        public const string TempleCharterDefinitionId = "government-charter.prototype.temple";
        public const string UniversityCharterDefinitionId = "government-charter.prototype.university";

        public const string NobleOfficeDefinitionId = "government-office.prototype.noble";
        public const string VillageChiefOfficeDefinitionId = "government-office.prototype.village-chief";
        public const string MayorGovernmentOfficeDefinitionId = "government-office.prototype.mayor";
        public const string ManorLordOfficeDefinitionId = "government-office.prototype.manor-lord";
        public const string DukeOfficeDefinitionId = "government-office.prototype.duke";
        public const string MonarchOfficeDefinitionId = "government-office.prototype.monarch";
        public const string MagistrateGovernmentOfficeDefinitionId = "government-office.prototype.magistrate";
        public const string GuardCaptainGovernmentOfficeDefinitionId = "government-office.prototype.guard-captain";

        public const string TradeLicenseDefinitionId = "government-permit.prototype.trade-license";
        public const string CraftLicenseDefinitionId = "government-permit.prototype.craft-license";
        public const string GatheringWritDefinitionId = "government-permit.prototype.resource-gathering-writ";
        public const string TravelWritDefinitionId = "government-permit.prototype.travel-writ";
        public const string SpellPracticeLicenseDefinitionId = "government-permit.prototype.spell-practice-license";

        public const string TownAdministrationLegitimacyPolicyId = "government-legitimacy-policy.prototype.town-administration";
        public const string HereditaryHierarchyLegitimacyPolicyId = "government-legitimacy-policy.prototype.hereditary-hierarchy";
        public const string CouncilLegitimacyPolicyId = "government-legitimacy-policy.prototype.council";
        public const string ReligiousLegitimacyPolicyId = "government-legitimacy-policy.prototype.religious";
        public const string PrototypeTownFiscalPolicyId = "government-fiscal-policy.prototype.town";
        public const string PrototypeManorFiscalPolicyId = "government-fiscal-policy.prototype.manor";
        public const string PrototypeDuchyFiscalPolicyId = "government-fiscal-policy.prototype.duchy";
        public const string PrototypeKingdomFiscalPolicyId = "government-fiscal-policy.prototype.kingdom";

        public static DefinitionRegistry AddMissingPrototypeGovernmentDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            definitions.AddRange(CreateMissingPolityDefinitions(ids));
            definitions.AddRange(CreateMissingGovernmentDefinitions(ids));
            definitions.AddRange(CreateMissingTerritoryDefinitions(ids));
            definitions.AddRange(CreateMissingClaimDefinitions(ids));
            definitions.AddRange(CreateMissingJurisdictionDefinitions(ids));
            definitions.AddRange(CreateMissingCharterDefinitions(ids));
            definitions.AddRange(CreateMissingOfficeDefinitions(ids));
            definitions.AddRange(CreateMissingPermitDefinitions(ids));
            definitions.AddRange(CreateMissingLegitimacyPolicyDefinitions(ids));
            definitions.AddRange(CreateMissingFiscalPolicyDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<PolityDefinition> CreateMissingPolityDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<PolityDefinition> definitions = new List<PolityDefinition>();
            AddPolity(definitions, ids, KingdomPolityDefinitionId, "Prototype Hereditary Kingdom", PolityCategory.Kingdom, GovernmentCategory.MonarchicalGovernment);
            AddPolity(definitions, ids, EmpirePolityDefinitionId, "Prototype Centralized Empire", PolityCategory.Empire, GovernmentCategory.ImperialGovernment);
            AddPolity(definitions, ids, RepublicPolityDefinitionId, "Prototype Republic", PolityCategory.Republic, GovernmentCategory.RepublicanGovernment);
            AddPolity(definitions, ids, CityStatePolityDefinitionId, "Prototype City-State", PolityCategory.CityState, GovernmentCategory.MunicipalGovernment);
            AddPolity(definitions, ids, ConfederationPolityDefinitionId, "Prototype Confederation", PolityCategory.Confederation, GovernmentCategory.CouncilGovernment);
            AddPolity(definitions, ids, TribalPolityDefinitionId, "Prototype Tribal Polity", PolityCategory.TribalPolity, GovernmentCategory.TribalCouncil);
            AddPolity(definitions, ids, ReligiousPolityDefinitionId, "Prototype Religious Polity", PolityCategory.ReligiousPolity, GovernmentCategory.ReligiousGovernment, nonTerritorial: true);
            AddPolity(definitions, ids, AutonomousPolityDefinitionId, "Prototype Autonomous Region", PolityCategory.AutonomousPolity, GovernmentCategory.RegionalGovernment);
            AddPolity(definitions, ids, DisputedPolityDefinitionId, "Prototype Disputed Polity", PolityCategory.DisputedPolity, GovernmentCategory.ClaimantGovernment);
            AddPolity(definitions, ids, ExiledCommunityPolityDefinitionId, "Prototype Exiled Political Community", PolityCategory.StatelessPoliticalCommunity, GovernmentCategory.GovernmentInExile, territorial: false, nonTerritorial: true);
            return definitions;
        }

        public static IReadOnlyList<GovernmentDefinition> CreateMissingGovernmentDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<GovernmentDefinition> definitions = new List<GovernmentDefinition>();
            AddGovernment(definitions, ids, RoyalGovernmentDefinitionId, "Prototype Central Royal Government", GovernmentCategory.MonarchicalGovernment, GovernmentLevel.Central, PolityCategory.Kingdom);
            AddGovernment(definitions, ids, RepublicanCouncilDefinitionId, "Prototype Republican Council", GovernmentCategory.RepublicanGovernment, GovernmentLevel.Central, PolityCategory.Republic);
            AddGovernment(definitions, ids, MunicipalCouncilDefinitionId, "Prototype Municipal Council", GovernmentCategory.MunicipalGovernment, GovernmentLevel.Municipal, PolityCategory.CityState);
            AddGovernment(definitions, ids, VillageAdministrationDefinitionId, "Prototype Village Administration", GovernmentCategory.MunicipalGovernment, GovernmentLevel.Local, PolityCategory.Kingdom);
            AddGovernment(definitions, ids, TownAdministrationDefinitionId, "Prototype Town Administration", GovernmentCategory.MunicipalGovernment, GovernmentLevel.Municipal, PolityCategory.Kingdom);
            AddGovernment(definitions, ids, ManorAdministrationDefinitionId, "Prototype Manor Administration", GovernmentCategory.RegionalGovernment, GovernmentLevel.County, PolityCategory.Kingdom);
            AddGovernment(definitions, ids, DucalAdministrationDefinitionId, "Prototype Ducal Administration", GovernmentCategory.ProvincialGovernment, GovernmentLevel.Provincial, PolityCategory.Kingdom);
            AddGovernment(definitions, ids, ProvincialAdministrationDefinitionId, "Prototype Provincial Administration", GovernmentCategory.ProvincialGovernment, GovernmentLevel.Provincial, PolityCategory.Kingdom);
            AddGovernment(definitions, ids, TribalCouncilDefinitionId, "Prototype Tribal Council", GovernmentCategory.TribalCouncil, GovernmentLevel.Central, PolityCategory.TribalPolity);
            AddGovernment(definitions, ids, ReligiousGovernmentDefinitionId, "Prototype Religious Government", GovernmentCategory.ReligiousGovernment, GovernmentLevel.NonTerritorial, PolityCategory.ReligiousPolity, territorialRequired: false);
            AddGovernment(definitions, ids, ProvisionalGovernmentDefinitionId, "Prototype Provisional Government", GovernmentCategory.ProvisionalGovernment, GovernmentLevel.Central, PolityCategory.DisputedPolity, provisional: true);
            AddGovernment(definitions, ids, OccupationAdministrationDefinitionId, "Prototype Occupation Administration", GovernmentCategory.OccupationAdministration, GovernmentLevel.Regional, PolityCategory.Kingdom, occupation: true);
            AddGovernment(definitions, ids, ExileGovernmentDefinitionId, "Prototype Government in Exile", GovernmentCategory.GovernmentInExile, GovernmentLevel.NonTerritorial, PolityCategory.StatelessPoliticalCommunity, territorialRequired: false, exile: true);
            AddGovernment(definitions, ids, ClaimantGovernmentDefinitionId, "Prototype Claimant Government", GovernmentCategory.ClaimantGovernment, GovernmentLevel.Central, PolityCategory.DisputedPolity);
            return definitions;
        }

        public static IReadOnlyList<PoliticalTerritoryDefinition> CreateMissingTerritoryDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<PoliticalTerritoryDefinition> definitions = new List<PoliticalTerritoryDefinition>();
            AddTerritory(definitions, ids, RealmTerritoryDefinitionId, "Prototype Realm Territory", PoliticalTerritoryCategory.Realm);
            AddTerritory(definitions, ids, ProvinceTerritoryDefinitionId, "Prototype Province Territory", PoliticalTerritoryCategory.Province);
            AddTerritory(definitions, ids, MunicipalityTerritoryDefinitionId, "Prototype Municipality Territory", PoliticalTerritoryCategory.City);
            AddTerritory(definitions, ids, VillageTerritoryDefinitionId, "Prototype Village Territory", PoliticalTerritoryCategory.Village);
            AddTerritory(definitions, ids, OccupiedTerritoryDefinitionId, "Prototype Occupied Area", PoliticalTerritoryCategory.OccupiedArea);
            AddTerritory(definitions, ids, AutonomousTerritoryDefinitionId, "Prototype Autonomous Region", PoliticalTerritoryCategory.AutonomousRegion);
            AddTerritory(definitions, ids, ReligiousTerritoryDefinitionId, "Prototype Religious Jurisdiction Territory", PoliticalTerritoryCategory.ReligiousJurisdiction, nonTerritorial: true);
            AddTerritory(definitions, ids, MilitaryDistrictTerritoryDefinitionId, "Prototype Military District", PoliticalTerritoryCategory.MilitaryDistrict);
            return definitions;
        }

        public static IReadOnlyList<TerritorialClaimDefinition> CreateMissingClaimDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<TerritorialClaimDefinition> definitions = new List<TerritorialClaimDefinition>();
            AddClaim(definitions, ids, SovereigntyClaimDefinitionId, "Prototype Sovereignty Claim", TerritorialClaimCategory.Sovereignty, governmentRequired: true);
            AddClaim(definitions, ids, AdministrativeClaimDefinitionId, "Prototype Administrative Claim", TerritorialClaimCategory.Administration, governmentRequired: true);
            AddClaim(definitions, ids, TreatyClaimDefinitionId, "Prototype Treaty-Based Claim", TerritorialClaimCategory.TreatyBased, governmentRequired: true);
            AddClaim(definitions, ids, OccupationClaimDefinitionId, "Prototype Occupation Claim", TerritorialClaimCategory.Occupation, governmentRequired: true);
            AddClaim(definitions, ids, AutonomyClaimDefinitionId, "Prototype Autonomy Claim", TerritorialClaimCategory.Autonomy, governmentRequired: true);
            return definitions;
        }

        public static IReadOnlyList<JurisdictionDefinition> CreateMissingJurisdictionDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<JurisdictionDefinition> definitions = new List<JurisdictionDefinition>();
            AddJurisdiction(definitions, ids, GeneralJurisdictionDefinitionId, "Prototype General Government Jurisdiction", JurisdictionCategory.GeneralGovernment, JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.Place | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.GeneralAdministration, JurisdictionSubjectMatter.PublicOrder, JurisdictionSubjectMatter.BorderAdministration });
            AddJurisdiction(definitions, ids, MunicipalJurisdictionDefinitionId, "Prototype Municipal Jurisdiction", JurisdictionCategory.Municipal, JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.Place | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.MunicipalServices, JurisdictionSubjectMatter.GeneralAdministration, JurisdictionSubjectMatter.PublicOrder, JurisdictionSubjectMatter.PropertyAdministration });
            AddJurisdiction(definitions, ids, MilitaryJurisdictionDefinitionId, "Prototype Military Jurisdiction", JurisdictionCategory.Military, JurisdictionScopeDimension.Person | JurisdictionScopeDimension.Organization | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.MilitaryDiscipline }, exclusive: true);
            AddJurisdiction(definitions, ids, ReligiousJurisdictionDefinitionId, "Prototype Religious Internal Jurisdiction", JurisdictionCategory.Religious, JurisdictionScopeDimension.Person | JurisdictionScopeDimension.Organization | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.ReligiousInternalAffairs });
            AddJurisdiction(definitions, ids, CommercialJurisdictionDefinitionId, "Prototype Commercial Jurisdiction", JurisdictionCategory.Commercial, JurisdictionScopeDimension.Organization | JurisdictionScopeDimension.Property | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.TradeRegulation });
            AddJurisdiction(definitions, ids, PropertyJurisdictionDefinitionId, "Prototype Property Jurisdiction", JurisdictionCategory.Property, JurisdictionScopeDimension.Property | JurisdictionScopeDimension.Place | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.PropertyAdministration });
            AddJurisdiction(definitions, ids, EmergencyJurisdictionDefinitionId, "Prototype Emergency Jurisdiction", JurisdictionCategory.Emergency, JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, new[] { JurisdictionSubjectMatter.EmergencyAdministration }, conflictPolicy: JurisdictionConflictPolicy.HigherPriorityWins);
            return definitions;
        }

        public static IReadOnlyList<GovernmentOrganizationCharterDefinition> CreateMissingCharterDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<GovernmentOrganizationCharterDefinition> definitions = new List<GovernmentOrganizationCharterDefinition>();
            AddCharter(definitions, ids, CityGuardCharterDefinitionId, "Prototype City Guard Agency Charter", GovernmentOrganizationCharterCategory.GovernmentAgency, "organization.prototype.city-guard", "legal-instrument.charter.prototype.city-guard", new[] { "government.power.enforce-law", "government.power.patrol-public-order", "government.power.make-lawful-arrest" }, new[] { "government.duty.protect-public", "government.duty.report-enforcement-actions" });
            AddCharter(definitions, ids, RoyalForgeCharterDefinitionId, "Prototype Royal Forge Public Enterprise Charter", GovernmentOrganizationCharterCategory.PublicEnterprise, "organization.prototype.royal-forge", "legal-instrument.charter.prototype.royal-forge", new[] { "government.power.manufacture-public-equipment", "government.power.purchase-strategic-materials" }, new[] { "government.duty.supply-civic-government", "government.duty.maintain-production-records" });
            AddCharter(definitions, ids, AdventurersGuildCharterDefinitionId, "Prototype Adventurers Guild Charter", GovernmentOrganizationCharterCategory.CharteredGuild, "organization.prototype.adventurers-guild", "legal-instrument.charter.prototype.adventurers-guild", new[] { "guild.power.register-adventurers", "guild.power.issue-contracts", "guild.power.assign-guild-rank" }, new[] { "guild.duty.keep-member-records", "guild.duty.report-dangerous-contracts" });
            AddCharter(definitions, ids, MerchantGuildCharterDefinitionId, "Prototype Merchant Guild Charter", GovernmentOrganizationCharterCategory.CharteredGuild, "organization.prototype.merchant-guild", "legal-instrument.charter.prototype.merchant-guild", new[] { "guild.power.register-merchants", "guild.power.certify-trade", "guild.power.mediate-commercial-disputes" }, new[] { "guild.duty.keep-trade-records", "guild.duty.report-fraud" });
            AddCharter(definitions, ids, TempleCharterDefinitionId, "Prototype Temple Recognition Charter", GovernmentOrganizationCharterCategory.ReligiousInstitution, "organization.prototype.temple", "legal-instrument.charter.prototype.temple", new[] { "institution.power.conduct-religious-services", "institution.power.train-clergy" }, new[] { "institution.duty.provide-sanctuary-care", "institution.duty.observe-public-law" });
            AddCharter(definitions, ids, UniversityCharterDefinitionId, "Prototype University Recognition Charter", GovernmentOrganizationCharterCategory.RecognizedInstitution, "organization.prototype.university", "legal-instrument.charter.prototype.university", new[] { "institution.power.teach", "institution.power.certify-learning", "institution.power.conduct-research" }, new[] { "institution.duty.keep-academic-records", "institution.duty.observe-public-law" });
            return definitions;
        }

        public static IReadOnlyList<GovernmentOfficeDefinition> CreateMissingOfficeDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<GovernmentOfficeDefinition> definitions = new List<GovernmentOfficeDefinition>();
            AddOffice(definitions, ids, NobleOfficeDefinitionId, "Noble", PrototypeOrganizationMembershipDefinitionFactory.NobleTitleOfficeId, GovernmentInstitutionRoleCategory.Custom, new[] { GovernmentOfficeSelectionMethod.HereditarySuccession, GovernmentOfficeSelectionMethod.TitleGrant }, PrototypeOrganizationAuthorityDefinitionFactory.NobleRoleId, GovernmentAuthorityTier.Noble, VillageChiefOfficeDefinitionId);
            AddOffice(definitions, ids, VillageChiefOfficeDefinitionId, "Village Chief", PrototypeOrganizationMembershipDefinitionFactory.VillageChiefOfficeId, GovernmentInstitutionRoleCategory.LocalAdministration, new[] { GovernmentOfficeSelectionMethod.SovereignAppointment, GovernmentOfficeSelectionMethod.CouncilSelection, GovernmentOfficeSelectionMethod.PopularElection, GovernmentOfficeSelectionMethod.Acclamation, GovernmentOfficeSelectionMethod.TemporaryAppointment }, PrototypeOrganizationAuthorityDefinitionFactory.VillageChiefRoleId, GovernmentAuthorityTier.Village, MayorGovernmentOfficeDefinitionId, head: true);
            AddOffice(definitions, ids, MayorGovernmentOfficeDefinitionId, "Mayor", PrototypeOrganizationMembershipDefinitionFactory.MayorOfficeId, GovernmentInstitutionRoleCategory.LocalAdministration, new[] { GovernmentOfficeSelectionMethod.SovereignAppointment, GovernmentOfficeSelectionMethod.CouncilSelection, GovernmentOfficeSelectionMethod.PopularElection, GovernmentOfficeSelectionMethod.TemporaryAppointment }, PrototypeOrganizationAuthorityDefinitionFactory.MayorRoleId, GovernmentAuthorityTier.Town, ManorLordOfficeDefinitionId, head: true);
            AddOffice(definitions, ids, ManorLordOfficeDefinitionId, "Manor Lord", PrototypeOrganizationMembershipDefinitionFactory.ManorLordOfficeId, GovernmentInstitutionRoleCategory.Executive, new[] { GovernmentOfficeSelectionMethod.HereditarySuccession, GovernmentOfficeSelectionMethod.SovereignAppointment, GovernmentOfficeSelectionMethod.TitleGrant, GovernmentOfficeSelectionMethod.Regency, GovernmentOfficeSelectionMethod.Conquest }, PrototypeOrganizationAuthorityDefinitionFactory.ManorLordRoleId, GovernmentAuthorityTier.Manor, DukeOfficeDefinitionId, head: true, confirmation: true);
            AddOffice(definitions, ids, DukeOfficeDefinitionId, "Duke or Duchess", PrototypeOrganizationMembershipDefinitionFactory.DukeOfficeId, GovernmentInstitutionRoleCategory.Executive, new[] { GovernmentOfficeSelectionMethod.HereditarySuccession, GovernmentOfficeSelectionMethod.SovereignAppointment, GovernmentOfficeSelectionMethod.TitleGrant, GovernmentOfficeSelectionMethod.Regency }, PrototypeOrganizationAuthorityDefinitionFactory.DukeRoleId, GovernmentAuthorityTier.Duchy, MonarchOfficeDefinitionId, head: true, confirmation: true, eligibility: GovernmentOfficeEligibilityRule.DirectDescendantOfReigningMonarch);
            AddOffice(definitions, ids, MonarchOfficeDefinitionId, "King or Queen", PrototypeOrganizationMembershipDefinitionFactory.MonarchOfficeId, GovernmentInstitutionRoleCategory.Executive, new[] { GovernmentOfficeSelectionMethod.HereditarySuccession, GovernmentOfficeSelectionMethod.Regency, GovernmentOfficeSelectionMethod.Conquest, GovernmentOfficeSelectionMethod.NobleElection }, PrototypeOrganizationAuthorityDefinitionFactory.MonarchRoleId, GovernmentAuthorityTier.Kingdom, string.Empty, head: true, confirmation: true);
            AddOffice(definitions, ids, MagistrateGovernmentOfficeDefinitionId, "Magistrate", PrototypeOrganizationMembershipDefinitionFactory.MagistrateOfficeId, GovernmentInstitutionRoleCategory.Court, new[] { GovernmentOfficeSelectionMethod.SovereignAppointment, GovernmentOfficeSelectionMethod.CouncilSelection, GovernmentOfficeSelectionMethod.TemporaryAppointment }, PrototypeOrganizationAuthorityDefinitionFactory.MagistrateRoleId, GovernmentAuthorityTier.Town, MayorGovernmentOfficeDefinitionId, confirmation: true);
            AddOffice(definitions, ids, GuardCaptainGovernmentOfficeDefinitionId, "Guard Captain", PrototypeOrganizationMembershipDefinitionFactory.GuardCaptainOfficeId, GovernmentInstitutionRoleCategory.MilitaryAdministration, new[] { GovernmentOfficeSelectionMethod.InternalPromotion, GovernmentOfficeSelectionMethod.SovereignAppointment, GovernmentOfficeSelectionMethod.TemporaryAppointment }, PrototypeOrganizationAuthorityDefinitionFactory.MilitaryOfficerRoleId, GovernmentAuthorityTier.Town, MayorGovernmentOfficeDefinitionId, confirmation: true);
            return definitions;
        }

        public static IReadOnlyList<GovernmentPermitDefinition> CreateMissingPermitDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<GovernmentPermitDefinition> definitions = new List<GovernmentPermitDefinition>();
            GovernmentPermitHolderCategory[] personsAndOrganizations = { GovernmentPermitHolderCategory.Person, GovernmentPermitHolderCategory.Organization, GovernmentPermitHolderCategory.Business };
            AddPermit(definitions, ids, TradeLicenseDefinitionId, "Trade License", GovernmentPermitCategory.TradeLicense, personsAndOrganizations, new[] { "government.action.conduct-regulated-trade", "government.action.operate-market-stall" });
            AddPermit(definitions, ids, CraftLicenseDefinitionId, "Craft License", GovernmentPermitCategory.CraftLicense, personsAndOrganizations, new[] { "government.action.operate-regulated-workshop", "government.action.sell-crafted-goods" });
            AddPermit(definitions, ids, GatheringWritDefinitionId, "Resource Gathering Writ", GovernmentPermitCategory.ResourceGatheringWrit, new[] { GovernmentPermitHolderCategory.Person, GovernmentPermitHolderCategory.Organization }, new[] { "government.action.gather-regulated-resources" });
            AddPermit(definitions, ids, TravelWritDefinitionId, "Travel Writ", GovernmentPermitCategory.TravelWrit, new[] { GovernmentPermitHolderCategory.Person, GovernmentPermitHolderCategory.Organization }, new[] { "government.action.enter-restricted-route", "government.action.cross-controlled-border" }, duration: 30d);
            AddPermit(definitions, ids, SpellPracticeLicenseDefinitionId, "Spell Practice License", GovernmentPermitCategory.SpellPracticeLicense, new[] { GovernmentPermitHolderCategory.Person, GovernmentPermitHolderCategory.Organization }, new[] { "government.action.practice-regulated-magic" });
            return definitions;
        }

        public static IReadOnlyList<GovernmentLegitimacyPolicyDefinition> CreateMissingLegitimacyPolicyDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<GovernmentLegitimacyPolicyDefinition> definitions = new List<GovernmentLegitimacyPolicyDefinition>();
            AddLegitimacyPolicy(definitions, ids, TownAdministrationLegitimacyPolicyId, "Town Administration Legitimacy", new[] { GovernmentCategory.MunicipalGovernment }, new[]
            {
                Weight(GovernmentLegitimacyComponent.SovereignRecognition, 1200), Weight(GovernmentLegitimacyComponent.OathsAndCustom, 1200),
                Weight(GovernmentLegitimacyComponent.TerritorialControl, 1600), Weight(GovernmentLegitimacyComponent.InstitutionalSupport, 1900),
                Weight(GovernmentLegitimacyComponent.PublicSupport, 1600), Weight(GovernmentLegitimacyComponent.AdministrativePerformance, 2500)
            });
            AddLegitimacyPolicy(definitions, ids, HereditaryHierarchyLegitimacyPolicyId, "Hereditary Hierarchy Legitimacy", new[] { GovernmentCategory.MonarchicalGovernment, GovernmentCategory.RegionalGovernment, GovernmentCategory.ProvincialGovernment }, new[]
            {
                Weight(GovernmentLegitimacyComponent.Inheritance, 2600), Weight(GovernmentLegitimacyComponent.SovereignRecognition, 2000),
                Weight(GovernmentLegitimacyComponent.OathsAndCustom, 1300), Weight(GovernmentLegitimacyComponent.TerritorialControl, 1500),
                Weight(GovernmentLegitimacyComponent.InstitutionalSupport, 1000), Weight(GovernmentLegitimacyComponent.AdministrativePerformance, 1600)
            });
            AddLegitimacyPolicy(definitions, ids, CouncilLegitimacyPolicyId, "Council Legitimacy", new[] { GovernmentCategory.CouncilGovernment, GovernmentCategory.RepublicanGovernment }, new[]
            {
                Weight(GovernmentLegitimacyComponent.ElectionMandate, 3000), Weight(GovernmentLegitimacyComponent.InstitutionalSupport, 2200),
                Weight(GovernmentLegitimacyComponent.PublicSupport, 1800), Weight(GovernmentLegitimacyComponent.TerritorialControl, 1200),
                Weight(GovernmentLegitimacyComponent.AdministrativePerformance, 1800)
            });
            AddLegitimacyPolicy(definitions, ids, ReligiousLegitimacyPolicyId, "Religious Government Legitimacy", new[] { GovernmentCategory.ReligiousGovernment }, new[]
            {
                Weight(GovernmentLegitimacyComponent.ReligiousRecognition, 3500), Weight(GovernmentLegitimacyComponent.OathsAndCustom, 1800),
                Weight(GovernmentLegitimacyComponent.InstitutionalSupport, 1600), Weight(GovernmentLegitimacyComponent.PublicSupport, 1200),
                Weight(GovernmentLegitimacyComponent.TerritorialControl, 900), Weight(GovernmentLegitimacyComponent.AdministrativePerformance, 1000)
            });
            return definitions;
        }

        public static IReadOnlyList<GovernmentFiscalPolicyDefinition> CreateMissingFiscalPolicyDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<GovernmentFiscalPolicyDefinition> definitions = new List<GovernmentFiscalPolicyDefinition>();
            if (!ids.Contains(PrototypeTownFiscalPolicyId))
            {
                GovernmentFiscalPolicyDefinition definition = ScriptableObject.CreateInstance<GovernmentFiscalPolicyDefinition>();
                definition.name = "Prototype Town Fiscal Policy";
                definition.DevelopmentConfigure(PrototypeTownFiscalPolicyId, "Prototype Town Fiscal Policy", new[] { PrototypeEconomyContentIds.RevenueSalesTax }, PrototypeInstitutionalContentIds.CivicAccount, budgetRequired: true);
                definitions.Add(definition);
                ids.Add(PrototypeTownFiscalPolicyId);
            }
            AddFiscalPolicy(definitions, ids, PrototypeManorFiscalPolicyId, "Prototype Manor Fiscal Policy", PrototypeInstitutionalContentIds.ManorAccount);
            AddFiscalPolicy(definitions, ids, PrototypeDuchyFiscalPolicyId, "Prototype Duchy Fiscal Policy", PrototypeInstitutionalContentIds.DuchyAccount);
            AddFiscalPolicy(definitions, ids, PrototypeKingdomFiscalPolicyId, "Prototype Kingdom Fiscal Policy", PrototypeInstitutionalContentIds.KingdomAccount);
            return definitions;
        }

        private static void AddFiscalPolicy(ICollection<GovernmentFiscalPolicyDefinition> definitions, ISet<string> ids, string id, string name, string treasuryAccountId)
        {
            if (ids.Contains(id)) return;
            GovernmentFiscalPolicyDefinition definition = ScriptableObject.CreateInstance<GovernmentFiscalPolicyDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, Array.Empty<string>(), treasuryAccountId, budgetRequired: true);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddPolity(ICollection<PolityDefinition> definitions, ISet<string> ids, string id, string name, PolityCategory category, GovernmentCategory governmentCategory, bool territorial = true, bool nonTerritorial = false)
        {
            if (ids.Contains(id)) return;
            PolityDefinition definition = ScriptableObject.CreateInstance<PolityDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, new[] { governmentCategory }, territorial, nonTerritorial, tagIds: Tags("polity"));
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddGovernment(ICollection<GovernmentDefinition> definitions, ISet<string> ids, string id, string name, GovernmentCategory category, GovernmentLevel level, PolityCategory polityCategory, bool territorialRequired = true, bool exile = false, bool provisional = true, bool occupation = false)
        {
            if (ids.Contains(id)) return;
            GovernmentDefinition definition = ScriptableObject.CreateInstance<GovernmentDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, level, new[] { polityCategory }, territorialRequired: territorialRequired, exile: exile, provisional: provisional, occupation: occupation, tagIds: Tags("government"));
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddTerritory(ICollection<PoliticalTerritoryDefinition> definitions, ISet<string> ids, string id, string name, PoliticalTerritoryCategory category, bool nonTerritorial = false)
        {
            if (ids.Contains(id)) return;
            PoliticalTerritoryDefinition definition = ScriptableObject.CreateInstance<PoliticalTerritoryDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, nonTerritorial: nonTerritorial, tagIds: Tags("territory"));
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddClaim(ICollection<TerritorialClaimDefinition> definitions, ISet<string> ids, string id, string name, TerritorialClaimCategory category, bool governmentRequired)
        {
            if (ids.Contains(id)) return;
            TerritorialClaimDefinition definition = ScriptableObject.CreateInstance<TerritorialClaimDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, governmentRequired: governmentRequired, tagIds: Tags("claim"));
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddJurisdiction(ICollection<JurisdictionDefinition> definitions, ISet<string> ids, string id, string name, JurisdictionCategory category, JurisdictionScopeDimension dimensions, IEnumerable<JurisdictionSubjectMatter> subjectMatters, JurisdictionConflictPolicy conflictPolicy = JurisdictionConflictPolicy.SpecificOverridesGeneral, bool exclusive = false)
        {
            if (ids.Contains(id)) return;
            JurisdictionDefinition definition = ScriptableObject.CreateInstance<JurisdictionDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, dimensions, subjectMatters, conflictPolicy, exclusive: exclusive, tagIds: Tags("jurisdiction"));
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddCharter(ICollection<GovernmentOrganizationCharterDefinition> definitions, ISet<string> ids, string id, string name, GovernmentOrganizationCharterCategory category, string organizationId, string instrumentId, IEnumerable<string> powers, IEnumerable<string> duties)
        {
            if (ids.Contains(id)) return;
            GovernmentOrganizationCharterDefinition definition = ScriptableObject.CreateInstance<GovernmentOrganizationCharterDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, "government.prototype.civic", organizationId, "organization.prototype.government", instrumentId, new[] { "jurisdiction.prototype.town" }, powers, duties, tagIds: new[] { "prototype", "government", "charter" });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddOffice(ICollection<GovernmentOfficeDefinition> definitions, ISet<string> ids, string id, string name, string organizationOfficeDefinitionId, GovernmentInstitutionRoleCategory role, IEnumerable<GovernmentOfficeSelectionMethod> methods, string authorityRoleId, GovernmentAuthorityTier tier, string superiorOfficeId, bool head = false, bool confirmation = false, GovernmentOfficeEligibilityRule eligibility = GovernmentOfficeEligibilityRule.None)
        {
            if (ids.Contains(id)) return;
            GovernmentOfficeDefinition definition = ScriptableObject.CreateInstance<GovernmentOfficeDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, organizationOfficeDefinitionId, role, methods, head, confirmation, authorityRoleId: authorityRoleId, tagIds: Tags("office"), tier: tier, superiorOfficeId: superiorOfficeId, eligibility: eligibility);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddPermit(ICollection<GovernmentPermitDefinition> definitions, ISet<string> ids, string id, string name, GovernmentPermitCategory category, IEnumerable<GovernmentPermitHolderCategory> holders, IEnumerable<string> actions, double duration = -1d)
        {
            if (ids.Contains(id)) return;
            GovernmentPermitDefinition definition = ScriptableObject.CreateInstance<GovernmentPermitDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, holders, actions, duration, tagIds: Tags("permit"));
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddLegitimacyPolicy(ICollection<GovernmentLegitimacyPolicyDefinition> definitions, ISet<string> ids, string id, string name, IEnumerable<GovernmentCategory> categories, IEnumerable<GovernmentLegitimacyWeight> weights)
        {
            if (ids.Contains(id)) return;
            GovernmentLegitimacyPolicyDefinition definition = ScriptableObject.CreateInstance<GovernmentLegitimacyPolicyDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, categories, weights);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static GovernmentLegitimacyWeight Weight(GovernmentLegitimacyComponent component, int weight) => new GovernmentLegitimacyWeight { component = component, weightBasisPoints = weight };

        private static HashSet<string> Set(IEnumerable<string> ids) => new HashSet<string>((ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        private static string[] Tags(string domain) => new[] { "prototype", "government", domain };
    }
}
