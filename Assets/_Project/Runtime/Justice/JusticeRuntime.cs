using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Justice
{
    public sealed class JusticeRuntime : IDisposable
    {
        private readonly Dictionary<string, CourtRecordData> courts = new Dictionary<string, CourtRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ArrestRecordData> arrests = new Dictionary<string, ArrestRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CustodyRecordData> custodyRecords = new Dictionary<string, CustodyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReleaseOrderRecordData> releaseOrders = new Dictionary<string, ReleaseOrderRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ChargeRecordData> charges = new Dictionary<string, ChargeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CourtCaseRecordData> cases = new Dictionary<string, CourtCaseRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PleaRecordData> pleas = new Dictionary<string, PleaRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, HearingRecordData> hearings = new Dictionary<string, HearingRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EvidenceSubmissionRecordData> evidenceSubmissions = new Dictionary<string, EvidenceSubmissionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProceduralRulingRecordData> rulings = new Dictionary<string, ProceduralRulingRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FindingRecordData> findings = new Dictionary<string, FindingRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, JudgmentRecordData> judgments = new Dictionary<string, JudgmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SentenceRecordData> sentences = new Dictionary<string, SentenceRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RemedyRecordData> remedies = new Dictionary<string, RemedyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, AppealRecordData> appeals = new Dictionary<string, AppealRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ClemencyRecordData> clemencies = new Dictionary<string, ClemencyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, JusticeTransactionRecordData> transactions = new Dictionary<string, JusticeTransactionRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private GovernmentRuntime governments;
        private LegalRuntime laws;
        private OrganizationRuntime organizations;
        private OrganizationAuthorityRuntime authority;
        private CrimeRuntime crimes;
        private HashSet<string> personIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> placeIds = new HashSet<string>(StringComparer.Ordinal);
        private string worldId = string.Empty;
        private bool disposed;

        public long Revision { get; private set; }
        public event Action<JusticeOperationResult> OperationCommitted;
        public event Action<JusticeMutationEvent> StateChanged;

        public IReadOnlyList<CourtRecordData> Courts => courts.Values.OrderBy(item => item.courtId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ArrestRecordData> Arrests => arrests.Values.OrderBy(item => item.arrestWorldTime).ThenBy(item => item.arrestId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CustodyRecordData> CustodyRecords => custodyRecords.Values.OrderBy(item => item.startWorldTime).ThenBy(item => item.custodyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ReleaseOrderRecordData> ReleaseOrders => releaseOrders.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.releaseOrderId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ChargeRecordData> Charges => charges.Values.OrderBy(item => item.filedWorldTime).ThenBy(item => item.chargeId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CourtCaseRecordData> Cases => cases.Values.OrderBy(item => item.filedWorldTime).ThenBy(item => item.caseId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PleaRecordData> Pleas => pleas.Values.OrderBy(item => item.enteredWorldTime).ThenBy(item => item.pleaId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<HearingRecordData> Hearings => hearings.Values.OrderBy(item => item.scheduledWorldTime).ThenBy(item => item.hearingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EvidenceSubmissionRecordData> EvidenceSubmissions => evidenceSubmissions.Values.OrderBy(item => item.submittedWorldTime).ThenBy(item => item.evidenceSubmissionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProceduralRulingRecordData> Rulings => rulings.Values.OrderBy(item => item.enteredWorldTime).ThenBy(item => item.rulingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FindingRecordData> Findings => findings.Values.OrderBy(item => item.enteredWorldTime).ThenBy(item => item.findingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<JudgmentRecordData> Judgments => judgments.Values.OrderBy(item => item.enteredWorldTime).ThenBy(item => item.judgmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<SentenceRecordData> Sentences => sentences.Values.OrderBy(item => item.imposedWorldTime).ThenBy(item => item.sentenceId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RemedyRecordData> Remedies => remedies.Values.OrderBy(item => item.orderedWorldTime).ThenBy(item => item.remedyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<AppealRecordData> Appeals => appeals.Values.OrderBy(item => item.filedWorldTime).ThenBy(item => item.appealId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ClemencyRecordData> Clemencies => clemencies.Values.OrderBy(item => item.grantedWorldTime).ThenBy(item => item.clemencyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitions, GovernmentRuntime governmentRuntime, LegalRuntime legalRuntime, OrganizationRuntime organizationRuntime, OrganizationAuthorityRuntime authorityRuntime, CrimeRuntime crimeRuntime, string runtimeWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            registry = definitions ?? registry;
            governments = governmentRuntime;
            laws = legalRuntime;
            organizations = organizationRuntime;
            authority = authorityRuntime;
            crimes = crimeRuntime;
            worldId = JusticeModelUtility.N(runtimeWorldId);
            personIds = new HashSet<string>(JusticeModelUtility.C(knownPersons), StringComparer.Ordinal);
            placeIds = new HashSet<string>(JusticeModelUtility.C(knownPlaces), StringComparer.Ordinal);
        }

        public bool TryGetCourt(string id, out CourtRecordData value) => TryClone(courts, id, item => item.Clone(), out value);
        public bool TryGetArrest(string id, out ArrestRecordData value) => TryClone(arrests, id, item => item.Clone(), out value);
        public bool TryGetCustody(string id, out CustodyRecordData value) => TryClone(custodyRecords, id, item => item.Clone(), out value);
        public bool TryGetCharge(string id, out ChargeRecordData value) => TryClone(charges, id, item => item.Clone(), out value);
        public bool TryGetCase(string id, out CourtCaseRecordData value) => TryClone(cases, id, item => item.Clone(), out value);
        public bool TryGetJudgment(string id, out JudgmentRecordData value) => TryClone(judgments, id, item => item.Clone(), out value);
        public bool TryGetSentence(string id, out SentenceRecordData value) => TryClone(sentences, id, item => item.Clone(), out value);
        public bool TryGetAppeal(string id, out AppealRecordData value) => TryClone(appeals, id, item => item.Clone(), out value);

        public CourtSelectionResult SelectCourt(JusticeCaseCategory category, IEnumerable<string> jurisdictionIds, string preferredCourtId, bool appellate, double evaluationWorldTime)
        {
            string[] requestedJurisdictions = JusticeModelUtility.C(jurisdictionIds);
            List<string> candidates = new List<string>();
            List<string> disqualified = new List<string>();
            List<string> conflicts = new List<string>();
            foreach (CourtRecordData court in courts.Values.OrderBy(item => item.courtId, StringComparer.Ordinal))
            {
                bool active = court.lifecycleState == JusticeCourtLifecycleState.Active || court.lifecycleState == JusticeCourtLifecycleState.EmergencyActive || court.lifecycleState == JusticeCourtLifecycleState.InExile || court.lifecycleState == JusticeCourtLifecycleState.OccupationTribunal;
                bool jurisdiction = requestedJurisdictions.Length == 0 || JusticeModelUtility.C(court.jurisdictionIds).Intersect(requestedJurisdictions, StringComparer.Ordinal).Any();
                bool definition = Def(court.courtDefinitionId, out CourtDefinition courtDefinition) && courtDefinition.SupportedCases.Contains(category) && courtDefinition.Appellate == appellate;
                if (active && jurisdiction && definition) candidates.Add(court.courtId); else disqualified.Add(court.courtId);
            }

            string preferred = JusticeModelUtility.N(preferredCourtId);
            string primary = candidates.Contains(preferred, StringComparer.Ordinal) ? preferred : candidates.FirstOrDefault() ?? string.Empty;
            if (candidates.Count > 1 && string.IsNullOrEmpty(preferred)) conflicts.Add("Multiple courts match; caller must choose or provide transfer policy.");
            if (string.IsNullOrEmpty(primary)) conflicts.Add("No court has active matching jurisdiction.");
            return new CourtSelectionResult(candidates, disqualified, primary, conflicts, evaluationWorldTime, Revision);
        }

        public JusticeOperationResult RegisterCourt(CourtRegisterRequest request)
        {
            request ??= new CourtRegisterRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string id = JusticeModelUtility.N(request.courtId);
            if (Duplicate(request.transactionId, "register-court", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || courts.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Court ID is invalid or already exists.", before);
            if (!Def(request.courtDefinitionId, out CourtDefinition courtDefinition)) return Fail(JusticeOperationCode.MissingDefinition, $"Court definition '{request.courtDefinitionId}' is missing.", before);
            if (!Def(request.justiceInstitutionDefinitionId, out JusticeInstitutionDefinition institutionDefinition)) return Fail(JusticeOperationCode.MissingDefinition, $"Justice institution definition '{request.justiceInstitutionDefinitionId}' is missing.", before);
            foreach (string jurisdictionId in JusticeModelUtility.C(request.jurisdictionIds)) if (governments == null || !governments.TryGetJurisdiction(jurisdictionId, out _)) return Fail(JusticeOperationCode.MissingJurisdiction, $"Jurisdiction '{jurisdictionId}' is missing.", before);
            foreach (string territoryId in JusticeModelUtility.C(request.territoryIds)) if (governments == null || !governments.TryGetTerritory(territoryId, out _)) return Fail(JusticeOperationCode.InvalidReference, $"Territory '{territoryId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.governmentId) && (governments == null || !governments.TryGetGovernment(request.governmentId, out _))) return Fail(JusticeOperationCode.InvalidReference, $"Government '{request.governmentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.organizationId) && organizations != null && !organizations.TryGetSnapshot(request.organizationId, out _)) return Fail(JusticeOperationCode.InvalidReference, $"Organization '{request.organizationId}' is missing.", before);
            if (!institutionDefinition.SupportedCases.Intersect(courtDefinition.SupportedCases).Any()) return Fail(JusticeOperationCode.InvalidDefinition, "Court and justice institution case support do not overlap.", before);
            CourtRecordData record = new CourtRecordData { courtId = id, courtDefinitionId = courtDefinition.Id, justiceInstitutionDefinitionId = institutionDefinition.Id, governmentId = JusticeModelUtility.N(request.governmentId), organizationId = JusticeModelUtility.N(request.organizationId), jurisdictionIds = JusticeModelUtility.C(request.jurisdictionIds), territoryIds = JusticeModelUtility.C(request.territoryIds), courthousePlaceId = JusticeModelUtility.N(request.courthousePlaceId), judgeOfficeIds = JusticeModelUtility.C(request.judgeOfficeIds), clerkOfficeIds = JusticeModelUtility.C(request.clerkOfficeIds), appealParentCourtId = JusticeModelUtility.N(request.appealParentCourtId), startWorldTime = request.worldTime, visibility = request.visibility, provenanceId = JusticeModelUtility.N(request.provenanceId), revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Court registration previewed.", before, before, id, preview: true);
            courts[id] = record;
            Complete(request.transactionId, "register-court", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Court registered.", before, Revision, id));
        }

        public JusticeOperationResult Arrest(ArrestRequest request)
        {
            request ??= new ArrestRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string id = JusticeModelUtility.N(request.arrestId);
            string personId = JusticeModelUtility.N(request.arrestedPersonId);
            if (Duplicate(request.transactionId, "arrest", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || arrests.ContainsKey(id) || !ValidatePerson(personId)) return Fail(JusticeOperationCode.InvalidRequest, "Arrest ID or arrested Person is invalid.", before);
            if (!Def(request.arrestDefinitionId, out ArrestDefinition definition)) return Fail(JusticeOperationCode.MissingDefinition, $"Arrest definition '{request.arrestDefinitionId}' is missing.", before);
            if (!ValidateJurisdictionAndTerritory(request.jurisdictionId, request.territoryId, before, out JusticeOperationResult invalid)) return invalid;
            JusticeLegalBasisData basis = request.legalBasis?.Clone();
            if (!ValidateArrestBasis(definition, basis, personId, request.arrestWorldTime, request.trustedSystemOperation || request.voluntarySurrender, before, out JusticeOperationResult basisFailure)) return basisFailure;
            if (!request.voluntarySurrender && !request.trustedSystemOperation && !HasAuthority(request.executingOfficeId) && !HasAuthority(basis?.authorityGrantId)) return Fail(JusticeOperationCode.MissingAuthority, "Arrest requires executing authority unless surrender or trusted system operation is explicit.", before);
            LegalApplicabilityStatus immunityStatus = laws?.Evaluate(new LegalApplicabilityRequest { personId = personId, territoryId = request.territoryId, actionId = "legal-process.arrest", worldTime = request.arrestWorldTime }).Status ?? LegalApplicabilityStatus.Unknown;
            if (!request.voluntarySurrender && immunityStatus == LegalApplicabilityStatus.Immune) return Fail(JusticeOperationCode.ImmunityBlocked, "Legal immunity blocks ordinary arrest.", before);
            string custodyId = JusticeModelUtility.N(request.custodyId);
            if (definition.CreatesCustody && string.IsNullOrEmpty(custodyId)) custodyId = $"custody.{id}";
            ArrestRecordData record = new ArrestRecordData { arrestId = id, arrestDefinitionId = definition.Id, arrestedPersonId = personId, executingPersonId = JusticeModelUtility.N(request.executingPersonId), executingOfficeId = JusticeModelUtility.N(request.executingOfficeId), executingOrganizationId = JusticeModelUtility.N(request.executingOrganizationId), executingGovernmentId = JusticeModelUtility.N(request.executingGovernmentId), legalBasis = basis, jurisdictionId = JusticeModelUtility.N(request.jurisdictionId), territoryId = JusticeModelUtility.N(request.territoryId), placeId = JusticeModelUtility.N(request.placeId), voluntarySurrender = request.voluntarySurrender, forceOutcomeId = JusticeModelUtility.N(request.forceOutcomeId), custodyId = custodyId, arrestWorldTime = request.arrestWorldTime, visibility = request.visibility, provenanceId = JusticeModelUtility.N(request.provenanceId), revision = 1 };
            CustodyRecordData custody = null;
            if (definition.CreatesCustody)
            {
                if (custodyRecords.ContainsKey(custodyId)) return Fail(JusticeOperationCode.InvalidRequest, $"Custody '{custodyId}' already exists.", before);
                custody = new CustodyRecordData { custodyId = custodyId, category = request.voluntarySurrender ? CustodyCategory.VoluntarySurrenderCustody : definition.Category == ArrestCategory.MilitaryApprehension ? CustodyCategory.MilitaryCustody : CustodyCategory.ArrestCustody, personId = personId, currentHolderGovernmentId = JusticeModelUtility.N(request.executingGovernmentId), currentHolderOrganizationId = JusticeModelUtility.N(request.executingOrganizationId), currentFacilityPlaceId = JusticeModelUtility.N(request.custodyFacilityPlaceId), legalBasis = basis?.Clone(), sourceArrestId = id, lifecycleState = CustodyLifecycleState.Active, startWorldTime = request.arrestWorldTime, reviewDueWorldTime = definition.DefaultDetentionReviewInterval <= 0d ? -1d : request.arrestWorldTime + definition.DefaultDetentionReviewInterval, visibility = request.visibility, revision = 1 };
            }
            if (request.preview) return JusticeOperationResult.Success("Arrest previewed.", before, before, id, preview: true);
            if (basis != null && !string.IsNullOrWhiteSpace(basis.incidentId) && crimes != null)
            {
                CrimeOperationResult wanted = crimes.ReconcileWantedStatusesForJustice(new WantedStatusJusticeResolutionRequest
                {
                    transactionId = $"justice.arrest.wanted.{id}",
                    resolutionId = id,
                    subjectPersonId = personId,
                    incidentIds = new[] { basis.incidentId },
                    targetState = WantedStatusLifecycleState.Suspended,
                    includeWarrantDerived = true,
                    correctionReason = $"Subject apprehended by arrest '{id}'.",
                    worldTime = request.arrestWorldTime
                });
                if (!wanted.Succeeded) return Fail(JusticeOperationCode.InvalidState, $"Arrest could not reconcile wanted status: {wanted.Message}", before);
            }
            arrests[id] = record;
            if (custody != null) custodyRecords[custody.custodyId] = custody;
            Complete(request.transactionId, "arrest", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Arrest recorded and custody created atomically.", before, Revision, id));
        }

        public JusticeOperationResult TransferCustody(CustodyTransferRequest request)
        {
            request ??= new CustodyTransferRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string id = JusticeModelUtility.N(request.custodyId);
            if (Duplicate(request.transactionId, "transfer-custody", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (!custodyRecords.TryGetValue(id, out CustodyRecordData custody)) return Fail(JusticeOperationCode.MissingCustody, $"Custody '{id}' is missing.", before);
            if (custody.lifecycleState != CustodyLifecycleState.Active) return Fail(JusticeOperationCode.InvalidState, "Only active custody can transfer.", before);
            if (string.IsNullOrWhiteSpace(request.targetHolderGovernmentId) && string.IsNullOrWhiteSpace(request.targetHolderOrganizationId)) return Fail(JusticeOperationCode.InvalidRequest, "Custody transfer requires a target holder.", before);
            if (request.preview) return JusticeOperationResult.Success("Custody transfer previewed.", before, before, id, preview: true);
            custody.currentHolderGovernmentId = JusticeModelUtility.N(request.targetHolderGovernmentId);
            custody.currentHolderOrganizationId = JusticeModelUtility.N(request.targetHolderOrganizationId);
            custody.currentFacilityPlaceId = JusticeModelUtility.N(request.targetFacilityPlaceId);
            custody.lifecycleState = CustodyLifecycleState.Transferred;
            custody.revision++;
            Complete(request.transactionId, "transfer-custody", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Custody transferred without deleting history.", before, Revision, id));
        }

        public JusticeOperationResult OrderRelease(ReleaseOrderRequest request)
        {
            request ??= new ReleaseOrderRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string id = JusticeModelUtility.N(request.releaseOrderId);
            string custodyId = JusticeModelUtility.N(request.custodyId);
            if (Duplicate(request.transactionId, "release", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || releaseOrders.ContainsKey(id) || request.category == ReleaseCategory.Unknown) return Fail(JusticeOperationCode.InvalidRequest, "Release order ID or category is invalid.", before);
            if (!custodyRecords.TryGetValue(custodyId, out CustodyRecordData custody)) return Fail(JusticeOperationCode.MissingCustody, $"Custody '{custodyId}' is missing.", before);
            if (request.preview) return JusticeOperationResult.Success("Release previewed.", before, before, id, preview: true);
            ReleaseOrderRecordData record = new ReleaseOrderRecordData { releaseOrderId = id, custodyId = custodyId, category = request.category, orderedByCourtId = JusticeModelUtility.N(request.orderedByCourtId), orderedByAuthorityId = JusticeModelUtility.N(request.orderedByAuthorityId), conditions = JusticeModelUtility.C(request.conditions), orderedWorldTime = request.orderedWorldTime, effectiveWorldTime = request.effectiveWorldTime, executed = true, visibility = custody.visibility, revision = 1 };
            releaseOrders[id] = record;
            custody.lifecycleState = CustodyLifecycleState.Released;
            custody.releaseOrderId = id;
            custody.endWorldTime = request.effectiveWorldTime;
            custody.revision++;
            Complete(request.transactionId, "release", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Release ordered and custody ended atomically.", before, Revision, id));
        }

        public JusticeOperationResult FileCase(CaseFileRequest request)
        {
            request ??= new CaseFileRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string id = JusticeModelUtility.N(request.caseId);
            if (Duplicate(request.transactionId, "file-case", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || cases.ContainsKey(id) || request.category == JusticeCaseCategory.Unknown) return Fail(JusticeOperationCode.InvalidRequest, "Case ID or category is invalid.", before);
            if (!courts.TryGetValue(JusticeModelUtility.N(request.courtId), out CourtRecordData court)) return Fail(JusticeOperationCode.MissingCourt, $"Court '{request.courtId}' is missing.", before);
            if (court.lifecycleState != JusticeCourtLifecycleState.Active) return Fail(JusticeOperationCode.InvalidState, "Court is not active.", before);
            foreach (string incidentId in JusticeModelUtility.C(request.incidentIds)) if (crimes == null || !crimes.TryGetIncident(incidentId, out _)) return Fail(JusticeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            JusticePartyData[] parties = (request.parties ?? Array.Empty<JusticePartyData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.role).ThenBy(item => item.personId, StringComparer.Ordinal).ToArray();
            if (parties.All(item => item.role != CasePartyRole.Defendant)) return Fail(JusticeOperationCode.InvalidRequest, "Case requires at least one defendant party.", before);
            CourtCaseRecordData record = new CourtCaseRecordData { caseId = id, category = request.category, courtId = court.courtId, incidentIds = JusticeModelUtility.C(request.incidentIds), parties = parties, lifecycleState = CourtCaseLifecycleState.Filed, filedWorldTime = request.filedWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Case filing previewed.", before, before, id, preview: true);
            cases[id] = record;
            Complete(request.transactionId, "file-case", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Court case filed.", before, Revision, id));
        }

        public JusticeOperationResult FileCharge(ChargeFileRequest request)
        {
            request ??= new ChargeFileRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string id = JusticeModelUtility.N(request.chargeId);
            string caseId = JusticeModelUtility.N(request.caseId);
            if (Duplicate(request.transactionId, "file-charge", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || charges.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Charge ID is invalid or already exists.", before);
            if (!cases.TryGetValue(caseId, out CourtCaseRecordData courtCase)) return Fail(JusticeOperationCode.MissingCase, $"Case '{caseId}' is missing.", before);
            if (!Def(request.chargeDefinitionId, out ChargeDefinition chargeDefinition)) return Fail(JusticeOperationCode.MissingDefinition, $"Charge definition '{request.chargeDefinitionId}' is missing.", before);
            if (crimes == null || !crimes.TryGetPotentialOffense(request.potentialOffenseId, out PotentialOffenseRecordData offense)) return Fail(JusticeOperationCode.MissingPotentialOffense, $"Potential offense '{request.potentialOffenseId}' is missing.", before);
            if (!crimes.TryGetIncident(request.incidentId, out _)) return Fail(JusticeOperationCode.MissingIncident, $"Incident '{request.incidentId}' is missing.", before);
            if (!CompareSufficiency(offense.evidenceSufficiency, chargeDefinition.MinimumFilingThreshold)) return Fail(JusticeOperationCode.ThresholdNotMet, "Charge filing threshold is not met.", before);
            if (offense.legalApplicabilityStatus != LegalApplicabilityStatus.Prohibited && offense.legalApplicabilityStatus != LegalApplicabilityStatus.Required) return Fail(JusticeOperationCode.MissingLegalBasis, "Potential offense does not reference active prohibitory or required law.", before);
            if (!request.trustedSystemOperation && !HasAuthority(string.Empty)) return Fail(JusticeOperationCode.MissingAuthority, "Charging requires explicit prosecutorial authority or trusted fixture operation.", before);
            ChargeRecordData record = new ChargeRecordData { chargeId = id, chargeDefinitionId = chargeDefinition.Id, caseId = caseId, defendantPersonId = JusticeModelUtility.N(request.defendantPersonId), incidentId = JusticeModelUtility.N(request.incidentId), potentialOffenseId = offense.potentialOffenseId, offenseDefinitionId = offense.offenseDefinitionId, legalInstrumentId = offense.legalInstrumentId, legalProvisionId = offense.legalProvisionId, legalProvisionVersion = offense.legalProvisionVersion, filingThreshold = request.filingThreshold, lifecycleState = ChargeLifecycleState.Filed, filedWorldTime = request.filedWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Charge filing previewed.", before, before, id, preview: true);
            charges[id] = record;
            courtCase.chargeIds = JusticeModelUtility.C(courtCase.chargeIds.Concat(new[] { id }));
            courtCase.lifecycleState = CourtCaseLifecycleState.Active;
            courtCase.revision++;
            Complete(request.transactionId, "file-charge", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Charge filed without creating a judgment.", before, Revision, id));
        }

        public JusticeOperationResult TransitionCharge(ChargeTransitionRequest request)
        {
            request ??= new ChargeTransitionRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.chargeId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "transition-charge", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (!charges.TryGetValue(id, out ChargeRecordData charge)) return Fail(JusticeOperationCode.MissingCharge, $"Charge '{id}' is missing.", before);
            if (request.targetState == ChargeLifecycleState.Unknown || request.targetState == ChargeLifecycleState.Invalid) return Fail(JusticeOperationCode.InvalidState, "Charge target state is invalid.", before);
            if (request.preview) return JusticeOperationResult.Success("Charge transition previewed.", before, before, id, preview: true);
            charge.lifecycleState = request.targetState;
            if (request.targetState == ChargeLifecycleState.Withdrawn || request.targetState == ChargeLifecycleState.Dismissed || request.targetState == ChargeLifecycleState.Superseded || request.targetState == ChargeLifecycleState.Adjudicated) charge.endedWorldTime = request.worldTime;
            charge.revision++;
            Complete(request.transactionId, "transition-charge", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Charge transitioned.", before, Revision, id));
        }

        public JusticeOperationResult EnterPlea(PleaRequest request)
        {
            request ??= new PleaRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.pleaId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "plea", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || pleas.ContainsKey(id) || request.category == PleaCategory.Unknown) return Fail(JusticeOperationCode.InvalidRequest, "Plea ID or category is invalid.", before);
            if (!cases.ContainsKey(request.caseId)) return Fail(JusticeOperationCode.MissingCase, $"Case '{request.caseId}' is missing.", before);
            if (!charges.ContainsKey(request.chargeId)) return Fail(JusticeOperationCode.MissingCharge, $"Charge '{request.chargeId}' is missing.", before);
            if (request.preview) return JusticeOperationResult.Success("Plea previewed.", before, before, id, preview: true);
            pleas[id] = new PleaRecordData { pleaId = id, caseId = JusticeModelUtility.N(request.caseId), chargeId = JusticeModelUtility.N(request.chargeId), defendantPersonId = JusticeModelUtility.N(request.defendantPersonId), category = request.category, statement = request.statement ?? string.Empty, enteredWorldTime = request.enteredWorldTime, hasPleaAgreement = request.hasPleaAgreement, revision = 1 };
            Complete(request.transactionId, "plea", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Formal response recorded without forcing judgment.", before, Revision, id));
        }

        public JusticeOperationResult ScheduleHearing(HearingScheduleRequest request)
        {
            request ??= new HearingScheduleRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.hearingId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "schedule-hearing", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || hearings.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Hearing ID is invalid or already exists.", before);
            if (!cases.TryGetValue(request.caseId, out CourtCaseRecordData courtCase)) return Fail(JusticeOperationCode.MissingCase, $"Case '{request.caseId}' is missing.", before);
            if (!Def(request.hearingDefinitionId, out HearingDefinition definition)) return Fail(JusticeOperationCode.MissingDefinition, $"Hearing definition '{request.hearingDefinitionId}' is missing.", before);
            if (definition.Category != request.category) return Fail(JusticeOperationCode.InvalidRequest, "Hearing category does not match definition.", before);
            HearingRecordData record = new HearingRecordData { hearingId = id, hearingDefinitionId = definition.Id, caseId = courtCase.caseId, category = request.category, lifecycleState = HearingLifecycleState.Scheduled, issueIds = JusticeModelUtility.C(request.issueIds), scheduledWorldTime = request.scheduledWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Hearing scheduling previewed.", before, before, id, preview: true);
            hearings[id] = record;
            courtCase.hearingIds = JusticeModelUtility.C(courtCase.hearingIds.Concat(new[] { id }));
            courtCase.revision++;
            Complete(request.transactionId, "schedule-hearing", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Hearing scheduled.", before, Revision, id));
        }

        public JusticeOperationResult TransitionHearing(HearingTransitionRequest request)
        {
            request ??= new HearingTransitionRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.hearingId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "transition-hearing", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (!hearings.TryGetValue(id, out HearingRecordData hearing)) return Fail(JusticeOperationCode.MissingHearing, $"Hearing '{id}' is missing.", before);
            if (request.targetState == HearingLifecycleState.Unknown || request.targetState == HearingLifecycleState.Invalid) return Fail(JusticeOperationCode.InvalidState, "Hearing target state is invalid.", before);
            if (request.preview) return JusticeOperationResult.Success("Hearing transition previewed.", before, before, id, preview: true);
            hearing.lifecycleState = request.targetState;
            if (request.targetState == HearingLifecycleState.Opened) hearing.openedWorldTime = request.worldTime;
            if (request.targetState == HearingLifecycleState.Completed || request.targetState == HearingLifecycleState.Cancelled) hearing.completedWorldTime = request.worldTime;
            hearing.revision++;
            Complete(request.transactionId, "transition-hearing", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Hearing transitioned.", before, Revision, id));
        }

        public JusticeOperationResult SubmitEvidence(EvidenceSubmissionRequest request)
        {
            request ??= new EvidenceSubmissionRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.evidenceSubmissionId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "submit-evidence", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || evidenceSubmissions.ContainsKey(id) || string.IsNullOrWhiteSpace(request.evidenceId)) return Fail(JusticeOperationCode.InvalidRequest, "Evidence submission ID or evidence reference is invalid.", before);
            if (!cases.ContainsKey(request.caseId)) return Fail(JusticeOperationCode.MissingCase, $"Case '{request.caseId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.hearingId) && !hearings.ContainsKey(request.hearingId)) return Fail(JusticeOperationCode.MissingHearing, $"Hearing '{request.hearingId}' is missing.", before);
            if (request.preview) return JusticeOperationResult.Success("Evidence submission previewed.", before, before, id, preview: true);
            evidenceSubmissions[id] = new EvidenceSubmissionRecordData { evidenceSubmissionId = id, caseId = JusticeModelUtility.N(request.caseId), hearingId = JusticeModelUtility.N(request.hearingId), evidenceId = JusticeModelUtility.N(request.evidenceId), submittedByPartyId = JusticeModelUtility.N(request.submittedByPartyId), submittedWorldTime = request.submittedWorldTime, visibility = request.visibility, revision = 1 };
            Complete(request.transactionId, "submit-evidence", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Evidence submission recorded by reference.", before, Revision, id));
        }

        public JusticeOperationResult RuleOnEvidence(EvidenceRulingRequest request)
        {
            request ??= new EvidenceRulingRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.evidenceSubmissionId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "rule-evidence", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (!evidenceSubmissions.TryGetValue(id, out EvidenceSubmissionRecordData submission)) return Fail(JusticeOperationCode.MissingEvidence, $"Evidence submission '{id}' is missing.", before);
            if (request.targetState == EvidenceRulingState.Unknown || request.targetState == EvidenceRulingState.Invalid) return Fail(JusticeOperationCode.InvalidState, "Evidence ruling target is invalid.", before);
            if (request.preview) return JusticeOperationResult.Success("Evidence ruling previewed.", before, before, id, preview: true);
            submission.rulingState = request.targetState;
            submission.rulingReason = request.reason ?? string.Empty;
            submission.revision++;
            Complete(request.transactionId, "rule-evidence", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Evidence ruling recorded without mutating evidence.", before, Revision, id));
        }

        public JusticeOperationResult RecordFinding(FindingRequest request)
        {
            request ??= new FindingRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.findingId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "finding", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || findings.ContainsKey(id) || request.category == FindingCategory.Unknown) return Fail(JusticeOperationCode.InvalidRequest, "Finding ID or category is invalid.", before);
            if (!cases.ContainsKey(request.caseId)) return Fail(JusticeOperationCode.MissingCase, $"Case '{request.caseId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.chargeId) && !charges.ContainsKey(request.chargeId)) return Fail(JusticeOperationCode.MissingCharge, $"Charge '{request.chargeId}' is missing.", before);
            if (request.preview) return JusticeOperationResult.Success("Finding previewed.", before, before, id, preview: true);
            findings[id] = new FindingRecordData { findingId = id, caseId = JusticeModelUtility.N(request.caseId), chargeId = JusticeModelUtility.N(request.chargeId), category = request.category, text = request.text ?? string.Empty, proven = request.proven, enteredWorldTime = request.enteredWorldTime, visibility = request.visibility, revision = 1 };
            Complete(request.transactionId, "finding", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Finding recorded.", before, Revision, id));
        }

        public JusticeOperationResult EnterJudgment(JudgmentRequest request)
        {
            request ??= new JudgmentRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.judgmentId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "judgment", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || judgments.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Judgment ID is invalid or already exists.", before);
            if (!cases.TryGetValue(request.caseId, out CourtCaseRecordData courtCase)) return Fail(JusticeOperationCode.MissingCase, $"Case '{request.caseId}' is missing.", before);
            JusticeChargeOutcomeData[] outcomes = (request.chargeOutcomes ?? Array.Empty<JusticeChargeOutcomeData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.chargeId, StringComparer.Ordinal).ToArray();
            if (outcomes.Length == 0 || outcomes.Any(item => item.outcome == JudgmentOutcome.Unknown || !charges.ContainsKey(item.chargeId))) return Fail(JusticeOperationCode.InvalidRequest, "Judgment must contain valid charge-level outcomes.", before);
            JudgmentRecordData record = new JudgmentRecordData { judgmentId = id, caseId = courtCase.caseId, courtId = courtCase.courtId, chargeOutcomes = outcomes, lifecycleState = JudgmentLifecycleState.Entered, enteredWorldTime = request.enteredWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Judgment previewed.", before, before, id, preview: true);
            if (crimes != null)
            {
                var resolutionGroups = outcomes
                    .Select(outcome => new { Outcome = outcome, Charge = charges.TryGetValue(outcome.chargeId, out ChargeRecordData charge) ? charge : null })
                    .Where(item => item.Charge != null && !string.IsNullOrWhiteSpace(item.Charge.incidentId) && !string.IsNullOrWhiteSpace(item.Charge.defendantPersonId))
                    .GroupBy(item => $"{item.Charge.defendantPersonId}\u001f{item.Charge.incidentId}", StringComparer.Ordinal);
                foreach (var group in resolutionGroups)
                {
                    ChargeRecordData charge = group.First().Charge;
                    bool adverse = group.Any(item => item.Outcome.outcome == JudgmentOutcome.Guilty || item.Outcome.outcome == JudgmentOutcome.Liable);
                    bool exonerating = group.All(item => item.Outcome.outcome == JudgmentOutcome.Acquitted || item.Outcome.outcome == JudgmentOutcome.Dismissed || item.Outcome.outcome == JudgmentOutcome.NotProven || item.Outcome.outcome == JudgmentOutcome.NotResponsible || item.Outcome.outcome == JudgmentOutcome.Vacated);
                    if (!adverse && !exonerating) continue;
                    CrimeOperationResult wanted = crimes.ReconcileWantedStatusesForJustice(new WantedStatusJusticeResolutionRequest
                    {
                        transactionId = $"justice.judgment.wanted.{id}.{charge.chargeId}",
                        resolutionId = id,
                        subjectPersonId = charge.defendantPersonId,
                        incidentIds = new[] { charge.incidentId },
                        targetState = WantedStatusLifecycleState.Cleared,
                        includeWarrantDerived = exonerating,
                        correctionReason = exonerating ? $"Cleared by exonerating judgment '{id}'." : $"Pretrial wanted status concluded by judgment '{id}'.",
                        worldTime = request.enteredWorldTime
                    });
                    if (!wanted.Succeeded) return Fail(JusticeOperationCode.InvalidState, $"Judgment could not reconcile wanted status: {wanted.Message}", before);
                }
            }
            judgments[id] = record;
            courtCase.judgmentIds = JusticeModelUtility.C(courtCase.judgmentIds.Concat(new[] { id }));
            courtCase.lifecycleState = CourtCaseLifecycleState.JudgmentEntered;
            courtCase.revision++;
            foreach (JusticeChargeOutcomeData outcome in outcomes)
                if (charges.TryGetValue(outcome.chargeId, out ChargeRecordData charge)) { charge.lifecycleState = ChargeLifecycleState.Adjudicated; charge.endedWorldTime = request.enteredWorldTime; charge.revision++; }
            Complete(request.transactionId, "judgment", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Judgment entered with charge-level outcomes.", before, Revision, id));
        }

        public JusticeOperationResult ImposeSentence(SentenceRequest request)
        {
            request ??= new SentenceRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.sentenceId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "sentence", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || sentences.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Sentence ID is invalid or already exists.", before);
            if (!Def(request.sentenceDefinitionId, out SentenceDefinition definition)) return Fail(JusticeOperationCode.MissingDefinition, $"Sentence definition '{request.sentenceDefinitionId}' is missing.", before);
            if (!judgments.TryGetValue(request.judgmentId, out JudgmentRecordData judgment)) return Fail(JusticeOperationCode.MissingJudgment, $"Judgment '{request.judgmentId}' is missing.", before);
            bool qualifyingOutcome = judgment.chargeOutcomes.Any(item => item.outcome == JudgmentOutcome.Guilty || item.outcome == JudgmentOutcome.Liable);
            if (definition.RequiresGuiltyOrLiableOutcome && !qualifyingOutcome) return Fail(JusticeOperationCode.InvalidState, "Sentence requires a guilty or liable judgment outcome.", before);
            SentenceComponentData[] components = (request.components ?? Array.Empty<SentenceComponentData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.componentId, StringComparer.Ordinal).ToArray();
            if (components.Length == 0 || components.Any(item => item.category == SentenceCategory.Unknown || item.state == SentenceComponentState.Invalid)) return Fail(JusticeOperationCode.InvalidRequest, "Sentence requires valid components.", before);
            if (definition.Category != SentenceCategory.Custom && components.All(item => item.category != definition.Category)) return Fail(JusticeOperationCode.InvalidRequest, "Sentence components do not include the definition category.", before);
            SentenceRecordData record = new SentenceRecordData { sentenceId = id, sentenceDefinitionId = definition.Id, judgmentId = judgment.judgmentId, caseId = JusticeModelUtility.N(request.caseId), defendantPersonId = JusticeModelUtility.N(request.defendantPersonId), components = components, lifecycleState = SentenceLifecycleState.Imposed, concurrent = request.concurrent, imposedWorldTime = request.imposedWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Sentence previewed.", before, before, id, preview: true);
            sentences[id] = record;
            Complete(request.transactionId, "sentence", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Sentence imposed without executing every component.", before, Revision, id));
        }

        public JusticeOperationResult ExecuteSentenceComponent(SentenceExecutionRequest request)
        {
            request ??= new SentenceExecutionRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.sentenceId);
            string componentId = JusticeModelUtility.N(request.componentId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "execute-sentence-component", $"{id}:{componentId}", before, out JusticeOperationResult duplicate)) return duplicate;
            if (!sentences.TryGetValue(id, out SentenceRecordData sentence)) return Fail(JusticeOperationCode.MissingSentence, $"Sentence '{id}' is missing.", before);
            SentenceComponentData component = sentence.components.FirstOrDefault(item => item.componentId == componentId);
            if (component == null) return Fail(JusticeOperationCode.InvalidReference, $"Sentence component '{componentId}' is missing.", before);
            if (component.executed) return Fail(JusticeOperationCode.InvalidState, "Sentence component already executed.", before);
            if (request.preview) return JusticeOperationResult.Success("Sentence execution previewed.", before, before, id, preview: true);
            component.executed = true;
            component.state = SentenceComponentState.Completed;
            component.startedWorldTime = component.startedWorldTime < 0d ? request.worldTime : component.startedWorldTime;
            component.completedWorldTime = request.worldTime;
            component.executionRecordId = $"sentence-execution.{id}.{componentId}";
            sentence.components = sentence.components.Select(item => item.componentId == componentId ? component : item).ToArray();
            sentence.lifecycleState = sentence.components.All(item => item.executed || item.state == SentenceComponentState.Cancelled) ? SentenceLifecycleState.Completed : SentenceLifecycleState.PartiallyCompleted;
            sentence.completedWorldTime = sentence.lifecycleState == SentenceLifecycleState.Completed ? request.worldTime : -1d;
            sentence.revision++;
            Complete(request.transactionId, "execute-sentence-component", $"{id}:{componentId}");
            Revision++;
            return Commit(JusticeOperationResult.Success("Sentence component execution recorded once.", before, Revision, id));
        }

        public JusticeOperationResult OrderRemedy(RemedyRequest request)
        {
            request ??= new RemedyRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.remedyId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "remedy", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || remedies.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Remedy ID is invalid or already exists.", before);
            if (!Def(request.remedyDefinitionId, out RemedyDefinition definition)) return Fail(JusticeOperationCode.MissingDefinition, $"Remedy definition '{request.remedyDefinitionId}' is missing.", before);
            if (!cases.ContainsKey(request.caseId)) return Fail(JusticeOperationCode.MissingCase, $"Case '{request.caseId}' is missing.", before);
            if (!judgments.ContainsKey(request.judgmentId)) return Fail(JusticeOperationCode.MissingJudgment, $"Judgment '{request.judgmentId}' is missing.", before);
            if (definition.Category != request.category) return Fail(JusticeOperationCode.InvalidRequest, "Remedy category does not match definition.", before);
            if (request.preview) return JusticeOperationResult.Success("Remedy previewed.", before, before, id, preview: true);
            remedies[id] = new RemedyRecordData { remedyId = id, remedyDefinitionId = definition.Id, caseId = JusticeModelUtility.N(request.caseId), judgmentId = JusticeModelUtility.N(request.judgmentId), category = request.category, lifecycleState = RemedyLifecycleState.Ordered, targetId = JusticeModelUtility.N(request.targetId), destinationRuntime = JusticeModelUtility.N(request.destinationRuntime), orderedWorldTime = request.orderedWorldTime, revision = 1 };
            Complete(request.transactionId, "remedy", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Remedy ordered through explicit destination boundary.", before, Revision, id));
        }

        public JusticeOperationResult FileAppeal(AppealRequest request)
        {
            request ??= new AppealRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.appealId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "appeal", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || appeals.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Appeal ID is invalid or already exists.", before);
            if (!Def(request.appealDefinitionId, out AppealDefinition definition)) return Fail(JusticeOperationCode.MissingDefinition, $"Appeal definition '{request.appealDefinitionId}' is missing.", before);
            if (!judgments.TryGetValue(request.sourceJudgmentId, out JudgmentRecordData judgment)) return Fail(JusticeOperationCode.MissingJudgment, $"Judgment '{request.sourceJudgmentId}' is missing.", before);
            if (!courts.TryGetValue(request.appellateCourtId, out CourtRecordData appellateCourt) || !Def(appellateCourt.courtDefinitionId, out CourtDefinition courtDefinition) || !courtDefinition.Appellate) return Fail(JusticeOperationCode.MissingCourt, "A valid appellate court is required.", before);
            AppealRecordData record = new AppealRecordData { appealId = id, appealDefinitionId = definition.Id, sourceJudgmentId = judgment.judgmentId, appellateCourtId = appellateCourt.courtId, lifecycleState = AppealLifecycleState.Filed, staysJudgment = request.staysJudgment && definition.MayStayJudgment, staysSentence = request.staysSentence && definition.MayStaySentence, filedWorldTime = request.filedWorldTime, revision = 1 };
            if (request.preview) return JusticeOperationResult.Success("Appeal previewed.", before, before, id, preview: true);
            appeals[id] = record;
            judgment.appealId = id;
            judgment.lifecycleState = record.staysJudgment ? JudgmentLifecycleState.Stayed : JudgmentLifecycleState.Appealed;
            judgment.revision++;
            foreach (SentenceRecordData sentence in sentences.Values.Where(item => item.judgmentId == judgment.judgmentId && record.staysSentence).OrderBy(item => item.sentenceId, StringComparer.Ordinal)) { sentence.lifecycleState = SentenceLifecycleState.Stayed; sentence.revision++; }
            Complete(request.transactionId, "appeal", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Appeal filed while preserving original judgment.", before, Revision, id));
        }

        public JusticeOperationResult DecideAppeal(AppealDecisionRequest request)
        {
            request ??= new AppealDecisionRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.appealId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "decide-appeal", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (!appeals.TryGetValue(id, out AppealRecordData appeal)) return Fail(JusticeOperationCode.MissingAppeal, $"Appeal '{id}' is missing.", before);
            if (request.outcome == AppealOutcome.Unknown) return Fail(JusticeOperationCode.InvalidRequest, "Appeal outcome is invalid.", before);
            if (request.preview) return JusticeOperationResult.Success("Appeal decision previewed.", before, before, id, preview: true);
            appeal.outcome = request.outcome;
            appeal.lifecycleState = AppealLifecycleState.Decided;
            appeal.decidedWorldTime = request.decidedWorldTime;
            appeal.remandCaseId = JusticeModelUtility.N(request.remandCaseId);
            appeal.revision++;
            if (judgments.TryGetValue(appeal.sourceJudgmentId, out JudgmentRecordData judgment))
            {
                judgment.lifecycleState = request.outcome switch { AppealOutcome.Affirmed => JudgmentLifecycleState.Final, AppealOutcome.Reversed => JudgmentLifecycleState.Reversed, AppealOutcome.Modified => JudgmentLifecycleState.Modified, AppealOutcome.Vacated => JudgmentLifecycleState.Vacated, AppealOutcome.Remanded => JudgmentLifecycleState.Appealed, _ => judgment.lifecycleState };
                judgment.revision++;
            }
            Complete(request.transactionId, "decide-appeal", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Appeal decision recorded without erasing lower record.", before, Revision, id));
        }

        public JusticeOperationResult GrantClemency(ClemencyRequest request)
        {
            request ??= new ClemencyRequest();
            long before = Revision;
            string id = JusticeModelUtility.N(request.clemencyId);
            if (!Ready(out JusticeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "clemency", id, before, out JusticeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || clemencies.ContainsKey(id)) return Fail(JusticeOperationCode.InvalidRequest, "Clemency ID is invalid or already exists.", before);
            if (!Def(request.clemencyDefinitionId, out ClemencyDefinition definition)) return Fail(JusticeOperationCode.MissingDefinition, $"Clemency definition '{request.clemencyDefinitionId}' is missing.", before);
            if (!judgments.ContainsKey(request.judgmentId)) return Fail(JusticeOperationCode.MissingJudgment, $"Judgment '{request.judgmentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.sentenceId) && !sentences.ContainsKey(request.sentenceId)) return Fail(JusticeOperationCode.MissingSentence, $"Sentence '{request.sentenceId}' is missing.", before);
            if (!request.trustedSystemOperation && !HasAuthority(request.grantorAuthorityId)) return Fail(JusticeOperationCode.MissingAuthority, "Clemency requires explicit grantor authority.", before);
            if (request.preview) return JusticeOperationResult.Success("Clemency previewed.", before, before, id, preview: true);
            clemencies[id] = new ClemencyRecordData { clemencyId = id, clemencyDefinitionId = definition.Id, judgmentId = JusticeModelUtility.N(request.judgmentId), sentenceId = JusticeModelUtility.N(request.sentenceId), grantorGovernmentId = JusticeModelUtility.N(request.grantorGovernmentId), grantorAuthorityId = JusticeModelUtility.N(request.grantorAuthorityId), category = definition.Category, lifecycleState = ClemencyLifecycleState.Granted, grantedWorldTime = request.grantedWorldTime, effectSummary = request.effectSummary ?? string.Empty, revision = 1 };
            if (sentences.TryGetValue(request.sentenceId, out SentenceRecordData sentence)) { sentence.lifecycleState = definition.Category == ClemencyCategory.Commutation ? SentenceLifecycleState.Commuted : SentenceLifecycleState.Remitted; sentence.revision++; }
            Complete(request.transactionId, "clemency", id);
            Revision++;
            return Commit(JusticeOperationResult.Success("Clemency granted while preserving judgment history.", before, Revision, id));
        }

        public JusticeOperationResult ProcessWorldTime(JusticeTimeEvaluationRequest request)
        {
            request ??= new JusticeTimeEvaluationRequest();
            long before = Revision;
            if (!Ready(out JusticeOperationResult failure)) return failure;
            string key = $"{JusticeModelUtility.N(request.boundaryId)}:{request.worldTime:0.###}";
            if (Duplicate(request.transactionId, "process-time", key, before, out JusticeOperationResult duplicate)) return duplicate;
            int changed = 0;
            foreach (CustodyRecordData custody in custodyRecords.Values.Where(item => item.lifecycleState == CustodyLifecycleState.Active && item.releaseDueWorldTime >= 0d && item.releaseDueWorldTime <= request.worldTime).OrderBy(item => item.releaseDueWorldTime).ThenBy(item => item.custodyId, StringComparer.Ordinal).Take(Math.Max(1, request.maximumOperations)))
            {
                if (request.preview) { changed++; continue; }
                custody.lifecycleState = CustodyLifecycleState.Expired;
                custody.endWorldTime = request.worldTime;
                custody.revision++;
                changed++;
            }
            foreach (SentenceRecordData sentence in sentences.Values.Where(item => item.lifecycleState == SentenceLifecycleState.Active && item.components.All(component => component.executed || component.completedWorldTime >= 0d && component.completedWorldTime <= request.worldTime)).OrderBy(item => item.sentenceId, StringComparer.Ordinal).Take(Math.Max(1, request.maximumOperations - changed)))
            {
                if (request.preview) { changed++; continue; }
                sentence.lifecycleState = SentenceLifecycleState.Completed;
                sentence.completedWorldTime = request.worldTime;
                sentence.revision++;
                changed++;
            }
            if (request.preview) return JusticeOperationResult.Success($"Justice time preview evaluated {changed} transitions.", before, before, key, preview: true);
            Complete(request.transactionId, "process-time", key);
            if (changed > 0) Revision++;
            return Commit(JusticeOperationResult.Success($"Justice time processed {changed} transitions.", before, Revision, key));
        }

        public JusticeProjectionResult<CourtCaseRecordData> ProjectCase(string caseId, bool privileged)
        {
            if (!cases.TryGetValue(JusticeModelUtility.N(caseId), out CourtCaseRecordData found)) return JusticeProjectionResult<CourtCaseRecordData>.Denied("Case is missing.", JusticeVisibilityDecision.Concealed);
            CourtCaseRecordData value = found.Clone();
            if (privileged || value.visibility == PoliticalVisibility.Public) return JusticeProjectionResult<CourtCaseRecordData>.Success(value, false, "Full case projection.");
            if (Concealed(value.visibility)) return JusticeProjectionResult<CourtCaseRecordData>.Denied("Case is concealed.", JusticeVisibilityDecision.Concealed);
            value.parties = value.parties.Where(item => item.role == CasePartyRole.Judge || item.role == CasePartyRole.Prosecutor).Select(item => item.Clone()).ToArray();
            value.chargeIds = Array.Empty<string>();
            value.hearingIds = Array.Empty<string>();
            return JusticeProjectionResult<CourtCaseRecordData>.Success(value, true, "Restricted case projection redacted.");
        }

        public JusticeProjectionResult<CustodyRecordData> ProjectCustody(string custodyId, bool privileged)
        {
            if (!custodyRecords.TryGetValue(JusticeModelUtility.N(custodyId), out CustodyRecordData found)) return JusticeProjectionResult<CustodyRecordData>.Denied("Custody is missing.", JusticeVisibilityDecision.Concealed);
            CustodyRecordData value = found.Clone();
            if (privileged || value.visibility == PoliticalVisibility.Public) return JusticeProjectionResult<CustodyRecordData>.Success(value, false, "Full custody projection.");
            if (Concealed(value.visibility)) return JusticeProjectionResult<CustodyRecordData>.Denied("Custody is concealed.", JusticeVisibilityDecision.Concealed);
            value.currentFacilityPlaceId = string.Empty;
            value.currentHolderOrganizationId = string.Empty;
            return JusticeProjectionResult<CustodyRecordData>.Success(value, true, "Restricted custody projection redacted.");
        }

        private static bool Concealed(PoliticalVisibility visibility)
        {
            return visibility == PoliticalVisibility.Secret
                || visibility == PoliticalVisibility.Hidden
                || visibility == PoliticalVisibility.DevelopmentOnly;
        }

        public JusticeRuntimeSaveData CreateSaveData()
        {
            return new JusticeRuntimeSaveData { schemaVersion = 1, worldId = JusticeModelUtility.N(worldId), revision = Revision, courts = Courts.ToArray(), arrests = Arrests.ToArray(), custodyRecords = CustodyRecords.ToArray(), releaseOrders = ReleaseOrders.ToArray(), charges = Charges.ToArray(), cases = Cases.ToArray(), pleas = Pleas.ToArray(), hearings = Hearings.ToArray(), evidenceSubmissions = EvidenceSubmissions.ToArray(), rulings = Rulings.ToArray(), findings = Findings.ToArray(), judgments = Judgments.ToArray(), sentences = Sentences.ToArray(), remedies = Remedies.ToArray(), appeals = Appeals.ToArray(), clemencies = Clemencies.ToArray(), transactions = transactions.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToArray() };
        }

        public JusticeOperationResult RestoreFromSaveData(JusticeRuntimeSaveData saveData, DefinitionRegistry definitions, GovernmentRuntime governmentRuntime, LegalRuntime legalRuntime, OrganizationRuntime organizationRuntime, OrganizationAuthorityRuntime authorityRuntime, CrimeRuntime crimeRuntime, string expectedWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            long before = Revision;
            Configure(definitions, governmentRuntime, legalRuntime, organizationRuntime, authorityRuntime, crimeRuntime, expectedWorldId, knownPersons, knownPlaces);
            if (!ValidateSaveData(saveData, definitions, governmentRuntime, legalRuntime, organizationRuntime, authorityRuntime, crimeRuntime, expectedWorldId, knownPersons, knownPlaces, out string failure)) return Fail(JusticeOperationCode.ValidationFailed, failure, before);
            RestoreInternal(saveData);
            return JusticeOperationResult.Success("Justice process restored.", before, Revision);
        }

        public static bool ValidateSaveData(JusticeRuntimeSaveData data, DefinitionRegistry definitions, GovernmentRuntime governments, LegalRuntime laws, OrganizationRuntime organizations, OrganizationAuthorityRuntime authority, CrimeRuntime crimes, string expectedWorldId, IEnumerable<string> people, IEnumerable<string> places, out string failure)
        {
            failure = string.Empty;
            if (data == null) { failure = "Justice save data is missing."; return false; }
            if (data.schemaVersion != 1) { failure = $"Unsupported justice schema version {data.schemaVersion}."; return false; }
            string world = JusticeModelUtility.N(expectedWorldId);
            if (!string.IsNullOrWhiteSpace(world) && !string.IsNullOrWhiteSpace(data.worldId) && !string.Equals(world, data.worldId, StringComparison.Ordinal)) { failure = "Justice save data belongs to a different world."; return false; }
            var savedCourts = CloneArray(data.courts); var savedArrests = CloneArray(data.arrests); var savedCustody = CloneArray(data.custodyRecords); var savedReleases = CloneArray(data.releaseOrders); var savedCharges = CloneArray(data.charges); var savedCases = CloneArray(data.cases); var savedPleas = CloneArray(data.pleas); var savedHearings = CloneArray(data.hearings); var savedEvidence = CloneArray(data.evidenceSubmissions); var savedRulings = CloneArray(data.rulings); var savedFindings = CloneArray(data.findings); var savedJudgments = CloneArray(data.judgments); var savedSentences = CloneArray(data.sentences); var savedRemedies = CloneArray(data.remedies); var savedAppeals = CloneArray(data.appeals); var savedClemencies = CloneArray(data.clemencies); var savedTransactions = CloneArray(data.transactions);
            if (!Unique(savedCourts.Select(item => item.courtId), "court", out failure) || !Unique(savedArrests.Select(item => item.arrestId), "arrest", out failure) || !Unique(savedCustody.Select(item => item.custodyId), "custody", out failure) || !Unique(savedCharges.Select(item => item.chargeId), "charge", out failure) || !Unique(savedCases.Select(item => item.caseId), "case", out failure) || !Unique(savedJudgments.Select(item => item.judgmentId), "judgment", out failure) || !Unique(savedSentences.Select(item => item.sentenceId), "sentence", out failure) || !Unique(savedTransactions.Select(item => item.transactionId), "transaction", out failure)) return false;
            Dictionary<string, CourtRecordData> courtById = savedCourts.ToDictionary(item => item.courtId, StringComparer.Ordinal);
            Dictionary<string, CustodyRecordData> custodyById = savedCustody.ToDictionary(item => item.custodyId, StringComparer.Ordinal);
            Dictionary<string, ChargeRecordData> chargeById = savedCharges.ToDictionary(item => item.chargeId, StringComparer.Ordinal);
            Dictionary<string, CourtCaseRecordData> caseById = savedCases.ToDictionary(item => item.caseId, StringComparer.Ordinal);
            Dictionary<string, HearingRecordData> hearingById = savedHearings.Where(item => !string.IsNullOrWhiteSpace(item.hearingId)).ToDictionary(item => item.hearingId, StringComparer.Ordinal);
            Dictionary<string, JudgmentRecordData> judgmentById = savedJudgments.ToDictionary(item => item.judgmentId, StringComparer.Ordinal);
            Dictionary<string, SentenceRecordData> sentenceById = savedSentences.ToDictionary(item => item.sentenceId, StringComparer.Ordinal);
            HashSet<string> personSet = new HashSet<string>(JusticeModelUtility.C(people), StringComparer.Ordinal);
            foreach (CourtRecordData court in savedCourts)
            {
                if (definitions == null || !definitions.TryGet(court.courtDefinitionId, out CourtDefinition _) || !definitions.TryGet(court.justiceInstitutionDefinitionId, out JusticeInstitutionDefinition _)) { failure = $"Court '{court.courtId}' has missing definitions."; return false; }
                foreach (string jurisdictionId in JusticeModelUtility.C(court.jurisdictionIds)) if (governments == null || !governments.TryGetJurisdiction(jurisdictionId, out _)) { failure = $"Court '{court.courtId}' references missing jurisdiction."; return false; }
                foreach (string territoryId in JusticeModelUtility.C(court.territoryIds)) if (governments == null || !governments.TryGetTerritory(territoryId, out _)) { failure = $"Court '{court.courtId}' references missing territory."; return false; }
            }
            foreach (ArrestRecordData arrest in savedArrests)
            {
                if (definitions == null || !definitions.TryGet(arrest.arrestDefinitionId, out ArrestDefinition _)) { failure = $"Arrest '{arrest.arrestId}' has missing definition."; return false; }
                if (!string.IsNullOrWhiteSpace(arrest.custodyId) && !custodyById.ContainsKey(arrest.custodyId)) { failure = $"Arrest '{arrest.arrestId}' references missing custody."; return false; }
                if (personSet.Count > 0 && !personSet.Contains(arrest.arrestedPersonId)) { failure = $"Arrest '{arrest.arrestId}' references missing Person."; return false; }
            }
            foreach (CustodyRecordData custody in savedCustody)
                if (string.IsNullOrWhiteSpace(custody.personId) || custody.lifecycleState == CustodyLifecycleState.Unknown || custody.lifecycleState == CustodyLifecycleState.Invalid || custody.legalBasis == null || custody.legalBasis.kind == ArrestLegalBasisKind.Unknown) { failure = $"Custody '{custody.custodyId}' has no active legal basis or valid state."; return false; }
            foreach (ReleaseOrderRecordData release in savedReleases)
                if (!custodyById.ContainsKey(release.custodyId) || release.category == ReleaseCategory.Unknown) { failure = $"Release order '{release.releaseOrderId}' has invalid custody or category."; return false; }
            foreach (CourtCaseRecordData courtCase in savedCases)
            {
                if (!courtById.ContainsKey(courtCase.courtId) || courtCase.category == JusticeCaseCategory.Unknown || courtCase.lifecycleState == CourtCaseLifecycleState.Unknown || courtCase.lifecycleState == CourtCaseLifecycleState.Invalid) { failure = $"Case '{courtCase.caseId}' has invalid court or lifecycle."; return false; }
                foreach (string incidentId in JusticeModelUtility.C(courtCase.incidentIds)) if (crimes == null || !crimes.TryGetIncident(incidentId, out _)) { failure = $"Case '{courtCase.caseId}' references missing incident."; return false; }
                foreach (string chargeId in JusticeModelUtility.C(courtCase.chargeIds)) if (!chargeById.ContainsKey(chargeId)) { failure = $"Case '{courtCase.caseId}' references missing charge."; return false; }
            }
            foreach (ChargeRecordData charge in savedCharges)
            {
                if (!caseById.ContainsKey(charge.caseId) || definitions == null || !definitions.TryGet(charge.chargeDefinitionId, out ChargeDefinition _) || charge.lifecycleState == ChargeLifecycleState.Unknown || charge.lifecycleState == ChargeLifecycleState.Invalid) { failure = $"Charge '{charge.chargeId}' has invalid case, definition, or lifecycle."; return false; }
                if (crimes == null || !crimes.TryGetPotentialOffense(charge.potentialOffenseId, out _)) { failure = $"Charge '{charge.chargeId}' references missing potential offense."; return false; }
            }
            foreach (PleaRecordData plea in savedPleas)
                if (!caseById.ContainsKey(plea.caseId) || !chargeById.ContainsKey(plea.chargeId) || plea.category == PleaCategory.Unknown) { failure = $"Plea '{plea.pleaId}' has invalid references."; return false; }
            foreach (HearingRecordData hearing in savedHearings)
                if (!caseById.ContainsKey(hearing.caseId) || definitions == null || !definitions.TryGet(hearing.hearingDefinitionId, out HearingDefinition _) || hearing.lifecycleState == HearingLifecycleState.Unknown || hearing.lifecycleState == HearingLifecycleState.Invalid) { failure = $"Hearing '{hearing.hearingId}' has invalid references."; return false; }
            foreach (EvidenceSubmissionRecordData evidence in savedEvidence)
                if (!caseById.ContainsKey(evidence.caseId) || !string.IsNullOrWhiteSpace(evidence.hearingId) && !hearingById.ContainsKey(evidence.hearingId) || string.IsNullOrWhiteSpace(evidence.evidenceId)) { failure = $"Evidence submission '{evidence.evidenceSubmissionId}' has invalid references."; return false; }
            foreach (ProceduralRulingRecordData ruling in savedRulings)
                if (!caseById.ContainsKey(ruling.caseId) || ruling.category == ProceduralRulingCategory.Unknown) { failure = $"Procedural ruling '{ruling.rulingId}' has invalid references."; return false; }
            foreach (FindingRecordData finding in savedFindings)
                if (!caseById.ContainsKey(finding.caseId) || !string.IsNullOrWhiteSpace(finding.chargeId) && !chargeById.ContainsKey(finding.chargeId) || finding.category == FindingCategory.Unknown) { failure = $"Finding '{finding.findingId}' has invalid references."; return false; }
            foreach (JudgmentRecordData judgment in savedJudgments)
                if (!caseById.ContainsKey(judgment.caseId) || !courtById.ContainsKey(judgment.courtId) || judgment.chargeOutcomes.Any(item => !chargeById.ContainsKey(item.chargeId) || item.outcome == JudgmentOutcome.Unknown)) { failure = $"Judgment '{judgment.judgmentId}' has invalid references or outcomes."; return false; }
            foreach (SentenceRecordData sentence in savedSentences)
                if (!judgmentById.ContainsKey(sentence.judgmentId) || definitions == null || !definitions.TryGet(sentence.sentenceDefinitionId, out SentenceDefinition _) || sentence.components.Any(item => item.category == SentenceCategory.Unknown || item.state == SentenceComponentState.Invalid)) { failure = $"Sentence '{sentence.sentenceId}' has invalid references or components."; return false; }
            foreach (RemedyRecordData remedy in savedRemedies)
                if (!caseById.ContainsKey(remedy.caseId) || !judgmentById.ContainsKey(remedy.judgmentId) || definitions == null || !definitions.TryGet(remedy.remedyDefinitionId, out RemedyDefinition _)) { failure = $"Remedy '{remedy.remedyId}' has invalid references."; return false; }
            foreach (AppealRecordData appeal in savedAppeals)
                if (!judgmentById.ContainsKey(appeal.sourceJudgmentId) || !courtById.ContainsKey(appeal.appellateCourtId) || definitions == null || !definitions.TryGet(appeal.appealDefinitionId, out AppealDefinition _)) { failure = $"Appeal '{appeal.appealId}' has invalid references."; return false; }
            foreach (ClemencyRecordData clemency in savedClemencies)
                if (!judgmentById.ContainsKey(clemency.judgmentId) || !string.IsNullOrWhiteSpace(clemency.sentenceId) && !sentenceById.ContainsKey(clemency.sentenceId) || definitions == null || !definitions.TryGet(clemency.clemencyDefinitionId, out ClemencyDefinition _)) { failure = $"Clemency '{clemency.clemencyId}' has invalid references."; return false; }
            return true;
        }

        public void Reset()
        {
            courts.Clear(); arrests.Clear(); custodyRecords.Clear(); releaseOrders.Clear(); charges.Clear(); cases.Clear(); pleas.Clear(); hearings.Clear(); evidenceSubmissions.Clear(); rulings.Clear(); findings.Clear(); judgments.Clear(); sentences.Clear(); remedies.Clear(); appeals.Clear(); clemencies.Clear(); transactions.Clear(); Revision = 0;
        }

        public void Dispose()
        {
            Reset();
            OperationCommitted = null;
            StateChanged = null;
            disposed = true;
        }

        private bool ValidateArrestBasis(ArrestDefinition definition, JusticeLegalBasisData basis, string personId, double worldTime, bool authorityBypass, long before, out JusticeOperationResult failure)
        {
            failure = null;
            if (basis == null || !definition.ValidLegalBases.Contains(basis.kind)) { failure = Fail(JusticeOperationCode.MissingLegalBasis, "Arrest legal basis is missing or unsupported by definition.", before); return false; }
            if (basis.kind == ArrestLegalBasisKind.ActiveArrestWarrant)
            {
                if (crimes == null || !crimes.TryGetWarrant(basis.warrantId, out WarrantRecordData warrant)) { failure = Fail(JusticeOperationCode.MissingWarrant, $"Warrant '{basis.warrantId}' is missing.", before); return false; }
                if (warrant.lifecycleState != WarrantLifecycleState.Issued && warrant.lifecycleState != WarrantLifecycleState.Active) { failure = Fail(JusticeOperationCode.InvalidState, "Warrant is not active for arrest.", before); return false; }
                if (warrant.expirationWorldTime >= 0d && warrant.expirationWorldTime < worldTime) { failure = Fail(JusticeOperationCode.Expired, "Warrant expired before arrest.", before); return false; }
                if (!string.Equals(warrant.scope?.targetId, personId, StringComparison.Ordinal)) { failure = Fail(JusticeOperationCode.InvalidReference, "Warrant scope does not target arrested Person.", before); return false; }
                if (definition.RequiredWarrantCategories.Count > 0 && crimes.TryGetWarrant(warrant.warrantId, out WarrantRecordData scoped) && registry.TryGet(scoped.warrantDefinitionId, out WarrantDefinition warrantDefinition) && !definition.RequiredWarrantCategories.Contains(warrantDefinition.Category)) { failure = Fail(JusticeOperationCode.InvalidReference, "Warrant category is not supported by arrest definition.", before); return false; }
                return true;
            }
            if (!definition.PermitsWarrantlessArrest && !authorityBypass) { failure = Fail(JusticeOperationCode.MissingWarrant, "Arrest definition requires a warrant.", before); return false; }
            if (basis.kind == ArrestLegalBasisKind.CaughtInAct && string.IsNullOrWhiteSpace(basis.incidentId)) { failure = Fail(JusticeOperationCode.MissingIncident, "Caught-in-act arrest requires an incident reference.", before); return false; }
            return true;
        }

        private bool ValidateJurisdictionAndTerritory(string jurisdictionId, string territoryId, long before, out JusticeOperationResult result)
        {
            result = null;
            if (!string.IsNullOrWhiteSpace(jurisdictionId) && (governments == null || !governments.TryGetJurisdiction(jurisdictionId, out _))) { result = Fail(JusticeOperationCode.MissingJurisdiction, $"Jurisdiction '{jurisdictionId}' is missing.", before); return false; }
            if (!string.IsNullOrWhiteSpace(territoryId) && (governments == null || !governments.TryGetTerritory(territoryId, out _))) { result = Fail(JusticeOperationCode.InvalidReference, $"Territory '{territoryId}' is missing.", before); return false; }
            return true;
        }

        private bool HasAuthority(string grantId) => !string.IsNullOrWhiteSpace(grantId) && authority != null && authority.TryGetGrant(grantId, out OrganizationAuthoritySnapshot grant) && grant.LifecycleState == OrganizationAuthorityGrantLifecycleState.Active;
        private static bool CompareSufficiency(EvidenceSufficiencyState actual, EvidenceSufficiencyState required) => Rank(actual) >= Rank(required);
        private static int Rank(EvidenceSufficiencyState value) => value switch { EvidenceSufficiencyState.None => 0, EvidenceSufficiencyState.Weak => 1, EvidenceSufficiencyState.Partial => 2, EvidenceSufficiencyState.Substantial => 3, EvidenceSufficiencyState.ThresholdMet => 4, EvidenceSufficiencyState.Contradicted => -1, EvidenceSufficiencyState.Disputed => -1, _ => 0 };
        private bool ValidatePerson(string personId) => !string.IsNullOrWhiteSpace(personId) && (personIds.Count == 0 || personIds.Contains(personId));
        private bool Ready(out JusticeOperationResult failure) { if (disposed) { failure = Fail(JusticeOperationCode.Disposed, "Justice runtime is disposed.", Revision); return false; } if (registry == null || governments == null || laws == null || crimes == null) { failure = Fail(JusticeOperationCode.InvalidState, "Justice runtime dependencies are not ready.", Revision); return false; } failure = null; return true; }
        private bool Def<T>(string id, out T definition) where T : class, IGameDefinition { definition = null; return registry != null && registry.TryGet(JusticeModelUtility.N(id), out definition); }
        private bool Duplicate(string tx, string operation, string subject, long before, out JusticeOperationResult result) { tx = JusticeModelUtility.N(tx); result = null; if (string.IsNullOrEmpty(tx) || !transactions.TryGetValue(tx, out JusticeTransactionRecordData existing)) return false; result = existing.operation == operation && existing.subjectId == subject ? JusticeOperationResult.Success("Duplicate justice transaction ignored.", before, before, subject, duplicate: true) : Fail(JusticeOperationCode.InvalidRequest, $"Transaction '{tx}' has different identity.", before); return true; }
        private void Complete(string tx, string operation, string subject) { tx = JusticeModelUtility.N(tx); if (!string.IsNullOrEmpty(tx)) transactions[tx] = new JusticeTransactionRecordData { transactionId = tx, operation = operation, subjectId = subject, revision = Revision + 1 }; }
        private JusticeOperationResult Commit(JusticeOperationResult result) { Action<JusticeOperationResult> handlers = OperationCommitted; if (handlers != null) foreach (Action<JusticeOperationResult> handler in handlers.GetInvocationList()) try { handler(result); } catch (Exception exception) { Debug.LogException(exception); } JusticeMutationEvent change = new JusticeMutationEvent(transactions.Values.Where(item => item.revision == Revision && item.subjectId == result.SubjectId).OrderBy(item => item.transactionId, StringComparer.Ordinal).FirstOrDefault()?.operation ?? string.Empty, result.SubjectId, Revision, result); Action<JusticeMutationEvent> changeHandlers = StateChanged; if (changeHandlers != null) foreach (Action<JusticeMutationEvent> handler in changeHandlers.GetInvocationList()) try { handler(change); } catch (Exception exception) { Debug.LogException(exception); } return result; }
        private JusticeOperationResult Fail(JusticeOperationCode code, string message, long revision) => JusticeOperationResult.Failure(code, message, revision);
        private static bool Unique(IEnumerable<string> values, string label, out string failure) { string[] ids = (values ?? Array.Empty<string>()).Select(JusticeModelUtility.N).ToArray(); if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) { failure = $"Justice save contains invalid or duplicate {label} IDs."; return false; } failure = string.Empty; return true; }
        private void RestoreInternal(JusticeRuntimeSaveData source) { Reset(); JusticeRuntimeSaveData data = source.Clone(); worldId = data.worldId; Revision = data.revision; foreach (var item in data.courts) courts[item.courtId] = item; foreach (var item in data.arrests) arrests[item.arrestId] = item; foreach (var item in data.custodyRecords) custodyRecords[item.custodyId] = item; foreach (var item in data.releaseOrders) releaseOrders[item.releaseOrderId] = item; foreach (var item in data.charges) charges[item.chargeId] = item; foreach (var item in data.cases) cases[item.caseId] = item; foreach (var item in data.pleas) pleas[item.pleaId] = item; foreach (var item in data.hearings) hearings[item.hearingId] = item; foreach (var item in data.evidenceSubmissions) evidenceSubmissions[item.evidenceSubmissionId] = item; foreach (var item in data.rulings) rulings[item.rulingId] = item; foreach (var item in data.findings) findings[item.findingId] = item; foreach (var item in data.judgments) judgments[item.judgmentId] = item; foreach (var item in data.sentences) sentences[item.sentenceId] = item; foreach (var item in data.remedies) remedies[item.remedyId] = item; foreach (var item in data.appeals) appeals[item.appealId] = item; foreach (var item in data.clemencies) clemencies[item.clemencyId] = item; foreach (var item in data.transactions) transactions[item.transactionId] = item; }
        private static T[] CloneArray<T>(IEnumerable<T> source) where T : class => (source ?? Array.Empty<T>()).Select(item => item switch { CourtRecordData value => value.Clone() as T, ArrestRecordData value => value.Clone() as T, CustodyRecordData value => value.Clone() as T, ReleaseOrderRecordData value => value.Clone() as T, ChargeRecordData value => value.Clone() as T, CourtCaseRecordData value => value.Clone() as T, PleaRecordData value => value.Clone() as T, HearingRecordData value => value.Clone() as T, EvidenceSubmissionRecordData value => value.Clone() as T, ProceduralRulingRecordData value => value.Clone() as T, FindingRecordData value => value.Clone() as T, JudgmentRecordData value => value.Clone() as T, SentenceRecordData value => value.Clone() as T, RemedyRecordData value => value.Clone() as T, AppealRecordData value => value.Clone() as T, ClemencyRecordData value => value.Clone() as T, JusticeTransactionRecordData value => value.Clone() as T, _ => null }).Where(item => item != null).ToArray();
        private static bool TryClone<T>(IDictionary<string, T> source, string id, Func<T, T> clone, out T value) where T : class { if (source.TryGetValue(JusticeModelUtility.N(id), out T found)) { value = clone(found); return true; } value = null; return false; }
    }
}
