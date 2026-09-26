using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Organizations.Integration;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class Group10InstitutionalPlayModeTests
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [UnityTest]
        public IEnumerator PrototypeScene_SeedsAConnectedAndPersistentInstitutionalWorld()
        {
            yield return SceneManager.LoadSceneAsync(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            PrototypePersistenceServiceBehaviour persistence = Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            Assert.That(persistence, Is.Not.Null);
            persistence.EnsureInitialized();

            Assert.That(persistence.Organizations.Snapshots, Has.Some.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == PrototypeInstitutionalContentIds.AdventurersGuild));
            Assert.That(persistence.Organizations.Snapshots, Has.Some.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == PrototypeInstitutionalContentIds.CivicOrganization));
            Assert.That(persistence.Organizations.Snapshots, Has.Some.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == PrototypeInstitutionalContentIds.ManorAdministrationOrganization));
            Assert.That(persistence.Organizations.Snapshots, Has.Some.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == PrototypeInstitutionalContentIds.DuchyAdministrationOrganization));
            Assert.That(persistence.Organizations.Snapshots, Has.Some.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == PrototypeInstitutionalContentIds.CrownAdministrationOrganization));
            Assert.That(persistence.Organizations.Snapshots, Has.Some.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == PrototypeInstitutionalContentIds.CityGuardOrganization));
            Assert.That(persistence.Organizations.Snapshots, Has.None.Matches<OrganizationSnapshot>(organization => organization.OrganizationId == "organization.prototype.independent"));
            Assert.That(persistence.OrganizationMemberships.TryGetMembership(PrototypeInstitutionalContentIds.PlayerGuildMembershipId(persistence.PlayerPersonId), out _), Is.False, "Players must not begin as automatic adventurers.");
            InteractionPointSceneBinding counter = Object.FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Include)
                .Single(binding => binding.LogicalId == PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId);
            PrototypeAdventurerGuildRegistrationDesk registrationDesk = counter.GetComponent<PrototypeAdventurerGuildRegistrationDesk>();
            Assert.That(registrationDesk, Is.Not.Null, "The physical guild counter must execute adventurer registration.");
            Assert.That(registrationDesk.InteractionPrompt, Is.EqualTo("Register as an Adventurer"));
            CharacterSkillCollection skills = persistence.PlayerSkills;
            Assert.That(skills, Is.Not.Null);
            Assert.That(skills.ClearDevelopmentState(confirmed: true).Succeeded, Is.True);
            PrototypeAdventurerRegistrationResult wrongDesk = persistence.RegisterPlayerAsAdventurerAtGuildDesk(PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId);
            Assert.That(wrongDesk.Succeeded, Is.False);
            Assert.That(persistence.OrganizationMemberships.TryGetMembership(PrototypeInstitutionalContentIds.PlayerGuildMembershipId(persistence.PlayerPersonId), out _), Is.False);
            PrototypeAdventurerRegistrationResult noSkillRegistration = persistence.RegisterPlayerAsAdventurerAtGuildDesk(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId);
            Assert.That(noSkillRegistration.Succeeded, Is.False);
            Assert.That(noSkillRegistration.Message, Does.Contain("combat-type skill"));
            Assert.That(persistence.ItemQualityDefinitionRegistry.TryGet("skill.appraisal", out SkillDefinition appraisal), Is.True);
            Assert.That(skills.GrantSkill(appraisal, SkillGrade.F, SkillAcquisitionSource.Development, "Registration eligibility test").Succeeded, Is.True);
            PrototypeAdventurerRegistrationResult utilitySkillRegistration = persistence.RegisterPlayerAsAdventurerAtGuildDesk(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId);
            Assert.That(utilitySkillRegistration.Succeeded, Is.False, "A non-combat skill must not satisfy adventurer registration.");
            Assert.That(persistence.ItemQualityDefinitionRegistry.TryGet("skill.swordsmanship", out SkillDefinition swordsmanship), Is.True);
            Assert.That(skills.GrantSkill(swordsmanship, SkillGrade.F, SkillAcquisitionSource.Development, "Registration eligibility test").Succeeded, Is.True);
            Assert.That(persistence.WorldInteractionPoints.TryGetPoint(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, out InteractionPointSnapshot counterPoint), Is.True);
            registrationDesk.HandleInteraction(default, counterPoint);
            Assert.That(persistence.OrganizationMemberships.TryGetMembership(PrototypeInstitutionalContentIds.PlayerGuildMembershipId(persistence.PlayerPersonId), out OrganizationMembershipSnapshot playerMembership), Is.True);
            Assert.That(playerMembership.PersonId, Is.EqualTo(persistence.PlayerPersonId));
            Assert.That(playerMembership.RankAssignments.Single(rank => rank.IsActive).rankDefinitionId, Is.EqualTo(PrototypeOrganizationMembershipDefinitionFactory.AdventurerGuildEntryRankId));
            Assert.That(registrationDesk.InteractionPrompt, Is.EqualTo("Speak with Adventurers Guild Receptionist"));
            PrototypeAdventurerRegistrationResult duplicateRegistration = persistence.RegisterPlayerAsAdventurerAtGuildDesk(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId);
            Assert.That(duplicateRegistration.Succeeded, Is.True, duplicateRegistration.Message);
            Assert.That(duplicateRegistration.AlreadyRegistered, Is.True);
            Assert.That(playerMembership.MembershipId, Is.EqualTo(duplicateRegistration.Membership.MembershipId));
            AssertInstitutionalPeople(persistence);
            Assert.That(persistence.Governments.TryGetGovernment(PrototypeInstitutionalContentIds.TownGovernment, out GovernmentRecordData townGovernment), Is.True);
            Assert.That(townGovernment.governmentDefinitionId, Is.EqualTo(PrototypeGovernmentDefinitionFactory.TownAdministrationDefinitionId));
            Assert.That(townGovernment.officialName, Is.EqualTo("Prototype Town Administration"));
            Assert.That(townGovernment.parentGovernmentId, Is.EqualTo(PrototypeInstitutionalContentIds.ManorGovernment));
            Assert.That(townGovernment.primaryGoverningOrganizationId, Is.EqualTo(PrototypeInstitutionalContentIds.CivicOrganization));
            Assert.That(persistence.Governments.TryGetGovernment(PrototypeInstitutionalContentIds.ManorGovernment, out GovernmentRecordData manorGovernment), Is.True);
            Assert.That(manorGovernment.parentGovernmentId, Is.EqualTo(PrototypeInstitutionalContentIds.DuchyGovernment));
            Assert.That(manorGovernment.primaryGoverningOrganizationId, Is.EqualTo(PrototypeInstitutionalContentIds.ManorAdministrationOrganization));
            Assert.That(persistence.Governments.TryGetGovernment(PrototypeInstitutionalContentIds.DuchyGovernment, out GovernmentRecordData duchyGovernment), Is.True);
            Assert.That(duchyGovernment.parentGovernmentId, Is.EqualTo(PrototypeInstitutionalContentIds.KingdomGovernment));
            Assert.That(duchyGovernment.primaryGoverningOrganizationId, Is.EqualTo(PrototypeInstitutionalContentIds.DuchyAdministrationOrganization));
            Assert.That(persistence.Governments.TryGetGovernment(PrototypeInstitutionalContentIds.KingdomGovernment, out GovernmentRecordData kingdomGovernment), Is.True);
            Assert.That(kingdomGovernment.parentGovernmentId, Is.Empty);
            Assert.That(kingdomGovernment.primaryGoverningOrganizationId, Is.EqualTo(PrototypeInstitutionalContentIds.CrownAdministrationOrganization));
            Assert.That(persistence.Governments.TryGetGovernment(PrototypeInstitutionalContentIds.WorldGovernment, out _), Is.True);
            Assert.That(persistence.Governments.TryGetTerritory(PrototypeInstitutionalContentIds.TownTerritory, out _), Is.True);
            Assert.That(persistence.Governments.TryGetJurisdiction(PrototypeInstitutionalContentIds.TownJurisdiction, out _), Is.True);
            Assert.That(persistence.Governments.TryGetJurisdiction(PrototypeInstitutionalContentIds.WorldJurisdiction, out _), Is.True);
            Assert.That(persistence.Laws.TryGetInstrument(PrototypeInstitutionalContentIds.WorldCode, out _), Is.True);
            Assert.That(persistence.Governments.OrganizationCharters.Count, Is.EqualTo(6));
            Assert.That(persistence.Governments.GetAgenciesForGovernment(PrototypeInstitutionalContentIds.TownGovernment).Single().organizationId, Is.EqualTo(PrototypeInstitutionalContentIds.CityGuardOrganization));
            Assert.That(persistence.Governments.CreateSaveData().organizationCharters.Length, Is.EqualTo(6), "Government charters must be part of the authoritative persisted world state.");
            Assert.That(persistence.Governments.OfficeTenureCount, Is.EqualTo(6));
            Assert.That(persistence.Governments.GetActiveOfficeholder(PrototypeInstitutionalContentIds.TownGovernment, PrototypeGovernmentDefinitionFactory.MayorGovernmentOfficeDefinitionId, 0d).holderPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.MayorPerson));
            Assert.That(persistence.Governments.GetActiveOfficeholder(PrototypeInstitutionalContentIds.ManorGovernment, PrototypeGovernmentDefinitionFactory.ManorLordOfficeDefinitionId, 0d).holderPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.ManorLordPerson));
            Assert.That(persistence.Governments.GetActiveOfficeholder(PrototypeInstitutionalContentIds.DuchyGovernment, PrototypeGovernmentDefinitionFactory.DukeOfficeDefinitionId, 0d).holderPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.DukePerson));
            Assert.That(persistence.Governments.GetActiveOfficeholder(PrototypeInstitutionalContentIds.KingdomGovernment, PrototypeGovernmentDefinitionFactory.MonarchOfficeDefinitionId, 0d).holderPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.MonarchPerson));
            Assert.That(persistence.Governments.FiscalMandates.Single(item => item.governmentId == PrototypeInstitutionalContentIds.TownGovernment).revenueDefinitionIds, Does.Contain(UnityIsekaiGame.Economy.PrototypeEconomyContentIds.RevenueSalesTax));
            Assert.That(persistence.Governments.FiscalMandateCount, Is.EqualTo(4));
            Assert.That(persistence.Governments.LegitimacyAssessments.Single(item => item.governmentId == PrototypeInstitutionalContentIds.TownGovernment).band, Is.EqualTo(GovernmentLegitimacyBand.Accepted));
            Assert.That(persistence.Governments.LegitimacyAssessmentCount, Is.EqualTo(4));
            Assert.That(persistence.Governments.ElectionCount, Is.Zero, "The prototype government must not schedule an election by default.");
            GovernmentRuntimeSaveData governmentSave = persistence.Governments.CreateSaveData();
            Assert.That(governmentSave.officeTenures.Length, Is.EqualTo(6));
            Assert.That(governmentSave.fiscalMandates.Length, Is.EqualTo(4));
            Assert.That(governmentSave.legitimacyAssessments.Length, Is.EqualTo(4));
            Assert.That(governmentSave.remittancePolicies.Length, Is.EqualTo(3));
            Assert.That(governmentSave.budgetCycles.Length, Is.EqualTo(4));
            Assert.That(governmentSave.actionRegulations.Length, Is.EqualTo(2));
            foreach (GovernmentOrganizationCharterRecordData charter in persistence.Governments.OrganizationCharters)
            {
                Assert.That(persistence.Laws.TryGetInstrument(charter.legalInstrumentId, out LegalInstrumentRecordData instrument), Is.True, charter.legalInstrumentId);
                Assert.That(instrument.provisionIds.Length, Is.EqualTo(charter.grantedPowerIds.Length + charter.dutyIds.Length));
                Assert.That(instrument.provisionIds.All(provisionId => persistence.Laws.TryGetProvision(provisionId, out _)), Is.True);
            }
            Assert.That(persistence.Justice.TryGetCourt(PrototypeInstitutionalContentIds.TownCourt, out _), Is.True);
            Assert.That(persistence.OrganizationResources.TryGetTreasury(PrototypeInstitutionalContentIds.CivicTreasury, out _), Is.True);
            Assert.That(persistence.OrganizationResources.TryGetTreasury(PrototypeInstitutionalContentIds.ManorTreasury, out OrganizationTreasuryRecordData manorTreasury), Is.True);
            Assert.That(manorTreasury.organizationId, Is.EqualTo(PrototypeInstitutionalContentIds.ManorAdministrationOrganization));
            Assert.That(persistence.OrganizationResources.TryGetTreasury(PrototypeInstitutionalContentIds.DuchyTreasury, out OrganizationTreasuryRecordData duchyTreasury), Is.True);
            Assert.That(duchyTreasury.organizationId, Is.EqualTo(PrototypeInstitutionalContentIds.DuchyAdministrationOrganization));
            Assert.That(persistence.OrganizationResources.TryGetTreasury(PrototypeInstitutionalContentIds.KingdomTreasury, out OrganizationTreasuryRecordData kingdomTreasury), Is.True);
            Assert.That(kingdomTreasury.organizationId, Is.EqualTo(PrototypeInstitutionalContentIds.CrownAdministrationOrganization));
            Assert.That(persistence.Economy.TryGetAccount(PrototypeInstitutionalContentIds.ManorEconomyAccount, out UnityIsekaiGame.Economy.EconomyAccountSnapshot manorEconomyAccount), Is.True);
            Assert.That(manorEconomyAccount.OwnerId, Is.EqualTo(PrototypeInstitutionalContentIds.ManorAdministrationOrganization));
            Assert.That(persistence.Economy.TryGetAccount(PrototypeInstitutionalContentIds.DuchyEconomyAccount, out UnityIsekaiGame.Economy.EconomyAccountSnapshot duchyEconomyAccount), Is.True);
            Assert.That(duchyEconomyAccount.OwnerId, Is.EqualTo(PrototypeInstitutionalContentIds.DuchyAdministrationOrganization));
            Assert.That(persistence.Economy.TryGetAccount(PrototypeInstitutionalContentIds.KingdomEconomyAccount, out UnityIsekaiGame.Economy.EconomyAccountSnapshot kingdomEconomyAccount), Is.True);
            Assert.That(kingdomEconomyAccount.OwnerId, Is.EqualTo(PrototypeInstitutionalContentIds.CrownAdministrationOrganization));
            Assert.That(persistence.OrganizationResources.Budgets, Has.Some.Matches<OrganizationBudgetRecordData>(budget => budget.budgetId == PrototypeInstitutionalContentIds.CivicBudget));
            Assert.That(persistence.Governments.BudgetCycles.Single(item => item.governmentId == PrototypeInstitutionalContentIds.ManorGovernment).actorPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.ManorLordPerson));
            Assert.That(persistence.Governments.BudgetCycles.Single(item => item.governmentId == PrototypeInstitutionalContentIds.DuchyGovernment).actorPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.DukePerson));
            Assert.That(persistence.Governments.BudgetCycles.Single(item => item.governmentId == PrototypeInstitutionalContentIds.KingdomGovernment).actorPersonId, Is.EqualTo(PrototypeInstitutionalContentIds.MonarchPerson));
            GovernmentPermitCheckResult merchantPermit = persistence.Governments.EvaluateRegulatedAction(UnityIsekaiGame.Economy.PrototypeEconomyContentIds.BusinessInstance, GovernmentPermitHolderCategory.Business, "government.action.conduct-regulated-trade", PrototypeInstitutionalContentIds.TownJurisdiction, 0d);
            Assert.That(merchantPermit.Required, Is.True);
            Assert.That(merchantPermit.Allowed, Is.True, merchantPermit.Message);

            AssertOptionalElectionAndPermitFlows(persistence);

            var readiness = persistence.InstitutionalIntegration.CreateReadinessSnapshot();
            Assert.That(readiness.Diagnostics, Has.None.Matches<Step13IntegrationDiagnostic>(diagnostic => diagnostic.Severity == Step13IntegrationDiagnosticSeverity.Error), string.Join("\n", readiness.Diagnostics));
            Assert.That(readiness.Runtimes.Count, Is.EqualTo(11));
            Assert.That(readiness.Runtimes, Has.All.Matches<Step13RuntimeSummary>(runtime => runtime.Ready));
            Assert.That(persistence.WorldReadiness?.succeeded, Is.True, persistence.WorldReadiness?.message);

            int incidentsBefore = persistence.Crimes.CreateSaveData().incidents.Length;
            int wantedBefore = persistence.Crimes.WantedStatuses.Count;
            persistence.Governments.TryGetJurisdiction(PrototypeInstitutionalContentIds.TownJurisdiction, out var townJurisdiction);
            bool theftRecorded = persistence.RecordTheftCrime(
                persistence.PlayerPersonId,
                "person.prototype.merchant",
                "item.instance.playmode.owned-pickup",
                "playmode.owned-item-pickup",
                townJurisdiction.placeIds[0]);
            Assert.That(theftRecorded, Is.True, "Taking an item owned by another known person should create a theft incident and potential offense.");
            Assert.That(persistence.Crimes.CreateSaveData().incidents.Length, Is.EqualTo(incidentsBefore + 1));
            CrimeIncidentRecordData theftIncident = persistence.Crimes.Incidents.Single(incident => incident.provenanceId == "item.instance.playmode.owned-pickup");
            Assert.That(theftIncident.reportIds, Is.Empty, "A violation must not report itself.");
            Assert.That(persistence.Crimes.WantedStatuses.Count, Is.EqualTo(wantedBefore), "An undiscovered and unreported violation must not create wanted status.");
            Assert.That(persistence.ReportCrimeIncident(theftIncident.incidentId, "person.prototype.merchant", "playmode.theft-report"), Is.True);
            WantedStatusRecordData questioning = persistence.Crimes.WantedStatuses.Single(status => status.subjectId == persistence.PlayerPersonId && status.lifecycleState == WantedStatusLifecycleState.Active);
            Assert.That(questioning.purpose, Is.EqualTo(WantedPurposeCategory.Questioning));
            Assert.That(questioning.jurisdictionId, Is.Empty, "Worldwide law creates a worldwide wanted status.");
            Assert.That(persistence.DiscoverCrimeIncidentByAuthority(theftIncident.incidentId, PrototypeInstitutionalContentIds.GuardPerson, "playmode.theft-discovery"), Is.True);
            WantedStatusRecordData arrest = persistence.Crimes.WantedStatuses.Single(status => status.wantedStatusId == questioning.wantedStatusId);
            Assert.That(arrest.purpose, Is.EqualTo(WantedPurposeCategory.Arrest));
            Assert.That(arrest.incidentIds, Is.EquivalentTo(new[] { theftIncident.incidentId }), "Repeated reports must link the incident without duplicating wanted records.");
            Assert.That(persistence.Crimes.WantedStatuses.Count, Is.EqualTo(wantedBefore + 1));
        }

        private static void AssertInstitutionalPeople(PrototypePersistenceServiceBehaviour persistence)
        {
            AssertMembership(persistence, "organization-membership.prototype.adventurers-guild.guildmaster", PrototypeInstitutionalContentIds.GuildmasterPerson, minimumRanks: 9);
            AssertMembership(persistence, "organization-membership.prototype.adventurers-guild.receptionist", PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson);
            AssertMembership(persistence, "organization-membership.prototype.merchant-guild.guildmaster", PrototypeInstitutionalContentIds.MerchantGuildmasterPerson, minimumRanks: 9);
            AssertMembership(persistence, "organization-membership.prototype.merchant-guild.receptionist", PrototypeInstitutionalContentIds.MerchantGuildReceptionistPerson);
            AssertMembership(persistence, "organization-membership.prototype.temple.priest", PrototypeInstitutionalContentIds.TemplePriestPerson, minimumRanks: 2);
            AssertMembership(persistence, "organization-membership.prototype.university.headmaster", PrototypeInstitutionalContentIds.UniversityHeadmasterPerson);
            AssertMembership(persistence, "organization-membership.prototype.manor-administration.lord", PrototypeInstitutionalContentIds.ManorLordPerson);
            AssertMembership(persistence, "organization-membership.prototype.duchy-administration.duke", PrototypeInstitutionalContentIds.DukePerson);
            AssertMembership(persistence, "organization-membership.prototype.crown-administration.monarch", PrototypeInstitutionalContentIds.MonarchPerson);

            Assert.That(persistence.OrganizationMemberships.TryGetOffice("organization-office-record.prototype.adventurers-guild.receptionist", out _), Is.True);
            Assert.That(persistence.OrganizationMemberships.TryGetOffice("organization-office-record.prototype.merchant-guild.guildmaster", out _), Is.True);
            Assert.That(persistence.OrganizationMemberships.TryGetOffice("organization-office-record.prototype.merchant-guild.receptionist", out _), Is.True);
            Assert.That(persistence.OrganizationMemberships.TryGetOffice("organization-office-record.prototype.university.headmaster", out _), Is.True);

            OrganizationAuthorizationResult receptionistRegistration = persistence.OrganizationAuthority.EvaluateAuthorization(Authorization(
                PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson,
                PrototypeInstitutionalContentIds.AdventurersGuild,
                PrototypeOrganizationAuthorityDefinitionFactory.AdmitMemberActionId,
                "playmode.receptionist.registration"));
            OrganizationAuthorizationResult receptionistRemoval = persistence.OrganizationAuthority.EvaluateAuthorization(Authorization(
                PrototypeInstitutionalContentIds.AdventurersGuildReceptionistPerson,
                PrototypeInstitutionalContentIds.AdventurersGuild,
                PrototypeOrganizationAuthorityDefinitionFactory.RemoveMemberActionId,
                "playmode.receptionist.removal"));
            OrganizationAuthorizationResult headmasterOrder = persistence.OrganizationAuthority.EvaluateAuthorization(Authorization(
                PrototypeInstitutionalContentIds.UniversityHeadmasterPerson,
                PrototypeInstitutionalContentIds.UniversityOrganization,
                PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId,
                "playmode.headmaster.order"));

            Assert.That(receptionistRegistration.Succeeded, Is.True, receptionistRegistration.Message);
            Assert.That(receptionistRemoval.Status, Is.EqualTo(OrganizationAuthorizationStatus.MissingPermission));
            Assert.That(headmasterOrder.Succeeded, Is.True, headmasterOrder.Message);
            OrganizationAuthorizationResult mayorBudget = persistence.OrganizationAuthority.EvaluateAuthorization(Authorization(
                PrototypeInstitutionalContentIds.MayorPerson,
                PrototypeInstitutionalContentIds.CivicOrganization,
                PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationBudgetActionId,
                "playmode.mayor.budget"));
            Assert.That(mayorBudget.Succeeded, Is.True, mayorBudget.Message);
            OrganizationAuthorizationResult manorLordBudget = persistence.OrganizationAuthority.EvaluateAuthorization(Authorization(
                PrototypeInstitutionalContentIds.ManorLordPerson,
                PrototypeInstitutionalContentIds.ManorAdministrationOrganization,
                PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationBudgetActionId,
                "playmode.manor-lord.budget"));
            OrganizationAuthorizationResult mayorCannotManageManor = persistence.OrganizationAuthority.EvaluateAuthorization(Authorization(
                PrototypeInstitutionalContentIds.MayorPerson,
                PrototypeInstitutionalContentIds.ManorAdministrationOrganization,
                PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationBudgetActionId,
                "playmode.mayor.manor-budget"));
            Assert.That(manorLordBudget.Succeeded, Is.True, manorLordBudget.Message);
            Assert.That(mayorCannotManageManor.Succeeded, Is.False, "The town Mayor must not inherit Manor treasury authority.");
        }

        private static void AssertOptionalElectionAndPermitFlows(PrototypePersistenceServiceBehaviour persistence)
        {
            PoliticalOperationResult scheduled = persistence.Governments.ScheduleElection(new GovernmentElectionScheduleRequest
            {
                transactionId = "playmode.election.schedule", electionId = "government-election.playmode.mayor",
                governmentId = PrototypeInstitutionalContentIds.TownGovernment, officeDefinitionId = PrototypeGovernmentDefinitionFactory.MayorGovernmentOfficeDefinitionId,
                selectionMethod = GovernmentOfficeSelectionMethod.PopularElection, administeringOrganizationId = PrototypeInstitutionalContentIds.CivicOrganization,
                eligibleVoterPersonIds = new[] { persistence.PlayerPersonId, PrototypeInstitutionalContentIds.MayorPerson }, scheduledWorldTime = 10d,
                votingStartWorldTime = 20d, votingEndWorldTime = 30d, provenanceId = "playmode"
            });
            Assert.That(scheduled.Succeeded, Is.True, scheduled.Message);
            Assert.That(persistence.Governments.NominateElectionCandidate(new GovernmentElectionNominationRequest { transactionId = "playmode.election.nominate.player", electionId = "government-election.playmode.mayor", candidatePersonId = persistence.PlayerPersonId, nominationSourceId = "playmode", worldTime = 15d }).Succeeded, Is.True);
            Assert.That(persistence.Governments.NominateElectionCandidate(new GovernmentElectionNominationRequest { transactionId = "playmode.election.nominate.mayor", electionId = "government-election.playmode.mayor", candidatePersonId = PrototypeInstitutionalContentIds.MayorPerson, nominationSourceId = "playmode", worldTime = 15d }).Succeeded, Is.True);
            Assert.That(persistence.Governments.CastElectionBallot(new GovernmentElectionBallotRequest { transactionId = "playmode.election.vote.player", ballotId = "government-ballot.playmode.player", electionId = "government-election.playmode.mayor", voterPersonId = persistence.PlayerPersonId, candidatePersonId = persistence.PlayerPersonId, worldTime = 25d }).Succeeded, Is.True);
            Assert.That(persistence.Governments.CastElectionBallot(new GovernmentElectionBallotRequest { transactionId = "playmode.election.vote.mayor", ballotId = "government-ballot.playmode.mayor", electionId = "government-election.playmode.mayor", voterPersonId = PrototypeInstitutionalContentIds.MayorPerson, candidatePersonId = persistence.PlayerPersonId, worldTime = 25d }).Succeeded, Is.True);
            PoliticalOperationResult certified = persistence.Governments.CertifyElection(new GovernmentElectionCertificationRequest { transactionId = "playmode.election.certify", electionId = "government-election.playmode.mayor", linkedResolutionId = "organization-resolution.playmode.mayor-election", worldTime = 31d });
            Assert.That(certified.Succeeded, Is.True, certified.Message);
            Assert.That(persistence.Governments.TryGetElection("government-election.playmode.mayor", out GovernmentElectionRecordData election), Is.True);
            Assert.That(election.winningPersonId, Is.EqualTo(persistence.PlayerPersonId));
            Assert.That(election.eligibleVoterPersonIds, Is.EquivalentTo(new[] { persistence.PlayerPersonId, PrototypeInstitutionalContentIds.MayorPerson }), "The election must retain its frozen voter roll.");

            PoliticalOperationResult permit = persistence.Governments.IssuePermit(new GovernmentPermitIssueRequest
            {
                transactionId = "playmode.permit.issue", permitId = "government-permit.playmode.player-trade",
                permitDefinitionId = PrototypeGovernmentDefinitionFactory.TradeLicenseDefinitionId, governmentId = PrototypeInstitutionalContentIds.TownGovernment,
                jurisdictionId = PrototypeInstitutionalContentIds.TownJurisdiction, holderCategory = GovernmentPermitHolderCategory.Person, holderId = persistence.PlayerPersonId,
                issuedByPersonId = PrototypeInstitutionalContentIds.MayorPerson, sourceAuthorityId = "government-office-tenure.prototype.mayor",
                worldTime = 40d, effectiveWorldTime = 40d, expirationWorldTime = 50d, provenanceId = "playmode"
            });
            Assert.That(permit.Succeeded, Is.True, permit.Message);
            Assert.That(persistence.Governments.HasActivePermit(persistence.PlayerPersonId, "government.action.conduct-regulated-trade", PrototypeInstitutionalContentIds.TownJurisdiction, 45d), Is.True);
            Assert.That(persistence.Governments.HasActivePermit(persistence.PlayerPersonId, "government.action.conduct-regulated-trade", PrototypeInstitutionalContentIds.TownJurisdiction, 51d), Is.False);
            PoliticalOperationResult timeResult = persistence.Governments.ProcessWorldTime(new PoliticalTimeEvaluationRequest { transactionId = "playmode.government.time.51", boundaryId = "government-time-boundary.playmode.51", worldTime = 51d });
            Assert.That(timeResult.Succeeded, Is.True, timeResult.Message);
            Assert.That(persistence.Governments.TryGetPermit("government-permit.playmode.player-trade", out GovernmentPermitRecordData expiredPermit), Is.True);
            Assert.That(expiredPermit.state, Is.EqualTo(GovernmentPermitState.Expired));
        }

        private static void AssertMembership(PrototypePersistenceServiceBehaviour persistence, string membershipId, string personId, int minimumRanks = 0)
        {
            Assert.That(persistence.OrganizationMemberships.TryGetMembership(membershipId, out OrganizationMembershipSnapshot membership), Is.True, membershipId);
            Assert.That(membership.PersonId, Is.EqualTo(personId));
            Assert.That(membership.RankAssignments.Count, Is.GreaterThanOrEqualTo(minimumRanks));
        }

        private static OrganizationAuthorizationRequest Authorization(string personId, string organizationId, string actionId, string operationId)
        {
            return new OrganizationAuthorizationRequest
            {
                actorPersonId = personId,
                organizationId = organizationId,
                actionDefinitionId = actionId,
                operationId = operationId,
                scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                worldTime = 10d
            };
        }
    }
}
