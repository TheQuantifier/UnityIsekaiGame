using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    public static class PrototypeOrganizationDefinitionFactory
    {
        public const string GuildDefinitionId = "organization-definition.guild";
        public const string CompanyDefinitionId = "organization-definition.company";
        public const string InstitutionDefinitionId = "organization-definition.institution";
        public const string ReligiousOrderDefinitionId = "organization-definition.religious-order";
        public const string MilitaryOrderDefinitionId = "organization-definition.military-order";
        public const string CivicBodyDefinitionId = "organization-definition.civic-body";
        public const string SecretSocietyDefinitionId = "organization-definition.secret-society";
        public const string CriminalOrganizationDefinitionId = "organization-definition.criminal-organization";
        public const string BranchDefinitionId = "organization-definition.branch";
        public const string HouseholdDefinitionId = "organization-definition.household";

        public static readonly string[] PrototypeOrganizationIds =
        {
            "organization.prototype.guild",
            "organization.prototype.merchant-guild",
            "organization.prototype.royal-forge",
            "organization.prototype.temple",
            "organization.prototype.university",
            "organization.prototype.government",
            "organization.prototype.independent"
        };

        public static DefinitionRegistry AddMissingPrototypeOrganizationDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            foreach (OrganizationDefinition definition in CreateMissingOrganizationDefinitions(ids))
            {
                definitions.Add(definition);
            }

            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<OrganizationDefinition> CreateMissingOrganizationDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<OrganizationDefinition> definitions = new List<OrganizationDefinition>();
            Add(definitions, ids, GuildDefinitionId, "Guild", OrganizationCategory.Guild, branches: true, affiliates: true, secret: false, hidden: false, tags: new[] { "profession", "guild" });
            Add(definitions, ids, CompanyDefinitionId, "Company", OrganizationCategory.Company, branches: true, affiliates: true, secret: false, hidden: false, tags: new[] { "economy", "business" });
            Add(definitions, ids, InstitutionDefinitionId, "Institution", OrganizationCategory.Institution, branches: true, affiliates: true, secret: false, hidden: false, tags: new[] { "institution" });
            Add(definitions, ids, ReligiousOrderDefinitionId, "Religious Order", OrganizationCategory.ReligiousOrder, branches: true, affiliates: true, secret: false, hidden: false, tags: new[] { "religion", "institution" });
            Add(definitions, ids, MilitaryOrderDefinitionId, "Military Order", OrganizationCategory.MilitaryOrder, branches: true, affiliates: true, secret: true, hidden: false, tags: new[] { "military" });
            Add(definitions, ids, CivicBodyDefinitionId, "Civic Body", OrganizationCategory.CivicBody, branches: true, affiliates: true, secret: false, hidden: false, tags: new[] { "civic" });
            Add(definitions, ids, SecretSocietyDefinitionId, "Secret Society", OrganizationCategory.SecretSociety, branches: true, affiliates: true, secret: true, hidden: true, tags: new[] { "secret" });
            Add(definitions, ids, CriminalOrganizationDefinitionId, "Criminal Organization", OrganizationCategory.CriminalOrganization, branches: true, affiliates: true, secret: true, hidden: true, tags: new[] { "criminal" });
            Add(definitions, ids, BranchDefinitionId, "Branch Organization", OrganizationCategory.Branch, branches: true, affiliates: true, secret: false, hidden: false, tags: new[] { "branch" });
            Add(definitions, ids, HouseholdDefinitionId, "Household", OrganizationCategory.Household, branches: false, affiliates: true, secret: true, hidden: false, tags: new[] { "household", "family" });
            return definitions;
        }

        public static void SeedPrototypeOrganizations(OrganizationRuntime runtime, DefinitionRegistry registry, string worldId)
        {
            if (runtime == null)
            {
                return;
            }

            runtime.Configure(registry, worldId);
            Seed(runtime, "organization.prototype.guild", GuildDefinitionId, "Prototype Adventurers Guild", "Guild", 0d);
            Seed(runtime, "organization.prototype.merchant-guild", GuildDefinitionId, "Prototype Merchant Guild", "Merchant Guild", 0d);
            Seed(runtime, "organization.prototype.royal-forge", CompanyDefinitionId, "Prototype Royal Forge", "Royal Forge", 0d);
            Seed(runtime, "organization.prototype.temple", ReligiousOrderDefinitionId, "Prototype Temple", "Temple", 0d);
            Seed(runtime, "organization.prototype.university", InstitutionDefinitionId, "Prototype University", "University", 0d);
            Seed(runtime, "organization.prototype.government", CivicBodyDefinitionId, "Prototype Civic Office", "Civic Office", 0d);
            Seed(runtime, "organization.prototype.independent", InstitutionDefinitionId, "Independent Practitioners", "Independent", 0d);
        }

        private static void Seed(OrganizationRuntime runtime, string organizationId, string definitionId, string name, string shortName, double worldTime)
        {
            runtime.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = organizationId,
                organizationDefinitionId = definitionId,
                officialName = name,
                shortName = shortName,
                initialLifecycleState = OrganizationLifecycleState.Active,
                foundingWorldTime = worldTime,
                visibility = OrganizationVisibility.Public,
                founders = new[] { new OrganizationFounderReferenceData { kind = OrganizationFounderKind.ScriptedWorldSetup, subjectId = "world.prototype" } },
                transactionId = $"prototype.seed.{organizationId}"
            });
        }

        private static void Add(
            ICollection<OrganizationDefinition> definitions,
            ISet<string> existingIds,
            string id,
            string displayName,
            OrganizationCategory category,
            bool branches,
            bool affiliates,
            bool secret,
            bool hidden,
            IEnumerable<string> tags)
        {
            if (existingIds.Contains(id))
            {
                return;
            }

            OrganizationDefinition definition = ScriptableObject.CreateInstance<OrganizationDefinition>();
            definition.name = displayName;
            definition.DevelopmentConfigure(
                id,
                displayName,
                category,
                OrganizationLifecycleState.Active,
                branches,
                affiliates,
                multipleParents: false,
                headquarters: true,
                requiredHeadquarters: false,
                operatingAreas: true,
                secretVisibility: secret,
                hiddenVisibility: hidden,
                tagIds: tags);
            definitions.Add(definition);
            existingIds.Add(id);
        }
    }
}
