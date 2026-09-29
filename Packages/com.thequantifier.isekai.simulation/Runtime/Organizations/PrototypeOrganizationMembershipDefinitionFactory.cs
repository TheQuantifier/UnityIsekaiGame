using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    public static class PrototypeOrganizationMembershipDefinitionFactory
    {
        public const string GuildFullMemberId = "organization-membership.prototype.guild.full-member";
        public const string GuildApplicantId = "organization-membership.prototype.guild.applicant";
        public const string GuildInviteeId = "organization-membership.prototype.guild.invitee";
        public const string GuildAssociateId = "organization-membership.prototype.guild.associate";
        public const string GuildStaffMemberId = "organization-membership.prototype.guild.staff-member";
        public const string BranchMemberId = "organization-membership.prototype.branch.member";
        public const string ForgeEmployeeMemberId = "organization-membership.prototype.forge.employee-member";
        public const string TempleClergyMemberId = "organization-membership.prototype.temple.clergy-member";
        public const string AcademicStaffMemberId = "organization-membership.prototype.academic.staff-member";
        public const string MilitaryMemberId = "organization-membership.prototype.military.member";
        public const string SecretMemberId = "organization-membership.prototype.secret.member";
        public const string CivicOfficialMemberId = "organization-membership.prototype.civic.official";

        public const string GuildRatingTrackId = "organization-rank-track.prototype.guild.rating";
        public const string AdventurerGuildEntryRankId = "organization-rank.prototype.adventurers-guild.g";
        public const string GuildFRankId = "organization-rank.prototype.guild.f";
        public const string GuildERankId = "organization-rank.prototype.guild.e";
        public const string GuildDRankId = "organization-rank.prototype.guild.d";
        public const string GuildCRankId = "organization-rank.prototype.guild.c";
        public const string GuildBRankId = "organization-rank.prototype.guild.b";
        public const string GuildARankId = "organization-rank.prototype.guild.a";
        public const string GuildSRankId = "organization-rank.prototype.guild.s";
        public const string GuildSSRankId = "organization-rank.prototype.guild.ss";
        public const string GuildSSSRankId = "organization-rank.prototype.guild.sss";
        public static readonly IReadOnlyList<string> GuildRatingRankIds = Array.AsReadOnly(new[]
        {
            GuildFRankId,
            GuildERankId,
            GuildDRankId,
            GuildCRankId,
            GuildBRankId,
            GuildARankId,
            GuildSRankId,
            GuildSSRankId,
            GuildSSSRankId
        });
        public const string MilitaryTrackId = "organization-rank-track.prototype.military.command";
        public const string MilitaryRecruitRankId = "organization-rank.prototype.military.recruit";
        public const string MilitarySergeantRankId = "organization-rank.prototype.military.sergeant";
        public const string MilitaryCaptainRankId = "organization-rank.prototype.military.captain";
        public const string TempleTrackId = "organization-rank-track.prototype.temple.clergy";
        public const string TempleAcolyteRankId = "organization-rank.prototype.temple.acolyte";
        public const string TemplePriestRankId = "organization-rank.prototype.temple.priest";

        public const string GuildmasterOfficeId = "organization-office.prototype.guild.guildmaster";
        public const string GuildReceptionistOfficeId = "organization-office.prototype.guild.receptionist";
        public const string GuildTreasurerOfficeId = "organization-office.prototype.guild.treasurer";
        public const string BranchChapterMasterOfficeId = "organization-office.prototype.branch.chapter-master";
        public const string GuardCaptainOfficeId = "organization-office.prototype.military.guard-captain";
        public const string ChiefPriestOfficeId = "organization-office.prototype.temple.chief-priest";
        public const string UniversityHeadmasterOfficeId = "organization-office.prototype.university.headmaster";
        public const string NobleTitleOfficeId = "organization-office.prototype.civic.noble";
        public const string VillageChiefOfficeId = "organization-office.prototype.civic.village-chief";
        public const string MayorOfficeId = "organization-office.prototype.civic.mayor";
        public const string ManorLordOfficeId = "organization-office.prototype.civic.manor-lord";
        public const string DukeOfficeId = "organization-office.prototype.civic.duke";
        public const string MonarchOfficeId = "organization-office.prototype.civic.monarch";
        public const string MagistrateOfficeId = "organization-office.prototype.civic.magistrate";

        public static DefinitionRegistry AddMissingPrototypeOrganizationMembershipDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingMembershipDefinitions(ids));
            definitions.AddRange(CreateMissingRankTrackDefinitions(ids));
            definitions.AddRange(CreateMissingRankDefinitions(ids));
            definitions.AddRange(CreateMissingOfficeDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<OrganizationMembershipDefinition> CreateMissingMembershipDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationMembershipDefinition> definitions = new List<OrganizationMembershipDefinition>();
            AddMembership(definitions, ids, GuildFullMemberId, "Guild Full Member", OrganizationMembershipCategory.FullMember, new[] { PrototypeOrganizationDefinitionFactory.GuildDefinitionId }, null, OrganizationMembershipStatus.Active);
            AddMembership(definitions, ids, GuildApplicantId, "Guild Applicant", OrganizationMembershipCategory.Applicant, new[] { PrototypeOrganizationDefinitionFactory.GuildDefinitionId }, null, OrganizationMembershipStatus.Applied, ranks: false, offices: false, invitation: false);
            AddMembership(definitions, ids, GuildInviteeId, "Guild Invitee", OrganizationMembershipCategory.Invitee, new[] { PrototypeOrganizationDefinitionFactory.GuildDefinitionId }, null, OrganizationMembershipStatus.Invited, ranks: false, offices: false, application: false);
            AddMembership(definitions, ids, GuildAssociateId, "Guild Associate", OrganizationMembershipCategory.AssociateMember, new[] { PrototypeOrganizationDefinitionFactory.GuildDefinitionId }, null, OrganizationMembershipStatus.Provisional);
            AddMembership(definitions, ids, GuildStaffMemberId, "Guild Staff Member", OrganizationMembershipCategory.EmployeeMember, new[] { PrototypeOrganizationDefinitionFactory.GuildDefinitionId }, null, OrganizationMembershipStatus.Active, ranks: false);
            AddMembership(definitions, ids, BranchMemberId, "Branch Member", OrganizationMembershipCategory.FullMember, new[] { PrototypeOrganizationDefinitionFactory.BranchDefinitionId }, null, OrganizationMembershipStatus.Active, requireParent: true);
            AddMembership(definitions, ids, ForgeEmployeeMemberId, "Forge Employee Member", OrganizationMembershipCategory.EmployeeMember, new[] { PrototypeOrganizationDefinitionFactory.CompanyDefinitionId }, null, OrganizationMembershipStatus.Active);
            AddMembership(definitions, ids, TempleClergyMemberId, "Temple Clergy Member", OrganizationMembershipCategory.ClergyMember, new[] { PrototypeOrganizationDefinitionFactory.ReligiousOrderDefinitionId }, null, OrganizationMembershipStatus.Active);
            AddMembership(definitions, ids, AcademicStaffMemberId, "Academic Staff Member", OrganizationMembershipCategory.EmployeeMember, new[] { PrototypeOrganizationDefinitionFactory.InstitutionDefinitionId }, null, OrganizationMembershipStatus.Active, ranks: false);
            AddMembership(definitions, ids, MilitaryMemberId, "Military Member", OrganizationMembershipCategory.MilitaryMember, new[] { PrototypeOrganizationDefinitionFactory.MilitaryOrderDefinitionId }, null, OrganizationMembershipStatus.Active);
            AddMembership(definitions, ids, SecretMemberId, "Secret Member", OrganizationMembershipCategory.SecretMember, new[] { PrototypeOrganizationDefinitionFactory.SecretSocietyDefinitionId }, null, OrganizationMembershipStatus.Active, visibility: OrganizationVisibility.Hidden);
            AddMembership(definitions, ids, CivicOfficialMemberId, "Civic Official", OrganizationMembershipCategory.FullMember, new[] { PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId }, null, OrganizationMembershipStatus.Active, ranks: false);
            return definitions;
        }

        public static IReadOnlyList<OrganizationRankTrackDefinition> CreateMissingRankTrackDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationRankTrackDefinition> definitions = new List<OrganizationRankTrackDefinition>();
            AddTrack(definitions, ids, GuildRatingTrackId, "Guild Rating Track", PrototypeOrganizationDefinitionFactory.GuildDefinitionId, new[] { GuildFullMemberId, GuildAssociateId });
            AddTrack(definitions, ids, MilitaryTrackId, "Military Command Track", PrototypeOrganizationDefinitionFactory.MilitaryOrderDefinitionId, new[] { MilitaryMemberId });
            AddTrack(definitions, ids, TempleTrackId, "Temple Clergy Track", PrototypeOrganizationDefinitionFactory.ReligiousOrderDefinitionId, new[] { TempleClergyMemberId });
            return definitions;
        }

        public static IReadOnlyList<OrganizationRankDefinition> CreateMissingRankDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationRankDefinition> definitions = new List<OrganizationRankDefinition>();
            AddRank(definitions, ids, AdventurerGuildEntryRankId, "G Rank", GuildRatingTrackId, 0, organizations: new[] { PrototypeInstitutionalContentIds.AdventurersGuild });
            AddRank(definitions, ids, GuildFRankId, "F Rank", GuildRatingTrackId, 10);
            AddRank(definitions, ids, GuildERankId, "E Rank", GuildRatingTrackId, 20, new[] { GuildFRankId });
            AddRank(definitions, ids, GuildDRankId, "D Rank", GuildRatingTrackId, 30, new[] { GuildERankId });
            AddRank(definitions, ids, GuildCRankId, "C Rank", GuildRatingTrackId, 40, new[] { GuildDRankId });
            AddRank(definitions, ids, GuildBRankId, "B Rank", GuildRatingTrackId, 50, new[] { GuildCRankId });
            AddRank(definitions, ids, GuildARankId, "A Rank", GuildRatingTrackId, 60, new[] { GuildBRankId });
            AddRank(definitions, ids, GuildSRankId, "S Rank", GuildRatingTrackId, 70, new[] { GuildARankId });
            AddRank(definitions, ids, GuildSSRankId, "SS Rank", GuildRatingTrackId, 80, new[] { GuildSRankId });
            AddRank(definitions, ids, GuildSSSRankId, "SSS Rank", GuildRatingTrackId, 90, new[] { GuildSSRankId }, terminal: true);
            AddRank(definitions, ids, MilitaryRecruitRankId, "Recruit", MilitaryTrackId, 10);
            AddRank(definitions, ids, MilitarySergeantRankId, "Sergeant", MilitaryTrackId, 20, new[] { MilitaryRecruitRankId });
            AddRank(definitions, ids, MilitaryCaptainRankId, "Captain", MilitaryTrackId, 30, new[] { MilitarySergeantRankId }, terminal: true);
            AddRank(definitions, ids, TempleAcolyteRankId, "Acolyte", TempleTrackId, 10);
            AddRank(definitions, ids, TemplePriestRankId, "Priest", TempleTrackId, 20, new[] { TempleAcolyteRankId });
            return definitions;
        }

        public static IReadOnlyList<OrganizationOfficeDefinition> CreateMissingOfficeDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = Set(existingDefinitionIds);
            List<OrganizationOfficeDefinition> definitions = new List<OrganizationOfficeDefinition>();
            AddOffice(definitions, ids, GuildmasterOfficeId, "Guildmaster", PrototypeOrganizationDefinitionFactory.GuildDefinitionId, new[] { GuildFullMemberId }, new[] { GuildSSSRankId }, 1);
            AddOffice(definitions, ids, GuildReceptionistOfficeId, "Guild Receptionist", PrototypeOrganizationDefinitionFactory.GuildDefinitionId, new[] { GuildStaffMemberId }, Array.Empty<string>(), 8, joint: true);
            AddOffice(definitions, ids, GuildTreasurerOfficeId, "Guild Treasurer", PrototypeOrganizationDefinitionFactory.GuildDefinitionId, new[] { GuildFullMemberId, GuildAssociateId }, Array.Empty<string>(), 2, joint: true);
            AddOffice(definitions, ids, BranchChapterMasterOfficeId, "Chapter Master", PrototypeOrganizationDefinitionFactory.BranchDefinitionId, new[] { BranchMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, GuardCaptainOfficeId, "Guard Captain", PrototypeOrganizationDefinitionFactory.MilitaryOrderDefinitionId, new[] { MilitaryMemberId }, new[] { MilitaryCaptainRankId }, 1);
            AddOffice(definitions, ids, ChiefPriestOfficeId, "Chief Priest", PrototypeOrganizationDefinitionFactory.ReligiousOrderDefinitionId, new[] { TempleClergyMemberId }, new[] { TemplePriestRankId }, 1);
            AddOffice(definitions, ids, UniversityHeadmasterOfficeId, "University Headmaster", PrototypeOrganizationDefinitionFactory.InstitutionDefinitionId, new[] { AcademicStaffMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, NobleTitleOfficeId, "Noble", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, VillageChiefOfficeId, "Village Chief", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, MayorOfficeId, "Mayor", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, ManorLordOfficeId, "Manor Lord", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, DukeOfficeId, "Duke or Duchess", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, MonarchOfficeId, "King or Queen", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 1);
            AddOffice(definitions, ids, MagistrateOfficeId, "Magistrate", PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId, new[] { CivicOfficialMemberId }, Array.Empty<string>(), 2);
            return definitions;
        }

        private static void AddMembership(ICollection<OrganizationMembershipDefinition> definitions, ISet<string> ids, string id, string name, OrganizationMembershipCategory category, IEnumerable<string> orgDefinitions, IEnumerable<OrganizationCategory> orgCategories, OrganizationMembershipStatus initial, bool ranks = true, bool offices = true, bool application = true, bool invitation = true, bool requireParent = false, OrganizationVisibility visibility = OrganizationVisibility.Public)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationMembershipDefinition definition = ScriptableObject.CreateInstance<OrganizationMembershipDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, orgDefinitions, orgCategories, initial, rankSupport: ranks, officeSupport: offices, application: application, invitation: invitation, requireParent: requireParent, membershipVisibility: visibility, tagIds: new[] { "prototype", "organization" });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddTrack(ICollection<OrganizationRankTrackDefinition> definitions, ISet<string> ids, string id, string name, string organizationDefinitionId, IEnumerable<string> memberships)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationRankTrackDefinition definition = ScriptableObject.CreateInstance<OrganizationRankTrackDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, organizationDefinitionId, memberships);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddRank(ICollection<OrganizationRankDefinition> definitions, ISet<string> ids, string id, string name, string trackId, int order, IEnumerable<string> prior = null, bool terminal = false, IEnumerable<string> organizations = null)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationRankDefinition definition = ScriptableObject.CreateInstance<OrganizationRankDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, trackId, order, prior, terminal: terminal, organizationIds: organizations);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddOffice(ICollection<OrganizationOfficeDefinition> definitions, ISet<string> ids, string id, string name, string organizationDefinitionId, IEnumerable<string> memberships, IEnumerable<string> ranks, int capacity, bool joint = false)
        {
            if (ids.Contains(id))
            {
                return;
            }

            OrganizationOfficeDefinition definition = ScriptableObject.CreateInstance<OrganizationOfficeDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, organizationDefinitionId, memberships: memberships, ranks: ranks, maximumHolders: capacity, joint: joint);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static HashSet<string> Set(IEnumerable<string> ids)
        {
            return ids == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(ids.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }
    }
}
