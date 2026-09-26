using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Organizations.Integration;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Gameplay
{
    public sealed partial class PrototypePersistenceServiceBehaviour
    {
        [Header("Institutional Simulation")]
        [SerializeField] private Step13InstitutionalSimulationSettings institutionalSimulationSettings = new Step13InstitutionalSimulationSettings();

        private bool configuringInstitutionalRuntime;
        private string institutionalConfigurationFingerprint = string.Empty;
        private Step13InstitutionalSimulationCoordinator institutionalSimulation;
        private Step13InstitutionalIntegrationFacade institutionalIntegration;
        private Step13InstitutionalTransactionCoordinator institutionalTransactions;
        private readonly HashSet<string> institutionalObservedPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private double nextInstitutionalConfigurationCheckRealtime;
        private long lastReconciledLegalRevision = -1L;

        public Step13InstitutionalIntegrationFacade InstitutionalIntegration
        {
            get { EnsureGroup10InstitutionalRuntime(); return institutionalIntegration; }
        }

        public Step13InstitutionalTransactionCoordinator InstitutionalTransactions
        {
            get { EnsureGroup10InstitutionalRuntime(); return institutionalTransactions; }
        }

        public IReadOnlyList<WantedStatusRecordData> ActivePlayerWantedStatuses
        {
            get
            {
                EnsureGroup10InstitutionalRuntime();
                string playerPersonId = ResolvePlayerPersonId();
                return worldCrimes.WantedStatuses
                    .Where(status => status.lifecycleState == WantedStatusLifecycleState.Active && string.Equals(status.subjectId, playerPersonId, StringComparison.Ordinal))
                    .ToArray();
            }
        }

        public bool IsPlayerWanted => ActivePlayerWantedStatuses.Count > 0;

        public bool IsPlayerRegisteredAsAdventurer
        {
            get
            {
                EnsureGroup10InstitutionalRuntime();
                return worldOrganizationMemberships != null
                    && worldOrganizationMemberships.TryGetMembership(PrototypeInstitutionalContentIds.PlayerGuildMembershipId(ResolvePlayerPersonId()), out OrganizationMembershipSnapshot membership)
                    && membership.IsActive
                    && string.Equals(membership.OrganizationId, PrototypeInstitutionalContentIds.AdventurersGuild, StringComparison.Ordinal);
            }
        }

        public PrototypeAdventurerRegistrationResult RegisterPlayerAsAdventurerAtGuildDesk(string interactionPointId)
        {
            EnsureGroup10InstitutionalRuntime();
            string playerId = ResolvePlayerPersonId();
            string membershipId = PrototypeInstitutionalContentIds.PlayerGuildMembershipId(playerId);
            if (!string.Equals(interactionPointId, PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, StringComparison.Ordinal))
            {
                return PrototypeAdventurerRegistrationResult.Failure("Adventurer registration is only available at the Adventurers Guild counter.");
            }

            bool receptionistAssigned = worldInteractionPoints != null
                && worldInteractionPoints.GetProviderAssignments(interactionPointId, includeHidden: true).Any(assignment =>
                    assignment.IsActive
                    && string.Equals(assignment.ServiceDefinitionId, PrototypeInteractionPointDefinitionFactory.RegisterAdventurerServiceId, StringComparison.Ordinal)
                    && string.Equals(assignment.ProviderEntity?.entityId, PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson, StringComparison.Ordinal));
            if (!receptionistAssigned)
            {
                return PrototypeAdventurerRegistrationResult.Failure("The Adventurers Guild receptionist is not available to process registration.");
            }

            if (worldOrganizationMemberships.TryGetMembership(membershipId, out OrganizationMembershipSnapshot existing) && existing.IsActive)
            {
                OrganizationRankAssignmentRecordData activeRank = existing.RankAssignments.SingleOrDefault(rank => rank.IsActive);
                string rankLabel = activeRank == null ? "registered" : activeRank.rankDefinitionId == PrototypeOrganizationMembershipDefinitionFactory.AdventurerGuildEntryRankId ? "G-rank" : "ranked";
                return PrototypeAdventurerRegistrationResult.ExistingRegistration(existing, $"You are already registered with the Adventurers Guild ({rankLabel}).");
            }

            if (playerSkills == null || !playerSkills.TryGetLearnedSkillOfType(SkillType.Combat, out SkillDefinition qualifyingCombatSkill))
            {
                return PrototypeAdventurerRegistrationResult.Failure("Adventurer registration requires at least one learned combat-type skill.");
            }

            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            string operationSuffix = $"{NormalizeRuntimeRecordSuffix(playerId)}.{worldOrganizationMemberships.Revision + 1}";
            OrganizationAuthorizationResult authorization = worldOrganizationAuthority.EvaluateAuthorization(new OrganizationAuthorizationRequest
            {
                operationId = $"adventurer-registration.authorize.{operationSuffix}",
                actorPersonId = PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson,
                organizationId = PrototypeInstitutionalContentIds.AdventurersGuild,
                actionDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.AdmitMemberActionId,
                scope = OrganizationAuthorityScopeData.ForOrganization(PrototypeInstitutionalContentIds.AdventurersGuild),
                targetPersonId = playerId,
                targetRecordId = membershipId,
                worldTime = worldTime
            });
            if (!authorization.Succeeded)
            {
                return PrototypeAdventurerRegistrationResult.Failure($"The guild receptionist cannot complete registration: {authorization.Message}");
            }

            OrganizationMembershipRuntimeSaveData rollback = worldOrganizationMemberships.CreateSaveData();
            OrganizationMembershipOperationResult membership = worldOrganizationMemberships.ApplyMembership(new OrganizationMembershipRequest
            {
                transactionId = $"adventurer-registration.membership.{operationSuffix}",
                membershipId = membershipId,
                organizationId = PrototypeInstitutionalContentIds.AdventurersGuild,
                personId = playerId,
                membershipDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                targetStatus = OrganizationMembershipStatus.Active,
                sourceKind = OrganizationMembershipSourceKind.Application,
                explicitConsent = true,
                sourceEventId = interactionPointId,
                sourceRecordId = PrototypeInteractionPointDefinitionFactory.RegisterAdventurerServiceId,
                provenanceId = "adventurers-guild-counter-registration",
                tags = new[] { "adventurer", "registered-at-guild-counter", $"qualified-by.{qualifyingCombatSkill.Id}" },
                worldTime = worldTime
            });
            if (!membership.Succeeded)
            {
                return PrototypeAdventurerRegistrationResult.Failure($"Adventurer registration failed: {membership.Message}");
            }

            OrganizationMembershipOperationResult rank = worldOrganizationMemberships.AssignRank(new OrganizationRankAssignmentRequest
            {
                transactionId = $"adventurer-registration.rank-g.{operationSuffix}",
                rankAssignmentId = $"organization-rank-assignment.prototype.adventurers-guild.{NormalizeRuntimeRecordSuffix(playerId)}.g.{worldOrganizationMemberships.Revision + 1}",
                membershipId = membershipId,
                rankDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.AdventurerGuildEntryRankId,
                assignedById = PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson,
                sourceEventId = interactionPointId,
                sourceRecordId = PrototypeInteractionPointDefinitionFactory.RegisterAdventurerServiceId,
                provenanceId = "adventurers-guild-counter-registration",
                worldTime = worldTime
            });
            if (!rank.Succeeded)
            {
                worldOrganizationMemberships.RestoreFromSaveData(rollback, GetDefinitionRegistry(), worldOrganizations, playerService == null ? UnityIsekaiGame.GameData.Persistence.PersistenceService.LocalWorldId : playerService.WorldId, GetPrototypeSocialPersonIds(playerId), GetPrototypeOrganizations());
                return PrototypeAdventurerRegistrationResult.Failure($"Adventurer registration could not assign G rank: {rank.Message}");
            }

            dirtyTracker?.MarkDirty("Player registered with the Adventurers Guild at G rank.");
            return PrototypeAdventurerRegistrationResult.Success(rank.Membership, "Registration complete. You are now a G-rank adventurer.");
        }

        private void EnsureGroup10InstitutionalRuntime()
        {
            if (configuringInstitutionalRuntime) return;
            double realtime = Time.unscaledTimeAsDouble;
            if (worldOrganizations != null && worldJustice != null && realtime < nextInstitutionalConfigurationCheckRealtime) return;
            nextInstitutionalConfigurationCheckRealtime = realtime + 1d;
            configuringInstitutionalRuntime = true;
            try
            {
                var registry = GetDefinitionRegistry();
                if (registry == null) return;
                string player = ResolvePlayerPersonId();
                string world = playerService == null ? UnityIsekaiGame.GameData.Persistence.PersistenceService.LocalWorldId : playerService.WorldId;
                string[] people = GetPrototypeSocialPersonIds(player);
                string[] places = GetKnownPlaceIds();

                worldOrganizations ??= new OrganizationRuntime();
                worldOrganizationMemberships ??= new OrganizationMembershipRuntime();
                worldOrganizationAuthority ??= new OrganizationAuthorityRuntime();
                worldOrganizationResources ??= new OrganizationResourceRuntime();
                worldOrganizationDecisions ??= new OrganizationDecisionRuntime();
                worldFactions ??= new UnityIsekaiGame.Factions.FactionRuntime();
                worldDiplomacy ??= new UnityIsekaiGame.Diplomacy.DiplomacyRuntime();
                worldGovernments ??= new UnityIsekaiGame.Governments.GovernmentRuntime();
                worldLaws ??= new UnityIsekaiGame.Laws.LegalRuntime();
                worldCrimes ??= new UnityIsekaiGame.Crimes.CrimeRuntime();
                worldJustice ??= new UnityIsekaiGame.Justice.JusticeRuntime();

                string[] organizationsBefore = GetPrototypeOrganizations();
                string fingerprint = $"{world}|{string.Join(",", people)}|{string.Join(",", places)}|{string.Join(",", organizationsBefore)}";
                if (!string.Equals(fingerprint, institutionalConfigurationFingerprint, StringComparison.Ordinal))
                {
                    PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(worldOrganizations, registry, world, people, places);
                    string[] organizationIds = GetPrototypeOrganizations();
                    worldOrganizationMemberships.Configure(registry, worldOrganizations, world, people, organizationIds);
                    worldOrganizationAuthority.Configure(registry, worldOrganizations, worldOrganizationMemberships, world, people, organizationIds);
                    worldOrganizationResources.Configure(registry, worldOrganizations, worldOrganizationAuthority, Economy, world, Properties, Businesses, ItemIdentities, ContractEconomy, Payroll);
                    worldOrganizationDecisions.Configure(registry, worldOrganizations, worldOrganizationMemberships, worldOrganizationAuthority, worldOrganizationResources, world, people, Economy);
                    worldFactions.Configure(registry, worldOrganizations, worldOrganizationMemberships, worldOrganizationAuthority, worldOrganizationResources, worldOrganizationDecisions, world, people);
                    worldDiplomacy.Configure(registry, worldOrganizations, worldFactions, worldOrganizationAuthority, worldOrganizationDecisions, worldOrganizationResources, world, people);
                    worldGovernments.Configure(registry, worldOrganizations, worldOrganizationMemberships, worldOrganizationAuthority, worldOrganizationDecisions, worldOrganizationResources, worldFactions, worldDiplomacy, Properties, world, people, places, FamilyRelationships, Economy, InstitutionalRevenue);
                    worldLaws.Configure(registry, worldGovernments, worldOrganizations, worldOrganizationAuthority, worldOrganizationDecisions, worldDiplomacy, Properties, world, people, places);
                    worldCrimes.Configure(registry, worldGovernments, worldLaws, worldOrganizationAuthority, worldDiplomacy, world, people, places);
                    worldJustice.Configure(registry, worldGovernments, worldLaws, worldOrganizations, worldOrganizationAuthority, worldCrimes, world, people, places);

                    double worldTime = playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds;
                    PrototypeInstitutionalBootstrapReport bootstrap = PrototypeInstitutionalWorldBootstrap.EnsureSeeded(registry, player, places, worldOrganizations, worldOrganizationMemberships, worldOrganizationAuthority, worldOrganizationResources, worldGovernments, worldLaws, worldJustice, FamilyRelationships, worldTime);
                    foreach (string diagnostic in bootstrap.Diagnostics) Debug.LogWarning(diagnostic);

                    institutionalIntegration = new Step13InstitutionalIntegrationFacade(registry, world, people, places, worldOrganizations, worldOrganizationMemberships, worldOrganizationAuthority, worldOrganizationResources, worldOrganizationDecisions, worldFactions, worldDiplomacy, worldGovernments, worldLaws, worldCrimes, worldJustice, Economy, Properties, Businesses, ItemIdentities);
                    Step13IntegrationValidationReport validation = institutionalIntegration.ValidateComplete();
                    foreach (Step13IntegrationDiagnostic diagnostic in validation.Diagnostics.Where(item => item.Severity != Step13IntegrationDiagnosticSeverity.Info))
                    {
                        if (diagnostic.Severity == Step13IntegrationDiagnosticSeverity.Error) Debug.LogError(diagnostic.ToString());
                        else Debug.LogWarning(diagnostic.ToString());
                    }
                    institutionalTransactions ??= new Step13InstitutionalTransactionCoordinator();
                    institutionalSimulation ??= new Step13InstitutionalSimulationCoordinator(reason => dirtyTracker?.MarkDirty(reason), institutionalSimulationSettings);
                    institutionalSimulation.Rebind(worldOrganizations, worldOrganizationMemberships, worldOrganizationAuthority, worldOrganizationResources, worldOrganizationDecisions, worldFactions, worldDiplomacy, worldGovernments, worldLaws, worldCrimes, worldJustice, institutionalSimulationSettings);
                    institutionalConfigurationFingerprint = $"{world}|{string.Join(",", people)}|{string.Join(",", places)}|{string.Join(",", GetPrototypeOrganizations())}";
                    lastReconciledLegalRevision = worldLaws.Revision;
                }
                else if (worldLaws.Revision != lastReconciledLegalRevision
                    || PrototypeInstitutionalWorldBootstrap.NeedsAuthoredLawReconciliation(registry, worldLaws))
                {
                    PrototypeInstitutionalBootstrapReport reconciliation = new PrototypeInstitutionalBootstrapReport();
                    double worldTime = playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds;
                    PrototypeInstitutionalWorldBootstrap.ReconcileAuthoredLaws(registry, worldLaws, reconciliation, worldTime);
                    foreach (string diagnostic in reconciliation.Diagnostics) Debug.LogWarning(diagnostic);
                    lastReconciledLegalRevision = worldLaws.Revision;
                }
            }
            finally
            {
                configuringInstitutionalRuntime = false;
            }
        }

        private void AdvanceGroup10InstitutionalSimulation()
        {
            EnsureGroup10InstitutionalRuntime();
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            institutionalSimulation?.Advance(worldTime);
        }

        private bool SynchronizeEmploymentOrganizationMemberships(string personId, double worldTime)
        {
            EnsureGroup10InstitutionalRuntime();
            long before = worldOrganizationMemberships.Revision;
            foreach (EmploymentRecordData employment in PositionEmployment.QueryEmploymentByPerson(personId, activeOnly: false))
            {
                if (string.IsNullOrWhiteSpace(employment.employerOrganizationId)) continue;
                OrganizationSnapshot organization = worldOrganizations.Snapshots.FirstOrDefault(item => string.Equals(item.OrganizationId, employment.employerOrganizationId, StringComparison.Ordinal));
                string definitionId = MembershipDefinitionForOrganization(organization?.DefinitionId);
                if (string.IsNullOrEmpty(definitionId)) continue;
                bool active = employment.state is EmploymentState.Accepted or EmploymentState.Active or EmploymentState.Probationary or EmploymentState.OnLeaveFoundation or EmploymentState.Suspended;
                string membershipId = $"organization-membership.employment.{employment.employmentId}";
                OrganizationMembershipStatus target = active ? (employment.state == EmploymentState.Suspended ? OrganizationMembershipStatus.Suspended : OrganizationMembershipStatus.Active) : OrganizationMembershipStatus.Resigned;
                if (worldOrganizationMemberships.TryGetMembership(membershipId, out OrganizationMembershipSnapshot current) && current.Status == target) continue;
                worldOrganizationMemberships.ApplyMembership(new OrganizationMembershipRequest
                {
                    transactionId = $"institution.employment-membership.{employment.employmentId}.{target}",
                    membershipId = membershipId,
                    organizationId = employment.employerOrganizationId,
                    personId = employment.personId,
                    membershipDefinitionId = definitionId,
                    targetStatus = target,
                    sourceKind = OrganizationMembershipSourceKind.EmploymentReference,
                    explicitConsent = true,
                    employmentId = employment.employmentId,
                    worldTime = worldTime,
                    endingPolicy = OrganizationMembershipEndingPolicy.EndActiveAssignments,
                    provenanceId = "profession-employment-bridge"
                });
            }
            return worldOrganizationMemberships.Revision != before;
        }

        private static string MembershipDefinitionForOrganization(string organizationDefinitionId) => organizationDefinitionId switch
        {
            PrototypeOrganizationDefinitionFactory.GuildDefinitionId => PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
            PrototypeOrganizationDefinitionFactory.CompanyDefinitionId => PrototypeOrganizationMembershipDefinitionFactory.ForgeEmployeeMemberId,
            PrototypeOrganizationDefinitionFactory.ReligiousOrderDefinitionId => PrototypeOrganizationMembershipDefinitionFactory.TempleClergyMemberId,
            PrototypeOrganizationDefinitionFactory.MilitaryOrderDefinitionId => PrototypeOrganizationMembershipDefinitionFactory.MilitaryMemberId,
            PrototypeOrganizationDefinitionFactory.CivicBodyDefinitionId => PrototypeOrganizationMembershipDefinitionFactory.CivicOfficialMemberId,
            _ => string.Empty
        };

        private void RecordKillingCrime(string allegedActorId, string victimId, string combatTransactionId)
        {
            RecordPersonLawViolation(
                allegedActorId,
                victimId,
                combatTransactionId,
                "killing",
                CrimeIncidentCategory.ViolentIncident,
                "crime.killing",
                "combat.death",
                OffenseElementKind.ResultOccurred,
                "death",
                "dead",
                "dead",
                string.Empty);
        }

        public bool RecordTheftCrime(string allegedActorId, string ownerPersonId, string propertyReferenceId, string sourceTransactionId, string placeId = "")
        {
            if (string.IsNullOrWhiteSpace(ownerPersonId) || string.Equals(allegedActorId, ownerPersonId, StringComparison.Ordinal)) return false;
            if (!string.IsNullOrWhiteSpace(allegedActorId)) institutionalObservedPersonIds.Add(allegedActorId.Trim());
            institutionalObservedPersonIds.Add(ownerPersonId.Trim());
            nextInstitutionalConfigurationCheckRealtime = 0d;
            return RecordPersonLawViolation(
                allegedActorId,
                ownerPersonId,
                sourceTransactionId,
                "theft",
                CrimeIncidentCategory.PropertyIncident,
                "crime.theft",
                propertyReferenceId,
                OffenseElementKind.ItemPossessionChanged,
                "item-possession",
                ownerPersonId,
                allegedActorId,
                placeId);
        }

        public bool ReportCrimeIncident(string incidentId, string reporterPersonId, string reportTransactionId, bool firstHand = true, int reporterReliabilityBasisPoints = 8000)
        {
            return ProcessCrimeReport(incidentId, reporterPersonId, reportTransactionId, firstHand, reporterReliabilityBasisPoints, authorityDiscovery: false);
        }

        public bool DiscoverCrimeIncidentByAuthority(string incidentId, string authorityPersonId, string discoveryTransactionId)
        {
            if (!IsPrototypeLawAuthority(authorityPersonId)) return false;
            return ProcessCrimeReport(incidentId, authorityPersonId, discoveryTransactionId, firstHand: true, reporterReliabilityBasisPoints: 10000, authorityDiscovery: true);
        }

        private bool RecordPersonLawViolation(
            string allegedActorId,
            string victimId,
            string sourceTransactionId,
            string recordKind,
            CrimeIncidentCategory incidentCategory,
            string actionId,
            string provenanceId,
            OffenseElementKind resultElementKind,
            string resultElementKey,
            string expectedValue,
            string observedValue,
            string placeIdOverride)
        {
            EnsureGroup10InstitutionalRuntime();
            string placeId = string.IsNullOrWhiteSpace(placeIdOverride)
                ? currentPlaceTracker == null ? string.Empty : currentPlaceTracker.CurrentPlaceId
                : placeIdOverride.Trim();
            if (string.IsNullOrWhiteSpace(placeId)
                || string.IsNullOrWhiteSpace(allegedActorId)
                || string.IsNullOrWhiteSpace(victimId))
            {
                return false;
            }

            string suffix = NormalizeRuntimeRecordSuffix(sourceTransactionId);
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            string territoryId = worldGovernments.Territories
                .Where(territory => territory.placeIds.Contains(placeId, StringComparer.Ordinal))
                .OrderBy(territory => territory.territoryId, StringComparer.Ordinal)
                .Select(territory => territory.territoryId)
                .FirstOrDefault() ?? string.Empty;
            LegalApplicabilityResult applicability = worldLaws.Evaluate(new LegalApplicabilityRequest { personId = allegedActorId, territoryId = territoryId, placeId = placeId, actionId = actionId, worldTime = worldTime });
            LegalProvisionRecordData provision = applicability.ApplicableProvisions.FirstOrDefault();
            LawRuleDefinition rule = GetDefinitionRegistry().DefinitionsById.Values.OfType<LawRuleDefinition>().FirstOrDefault(candidate => candidate.ProvisionId == provision?.provisionId);
            if (applicability.Status != LegalApplicabilityStatus.Prohibited || rule == null) return false;

            string incidentId = $"crime-incident.{recordKind}.{suffix}";
            string offenseId = $"potential-offense.{recordKind}.{suffix}";
            long before = worldCrimes.Revision;
            CrimeRuntimeSaveData rollback = worldCrimes.CreateSaveData();

            CrimeOperationResult incident = worldCrimes.RecordIncident(new CrimeIncidentRequest
            {
                transactionId = $"institution.{recordKind}.incident.{suffix}",
                incidentId = incidentId,
                category = incidentCategory,
                occurrenceStartWorldTime = worldTime,
                occurrenceEndWorldTime = worldTime,
                discoveryWorldTime = -1d,
                reportingWorldTime = -1d,
                primaryPlaceId = placeId,
                primaryTerritoryId = territoryId,
                involvedSubjects = new[] { CrimeSubjectReferenceData.Person(allegedActorId, "alleged-actor"), CrimeSubjectReferenceData.Person(victimId, "victim") },
                jurisdictionIds = rule.JurisdictionIds.ToArray(),
                victimIds = new[] { victimId },
                witnessIds = Array.Empty<string>(),
                visibility = PoliticalVisibility.Restricted,
                provenanceId = provenanceId
            });
            if (!incident.Succeeded) { Debug.LogWarning($"{recordKind} crime incident was not recorded: {incident.Message}"); return false; }

            CrimeOperationResult offense = worldCrimes.EvaluatePotentialOffense(new PotentialOffenseEvaluationRequest
            {
                transactionId = $"institution.{recordKind}.offense.{suffix}",
                potentialOffenseId = offenseId,
                incidentId = incidentId,
                offenseDefinitionId = rule.OffenseDefinitionId,
                allegedActorIds = new[] { allegedActorId },
                victimOrTargetIds = new[] { victimId },
                actionId = actionId,
                stage = OffenseStage.Completed,
                participation = ParticipationCategory.PrincipalActor,
                evidenceSufficiency = EvidenceSufficiencyState.Weak,
                elementEvaluations = new[]
                {
                    new OffenseElementEvaluationData { kind = OffenseElementKind.ActorConduct, key = "conduct", expectedValue = actionId, observedValue = actionId, supported = true, evidenceId = $"evidence.{recordKind}.{suffix}" },
                    new OffenseElementEvaluationData { kind = resultElementKind, key = resultElementKey, expectedValue = expectedValue, observedValue = observedValue, supported = true, evidenceId = $"evidence.{recordKind}.{suffix}" }
                },
                visibility = PoliticalVisibility.Restricted,
                provenanceId = provenanceId
            });
            if (!offense.Succeeded)
            {
                RestoreCrimeWorkflow(rollback);
                Debug.LogWarning($"{recordKind} offense was not evaluated; the incident was rolled back: {offense.Message}");
                return false;
            }
            if (worldCrimes.Revision != before) dirtyTracker?.MarkDirty($"institution.{recordKind}-crime");
            return offense.Succeeded;
        }

        private bool ProcessCrimeReport(string incidentId, string reporterPersonId, string sourceTransactionId, bool firstHand, int reporterReliabilityBasisPoints, bool authorityDiscovery)
        {
            EnsureGroup10InstitutionalRuntime();
            if (string.IsNullOrWhiteSpace(incidentId)
                || string.IsNullOrWhiteSpace(reporterPersonId)
                || !worldCrimes.TryGetIncident(incidentId, out CrimeIncidentRecordData incident)) return false;
            PotentialOffenseRecordData offense = worldCrimes.PotentialOffenses.FirstOrDefault(candidate => candidate.incidentId == incident.incidentId && candidate.legalApplicabilityStatus == LegalApplicabilityStatus.Prohibited);
            if (offense == null) return false;
            LawRuleDefinition rule = GetDefinitionRegistry().DefinitionsById.Values.OfType<LawRuleDefinition>().FirstOrDefault(candidate => candidate.ProvisionId == offense.legalProvisionId);
            string allegedActorId = offense.allegedActorIds.FirstOrDefault();
            if (rule == null || string.IsNullOrWhiteSpace(allegedActorId)) return false;

            string suffix = NormalizeRuntimeRecordSuffix(sourceTransactionId);
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            int reliability = Mathf.Clamp(reporterReliabilityBasisPoints, 0, 10000);
            string reportId = $"crime-report.{suffix}";
            string allegationId = $"crime-allegation.{suffix}";
            CrimeRuntimeSaveData rollback = worldCrimes.CreateSaveData();
            CrimeOperationResult report = worldCrimes.SubmitReport(new CrimeReportRequest
            {
                transactionId = $"institution.crime-report.{suffix}",
                reportId = reportId,
                incidentId = incident.incidentId,
                category = authorityDiscovery
                    ? CrimeReportCategory.OfficialReport
                    : incident.victimIds.Contains(reporterPersonId, StringComparer.Ordinal)
                        ? CrimeReportCategory.VictimReport
                        : firstHand ? CrimeReportCategory.WitnessReport : CrimeReportCategory.DelayedReport,
                reporterSubjectId = reporterPersonId,
                reporterSubjectType = "Person",
                firstHand = firstHand,
                submittedWorldTime = worldTime,
                reporterReliabilityBasisPoints = reliability,
                visibility = PoliticalVisibility.Restricted,
                provenanceId = authorityDiscovery ? "authority.discovery" : "person.report"
            });
            if (!report.Succeeded) return false;

            EvidenceSufficiencyState sufficiency = authorityDiscovery ? EvidenceSufficiencyState.ThresholdMet : EvidenceSufficiencyState.Partial;
            CrimeOperationResult allegation = worldCrimes.RecordAllegation(new CrimeAllegationRequest
            {
                transactionId = $"institution.crime-allegation.{suffix}",
                allegationId = allegationId,
                incidentId = incident.incidentId,
                reportId = reportId,
                potentialOffenseId = offense.potentialOffenseId,
                claimedActorId = allegedActorId,
                claimedVictimId = offense.victimOrTargetIds.FirstOrDefault() ?? string.Empty,
                claimedTargetId = offense.victimOrTargetIds.FirstOrDefault() ?? string.Empty,
                conductSummary = authorityDiscovery ? "An authorized official discovered the prohibited conduct." : "A person reported the prohibited conduct.",
                sufficiency = sufficiency,
                visibility = PoliticalVisibility.Restricted,
                provenanceId = authorityDiscovery ? "authority.discovery" : "person.report"
            });
            if (!allegation.Succeeded)
            {
                RestoreCrimeWorkflow(rollback);
                return false;
            }

            bool credible = authorityDiscovery || reliability >= rule.CredibleReportReliabilityBasisPoints;
            if (!credible)
            {
                dirtyTracker?.MarkDirty("institution.crime-report");
                return true;
            }

            bool worldwide = rule.ScopeKind == LawScopeKind.Worldwide;
            string enforcementJurisdictionId = worldwide ? string.Empty : rule.JurisdictionIds.FirstOrDefault() ?? string.Empty;
            string enforcementTerritoryId = worldwide ? string.Empty : offense.territoryId;
            string[] enforcementPlaceIds = worldwide || rule.ScopeKind != LawScopeKind.Place ? Array.Empty<string>() : rule.PlaceIds.ToArray();
            string wantedDefinitionId = authorityDiscovery ? rule.AuthorityDiscoveredWantedDefinitionId : rule.ReportedWantedDefinitionId;
            string scopeSuffix = worldwide ? "world" : NormalizeRuntimeRecordSuffix(enforcementJurisdictionId);
            string subjectSuffix = NormalizeRuntimeRecordSuffix(allegedActorId);
            CrimeOperationResult wanted = worldCrimes.UpsertWantedStatus(new WantedStatusUpsertRequest
            {
                transactionId = $"institution.wanted.{suffix}",
                wantedStatusId = $"wanted-status.{scopeSuffix}.{subjectSuffix}",
                wantedDefinitionId = wantedDefinitionId,
                incidentId = incident.incidentId,
                subjectId = allegedActorId,
                subjectType = "Person",
                jurisdictionId = enforcementJurisdictionId,
                territoryId = enforcementTerritoryId,
                placeIds = enforcementPlaceIds,
                risk = rule.ActionId == "crime.killing" ? WantedRiskAssessment.PotentiallyArmed : WantedRiskAssessment.Nonviolent,
                activeWorldTime = worldTime,
                expirationWorldTime = -1d,
                visibility = PoliticalVisibility.Restricted
            });
            if (!wanted.Succeeded)
            {
                RestoreCrimeWorkflow(rollback);
                return false;
            }
            dirtyTracker?.MarkDirty(authorityDiscovery ? "institution.crime-authority-discovery" : "institution.crime-report");
            return wanted.Succeeded;
        }

        private bool IsPrototypeLawAuthority(string personId)
        {
            if (worldOrganizationAuthority == null || string.IsNullOrWhiteSpace(personId)) return false;
            double worldTime = playTimeTracker == null ? Time.unscaledTimeAsDouble : playTimeTracker.CumulativeSeconds;
            foreach (string organizationId in new[]
            {
                PrototypeInstitutionalContentIds.CityGuardOrganization,
                PrototypeInstitutionalContentIds.CivicOrganization,
                PrototypeInstitutionalContentIds.ManorAdministrationOrganization,
                PrototypeInstitutionalContentIds.DuchyAdministrationOrganization,
                PrototypeInstitutionalContentIds.CrownAdministrationOrganization
            })
            {
                OrganizationAuthorizationResult authorization = worldOrganizationAuthority.EvaluateAuthorization(new OrganizationAuthorizationRequest
                {
                    actorPersonId = personId,
                    organizationId = organizationId,
                    actionDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.RecordOfficialCrimeDiscoveryActionId,
                    scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                    targetRecordId = "crime-incident",
                    allowDelegatedAuthority = true,
                    worldTime = worldTime
                });
                if (authorization.Succeeded) return true;
            }
            return false;
        }

        private bool RestoreCrimeWorkflow(CrimeRuntimeSaveData rollback)
        {
            string player = ResolvePlayerPersonId();
            string world = playerService == null ? UnityIsekaiGame.GameData.Persistence.PersistenceService.LocalWorldId : playerService.WorldId;
            CrimeOperationResult restored = worldCrimes.RestoreFromSaveData(rollback, GetDefinitionRegistry(), worldGovernments, worldLaws, worldOrganizationAuthority, worldDiplomacy, world, GetPrototypeSocialPersonIds(player), GetKnownPlaceIds());
            if (!restored.Succeeded) Debug.LogError($"Crime workflow rollback failed: {restored.Message}");
            return restored.Succeeded;
        }

        private static string NormalizeRuntimeRecordSuffix(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim().ToLowerInvariant();
            char[] normalized = source.Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-').ToArray();
            return new string(normalized);
        }
    }
}
