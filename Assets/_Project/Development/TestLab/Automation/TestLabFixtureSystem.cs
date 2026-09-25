#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Development.Automation.Fixtures.Core;
using UnityIsekaiGame.Development.Automation.Fixtures.History;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Integration;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Rumors;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Development.Automation
{
    public enum TestLabScenarioIsolationMode
    {
        FreshRuntime = 0,
        SnapshotRestore = 1,
        SharedRuntime = 2,
        PersistentFixture = 3
    }

    public enum TestLabFixtureEnsureOutcome
    {
        Created = 0,
        ReusedEquivalent = 1,
        Conflict = 2,
        DependencyFailure = 3,
        ValidationFailure = 4
    }

    public enum TestLabFixtureMutationKind
    {
        Created = 0,
        Reused = 1,
        Modified = 2,
        Deleted = 3,
        Conflict = 4
    }

    public sealed class TestLabFixtureHandle
    {
        public TestLabFixtureHandle(string fixtureId, string kind, string stableId, string signature, TestLabFixtureEnsureOutcome outcome, string message = "")
        {
            FixtureId = Normalize(fixtureId, "fixture");
            Kind = Normalize(kind, "record");
            StableId = Normalize(stableId, string.Empty);
            Signature = signature ?? string.Empty;
            Outcome = outcome;
            Message = message ?? string.Empty;
        }

        public string FixtureId { get; }
        public string Kind { get; }
        public string StableId { get; }
        public string Signature { get; }
        public TestLabFixtureEnsureOutcome Outcome { get; }
        public string Message { get; }
        public bool Succeeded => Outcome == TestLabFixtureEnsureOutcome.Created || Outcome == TestLabFixtureEnsureOutcome.ReusedEquivalent;

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    public sealed class TestLabFixtureHandle<TPayload>
    {
        public TestLabFixtureHandle(TestLabFixtureHandle handle, TPayload payload)
        {
            Handle = handle ?? new TestLabFixtureHandle("fixture", typeof(TPayload).Name, string.Empty, string.Empty, TestLabFixtureEnsureOutcome.ValidationFailure, "Missing fixture handle.");
            Payload = payload;
        }

        public TestLabFixtureHandle Handle { get; }
        public TPayload Payload { get; }
        public bool Succeeded => Handle.Succeeded;
    }

    public sealed class TestLabRuntimeFixturePayload
    {
        public TestLabRuntimeFixturePayload(TestLabRuntimeBundle runtimes, string ownerPersonId, string worldId)
        {
            Runtimes = runtimes;
            OwnerPersonId = ownerPersonId ?? string.Empty;
            WorldId = worldId ?? string.Empty;
        }

        public TestLabRuntimeBundle Runtimes { get; }
        public string OwnerPersonId { get; }
        public string WorldId { get; }
    }

    public sealed class TestLabFixtureMutationRecord
    {
        public TestLabFixtureMutationRecord(string fixtureId, string kind, string stableId, TestLabFixtureMutationKind mutationKind, string beforeSignature, string afterSignature, string message)
        {
            FixtureId = fixtureId ?? string.Empty;
            Kind = kind ?? string.Empty;
            StableId = stableId ?? string.Empty;
            MutationKind = mutationKind;
            BeforeSignature = beforeSignature ?? string.Empty;
            AfterSignature = afterSignature ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string FixtureId { get; }
        public string Kind { get; }
        public string StableId { get; }
        public TestLabFixtureMutationKind MutationKind { get; }
        public string BeforeSignature { get; }
        public string AfterSignature { get; }
        public string Message { get; }
    }

    public sealed class TestLabFixtureOwnershipLedger
    {
        private readonly List<TestLabFixtureMutationRecord> records = new List<TestLabFixtureMutationRecord>();
        private readonly Dictionary<string, TestLabFixtureHandle> handlesByStableId = new Dictionary<string, TestLabFixtureHandle>(StringComparer.Ordinal);
        private readonly List<string> conflicts = new List<string>();

        public IReadOnlyList<TestLabFixtureMutationRecord> Records => records.ToArray();
        public IReadOnlyList<string> Conflicts => conflicts.ToArray();
        public IReadOnlyList<string> OwnedStableIds => handlesByStableId.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        public bool HasConflicts => conflicts.Count > 0;

        public TestLabFixtureHandle EnsureEquivalent(string fixtureId, string kind, string stableId, string expectedSignature, bool exists, string actualSignature = "")
        {
            fixtureId = Normalize(fixtureId, "fixture");
            kind = Normalize(kind, "record");
            stableId = Normalize(stableId, string.Empty);
            expectedSignature ??= string.Empty;
            actualSignature ??= string.Empty;

            if (string.IsNullOrWhiteSpace(stableId))
            {
                string message = $"Fixture '{fixtureId}' cannot own a blank {kind} stable ID.";
                conflicts.Add(message);
                TestLabFixtureHandle failed = new TestLabFixtureHandle(fixtureId, kind, stableId, expectedSignature, TestLabFixtureEnsureOutcome.ValidationFailure, message);
                Record(failed, TestLabFixtureMutationKind.Conflict, actualSignature, expectedSignature, message);
                return failed;
            }

            if (handlesByStableId.TryGetValue(stableId, out TestLabFixtureHandle previous)
                && !string.Equals(previous.FixtureId, fixtureId, StringComparison.Ordinal))
            {
                string message = $"Stable ID '{stableId}' is already owned by fixture '{previous.FixtureId}', not '{fixtureId}'.";
                conflicts.Add(message);
                TestLabFixtureHandle failed = new TestLabFixtureHandle(fixtureId, kind, stableId, expectedSignature, TestLabFixtureEnsureOutcome.Conflict, message);
                Record(failed, TestLabFixtureMutationKind.Conflict, previous.Signature, expectedSignature, message);
                return failed;
            }

            if (exists && !string.Equals(expectedSignature, actualSignature, StringComparison.Ordinal))
            {
                string message = $"Fixture '{fixtureId}' found existing {kind} '{stableId}' with a different signature.";
                conflicts.Add(message);
                TestLabFixtureHandle failed = new TestLabFixtureHandle(fixtureId, kind, stableId, expectedSignature, TestLabFixtureEnsureOutcome.Conflict, message);
                Record(failed, TestLabFixtureMutationKind.Conflict, actualSignature, expectedSignature, message);
                return failed;
            }

            TestLabFixtureEnsureOutcome outcome = exists ? TestLabFixtureEnsureOutcome.ReusedEquivalent : TestLabFixtureEnsureOutcome.Created;
            TestLabFixtureHandle handle = new TestLabFixtureHandle(fixtureId, kind, stableId, expectedSignature, outcome);
            handlesByStableId[stableId] = handle;
            Record(handle, exists ? TestLabFixtureMutationKind.Reused : TestLabFixtureMutationKind.Created, actualSignature, expectedSignature, string.Empty);
            return handle;
        }

        public void RecordModified(string fixtureId, string kind, string stableId, string beforeSignature, string afterSignature, string message = "")
        {
            records.Add(new TestLabFixtureMutationRecord(fixtureId, kind, stableId, TestLabFixtureMutationKind.Modified, beforeSignature, afterSignature, message));
        }

        public TestLabFixtureLedgerSnapshot CreateSnapshot()
        {
            return new TestLabFixtureLedgerSnapshot(records.ToArray(), conflicts.ToArray());
        }

        private void Record(TestLabFixtureHandle handle, TestLabFixtureMutationKind mutationKind, string beforeSignature, string afterSignature, string message)
        {
            records.Add(new TestLabFixtureMutationRecord(handle.FixtureId, handle.Kind, handle.StableId, mutationKind, beforeSignature, afterSignature, message));
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    public sealed class TestLabFixtureLedgerSnapshot
    {
        public TestLabFixtureLedgerSnapshot(IReadOnlyList<TestLabFixtureMutationRecord> records, IReadOnlyList<string> conflicts)
        {
            Records = (records ?? Array.Empty<TestLabFixtureMutationRecord>()).ToArray();
            Conflicts = (conflicts ?? Array.Empty<string>()).ToArray();
        }

        public IReadOnlyList<TestLabFixtureMutationRecord> Records { get; }
        public IReadOnlyList<string> Conflicts { get; }
    }

    public interface ITestLabFixtureProvider
    {
        string FixtureId { get; }
        IReadOnlyList<string> Dependencies { get; }
        TestLabFixtureHandle Prepare(TestLabScenarioContext context);
        TestLabFixtureHandle Validate(TestLabScenarioContext context);
    }

    public sealed class TestLabFixtureProvider : ITestLabFixtureProvider
    {
        private readonly Func<TestLabScenarioContext, TestLabFixtureHandle> prepare;
        private readonly Func<TestLabScenarioContext, TestLabFixtureHandle> validate;
        private readonly IReadOnlyList<string> dependencies;

        public TestLabFixtureProvider(
            string fixtureId,
            IEnumerable<string> dependencies,
            Func<TestLabScenarioContext, TestLabFixtureHandle> prepare,
            Func<TestLabScenarioContext, TestLabFixtureHandle> validate = null)
        {
            FixtureId = fixtureId ?? string.Empty;
            this.dependencies = (dependencies ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            this.prepare = prepare;
            this.validate = validate;
        }

        public string FixtureId { get; }
        public IReadOnlyList<string> Dependencies => dependencies;

        public TestLabFixtureHandle Prepare(TestLabScenarioContext context)
        {
            return prepare == null
                ? new TestLabFixtureHandle(FixtureId, "fixture", FixtureId, string.Empty, TestLabFixtureEnsureOutcome.ValidationFailure, $"Fixture '{FixtureId}' has no prepare action.")
                : prepare(context);
        }

        public TestLabFixtureHandle Validate(TestLabScenarioContext context)
        {
            return validate == null ? Prepare(context) : validate(context);
        }
    }

    public sealed class TestLabFixtureRegistry
    {
        private readonly Dictionary<string, ITestLabFixtureProvider> providersById = new Dictionary<string, ITestLabFixtureProvider>(StringComparer.Ordinal);
        private readonly Dictionary<string, TestLabFixtureHandle> preparedHandles = new Dictionary<string, TestLabFixtureHandle>(StringComparer.Ordinal);

        public IReadOnlyList<string> RegisteredFixtureIds => providersById.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public bool TryRegister(ITestLabFixtureProvider provider, out string failure)
        {
            failure = string.Empty;
            if (provider == null)
            {
                failure = "Fixture provider is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(provider.FixtureId))
            {
                failure = "Fixture provider has no stable fixture ID.";
                return false;
            }

            if (providersById.ContainsKey(provider.FixtureId))
            {
                failure = $"Duplicate fixture provider ID '{provider.FixtureId}'.";
                return false;
            }

            providersById.Add(provider.FixtureId, provider);
            return true;
        }

        public TestLabFixtureHandle Require(string fixtureId, TestLabScenarioContext context)
        {
            fixtureId = fixtureId ?? string.Empty;
            if (preparedHandles.TryGetValue(fixtureId, out TestLabFixtureHandle existing))
            {
                return existing;
            }

            if (!providersById.TryGetValue(fixtureId, out ITestLabFixtureProvider provider))
            {
                return new TestLabFixtureHandle(fixtureId, "fixture", fixtureId, string.Empty, TestLabFixtureEnsureOutcome.DependencyFailure, $"Fixture provider '{fixtureId}' is not registered.");
            }

            foreach (string dependency in provider.Dependencies ?? Array.Empty<string>())
            {
                TestLabFixtureHandle dependencyHandle = Require(dependency, context);
                if (!dependencyHandle.Succeeded)
                {
                    return new TestLabFixtureHandle(fixtureId, "fixture", fixtureId, string.Empty, TestLabFixtureEnsureOutcome.DependencyFailure, dependencyHandle.Message);
                }
            }

            TestLabFixtureHandle handle = provider.Prepare(context) ?? new TestLabFixtureHandle(fixtureId, "fixture", fixtureId, string.Empty, TestLabFixtureEnsureOutcome.ValidationFailure, $"Fixture provider '{fixtureId}' returned no handle.");
            preparedHandles[fixtureId] = handle;
            return handle;
        }

        public IReadOnlyList<string> ValidateDependencyGraph()
        {
            List<string> errors = new List<string>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (ITestLabFixtureProvider provider in providersById.Values.OrderBy(provider => provider.FixtureId, StringComparer.Ordinal))
            {
                Visit(provider, visiting, visited, errors);
            }

            return errors;
        }

        private void Visit(ITestLabFixtureProvider provider, HashSet<string> visiting, HashSet<string> visited, List<string> errors)
        {
            if (provider == null || visited.Contains(provider.FixtureId))
            {
                return;
            }

            if (!visiting.Add(provider.FixtureId))
            {
                errors.Add($"Fixture dependency cycle includes '{provider.FixtureId}'.");
                return;
            }

            foreach (string dependency in provider.Dependencies ?? Array.Empty<string>())
            {
                if (!providersById.TryGetValue(dependency, out ITestLabFixtureProvider dependencyProvider))
                {
                    errors.Add($"Fixture '{provider.FixtureId}' depends on missing fixture '{dependency}'.");
                    continue;
                }

                Visit(dependencyProvider, visiting, visited, errors);
            }

            visiting.Remove(provider.FixtureId);
            visited.Add(provider.FixtureId);
        }
    }

    public sealed class TestLabRuntimeBundle : IDisposable
    {
        private static readonly string[] DefaultRelationshipPersonIds =
        {
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.parent",
            "person.prototype.child",
            "person.prototype.dependent",
            "person.prototype.partner",
            "person.prototype.spouse",
            "person.prototype.sibling",
            "person.prototype.cousin",
            "person.prototype.mentor",
            "person.prototype.student"
        };

        private readonly GameObject ownedKnowledgeObject;

        private TestLabRuntimeBundle(
            DefinitionRegistry definitionRegistry,
            string personId,
            string worldId,
            IReadOnlyList<string> knownPersonIds,
            IReadOnlyList<string> knownBodyIds,
            PersonKnowledgeRuntime knowledge,
            AuthoritativeHistoryRuntime history,
            PersonMemoryRuntime memory,
            InformationSourceRuntime sources,
            InformationTransferRuntime transfers,
            InformationAccessRuntime access,
            KnowledgeRecordRuntime records,
            ItemInstanceIdentityRuntime itemInstances,
            ItemCompositionRuntime itemCompositions,
            ItemQualityAffixRuntime itemQualityAffixes,
            ItemDurabilityRuntime itemDurability,
            ProductionRequirementRuntime productionRequirements,
            RecipeKnowledgeRuntime recipeKnowledge,
            CraftingExecutionRuntime craftingExecution,
            ProductionWorkflowRuntime productionWorkflow,
            ExperimentationRuntime experimentation,
            PersonProfessionRuntime professions,
            ProfessionEntryRuntime professionEntries,
            TrainingRuntime training,
            ProfessionalActivityRuntime professionalActivities,
            CredentialRuntime credentials,
            ProfessionalRankRuntime professionalRanks,
            PositionEmploymentRuntime positionEmployment,
            CareerHistoryRuntime careerHistory,
            LifePathRuntime lifePaths,
            EconomyRuntime economy,
            MarketRuntime markets,
            TradeRuntime trades,
            PayrollRuntime payroll,
            BusinessRuntime businesses,
            PropertyRuntime properties,
            ContractEconomyRuntime contracts,
            InstitutionalRevenueRuntime institutionalRevenue,
            RegionalFlowRuntime regionalFlow,
            RelationshipRuntime relationships,
            InterpersonalAttitudeRuntime attitudes,
            ReputationRuntime reputation,
            RumorRuntime rumors,
            SocialInteractionRuntime socialInteractions,
            SocialNormRuntime socialNorms,
            SocialNetworkRuntime socialNetworks,
            SocialDecisionRuntime socialDecisions,
            SocialInfluenceRuntime socialInfluence,
            SocialEmotionRuntime socialEmotions,
            FamilyRelationshipRuntime familyRelationships,
            OrganizationRuntime organizations,
            OrganizationMembershipRuntime organizationMemberships,
            OrganizationAuthorityRuntime organizationAuthority,
            OrganizationResourceRuntime organizationResources,
            OrganizationDecisionRuntime organizationDecisions,
            FactionRuntime factions,
            DiplomacyRuntime diplomacy,
            GovernmentRuntime governments,
            LegalRuntime laws,
            CrimeRuntime crimes,
            JusticeRuntime justice,
            LocationRuntime locations,
            EntityLocationRuntime entityLocations,
            InteractionPointRuntime interactionPoints,
            LocationConnectionRuntime locationConnections,
            LocationRouteRuntime locationRoutes,
            TravelJourneyRuntime travelJourneys,
            TravelConditionRuntime travelConditions,
            PoliticalTravelRuntime politicalTravel,
            GameObject ownedKnowledgeObject)
        {
            DefinitionRegistry = definitionRegistry;
            PersonId = personId ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            KnownPersonIds = (knownPersonIds ?? Array.Empty<string>()).ToArray();
            KnownBodyIds = (knownBodyIds ?? Array.Empty<string>()).ToArray();
            Knowledge = knowledge;
            History = history;
            Memory = memory;
            Sources = sources;
            Transfers = transfers;
            Access = access;
            Records = records;
            ItemInstances = itemInstances;
            ItemCompositions = itemCompositions;
            ItemQualityAffixes = itemQualityAffixes;
            ItemDurability = itemDurability;
            ProductionRequirements = productionRequirements;
            RecipeKnowledge = recipeKnowledge;
            CraftingExecution = craftingExecution;
            ProductionWorkflow = productionWorkflow;
            Experimentation = experimentation;
            Professions = professions;
            ProfessionEntries = professionEntries;
            Training = training;
            ProfessionalActivities = professionalActivities;
            Credentials = credentials;
            ProfessionalRanks = professionalRanks;
            PositionEmployment = positionEmployment;
            CareerHistory = careerHistory;
            LifePaths = lifePaths;
            Economy = economy;
            Markets = markets;
            Trades = trades;
            Payroll = payroll;
            Businesses = businesses;
            Properties = properties;
            Contracts = contracts;
            InstitutionalRevenue = institutionalRevenue;
            RegionalFlow = regionalFlow;
            Relationships = relationships;
            Attitudes = attitudes;
            Reputation = reputation;
            Rumors = rumors;
            SocialInteractions = socialInteractions;
            SocialNorms = socialNorms;
            SocialNetworks = socialNetworks;
            SocialDecisions = socialDecisions;
            SocialInfluence = socialInfluence;
            SocialEmotions = socialEmotions;
            FamilyRelationships = familyRelationships;
            Organizations = organizations;
            OrganizationMemberships = organizationMemberships;
            OrganizationAuthority = organizationAuthority;
            OrganizationResources = organizationResources;
            OrganizationDecisions = organizationDecisions;
            Factions = factions;
            Diplomacy = diplomacy;
            Governments = governments;
            Laws = laws;
            Crimes = crimes;
            Justice = justice;
            Locations = locations;
            EntityLocations = entityLocations;
            InteractionPoints = interactionPoints;
            LocationConnections = locationConnections;
            LocationRoutes = locationRoutes;
            TravelJourneys = travelJourneys;
            TravelConditions = travelConditions;
            PoliticalTravel = politicalTravel;
            this.ownedKnowledgeObject = ownedKnowledgeObject;
            Facade = new KnowledgeHistoryFacade(CreateRuntimeSet());
        }

        public DefinitionRegistry DefinitionRegistry { get; }
        public string PersonId { get; }
        public string WorldId { get; }
        public IReadOnlyList<string> KnownPersonIds { get; }
        public IReadOnlyList<string> KnownBodyIds { get; }
        public PersonKnowledgeRuntime Knowledge { get; }
        public AuthoritativeHistoryRuntime History { get; }
        public PersonMemoryRuntime Memory { get; }
        public InformationSourceRuntime Sources { get; }
        public InformationTransferRuntime Transfers { get; }
        public InformationAccessRuntime Access { get; }
        public KnowledgeRecordRuntime Records { get; }
        public ItemInstanceIdentityRuntime ItemInstances { get; }
        public ItemCompositionRuntime ItemCompositions { get; }
        public ItemQualityAffixRuntime ItemQualityAffixes { get; }
        public ItemDurabilityRuntime ItemDurability { get; }
        public ProductionRequirementRuntime ProductionRequirements { get; }
        public RecipeKnowledgeRuntime RecipeKnowledge { get; }
        public CraftingExecutionRuntime CraftingExecution { get; }
        public ProductionWorkflowRuntime ProductionWorkflow { get; }
        public ExperimentationRuntime Experimentation { get; }
        public PersonProfessionRuntime Professions { get; }
        public ProfessionEntryRuntime ProfessionEntries { get; }
        public TrainingRuntime Training { get; }
        public ProfessionalActivityRuntime ProfessionalActivities { get; }
        public CredentialRuntime Credentials { get; }
        public ProfessionalRankRuntime ProfessionalRanks { get; }
        public PositionEmploymentRuntime PositionEmployment { get; }
        public CareerHistoryRuntime CareerHistory { get; }
        public LifePathRuntime LifePaths { get; }
        public EconomyRuntime Economy { get; }
        public MarketRuntime Markets { get; }
        public TradeRuntime Trades { get; }
        public PayrollRuntime Payroll { get; }
        public BusinessRuntime Businesses { get; }
        public PropertyRuntime Properties { get; }
        public ContractEconomyRuntime Contracts { get; }
        public InstitutionalRevenueRuntime InstitutionalRevenue { get; }
        public RegionalFlowRuntime RegionalFlow { get; }
        public RelationshipRuntime Relationships { get; }
        public InterpersonalAttitudeRuntime Attitudes { get; }
        public ReputationRuntime Reputation { get; }
        public RumorRuntime Rumors { get; }
        public SocialInteractionRuntime SocialInteractions { get; }
        public SocialNormRuntime SocialNorms { get; }
        public SocialNetworkRuntime SocialNetworks { get; }
        public SocialDecisionRuntime SocialDecisions { get; }
        public SocialInfluenceRuntime SocialInfluence { get; }
        public SocialEmotionRuntime SocialEmotions { get; }
        public FamilyRelationshipRuntime FamilyRelationships { get; }
        public OrganizationRuntime Organizations { get; }
        public OrganizationMembershipRuntime OrganizationMemberships { get; }
        public OrganizationAuthorityRuntime OrganizationAuthority { get; }
        public OrganizationResourceRuntime OrganizationResources { get; }
        public OrganizationDecisionRuntime OrganizationDecisions { get; }
        public FactionRuntime Factions { get; }
        public DiplomacyRuntime Diplomacy { get; }
        public GovernmentRuntime Governments { get; }
        public LegalRuntime Laws { get; }
        public CrimeRuntime Crimes { get; }
        public JusticeRuntime Justice { get; }
        public LocationRuntime Locations { get; }
        public EntityLocationRuntime EntityLocations { get; }
        public InteractionPointRuntime InteractionPoints { get; }
        public LocationConnectionRuntime LocationConnections { get; }
        public LocationRouteRuntime LocationRoutes { get; }
        public TravelJourneyRuntime TravelJourneys { get; }
        public TravelConditionRuntime TravelConditions { get; }
        public PoliticalTravelRuntime PoliticalTravel { get; }
        public KnowledgeHistoryFacade Facade { get; }

        public static TestLabRuntimeBundle FromExisting(
            DefinitionRegistry definitionRegistry,
            string personId,
            string worldId,
            IReadOnlyList<string> knownPersonIds,
            IReadOnlyList<string> knownBodyIds,
            PersonKnowledgeRuntime knowledge,
            AuthoritativeHistoryRuntime history,
            PersonMemoryRuntime memory,
            InformationSourceRuntime sources,
            InformationTransferRuntime transfers,
            InformationAccessRuntime access,
            KnowledgeRecordRuntime records,
            ItemInstanceIdentityRuntime itemInstances = null,
            ItemCompositionRuntime itemCompositions = null,
            ItemQualityAffixRuntime itemQualityAffixes = null,
            ItemDurabilityRuntime itemDurability = null,
            ProductionRequirementRuntime productionRequirements = null,
            RecipeKnowledgeRuntime recipeKnowledge = null,
            CraftingExecutionRuntime craftingExecution = null,
            ProductionWorkflowRuntime productionWorkflow = null,
            ExperimentationRuntime experimentation = null,
            PersonProfessionRuntime professions = null,
            ProfessionEntryRuntime professionEntries = null,
            TrainingRuntime training = null,
            ProfessionalActivityRuntime professionalActivities = null,
            CredentialRuntime credentials = null,
            ProfessionalRankRuntime professionalRanks = null,
            PositionEmploymentRuntime positionEmployment = null,
            CareerHistoryRuntime careerHistory = null,
            LifePathRuntime lifePaths = null,
            EconomyRuntime economy = null,
            MarketRuntime markets = null,
            TradeRuntime trades = null,
            PayrollRuntime payroll = null,
            BusinessRuntime businesses = null,
            PropertyRuntime properties = null,
            ContractEconomyRuntime contracts = null,
            InstitutionalRevenueRuntime institutionalRevenue = null,
            RegionalFlowRuntime regionalFlow = null,
            RelationshipRuntime relationships = null,
            InterpersonalAttitudeRuntime attitudes = null,
            ReputationRuntime reputation = null,
            RumorRuntime rumors = null,
            SocialInteractionRuntime socialInteractions = null,
            SocialNormRuntime socialNorms = null,
            SocialNetworkRuntime socialNetworks = null,
            SocialDecisionRuntime socialDecisions = null,
            SocialInfluenceRuntime socialInfluence = null,
            SocialEmotionRuntime socialEmotions = null,
            FamilyRelationshipRuntime familyRelationships = null,
            OrganizationRuntime organizations = null,
            OrganizationMembershipRuntime organizationMemberships = null,
            OrganizationAuthorityRuntime organizationAuthority = null,
            OrganizationResourceRuntime organizationResources = null,
            OrganizationDecisionRuntime organizationDecisions = null,
            FactionRuntime factions = null,
            DiplomacyRuntime diplomacy = null,
            GovernmentRuntime governments = null,
            LegalRuntime laws = null,
            CrimeRuntime crimes = null,
            JusticeRuntime justice = null,
            LocationRuntime locations = null,
            EntityLocationRuntime entityLocations = null,
            InteractionPointRuntime interactionPoints = null,
            LocationConnectionRuntime locationConnections = null,
            LocationRouteRuntime locationRoutes = null,
            TravelJourneyRuntime travelJourneys = null,
            TravelConditionRuntime travelConditions = null,
            PoliticalTravelRuntime politicalTravel = null)
        {
            string[] persons = ExpandKnownPersons(knownPersonIds, personId);
            PersonProfessionRuntime professionRuntime = professions ?? new PersonProfessionRuntime();
            professionRuntime.Configure(definitionRegistry, persons);
            ProfessionEntryRuntime entryRuntime = professionEntries ?? new ProfessionEntryRuntime();
            entryRuntime.Configure(definitionRegistry, professionRuntime, persons);
            TrainingRuntime trainingRuntime = training ?? new TrainingRuntime();
            trainingRuntime.Configure(definitionRegistry, professionRuntime, transfers, persons);
            ProfessionalActivityRuntime professionalActivityRuntime = professionalActivities ?? new ProfessionalActivityRuntime();
            professionalActivityRuntime.Configure(definitionRegistry, professionRuntime, persons);
            CredentialRuntime credentialRuntime = credentials ?? new CredentialRuntime();
            credentialRuntime.Configure(definitionRegistry, professionRuntime, trainingRuntime, professionalActivityRuntime, persons, DefaultCredentialAuthorityIds);
            ProfessionalRankRuntime rankRuntime = professionalRanks ?? new ProfessionalRankRuntime();
            rankRuntime.Configure(definitionRegistry, professionRuntime, trainingRuntime, professionalActivityRuntime, credentialRuntime, persons, DefaultCredentialAuthorityIds);
            PositionEmploymentRuntime positionRuntime = positionEmployment ?? new PositionEmploymentRuntime();
            positionRuntime.Configure(definitionRegistry, professionRuntime, trainingRuntime, professionalActivityRuntime, credentialRuntime, rankRuntime, persons, DefaultOrganizationIds, DefaultCredentialAuthorityIds);
            CareerHistoryRuntime careerRuntime = careerHistory ?? new CareerHistoryRuntime();
            careerRuntime.Configure(definitionRegistry, professionRuntime, trainingRuntime, professionalActivityRuntime, credentialRuntime, rankRuntime, positionRuntime, persons, DefaultOrganizationIds, DefaultCredentialAuthorityIds);
            LifePathRuntime lifePathRuntime = lifePaths ?? new LifePathRuntime();
            lifePathRuntime.Configure(definitionRegistry, professionRuntime, trainingRuntime, professionalActivityRuntime, credentialRuntime, rankRuntime, positionRuntime, careerRuntime, persons, DefaultOrganizationIds);
            EconomyRuntime economyRuntime = economy ?? new EconomyRuntime();
            economyRuntime.Configure(definitionRegistry, worldId);
            MarketRuntime marketRuntime = markets ?? new MarketRuntime();
            marketRuntime.Configure(definitionRegistry, worldId);
            TradeRuntime tradeRuntime = trades ?? new TradeRuntime();
            tradeRuntime.Configure(definitionRegistry, worldId);
            PayrollRuntime payrollRuntime = payroll ?? new PayrollRuntime();
            payrollRuntime.Configure(definitionRegistry, worldId);
            BusinessRuntime businessRuntime = businesses ?? new BusinessRuntime();
            businessRuntime.Configure(definitionRegistry, worldId);
            PropertyRuntime propertyRuntime = properties ?? new PropertyRuntime();
            propertyRuntime.Configure(definitionRegistry, worldId);
            ContractEconomyRuntime contractRuntime = contracts ?? new ContractEconomyRuntime();
            contractRuntime.Configure(definitionRegistry, worldId);
            InstitutionalRevenueRuntime institutionalRevenueRuntime = institutionalRevenue ?? new InstitutionalRevenueRuntime();
            institutionalRevenueRuntime.Configure(definitionRegistry, worldId);
            RegionalFlowRuntime regionalFlowRuntime = regionalFlow ?? new RegionalFlowRuntime();
            regionalFlowRuntime.Configure(definitionRegistry, worldId);
            LocationRuntime locationRuntime = locations ?? new LocationRuntime();
            if (locations == null)
            {
                PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locationRuntime, definitionRegistry, worldId);
            }

            locationRuntime.Configure(definitionRegistry, worldId);
            EntityLocationRuntime entityLocationRuntime = entityLocations ?? new EntityLocationRuntime();
            if (entityLocations == null)
            {
                PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocationRuntime, locationRuntime, worldId);
            }
            else
            {
                entityLocationRuntime.Configure(locationRuntime, worldId);
            }

            InteractionPointRuntime interactionPointRuntime = interactionPoints ?? new InteractionPointRuntime();
            if (interactionPoints == null)
            {
                PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactionPointRuntime, definitionRegistry, locationRuntime, entityLocationRuntime, worldId);
            }
            else
            {
                interactionPointRuntime.Configure(definitionRegistry, locationRuntime, entityLocationRuntime, worldId);
            }

            LocationConnectionRuntime locationConnectionRuntime = locationConnections ?? new LocationConnectionRuntime();
            if (locationConnections == null)
            {
                PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(locationConnectionRuntime, definitionRegistry, locationRuntime, entityLocationRuntime, interactionPointRuntime, worldId);
            }
            else
            {
                locationConnectionRuntime.Configure(definitionRegistry, locationRuntime, entityLocationRuntime, interactionPointRuntime, worldId);
            }

            LocationRouteRuntime locationRouteRuntime = locationRoutes ?? new LocationRouteRuntime();
            if (locationRoutes == null)
            {
                PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(locationRouteRuntime, definitionRegistry, locationRuntime, locationConnectionRuntime, worldId);
            }
            else
            {
                locationRouteRuntime.Configure(definitionRegistry, locationRuntime, locationConnectionRuntime, worldId);
            }

            TravelConditionRuntime travelConditionRuntime = travelConditions ?? new TravelConditionRuntime();
            travelConditionRuntime.Configure(definitionRegistry, locationRouteRuntime, null, worldId);
            locationRouteRuntime.Configure(definitionRegistry, locationRuntime, locationConnectionRuntime, worldId, travelConditionRuntime);
            TravelJourneyRuntime travelJourneyRuntime = travelJourneys ?? new TravelJourneyRuntime();
            travelJourneyRuntime.Configure(definitionRegistry, locationRuntime, entityLocationRuntime, locationConnectionRuntime, locationRouteRuntime, worldId, travelConditionRuntime);
            travelConditionRuntime.Configure(definitionRegistry, locationRouteRuntime, travelJourneyRuntime, worldId);

            RelationshipRuntime relationshipRuntime = relationships ?? new RelationshipRuntime();
            relationshipRuntime.Configure(definitionRegistry, persons);
            InterpersonalAttitudeRuntime attitudeRuntime = attitudes ?? new InterpersonalAttitudeRuntime();
            attitudeRuntime.Configure(definitionRegistry, persons);
            ReputationRuntime reputationRuntime = reputation ?? new ReputationRuntime();
            reputationRuntime.Configure(definitionRegistry, persons);
            RumorRuntime rumorRuntime = rumors ?? new RumorRuntime();
            rumorRuntime.Configure(
                definitionRegistry,
                persons,
                requestedPersonId => string.Equals(requestedPersonId, personId, StringComparison.Ordinal) ? knowledge : null,
                requestedPersonId => string.Equals(requestedPersonId, personId, StringComparison.Ordinal) ? memory : null);
            SocialInteractionRuntime interactionRuntime = socialInteractions ?? new SocialInteractionRuntime();
            interactionRuntime.Configure(definitionRegistry, persons, relationshipRuntime, attitudeRuntime, reputationRuntime, rumorRuntime);
            SocialNormRuntime normRuntime = socialNorms ?? new SocialNormRuntime();
            normRuntime.Configure(definitionRegistry, persons, relationshipRuntime, attitudeRuntime, reputationRuntime, rumorRuntime, interactionRuntime);
            SocialNetworkRuntime networkRuntime = socialNetworks ?? new SocialNetworkRuntime();
            networkRuntime.Configure(definitionRegistry, persons, relationshipRuntime, attitudeRuntime, reputationRuntime, rumorRuntime, interactionRuntime, normRuntime);
            SocialDecisionRuntime decisionRuntime = socialDecisions ?? new SocialDecisionRuntime();
            SocialInfluenceRuntime influenceRuntime = socialInfluence ?? new SocialInfluenceRuntime();
            influenceRuntime.Configure(definitionRegistry, persons, attitudeRuntime, reputationRuntime, interactionRuntime, new[] { knowledge });
            SocialEmotionRuntime emotionRuntime = socialEmotions ?? new SocialEmotionRuntime();
            emotionRuntime.Configure(definitionRegistry, persons, relationshipRuntime, attitudeRuntime, reputationRuntime, rumorRuntime, interactionRuntime, normRuntime, networkRuntime, influenceRuntime);
            decisionRuntime.Configure(definitionRegistry, persons, interactionRuntime, relationshipRuntime, attitudeRuntime, reputationRuntime, rumorRuntime, normRuntime, networkRuntime, SocialDecisionModifierSourceCollection.Compose(influenceRuntime, emotionRuntime));
            FamilyRelationshipRuntime familyRuntime = familyRelationships ?? new FamilyRelationshipRuntime();
            familyRuntime.Configure(definitionRegistry, persons, relationshipRuntime, attitudeRuntime, interactionRuntime, worldId, AdultPersons(persons));
            OrganizationRuntime organizationRuntime = organizations ?? new OrganizationRuntime();
            if (organizations == null)
            {
                PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(organizationRuntime, definitionRegistry, worldId);
            }

            organizationRuntime.Configure(definitionRegistry, worldId, persons, Array.Empty<string>());
            OrganizationMembershipRuntime membershipRuntime = organizationMemberships ?? new OrganizationMembershipRuntime();
            membershipRuntime.Configure(definitionRegistry, organizationRuntime, worldId, persons, DefaultOrganizationIds);
            OrganizationAuthorityRuntime authorityRuntime = organizationAuthority ?? new OrganizationAuthorityRuntime();
            authorityRuntime.Configure(definitionRegistry, organizationRuntime, membershipRuntime, worldId, persons, DefaultOrganizationIds);
            ItemInstanceIdentityRuntime itemRuntime = itemInstances ?? new ItemInstanceIdentityRuntime();
            OrganizationResourceRuntime resourceRuntime = organizationResources ?? new OrganizationResourceRuntime();
            resourceRuntime.Configure(definitionRegistry, organizationRuntime, authorityRuntime, economyRuntime, worldId, propertyRuntime, businessRuntime, itemRuntime, contractRuntime, payrollRuntime);
            OrganizationDecisionRuntime organizationDecisionRuntime = organizationDecisions ?? new OrganizationDecisionRuntime();
            organizationDecisionRuntime.Configure(definitionRegistry, organizationRuntime, membershipRuntime, authorityRuntime, resourceRuntime, worldId, persons, economyRuntime);
            FactionRuntime factionRuntime = factions ?? new FactionRuntime();
            factionRuntime.Configure(definitionRegistry, organizationRuntime, membershipRuntime, authorityRuntime, resourceRuntime, organizationDecisionRuntime, worldId, persons);
            DiplomacyRuntime diplomacyRuntime = diplomacy ?? new DiplomacyRuntime();
            diplomacyRuntime.Configure(definitionRegistry, organizationRuntime, factionRuntime, authorityRuntime, organizationDecisionRuntime, resourceRuntime, worldId, persons);
            GovernmentRuntime governmentRuntime = governments ?? new GovernmentRuntime();
            governmentRuntime.Configure(definitionRegistry, organizationRuntime, membershipRuntime, authorityRuntime, organizationDecisionRuntime, resourceRuntime, factionRuntime, diplomacyRuntime, propertyRuntime, worldId, persons, Array.Empty<string>());
            LegalRuntime legalRuntime = laws ?? new LegalRuntime();
            legalRuntime.Configure(definitionRegistry, governmentRuntime, organizationRuntime, authorityRuntime, organizationDecisionRuntime, diplomacyRuntime, propertyRuntime, worldId, persons, Array.Empty<string>());
            CrimeRuntime crimeRuntime = crimes ?? new CrimeRuntime();
            crimeRuntime.Configure(definitionRegistry, governmentRuntime, legalRuntime, authorityRuntime, diplomacyRuntime, worldId, persons, Array.Empty<string>());
            JusticeRuntime justiceRuntime = justice ?? new JusticeRuntime();
            justiceRuntime.Configure(definitionRegistry, governmentRuntime, legalRuntime, organizationRuntime, authorityRuntime, crimeRuntime, worldId, persons, Array.Empty<string>());
            PoliticalTravelRuntime politicalTravelRuntime = politicalTravel ?? new PoliticalTravelRuntime();
            politicalTravelRuntime.Configure(definitionRegistry, governmentRuntime, legalRuntime, crimeRuntime, justiceRuntime, locationRuntime, locationRouteRuntime, worldId);
            return new TestLabRuntimeBundle(definitionRegistry, personId, worldId, persons, knownBodyIds, knowledge, history, memory, sources, transfers, access, records, itemRuntime, itemCompositions ?? new ItemCompositionRuntime(), itemQualityAffixes ?? new ItemQualityAffixRuntime(), itemDurability ?? new ItemDurabilityRuntime(), productionRequirements ?? new ProductionRequirementRuntime(), recipeKnowledge ?? new RecipeKnowledgeRuntime(), craftingExecution ?? new CraftingExecutionRuntime(), productionWorkflow ?? new ProductionWorkflowRuntime(), experimentation ?? new ExperimentationRuntime(), professionRuntime, entryRuntime, trainingRuntime, professionalActivityRuntime, credentialRuntime, rankRuntime, positionRuntime, careerRuntime, lifePathRuntime, economyRuntime, marketRuntime, tradeRuntime, payrollRuntime, businessRuntime, propertyRuntime, contractRuntime, institutionalRevenueRuntime, regionalFlowRuntime, relationshipRuntime, attitudeRuntime, reputationRuntime, rumorRuntime, interactionRuntime, normRuntime, networkRuntime, decisionRuntime, influenceRuntime, emotionRuntime, familyRuntime, organizationRuntime, membershipRuntime, authorityRuntime, resourceRuntime, organizationDecisionRuntime, factionRuntime, diplomacyRuntime, governmentRuntime, legalRuntime, crimeRuntime, justiceRuntime, locationRuntime, entityLocationRuntime, interactionPointRuntime, locationConnectionRuntime, locationRouteRuntime, travelJourneyRuntime, travelConditionRuntime, politicalTravelRuntime, null);
        }

        public static TestLabRuntimeBundle CreateFresh(
            DefinitionRegistry definitionRegistry,
            string personId,
            string worldId,
            IReadOnlyList<string> knownPersonIds,
            IReadOnlyList<string> knownBodyIds,
            string objectName = "Test Lab Fresh Knowledge Runtime")
        {
            GameObject knowledgeObject = new GameObject(string.IsNullOrWhiteSpace(objectName) ? "Test Lab Fresh Knowledge Runtime" : objectName);
            knowledgeObject.hideFlags = HideFlags.HideAndDontSave;
            PersonKnowledgeRuntime knowledge = knowledgeObject.AddComponent<PersonKnowledgeRuntime>();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            InformationSourceRuntime sources = new InformationSourceRuntime();
            InformationTransferRuntime transfers = new InformationTransferRuntime();
            InformationAccessRuntime access = new InformationAccessRuntime();
            KnowledgeRecordRuntime records = new KnowledgeRecordRuntime();
            ItemInstanceIdentityRuntime itemInstances = new ItemInstanceIdentityRuntime();
            ItemCompositionRuntime itemCompositions = new ItemCompositionRuntime();
            ItemQualityAffixRuntime itemQualityAffixes = new ItemQualityAffixRuntime();
            ItemDurabilityRuntime itemDurability = new ItemDurabilityRuntime();
            ProductionRequirementRuntime productionRequirements = new ProductionRequirementRuntime();
            RecipeKnowledgeRuntime recipeKnowledge = new RecipeKnowledgeRuntime();
            CraftingExecutionRuntime craftingExecution = new CraftingExecutionRuntime();
            ProductionWorkflowRuntime productionWorkflow = new ProductionWorkflowRuntime();
            ExperimentationRuntime experimentation = new ExperimentationRuntime();
            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            ProfessionEntryRuntime professionEntries = new ProfessionEntryRuntime();
            TrainingRuntime training = new TrainingRuntime();
            ProfessionalActivityRuntime professionalActivities = new ProfessionalActivityRuntime();
            CredentialRuntime credentials = new CredentialRuntime();
            ProfessionalRankRuntime professionalRanks = new ProfessionalRankRuntime();
            PositionEmploymentRuntime positionEmployment = new PositionEmploymentRuntime();
            CareerHistoryRuntime careerHistory = new CareerHistoryRuntime();
            LifePathRuntime lifePaths = new LifePathRuntime();
            EconomyRuntime economy = new EconomyRuntime();
            MarketRuntime markets = new MarketRuntime();
            TradeRuntime trades = new TradeRuntime();
            PayrollRuntime payroll = new PayrollRuntime();
            BusinessRuntime businesses = new BusinessRuntime();
            PropertyRuntime properties = new PropertyRuntime();
            ContractEconomyRuntime contracts = new ContractEconomyRuntime();
            InstitutionalRevenueRuntime institutionalRevenue = new InstitutionalRevenueRuntime();
            RegionalFlowRuntime regionalFlow = new RegionalFlowRuntime();
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            RumorRuntime rumors = new RumorRuntime();
            SocialInteractionRuntime socialInteractions = new SocialInteractionRuntime();
            SocialNormRuntime socialNorms = new SocialNormRuntime();
            SocialNetworkRuntime socialNetworks = new SocialNetworkRuntime();
            SocialDecisionRuntime socialDecisions = new SocialDecisionRuntime();
            SocialInfluenceRuntime socialInfluence = new SocialInfluenceRuntime();
            SocialEmotionRuntime socialEmotions = new SocialEmotionRuntime();
            FamilyRelationshipRuntime familyRelationships = new FamilyRelationshipRuntime();
            OrganizationRuntime organizations = new OrganizationRuntime();
            OrganizationMembershipRuntime organizationMemberships = new OrganizationMembershipRuntime();
            OrganizationAuthorityRuntime organizationAuthority = new OrganizationAuthorityRuntime();
            OrganizationResourceRuntime organizationResources = new OrganizationResourceRuntime();
            OrganizationDecisionRuntime organizationDecisions = new OrganizationDecisionRuntime();
            FactionRuntime factions = new FactionRuntime();
            DiplomacyRuntime diplomacy = new DiplomacyRuntime();
            GovernmentRuntime governments = new GovernmentRuntime();
            LegalRuntime laws = new LegalRuntime();
            CrimeRuntime crimes = new CrimeRuntime();
            JusticeRuntime justice = new JusticeRuntime();
            LocationRuntime locations = new LocationRuntime();
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            InteractionPointRuntime interactionPoints = new InteractionPointRuntime();
            LocationConnectionRuntime locationConnections = new LocationConnectionRuntime();
            LocationRouteRuntime locationRoutes = new LocationRouteRuntime();
            TravelJourneyRuntime travelJourneys = new TravelJourneyRuntime();
            TravelConditionRuntime travelConditions = new TravelConditionRuntime();
            PoliticalTravelRuntime politicalTravel = new PoliticalTravelRuntime();

            string[] persons = ExpandKnownPersons(knownPersonIds, personId);
            string[] bodies = (knownBodyIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            history.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, bodies);
            knowledge.Configure(definitionRegistry, personId);
            memory.Configure(personId, definitionRegistry, history, persons);
            sources.Configure(definitionRegistry, personId);
            transfers.Configure(definitionRegistry, personId);
            access.Configure(definitionRegistry, personId);
            records.Configure(definitionRegistry, personId);
            professions.Configure(definitionRegistry, persons);
            professionEntries.Configure(definitionRegistry, professions, persons);
            training.Configure(definitionRegistry, professions, transfers, persons);
            professionalActivities.Configure(definitionRegistry, professions, persons);
            credentials.Configure(definitionRegistry, professions, training, professionalActivities, persons, DefaultCredentialAuthorityIds);
            professionalRanks.Configure(definitionRegistry, professions, training, professionalActivities, credentials, persons, DefaultCredentialAuthorityIds);
            positionEmployment.Configure(definitionRegistry, professions, training, professionalActivities, credentials, professionalRanks, persons, DefaultOrganizationIds, DefaultCredentialAuthorityIds);
            careerHistory.Configure(definitionRegistry, professions, training, professionalActivities, credentials, professionalRanks, positionEmployment, persons, DefaultOrganizationIds, DefaultCredentialAuthorityIds);
            lifePaths.Configure(definitionRegistry, professions, training, professionalActivities, credentials, professionalRanks, positionEmployment, careerHistory, persons, DefaultOrganizationIds);
            economy.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            markets.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            trades.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            payroll.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            businesses.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            properties.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            contracts.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            institutionalRevenue.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            regionalFlow.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            locations.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactionPoints, definitionRegistry, locations, entityLocations, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(locationConnections, definitionRegistry, locations, entityLocations, interactionPoints, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(locationRoutes, definitionRegistry, locations, locationConnections, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            travelConditions.Configure(definitionRegistry, locationRoutes, null, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            locationRoutes.Configure(definitionRegistry, locations, locationConnections, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, travelConditions);
            travelJourneys.Configure(definitionRegistry, locations, entityLocations, locationConnections, locationRoutes, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, travelConditions);
            travelConditions.Configure(definitionRegistry, locationRoutes, travelJourneys, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            relationships.Configure(definitionRegistry, persons);
            attitudes.Configure(definitionRegistry, persons);
            reputation.Configure(definitionRegistry, persons);
            rumors.Configure(
                definitionRegistry,
                persons,
                requestedPersonId => string.Equals(requestedPersonId, personId, StringComparison.Ordinal) ? knowledge : null,
                requestedPersonId => string.Equals(requestedPersonId, personId, StringComparison.Ordinal) ? memory : null);
            socialInteractions.Configure(definitionRegistry, persons, relationships, attitudes, reputation, rumors);
            socialNorms.Configure(definitionRegistry, persons, relationships, attitudes, reputation, rumors, socialInteractions);
            socialNetworks.Configure(definitionRegistry, persons, relationships, attitudes, reputation, rumors, socialInteractions, socialNorms);
            socialInfluence.Configure(definitionRegistry, persons, attitudes, reputation, socialInteractions, new[] { knowledge });
            socialEmotions.Configure(definitionRegistry, persons, relationships, attitudes, reputation, rumors, socialInteractions, socialNorms, socialNetworks, socialInfluence);
            socialDecisions.Configure(definitionRegistry, persons, socialInteractions, relationships, attitudes, reputation, rumors, socialNorms, socialNetworks, SocialDecisionModifierSourceCollection.Compose(socialInfluence, socialEmotions));
            familyRelationships.Configure(definitionRegistry, persons, relationships, attitudes, socialInteractions, worldId, AdultPersons(persons));
            PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(organizations, definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);
            organizations.Configure(definitionRegistry, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, Array.Empty<string>());
            organizationMemberships.Configure(definitionRegistry, organizations, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, DefaultOrganizationIds);
            organizationAuthority.Configure(definitionRegistry, organizations, organizationMemberships, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, DefaultOrganizationIds);
            organizationResources.Configure(definitionRegistry, organizations, organizationAuthority, economy, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, properties, businesses, itemInstances, contracts, payroll);
            organizationDecisions.Configure(definitionRegistry, organizations, organizationMemberships, organizationAuthority, organizationResources, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, economy);
            factions.Configure(definitionRegistry, organizations, organizationMemberships, organizationAuthority, organizationResources, organizationDecisions, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons);
            diplomacy.Configure(definitionRegistry, organizations, factions, organizationAuthority, organizationDecisions, organizationResources, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons);
            governments.Configure(definitionRegistry, organizations, organizationMemberships, organizationAuthority, organizationDecisions, organizationResources, factions, diplomacy, properties, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, Array.Empty<string>());
            laws.Configure(definitionRegistry, governments, organizations, organizationAuthority, organizationDecisions, diplomacy, properties, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, Array.Empty<string>());
            crimes.Configure(definitionRegistry, governments, laws, organizationAuthority, diplomacy, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, Array.Empty<string>());
            justice.Configure(definitionRegistry, governments, laws, organizations, organizationAuthority, crimes, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId, persons, Array.Empty<string>());
            politicalTravel.Configure(definitionRegistry, governments, laws, crimes, justice, locations, locationRoutes, string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId);

            return new TestLabRuntimeBundle(definitionRegistry, personId, worldId, persons, bodies, knowledge, history, memory, sources, transfers, access, records, itemInstances, itemCompositions, itemQualityAffixes, itemDurability, productionRequirements, recipeKnowledge, craftingExecution, productionWorkflow, experimentation, professions, professionEntries, training, professionalActivities, credentials, professionalRanks, positionEmployment, careerHistory, lifePaths, economy, markets, trades, payroll, businesses, properties, contracts, institutionalRevenue, regionalFlow, relationships, attitudes, reputation, rumors, socialInteractions, socialNorms, socialNetworks, socialDecisions, socialInfluence, socialEmotions, familyRelationships, organizations, organizationMemberships, organizationAuthority, organizationResources, organizationDecisions, factions, diplomacy, governments, laws, crimes, justice, locations, entityLocations, interactionPoints, locationConnections, locationRoutes, travelJourneys, travelConditions, politicalTravel, knowledgeObject);
        }

        private static string[] ExpandKnownPersons(IReadOnlyList<string> knownPersonIds, string ownerPersonId)
        {
            return (knownPersonIds ?? Array.Empty<string>())
                .Concat(new[] { ownerPersonId })
                .Concat(DefaultRelationshipPersonIds)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] AdultPersons(IEnumerable<string> personIds)
        {
            return (personIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Where(value => !value.Contains(".child", StringComparison.Ordinal) && !value.Contains(".dependent", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public KnowledgeHistoryRuntimeSet CreateRuntimeSet()
        {
            return new KnowledgeHistoryRuntimeSet
            {
                DefinitionRegistry = DefinitionRegistry,
                PersonId = PersonId,
                WorldId = WorldId,
                KnownPersonIds = KnownPersonIds,
                KnownBodyIds = KnownBodyIds,
                KnowledgeRuntime = Knowledge,
                HistoryRuntime = History,
                MemoryRuntime = Memory,
                SourceRuntime = Sources,
                TransferRuntime = Transfers,
                AccessRuntime = Access,
                RecordRuntime = Records
            };
        }

        public TestLabRuntimeBundleSnapshot CreateSnapshot()
        {
            return new TestLabRuntimeBundleSnapshot(
                Knowledge?.CreateSaveData(),
                History?.CreateSaveData(),
                Memory?.CreateSaveData(),
                Sources?.CreateSaveData(),
                Transfers?.CreateSaveData(),
                Access?.CreateSaveData(),
                Records?.CreateSaveData(),
                ItemInstances?.CreateSaveData(),
                ItemCompositions?.CreateSaveData(),
                ItemQualityAffixes?.CreateSaveData(),
                ItemDurability?.CreateSaveData(),
                ProductionRequirements?.CreateSaveData(),
                RecipeKnowledge?.CreateSaveData(),
                CraftingExecution?.CreateSaveData(),
                ProductionWorkflow?.CreateSaveData(),
                Experimentation?.CreateSaveData(),
                Professions?.CreateSaveData(),
                ProfessionEntries?.CreateSaveData(),
                Training?.CreateSaveData(),
                ProfessionalActivities?.CreateSaveData(),
                Credentials?.CreateSaveData(),
                ProfessionalRanks?.CreateSaveData(),
                PositionEmployment?.CreateSaveData(),
                CareerHistory?.CreateSaveData(),
                LifePaths?.CreateSaveData(),
                Economy?.CreateSaveData(),
                Markets?.CreateSaveData(),
                Trades?.CreateSaveData(),
                Payroll?.CreateSaveData(),
                Businesses?.CreateSaveData(),
                Properties?.CreateSaveData(),
                Contracts?.CreateSaveData(),
                InstitutionalRevenue?.CreateSaveData(),
                RegionalFlow?.CreateSaveData(),
                Relationships?.CreateSaveData(),
                Attitudes?.CreateSaveData(),
                Reputation?.CreateSaveData(),
                Rumors?.CreateSaveData(),
                SocialInteractions?.CreateSaveData(),
                SocialNorms?.CreateSaveData(),
                SocialNetworks?.CreateSaveData(),
                SocialDecisions?.CreateSaveData(),
                SocialInfluence?.CreateSaveData(),
                SocialEmotions?.CreateSaveData(),
                FamilyRelationships?.CreateSaveData(),
                Organizations?.CreateSaveData(),
                OrganizationMemberships?.CreateSaveData(),
                OrganizationAuthority?.CreateSaveData(),
                OrganizationResources?.CreateSaveData(),
                OrganizationDecisions?.CreateSaveData(),
                Factions?.CreateSaveData(),
                Diplomacy?.CreateSaveData(),
                Governments?.CreateSaveData(),
                Laws?.CreateSaveData(),
                Crimes?.CreateSaveData(),
                Justice?.CreateSaveData(),
                Locations?.CreateSaveData(),
                EntityLocations?.CreateSaveData(),
                InteractionPoints?.CreateSaveData(),
                LocationConnections?.CreateSaveData(),
                LocationRoutes?.CreateSaveData(),
                TravelJourneys?.CreateSaveData(),
                TravelConditions?.CreateSaveData(),
                PoliticalTravel?.CreateSaveData());
        }

        public TestLabRuntimeBundleFingerprint CreateFingerprint()
        {
            return new TestLabRuntimeBundleFingerprint(new[]
            {
                TestLabRuntimeFingerprintSection.FromObject("Knowledge", Knowledge?.KnowledgeRevision ?? 0L, Knowledge?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("History", History?.HistoryRevision ?? 0L, History?.CreateSaveData(), History?.NextSequence ?? 0L),
                TestLabRuntimeFingerprintSection.FromObject("Memory", Memory?.MemoryRevision ?? 0L, Memory?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Sources", Sources?.SourceRevision ?? 0L, Sources?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Transfers", Transfers?.TransferRevision ?? 0L, Transfers?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Access", Access?.AccessRevision ?? 0L, Access?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Records", Records?.RecordRevision ?? 0L, Records?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Items", ItemInstances?.Revision ?? 0L, ItemInstances?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ItemCompositions", ItemCompositions?.Revision ?? 0L, ItemCompositions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ItemQualityAffixes", ItemQualityAffixes?.Revision ?? 0L, ItemQualityAffixes?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ItemDurability", ItemDurability?.Revision ?? 0L, ItemDurability?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ProductionRequirements", ProductionRequirements?.Revision ?? 0L, ProductionRequirements?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("RecipeKnowledge", RecipeKnowledge?.Revision ?? 0L, RecipeKnowledge?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("CraftingExecution", CraftingExecution?.Revision ?? 0L, CraftingExecution?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ProductionWorkflow", ProductionWorkflow?.Revision ?? 0L, ProductionWorkflow?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Experimentation", Experimentation?.Revision ?? 0L, Experimentation?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Professions", Professions?.Revision ?? 0L, Professions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ProfessionEntries", ProfessionEntries?.Revision ?? 0L, ProfessionEntries?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Training", Training?.Revision ?? 0L, Training?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ProfessionalActivities", ProfessionalActivities?.Revision ?? 0L, ProfessionalActivities?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Credentials", Credentials?.Revision ?? 0L, Credentials?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("ProfessionalRanks", ProfessionalRanks?.Revision ?? 0L, ProfessionalRanks?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("PositionEmployment", PositionEmployment?.Revision ?? 0L, PositionEmployment?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("CareerHistory", CareerHistory?.Revision ?? 0L, CareerHistory?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("LifePaths", LifePaths?.Revision ?? 0L, LifePaths?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Economy", Economy?.Revision ?? 0L, Economy?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Markets", Markets?.Revision ?? 0L, Markets?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Trades", Trades?.Revision ?? 0L, Trades?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Payroll", Payroll?.Revision ?? 0L, Payroll?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Businesses", Businesses?.Revision ?? 0L, Businesses?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Properties", Properties?.Revision ?? 0L, Properties?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Contracts", Contracts?.Revision ?? 0L, Contracts?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("InstitutionalRevenue", InstitutionalRevenue?.Revision ?? 0L, InstitutionalRevenue?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("RegionalFlow", RegionalFlow?.Revision ?? 0L, RegionalFlow?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Relationships", Relationships?.Revision ?? 0L, Relationships?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Attitudes", Attitudes?.Revision ?? 0L, Attitudes?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Reputation", Reputation?.Revision ?? 0L, Reputation?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Rumors", Rumors?.Revision ?? 0L, Rumors?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("SocialInteractions", SocialInteractions?.Revision ?? 0L, SocialInteractions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("SocialNorms", SocialNorms?.Revision ?? 0L, SocialNorms?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("SocialNetworks", SocialNetworks?.Revision ?? 0L, SocialNetworks?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("SocialDecisions", SocialDecisions?.Revision ?? 0L, SocialDecisions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("SocialInfluence", SocialInfluence?.Revision ?? 0L, SocialInfluence?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("SocialEmotions", SocialEmotions?.Revision ?? 0L, SocialEmotions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("FamilyRelationships", FamilyRelationships?.Revision ?? 0L, FamilyRelationships?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Organizations", Organizations?.Revision ?? 0L, Organizations?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("OrganizationMemberships", OrganizationMemberships?.Revision ?? 0L, OrganizationMemberships?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("OrganizationAuthority", OrganizationAuthority?.Revision ?? 0L, OrganizationAuthority?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("OrganizationResources", OrganizationResources?.Revision ?? 0L, OrganizationResources?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("OrganizationDecisions", OrganizationDecisions?.Revision ?? 0L, OrganizationDecisions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Factions", Factions?.Revision ?? 0L, Factions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Diplomacy", Diplomacy?.Revision ?? 0L, Diplomacy?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Governments", Governments?.Revision ?? 0L, Governments?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Laws", Laws?.Revision ?? 0L, Laws?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Crimes", Crimes?.Revision ?? 0L, Crimes?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Justice", Justice?.Revision ?? 0L, Justice?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("Locations", Locations?.Revision ?? 0L, Locations?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("EntityLocations", EntityLocations?.Revision ?? 0L, EntityLocations?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("InteractionPoints", InteractionPoints?.Revision ?? 0L, InteractionPoints?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("LocationConnections", LocationConnections?.Revision ?? 0L, LocationConnections?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("LocationRoutes", LocationRoutes?.Revision ?? 0L, LocationRoutes?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("TravelJourneys", TravelJourneys?.Revision ?? 0L, TravelJourneys?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("TravelConditions", TravelConditions?.Revision ?? 0L, TravelConditions?.CreateSaveData()),
                TestLabRuntimeFingerprintSection.FromObject("PoliticalTravel", PoliticalTravel?.Revision ?? 0L, PoliticalTravel?.CreateSaveData())
            });
        }

        public bool RestoreSnapshot(TestLabRuntimeBundleSnapshot snapshot, out string failure)
        {
            failure = string.Empty;
            if (snapshot == null)
            {
                return true;
            }

            if (Knowledge != null && snapshot.Knowledge != null)
            {
                KnowledgeOperationResult result = Knowledge.RestoreFromSaveData(snapshot.Knowledge, DefinitionRegistry, PersonId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Knowledge restore failed: {result.Message}";
                    return false;
                }
            }

            if (History != null && snapshot.History != null)
            {
                HistoryOperationResult result = History.RestoreFromSaveData(snapshot.History, DefinitionRegistry, KnownPersonIds, KnownBodyIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"History restore failed: {result.Message}";
                    return false;
                }
            }

            if (Memory != null && snapshot.Memory != null)
            {
                HistoryOperationResult result = Memory.RestoreFromSaveData(snapshot.Memory, DefinitionRegistry, History, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Memory restore failed: {result.Message}";
                    return false;
                }
            }

            if (Sources != null && snapshot.Sources != null)
            {
                InformationSourceOperationResult result = Sources.RestoreFromSaveData(snapshot.Sources, DefinitionRegistry, PersonId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Information source restore failed: {result.Message}";
                    return false;
                }
            }

            if (Transfers != null && snapshot.Transfers != null)
            {
                InformationTransferResult result = Transfers.RestoreFromSaveData(snapshot.Transfers, DefinitionRegistry, PersonId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Information transfer restore failed: {result.Message}";
                    return false;
                }
            }

            if (Access != null && snapshot.Access != null)
            {
                InformationAccessOperationResult result = Access.RestoreFromSaveData(snapshot.Access, DefinitionRegistry, PersonId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Information access restore failed: {result.Message}";
                    return false;
                }
            }

            if (Records != null && snapshot.Records != null)
            {
                KnowledgeRecordOperationResult result = Records.RestoreFromSaveData(snapshot.Records, DefinitionRegistry, PersonId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Knowledge record restore failed: {result.Message}";
                    return false;
                }
            }

            if (ItemInstances != null && snapshot.ItemInstances != null)
            {
                ItemInstanceOperationResult result = ItemInstances.RestoreFromSaveData(snapshot.ItemInstances, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Item instance restore failed: {result.Message}";
                    return false;
                }
            }

            if (ItemCompositions != null && snapshot.ItemCompositions != null)
            {
                ItemCompositionOperationResult result = ItemCompositions.RestoreFromSaveData(snapshot.ItemCompositions, DefinitionRegistry, ItemInstances);
                if (!result.Succeeded)
                {
                    failure = $"Item composition restore failed: {result.Message}";
                    return false;
                }
            }

            if (ItemQualityAffixes != null && snapshot.ItemQualityAffixes != null)
            {
                ItemQualityAffixOperationResult result = ItemQualityAffixes.RestoreFromSaveData(snapshot.ItemQualityAffixes, DefinitionRegistry, ItemInstances);
                if (!result.Succeeded)
                {
                    failure = $"Item quality restore failed: {result.Message}";
                    return false;
                }
            }

            if (ItemDurability != null && snapshot.ItemDurability != null)
            {
                ItemDurabilityOperationResult result = ItemDurability.RestoreFromSaveData(snapshot.ItemDurability, DefinitionRegistry, ItemInstances, ItemCompositions);
                if (!result.Succeeded)
                {
                    failure = $"Item durability restore failed: {result.Message}";
                    return false;
                }
            }

            if (ProductionRequirements != null && snapshot.ProductionRequirements != null)
            {
                ProductionRequirementEvaluationResult result = ProductionRequirements.RestoreFromSaveData(snapshot.ProductionRequirements);
                if (!result.Succeeded)
                {
                    failure = $"Production requirement restore failed: {result.Message}";
                    return false;
                }
            }

            if (RecipeKnowledge != null && snapshot.RecipeKnowledge != null)
            {
                if (!RecipeKnowledge.RestoreFromSaveData(snapshot.RecipeKnowledge, DefinitionRegistry, out string recipeFailure))
                {
                    failure = $"Recipe knowledge restore failed: {recipeFailure}";
                    return false;
                }
            }

            if (CraftingExecution != null && snapshot.CraftingExecution != null)
            {
                CraftingExecutionResult result = CraftingExecution.RestoreFromSaveData(snapshot.CraftingExecution, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Crafting execution restore failed: {result.Message}";
                    return false;
                }
            }

            if (ProductionWorkflow != null && snapshot.ProductionWorkflow != null)
            {
                ProductionWorkflowResult result = ProductionWorkflow.RestoreFromSaveData(snapshot.ProductionWorkflow, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Production workflow restore failed: {result.Message}";
                    return false;
                }
            }

            if (Experimentation != null && snapshot.Experimentation != null)
            {
                ExperimentationResult result = Experimentation.RestoreFromSaveData(snapshot.Experimentation, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Experimentation restore failed: {result.Message}";
                    return false;
                }
            }

            if (Professions != null && snapshot.Professions != null)
            {
                ProfessionOperationResult result = Professions.RestoreFromSaveData(snapshot.Professions, DefinitionRegistry, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Profession restore failed: {result.Message}";
                    return false;
                }
            }

            if (ProfessionEntries != null && snapshot.ProfessionEntries != null)
            {
                ProfessionEntryOperationResult result = ProfessionEntries.RestoreFromSaveData(snapshot.ProfessionEntries, DefinitionRegistry, Professions, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Profession entry restore failed: {result.Message}";
                    return false;
                }
            }

            if (Training != null && snapshot.Training != null)
            {
                TrainingOperationResult result = Training.RestoreFromSaveData(snapshot.Training, DefinitionRegistry, Professions, Transfers, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Training restore failed: {result.Message}";
                    return false;
                }
            }

            if (ProfessionalActivities != null && snapshot.ProfessionalActivities != null)
            {
                ProfessionalActivityOperationResult result = ProfessionalActivities.RestoreFromSaveData(snapshot.ProfessionalActivities, DefinitionRegistry, Professions, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Professional activity restore failed: {result.Message}";
                    return false;
                }
            }

            if (Credentials != null && snapshot.Credentials != null)
            {
                CredentialOperationResult result = Credentials.RestoreFromSaveData(snapshot.Credentials, DefinitionRegistry, Professions, Training, ProfessionalActivities, KnownPersonIds, DefaultCredentialAuthorityIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Credential restore failed: {result.Message}";
                    return false;
                }
            }

            if (ProfessionalRanks != null && snapshot.ProfessionalRanks != null)
            {
                ProfessionalRankOperationResult result = ProfessionalRanks.RestoreFromSaveData(snapshot.ProfessionalRanks, DefinitionRegistry, Professions, Training, ProfessionalActivities, Credentials, KnownPersonIds, DefaultCredentialAuthorityIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Professional rank restore failed: {result.Message}";
                    return false;
                }
            }

            if (PositionEmployment != null && snapshot.PositionEmployment != null)
            {
                PositionEmploymentOperationResult result = PositionEmployment.RestoreFromSaveData(snapshot.PositionEmployment, DefinitionRegistry, Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, KnownPersonIds, DefaultOrganizationIds, DefaultCredentialAuthorityIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Position employment restore failed: {result.Message}";
                    return false;
                }
            }

            if (CareerHistory != null && snapshot.CareerHistory != null)
            {
                CareerHistoryOperationResult result = CareerHistory.RestoreFromSaveData(snapshot.CareerHistory, DefinitionRegistry, Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, KnownPersonIds, DefaultOrganizationIds, DefaultCredentialAuthorityIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Career history restore failed: {result.Message}";
                    return false;
                }
            }

            if (LifePaths != null && snapshot.LifePaths != null)
            {
                LifePathOperationResult result = LifePaths.RestoreFromSaveData(snapshot.LifePaths, DefinitionRegistry, Professions, Training, ProfessionalActivities, Credentials, ProfessionalRanks, PositionEmployment, CareerHistory, KnownPersonIds, DefaultOrganizationIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Life path restore failed: {result.Message}";
                    return false;
                }
            }

            if (Economy != null && snapshot.Economy != null)
            {
                EconomyOperationResult result = Economy.RestoreFromSaveData(snapshot.Economy, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Economy restore failed: {result.Message}";
                    return false;
                }
            }

            if (Markets != null && snapshot.Markets != null)
            {
                MarketOperationResult result = Markets.RestoreFromSaveData(snapshot.Markets, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Market restore failed: {result.Message}";
                    return false;
                }
            }

            if (Trades != null && snapshot.Trades != null)
            {
                TradeOperationResult result = Trades.RestoreFromSaveData(snapshot.Trades, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Trade restore failed: {result.Message}";
                    return false;
                }
            }

            if (Payroll != null && snapshot.Payroll != null)
            {
                PayrollOperationResult result = Payroll.RestoreFromSaveData(snapshot.Payroll, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Payroll restore failed: {result.Message}";
                    return false;
                }
            }

            if (Businesses != null && snapshot.Businesses != null)
            {
                BusinessOperationResult result = Businesses.RestoreFromSaveData(snapshot.Businesses, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Business restore failed: {result.Message}";
                    return false;
                }
            }

            if (Properties != null && snapshot.Properties != null)
            {
                PropertyOperationResult result = Properties.RestoreFromSaveData(snapshot.Properties, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Property restore failed: {result.Message}";
                    return false;
                }
            }

            if (Contracts != null && snapshot.Contracts != null)
            {
                ContractEconomyOperationResult result = Contracts.RestoreFromSaveData(snapshot.Contracts, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Contract restore failed: {result.Message}";
                    return false;
                }
            }

            if (InstitutionalRevenue != null && snapshot.InstitutionalRevenue != null)
            {
                InstitutionalRevenueOperationResult result = InstitutionalRevenue.RestoreFromSaveData(snapshot.InstitutionalRevenue, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Institutional revenue restore failed: {result.Message}";
                    return false;
                }
            }

            if (RegionalFlow != null && snapshot.RegionalFlow != null)
            {
                RegionalFlowOperationResult result = RegionalFlow.RestoreFromSaveData(snapshot.RegionalFlow, DefinitionRegistry);
                if (!result.Succeeded)
                {
                    failure = $"Regional flow restore failed: {result.Message}";
                    return false;
                }
            }

            if (Locations != null && snapshot.Locations != null)
            {
                LocationOperationResult result = Locations.RestoreFromSaveData(snapshot.Locations, DefinitionRegistry, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Location restore failed: {result.Message}";
                    return false;
                }
            }

            if (EntityLocations != null && snapshot.EntityLocations != null)
            {
                EntityLocationOperationResult result = EntityLocations.RestoreFromSaveData(snapshot.EntityLocations, Locations, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Entity location restore failed: {result.Message}";
                    return false;
                }
            }

            if (InteractionPoints != null && snapshot.InteractionPoints != null)
            {
                InteractionPointOperationResult result = InteractionPoints.RestoreFromSaveData(snapshot.InteractionPoints, Locations, EntityLocations, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Interaction point restore failed: {result.Message}";
                    return false;
                }
            }

            if (LocationConnections != null && snapshot.LocationConnections != null)
            {
                LocationConnectionOperationResult result = LocationConnections.RestoreFromSaveData(snapshot.LocationConnections, Locations, EntityLocations, InteractionPoints, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Location connection restore failed: {result.Message}";
                    return false;
                }
            }

            if (LocationRoutes != null && snapshot.LocationRoutes != null)
            {
                LocationRouteMutationResult result = LocationRoutes.RestoreFromSaveData(snapshot.LocationRoutes, Locations, LocationConnections, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Location route restore failed: {result.Message}";
                    return false;
                }
            }

            if (TravelConditions != null && snapshot.TravelConditions != null)
            {
                TravelConditionOperationResult result = TravelConditions.RestoreFromSaveData(snapshot.TravelConditions, DefinitionRegistry, LocationRoutes, TravelJourneys, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Travel condition restore failed: {result.Message}";
                    return false;
                }
            }

            if (TravelJourneys != null && snapshot.TravelJourneys != null)
            {
                TravelJourneyOperationResult result = TravelJourneys.RestoreFromSaveData(snapshot.TravelJourneys, DefinitionRegistry, Locations, EntityLocations, LocationConnections, LocationRoutes, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Travel journey restore failed: {result.Message}";
                    return false;
                }
            }

            if (Relationships != null && snapshot.Relationships != null)
            {
                RelationshipOperationResult result = Relationships.RestoreFromSaveData(snapshot.Relationships, DefinitionRegistry, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Relationship restore failed: {result.Message}";
                    return false;
                }
            }

            if (Attitudes != null && snapshot.Attitudes != null)
            {
                AttitudeMutationResult result = Attitudes.RestoreFromSaveData(snapshot.Attitudes, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Interpersonal attitude restore failed: {result.Message}";
                    return false;
                }
            }

            if (Reputation != null && snapshot.Reputation != null)
            {
                ReputationMutationResult result = Reputation.RestoreFromSaveData(snapshot.Reputation, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Reputation restore failed: {result.Message}";
                    return false;
                }
            }

            if (Rumors != null && snapshot.Rumors != null)
            {
                RumorOperationResult result = Rumors.RestoreFromSaveData(snapshot.Rumors, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Rumor restore failed: {result.Message}";
                    return false;
                }
            }

            if (SocialInteractions != null && snapshot.SocialInteractions != null)
            {
                SocialInteractionResult result = SocialInteractions.RestoreFromSaveData(snapshot.SocialInteractions, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Social Interaction restore failed: {result.Message}";
                    return false;
                }
            }

            if (SocialNorms != null && snapshot.SocialNorms != null)
            {
                SocialNormEvaluationResult result = SocialNorms.RestoreFromSaveData(snapshot.SocialNorms, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Social Norm restore failed: {result.Message}";
                    return false;
                }
            }

            if (SocialNetworks != null && snapshot.SocialNetworks != null)
            {
                SocialNetworkMutationResult result = SocialNetworks.RestoreFromSaveData(snapshot.SocialNetworks, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Social Network restore failed: {result.Message}";
                    return false;
                }
            }

            if (SocialDecisions != null && snapshot.SocialDecisions != null)
            {
                SocialDecisionResult result = SocialDecisions.RestoreFromSaveData(snapshot.SocialDecisions, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Social Decision restore failed: {result.Message}";
                    return false;
                }
            }

            if (SocialInfluence != null && snapshot.SocialInfluence != null)
            {
                SocialInfluenceResult result = SocialInfluence.RestoreFromSaveData(snapshot.SocialInfluence, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Social Influence restore failed: {result.Message}";
                    return false;
                }
            }

            if (SocialEmotions != null && snapshot.SocialEmotions != null)
            {
                SocialEmotionResult result = SocialEmotions.RestoreFromSaveData(snapshot.SocialEmotions, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Social Emotion restore failed: {result.Message}";
                    return false;
                }
            }

            if (FamilyRelationships != null && snapshot.FamilyRelationships != null)
            {
                RomanticTransitionResult result = FamilyRelationships.RestoreFromSaveData(snapshot.FamilyRelationships, DefinitionRegistry, KnownPersonIds, restoringState: true);
                if (!result.Succeeded)
                {
                    failure = $"Family Relationship restore failed: {result.Message}";
                    return false;
                }
            }

            if (Organizations != null && snapshot.Organizations != null)
            {
                OrganizationOperationResult result = Organizations.RestoreFromSaveData(snapshot.Organizations, DefinitionRegistry, WorldId, KnownPersonIds, Array.Empty<string>(), restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Organization restore failed: {result.Message}";
                    return false;
                }
            }

            if (OrganizationMemberships != null && snapshot.OrganizationMemberships != null)
            {
                OrganizationMembershipOperationResult result = OrganizationMemberships.RestoreFromSaveData(snapshot.OrganizationMemberships, DefinitionRegistry, Organizations, WorldId, KnownPersonIds, DefaultOrganizationIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Organization membership restore failed: {result.Message}";
                    return false;
                }
            }

            if (OrganizationAuthority != null && snapshot.OrganizationAuthority != null)
            {
                OrganizationAuthorityOperationResult result = OrganizationAuthority.RestoreFromSaveData(snapshot.OrganizationAuthority, DefinitionRegistry, Organizations, OrganizationMemberships, WorldId, KnownPersonIds, DefaultOrganizationIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Organization authority restore failed: {result.Message}";
                    return false;
                }
            }

            if (OrganizationResources != null && snapshot.OrganizationResources != null)
            {
                OrganizationResourceOperationResult result = OrganizationResources.RestoreFromSaveData(snapshot.OrganizationResources, DefinitionRegistry, Organizations, OrganizationAuthority, Economy, WorldId, Properties, Businesses, ItemInstances, restoring: true, contractRuntime: Contracts, payrollRuntime: Payroll);
                if (!result.Succeeded)
                {
                    failure = $"Organization resource restore failed: {result.Message}";
                    return false;
                }
            }

            if (OrganizationDecisions != null && snapshot.OrganizationDecisions != null)
            {
                OrganizationDecisionOperationResult result = OrganizationDecisions.RestoreFromSaveData(snapshot.OrganizationDecisions, DefinitionRegistry, Organizations, OrganizationMemberships, OrganizationAuthority, OrganizationResources, WorldId, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Organization decision restore failed: {result.Message}";
                    return false;
                }
            }

            if (Factions != null && snapshot.Factions != null)
            {
                FactionOperationResult result = Factions.RestoreFromSaveData(snapshot.Factions, DefinitionRegistry, Organizations, OrganizationMemberships, OrganizationAuthority, OrganizationResources, OrganizationDecisions, WorldId, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Faction restore failed: {result.Message}";
                    return false;
                }
            }

            if (Diplomacy != null && snapshot.Diplomacy != null)
            {
                DiplomacyOperationResult result = Diplomacy.RestoreFromSaveData(snapshot.Diplomacy, DefinitionRegistry, Organizations, Factions, OrganizationAuthority, OrganizationDecisions, OrganizationResources, WorldId, KnownPersonIds, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Diplomacy restore failed: {result.Message}";
                    return false;
                }
            }

            if (Governments != null && snapshot.Governments != null)
            {
                PoliticalOperationResult result = Governments.RestoreFromSaveData(snapshot.Governments, DefinitionRegistry, Organizations, OrganizationMemberships, OrganizationAuthority, OrganizationDecisions, OrganizationResources, Factions, Diplomacy, Properties, WorldId, KnownPersonIds, Array.Empty<string>(), restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Government restore failed: {result.Message}";
                    return false;
                }
            }

            if (Laws != null && snapshot.Laws != null)
            {
                LegalOperationResult result = Laws.RestoreFromSaveData(snapshot.Laws, DefinitionRegistry, Governments, Organizations, OrganizationAuthority, OrganizationDecisions, Diplomacy, Properties, WorldId, KnownPersonIds, Array.Empty<string>());
                if (!result.Succeeded)
                {
                    failure = $"Legal restore failed: {result.Message}";
                    return false;
                }
            }

            if (Crimes != null && snapshot.Crimes != null)
            {
                CrimeOperationResult result = Crimes.RestoreFromSaveData(snapshot.Crimes, DefinitionRegistry, Governments, Laws, OrganizationAuthority, Diplomacy, WorldId, KnownPersonIds, Array.Empty<string>());
                if (!result.Succeeded)
                {
                    failure = $"Crime restore failed: {result.Message}";
                    return false;
                }
            }

            if (Justice != null && snapshot.Justice != null)
            {
                JusticeOperationResult result = Justice.RestoreFromSaveData(snapshot.Justice, DefinitionRegistry, Governments, Laws, Organizations, OrganizationAuthority, Crimes, WorldId, KnownPersonIds, Array.Empty<string>());
                if (!result.Succeeded)
                {
                    failure = $"Justice restore failed: {result.Message}";
                    return false;
                }
            }

            if (PoliticalTravel != null && snapshot.PoliticalTravel != null)
            {
                PoliticalTravelOperationResult result = PoliticalTravel.RestoreFromSaveData(snapshot.PoliticalTravel, Governments, Laws, Crimes, Locations, LocationRoutes, WorldId, restoring: true);
                if (!result.Succeeded)
                {
                    failure = $"Political travel restore failed: {result.Message}";
                    return false;
                }
            }

            return true;
        }

        private static readonly string[] DefaultCredentialAuthorityIds =
        {
            "authority.guild.prototype",
            "authority.medical.prototype",
            "authority.government.prototype",
            "authority.school.prototype",
            ProfessionContentIds.PositionAppointAuthorityId,
            ProfessionContentIds.PositionDutyAssignAuthorityId,
            ProfessionContentIds.PositionSuperviseAuthorityId,
            ProfessionContentIds.PositionRestrictedRecordsAuthorityId,
            ProfessionContentIds.BlacksmithTeachPermissionId,
            ProfessionContentIds.ForgeRestrictedStationPermissionId,
            "organization.prototype.guild",
            "organization.prototype.royal-forge",
            "organization.prototype.temple",
            "organization.prototype.university",
            "organization.prototype.government",
            "organization.prototype.independent",
            PersistenceService.LocalPlayerId
        };

        private static readonly string[] DefaultOrganizationIds =
            PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds.Concat(new[] { PersistenceService.LocalPlayerId }).ToArray();

        public void Dispose()
        {
            PoliticalTravel?.Dispose();
            TravelConditions?.Dispose();
            TravelJourneys?.Dispose();
            LocationRoutes?.Dispose();
            LocationConnections?.Dispose();
            InteractionPoints?.Dispose();
            EntityLocations?.Dispose();
            Locations?.Dispose();
            Justice?.Dispose();
            Crimes?.Dispose();
            Laws?.Dispose();
            Governments?.Dispose();
            Diplomacy?.Dispose();
            Factions?.Dispose();
            OrganizationDecisions?.Dispose();
            OrganizationResources?.Dispose();
            if (ownedKnowledgeObject == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(ownedKnowledgeObject);
                return;
            }
#endif
            UnityEngine.Object.Destroy(ownedKnowledgeObject);
        }
    }

    public sealed class TestLabRuntimeBundleSnapshot
    {
        public TestLabRuntimeBundleSnapshot(
            PersonKnowledgeSaveData knowledge,
            AuthoritativeHistorySaveData history,
            PersonMemorySaveData memory,
            InformationSourceSaveData sources,
            InformationTransferSaveData transfers,
            InformationAccessSaveData access,
            KnowledgeRecordSaveData records,
            ItemInstanceRuntimeSaveData itemInstances,
            ItemCompositionRuntimeSaveData itemCompositions,
            ItemQualityAffixRuntimeSaveData itemQualityAffixes,
            ItemDurabilityRuntimeSaveData itemDurability,
            ProductionRequirementRuntimeSaveData productionRequirements,
            RecipeKnowledgeSaveData recipeKnowledge,
            CraftingExecutionRuntimeSaveData craftingExecution,
            ProductionWorkflowRuntimeSaveData productionWorkflow,
            ExperimentationRuntimeSaveData experimentation,
            PersonProfessionRuntimeSaveData professions,
            ProfessionEntryRuntimeSaveData professionEntries,
            TrainingRuntimeSaveData training,
            ProfessionalActivityRuntimeSaveData professionalActivities,
            CredentialRuntimeSaveData credentials,
            ProfessionalRankRuntimeSaveData professionalRanks,
            PositionEmploymentRuntimeSaveData positionEmployment,
            CareerHistoryRuntimeSaveData careerHistory,
            LifePathRuntimeSaveData lifePaths,
            EconomyRuntimeSaveData economy,
            MarketRuntimeSaveData markets,
            TradeRuntimeSaveData trades,
            PayrollRuntimeSaveData payroll,
            BusinessRuntimeSaveData businesses,
            PropertyRuntimeSaveData properties,
            ContractRuntimeSaveData contracts,
            InstitutionalRevenueRuntimeSaveData institutionalRevenue,
            RegionalFlowRuntimeSaveData regionalFlow,
            RelationshipRuntimeSaveData relationships,
            InterpersonalAttitudeRuntimeSaveData attitudes,
            ReputationRuntimeSaveData reputation,
            RumorRuntimeSaveData rumors,
            SocialInteractionRuntimeSaveData socialInteractions,
            SocialNormRuntimeSaveData socialNorms,
            SocialNetworkRuntimeSaveData socialNetworks,
            SocialDecisionRuntimeSaveData socialDecisions,
            SocialInfluenceRuntimeSaveData socialInfluence,
            SocialEmotionRuntimeSaveData socialEmotions,
            FamilyRelationshipRuntimeSaveData familyRelationships,
            OrganizationRuntimeSaveData organizations,
            OrganizationMembershipRuntimeSaveData organizationMemberships,
            OrganizationAuthorityRuntimeSaveData organizationAuthority,
            OrganizationResourceRuntimeSaveData organizationResources,
            OrganizationDecisionRuntimeSaveData organizationDecisions,
            FactionRuntimeSaveData factions,
            DiplomacyRuntimeSaveData diplomacy,
            GovernmentRuntimeSaveData governments,
            LegalRuntimeSaveData laws,
            CrimeRuntimeSaveData crimes,
            JusticeRuntimeSaveData justice,
            LocationRuntimeSaveData locations,
            EntityLocationRuntimeSaveData entityLocations,
            InteractionPointRuntimeSaveData interactionPoints,
            LocationConnectionRuntimeSaveData locationConnections,
            LocationRouteRuntimeSaveData locationRoutes,
            TravelJourneyRuntimeSaveData travelJourneys,
            TravelConditionRuntimeSaveData travelConditions,
            PoliticalTravelRuntimeSaveData politicalTravel)
        {
            Knowledge = knowledge;
            History = history;
            Memory = memory;
            Sources = sources;
            Transfers = transfers;
            Access = access;
            Records = records;
            ItemInstances = itemInstances;
            ItemCompositions = itemCompositions;
            ItemQualityAffixes = itemQualityAffixes;
            ItemDurability = itemDurability;
            ProductionRequirements = productionRequirements;
            RecipeKnowledge = recipeKnowledge;
            CraftingExecution = craftingExecution;
            ProductionWorkflow = productionWorkflow;
            Experimentation = experimentation;
            Professions = professions;
            ProfessionEntries = professionEntries;
            Training = training;
            ProfessionalActivities = professionalActivities;
            Credentials = credentials;
            ProfessionalRanks = professionalRanks;
            PositionEmployment = positionEmployment;
            CareerHistory = careerHistory;
            LifePaths = lifePaths;
            Economy = economy;
            Markets = markets;
            Trades = trades;
            Payroll = payroll;
            Businesses = businesses;
            Properties = properties;
            Contracts = contracts;
            InstitutionalRevenue = institutionalRevenue;
            RegionalFlow = regionalFlow;
            Relationships = relationships;
            Attitudes = attitudes;
            Reputation = reputation;
            Rumors = rumors;
            SocialInteractions = socialInteractions;
            SocialNorms = socialNorms;
            SocialNetworks = socialNetworks;
            SocialDecisions = socialDecisions;
            SocialInfluence = socialInfluence;
            SocialEmotions = socialEmotions;
            FamilyRelationships = familyRelationships;
            Organizations = organizations;
            OrganizationMemberships = organizationMemberships;
            OrganizationAuthority = organizationAuthority;
            OrganizationResources = organizationResources;
            OrganizationDecisions = organizationDecisions;
            Factions = factions;
            Diplomacy = diplomacy;
            Governments = governments;
            Laws = laws;
            Crimes = crimes;
            Justice = justice;
            Locations = locations;
            EntityLocations = entityLocations;
            InteractionPoints = interactionPoints;
            LocationConnections = locationConnections;
            LocationRoutes = locationRoutes;
            TravelJourneys = travelJourneys;
            TravelConditions = travelConditions;
            PoliticalTravel = politicalTravel;
        }

        public PersonKnowledgeSaveData Knowledge { get; }
        public AuthoritativeHistorySaveData History { get; }
        public PersonMemorySaveData Memory { get; }
        public InformationSourceSaveData Sources { get; }
        public InformationTransferSaveData Transfers { get; }
        public InformationAccessSaveData Access { get; }
        public KnowledgeRecordSaveData Records { get; }
        public ItemInstanceRuntimeSaveData ItemInstances { get; }
        public ItemCompositionRuntimeSaveData ItemCompositions { get; }
        public ItemQualityAffixRuntimeSaveData ItemQualityAffixes { get; }
        public ItemDurabilityRuntimeSaveData ItemDurability { get; }
        public ProductionRequirementRuntimeSaveData ProductionRequirements { get; }
        public RecipeKnowledgeSaveData RecipeKnowledge { get; }
        public CraftingExecutionRuntimeSaveData CraftingExecution { get; }
        public ProductionWorkflowRuntimeSaveData ProductionWorkflow { get; }
        public ExperimentationRuntimeSaveData Experimentation { get; }
        public PersonProfessionRuntimeSaveData Professions { get; }
        public ProfessionEntryRuntimeSaveData ProfessionEntries { get; }
        public TrainingRuntimeSaveData Training { get; }
        public ProfessionalActivityRuntimeSaveData ProfessionalActivities { get; }
        public CredentialRuntimeSaveData Credentials { get; }
        public ProfessionalRankRuntimeSaveData ProfessionalRanks { get; }
        public PositionEmploymentRuntimeSaveData PositionEmployment { get; }
        public CareerHistoryRuntimeSaveData CareerHistory { get; }
        public LifePathRuntimeSaveData LifePaths { get; }
        public EconomyRuntimeSaveData Economy { get; }
        public MarketRuntimeSaveData Markets { get; }
        public TradeRuntimeSaveData Trades { get; }
        public PayrollRuntimeSaveData Payroll { get; }
        public BusinessRuntimeSaveData Businesses { get; }
        public PropertyRuntimeSaveData Properties { get; }
        public ContractRuntimeSaveData Contracts { get; }
        public InstitutionalRevenueRuntimeSaveData InstitutionalRevenue { get; }
        public RegionalFlowRuntimeSaveData RegionalFlow { get; }
        public RelationshipRuntimeSaveData Relationships { get; }
        public InterpersonalAttitudeRuntimeSaveData Attitudes { get; }
        public ReputationRuntimeSaveData Reputation { get; }
        public RumorRuntimeSaveData Rumors { get; }
        public SocialInteractionRuntimeSaveData SocialInteractions { get; }
        public SocialNormRuntimeSaveData SocialNorms { get; }
        public SocialNetworkRuntimeSaveData SocialNetworks { get; }
        public SocialDecisionRuntimeSaveData SocialDecisions { get; }
        public SocialInfluenceRuntimeSaveData SocialInfluence { get; }
        public SocialEmotionRuntimeSaveData SocialEmotions { get; }
        public FamilyRelationshipRuntimeSaveData FamilyRelationships { get; }
        public OrganizationRuntimeSaveData Organizations { get; }
        public OrganizationMembershipRuntimeSaveData OrganizationMemberships { get; }
        public OrganizationAuthorityRuntimeSaveData OrganizationAuthority { get; }
        public OrganizationResourceRuntimeSaveData OrganizationResources { get; }
        public OrganizationDecisionRuntimeSaveData OrganizationDecisions { get; }
        public FactionRuntimeSaveData Factions { get; }
        public DiplomacyRuntimeSaveData Diplomacy { get; }
        public GovernmentRuntimeSaveData Governments { get; }
        public LegalRuntimeSaveData Laws { get; }
        public CrimeRuntimeSaveData Crimes { get; }
        public JusticeRuntimeSaveData Justice { get; }
        public LocationRuntimeSaveData Locations { get; }
        public EntityLocationRuntimeSaveData EntityLocations { get; }
        public InteractionPointRuntimeSaveData InteractionPoints { get; }
        public LocationConnectionRuntimeSaveData LocationConnections { get; }
        public LocationRouteRuntimeSaveData LocationRoutes { get; }
        public TravelJourneyRuntimeSaveData TravelJourneys { get; }
        public TravelConditionRuntimeSaveData TravelConditions { get; }
        public PoliticalTravelRuntimeSaveData PoliticalTravel { get; }
    }

    public sealed class TestLabRuntimeBundleFingerprint
    {
        private readonly IReadOnlyList<TestLabRuntimeFingerprintSection> sections;

        public TestLabRuntimeBundleFingerprint(IEnumerable<TestLabRuntimeFingerprintSection> sections)
        {
            this.sections = (sections ?? Array.Empty<TestLabRuntimeFingerprintSection>())
                .Where(section => section != null)
                .OrderBy(section => section.Area, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<TestLabRuntimeFingerprintSection> Sections => sections;

        public IReadOnlyList<TestLabRuntimeFingerprintDiff> Compare(TestLabRuntimeBundleFingerprint other)
        {
            Dictionary<string, TestLabRuntimeFingerprintSection> before = Sections.ToDictionary(section => section.Area, StringComparer.Ordinal);
            Dictionary<string, TestLabRuntimeFingerprintSection> after = (other?.Sections ?? Array.Empty<TestLabRuntimeFingerprintSection>()).ToDictionary(section => section.Area, StringComparer.Ordinal);
            List<TestLabRuntimeFingerprintDiff> diffs = new List<TestLabRuntimeFingerprintDiff>();
            foreach (string area in before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                before.TryGetValue(area, out TestLabRuntimeFingerprintSection prior);
                after.TryGetValue(area, out TestLabRuntimeFingerprintSection current);
                if (!TestLabRuntimeFingerprintSection.SemanticallyEquals(prior, current))
                {
                    diffs.Add(new TestLabRuntimeFingerprintDiff(area, prior, current));
                }
            }

            return diffs;
        }
    }

    public sealed class TestLabRuntimeFingerprintSection
    {
        public TestLabRuntimeFingerprintSection(string area, long revision, long sequence, string contentHash, int contentLength)
        {
            Area = area ?? string.Empty;
            Revision = revision;
            Sequence = sequence;
            ContentHash = contentHash ?? string.Empty;
            ContentLength = contentLength;
        }

        public string Area { get; }
        public long Revision { get; }
        public long Sequence { get; }
        public string ContentHash { get; }
        public int ContentLength { get; }

        public string ToDiagnostic()
        {
            return $"{Area}:rev={Revision};seq={Sequence};hash={ContentHash};len={ContentLength}";
        }

        public static bool SemanticallyEquals(TestLabRuntimeFingerprintSection left, TestLabRuntimeFingerprintSection right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.Area, right.Area, StringComparison.Ordinal)
                && left.Revision == right.Revision
                && left.Sequence == right.Sequence
                && left.ContentLength == right.ContentLength
                && string.Equals(left.ContentHash, right.ContentHash, StringComparison.Ordinal);
        }

        public static TestLabRuntimeFingerprintSection FromObject(string area, long revision, object state, long sequence = 0L)
        {
            string json = state == null ? string.Empty : JsonUtility.ToJson(state);
            return FromText(area, revision, json, sequence);
        }

        public static TestLabRuntimeFingerprintSection FromText(string area, long revision, string state, long sequence = 0L)
        {
            string text = state ?? string.Empty;
            return new TestLabRuntimeFingerprintSection(area, revision, sequence, DeterministicHash(text), text.Length);
        }

        private static string DeterministicHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }
    }

    public sealed class TestLabRuntimeFingerprintDiff
    {
        public TestLabRuntimeFingerprintDiff(string area, TestLabRuntimeFingerprintSection before, TestLabRuntimeFingerprintSection after)
        {
            Area = area ?? string.Empty;
            Before = before;
            After = after;
        }

        public string Area { get; }
        public TestLabRuntimeFingerprintSection Before { get; }
        public TestLabRuntimeFingerprintSection After { get; }

        public string ToDiagnostic()
        {
            return $"{Area} [{Before?.ToDiagnostic() ?? "missing"}] -> [{After?.ToDiagnostic() ?? "missing"}]";
        }
    }

    public sealed class TestLabScenarioContext : IDisposable
    {
        public const string RuntimeBaselineFixtureId = "automation.runtime-baseline";
        public const string MutableStateScopeFixtureId = "automation.scenario-mutable-state";
        public static readonly IReadOnlyList<string> DefaultRequiredFixtureIds = new[] { RuntimeBaselineFixtureId, MutableStateScopeFixtureId };
        private readonly TestLabRuntimeBundle ownedFreshRuntime;
        private readonly IReadOnlyList<string> requiredFixtureIds;
        private readonly Dictionary<string, object> fixturePayloads = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Func<TestLabRuntimeArea, IEnumerable<TestLabRuntimeFingerprintSection>> additionalFingerprintSections;
        private readonly TestLabRuntimeBundleSnapshot restoreSnapshot;
        private readonly TestLabRuntimeBundleFingerprint baselineFingerprint;
        private string cleanupFailure;
        private bool isolationRestored;

        public TestLabScenarioContext(
            string runId,
            string suiteId,
            string scenarioId,
            TestLabScenarioIsolationMode isolationMode,
            TestLabRuntimeBundle runtimeBundle,
            TestLabRuntimeBundle ownedFreshRuntime = null,
            TestLabRuntimeArea requiredRuntimeAreas = TestLabRuntimeArea.KnowledgeHistory,
            IEnumerable<string> requiredFixtureIds = null,
            Func<TestLabRuntimeArea, IEnumerable<TestLabRuntimeFingerprintSection>> additionalFingerprintSections = null)
        {
            RunId = runId ?? string.Empty;
            SuiteId = suiteId ?? string.Empty;
            ScenarioId = scenarioId ?? string.Empty;
            IsolationMode = isolationMode;
            RequiredRuntimeAreas = requiredRuntimeAreas;
            Runtimes = runtimeBundle;
            this.ownedFreshRuntime = ownedFreshRuntime;
            Fixtures = new TestLabFixtureRegistry();
            Ledger = new TestLabFixtureOwnershipLedger();
            this.additionalFingerprintSections = additionalFingerprintSections;
            this.requiredFixtureIds = (requiredFixtureIds ?? DefaultRequiredFixtureIds).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            restoreSnapshot = isolationMode == TestLabScenarioIsolationMode.SnapshotRestore ? runtimeBundle?.CreateSnapshot() : null;
            baselineFingerprint = CreateCurrentFingerprint();
            RegisterCoreFixtures();
        }

        public string RunId { get; }
        public string SuiteId { get; }
        public string ScenarioId { get; }
        public TestLabScenarioIsolationMode IsolationMode { get; }
        public TestLabRuntimeArea RequiredRuntimeAreas { get; }
        public TestLabRuntimeBundle Runtimes { get; }
        public TestLabFixtureRegistry Fixtures { get; }
        public TestLabFixtureOwnershipLedger Ledger { get; }
        public IReadOnlyList<string> RequiredFixtureIds => requiredFixtureIds;
        public string Namespace => Sanitize($"{SuiteId}.{ScenarioId}.{RunId}");

        public string ScopedId(string category, string slug)
        {
            string normalizedCategory = Sanitize(category);
            string normalizedSlug = Sanitize(slug);
            return $"{normalizedCategory}.fixture.{Namespace}.{normalizedSlug}";
        }

        public void SetFixturePayload<TPayload>(string fixtureId, TPayload payload)
        {
            if (string.IsNullOrWhiteSpace(fixtureId))
            {
                return;
            }

            fixturePayloads[fixtureId] = payload;
        }

        public bool TryGetFixturePayload<TPayload>(string fixtureId, out TPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(fixtureId)
                && fixturePayloads.TryGetValue(fixtureId, out object value)
                && value is TPayload typed)
            {
                payload = typed;
                return true;
            }

            payload = default;
            return false;
        }

        public TestLabAutomationStepResult Preflight()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(RunId))
            {
                errors.Add("Scenario run ID is missing.");
            }

            if (string.IsNullOrWhiteSpace(SuiteId))
            {
                errors.Add("Scenario suite ID is missing.");
            }

            if (string.IsNullOrWhiteSpace(ScenarioId))
            {
                errors.Add("Scenario ID is missing.");
            }

            if (RequiredFixtureIds.Count == 0)
            {
                errors.Add("Scenario declares no fixture requirements.");
            }

            if (!CanIsolate(RuntimeIsolationSupportedAreas, RequiredRuntimeAreas)
                && (IsolationMode == TestLabScenarioIsolationMode.FreshRuntime
                    || IsolationMode == TestLabScenarioIsolationMode.SnapshotRestore))
            {
                errors.Add($"{IsolationMode} cannot isolate required runtime areas '{RequiredRuntimeAreas}'. Supported isolated areas: {RuntimeIsolationSupportedAreas}.");
            }

            errors.AddRange(Fixtures.ValidateDependencyGraph());
            if (errors.Count > 0)
            {
                return TestLabAssertions.Fail("fixture.preflight", "Fixture preflight", "CleanFixtureScope", "valid", "invalid", string.Join(" | ", errors));
            }

            foreach (string fixtureId in RequiredFixtureIds)
            {
                TestLabFixtureHandle handle = Fixtures.Require(fixtureId, this);
                if (!handle.Succeeded)
                {
                    string failureDiagnostics = $"Suite={SuiteId} Scenario={ScenarioId} Run={RunId} Isolation={IsolationMode} Fixture={fixtureId} Code={handle.Outcome} {handle.Message}";
                    return TestLabAssertions.Fail("fixture.preflight", "Fixture preflight", "FixturePrepared", "Succeeded", handle.Outcome, failureDiagnostics);
                }
            }

            string diagnostics = $"Isolation={IsolationMode} RuntimeAreas={RequiredRuntimeAreas} Namespace={Namespace} RequiredFixtures={string.Join(",", RequiredFixtureIds)} Fixtures={Fixtures.RegisteredFixtureIds.Count} RuntimeBundle={(Runtimes == null ? "None" : "Available")}.";
            return TestLabAssertions.Pass("fixture.preflight", "Fixture preflight", diagnostics);
        }

        public TestLabAutomationStepResult AuditMutationsBeforeRestore()
        {
            TestLabFixtureLedgerSnapshot snapshot = Ledger.CreateSnapshot();
            if (snapshot.Conflicts.Count > 0)
            {
                return TestLabAssertions.Fail("fixture.audit", "Fixture mutation audit", "NoFixtureConflicts", "0", snapshot.Conflicts.Count, string.Join(" | ", snapshot.Conflicts));
            }

            IReadOnlyList<TestLabRuntimeFingerprintDiff> diffs = baselineFingerprint?.Compare(CreateCurrentFingerprint()) ?? Array.Empty<TestLabRuntimeFingerprintDiff>();
            bool mutableScopeDeclared = RequiredFixtureIds.Any(id => string.Equals(id, MutableStateScopeFixtureId, StringComparison.Ordinal));
            int ownedMutationCount = snapshot.Records.Count(record => !IsCoreScopeRecord(record));
            bool changesAllowed = diffs.Count == 0
                || (mutableScopeDeclared && (IsolationMode == TestLabScenarioIsolationMode.FreshRuntime
                    || IsolationMode == TestLabScenarioIsolationMode.SharedRuntime
                    || IsolationMode == TestLabScenarioIsolationMode.PersistentFixture
                    || ownedMutationCount > 0));

            string diagnostics = $"Suite={SuiteId} Scenario={ScenarioId} Run={RunId} Isolation={IsolationMode} Diffs={diffs.Count} OwnedMutations={ownedMutationCount} LedgerRecords={snapshot.Records.Count} OwnedIds={string.Join(",", Ledger.OwnedStableIds)}";
            if (diffs.Count > 0)
            {
                diagnostics += $" Changes={string.Join(" | ", diffs.Select(diff => diff.ToDiagnostic()))}";
            }

            return changesAllowed
                ? TestLabAssertions.Pass("fixture.audit", "Fixture mutation audit", diagnostics)
                : TestLabAssertions.Fail("fixture.audit", "Fixture mutation audit", "DeclaredMutationsOnly", "declared", "undeclared", diagnostics);
        }

        public TestLabAutomationStepResult RestoreIsolation()
        {
            if (isolationRestored)
            {
                return TestLabAssertions.Pass("fixture.restore", "Fixture restore", $"Isolation={IsolationMode} already restored.");
            }

            isolationRestored = true;
            if (restoreSnapshot != null && Runtimes != null && !Runtimes.RestoreSnapshot(restoreSnapshot, out cleanupFailure))
            {
                cleanupFailure = $"Snapshot restore failed for {SuiteId}/{ScenarioId}: {cleanupFailure}";
                return TestLabAssertions.Fail("fixture.restore", "Fixture restore", "SnapshotRestored", "Succeeded", "RestoreFailed", cleanupFailure);
            }

            return TestLabAssertions.Pass("fixture.restore", "Fixture restore", $"Isolation={IsolationMode} restored.");
        }

        public TestLabAutomationStepResult VerifyRestoredBaseline()
        {
            if (!string.IsNullOrWhiteSpace(cleanupFailure))
            {
                return TestLabAssertions.Fail("fixture.integrity", "Fixture baseline integrity", "CleanFixtureScope", "Succeeded", "CleanupFailed", cleanupFailure);
            }

            if (baselineFingerprint == null || Runtimes == null || IsolationMode != TestLabScenarioIsolationMode.SnapshotRestore)
            {
                return TestLabAssertions.Pass("fixture.integrity", "Fixture baseline integrity", $"Isolation={IsolationMode} no baseline restore comparison required.");
            }

            IReadOnlyList<TestLabRuntimeFingerprintDiff> diffs = baselineFingerprint.Compare(CreateCurrentFingerprint());
            if (diffs.Count > 0)
            {
                return TestLabAssertions.Fail("fixture.integrity", "Fixture baseline integrity", "BaselineRestored", "clean", "dirty", string.Join(" | ", diffs.Select(diff => diff.ToDiagnostic())));
            }

            return TestLabAssertions.Pass("fixture.integrity", "Fixture baseline integrity", $"Isolation={IsolationMode} baseline restored.");
        }

        public void Dispose()
        {
            RestoreIsolation();
            ownedFreshRuntime?.Dispose();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "id";
            }

            char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars).Trim('.', '-');
        }

        private const TestLabRuntimeArea RuntimeIsolationSupportedAreas = TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.Quests;

        private static bool CanIsolate(TestLabRuntimeArea supported, TestLabRuntimeArea required)
        {
            return (required & ~supported) == TestLabRuntimeArea.None;
        }

        private static bool IsCoreScopeRecord(TestLabFixtureMutationRecord record)
        {
            return record == null
                || string.Equals(record.FixtureId, RuntimeBaselineFixtureId, StringComparison.Ordinal)
                || string.Equals(record.FixtureId, MutableStateScopeFixtureId, StringComparison.Ordinal);
        }

        private TestLabRuntimeBundleFingerprint CreateCurrentFingerprint()
        {
            IEnumerable<TestLabRuntimeFingerprintSection> runtimeSections = Runtimes?.CreateFingerprint().Sections ?? Array.Empty<TestLabRuntimeFingerprintSection>();
            IEnumerable<TestLabRuntimeFingerprintSection> additionalSections = additionalFingerprintSections == null
                ? Array.Empty<TestLabRuntimeFingerprintSection>()
                : additionalFingerprintSections(RequiredRuntimeAreas) ?? Array.Empty<TestLabRuntimeFingerprintSection>();
            return new TestLabRuntimeBundleFingerprint(runtimeSections.Concat(additionalSections));
        }

        private void RegisterCoreFixtures()
        {
            TestLabCoreFixtureProviders.RegisterDefaults(this);
            TestLabHistoryFixtureProviders.RegisterDefaults(this);
        }
    }
}
#endif
