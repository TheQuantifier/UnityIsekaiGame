using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Social.Family;

namespace UnityIsekaiGame.Organizations
{
    public static class PrototypeInstitutionalContentIds
    {
        public const string AdventurersGuild = "organization.prototype.adventurers-guild";
        public const string MerchantGuild = "organization.prototype.merchant-guild";
        public const string TempleOrganization = "organization.prototype.temple";
        public const string UniversityOrganization = "organization.prototype.university";
        public const string CivicOrganization = "organization.prototype.government";
        public const string ManorAdministrationOrganization = "organization.prototype.manor-administration";
        public const string DuchyAdministrationOrganization = "organization.prototype.duchy-administration";
        public const string CrownAdministrationOrganization = "organization.prototype.crown-administration";
        public const string CityGuardOrganization = "organization.prototype.city-guard";
        public const string RoyalForgeOrganization = "organization.prototype.royal-forge";
        public const string MayorPerson = "person.prototype.mayor";
        public const string GuardPerson = "person.prototype.guard";
        public const string MagistratePerson = "person.prototype.magistrate";
        public const string GuildmasterPerson = "person.prototype.guildmaster";
        public const string MerchantGuildmasterPerson = "person.prototype.merchant-guildmaster";
        public const string AdventurersGuildReceptionistPerson = "person.prototype.adventurers-guild-receptionist";
        public const string MerchantGuildReceptionistPerson = "person.prototype.merchant-guild-receptionist";
        public const string TemplePriestPerson = "person.prototype.temple-priest";
        public const string UniversityHeadmasterPerson = "person.prototype.university-headmaster";
        public const string ManorLordPerson = "person.prototype.manor-lord";
        public const string DukePerson = "person.prototype.duke";
        public const string MonarchPerson = "person.prototype.monarch";
        public const string RealmPolity = "polity.prototype.realm";
        public const string TownPolity = RealmPolity;
        public const string KingdomGovernment = "government.prototype.kingdom";
        public const string DuchyGovernment = "government.prototype.duchy";
        public const string ManorGovernment = "government.prototype.manor";
        public const string TownGovernment = "government.prototype.civic";
        public const string TownTerritory = "political-territory.prototype.town";
        public const string TownJurisdiction = "jurisdiction.prototype.town";
        public const string WorldPolity = "polity.world.fundamental-order";
        public const string WorldGovernment = PrototypeLegalDefinitionFactory.WorldGovernmentId;
        public const string WorldJurisdiction = PrototypeLegalDefinitionFactory.WorldJurisdictionId;
        public const string WorldCode = PrototypeLegalDefinitionFactory.FundamentalWorldCodeInstrumentId;
        public const string TownCourt = "court.prototype.town";
        public const string CivicTreasury = "organization-treasury.prototype.civic";
        public const string CivicAccount = "organization-account.prototype.civic.general";
        public const string CivicBudget = "organization-budget.prototype.civic.general";
        public const string ManorTreasury = "organization-treasury.prototype.manor";
        public const string ManorAccount = "organization-account.prototype.manor.general";
        public const string ManorEconomyAccount = "economy-account.government.prototype.manor";
        public const string ManorBudget = "organization-budget.prototype.manor.general";
        public const string DuchyTreasury = "organization-treasury.prototype.duchy";
        public const string DuchyAccount = "organization-account.prototype.duchy.general";
        public const string DuchyEconomyAccount = "economy-account.government.prototype.duchy";
        public const string DuchyBudget = "organization-budget.prototype.duchy.general";
        public const string KingdomTreasury = "organization-treasury.prototype.kingdom";
        public const string KingdomAccount = "organization-account.prototype.kingdom.general";
        public const string KingdomEconomyAccount = "economy-account.government.prototype.kingdom";
        public const string KingdomBudget = "organization-budget.prototype.kingdom.general";

        public static readonly string[] InstitutionalPersonIds =
        {
            MayorPerson,
            GuardPerson,
            MagistratePerson,
            GuildmasterPerson,
            MerchantGuildmasterPerson,
            AdventurersGuildReceptionistPerson,
            MerchantGuildReceptionistPerson,
            TemplePriestPerson,
            UniversityHeadmasterPerson,
            ManorLordPerson,
            DukePerson,
            MonarchPerson
        };

        public static string PlayerGuildMembershipId(string playerPersonId)
        {
            string value = string.IsNullOrWhiteSpace(playerPersonId) ? "unknown-player" : playerPersonId.Trim().ToLowerInvariant();
            return $"organization-membership.prototype.adventurers-guild.{new string(value.Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-').ToArray())}";
        }
    }

    public sealed class PrototypeInstitutionalBootstrapReport
    {
        private readonly List<string> diagnostics = new List<string>();
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();
        public bool Succeeded => diagnostics.Count == 0;
        internal void Add(string message) { if (!string.IsNullOrWhiteSpace(message)) diagnostics.Add(message); }
    }

    public static class PrototypeInstitutionalWorldBootstrap
    {
        public static PrototypeInstitutionalBootstrapReport EnsureSeeded(
            DefinitionRegistry registry,
            string playerPersonId,
            IEnumerable<string> knownPlaceIds,
            OrganizationRuntime organizations,
            OrganizationMembershipRuntime memberships,
            OrganizationAuthorityRuntime authority,
            OrganizationResourceRuntime resources,
            GovernmentRuntime governments,
            LegalRuntime laws,
            JusticeRuntime justice,
            FamilyRelationshipRuntime familyRelationships,
            double worldTime = 0d)
        {
            PrototypeInstitutionalBootstrapReport report = new PrototypeInstitutionalBootstrapReport();
            if (registry == null || organizations == null || memberships == null || authority == null || resources == null || governments == null || laws == null || justice == null || familyRelationships == null)
            {
                report.Add("Institutional bootstrap dependencies are incomplete.");
                return report;
            }

            string player = string.IsNullOrWhiteSpace(playerPersonId) ? "person.prototype.player" : playerPersonId.Trim();
            string[] places = (knownPlaceIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string civicPlace = places.Contains("location.prototype.civic-office", StringComparer.Ordinal) ? "location.prototype.civic-office" : places.FirstOrDefault() ?? string.Empty;

            OrganizationMembershipSnapshot guildmaster = EnsureMembership(memberships, "organization-membership.prototype.adventurers-guild.guildmaster", PrototypeInstitutionalContentIds.AdventurersGuild, PrototypeInstitutionalContentIds.GuildmasterPerson, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, report);
            EnsureTopGuildRating(memberships, guildmaster, "adventurers-guildmaster", report);
            EnsureOffice(memberships, "organization-office-record.prototype.adventurers-guild.guildmaster", PrototypeInstitutionalContentIds.AdventurersGuild, PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, "Guildmaster", guildmaster, report);

            OrganizationMembershipSnapshot adventurersReceptionist = EnsureMembership(memberships, "organization-membership.prototype.adventurers-guild.receptionist", PrototypeInstitutionalContentIds.AdventurersGuild, PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson, PrototypeOrganizationMembershipDefinitionFactory.GuildStaffMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.adventurers-guild.receptionist", PrototypeInstitutionalContentIds.AdventurersGuild, PrototypeOrganizationMembershipDefinitionFactory.GuildReceptionistOfficeId, "Adventurers Guild Receptionist", adventurersReceptionist, report);

            OrganizationMembershipSnapshot merchantGuildmaster = EnsureMembership(memberships, "organization-membership.prototype.merchant-guild.guildmaster", PrototypeInstitutionalContentIds.MerchantGuild, PrototypeInstitutionalContentIds.MerchantGuildmasterPerson, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, report);
            EnsureTopGuildRating(memberships, merchantGuildmaster, "merchant-guildmaster", report);
            EnsureOffice(memberships, "organization-office-record.prototype.merchant-guild.guildmaster", PrototypeInstitutionalContentIds.MerchantGuild, PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, "Merchant Guildmaster", merchantGuildmaster, report);

            OrganizationMembershipSnapshot merchantReceptionist = EnsureMembership(memberships, "organization-membership.prototype.merchant-guild.receptionist", PrototypeInstitutionalContentIds.MerchantGuild, PrototypeInstitutionalContentIds.MerchantGuildReceptionistPerson, PrototypeOrganizationMembershipDefinitionFactory.GuildStaffMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.merchant-guild.receptionist", PrototypeInstitutionalContentIds.MerchantGuild, PrototypeOrganizationMembershipDefinitionFactory.GuildReceptionistOfficeId, "Merchant Guild Receptionist", merchantReceptionist, report);

            OrganizationMembershipSnapshot priest = EnsureMembership(memberships, "organization-membership.prototype.temple.priest", PrototypeInstitutionalContentIds.TempleOrganization, PrototypeInstitutionalContentIds.TemplePriestPerson, PrototypeOrganizationMembershipDefinitionFactory.TempleClergyMemberId, report);
            EnsureRank(memberships, priest, PrototypeOrganizationMembershipDefinitionFactory.TempleAcolyteRankId, "temple-priest-acolyte", report);
            EnsureRank(memberships, priest, PrototypeOrganizationMembershipDefinitionFactory.TemplePriestRankId, "temple-priest", report);

            OrganizationMembershipSnapshot headmaster = EnsureMembership(memberships, "organization-membership.prototype.university.headmaster", PrototypeInstitutionalContentIds.UniversityOrganization, PrototypeInstitutionalContentIds.UniversityHeadmasterPerson, PrototypeOrganizationMembershipDefinitionFactory.AcademicStaffMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.university.headmaster", PrototypeInstitutionalContentIds.UniversityOrganization, PrototypeOrganizationMembershipDefinitionFactory.UniversityHeadmasterOfficeId, "University Headmaster", headmaster, report);

            OrganizationMembershipSnapshot mayor = EnsureMembership(memberships, "organization-membership.prototype.civic.mayor", PrototypeInstitutionalContentIds.CivicOrganization, PrototypeInstitutionalContentIds.MayorPerson, PrototypeOrganizationMembershipDefinitionFactory.CivicOfficialMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.civic.mayor", PrototypeInstitutionalContentIds.CivicOrganization, PrototypeOrganizationMembershipDefinitionFactory.MayorOfficeId, "Mayor", mayor, report);
            OrganizationMembershipSnapshot magistrate = EnsureMembership(memberships, "organization-membership.prototype.civic.magistrate", PrototypeInstitutionalContentIds.CivicOrganization, PrototypeInstitutionalContentIds.MagistratePerson, PrototypeOrganizationMembershipDefinitionFactory.CivicOfficialMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.civic.magistrate", PrototypeInstitutionalContentIds.CivicOrganization, PrototypeOrganizationMembershipDefinitionFactory.MagistrateOfficeId, "Magistrate", magistrate, report);

            OrganizationMembershipSnapshot manorLord = EnsureMembership(memberships, "organization-membership.prototype.manor-administration.lord", PrototypeInstitutionalContentIds.ManorAdministrationOrganization, PrototypeInstitutionalContentIds.ManorLordPerson, PrototypeOrganizationMembershipDefinitionFactory.CivicOfficialMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.manor-administration.lord", PrototypeInstitutionalContentIds.ManorAdministrationOrganization, PrototypeOrganizationMembershipDefinitionFactory.ManorLordOfficeId, "Manor Lord", manorLord, report);
            OrganizationMembershipSnapshot duke = EnsureMembership(memberships, "organization-membership.prototype.duchy-administration.duke", PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, PrototypeInstitutionalContentIds.DukePerson, PrototypeOrganizationMembershipDefinitionFactory.CivicOfficialMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.duchy-administration.duke", PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, PrototypeOrganizationMembershipDefinitionFactory.DukeOfficeId, "Duke or Duchess", duke, report);
            OrganizationMembershipSnapshot monarch = EnsureMembership(memberships, "organization-membership.prototype.crown-administration.monarch", PrototypeInstitutionalContentIds.CrownAdministrationOrganization, PrototypeInstitutionalContentIds.MonarchPerson, PrototypeOrganizationMembershipDefinitionFactory.CivicOfficialMemberId, report);
            EnsureOffice(memberships, "organization-office-record.prototype.crown-administration.monarch", PrototypeInstitutionalContentIds.CrownAdministrationOrganization, PrototypeOrganizationMembershipDefinitionFactory.MonarchOfficeId, "King or Queen", monarch, report);

            OrganizationMembershipSnapshot guard = EnsureMembership(memberships, "organization-membership.prototype.city-guard.captain", PrototypeInstitutionalContentIds.CityGuardOrganization, PrototypeInstitutionalContentIds.GuardPerson, PrototypeOrganizationMembershipDefinitionFactory.MilitaryMemberId, report);
            EnsureRank(memberships, guard, PrototypeOrganizationMembershipDefinitionFactory.MilitaryRecruitRankId, "guard-recruit", report);
            EnsureRank(memberships, guard, PrototypeOrganizationMembershipDefinitionFactory.MilitarySergeantRankId, "guard-sergeant", report);
            EnsureRank(memberships, guard, PrototypeOrganizationMembershipDefinitionFactory.MilitaryCaptainRankId, "guard-captain", report);
            EnsureOffice(memberships, "organization-office-record.prototype.city-guard.captain", PrototypeInstitutionalContentIds.CityGuardOrganization, PrototypeOrganizationMembershipDefinitionFactory.GuardCaptainOfficeId, "Guard Captain", guard, report);

            EnsurePoliticalFoundation(governments, places, civicPlace, report);
            EnsureDynasticLineage(familyRelationships, report, worldTime);
            ReconcileAuthoredLaws(registry, laws, report, worldTime);
            EnsureGovernmentOrganizationCharters(registry, governments, laws, report, worldTime);
            EnsureCitizenship(laws, player, civicPlace, report);
            EnsureCourt(justice, civicPlace, report);
            EnsureCivicTreasury(resources, report);
            EnsureGovernmentAdministration(governments, report, worldTime);
            EnsureFiscalHierarchy(governments, report, worldTime);
            EnsurePrototypePermitEnforcement(governments, report, worldTime);
            return report;
        }

        private static OrganizationMembershipSnapshot EnsureMembership(OrganizationMembershipRuntime runtime, string id, string organizationId, string personId, string definitionId, PrototypeInstitutionalBootstrapReport report)
        {
            if (runtime.TryGetMembership(id, out OrganizationMembershipSnapshot existing)) return existing;
            OrganizationMembershipOperationResult result = runtime.ApplyMembership(new OrganizationMembershipRequest
            {
                transactionId = $"prototype.institution.seed.{id}", membershipId = id, organizationId = organizationId, personId = personId,
                membershipDefinitionId = definitionId, targetStatus = OrganizationMembershipStatus.Active, sourceKind = OrganizationMembershipSourceKind.WorldSetup,
                explicitConsent = true, worldTime = 0d, provenanceId = "prototype.institutional-bootstrap"
            });
            if (!result.Succeeded) report.Add($"Membership '{id}' was not seeded: {result.Message}");
            return result.Membership;
        }

        private static void EnsureRank(OrganizationMembershipRuntime runtime, OrganizationMembershipSnapshot membership, string rankId, string suffix, PrototypeInstitutionalBootstrapReport report)
        {
            if (membership == null || membership.RankAssignments.Any(item => item.rankDefinitionId == rankId)) return;
            OrganizationMembershipOperationResult result = runtime.AssignRank(new OrganizationRankAssignmentRequest
            {
                transactionId = $"prototype.institution.seed.rank.{suffix}", rankAssignmentId = $"organization-rank-assignment.prototype.{suffix}",
                membershipId = membership.MembershipId, rankDefinitionId = rankId, assignedById = "world.prototype", worldTime = 0d,
                provenanceId = "prototype.institutional-bootstrap"
            });
            if (!result.Succeeded) report.Add($"Rank '{rankId}' was not seeded: {result.Message}");
            runtime.TryGetMembership(membership.MembershipId, out membership);
        }

        private static void EnsureTopGuildRating(OrganizationMembershipRuntime runtime, OrganizationMembershipSnapshot membership, string suffix, PrototypeInstitutionalBootstrapReport report)
        {
            IReadOnlyList<string> rankIds = PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds;
            for (int index = 0; index < rankIds.Count; index++)
            {
                EnsureRank(runtime, membership, rankIds[index], $"{suffix}-{index + 1}", report);
                if (membership != null) runtime.TryGetMembership(membership.MembershipId, out membership);
            }
        }

        private static void EnsureOffice(OrganizationMembershipRuntime runtime, string officeId, string organizationId, string definitionId, string displayName, OrganizationMembershipSnapshot membership, PrototypeInstitutionalBootstrapReport report)
        {
            if (!runtime.TryGetOffice(officeId, out _))
            {
                OrganizationMembershipOperationResult created = runtime.CreateOffice(new OrganizationOfficeRequest { transactionId = $"prototype.institution.seed.{officeId}", officeId = officeId, organizationId = organizationId, officeDefinitionId = definitionId, displayName = displayName, worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" });
                if (!created.Succeeded) report.Add($"Office '{officeId}' was not seeded: {created.Message}");
            }
            if (membership == null || membership.OfficeAssignments.Any(item => item.officeId == officeId)) return;
            string assignmentId = officeId.Replace("organization-office-record.", "organization-office-assignment.", StringComparison.Ordinal);
            OrganizationMembershipOperationResult assigned = runtime.AssignOffice(new OrganizationOfficeAssignmentRequest { transactionId = $"prototype.institution.seed.assignment.{officeId}", officeAssignmentId = assignmentId, officeId = officeId, membershipId = membership.MembershipId, appointedById = "world.prototype", worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" });
            if (!assigned.Succeeded) report.Add($"Office assignment '{officeId}' was not seeded: {assigned.Message}");
        }

        private static void EnsureDynasticLineage(FamilyRelationshipRuntime runtime, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            bool exists = runtime.GetParents(PrototypeInstitutionalContentIds.DukePerson, ParentageKind.Biological, privileged: true)
                .Any(parent => parent.IncludesPerson(PrototypeInstitutionalContentIds.MonarchPerson));
            if (exists) return;
            FamilyRelationshipMutationResult result = runtime.RecordParentage(new FamilyParentageRequest
            {
                transactionId = "prototype.institution.seed.monarch-duke-lineage",
                recordId = "relationship-record.prototype.monarch-parent-of-duke",
                parentPersonId = PrototypeInstitutionalContentIds.MonarchPerson,
                childPersonId = PrototypeInstitutionalContentIds.DukePerson,
                parentageKind = ParentageKind.Biological,
                evidenceStatus = ParentageEvidenceStatus.Confirmed,
                visibility = FamilyVisibility.Public,
                sourceRecordId = "prototype-institutional-bootstrap",
                worldTime = worldTime
            });
            if (!result.Succeeded) report.Add($"Prototype royal lineage was not seeded: {result.Message}");
        }

        private static void EnsurePoliticalFoundation(GovernmentRuntime runtime, string[] places, string civicPlace, PrototypeInstitutionalBootstrapReport report)
        {
            Check(runtime.TryGetPolity(PrototypeInstitutionalContentIds.WorldPolity, out _) ? null : runtime.CreatePolity(new PolityCreateRequest { transactionId = "prototype.institution.seed.world-polity", polityId = PrototypeInstitutionalContentIds.WorldPolity, polityDefinitionId = PrototypeGovernmentDefinitionFactory.RepublicPolityDefinitionId, officialName = "Fundamental World Order", worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" }), "world polity", report);
            Check(runtime.TryGetGovernment(PrototypeInstitutionalContentIds.WorldGovernment, out _) ? null : runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "prototype.institution.seed.world-government", governmentId = PrototypeInstitutionalContentIds.WorldGovernment, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RepublicanCouncilDefinitionId, polityId = PrototypeInstitutionalContentIds.WorldPolity, officialName = "Fundamental World Authority", primaryGoverningOrganizationId = PrototypeLegalDefinitionFactory.WorldAuthorityOrganizationId, governingOrganizationIds = new[] { PrototypeLegalDefinitionFactory.WorldAuthorityOrganizationId }, level = GovernmentLevel.Central, worldTime = 0d }), "world government", report);
            Check(runtime.TryGetJurisdiction(PrototypeInstitutionalContentIds.WorldJurisdiction, out _) ? null : runtime.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = "prototype.institution.seed.world-jurisdiction", jurisdictionId = PrototypeInstitutionalContentIds.WorldJurisdiction, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = PrototypeInstitutionalContentIds.WorldGovernment, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.PublicOrder }, priority = 1000, conflictPolicy = JurisdictionConflictPolicy.HigherPriorityWins, worldTime = 0d }), "world jurisdiction", report);
            Check(runtime.TryGetPolity(PrototypeInstitutionalContentIds.RealmPolity, out _) ? null : runtime.CreatePolity(new PolityCreateRequest { transactionId = "prototype.institution.seed.realm-polity", polityId = PrototypeInstitutionalContentIds.RealmPolity, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Prototype Kingdom", capitalPlaceIds = string.IsNullOrEmpty(civicPlace) ? Array.Empty<string>() : new[] { civicPlace }, worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" }), "realm polity", report);
            Check(runtime.TryGetGovernment(PrototypeInstitutionalContentIds.KingdomGovernment, out _) ? null : runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "prototype.institution.seed.kingdom-government", governmentId = PrototypeInstitutionalContentIds.KingdomGovernment, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = PrototypeInstitutionalContentIds.RealmPolity, officialName = "Prototype Crown", primaryGoverningOrganizationId = PrototypeInstitutionalContentIds.CrownAdministrationOrganization, governingOrganizationIds = new[] { PrototypeInstitutionalContentIds.CrownAdministrationOrganization }, level = GovernmentLevel.Central, worldTime = 0d }), "kingdom government", report);
            Check(runtime.TryGetGovernment(PrototypeInstitutionalContentIds.DuchyGovernment, out _) ? null : runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "prototype.institution.seed.duchy-government", governmentId = PrototypeInstitutionalContentIds.DuchyGovernment, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.DucalAdministrationDefinitionId, polityId = PrototypeInstitutionalContentIds.RealmPolity, officialName = "Prototype Duchy Administration", primaryGoverningOrganizationId = PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, governingOrganizationIds = new[] { PrototypeInstitutionalContentIds.DuchyAdministrationOrganization }, parentGovernmentId = PrototypeInstitutionalContentIds.KingdomGovernment, level = GovernmentLevel.Provincial, worldTime = 0d }), "duchy government", report);
            Check(runtime.TryGetGovernment(PrototypeInstitutionalContentIds.ManorGovernment, out _) ? null : runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "prototype.institution.seed.manor-government", governmentId = PrototypeInstitutionalContentIds.ManorGovernment, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.ManorAdministrationDefinitionId, polityId = PrototypeInstitutionalContentIds.RealmPolity, officialName = "Prototype Manor Administration", primaryGoverningOrganizationId = PrototypeInstitutionalContentIds.ManorAdministrationOrganization, governingOrganizationIds = new[] { PrototypeInstitutionalContentIds.ManorAdministrationOrganization }, parentGovernmentId = PrototypeInstitutionalContentIds.DuchyGovernment, level = GovernmentLevel.County, worldTime = 0d }), "manor government", report);
            Check(runtime.TryGetGovernment(PrototypeInstitutionalContentIds.TownGovernment, out _) ? null : runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "prototype.institution.seed.government", governmentId = PrototypeInstitutionalContentIds.TownGovernment, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.TownAdministrationDefinitionId, polityId = PrototypeInstitutionalContentIds.RealmPolity, officialName = "Prototype Town Administration", primaryGoverningOrganizationId = PrototypeInstitutionalContentIds.CivicOrganization, governingOrganizationIds = new[] { PrototypeInstitutionalContentIds.CivicOrganization }, parentGovernmentId = PrototypeInstitutionalContentIds.ManorGovernment, level = GovernmentLevel.Municipal, worldTime = 0d }), "town government", report);
            Check(runtime.SetGovernmentParent(PrototypeInstitutionalContentIds.KingdomGovernment, string.Empty, "prototype.institution.align.kingdom-parent"), "kingdom hierarchy", report);
            Check(runtime.SetGovernmentParent(PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.KingdomGovernment, "prototype.institution.align.duchy-parent"), "duchy hierarchy", report);
            Check(runtime.SetGovernmentParent(PrototypeInstitutionalContentIds.ManorGovernment, PrototypeInstitutionalContentIds.DuchyGovernment, "prototype.institution.align.manor-parent"), "manor hierarchy", report);
            Check(runtime.SetGovernmentParent(PrototypeInstitutionalContentIds.TownGovernment, PrototypeInstitutionalContentIds.ManorGovernment, "prototype.institution.align.town-parent"), "town hierarchy", report);
            Check(runtime.SetGovernmentOrganizations(PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeInstitutionalContentIds.CrownAdministrationOrganization, new[] { PrototypeInstitutionalContentIds.CrownAdministrationOrganization }, "prototype.institution.align.kingdom-organization"), "kingdom administration", report);
            Check(runtime.SetGovernmentOrganizations(PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, new[] { PrototypeInstitutionalContentIds.DuchyAdministrationOrganization }, "prototype.institution.align.duchy-organization"), "duchy administration", report);
            Check(runtime.SetGovernmentOrganizations(PrototypeInstitutionalContentIds.ManorGovernment, PrototypeInstitutionalContentIds.ManorAdministrationOrganization, new[] { PrototypeInstitutionalContentIds.ManorAdministrationOrganization }, "prototype.institution.align.manor-organization"), "manor administration", report);
            Check(runtime.SetGovernmentOrganizations(PrototypeInstitutionalContentIds.TownGovernment, PrototypeInstitutionalContentIds.CivicOrganization, new[] { PrototypeInstitutionalContentIds.CivicOrganization }, "prototype.institution.align.town-organization"), "town administration", report);
            Check(runtime.TryGetTerritory(PrototypeInstitutionalContentIds.TownTerritory, out _) ? null : runtime.CreateTerritory(new TerritoryCreateRequest { transactionId = "prototype.institution.seed.territory", territoryId = PrototypeInstitutionalContentIds.TownTerritory, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.MunicipalityTerritoryDefinitionId, displayName = "Prototype Town Territory", polityId = PrototypeInstitutionalContentIds.TownPolity, primaryGovernmentId = PrototypeInstitutionalContentIds.TownGovernment, placeIds = places, worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" }), "town territory", report);
            Check(runtime.TryGetJurisdiction(PrototypeInstitutionalContentIds.TownJurisdiction, out _) ? null : runtime.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = "prototype.institution.seed.jurisdiction", jurisdictionId = PrototypeInstitutionalContentIds.TownJurisdiction, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.MunicipalJurisdictionDefinitionId, governmentId = PrototypeInstitutionalContentIds.TownGovernment, category = JurisdictionCategory.Municipal, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.Place | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.GeneralAdministration, JurisdictionSubjectMatter.PublicOrder, JurisdictionSubjectMatter.PropertyAdministration }, territoryIds = new[] { PrototypeInstitutionalContentIds.TownTerritory }, placeIds = places, priority = 100, conflictPolicy = JurisdictionConflictPolicy.HigherPriorityWins, worldTime = 0d }), "town jurisdiction", report);
        }

        public static void ReconcileAuthoredLaws(DefinitionRegistry registry, LegalRuntime runtime, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            IEnumerable<IGrouping<string, LawRuleDefinition>> instrumentGroups = registry.DefinitionsById.Values
                .OfType<LawRuleDefinition>()
                .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                .GroupBy(rule => rule.InstrumentId, StringComparer.Ordinal);
            foreach (IGrouping<string, LawRuleDefinition> group in instrumentGroups)
            {
                if (string.IsNullOrWhiteSpace(group.Key)) continue;
                LawRuleDefinition first = group.First();
                if (group.Any(rule => rule.InstrumentDefinitionId != first.InstrumentDefinitionId
                    || rule.AuthorityDefinitionId != first.AuthorityDefinitionId
                    || rule.IssuingGovernmentId != first.IssuingGovernmentId
                    || rule.IssuingOrganizationId != first.IssuingOrganizationId
                    || !rule.JurisdictionIds.SequenceEqual(first.JurisdictionIds)))
                {
                    report.Add($"Law instrument '{group.Key}' contains rules with inconsistent authority metadata.");
                    continue;
                }

                LegalProvisionCreateRequest[] provisions = group.Select(rule => BuildAuthoredProvision(rule, 0d)).ToArray();
                if (!runtime.TryGetInstrument(group.Key, out LegalInstrumentRecordData existingInstrument))
                {
                    LegalOperationResult result = runtime.Enact(new EnactLegalInstrumentRequest
                    {
                        transactionId = $"prototype.institution.seed.{group.Key}",
                        instrumentId = group.Key,
                        instrumentDefinitionId = first.InstrumentDefinitionId,
                        authorityDefinitionId = first.AuthorityDefinitionId,
                        title = first.InstrumentTitle,
                        shortTitle = first.InstrumentShortTitle,
                        citation = first.Citation,
                        governmentId = first.IssuingGovernmentId,
                        organizationId = first.IssuingOrganizationId,
                        officeId = first.IssuingOfficeId,
                        jurisdictionIds = first.JurisdictionIds.ToArray(),
                        provisions = provisions,
                        enactmentWorldTime = 0d,
                        publicationWorldTime = 0d,
                        effectiveWorldTime = 0d,
                        published = true,
                        promulgated = true,
                        visibility = PoliticalVisibility.Public,
                        provenanceId = "authored-law-bootstrap",
                        trustedSystemOperation = true
                    });
                    if (!result.Succeeded) report.Add($"Legal instrument '{group.Key}' was not seeded: {result.Message}");
                    continue;
                }

                if (existingInstrument.instrumentDefinitionId != first.InstrumentDefinitionId
                    || existingInstrument.authorityDefinitionId != first.AuthorityDefinitionId
                    || existingInstrument.governmentId != first.IssuingGovernmentId
                    || existingInstrument.organizationId != first.IssuingOrganizationId
                    || !PoliticalModelUtility.Clean(existingInstrument.jurisdictionIds).SequenceEqual(first.JurisdictionIds))
                {
                    report.Add($"Existing legal instrument '{group.Key}' does not match its authored authority metadata and requires an explicit successor instrument.");
                    continue;
                }

                foreach (LawRuleDefinition rule in group)
                {
                    LegalProvisionCreateRequest authored = BuildAuthoredProvision(rule, worldTime);
                    if (!runtime.TryGetProvision(rule.ProvisionId, out LegalProvisionRecordData existingProvision))
                    {
                        string signature = AuthoredProvisionSignature(rule);
                        LegalOperationResult added = runtime.AddProvision(new AddLegalProvisionRequest
                        {
                            transactionId = $"prototype.institution.add-law.{signature}",
                            amendmentId = $"legal-amendment.authored-add.{signature}",
                            instrumentId = group.Key,
                            effectiveWorldTime = Math.Max(0d, worldTime),
                            provision = authored,
                            trustedSystemOperation = true
                        });
                        if (!added.Succeeded) report.Add($"Authored provision '{rule.ProvisionId}' was not added: {added.Message}");
                        continue;
                    }

                    LegalProvisionVersionData current = existingProvision.VersionAt(Math.Max(worldTime, existingProvision.versions.Max(version => version.effectiveWorldTime)))
                        ?? existingProvision.versions.OrderByDescending(version => version.version).First();
                    if (Equivalent(current, authored.version)) continue;
                    string amendmentSignature = AuthoredProvisionSignature(rule);
                    double effectiveTime = Math.Max(Math.Max(0d, worldTime), current.effectiveWorldTime + 0.000001d);
                    LegalProvisionVersionData amendedVersion = authored.version.Clone();
                    amendedVersion.effectiveWorldTime = effectiveTime;
                    LegalOperationResult amended = runtime.AmendProvision(new AmendLegalProvisionRequest
                    {
                        transactionId = $"prototype.institution.amend-law.{amendmentSignature}",
                        amendmentId = $"legal-amendment.authored-sync.{amendmentSignature}",
                        provisionId = rule.ProvisionId,
                        effectiveWorldTime = effectiveTime,
                        version = amendedVersion,
                        trustedSystemOperation = true
                    });
                    if (!amended.Succeeded) report.Add($"Authored provision '{rule.ProvisionId}' was not synchronized: {amended.Message}");
                }
            }
        }

        public static void EnsureGovernmentOrganizationCharters(DefinitionRegistry registry, GovernmentRuntime governments, LegalRuntime laws, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            foreach (GovernmentOrganizationCharterDefinition definition in registry.DefinitionsById.Values.OfType<GovernmentOrganizationCharterDefinition>().OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                List<LegalProvisionCreateRequest> expected = new List<LegalProvisionCreateRequest>();
                int powerIndex = 0;
                foreach (string powerId in definition.GrantedPowerIds)
                {
                    expected.Add(CharterProvision(definition, powerId, LegalEffectCategory.Permission, PrototypeLegalDefinitionFactory.PermissionProvisionId, ++powerIndex));
                }
                int dutyIndex = 0;
                foreach (string dutyId in definition.DutyIds)
                {
                    expected.Add(CharterProvision(definition, dutyId, LegalEffectCategory.Duty, PrototypeLegalDefinitionFactory.DutyProvisionId, ++dutyIndex));
                }

                if (!laws.TryGetInstrument(definition.LegalInstrumentId, out LegalInstrumentRecordData instrument))
                {
                    LegalOperationResult enacted = laws.Enact(new EnactLegalInstrumentRequest
                    {
                        transactionId = $"prototype.institution.charter.enact.{definition.Id}",
                        instrumentId = definition.LegalInstrumentId,
                        instrumentDefinitionId = PrototypeLegalDefinitionFactory.CharterId,
                        authorityDefinitionId = PrototypeLegalDefinitionFactory.MunicipalAuthorityId,
                        title = definition.DisplayName,
                        shortTitle = definition.DisplayName,
                        citation = $"Charter: {definition.DisplayName}",
                        governmentId = definition.GovernmentId,
                        organizationId = definition.SupervisingOrganizationId,
                        jurisdictionIds = definition.JurisdictionIds.ToArray(),
                        provisions = expected.ToArray(),
                        enactmentWorldTime = worldTime,
                        publicationWorldTime = worldTime,
                        effectiveWorldTime = worldTime,
                        published = true,
                        promulgated = true,
                        visibility = definition.DefaultVisibility,
                        provenanceId = definition.Id,
                        trustedSystemOperation = true
                    });
                    if (!enacted.Succeeded) report.Add($"Organization charter law '{definition.LegalInstrumentId}' was not enacted: {enacted.Message}");
                }
                else
                {
                    HashSet<string> expectedIds = new HashSet<string>(expected.Select(item => item.provisionId), StringComparer.Ordinal);
                    foreach (LegalProvisionCreateRequest provision in expected)
                    {
                        if (laws.TryGetProvision(provision.provisionId, out _)) continue;
                        LegalOperationResult added = laws.AddProvision(new AddLegalProvisionRequest
                        {
                            transactionId = $"prototype.institution.charter.add.{provision.provisionId}",
                            instrumentId = definition.LegalInstrumentId,
                            provision = provision,
                            trustedSystemOperation = true
                        });
                        if (!added.Succeeded) report.Add($"Organization charter provision '{provision.provisionId}' was not added: {added.Message}");
                    }
                    foreach (string provisionId in instrument.provisionIds.Where(id => !expectedIds.Contains(id)))
                    {
                        if (!laws.TryGetProvision(provisionId, out LegalProvisionRecordData obsolete) || obsolete.lifecycleState == LegalProvisionLifecycleState.Repealed) continue;
                        LegalOperationResult repealed = laws.TransitionProvision(new LegalProvisionTransitionRequest
                        {
                            transactionId = $"prototype.institution.charter.repeal.{provisionId}",
                            provisionId = provisionId,
                            targetState = LegalProvisionLifecycleState.Repealed,
                            worldTime = worldTime,
                            trustedSystemOperation = true
                        });
                        if (!repealed.Succeeded) report.Add($"Obsolete organization charter provision '{provisionId}' was not repealed: {repealed.Message}");
                    }
                }

                string charterId = definition.Id.Replace("government-charter.", "government-charter-record.", StringComparison.Ordinal);
                if (!governments.TryGetOrganizationCharter(charterId, out GovernmentOrganizationCharterRecordData existingCharter))
                {
                    PoliticalOperationResult registered = governments.RegisterOrganizationCharter(new GovernmentOrganizationCharterRequest
                    {
                        transactionId = $"prototype.institution.charter.register.{definition.Id}",
                        charterId = charterId,
                        charterDefinitionId = definition.Id,
                        worldTime = worldTime,
                        provenanceId = definition.Id
                    });
                    if (!registered.Succeeded) report.Add($"Government organization charter '{charterId}' was not registered: {registered.Message}");
                }
                else if (!Equivalent(existingCharter, definition))
                {
                    PoliticalOperationResult synchronized = governments.SynchronizeOrganizationCharter(new GovernmentOrganizationCharterSynchronizationRequest
                    {
                        transactionId = $"prototype.institution.charter.sync.{CharterSignature(definition)}",
                        charterId = charterId,
                        charterDefinitionId = definition.Id,
                        provenanceId = definition.Id
                    });
                    if (!synchronized.Succeeded) report.Add($"Government organization charter '{charterId}' was not synchronized: {synchronized.Message}");
                }
            }
        }

        private static bool Equivalent(GovernmentOrganizationCharterRecordData record, GovernmentOrganizationCharterDefinition definition)
        {
            return record != null && definition != null
                && record.charterDefinitionId == definition.Id
                && record.governmentId == definition.GovernmentId
                && record.organizationId == definition.OrganizationId
                && record.supervisingOrganizationId == definition.SupervisingOrganizationId
                && record.legalInstrumentId == definition.LegalInstrumentId
                && PoliticalModelUtility.Clean(record.jurisdictionIds).SequenceEqual(definition.JurisdictionIds)
                && PoliticalModelUtility.Clean(record.grantedPowerIds).SequenceEqual(definition.GrantedPowerIds)
                && PoliticalModelUtility.Clean(record.dutyIds).SequenceEqual(definition.DutyIds)
                && record.category == definition.Category
                && record.revocable == definition.Revocable
                && record.visibility == definition.DefaultVisibility;
        }

        private static string CharterSignature(GovernmentOrganizationCharterDefinition definition)
        {
            string source = string.Join("|", new[]
            {
                definition.Id, definition.Version.ToString(), definition.Category.ToString(), definition.GovernmentId,
                definition.OrganizationId, definition.SupervisingOrganizationId, definition.LegalInstrumentId,
                string.Join(",", definition.JurisdictionIds), string.Join(",", definition.GrantedPowerIds),
                string.Join(",", definition.DutyIds), definition.Revocable.ToString(), definition.DefaultVisibility.ToString()
            });
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char character in source) { hash ^= character; hash *= 1099511628211UL; }
                return $"{NormalizeId(definition.Id)}.{hash:x16}";
            }
        }

        private static LegalProvisionCreateRequest CharterProvision(GovernmentOrganizationCharterDefinition definition, string actionId, LegalEffectCategory effect, string provisionDefinitionId, int index)
        {
            string kind = effect == LegalEffectCategory.Permission ? "power" : "duty";
            return new LegalProvisionCreateRequest
            {
                provisionId = $"{definition.LegalInstrumentId}.{kind}.{index:00}",
                provisionDefinitionId = provisionDefinitionId,
                citation = $"{definition.DisplayName} {kind} {index}",
                version = new LegalProvisionVersionData
                {
                    version = 1,
                    effect = effect,
                    actionId = actionId,
                    subjectMatterId = "organization.charter",
                    organizationIds = new[] { definition.OrganizationId },
                    effectiveWorldTime = 0d,
                    provenanceId = definition.Id
                }
            };
        }

        public static bool NeedsAuthoredLawReconciliation(DefinitionRegistry registry, LegalRuntime runtime)
        {
            if (registry == null || runtime == null) return false;
            foreach (LawRuleDefinition rule in registry.DefinitionsById.Values.OfType<LawRuleDefinition>())
            {
                if (!runtime.TryGetInstrument(rule.InstrumentId, out _)
                    || !runtime.TryGetProvision(rule.ProvisionId, out LegalProvisionRecordData provision)) return true;
                LegalProvisionVersionData current = provision.versions?.OrderByDescending(version => version.version).FirstOrDefault();
                if (!Equivalent(current, BuildAuthoredProvision(rule, current?.effectiveWorldTime ?? 0d).version)) return true;
            }
            return false;
        }

        private static LegalProvisionCreateRequest BuildAuthoredProvision(LawRuleDefinition rule, double effectiveWorldTime)
        {
            return new LegalProvisionCreateRequest
            {
                provisionId = rule.ProvisionId,
                provisionDefinitionId = rule.Effect switch
                {
                    LegalEffectCategory.Right => PrototypeLegalDefinitionFactory.RightProvisionId,
                    LegalEffectCategory.Permission => PrototypeLegalDefinitionFactory.PermissionProvisionId,
                    LegalEffectCategory.Duty => PrototypeLegalDefinitionFactory.DutyProvisionId,
                    LegalEffectCategory.Exemption => PrototypeLegalDefinitionFactory.ExemptionProvisionId,
                    LegalEffectCategory.Immunity => PrototypeLegalDefinitionFactory.ImmunityProvisionId,
                    _ => PrototypeLegalDefinitionFactory.ProhibitionProvisionId
                },
                citation = rule.Citation,
                version = new LegalProvisionVersionData
                {
                    effect = rule.Effect,
                    actionId = rule.ActionId,
                    subjectMatterId = rule.SubjectMatterId,
                    organizationIds = rule.OrganizationIds.ToArray(),
                    territoryIds = rule.TerritoryIds.ToArray(),
                    placeIds = rule.PlaceIds.ToArray(),
                    propertyIds = rule.PropertyIds.ToArray(),
                    effectiveWorldTime = Math.Max(0d, effectiveWorldTime),
                    provenanceId = rule.Id
                }
            };
        }

        private static bool Equivalent(LegalProvisionVersionData left, LegalProvisionVersionData right)
        {
            if (left == null || right == null) return false;
            return left.effect == right.effect
                && string.Equals(left.actionId, right.actionId, StringComparison.Ordinal)
                && string.Equals(left.subjectMatterId, right.subjectMatterId, StringComparison.Ordinal)
                && PoliticalModelUtility.Clean(left.organizationIds).SequenceEqual(PoliticalModelUtility.Clean(right.organizationIds))
                && PoliticalModelUtility.Clean(left.territoryIds).SequenceEqual(PoliticalModelUtility.Clean(right.territoryIds))
                && PoliticalModelUtility.Clean(left.placeIds).SequenceEqual(PoliticalModelUtility.Clean(right.placeIds))
                && PoliticalModelUtility.Clean(left.propertyIds).SequenceEqual(PoliticalModelUtility.Clean(right.propertyIds));
        }

        private static string AuthoredProvisionSignature(LawRuleDefinition rule)
        {
            string source = string.Join("|", new[]
            {
                rule.Id, rule.Version.ToString(), rule.Effect.ToString(), rule.ActionId, rule.SubjectMatterId,
                string.Join(",", rule.OrganizationIds), string.Join(",", rule.TerritoryIds), string.Join(",", rule.PlaceIds), string.Join(",", rule.PropertyIds)
            });
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char character in source) { hash ^= character; hash *= 1099511628211UL; }
                return $"{NormalizeId(rule.Id)}.{hash:x16}";
            }
        }

        private static string NormalizeId(string value) => new string((value ?? string.Empty).ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-').ToArray());

        private static void EnsureCitizenship(LegalRuntime runtime, string player, string residencePlace, PrototypeInstitutionalBootstrapReport report)
        {
            string id = $"legal-status.prototype.citizen.{player}";
            if (runtime.TryGetStatus(id, out _)) return;
            LegalOperationResult result = runtime.GrantLegalStatus(new LegalStatusGrantRequest { transactionId = $"prototype.institution.seed.citizenship.{player}", statusId = id, statusDefinitionId = PrototypeLegalDefinitionFactory.CitizenStatusId, citizenshipDefinitionId = PrototypeLegalDefinitionFactory.CitizenshipId, personId = player, polityId = PrototypeInstitutionalContentIds.TownPolity, recognizingGovernmentId = PrototypeInstitutionalContentIds.TownGovernment, residencePlaceId = residencePlace, acquisitionRoute = CitizenshipAcquisitionRoute.Birth, consentGiven = true, effectiveWorldTime = 0d, trustedSystemOperation = true, provenanceId = "prototype.institutional-bootstrap" });
            if (!result.Succeeded) report.Add($"Player citizenship was not seeded: {result.Message}");
        }

        private static void EnsureCourt(JusticeRuntime runtime, string civicPlace, PrototypeInstitutionalBootstrapReport report)
        {
            if (runtime.TryGetCourt(PrototypeInstitutionalContentIds.TownCourt, out _)) return;
            JusticeOperationResult result = runtime.RegisterCourt(new CourtRegisterRequest { transactionId = "prototype.institution.seed.court", courtId = PrototypeInstitutionalContentIds.TownCourt, courtDefinitionId = PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId, justiceInstitutionDefinitionId = PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId, governmentId = PrototypeInstitutionalContentIds.TownGovernment, organizationId = PrototypeInstitutionalContentIds.CivicOrganization, jurisdictionIds = new[] { PrototypeInstitutionalContentIds.TownJurisdiction }, territoryIds = new[] { PrototypeInstitutionalContentIds.TownTerritory }, courthousePlaceId = civicPlace, judgeOfficeIds = new[] { "organization-office-record.prototype.civic.magistrate" }, clerkOfficeIds = Array.Empty<string>(), worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" });
            if (!result.Succeeded) report.Add($"Town court was not seeded: {result.Message}");
        }

        private static void EnsureCivicTreasury(OrganizationResourceRuntime runtime, PrototypeInstitutionalBootstrapReport report)
        {
            if (!runtime.TryGetTreasury(PrototypeInstitutionalContentIds.CivicTreasury, out _))
            {
                OrganizationResourceOperationResult treasury = runtime.CreateTreasury(new OrganizationTreasuryRequest { transactionId = "prototype.institution.seed.civic-treasury", treasuryId = PrototypeInstitutionalContentIds.CivicTreasury, organizationId = PrototypeInstitutionalContentIds.CivicOrganization, resourceTypeDefinitionId = PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId, officialName = "Prototype Civic Treasury", actorPersonId = PrototypeInstitutionalContentIds.MayorPerson, worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" });
                if (!treasury.Succeeded) report.Add($"Civic treasury was not seeded: {treasury.Message}");
            }
            if (!runtime.TryGetAccount(PrototypeInstitutionalContentIds.CivicAccount, out _))
            {
                OrganizationResourceOperationResult account = runtime.CreateAccount(new OrganizationAccountRequest { transactionId = "prototype.institution.seed.civic-account", accountId = PrototypeInstitutionalContentIds.CivicAccount, treasuryId = PrototypeInstitutionalContentIds.CivicTreasury, organizationId = PrototypeInstitutionalContentIds.CivicOrganization, economyAccountId = PrototypeEconomyContentIds.TreasuryAccount, officialName = "Prototype Civic General Fund", currencyDefinitionId = PrototypeEconomyContentIds.CurrencyGold, openingBalanceUnits = 0L, actorPersonId = PrototypeInstitutionalContentIds.MayorPerson, worldTime = 0d, provenanceId = "prototype.institutional-bootstrap" });
                if (!account.Succeeded) report.Add($"Civic treasury account was not seeded: {account.Message}");
            }
            if (!runtime.Budgets.Any(value => value.budgetId == PrototypeInstitutionalContentIds.CivicBudget))
            {
                OrganizationResourceOperationResult budget = runtime.CreateBudget(new OrganizationBudgetRequest
                {
                    transactionId = "prototype.institution.seed.civic-budget", budgetId = PrototypeInstitutionalContentIds.CivicBudget,
                    organizationId = PrototypeInstitutionalContentIds.CivicOrganization, treasuryId = PrototypeInstitutionalContentIds.CivicTreasury,
                    accountId = PrototypeInstitutionalContentIds.CivicAccount, category = OrganizationBudgetCategory.GeneralOperations,
                    enforcementPolicy = OrganizationBudgetEnforcementPolicy.WarnWhenExceeded, currencyDefinitionId = PrototypeEconomyContentIds.CurrencyGold,
                    authorizedUnits = 1000000L, purpose = "Town administration, services, public order, and emergency reserves",
                    fundingSourceId = PrototypeEconomyContentIds.RevenueSalesTax, sourceAuthorityId = "government-office-record.prototype.civic.mayor",
                    actorPersonId = PrototypeInstitutionalContentIds.MayorPerson, startWorldTime = 0d, endWorldTime = 31536000d, provenanceId = "prototype.institutional-bootstrap"
                });
                if (!budget.Succeeded) report.Add($"Civic budget was not seeded: {budget.Message}");
            }
            EnsureTierTreasury(runtime, PrototypeInstitutionalContentIds.ManorTreasury, PrototypeInstitutionalContentIds.ManorAccount, PrototypeInstitutionalContentIds.ManorEconomyAccount, PrototypeInstitutionalContentIds.ManorBudget, PrototypeInstitutionalContentIds.ManorAdministrationOrganization, PrototypeInstitutionalContentIds.ManorLordPerson, "organization-office-record.prototype.manor-administration.lord", "government-remittance-policy.prototype.town-to-manor", "Manor", report);
            EnsureTierTreasury(runtime, PrototypeInstitutionalContentIds.DuchyTreasury, PrototypeInstitutionalContentIds.DuchyAccount, PrototypeInstitutionalContentIds.DuchyEconomyAccount, PrototypeInstitutionalContentIds.DuchyBudget, PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, PrototypeInstitutionalContentIds.DukePerson, "organization-office-record.prototype.duchy-administration.duke", "government-remittance-policy.prototype.manor-to-duchy", "Duchy", report);
            EnsureTierTreasury(runtime, PrototypeInstitutionalContentIds.KingdomTreasury, PrototypeInstitutionalContentIds.KingdomAccount, PrototypeInstitutionalContentIds.KingdomEconomyAccount, PrototypeInstitutionalContentIds.KingdomBudget, PrototypeInstitutionalContentIds.CrownAdministrationOrganization, PrototypeInstitutionalContentIds.MonarchPerson, "organization-office-record.prototype.crown-administration.monarch", "government-remittance-policy.prototype.duchy-to-kingdom", "Kingdom", report);
        }

        private static void EnsureTierTreasury(OrganizationResourceRuntime runtime, string treasuryId, string accountId, string economyAccountId, string budgetId, string organizationId, string actorPersonId, string sourceAuthorityId, string fundingSourceId, string label, PrototypeInstitutionalBootstrapReport report)
        {
            if (!runtime.TryGetTreasury(treasuryId, out _))
            {
                OrganizationResourceOperationResult treasury = runtime.CreateTreasury(new OrganizationTreasuryRequest { transactionId = $"prototype.institution.seed.{label.ToLowerInvariant()}-treasury", treasuryId = treasuryId, organizationId = organizationId, resourceTypeDefinitionId = PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId, officialName = $"Prototype {label} Treasury", actorPersonId = actorPersonId, worldTime = 0d, provenanceId = "prototype-institutional-bootstrap" });
                if (!treasury.Succeeded) report.Add($"{label} treasury was not seeded: {treasury.Message}");
            }
            if (!runtime.TryGetAccount(accountId, out _))
            {
                OrganizationResourceOperationResult account = runtime.CreateAccount(new OrganizationAccountRequest { transactionId = $"prototype.institution.seed.{label.ToLowerInvariant()}-account", accountId = accountId, treasuryId = treasuryId, organizationId = organizationId, economyAccountId = economyAccountId, officialName = $"Prototype {label} General Fund", currencyDefinitionId = PrototypeEconomyContentIds.CurrencyGold, actorPersonId = actorPersonId, worldTime = 0d, provenanceId = "prototype-institutional-bootstrap" });
                if (!account.Succeeded) report.Add($"{label} treasury account was not seeded: {account.Message}");
            }
            if (!runtime.Budgets.Any(value => value.budgetId == budgetId))
            {
                OrganizationResourceOperationResult budget = runtime.CreateBudget(new OrganizationBudgetRequest { transactionId = $"prototype.institution.seed.{label.ToLowerInvariant()}-budget", budgetId = budgetId, organizationId = organizationId, treasuryId = treasuryId, accountId = accountId, category = OrganizationBudgetCategory.GeneralOperations, enforcementPolicy = OrganizationBudgetEnforcementPolicy.WarnWhenExceeded, currencyDefinitionId = PrototypeEconomyContentIds.CurrencyGold, authorizedUnits = 1000000L, purpose = $"{label} administration and reserves", fundingSourceId = fundingSourceId, sourceAuthorityId = sourceAuthorityId, actorPersonId = actorPersonId, startWorldTime = 0d, endWorldTime = 31536000d, provenanceId = "prototype-institutional-bootstrap" });
                if (!budget.Succeeded) report.Add($"{label} budget was not seeded: {budget.Message}");
            }
        }

        private static void EnsureFiscalHierarchy(GovernmentRuntime runtime, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            EnsureRemittance(runtime, "government-remittance-policy.prototype.town-to-manor", PrototypeInstitutionalContentIds.TownGovernment, PrototypeInstitutionalContentIds.ManorGovernment, PrototypeEconomyContentIds.TreasuryAccount, PrototypeInstitutionalContentIds.ManorEconomyAccount, new[] { PrototypeEconomyContentIds.RevenueSalesTax }, "government-office-tenure.prototype.mayor", report, worldTime);
            EnsureRemittance(runtime, "government-remittance-policy.prototype.manor-to-duchy", PrototypeInstitutionalContentIds.ManorGovernment, PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.ManorEconomyAccount, PrototypeInstitutionalContentIds.DuchyEconomyAccount, Array.Empty<string>(), "government-office-tenure.prototype.manor-lord", report, worldTime);
            EnsureRemittance(runtime, "government-remittance-policy.prototype.duchy-to-kingdom", PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeInstitutionalContentIds.DuchyEconomyAccount, PrototypeInstitutionalContentIds.KingdomEconomyAccount, Array.Empty<string>(), "government-office-tenure.prototype.duke", report, worldTime);
            EnsureBudgetCycle(runtime, "government-budget-cycle.prototype.town", PrototypeInstitutionalContentIds.TownGovernment, PrototypeInstitutionalContentIds.CivicOrganization, PrototypeInstitutionalContentIds.CivicTreasury, PrototypeInstitutionalContentIds.CivicAccount, PrototypeInstitutionalContentIds.CivicBudget, PrototypeEconomyContentIds.RevenueSalesTax, PrototypeInstitutionalContentIds.MayorPerson, "government-office-tenure.prototype.mayor", report);
            EnsureBudgetCycle(runtime, "government-budget-cycle.prototype.manor", PrototypeInstitutionalContentIds.ManorGovernment, PrototypeInstitutionalContentIds.ManorAdministrationOrganization, PrototypeInstitutionalContentIds.ManorTreasury, PrototypeInstitutionalContentIds.ManorAccount, PrototypeInstitutionalContentIds.ManorBudget, "government-remittance-policy.prototype.town-to-manor", PrototypeInstitutionalContentIds.ManorLordPerson, "government-office-tenure.prototype.manor-lord", report);
            EnsureBudgetCycle(runtime, "government-budget-cycle.prototype.duchy", PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, PrototypeInstitutionalContentIds.DuchyTreasury, PrototypeInstitutionalContentIds.DuchyAccount, PrototypeInstitutionalContentIds.DuchyBudget, "government-remittance-policy.prototype.manor-to-duchy", PrototypeInstitutionalContentIds.DukePerson, "government-office-tenure.prototype.duke", report);
            EnsureBudgetCycle(runtime, "government-budget-cycle.prototype.kingdom", PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeInstitutionalContentIds.CrownAdministrationOrganization, PrototypeInstitutionalContentIds.KingdomTreasury, PrototypeInstitutionalContentIds.KingdomAccount, PrototypeInstitutionalContentIds.KingdomBudget, "government-remittance-policy.prototype.duchy-to-kingdom", PrototypeInstitutionalContentIds.MonarchPerson, "government-office-tenure.prototype.monarch", report);
        }

        private static void EnsureBudgetCycle(GovernmentRuntime runtime, string cycleId, string governmentId, string organizationId, string treasuryId, string accountId, string currentBudgetId, string fundingSourceId, string actorPersonId, string sourceAuthorityId, PrototypeInstitutionalBootstrapReport report)
        {
            if (runtime.BudgetCycles.Any(value => value.cycleId == cycleId)) return;
            Check(runtime.RegisterBudgetCycle(new GovernmentBudgetCycleRequest
            {
                transactionId = $"prototype.institution.seed.{cycleId}", cycleId = cycleId, governmentId = governmentId,
                organizationId = organizationId, treasuryId = treasuryId, accountId = accountId,
                currencyDefinitionId = PrototypeEconomyContentIds.CurrencyGold, budgetIdPrefix = currentBudgetId,
                currentBudgetId = currentBudgetId, category = OrganizationBudgetCategory.GeneralOperations,
                enforcementPolicy = OrganizationBudgetEnforcementPolicy.WarnWhenExceeded, authorizedUnits = 1000000L,
                purpose = "Government administration, public services, public order, and reserves", fundingSourceId = fundingSourceId,
                actorPersonId = actorPersonId, sourceAuthorityId = sourceAuthorityId,
                cycleDurationWorldTime = 31536000d, nextCycleWorldTime = 31536000d,
                provenanceId = "prototype-institutional-bootstrap"
            }), $"budget cycle '{cycleId}'", report);
        }

        private static void EnsureRemittance(GovernmentRuntime runtime, string policyId, string sourceGovernmentId, string destinationGovernmentId, string sourceAccountId, string destinationAccountId, string[] revenueIds, string sourceAuthorityId, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            if (runtime.RemittancePolicies.Any(value => value.policyId == policyId)) return;
            Check(runtime.EnactRemittancePolicy(new GovernmentRemittancePolicyRequest
            {
                transactionId = $"prototype.institution.seed.{policyId}", policyId = policyId,
                sourceGovernmentId = sourceGovernmentId, destinationGovernmentId = destinationGovernmentId,
                sourceEconomyAccountId = sourceAccountId, destinationEconomyAccountId = destinationAccountId,
                currencyDefinitionId = PrototypeEconomyContentIds.CurrencyGold, eligibleRevenueDefinitionIds = revenueIds,
                remittanceBasisPoints = 2500, fixedTributeUnits = 0L, minimumReserveUnits = 0L,
                settlementIntervalWorldTime = 86400d, firstSettlementWorldTime = worldTime + 86400d,
                gracePeriodWorldTime = 86400d, sourceAuthorityId = sourceAuthorityId,
                provenanceId = "prototype-institutional-bootstrap"
            }), $"remittance policy '{policyId}'", report);
        }

        private static void EnsurePrototypePermitEnforcement(GovernmentRuntime runtime, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            EnsurePermit(runtime, "government-permit.prototype.market-business.trade", PrototypeGovernmentDefinitionFactory.TradeLicenseDefinitionId, report, worldTime);
            EnsurePermit(runtime, "government-permit.prototype.market-business.craft", PrototypeGovernmentDefinitionFactory.CraftLicenseDefinitionId, report, worldTime);
            EnsureRegulation(runtime, "government-regulation.prototype.business-trade", "government.action.conduct-regulated-trade", PrototypeGovernmentDefinitionFactory.TradeLicenseDefinitionId, report, worldTime);
            EnsureRegulation(runtime, "government-regulation.prototype.business-workshop", "government.action.operate-regulated-workshop", PrototypeGovernmentDefinitionFactory.CraftLicenseDefinitionId, report, worldTime);
        }

        private static void EnsurePermit(GovernmentRuntime runtime, string permitId, string definitionId, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            if (runtime.TryGetPermit(permitId, out _)) return;
            Check(runtime.IssuePermit(new GovernmentPermitIssueRequest { transactionId = $"prototype.institution.seed.{permitId}", permitId = permitId, permitDefinitionId = definitionId, governmentId = PrototypeInstitutionalContentIds.TownGovernment, jurisdictionId = PrototypeInstitutionalContentIds.TownJurisdiction, holderCategory = GovernmentPermitHolderCategory.Business, holderId = PrototypeEconomyContentIds.BusinessInstance, issuedByPersonId = PrototypeInstitutionalContentIds.MayorPerson, sourceAuthorityId = "government-office-tenure.prototype.mayor", worldTime = worldTime, effectiveWorldTime = worldTime, provenanceId = "prototype-institutional-bootstrap" }), $"permit '{permitId}'", report);
        }

        private static void EnsureRegulation(GovernmentRuntime runtime, string regulationId, string actionId, string permitDefinitionId, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            if (runtime.ActionRegulations.Any(value => value.regulationId == regulationId)) return;
            Check(runtime.RegisterActionRegulation(new GovernmentActionRegulationRequest { transactionId = $"prototype.institution.seed.{regulationId}", regulationId = regulationId, governmentId = PrototypeInstitutionalContentIds.TownGovernment, jurisdictionId = PrototypeInstitutionalContentIds.TownJurisdiction, actionId = actionId, requiredPermitDefinitionId = permitDefinitionId, regulatedHolderCategories = new[] { GovernmentPermitHolderCategory.Business }, effectiveWorldTime = worldTime, provenanceId = "prototype-institutional-bootstrap" }), $"regulation '{regulationId}'", report);
        }

        private static void EnsureGovernmentAdministration(GovernmentRuntime runtime, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            EnsureTenure(runtime, "government-office-tenure.prototype.monarch", PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeGovernmentDefinitionFactory.MonarchOfficeDefinitionId, "organization-office-record.prototype.crown-administration.monarch", "organization-office-assignment.prototype.crown-administration.monarch", PrototypeInstitutionalContentIds.MonarchPerson, GovernmentOfficeSelectionMethod.HereditarySuccession, "custom.prototype.royal-succession", "relationship-record.prototype.monarch-parent-of-duke", string.Empty, report, worldTime);
            EnsureTenure(runtime, "government-office-tenure.prototype.duke", PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeGovernmentDefinitionFactory.DukeOfficeDefinitionId, "organization-office-record.prototype.duchy-administration.duke", "organization-office-assignment.prototype.duchy-administration.duke", PrototypeInstitutionalContentIds.DukePerson, GovernmentOfficeSelectionMethod.HereditarySuccession, "government-office-tenure.prototype.monarch", "government-office-tenure.prototype.monarch", PrototypeInstitutionalContentIds.MonarchPerson, report, worldTime);
            EnsureTenure(runtime, "government-office-tenure.prototype.manor-lord", PrototypeInstitutionalContentIds.ManorGovernment, PrototypeGovernmentDefinitionFactory.ManorLordOfficeDefinitionId, "organization-office-record.prototype.manor-administration.lord", "organization-office-assignment.prototype.manor-administration.lord", PrototypeInstitutionalContentIds.ManorLordPerson, GovernmentOfficeSelectionMethod.SovereignAppointment, "government-office-tenure.prototype.duke", "government-office-tenure.prototype.duke", string.Empty, report, worldTime);
            EnsureTenure(runtime, "government-office-tenure.prototype.mayor", PrototypeInstitutionalContentIds.TownGovernment, PrototypeGovernmentDefinitionFactory.MayorGovernmentOfficeDefinitionId, "organization-office-record.prototype.civic.mayor", "organization-office-assignment.prototype.civic.mayor", PrototypeInstitutionalContentIds.MayorPerson, GovernmentOfficeSelectionMethod.SovereignAppointment, "government-office-tenure.prototype.manor-lord", "legal-instrument.prototype.mayoral-charter", string.Empty, report, worldTime);
            EnsureTenure(runtime, "government-office-tenure.prototype.magistrate", PrototypeInstitutionalContentIds.TownGovernment, PrototypeGovernmentDefinitionFactory.MagistrateGovernmentOfficeDefinitionId, "organization-office-record.prototype.civic.magistrate", "organization-office-assignment.prototype.civic.magistrate", PrototypeInstitutionalContentIds.MagistratePerson, GovernmentOfficeSelectionMethod.SovereignAppointment, PrototypeInstitutionalContentIds.MayorPerson, "government-office-tenure.prototype.mayor", string.Empty, report, worldTime);
            EnsureTenure(runtime, "government-office-tenure.prototype.guard-captain", PrototypeInstitutionalContentIds.TownGovernment, PrototypeGovernmentDefinitionFactory.GuardCaptainGovernmentOfficeDefinitionId, "organization-office-record.prototype.city-guard.captain", "organization-office-assignment.prototype.city-guard.captain", PrototypeInstitutionalContentIds.GuardPerson, GovernmentOfficeSelectionMethod.InternalPromotion, PrototypeInstitutionalContentIds.CityGuardOrganization, "government-office-tenure.prototype.mayor", string.Empty, report, worldTime);

            if (!runtime.TryGetFiscalMandate("government-fiscal-mandate.prototype.town", out _))
            {
                Check(runtime.EnactFiscalMandate(new GovernmentFiscalMandateRequest
                {
                    transactionId = "prototype.institution.seed.fiscal-mandate", mandateId = "government-fiscal-mandate.prototype.town",
                    fiscalPolicyDefinitionId = PrototypeGovernmentDefinitionFactory.PrototypeTownFiscalPolicyId, governmentId = PrototypeInstitutionalContentIds.TownGovernment,
                    organizationBudgetId = PrototypeInstitutionalContentIds.CivicBudget, sourceAuthorityId = "government-office-tenure.prototype.mayor",
                    worldTime = worldTime, provenanceId = "prototype.institutional-bootstrap"
                }), "town fiscal mandate", report);
            }
            EnsureFiscalMandate(runtime, "government-fiscal-mandate.prototype.manor", PrototypeGovernmentDefinitionFactory.PrototypeManorFiscalPolicyId, PrototypeInstitutionalContentIds.ManorGovernment, PrototypeInstitutionalContentIds.ManorBudget, "government-office-tenure.prototype.manor-lord", report, worldTime);
            EnsureFiscalMandate(runtime, "government-fiscal-mandate.prototype.duchy", PrototypeGovernmentDefinitionFactory.PrototypeDuchyFiscalPolicyId, PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.DuchyBudget, "government-office-tenure.prototype.duke", report, worldTime);
            EnsureFiscalMandate(runtime, "government-fiscal-mandate.prototype.kingdom", PrototypeGovernmentDefinitionFactory.PrototypeKingdomFiscalPolicyId, PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeInstitutionalContentIds.KingdomBudget, "government-office-tenure.prototype.monarch", report, worldTime);

            if (!runtime.TryGetLegitimacyAssessment("government-legitimacy.prototype.town.initial", out _))
            {
                Check(runtime.AssessLegitimacy(new GovernmentLegitimacyAssessmentRequest
                {
                    transactionId = "prototype.institution.seed.legitimacy", assessmentId = "government-legitimacy.prototype.town.initial",
                    governmentId = PrototypeInstitutionalContentIds.TownGovernment, policyDefinitionId = PrototypeGovernmentDefinitionFactory.TownAdministrationLegitimacyPolicyId,
                    components = new[]
                    {
                        Score(GovernmentLegitimacyComponent.SovereignRecognition, 8500, PrototypeInstitutionalContentIds.ManorGovernment),
                        Score(GovernmentLegitimacyComponent.OathsAndCustom, 7200, PrototypeInstitutionalContentIds.CivicOrganization),
                        Score(GovernmentLegitimacyComponent.TerritorialControl, 9000, PrototypeInstitutionalContentIds.TownTerritory),
                        Score(GovernmentLegitimacyComponent.InstitutionalSupport, 7800, PrototypeInstitutionalContentIds.CivicOrganization),
                        Score(GovernmentLegitimacyComponent.PublicSupport, 7300, PrototypeInstitutionalContentIds.TownPolity),
                        Score(GovernmentLegitimacyComponent.AdministrativePerformance, 7000, PrototypeInstitutionalContentIds.TownGovernment)
                    },
                    worldTime = worldTime, assessorId = PrototypeInstitutionalContentIds.MayorPerson, provenanceId = "prototype.institutional-bootstrap"
                }), "town legitimacy", report);
            }
            EnsureHierarchyLegitimacy(runtime, "government-legitimacy.prototype.manor.initial", PrototypeInstitutionalContentIds.ManorGovernment, PrototypeInstitutionalContentIds.ManorLordPerson, PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.ManorAdministrationOrganization, 7600, 8800, report, worldTime);
            EnsureHierarchyLegitimacy(runtime, "government-legitimacy.prototype.duchy.initial", PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeInstitutionalContentIds.DukePerson, PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeInstitutionalContentIds.DuchyAdministrationOrganization, 9000, 9000, report, worldTime);
            EnsureHierarchyLegitimacy(runtime, "government-legitimacy.prototype.kingdom.initial", PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeInstitutionalContentIds.MonarchPerson, PrototypeInstitutionalContentIds.WorldGovernment, PrototypeInstitutionalContentIds.CrownAdministrationOrganization, 9200, 8500, report, worldTime);
        }

        private static void EnsureFiscalMandate(GovernmentRuntime runtime, string mandateId, string policyDefinitionId, string governmentId, string budgetId, string sourceAuthorityId, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            if (runtime.TryGetFiscalMandate(mandateId, out _)) return;
            Check(runtime.EnactFiscalMandate(new GovernmentFiscalMandateRequest
            {
                transactionId = $"prototype.institution.seed.{mandateId}", mandateId = mandateId,
                fiscalPolicyDefinitionId = policyDefinitionId, governmentId = governmentId,
                organizationBudgetId = budgetId, sourceAuthorityId = sourceAuthorityId,
                worldTime = worldTime, provenanceId = "prototype.institutional-bootstrap"
            }), mandateId, report);
        }

        private static void EnsureHierarchyLegitimacy(GovernmentRuntime runtime, string assessmentId, string governmentId, string assessorId, string recognitionSourceId, string organizationId, int inheritanceScore, int recognitionScore, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            if (runtime.TryGetLegitimacyAssessment(assessmentId, out _)) return;
            Check(runtime.AssessLegitimacy(new GovernmentLegitimacyAssessmentRequest
            {
                transactionId = $"prototype.institution.seed.{assessmentId}", assessmentId = assessmentId,
                governmentId = governmentId, policyDefinitionId = PrototypeGovernmentDefinitionFactory.HereditaryHierarchyLegitimacyPolicyId,
                components = new[]
                {
                    Score(GovernmentLegitimacyComponent.Inheritance, inheritanceScore, assessorId),
                    Score(GovernmentLegitimacyComponent.SovereignRecognition, recognitionScore, recognitionSourceId),
                    Score(GovernmentLegitimacyComponent.OathsAndCustom, 8000, organizationId),
                    Score(GovernmentLegitimacyComponent.TerritorialControl, 8200, PrototypeInstitutionalContentIds.RealmPolity),
                    Score(GovernmentLegitimacyComponent.InstitutionalSupport, 7900, organizationId),
                    Score(GovernmentLegitimacyComponent.AdministrativePerformance, 7200, governmentId)
                },
                worldTime = worldTime, assessorId = assessorId, provenanceId = "prototype-institutional-bootstrap"
            }), assessmentId, report);
        }

        private static void EnsureTenure(GovernmentRuntime runtime, string tenureId, string governmentId, string definitionId, string officeId, string assignmentId, string holderId, GovernmentOfficeSelectionMethod method, string authorityId, string confirmationId, string lineageAnchorPersonId, PrototypeInstitutionalBootstrapReport report, double worldTime)
        {
            if (runtime.TryGetOfficeTenure(tenureId, out _)) return;
            Check(runtime.InstallOfficeholder(new GovernmentOfficeInstallationRequest
            {
                transactionId = $"prototype.institution.seed.{tenureId}", tenureId = tenureId, governmentId = governmentId,
                officeDefinitionId = definitionId, organizationOfficeId = officeId, officeAssignmentId = assignmentId, holderPersonId = holderId,
                selectionMethod = method, state = GovernmentOfficeTenureState.Active, appointingAuthorityId = authorityId,
                confirmationSourceId = confirmationId, lineageAnchorPersonId = lineageAnchorPersonId,
                worldTime = worldTime, provenanceId = "prototype-institutional-bootstrap"
            }), tenureId, report);
        }

        private static GovernmentLegitimacyComponentScore Score(GovernmentLegitimacyComponent component, int score, string sourceId) => new GovernmentLegitimacyComponentScore { component = component, scoreBasisPoints = score, sourceId = sourceId };

        private static void Check(PoliticalOperationResult result, string label, PrototypeInstitutionalBootstrapReport report)
        {
            if (result != null && !result.Succeeded) report.Add($"Prototype {label} was not seeded: {result.Message}");
        }
    }
}
