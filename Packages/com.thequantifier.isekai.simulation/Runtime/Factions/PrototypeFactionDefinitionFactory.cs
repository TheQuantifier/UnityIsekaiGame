using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Factions
{
    public static class PrototypeFactionDefinitionFactory
    {
        public const string ReformFactionId = "faction.prototype.guild.reformists";
        public const string TraditionalistFactionId = "faction.prototype.guild.traditionalists";
        public const string LeaderSupportFactionId = "faction.prototype.guildmaster-loyalists";
        public const string OppositionFactionId = "faction.prototype.guild.opposition";
        public const string MerchantInterestFactionId = "faction.prototype.merchant-interest";
        public const string ReligiousInterestFactionId = "faction.prototype.sanctuary-interest";
        public const string MilitaryInterestFactionId = "faction.prototype.militia-interest";
        public const string ClaimantFactionId = "faction.prototype.claimant-support";
        public const string SecretFactionId = "faction.prototype.secret-society";
        public const string CrossOrgMovementFactionId = "faction.prototype.cross-guild-reform";
        public const string IndependentMovementFactionId = "faction.prototype.free-company-movement";

        public const string FormalMemberAffiliationId = "faction-affiliation.prototype.formal-member";
        public const string SupporterAffiliationId = "faction-affiliation.prototype.supporter";
        public const string SympathizerAffiliationId = "faction-affiliation.prototype.sympathizer";
        public const string OpponentAffiliationId = "faction-affiliation.prototype.opponent";
        public const string SecretMemberAffiliationId = "faction-affiliation.prototype.secret-member";
        public const string InfiltratorAffiliationId = "faction-affiliation.prototype.infiltrator";

        public const string MemberRoleId = "faction-role.prototype.member";
        public const string OrganizerRoleId = "faction-role.prototype.organizer";
        public const string SpokespersonRoleId = "faction-role.prototype.spokesperson";
        public const string SeniorLeaderRoleId = "faction-role.prototype.senior-leader";
        public const string AgentRoleId = "faction-role.prototype.agent";
        public const string InfiltratorRoleId = "faction-role.prototype.infiltrator";

        public const string ProposalPositionId = "faction-position.prototype.organization-proposal";
        public const string PolicyPositionId = "faction-position.prototype.organization-policy";
        public const string OfficePositionId = "faction-position.prototype.organization-office";
        public const string AxisPositionId = "faction-position.prototype.alignment-axis";

        public const string ReformTraditionAxisId = "faction-axis.prototype.reform-tradition";
        public const string CentralLocalAxisId = "faction-axis.prototype.central-local";

        public static DefinitionRegistry AddMissingPrototypeFactionDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            definitions.AddRange(CreateMissingFactionDefinitions(ids));
            definitions.AddRange(CreateMissingAffiliationDefinitions(ids));
            definitions.AddRange(CreateMissingRoleDefinitions(ids));
            definitions.AddRange(CreateMissingPositionDefinitions(ids));
            definitions.AddRange(CreateMissingAxisDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<FactionDefinition> CreateMissingFactionDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<FactionDefinition> definitions = new List<FactionDefinition>();
            AddFaction(definitions, ids, ReformFactionId, "Prototype Guild Reformists", PoliticalFactionCategory.ReformMovement, FactionHostContextKind.SingleOrganization, secret: false, requiresMembership: true);
            AddFaction(definitions, ids, TraditionalistFactionId, "Prototype Guild Traditionalists", PoliticalFactionCategory.TraditionalistBloc, FactionHostContextKind.SingleOrganization, secret: false, requiresMembership: true);
            AddFaction(definitions, ids, LeaderSupportFactionId, "Guildmaster Loyalists", PoliticalFactionCategory.LeadershipSupportBloc, FactionHostContextKind.SingleOrganization, secret: false, requiresMembership: true);
            AddFaction(definitions, ids, OppositionFactionId, "Guild Opposition Circle", PoliticalFactionCategory.OppositionBloc, FactionHostContextKind.SingleOrganization, secret: false, requiresMembership: false);
            AddFaction(definitions, ids, MerchantInterestFactionId, "Merchant Interest Bloc", PoliticalFactionCategory.EconomicInterestBloc, FactionHostContextKind.MultipleOrganizations, spansOrganizations: true);
            AddFaction(definitions, ids, ReligiousInterestFactionId, "Sanctuary Interest Bloc", PoliticalFactionCategory.ReligiousBloc, FactionHostContextKind.PlaceOrRegion);
            AddFaction(definitions, ids, MilitaryInterestFactionId, "Militia Interest Bloc", PoliticalFactionCategory.MilitaryBloc, FactionHostContextKind.PlaceOrRegion);
            AddFaction(definitions, ids, ClaimantFactionId, "Claimant Support Circle", PoliticalFactionCategory.ClaimantSupportFaction, FactionHostContextKind.PlaceOrRegion);
            AddFaction(definitions, ids, SecretFactionId, "Hidden Lantern Society", PoliticalFactionCategory.SecretPoliticalSociety, FactionHostContextKind.SingleOrganization, secret: true, requiresMembership: false, visibility: FactionVisibility.Secret);
            AddFaction(definitions, ids, CrossOrgMovementFactionId, "Cross Guild Reform Movement", PoliticalFactionCategory.CrossOrganizationalCoalition, FactionHostContextKind.MultipleOrganizations, spansOrganizations: true);
            AddFaction(definitions, ids, IndependentMovementFactionId, "Free Company Movement", PoliticalFactionCategory.IndependentPoliticalMovement, FactionHostContextKind.Independent);
            return definitions;
        }

        public static IReadOnlyList<FactionAffiliationDefinition> CreateMissingAffiliationDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<FactionAffiliationDefinition> definitions = new List<FactionAffiliationDefinition>();
            AddAffiliation(definitions, ids, FormalMemberAffiliationId, "Formal Faction Member", FactionAffiliationCategory.FormalMember, FactionAffiliationConsentPolicy.ExplicitConsentRequired, requiresOrganizationMembership: true);
            AddAffiliation(definitions, ids, SupporterAffiliationId, "Faction Supporter", FactionAffiliationCategory.Supporter, FactionAffiliationConsentPolicy.NoConsentRequired, supportOnly: true, simultaneous: true);
            AddAffiliation(definitions, ids, SympathizerAffiliationId, "Faction Sympathizer", FactionAffiliationCategory.Sympathizer, FactionAffiliationConsentPolicy.NoConsentRequired, supportOnly: true, simultaneous: true);
            AddAffiliation(definitions, ids, OpponentAffiliationId, "Faction Opponent", FactionAffiliationCategory.Opponent, FactionAffiliationConsentPolicy.NoConsentRequired, supportOnly: true, simultaneous: true);
            AddAffiliation(definitions, ids, SecretMemberAffiliationId, "Secret Faction Member", FactionAffiliationCategory.SecretMember, FactionAffiliationConsentPolicy.ExplicitConsentRequired, isPublic: false, secret: true, simultaneous: true);
            AddAffiliation(definitions, ids, InfiltratorAffiliationId, "Faction Infiltrator", FactionAffiliationCategory.Infiltrator, FactionAffiliationConsentPolicy.CovertOperationRequired, isPublic: false, secret: true, simultaneous: true, infiltration: true);
            return definitions;
        }

        public static IReadOnlyList<FactionRoleDefinition> CreateMissingRoleDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<FactionRoleDefinition> definitions = new List<FactionRoleDefinition>();
            AddRole(definitions, ids, MemberRoleId, "Faction Member Role", FactionRoleCategory.Member);
            AddRole(definitions, ids, OrganizerRoleId, "Faction Organizer", FactionRoleCategory.Organizer);
            AddRole(definitions, ids, SpokespersonRoleId, "Faction Spokesperson", FactionRoleCategory.Spokesperson);
            AddRole(definitions, ids, SeniorLeaderRoleId, "Faction Senior Leader", FactionRoleCategory.SeniorLeader, leadership: true);
            AddRole(definitions, ids, AgentRoleId, "Faction Agent", FactionRoleCategory.Agent, visibility: FactionVisibility.Hidden);
            AddRole(definitions, ids, InfiltratorRoleId, "Faction Infiltrator Role", FactionRoleCategory.Infiltrator, visibility: FactionVisibility.Secret);
            return definitions;
        }

        public static IReadOnlyList<FactionPositionDefinition> CreateMissingPositionDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<FactionPositionDefinition> definitions = new List<FactionPositionDefinition>();
            AddPosition(definitions, ids, ProposalPositionId, "Faction Proposal Position", FactionPositionTargetKind.OrganizationProposal);
            AddPosition(definitions, ids, PolicyPositionId, "Faction Policy Position", FactionPositionTargetKind.OrganizationPolicy);
            AddPosition(definitions, ids, OfficePositionId, "Faction Office Position", FactionPositionTargetKind.OrganizationOffice);
            AddPosition(definitions, ids, AxisPositionId, "Faction Alignment Axis Position", FactionPositionTargetKind.AlignmentAxis);
            return definitions;
        }

        public static IReadOnlyList<FactionAlignmentAxisDefinition> CreateMissingAxisDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<FactionAlignmentAxisDefinition> definitions = new List<FactionAlignmentAxisDefinition>();
            AddAxis(definitions, ids, ReformTraditionAxisId, "Reform to Tradition Axis", -100, 100, 0);
            AddAxis(definitions, ids, CentralLocalAxisId, "Central to Local Control Axis", -100, 100, 0);
            return definitions;
        }

        private static void AddFaction(ICollection<FactionDefinition> definitions, ISet<string> ids, string id, string name, PoliticalFactionCategory category, FactionHostContextKind host, bool secret = false, bool requiresMembership = false, bool spansOrganizations = false, FactionVisibility visibility = FactionVisibility.Public)
        {
            if (ids.Contains(id)) return;
            FactionDefinition definition = ScriptableObject.CreateInstance<FactionDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, FactionKind.Other, category, host, formalMembership: true, supportWithoutMembership: true, secretMembership: secret, requiresOrganizationMembership: requiresMembership, spansOrganizations: spansOrganizations, factionVisibility: visibility, axes: new[] { ReformTraditionAxisId, CentralLocalAxisId }, platforms: new[] { ProposalPositionId, PolicyPositionId });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddAffiliation(ICollection<FactionAffiliationDefinition> definitions, ISet<string> ids, string id, string name, FactionAffiliationCategory category, FactionAffiliationConsentPolicy consent, bool isPublic = true, bool simultaneous = false, bool requiresOrganizationMembership = false, bool supportOnly = false, bool secret = false, bool infiltration = false)
        {
            if (ids.Contains(id)) return;
            FactionAffiliationDefinition definition = ScriptableObject.CreateInstance<FactionAffiliationDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, consent, isPublic, simultaneous, requiresOrganizationMembership, voteEligible: category != FactionAffiliationCategory.Opponent, roleEligible: !supportOnly, infiltration: infiltration, supportOnly: supportOnly, affiliationVisibility: secret ? FactionVisibility.Secret : FactionVisibility.Public, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddRole(ICollection<FactionRoleDefinition> definitions, ISet<string> ids, string id, string name, FactionRoleCategory category, bool leadership = false, FactionVisibility visibility = FactionVisibility.Public)
        {
            if (ids.Contains(id)) return;
            FactionRoleDefinition definition = ScriptableObject.CreateInstance<FactionRoleDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, leadership, multiple: true, activeAffiliation: true, roleVisibility: visibility, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddPosition(ICollection<FactionPositionDefinition> definitions, ISet<string> ids, string id, string name, FactionPositionTargetKind target)
        {
            if (ids.Contains(id)) return;
            FactionPositionDefinition definition = ScriptableObject.CreateInstance<FactionPositionDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, target, FactionPositionStance.Neutral, temporary: true, disputes: true, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddAxis(ICollection<FactionAlignmentAxisDefinition> definitions, ISet<string> ids, string id, string name, int min, int max, int neutral)
        {
            if (ids.Contains(id)) return;
            FactionAlignmentAxisDefinition definition = ScriptableObject.CreateInstance<FactionAlignmentAxisDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, min, max, neutral, Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static HashSet<string> Set(IEnumerable<string> ids) => new HashSet<string>((ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        private static string[] Tags() => new[] { "prototype", "faction", "politics" };
    }
}
