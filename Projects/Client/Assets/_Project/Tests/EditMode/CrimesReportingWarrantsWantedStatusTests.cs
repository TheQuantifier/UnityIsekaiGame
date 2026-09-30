using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class CrimesReportingWarrantsWantedStatusTests
    {
        private const string ActorId = "person.prototype.player";
        private const string VictimId = "person.prototype.friend";
        private const string WitnessId = "person.prototype.mentor";
        private const string TerritoryId = "political-territory.test.realm";
        private const string JurisdictionId = "jurisdiction.test.general";
        private const string GovernmentId = "government.test.royal";

        [Test]
        public void PrototypeCrimeDefinitionsValidateWithoutWarnings()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionCatalog catalog = ClassificationTestFactory.CreateCatalog(registry.DefinitionsById.Values.OfType<ScriptableObject>().ToArray());
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());
            Assert.That(registry.TryGet(PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId, out LegalOffenseDefinition offense), Is.True);
            Assert.That(registry.TryGet(PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId, out WarrantDefinition warrant), Is.True);
            Assert.That(registry.TryGet(PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId, out WantedStatusDefinition wanted), Is.True);
            Assert.That(offense.LegalActionId, Is.EqualTo("crime.attack"));
            Assert.That(warrant.AllowedScopes, Does.Contain(WarrantScopeKind.Person));
            Assert.That(wanted.Purpose, Is.EqualTo(WantedPurposeCategory.Arrest));
        }

        [Test]
        public void IncidentReportAndPotentialOffenseUseHistoricalLegalApplicability()
        {
            RuntimeFixture fixture = CreateFixture();
            EnactCrimeLaw(fixture, "attack", "crime.attack");
            CrimeOperationResult incident = fixture.Crimes.RecordIncident(IncidentRequest("attack"));
            CrimeOperationResult report = fixture.Crimes.SubmitReport(ReportRequest("attack"));
            CrimeOperationResult offense = fixture.Crimes.EvaluatePotentialOffense(OffenseRequest("attack"));
            CrimeOperationResult duplicate = fixture.Crimes.EvaluatePotentialOffense(OffenseRequest("attack"));

            Assert.That(incident.Succeeded, Is.True, incident.Message);
            Assert.That(report.Succeeded, Is.True, report.Message);
            Assert.That(offense.Succeeded, Is.True, offense.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Crimes.TryGetPotentialOffense("potential-offense.test.attack", out PotentialOffenseRecordData record), Is.True);
            Assert.That(record.legalApplicabilityStatus, Is.EqualTo(LegalApplicabilityStatus.Prohibited));
            Assert.That(record.status, Is.EqualTo(PotentialOffenseStatus.ElementsSupported));
            Assert.That(record.legalProvisionVersion, Is.EqualTo(1));
        }

        [Test]
        public void MissingIncidentAndWeakWarrantRejectWithoutPartialMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            EnactCrimeLaw(fixture, "attack", "crime.attack");
            long before = fixture.Crimes.Revision;
            CrimeOperationResult missingIncident = fixture.Crimes.EvaluatePotentialOffense(OffenseRequest("missing"));
            fixture.Crimes.RecordIncident(IncidentRequest("attack"));
            fixture.Crimes.SubmitReport(ReportRequest("attack"));
            fixture.Crimes.EvaluatePotentialOffense(OffenseRequest("attack"));
            CrimeOperationResult weakWarrant = fixture.Crimes.RequestWarrant(WarrantRequest("weak", EvidenceSufficiencyState.Partial));

            Assert.That(missingIncident.Code, Is.EqualTo(CrimeOperationCode.MissingIncident));
            Assert.That(fixture.Crimes.PotentialOffenses.Count, Is.EqualTo(1));
            Assert.That(fixture.Crimes.Revision, Is.GreaterThan(before));
            Assert.That(weakWarrant.Code, Is.EqualTo(CrimeOperationCode.ThresholdNotMet));
            Assert.That(fixture.Crimes.WarrantRequests.Count, Is.Zero);
        }

        [Test]
        public void SuspectWarrantAndWantedLifecyclePreserveRecordsWithoutTreatingSuspicionAsGuilt()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrime(fixture);
            CrimeOperationResult suspect = fixture.Crimes.AddSuspect(new CrimeSuspectRequest { transactionId = "tx.suspect", suspectId = "crime-suspect.test.actor", incidentId = "crime-incident.test.attack", potentialOffenseId = "potential-offense.test.attack", subjectId = ActorId, participation = ParticipationCategory.PrincipalActor, basis = "victim report", worldTime = 14d });
            CrimeOperationResult cleared = fixture.Crimes.TransitionSuspect(new CrimeSuspectTransitionRequest { transactionId = "tx.suspect.clear", suspectId = "crime-suspect.test.actor", targetState = SuspectLifecycleState.Misidentified, misidentified = true, reason = "identity contradicted", worldTime = 15d });
            CrimeOperationResult requested = fixture.Crimes.RequestWarrant(WarrantRequest("arrest", EvidenceSufficiencyState.Substantial));
            CrimeOperationResult deniedAuthority = fixture.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = "tx.review.denied", warrantRequestId = "warrant-request.test.arrest", reviewId = "authority-grant.missing", approve = true });
            CrimeOperationResult approved = fixture.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = "tx.review.approve", warrantRequestId = "warrant-request.test.arrest", reviewId = "trusted.system", approve = true, trustedSystemOperation = true });
            CrimeOperationResult issued = fixture.Crimes.IssueWarrant(new WarrantIssueRequest { transactionId = "tx.issue", warrantId = "warrant.test.arrest", warrantRequestId = "warrant-request.test.arrest", issuedByPersonId = ActorId, issuedWorldTime = 16d, activationWorldTime = 16d, expirationWorldTime = 30d, trustedSystemOperation = true });

            Assert.That(suspect.Succeeded, Is.True, suspect.Message);
            Assert.That(cleared.Succeeded, Is.True, cleared.Message);
            Assert.That(deniedAuthority.Code, Is.EqualTo(CrimeOperationCode.MissingAuthority));
            Assert.That(requested.Succeeded, Is.True, requested.Message);
            Assert.That(approved.Succeeded, Is.True, approved.Message);
            Assert.That(issued.Succeeded, Is.True, issued.Message);
            Assert.That(fixture.Crimes.TryGetSuspect("crime-suspect.test.actor", out CrimeSuspectRecordData suspectRecord), Is.True);
            Assert.That(suspectRecord.lifecycleState, Is.EqualTo(SuspectLifecycleState.Misidentified));
            Assert.That(fixture.Crimes.WantedStatuses.Single().derivedFromWarrant, Is.True);
        }

        [Test]
        public void InvalidPeopleAndReliabilityAreRejectedWithoutMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            CrimeIncidentRequest invalidIncident = IncidentRequest("invalid-person");
            invalidIncident.witnessIds = new[] { "person.missing" };
            CrimeOperationResult missingPerson = fixture.Crimes.RecordIncident(invalidIncident);
            Assert.That(missingPerson.Succeeded, Is.False);
            Assert.That(fixture.Crimes.Incidents, Is.Empty);

            Assert.That(fixture.Crimes.RecordIncident(IncidentRequest("invalid-report")).Succeeded, Is.True);
            CrimeReportRequest invalidReport = ReportRequest("invalid-report");
            invalidReport.reporterReliabilityBasisPoints = 10001;
            CrimeOperationResult reliability = fixture.Crimes.SubmitReport(invalidReport);
            Assert.That(reliability.Succeeded, Is.False);
            Assert.That(fixture.Crimes.Reports, Is.Empty);
        }

        [Test]
        public void WantedUpsertLinksIncidentsWithoutDuplicatesAndEscalatesQuestioningToArrest()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrime(fixture);
            CrimeOperationResult questioning = fixture.Crimes.UpsertWantedStatus(new WantedStatusUpsertRequest
            {
                transactionId = "tx.wanted.upsert.questioning",
                wantedStatusId = "wanted-status.test.active",
                wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForQuestioningDefinitionId,
                incidentId = "crime-incident.test.attack",
                subjectId = ActorId,
                jurisdictionId = JurisdictionId,
                territoryId = TerritoryId,
                risk = WantedRiskAssessment.Nonviolent,
                activeWorldTime = 20d
            });
            CrimeOperationResult arrest = fixture.Crimes.UpsertWantedStatus(new WantedStatusUpsertRequest
            {
                transactionId = "tx.wanted.upsert.arrest",
                wantedStatusId = "wanted-status.test.should-not-be-created",
                wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId,
                incidentId = "crime-incident.test.attack",
                subjectId = ActorId,
                jurisdictionId = JurisdictionId,
                territoryId = TerritoryId,
                risk = WantedRiskAssessment.PotentiallyArmed,
                activeWorldTime = 21d
            });

            Assert.That(questioning.Succeeded, Is.True, questioning.Message);
            Assert.That(arrest.Succeeded, Is.True, arrest.Message);
            Assert.That(arrest.SubjectId, Is.EqualTo(questioning.SubjectId));
            Assert.That(fixture.Crimes.WantedStatuses.Count, Is.EqualTo(1));
            WantedStatusRecordData status = fixture.Crimes.WantedStatuses.Single();
            Assert.That(status.purpose, Is.EqualTo(WantedPurposeCategory.Arrest));
            Assert.That(status.risk, Is.EqualTo(WantedRiskAssessment.PotentiallyArmed));
            Assert.That(status.incidentIds, Is.EquivalentTo(new[] { "crime-incident.test.attack" }));
        }

        [Test]
        public void ProjectionsRedactRestrictedCrimeRecordsWithoutMutatingRuntime()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrime(fixture);
            fixture.Crimes.CreateWantedStatus(new WantedStatusRequest { transactionId = "tx.wanted", wantedStatusId = "wanted-status.test.questioning", wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForQuestioningDefinitionId, incidentIds = new[] { "crime-incident.test.attack" }, subjectId = ActorId, jurisdictionId = JurisdictionId, territoryId = TerritoryId, activeWorldTime = 20d, visibility = PoliticalVisibility.Restricted });
            long before = fixture.Crimes.Revision;

            CrimeProjectionResult<CrimeIncidentRecordData> incident = fixture.Crimes.ProjectIncident("crime-incident.test.attack", privileged: false);
            CrimeProjectionResult<WantedStatusRecordData> wanted = fixture.Crimes.ProjectWantedStatus("wanted-status.test.questioning", privileged: false);

            Assert.That(incident.Succeeded, Is.True);
            Assert.That(incident.Redacted, Is.True);
            Assert.That(incident.Record.victimIds, Is.Empty);
            Assert.That(wanted.Succeeded, Is.True);
            Assert.That(wanted.Redacted, Is.True);
            Assert.That(wanted.Record.subjectId, Is.Empty);
            Assert.That(fixture.Crimes.Revision, Is.EqualTo(before));
        }

        [Test]
        public void PersistencePrepareRejectsCorruptCrimeGraphWithoutLiveMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrime(fixture);
            CrimePersistenceParticipant participant = new CrimePersistenceParticipant(fixture.Crimes, () => fixture.Registry, () => fixture.Governments, () => fixture.Laws, () => fixture.Authority, () => fixture.Diplomacy, PersistenceService.LocalWorldId, () => new[] { ActorId, VictimId, WitnessId }, () => Array.Empty<string>());
            PersistenceParticipantSaveResult captured = participant.CapturePayload();
            CrimeRuntimeSaveData corrupt = fixture.Crimes.CreateSaveData();
            corrupt.reports[0].incidentId = "crime-incident.missing";
            long before = fixture.Crimes.Revision;

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), CrimePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(captured.PayloadJson, CrimePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Crimes.Revision, Is.EqualTo(before));
            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
        }

        private static void CreateCoreCrime(RuntimeFixture fixture)
        {
            EnactCrimeLaw(fixture, "attack", "crime.attack");
            Assert.That(fixture.Crimes.RecordIncident(IncidentRequest("attack")).Succeeded, Is.True);
            Assert.That(fixture.Crimes.SubmitReport(ReportRequest("attack")).Succeeded, Is.True);
            Assert.That(fixture.Crimes.EvaluatePotentialOffense(OffenseRequest("attack")).Succeeded, Is.True);
        }

        private static RuntimeFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            OrganizationRuntime organizations = new OrganizationRuntime();
            PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(organizations, registry, PersistenceService.LocalWorldId);
            string[] knownPersons = { ActorId, VictimId, WitnessId };
            organizations.Configure(registry, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            OrganizationMembershipRuntime memberships = new OrganizationMembershipRuntime(); memberships.Configure(registry, organizations, PersistenceService.LocalWorldId, knownPersons, PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds);
            OrganizationAuthorityRuntime authority = new OrganizationAuthorityRuntime(); authority.Configure(registry, organizations, memberships, PersistenceService.LocalWorldId, knownPersons, PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds);
            OrganizationResourceRuntime resources = new OrganizationResourceRuntime(); resources.Configure(registry, organizations, authority, null, PersistenceService.LocalWorldId);
            OrganizationDecisionRuntime decisions = new OrganizationDecisionRuntime(); decisions.Configure(registry, organizations, memberships, authority, resources, PersistenceService.LocalWorldId, knownPersons, null);
            FactionRuntime factions = new FactionRuntime(); factions.Configure(registry, organizations, memberships, authority, resources, decisions, PersistenceService.LocalWorldId, knownPersons);
            DiplomacyRuntime diplomacy = new DiplomacyRuntime(); diplomacy.Configure(registry, organizations, factions, authority, decisions, resources, PersistenceService.LocalWorldId, knownPersons);
            GovernmentRuntime governments = new GovernmentRuntime(); governments.Configure(registry, organizations, memberships, authority, decisions, resources, factions, diplomacy, null, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            Assert.That(governments.CreatePolity(new PolityCreateRequest { transactionId = "tx.polity", polityId = "polity.test.kingdom", polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Test Kingdom", worldTime = 1d }).Succeeded, Is.True);
            Assert.That(governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "tx.government", governmentId = GovernmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = "polity.test.kingdom", officialName = "Test Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 2d }).Succeeded, Is.True);
            Assert.That(governments.CreateTerritory(new TerritoryCreateRequest { transactionId = "tx.territory", territoryId = TerritoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Test Realm", polityId = "polity.test.kingdom", primaryGovernmentId = GovernmentId, placeIds = new[] { "place.test.capital" }, worldTime = 3d }).Succeeded, Is.True);
            Assert.That(governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = "tx.jurisdiction", jurisdictionId = JurisdictionId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = GovernmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.GeneralAdministration }, territoryIds = new[] { TerritoryId }, priority = 100, worldTime = 4d }).Succeeded, Is.True);
            LegalRuntime laws = new LegalRuntime(); laws.Configure(registry, governments, organizations, authority, decisions, diplomacy, null, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            CrimeRuntime crimes = new CrimeRuntime(); crimes.Configure(registry, governments, laws, authority, diplomacy, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            return new RuntimeFixture(registry, organizations, authority, diplomacy, governments, laws, crimes);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(registry);
            registry = PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(registry);
            registry = PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(registry);
            registry = PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(registry);
            registry = PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(registry);
            registry = PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(registry);
            registry = PrototypeDiplomacyDefinitionFactory.AddMissingPrototypeDiplomacyDefinitions(registry);
            registry = PrototypeGovernmentDefinitionFactory.AddMissingPrototypeGovernmentDefinitions(registry);
            registry = PrototypeLegalDefinitionFactory.AddMissingPrototypeLegalDefinitions(registry);
            return PrototypeCrimeDefinitionFactory.AddMissingPrototypeCrimeDefinitions(registry);
        }

        private static void EnactCrimeLaw(RuntimeFixture fixture, string suffix, string actionId)
        {
            EnactLegalInstrumentRequest request = new EnactLegalInstrumentRequest
            {
                transactionId = $"tx.enact.{suffix}",
                instrumentId = $"legal-instrument.test.{suffix}",
                instrumentDefinitionId = PrototypeLegalDefinitionFactory.CentralStatuteId,
                authorityDefinitionId = PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                title = "Test Crime Law",
                governmentId = GovernmentId,
                organizationId = "organization.prototype.adventurers-guild",
                jurisdictionIds = new[] { JurisdictionId },
                enactmentWorldTime = 5d,
                publicationWorldTime = 5d,
                effectiveWorldTime = 10d,
                published = true,
                visibility = PoliticalVisibility.Public,
                trustedSystemOperation = true,
                provisions = new[]
                {
                    new LegalProvisionCreateRequest
                    {
                        provisionId = $"legal-provision.test.{suffix}",
                        provisionDefinitionId = PrototypeLegalDefinitionFactory.ProhibitionProvisionId,
                        version = new LegalProvisionVersionData { actionId = actionId, territoryIds = new[] { TerritoryId }, effectiveWorldTime = 10d }
                    }
                }
            };
            Assert.That(fixture.Laws.Enact(request).Succeeded, Is.True);
        }

        private static CrimeIncidentRequest IncidentRequest(string suffix) => new CrimeIncidentRequest
        {
            transactionId = $"tx.incident.{suffix}",
            incidentId = $"crime-incident.test.{suffix}",
            category = CrimeIncidentCategory.ViolentIncident,
            occurrenceStartWorldTime = 12d,
            occurrenceEndWorldTime = 12.25d,
            discoveryWorldTime = 12.5d,
            reportingWorldTime = 13d,
            historicalEventIds = new[] { $"event.test.{suffix}" },
            primaryPlaceId = "place.test.capital",
            primaryTerritoryId = TerritoryId,
            jurisdictionIds = new[] { JurisdictionId },
            involvedSubjects = new[] { CrimeSubjectReferenceData.Person(ActorId, "alleged-actor"), CrimeSubjectReferenceData.Person(VictimId, "victim") },
            victimIds = new[] { VictimId },
            witnessIds = new[] { WitnessId },
            visibility = PoliticalVisibility.Restricted
        };

        private static CrimeReportRequest ReportRequest(string suffix) => new CrimeReportRequest { transactionId = $"tx.report.{suffix}", reportId = $"crime-report.test.{suffix}", incidentId = $"crime-incident.test.{suffix}", category = CrimeReportCategory.VictimReport, reporterSubjectId = VictimId, reporterSubjectType = "Person", firstHand = true, submittedWorldTime = 13d, reporterReliabilityBasisPoints = 8000, visibility = PoliticalVisibility.Restricted };

        private static PotentialOffenseEvaluationRequest OffenseRequest(string suffix) => new PotentialOffenseEvaluationRequest { transactionId = $"tx.offense.{suffix}", potentialOffenseId = $"potential-offense.test.{suffix}", incidentId = $"crime-incident.test.{suffix}", offenseDefinitionId = PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId, allegedActorIds = new[] { ActorId }, victimOrTargetIds = new[] { VictimId }, actionId = "crime.attack", stage = OffenseStage.Completed, participation = ParticipationCategory.PrincipalActor, evidenceSufficiency = EvidenceSufficiencyState.Substantial, elementEvaluations = new[] { new OffenseElementEvaluationData { kind = OffenseElementKind.ActorConduct, key = "conduct", expectedValue = "crime.attack", observedValue = "crime.attack", supported = true, evidenceId = $"evidence.test.{suffix}" } }, visibility = PoliticalVisibility.Restricted };

        private static WarrantRequestCreateRequest WarrantRequest(string suffix, EvidenceSufficiencyState threshold) => new WarrantRequestCreateRequest { transactionId = $"tx.warrant-request.{suffix}", warrantRequestId = $"warrant-request.test.{suffix}", warrantDefinitionId = PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId, incidentId = "crime-incident.test.attack", potentialOffenseId = "potential-offense.test.attack", requestedByPersonId = VictimId, issuingGovernmentId = GovernmentId, issuingOrganizationId = "organization.prototype.adventurers-guild", scope = new WarrantScopeData { kind = WarrantScopeKind.Person, targetId = ActorId, territoryIds = new[] { TerritoryId }, jurisdictionIds = new[] { JurisdictionId } }, assertedThreshold = threshold, requestedWorldTime = 15d, visibility = PoliticalVisibility.Restricted };

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationAuthorityRuntime authority, DiplomacyRuntime diplomacy, GovernmentRuntime governments, LegalRuntime laws, CrimeRuntime crimes) { Registry = registry; Organizations = organizations; Authority = authority; Diplomacy = diplomacy; Governments = governments; Laws = laws; Crimes = crimes; }
            public DefinitionRegistry Registry { get; } public OrganizationRuntime Organizations { get; } public OrganizationAuthorityRuntime Authority { get; } public DiplomacyRuntime Diplomacy { get; } public GovernmentRuntime Governments { get; } public LegalRuntime Laws { get; } public CrimeRuntime Crimes { get; }
        }
    }
}
