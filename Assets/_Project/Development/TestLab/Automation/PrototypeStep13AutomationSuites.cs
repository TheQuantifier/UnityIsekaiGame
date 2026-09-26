#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Organizations.Integration;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(13, "Organizations", 1300)]
    public static class PrototypeStep13AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.1.organization-identity-records",
                "Organization Identity and Records",
                "13.1",
                "Persistent organization records with stable identity, lifecycle, hierarchy, visibility projections, and persistence.",
                13010,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "OrganizationRuntime", "OrganizationDefinition", "OrganizationPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("readiness-and-prototype-definitions", "Organization definitions and prototype records are available", 10,
                        Step("step13-organization-readiness", "Resolve definitions and seeded records", ReadinessAndPrototypeDefinitions)),
                    Scenario("create-rename-lifecycle", "Organizations create, rename, and transition lifecycle deterministically", 20,
                        Step("step13-organization-lifecycle", "Create, rename, duplicate, and transition", CreateRenameLifecycle)),
                    Scenario("links-and-projections", "Organization links and visibility projections enforce boundaries", 30,
                        Step("step13-organization-links", "Link hierarchy and read projections", LinksAndProjections)),
                    Scenario("persistence-validation", "Organization persistence validates before restoring", 40,
                        Step("step13-organization-persistence", "Save, restore, and reject invalid payloads", PersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.2.organization-memberships-ranks-offices",
                "Organization Memberships, Ranks, and Offices",
                "13.2",
                "Persistent organization membership, rank, and office records with idempotent lifecycle operations and persistence validation.",
                13020,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "OrganizationMembershipRuntime", "OrganizationRuntime", "OrganizationMembershipDefinition", "OrganizationRankDefinition", "OrganizationOfficeDefinition", "OrganizationMembershipPersistenceParticipant" },
                scenarios: new[]
                {
                    MembershipScenario("readiness-and-definitions", "Membership, rank, and office definitions are available", 10,
                        Step("step13-membership-readiness", "Resolve organization membership definitions", MembershipReadiness)),
                    MembershipScenario("application-invitation-consent", "Applications, invitations, and acceptance preserve consent boundaries", 20,
                        Step("step13-membership-consent", "Create pending records and require consent", ApplicationInvitationConsent)),
                    MembershipScenario("branch-membership", "Branch membership depends on a parent membership", 30,
                        Step("step13-membership-branch", "Create parent and branch membership", BranchMembership)),
                    MembershipScenario("rank-progression", "Rank assignments are deterministic and ordered", 40,
                        Step("step13-membership-ranks", "Assign and compare organization ranks", RankProgression)),
                    MembershipScenario("office-appointments", "Offices support vacancy, capacity, acting, and joint holders", 50,
                        Step("step13-membership-offices", "Create and assign organization offices", OfficeAppointments)),
                    MembershipScenario("ending-dependencies-idempotence", "Ending membership respects active assignments and duplicate transactions", 60,
                        Step("step13-membership-ending", "End membership with assignment policy", EndingDependenciesAndIdempotence)),
                    MembershipScenario("projection-and-persistence-validation", "Membership projections and persistence reject corrupt graphs without mutation", 70,
                        Step("step13-membership-persistence", "Project, save, restore, and reject invalid membership state", MembershipProjectionAndPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.3.organizational-roles-permissions-authority",
                "Organizational Roles, Permissions, and Institutional Authority",
                "13.3",
                "Definition-backed organization permissions, authority roles, grants, delegations, approvals, projections, and persistence.",
                13030,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "OrganizationAuthorityRuntime", "OrganizationRuntime", "OrganizationMembershipRuntime", "OrganizationPermissionDefinition", "InstitutionalActionDefinition", "OrganizationAuthorityRoleDefinition", "OrganizationAuthorityBindingDefinition", "OrganizationAuthorityPersistenceParticipant" },
                scenarios: new[]
                {
                    AuthorityScenario("readiness-and-definitions", "Authority permissions, roles, actions, and bindings are available", 10,
                        Step("step13-authority-readiness", "Resolve organization authority definitions", AuthorityReadiness)),
                    AuthorityScenario("membership-rank-office-authority", "Membership, rank, and office bindings produce effective authority", 20,
                        Step("step13-authority-bindings", "Evaluate bound membership authority", AuthorityFromMembershipRankOffice)),
                    AuthorityScenario("direct-grants-delegation", "Direct grants and delegations are scoped, expiring, and idempotent", 30,
                        Step("step13-authority-delegation", "Create and delegate scoped authority", AuthorityDirectGrantsDelegation)),
                    AuthorityScenario("branch-scope-boundaries", "Branch authority requires explicit scoped bindings", 40,
                        Step("step13-authority-branch", "Evaluate parent and branch authority boundaries", AuthorityBranchScopeBoundaries)),
                    AuthorityScenario("joint-approval-audits", "Joint approvals are consumed and audited explicitly", 50,
                        Step("step13-authority-approvals", "Authorize joint institutional action", AuthorityJointApprovalAudits)),
                    AuthorityScenario("persistence-validation", "Authority persistence validates before restoring", 60,
                        Step("step13-authority-persistence", "Save, restore, and reject invalid authority state", AuthorityPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.4.organizational-resources-treasuries-property",
                "Organizational Resources, Treasuries, and Property",
                "13.4",
                "Organization treasury metadata coordinates authoritative Economy, Property, Business, and item identity state under institutional authority.",
                13040,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "OrganizationResourceRuntime", "OrganizationAuthorityRuntime", "EconomyRuntime", "PropertyRuntime", "BusinessRuntime", "ItemInstanceIdentityRuntime", "OrganizationResourcePersistenceParticipant" },
                scenarios: new[]
                {
                    ResourceScenario("runtime-readiness", "Organization resource definitions and runtimes are ready", 10,
                        Step("step13-resources-readiness", "Validate resource runtime dependencies", ResourceRuntimeReadiness)),
                    ResourceScenario("treasury-creation", "Treasury and account identities are stable and queryable", 20,
                        Step("step13-resources-treasury", "Create treasury and organization accounts", ResourceTreasuryCreation)),
                    ResourceScenario("basic-transfer", "Deposits and transfers use Economy and remain idempotent", 30,
                        Step("step13-resources-transfer", "Deposit and transfer authoritative funds", ResourceTransferIdempotence)),
                    ResourceScenario("authority-denial", "Unauthorized resource mutations fail without financial mutation", 40,
                        Step("step13-resources-authority", "Enforce organization financial authority", ResourceAuthorityDenial)),
                    ResourceScenario("restricted-funds", "Restricted and budgeted funds remain distinct", 50,
                        Step("step13-resources-allocations", "Validate fund allocation boundaries", ResourceAllocations)),
                    ResourceScenario("reservation", "Resource reservations are explicit and time-bound", 60,
                        Step("step13-resources-reservation", "Reserve and release authoritative funds", ResourceReservation)),
                    ResourceScenario("joint-approval", "Large transfers require independent joint approval", 70,
                        Step("step13-resources-joint-approval", "Authorize a joint financial action", ResourceJointApproval)),
                    ResourceScenario("inventory-and-custody", "Inventory associations and custody preserve Step 9 identity", 80,
                        Step("step13-resources-custody", "Associate inventory and track item custody", ResourceInventoryCustody)),
                    ResourceScenario("property-associations", "Property links require authoritative ownership", 90,
                        Step("step13-resources-assets", "Associate Step 11 property and business records", ResourcePropertyBusinessAssociations)),
                    ResourceScenario("business-and-revenue", "Business ownership and revenue routing stay delegated", 100,
                        Step("step13-resources-revenue", "Route business revenue through Economy accounts", ResourceRevenueRouting)),
                    ResourceScenario("payroll-funding", "Payroll funding uses an organization Economy account", 110,
                        Step("step13-resources-payroll", "Expose payroll funding without duplicating payroll state", ResourcePayrollFunding)),
                    ResourceScenario("branch-finances", "Branch accounts remain separate in consolidated views", 120,
                        Step("step13-resources-branch", "Query branch and parent finances", ResourceBranchFinances)),
                    ResourceScenario("dissolution-boundary", "Dissolution plans freeze resources without inventing beneficiaries", 130,
                        Step("step13-resources-dissolution", "Execute explicit dissolution resource plan", ResourceDissolutionBoundary)),
                    ResourceScenario("reconciliation", "Reconciliation and redacted projections are deterministic and read-only", 140,
                        Step("step13-resources-reconcile", "Reconcile and project resource state", ResourceReconciliationProjection)),
                    ResourceScenario("persistence-validation", "Resource persistence restores all metadata without replaying money", 150,
                        Step("step13-resources-persistence", "Save, restore, and reject resource graph drift", ResourcePersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.5.organizational-goals-policies-decisions",
                "Organizational Goals, Policies, Proposals, and Internal Decisions",
                "13.5",
                "Definition-backed organization goals, policies, proposals, voting, resolutions, execution plans, persistence, and projections.",
                13050,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "OrganizationDecisionRuntime", "OrganizationAuthorityRuntime", "OrganizationResourceRuntime", "OrganizationDecisionPersistenceParticipant" },
                scenarios: new[]
                {
                    DecisionScenario("runtime-readiness", "Organization decision definitions and runtimes are ready", 10,
                        Step("step13-decisions-readiness", "Validate organization decision definitions", DecisionRuntimeReadiness)),
                    DecisionScenario("goals-and-policies", "Goals and policies create, resolve, conflict, and progress deterministically", 20,
                        Step("step13-decisions-goals-policies", "Create goals and resolve policies", DecisionGoalsPolicies)),
                    DecisionScenario("proposal-vote-resolution", "Proposals, amendments, votes, and resolutions follow procedure", 30,
                        Step("step13-decisions-proposal", "Submit, amend, vote, and close proposal", DecisionProposalVoteResolution)),
                    DecisionScenario("execution-persistence-projection", "Resolution execution, persistence, and projections preserve authoritative ownership", 40,
                        Step("step13-decisions-execution", "Execute resolution and validate persistence", DecisionExecutionPersistenceProjection))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.6.faction-identity-alignment-internal-politics",
                "Faction Identity, Alignment, and Internal Political Dynamics",
                "13.6",
                "Definition-backed political factions with affiliations, roles, platforms, vote recommendations, influence reports, split/merge history, visibility projections, and persistence.",
                13060,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "FactionRuntime", "FactionDefinition", "FactionPersistenceParticipant", "OrganizationDecisionRuntime" },
                scenarios: new[]
                {
                    FactionScenario("runtime-readiness", "Faction definitions and owned runtime are ready", 10,
                        Step("step13-factions-readiness", "Validate faction definitions and runtime ownership", FactionRuntimeReadiness)),
                    FactionScenario("identity-lifecycle-hosts", "Factions create, rename, and transition lifecycle without becoming organizations", 20,
                        Step("step13-factions-identity", "Create hosted and independent faction records", FactionIdentityLifecycleHosts)),
                    FactionScenario("affiliations-and-roles", "Faction affiliations and internal roles remain separate from organization membership", 30,
                        Step("step13-factions-affiliations", "Apply affiliations and assign roles through eligibility rules", FactionAffiliationsAndRoles)),
                    FactionScenario("positions-recommendations-cohesion", "Faction positions and vote recommendations read organization decisions without owning votes", 40,
                        Step("step13-factions-cohesion", "Set platform positions and measure vote cohesion", FactionPositionsRecommendationsCohesion)),
                    FactionScenario("split-merge-disposition-projection-persistence", "Split, merge, disposition, projection, and persistence preserve faction state", 50,
                        Step("step13-factions-persistence", "Validate structural changes, redaction, and save restore", FactionSplitMergeDispositionProjectionPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.7.diplomacy-alliances-rivalries-war-status",
                "Diplomacy, Alliances, Rivalries, and War Status",
                "13.7",
                "Formal organization and eligible faction diplomacy with relations, agreements, clauses, breaches, war status, projections, and persistence.",
                13070,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "DiplomacyRuntime", "DiplomaticRelationDefinition", "DiplomaticAgreementDefinition", "DiplomacyPersistenceParticipant" },
                scenarios: new[]
                {
                    DiplomacyScenario("runtime-readiness", "Diplomacy definitions and runtime ownership are ready", 10,
                        Step("step13-diplomacy-readiness", "Resolve diplomacy definitions", DiplomacyRuntimeReadiness)),
                    DiplomacyScenario("actor-eligibility-relations", "Organizations and eligible factions can form relations while internal factions are rejected", 20,
                        Step("step13-diplomacy-relations", "Create recognition, alliance, rivalry, and reject internal faction treaty actor", DiplomacyActorEligibilityRelations)),
                    DiplomacyScenario("agreements-clauses-breaches", "Agreements, clauses, signatures, ratification, activation, and breach state remain explicit", 30,
                        Step("step13-diplomacy-agreements", "Create agreement lifecycle and breach record", DiplomacyAgreementsClausesBreaches)),
                    DiplomacyScenario("war-status-and-incidents", "War status tracks sides, participation, ceasefire, peace, and incidents without combat simulation", 40,
                        Step("step13-diplomacy-war", "Declare and transition formal war", DiplomacyWarStatusIncidents)),
                    DiplomacyScenario("projection-persistence-validation", "Diplomacy projections and persistence validate before restore", 50,
                        Step("step13-diplomacy-persistence", "Project, save, restore, and reject corrupt diplomacy graph", DiplomacyProjectionPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.8.governments-territories-sovereignty-jurisdictions",
                "Governments, Territories, Sovereignty, and Jurisdictions",
                "13.8",
                "Persistent polity, government, territory, sovereignty, control, administration, seat, transition, and jurisdiction records with deterministic resolution.",
                13080,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "GovernmentRuntime", "PolityDefinition", "GovernmentDefinition", "PoliticalTerritoryDefinition", "JurisdictionDefinition", "GovernmentPersistenceParticipant" },
                scenarios: new[]
                {
                    GovernmentScenario("runtime-readiness", "Government definitions and runtime ownership are ready", 10,
                        Step("step13-government-readiness", "Resolve government definitions", GovernmentRuntimeReadiness)),
                    GovernmentScenario("polity-government-territory", "Polity, government, and territory identities remain distinct", 20,
                        Step("step13-government-identity", "Create polity, government, and territory records", GovernmentIdentityAndTerritory)),
                    GovernmentScenario("claims-control-administration-jurisdiction", "Claims, control, administration, sovereignty, seats, and jurisdiction are explicit", 30,
                        Step("step13-government-jurisdiction", "Resolve territorial authority records", GovernmentClaimsAndJurisdiction)),
                    GovernmentScenario("projection-persistence-validation", "Government projections and persistence validate before restore", 40,
                        Step("step13-government-persistence", "Project, save, restore, and reject corrupt government graph", GovernmentProjectionPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.9.laws-rights-permissions-citizenship",
                "Laws, Rights, Legal Permissions, and Citizenship",
                "13.9",
                "Definition-backed legal instruments, provisions, applicability, entitlements, legal status, citizenship, historical law, transitions, and persistence.",
                13090,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "LegalRuntime", "GovernmentRuntime", "LegalAuthorityDefinition", "LegalInstrumentDefinition", "LegalProvisionDefinition", "LegalStatusDefinition", "LegalPersistenceParticipant" },
                scenarios: new[]
                {
                    LegalScenario("runtime-readiness", "Legal definitions and runtime ownership are ready", 10,
                        Step("step13-legal-readiness", "Resolve legal definitions", LegalRuntimeReadiness)),
                    LegalScenario("central-and-municipal-law", "Central and municipal instruments preserve legal hierarchy", 20,
                        Step("step13-legal-enact", "Enact and evaluate central law", LegalEnactAndEvaluate)),
                    LegalScenario("legal-authority-separation", "Institutional authority and legal permission remain separate", 30,
                        Step("step13-legal-authority", "Reject missing legal authority", LegalAuthoritySeparation)),
                    LegalScenario("publication-and-effective-time", "Publication and effective time control activation", 40,
                        Step("step13-legal-time", "Activate scheduled law deterministically", LegalPublicationAndTime)),
                    LegalScenario("amendment-and-historical-law", "Amendments preserve historical provision versions", 50,
                        Step("step13-legal-amendment", "Evaluate historical and amended law", LegalAmendmentHistory)),
                    LegalScenario("repeal-and-supersession", "Repeal and supersession preserve instrument identity", 60,
                        Step("step13-legal-repeal", "Transition instrument lifecycle", LegalRepeal)),
                    LegalScenario("rights-and-permits", "Rights and permits produce scoped entitlement records", 70,
                        Step("step13-legal-entitlement", "Grant and evaluate scoped entitlement", LegalEntitlements)),
                    LegalScenario("immunity", "Immunity overrides applicable prohibitions without deleting law", 80,
                        Step("step13-legal-immunity", "Evaluate individual immunity", LegalImmunity)),
                    LegalScenario("citizenship", "Citizenship is persistent Person legal status", 90,
                        Step("step13-legal-citizenship", "Grant and transition citizenship", LegalCitizenship)),
                    LegalScenario("government-in-exile", "Government-in-exile law remains explicit", 100,
                        Step("step13-legal-exile", "Enact under government lifecycle boundaries", LegalGovernmentLifecycle)),
                    LegalScenario("territorial-transition", "Territorial legal transitions remain planned records", 110,
                        Step("step13-legal-transition", "Preserve transition data", LegalTransitionPersistence)),
                    LegalScenario("treaty-implementation", "Treaty implementation keeps the source agreement reference", 120,
                        Step("step13-legal-treaty", "Enact treaty implementation law", LegalTreatyImplementation)),
                    LegalScenario("conflict-resolution", "Conflicting provisions resolve deterministically", 130,
                        Step("step13-legal-conflict", "Resolve legal hierarchy conflict", LegalConflictResolution)),
                    LegalScenario("visibility-boundary", "Hidden law remains authoritative without public disclosure", 140,
                        Step("step13-legal-visibility", "Evaluate hidden authoritative law", LegalVisibilityBoundary)),
                    LegalScenario("persistence", "Legal persistence restores and rejects corrupt graphs", 150,
                        Step("step13-legal-persistence", "Save, restore, and reject invalid legal state", LegalPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.10.crimes-reporting-warrants-wanted-status",
                "Crimes, Reporting, Warrants, and Wanted Status",
                "13.10",
                "Potential offense incidents, reports, allegations, suspects, evidentiary links, warrants, wanted notices, lifecycle transitions, projections, and persistence.",
                13100,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "CrimeRuntime", "LegalRuntime", "GovernmentRuntime", "LegalOffenseDefinition", "WarrantDefinition", "WantedStatusDefinition", "CrimePersistenceParticipant" },
                scenarios: new[]
                {
                    CrimeScenario("runtime-readiness", "Crime definitions and runtime ownership are ready", 10,
                        Step("step13-crime-readiness", "Resolve crime definitions", CrimeRuntimeReadiness)),
                    CrimeScenario("incident-report-offense", "Incident reports produce legally evaluated potential offenses", 20,
                        Step("step13-crime-incident-report", "Record incident, report, and potential offense", CrimeIncidentReportOffense)),
                    CrimeScenario("allegation-suspect-evidence", "Allegations, suspects, and evidence remain explicit records", 30,
                        Step("step13-crime-allegation-suspect", "Link allegation, suspect, and evidence", CrimeAllegationSuspectEvidence)),
                    CrimeScenario("warrant-threshold-authority", "Warrants require sufficient evidence and explicit authority", 40,
                        Step("step13-crime-warrant-authority", "Request, review, and issue warrant", CrimeWarrantThresholdAuthority)),
                    CrimeScenario("wanted-status-notice", "Wanted status and notices are scoped lifecycle records", 50,
                        Step("step13-crime-wanted-notice", "Create and publish wanted status", CrimeWantedStatusNotice)),
                    CrimeScenario("projection-boundaries", "Crime projections redact restricted records", 60,
                        Step("step13-crime-projection", "Project restricted incident and wanted status", CrimeProjectionBoundaries)),
                    CrimeScenario("time-and-derived-lifecycle", "Warrant and derived wanted status expire deterministically", 70,
                        Step("step13-crime-time", "Process crime time boundaries", CrimeTimeAndDerivedLifecycle)),
                    CrimeScenario("persistence", "Crime persistence restores and rejects corrupt graphs", 80,
                        Step("step13-crime-persistence", "Save, restore, and reject invalid crime state", CrimePersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.11.arrest-courts-judgments-punishments",
                "Arrest, Detention, Courts, Judgments, and Punishments",
                "13.11",
                "Definition-backed justice process records for courts, arrest, custody, charges, hearings, findings, judgments, sentences, remedies, appeals, clemency, projections, and persistence.",
                13110,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "JusticeRuntime", "CrimeRuntime", "LegalRuntime", "GovernmentRuntime", "JusticePersistenceParticipant" },
                scenarios: new[]
                {
                    JusticeScenario("runtime-readiness", "Justice definitions and runtime ownership are ready", 10,
                        Step("step13-justice-readiness", "Resolve justice definitions and runtime", JusticeRuntimeReadiness)),
                    JusticeScenario("court-selection", "Courts register and resolve deterministically by jurisdiction", 20,
                        Step("step13-justice-court-selection", "Register courts and select primary jurisdiction", JusticeCourtSelection)),
                    JusticeScenario("arrest-custody-release", "Arrest, custody transfer, and release preserve legal basis and history", 30,
                        Step("step13-justice-arrest-custody", "Execute warrant arrest and custody lifecycle", JusticeArrestCustodyRelease)),
                    JusticeScenario("case-charge-plea-hearing", "Cases, charges, pleas, and hearings remain explicit process records", 40,
                        Step("step13-justice-case-charge", "File case, charge, plea, and hearing", JusticeCaseChargePleaHearing)),
                    JusticeScenario("evidence-finding-judgment", "Evidence rulings, findings, and judgments preserve charge-level outcomes", 50,
                        Step("step13-justice-judgment", "Submit evidence, record finding, and enter judgment", JusticeEvidenceFindingJudgment)),
                    JusticeScenario("sentences-remedies-appeals-clemency", "Sentences, remedies, appeals, and clemency operate without rewriting judgment history", 60,
                        Step("step13-justice-sentence-appeal", "Impose sentence, order remedy, appeal, and clemency", JusticeSentencesRemediesAppealsClemency)),
                    JusticeScenario("projection-persistence-validation", "Justice projections redact restricted data and persistence rejects corrupt graphs", 70,
                        Step("step13-justice-persistence", "Project, save, restore, and reject invalid justice graph", JusticeProjectionPersistenceValidation))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.13.12.organizations-governments-law-integration-finalization",
                "Organizations, Factions, Governments, Law, and Justice Integration Finalization",
                "13.12",
                "Step 13 integration readiness, runtime ownership, protected action ordering, bounded projections, persistence dependency graph, and cross-runtime transaction hardening.",
                13120,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[]
                {
                    "Step13InstitutionalIntegrationFacade",
                    "OrganizationRuntime",
                    "OrganizationMembershipRuntime",
                    "OrganizationAuthorityRuntime",
                    "OrganizationResourceRuntime",
                    "OrganizationDecisionRuntime",
                    "FactionRuntime",
                    "DiplomacyRuntime",
                    "GovernmentRuntime",
                    "LegalRuntime",
                    "CrimeRuntime",
                    "JusticeRuntime"
                },
                scenarios: new[]
                {
                    IntegrationScenario("readiness-ownership-graph", "Step 13 readiness and ownership graph are complete", 10,
                        Step("step13-integration-readiness", "Validate readiness, ownership, and persistence dependencies", Step13IntegrationReadinessOwnershipGraph)),
                    IntegrationScenario("identity-action-pipeline", "Protected institutional actions evaluate gates in stable order", 20,
                        Step("step13-integration-action", "Evaluate identity, authority, jurisdiction, law, domain, consent, resource, and timing gates", Step13IntegrationActionPipeline)),
                    IntegrationScenario("projection-persistence-restore", "Bounded institutional projections and runtime snapshots are deterministic", 30,
                        Step("step13-integration-snapshot", "Create immutable access-aware institutional context snapshots", Step13IntegrationProjectionPersistenceRestore)),
                    IntegrationScenario("transaction-atomicity-idempotence", "Cross-runtime transaction coordination is atomic and idempotent", 40,
                        Step("step13-integration-transaction", "Preview, rollback failed commits, and reject duplicate side effects", Step13IntegrationTransactionAtomicity)),
                    IntegrationScenario("boundaries-and-handoffs", "Step 13 exposes clean ownership handoffs to adjacent systems", 50,
                        Step("step13-integration-boundaries", "Validate non-owned domains stay external and derived", Step13IntegrationBoundariesAndHandoffs))
                }), out _);
        }

        private static TestLabAutomationStepResult Step13IntegrationReadinessOwnershipGraph(TestLabAutomationContext context)
        {
            Step13InstitutionalIntegrationFacade facade = CreateIntegrationFacade(context);
            Step13IntegrationValidationReport report = facade.ValidateComplete();
            Step13ReadinessSnapshot readiness = facade.CreateReadinessSnapshot();

            bool uniqueOwnership = facade.OwnershipMap.Select(item => item.DomainId).Distinct(StringComparer.Ordinal).Count() == facade.OwnershipMap.Count;
            bool organizationOwned = facade.OwnershipMap.Any(item => item.DomainId == "organization-identity" && item.AuthoritativeRuntime == nameof(OrganizationRuntime));
            bool warrantOwned = facade.OwnershipMap.Any(item => item.DomainId == "warrant" && item.AuthoritativeRuntime == nameof(CrimeRuntime));
            bool judgmentOwned = facade.OwnershipMap.Any(item => item.DomainId == "judgment" && item.AuthoritativeRuntime == nameof(JusticeRuntime));
            bool justiceDependsOnCrime = facade.PersistenceDependencies.Any(item => item.ParticipantKey == JusticePersistenceParticipant.Key && item.DependsOn.Contains(CrimePersistenceParticipant.Key));
            bool valid = report.Succeeded && readiness.Status == Step13IntegrationHealthStatus.Ready && uniqueOwnership && organizationOwned && warrantOwned && judgmentOwned && justiceDependsOnCrime && readiness.Runtimes.Count == 11;

            string diagnostics = report.Diagnostics.Count == 0 ? "None" : string.Join(" | ", report.Diagnostics.Select(item => item.ToString()));
            return TestLabAssertions.True("step13-integration-readiness", "Validate readiness, ownership, and persistence dependencies", valid, $"Status={readiness.Status} {report.ToSummary()} Runtimes={readiness.Runtimes.Count} Ownership={facade.OwnershipMap.Count} Dependencies={facade.PersistenceDependencies.Count} Diagnostics={diagnostics}");
        }

        private static TestLabAutomationStepResult Step13IntegrationActionPipeline(TestLabAutomationContext context)
        {
            Step13InstitutionalIntegrationFacade facade = CreateIntegrationFacade(context);
            Step13InstitutionalActionContext validContext = new Step13InstitutionalActionContext(
                PrimaryAuthorityActorId(context),
                "organization.prototype.adventurers-guild",
                "government.prototype.village",
                $"office-assignment.testlab.integration.{context.RunId}",
                $"authority-grant.testlab.integration.{context.RunId}",
                new Step13InstitutionalSubjectReference(Step13InstitutionalSubjectType.Warrant, $"warrant.testlab.integration.{context.RunId}", context.ScenarioContext.Runtimes.WorldId, nameof(CrimeRuntime)),
                "institutional-action.prototype.issue-warrant",
                "place.testlab.capital",
                "political-territory.testlab.integration",
                "jurisdiction.testlab.integration",
                "legal-subject.prototype.public-order",
                string.Empty,
                string.Empty,
                $"incident.testlab.integration.{context.RunId}",
                string.Empty,
                100d,
                $"provenance.testlab.integration.{context.RunId}",
                Step13ProjectionVisibility.Official);
            Step13InstitutionalActionContext missingAuthority = new Step13InstitutionalActionContext(
                PrimaryAuthorityActorId(context),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new Step13InstitutionalSubjectReference(Step13InstitutionalSubjectType.Warrant, $"warrant.testlab.integration.{context.RunId}", context.ScenarioContext.Runtimes.WorldId, nameof(CrimeRuntime)),
                "institutional-action.prototype.issue-warrant",
                "place.testlab.capital",
                "political-territory.testlab.integration",
                "jurisdiction.testlab.integration",
                "legal-subject.prototype.public-order",
                string.Empty,
                string.Empty,
                $"incident.testlab.integration.{context.RunId}",
                string.Empty,
                100d);

            Step13ActionEvaluationResult allowed = facade.EvaluateProtectedAction(validContext);
            Step13ActionEvaluationResult denied = facade.EvaluateProtectedAction(missingAuthority);
            bool ordered = allowed.Gates.Select(item => item.Gate).SequenceEqual(allowed.Gates.Select(item => item.Gate).OrderBy(item => item));
            bool authorityBlocked = denied.FailedGates.Any(item => item.Gate == Step13ActionGate.Authority);
            bool valid = allowed.Executable && ordered && !denied.Executable && authorityBlocked && denied.FailedGates.Any(item => item.Gate == Step13ActionGate.Prepared);

            return TestLabAssertions.True("step13-integration-action", "Evaluate identity, authority, jurisdiction, law, domain, consent, resource, and timing gates", valid, $"Allowed={allowed.Executable} Denied={denied.Executable} Ordered={ordered} Failed={string.Join(",", denied.FailedGates.Select(item => item.Gate))} Fingerprints={allowed.Fingerprint}/{denied.Fingerprint}");
        }

        private static TestLabAutomationStepResult Step13IntegrationProjectionPersistenceRestore(TestLabAutomationContext context)
        {
            Step13InstitutionalIntegrationFacade facade = CreateIntegrationFacade(context);
            string organizationId = $"organization.testlab.integration.snapshot.{context.RunId}";
            OrganizationOperationResult organization = context.ScenarioContext.Runtimes.Organizations.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = organizationId,
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                officialName = "Integration Snapshot Guild",
                shortName = "Integration",
                initialLifecycleState = OrganizationLifecycleState.Active,
                visibility = OrganizationVisibility.Public,
                transactionId = $"testlab.integration.snapshot.organization.{context.RunId}"
            });
            Step13InstitutionalContextOptions options = new Step13InstitutionalContextOptions
            {
                MaxOrganizations = 1,
                MaxMemberships = 1,
                MaxAuthority = 1,
                MaxResources = 1,
                MaxDecisions = 1,
                MaxFactions = 1,
                MaxDiplomacy = 1,
                MaxGovernments = 1,
                MaxLaws = 1,
                MaxCrimes = 1,
                MaxJustice = 1
            };

            Step13InstitutionalContextSnapshot first = facade.CreateInstitutionalContextSnapshot(PrimaryAuthorityActorId(context), PrimaryAuthorityActorId(context), organizationId, string.Empty, string.Empty, 100d, options);
            Step13InstitutionalContextSnapshot second = facade.CreateInstitutionalContextSnapshot(PrimaryAuthorityActorId(context), PrimaryAuthorityActorId(context), organizationId, string.Empty, string.Empty, 100d, options);
            Step13ContextRecordReference[] returned = first.Records as Step13ContextRecordReference[];
            if (returned != null && returned.Length > 0)
            {
                returned[0] = new Step13ContextRecordReference("mutated", "mutated", Step13InstitutionalProjectionState.Diagnostic, Step13ProjectionVisibility.Diagnostic);
            }

            bool deterministic = first.Fingerprint == second.Fingerprint;
            bool immutable = !first.Records.Any(item => item.RuntimeName == "mutated");
            bool bounded = first.Records.Count <= 11;
            bool sourced = first.SourceRuntimes.Count == 11;
            bool valid = organization.Succeeded && deterministic && immutable && bounded && sourced && first.Records.Count > 0;

            return TestLabAssertions.True("step13-integration-snapshot", "Create immutable access-aware institutional context snapshots", valid, $"Organization={organization.Status} Deterministic={deterministic} Immutable={immutable} Records={first.Records.Count} Runtimes={first.SourceRuntimes.Count} Truncated={first.Truncated} Diagnostics={string.Join(",", first.Diagnostics)}");
        }

        private static TestLabAutomationStepResult Step13IntegrationTransactionAtomicity(TestLabAutomationContext context)
        {
            Step13InstitutionalTransactionCoordinator coordinator = new Step13InstitutionalTransactionCoordinator();
            bool previewed = false;
            bool committed = false;
            bool rolledBack = false;
            Step13TransactionParticipantPlan[] failingPlans =
            {
                new Step13TransactionParticipantPlan(nameof(CrimeRuntime), Step13TransactionFailurePolicy.Required, () => previewed = true, () => true, () => committed = true, () => rolledBack = true),
                new Step13TransactionParticipantPlan(nameof(JusticeRuntime), Step13TransactionFailurePolicy.Required, () => true, () => true, () => false, () => rolledBack = true)
            };

            Step13TransactionResult preview = coordinator.Execute($"tx.step13.integration.preview.{context.RunId}", failingPlans, preview: true);
            Step13TransactionResult failed = coordinator.Execute($"tx.step13.integration.fail.{context.RunId}", failingPlans);
            Step13TransactionResult success = coordinator.Execute($"tx.step13.integration.success.{context.RunId}", new[]
            {
                new Step13TransactionParticipantPlan(nameof(JusticeRuntime), Step13TransactionFailurePolicy.Required, () => true, () => true, () => true, () => true)
            });
            Step13TransactionResult duplicate = coordinator.Execute($"tx.step13.integration.success.{context.RunId}", failingPlans);

            bool rollbackRecorded = failed.Participants.Any(item => item.Stage == Step13TransactionStage.Rollback && item.RuntimeName == nameof(CrimeRuntime));
            bool valid = preview.Succeeded && preview.Preview && previewed && !failed.Succeeded && committed && rolledBack && rollbackRecorded && success.Succeeded && duplicate.Succeeded && duplicate.Duplicate;

            return TestLabAssertions.True("step13-integration-transaction", "Preview, rollback failed commits, and reject duplicate side effects", valid, $"Preview={preview.Succeeded}/{preview.Preview} Failed={failed.Succeeded} Success={success.Succeeded} Duplicate={duplicate.Duplicate} Rollback={rollbackRecorded}");
        }

        private static TestLabAutomationStepResult Step13IntegrationBoundariesAndHandoffs(TestLabAutomationContext context)
        {
            Step13InstitutionalIntegrationFacade facade = CreateIntegrationFacade(context);
            Step13OwnershipEntry[] ownership = facade.OwnershipMap.ToArray();
            bool economyExternal = ownership.Any(item => item.DomainId == "financial-transaction" && item.AuthoritativeRuntime == nameof(EconomyRuntime) && item.Derived);
            bool propertyExternal = ownership.Any(item => item.DomainId == "property-ownership" && item.AuthoritativeRuntime == nameof(PropertyRuntime) && item.Derived);
            bool reputationExternal = ownership.Any(item => item.DomainId == "social-reputation" && item.AuthoritativeRuntime == "ReputationRuntime" && item.Derived);
            bool knowledgeExternal = ownership.Any(item => item.DomainId == "knowledge-visibility" && item.AuthoritativeRuntime == "InformationAccessRuntime" && item.Derived);
            bool unique = ownership.Select(item => item.DomainId).Distinct(StringComparer.Ordinal).Count() == ownership.Length;
            bool valid = economyExternal && propertyExternal && reputationExternal && knowledgeExternal && unique;

            return TestLabAssertions.True("step13-integration-boundaries", "Validate non-owned domains stay external and derived", valid, $"Economy={economyExternal} Property={propertyExternal} Reputation={reputationExternal} Knowledge={knowledgeExternal} Unique={unique} Domains={ownership.Length}");
        }

        private static TestLabAutomationStepResult ReadinessAndPrototypeDefinitions(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out OrganizationRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-organization-readiness", "Resolve definitions and seeded records", "OrganizationRuntime", "Present", "Missing", failure);
            }

            bool guildDefinition = context.ScenarioContext.Runtimes.DefinitionRegistry.TryGet(PrototypeOrganizationDefinitionFactory.GuildDefinitionId, out OrganizationDefinition guild);
            bool secretDefinition = context.ScenarioContext.Runtimes.DefinitionRegistry.TryGet(PrototypeOrganizationDefinitionFactory.SecretSocietyDefinitionId, out OrganizationDefinition secret);
            bool seededGuild = runtime.TryGetSnapshot(PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds[0], out OrganizationSnapshot guildSnapshot);
            bool seededForge = runtime.TryGetSnapshot("organization.prototype.royal-forge", out _);
            bool valid = guildDefinition
                && secretDefinition
                && guild.Category == OrganizationCategory.Guild
                && secret.SupportsVisibility(OrganizationVisibility.Hidden)
                && seededGuild
                && seededForge
                && guildSnapshot.CurrentName.Length > 0;

            return TestLabAssertions.True("step13-organization-readiness", "Resolve definitions and seeded records", valid, $"Definitions={guildDefinition}/{secretDefinition} Seeded={runtime.Count} Guild={guildSnapshot?.CurrentName}");
        }

        private static TestLabAutomationStepResult LegalRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetLegalRuntime(context, out LegalRuntime runtime, out string failure)) return LegalFail("step13-legal-readiness", failure);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool valid = registry.TryGet(PrototypeLegalDefinitionFactory.SovereignAuthorityId, out LegalAuthorityDefinition authority)
                && registry.TryGet(PrototypeLegalDefinitionFactory.CentralStatuteId, out LegalInstrumentDefinition instrument)
                && registry.TryGet(PrototypeLegalDefinitionFactory.RightProvisionId, out LegalProvisionDefinition provision)
                && registry.TryGet(PrototypeLegalDefinitionFactory.CitizenStatusId, out LegalStatusDefinition status)
                && registry.TryGet(PrototypeLegalDefinitionFactory.CitizenshipId, out CitizenshipDefinition citizenship)
                && authority.Category == LegalAuthorityCategory.SovereignLegislative
                && instrument.Category == LegalInstrumentCategory.Statute
                && provision.EffectCategory == LegalEffectCategory.Right
                && status.Category == LegalStatusCategory.Citizen
                && citizenship.Routes.Count > 0
                && runtime.Revision == 0L;
            return TestLabAssertions.True("step13-legal-readiness", "Resolve legal definitions", valid, $"Ready={valid} Revision={runtime.Revision}");
        }

        private static TestLabAutomationStepResult LegalEnactAndEvaluate(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            LegalOperationResult enacted = EnactLegal(context, runtime, "central", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.RightProvisionId, LegalEffectCategory.Right, "activity.prototype.trade", territoryId, 10d);
            LegalApplicabilityResult evaluated = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.trade", worldTime = 11d });
            return TestLabAssertions.True("step13-legal-enact", "Enact and evaluate central law", enacted.Succeeded && evaluated.Status == LegalApplicabilityStatus.Permitted, $"Enact={enacted.Code} Evaluate={evaluated.Status}");
        }

        private static TestLabAutomationStepResult LegalAuthoritySeparation(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegalInstrumentRequest request = LegalRequest(context, "unauthorized", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.PermissionProvisionId, "activity.prototype.trade", territoryId, 10d);
            request.trustedSystemOperation = false;
            long before = runtime.Revision;
            LegalOperationResult result = runtime.Enact(request);
            return TestLabAssertions.True("step13-legal-authority", "Reject missing legal authority", !result.Succeeded && result.Code == LegalOperationCode.MissingAuthority && runtime.Revision == before, $"Result={result.Code} NoMutation={runtime.Revision == before}");
        }

        private static TestLabAutomationStepResult LegalPublicationAndTime(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            LegalOperationResult enacted = EnactLegal(context, runtime, "scheduled", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.PermissionProvisionId, LegalEffectCategory.Permission, "activity.prototype.travel", territoryId, 20d, enactmentTime: 10d);
            LegalApplicabilityStatus before = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.travel", worldTime = 15d }).Status;
            LegalOperationResult activated = runtime.ProcessWorldTime(new LegalTimeEvaluationRequest { transactionId = LegalTx(context, "time"), boundaryId = $"legal-boundary.{context.RunId}", worldTime = 20d });
            LegalOperationResult duplicate = runtime.ProcessWorldTime(new LegalTimeEvaluationRequest { transactionId = LegalTx(context, "time"), boundaryId = $"legal-boundary.{context.RunId}", worldTime = 20d });
            LegalApplicabilityStatus after = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.travel", worldTime = 20d }).Status;
            return TestLabAssertions.True("step13-legal-time", "Activate scheduled law deterministically", enacted.Succeeded && before == LegalApplicabilityStatus.NoApplicableLaw && activated.Succeeded && duplicate.Duplicate && after == LegalApplicabilityStatus.Permitted, $"Before={before} Activate={activated.Code} Duplicate={duplicate.Code} After={after}");
        }

        private static TestLabAutomationStepResult LegalAmendmentHistory(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegal(context, runtime, "amended", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.PermissionProvisionId, LegalEffectCategory.Permission, "activity.prototype.trade", territoryId, 10d);
            string provisionId = $"legal-provision.testlab.amended.{context.RunId}";
            LegalOperationResult amended = runtime.AmendProvision(new AmendLegalProvisionRequest { transactionId = LegalTx(context, "amend"), amendmentId = $"legal-amendment.testlab.{context.RunId}", provisionId = provisionId, effectiveWorldTime = 20d, trustedSystemOperation = true, version = new LegalProvisionVersionData { effect = LegalEffectCategory.Permission, actionId = "activity.prototype.travel", territoryIds = new[] { territoryId } } });
            LegalApplicabilityStatus historical = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.trade", worldTime = 15d }).Status;
            LegalApplicabilityStatus current = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.travel", worldTime = 25d }).Status;
            return TestLabAssertions.True("step13-legal-amendment", "Evaluate historical and amended law", amended.Succeeded && historical == LegalApplicabilityStatus.Permitted && current == LegalApplicabilityStatus.Permitted, $"Amend={amended.Code} Historical={historical} Current={current}");
        }

        private static TestLabAutomationStepResult LegalRepeal(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegal(context, runtime, "repeal", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.ProhibitionProvisionId, LegalEffectCategory.Prohibition, "activity.prototype.hunt", territoryId, 10d);
            string instrumentId = $"legal-instrument.testlab.repeal.{context.RunId}";
            LegalOperationResult result = runtime.TransitionInstrument(new LegalInstrumentTransitionRequest { transactionId = LegalTx(context, "repeal"), instrumentId = instrumentId, targetState = LegalInstrumentLifecycleState.Repealed, worldTime = 20d, trustedSystemOperation = true });
            LegalApplicabilityStatus status = runtime.Evaluate(new LegalApplicabilityRequest { territoryId = territoryId, actionId = "activity.prototype.hunt", worldTime = 21d }).Status;
            return TestLabAssertions.True("step13-legal-repeal", "Transition instrument lifecycle", result.Succeeded && status == LegalApplicabilityStatus.NoApplicableLaw && runtime.TryGetInstrument(instrumentId, out LegalInstrumentRecordData record) && record.lifecycleState == LegalInstrumentLifecycleState.Repealed, $"Transition={result.Code} Evaluate={status}");
        }

        private static TestLabAutomationStepResult LegalEntitlements(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out _);
            LegalOperationResult result = runtime.GrantEntitlement(new LegalEntitlementRequest { transactionId = LegalTx(context, "permit"), entitlementId = $"legal-permission.testlab.{context.RunId}", effect = LegalEffectCategory.Permission, personId = context.ScenarioContext.Runtimes.PersonId, actionId = "activity.prototype.trade", territoryId = territoryId, effectiveWorldTime = 10d, expirationWorldTime = 30d, trustedSystemOperation = true });
            LegalApplicabilityStatus status = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.trade", worldTime = 11d }).Status;
            return TestLabAssertions.True("step13-legal-entitlement", "Grant and evaluate scoped entitlement", result.Succeeded && status == LegalApplicabilityStatus.Permitted && runtime.Entitlements.Count == 1, $"Grant={result.Code} Evaluate={status}");
        }

        private static TestLabAutomationStepResult LegalImmunity(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegal(context, runtime, "immunity-law", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.ProhibitionProvisionId, LegalEffectCategory.Prohibition, "activity.prototype.entry", territoryId, 10d);
            runtime.GrantEntitlement(new LegalEntitlementRequest { transactionId = LegalTx(context, "immunity"), entitlementId = $"legal-immunity.testlab.{context.RunId}", effect = LegalEffectCategory.Immunity, personId = context.ScenarioContext.Runtimes.PersonId, actionId = "activity.prototype.entry", territoryId = territoryId, effectiveWorldTime = 10d, trustedSystemOperation = true });
            LegalApplicabilityStatus status = runtime.Evaluate(new LegalApplicabilityRequest { personId = context.ScenarioContext.Runtimes.PersonId, territoryId = territoryId, actionId = "activity.prototype.entry", worldTime = 11d }).Status;
            return TestLabAssertions.True("step13-legal-immunity", "Evaluate individual immunity", status == LegalApplicabilityStatus.Immune, $"Evaluate={status}");
        }

        private static TestLabAutomationStepResult LegalCitizenship(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out string polityId, out string governmentId, out _, out _);
            string statusId = $"legal-status.testlab.citizen.{context.RunId}";
            LegalOperationResult grant = runtime.GrantLegalStatus(new LegalStatusGrantRequest { transactionId = LegalTx(context, "citizen"), statusId = statusId, statusDefinitionId = PrototypeLegalDefinitionFactory.CitizenStatusId, citizenshipDefinitionId = PrototypeLegalDefinitionFactory.CitizenshipId, personId = context.ScenarioContext.Runtimes.PersonId, polityId = polityId, recognizingGovernmentId = governmentId, acquisitionRoute = CitizenshipAcquisitionRoute.Grant, consentGiven = true, effectiveWorldTime = 10d, trustedSystemOperation = true });
            LegalOperationResult transition = runtime.TransitionLegalStatus(new LegalStatusTransitionRequest { transactionId = LegalTx(context, "renounce"), statusId = statusId, targetState = LegalStatusLifecycleState.Renounced, personConsent = true, worldTime = 20d, trustedSystemOperation = true });
            return TestLabAssertions.True("step13-legal-citizenship", "Grant and transition citizenship", grant.Succeeded && transition.Succeeded && runtime.TryGetStatus(statusId, out PersonLegalStatusRecordData status) && status.lifecycleState == LegalStatusLifecycleState.Renounced && status.endedWorldTime == 20d, $"Grant={grant.Code} Transition={transition.Code}");
        }

        private static TestLabAutomationStepResult LegalGovernmentLifecycle(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            LegalOperationResult enacted = EnactLegal(context, runtime, "exile", jurisdictionId, PrototypeLegalDefinitionFactory.EmergencyOrderId, PrototypeLegalDefinitionFactory.DutyProvisionId, LegalEffectCategory.Duty, "activity.prototype.report", territoryId, 10d, expirationTime: 30d);
            return TestLabAssertions.True("step13-legal-exile", "Enact under government lifecycle boundaries", enacted.Succeeded && runtime.Instruments.Single().expirationWorldTime == 30d, $"Enact={enacted.Code} Expiry={runtime.Instruments.SingleOrDefault()?.expirationWorldTime}");
        }

        private static TestLabAutomationStepResult LegalTransitionPersistence(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out string polityId, out _, out _, out _);
            string targetPolityId = $"polity.successor.{context.RunId}";
            context.ScenarioContext.Runtimes.Governments.CreatePolity(new PolityCreateRequest { transactionId = LegalTx(context, "successor-polity"), polityId = targetPolityId, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Legal Successor Polity", worldTime = 5d });
            LegalOperationResult result = runtime.PlanTransition(new LegalTransitionPlanRequest { transactionId = LegalTx(context, "transition"), transitionId = $"legal-transition.testlab.{context.RunId}", kind = LegalTransitionKind.TerritorialTransfer, sourcePolityId = polityId, targetPolityId = targetPolityId, plannedWorldTime = 30d, diagnostics = "Transition requires an explicit successor enactment." });
            return TestLabAssertions.True("step13-legal-transition", "Preserve transition data", result.Succeeded && runtime.Transitions.Count == 1 && !runtime.Transitions[0].executed, $"Plan={result.Code} Count={runtime.Transitions.Count}");
        }

        private static TestLabAutomationStepResult LegalTreatyImplementation(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            string agreementId = $"diplomatic-agreement.prototype.{context.RunId}";
            DiplomacyOperationResult agreement = context.ScenarioContext.Runtimes.Diplomacy.CreateAgreement(new DiplomaticAgreementRequest
            {
                transactionId = LegalTx(context, "treaty-agreement"),
                agreementId = agreementId,
                agreementDefinitionId = PrototypeDiplomacyDefinitionFactory.MutualDefenseAgreementId,
                title = "Legal Test Implementation Agreement",
                initialState = DiplomaticAgreementLifecycleState.Draft,
                visibility = DiplomaticVisibility.Restricted,
                worldTime = 4d,
                parties = new[]
                {
                    Party($"{agreementId}.party.guild", Org("organization.prototype.adventurers-guild")),
                    Party($"{agreementId}.party.forge", Org("organization.prototype.royal-forge"))
                },
                clauses = new[]
                {
                    Clause($"{agreementId}.clause", PrototypeDiplomacyDefinitionFactory.DefenseAssistanceClauseId, DiplomaticClauseCategory.DefenseAssistance, DiplomaticVisibility.Restricted)
                }
            });
            EnactLegalInstrumentRequest request = LegalRequest(context, "treaty", jurisdictionId, PrototypeLegalDefinitionFactory.TreatyImplementationId, PrototypeLegalDefinitionFactory.DutyProvisionId, "activity.prototype.treaty-duty", territoryId, 10d);
            request.sourceAgreementId = agreementId;
            LegalOperationResult result = runtime.Enact(request);
            bool stored = runtime.TryGetInstrument(request.instrumentId, out LegalInstrumentRecordData record) && record.sourceAgreementId == request.sourceAgreementId;
            return TestLabAssertions.True("step13-legal-treaty", "Enact treaty implementation law", agreement.Succeeded && result.Succeeded && stored, $"Agreement={agreement.Code} Enact={result.Code} SourceStored={stored}");
        }

        private static TestLabAutomationStepResult LegalConflictResolution(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegal(context, runtime, "conflict-permit", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.PermissionProvisionId, LegalEffectCategory.Permission, "activity.prototype.trade", territoryId, 10d);
            EnactLegal(context, runtime, "conflict-prohibit", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.ProhibitionProvisionId, LegalEffectCategory.Prohibition, "activity.prototype.trade", territoryId, 11d);
            LegalApplicabilityStatus status = runtime.Evaluate(new LegalApplicabilityRequest { territoryId = territoryId, actionId = "activity.prototype.trade", worldTime = 12d }).Status;
            return TestLabAssertions.True("step13-legal-conflict", "Resolve legal hierarchy conflict", status == LegalApplicabilityStatus.Prohibited, $"Evaluate={status}");
        }

        private static TestLabAutomationStepResult LegalVisibilityBoundary(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegalInstrumentRequest request = LegalRequest(context, "hidden", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.ProhibitionProvisionId, "activity.prototype.secret", territoryId, 10d);
            request.visibility = PoliticalVisibility.Hidden;
            request.published = false;
            LegalOperationResult enacted = runtime.Enact(request);
            LegalApplicabilityStatus authoritative = runtime.Evaluate(new LegalApplicabilityRequest { territoryId = territoryId, actionId = "activity.prototype.secret", worldTime = 11d }).Status;
            LegalProjectionResult<LegalInstrumentRecordData> projection = runtime.ProjectInstrument(request.instrumentId, privileged: false);
            return TestLabAssertions.True("step13-legal-visibility", "Evaluate hidden authoritative law", enacted.Succeeded && authoritative == LegalApplicabilityStatus.Prohibited && !projection.Succeeded, $"Enact={enacted.Code} Authoritative={authoritative} Projection={projection.Succeeded}");
        }

        private static TestLabAutomationStepResult LegalPersistenceValidation(TestLabAutomationContext context)
        {
            PrepareLegalFixture(context, out LegalRuntime runtime, out _, out _, out string territoryId, out string jurisdictionId);
            EnactLegal(context, runtime, "persist", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.RightProvisionId, LegalEffectCategory.Right, "activity.prototype.trade", territoryId, 10d);
            LegalRuntimeSaveData save = runtime.CreateSaveData();
            LegalRuntime restored = new LegalRuntime();
            TestLabRuntimeBundle bundle = context.ScenarioContext.Runtimes;
            LegalOperationResult restore = restored.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Governments, bundle.Organizations, bundle.OrganizationAuthority, bundle.OrganizationDecisions, bundle.Diplomacy, bundle.Properties, bundle.WorldId, bundle.KnownPersonIds, Array.Empty<string>());
            LegalRuntimeSaveData corrupt = save.Clone();
            corrupt.provisions[0].instrumentId = "legal-instrument.missing";
            long before = restored.Revision;
            LegalOperationResult rejected = restored.RestoreFromSaveData(corrupt, bundle.DefinitionRegistry, bundle.Governments, bundle.Organizations, bundle.OrganizationAuthority, bundle.OrganizationDecisions, bundle.Diplomacy, bundle.Properties, bundle.WorldId, bundle.KnownPersonIds, Array.Empty<string>());
            bool valid = restore.Succeeded && !rejected.Succeeded && restored.Revision == before && restored.Instruments.Count == 1;
            restored.Dispose();
            return TestLabAssertions.True("step13-legal-persistence", "Save, restore, and reject invalid legal state", valid, $"Restore={restore.Code} Reject={rejected.Code} NoMutation={restored.Revision == 0 || before > 0}");
        }

        private static TestLabAutomationStepResult CrimeRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetCrimeRuntime(context, out CrimeRuntime runtime, out string failure)) return CrimeFail("step13-crime-readiness", failure);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool offenseFound = registry.TryGet(PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId, out LegalOffenseDefinition offense);
            bool warrantFound = registry.TryGet(PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId, out WarrantDefinition warrant);
            bool wantedFound = registry.TryGet(PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId, out WantedStatusDefinition wanted);
            bool valid = offenseFound
                && warrantFound
                && wantedFound
                && offense.Category == OffenseCategory.ViolenceAgainstPerson
                && offense.LegalActionId == "crime.attack"
                && warrant.Category == WarrantCategory.Arrest
                && wanted.Purpose == WantedPurposeCategory.Arrest
                && runtime.Revision == 0L;
            return TestLabAssertions.True("step13-crime-readiness", "Resolve crime definitions", valid, $"Definitions={offenseFound}/{warrantFound}/{wantedFound} Revision={runtime.Revision}");
        }

        private static TestLabAutomationStepResult CrimeIncidentReportOffense(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "incident");
            CrimeOperationResult incident = fixture.Crimes.RecordIncident(CrimeIncident(context, fixture, "incident"));
            CrimeOperationResult report = fixture.Crimes.SubmitReport(CrimeReport(context, fixture, "incident"));
            CrimeOperationResult preview = fixture.Crimes.EvaluatePotentialOffense(CrimeOffense(context, fixture, "incident-preview", preview: true));
            CrimeOperationResult offense = fixture.Crimes.EvaluatePotentialOffense(CrimeOffense(context, fixture, "incident"));
            CrimeOperationResult duplicate = fixture.Crimes.EvaluatePotentialOffense(CrimeOffense(context, fixture, "incident"));
            fixture.Crimes.TryGetPotentialOffense(fixture.OffenseId, out PotentialOffenseRecordData record);
            bool valid = incident.Succeeded
                && report.Succeeded
                && preview.Preview
                && offense.Succeeded
                && duplicate.Duplicate
                && record != null
                && record.legalApplicabilityStatus == LegalApplicabilityStatus.Prohibited
                && record.status == PotentialOffenseStatus.ElementsSupported
                && !string.IsNullOrWhiteSpace(record.legalProvisionId);
            return TestLabAssertions.True("step13-crime-incident-report", "Record incident, report, and potential offense", valid, $"Incident={incident.Code} Report={report.Code} Preview={preview.Code} Offense={offense.Code} Duplicate={duplicate.Code} Legal={record?.legalApplicabilityStatus} Status={record?.status}");
        }

        private static TestLabAutomationStepResult CrimeAllegationSuspectEvidence(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "records");
            CreateCrimeCoreRecords(context, fixture, "records");
            CrimeOperationResult allegation = fixture.Crimes.RecordAllegation(new CrimeAllegationRequest { transactionId = CrimeTx(context, "allegation"), allegationId = fixture.AllegationId, incidentId = fixture.IncidentId, reportId = fixture.ReportId, potentialOffenseId = fixture.OffenseId, claimedActorId = fixture.ActorId, claimedVictimId = fixture.VictimId, conductSummary = "Reported physical attack.", sufficiency = EvidenceSufficiencyState.Substantial });
            CrimeOperationResult suspect = fixture.Crimes.AddSuspect(new CrimeSuspectRequest { transactionId = CrimeTx(context, "suspect"), suspectId = fixture.SuspectId, incidentId = fixture.IncidentId, potentialOffenseId = fixture.OffenseId, subjectId = fixture.ActorId, participation = ParticipationCategory.PrincipalActor, basis = "victim and witness report", worldTime = 13d });
            CrimeOperationResult evidence = fixture.Crimes.LinkEvidence(new CrimeEvidenceLinkRequest { transactionId = CrimeTx(context, "evidence"), evidenceLinkId = fixture.EvidenceLinkId, incidentId = fixture.IncidentId, reportId = fixture.ReportId, potentialOffenseId = fixture.OffenseId, evidenceId = $"evidence.testlab.crime.{context.RunId}", relevance = EvidenceRelevance.Supports, sufficiency = EvidenceSufficiencyState.Substantial, sourceId = fixture.ReportId, worldTime = 14d });
            CrimeOperationResult cleared = fixture.Crimes.TransitionSuspect(new CrimeSuspectTransitionRequest { transactionId = CrimeTx(context, "clear-suspect"), suspectId = fixture.SuspectId, targetState = SuspectLifecycleState.Misidentified, misidentified = true, reason = "Later evidence contradicted identification.", worldTime = 15d });
            fixture.Crimes.TryGetSuspect(fixture.SuspectId, out CrimeSuspectRecordData suspectRecord);
            bool valid = allegation.Succeeded
                && suspect.Succeeded
                && evidence.Succeeded
                && cleared.Succeeded
                && suspectRecord != null
                && suspectRecord.lifecycleState == SuspectLifecycleState.Misidentified
                && suspectRecord.misidentified
                && fixture.Crimes.Allegations.Count == 1
                && fixture.Crimes.EvidenceLinks.Count == 1;
            return TestLabAssertions.True("step13-crime-allegation-suspect", "Link allegation, suspect, and evidence", valid, $"Allegation={allegation.Code} Suspect={suspect.Code} Evidence={evidence.Code} Cleared={cleared.Code} State={suspectRecord?.lifecycleState}");
        }

        private static TestLabAutomationStepResult CrimeWarrantThresholdAuthority(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "warrant");
            CreateCrimeCoreRecords(context, fixture, "warrant");
            WarrantRequestCreateRequest low = CrimeWarrantRequest(context, fixture, "low", EvidenceSufficiencyState.Partial);
            CrimeOperationResult thresholdDenied = fixture.Crimes.RequestWarrant(low);
            CrimeOperationResult requested = fixture.Crimes.RequestWarrant(CrimeWarrantRequest(context, fixture, "arrest", EvidenceSufficiencyState.Substantial));
            CrimeOperationResult authorityDenied = fixture.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = CrimeTx(context, "review-denied"), warrantRequestId = fixture.WarrantRequestId, reviewId = "authority-grant.missing", approve = true });
            CrimeOperationResult approved = fixture.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = CrimeTx(context, "review-approved"), warrantRequestId = fixture.WarrantRequestId, reviewId = "trusted.system", approve = true, trustedSystemOperation = true });
            CrimeOperationResult issued = fixture.Crimes.IssueWarrant(new WarrantIssueRequest { transactionId = CrimeTx(context, "issue"), warrantId = fixture.WarrantId, warrantRequestId = fixture.WarrantRequestId, issuedByPersonId = fixture.ActorId, issuedWorldTime = 16d, activationWorldTime = 16d, expirationWorldTime = 30d, trustedSystemOperation = true });
            bool derivedWanted = fixture.Crimes.WantedStatuses.Any(item => item.warrantId == fixture.WarrantId && item.subjectId == fixture.ActorId && item.derivedFromWarrant);
            bool valid = thresholdDenied.Code == CrimeOperationCode.ThresholdNotMet
                && requested.Succeeded
                && authorityDenied.Code == CrimeOperationCode.MissingAuthority
                && approved.Succeeded
                && issued.Succeeded
                && fixture.Crimes.Warrants.Count == 1
                && derivedWanted;
            return TestLabAssertions.True("step13-crime-warrant-authority", "Request, review, and issue warrant", valid, $"Threshold={thresholdDenied.Code} Request={requested.Code} Authority={authorityDenied.Code} Approve={approved.Code} Issue={issued.Code} Wanted={derivedWanted}");
        }

        private static TestLabAutomationStepResult CrimeWantedStatusNotice(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "wanted");
            CreateCrimeCoreRecords(context, fixture, "wanted");
            CrimeOperationResult wanted = fixture.Crimes.CreateWantedStatus(new WantedStatusRequest { transactionId = CrimeTx(context, "wanted"), wantedStatusId = fixture.WantedId, wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForQuestioningDefinitionId, incidentIds = new[] { fixture.IncidentId }, subjectId = fixture.ActorId, jurisdictionId = fixture.JurisdictionId, territoryId = fixture.TerritoryId, risk = WantedRiskAssessment.Nonviolent, activeWorldTime = 18d, expirationWorldTime = 24d, visibility = PoliticalVisibility.Restricted });
            CrimeOperationResult notice = fixture.Crimes.PublishWantedNotice(new WantedNoticeRequest { transactionId = CrimeTx(context, "notice"), noticeId = fixture.NoticeId, wantedStatusId = fixture.WantedId, issuingGovernmentId = fixture.GovernmentId, text = "Wanted for questioning in a reported assault.", publishedWorldTime = 19d, visibility = PoliticalVisibility.Public });
            CrimeOperationResult corrected = fixture.Crimes.TransitionWantedStatus(new WantedStatusTransitionRequest { transactionId = CrimeTx(context, "wanted-clear"), wantedStatusId = fixture.WantedId, targetState = WantedStatusLifecycleState.Cleared, correctionReason = "Questioning completed.", worldTime = 20d });
            fixture.Crimes.TryGetWantedStatus(fixture.WantedId, out WantedStatusRecordData status);
            bool valid = wanted.Succeeded && notice.Succeeded && corrected.Succeeded && status != null && status.lifecycleState == WantedStatusLifecycleState.Cleared && fixture.Crimes.WantedNotices.Count == 1;
            return TestLabAssertions.True("step13-crime-wanted-notice", "Create and publish wanted status", valid, $"Wanted={wanted.Code} Notice={notice.Code} Clear={corrected.Code} State={status?.lifecycleState}");
        }

        private static TestLabAutomationStepResult CrimeProjectionBoundaries(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "projection");
            CreateCrimeCoreRecords(context, fixture, "projection");
            fixture.Crimes.CreateWantedStatus(new WantedStatusRequest { transactionId = CrimeTx(context, "projection-wanted"), wantedStatusId = fixture.WantedId, wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForLocationDefinitionId, incidentIds = new[] { fixture.IncidentId }, subjectId = fixture.ActorId, jurisdictionId = fixture.JurisdictionId, territoryId = fixture.TerritoryId, activeWorldTime = 18d, visibility = PoliticalVisibility.Restricted });
            CrimeProjectionResult<CrimeIncidentRecordData> publicIncident = fixture.Crimes.ProjectIncident(fixture.IncidentId, privileged: false);
            CrimeProjectionResult<CrimeIncidentRecordData> privilegedIncident = fixture.Crimes.ProjectIncident(fixture.IncidentId, privileged: true);
            CrimeProjectionResult<WantedStatusRecordData> publicWanted = fixture.Crimes.ProjectWantedStatus(fixture.WantedId, privileged: false);
            bool valid = publicIncident.Succeeded
                && publicIncident.Redacted
                && publicIncident.Record.victimIds.Length == 0
                && privilegedIncident.Succeeded
                && !privilegedIncident.Redacted
                && privilegedIncident.Record.victimIds.Length == 1
                && publicWanted.Succeeded
                && publicWanted.Redacted
                && string.IsNullOrEmpty(publicWanted.Record.subjectId);
            return TestLabAssertions.True("step13-crime-projection", "Project restricted incident and wanted status", valid, $"Incident={publicIncident.Succeeded}/{publicIncident.Redacted} Privileged={privilegedIncident.Succeeded}/{privilegedIncident.Redacted} Wanted={publicWanted.Succeeded}/{publicWanted.Redacted}");
        }

        private static TestLabAutomationStepResult CrimeTimeAndDerivedLifecycle(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "time");
            CreateCrimeCoreRecords(context, fixture, "time");
            CrimeOperationResult requested = fixture.Crimes.RequestWarrant(CrimeWarrantRequest(context, fixture, "time", EvidenceSufficiencyState.Substantial));
            CrimeOperationResult approved = fixture.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = CrimeTx(context, "time-review"), warrantRequestId = fixture.WarrantRequestId, reviewId = "trusted.system", approve = true, trustedSystemOperation = true });
            CrimeOperationResult issued = fixture.Crimes.IssueWarrant(new WarrantIssueRequest { transactionId = CrimeTx(context, "time-issue"), warrantId = fixture.WarrantId, warrantRequestId = fixture.WarrantRequestId, issuedByPersonId = fixture.ActorId, issuedWorldTime = 10d, activationWorldTime = 10d, expirationWorldTime = 20d, trustedSystemOperation = true });
            CrimeOperationResult processed = fixture.Crimes.ProcessWorldTime(new CrimeTimeEvaluationRequest { transactionId = CrimeTx(context, "time-boundary"), boundaryId = $"crime-boundary.testlab.{context.RunId}", worldTime = 21d });
            long revision = fixture.Crimes.Revision;
            CrimeOperationResult duplicate = fixture.Crimes.ProcessWorldTime(new CrimeTimeEvaluationRequest { transactionId = CrimeTx(context, "time-boundary"), boundaryId = $"crime-boundary.testlab.{context.RunId}", worldTime = 21d });
            fixture.Crimes.TryGetWarrant(fixture.WarrantId, out WarrantRecordData warrant);
            WantedStatusRecordData derived = fixture.Crimes.WantedStatuses.SingleOrDefault(item => item.warrantId == fixture.WarrantId);
            bool valid = requested.Succeeded && approved.Succeeded && issued.Succeeded && processed.Succeeded && duplicate.Duplicate && fixture.Crimes.Revision == revision && warrant?.lifecycleState == WarrantLifecycleState.Expired && derived?.lifecycleState == WantedStatusLifecycleState.Expired;
            return TestLabAssertions.True("step13-crime-time", "Process crime time boundaries", valid, $"Issue={issued.Code} Process={processed.Code} Duplicate={duplicate.Code} Warrant={warrant?.lifecycleState} Wanted={derived?.lifecycleState}");
        }

        private static TestLabAutomationStepResult CrimePersistenceValidation(TestLabAutomationContext context)
        {
            CrimeFixture fixture = PrepareCrimeFixture(context, "persist");
            CreateCrimeCoreRecords(context, fixture, "persist");
            fixture.Crimes.OpenInvestigation(new InvestigationRecordRequest { transactionId = CrimeTx(context, "investigation"), investigationId = fixture.InvestigationId, incidentId = fixture.IncidentId, responsibleGovernmentId = fixture.GovernmentId, responsibleOrganizationId = "organization.prototype.adventurers-guild", reviewerPersonIds = new[] { fixture.ActorId }, openedWorldTime = 15d });
            CrimeRuntimeSaveData save = fixture.Crimes.CreateSaveData();
            CrimeRuntime restored = new CrimeRuntime();
            TestLabRuntimeBundle bundle = context.ScenarioContext.Runtimes;
            CrimeOperationResult restore = restored.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Governments, bundle.Laws, bundle.OrganizationAuthority, bundle.Diplomacy, bundle.WorldId, bundle.KnownPersonIds, Array.Empty<string>());
            CrimeRuntimeSaveData corrupt = save.Clone();
            corrupt.reports[0].incidentId = "crime-incident.missing";
            long before = restored.Revision;
            CrimeOperationResult rejected = restored.RestoreFromSaveData(corrupt, bundle.DefinitionRegistry, bundle.Governments, bundle.Laws, bundle.OrganizationAuthority, bundle.Diplomacy, bundle.WorldId, bundle.KnownPersonIds, Array.Empty<string>());
            int incidentCount = restored.Incidents.Count;
            int reportCount = restored.Reports.Count;
            int offenseCount = restored.PotentialOffenses.Count;
            bool valid = restore.Succeeded && rejected.Code == CrimeOperationCode.ValidationFailed && restored.Revision == before && incidentCount == 1 && reportCount == 1 && offenseCount == 1;
            restored.Dispose();
            return TestLabAssertions.True("step13-crime-persistence", "Save, restore, and reject invalid crime state", valid, $"Restore={restore.Code} Reject={rejected.Code} Counts={incidentCount}/{reportCount}/{offenseCount}");
        }

        private static TestLabAutomationStepResult JusticeRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetJusticeRuntime(context, out JusticeRuntime runtime, out string failure)) return JusticeFail("step13-justice-readiness", failure);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool valid = registry.TryGet(PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId, out JusticeInstitutionDefinition institution)
                && registry.TryGet(PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId, out CourtDefinition court)
                && registry.TryGet(PrototypeJusticeDefinitionFactory.WarrantArrestDefinitionId, out ArrestDefinition arrest)
                && registry.TryGet(PrototypeJusticeDefinitionFactory.CriminalChargeDefinitionId, out ChargeDefinition charge)
                && registry.TryGet(PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId, out HearingDefinition hearing)
                && registry.TryGet(PrototypeJusticeDefinitionFactory.ImprisonmentSentenceDefinitionId, out SentenceDefinition sentence)
                && registry.TryGet(PrototypeJusticeDefinitionFactory.JudgmentAppealDefinitionId, out AppealDefinition appeal)
                && institution.Category == JusticeInstitutionCategory.GeneralCourt
                && court.SupportedCases.Contains(JusticeCaseCategory.Criminal)
                && arrest.ValidLegalBases.Contains(ArrestLegalBasisKind.ActiveArrestWarrant)
                && charge.Category == ChargeCategory.CriminalCharge
                && hearing.PermitsFindings
                && sentence.CreatesCustody
                && appeal.MayStayJudgment
                && runtime.Revision == 0L;
            return TestLabAssertions.True("step13-justice-readiness", "Resolve justice definitions and runtime", valid, $"Definitions={valid} Revision={runtime.Revision}");
        }

        private static TestLabAutomationStepResult JusticeCourtSelection(TestLabAutomationContext context)
        {
            JusticeFixture fixture = PrepareJusticeFixture(context, "court", issueWarrant: false, registerCourt: false);
            JusticeOperationResult preview = fixture.Justice.RegisterCourt(JusticeCourtRequest(context, fixture, "preview", preview: true));
            JusticeOperationResult registered = fixture.Justice.RegisterCourt(JusticeCourtRequest(context, fixture, "primary"));
            JusticeOperationResult duplicate = fixture.Justice.RegisterCourt(JusticeCourtRequest(context, fixture, "primary"));
            CourtSelectionResult selection = fixture.Justice.SelectCourt(JusticeCaseCategory.Criminal, new[] { fixture.Crime.JurisdictionId }, fixture.CourtId, appellate: false, evaluationWorldTime: 19d);
            bool valid = preview.Preview
                && registered.Succeeded
                && duplicate.Duplicate
                && selection.Resolved
                && selection.PrimaryCourtId == fixture.CourtId
                && selection.CandidateCourtIds.Contains(fixture.CourtId);
            return TestLabAssertions.True("step13-justice-court-selection", "Register courts and select primary jurisdiction", valid, $"Preview={preview.Code} Register={registered.Code} Duplicate={duplicate.Code} Selection={selection.PrimaryCourtId} Candidates={selection.CandidateCourtIds.Count}");
        }

        private static TestLabAutomationStepResult JusticeArrestCustodyRelease(TestLabAutomationContext context)
        {
            JusticeFixture fixture = PrepareJusticeFixture(context, "arrest");
            JusticeOperationResult arrest = fixture.Justice.Arrest(JusticeArrestRequest(context, fixture, "arrest"));
            JusticeOperationResult duplicate = fixture.Justice.Arrest(JusticeArrestRequest(context, fixture, "arrest"));
            JusticeOperationResult transfer = fixture.Justice.TransferCustody(new CustodyTransferRequest { transactionId = JusticeTx(context, "transfer-arrest"), custodyId = fixture.CustodyId, targetHolderGovernmentId = fixture.Crime.GovernmentId, targetHolderOrganizationId = "organization.prototype.adventurers-guild", targetFacilityPlaceId = "place.testlab.detention", worldTime = 18d });
            JusticeOperationResult release = fixture.Justice.OrderRelease(new ReleaseOrderRequest { transactionId = JusticeTx(context, "release-arrest"), releaseOrderId = fixture.ReleaseOrderId, custodyId = fixture.CustodyId, category = ReleaseCategory.PendingTrial, orderedByCourtId = fixture.CourtId, orderedWorldTime = 19d, effectiveWorldTime = 19d, conditions = new[] { "appear-at-next-hearing" } });
            fixture.Justice.TryGetCustody(fixture.CustodyId, out CustodyRecordData custody);
            bool valid = arrest.Succeeded
                && duplicate.Duplicate
                && transfer.Succeeded
                && release.Succeeded
                && custody != null
                && custody.lifecycleState == CustodyLifecycleState.Released
                && custody.releaseOrderId == fixture.ReleaseOrderId
                && fixture.Justice.Arrests.Count == 1
                && fixture.Justice.ReleaseOrders.Count == 1;
            return TestLabAssertions.True("step13-justice-arrest-custody", "Execute warrant arrest and custody lifecycle", valid, $"Arrest={arrest.Code} Duplicate={duplicate.Code} Transfer={transfer.Code} Release={release.Code} Custody={custody?.lifecycleState}");
        }

        private static TestLabAutomationStepResult JusticeCaseChargePleaHearing(TestLabAutomationContext context)
        {
            JusticeFixture fixture = PrepareJusticeFixture(context, "case");
            JusticeOperationResult caseFile = FileJusticeCase(context, fixture);
            JusticeOperationResult charge = FileJusticeCharge(context, fixture);
            JusticeOperationResult plea = fixture.Justice.EnterPlea(new PleaRequest { transactionId = JusticeTx(context, "plea-case"), pleaId = fixture.PleaId, caseId = fixture.CaseId, chargeId = fixture.ChargeId, defendantPersonId = fixture.Crime.ActorId, category = PleaCategory.NotGuilty, statement = "Not guilty.", enteredWorldTime = 23d });
            JusticeOperationResult hearing = fixture.Justice.ScheduleHearing(new HearingScheduleRequest { transactionId = JusticeTx(context, "hearing-case"), hearingId = fixture.HearingId, hearingDefinitionId = PrototypeJusticeDefinitionFactory.InitialHearingDefinitionId, caseId = fixture.CaseId, category = HearingCategory.InitialAppearance, issueIds = new[] { fixture.ChargeId }, scheduledWorldTime = 24d });
            JusticeOperationResult opened = fixture.Justice.TransitionHearing(new HearingTransitionRequest { transactionId = JusticeTx(context, "hearing-open-case"), hearingId = fixture.HearingId, targetState = HearingLifecycleState.Opened, worldTime = 24d });
            fixture.Justice.TryGetCase(fixture.CaseId, out CourtCaseRecordData courtCase);
            bool valid = caseFile.Succeeded
                && charge.Succeeded
                && plea.Succeeded
                && hearing.Succeeded
                && opened.Succeeded
                && courtCase != null
                && courtCase.chargeIds.Contains(fixture.ChargeId)
                && courtCase.hearingIds.Contains(fixture.HearingId)
                && fixture.Justice.Judgments.Count == 0;
            return TestLabAssertions.True("step13-justice-case-charge", "File case, charge, plea, and hearing", valid, $"Case={caseFile.Code} Charge={charge.Code} Plea={plea.Code} Hearing={hearing.Code} Opened={opened.Code} Judgments={fixture.Justice.Judgments.Count}");
        }

        private static TestLabAutomationStepResult JusticeEvidenceFindingJudgment(TestLabAutomationContext context)
        {
            JusticeFixture fixture = PrepareJusticeFixture(context, "judgment");
            FileJusticeCase(context, fixture);
            FileJusticeCharge(context, fixture);
            JusticeOperationResult hearing = fixture.Justice.ScheduleHearing(new HearingScheduleRequest { transactionId = JusticeTx(context, "trial-judgment"), hearingId = fixture.HearingId, hearingDefinitionId = PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId, caseId = fixture.CaseId, category = HearingCategory.Trial, issueIds = new[] { fixture.ChargeId }, scheduledWorldTime = 25d });
            JusticeOperationResult evidence = fixture.Justice.SubmitEvidence(new EvidenceSubmissionRequest { transactionId = JusticeTx(context, "evidence-judgment"), evidenceSubmissionId = fixture.EvidenceSubmissionId, caseId = fixture.CaseId, hearingId = fixture.HearingId, evidenceId = fixture.Crime.EvidenceLinkId, submittedByPartyId = fixture.ProsecutorPartyId, submittedWorldTime = 25.1d });
            JusticeOperationResult ruling = fixture.Justice.RuleOnEvidence(new EvidenceRulingRequest { transactionId = JusticeTx(context, "ruling-judgment"), evidenceSubmissionId = fixture.EvidenceSubmissionId, targetState = EvidenceRulingState.Admitted, reason = "Relevant to charge." });
            JusticeOperationResult finding = fixture.Justice.RecordFinding(new FindingRequest { transactionId = JusticeTx(context, "finding-judgment"), findingId = fixture.FindingId, caseId = fixture.CaseId, chargeId = fixture.ChargeId, category = FindingCategory.Fact, text = "Elements proven by admitted evidence.", proven = true, enteredWorldTime = 26d });
            JusticeOperationResult judgment = fixture.Justice.EnterJudgment(new JudgmentRequest { transactionId = JusticeTx(context, "judgment"), judgmentId = fixture.JudgmentId, caseId = fixture.CaseId, chargeOutcomes = new[] { new JusticeChargeOutcomeData { chargeId = fixture.ChargeId, findingId = fixture.FindingId, outcome = JudgmentOutcome.Guilty, reason = "Substantial evidence supports every element." } }, enteredWorldTime = 27d });
            fixture.Justice.TryGetCharge(fixture.ChargeId, out ChargeRecordData charge);
            bool valid = hearing.Succeeded
                && evidence.Succeeded
                && ruling.Succeeded
                && finding.Succeeded
                && judgment.Succeeded
                && charge != null
                && charge.lifecycleState == ChargeLifecycleState.Adjudicated
                && fixture.Justice.Findings.Count == 1
                && fixture.Justice.Judgments.Count == 1;
            return TestLabAssertions.True("step13-justice-judgment", "Submit evidence, record finding, and enter judgment", valid, $"Hearing={hearing.Code} Evidence={evidence.Code} Ruling={ruling.Code} Finding={finding.Code} Judgment={judgment.Code} Charge={charge?.lifecycleState}");
        }

        private static TestLabAutomationStepResult JusticeSentencesRemediesAppealsClemency(TestLabAutomationContext context)
        {
            JusticeFixture fixture = PrepareJusticeFixture(context, "sentence", registerAppealCourt: true);
            CreateJudgedCase(context, fixture);
            JusticeOperationResult sentence = fixture.Justice.ImposeSentence(new SentenceRequest { transactionId = JusticeTx(context, "sentence"), sentenceId = fixture.SentenceId, sentenceDefinitionId = PrototypeJusticeDefinitionFactory.FineSentenceDefinitionId, judgmentId = fixture.JudgmentId, caseId = fixture.CaseId, defendantPersonId = fixture.Crime.ActorId, imposedWorldTime = 28d, components = new[] { new SentenceComponentData { componentId = fixture.SentenceComponentId, category = SentenceCategory.Fine, state = SentenceComponentState.Pending, amount = 25, currencyId = "currency.prototype.coin", destinationRuntime = "economy" } } });
            JusticeOperationResult execute = fixture.Justice.ExecuteSentenceComponent(new SentenceExecutionRequest { transactionId = JusticeTx(context, "sentence-execute"), sentenceId = fixture.SentenceId, componentId = fixture.SentenceComponentId, worldTime = 29d });
            JusticeOperationResult remedy = fixture.Justice.OrderRemedy(new RemedyRequest { transactionId = JusticeTx(context, "remedy"), remedyId = fixture.RemedyId, remedyDefinitionId = PrototypeJusticeDefinitionFactory.PropertyReturnRemedyDefinitionId, caseId = fixture.CaseId, judgmentId = fixture.JudgmentId, category = RemedyCategory.PropertyReturn, targetId = "property.prototype.confiscated", destinationRuntime = "property", orderedWorldTime = 30d });
            JusticeOperationResult appeal = fixture.Justice.FileAppeal(new AppealRequest { transactionId = JusticeTx(context, "appeal"), appealId = fixture.AppealId, appealDefinitionId = PrototypeJusticeDefinitionFactory.JudgmentAppealDefinitionId, sourceJudgmentId = fixture.JudgmentId, appellateCourtId = fixture.AppellateCourtId, staysJudgment = false, staysSentence = true, filedWorldTime = 31d });
            JusticeOperationResult decision = fixture.Justice.DecideAppeal(new AppealDecisionRequest { transactionId = JusticeTx(context, "appeal-decision"), appealId = fixture.AppealId, outcome = AppealOutcome.Affirmed, decidedWorldTime = 32d });
            JusticeOperationResult clemency = fixture.Justice.GrantClemency(new ClemencyRequest { transactionId = JusticeTx(context, "clemency"), clemencyId = fixture.ClemencyId, clemencyDefinitionId = PrototypeJusticeDefinitionFactory.CommutationClemencyDefinitionId, judgmentId = fixture.JudgmentId, sentenceId = fixture.SentenceId, grantorGovernmentId = fixture.Crime.GovernmentId, effectSummary = "Fine satisfied by public service.", grantedWorldTime = 33d, trustedSystemOperation = true });
            fixture.Justice.TryGetJudgment(fixture.JudgmentId, out JudgmentRecordData judgment);
            fixture.Justice.TryGetSentence(fixture.SentenceId, out SentenceRecordData sentenceRecord);
            bool valid = sentence.Succeeded
                && execute.Succeeded
                && remedy.Succeeded
                && appeal.Succeeded
                && decision.Succeeded
                && clemency.Succeeded
                && judgment != null
                && judgment.lifecycleState == JudgmentLifecycleState.Final
                && sentenceRecord != null
                && sentenceRecord.lifecycleState == SentenceLifecycleState.Commuted
                && fixture.Justice.Remedies.Count == 1
                && fixture.Justice.Appeals.Count == 1
                && fixture.Justice.Clemencies.Count == 1;
            return TestLabAssertions.True("step13-justice-sentence-appeal", "Impose sentence, order remedy, appeal, and clemency", valid, $"Sentence={sentence.Code} Execute={execute.Code} Remedy={remedy.Code} Appeal={appeal.Code}/{decision.Code} Clemency={clemency.Code} States={judgment?.lifecycleState}/{sentenceRecord?.lifecycleState}");
        }

        private static TestLabAutomationStepResult JusticeProjectionPersistenceValidation(TestLabAutomationContext context)
        {
            JusticeFixture fixture = PrepareJusticeFixture(context, "persist", registerAppealCourt: true);
            CreateJudgedCase(context, fixture);
            JusticeOperationResult arrest = fixture.Justice.Arrest(JusticeArrestRequest(context, fixture, "persist"));
            JusticeProjectionResult<CourtCaseRecordData> publicCase = fixture.Justice.ProjectCase(fixture.CaseId, privileged: false);
            JusticeProjectionResult<CourtCaseRecordData> privilegedCase = fixture.Justice.ProjectCase(fixture.CaseId, privileged: true);
            JusticeProjectionResult<CustodyRecordData> publicCustody = fixture.Justice.ProjectCustody(fixture.CustodyId, privileged: false);
            JusticeRuntimeSaveData save = fixture.Justice.CreateSaveData();
            JusticeRuntime restored = new JusticeRuntime();
            TestLabRuntimeBundle bundle = context.ScenarioContext.Runtimes;
            JusticeOperationResult restore = restored.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Governments, bundle.Laws, bundle.Organizations, bundle.OrganizationAuthority, bundle.Crimes, bundle.WorldId, bundle.KnownPersonIds, Array.Empty<string>());
            JusticeRuntimeSaveData corrupt = save.Clone();
            corrupt.cases[0].courtId = "court.testlab.missing";
            long before = restored.Revision;
            JusticeOperationResult rejected = restored.RestoreFromSaveData(corrupt, bundle.DefinitionRegistry, bundle.Governments, bundle.Laws, bundle.Organizations, bundle.OrganizationAuthority, bundle.Crimes, bundle.WorldId, bundle.KnownPersonIds, Array.Empty<string>());
            bool valid = arrest.Succeeded
                && publicCase.Succeeded
                && publicCase.Redacted
                && publicCase.Record.chargeIds.Length == 0
                && privilegedCase.Succeeded
                && !privilegedCase.Redacted
                && privilegedCase.Record.chargeIds.Length == 1
                && publicCustody.Succeeded
                && publicCustody.Redacted
                && string.IsNullOrEmpty(publicCustody.Record.currentFacilityPlaceId)
                && restore.Succeeded
                && rejected.Code == JusticeOperationCode.ValidationFailed
                && restored.Revision == before
                && restored.Cases.Count == 1;
            int restoredCaseCount = restored.Cases.Count;
            restored.Dispose();
            return TestLabAssertions.True("step13-justice-persistence", "Project, save, restore, and reject invalid justice graph", valid, $"Arrest={arrest.Code} Case={publicCase.Succeeded}/{publicCase.Redacted} Custody={publicCustody.Succeeded}/{publicCustody.Redacted} Restore={restore.Code} Reject={rejected.Code} Counts={restoredCaseCount}");
        }

        private static TestLabAutomationStepResult CreateRenameLifecycle(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out OrganizationRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-organization-lifecycle", "Create, rename, duplicate, and transition", "OrganizationRuntime", "Present", "Missing", failure);
            }

            long before = runtime.Revision;
            string organizationId = $"organization.testlab.guild.{context.RunId}";
            OrganizationOperationResult preview = runtime.CreateOrganization(CreateGuildRequest(organizationId, "Test Lab Guild", context.RunId, preview: true));
            OrganizationOperationResult create = runtime.CreateOrganization(CreateGuildRequest(organizationId, "Test Lab Guild", context.RunId));
            OrganizationOperationResult duplicate = runtime.CreateOrganization(CreateGuildRequest(organizationId, "Test Lab Guild", context.RunId));
            OrganizationOperationResult rename = runtime.RenameOrganization(new OrganizationRenameRequest
            {
                organizationId = organizationId,
                newOfficialName = "Test Lab Guild Office",
                effectiveWorldTime = 20d,
                transactionId = $"testlab.organization.rename.{context.RunId}"
            });
            OrganizationOperationResult dormant = runtime.TransitionLifecycle(new OrganizationLifecycleTransitionRequest
            {
                organizationId = organizationId,
                targetState = OrganizationLifecycleState.Dormant,
                worldTime = 30d,
                transactionId = $"testlab.organization.lifecycle.{context.RunId}"
            });
            runtime.TryGetSnapshot(organizationId, out OrganizationSnapshot snapshot);

            bool valid = preview.Status == OrganizationOperationStatus.Preview
                && create.Succeeded
                && duplicate.Duplicate
                && rename.Succeeded
                && dormant.Succeeded
                && snapshot != null
                && snapshot.CurrentName == "Test Lab Guild Office"
                && snapshot.LifecycleState == OrganizationLifecycleState.Dormant
                && runtime.Revision > before;
            return TestLabAssertions.True("step13-organization-lifecycle", "Create, rename, duplicate, and transition", valid, $"Preview={preview.Status} Create={create.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Rename={rename.Status} Dormant={dormant.Status} Revision={before}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult LinksAndProjections(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out OrganizationRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-organization-links", "Link hierarchy and read projections", "OrganizationRuntime", "Present", "Missing", failure);
            }

            string parentId = $"organization.testlab.parent.{context.RunId}";
            string childId = $"organization.testlab.branch.{context.RunId}";
            string hiddenId = $"organization.testlab.hidden.{context.RunId}";
            OrganizationOperationResult parent = runtime.CreateOrganization(CreateGuildRequest(parentId, "Parent Test Guild", context.RunId));
            OrganizationOperationResult child = runtime.CreateOrganization(CreateGuildRequest(childId, "Branch Test Guild", context.RunId));
            OrganizationOperationResult hidden = runtime.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = hiddenId,
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.SecretSocietyDefinitionId,
                officialName = "Hidden Test Circle",
                initialLifecycleState = OrganizationLifecycleState.Active,
                visibility = OrganizationVisibility.Hidden,
                transactionId = $"testlab.organization.create.hidden.{context.RunId}"
            });
            OrganizationOperationResult link = runtime.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = childId,
                targetOrganizationId = parentId,
                kind = OrganizationLinkKind.Parent,
                transactionId = $"testlab.organization.link.parent.{context.RunId}"
            });
            OrganizationOperationResult cycle = runtime.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = parentId,
                targetOrganizationId = childId,
                kind = OrganizationLinkKind.Parent,
                transactionId = $"testlab.organization.link.cycle.{context.RunId}"
            });
            OrganizationProjection redacted = runtime.ProjectOrganization(childId, PersistenceService.LocalPlayerId);
            OrganizationProjection concealed = runtime.ProjectOrganization(hiddenId, PersistenceService.LocalPlayerId);

            bool valid = parent.Succeeded
                && child.Succeeded
                && hidden.Succeeded
                && link.Succeeded
                && cycle.Status == OrganizationOperationStatus.CycleDetected
                && runtime.QueryByParent(parentId).Any(snapshot => snapshot.OrganizationId == childId)
                && redacted.Access == OrganizationProjectionAccess.Full
                && concealed.Access == OrganizationProjectionAccess.Concealed;
            return TestLabAssertions.True("step13-organization-links", "Link hierarchy and read projections", valid, $"Parent={parent.Status} Child={child.Status} Hidden={hidden.Status} Link={link.Status} Cycle={cycle.Status} Redacted={redacted.Access} Concealed={concealed.Access}");
        }

        private static TestLabAutomationStepResult PersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out OrganizationRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-organization-persistence", "Save, restore, and reject invalid payloads", "OrganizationRuntime", "Present", "Missing", failure);
            }

            string organizationId = $"organization.testlab.persisted.{context.RunId}";
            OrganizationOperationResult create = runtime.CreateOrganization(CreateGuildRequest(organizationId, "Persisted Test Guild", context.RunId));
            OrganizationRuntimeSaveData save = runtime.CreateSaveData();
            OrganizationRuntime restored = new OrganizationRuntime();
            OrganizationOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, PersistenceService.LocalWorldId, context.ScenarioContext.Runtimes.KnownPersonIds, Array.Empty<string>(), restoring: true);
            OrganizationRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationRuntimeSaveData>(JsonUtility.ToJson(save));
            OrganizationRecordData record = corrupt.records.First(item => item.organizationId == organizationId);
            record.organizationDefinitionId = "organization-definition.missing";
            bool rejected = !OrganizationRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, PersistenceService.LocalWorldId, context.ScenarioContext.Runtimes.KnownPersonIds, Array.Empty<string>(), out string validationFailure);

            bool valid = create.Succeeded
                && restore.Succeeded
                && restored.TryGetSnapshot(organizationId, out OrganizationSnapshot restoredSnapshot)
                && restoredSnapshot.CurrentName == "Persisted Test Guild"
                && rejected
                && runtime.TryGetSnapshot(organizationId, out OrganizationSnapshot liveSnapshot)
                && liveSnapshot.CurrentName == "Persisted Test Guild";
            return TestLabAssertions.True("step13-organization-persistence", "Save, restore, and reject invalid payloads", valid, $"Create={create.Status} Restore={restore.Status} Rejected={rejected}:{validationFailure} Count={runtime.Count}/{restored.Count}");
        }

        private static TestLabAutomationStepResult MembershipReadiness(TestLabAutomationContext context)
        {
            if (!TryGetMembershipRuntime(context, out OrganizationMembershipRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-membership-readiness", "Resolve organization membership definitions", "OrganizationMembershipRuntime", "Present", "Missing", failure);
            }

            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool full = registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, out OrganizationMembershipDefinition fullMember);
            bool invitee = registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId, out OrganizationMembershipDefinition inviteeMember);
            bool track = registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildRatingTrackId, out OrganizationRankTrackDefinition ratingTrack);
            bool topRank = registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId, out OrganizationRankDefinition sssRank);
            bool office = registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, out OrganizationOfficeDefinition guildmasterOffice);
            bool valid = runtime != null
                && full
                && invitee
                && track
                && topRank
                && office
                && fullMember.SupportsRanks
                && fullMember.SupportsOffices
                && inviteeMember.InitialStatus == OrganizationMembershipStatus.Invited
                && ratingTrack.SupportedMembershipDefinitionIds.Contains(PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId)
                && sssRank.TerminalRank
                && guildmasterOffice.RequiredRankDefinitionIds.Contains(PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId);

            return TestLabAssertions.True("step13-membership-readiness", "Resolve organization membership definitions", valid, $"Definitions={full}/{invitee}/{track}/{topRank}/{office} Runtime={runtime.MembershipCount}/{runtime.OfficeCount}");
        }

        private static TestLabAutomationStepResult ApplicationInvitationConsent(TestLabAutomationContext context)
        {
            if (!TryGetMembershipRuntime(context, out OrganizationMembershipRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-membership-consent", "Create pending records and require consent", "OrganizationMembershipRuntime", "Present", "Missing", failure);
            }

            string applicantId = "person.prototype.friend";
            string inviteeId = "person.prototype.rival";
            OrganizationMembershipOperationResult application = runtime.ApplyMembership(MembershipRequest(
                $"organization-membership.testlab.application.{context.RunId}",
                "organization.prototype.adventurers-guild",
                applicantId,
                PrototypeOrganizationMembershipDefinitionFactory.GuildApplicantId,
                OrganizationMembershipStatus.Applied,
                OrganizationMembershipSourceKind.Application,
                $"testlab.membership.application.{context.RunId}"));
            OrganizationMembershipOperationResult invitation = runtime.ApplyMembership(MembershipRequest(
                $"organization-membership.testlab.invitation.{context.RunId}",
                "organization.prototype.adventurers-guild",
                inviteeId,
                PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                OrganizationMembershipStatus.Invited,
                OrganizationMembershipSourceKind.Invitation,
                $"testlab.membership.invitation.{context.RunId}"));
            OrganizationMembershipOperationResult denied = runtime.ApplyMembership(MembershipRequest(
                invitation.Membership?.MembershipId,
                "organization.prototype.adventurers-guild",
                inviteeId,
                PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.Invitation,
                $"testlab.membership.invitation.denied.{context.RunId}"));
            OrganizationMembershipRequest accept = MembershipRequest(
                invitation.Membership?.MembershipId,
                "organization.prototype.adventurers-guild",
                inviteeId,
                PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.Invitation,
                $"testlab.membership.invitation.accept.{context.RunId}");
            accept.explicitConsent = true;
            OrganizationMembershipOperationResult accepted = runtime.ApplyMembership(accept);
            OrganizationMembershipOperationResult duplicate = runtime.ApplyMembership(accept);

            bool valid = application.Succeeded
                && application.Membership.Status == OrganizationMembershipStatus.Applied
                && invitation.Succeeded
                && invitation.Membership.Status == OrganizationMembershipStatus.Invited
                && denied.Status == OrganizationMembershipOperationStatus.ConsentRequired
                && accepted.Succeeded
                && accepted.Membership.Status == OrganizationMembershipStatus.Active
                && duplicate.Duplicate;

            return TestLabAssertions.True("step13-membership-consent", "Create pending records and require consent", valid, $"Application={application.Status}/{application.Membership?.Status} Invitation={invitation.Status}/{invitation.Membership?.Status} Denied={denied.Status} Accepted={accepted.Status}/{accepted.Membership?.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate}");
        }

        private static TestLabAutomationStepResult BranchMembership(TestLabAutomationContext context)
        {
            bool hasMemberships = TryGetMembershipRuntime(context, out OrganizationMembershipRuntime memberships, out string membershipFailure);
            bool hasOrganizations = TryGetRuntime(context, out OrganizationRuntime organizations, out string organizationFailure);
            if (!hasMemberships || !hasOrganizations)
            {
                return TestLabAssertions.Fail("step13-membership-branch", "Create parent and branch membership", "OrganizationMembershipRuntime", "Present", "Missing", $"{membershipFailure} {organizationFailure}".Trim());
            }

            string personId = "person.prototype.student";
            string branchId = $"organization.testlab.branch.{context.RunId}";
            OrganizationOperationResult branch = organizations.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = branchId,
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.BranchDefinitionId,
                officialName = "Test Lab Guild Branch",
                initialLifecycleState = OrganizationLifecycleState.Active,
                visibility = OrganizationVisibility.Public,
                transactionId = $"testlab.organization.branch.{context.RunId}"
            });
            OrganizationOperationResult link = organizations.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = branchId,
                targetOrganizationId = "organization.prototype.adventurers-guild",
                kind = OrganizationLinkKind.Parent,
                transactionId = $"testlab.organization.branch.link.{context.RunId}"
            });
            memberships.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, organizations, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            OrganizationMembershipOperationResult missingParent = memberships.ApplyMembership(MembershipRequest(
                $"organization-membership.testlab.branch.missing.{context.RunId}",
                branchId,
                personId,
                PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.WorldSetup,
                $"testlab.membership.branch.missing.{context.RunId}",
                consent: true));
            OrganizationMembershipOperationResult parent = memberships.ApplyMembership(MembershipRequest(
                $"organization-membership.testlab.parent.{context.RunId}",
                "organization.prototype.adventurers-guild",
                personId,
                PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.WorldSetup,
                $"testlab.membership.parent.{context.RunId}",
                consent: true));
            OrganizationMembershipRequest branchMembershipRequest = MembershipRequest(
                $"organization-membership.testlab.branch.{context.RunId}",
                branchId,
                personId,
                PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.WorldSetup,
                $"testlab.membership.branch.{context.RunId}",
                consent: true);
            branchMembershipRequest.parentMembershipId = parent.Membership?.MembershipId;
            branchMembershipRequest.branchOrganizationId = branchId;
            OrganizationMembershipOperationResult child = memberships.ApplyMembership(branchMembershipRequest);

            bool valid = branch.Succeeded
                && link.Succeeded
                && missingParent.Status == OrganizationMembershipOperationStatus.InvalidDependency
                && parent.Succeeded
                && child.Succeeded
                && child.Membership.Data.parentMembershipId == parent.Membership.MembershipId
                && memberships.QueryMemberships(personId).Select(snapshot => snapshot.MembershipId).SequenceEqual(memberships.QueryMemberships(personId).Select(snapshot => snapshot.MembershipId));

            return TestLabAssertions.True("step13-membership-branch", "Create parent and branch membership", valid, $"Branch={branch.Status} Link={link.Status} MissingParent={missingParent.Status} Parent={parent.Status} Child={child.Status} Query={memberships.QueryMemberships(personId).Count}");
        }

        private static TestLabAutomationStepResult RankProgression(TestLabAutomationContext context)
        {
            if (!TryGetMembershipRuntime(context, out OrganizationMembershipRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-membership-ranks", "Assign and compare organization ranks", "OrganizationMembershipRuntime", "Present", "Missing", failure);
            }

            string membershipId = $"organization-membership.testlab.rank.{context.RunId}";
            OrganizationMembershipOperationResult member = runtime.ApplyMembership(MembershipRequest(membershipId, "organization.prototype.adventurers-guild", "person.prototype.mentor", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.membership.rank.member.{context.RunId}", consent: true));
            OrganizationMembershipOperationResult skipped = runtime.AssignRank(RankRequest($"organization-rank-assignment.testlab.skipped.{context.RunId}", membershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildERankId, $"testlab.membership.rank.skipped.{context.RunId}"));
            OrganizationMembershipOperationResult[] progression = PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds
                .Select((rankId, index) => runtime.AssignRank(RankRequest($"organization-rank-assignment.testlab.rating.{index}.{context.RunId}", membershipId, rankId, $"testlab.membership.rank.rating.{index}.{context.RunId}")))
                .ToArray();
            OrganizationMembershipOperationResult duplicate = runtime.AssignRank(RankRequest($"organization-rank-assignment.testlab.rating.8.{context.RunId}", membershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId, $"testlab.membership.rank.rating.8.{context.RunId}"));
            runtime.TryGetMembership(membershipId, out OrganizationMembershipSnapshot snapshot);

            bool valid = member.Succeeded
                && skipped.Status == OrganizationMembershipOperationStatus.InvalidTransition
                && progression.All(result => result.Succeeded)
                && duplicate.Duplicate
                && runtime.CompareRanks(PrototypeOrganizationMembershipDefinitionFactory.GuildFRankId, PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId) < 0
                && snapshot.RankAssignments.Count == 9
                && snapshot.RankAssignments.Count(item => item.state == OrganizationRankAssignmentState.Active) == 1
                && snapshot.RankAssignments.Single(item => item.state == OrganizationRankAssignmentState.Active).rankDefinitionId == PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId;

            return TestLabAssertions.True("step13-membership-ranks", "Assign and compare organization ranks", valid, $"Member={member.Status} Skipped={skipped.Status} Progression={string.Join(",", progression.Select(result => result.Status))} Duplicate={duplicate.Status}/{duplicate.Duplicate} Active={snapshot?.RankAssignments.Count(item => item.state == OrganizationRankAssignmentState.Active)}");
        }

        private static TestLabAutomationStepResult OfficeAppointments(TestLabAutomationContext context)
        {
            if (!TryGetMembershipRuntime(context, out OrganizationMembershipRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-membership-offices", "Create and assign organization offices", "OrganizationMembershipRuntime", "Present", "Missing", failure);
            }

            string masterMembershipId = $"organization-membership.testlab.office.master.{context.RunId}";
            string associateOneId = $"organization-membership.testlab.office.associate1.{context.RunId}";
            string associateTwoId = $"organization-membership.testlab.office.associate2.{context.RunId}";
            OrganizationMembershipOperationResult masterMember = runtime.ApplyMembership(MembershipRequest(masterMembershipId, "organization.prototype.adventurers-guild", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.office.member.master.{context.RunId}", consent: true));
            for (int index = 0; index < PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.Count; index++)
            {
                runtime.AssignRank(RankRequest($"organization-rank-assignment.testlab.office.rating.{index}.{context.RunId}", masterMembershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds[index], $"testlab.office.rank.rating.{index}.{context.RunId}"));
            }
            OrganizationMembershipOperationResult guildmaster = runtime.CreateOffice(OfficeRequest($"organization-office-record.testlab.guildmaster.{context.RunId}", "organization.prototype.adventurers-guild", PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, $"testlab.office.guildmaster.{context.RunId}"));
            OrganizationMembershipOperationResult assignGuildmaster = runtime.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.guildmaster.{context.RunId}", guildmaster.Office?.OfficeId, masterMembershipId, $"testlab.office.assign.guildmaster.{context.RunId}"));
            OrganizationMembershipOperationResult duplicateGuildmaster = runtime.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.guildmaster.duplicate.{context.RunId}", guildmaster.Office?.OfficeId, masterMembershipId, $"testlab.office.assign.guildmaster.duplicate.{context.RunId}"));

            OrganizationMembershipOperationResult associateOne = runtime.ApplyMembership(MembershipRequest(associateOneId, "organization.prototype.adventurers-guild", "person.prototype.cousin", PrototypeOrganizationMembershipDefinitionFactory.GuildAssociateId, OrganizationMembershipStatus.Provisional, OrganizationMembershipSourceKind.WorldSetup, $"testlab.office.member.associate1.{context.RunId}", consent: true));
            OrganizationMembershipOperationResult associateTwo = runtime.ApplyMembership(MembershipRequest(associateTwoId, "organization.prototype.adventurers-guild", "person.prototype.student", PrototypeOrganizationMembershipDefinitionFactory.GuildAssociateId, OrganizationMembershipStatus.Provisional, OrganizationMembershipSourceKind.WorldSetup, $"testlab.office.member.associate2.{context.RunId}", consent: true));
            OrganizationMembershipOperationResult treasurer = runtime.CreateOffice(OfficeRequest($"organization-office-record.testlab.treasurer.{context.RunId}", "organization.prototype.adventurers-guild", PrototypeOrganizationMembershipDefinitionFactory.GuildTreasurerOfficeId, $"testlab.office.treasurer.{context.RunId}", maximumHolders: 2));
            OrganizationMembershipOperationResult assignOne = runtime.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.treasurer1.{context.RunId}", treasurer.Office?.OfficeId, associateOneId, $"testlab.office.assign.treasurer1.{context.RunId}", acting: true));
            OrganizationMembershipOperationResult assignTwo = runtime.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.treasurer2.{context.RunId}", treasurer.Office?.OfficeId, associateTwoId, $"testlab.office.assign.treasurer2.{context.RunId}"));
            runtime.TryGetOffice(treasurer.Office?.OfficeId, out OrganizationOfficeSnapshot treasurerSnapshot);

            bool valid = masterMember.Succeeded
                && guildmaster.Succeeded
                && assignGuildmaster.Succeeded
                && duplicateGuildmaster.Status == OrganizationMembershipOperationStatus.CapacityFull
                && associateOne.Succeeded
                && associateTwo.Succeeded
                && treasurer.Succeeded
                && assignOne.Succeeded
                && assignOne.OfficeAssignment.acting
                && assignTwo.Succeeded
                && treasurerSnapshot.Assignments.Count(item => item.IsActive) == 2
                && !treasurerSnapshot.IsVacant;

            return TestLabAssertions.True("step13-membership-offices", "Create and assign organization offices", valid, $"Master={masterMember.Status} Guildmaster={guildmaster.Status}/{assignGuildmaster.Status}/{duplicateGuildmaster.Status} Treasurer={treasurer.Status} Assignments={assignOne.Status}/{assignTwo.Status} Active={treasurerSnapshot?.Assignments.Count(item => item.IsActive)}");
        }

        private static TestLabAutomationStepResult EndingDependenciesAndIdempotence(TestLabAutomationContext context)
        {
            if (!TryGetMembershipRuntime(context, out OrganizationMembershipRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-membership-ending", "End membership with assignment policy", "OrganizationMembershipRuntime", "Present", "Missing", failure);
            }

            string membershipId = $"organization-membership.testlab.end.{context.RunId}";
            OrganizationMembershipOperationResult member = runtime.ApplyMembership(MembershipRequest(membershipId, "organization.prototype.adventurers-guild", "person.prototype.dependent", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.membership.end.member.{context.RunId}", consent: true));
            runtime.AssignRank(RankRequest($"organization-rank-assignment.testlab.end.f.{context.RunId}", membershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildFRankId, $"testlab.membership.end.rank.{context.RunId}"));
            OrganizationMembershipOperationResult office = runtime.CreateOffice(OfficeRequest($"organization-office-record.testlab.end.treasurer.{context.RunId}", "organization.prototype.adventurers-guild", PrototypeOrganizationMembershipDefinitionFactory.GuildTreasurerOfficeId, $"testlab.membership.end.office.{context.RunId}", maximumHolders: 2));
            runtime.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.end.treasurer.{context.RunId}", office.Office?.OfficeId, membershipId, $"testlab.membership.end.office.assign.{context.RunId}"));
            OrganizationMembershipRequest blockedRequest = MembershipRequest(membershipId, "organization.prototype.adventurers-guild", "person.prototype.dependent", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Resigned, OrganizationMembershipSourceKind.ScriptedEvent, $"testlab.membership.end.blocked.{context.RunId}");
            OrganizationMembershipOperationResult blocked = runtime.ApplyMembership(blockedRequest);
            OrganizationMembershipRequest endRequest = MembershipRequest(membershipId, "organization.prototype.adventurers-guild", "person.prototype.dependent", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Resigned, OrganizationMembershipSourceKind.ScriptedEvent, $"testlab.membership.end.apply.{context.RunId}");
            endRequest.endingPolicy = OrganizationMembershipEndingPolicy.EndActiveAssignments;
            OrganizationMembershipOperationResult ended = runtime.ApplyMembership(endRequest);
            OrganizationMembershipOperationResult duplicate = runtime.ApplyMembership(endRequest);
            runtime.TryGetMembership(membershipId, out OrganizationMembershipSnapshot snapshot);

            bool valid = member.Succeeded
                && blocked.Status == OrganizationMembershipOperationStatus.ActiveAssignmentsBlockEnding
                && ended.Succeeded
                && ended.Membership.Status == OrganizationMembershipStatus.Resigned
                && duplicate.Duplicate
                && snapshot.RankAssignments.All(rank => !rank.IsActive)
                && snapshot.OfficeAssignments.All(assignment => !assignment.IsActive);

            return TestLabAssertions.True("step13-membership-ending", "End membership with assignment policy", valid, $"Member={member.Status} Blocked={blocked.Status} Ended={ended.Status}/{ended.Membership?.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Ranks={snapshot?.RankAssignments.Count} Offices={snapshot?.OfficeAssignments.Count}");
        }

        private static TestLabAutomationStepResult MembershipProjectionAndPersistence(TestLabAutomationContext context)
        {
            if (!TryGetMembershipRuntime(context, out OrganizationMembershipRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-membership-persistence", "Project, save, restore, and reject invalid membership state", "OrganizationMembershipRuntime", "Present", "Missing", failure);
            }

            string visibleMembershipId = $"organization-membership.testlab.persist.visible.{context.RunId}";
            string hiddenMembershipId = $"organization-membership.testlab.persist.hidden.{context.RunId}";
            OrganizationMembershipOperationResult visible = runtime.ApplyMembership(MembershipRequest(visibleMembershipId, "organization.prototype.adventurers-guild", "person.prototype.partner", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.membership.persist.visible.{context.RunId}", consent: true));
            OrganizationMembershipRequest hiddenRequest = MembershipRequest(hiddenMembershipId, "organization.prototype.adventurers-guild", "person.prototype.spouse", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.membership.persist.hidden.{context.RunId}", consent: true);
            hiddenRequest.visibility = OrganizationVisibility.Hidden;
            OrganizationMembershipOperationResult hidden = runtime.ApplyMembership(hiddenRequest);
            OrganizationMembershipProjection publicProjection = runtime.ProjectMembership(visibleMembershipId, "person.prototype.friend");
            OrganizationMembershipProjection hiddenProjection = runtime.ProjectMembership(hiddenMembershipId, "person.prototype.friend");
            OrganizationMembershipProjection privilegedProjection = runtime.ProjectMembership(hiddenMembershipId, "person.prototype.friend", privileged: true);

            OrganizationMembershipRuntimeSaveData save = runtime.CreateSaveData();
            OrganizationMembershipRuntime restored = new OrganizationMembershipRuntime();
            OrganizationMembershipOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId), restoring: true);
            OrganizationMembershipRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationMembershipRuntimeSaveData>(JsonUtility.ToJson(save));
            corrupt.memberships.First(item => item.membershipId == visibleMembershipId).membershipDefinitionId = "organization-membership.missing";
            bool rejected = !OrganizationMembershipRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId), out string validationFailure);
            bool liveUnchanged = runtime.TryGetMembership(visibleMembershipId, out OrganizationMembershipSnapshot liveVisible)
                && liveVisible.Status == OrganizationMembershipStatus.Active
                && runtime.MembershipCount == save.memberships.Count;

            bool valid = visible.Succeeded
                && hidden.Succeeded
                && publicProjection.Access == OrganizationMembershipProjectionAccess.Full
                && hiddenProjection.Access == OrganizationMembershipProjectionAccess.Concealed
                && privilegedProjection.Access == OrganizationMembershipProjectionAccess.Full
                && restore.Succeeded
                && restored.TryGetMembership(visibleMembershipId, out OrganizationMembershipSnapshot restoredVisible)
                && restoredVisible.Status == OrganizationMembershipStatus.Active
                && rejected
                && liveUnchanged;

            return TestLabAssertions.True("step13-membership-persistence", "Project, save, restore, and reject invalid membership state", valid, $"Visible={visible.Status} Hidden={hidden.Status} Projection={publicProjection.Access}/{hiddenProjection.Access}/{privilegedProjection.Access} Restore={restore.Status} Rejected={rejected}:{validationFailure} Live={liveUnchanged}");
        }

        private static TestLabAutomationStepResult AuthorityReadiness(TestLabAutomationContext context)
        {
            if (!TryGetAuthorityRuntime(context, out OrganizationAuthorityRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-authority-readiness", "Resolve organization authority definitions", "OrganizationAuthorityRuntime", "Present", "Missing", failure);
            }

            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool permission = registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholdersPermissionId, out OrganizationPermissionDefinition appoint)
                && appoint.Category == OrganizationPermissionCategory.ManageOffices;
            bool action = registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholderActionId, out InstitutionalActionDefinition appointAction)
                && appointAction.RequiredPermissionIds.Contains(PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholdersPermissionId);
            bool role = registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId, out OrganizationAuthorityRoleDefinition guildmaster)
                && guildmaster.GrantedPermissionIds.Contains(PrototypeOrganizationAuthorityDefinitionFactory.PromoteMembersPermissionId);
            bool binding = registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterOfficeBindingId, out OrganizationAuthorityBindingDefinition bindingDefinition)
                && bindingDefinition.AuthorityRoleDefinitionId == PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId;
            string actorId = PrimaryAuthorityActorId(context);
            OrganizationEffectiveAuthoritySnapshot first = runtime.QueryEffectiveAuthority(actorId, "organization.prototype.adventurers-guild", 10d);
            OrganizationEffectiveAuthoritySnapshot second = runtime.QueryEffectiveAuthority(actorId, "organization.prototype.adventurers-guild", 10d);

            bool valid = permission
                && action
                && role
                && binding
                && first.RuntimeRevision == second.RuntimeRevision
                && first.Sources.Count == second.Sources.Count;

            return TestLabAssertions.True("step13-authority-readiness", "Resolve organization authority definitions", valid, $"Permission={permission} Action={action} Role={role} Binding={binding} Sources={first.Sources.Count}/{second.Sources.Count}");
        }

        private static TestLabAutomationStepResult AuthorityFromMembershipRankOffice(TestLabAutomationContext context)
        {
            bool hasAuthority = TryGetAuthorityRuntime(context, out OrganizationAuthorityRuntime authority, out string authorityFailure);
            bool hasMemberships = TryGetMembershipRuntime(context, out OrganizationMembershipRuntime memberships, out string membershipFailure);
            if (!hasAuthority || !hasMemberships)
            {
                string details = authorityFailure.Length > 0 ? authorityFailure : membershipFailure;
                return TestLabAssertions.Fail("step13-authority-bindings", "Evaluate bound membership authority", "OrganizationAuthorityRuntime", "Present", "Missing", details);
            }

            string actorId = PrimaryAuthorityActorId(context);
            CreateAuthorityGuildmaster(context, actorId, "master");
            memberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.authority.general.{context.RunId}", "organization.prototype.adventurers-guild", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.member.general.{context.RunId}", consent: true));

            OrganizationAuthorizationResult guildmaster = authority.EvaluateAuthorization(AuthorityRequest(actorId, "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholderActionId, $"testlab.authority.auth.guildmaster.{context.RunId}"));
            OrganizationAuthorizationResult general = authority.EvaluateAuthorization(AuthorityRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholderActionId, $"testlab.authority.auth.general.{context.RunId}"));
            OrganizationEffectiveAuthoritySnapshot effective = authority.QueryEffectiveAuthority(actorId, "organization.prototype.adventurers-guild", 100d);

            bool valid = guildmaster.Succeeded
                && general.Status == OrganizationAuthorizationStatus.MissingPermission
                && effective.Sources.Any(source => source.permissionDefinitionId == PrototypeOrganizationAuthorityDefinitionFactory.PromoteMembersPermissionId)
                && memberships.MembershipCount >= 2;

            return TestLabAssertions.True("step13-authority-bindings", "Evaluate bound membership authority", valid, $"Guildmaster={guildmaster.Status} General={general.Status} Sources={effective.Sources.Count} Memberships={memberships.MembershipCount}");
        }

        private static TestLabAutomationStepResult AuthorityDirectGrantsDelegation(TestLabAutomationContext context)
        {
            if (!TryGetAuthorityRuntime(context, out OrganizationAuthorityRuntime authority, out string failure))
            {
                return TestLabAssertions.Fail("step13-authority-delegation", "Create and delegate scoped authority", "OrganizationAuthorityRuntime", "Present", "Missing", failure);
            }

            string actorId = PrimaryAuthorityActorId(context);
            CreateAuthorityGuildmaster(context, actorId, "master");
            context.ScenarioContext.Runtimes.OrganizationMemberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.authority.direct.friend.{context.RunId}", "organization.prototype.adventurers-guild", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.direct.friend.member.{context.RunId}", consent: true));
            OrganizationAuthorityOperationResult direct = authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = $"organization-authority-grant.testlab.direct.{context.RunId}",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = actorId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 20d,
                expirationWorldTime = 40d,
                delegationPolicy = OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation,
                transactionId = $"testlab.authority.direct.{context.RunId}"
            });
            OrganizationAuthorityOperationResult duplicate = authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = direct.Grant?.GrantId,
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = actorId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 20d,
                expirationWorldTime = 40d,
                transactionId = $"testlab.authority.direct.{context.RunId}"
            });
            OrganizationAuthorizationResult authorized = authority.EvaluateAuthorization(AuthorityRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, $"testlab.authority.friend.orders.{context.RunId}", 30d));
            OrganizationAuthorizationResult expired = authority.EvaluateAuthorization(AuthorityRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, $"testlab.authority.friend.orders.expired.{context.RunId}", 50d));
            OrganizationAuthorityOperationResult delegated = authority.DelegateAuthority(new OrganizationDelegationRequest
            {
                delegationGrantId = $"organization-authority-grant.testlab.delegated.{context.RunId}",
                organizationId = "organization.prototype.adventurers-guild",
                delegatorPersonId = "person.prototype.friend",
                recipientPersonId = "person.prototype.student",
                sourceAuthorityId = direct.Grant?.GrantId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 25d,
                expirationWorldTime = 35d,
                transactionId = $"testlab.authority.delegate.{context.RunId}"
            });
            OrganizationAuthorityOperationResult redelegated = authority.DelegateAuthority(new OrganizationDelegationRequest
            {
                delegationGrantId = $"organization-authority-grant.testlab.redelegated.{context.RunId}",
                organizationId = "organization.prototype.adventurers-guild",
                delegatorPersonId = "person.prototype.student",
                recipientPersonId = "person.prototype.rival",
                sourceAuthorityId = delegated.Grant?.GrantId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 26d,
                expirationWorldTime = 30d,
                transactionId = $"testlab.authority.redelegate.{context.RunId}"
            });

            bool valid = direct.Succeeded
                && duplicate.Duplicate
                && authorized.Succeeded
                && expired.Status == OrganizationAuthorizationStatus.MissingPermission
                && delegated.Succeeded
                && !redelegated.Succeeded
                && redelegated.Status == OrganizationAuthorizationStatus.InvalidDependency;

            return TestLabAssertions.True("step13-authority-delegation", "Create and delegate scoped authority", valid, $"Direct={direct.Status} Duplicate={duplicate.Status}/{duplicate.Duplicate} Authorized={authorized.Status} Expired={expired.Status} Delegated={delegated.Status} Redelegated={redelegated.Status}");
        }

        private static TestLabAutomationStepResult AuthorityBranchScopeBoundaries(TestLabAutomationContext context)
        {
            bool hasAuthority = TryGetAuthorityRuntime(context, out OrganizationAuthorityRuntime authority, out string authorityFailure);
            bool hasOrganizations = TryGetRuntime(context, out OrganizationRuntime organizations, out string organizationFailure);
            bool hasMemberships = TryGetMembershipRuntime(context, out OrganizationMembershipRuntime memberships, out string membershipFailure);
            if (!hasAuthority || !hasOrganizations || !hasMemberships)
            {
                string details = authorityFailure.Length > 0 ? authorityFailure : organizationFailure.Length > 0 ? organizationFailure : membershipFailure;
                return TestLabAssertions.Fail("step13-authority-branch", "Evaluate parent and branch authority boundaries", "OrganizationAuthorityRuntime", "Present", "Missing", details);
            }

            string branchId = $"organization.testlab.branch.authority.{context.RunId}";
            organizations.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = branchId,
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.BranchDefinitionId,
                officialName = "Authority Branch",
                initialLifecycleState = OrganizationLifecycleState.Active,
                transactionId = $"testlab.authority.branch.create.{context.RunId}"
            });
            organizations.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = branchId,
                targetOrganizationId = "organization.prototype.adventurers-guild",
                kind = OrganizationLinkKind.Parent,
                transactionId = $"testlab.authority.branch.link.{context.RunId}"
            });
            authority.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, organizations, memberships, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            string actorId = PrimaryAuthorityActorId(context);
            OrganizationMembershipOperationResult parentMembership = memberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.branch.parent.{context.RunId}", "organization.prototype.adventurers-guild", actorId, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.branch.parent.member.{context.RunId}", consent: true));
            AssignGuildMasterRank(memberships, parentMembership.Membership?.MembershipId, context.RunId, "branch-parent");
            OrganizationMembershipRequest branchRequest = MembershipRequest($"organization-membership.testlab.branch.member.{context.RunId}", branchId, "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.branch.member.{context.RunId}", consent: true);
            branchRequest.parentMembershipId = parentMembership.Membership?.MembershipId;
            branchRequest.branchOrganizationId = branchId;
            OrganizationMembershipOperationResult branchMembership = memberships.ApplyMembership(branchRequest);
            OrganizationMembershipOperationResult branchOffice = memberships.CreateOffice(OfficeRequest($"organization-office-record.testlab.branch.master.{context.RunId}", branchId, PrototypeOrganizationMembershipDefinitionFactory.BranchChapterMasterOfficeId, $"testlab.authority.branch.office.{context.RunId}"));
            OrganizationMembershipOperationResult branchAssignment = memberships.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.branch.master.{context.RunId}", branchOffice.Office?.OfficeId, branchMembership.Membership?.MembershipId, $"testlab.authority.branch.office.assign.{context.RunId}"));

            OrganizationAuthorizationResult parentOnBranch = authority.EvaluateAuthorization(AuthorityRequest(actorId, branchId, PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, $"testlab.authority.parent.branch.{context.RunId}"));
            OrganizationAuthorizationResult branchOnBranch = authority.EvaluateAuthorization(AuthorityRequest("person.prototype.friend", branchId, PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, $"testlab.authority.branch.branch.{context.RunId}"));
            OrganizationAuthorizationResult branchOnParent = authority.EvaluateAuthorization(AuthorityRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, $"testlab.authority.branch.parent.{context.RunId}"));

            bool valid = parentMembership.Succeeded
                && branchMembership.Succeeded
                && branchOffice.Succeeded
                && branchAssignment.Succeeded
                && parentOnBranch.Status == OrganizationAuthorizationStatus.MissingPermission
                && branchOnBranch.Succeeded
                && branchOnParent.Status == OrganizationAuthorizationStatus.MissingPermission;

            return TestLabAssertions.True("step13-authority-branch", "Evaluate parent and branch authority boundaries", valid, $"Parent={parentOnBranch.Status} Branch={branchOnBranch.Status} Reverse={branchOnParent.Status} Memberships={parentMembership.Status}/{branchMembership.Status} Office={branchOffice.Status}/{branchAssignment.Status}");
        }

        private static TestLabAutomationStepResult AuthorityJointApprovalAudits(TestLabAutomationContext context)
        {
            if (!TryGetAuthorityRuntime(context, out OrganizationAuthorityRuntime authority, out string failure))
            {
                return TestLabAssertions.Fail("step13-authority-approvals", "Authorize joint institutional action", "OrganizationAuthorityRuntime", "Present", "Missing", failure);
            }

            string actorId = PrimaryAuthorityActorId(context);
            CreateAuthorityGuildmaster(context, actorId, "approver-master");
            context.ScenarioContext.Runtimes.OrganizationMemberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.approver.mentor.{context.RunId}", "organization.prototype.adventurers-guild", "person.prototype.mentor", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.approver.mentor.{context.RunId}", consent: true));
            context.ScenarioContext.Runtimes.OrganizationMemberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.approver.partner.{context.RunId}", "organization.prototype.adventurers-guild", "person.prototype.partner", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.approver.partner.{context.RunId}", consent: true));
            OrganizationAuthorityOperationResult mentorGrant = GrantGuildmasterRole(authority, actorId, "person.prototype.mentor", context.RunId, "mentor");
            OrganizationAuthorityOperationResult partnerGrant = GrantGuildmasterRole(authority, actorId, "person.prototype.partner", context.RunId, "partner");
            string operationId = $"testlab.authority.operation.headquarters.{context.RunId}";
            OrganizationAuthorityOperationResult approvalOne = authority.RecordApproval(ApprovalRequest($"organization-authority-approval.testlab.one.{context.RunId}", operationId, "person.prototype.mentor"));
            OrganizationAuthorityOperationResult approvalTwo = authority.RecordApproval(ApprovalRequest($"organization-authority-approval.testlab.two.{context.RunId}", operationId, "person.prototype.partner"));
            OrganizationAuthorizationRequest deniedRequest = AuthorityRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId, operationId);
            deniedRequest.consumeApprovals = true;
            OrganizationAuthorizationRequest authorizedRequest = AuthorityRequest(actorId, "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId, operationId);
            authorizedRequest.consumeApprovals = true;

            OrganizationAuthorizationResult denied = authority.EvaluateAuthorization(deniedRequest);
            OrganizationAuthorizationResult authorized = authority.EvaluateAuthorization(authorizedRequest);
            OrganizationAuthorityOperationResult audit = authority.RecordAuthorizationAudit(authorized, $"organization-authority-audit.testlab.headquarters.{context.RunId}", 120d);

            bool valid = mentorGrant.Succeeded
                && partnerGrant.Succeeded
                && approvalOne.Succeeded
                && approvalTwo.Succeeded
                && !denied.Succeeded
                && authorized.Succeeded
                && authorized.ApprovalIds.Count == 2
                && authority.Approvals.All(approval => approval.LifecycleState == OrganizationApprovalLifecycleState.Consumed)
                && audit.Succeeded
                && authority.Audits.Any(item => item.Status == OrganizationAuthorizationStatus.Authorized);

            return TestLabAssertions.True("step13-authority-approvals", "Authorize joint institutional action", valid, $"Grants={mentorGrant.Status}/{partnerGrant.Status} Approvals={approvalOne.Status}/{approvalTwo.Status} Denied={denied.Status} Authorized={authorized.Status} Audit={audit.Status}");
        }

        private static TestLabAutomationStepResult AuthorityPersistenceValidation(TestLabAutomationContext context)
        {
            bool hasAuthority = TryGetAuthorityRuntime(context, out OrganizationAuthorityRuntime authority, out string authorityFailure);
            bool hasOrganizations = TryGetRuntime(context, out OrganizationRuntime organizations, out string organizationFailure);
            bool hasMemberships = TryGetMembershipRuntime(context, out OrganizationMembershipRuntime memberships, out string membershipFailure);
            if (!hasAuthority || !hasOrganizations || !hasMemberships)
            {
                string details = authorityFailure.Length > 0 ? authorityFailure : organizationFailure.Length > 0 ? organizationFailure : membershipFailure;
                return TestLabAssertions.Fail("step13-authority-persistence", "Save, restore, and reject invalid authority state", "OrganizationAuthorityRuntime", "Present", "Missing", details);
            }

            OrganizationAuthorityOperationResult grant = authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = $"organization-authority-grant.testlab.persist.{context.RunId}",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = PrimaryAuthorityActorId(context),
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.ViewRestrictedInformationPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 0d,
                transactionId = $"testlab.authority.persist.{context.RunId}"
            });
            OrganizationAuthorityRuntimeSaveData save = authority.CreateSaveData();
            OrganizationAuthorityRuntime restored = new OrganizationAuthorityRuntime();
            OrganizationAuthorityOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, organizations, memberships, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            OrganizationAuthorityRuntimeSaveData corrupt = save.Clone();
            corrupt.grants[0].permissionDefinitionIds = new[] { "organization-permission.missing" };
            bool rejected = !OrganizationAuthorityRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, organizations, memberships, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId), out string validationFailure);
            bool liveUnchanged = authority.TryGetGrant(grant.Grant?.GrantId, out OrganizationAuthoritySnapshot live)
                && live.Data.permissionDefinitionIds.Contains(PrototypeOrganizationAuthorityDefinitionFactory.ViewRestrictedInformationPermissionId)
                && authority.GrantCount == save.grants.Count;

            bool valid = grant.Succeeded
                && restore.Succeeded
                && restored.TryGetGrant(grant.Grant?.GrantId, out _)
                && rejected
                && liveUnchanged;

            return TestLabAssertions.True("step13-authority-persistence", "Save, restore, and reject invalid authority state", valid, $"Grant={grant.Status} Restore={restore.Status} Rejected={rejected}:{validationFailure} Live={liveUnchanged}");
        }

        private static TestLabAutomationStepResult ResourceRuntimeReadiness(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            OrganizationResourceTypeDefinition currencyType = null;
            bool resourceType = runtimes?.DefinitionRegistry?.TryGet(PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId, out currencyType) == true;
            bool currency = TryResourceCurrency(context, out CurrencyDefinition currencyDefinition);
            bool valid = runtimes?.OrganizationResources?.IsReady == true
                && runtimes.Organizations != null
                && runtimes.OrganizationMemberships != null
                && runtimes.OrganizationAuthority != null
                && runtimes.Economy != null
                && runtimes.Properties != null
                && runtimes.Businesses != null
                && runtimes.ItemInstances != null
                && resourceType
                && currency
                && currencyType.Category == OrganizationResourceCategory.Currency;
            return TestLabAssertions.True("step13-resources-readiness", "Validate resource runtime dependencies", valid, $"Ready={runtimes?.OrganizationResources?.IsReady} Type={resourceType}:{currencyType?.Category} Currency={currencyDefinition?.Id} Dependencies={runtimes?.Organizations != null}/{runtimes?.OrganizationAuthority != null}/{runtimes?.Economy != null}/{runtimes?.Properties != null}/{runtimes?.Businesses != null}/{runtimes?.ItemInstances != null}");
        }

        private static TestLabAutomationStepResult ResourceTreasuryCreation(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 500L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            bool queried = prepared
                && resources.QueryTreasuries("organization.prototype.adventurers-guild", activeOnly: true).Count == 1
                && resources.QueryAccounts("organization.prototype.adventurers-guild").Count == 2
                && resources.GetBalance(ResourceOperatingAccountId(context), 10d)?.BalanceUnits == 500L
                && resources.GetBalance(ResourceReserveAccountId(context), 10d)?.BalanceUnits == 0L;
            return TestLabAssertions.True("step13-resources-treasury", "Create treasury and organization accounts", prepared && queried, $"Prepared={prepared}:{failure} Actor={actorId} Currency={currency?.Id} Treasuries={resources?.TreasuryCount} Accounts={resources?.AccountCount}");
        }

        private static TestLabAutomationStepResult ResourceTransferIdempotence(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 100L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-transfer", "Deposit and transfer authoritative funds", "ResourceFixture", "Prepared", "Failed", failure);
            string depositId = $"testlab.resources.deposit.{context.RunId}";
            OrganizationResourceOperationResult deposit = resources.DepositFunds(new OrganizationFinancialTransactionRequest
            {
                transactionId = depositId, organizationId = "organization.prototype.adventurers-guild", destinationAccountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id,
                units = 50L, actorPersonId = actorId, relatedRecordId = $"revenue.testlab.{context.RunId}", purpose = "test revenue", worldTime = 10d
            });
            OrganizationFinancialTransactionRequest transferRequest = ResourceTransferRequest(context, currency.Id, actorId, 40L, $"testlab.resources.transfer.{context.RunId}", 11d);
            OrganizationResourceOperationResult transfer = resources.TransferFunds(transferRequest);
            OrganizationResourceOperationResult duplicate = resources.TransferFunds(transferRequest);
            EconomyRuntimeSaveData economy = context.ScenarioContext.Runtimes.Economy.CreateSaveData();
            EconomyLedgerEntryData[] ledger = economy.ledgerEntries.Where(entry => entry.transactionId == transferRequest.transactionId).ToArray();
            bool balanced = ledger.Where(entry => entry.kind == EconomyLedgerEntryKind.Debit).Sum(entry => entry.units) == ledger.Where(entry => entry.kind == EconomyLedgerEntryKind.Credit).Sum(entry => entry.units);
            bool valid = deposit.Succeeded && transfer.Succeeded && duplicate.Duplicate && balanced
                && resources.GetBalance(ResourceOperatingAccountId(context), 12d).BalanceUnits == 110L
                && resources.GetBalance(ResourceReserveAccountId(context), 12d).BalanceUnits == 40L
                && economy.transactions.Count(entry => entry.transactionId == transferRequest.transactionId) == 1;
            return TestLabAssertions.True("step13-resources-transfer", "Deposit and transfer authoritative funds", valid, $"Deposit={deposit.Code} Transfer={transfer.Code} Duplicate={duplicate.Code}/{duplicate.Duplicate} Ledger={ledger.Length}/{balanced} Balances={resources.GetBalance(ResourceOperatingAccountId(context), 12d)?.BalanceUnits}/{resources.GetBalance(ResourceReserveAccountId(context), 12d)?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult ResourceAuthorityDenial(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 100L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-authority", "Enforce organization financial authority", "ResourceFixture", "Prepared", "Failed", failure);
            long before = resources.GetBalance(ResourceOperatingAccountId(context), 10d).BalanceUnits;
            OrganizationFinancialTransactionRequest deniedRequest = new OrganizationFinancialTransactionRequest
            {
                transactionId = $"testlab.resources.withdraw.denied.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", sourceAccountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id,
                units = 10L, actorPersonId = "person.prototype.friend", relatedRecordId = $"settlement.denied.{context.RunId}", purpose = "unauthorized", worldTime = 10d
            };
            OrganizationResourceOperationResult denied = resources.WithdrawFunds(deniedRequest);
            deniedRequest.transactionId = $"testlab.resources.withdraw.allowed.{context.RunId}";
            deniedRequest.actorPersonId = actorId;
            deniedRequest.relatedRecordId = $"settlement.allowed.{context.RunId}";
            OrganizationResourceOperationResult allowed = resources.WithdrawFunds(deniedRequest);
            long after = resources.GetBalance(ResourceOperatingAccountId(context), 10d).BalanceUnits;
            bool valid = !denied.Succeeded && denied.Code == OrganizationResourceOperationCode.Unauthorized && allowed.Succeeded && before == 100L && after == 90L;
            return TestLabAssertions.True("step13-resources-authority", "Enforce organization financial authority", valid, $"Denied={denied.Code} Allowed={allowed.Code} Balance={before}->{after}");
        }

        private static TestLabAutomationStepResult ResourceAllocations(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 1000L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-allocations", "Validate fund allocation boundaries", "ResourceFixture", "Prepared", "Failed", failure);
            string restrictionId = $"organization-restriction.testlab.{context.RunId}";
            OrganizationResourceOperationResult restriction = resources.AddFundRestriction(new OrganizationFundRestrictionRequest
            {
                transactionId = $"testlab.resources.restrict.{context.RunId}", restrictionId = restrictionId, organizationId = "organization.prototype.adventurers-guild", accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id,
                units = 700L, allowedPurpose = "healing", sourceReferenceId = $"donation.testlab.{context.RunId}", actorPersonId = actorId, startWorldTime = 10d
            });
            OrganizationResourceOperationResult blocked = resources.TransferFunds(ResourceTransferRequest(context, currency.Id, actorId, 400L, $"testlab.resources.restricted.blocked.{context.RunId}", 11d));
            OrganizationFinancialTransactionRequest allowedRequest = ResourceTransferRequest(context, currency.Id, actorId, 400L, $"testlab.resources.restricted.allowed.{context.RunId}", 12d);
            allowedRequest.restrictionId = restrictionId;
            allowedRequest.purpose = "healing";
            OrganizationResourceOperationResult allowed = resources.TransferFunds(allowedRequest);
            string reservationId = $"organization-reservation.testlab.{context.RunId}";
            OrganizationResourceOperationResult reservation = resources.ReserveResource(new OrganizationReservationRequest
            {
                transactionId = $"testlab.resources.reserve.{context.RunId}", reservationId = reservationId, organizationId = "organization.prototype.adventurers-guild", accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id,
                amountUnits = 100L, category = OrganizationReservationCategory.Contract, purpose = "contract", requestingOperationId = $"contract.testlab.{context.RunId}", actorPersonId = actorId, startWorldTime = 13d, expirationWorldTime = 30d
            });
            string budgetId = $"organization-budget.testlab.{context.RunId}";
            OrganizationResourceOperationResult budget = resources.CreateBudget(new OrganizationBudgetRequest
            {
                transactionId = $"testlab.resources.budget.{context.RunId}", budgetId = budgetId, organizationId = "organization.prototype.adventurers-guild", treasuryId = ResourceTreasuryId(context), accountId = ResourceOperatingAccountId(context),
                category = OrganizationBudgetCategory.Procurement, enforcementPolicy = OrganizationBudgetEnforcementPolicy.HardMaximum, currencyDefinitionId = currency.Id, authorizedUnits = 50L, purpose = "procurement", actorPersonId = actorId, startWorldTime = 13d
            });
            OrganizationFinancialTransactionRequest overBudgetRequest = ResourceTransferRequest(context, currency.Id, actorId, 51L, $"testlab.resources.budget.blocked.{context.RunId}", 14d);
            overBudgetRequest.budgetId = budgetId;
            overBudgetRequest.purpose = "procurement";
            OrganizationResourceOperationResult overBudget = resources.TransferFunds(overBudgetRequest);
            OrganizationAccountBalanceSnapshot balance = resources.GetBalance(ResourceOperatingAccountId(context), 14d);
            bool valid = restriction.Succeeded && blocked.Code == OrganizationResourceOperationCode.InsufficientFunds && allowed.Succeeded && reservation.Succeeded && budget.Succeeded && overBudget.Code == OrganizationResourceOperationCode.BudgetExceeded
                && balance.RestrictedUnits == 300L && balance.EncumberedUnits == 100L && balance.AvailableUnits == 200L;
            return TestLabAssertions.True("step13-resources-allocations", "Validate fund allocation boundaries", valid, $"Restriction={restriction.Code} Blocked={blocked.Code} Allowed={allowed.Code} Reservation={reservation.Code} Budget={budget.Code}/{overBudget.Code} Balance={balance?.BalanceUnits}/{balance?.AvailableUnits}/{balance?.RestrictedUnits}/{balance?.ReservedUnits}/{balance?.EncumberedUnits}");
        }

        private static TestLabAutomationStepResult ResourceInventoryCustody(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 0L, out OrganizationResourceRuntime resources, out _, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-custody", "Associate inventory and track item custody", "ResourceFixture", "Prepared", "Failed", failure);
            OrganizationResourceOperationResult inventory = resources.AssociateInventory(new OrganizationAssociationRequest
            {
                transactionId = $"testlab.resources.inventory.{context.RunId}", associationId = $"organization-inventory-association.testlab.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", resourceId = $"inventory.organization.testlab.{context.RunId}",
                category = (int)OrganizationInventoryCategory.Armory, actorPersonId = actorId, startWorldTime = 10d
            });
            ItemDefinition itemDefinition = context.ScenarioContext.Runtimes.DefinitionRegistry.DefinitionsById.Values.OfType<ItemDefinition>().FirstOrDefault();
            ItemInstanceOperationResult created = itemDefinition == null ? null : context.ScenarioContext.Runtimes.ItemInstances.CreateItem(itemDefinition, ownerPersonId: "person.prototype.friend", custodianPersonId: "person.prototype.friend", creationSourceId: context.RunId);
            string itemId = created?.Snapshot?.ItemInstanceId;
            OrganizationResourceOperationResult custody = resources.AssignCustody(new OrganizationCustodyRequest
            {
                transactionId = $"testlab.resources.custody.{context.RunId}", custodyId = $"organization-custody.testlab.{context.RunId}", organizationId = "organization.prototype.adventurers-guild",
                asset = new OrganizationAssetReferenceData { kind = OrganizationAssetReferenceKind.ItemInstance, resourceId = itemId, definitionId = itemDefinition?.Id, worldId = context.ScenarioContext.Runtimes.WorldId },
                custodianPersonId = "person.prototype.student", actorPersonId = actorId, sourceInventoryId = $"inventory.organization.testlab.{context.RunId}", destinationInventoryId = "person.prototype.student", startWorldTime = 11d
            });
            OrganizationResourceOperationResult returned = resources.ReturnCustody($"organization-custody.testlab.{context.RunId}", $"testlab.resources.custody.return.{context.RunId}", actorId, 12d);
            bool unchanged = context.ScenarioContext.Runtimes.ItemInstances.TryGetSnapshot(itemId, out ItemInstanceSnapshot item) && item.OwnerPersonId == "person.prototype.friend" && item.CustodianPersonId == "person.prototype.friend";
            bool valid = inventory.Succeeded && created?.Succeeded == true && custody.Succeeded && returned.Succeeded && unchanged;
            return TestLabAssertions.True("step13-resources-custody", "Associate inventory and track item custody", valid, $"Inventory={inventory.Code} Item={created?.Status}:{itemId} Custody={custody.Code}/{returned.Code} OwnershipUnchanged={unchanged}");
        }

        private static TestLabAutomationStepResult ResourceReservation(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 200L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-reservation", "Reserve and release authoritative funds", "ResourceFixture", "Prepared", "Failed", failure);
            string reservationId = $"organization-reservation.testlab.explicit.{context.RunId}";
            OrganizationResourceOperationResult reserve = resources.ReserveResource(new OrganizationReservationRequest
            {
                transactionId = $"testlab.resources.reservation.explicit.{context.RunId}", reservationId = reservationId, organizationId = "organization.prototype.adventurers-guild",
                accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id, amountUnits = 75L, category = OrganizationReservationCategory.Contract,
                purpose = "contract", requestingOperationId = $"contract.testlab.{context.RunId}", actorPersonId = actorId, startWorldTime = 10d, expirationWorldTime = 20d
            });
            long reserved = resources.GetBalance(ResourceOperatingAccountId(context), 10d)?.EncumberedUnits ?? -1L;
            OrganizationResourceOperationResult release = resources.ReleaseReservation(reservationId, $"testlab.resources.reservation.release.{context.RunId}", actorId, PrototypeOrganizationAuthorityDefinitionFactory.ManageResourceReservationActionId, 12d);
            long released = resources.GetBalance(ResourceOperatingAccountId(context), 12d)?.EncumberedUnits ?? -1L;
            bool valid = reserve.Succeeded && reserved == 75L && release.Succeeded && released == 0L && resources.Reservations.Single().lifecycleState == OrganizationReservationLifecycleState.Released;
            return TestLabAssertions.True("step13-resources-reservation", "Reserve and release authoritative funds", valid, $"Reserve={reserve.Code}:{reserved} Release={release.Code}:{released}");
        }

        private static TestLabAutomationStepResult ResourceJointApproval(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 500L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-joint-approval", "Authorize a joint financial action", "ResourceFixture", "Prepared", "Failed", failure);
            OrganizationAuthorityRuntime authority = context.ScenarioContext.Runtimes.OrganizationAuthority;
            OrganizationAuthorityOperationResult mentor = GrantGuildmasterRole(authority, actorId, "person.prototype.mentor", context.RunId, "resource-mentor");
            OrganizationAuthorityOperationResult partner = GrantGuildmasterRole(authority, actorId, "person.prototype.partner", context.RunId, "resource-partner");
            string operationId = $"testlab.resources.joint.transfer.{context.RunId}";
            Func<string, string, OrganizationApprovalRequest> approval = (id, approver) => new OrganizationApprovalRequest
            {
                approvalId = id, operationId = operationId, organizationId = "organization.prototype.adventurers-guild", actionDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.LargeOrganizationTransferActionId,
                approverPersonId = approver, scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"), approvedWorldTime = 10d, transactionId = $"tx.{id}"
            };
            OrganizationAuthorityOperationResult first = authority.RecordApproval(approval($"organization-approval.resources.first.{context.RunId}", "person.prototype.mentor"));
            OrganizationAuthorityOperationResult second = authority.RecordApproval(approval($"organization-approval.resources.second.{context.RunId}", "person.prototype.partner"));
            OrganizationFinancialTransactionRequest request = ResourceTransferRequest(context, currency.Id, actorId, 100L, operationId, 11d);
            request.actionDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.LargeOrganizationTransferActionId;
            request.approvalPersonIds = new[] { "person.prototype.mentor", "person.prototype.partner" };
            OrganizationResourceOperationResult transfer = resources.TransferFunds(request);
            bool consumed = authority.Approvals.Where(item => item.Data.operationId == operationId).All(item => item.LifecycleState == OrganizationApprovalLifecycleState.Consumed);
            bool valid = mentor.Succeeded && partner.Succeeded && first.Succeeded && second.Succeeded && transfer.Succeeded && consumed;
            return TestLabAssertions.True("step13-resources-joint-approval", "Authorize a joint financial action", valid, $"Grants={mentor.Status}/{partner.Status} Approvals={first.Status}/{second.Status} Transfer={transfer.Code} Consumed={consumed}");
        }

        private static TestLabAutomationStepResult ResourceRevenueRouting(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 400L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-revenue", "Route business revenue through Economy accounts", "ResourceFixture", "Prepared", "Failed", failure);
            string sourceId = $"business-revenue.testlab.{context.RunId}";
            OrganizationResourceOperationResult rule = resources.CreateRevenueRoutingRule(new OrganizationRevenueRoutingRequest
            {
                transactionId = $"testlab.resources.route.rule.{context.RunId}", routingRuleId = $"organization-routing.testlab.{context.RunId}", organizationId = "organization.prototype.adventurers-guild",
                revenueSourceId = sourceId, destinationAccountId = ResourceReserveAccountId(context), percentageBasisPoints = 2500L, priority = 10,
                purpose = "institutional reserve", actorPersonId = actorId, startWorldTime = 10d
            });
            OrganizationResourceOperationResult routed = resources.ApplyRevenueRouting(new OrganizationRevenueRoutingExecutionRequest
            {
                transactionId = $"testlab.resources.route.execute.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", revenueSourceId = sourceId,
                sourceAccountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id, grossUnits = 200L, actorPersonId = actorId, worldTime = 11d
            });
            bool valid = rule.Succeeded && routed.Succeeded && resources.GetBalance(ResourceOperatingAccountId(context), 11d)?.BalanceUnits == 350L && resources.GetBalance(ResourceReserveAccountId(context), 11d)?.BalanceUnits == 50L;
            return TestLabAssertions.True("step13-resources-revenue", "Route business revenue through Economy accounts", valid, $"Rule={rule.Code} Route={routed.Code} Balances={resources.GetBalance(ResourceOperatingAccountId(context), 11d)?.BalanceUnits}/{resources.GetBalance(ResourceReserveAccountId(context), 11d)?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult ResourcePayrollFunding(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 300L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-payroll", "Expose payroll funding without duplicating payroll state", "ResourceFixture", "Prepared", "Failed", failure);
            OrganizationResourceOperationResult reservation = resources.ReserveResource(new OrganizationReservationRequest
            {
                transactionId = $"testlab.resources.payroll.reserve.{context.RunId}", reservationId = $"organization-reservation.payroll.{context.RunId}", organizationId = "organization.prototype.adventurers-guild",
                accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id, amountUnits = 80L, category = OrganizationReservationCategory.Payroll,
                purpose = "payroll", requestingOperationId = $"payroll-run.testlab.{context.RunId}", actorPersonId = actorId, startWorldTime = 10d
            });
            OrganizationAccountBalanceSnapshot balance = resources.GetBalance(ResourceOperatingAccountId(context), 10d);
            bool valid = context.ScenarioContext.Runtimes.Payroll != null && reservation.Succeeded && balance.ReservedUnits == 80L && resources.QueryLiabilities("organization.prototype.adventurers-guild").Count == 0;
            return TestLabAssertions.True("step13-resources-payroll", "Expose payroll funding without duplicating payroll state", valid, $"PayrollReady={context.ScenarioContext.Runtimes.Payroll != null} Reservation={reservation.Code} Reserved={balance?.ReservedUnits} DelegatedLiabilities={resources.QueryLiabilities("organization.prototype.adventurers-guild").Count}");
        }

        private static TestLabAutomationStepResult ResourceBranchFinances(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 150L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-branch", "Query branch and parent finances", "ResourceFixture", "Prepared", "Failed", failure);
            OrganizationConsolidatedResourceSnapshot view = resources.GetConsolidatedView("organization.prototype.adventurers-guild", 10d);
            bool separate = view.AccountBalances.Select(item => item.Account.accountId).Distinct(StringComparer.Ordinal).Count() == view.AccountBalances.Count;
            bool valid = separate && view.OrganizationIds.Contains("organization.prototype.adventurers-guild") && view.Total(currency.Id) == 150L && resources.QueryAccounts("organization.prototype.adventurers-guild").Count == 2;
            return TestLabAssertions.True("step13-resources-branch", "Query branch and parent finances", valid, $"Organizations=[{string.Join(",", view.OrganizationIds)}] Accounts={view.AccountBalances.Count} Separate={separate} Total={view.Total(currency.Id)} Actor={actorId}");
        }

        private static TestLabAutomationStepResult ResourceDissolutionBoundary(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 100L, out OrganizationResourceRuntime resources, out _, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-dissolution", "Execute explicit dissolution resource plan", "ResourceFixture", "Prepared", "Failed", failure);
            string planId = $"organization-dissolution-plan.testlab.{context.RunId}";
            OrganizationResourceOperationResult create = resources.CreateDissolutionResourcePlan(new OrganizationDissolutionResourcePlanRequest
            {
                transactionId = $"testlab.resources.dissolution.create.{context.RunId}", planId = planId, organizationId = "organization.prototype.adventurers-guild",
                accountIdsToFreeze = new[] { ResourceOperatingAccountId(context), ResourceReserveAccountId(context) }, preservedObligationIds = new[] { $"obligation.unresolved.{context.RunId}" }, actorPersonId = actorId, worldTime = 20d
            });
            OrganizationResourceOperationResult execute = resources.ExecuteDissolutionResourcePlan(planId, $"testlab.resources.dissolution.execute.{context.RunId}", actorId, Array.Empty<string>(), 21d);
            bool valid = create.Succeeded && execute.Succeeded && resources.Accounts.All(item => item.lifecycleState == OrganizationAccountLifecycleState.Frozen)
                && resources.DissolutionPlans.Single().assetInstructions.Length == 0 && resources.DissolutionPlans.Single().preservedObligationIds.Length == 1;
            return TestLabAssertions.True("step13-resources-dissolution", "Execute explicit dissolution resource plan", valid, $"Create={create.Code} Execute={execute.Code} States=[{string.Join(",", resources.Accounts.Select(item => item.lifecycleState))}] Preserved={resources.DissolutionPlans.SingleOrDefault()?.preservedObligationIds.Length}");
        }

        private static TestLabAutomationStepResult ResourcePropertyBusinessAssociations(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 0L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-assets", "Associate Step 11 property and business records", "ResourceFixture", "Prepared", "Failed", failure);
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PropertyDefinition propertyDefinition = ScriptableObject.CreateInstance<PropertyDefinition>();
            propertyDefinition.Initialize($"property.testlab.organization-definition.{context.RunId}", "Test Lab Organization Property", PropertyCategory.CommercialBuilding);
            propertyDefinition.SetPolicies(Array.Empty<PropertyCategory>(), new[] { PropertyOwnershipModel.Sole, PropertyOwnershipModel.SharedFractional }, new[] { PropertyUseCategory.Commercial }, currency.Id);
            BusinessDefinition businessDefinition = ScriptableObject.CreateInstance<BusinessDefinition>();
            businessDefinition.Initialize($"business.testlab.organization-definition.{context.RunId}", "Test Lab Organization Business", BusinessCategory.MerchantShop);
            DefinitionRegistry extendedRegistry = new DefinitionRegistry(runtimes.DefinitionRegistry.DefinitionsById.Values.Concat(new IGameDefinition[] { propertyDefinition, businessDefinition }));
            runtimes.Properties.Configure(extendedRegistry, runtimes.WorldId);
            runtimes.Businesses.Configure(extendedRegistry, runtimes.WorldId);
            runtimes.Economy.Configure(extendedRegistry, runtimes.WorldId);
            resources.Configure(extendedRegistry, runtimes.Organizations, runtimes.OrganizationAuthority, runtimes.Economy, runtimes.WorldId, runtimes.Properties, runtimes.Businesses, runtimes.ItemInstances);
            string propertyId = $"property.testlab.organization.{context.RunId}";
            PropertyOperationResult property = runtimes.Properties.RegisterProperty(new PropertyInstanceData { propertyId = propertyId, propertyDefinitionId = propertyDefinition.Id, displayName = "Test Lab Guild Property", spatialReferenceId = $"place.testlab.{context.RunId}", currentUses = new[] { PropertyUseCategory.Commercial }, creationWorldTime = 1d });
            string propertyOwnershipId = $"property-ownership.testlab.organization.{context.RunId}";
            PropertyOperationResult propertyOwner = property?.Succeeded == true ? runtimes.Properties.CreateOwnership(new PropertyOwnershipInterestData
            {
                ownershipInterestId = propertyOwnershipId, propertyId = propertyId, owner = new PropertySubjectReferenceData { kind = PropertySubjectKind.Organization, subjectId = "organization.prototype.adventurers-guild" }, ownershipModel = PropertyOwnershipModel.Sole,
                ownershipShare = PropertyShareData.Full(), votingShare = PropertyShareData.Full(), economicBenefitShare = PropertyShareData.Full(), effectiveStartWorldTime = 1d
            }, 1d) : null;
            OrganizationResourceOperationResult propertyAssociation = resources.AssociateProperty(new OrganizationAssociationRequest
            {
                transactionId = $"testlab.resources.property.{context.RunId}", associationId = $"organization-property-association.testlab.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", resourceId = propertyId,
                sourceRecordId = propertyOwnershipId, category = (int)OrganizationPropertyAssociationCategory.Owner, actorPersonId = actorId, startWorldTime = 10d
            });

            string businessId = $"business.testlab.organization.{context.RunId}";
            BusinessOperationResult business = runtimes.Businesses.CreateBusiness(new BusinessInstanceData
            {
                businessId = businessId, businessDefinitionId = businessDefinition.Id, displayName = "Test Lab Guild Business", linkedOrganizationId = "organization.prototype.adventurers-guild", founderSubjectIds = new[] { actorId }, operatingCurrencyIds = new[] { currency.Id }, state = BusinessState.Active, createdWorldTime = 1d
            });
            string businessOwnershipId = $"business-ownership.testlab.organization.{context.RunId}";
            BusinessOperationResult businessOwner = business?.Succeeded == true ? runtimes.Businesses.AddOwnership(new BusinessOwnershipRecordData
            {
                ownershipRecordId = businessOwnershipId, businessId = businessId, owner = new BusinessSubjectReferenceData { kind = BusinessOwnerSubjectKind.Organization, subjectId = "organization.prototype.adventurers-guild" }, category = BusinessOwnershipCategory.SoleOwner,
                economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L }, votingShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L }, effectiveStartWorldTime = 1d
            }, 1d) : null;
            OrganizationResourceOperationResult businessAssociation = resources.AssociateBusiness(new OrganizationAssociationRequest
            {
                transactionId = $"testlab.resources.business.{context.RunId}", associationId = $"organization-business-association.testlab.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", resourceId = businessId,
                sourceRecordId = businessOwnershipId, category = (int)OrganizationBusinessAssociationCategory.Owner, shareBasisPoints = 10000L, actorPersonId = actorId, startWorldTime = 10d
            });
            OrganizationResourceOperationResult fabricated = resources.AssociateProperty(new OrganizationAssociationRequest
            {
                transactionId = $"testlab.resources.property.fabricated.{context.RunId}", associationId = $"organization-property-association.testlab.fabricated.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", resourceId = propertyId,
                sourceRecordId = "property-ownership.missing", category = (int)OrganizationPropertyAssociationCategory.Owner, actorPersonId = actorId, startWorldTime = 10d
            });
            bool valid = property?.Succeeded == true && propertyOwner?.Succeeded == true && propertyAssociation.Succeeded && business?.Succeeded == true && businessOwner?.Succeeded == true && businessAssociation.Succeeded && !fabricated.Succeeded;
            return TestLabAssertions.True("step13-resources-assets", "Associate Step 11 property and business records", valid, $"Property={property?.Code}/{propertyOwner?.Code}/{propertyAssociation.Code} Business={business?.Code}/{businessOwner?.Code}/{businessAssociation.Code} Fabricated={fabricated.Code}");
        }

        private static TestLabAutomationStepResult ResourceLifecycleBoundaries(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 100L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-lifecycle", "Freeze and reactivate an organization account", "ResourceFixture", "Prepared", "Failed", failure);
            OrganizationAccountLifecycleRequest request = new OrganizationAccountLifecycleRequest { transactionId = $"testlab.resources.freeze.{context.RunId}", accountId = ResourceOperatingAccountId(context), targetState = OrganizationAccountLifecycleState.Frozen, actorPersonId = actorId, worldTime = 10d };
            OrganizationResourceOperationResult frozen = resources.ChangeAccountLifecycle(request);
            OrganizationResourceOperationResult blocked = resources.TransferFunds(ResourceTransferRequest(context, currency.Id, actorId, 10L, $"testlab.resources.freeze.blocked.{context.RunId}", 11d));
            request.transactionId = $"testlab.resources.reactivate.{context.RunId}";
            request.targetState = OrganizationAccountLifecycleState.Active;
            request.worldTime = 12d;
            OrganizationResourceOperationResult reactivated = resources.ChangeAccountLifecycle(request);
            bool economyActive = context.ScenarioContext.Runtimes.Economy.TryGetAccount(ResourceOperatingEconomyAccountId(context), out EconomyAccountSnapshot account) && account.Data.state == EconomyAccountState.Active;
            bool valid = frozen.Succeeded && blocked.Code == OrganizationResourceOperationCode.AccountFrozen && reactivated.Succeeded && economyActive && resources.GetBalance(ResourceOperatingAccountId(context), 12d).BalanceUnits == 100L;
            return TestLabAssertions.True("step13-resources-lifecycle", "Freeze and reactivate an organization account", valid, $"Frozen={frozen.Code} Blocked={blocked.Code} Reactivated={reactivated.Code} EconomyActive={economyActive}");
        }

        private static TestLabAutomationStepResult ResourceReconciliationProjection(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 250L, out OrganizationResourceRuntime resources, out _, out _, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-reconcile", "Reconcile and project resource state", "ResourceFixture", "Prepared", "Failed", failure);
            long revision = resources.Revision;
            OrganizationReconciliationResult first = resources.Reconcile("organization.prototype.adventurers-guild", 10d);
            OrganizationReconciliationResult second = resources.Reconcile("organization.prototype.adventurers-guild", 10d);
            OrganizationResourceProjection redacted = resources.ProjectAccount(ResourceOperatingAccountId(context), OrganizationResourceProjectionAccess.Redacted, 10d);
            OrganizationResourceProjection full = resources.ProjectAccount(ResourceOperatingAccountId(context), OrganizationResourceProjectionAccess.Full, 10d);
            bool valid = first.IsReconciled && second.IsReconciled && first.Discrepancies.Count == second.Discrepancies.Count && redacted.Redacted && redacted.Balance.BalanceUnits == 0L && full.Balance.BalanceUnits == 250L && resources.Revision == revision;
            return TestLabAssertions.True("step13-resources-reconcile", "Reconcile and project resource state", valid, $"Reconciled={first.IsReconciled}/{second.IsReconciled} Diagnostics={first.Discrepancies.Count}/{second.Discrepancies.Count} Redacted={redacted.Access}:{redacted.Balance?.BalanceUnits} Full={full.Access}:{full.Balance?.BalanceUnits} Revision={revision}->{resources.Revision}");
        }

        private static TestLabAutomationStepResult ResourcePersistenceValidation(TestLabAutomationContext context)
        {
            bool prepared = PrepareResourceAccounts(context, 300L, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure);
            if (!prepared) return TestLabAssertions.Fail("step13-resources-persistence", "Save, restore, and reject resource graph drift", "ResourceFixture", "Prepared", "Failed", failure);
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            resources.AddFundRestriction(new OrganizationFundRestrictionRequest { transactionId = $"testlab.resources.persist.restriction.{context.RunId}", restrictionId = $"restriction.persist.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id, units = 25L, allowedPurpose = "preserved", actorPersonId = actorId, startWorldTime = 5d });
            resources.CreateBudget(new OrganizationBudgetRequest { transactionId = $"testlab.resources.persist.budget.{context.RunId}", budgetId = $"budget.persist.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", treasuryId = ResourceTreasuryId(context), accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id, authorizedUnits = 50L, purpose = "preserved", actorPersonId = actorId, startWorldTime = 5d });
            resources.ReserveResource(new OrganizationReservationRequest { transactionId = $"testlab.resources.persist.reservation.{context.RunId}", reservationId = $"reservation.persist.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", accountId = ResourceOperatingAccountId(context), currencyDefinitionId = currency.Id, amountUnits = 20L, category = OrganizationReservationCategory.Contract, actorPersonId = actorId, startWorldTime = 5d });
            resources.CreateRevenueRoutingRule(new OrganizationRevenueRoutingRequest { transactionId = $"testlab.resources.persist.routing.{context.RunId}", routingRuleId = $"routing.persist.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", revenueSourceId = $"revenue.persist.{context.RunId}", destinationAccountId = ResourceReserveAccountId(context), fixedUnits = 1L, actorPersonId = actorId, startWorldTime = 5d });
            resources.CreateDissolutionResourcePlan(new OrganizationDissolutionResourcePlanRequest { transactionId = $"testlab.resources.persist.dissolution.{context.RunId}", planId = $"dissolution.persist.{context.RunId}", organizationId = "organization.prototype.adventurers-guild", accountIdsToFreeze = new[] { ResourceOperatingAccountId(context) }, actorPersonId = actorId, worldTime = 5d });
            OrganizationResourceRuntimeSaveData save = resources.CreateSaveData();
            long economyRevision = runtimes.Economy.Revision;
            OrganizationResourceRuntime restored = new OrganizationResourceRuntime();
            OrganizationResourceOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Organizations, runtimes.OrganizationAuthority, runtimes.Economy, runtimes.WorldId, runtimes.Properties, runtimes.Businesses, runtimes.ItemInstances, restoring: true, contractRuntime: runtimes.Contracts, payrollRuntime: runtimes.Payroll);
            OrganizationResourceRuntimeSaveData corrupt = save.Clone();
            corrupt.accounts[0].economyAccountId = "economy-account.missing";
            bool rejected = !OrganizationResourceRuntime.ValidateSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Organizations, runtimes.Economy, runtimes.WorldId, runtimes.Properties, runtimes.Businesses, runtimes.ItemInstances, out string validationFailure);
            bool allRecords = restored.RestrictionCount == 1 && restored.BudgetCount == 1 && restored.ReservationCount == 1 && restored.RevenueRoutingRules.Count == 1 && restored.DissolutionPlans.Count == 1;
            bool valid = restore.Succeeded && restored.AccountCount == 2 && allRecords && restored.GetBalance(ResourceOperatingAccountId(context), 10d)?.BalanceUnits == 300L && runtimes.Economy.Revision == economyRevision && rejected && resources.AccountCount == 2;
            return TestLabAssertions.True("step13-resources-persistence", "Save, restore, and reject resource graph drift", valid, $"Restore={restore.Code} Accounts={resources.AccountCount}/{restored.AccountCount} Records={restored.RestrictionCount}/{restored.BudgetCount}/{restored.ReservationCount}/{restored.RevenueRoutingRules.Count}/{restored.DissolutionPlans.Count} Balance={restored.GetBalance(ResourceOperatingAccountId(context), 10d)?.BalanceUnits} EconomyRevision={economyRevision}->{runtimes.Economy.Revision} Rejected={rejected}:{validationFailure}");
        }

        private static TestLabAutomationStepResult DecisionRuntimeReadiness(TestLabAutomationContext context)
        {
            OrganizationDecisionRuntime decisions = context?.ScenarioContext?.Runtimes?.OrganizationDecisions;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            bool definitions = registry != null
                && registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId, out OrganizationGoalDefinition goal)
                && registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.ConfidentialityPolicyId, out OrganizationPolicyDefinition policy)
                && registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.SimpleMajorityProcedureId, out OrganizationDecisionProcedureDefinition procedure)
                && registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.EstablishGoalProposalId, out OrganizationProposalDefinition proposal)
                && goal.ProgressSourceKind == OrganizationGoalProgressSourceKind.ActiveMembershipCount
                && policy.ParameterSchema.Count >= 2
                && procedure.VoterEligibility == OrganizationVoterEligibilityKind.ActiveMembers
                && proposal.SupportedExecutionOperations.Contains(OrganizationDecisionExecutionOperationKind.EstablishGoal);
            bool valid = decisions?.IsReady == true && definitions;
            return TestLabAssertions.True("step13-decisions-readiness", "Validate organization decision definitions", valid, $"Ready={decisions?.IsReady} Definitions={definitions} Counts={decisions?.GoalCount}/{decisions?.PolicyCount}/{decisions?.ProposalCount}");
        }

        private static TestLabAutomationStepResult DecisionGoalsPolicies(TestLabAutomationContext context)
        {
            if (!PrepareDecisionFixture(context, 100L, out OrganizationDecisionRuntime decisions, out _, out _, out string actorId, out string failure))
            {
                return TestLabAssertions.Fail("step13-decisions-goals-policies", "Create goals and resolve policies", "DecisionFixture", "Ready", "Missing", failure);
            }

            OrganizationDecisionOperationResult goal = decisions.CreateGoal(new OrganizationGoalRequest
            {
                transactionId = $"testlab.decisions.goal.{context.RunId}",
                goalId = $"organization-goal-record.testlab.recruit.{context.RunId}",
                organizationId = "organization.prototype.adventurers-guild",
                goalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
                targetValue = 3L,
                priority = 25,
                actorPersonId = actorId,
                worldTime = 10d
            });
            OrganizationDecisionOperationResult policy = decisions.CreatePolicy(PolicyRequest(context, $"organization-policy-record.testlab.confidentiality.{context.RunId}", actorId, 12d));
            OrganizationDecisionOperationResult conflict = decisions.CreatePolicy(PolicyRequest(context, $"organization-policy-record.testlab.confidentiality.conflict.{context.RunId}", actorId, 13d));
            OrganizationPolicyRequest replacementRequest = PolicyRequest(context, $"organization-policy-record.testlab.confidentiality.replacement.{context.RunId}", actorId, 14d);
            replacementRequest.supersedesPolicyId = policy.Policy?.policyId;
            replacementRequest.parameters = new[] { PolicyParam("visibility", OrganizationPolicyParameterType.EnumValue, stringValue: OrganizationVisibility.Secret.ToString()), PolicyParam("reshare_allowed", OrganizationPolicyParameterType.Boolean, boolValue: false) };
            OrganizationDecisionOperationResult replacement = decisions.CreatePolicy(replacementRequest);
            OrganizationPolicyResolutionResult resolved = decisions.ResolvePolicies(new OrganizationPolicyQuery
            {
                organizationId = "organization.prototype.adventurers-guild",
                policyDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.ConfidentialityPolicyId,
                scope = OrganizationPolicyScopeData.EntireOrganization("organization.prototype.adventurers-guild"),
                worldTime = 15d
            });
            OrganizationPolicyRecordData supersededPolicy = decisions.Policies.FirstOrDefault(item => item.policyId == policy.Policy?.policyId);
            bool valid = goal.Succeeded
                && goal.Goal?.lifecycleState == OrganizationGoalLifecycleState.Completed
                && policy.Succeeded
                && conflict.Code == OrganizationDecisionOperationCode.InvalidConflict
                && replacement.Succeeded
                && supersededPolicy?.lifecycleState == OrganizationPolicyLifecycleState.Superseded
                && supersededPolicy?.supersededByPolicyId == replacement.Policy?.policyId
                && resolved.EffectivePolicy?.policyId == replacement.Policy?.policyId
                && resolved.SuppressedPolicies.Count == 0;
            return TestLabAssertions.True("step13-decisions-goals-policies", "Create goals and resolve policies", valid, $"Goal={goal.Code}:{goal.Goal?.currentValue}/{goal.Goal?.targetValue}:{goal.Goal?.lifecycleState} Policy={policy.Code} Conflict={conflict.Code} Replacement={replacement.Code} Superseded={supersededPolicy?.lifecycleState}:{supersededPolicy?.supersededByPolicyId} Effective={resolved.EffectivePolicy?.policyId} Suppressed={resolved.SuppressedPolicies.Count}");
        }

        private static TestLabAutomationStepResult DecisionProposalVoteResolution(TestLabAutomationContext context)
        {
            if (!PrepareDecisionFixture(context, 100L, out OrganizationDecisionRuntime decisions, out _, out _, out string actorId, out string failure))
            {
                return TestLabAssertions.Fail("step13-decisions-proposal", "Submit, amend, vote, and close proposal", "DecisionFixture", "Ready", "Missing", failure);
            }

            string proposalId = $"organization-proposal.testlab.goal.{context.RunId}";
            OrganizationDecisionOperationResult submit = decisions.SubmitProposal(new OrganizationProposalRequest
            {
                transactionId = $"testlab.decisions.proposal.submit.{context.RunId}",
                proposalId = proposalId,
                organizationId = "organization.prototype.adventurers-guild",
                proposalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.EstablishGoalProposalId,
                title = "Create recruitment goal",
                proposerPersonId = actorId,
                requestedExecutionOperations = new[] { GoalOperation(context, "initial", $"organization-goal-record.testlab.execution.initial.{context.RunId}", 5L) },
                submittedWorldTime = 10d,
                votingStartWorldTime = 10d,
                votingEndWorldTime = 20d
            });
            OrganizationDecisionOperationResult amend = decisions.SubmitAmendment(new OrganizationAmendmentRequest
            {
                transactionId = $"testlab.decisions.proposal.amend.{context.RunId}",
                amendmentId = $"organization-amendment.testlab.goal.{context.RunId}",
                proposalId = proposalId,
                proposerPersonId = "person.prototype.friend",
                summary = "Reduce target to current membership.",
                replacementExecutionOperations = new[] { GoalOperation(context, "amended", $"organization-goal-record.testlab.execution.amended.{context.RunId}", 3L) },
                worldTime = 11d
            });
            OrganizationDecisionOperationResult voteOne = decisions.CastVote(VoteRequest(context, proposalId, actorId, "actor", OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult voteTwo = decisions.CastVote(VoteRequest(context, proposalId, "person.prototype.friend", "friend", OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult voteReplacement = decisions.CastVote(VoteRequest(context, proposalId, "person.prototype.friend", "friend-replace", OrganizationVoteChoice.Reject));
            OrganizationDecisionTallySnapshot tally = decisions.TallyProposal(proposalId);
            OrganizationDecisionOperationResult close = decisions.CloseVote(new OrganizationCloseVoteRequest
            {
                transactionId = $"testlab.decisions.proposal.close.{context.RunId}",
                proposalId = proposalId,
                resolutionId = $"organization-resolution.testlab.goal.{context.RunId}",
                actorPersonId = actorId,
                worldTime = 21d
            });
            bool valid = submit.Succeeded
                && amend.Succeeded
                && voteOne.Succeeded
                && voteTwo.Succeeded
                && voteReplacement.Succeeded
                && tally.ParticipatingCount == 2
                && tally.ApproveWeight == 1L
                && tally.RejectWeight == 1L
                && close.Succeeded
                && close.Resolution?.outcome == OrganizationResolutionOutcome.Tied;
            return TestLabAssertions.True("step13-decisions-proposal", "Submit, amend, vote, and close proposal", valid, $"Submit={submit.Code} Amend={amend.Code} Votes={voteOne.Code}/{voteTwo.Code}/{voteReplacement.Code} Tally={tally.ApproveWeight}/{tally.RejectWeight}/{tally.ParticipatingCount} Close={close.Code}:{close.Resolution?.outcome}");
        }

        private static TestLabAutomationStepResult DecisionExecutionPersistenceProjection(TestLabAutomationContext context)
        {
            if (!PrepareDecisionFixture(context, 250L, out OrganizationDecisionRuntime decisions, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure))
            {
                return TestLabAssertions.Fail("step13-decisions-execution", "Execute resolution and validate persistence", "DecisionFixture", "Ready", "Missing", failure);
            }

            string proposalId = $"organization-proposal.testlab.execute.{context.RunId}";
            string resolutionId = $"organization-resolution.testlab.execute.{context.RunId}";
            OrganizationDecisionOperationResult submit = decisions.SubmitProposal(new OrganizationProposalRequest
            {
                transactionId = $"testlab.decisions.execute.submit.{context.RunId}",
                proposalId = proposalId,
                organizationId = "organization.prototype.adventurers-guild",
                proposalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.ApproveBudgetProposalId,
                title = "Approve training budget",
                proposerPersonId = actorId,
                requestedExecutionOperations = new[] { BudgetOperation(context, currency.Id, 40L) },
                submittedWorldTime = 10d,
                votingStartWorldTime = 10d,
                votingEndWorldTime = 20d
            });
            OrganizationDecisionOperationResult voteActor = decisions.CastVote(VoteRequest(context, proposalId, actorId, "execute-actor", OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult voteFriend = decisions.CastVote(VoteRequest(context, proposalId, "person.prototype.friend", "execute-friend", OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult close = decisions.CloseVote(new OrganizationCloseVoteRequest { transactionId = $"testlab.decisions.execute.close.{context.RunId}", proposalId = proposalId, resolutionId = resolutionId, actorPersonId = actorId, worldTime = 21d });
            OrganizationDecisionOperationResult preview = decisions.ExecuteResolution(new OrganizationDecisionExecutionRequest { transactionId = $"testlab.decisions.execute.preview.{context.RunId}", executionId = $"organization-execution.testlab.preview.{context.RunId}", resolutionId = resolutionId, actorPersonId = actorId, worldTime = 22d, preview = true });
            bool previewNoMutation = resources.BudgetCount == 0;
            OrganizationDecisionOperationResult execute = decisions.ExecuteResolution(new OrganizationDecisionExecutionRequest { transactionId = $"testlab.decisions.execute.apply.{context.RunId}", executionId = $"organization-execution.testlab.apply.{context.RunId}", resolutionId = resolutionId, actorPersonId = actorId, worldTime = 23d });
            OrganizationDecisionProjection redacted = decisions.GetProposalProjection(proposalId, OrganizationDecisionProjectionAccess.Redacted);
            OrganizationDecisionProjection denied = decisions.GetProposalProjection(proposalId, OrganizationDecisionProjectionAccess.Denied);
            OrganizationDecisionRuntimeSaveData save = decisions.CreateSaveData();
            OrganizationDecisionRuntime restored = new OrganizationDecisionRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Economy);
            OrganizationDecisionOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds);
            OrganizationDecisionRuntimeSaveData corrupt = save.Clone();
            if (corrupt.proposals.Count > 0) corrupt.proposals[0].organizationId = "organization.missing";
            bool rejected = !OrganizationDecisionRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);
            OrganizationDecisionPersistenceParticipant participant = new OrganizationDecisionPersistenceParticipant(decisions, () => context.ScenarioContext.Runtimes.DefinitionRegistry, () => context.ScenarioContext.Runtimes.Organizations, () => context.ScenarioContext.Runtimes.OrganizationMemberships, () => context.ScenarioContext.Runtimes.OrganizationAuthority, () => context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.WorldId, () => context.ScenarioContext.Runtimes.KnownPersonIds.ToArray());
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(save), OrganizationDecisionPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = submit.Succeeded
                && voteActor.Succeeded
                && voteFriend.Succeeded
                && close.Succeeded
                && close.Resolution?.outcome == OrganizationResolutionOutcome.Adopted
                && preview.Succeeded
                && previewNoMutation
                && execute.Succeeded
                && resources.BudgetCount == 1
                && redacted.Succeeded
                && redacted.Redacted
                && !denied.Succeeded
                && restore.Succeeded
                && restored.ProposalCount == decisions.ProposalCount
                && corrupt.proposals.Count > 0
                && rejected
                && prepared.Succeeded;
            return TestLabAssertions.True("step13-decisions-execution", "Execute resolution and validate persistence", valid, $"Submit={submit.Code} Votes={voteActor.Code}/{voteFriend.Code} Close={close.Code}:{close.Resolution?.outcome} Preview={preview.Code}/{previewNoMutation} Execute={execute.Code} Budgets={resources.BudgetCount} Projection={redacted.Access}/{denied.Access} Restore={restore.Code} Rejected={rejected}:{validationFailure} Prepare={prepared.Succeeded}");
        }

        private static TestLabAutomationStepResult FactionRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetFactionRuntime(context, out FactionRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-factions-readiness", "Validate faction definitions and runtime ownership", "FactionRuntime", "Present", "Missing", failure);
            }

            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool reform = registry.TryGet(PrototypeFactionDefinitionFactory.ReformFactionId, out FactionDefinition reformDefinition);
            bool secret = registry.TryGet(PrototypeFactionDefinitionFactory.SecretMemberAffiliationId, out FactionAffiliationDefinition secretAffiliation);
            bool role = registry.TryGet(PrototypeFactionDefinitionFactory.SeniorLeaderRoleId, out FactionRoleDefinition leaderRole);
            bool position = registry.TryGet(PrototypeFactionDefinitionFactory.ProposalPositionId, out FactionPositionDefinition proposalPosition);
            bool axis = registry.TryGet(PrototypeFactionDefinitionFactory.ReformTraditionAxisId, out FactionAlignmentAxisDefinition axisDefinition);
            bool valid = runtime.IsReady
                && reform
                && secret
                && role
                && position
                && axis
                && reformDefinition.OrganizationMembershipRequired
                && FactionModelUtility.IsSecret(secretAffiliation.Visibility)
                && leaderRole.LeadershipRole
                && proposalPosition.TargetKind == FactionPositionTargetKind.OrganizationProposal
                && axisDefinition.MinimumValue < axisDefinition.MaximumValue;
            return TestLabAssertions.True("step13-factions-readiness", "Validate faction definitions and runtime ownership", valid, $"Ready={runtime.IsReady} Definitions={reform}/{secret}/{role}/{position}/{axis} Counts={runtime.FactionCount}/{runtime.AffiliationCount}");
        }

        private static TestLabAutomationStepResult FactionIdentityLifecycleHosts(TestLabAutomationContext context)
        {
            if (!TryGetFactionRuntime(context, out FactionRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-factions-identity", "Create hosted and independent faction records", "FactionRuntime", "Present", "Missing", failure);
            }

            long before = runtime.Revision;
            FactionOperationResult preview = runtime.CreateFaction(FactionCreate(context, "preview", PrototypeFactionDefinitionFactory.ReformFactionId, "Preview Reformists", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild"), preview: true));
            FactionOperationResult create = runtime.CreateFaction(FactionCreate(context, "identity", PrototypeFactionDefinitionFactory.ReformFactionId, "Guild Reform Bloc", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult duplicate = runtime.CreateFaction(FactionCreate(context, "identity", PrototypeFactionDefinitionFactory.ReformFactionId, "Guild Reform Bloc", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult independent = runtime.CreateFaction(FactionCreate(context, "independent", PrototypeFactionDefinitionFactory.IndependentMovementFactionId, "Free Company Voice", FactionHostContextData.Independent()));
            FactionOperationResult rename = runtime.RenameFaction(Tx(context, "faction-rename"), create.Faction?.factionId, $"faction-name.testlab.rename.{context.RunId}", "Guild Reform Caucus", FactionNameCategory.Public, 2d);
            FactionOperationResult transition = runtime.TransitionFaction(new FactionLifecycleRequest { transactionId = Tx(context, "faction-dormant"), factionId = independent.Faction?.factionId, targetState = FactionLifecycleState.Dormant, worldTime = 3d });
            bool noOrganizationMutation = context.ScenarioContext.Runtimes.Organizations.Count == PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds.Length;
            bool valid = preview.Succeeded
                && preview.Preview
                && create.Succeeded
                && duplicate.Code == FactionOperationCode.Duplicate
                && independent.Succeeded
                && rename.Succeeded
                && transition.Succeeded
                && runtime.Revision > before
                && noOrganizationMutation;
            return TestLabAssertions.True("step13-factions-identity", "Create hosted and independent faction records", valid, $"Preview={preview.Code} Create={create.Code} Duplicate={duplicate.Code} Independent={independent.Code} Rename={rename.Code} Transition={transition.Code} OrgStable={noOrganizationMutation}");
        }

        private static TestLabAutomationStepResult FactionAffiliationsAndRoles(TestLabAutomationContext context)
        {
            if (!TryGetFactionRuntime(context, out FactionRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-factions-affiliations", "Apply affiliations and assign roles through eligibility rules", "FactionRuntime", "Present", "Missing", failure);
            }

            string actorId = PrimaryAuthorityActorId(context);
            string factionId = $"faction.testlab.affiliation.{context.RunId}";
            FactionOperationResult faction = runtime.CreateFaction(FactionCreate(context, "affiliation", PrototypeFactionDefinitionFactory.ReformFactionId, "Affiliation Reformists", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult denied = runtime.ApplyAffiliation(FactionAffiliation(context, "denied", factionId, "person.prototype.friend", PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            OrganizationMembershipOperationResult membership = context.ScenarioContext.Runtimes.OrganizationMemberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.faction.actor.{context.RunId}", "organization.prototype.adventurers-guild", actorId, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, Tx(context, "faction-org-member"), consent: true));
            FactionEligibilityResult eligibility = runtime.EvaluateAffiliationEligibility(FactionAffiliation(context, "eligible", factionId, actorId, PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            FactionOperationResult affiliation = runtime.ApplyAffiliation(FactionAffiliation(context, "actor", factionId, actorId, PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            FactionOperationResult role = runtime.AssignRole(new FactionRoleAssignmentRequest { transactionId = Tx(context, "faction-role"), roleAssignmentId = $"faction-role-assignment.testlab.organizer.{context.RunId}", affiliationId = affiliation.Affiliation?.affiliationId, roleDefinitionId = PrototypeFactionDefinitionFactory.OrganizerRoleId, worldTime = 2d });
            FactionOperationResult supporter = runtime.ApplyAffiliation(FactionAffiliation(context, "supporter", factionId, "person.prototype.friend", PrototypeFactionDefinitionFactory.SupporterAffiliationId, consent: false));
            bool membershipNotGranted = !context.ScenarioContext.Runtimes.OrganizationMemberships.Memberships.Any(item => item.PersonId == "person.prototype.friend" && item.OrganizationId == "organization.prototype.adventurers-guild" && item.IsActive);
            bool valid = faction.Succeeded
                && !denied.Succeeded
                && membership.Succeeded
                && eligibility.Eligible
                && affiliation.Succeeded
                && role.Succeeded
                && supporter.Succeeded
                && membershipNotGranted;
            return TestLabAssertions.True("step13-factions-affiliations", "Apply affiliations and assign roles through eligibility rules", valid, $"Faction={faction.Code} Denied={denied.Code} Membership={membership.Status} Eligibility={eligibility.Eligible}:{eligibility.RequiresConsent} Affiliation={affiliation.Code} Role={role.Code} Supporter={supporter.Code} NoOrgGrant={membershipNotGranted}");
        }

        private static TestLabAutomationStepResult FactionPositionsRecommendationsCohesion(TestLabAutomationContext context)
        {
            if (!TryGetFactionRuntime(context, out FactionRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-factions-cohesion", "Set platform positions and measure vote cohesion", "FactionRuntime", "Present", "Missing", failure);
            }

            if (!PrepareDecisionFixture(context, 100L, out OrganizationDecisionRuntime decisions, out _, out _, out string actorId, out failure))
            {
                return TestLabAssertions.Fail("step13-factions-cohesion", "Set platform positions and measure vote cohesion", "DecisionFixture", "Ready", "Missing", failure);
            }

            string factionId = $"faction.testlab.cohesion.{context.RunId}";
            FactionOperationResult faction = runtime.CreateFaction(FactionCreate(context, "cohesion", PrototypeFactionDefinitionFactory.ReformFactionId, "Cohesion Reformists", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult actorAffiliation = runtime.ApplyAffiliation(FactionAffiliation(context, "cohesion-actor", factionId, actorId, PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            FactionOperationResult friendAffiliation = runtime.ApplyAffiliation(FactionAffiliation(context, "cohesion-friend", factionId, "person.prototype.friend", PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            string proposalId = $"organization-proposal.testlab.faction.{context.RunId}";
            OrganizationDecisionOperationResult submit = decisions.SubmitProposal(new OrganizationProposalRequest
            {
                transactionId = Tx(context, "faction-proposal-submit"),
                proposalId = proposalId,
                organizationId = "organization.prototype.adventurers-guild",
                proposalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.EstablishGoalProposalId,
                title = "Faction backed recruitment goal",
                proposerPersonId = actorId,
                requestedExecutionOperations = new[] { GoalOperation(context, "faction", $"organization-goal-record.testlab.faction.{context.RunId}", 4L) },
                submittedWorldTime = 10d,
                votingStartWorldTime = 10d,
                votingEndWorldTime = 20d
            });
            FactionOperationResult position = runtime.SetPosition(new FactionPositionRequest { transactionId = Tx(context, "faction-position"), positionId = $"faction-position.testlab.proposal.{context.RunId}", factionId = factionId, positionDefinitionId = PrototypeFactionDefinitionFactory.ProposalPositionId, targetKind = FactionPositionTargetKind.OrganizationProposal, targetId = proposalId, stance = FactionPositionStance.Supports, weight = 5, worldTime = 11d });
            FactionOperationResult recommendation = runtime.RecommendVote(new FactionRecommendationRequest { transactionId = Tx(context, "faction-recommend"), recommendationId = $"faction-recommendation.testlab.{context.RunId}", factionId = factionId, proposalId = proposalId, recommendation = FactionVoteRecommendationKind.Support, issuedByPersonId = actorId, worldTime = 12d });
            OrganizationDecisionOperationResult voteActor = decisions.CastVote(VoteRequest(context, proposalId, actorId, "faction-actor", OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult voteFriend = decisions.CastVote(VoteRequest(context, proposalId, "person.prototype.friend", "faction-friend", OrganizationVoteChoice.Reject));
            FactionVoteCohesionReport cohesion = runtime.CreateVoteCohesionReport(factionId, proposalId, 13d);
            FactionInfluenceReport influence = runtime.CreateInfluenceReport(factionId, "organization.prototype.adventurers-guild", 13d);
            bool voteRuntimeOwner = decisions.VoteCount == 2;
            bool valid = faction.Succeeded
                && actorAffiliation.Succeeded
                && friendAffiliation.Succeeded
                && submit.Succeeded
                && position.Succeeded
                && recommendation.Succeeded
                && voteActor.Succeeded
                && voteFriend.Succeeded
                && cohesion.AlignedVotes == 1
                && cohesion.OpposedVotes == 1
                && influence.InfluenceScore > 0
                && voteRuntimeOwner;
            return TestLabAssertions.True("step13-factions-cohesion", "Set platform positions and measure vote cohesion", valid, $"Faction={faction.Code} Affiliations={actorAffiliation.Code}/{friendAffiliation.Code} Proposal={submit.Code} Position={position.Code} Recommend={recommendation.Code} Votes={voteActor.Code}/{voteFriend.Code} Cohesion={cohesion.AlignedVotes}/{cohesion.OpposedVotes}/{cohesion.CountedVotes} Influence={influence.InfluenceScore} VoteOwner={voteRuntimeOwner}");
        }

        private static TestLabAutomationStepResult FactionSplitMergeDispositionProjectionPersistence(TestLabAutomationContext context)
        {
            if (!TryGetFactionRuntime(context, out FactionRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-factions-persistence", "Validate structural changes, redaction, and save restore", "FactionRuntime", "Present", "Missing", failure);
            }

            string sourceId = $"faction.testlab.source.{context.RunId}";
            string rivalId = $"faction.testlab.rival.{context.RunId}";
            FactionOperationResult source = runtime.CreateFaction(FactionCreate(context, "source", PrototypeFactionDefinitionFactory.CrossOrgMovementFactionId, "Source Coalition", new FactionHostContextData { contextKind = FactionHostContextKind.MultipleOrganizations, organizationIds = new[] { "organization.prototype.adventurers-guild", "organization.prototype.royal-forge" } }));
            FactionOperationResult rival = runtime.CreateFaction(FactionCreate(context, "rival", PrototypeFactionDefinitionFactory.TraditionalistFactionId, "Traditionalist Rival", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult disposition = runtime.SetDisposition(new FactionDispositionRequest { transactionId = Tx(context, "faction-disposition"), dispositionId = $"faction-disposition.testlab.{context.RunId}", sourceFactionId = sourceId, targetFactionId = rivalId, disposition = FactionDispositionKind.Competitive, intensity = 60, worldTime = 2d });
            FactionOperationResult secret = runtime.CreateFaction(FactionCreate(context, "secret", PrototypeFactionDefinitionFactory.SecretFactionId, "Hidden Lantern Society", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild"), visibility: FactionVisibility.Secret));
            FactionProjection concealed = runtime.GetFactionProjection(secret.Faction?.factionId, new FactionProjectionContext());
            FactionProjection development = runtime.GetFactionProjection(secret.Faction?.factionId, new FactionProjectionContext { developmentView = true, privileged = true });
            FactionOperationResult split = runtime.SplitFaction(Tx(context, "faction-split"), sourceId, new[]
            {
                FactionCreate(context, "split-a", PrototypeFactionDefinitionFactory.MerchantInterestFactionId, "Merchant Successor", new FactionHostContextData { contextKind = FactionHostContextKind.MultipleOrganizations, organizationIds = new[] { "organization.prototype.adventurers-guild", "organization.prototype.royal-forge" } }),
                FactionCreate(context, "split-b", PrototypeFactionDefinitionFactory.ReligiousInterestFactionId, "Sanctuary Successor", new FactionHostContextData { contextKind = FactionHostContextKind.PlaceOrRegion, placeOrRegionId = "place.prototype.region" })
            }, Array.Empty<string>(), 3d);
            FactionOperationResult merge = runtime.MergeFactions(Tx(context, "faction-merge"), new[] { rivalId, secret.Faction?.factionId }, FactionCreate(context, "merged", PrototypeFactionDefinitionFactory.LeaderSupportFactionId, "Merged Loyalists", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")), 4d);
            FactionRuntimeSaveData save = runtime.CreateSaveData();
            FactionRuntime restored = new FactionRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.OrganizationDecisions, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds);
            FactionOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.OrganizationDecisions, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds);
            FactionRuntimeSaveData corrupt = save.Clone();
            if (corrupt.factions.Count > 0) corrupt.factions[0].factionDefinitionId = "faction.missing-definition";
            bool rejected = !FactionRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, out string validationFailure);
            bool valid = source.Succeeded
                && rival.Succeeded
                && disposition.Succeeded
                && secret.Succeeded
                && concealed.Access == FactionProjectionAccess.Concealed
                && development.Access == FactionProjectionAccess.Development
                && split.Succeeded
                && merge.Succeeded
                && restore.Succeeded
                && restored.FactionCount == runtime.FactionCount
                && rejected;
            return TestLabAssertions.True("step13-factions-persistence", "Validate structural changes, redaction, and save restore", valid, $"Source={source.Code} Rival={rival.Code} Disposition={disposition.Code} Secret={secret.Code} Projection={concealed.Access}/{development.Access} Split={split.Code} Merge={merge.Code} Restore={restore.Code} Counts={restored.FactionCount}/{runtime.FactionCount} Reject={rejected}:{validationFailure}");
        }

        private static bool PrepareResourceAccounts(TestLabAutomationContext context, long openingBalance, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure)
        {
            resources = context?.ScenarioContext?.Runtimes?.OrganizationResources;
            currency = null;
            actorId = PrimaryAuthorityActorId(context);
            failure = string.Empty;
            if (resources == null || !resources.IsReady) { failure = "OrganizationResourceRuntime is missing or not ready."; return false; }
            if (!TryResourceCurrency(context, out currency)) { failure = "No active CurrencyDefinition is available."; return false; }
            CreateAuthorityGuildmaster(context, actorId, "resources");
            OrganizationResourceOperationResult treasury = resources.CreateTreasury(new OrganizationTreasuryRequest
            {
                transactionId = $"testlab.resources.treasury.{context.RunId}", treasuryId = ResourceTreasuryId(context), organizationId = "organization.prototype.adventurers-guild", resourceTypeDefinitionId = PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                officialName = "Test Lab Guild Treasury", actorPersonId = actorId, worldTime = 1d
            });
            OrganizationResourceOperationResult operating = resources.CreateAccount(new OrganizationAccountRequest
            {
                transactionId = $"testlab.resources.account.operating.{context.RunId}", accountId = ResourceOperatingAccountId(context), treasuryId = ResourceTreasuryId(context), organizationId = "organization.prototype.adventurers-guild", economyAccountId = ResourceOperatingEconomyAccountId(context),
                officialName = "Test Lab Operating", currencyDefinitionId = currency.Id, openingBalanceUnits = openingBalance, actorPersonId = actorId, worldTime = 2d
            });
            OrganizationResourceOperationResult reserve = resources.CreateAccount(new OrganizationAccountRequest
            {
                transactionId = $"testlab.resources.account.reserve.{context.RunId}", accountId = ResourceReserveAccountId(context), treasuryId = ResourceTreasuryId(context), organizationId = "organization.prototype.adventurers-guild", economyAccountId = ResourceReserveEconomyAccountId(context),
                officialName = "Test Lab Reserve", category = OrganizationAccountCategory.Reserve, currencyDefinitionId = currency.Id, openingBalanceUnits = 0L, actorPersonId = actorId, worldTime = 2d
            });
            if (!treasury.Succeeded || !operating.Succeeded || !reserve.Succeeded)
            {
                failure = $"Treasury={treasury.Code}:{treasury.Message} Operating={operating.Code}:{operating.Message} Reserve={reserve.Code}:{reserve.Message}";
                return false;
            }
            return true;
        }

        private static bool TryResourceCurrency(TestLabAutomationContext context, out CurrencyDefinition currency)
        {
            currency = context?.ScenarioContext?.Runtimes?.DefinitionRegistry?.DefinitionsById.Values.OfType<CurrencyDefinition>().OrderBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
            return currency != null;
        }

        private static OrganizationFinancialTransactionRequest ResourceTransferRequest(TestLabAutomationContext context, string currencyId, string actorId, long units, string transactionId, double worldTime) => new OrganizationFinancialTransactionRequest
        {
            transactionId = transactionId, organizationId = "organization.prototype.adventurers-guild", sourceAccountId = ResourceOperatingAccountId(context), destinationAccountId = ResourceReserveAccountId(context), currencyDefinitionId = currencyId,
            units = units, transactionKind = EconomyTransactionKind.Transfer, actorPersonId = actorId, purpose = "reserve allocation", worldTime = worldTime
        };

        private static string ResourceTreasuryId(TestLabAutomationContext context) => $"organization-treasury.testlab.{context.RunId}";
        private static string ResourceOperatingAccountId(TestLabAutomationContext context) => $"organization-account.testlab.operating.{context.RunId}";
        private static string ResourceReserveAccountId(TestLabAutomationContext context) => $"organization-account.testlab.reserve.{context.RunId}";
        private static string ResourceOperatingEconomyAccountId(TestLabAutomationContext context) => $"economy.organization.testlab.operating.{context.RunId}";
        private static string ResourceReserveEconomyAccountId(TestLabAutomationContext context) => $"economy.organization.testlab.reserve.{context.RunId}";

        private static bool PrepareDecisionFixture(TestLabAutomationContext context, long openingBalance, out OrganizationDecisionRuntime decisions, out OrganizationResourceRuntime resources, out CurrencyDefinition currency, out string actorId, out string failure)
        {
            decisions = context?.ScenarioContext?.Runtimes?.OrganizationDecisions;
            if (!PrepareResourceAccounts(context, openingBalance, out resources, out currency, out actorId, out failure))
            {
                decisions = null;
                return false;
            }

            if (decisions == null || !decisions.IsReady)
            {
                failure = "OrganizationDecisionRuntime is missing or not ready.";
                return false;
            }

            OrganizationMembershipRuntime memberships = context.ScenarioContext.Runtimes.OrganizationMemberships;
            memberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.decision.friend.{context.RunId}", "organization.prototype.adventurers-guild", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.decision.member.friend.{context.RunId}", consent: true));
            memberships.ApplyMembership(MembershipRequest($"organization-membership.testlab.decision.mentor.{context.RunId}", "organization.prototype.adventurers-guild", "person.prototype.mentor", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.decision.member.mentor.{context.RunId}", consent: true));
            decisions.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, memberships, context.ScenarioContext.Runtimes.OrganizationAuthority, resources, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, context.ScenarioContext.Runtimes.Economy);
            return true;
        }

        private static OrganizationPolicyRequest PolicyRequest(TestLabAutomationContext context, string policyId, string actorId, double worldTime) => new OrganizationPolicyRequest
        {
            transactionId = $"testlab.decisions.policy.{policyId}.{context.RunId}",
            policyId = policyId,
            organizationId = "organization.prototype.adventurers-guild",
            policyDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.ConfidentialityPolicyId,
            scope = OrganizationPolicyScopeData.EntireOrganization("organization.prototype.adventurers-guild"),
            parameters = new[]
            {
                PolicyParam("visibility", OrganizationPolicyParameterType.EnumValue, stringValue: OrganizationVisibility.Restricted.ToString()),
                PolicyParam("reshare_allowed", OrganizationPolicyParameterType.Boolean, boolValue: true)
            },
            priority = 100,
            actorPersonId = actorId,
            adoptedWorldTime = worldTime,
            effectiveStartWorldTime = worldTime,
            visibility = OrganizationVisibility.Restricted
        };

        private static OrganizationPolicyParameterValueData PolicyParam(string parameterId, OrganizationPolicyParameterType type, string stringValue = "", long longValue = 0L, bool boolValue = false) => new OrganizationPolicyParameterValueData
        {
            parameterId = parameterId,
            type = type,
            stringValue = stringValue ?? string.Empty,
            longValue = longValue,
            boolValue = boolValue
        };

        private static OrganizationVoteRequest VoteRequest(TestLabAutomationContext context, string proposalId, string voterId, string suffix, OrganizationVoteChoice choice) => new OrganizationVoteRequest
        {
            transactionId = $"testlab.decisions.vote.{suffix}.{context.RunId}",
            voteId = $"organization-vote.testlab.{suffix}.{context.RunId}",
            proposalId = proposalId,
            voterPersonId = voterId,
            choice = choice,
            worldTime = 12d
        };

        private static OrganizationDecisionExecutionOperationData GoalOperation(TestLabAutomationContext context, string suffix, string goalId, long targetValue) => new OrganizationDecisionExecutionOperationData
        {
            operationId = $"decision-operation.goal.{suffix}.{context.RunId}",
            kind = OrganizationDecisionExecutionOperationKind.EstablishGoal,
            targetId = goalId,
            definitionId = PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
            goalPayload = new OrganizationGoalRecordData
            {
                goalId = goalId,
                organizationId = "organization.prototype.adventurers-guild",
                goalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
                displayName = $"Recruitment Goal {suffix}",
                targetValue = targetValue,
                priority = 50,
                visibility = OrganizationVisibility.Restricted
            },
            required = true
        };

        private static OrganizationDecisionExecutionOperationData BudgetOperation(TestLabAutomationContext context, string currencyId, long units) => new OrganizationDecisionExecutionOperationData
        {
            operationId = $"decision-operation.budget.{context.RunId}",
            kind = OrganizationDecisionExecutionOperationKind.ApproveBudget,
            targetId = $"organization-budget.testlab.decision.{context.RunId}",
            treasuryId = ResourceTreasuryId(context),
            accountId = ResourceOperatingAccountId(context),
            currencyDefinitionId = currencyId,
            units = units,
            purpose = "decision-approved training budget",
            required = true
        };

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, displayName, action);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationDefinitionFactory.SecretSocietyDefinitionId
                });
        }

        private static ITestLabAutomationScenario MembershipScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationDefinitionFactory.BranchDefinitionId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildApplicantId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildAssociateId,
                    PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildRatingTrackId,
                    PrototypeOrganizationMembershipDefinitionFactory.AdventurerGuildEntryRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildERankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildDRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildCRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildBRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildARankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildTreasurerOfficeId
                });
        }

        private static ITestLabAutomationScenario AuthorityScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationDefinitionFactory.BranchDefinitionId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildRatingTrackId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    PrototypeOrganizationMembershipDefinitionFactory.BranchChapterMasterOfficeId,
                    PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholdersPermissionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.ViewRestrictedInformationPermissionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                    PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholderActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterOfficeBindingId,
                    PrototypeOrganizationAuthorityDefinitionFactory.BranchChapterMasterOfficeBindingId
                });
        }

        private static ITestLabAutomationScenario ResourceScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                    PrototypeOrganizationAuthorityDefinitionFactory.CreateTreasuryActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.TransferOrganizationFundsActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.ManageRestrictedFundsActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.AssignAssetCustodyActionId,
                    PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                    "currency.gold"
                });
        }

        private static ITestLabAutomationScenario DecisionScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                    PrototypeOrganizationAuthorityDefinitionFactory.SubmitDecisionProposalActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.AmendDecisionProposalActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.CastOrganizationVoteActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.CloseOrganizationVoteActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId,
                    PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
                    PrototypeOrganizationDecisionDefinitionFactory.ReserveFundGoalId,
                    PrototypeOrganizationDecisionDefinitionFactory.ConfidentialityPolicyId,
                    PrototypeOrganizationDecisionDefinitionFactory.BudgetLimitPolicyId,
                    PrototypeOrganizationDecisionDefinitionFactory.SimpleMajorityProcedureId,
                    PrototypeOrganizationDecisionDefinitionFactory.SecretBallotProcedureId,
                    PrototypeOrganizationDecisionDefinitionFactory.AdoptPolicyProposalId,
                    PrototypeOrganizationDecisionDefinitionFactory.EstablishGoalProposalId,
                    PrototypeOrganizationDecisionDefinitionFactory.ApproveBudgetProposalId,
                    PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                    "currency.gold"
                });
        }

        private static ITestLabAutomationScenario FactionScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                    PrototypeOrganizationAuthorityDefinitionFactory.SubmitDecisionProposalActionId,
                    PrototypeOrganizationAuthorityDefinitionFactory.CastOrganizationVoteActionId,
                    PrototypeOrganizationDecisionDefinitionFactory.EstablishGoalProposalId,
                    PrototypeOrganizationDecisionDefinitionFactory.SimpleMajorityProcedureId,
                    PrototypeFactionDefinitionFactory.ReformFactionId,
                    PrototypeFactionDefinitionFactory.TraditionalistFactionId,
                    PrototypeFactionDefinitionFactory.SecretFactionId,
                    PrototypeFactionDefinitionFactory.CrossOrgMovementFactionId,
                    PrototypeFactionDefinitionFactory.IndependentMovementFactionId,
                    PrototypeFactionDefinitionFactory.FormalMemberAffiliationId,
                    PrototypeFactionDefinitionFactory.SupporterAffiliationId,
                    PrototypeFactionDefinitionFactory.SecretMemberAffiliationId,
                    PrototypeFactionDefinitionFactory.OrganizerRoleId,
                    PrototypeFactionDefinitionFactory.SeniorLeaderRoleId,
                    PrototypeFactionDefinitionFactory.ProposalPositionId,
                    PrototypeFactionDefinitionFactory.ReformTraditionAxisId,
                    PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                    "currency.gold"
                });
        }

        private static ITestLabAutomationScenario DiplomacyScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationDefinitionFactory.CompanyDefinitionId,
                    PrototypeFactionDefinitionFactory.ReformFactionId,
                    PrototypeFactionDefinitionFactory.CrossOrgMovementFactionId,
                    PrototypeFactionDefinitionFactory.IndependentMovementFactionId,
                    PrototypeDiplomacyDefinitionFactory.RecognitionRelationId,
                    PrototypeDiplomacyDefinitionFactory.AllianceRelationId,
                    PrototypeDiplomacyDefinitionFactory.RivalryRelationId,
                    PrototypeDiplomacyDefinitionFactory.MutualDefenseAgreementId,
                    PrototypeDiplomacyDefinitionFactory.TradeCooperationAgreementId,
                    PrototypeDiplomacyDefinitionFactory.DefenseAssistanceClauseId,
                    PrototypeDiplomacyDefinitionFactory.TradeResourceClauseId,
                    PrototypeDiplomacyDefinitionFactory.FormalWarDefinitionId,
                    PrototypeDiplomacyDefinitionFactory.CeasefireAgreementId,
                    PrototypeDiplomacyDefinitionFactory.PeaceAgreementId
                });
        }

        private static ITestLabAutomationScenario GovernmentScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId,
                    PrototypeGovernmentDefinitionFactory.SovereigntyClaimDefinitionId,
                    PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId,
                    PrototypeGovernmentDefinitionFactory.MunicipalJurisdictionDefinitionId
                });
        }

        private static ITestLabAutomationScenario LegalScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Economy,
                requiredDefinitionIds: new[]
                {
                    PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId,
                    PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId,
                    PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                    PrototypeLegalDefinitionFactory.CentralStatuteId,
                    PrototypeLegalDefinitionFactory.RightProvisionId,
                    PrototypeLegalDefinitionFactory.PermissionProvisionId,
                    PrototypeLegalDefinitionFactory.ProhibitionProvisionId,
                    PrototypeLegalDefinitionFactory.CitizenStatusId,
                    PrototypeLegalDefinitionFactory.CitizenshipId
                });
        }

        private static ITestLabAutomationScenario CrimeScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId,
                    PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId,
                    PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                    PrototypeLegalDefinitionFactory.CentralStatuteId,
                    PrototypeLegalDefinitionFactory.ProhibitionProvisionId,
                    PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId,
                    PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId,
                    PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId
                });
        }

        private static ITestLabAutomationScenario JusticeScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items,
                requiredDefinitionIds: new[]
                {
                    PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId,
                    PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId,
                    PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                    PrototypeLegalDefinitionFactory.CentralStatuteId,
                    PrototypeLegalDefinitionFactory.ProhibitionProvisionId,
                    PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId,
                    PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId,
                    PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId,
                    PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId,
                    PrototypeJusticeDefinitionFactory.WarrantArrestDefinitionId,
                    PrototypeJusticeDefinitionFactory.CriminalChargeDefinitionId,
                    PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId,
                    PrototypeJusticeDefinitionFactory.FineSentenceDefinitionId,
                    PrototypeJusticeDefinitionFactory.JudgmentAppealDefinitionId
                });
        }

        private static ITestLabAutomationScenario IntegrationScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Organizations
                    | TestLabRuntimeArea.OrganizationMemberships
                    | TestLabRuntimeArea.OrganizationAuthority
                    | TestLabRuntimeArea.OrganizationResources
                    | TestLabRuntimeArea.OrganizationDecisions
                    | TestLabRuntimeArea.Factions
                    | TestLabRuntimeArea.Diplomacy
                    | TestLabRuntimeArea.Governments
                    | TestLabRuntimeArea.Laws
                    | TestLabRuntimeArea.Crimes
                    | TestLabRuntimeArea.Justice
                    | TestLabRuntimeArea.Economy
                    | TestLabRuntimeArea.Items
                    | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[]
                {
                    PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                    PrototypeOrganizationDefinitionFactory.CompanyDefinitionId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId,
                    PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                    PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId,
                    PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                    PrototypeOrganizationDecisionDefinitionFactory.SimpleMajorityProcedureId,
                    PrototypeFactionDefinitionFactory.ReformFactionId,
                    PrototypeDiplomacyDefinitionFactory.RecognitionRelationId,
                    PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId,
                    PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId,
                    PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId,
                    PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                    PrototypeLegalDefinitionFactory.CentralStatuteId,
                    PrototypeLegalDefinitionFactory.ProhibitionProvisionId,
                    PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId,
                    PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId,
                    PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId,
                    PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId
                });
        }

        private static void PrepareLegalFixture(TestLabAutomationContext context, out LegalRuntime laws, out string polityId, out string governmentId, out string territoryId, out string jurisdictionId)
        {
            laws = context.ScenarioContext.Runtimes.Laws;
            GovernmentRuntime governments = context.ScenarioContext.Runtimes.Governments;
            polityId = $"polity.testlab.legal.{context.RunId}";
            governmentId = $"government.testlab.legal.{context.RunId}";
            territoryId = $"political-territory.testlab.legal.{context.RunId}";
            jurisdictionId = $"jurisdiction.testlab.legal.{context.RunId}";
            governments.CreatePolity(new PolityCreateRequest { transactionId = LegalTx(context, "polity"), polityId = polityId, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Legal Test Polity", worldTime = 1d });
            governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = LegalTx(context, "government"), governmentId = governmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = polityId, officialName = "Legal Test Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 2d });
            governments.CreateTerritory(new TerritoryCreateRequest { transactionId = LegalTx(context, "territory"), territoryId = territoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Legal Test Territory", polityId = polityId, primaryGovernmentId = governmentId, placeIds = new[] { "place.testlab.capital" }, worldTime = 3d });
            governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = LegalTx(context, "jurisdiction"), jurisdictionId = jurisdictionId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = governmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.GeneralAdministration }, territoryIds = new[] { territoryId }, priority = 100, worldTime = 4d });
        }

        private static LegalOperationResult EnactLegal(TestLabAutomationContext context, LegalRuntime runtime, string suffix, string jurisdictionId, string instrumentDefinitionId, string provisionDefinitionId, LegalEffectCategory effect, string actionId, string territoryId, double effectiveTime, double enactmentTime = 5d, double expirationTime = -1d)
        {
            EnactLegalInstrumentRequest request = LegalRequest(context, suffix, jurisdictionId, instrumentDefinitionId, provisionDefinitionId, actionId, territoryId, effectiveTime);
            request.enactmentWorldTime = enactmentTime;
            request.publicationWorldTime = enactmentTime;
            request.expirationWorldTime = expirationTime;
            return runtime.Enact(request);
        }

        private static EnactLegalInstrumentRequest LegalRequest(TestLabAutomationContext context, string suffix, string jurisdictionId, string instrumentDefinitionId, string provisionDefinitionId, string actionId, string territoryId, double effectiveTime)
        {
            string governmentId = $"government.testlab.legal.{context.RunId}";
            return new EnactLegalInstrumentRequest
            {
                transactionId = LegalTx(context, $"enact-{suffix}"),
                instrumentId = $"legal-instrument.testlab.{suffix}.{context.RunId}",
                instrumentDefinitionId = instrumentDefinitionId,
                authorityDefinitionId = PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                title = $"Test Lab {suffix} Law",
                governmentId = governmentId,
                organizationId = "organization.prototype.adventurers-guild",
                jurisdictionIds = new[] { jurisdictionId },
                enactmentWorldTime = 5d,
                publicationWorldTime = 5d,
                effectiveWorldTime = effectiveTime,
                published = true,
                promulgated = true,
                visibility = PoliticalVisibility.Public,
                trustedSystemOperation = true,
                provisions = new[]
                {
                    new LegalProvisionCreateRequest
                    {
                        provisionId = $"legal-provision.testlab.{suffix}.{context.RunId}",
                        provisionDefinitionId = provisionDefinitionId,
                        citation = "section 1",
                        version = new LegalProvisionVersionData { effect = LegalEffectCategory.Unknown, actionId = actionId, territoryIds = new[] { territoryId }, effectiveWorldTime = effectiveTime }
                    }
                }
            };
        }

        private static TestLabAutomationStepResult LegalFail(string stepId, string failure) => TestLabAssertions.Fail(stepId, "Resolve legal runtime", "LegalRuntime", "Present", "Missing", failure);
        private static string LegalTx(TestLabAutomationContext context, string suffix) => $"testlab.feature13.9.{suffix}.{context?.RunId ?? "run"}";

        private static CrimeFixture PrepareCrimeFixture(TestLabAutomationContext context, string suffix)
        {
            PrepareLegalFixture(context, out LegalRuntime laws, out _, out string governmentId, out string territoryId, out string jurisdictionId);
            EnactLegal(context, laws, $"crime-{suffix}", jurisdictionId, PrototypeLegalDefinitionFactory.CentralStatuteId, PrototypeLegalDefinitionFactory.ProhibitionProvisionId, LegalEffectCategory.Prohibition, "crime.attack", territoryId, 10d);
            CrimeRuntime crimes = context.ScenarioContext.Runtimes.Crimes;
            return new CrimeFixture(
                crimes,
                context.ScenarioContext.Runtimes.PersonId,
                "person.prototype.friend",
                governmentId,
                territoryId,
                jurisdictionId,
                $"crime-incident.testlab.{suffix}.{context.RunId}",
                $"crime-report.testlab.{suffix}.{context.RunId}",
                $"potential-offense.testlab.{suffix}.{context.RunId}",
                $"crime-allegation.testlab.{suffix}.{context.RunId}",
                $"crime-suspect.testlab.{suffix}.{context.RunId}",
                $"crime-evidence-link.testlab.{suffix}.{context.RunId}",
                $"crime-investigation.testlab.{suffix}.{context.RunId}",
                $"warrant-request.testlab.{suffix}.{context.RunId}",
                $"warrant.testlab.{suffix}.{context.RunId}",
                $"wanted-status.testlab.{suffix}.{context.RunId}",
                $"wanted-notice.testlab.{suffix}.{context.RunId}");
        }

        private static void CreateCrimeCoreRecords(TestLabAutomationContext context, CrimeFixture fixture, string suffix)
        {
            fixture.Crimes.RecordIncident(CrimeIncident(context, fixture, suffix));
            fixture.Crimes.SubmitReport(CrimeReport(context, fixture, suffix));
            fixture.Crimes.EvaluatePotentialOffense(CrimeOffense(context, fixture, suffix));
        }

        private static CrimeIncidentRequest CrimeIncident(TestLabAutomationContext context, CrimeFixture fixture, string suffix) => new CrimeIncidentRequest
        {
            transactionId = CrimeTx(context, $"incident-{suffix}"),
            incidentId = fixture.IncidentId,
            category = CrimeIncidentCategory.ViolentIncident,
            occurrenceStartWorldTime = 12d,
            occurrenceEndWorldTime = 12.25d,
            discoveryWorldTime = 12.5d,
            reportingWorldTime = 13d,
            historicalEventIds = new[] { $"event.testlab.crime.{suffix}.{context.RunId}" },
            primaryPlaceId = "place.testlab.capital",
            primaryTerritoryId = fixture.TerritoryId,
            jurisdictionIds = new[] { fixture.JurisdictionId },
            involvedSubjects = new[] { CrimeSubjectReferenceData.Person(fixture.ActorId, "alleged-actor"), CrimeSubjectReferenceData.Person(fixture.VictimId, "victim") },
            victimIds = new[] { fixture.VictimId },
            witnessIds = new[] { "person.prototype.mentor" },
            visibility = PoliticalVisibility.Restricted,
            provenanceId = $"source.testlab.crime.{suffix}.{context.RunId}"
        };

        private static CrimeReportRequest CrimeReport(TestLabAutomationContext context, CrimeFixture fixture, string suffix) => new CrimeReportRequest
        {
            transactionId = CrimeTx(context, $"report-{suffix}"),
            reportId = fixture.ReportId,
            incidentId = fixture.IncidentId,
            category = CrimeReportCategory.VictimReport,
            reporterSubjectId = fixture.VictimId,
            reporterSubjectType = "Person",
            firstHand = true,
            submittedWorldTime = 13d,
            reporterReliabilityBasisPoints = 8000,
            visibility = PoliticalVisibility.Restricted,
            provenanceId = $"source.testlab.report.{suffix}.{context.RunId}"
        };

        private static PotentialOffenseEvaluationRequest CrimeOffense(TestLabAutomationContext context, CrimeFixture fixture, string suffix, bool preview = false) => new PotentialOffenseEvaluationRequest
        {
            transactionId = CrimeTx(context, $"offense-{suffix}"),
            potentialOffenseId = preview ? $"{fixture.OffenseId}.preview" : fixture.OffenseId,
            incidentId = fixture.IncidentId,
            offenseDefinitionId = PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId,
            allegedActorIds = new[] { fixture.ActorId },
            victimOrTargetIds = new[] { fixture.VictimId },
            actionId = "crime.attack",
            stage = OffenseStage.Completed,
            participation = ParticipationCategory.PrincipalActor,
            evidenceSufficiency = EvidenceSufficiencyState.Substantial,
            elementEvaluations = new[]
            {
                new OffenseElementEvaluationData { kind = OffenseElementKind.ActorConduct, key = "conduct", expectedValue = "crime.attack", observedValue = "crime.attack", supported = true, evidenceId = $"evidence.testlab.crime.{suffix}.{context.RunId}" }
            },
            visibility = PoliticalVisibility.Restricted,
            provenanceId = $"source.testlab.offense.{suffix}.{context.RunId}",
            preview = preview
        };

        private static WarrantRequestCreateRequest CrimeWarrantRequest(TestLabAutomationContext context, CrimeFixture fixture, string suffix, EvidenceSufficiencyState assertedThreshold) => new WarrantRequestCreateRequest
        {
            transactionId = CrimeTx(context, $"warrant-request-{suffix}"),
            warrantRequestId = string.Equals(suffix, "low", StringComparison.Ordinal) ? $"{fixture.WarrantRequestId}.low" : fixture.WarrantRequestId,
            warrantDefinitionId = PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId,
            incidentId = fixture.IncidentId,
            potentialOffenseId = fixture.OffenseId,
            requestedByPersonId = fixture.VictimId,
            issuingGovernmentId = fixture.GovernmentId,
            issuingOrganizationId = "organization.prototype.adventurers-guild",
            scope = new WarrantScopeData { kind = WarrantScopeKind.Person, targetId = fixture.ActorId, jurisdictionIds = new[] { fixture.JurisdictionId }, territoryIds = new[] { fixture.TerritoryId }, purpose = "arrest for reported assault" },
            assertedThreshold = assertedThreshold,
            requestedWorldTime = 15d,
            visibility = PoliticalVisibility.Restricted
        };

        private static TestLabAutomationStepResult CrimeFail(string stepId, string failure) => TestLabAssertions.Fail(stepId, "Resolve crime runtime", "CrimeRuntime", "Present", "Missing", failure);
        private static string CrimeTx(TestLabAutomationContext context, string suffix) => $"testlab.feature13.10.{suffix}.{context?.RunId ?? "run"}";

        private static JusticeFixture PrepareJusticeFixture(TestLabAutomationContext context, string suffix, bool issueWarrant = true, bool registerCourt = true, bool registerAppealCourt = false)
        {
            CrimeFixture crime = PrepareCrimeFixture(context, $"justice-{suffix}");
            CreateCrimeCoreRecords(context, crime, $"justice-{suffix}");
            JusticeRuntime justice = context.ScenarioContext.Runtimes.Justice;
            JusticeFixture fixture = new JusticeFixture(
                justice,
                crime,
                $"court.testlab.justice.{suffix}.{context.RunId}",
                $"court.testlab.justice.appellate.{suffix}.{context.RunId}",
                $"arrest.testlab.justice.{suffix}.{context.RunId}",
                $"custody.testlab.justice.{suffix}.{context.RunId}",
                $"release-order.testlab.justice.{suffix}.{context.RunId}",
                $"case.testlab.justice.{suffix}.{context.RunId}",
                $"charge.testlab.justice.{suffix}.{context.RunId}",
                $"party.testlab.justice.defendant.{suffix}.{context.RunId}",
                $"party.testlab.justice.prosecutor.{suffix}.{context.RunId}",
                $"plea.testlab.justice.{suffix}.{context.RunId}",
                $"hearing.testlab.justice.{suffix}.{context.RunId}",
                $"evidence-submission.testlab.justice.{suffix}.{context.RunId}",
                $"finding.testlab.justice.{suffix}.{context.RunId}",
                $"judgment.testlab.justice.{suffix}.{context.RunId}",
                $"sentence.testlab.justice.{suffix}.{context.RunId}",
                $"sentence-component.testlab.justice.{suffix}.{context.RunId}",
                $"remedy.testlab.justice.{suffix}.{context.RunId}",
                $"appeal.testlab.justice.{suffix}.{context.RunId}",
                $"clemency.testlab.justice.{suffix}.{context.RunId}");

            if (issueWarrant)
            {
                crime.Crimes.RequestWarrant(CrimeWarrantRequest(context, crime, $"justice-{suffix}", EvidenceSufficiencyState.Substantial));
                crime.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = JusticeTx(context, $"warrant-review-{suffix}"), warrantRequestId = crime.WarrantRequestId, reviewId = "trusted.system", approve = true, trustedSystemOperation = true });
                crime.Crimes.IssueWarrant(new WarrantIssueRequest { transactionId = JusticeTx(context, $"warrant-issue-{suffix}"), warrantId = crime.WarrantId, warrantRequestId = crime.WarrantRequestId, issuedByPersonId = crime.VictimId, issuedWorldTime = 16d, activationWorldTime = 16d, expirationWorldTime = 40d, trustedSystemOperation = true });
            }

            if (registerCourt)
            {
                justice.RegisterCourt(JusticeCourtRequest(context, fixture, "primary"));
            }

            if (registerAppealCourt)
            {
                justice.RegisterCourt(JusticeCourtRequest(context, fixture, "appeal"));
            }

            return fixture;
        }

        private static CourtRegisterRequest JusticeCourtRequest(TestLabAutomationContext context, JusticeFixture fixture, string suffix, bool preview = false)
        {
            bool appellate = string.Equals(suffix, "appeal", StringComparison.Ordinal);
            return new CourtRegisterRequest
            {
                transactionId = JusticeTx(context, $"court-{suffix}"),
                courtId = appellate ? fixture.AppellateCourtId : preview ? $"{fixture.CourtId}.preview" : fixture.CourtId,
                courtDefinitionId = appellate ? PrototypeJusticeDefinitionFactory.AppellateCourtDefinitionId : PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId,
                justiceInstitutionDefinitionId = PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId,
                governmentId = fixture.Crime.GovernmentId,
                jurisdictionIds = new[] { fixture.Crime.JurisdictionId },
                territoryIds = new[] { fixture.Crime.TerritoryId },
                courthousePlaceId = appellate ? "place.testlab.appellate-court" : "place.testlab.court",
                judgeOfficeIds = new[] { "office.prototype.judge" },
                clerkOfficeIds = new[] { "office.prototype.clerk" },
                appealParentCourtId = appellate ? string.Empty : fixture.AppellateCourtId,
                worldTime = 17d,
                visibility = PoliticalVisibility.Public,
                preview = preview
            };
        }

        private static ArrestRequest JusticeArrestRequest(TestLabAutomationContext context, JusticeFixture fixture, string suffix) => new ArrestRequest
        {
            transactionId = JusticeTx(context, $"arrest-{suffix}"),
            arrestId = fixture.ArrestId,
            arrestDefinitionId = PrototypeJusticeDefinitionFactory.WarrantArrestDefinitionId,
            arrestedPersonId = fixture.Crime.ActorId,
            executingPersonId = fixture.Crime.VictimId,
            executingGovernmentId = fixture.Crime.GovernmentId,
            executingOrganizationId = "organization.prototype.adventurers-guild",
            legalBasis = new JusticeLegalBasisData { kind = ArrestLegalBasisKind.ActiveArrestWarrant, warrantId = fixture.Crime.WarrantId, incidentId = fixture.Crime.IncidentId, potentialOffenseId = fixture.Crime.OffenseId, effectiveWorldTime = 16d, expirationWorldTime = 40d },
            jurisdictionId = fixture.Crime.JurisdictionId,
            territoryId = fixture.Crime.TerritoryId,
            placeId = "place.testlab.arrest-location",
            custodyId = fixture.CustodyId,
            custodyFacilityPlaceId = "place.testlab.detention",
            arrestWorldTime = 17.5d,
            visibility = PoliticalVisibility.Restricted,
            trustedSystemOperation = true
        };

        private static JusticeOperationResult FileJusticeCase(TestLabAutomationContext context, JusticeFixture fixture)
        {
            return fixture.Justice.FileCase(new CaseFileRequest
            {
                transactionId = JusticeTx(context, $"case-{fixture.CaseId}"),
                caseId = fixture.CaseId,
                category = JusticeCaseCategory.Criminal,
                courtId = fixture.CourtId,
                incidentIds = new[] { fixture.Crime.IncidentId },
                parties = new[]
                {
                    new JusticePartyData { partyId = fixture.DefendantPartyId, personId = fixture.Crime.ActorId, role = CasePartyRole.Defendant, visibility = PoliticalVisibility.Restricted },
                    new JusticePartyData { partyId = fixture.ProsecutorPartyId, organizationId = "organization.prototype.adventurers-guild", role = CasePartyRole.Prosecutor, visibility = PoliticalVisibility.Public }
                },
                filedWorldTime = 21d,
                visibility = PoliticalVisibility.Restricted
            });
        }

        private static JusticeOperationResult FileJusticeCharge(TestLabAutomationContext context, JusticeFixture fixture)
        {
            return fixture.Justice.FileCharge(new ChargeFileRequest
            {
                transactionId = JusticeTx(context, $"charge-{fixture.ChargeId}"),
                chargeId = fixture.ChargeId,
                chargeDefinitionId = PrototypeJusticeDefinitionFactory.CriminalChargeDefinitionId,
                caseId = fixture.CaseId,
                defendantPersonId = fixture.Crime.ActorId,
                incidentId = fixture.Crime.IncidentId,
                potentialOffenseId = fixture.Crime.OffenseId,
                filingThreshold = EvidenceSufficiencyState.Substantial,
                filedWorldTime = 22d,
                trustedSystemOperation = true,
                visibility = PoliticalVisibility.Restricted
            });
        }

        private static void CreateJudgedCase(TestLabAutomationContext context, JusticeFixture fixture)
        {
            FileJusticeCase(context, fixture);
            FileJusticeCharge(context, fixture);
            fixture.Justice.ScheduleHearing(new HearingScheduleRequest { transactionId = JusticeTx(context, $"trial-{fixture.HearingId}"), hearingId = fixture.HearingId, hearingDefinitionId = PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId, caseId = fixture.CaseId, category = HearingCategory.Trial, issueIds = new[] { fixture.ChargeId }, scheduledWorldTime = 25d });
            fixture.Justice.SubmitEvidence(new EvidenceSubmissionRequest { transactionId = JusticeTx(context, $"evidence-{fixture.EvidenceSubmissionId}"), evidenceSubmissionId = fixture.EvidenceSubmissionId, caseId = fixture.CaseId, hearingId = fixture.HearingId, evidenceId = fixture.Crime.EvidenceLinkId, submittedByPartyId = fixture.ProsecutorPartyId, submittedWorldTime = 25.1d });
            fixture.Justice.RuleOnEvidence(new EvidenceRulingRequest { transactionId = JusticeTx(context, $"ruling-{fixture.EvidenceSubmissionId}"), evidenceSubmissionId = fixture.EvidenceSubmissionId, targetState = EvidenceRulingState.Admitted, reason = "Relevant to charge." });
            fixture.Justice.RecordFinding(new FindingRequest { transactionId = JusticeTx(context, $"finding-{fixture.FindingId}"), findingId = fixture.FindingId, caseId = fixture.CaseId, chargeId = fixture.ChargeId, category = FindingCategory.Fact, text = "Elements proven.", proven = true, enteredWorldTime = 26d });
            fixture.Justice.EnterJudgment(new JudgmentRequest { transactionId = JusticeTx(context, $"judgment-{fixture.JudgmentId}"), judgmentId = fixture.JudgmentId, caseId = fixture.CaseId, chargeOutcomes = new[] { new JusticeChargeOutcomeData { chargeId = fixture.ChargeId, findingId = fixture.FindingId, outcome = JudgmentOutcome.Guilty, reason = "Elements proven." } }, enteredWorldTime = 27d });
        }

        private static TestLabAutomationStepResult JusticeFail(string stepId, string failure) => TestLabAssertions.Fail(stepId, "Resolve justice runtime", "JusticeRuntime", "Present", "Missing", failure);
        private static string JusticeTx(TestLabAutomationContext context, string suffix) => $"testlab.feature13.11.{suffix}.{context?.RunId ?? "run"}";

        private sealed class JusticeFixture
        {
            public JusticeFixture(JusticeRuntime justice, CrimeFixture crime, string courtId, string appellateCourtId, string arrestId, string custodyId, string releaseOrderId, string caseId, string chargeId, string defendantPartyId, string prosecutorPartyId, string pleaId, string hearingId, string evidenceSubmissionId, string findingId, string judgmentId, string sentenceId, string sentenceComponentId, string remedyId, string appealId, string clemencyId)
            {
                Justice = justice;
                Crime = crime;
                CourtId = courtId;
                AppellateCourtId = appellateCourtId;
                ArrestId = arrestId;
                CustodyId = custodyId;
                ReleaseOrderId = releaseOrderId;
                CaseId = caseId;
                ChargeId = chargeId;
                DefendantPartyId = defendantPartyId;
                ProsecutorPartyId = prosecutorPartyId;
                PleaId = pleaId;
                HearingId = hearingId;
                EvidenceSubmissionId = evidenceSubmissionId;
                FindingId = findingId;
                JudgmentId = judgmentId;
                SentenceId = sentenceId;
                SentenceComponentId = sentenceComponentId;
                RemedyId = remedyId;
                AppealId = appealId;
                ClemencyId = clemencyId;
            }

            public JusticeRuntime Justice { get; }
            public CrimeFixture Crime { get; }
            public string CourtId { get; }
            public string AppellateCourtId { get; }
            public string ArrestId { get; }
            public string CustodyId { get; }
            public string ReleaseOrderId { get; }
            public string CaseId { get; }
            public string ChargeId { get; }
            public string DefendantPartyId { get; }
            public string ProsecutorPartyId { get; }
            public string PleaId { get; }
            public string HearingId { get; }
            public string EvidenceSubmissionId { get; }
            public string FindingId { get; }
            public string JudgmentId { get; }
            public string SentenceId { get; }
            public string SentenceComponentId { get; }
            public string RemedyId { get; }
            public string AppealId { get; }
            public string ClemencyId { get; }
        }

        private sealed class CrimeFixture
        {
            public CrimeFixture(CrimeRuntime crimes, string actorId, string victimId, string governmentId, string territoryId, string jurisdictionId, string incidentId, string reportId, string offenseId, string allegationId, string suspectId, string evidenceLinkId, string investigationId, string warrantRequestId, string warrantId, string wantedId, string noticeId)
            {
                Crimes = crimes;
                ActorId = actorId;
                VictimId = victimId;
                GovernmentId = governmentId;
                TerritoryId = territoryId;
                JurisdictionId = jurisdictionId;
                IncidentId = incidentId;
                ReportId = reportId;
                OffenseId = offenseId;
                AllegationId = allegationId;
                SuspectId = suspectId;
                EvidenceLinkId = evidenceLinkId;
                InvestigationId = investigationId;
                WarrantRequestId = warrantRequestId;
                WarrantId = warrantId;
                WantedId = wantedId;
                NoticeId = noticeId;
            }

            public CrimeRuntime Crimes { get; }
            public string ActorId { get; }
            public string VictimId { get; }
            public string GovernmentId { get; }
            public string TerritoryId { get; }
            public string JurisdictionId { get; }
            public string IncidentId { get; }
            public string ReportId { get; }
            public string OffenseId { get; }
            public string AllegationId { get; }
            public string SuspectId { get; }
            public string EvidenceLinkId { get; }
            public string InvestigationId { get; }
            public string WarrantRequestId { get; }
            public string WarrantId { get; }
            public string WantedId { get; }
            public string NoticeId { get; }
        }

        private static TestLabAutomationStepResult GovernmentRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetGovernmentRuntime(context, out GovernmentRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-government-readiness", "Resolve government definitions", "GovernmentRuntime", "Present", "Missing", failure);
            }

            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool ready = registry.TryGet(PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, out PolityDefinition polity)
                && registry.TryGet(PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, out GovernmentDefinition government)
                && registry.TryGet(PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, out PoliticalTerritoryDefinition territory)
                && registry.TryGet(PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, out JurisdictionDefinition jurisdiction)
                && polity.Category == PolityCategory.Kingdom
                && government.Category == GovernmentCategory.MonarchicalGovernment
                && territory.Category == PoliticalTerritoryCategory.Realm
                && jurisdiction.Category == JurisdictionCategory.GeneralGovernment
                && runtime.Revision == 0L;
            return TestLabAssertions.True("step13-government-readiness", "Resolve government definitions", ready, $"Ready={ready} Revision={runtime.Revision}");
        }

        private static TestLabAutomationStepResult GovernmentIdentityAndTerritory(TestLabAutomationContext context)
        {
            if (!TryGetGovernmentRuntime(context, out GovernmentRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-government-identity", "Create polity, government, and territory records", "GovernmentRuntime", "Present", "Missing", failure);
            }

            string suffix = context.RunId;
            string polityId = $"polity.testlab.kingdom.{suffix}";
            string governmentId = $"government.testlab.royal.{suffix}";
            string territoryId = $"political-territory.testlab.realm.{suffix}";
            PoliticalOperationResult polity = runtime.CreatePolity(new PolityCreateRequest { transactionId = GovernmentTx(context, "polity"), polityId = polityId, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Test Lab Kingdom", worldTime = 1d });
            PoliticalOperationResult government = runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = GovernmentTx(context, "government"), governmentId = governmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = polityId, officialName = "Test Lab Royal Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 2d });
            PoliticalOperationResult territory = runtime.CreateTerritory(new TerritoryCreateRequest { transactionId = GovernmentTx(context, "territory"), territoryId = territoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Test Lab Realm", polityId = polityId, primaryGovernmentId = governmentId, placeIds = new[] { "place.testlab.capital" }, worldTime = 3d });
            PoliticalOperationResult duplicate = runtime.CreateTerritory(new TerritoryCreateRequest { transactionId = GovernmentTx(context, "territory"), territoryId = territoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Test Lab Realm", polityId = polityId, primaryGovernmentId = governmentId, placeIds = new[] { "place.testlab.capital" }, worldTime = 3d });
            bool valid = polity.Succeeded && government.Succeeded && territory.Succeeded && duplicate.Succeeded && duplicate.Code == PoliticalOperationCode.Duplicate
                && runtime.PolityCount == 1 && runtime.GovernmentCount == 1 && runtime.TerritoryCount == 1;
            return TestLabAssertions.True("step13-government-identity", "Create polity, government, and territory records", valid, $"Polity={polity.Code} Government={government.Code} Territory={territory.Code} Duplicate={duplicate.Code} Counts={runtime.PolityCount}/{runtime.GovernmentCount}/{runtime.TerritoryCount}");
        }

        private static TestLabAutomationStepResult GovernmentClaimsAndJurisdiction(TestLabAutomationContext context)
        {
            if (!TryGetGovernmentRuntime(context, out GovernmentRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-government-jurisdiction", "Resolve territorial authority records", "GovernmentRuntime", "Present", "Missing", failure);
            }

            CreateGovernmentFixture(context, runtime, out string polityId, out string governmentId, out string territoryId);
            PoliticalOperationResult claim = runtime.AssertTerritorialClaim(new TerritorialClaimRequest { transactionId = GovernmentTx(context, "claim"), claimId = $"territorial-claim.testlab.{context.RunId}", claimDefinitionId = PrototypeGovernmentDefinitionFactory.SovereigntyClaimDefinitionId, territoryId = territoryId, claimantPolityId = polityId, claimantGovernmentId = governmentId, category = TerritorialClaimCategory.Sovereignty, worldTime = 4d });
            PoliticalOperationResult control = runtime.RecordControl(new TerritorialControlRequest { transactionId = GovernmentTx(context, "control"), controlId = $"territorial-control.testlab.{context.RunId}", territoryId = territoryId, controllingGovernmentId = governmentId, worldTime = 5d });
            PoliticalOperationResult administration = runtime.RecordAdministration(new TerritoryAdministrationRequest { transactionId = GovernmentTx(context, "administration"), administrationId = $"territory-administration.testlab.{context.RunId}", territoryId = territoryId, administeringGovernmentId = governmentId, worldTime = 5d });
            PoliticalOperationResult seat = runtime.RegisterSeat(new GovernmentSeatRequest { transactionId = GovernmentTx(context, "seat"), seatId = $"government-seat.testlab.{context.RunId}", governmentId = governmentId, placeId = "place.testlab.capital", primary = true, worldTime = 5d });
            PoliticalOperationResult sovereignty = runtime.AssertSovereignty(new SovereigntyClaimRequest { transactionId = GovernmentTx(context, "sovereignty"), sovereigntyClaimId = $"sovereignty-claim.testlab.{context.RunId}", polityId = polityId, governmentId = governmentId, territoryId = territoryId, worldTime = 6d });
            string generalId = $"jurisdiction.testlab.general.{context.RunId}";
            PoliticalOperationResult jurisdiction = runtime.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = GovernmentTx(context, "jurisdiction"), jurisdictionId = generalId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = governmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.GeneralAdministration }, territoryIds = new[] { territoryId }, priority = 10, worldTime = 7d });
            JurisdictionResolutionResult resolved = runtime.ResolveJurisdiction(new JurisdictionResolutionRequest { requesterGovernmentId = governmentId, territoryId = territoryId, subjectMatter = JurisdictionSubjectMatter.GeneralAdministration, worldTime = 8d });
            bool valid = claim.Succeeded && control.Succeeded && administration.Succeeded && seat.Succeeded && sovereignty.Succeeded && jurisdiction.Succeeded
                && resolved.Status == JurisdictionResolutionStatus.Applicable && resolved.SelectedJurisdiction?.jurisdictionId == generalId;
            return TestLabAssertions.True("step13-government-jurisdiction", "Resolve territorial authority records", valid, $"Claim={claim.Code} Control={control.Code} Administration={administration.Code} Seat={seat.Code} Sovereignty={sovereignty.Code} Jurisdiction={jurisdiction.Code} Resolution={resolved.Status}");
        }

        private static TestLabAutomationStepResult GovernmentProjectionPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetGovernmentRuntime(context, out GovernmentRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-government-persistence", "Project, save, restore, and reject corrupt government graph", "GovernmentRuntime", "Present", "Missing", failure);
            }

            CreateGovernmentFixture(context, runtime, out string polityId, out string governmentId, out _ , PoliticalVisibility.Secret);
            PoliticalProjectionResult<GovernmentRecordData> redacted = runtime.ProjectGovernment(governmentId, privileged: false);
            PoliticalProjectionResult<GovernmentRecordData> full = runtime.ProjectGovernment(governmentId, privileged: true);
            GovernmentRuntimeSaveData save = runtime.CreateSaveData();
            GovernmentRuntime restored = new GovernmentRuntime();
            PoliticalOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationDecisions, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.Factions, context.ScenarioContext.Runtimes.Diplomacy, context.ScenarioContext.Runtimes.Properties, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, Array.Empty<string>(), restoring: true);
            GovernmentRuntimeSaveData corrupt = save.Clone();
            corrupt.governments[0].polityId = "polity.missing";
            long before = restored.Revision;
            PoliticalOperationResult rejected = restored.RestoreFromSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.OrganizationMemberships, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationDecisions, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.Factions, context.ScenarioContext.Runtimes.Diplomacy, context.ScenarioContext.Runtimes.Properties, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, Array.Empty<string>(), restoring: true);
            bool valid = redacted.Succeeded && redacted.Redacted && full.Succeeded && !full.Redacted && restore.Succeeded && !rejected.Succeeded && restored.Revision == before && restored.TryGetPolity(polityId, out _);
            restored.Dispose();
            return TestLabAssertions.True("step13-government-persistence", "Project, save, restore, and reject corrupt government graph", valid, $"Redacted={redacted.Decision} Full={full.Decision} Restore={restore.Code} Reject={rejected.Code} NoMutation={restored.Revision == before}");
        }

        private static void CreateGovernmentFixture(TestLabAutomationContext context, GovernmentRuntime runtime, out string polityId, out string governmentId, out string territoryId, PoliticalVisibility visibility = PoliticalVisibility.Public)
        {
            polityId = $"polity.testlab.fixture.{context.RunId}";
            governmentId = $"government.testlab.fixture.{context.RunId}";
            territoryId = $"political-territory.testlab.fixture.{context.RunId}";
            runtime.CreatePolity(new PolityCreateRequest { transactionId = GovernmentTx(context, "fixture-polity"), polityId = polityId, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Fixture Kingdom", worldTime = 1d, visibility = visibility });
            runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = GovernmentTx(context, "fixture-government"), governmentId = governmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = polityId, officialName = "Fixture Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 2d, visibility = visibility });
            runtime.CreateTerritory(new TerritoryCreateRequest { transactionId = GovernmentTx(context, "fixture-territory"), territoryId = territoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Fixture Realm", polityId = polityId, primaryGovernmentId = governmentId, placeIds = new[] { "place.testlab.capital" }, worldTime = 3d, visibility = visibility });
        }

        private static string GovernmentTx(TestLabAutomationContext context, string suffix) => $"testlab.feature13.8.{suffix}.{context?.RunId ?? "run"}";

        private static TestLabAutomationStepResult DiplomacyRuntimeReadiness(TestLabAutomationContext context)
        {
            if (!TryGetDiplomacyRuntime(context, out DiplomacyRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-diplomacy-readiness", "Resolve diplomacy definitions", "DiplomacyRuntime", "Present", "Missing", failure);
            }

            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool relation = registry.TryGet(PrototypeDiplomacyDefinitionFactory.AllianceRelationId, out DiplomaticRelationDefinition alliance);
            bool agreement = registry.TryGet(PrototypeDiplomacyDefinitionFactory.MutualDefenseAgreementId, out DiplomaticAgreementDefinition pact);
            bool clause = registry.TryGet(PrototypeDiplomacyDefinitionFactory.DefenseAssistanceClauseId, out DiplomaticClauseDefinition defense);
            bool war = registry.TryGet(PrototypeDiplomacyDefinitionFactory.FormalWarDefinitionId, out DiplomaticWarDefinition formalWar);
            bool valid = runtime != null
                && relation
                && agreement
                && clause
                && war
                && alliance.Category == DiplomaticRelationCategory.Allied
                && pact.Category == DiplomaticAgreementCategory.MutualDefense
                && defense.BreachTrackable
                && !formalWar.SupportsFactionalParticipants;
            return TestLabAssertions.True("step13-diplomacy-readiness", "Resolve diplomacy definitions", valid, $"Definitions={relation}/{agreement}/{clause}/{war} RuntimeRevision={runtime?.Revision ?? -1}");
        }

        private static TestLabAutomationStepResult DiplomacyActorEligibilityRelations(TestLabAutomationContext context)
        {
            if (!TryGetDiplomacyRuntime(context, out DiplomacyRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-diplomacy-relations", "Create recognition, alliance, rivalry, and reject internal faction treaty actor", "DiplomacyRuntime", "Present", "Missing", failure);
            }

            FactionRuntime factions = context.ScenarioContext.Runtimes.Factions;
            FactionOperationResult internalFaction = factions.CreateFaction(FactionCreate(context, "diplomacy-internal", PrototypeFactionDefinitionFactory.ReformFactionId, "Internal Diplomacy Reformists", FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult crossFaction = factions.CreateFaction(FactionCreate(context, "diplomacy-cross", PrototypeFactionDefinitionFactory.CrossOrgMovementFactionId, "Cross Organization Diplomats", new FactionHostContextData { contextKind = FactionHostContextKind.MultipleOrganizations, organizationIds = new[] { "organization.prototype.adventurers-guild", "organization.prototype.royal-forge" } }));

            DiplomacyOperationResult preview = runtime.CreateRelation(Relation(context, "preview", PrototypeDiplomacyDefinitionFactory.RecognitionRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge"), preview: true));
            DiplomacyOperationResult recognition = runtime.CreateRelation(Relation(context, "recognition", PrototypeDiplomacyDefinitionFactory.RecognitionRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge")));
            DiplomacyOperationResult duplicate = runtime.CreateRelation(Relation(context, "recognition", PrototypeDiplomacyDefinitionFactory.RecognitionRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge")));
            DiplomacyOperationResult alliance = runtime.CreateRelation(Relation(context, "alliance", PrototypeDiplomacyDefinitionFactory.AllianceRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge")));
            DiplomacyOperationResult rivalry = runtime.CreateRelation(Relation(context, "rivalry", PrototypeDiplomacyDefinitionFactory.RivalryRelationId, Org("organization.prototype.adventurers-guild"), Faction(crossFaction.Faction?.factionId)));
            DiplomacyOperationResult rejected = runtime.CreateRelation(Relation(context, "rejected-internal", PrototypeDiplomacyDefinitionFactory.AllianceRelationId, Org("organization.prototype.adventurers-guild"), Faction(internalFaction.Faction?.factionId)));

            bool mirrored = runtime.QueryRelationsForActor(Org("organization.prototype.royal-forge"), activeOnly: true).Any(item => item.relationId.EndsWith(".reciprocal", StringComparison.Ordinal));
            bool valid = internalFaction.Succeeded
                && crossFaction.Succeeded
                && preview.Code == DiplomaticOperationCode.Preview
                && recognition.Succeeded
                && duplicate.Duplicate
                && alliance.Succeeded
                && rivalry.Succeeded
                && !rejected.Succeeded
                && rejected.Code == DiplomaticOperationCode.ActorIneligible
                && mirrored;
            return TestLabAssertions.True("step13-diplomacy-relations", "Create recognition, alliance, rivalry, and reject internal faction treaty actor", valid, $"Preview={preview.Code} Recognition={recognition.Code} Duplicate={duplicate.Code}/{duplicate.Duplicate} Alliance={alliance.Code} Rivalry={rivalry.Code} Rejected={rejected.Code} Mirrored={mirrored}");
        }

        private static TestLabAutomationStepResult DiplomacyAgreementsClausesBreaches(TestLabAutomationContext context)
        {
            if (!TryGetDiplomacyRuntime(context, out DiplomacyRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-diplomacy-agreements", "Create agreement lifecycle and breach record", "DiplomacyRuntime", "Present", "Missing", failure);
            }

            string agreementId = $"diplomatic-agreement.testlab.mutual-defense.{context.RunId}";
            string guildParty = $"{agreementId}.party.guild";
            string forgeParty = $"{agreementId}.party.forge";
            string clauseId = $"{agreementId}.clause.defense";
            DiplomacyOperationResult draft = runtime.CreateAgreement(new DiplomaticAgreementRequest
            {
                transactionId = DiplomacyTx(context, "agreement-draft"),
                agreementId = agreementId,
                agreementDefinitionId = PrototypeDiplomacyDefinitionFactory.MutualDefenseAgreementId,
                title = "Test Lab Mutual Defense Pact",
                initialState = DiplomaticAgreementLifecycleState.Draft,
                visibility = DiplomaticVisibility.Restricted,
                worldTime = 10d,
                parties = new[] { Party(guildParty, Org("organization.prototype.adventurers-guild")), Party(forgeParty, Org("organization.prototype.royal-forge")) },
                clauses = new[] { Clause(clauseId, PrototypeDiplomacyDefinitionFactory.DefenseAssistanceClauseId, DiplomaticClauseCategory.DefenseAssistance, DiplomaticVisibility.Restricted) }
            });
            DiplomacyOperationResult signA = runtime.SignAgreement(new DiplomaticSignatureRequest { transactionId = DiplomacyTx(context, "agreement-sign-a"), agreementId = agreementId, partyId = guildParty, signerPersonId = PrimaryAuthorityActorId(context), worldTime = 11d });
            DiplomacyOperationResult signB = runtime.SignAgreement(new DiplomaticSignatureRequest { transactionId = DiplomacyTx(context, "agreement-sign-b"), agreementId = agreementId, partyId = forgeParty, signerPersonId = "person.prototype.friend", worldTime = 12d });
            DiplomacyOperationResult ratify = runtime.RatifyAgreement(DiplomacyTx(context, "agreement-ratify"), agreementId, guildParty, $"organization-resolution.testlab.diplomacy.{context.RunId}", 13d);
            DiplomacyOperationResult activate = runtime.ActivateAgreement(DiplomacyTx(context, "agreement-activate"), agreementId, 14d);
            DiplomacyOperationResult breach = runtime.RecordBreach(new DiplomaticBreachRequest
            {
                transactionId = DiplomacyTx(context, "agreement-breach"),
                breachId = $"diplomatic-breach.testlab.defense.{context.RunId}",
                agreementId = agreementId,
                clauseId = clauseId,
                allegedActor = Org("organization.prototype.royal-forge"),
                state = DiplomaticBreachState.Confirmed,
                worldTime = 15d,
                notes = "Defense assistance did not arrive."
            });
            runtime.TryGetAgreement(agreementId, out DiplomaticAgreementRecordData saved);

            bool valid = draft.Succeeded
                && signA.Succeeded
                && signB.Succeeded
                && ratify.Succeeded
                && activate.Succeeded
                && breach.Succeeded
                && saved != null
                && saved.lifecycleState == DiplomaticAgreementLifecycleState.Active
                && saved.clauseIds.Contains(clauseId);
            return TestLabAssertions.True("step13-diplomacy-agreements", "Create agreement lifecycle and breach record", valid, $"Draft={draft.Code} Sign={signA.Code}/{signB.Code} Ratify={ratify.Code} Activate={activate.Code} Breach={breach.Code} State={saved?.lifecycleState}");
        }

        private static TestLabAutomationStepResult DiplomacyWarStatusIncidents(TestLabAutomationContext context)
        {
            if (!TryGetDiplomacyRuntime(context, out DiplomacyRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-diplomacy-war", "Declare and transition formal war", "DiplomacyRuntime", "Present", "Missing", failure);
            }

            string warId = $"diplomatic-war.testlab.guild-forge.{context.RunId}";
            DiplomacyOperationResult declare = runtime.DeclareWar(new DiplomaticWarDeclarationRequest
            {
                transactionId = DiplomacyTx(context, "war-declare"),
                warId = warId,
                warDefinitionId = PrototypeDiplomacyDefinitionFactory.FormalWarDefinitionId,
                title = "Guild Forge War",
                sideA = new[] { Org("organization.prototype.adventurers-guild") },
                sideB = new[] { Org("organization.prototype.royal-forge") },
                worldTime = 20d,
                declarationRecordId = $"diplomatic-record.testlab.war.declaration.{context.RunId}"
            });
            DiplomacyOperationResult duplicate = runtime.DeclareWar(new DiplomaticWarDeclarationRequest
            {
                transactionId = DiplomacyTx(context, "war-declare"),
                warId = warId,
                warDefinitionId = PrototypeDiplomacyDefinitionFactory.FormalWarDefinitionId,
                sideA = new[] { Org("organization.prototype.adventurers-guild") },
                sideB = new[] { Org("organization.prototype.royal-forge") },
                worldTime = 20d
            });
            DiplomacyOperationResult incident = runtime.RecordIncident(new DiplomaticIncidentRequest
            {
                transactionId = DiplomacyTx(context, "war-incident"),
                incidentId = $"diplomatic-incident.testlab.border.{context.RunId}",
                warId = warId,
                category = DiplomaticIncidentCategory.BorderIncident,
                sourceActor = Org("organization.prototype.adventurers-guild"),
                targetActor = Org("organization.prototype.royal-forge"),
                worldTime = 21d,
                publicSummary = "Border clash reported."
            });
            DiplomacyOperationResult ceasefire = runtime.TransitionWar(DiplomacyTx(context, "war-ceasefire"), warId, DiplomaticWarLifecycleState.Ceasefire, 22d, $"diplomatic-agreement.testlab.ceasefire.{context.RunId}");
            DiplomacyOperationResult peace = runtime.TransitionWar(DiplomacyTx(context, "war-peace"), warId, DiplomaticWarLifecycleState.Ended, 23d, $"diplomatic-agreement.testlab.peace.{context.RunId}");
            runtime.TryGetWar(warId, out DiplomaticWarRecordData saved);

            bool valid = declare.Succeeded
                && duplicate.Duplicate
                && incident.Succeeded
                && ceasefire.Succeeded
                && peace.Succeeded
                && saved != null
                && saved.lifecycleState == DiplomaticWarLifecycleState.Ended
                && saved.sideIds.Length == 2
                && saved.participationIds.Length == 2;
            return TestLabAssertions.True("step13-diplomacy-war", "Declare and transition formal war", valid, $"Declare={declare.Code} Duplicate={duplicate.Code}/{duplicate.Duplicate} Incident={incident.Code} Ceasefire={ceasefire.Code} Peace={peace.Code} State={saved?.lifecycleState}");
        }

        private static TestLabAutomationStepResult DiplomacyProjectionPersistenceValidation(TestLabAutomationContext context)
        {
            if (!TryGetDiplomacyRuntime(context, out DiplomacyRuntime runtime, out string failure))
            {
                return TestLabAssertions.Fail("step13-diplomacy-persistence", "Project, save, restore, and reject corrupt diplomacy graph", "DiplomacyRuntime", "Present", "Missing", failure);
            }

            string relationId = $"diplomatic-relation.testlab.secret.{context.RunId}";
            DiplomacyOperationResult relation = runtime.CreateRelation(Relation(context, "secret", PrototypeDiplomacyDefinitionFactory.CooperativeRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge"), visibility: DiplomaticVisibility.Secret));
            DiplomaticProjection redacted = runtime.GetProjection(relationId, privileged: false);
            DiplomaticProjection privileged = runtime.GetProjection(relationId, privileged: true);
            DiplomacyRuntimeSaveData save = runtime.CreateSaveData();
            DiplomacyRuntime restored = new DiplomacyRuntime();
            DiplomacyOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.Factions, context.ScenarioContext.Runtimes.OrganizationAuthority, context.ScenarioContext.Runtimes.OrganizationDecisions, context.ScenarioContext.Runtimes.OrganizationResources, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, restoring: true);
            DiplomacyRuntimeSaveData corrupt = save.Clone();
            corrupt.relations[0].targetActor.actorId = "organization.prototype.missing";
            bool rejected = !DiplomacyRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.Organizations, context.ScenarioContext.Runtimes.Factions, context.ScenarioContext.Runtimes.WorldId, context.ScenarioContext.Runtimes.KnownPersonIds, out string rejectFailure);

            bool valid = relation.Succeeded
                && redacted.Access == DiplomaticProjectionAccess.Redacted
                && privileged.Access == DiplomaticProjectionAccess.Privileged
                && restore.Succeeded
                && restored.RelationCount == runtime.RelationCount
                && rejected
                && !string.IsNullOrWhiteSpace(rejectFailure);
            restored.Dispose();
            return TestLabAssertions.True("step13-diplomacy-persistence", "Project, save, restore, and reject corrupt diplomacy graph", valid, $"Relation={relation.Code} Redacted={redacted.Access} Privileged={privileged.Access} Restore={restore.Code} Rejected={rejected} Failure={rejectFailure}");
        }

        private static FactionCreateRequest FactionCreate(TestLabAutomationContext context, string suffix, string definitionId, string name, FactionHostContextData host, FactionVisibility visibility = FactionVisibility.Public, bool preview = false)
        {
            return new FactionCreateRequest
            {
                transactionId = Tx(context, $"faction-create-{suffix}"),
                factionId = $"faction.testlab.{suffix}.{context.RunId}",
                factionDefinitionId = definitionId,
                officialName = name,
                publicDescription = $"{name} prototype faction.",
                hostContext = host?.Clone() ?? FactionHostContextData.Independent(),
                founderPersonId = PrimaryAuthorityActorId(context),
                founderOrganizationId = host?.primaryOrganizationId ?? string.Empty,
                worldTime = 1d,
                initialState = FactionLifecycleState.Active,
                visibility = visibility,
                tags = new[] { "testlab", "feature13.6" },
                preview = preview
            };
        }

        private static FactionAffiliationRequest FactionAffiliation(TestLabAutomationContext context, string suffix, string factionId, string personId, string definitionId, bool consent)
        {
            return new FactionAffiliationRequest
            {
                transactionId = Tx(context, $"faction-affiliation-{suffix}"),
                affiliationId = $"faction-affiliation.testlab.{suffix}.{context.RunId}",
                factionId = factionId,
                personId = personId,
                affiliationDefinitionId = definitionId,
                explicitConsent = consent,
                organizationContextId = "organization.prototype.adventurers-guild",
                worldTime = 2d,
                visibility = definitionId == PrototypeFactionDefinitionFactory.SecretMemberAffiliationId ? FactionVisibility.Secret : FactionVisibility.Public
            };
        }

        private static DiplomaticRelationRequest Relation(TestLabAutomationContext context, string suffix, string definitionId, DiplomaticActorReferenceData source, DiplomaticActorReferenceData target, DiplomaticVisibility visibility = DiplomaticVisibility.Public, bool preview = false)
        {
            return new DiplomaticRelationRequest
            {
                transactionId = DiplomacyTx(context, $"relation-{suffix}"),
                relationId = $"diplomatic-relation.testlab.{suffix}.{context.RunId}",
                relationDefinitionId = definitionId,
                sourceActor = source,
                targetActor = target,
                visibility = visibility,
                worldTime = 3d,
                publicSummary = $"Test Lab diplomacy relation {suffix}.",
                preview = preview
            };
        }

        private static DiplomaticAgreementPartyRecordData Party(string partyId, DiplomaticActorReferenceData actor, DiplomaticPartyRole role = DiplomaticPartyRole.Principal)
        {
            return new DiplomaticAgreementPartyRecordData
            {
                partyId = partyId,
                actor = actor,
                role = role,
                joinedWorldTime = 10d,
                active = true
            };
        }

        private static DiplomaticClauseRecordData Clause(string clauseId, string definitionId, DiplomaticClauseCategory category, DiplomaticVisibility visibility)
        {
            return new DiplomaticClauseRecordData
            {
                clauseId = clauseId,
                clauseDefinitionId = definitionId,
                category = category,
                lifecycleState = DiplomaticClauseLifecycleState.Draft,
                visibility = visibility,
                effectiveWorldTime = 10d,
                parameters = new[]
                {
                    new DiplomaticClauseParameterData
                    {
                        parameterId = "scope",
                        valueType = DiplomaticClauseParameterType.Text,
                        stringValue = "prototype diplomatic obligation"
                    }
                }
            };
        }

        private static DiplomaticActorReferenceData Org(string organizationId) => DiplomaticActorReferenceData.Organization(organizationId);
        private static DiplomaticActorReferenceData Faction(string factionId) => DiplomaticActorReferenceData.Faction(factionId);
        private static string DiplomacyTx(TestLabAutomationContext context, string suffix) => $"testlab.feature13.7.{suffix}.{context?.RunId ?? "run"}";
        private static string Tx(TestLabAutomationContext context, string suffix) => $"testlab.feature13.6.{suffix}.{context?.RunId ?? "run"}";

        private static OrganizationCreateRequest CreateGuildRequest(string organizationId, string name, string runId, bool preview = false)
        {
            return new OrganizationCreateRequest
            {
                organizationId = organizationId,
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                officialName = name,
                shortName = "Guild",
                aliases = new[] { "Guildhouse" },
                initialLifecycleState = OrganizationLifecycleState.Active,
                visibility = OrganizationVisibility.Public,
                transactionId = $"testlab.organization.create.{organizationId}.{runId}",
                preview = preview
            };
        }

        private static OrganizationMembershipRequest MembershipRequest(
            string membershipId,
            string organizationId,
            string personId,
            string membershipDefinitionId,
            OrganizationMembershipStatus targetStatus,
            OrganizationMembershipSourceKind sourceKind,
            string transactionId,
            bool consent = false)
        {
            return new OrganizationMembershipRequest
            {
                membershipId = membershipId,
                organizationId = organizationId,
                personId = personId,
                membershipDefinitionId = membershipDefinitionId,
                targetStatus = targetStatus,
                sourceKind = sourceKind,
                worldTime = 10d,
                explicitConsent = consent,
                visibility = OrganizationVisibility.Public,
                transactionId = transactionId
            };
        }

        private static OrganizationRankAssignmentRequest RankRequest(string assignmentId, string membershipId, string rankDefinitionId, string transactionId)
        {
            return new OrganizationRankAssignmentRequest
            {
                rankAssignmentId = assignmentId,
                membershipId = membershipId,
                rankDefinitionId = rankDefinitionId,
                worldTime = 20d,
                assignedById = PersistenceService.LocalPlayerId,
                transactionId = transactionId
            };
        }

        private static OrganizationOfficeRequest OfficeRequest(string officeId, string organizationId, string officeDefinitionId, string transactionId, int maximumHolders = 0)
        {
            return new OrganizationOfficeRequest
            {
                officeId = officeId,
                organizationId = organizationId,
                officeDefinitionId = officeDefinitionId,
                maximumActiveHolders = maximumHolders,
                vacancyAllowed = true,
                worldTime = 30d,
                visibility = OrganizationVisibility.Public,
                transactionId = transactionId
            };
        }

        private static OrganizationOfficeAssignmentRequest OfficeAssignmentRequest(string assignmentId, string officeId, string membershipId, string transactionId, bool acting = false)
        {
            return new OrganizationOfficeAssignmentRequest
            {
                officeAssignmentId = assignmentId,
                officeId = officeId,
                membershipId = membershipId,
                acting = acting,
                worldTime = 40d,
                appointedById = PersistenceService.LocalPlayerId,
                transactionId = transactionId
            };
        }

        private static OrganizationAuthorizationRequest AuthorityRequest(string personId, string organizationId, string actionDefinitionId, string operationId, double worldTime = 100d)
        {
            return new OrganizationAuthorizationRequest
            {
                actorPersonId = personId,
                organizationId = organizationId,
                actionDefinitionId = actionDefinitionId,
                operationId = operationId,
                scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                worldTime = worldTime
            };
        }

        private static OrganizationApprovalRequest ApprovalRequest(string approvalId, string operationId, string approverId)
        {
            return new OrganizationApprovalRequest
            {
                approvalId = approvalId,
                operationId = operationId,
                organizationId = "organization.prototype.adventurers-guild",
                actionDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId,
                approverPersonId = approverId,
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                approvedWorldTime = 90d,
                transactionId = $"tx.{approvalId}"
            };
        }

        private static void CreateAuthorityGuildmaster(TestLabAutomationContext context, string personId, string suffix)
        {
            OrganizationMembershipRuntime memberships = context.ScenarioContext.Runtimes.OrganizationMemberships;
            string membershipId = $"organization-membership.testlab.authority.guildmaster.{suffix}.{context.RunId}";
            OrganizationMembershipOperationResult member = memberships.ApplyMembership(MembershipRequest(membershipId, "organization.prototype.adventurers-guild", personId, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, $"testlab.authority.guildmaster.member.{suffix}.{context.RunId}", consent: true));
            AssignGuildMasterRank(memberships, member.Membership?.MembershipId, context.RunId, suffix);
            OrganizationMembershipOperationResult office = memberships.CreateOffice(OfficeRequest($"organization-office-record.testlab.guildmaster.{suffix}.{context.RunId}", "organization.prototype.adventurers-guild", PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, $"testlab.authority.guildmaster.office.{suffix}.{context.RunId}"));
            memberships.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.testlab.guildmaster.{suffix}.{context.RunId}", office.Office?.OfficeId, member.Membership?.MembershipId, $"testlab.authority.guildmaster.office.assign.{suffix}.{context.RunId}"));
        }

        private static void AssignGuildMasterRank(OrganizationMembershipRuntime memberships, string membershipId, string runId, string suffix)
        {
            if (string.IsNullOrWhiteSpace(membershipId))
            {
                return;
            }

            for (int index = 0; index < PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.Count; index++)
            {
                memberships.AssignRank(RankRequest($"organization-rank-assignment.testlab.rating.{index}.{suffix}.{runId}", membershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds[index], $"testlab.authority.rank.rating.{index}.{suffix}.{runId}"));
            }
        }

        private static OrganizationAuthorityOperationResult GrantGuildmasterRole(OrganizationAuthorityRuntime authority, string grantorPersonId, string granteePersonId, string runId, string suffix)
        {
            return authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = $"organization-authority-grant.testlab.guildmaster.{suffix}.{runId}",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = granteePersonId,
                grantorPersonId = grantorPersonId,
                authorityRoleDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                transactionId = $"testlab.authority.guildmaster.grant.{suffix}.{runId}"
            });
        }

        private static string PrimaryAuthorityActorId(TestLabAutomationContext context)
        {
            string personId = context?.ScenarioContext?.Runtimes?.PersonId;
            return string.IsNullOrWhiteSpace(personId) ? PersistenceService.LocalPlayerId : personId;
        }

        private static Step13InstitutionalIntegrationFacade CreateIntegrationFacade(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            return new Step13InstitutionalIntegrationFacade(
                runtimes?.DefinitionRegistry,
                runtimes?.WorldId,
                runtimes?.KnownPersonIds ?? Array.Empty<string>(),
                Array.Empty<string>(),
                runtimes?.Organizations,
                runtimes?.OrganizationMemberships,
                runtimes?.OrganizationAuthority,
                runtimes?.OrganizationResources,
                runtimes?.OrganizationDecisions,
                runtimes?.Factions,
                runtimes?.Diplomacy,
                runtimes?.Governments,
                runtimes?.Laws,
                runtimes?.Crimes,
                runtimes?.Justice,
                runtimes?.Economy,
                runtimes?.Properties,
                runtimes?.Businesses,
                runtimes?.ItemInstances);
        }

        private static bool TryGetRuntime(TestLabAutomationContext context, out OrganizationRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Organizations;
            if (runtime == null)
            {
                failure = "OrganizationRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetMembershipRuntime(TestLabAutomationContext context, out OrganizationMembershipRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.OrganizationMemberships;
            if (runtime == null)
            {
                failure = "OrganizationMembershipRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetAuthorityRuntime(TestLabAutomationContext context, out OrganizationAuthorityRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.OrganizationAuthority;
            if (runtime == null)
            {
                failure = "OrganizationAuthorityRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetFactionRuntime(TestLabAutomationContext context, out FactionRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Factions;
            if (runtime == null)
            {
                failure = "FactionRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetDiplomacyRuntime(TestLabAutomationContext context, out DiplomacyRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Diplomacy;
            if (runtime == null)
            {
                failure = "DiplomacyRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetGovernmentRuntime(TestLabAutomationContext context, out GovernmentRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Governments;
            if (runtime == null)
            {
                failure = "GovernmentRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetLegalRuntime(TestLabAutomationContext context, out LegalRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Laws;
            if (runtime == null)
            {
                failure = "LegalRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetCrimeRuntime(TestLabAutomationContext context, out CrimeRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Crimes;
            if (runtime == null)
            {
                failure = "CrimeRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetJusticeRuntime(TestLabAutomationContext context, out JusticeRuntime runtime, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.Justice;
            if (runtime == null)
            {
                failure = "JusticeRuntime is missing from the Test Lab runtime bundle.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
#endif
