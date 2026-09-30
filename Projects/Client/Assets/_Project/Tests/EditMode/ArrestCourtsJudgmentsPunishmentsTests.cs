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
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class ArrestCourtsJudgmentsPunishmentsTests
    {
        private const string ActorId = "person.prototype.player";
        private const string VictimId = "person.prototype.friend";
        private const string WitnessId = "person.prototype.mentor";
        private const string TerritoryId = "political-territory.test.justice.realm";
        private const string JurisdictionId = "jurisdiction.test.justice.general";
        private const string GovernmentId = "government.test.justice.royal";
        private const string CourtId = "court.test.justice.general";
        private const string AppellateCourtId = "court.test.justice.appellate";

        [Test]
        public void PrototypeJusticeDefinitionsValidateWithoutWarnings()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionCatalog catalog = ClassificationTestFactory.CreateCatalog(registry.DefinitionsById.Values.OfType<ScriptableObject>().ToArray());
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());
            Assert.That(registry.TryGet(PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId, out JusticeInstitutionDefinition institution), Is.True);
            Assert.That(registry.TryGet(PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId, out CourtDefinition court), Is.True);
            Assert.That(registry.TryGet(PrototypeJusticeDefinitionFactory.WarrantArrestDefinitionId, out ArrestDefinition arrest), Is.True);
            Assert.That(registry.TryGet(PrototypeJusticeDefinitionFactory.CriminalChargeDefinitionId, out ChargeDefinition charge), Is.True);
            Assert.That(registry.TryGet(PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId, out HearingDefinition hearing), Is.True);
            Assert.That(institution.SupportedCases, Does.Contain(JusticeCaseCategory.Criminal));
            Assert.That(court.AvailableOutcomes, Does.Contain(JudgmentOutcome.Guilty));
            Assert.That(arrest.ValidLegalBases, Does.Contain(ArrestLegalBasisKind.ActiveArrestWarrant));
            Assert.That(charge.SupportedOffenseCategories, Does.Contain(OffenseCategory.ViolenceAgainstPerson));
            Assert.That(hearing.PermitsFindings, Is.True);
        }

        [Test]
        public void WarrantArrestCreatesCustodyWithoutJudgment()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrimeAndWarrant(fixture, "arrest");
            RegisterCourt(fixture, CourtId);

            JusticeOperationResult arrest = fixture.Justice.Arrest(ArrestRequest("arrest", "arrest.test.justice", "custody.test.justice"));
            JusticeOperationResult duplicate = fixture.Justice.Arrest(ArrestRequest("arrest", "arrest.test.justice", "custody.test.justice"));

            Assert.That(arrest.Succeeded, Is.True, arrest.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Justice.TryGetArrest("arrest.test.justice", out ArrestRecordData arrestRecord), Is.True);
            Assert.That(fixture.Justice.TryGetCustody("custody.test.justice", out CustodyRecordData custody), Is.True);
            Assert.That(arrestRecord.legalBasis.kind, Is.EqualTo(ArrestLegalBasisKind.ActiveArrestWarrant));
            Assert.That(custody.lifecycleState, Is.EqualTo(CustodyLifecycleState.Active));
            Assert.That(custody.personId, Is.EqualTo(ActorId));
            Assert.That(fixture.Justice.Judgments, Is.Empty);
            Assert.That(fixture.Crimes.WantedStatuses.Single().lifecycleState, Is.EqualTo(WantedStatusLifecycleState.Suspended), "A completed arrest must suspend every wanted notice supporting that arrest, including warrant-derived notices.");
        }

        [Test]
        public void ExoneratingJudgmentClearsWantedStatusForTheIncident()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrimeAndWarrant(fixture, "exonerated");
            RegisterCourt(fixture, CourtId);
            Assert.That(FileCase(fixture, "case.test.justice.exonerated").Succeeded, Is.True);
            Assert.That(FileCharge(fixture, "case.test.justice.exonerated", "charge.test.justice.exonerated", "exonerated").Succeeded, Is.True);

            JusticeOperationResult judgment = fixture.Justice.EnterJudgment(new JudgmentRequest
            {
                transactionId = "tx.justice.judgment.exonerated",
                judgmentId = "judgment.test.justice.exonerated",
                caseId = "case.test.justice.exonerated",
                chargeOutcomes = new[] { new JusticeChargeOutcomeData { chargeId = "charge.test.justice.exonerated", outcome = JudgmentOutcome.Acquitted, reason = "The charge was not proven." } },
                enteredWorldTime = 22d
            });

            Assert.That(judgment.Succeeded, Is.True, judgment.Message);
            Assert.That(fixture.Crimes.WantedStatuses.Single().lifecycleState, Is.EqualTo(WantedStatusLifecycleState.Cleared));
        }

        [Test]
        public void CaseChargePleaHearingAndJudgmentPreserveProcessBoundaries()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateCoreCrimeAndWarrant(fixture, "case");
            RegisterCourt(fixture, CourtId);

            JusticeOperationResult caseFile = FileCase(fixture, "case.test.justice");
            JusticeOperationResult charge = FileCharge(fixture, "case.test.justice", "charge.test.justice");
            JusticeOperationResult plea = fixture.Justice.EnterPlea(new PleaRequest { transactionId = "tx.justice.plea", pleaId = "plea.test.justice", caseId = "case.test.justice", chargeId = "charge.test.justice", defendantPersonId = ActorId, category = PleaCategory.NotGuilty, statement = "Not guilty.", enteredWorldTime = 18d });
            JusticeOperationResult hearing = fixture.Justice.ScheduleHearing(new HearingScheduleRequest { transactionId = "tx.justice.hearing", hearingId = "hearing.test.justice", hearingDefinitionId = PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId, caseId = "case.test.justice", category = HearingCategory.Trial, issueIds = new[] { "charge.test.justice" }, scheduledWorldTime = 19d });
            JusticeOperationResult evidence = fixture.Justice.SubmitEvidence(new EvidenceSubmissionRequest { transactionId = "tx.justice.evidence", evidenceSubmissionId = "evidence-submission.test.justice", caseId = "case.test.justice", hearingId = "hearing.test.justice", evidenceId = "crime-evidence-link.test.case", submittedByPartyId = "party.test.prosecutor", submittedWorldTime = 20d });
            JusticeOperationResult ruling = fixture.Justice.RuleOnEvidence(new EvidenceRulingRequest { transactionId = "tx.justice.ruling", evidenceSubmissionId = "evidence-submission.test.justice", targetState = EvidenceRulingState.Admitted, reason = "Relevant." });
            JusticeOperationResult finding = fixture.Justice.RecordFinding(new FindingRequest { transactionId = "tx.justice.finding", findingId = "finding.test.justice", caseId = "case.test.justice", chargeId = "charge.test.justice", category = FindingCategory.Fact, text = "Elements proven.", proven = true, enteredWorldTime = 21d });
            JusticeOperationResult judgment = fixture.Justice.EnterJudgment(new JudgmentRequest { transactionId = "tx.justice.judgment", judgmentId = "judgment.test.justice", caseId = "case.test.justice", chargeOutcomes = new[] { new JusticeChargeOutcomeData { chargeId = "charge.test.justice", findingId = "finding.test.justice", outcome = JudgmentOutcome.Guilty, reason = "Elements proven." } }, enteredWorldTime = 22d });

            Assert.That(caseFile.Succeeded, Is.True, caseFile.Message);
            Assert.That(charge.Succeeded, Is.True, charge.Message);
            Assert.That(plea.Succeeded, Is.True, plea.Message);
            Assert.That(hearing.Succeeded, Is.True, hearing.Message);
            Assert.That(evidence.Succeeded, Is.True, evidence.Message);
            Assert.That(ruling.Succeeded, Is.True, ruling.Message);
            Assert.That(finding.Succeeded, Is.True, finding.Message);
            Assert.That(judgment.Succeeded, Is.True, judgment.Message);
            Assert.That(fixture.Justice.TryGetCharge("charge.test.justice", out ChargeRecordData chargeRecord), Is.True);
            Assert.That(chargeRecord.lifecycleState, Is.EqualTo(ChargeLifecycleState.Adjudicated));
            Assert.That(fixture.Justice.TryGetJudgment("judgment.test.justice", out JudgmentRecordData judgmentRecord), Is.True);
            Assert.That(judgmentRecord.chargeOutcomes.Single().outcome, Is.EqualTo(JudgmentOutcome.Guilty));
        }

        [Test]
        public void SentenceRemedyAppealAndClemencyDoNotRewriteOriginalJudgment()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateJudgedCase(fixture, "sentence");
            RegisterCourt(fixture, AppellateCourtId, appellate: true);

            JusticeOperationResult sentence = fixture.Justice.ImposeSentence(new SentenceRequest { transactionId = "tx.justice.sentence", sentenceId = "sentence.test.justice", sentenceDefinitionId = PrototypeJusticeDefinitionFactory.FineSentenceDefinitionId, judgmentId = "judgment.test.justice.sentence", caseId = "case.test.justice.sentence", defendantPersonId = ActorId, imposedWorldTime = 23d, components = new[] { new SentenceComponentData { componentId = "sentence-component.test.justice.fine", category = SentenceCategory.Fine, state = SentenceComponentState.Pending, amount = 50, currencyId = "currency.prototype.coin", destinationRuntime = "economy" } } });
            JusticeOperationResult execute = fixture.Justice.ExecuteSentenceComponent(new SentenceExecutionRequest { transactionId = "tx.justice.sentence.execute", sentenceId = "sentence.test.justice", componentId = "sentence-component.test.justice.fine", worldTime = 24d });
            JusticeOperationResult remedy = fixture.Justice.OrderRemedy(new RemedyRequest { transactionId = "tx.justice.remedy", remedyId = "remedy.test.justice", remedyDefinitionId = PrototypeJusticeDefinitionFactory.PropertyReturnRemedyDefinitionId, caseId = "case.test.justice.sentence", judgmentId = "judgment.test.justice.sentence", category = RemedyCategory.PropertyReturn, targetId = "property.test.confiscated", destinationRuntime = "property", orderedWorldTime = 25d });
            JusticeOperationResult appeal = fixture.Justice.FileAppeal(new AppealRequest { transactionId = "tx.justice.appeal", appealId = "appeal.test.justice", appealDefinitionId = PrototypeJusticeDefinitionFactory.JudgmentAppealDefinitionId, sourceJudgmentId = "judgment.test.justice.sentence", appellateCourtId = AppellateCourtId, staysJudgment = false, staysSentence = true, filedWorldTime = 26d });
            JusticeOperationResult decided = fixture.Justice.DecideAppeal(new AppealDecisionRequest { transactionId = "tx.justice.appeal.decision", appealId = "appeal.test.justice", outcome = AppealOutcome.Affirmed, decidedWorldTime = 27d });
            JusticeOperationResult clemency = fixture.Justice.GrantClemency(new ClemencyRequest { transactionId = "tx.justice.clemency", clemencyId = "clemency.test.justice", clemencyDefinitionId = PrototypeJusticeDefinitionFactory.CommutationClemencyDefinitionId, judgmentId = "judgment.test.justice.sentence", sentenceId = "sentence.test.justice", grantorGovernmentId = GovernmentId, grantedWorldTime = 28d, effectSummary = "Sentence commuted.", trustedSystemOperation = true });

            Assert.That(sentence.Succeeded, Is.True, sentence.Message);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(remedy.Succeeded, Is.True, remedy.Message);
            Assert.That(appeal.Succeeded, Is.True, appeal.Message);
            Assert.That(decided.Succeeded, Is.True, decided.Message);
            Assert.That(clemency.Succeeded, Is.True, clemency.Message);
            Assert.That(fixture.Justice.TryGetJudgment("judgment.test.justice.sentence", out JudgmentRecordData judgment), Is.True);
            Assert.That(judgment.lifecycleState, Is.EqualTo(JudgmentLifecycleState.Final));
            Assert.That(judgment.chargeOutcomes.Single().outcome, Is.EqualTo(JudgmentOutcome.Guilty));
            Assert.That(fixture.Justice.TryGetSentence("sentence.test.justice", out SentenceRecordData savedSentence), Is.True);
            Assert.That(savedSentence.lifecycleState, Is.EqualTo(SentenceLifecycleState.Commuted));
        }

        [Test]
        public void PersistencePrepareRejectsCorruptJusticeGraphWithoutLiveMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateJudgedCase(fixture, "persist");
            JusticePersistenceParticipant participant = new JusticePersistenceParticipant(fixture.Justice, () => fixture.Registry, () => fixture.Governments, () => fixture.Laws, () => fixture.Organizations, () => fixture.Authority, () => fixture.Crimes, PersistenceService.LocalWorldId, () => new[] { ActorId, VictimId, WitnessId }, () => Array.Empty<string>());
            PersistenceParticipantSaveResult captured = participant.CapturePayload();
            JusticeRuntimeSaveData corrupt = fixture.Justice.CreateSaveData();
            corrupt.cases[0].courtId = "court.test.missing";
            long before = fixture.Justice.Revision;

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), JusticePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(captured.PayloadJson, JusticePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(captured.Succeeded, Is.True, captured.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Justice.Revision, Is.EqualTo(before));
            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
        }

        [Test]
        public void RestrictedJusticeProjectionsRedactWhilePrivilegedProjectionRemainsFull()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateJudgedCase(fixture, "projection");
            JusticeOperationResult arrest = fixture.Justice.Arrest(ArrestRequest("projection", "arrest.test.justice.projection", "custody.test.justice.projection"));

            JusticeProjectionResult<CourtCaseRecordData> publicCase = fixture.Justice.ProjectCase("case.test.justice.projection", privileged: false);
            JusticeProjectionResult<CourtCaseRecordData> privilegedCase = fixture.Justice.ProjectCase("case.test.justice.projection", privileged: true);
            JusticeProjectionResult<CustodyRecordData> publicCustody = fixture.Justice.ProjectCustody("custody.test.justice.projection", privileged: false);

            Assert.That(arrest.Succeeded, Is.True, arrest.Message);
            Assert.That(publicCase.Succeeded, Is.True, publicCase.Message);
            Assert.That(publicCase.Redacted, Is.True);
            Assert.That(publicCase.Record.chargeIds, Is.Empty);
            Assert.That(publicCase.Record.parties.Any(party => party.role == CasePartyRole.Defendant), Is.False);
            Assert.That(privilegedCase.Succeeded, Is.True, privilegedCase.Message);
            Assert.That(privilegedCase.Redacted, Is.False);
            Assert.That(privilegedCase.Record.chargeIds, Has.Length.EqualTo(1));
            Assert.That(privilegedCase.Record.parties.Any(party => party.role == CasePartyRole.Defendant), Is.True);
            Assert.That(publicCustody.Succeeded, Is.True, publicCustody.Message);
            Assert.That(publicCustody.Redacted, Is.True);
            Assert.That(publicCustody.Record.currentFacilityPlaceId, Is.Empty);
        }

        private static void CreateJudgedCase(RuntimeFixture fixture, string suffix)
        {
            CreateCoreCrimeAndWarrant(fixture, suffix);
            RegisterCourt(fixture, CourtId);
            string caseId = $"case.test.justice.{suffix}";
            string chargeId = $"charge.test.justice.{suffix}";
            string hearingId = $"hearing.test.justice.{suffix}";
            string evidenceSubmissionId = $"evidence-submission.test.justice.{suffix}";
            string findingId = $"finding.test.justice.{suffix}";
            string judgmentId = $"judgment.test.justice.{suffix}";
            Assert.That(FileCase(fixture, caseId).Succeeded, Is.True);
            Assert.That(FileCharge(fixture, caseId, chargeId, suffix).Succeeded, Is.True);
            Assert.That(fixture.Justice.ScheduleHearing(new HearingScheduleRequest { transactionId = $"tx.justice.{suffix}.hearing", hearingId = hearingId, hearingDefinitionId = PrototypeJusticeDefinitionFactory.TrialHearingDefinitionId, caseId = caseId, category = HearingCategory.Trial, issueIds = new[] { chargeId }, scheduledWorldTime = 19d }).Succeeded, Is.True);
            Assert.That(fixture.Justice.SubmitEvidence(new EvidenceSubmissionRequest { transactionId = $"tx.justice.{suffix}.evidence", evidenceSubmissionId = evidenceSubmissionId, caseId = caseId, hearingId = hearingId, evidenceId = $"crime-evidence-link.test.{suffix}", submittedByPartyId = "party.test.prosecutor", submittedWorldTime = 20d }).Succeeded, Is.True);
            Assert.That(fixture.Justice.RuleOnEvidence(new EvidenceRulingRequest { transactionId = $"tx.justice.{suffix}.ruling", evidenceSubmissionId = evidenceSubmissionId, targetState = EvidenceRulingState.Admitted, reason = "Relevant." }).Succeeded, Is.True);
            Assert.That(fixture.Justice.RecordFinding(new FindingRequest { transactionId = $"tx.justice.{suffix}.finding", findingId = findingId, caseId = caseId, chargeId = chargeId, category = FindingCategory.Fact, text = "Elements proven.", proven = true, enteredWorldTime = 21d }).Succeeded, Is.True);
            Assert.That(fixture.Justice.EnterJudgment(new JudgmentRequest { transactionId = $"tx.justice.{suffix}.judgment", judgmentId = judgmentId, caseId = caseId, chargeOutcomes = new[] { new JusticeChargeOutcomeData { chargeId = chargeId, findingId = findingId, outcome = JudgmentOutcome.Guilty, reason = "Elements proven." } }, enteredWorldTime = 22d }).Succeeded, Is.True);
        }

        private static void CreateCoreCrimeAndWarrant(RuntimeFixture fixture, string suffix)
        {
            EnactCrimeLaw(fixture, suffix, "crime.attack");
            Assert.That(fixture.Crimes.RecordIncident(IncidentRequest(suffix)).Succeeded, Is.True);
            Assert.That(fixture.Crimes.SubmitReport(ReportRequest(suffix)).Succeeded, Is.True);
            Assert.That(fixture.Crimes.EvaluatePotentialOffense(OffenseRequest(suffix)).Succeeded, Is.True);
            Assert.That(fixture.Crimes.RequestWarrant(WarrantRequest(suffix, EvidenceSufficiencyState.Substantial)).Succeeded, Is.True);
            Assert.That(fixture.Crimes.ReviewWarrantRequest(new WarrantReviewRequest { transactionId = $"tx.warrant-review.{suffix}", warrantRequestId = $"warrant-request.test.{suffix}", reviewId = "trusted.system", approve = true, trustedSystemOperation = true }).Succeeded, Is.True);
            Assert.That(fixture.Crimes.IssueWarrant(new WarrantIssueRequest { transactionId = $"tx.warrant-issue.{suffix}", warrantId = $"warrant.test.{suffix}", warrantRequestId = $"warrant-request.test.{suffix}", issuedByPersonId = VictimId, issuedWorldTime = 16d, activationWorldTime = 16d, expirationWorldTime = 40d, trustedSystemOperation = true }).Succeeded, Is.True);
        }

        private static void RegisterCourt(RuntimeFixture fixture, string courtId, bool appellate = false)
        {
            JusticeOperationResult result = fixture.Justice.RegisterCourt(new CourtRegisterRequest
            {
                transactionId = $"tx.justice.court.{courtId}",
                courtId = courtId,
                courtDefinitionId = appellate ? PrototypeJusticeDefinitionFactory.AppellateCourtDefinitionId : PrototypeJusticeDefinitionFactory.GeneralCourtDefinitionId,
                justiceInstitutionDefinitionId = PrototypeJusticeDefinitionFactory.GeneralJusticeInstitutionId,
                governmentId = GovernmentId,
                jurisdictionIds = new[] { JurisdictionId },
                territoryIds = new[] { TerritoryId },
                courthousePlaceId = appellate ? "place.test.appellate-court" : "place.test.court",
                worldTime = 15d
            });
            Assert.That(result.Succeeded || result.Duplicate, Is.True, result.Message);
        }

        private static JusticeOperationResult FileCase(RuntimeFixture fixture, string caseId)
        {
            return fixture.Justice.FileCase(new CaseFileRequest
            {
                transactionId = $"tx.justice.case.{caseId}",
                caseId = caseId,
                category = JusticeCaseCategory.Criminal,
                courtId = CourtId,
                incidentIds = new[] { caseId.Split('.').Last() == "justice" ? "crime-incident.test.case" : $"crime-incident.test.{caseId.Split('.').Last()}" },
                parties = new[]
                {
                    new JusticePartyData { partyId = "party.test.defendant", personId = ActorId, role = CasePartyRole.Defendant, visibility = PoliticalVisibility.Restricted },
                    new JusticePartyData { partyId = "party.test.prosecutor", organizationId = "organization.prototype.adventurers-guild", role = CasePartyRole.Prosecutor, visibility = PoliticalVisibility.Public }
                },
                filedWorldTime = 17d,
                visibility = PoliticalVisibility.Restricted
            });
        }

        private static JusticeOperationResult FileCharge(RuntimeFixture fixture, string caseId, string chargeId, string suffix = "case")
        {
            return fixture.Justice.FileCharge(new ChargeFileRequest
            {
                transactionId = $"tx.justice.charge.{chargeId}",
                chargeId = chargeId,
                chargeDefinitionId = PrototypeJusticeDefinitionFactory.CriminalChargeDefinitionId,
                caseId = caseId,
                defendantPersonId = ActorId,
                incidentId = $"crime-incident.test.{suffix}",
                potentialOffenseId = $"potential-offense.test.{suffix}",
                filingThreshold = EvidenceSufficiencyState.Substantial,
                filedWorldTime = 18d,
                trustedSystemOperation = true,
                visibility = PoliticalVisibility.Restricted
            });
        }

        private static ArrestRequest ArrestRequest(string suffix, string arrestId, string custodyId) => new ArrestRequest
        {
            transactionId = $"tx.justice.arrest.{suffix}",
            arrestId = arrestId,
            arrestDefinitionId = PrototypeJusticeDefinitionFactory.WarrantArrestDefinitionId,
            arrestedPersonId = ActorId,
            executingPersonId = VictimId,
            executingGovernmentId = GovernmentId,
            executingOrganizationId = "organization.prototype.adventurers-guild",
            legalBasis = new JusticeLegalBasisData { kind = ArrestLegalBasisKind.ActiveArrestWarrant, warrantId = $"warrant.test.{suffix}", incidentId = $"crime-incident.test.{suffix}", potentialOffenseId = $"potential-offense.test.{suffix}", effectiveWorldTime = 16d, expirationWorldTime = 40d },
            jurisdictionId = JurisdictionId,
            territoryId = TerritoryId,
            placeId = "place.test.arrest",
            custodyId = custodyId,
            custodyFacilityPlaceId = "place.test.detention",
            arrestWorldTime = 17d,
            trustedSystemOperation = true,
            visibility = PoliticalVisibility.Restricted
        };

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
            Assert.That(governments.CreatePolity(new PolityCreateRequest { transactionId = "tx.justice.polity", polityId = "polity.test.justice.kingdom", polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Justice Test Kingdom", worldTime = 1d }).Succeeded, Is.True);
            Assert.That(governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "tx.justice.government", governmentId = GovernmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = "polity.test.justice.kingdom", officialName = "Justice Test Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 2d }).Succeeded, Is.True);
            Assert.That(governments.CreateTerritory(new TerritoryCreateRequest { transactionId = "tx.justice.territory", territoryId = TerritoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Justice Test Realm", polityId = "polity.test.justice.kingdom", primaryGovernmentId = GovernmentId, placeIds = new[] { "place.test.capital" }, worldTime = 3d }).Succeeded, Is.True);
            Assert.That(governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = "tx.justice.jurisdiction", jurisdictionId = JurisdictionId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = GovernmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.GeneralAdministration }, territoryIds = new[] { TerritoryId }, priority = 100, worldTime = 4d }).Succeeded, Is.True);
            LegalRuntime laws = new LegalRuntime(); laws.Configure(registry, governments, organizations, authority, decisions, diplomacy, null, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            CrimeRuntime crimes = new CrimeRuntime(); crimes.Configure(registry, governments, laws, authority, diplomacy, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            JusticeRuntime justice = new JusticeRuntime(); justice.Configure(registry, governments, laws, organizations, authority, crimes, PersistenceService.LocalWorldId, knownPersons, Array.Empty<string>());
            return new RuntimeFixture(registry, organizations, authority, diplomacy, governments, laws, crimes, justice);
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
            registry = PrototypeCrimeDefinitionFactory.AddMissingPrototypeCrimeDefinitions(registry);
            return PrototypeJusticeDefinitionFactory.AddMissingPrototypeJusticeDefinitions(registry);
        }

        private static void EnactCrimeLaw(RuntimeFixture fixture, string suffix, string actionId)
        {
            EnactLegalInstrumentRequest request = new EnactLegalInstrumentRequest
            {
                transactionId = $"tx.justice.enact.{suffix}",
                instrumentId = $"legal-instrument.test.justice.{suffix}",
                instrumentDefinitionId = PrototypeLegalDefinitionFactory.CentralStatuteId,
                authorityDefinitionId = PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                title = "Justice Test Crime Law",
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
                        provisionId = $"legal-provision.test.justice.{suffix}",
                        provisionDefinitionId = PrototypeLegalDefinitionFactory.ProhibitionProvisionId,
                        version = new LegalProvisionVersionData { actionId = actionId, territoryIds = new[] { TerritoryId }, effectiveWorldTime = 10d }
                    }
                }
            };
            Assert.That(fixture.Laws.Enact(request).Succeeded, Is.True);
        }

        private static CrimeIncidentRequest IncidentRequest(string suffix) => new CrimeIncidentRequest
        {
            transactionId = $"tx.justice.incident.{suffix}",
            incidentId = $"crime-incident.test.{suffix}",
            category = CrimeIncidentCategory.ViolentIncident,
            occurrenceStartWorldTime = 12d,
            occurrenceEndWorldTime = 12.25d,
            discoveryWorldTime = 12.5d,
            reportingWorldTime = 13d,
            historicalEventIds = new[] { $"event.test.justice.{suffix}" },
            primaryPlaceId = "place.test.capital",
            primaryTerritoryId = TerritoryId,
            jurisdictionIds = new[] { JurisdictionId },
            involvedSubjects = new[] { CrimeSubjectReferenceData.Person(ActorId, "alleged-actor"), CrimeSubjectReferenceData.Person(VictimId, "victim") },
            victimIds = new[] { VictimId },
            witnessIds = new[] { WitnessId },
            visibility = PoliticalVisibility.Restricted
        };

        private static CrimeReportRequest ReportRequest(string suffix) => new CrimeReportRequest { transactionId = $"tx.justice.report.{suffix}", reportId = $"crime-report.test.{suffix}", incidentId = $"crime-incident.test.{suffix}", category = CrimeReportCategory.VictimReport, reporterSubjectId = VictimId, reporterSubjectType = "Person", firstHand = true, submittedWorldTime = 13d, reporterReliabilityBasisPoints = 8000, visibility = PoliticalVisibility.Restricted };
        private static PotentialOffenseEvaluationRequest OffenseRequest(string suffix) => new PotentialOffenseEvaluationRequest { transactionId = $"tx.justice.offense.{suffix}", potentialOffenseId = $"potential-offense.test.{suffix}", incidentId = $"crime-incident.test.{suffix}", offenseDefinitionId = PrototypeCrimeDefinitionFactory.UnlawfulPhysicalAttackOffenseId, allegedActorIds = new[] { ActorId }, victimOrTargetIds = new[] { VictimId }, actionId = "crime.attack", stage = OffenseStage.Completed, participation = ParticipationCategory.PrincipalActor, evidenceSufficiency = EvidenceSufficiencyState.Substantial, elementEvaluations = new[] { new OffenseElementEvaluationData { kind = OffenseElementKind.ActorConduct, key = "conduct", expectedValue = "crime.attack", observedValue = "crime.attack", supported = true, evidenceId = $"evidence.test.justice.{suffix}" } }, visibility = PoliticalVisibility.Restricted };
        private static WarrantRequestCreateRequest WarrantRequest(string suffix, EvidenceSufficiencyState threshold) => new WarrantRequestCreateRequest { transactionId = $"tx.justice.warrant-request.{suffix}", warrantRequestId = $"warrant-request.test.{suffix}", warrantDefinitionId = PrototypeCrimeDefinitionFactory.ArrestWarrantDefinitionId, incidentId = $"crime-incident.test.{suffix}", potentialOffenseId = $"potential-offense.test.{suffix}", requestedByPersonId = VictimId, issuingGovernmentId = GovernmentId, issuingOrganizationId = "organization.prototype.adventurers-guild", scope = new WarrantScopeData { kind = WarrantScopeKind.Person, targetId = ActorId, territoryIds = new[] { TerritoryId }, jurisdictionIds = new[] { JurisdictionId } }, assertedThreshold = threshold, requestedWorldTime = 15d, visibility = PoliticalVisibility.Restricted };

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationAuthorityRuntime authority, DiplomacyRuntime diplomacy, GovernmentRuntime governments, LegalRuntime laws, CrimeRuntime crimes, JusticeRuntime justice) { Registry = registry; Organizations = organizations; Authority = authority; Diplomacy = diplomacy; Governments = governments; Laws = laws; Crimes = crimes; Justice = justice; }
            public DefinitionRegistry Registry { get; } public OrganizationRuntime Organizations { get; } public OrganizationAuthorityRuntime Authority { get; } public DiplomacyRuntime Diplomacy { get; } public GovernmentRuntime Governments { get; } public LegalRuntime Laws { get; } public CrimeRuntime Crimes { get; } public JusticeRuntime Justice { get; }
        }
    }
}
