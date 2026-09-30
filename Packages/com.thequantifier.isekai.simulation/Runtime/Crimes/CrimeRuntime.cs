using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Crimes
{
    public sealed class CrimeRuntime : IDisposable
    {
        private readonly Dictionary<string, CrimeIncidentRecordData> incidents = new Dictionary<string, CrimeIncidentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CrimeReportRecordData> reports = new Dictionary<string, CrimeReportRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CrimeAllegationRecordData> allegations = new Dictionary<string, CrimeAllegationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CrimeSuspectRecordData> suspects = new Dictionary<string, CrimeSuspectRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CrimeEvidenceLinkRecordData> evidenceLinks = new Dictionary<string, CrimeEvidenceLinkRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PotentialOffenseRecordData> potentialOffenses = new Dictionary<string, PotentialOffenseRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InvestigationRecordData> investigations = new Dictionary<string, InvestigationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WarrantRequestRecordData> warrantRequests = new Dictionary<string, WarrantRequestRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WarrantRecordData> warrants = new Dictionary<string, WarrantRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WantedStatusRecordData> wantedStatuses = new Dictionary<string, WantedStatusRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WantedNoticeRecordData> wantedNotices = new Dictionary<string, WantedNoticeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CrimeTransactionRecordData> transactions = new Dictionary<string, CrimeTransactionRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private GovernmentRuntime governments;
        private LegalRuntime laws;
        private OrganizationAuthorityRuntime authority;
        private DiplomacyRuntime diplomacy;
        private HashSet<string> personIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> placeIds = new HashSet<string>(StringComparer.Ordinal);
        private string worldId = string.Empty;
        private bool disposed;

        public long Revision { get; private set; }
        public event Action<CrimeOperationResult> OperationCommitted;
        public event Action<CrimeMutationEvent> StateChanged;

        public IReadOnlyList<CrimeIncidentRecordData> Incidents => incidents.Values.OrderBy(item => item.occurrenceStartWorldTime).ThenBy(item => item.incidentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CrimeReportRecordData> Reports => reports.Values.OrderBy(item => item.submittedWorldTime).ThenBy(item => item.reportId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CrimeAllegationRecordData> Allegations => allegations.Values.OrderBy(item => item.incidentId, StringComparer.Ordinal).ThenBy(item => item.allegationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CrimeSuspectRecordData> Suspects => suspects.Values.OrderBy(item => item.incidentId, StringComparer.Ordinal).ThenBy(item => item.subjectId, StringComparer.Ordinal).ThenBy(item => item.suspectId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CrimeEvidenceLinkRecordData> EvidenceLinks => evidenceLinks.Values.OrderBy(item => item.incidentId, StringComparer.Ordinal).ThenBy(item => item.evidenceLinkId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PotentialOffenseRecordData> PotentialOffenses => potentialOffenses.Values.OrderBy(item => item.incidentId, StringComparer.Ordinal).ThenBy(item => item.offenseDefinitionId, StringComparer.Ordinal).ThenBy(item => item.potentialOffenseId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<InvestigationRecordData> Investigations => investigations.Values.OrderBy(item => item.openedWorldTime).ThenBy(item => item.investigationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WarrantRequestRecordData> WarrantRequests => warrantRequests.Values.OrderBy(item => item.requestedWorldTime).ThenBy(item => item.warrantRequestId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WarrantRecordData> Warrants => warrants.Values.OrderBy(item => item.issuedWorldTime).ThenBy(item => item.warrantId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WantedStatusRecordData> WantedStatuses => wantedStatuses.Values.OrderBy(item => item.activeWorldTime).ThenBy(item => item.wantedStatusId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WantedNoticeRecordData> WantedNotices => wantedNotices.Values.OrderBy(item => item.publishedWorldTime).ThenBy(item => item.noticeId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitions, GovernmentRuntime governmentRuntime, LegalRuntime legalRuntime, OrganizationAuthorityRuntime authorityRuntime, DiplomacyRuntime diplomacyRuntime, string runtimeWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            registry = definitions ?? registry;
            governments = governmentRuntime;
            laws = legalRuntime;
            authority = authorityRuntime;
            diplomacy = diplomacyRuntime;
            worldId = CrimeModelUtility.N(runtimeWorldId);
            personIds = new HashSet<string>(CrimeModelUtility.C(knownPersons), StringComparer.Ordinal);
            placeIds = new HashSet<string>(CrimeModelUtility.C(knownPlaces), StringComparer.Ordinal);
        }

        public bool TryGetIncident(string id, out CrimeIncidentRecordData value) => TryClone(incidents, id, item => item.Clone(), out value);
        public bool TryGetReport(string id, out CrimeReportRecordData value) => TryClone(reports, id, item => item.Clone(), out value);
        public bool TryGetPotentialOffense(string id, out PotentialOffenseRecordData value) => TryClone(potentialOffenses, id, item => item.Clone(), out value);
        public bool TryGetSuspect(string id, out CrimeSuspectRecordData value) => TryClone(suspects, id, item => item.Clone(), out value);
        public bool TryGetWarrantRequest(string id, out WarrantRequestRecordData value) => TryClone(warrantRequests, id, item => item.Clone(), out value);
        public bool TryGetWarrant(string id, out WarrantRecordData value) => TryClone(warrants, id, item => item.Clone(), out value);
        public bool TryGetWantedStatus(string id, out WantedStatusRecordData value) => TryClone(wantedStatuses, id, item => item.Clone(), out value);

        public CrimeOperationResult RecordIncident(CrimeIncidentRequest request)
        {
            request ??= new CrimeIncidentRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.incidentId);
            if (Duplicate(request.transactionId, "record-incident", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || incidents.ContainsKey(id) || request.category == CrimeIncidentCategory.Unknown) return Fail(CrimeOperationCode.InvalidRequest, "Crime incident ID or category is invalid.", before);
            string[] jurisdictions = CrimeModelUtility.C(request.jurisdictionIds);
            foreach (string jurisdictionId in jurisdictions)
                if (governments == null || !governments.TryGetJurisdiction(jurisdictionId, out _)) return Fail(CrimeOperationCode.MissingJurisdiction, $"Jurisdiction '{jurisdictionId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.primaryTerritoryId) && (governments == null || !governments.TryGetTerritory(request.primaryTerritoryId, out _))) return Fail(CrimeOperationCode.InvalidReference, $"Territory '{request.primaryTerritoryId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.primaryPlaceId) && placeIds.Count > 0 && !placeIds.Contains(request.primaryPlaceId.Trim())) return Fail(CrimeOperationCode.InvalidReference, $"Place '{request.primaryPlaceId}' is missing.", before);
            if (request.occurrenceEndWorldTime > 0d && request.occurrenceEndWorldTime < request.occurrenceStartWorldTime) return Fail(CrimeOperationCode.InvalidRequest, "Crime incident occurrence end precedes its start.", before);
            CrimeSubjectReferenceData[] subjects = (request.involvedSubjects ?? Array.Empty<CrimeSubjectReferenceData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.subjectType).ThenBy(item => item.subjectId, StringComparer.Ordinal).ToArray();
            foreach (CrimeSubjectReferenceData subject in subjects)
                if (string.Equals(subject.subjectType, "Person", StringComparison.Ordinal) && !ValidatePerson(subject.subjectId)) return Fail(CrimeOperationCode.InvalidReference, $"Person '{subject.subjectId}' is missing.", before);
            string[] victimIds = CrimeModelUtility.C(request.victimIds);
            string[] witnessIds = CrimeModelUtility.C(request.witnessIds);
            foreach (string personId in victimIds.Concat(witnessIds)) if (!ValidatePerson(personId)) return Fail(CrimeOperationCode.InvalidReference, $"Victim or witness Person '{personId}' is missing.", before);
            CrimeIncidentRecordData record = new CrimeIncidentRecordData { incidentId = id, category = request.category, lifecycleState = CrimeIncidentLifecycleState.Recorded, occurrenceStartWorldTime = request.occurrenceStartWorldTime, occurrenceEndWorldTime = request.occurrenceEndWorldTime, discoveryWorldTime = request.discoveryWorldTime, reportingWorldTime = request.reportingWorldTime, historicalEventIds = CrimeModelUtility.C(request.historicalEventIds), primaryPlaceId = CrimeModelUtility.N(request.primaryPlaceId), primaryTerritoryId = CrimeModelUtility.N(request.primaryTerritoryId), involvedSubjects = subjects, victimIds = victimIds, witnessIds = witnessIds, jurisdictionIds = jurisdictions, visibility = request.visibility, provenanceId = CrimeModelUtility.N(request.provenanceId), revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Crime incident previewed.", before, before, id, preview: true);
            incidents[id] = record;
            Complete(request.transactionId, "record-incident", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Crime incident recorded atomically.", before, Revision, id));
        }

        public CrimeOperationResult SubmitReport(CrimeReportRequest request)
        {
            request ??= new CrimeReportRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.reportId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            if (Duplicate(request.transactionId, "submit-report", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || reports.ContainsKey(id) || request.category == CrimeReportCategory.Unknown) return Fail(CrimeOperationCode.InvalidRequest, "Crime report ID or category is invalid.", before);
            if (!incidents.TryGetValue(incidentId, out CrimeIncidentRecordData incident)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.reporterSubjectId) && string.Equals(CrimeModelUtility.N(request.reporterSubjectType), "Person", StringComparison.Ordinal) && !ValidatePerson(request.reporterSubjectId)) return Fail(CrimeOperationCode.InvalidReference, $"Reporter '{request.reporterSubjectId}' is missing.", before);
            if (request.reporterReliabilityBasisPoints < 0 || request.reporterReliabilityBasisPoints > 10000) return Fail(CrimeOperationCode.InvalidRequest, "Reporter reliability must be between 0 and 10,000 basis points.", before);
            if (incident.lifecycleState == CrimeIncidentLifecycleState.Invalid || incident.lifecycleState == CrimeIncidentLifecycleState.Merged) return Fail(CrimeOperationCode.InvalidState, "Incident is not reportable.", before);
            CrimeReportRecordData record = new CrimeReportRecordData { reportId = id, incidentId = incidentId, category = request.category, lifecycleState = CrimeReportLifecycleState.Submitted, reporterSubjectId = CrimeModelUtility.N(request.reporterSubjectId), reporterSubjectType = CrimeModelUtility.N(request.reporterSubjectType), anonymous = request.anonymous, firstHand = request.firstHand, sourceRumorId = CrimeModelUtility.N(request.sourceRumorId), submittedWorldTime = request.submittedWorldTime, reporterReliabilityBasisPoints = request.reporterReliabilityBasisPoints, visibility = request.visibility, provenanceId = CrimeModelUtility.N(request.provenanceId), revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Crime report previewed.", before, before, id, preview: true);
            reports[id] = record;
            incident.reportIds = CrimeModelUtility.C(incident.reportIds.Concat(new[] { id }));
            if (incident.reportingWorldTime < 0d || request.submittedWorldTime < incident.reportingWorldTime) incident.reportingWorldTime = request.submittedWorldTime;
            if (request.category == CrimeReportCategory.OfficialReport && (incident.discoveryWorldTime < 0d || request.submittedWorldTime < incident.discoveryWorldTime)) incident.discoveryWorldTime = request.submittedWorldTime;
            incident.lifecycleState = CrimeIncidentLifecycleState.AwaitingReview;
            incident.revision++;
            Complete(request.transactionId, "submit-report", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Crime report submitted and indexed.", before, Revision, id));
        }

        public CrimeOperationResult EvaluatePotentialOffense(PotentialOffenseEvaluationRequest request)
        {
            request ??= new PotentialOffenseEvaluationRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.potentialOffenseId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            if (Duplicate(request.transactionId, "evaluate-offense", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || potentialOffenses.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, "Potential offense ID is invalid or already exists.", before);
            if (!incidents.TryGetValue(incidentId, out CrimeIncidentRecordData incident)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!Def(request.offenseDefinitionId, out LegalOffenseDefinition definition)) return Fail(CrimeOperationCode.MissingDefinition, $"Offense definition '{request.offenseDefinitionId}' is missing.", before);
            if (!definition.SupportedStages.Contains(request.stage) || !definition.SupportedParticipation.Contains(request.participation)) return Fail(CrimeOperationCode.InvalidRequest, "Offense stage or participation is not supported by definition.", before);
            string[] allegedActorIds = CrimeModelUtility.C(request.allegedActorIds);
            string[] victimOrTargetIds = CrimeModelUtility.C(request.victimOrTargetIds);
            foreach (string personId in allegedActorIds.Concat(victimOrTargetIds)) if (!ValidatePerson(personId)) return Fail(CrimeOperationCode.InvalidReference, $"Potential offense Person '{personId}' is missing.", before);
            string actorId = allegedActorIds.FirstOrDefault();
            LegalApplicabilityResult legal = laws?.Evaluate(new LegalApplicabilityRequest { personId = actorId, territoryId = incident.primaryTerritoryId, placeId = incident.primaryPlaceId, actionId = string.IsNullOrWhiteSpace(request.actionId) ? definition.LegalActionId : request.actionId, worldTime = incident.occurrenceStartWorldTime });
            LegalApplicabilityStatus legalStatus = legal?.Status ?? LegalApplicabilityStatus.Unknown;
            PotentialOffenseStatus status = ResolvePotentialOffenseStatus(legalStatus, request.evidenceSufficiency, definition.MinimumChargeThreshold);
            OffenseElementEvaluationData[] elements = BuildElements(request.elementEvaluations, definition);
            LegalProvisionRecordData applicableProvision = legal?.ApplicableProvisions?.FirstOrDefault();
            PotentialOffenseRecordData record = new PotentialOffenseRecordData { potentialOffenseId = id, incidentId = incidentId, offenseDefinitionId = definition.Id, legalInstrumentId = applicableProvision?.instrumentId ?? string.Empty, legalProvisionId = applicableProvision?.provisionId ?? string.Empty, legalProvisionVersion = applicableProvision?.VersionAt(incident.occurrenceStartWorldTime)?.version ?? 0, jurisdictionIds = CrimeModelUtility.C(incident.jurisdictionIds), allegedActorIds = allegedActorIds, victimOrTargetIds = victimOrTargetIds, eventIds = CrimeModelUtility.C(incident.historicalEventIds), placeId = incident.primaryPlaceId, territoryId = incident.primaryTerritoryId, occurrenceStartWorldTime = incident.occurrenceStartWorldTime, occurrenceEndWorldTime = incident.occurrenceEndWorldTime, stage = request.stage, participation = request.participation, elementEvaluations = elements, legalApplicabilityStatus = legalStatus, status = status, evidenceSufficiency = request.evidenceSufficiency, visibility = request.visibility, provenanceId = CrimeModelUtility.N(request.provenanceId), revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Potential offense previewed.", before, before, id, preview: true);
            potentialOffenses[id] = record;
            incident.potentialOffenseIds = CrimeModelUtility.C(incident.potentialOffenseIds.Concat(new[] { id }));
            incident.lifecycleState = CrimeIncidentLifecycleState.UnderReview;
            incident.revision++;
            Complete(request.transactionId, "evaluate-offense", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Potential offense evaluated against current law.", before, Revision, id));
        }

        public CrimeOperationResult RecordAllegation(CrimeAllegationRequest request)
        {
            request ??= new CrimeAllegationRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.allegationId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            string reportId = CrimeModelUtility.N(request.reportId);
            string offenseId = CrimeModelUtility.N(request.potentialOffenseId);
            if (Duplicate(request.transactionId, "record-allegation", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || allegations.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, "Allegation ID is invalid or already exists.", before);
            if (!incidents.ContainsKey(incidentId)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!reports.TryGetValue(reportId, out CrimeReportRecordData report) || report.incidentId != incidentId) return Fail(CrimeOperationCode.MissingReport, "Report is missing or does not belong to incident.", before);
            if (!potentialOffenses.ContainsKey(offenseId)) return Fail(CrimeOperationCode.MissingOffense, $"Potential offense '{offenseId}' is missing.", before);
            foreach (string personId in CrimeModelUtility.C(new[] { request.claimedActorId, request.claimedVictimId, request.claimedTargetId })) if (!ValidatePerson(personId)) return Fail(CrimeOperationCode.InvalidReference, $"Allegation Person '{personId}' is missing.", before);
            CrimeAllegationRecordData record = new CrimeAllegationRecordData { allegationId = id, incidentId = incidentId, reportId = reportId, potentialOffenseId = offenseId, claimedActorId = CrimeModelUtility.N(request.claimedActorId), claimedVictimId = CrimeModelUtility.N(request.claimedVictimId), claimedTargetId = CrimeModelUtility.N(request.claimedTargetId), conductSummary = request.conductSummary ?? string.Empty, lifecycleState = AllegationLifecycleState.Recorded, sufficiency = request.sufficiency, visibility = request.visibility, provenanceId = CrimeModelUtility.N(request.provenanceId), revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Allegation previewed.", before, before, id, preview: true);
            allegations[id] = record;
            report.allegationIds = CrimeModelUtility.C(report.allegationIds.Concat(new[] { id }));
            report.allegedOffenseIds = CrimeModelUtility.C(report.allegedOffenseIds.Concat(new[] { offenseId }));
            report.revision++;
            Complete(request.transactionId, "record-allegation", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Allegation recorded without mutating legal records.", before, Revision, id));
        }

        public CrimeOperationResult AddSuspect(CrimeSuspectRequest request)
        {
            request ??= new CrimeSuspectRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.suspectId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            if (Duplicate(request.transactionId, "add-suspect", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || suspects.ContainsKey(id) || string.IsNullOrWhiteSpace(request.subjectId)) return Fail(CrimeOperationCode.InvalidRequest, "Suspect identity is invalid.", before);
            if (!incidents.TryGetValue(incidentId, out CrimeIncidentRecordData incident)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.potentialOffenseId) && !potentialOffenses.ContainsKey(CrimeModelUtility.N(request.potentialOffenseId))) return Fail(CrimeOperationCode.MissingOffense, $"Potential offense '{request.potentialOffenseId}' is missing.", before);
            if (string.Equals(CrimeModelUtility.N(request.subjectType), "Person", StringComparison.Ordinal) && !ValidatePerson(request.subjectId)) return Fail(CrimeOperationCode.InvalidReference, $"Suspect Person '{request.subjectId}' is missing.", before);
            CrimeSuspectRecordData record = new CrimeSuspectRecordData { suspectId = id, incidentId = incidentId, potentialOffenseId = CrimeModelUtility.N(request.potentialOffenseId), subjectId = CrimeModelUtility.N(request.subjectId), subjectType = CrimeModelUtility.N(request.subjectType), participation = request.participation, lifecycleState = SuspectLifecycleState.Suspected, basis = request.basis ?? string.Empty, createdWorldTime = request.worldTime, visibility = request.visibility, provenanceId = CrimeModelUtility.N(request.provenanceId), revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Suspect previewed.", before, before, id, preview: true);
            suspects[id] = record;
            incident.suspectIds = CrimeModelUtility.C(incident.suspectIds.Concat(new[] { id }));
            incident.revision++;
            Complete(request.transactionId, "add-suspect", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Suspect recorded as a lifecycle record.", before, Revision, id));
        }

        public CrimeOperationResult TransitionSuspect(CrimeSuspectTransitionRequest request)
        {
            request ??= new CrimeSuspectTransitionRequest();
            long before = Revision;
            string id = CrimeModelUtility.N(request.suspectId);
            if (!Ready(out CrimeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "transition-suspect", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (!suspects.TryGetValue(id, out CrimeSuspectRecordData current) || request.targetState == SuspectLifecycleState.Unknown) return Fail(CrimeOperationCode.InvalidState, "Suspect or target lifecycle state is invalid.", before);
            CrimeSuspectRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.misidentified = request.misidentified;
            changed.clearedReason = request.reason ?? string.Empty;
            if (request.targetState == SuspectLifecycleState.Cleared || request.targetState == SuspectLifecycleState.Misidentified || request.targetState == SuspectLifecycleState.NoLongerSought || request.targetState == SuspectLifecycleState.Historical) changed.endedWorldTime = request.worldTime;
            changed.revision++;
            if (request.preview) return CrimeOperationResult.Success("Suspect transition previewed.", before, before, id, preview: true);
            suspects[id] = changed;
            Complete(request.transactionId, "transition-suspect", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Suspect lifecycle transitioned without deleting the original record.", before, Revision, id));
        }

        public CrimeOperationResult LinkEvidence(CrimeEvidenceLinkRequest request)
        {
            request ??= new CrimeEvidenceLinkRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.evidenceLinkId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            if (Duplicate(request.transactionId, "link-evidence", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || evidenceLinks.ContainsKey(id) || string.IsNullOrWhiteSpace(request.evidenceId)) return Fail(CrimeOperationCode.InvalidRequest, "Evidence link identity is invalid.", before);
            if (!incidents.TryGetValue(incidentId, out CrimeIncidentRecordData incident)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.reportId) && !reports.ContainsKey(CrimeModelUtility.N(request.reportId))) return Fail(CrimeOperationCode.MissingReport, "Report reference is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.potentialOffenseId) && !potentialOffenses.ContainsKey(CrimeModelUtility.N(request.potentialOffenseId))) return Fail(CrimeOperationCode.MissingOffense, "Potential offense reference is missing.", before);
            CrimeEvidenceLinkRecordData record = new CrimeEvidenceLinkRecordData { evidenceLinkId = id, incidentId = incidentId, reportId = CrimeModelUtility.N(request.reportId), potentialOffenseId = CrimeModelUtility.N(request.potentialOffenseId), evidenceId = CrimeModelUtility.N(request.evidenceId), relevance = request.relevance, sufficiency = request.sufficiency, sourceId = CrimeModelUtility.N(request.sourceId), linkedWorldTime = request.worldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Evidence link previewed.", before, before, id, preview: true);
            evidenceLinks[id] = record;
            incident.evidenceLinkIds = CrimeModelUtility.C(incident.evidenceLinkIds.Concat(new[] { id }));
            incident.revision++;
            Complete(request.transactionId, "link-evidence", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Evidence linked to crime records without owning evidence content.", before, Revision, id));
        }

        public CrimeOperationResult OpenInvestigation(InvestigationRecordRequest request)
        {
            request ??= new InvestigationRecordRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.investigationId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            if (Duplicate(request.transactionId, "open-investigation", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || investigations.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, "Investigation ID is invalid.", before);
            if (!incidents.TryGetValue(incidentId, out CrimeIncidentRecordData incident)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.responsibleGovernmentId) && (governments == null || !governments.TryGetGovernment(request.responsibleGovernmentId, out _))) return Fail(CrimeOperationCode.MissingJurisdiction, "Responsible government is missing.", before);
            InvestigationRecordData record = new InvestigationRecordData { investigationId = id, incidentId = incidentId, responsibleGovernmentId = CrimeModelUtility.N(request.responsibleGovernmentId), responsibleOrganizationId = CrimeModelUtility.N(request.responsibleOrganizationId), reviewerPersonIds = CrimeModelUtility.C(request.reviewerPersonIds), evidenceLinkIds = CrimeModelUtility.C(incident.evidenceLinkIds), state = CrimeIncidentLifecycleState.UnderReview, openedWorldTime = request.openedWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Investigation previewed.", before, before, id, preview: true);
            investigations[id] = record;
            incident.lifecycleState = CrimeIncidentLifecycleState.UnderReview;
            incident.revision++;
            Complete(request.transactionId, "open-investigation", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Investigation opened with explicit responsible authority.", before, Revision, id));
        }

        public CrimeOperationResult RequestWarrant(WarrantRequestCreateRequest request)
        {
            request ??= new WarrantRequestCreateRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.warrantRequestId);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            string offenseId = CrimeModelUtility.N(request.potentialOffenseId);
            if (Duplicate(request.transactionId, "request-warrant", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || warrantRequests.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, "Warrant request ID is invalid.", before);
            if (!Def(request.warrantDefinitionId, out WarrantDefinition definition)) return Fail(CrimeOperationCode.MissingDefinition, $"Warrant definition '{request.warrantDefinitionId}' is missing.", before);
            if (!incidents.ContainsKey(incidentId)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (!potentialOffenses.TryGetValue(offenseId, out PotentialOffenseRecordData offense)) return Fail(CrimeOperationCode.MissingOffense, $"Potential offense '{offenseId}' is missing.", before);
            if (governments == null || !governments.TryGetGovernment(request.issuingGovernmentId, out _)) return Fail(CrimeOperationCode.MissingJurisdiction, "Issuing government is missing.", before);
            if (!ValidateScope(request.scope, definition, out string scopeFailure)) return Fail(CrimeOperationCode.InvalidRequest, scopeFailure, before);
            EvidenceSufficiencyState required = Max(definition.MinimumThreshold, Def(offense.offenseDefinitionId, out LegalOffenseDefinition offenseDefinition) ? offenseDefinition.MinimumWarrantThreshold : EvidenceSufficiencyState.Unknown);
            if (CompareSufficiency(request.assertedThreshold, required) < 0) return Fail(CrimeOperationCode.ThresholdNotMet, "Evidence threshold is not met for this warrant.", before);
            WarrantRequestRecordData record = new WarrantRequestRecordData { warrantRequestId = id, warrantDefinitionId = definition.Id, incidentId = incidentId, potentialOffenseId = offenseId, requestedByPersonId = CrimeModelUtility.N(request.requestedByPersonId), issuingGovernmentId = CrimeModelUtility.N(request.issuingGovernmentId), issuingOrganizationId = CrimeModelUtility.N(request.issuingOrganizationId), issuingOfficeId = CrimeModelUtility.N(request.issuingOfficeId), scope = request.scope?.Clone(), assertedThreshold = request.assertedThreshold, lifecycleState = WarrantRequestLifecycleState.Requested, requestedWorldTime = request.requestedWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Warrant request previewed.", before, before, id, preview: true);
            warrantRequests[id] = record;
            Complete(request.transactionId, "request-warrant", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Warrant request recorded pending explicit review.", before, Revision, id));
        }

        public CrimeOperationResult ReviewWarrantRequest(WarrantReviewRequest request)
        {
            request ??= new WarrantReviewRequest();
            long before = Revision;
            string id = CrimeModelUtility.N(request.warrantRequestId);
            if (!Ready(out CrimeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "review-warrant-request", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (!request.trustedSystemOperation && !HasAuthority(request.reviewId)) return Fail(CrimeOperationCode.MissingAuthority, "Warrant review requires explicit institutional authority.", before);
            if (!warrantRequests.TryGetValue(id, out WarrantRequestRecordData current) || current.lifecycleState != WarrantRequestLifecycleState.Requested && current.lifecycleState != WarrantRequestLifecycleState.UnderReview) return Fail(CrimeOperationCode.InvalidState, "Warrant request is missing or not reviewable.", before);
            WarrantRequestRecordData changed = current.Clone();
            changed.lifecycleState = request.approve ? WarrantRequestLifecycleState.Approved : WarrantRequestLifecycleState.Denied;
            changed.reviewId = CrimeModelUtility.N(request.reviewId);
            changed.denialReason = request.denialReason ?? string.Empty;
            changed.revision++;
            if (request.preview) return CrimeOperationResult.Success("Warrant review previewed.", before, before, id, preview: true);
            warrantRequests[id] = changed;
            Complete(request.transactionId, "review-warrant-request", id);
            Revision++;
            return Commit(CrimeOperationResult.Success(request.approve ? "Warrant request approved." : "Warrant request denied.", before, Revision, id));
        }

        public CrimeOperationResult IssueWarrant(WarrantIssueRequest request)
        {
            request ??= new WarrantIssueRequest();
            long before = Revision;
            string id = CrimeModelUtility.N(request.warrantId);
            if (!Ready(out CrimeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "issue-warrant", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (!request.trustedSystemOperation && !HasAuthority(request.issuedByPersonId)) return Fail(CrimeOperationCode.MissingAuthority, "Warrant issue requires explicit institutional authority.", before);
            if (string.IsNullOrEmpty(id) || warrants.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, "Warrant ID is invalid.", before);
            if (!warrantRequests.TryGetValue(CrimeModelUtility.N(request.warrantRequestId), out WarrantRequestRecordData source) || source.lifecycleState != WarrantRequestLifecycleState.Approved) return Fail(CrimeOperationCode.InvalidState, "Warrant request is not approved.", before);
            if (!Def(source.warrantDefinitionId, out WarrantDefinition definition)) return Fail(CrimeOperationCode.MissingDefinition, "Warrant definition is missing.", before);
            if (request.expirationWorldTime >= 0d && request.expirationWorldTime < request.activationWorldTime) return Fail(CrimeOperationCode.InvalidRequest, "Warrant expiration precedes activation.", before);
            WarrantRecordData warrant = new WarrantRecordData { warrantId = id, warrantDefinitionId = source.warrantDefinitionId, warrantRequestId = source.warrantRequestId, incidentId = source.incidentId, potentialOffenseId = source.potentialOffenseId, issuedByPersonId = CrimeModelUtility.N(request.issuedByPersonId), issuingGovernmentId = source.issuingGovernmentId, issuingOrganizationId = source.issuingOrganizationId, issuingOfficeId = source.issuingOfficeId, scope = source.scope?.Clone(), lifecycleState = request.activationWorldTime <= request.issuedWorldTime ? WarrantLifecycleState.Active : WarrantLifecycleState.Issued, issuedWorldTime = request.issuedWorldTime, activationWorldTime = request.activationWorldTime, expirationWorldTime = request.expirationWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Warrant issue previewed.", before, before, id, preview: true);
            warrants[id] = warrant;
            source.lifecycleState = WarrantRequestLifecycleState.Superseded;
            source.revision++;
            if (definition.CreatesDerivedWantedStatus && warrant.scope != null && warrant.scope.kind == WarrantScopeKind.Person && !string.IsNullOrWhiteSpace(warrant.scope.targetId))
            {
                string wantedDefinitionId = WantedDefinitionFor(definition.Category);
                if (!string.IsNullOrEmpty(wantedDefinitionId))
                {
                    wantedStatuses[$"{id}.wanted"] = new WantedStatusRecordData { wantedStatusId = $"{id}.wanted", wantedDefinitionId = wantedDefinitionId, warrantId = id, incidentIds = CrimeModelUtility.C(new[] { warrant.incidentId }), subjectId = warrant.scope.targetId, subjectType = "Person", jurisdictionId = warrant.scope.jurisdictionIds.FirstOrDefault() ?? string.Empty, territoryId = warrant.scope.territoryIds.FirstOrDefault() ?? string.Empty, purpose = PurposeFor(definition.Category), risk = WantedRiskAssessment.PotentiallyArmed, lifecycleState = WantedStatusLifecycleState.Active, derivedFromWarrant = true, activeWorldTime = warrant.activationWorldTime, expirationWorldTime = warrant.expirationWorldTime, visibility = request.visibility, revision = 1 };
                }
            }
            Complete(request.transactionId, "issue-warrant", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Warrant issued and derived wanted status synchronized.", before, Revision, id));
        }

        public CrimeOperationResult TransitionWarrant(WarrantTransitionRequest request)
        {
            request ??= new WarrantTransitionRequest();
            long before = Revision;
            string id = CrimeModelUtility.N(request.warrantId);
            if (!Ready(out CrimeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "transition-warrant", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (!warrants.TryGetValue(id, out WarrantRecordData current) || request.targetState == WarrantLifecycleState.Unknown) return Fail(CrimeOperationCode.MissingWarrant, "Warrant is missing or target state is invalid.", before);
            WarrantRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.supersededByWarrantId = CrimeModelUtility.N(request.supersededByWarrantId);
            changed.satisfactionRecordId = CrimeModelUtility.N(request.satisfactionRecordId);
            changed.revision++;
            if (request.preview) return CrimeOperationResult.Success("Warrant transition previewed.", before, before, id, preview: true);
            warrants[id] = changed;
            TransitionDerivedWantedForWarrant(id, request.targetState, request.worldTime);
            Complete(request.transactionId, "transition-warrant", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Warrant lifecycle transitioned and derived wanted state reconciled.", before, Revision, id));
        }

        public CrimeOperationResult CreateWantedStatus(WantedStatusRequest request)
        {
            request ??= new WantedStatusRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.wantedStatusId);
            if (Duplicate(request.transactionId, "create-wanted-status", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || wantedStatuses.ContainsKey(id) || string.IsNullOrWhiteSpace(request.subjectId)) return Fail(CrimeOperationCode.InvalidRequest, "Wanted status identity is invalid.", before);
            if (!Def(request.wantedDefinitionId, out WantedStatusDefinition definition)) return Fail(CrimeOperationCode.MissingDefinition, $"Wanted definition '{request.wantedDefinitionId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.warrantId) && !warrants.ContainsKey(CrimeModelUtility.N(request.warrantId))) return Fail(CrimeOperationCode.MissingWarrant, "Source warrant is missing.", before);
            if (request.expirationWorldTime >= 0d && request.expirationWorldTime < request.activeWorldTime) return Fail(CrimeOperationCode.InvalidRequest, "Wanted status expiration precedes activation.", before);
            string[] incidentIds = CrimeModelUtility.C(request.incidentIds);
            if (incidentIds.Any(incidentId => !incidents.ContainsKey(incidentId))) return Fail(CrimeOperationCode.MissingIncident, "A source incident is missing.", before);
            string[] wantedPlaceIds = CrimeModelUtility.C(request.placeIds);
            if (placeIds.Count > 0 && wantedPlaceIds.Any(placeId => !placeIds.Contains(placeId))) return Fail(CrimeOperationCode.InvalidReference, "A wanted-status place is missing.", before);
            if (!ValidateWantedScope(request.subjectId, request.subjectType, request.jurisdictionId, request.territoryId, before, out CrimeOperationResult scopeFailure)) return scopeFailure;
            WantedStatusRecordData record = new WantedStatusRecordData { wantedStatusId = id, wantedDefinitionId = definition.Id, warrantId = CrimeModelUtility.N(request.warrantId), incidentIds = incidentIds, subjectId = CrimeModelUtility.N(request.subjectId), subjectType = CrimeModelUtility.N(request.subjectType), jurisdictionId = CrimeModelUtility.N(request.jurisdictionId), territoryId = CrimeModelUtility.N(request.territoryId), placeIds = wantedPlaceIds, purpose = definition.Purpose, risk = request.risk, lifecycleState = WantedStatusLifecycleState.Active, derivedFromWarrant = request.derivedFromWarrant, activeWorldTime = request.activeWorldTime, expirationWorldTime = request.expirationWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Wanted status previewed.", before, before, id, preview: true);
            wantedStatuses[id] = record;
            Complete(request.transactionId, "create-wanted-status", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Wanted status recorded independently from suspect identity.", before, Revision, id));
        }

        public CrimeOperationResult UpsertWantedStatus(WantedStatusUpsertRequest request)
        {
            request ??= new WantedStatusUpsertRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            if (!Def(request.wantedDefinitionId, out WantedStatusDefinition definition)) return Fail(CrimeOperationCode.MissingDefinition, $"Wanted definition '{request.wantedDefinitionId}' is missing.", before);
            string subjectId = CrimeModelUtility.N(request.subjectId);
            string jurisdictionId = CrimeModelUtility.N(request.jurisdictionId);
            string territoryId = CrimeModelUtility.N(request.territoryId);
            string[] wantedPlaceIds = CrimeModelUtility.C(request.placeIds);
            string incidentId = CrimeModelUtility.N(request.incidentId);
            if (string.IsNullOrEmpty(subjectId)) return Fail(CrimeOperationCode.InvalidRequest, "Wanted subject is required.", before);
            if (!string.IsNullOrEmpty(incidentId) && !incidents.ContainsKey(incidentId)) return Fail(CrimeOperationCode.MissingIncident, $"Incident '{incidentId}' is missing.", before);
            if (placeIds.Count > 0 && wantedPlaceIds.Any(placeId => !placeIds.Contains(placeId))) return Fail(CrimeOperationCode.InvalidReference, "A wanted-status place is missing.", before);
            if (!ValidateWantedScope(subjectId, request.subjectType, jurisdictionId, territoryId, before, out CrimeOperationResult scopeFailure)) return scopeFailure;
            WantedStatusRecordData current = wantedStatuses.Values
                .Where(item => item.lifecycleState == WantedStatusLifecycleState.Active
                    && item.subjectId == subjectId
                    && item.jurisdictionId == jurisdictionId
                    && item.territoryId == territoryId
                    && CrimeModelUtility.C(item.placeIds).SequenceEqual(wantedPlaceIds))
                .OrderByDescending(item => WantedPurposeRank(item.purpose))
                .ThenBy(item => item.wantedStatusId, StringComparer.Ordinal)
                .FirstOrDefault();
            string id = current?.wantedStatusId ?? CrimeModelUtility.N(request.wantedStatusId);
            if (Duplicate(request.transactionId, "upsert-wanted-status", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id)) return Fail(CrimeOperationCode.InvalidRequest, "Wanted status ID is required when no matching active status exists.", before);

            WantedStatusRecordData changed;
            if (current == null)
            {
                if (wantedStatuses.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, $"Wanted status '{id}' already exists outside the active enforcement scope.", before);
                changed = new WantedStatusRecordData
                {
                    wantedStatusId = id,
                    wantedDefinitionId = definition.Id,
                    incidentIds = CrimeModelUtility.C(new[] { incidentId }),
                    subjectId = subjectId,
                    subjectType = CrimeModelUtility.N(request.subjectType),
                    jurisdictionId = jurisdictionId,
                    territoryId = territoryId,
                    placeIds = wantedPlaceIds,
                    purpose = definition.Purpose,
                    risk = request.risk,
                    lifecycleState = WantedStatusLifecycleState.Active,
                    activeWorldTime = request.activeWorldTime,
                    expirationWorldTime = request.expirationWorldTime,
                    visibility = request.visibility,
                    revision = 1
                };
            }
            else
            {
                changed = current.Clone();
                changed.incidentIds = CrimeModelUtility.C(changed.incidentIds.Concat(new[] { incidentId }));
                if (WantedPurposeRank(definition.Purpose) > WantedPurposeRank(changed.purpose))
                {
                    changed.wantedDefinitionId = definition.Id;
                    changed.purpose = definition.Purpose;
                }
                if (WantedRiskRank(request.risk) > WantedRiskRank(changed.risk)) changed.risk = request.risk;
                changed.activeWorldTime = Math.Min(changed.activeWorldTime, request.activeWorldTime);
                changed.expirationWorldTime = changed.expirationWorldTime < 0d || request.expirationWorldTime < 0d ? -1d : Math.Max(changed.expirationWorldTime, request.expirationWorldTime);
                changed.visibility = (PoliticalVisibility)Math.Max((int)changed.visibility, (int)request.visibility);
                changed.revision++;
            }
            if (request.preview) return CrimeOperationResult.Success(current == null ? "Wanted status creation previewed." : "Wanted status update previewed.", before, before, id, preview: true);
            wantedStatuses[id] = changed;
            Complete(request.transactionId, "upsert-wanted-status", id);
            Revision++;
            return Commit(CrimeOperationResult.Success(current == null ? "Wanted status created for the enforcement scope." : "Wanted status linked to the incident and escalated when required.", before, Revision, id));
        }

        public CrimeOperationResult TransitionWantedStatus(WantedStatusTransitionRequest request)
        {
            request ??= new WantedStatusTransitionRequest();
            long before = Revision;
            string id = CrimeModelUtility.N(request.wantedStatusId);
            if (!Ready(out CrimeOperationResult failure)) return failure;
            if (Duplicate(request.transactionId, "transition-wanted-status", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (!wantedStatuses.TryGetValue(id, out WantedStatusRecordData current) || request.targetState == WantedStatusLifecycleState.Unknown) return Fail(CrimeOperationCode.MissingWantedStatus, "Wanted status is missing or target state is invalid.", before);
            WantedStatusRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.correctionReason = request.correctionReason ?? string.Empty;
            changed.revision++;
            if (request.preview) return CrimeOperationResult.Success("Wanted status transition previewed.", before, before, id, preview: true);
            wantedStatuses[id] = changed;
            Complete(request.transactionId, "transition-wanted-status", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Wanted status lifecycle transitioned without deleting history.", before, Revision, id));
        }

        public CrimeOperationResult ReconcileWantedStatusesForJustice(WantedStatusJusticeResolutionRequest request)
        {
            request ??= new WantedStatusJusticeResolutionRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string resolutionId = CrimeModelUtility.N(request.resolutionId);
            if (Duplicate(request.transactionId, "reconcile-wanted-for-justice", resolutionId, before, out CrimeOperationResult duplicate)) return duplicate;
            string subjectId = CrimeModelUtility.N(request.subjectPersonId);
            string[] incidentIds = CrimeModelUtility.C(request.incidentIds);
            if (string.IsNullOrEmpty(resolutionId) || string.IsNullOrEmpty(subjectId) || !ValidatePerson(subjectId) || incidentIds.Length == 0 || incidentIds.Any(id => !incidents.ContainsKey(id))) return Fail(CrimeOperationCode.InvalidRequest, "Justice wanted-status reconciliation requires a known Person and valid incident IDs.", before);
            if (request.targetState != WantedStatusLifecycleState.Cleared && request.targetState != WantedStatusLifecycleState.Suspended && request.targetState != WantedStatusLifecycleState.Expired) return Fail(CrimeOperationCode.InvalidState, "Justice may only clear, suspend, or expire wanted statuses.", before);
            WantedStatusRecordData[] matched = wantedStatuses.Values
                .Where(item => item.lifecycleState == WantedStatusLifecycleState.Active
                    && item.subjectId == subjectId
                    && (request.includeWarrantDerived || !item.derivedFromWarrant)
                    && CrimeModelUtility.C(item.incidentIds).Intersect(incidentIds, StringComparer.Ordinal).Any())
                .OrderBy(item => item.wantedStatusId, StringComparer.Ordinal)
                .ToArray();
            if (request.preview) return CrimeOperationResult.Success($"Justice wanted-status reconciliation previewed for {matched.Length} status(es).", before, before, resolutionId, preview: true);
            foreach (WantedStatusRecordData status in matched)
            {
                status.lifecycleState = request.targetState;
                status.correctionReason = string.IsNullOrWhiteSpace(request.correctionReason) ? $"Resolved by justice record '{resolutionId}' at {request.worldTime}." : request.correctionReason.Trim();
                status.revision++;
            }
            Complete(request.transactionId, "reconcile-wanted-for-justice", resolutionId);
            Revision++;
            return Commit(CrimeOperationResult.Success($"Justice reconciled {matched.Length} wanted status(es).", before, Revision, resolutionId));
        }

        public CrimeOperationResult PublishWantedNotice(WantedNoticeRequest request)
        {
            request ??= new WantedNoticeRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string id = CrimeModelUtility.N(request.noticeId);
            string wantedId = CrimeModelUtility.N(request.wantedStatusId);
            if (Duplicate(request.transactionId, "publish-wanted-notice", id, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(id) || wantedNotices.ContainsKey(id)) return Fail(CrimeOperationCode.InvalidRequest, "Wanted notice identity is invalid.", before);
            if (!wantedStatuses.ContainsKey(wantedId)) return Fail(CrimeOperationCode.MissingWantedStatus, "Wanted status is missing.", before);
            WantedNoticeRecordData record = new WantedNoticeRecordData { noticeId = id, wantedStatusId = wantedId, issuingGovernmentId = CrimeModelUtility.N(request.issuingGovernmentId), text = request.text ?? string.Empty, publishedWorldTime = request.publishedWorldTime, visibility = request.visibility, revision = 1 };
            if (request.preview) return CrimeOperationResult.Success("Wanted notice previewed.", before, before, id, preview: true);
            wantedNotices[id] = record;
            Complete(request.transactionId, "publish-wanted-notice", id);
            Revision++;
            return Commit(CrimeOperationResult.Success("Wanted notice published as a projection record.", before, Revision, id));
        }

        public CrimeOperationResult ProcessWorldTime(CrimeTimeEvaluationRequest request)
        {
            request ??= new CrimeTimeEvaluationRequest();
            long before = Revision;
            if (!Ready(out CrimeOperationResult failure)) return failure;
            string boundary = CrimeModelUtility.N(request.boundaryId);
            if (Duplicate(request.transactionId, "process-time", boundary, before, out CrimeOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(boundary)) return Fail(CrimeOperationCode.InvalidRequest, "Crime time boundary is required.", before);
            int limit = Math.Max(1, request.maximumOperations);
            var due = warrants.Values.Where(item => (item.lifecycleState == WarrantLifecycleState.Issued || item.lifecycleState == WarrantLifecycleState.Active) && (item.lifecycleState == WarrantLifecycleState.Issued && request.worldTime >= item.activationWorldTime || item.expirationWorldTime >= 0d && request.worldTime > item.expirationWorldTime)).Select(item => new { Kind = 0, Time = item.lifecycleState == WarrantLifecycleState.Issued ? item.activationWorldTime : item.expirationWorldTime, Id = item.warrantId, Warrant = item, Wanted = (WantedStatusRecordData)null }).Concat(wantedStatuses.Values.Where(item => item.lifecycleState == WantedStatusLifecycleState.Active && item.expirationWorldTime >= 0d && request.worldTime > item.expirationWorldTime).Select(item => new { Kind = 1, Time = item.expirationWorldTime, Id = item.wantedStatusId, Warrant = (WarrantRecordData)null, Wanted = item })).OrderBy(item => item.Time).ThenBy(item => item.Kind).ThenBy(item => item.Id, StringComparer.Ordinal).Take(limit).ToArray();
            if (request.preview) return CrimeOperationResult.Success($"Crime time preview selected {due.Length} operation(s).", before, before, boundary, preview: true);
            foreach (var item in due)
            {
                if (item.Warrant != null)
                {
                    item.Warrant.lifecycleState = item.Warrant.lifecycleState == WarrantLifecycleState.Issued ? WarrantLifecycleState.Active : WarrantLifecycleState.Expired;
                    item.Warrant.revision++;
                    if (item.Warrant.lifecycleState == WarrantLifecycleState.Expired) TransitionDerivedWantedForWarrant(item.Warrant.warrantId, WarrantLifecycleState.Expired, request.worldTime);
                }
                else if (item.Wanted != null)
                {
                    item.Wanted.lifecycleState = WantedStatusLifecycleState.Expired;
                    item.Wanted.revision++;
                }
            }
            Complete(request.transactionId, "process-time", boundary);
            Revision++;
            return Commit(CrimeOperationResult.Success($"Crime time boundary processed {due.Length} operation(s).", before, Revision, boundary));
        }

        public CrimeProjectionResult<CrimeIncidentRecordData> ProjectIncident(string incidentId, bool privileged)
        {
            if (!incidents.TryGetValue(CrimeModelUtility.N(incidentId), out CrimeIncidentRecordData source)) return CrimeProjectionResult<CrimeIncidentRecordData>.Denied("Crime incident is missing.");
            if (privileged || source.visibility == PoliticalVisibility.Public) return CrimeProjectionResult<CrimeIncidentRecordData>.Success(source.Clone(), false, "Full incident projection returned.");
            if (source.visibility == PoliticalVisibility.Hidden || source.visibility == PoliticalVisibility.Secret) return CrimeProjectionResult<CrimeIncidentRecordData>.Denied("Crime incident is concealed.");
            CrimeIncidentRecordData redacted = source.Clone();
            redacted.involvedSubjects = Array.Empty<CrimeSubjectReferenceData>();
            redacted.victimIds = Array.Empty<string>();
            redacted.witnessIds = Array.Empty<string>();
            redacted.suspectIds = Array.Empty<string>();
            redacted.evidenceLinkIds = Array.Empty<string>();
            redacted.provenanceId = string.Empty;
            return CrimeProjectionResult<CrimeIncidentRecordData>.Success(redacted, true, "Redacted incident projection returned.");
        }

        public CrimeProjectionResult<WantedStatusRecordData> ProjectWantedStatus(string wantedStatusId, bool privileged)
        {
            if (!wantedStatuses.TryGetValue(CrimeModelUtility.N(wantedStatusId), out WantedStatusRecordData source)) return CrimeProjectionResult<WantedStatusRecordData>.Denied("Wanted status is missing.");
            if (privileged || source.visibility == PoliticalVisibility.Public) return CrimeProjectionResult<WantedStatusRecordData>.Success(source.Clone(), false, "Full wanted status projection returned.");
            if (source.visibility == PoliticalVisibility.Hidden || source.visibility == PoliticalVisibility.Secret) return CrimeProjectionResult<WantedStatusRecordData>.Denied("Wanted status is concealed.");
            WantedStatusRecordData redacted = source.Clone();
            redacted.subjectId = string.Empty;
            redacted.warrantId = string.Empty;
            redacted.incidentIds = Array.Empty<string>();
            redacted.correctionReason = string.Empty;
            return CrimeProjectionResult<WantedStatusRecordData>.Success(redacted, true, "Redacted wanted status projection returned.");
        }

        public CrimeRuntimeSaveData CreateSaveData() => new CrimeRuntimeSaveData { schemaVersion = 2, worldId = worldId, revision = Revision, incidents = Incidents.ToArray(), reports = Reports.ToArray(), allegations = Allegations.ToArray(), suspects = Suspects.ToArray(), evidenceLinks = EvidenceLinks.ToArray(), potentialOffenses = PotentialOffenses.ToArray(), investigations = Investigations.ToArray(), warrantRequests = WarrantRequests.ToArray(), warrants = Warrants.ToArray(), wantedStatuses = WantedStatuses.ToArray(), wantedNotices = WantedNotices.ToArray(), transactions = transactions.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToArray() };

        public CrimeOperationResult RestoreFromSaveData(CrimeRuntimeSaveData data, DefinitionRegistry definitions, GovernmentRuntime governmentRuntime, LegalRuntime legalRuntime, OrganizationAuthorityRuntime authorityRuntime, DiplomacyRuntime diplomacyRuntime, string expectedWorldId, IEnumerable<string> knownPersons, IEnumerable<string> knownPlaces)
        {
            long before = Revision;
            if (!ValidateSaveData(data, definitions, governmentRuntime, legalRuntime, authorityRuntime, diplomacyRuntime, expectedWorldId, knownPersons, knownPlaces, out string failure)) return Fail(CrimeOperationCode.ValidationFailed, failure, before);
            Configure(definitions, governmentRuntime, legalRuntime, authorityRuntime, diplomacyRuntime, expectedWorldId, knownPersons, knownPlaces);
            RestoreInternal(data);
            return CrimeOperationResult.Success("Crime runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(CrimeRuntimeSaveData data, DefinitionRegistry definitions, GovernmentRuntime governments, LegalRuntime laws, OrganizationAuthorityRuntime authority, DiplomacyRuntime diplomacy, string expectedWorldId, IEnumerable<string> persons, IEnumerable<string> places, out string failure)
        {
            failure = string.Empty;
            if (data == null) { failure = "Crime save data is missing."; return false; }
            if (data.schemaVersion != 2) { failure = $"Unsupported crime schema {data.schemaVersion}."; return false; }
            if (!string.Equals(CrimeModelUtility.N(data.worldId), CrimeModelUtility.N(expectedWorldId), StringComparison.Ordinal)) { failure = "Crime save world does not match runtime world."; return false; }
            CrimeIncidentRecordData[] savedIncidents = data.incidents ?? Array.Empty<CrimeIncidentRecordData>();
            CrimeReportRecordData[] savedReports = data.reports ?? Array.Empty<CrimeReportRecordData>();
            CrimeAllegationRecordData[] savedAllegations = data.allegations ?? Array.Empty<CrimeAllegationRecordData>();
            CrimeSuspectRecordData[] savedSuspects = data.suspects ?? Array.Empty<CrimeSuspectRecordData>();
            CrimeEvidenceLinkRecordData[] savedEvidence = data.evidenceLinks ?? Array.Empty<CrimeEvidenceLinkRecordData>();
            PotentialOffenseRecordData[] savedOffenses = data.potentialOffenses ?? Array.Empty<PotentialOffenseRecordData>();
            InvestigationRecordData[] savedInvestigations = data.investigations ?? Array.Empty<InvestigationRecordData>();
            WarrantRequestRecordData[] savedWarrantRequests = data.warrantRequests ?? Array.Empty<WarrantRequestRecordData>();
            WarrantRecordData[] savedWarrants = data.warrants ?? Array.Empty<WarrantRecordData>();
            WantedStatusRecordData[] savedWanted = data.wantedStatuses ?? Array.Empty<WantedStatusRecordData>();
            WantedNoticeRecordData[] savedNotices = data.wantedNotices ?? Array.Empty<WantedNoticeRecordData>();
            CrimeTransactionRecordData[] savedTransactions = data.transactions ?? Array.Empty<CrimeTransactionRecordData>();
            if (savedIncidents.Any(item => item == null) || savedReports.Any(item => item == null) || savedAllegations.Any(item => item == null) || savedSuspects.Any(item => item == null) || savedEvidence.Any(item => item == null) || savedOffenses.Any(item => item == null) || savedInvestigations.Any(item => item == null) || savedWarrantRequests.Any(item => item == null) || savedWarrants.Any(item => item == null) || savedWanted.Any(item => item == null) || savedNotices.Any(item => item == null) || savedTransactions.Any(item => item == null)) { failure = "Crime save contains a null record."; return false; }
            if (!Unique(savedIncidents.Select(item => item.incidentId), "incident", out failure) || !Unique(savedReports.Select(item => item.reportId), "report", out failure) || !Unique(savedAllegations.Select(item => item.allegationId), "allegation", out failure) || !Unique(savedSuspects.Select(item => item.suspectId), "suspect", out failure) || !Unique(savedEvidence.Select(item => item.evidenceLinkId), "evidence link", out failure) || !Unique(savedOffenses.Select(item => item.potentialOffenseId), "potential offense", out failure) || !Unique(savedInvestigations.Select(item => item.investigationId), "investigation", out failure) || !Unique(savedWarrantRequests.Select(item => item.warrantRequestId), "warrant request", out failure) || !Unique(savedWarrants.Select(item => item.warrantId), "warrant", out failure) || !Unique(savedWanted.Select(item => item.wantedStatusId), "wanted status", out failure) || !Unique(savedNotices.Select(item => item.noticeId), "wanted notice", out failure) || !Unique(savedTransactions.Select(item => item.transactionId), "transaction", out failure)) return false;
            Dictionary<string, CrimeIncidentRecordData> incidentById = savedIncidents.ToDictionary(item => item.incidentId, StringComparer.Ordinal);
            Dictionary<string, CrimeReportRecordData> reportById = savedReports.ToDictionary(item => item.reportId, StringComparer.Ordinal);
            Dictionary<string, PotentialOffenseRecordData> offenseById = savedOffenses.ToDictionary(item => item.potentialOffenseId, StringComparer.Ordinal);
            Dictionary<string, WarrantRequestRecordData> warrantRequestById = savedWarrantRequests.ToDictionary(item => item.warrantRequestId, StringComparer.Ordinal);
            Dictionary<string, WarrantRecordData> warrantById = savedWarrants.ToDictionary(item => item.warrantId, StringComparer.Ordinal);
            Dictionary<string, WantedStatusRecordData> wantedById = savedWanted.ToDictionary(item => item.wantedStatusId, StringComparer.Ordinal);
            HashSet<string> personSet = new HashSet<string>(CrimeModelUtility.C(persons), StringComparer.Ordinal);
            HashSet<string> placeSet = new HashSet<string>(CrimeModelUtility.C(places), StringComparer.Ordinal);

            foreach (CrimeIncidentRecordData item in savedIncidents)
            {
                if (item.category == CrimeIncidentCategory.Unknown || item.lifecycleState == CrimeIncidentLifecycleState.Unknown || item.lifecycleState == CrimeIncidentLifecycleState.Invalid) { failure = $"Incident '{item.incidentId}' has invalid state."; return false; }
                if (item.occurrenceEndWorldTime > 0d && item.occurrenceEndWorldTime < item.occurrenceStartWorldTime) { failure = $"Incident '{item.incidentId}' has invalid occurrence times."; return false; }
                if (!string.IsNullOrWhiteSpace(item.primaryTerritoryId) && (governments == null || !governments.TryGetTerritory(item.primaryTerritoryId, out _))) { failure = $"Incident '{item.incidentId}' has missing territory."; return false; }
                if (!string.IsNullOrWhiteSpace(item.primaryPlaceId) && placeSet.Count > 0 && !placeSet.Contains(item.primaryPlaceId)) { failure = $"Incident '{item.incidentId}' has missing place."; return false; }
                foreach (string jurisdictionId in CrimeModelUtility.C(item.jurisdictionIds)) if (governments == null || !governments.TryGetJurisdiction(jurisdictionId, out _)) { failure = $"Incident '{item.incidentId}' has missing jurisdiction."; return false; }
                foreach (CrimeSubjectReferenceData subject in item.involvedSubjects ?? Array.Empty<CrimeSubjectReferenceData>()) if (subject != null && string.Equals(subject.subjectType, "Person", StringComparison.Ordinal) && personSet.Count > 0 && !personSet.Contains(subject.subjectId)) { failure = $"Incident '{item.incidentId}' has a missing involved Person."; return false; }
                foreach (string personId in CrimeModelUtility.C(item.victimIds).Concat(CrimeModelUtility.C(item.witnessIds))) if (personSet.Count > 0 && !personSet.Contains(personId)) { failure = $"Incident '{item.incidentId}' has a missing victim or witness."; return false; }
                if (CrimeModelUtility.C(item.reportIds).Any(id => !reportById.ContainsKey(id)) || CrimeModelUtility.C(item.potentialOffenseIds).Any(id => !offenseById.ContainsKey(id)) || CrimeModelUtility.C(item.suspectIds).Any(id => !savedSuspects.Any(suspect => suspect.suspectId == id)) || CrimeModelUtility.C(item.evidenceLinkIds).Any(id => !savedEvidence.Any(evidence => evidence.evidenceLinkId == id))) { failure = $"Incident '{item.incidentId}' has broken indexes."; return false; }
            }

            foreach (CrimeReportRecordData item in savedReports)
            {
                if (!incidentById.ContainsKey(item.incidentId) || item.category == CrimeReportCategory.Unknown || item.lifecycleState == CrimeReportLifecycleState.Unknown || item.lifecycleState == CrimeReportLifecycleState.Invalid) { failure = $"Report '{item.reportId}' has invalid state or incident."; return false; }
                if (personSet.Count > 0 && string.Equals(item.reporterSubjectType, "Person", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(item.reporterSubjectId) && !personSet.Contains(item.reporterSubjectId)) { failure = $"Report '{item.reportId}' has missing reporter."; return false; }
                if (item.reporterReliabilityBasisPoints < 0 || item.reporterReliabilityBasisPoints > 10000) { failure = $"Report '{item.reportId}' has invalid reliability."; return false; }
                if (CrimeModelUtility.C(item.allegationIds).Any(id => !savedAllegations.Any(allegation => allegation.allegationId == id)) || CrimeModelUtility.C(item.allegedOffenseIds).Any(id => !offenseById.ContainsKey(id))) { failure = $"Report '{item.reportId}' has broken indexes."; return false; }
            }

            foreach (PotentialOffenseRecordData item in savedOffenses)
            {
                if (!incidentById.ContainsKey(item.incidentId) || definitions == null || !definitions.TryGet(item.offenseDefinitionId, out LegalOffenseDefinition definition)) { failure = $"Potential offense '{item.potentialOffenseId}' has invalid incident or definition."; return false; }
                if (!definition.SupportedStages.Contains(item.stage) || !definition.SupportedParticipation.Contains(item.participation) || item.status == PotentialOffenseStatus.Unknown || item.status == PotentialOffenseStatus.Invalid) { failure = $"Potential offense '{item.potentialOffenseId}' has invalid lifecycle or classification."; return false; }
                foreach (string actorId in CrimeModelUtility.C(item.allegedActorIds)) if (personSet.Count > 0 && !personSet.Contains(actorId)) { failure = $"Potential offense '{item.potentialOffenseId}' has missing alleged actor."; return false; }
                foreach (string targetId in CrimeModelUtility.C(item.victimOrTargetIds)) if (personSet.Count > 0 && !personSet.Contains(targetId)) { failure = $"Potential offense '{item.potentialOffenseId}' has missing victim or target."; return false; }
                if (!string.IsNullOrWhiteSpace(item.legalProvisionId) && (laws == null || !laws.TryGetProvision(item.legalProvisionId, out _))) { failure = $"Potential offense '{item.potentialOffenseId}' has missing legal provision."; return false; }
            }

            foreach (CrimeAllegationRecordData item in savedAllegations)
            {
                if (!incidentById.ContainsKey(item.incidentId) || !reportById.ContainsKey(item.reportId) || !offenseById.ContainsKey(item.potentialOffenseId) || item.lifecycleState == AllegationLifecycleState.Unknown || item.lifecycleState == AllegationLifecycleState.Invalid) { failure = $"Allegation '{item.allegationId}' has invalid references."; return false; }
                foreach (string personId in CrimeModelUtility.C(new[] { item.claimedActorId, item.claimedVictimId, item.claimedTargetId })) if (personSet.Count > 0 && !personSet.Contains(personId)) { failure = $"Allegation '{item.allegationId}' has a missing claimed Person."; return false; }
            }
            foreach (CrimeSuspectRecordData item in savedSuspects)
                if (!incidentById.ContainsKey(item.incidentId) || !string.IsNullOrWhiteSpace(item.potentialOffenseId) && !offenseById.ContainsKey(item.potentialOffenseId) || string.IsNullOrWhiteSpace(item.subjectId) || item.lifecycleState == SuspectLifecycleState.Unknown || item.lifecycleState == SuspectLifecycleState.Invalid) { failure = $"Suspect '{item.suspectId}' has invalid references."; return false; }
            foreach (CrimeEvidenceLinkRecordData item in savedEvidence)
                if (!incidentById.ContainsKey(item.incidentId) || !string.IsNullOrWhiteSpace(item.reportId) && !reportById.ContainsKey(item.reportId) || !string.IsNullOrWhiteSpace(item.potentialOffenseId) && !offenseById.ContainsKey(item.potentialOffenseId) || string.IsNullOrWhiteSpace(item.evidenceId)) { failure = $"Evidence link '{item.evidenceLinkId}' has invalid references."; return false; }
            foreach (InvestigationRecordData item in savedInvestigations)
                if (!incidentById.ContainsKey(item.incidentId) || !string.IsNullOrWhiteSpace(item.responsibleGovernmentId) && (governments == null || !governments.TryGetGovernment(item.responsibleGovernmentId, out _))) { failure = $"Investigation '{item.investigationId}' has invalid references."; return false; }
            foreach (WarrantRequestRecordData item in savedWarrantRequests)
                if (!incidentById.ContainsKey(item.incidentId) || !offenseById.ContainsKey(item.potentialOffenseId) || definitions == null || !definitions.TryGet(item.warrantDefinitionId, out WarrantDefinition warrantDefinition) || !StaticScopeValid(item.scope, warrantDefinition, out failure) || item.lifecycleState == WarrantRequestLifecycleState.Unknown || item.lifecycleState == WarrantRequestLifecycleState.Invalid) { failure = string.IsNullOrEmpty(failure) ? $"Warrant request '{item.warrantRequestId}' has invalid references." : failure; return false; }
            foreach (WarrantRecordData item in savedWarrants)
                if (!warrantRequestById.ContainsKey(item.warrantRequestId) || !incidentById.ContainsKey(item.incidentId) || !offenseById.ContainsKey(item.potentialOffenseId) || definitions == null || !definitions.TryGet(item.warrantDefinitionId, out WarrantDefinition _) || item.lifecycleState == WarrantLifecycleState.Unknown || item.lifecycleState == WarrantLifecycleState.Invalid || item.expirationWorldTime >= 0d && item.expirationWorldTime < item.activationWorldTime) { failure = $"Warrant '{item.warrantId}' has invalid references or dates."; return false; }
            foreach (WantedStatusRecordData item in savedWanted)
            {
                if (definitions == null || !definitions.TryGet(item.wantedDefinitionId, out WantedStatusDefinition _) || !string.IsNullOrWhiteSpace(item.warrantId) && !warrantById.ContainsKey(item.warrantId) || CrimeModelUtility.C(item.incidentIds).Any(id => !incidentById.ContainsKey(id)) || placeSet.Count > 0 && CrimeModelUtility.C(item.placeIds).Any(id => !placeSet.Contains(id)) || string.IsNullOrWhiteSpace(item.subjectId) || string.IsNullOrWhiteSpace(item.subjectType) || item.lifecycleState == WantedStatusLifecycleState.Unknown || item.lifecycleState == WantedStatusLifecycleState.Invalid || item.expirationWorldTime >= 0d && item.expirationWorldTime < item.activeWorldTime) { failure = $"Wanted status '{item.wantedStatusId}' has invalid references or dates."; return false; }
                if (string.Equals(item.subjectType, "Person", StringComparison.Ordinal) && personSet.Count > 0 && !personSet.Contains(item.subjectId)) { failure = $"Wanted status '{item.wantedStatusId}' has a missing Person subject."; return false; }
                if (!string.IsNullOrWhiteSpace(item.jurisdictionId) && (governments == null || !governments.TryGetJurisdiction(item.jurisdictionId, out JurisdictionRecordData jurisdiction))) { failure = $"Wanted status '{item.wantedStatusId}' has a missing jurisdiction."; return false; }
                if (!string.IsNullOrWhiteSpace(item.territoryId) && (governments == null || !governments.TryGetTerritory(item.territoryId, out _))) { failure = $"Wanted status '{item.wantedStatusId}' has a missing territory."; return false; }
                if (!string.IsNullOrWhiteSpace(item.jurisdictionId) && !string.IsNullOrWhiteSpace(item.territoryId) && governments.TryGetJurisdiction(item.jurisdictionId, out JurisdictionRecordData scopedJurisdiction) && scopedJurisdiction.territoryIds.Length > 0 && !scopedJurisdiction.territoryIds.Contains(item.territoryId, StringComparer.Ordinal)) { failure = $"Wanted status '{item.wantedStatusId}' has a territory outside its jurisdiction."; return false; }
            }
            foreach (WantedNoticeRecordData item in savedNotices)
                if (!wantedById.ContainsKey(item.wantedStatusId) || string.IsNullOrWhiteSpace(item.text)) { failure = $"Wanted notice '{item.noticeId}' has invalid references."; return false; }
            if (savedTransactions.Any(item => string.IsNullOrWhiteSpace(item.operation) || string.IsNullOrWhiteSpace(item.subjectId))) { failure = "Crime save contains an invalid transaction identity."; return false; }
            return true;
        }

        public void Reset()
        {
            incidents.Clear(); reports.Clear(); allegations.Clear(); suspects.Clear(); evidenceLinks.Clear(); potentialOffenses.Clear(); investigations.Clear(); warrantRequests.Clear(); warrants.Clear(); wantedStatuses.Clear(); wantedNotices.Clear(); transactions.Clear(); Revision = 0;
        }

        public void Dispose()
        {
            Reset();
            OperationCommitted = null;
            StateChanged = null;
            disposed = true;
        }

        private static PotentialOffenseStatus ResolvePotentialOffenseStatus(LegalApplicabilityStatus legalStatus, EvidenceSufficiencyState evidence, EvidenceSufficiencyState threshold)
        {
            if (legalStatus == LegalApplicabilityStatus.Immune) return PotentialOffenseStatus.ImmunityRelevant;
            if (legalStatus == LegalApplicabilityStatus.Exempt) return PotentialOffenseStatus.Exempt;
            if (legalStatus == LegalApplicabilityStatus.Prohibited || legalStatus == LegalApplicabilityStatus.Required) return CompareSufficiency(evidence, threshold) >= 0 ? PotentialOffenseStatus.ElementsSupported : PotentialOffenseStatus.ElementsPartiallySupported;
            if (legalStatus == LegalApplicabilityStatus.Permitted || legalStatus == LegalApplicabilityStatus.NoApplicableLaw) return PotentialOffenseStatus.LegallyExcluded;
            if (legalStatus == LegalApplicabilityStatus.Conflict) return PotentialOffenseStatus.Plausible;
            return PotentialOffenseStatus.InsufficientInformation;
        }

        private static OffenseElementEvaluationData[] BuildElements(IEnumerable<OffenseElementEvaluationData> requested, LegalOffenseDefinition definition)
        {
            OffenseElementEvaluationData[] provided = (requested ?? Array.Empty<OffenseElementEvaluationData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.kind).ThenBy(item => item.key, StringComparer.Ordinal).ToArray();
            if (provided.Length > 0) return provided;
            return definition.RequiredElements.Select(item => new OffenseElementEvaluationData { kind = item.Kind, key = item.Key, expectedValue = item.ExpectedValue, supported = !item.Required, diagnostics = item.Required ? "Required element pending evidence." : "Optional element not required." }).ToArray();
        }

        private bool ValidateScope(WarrantScopeData scope, WarrantDefinition definition, out string failure) => StaticScopeValid(scope, definition, out failure) && ValidateScopeReferences(scope, out failure);

        private static bool StaticScopeValid(WarrantScopeData scope, WarrantDefinition definition, out string failure)
        {
            failure = string.Empty;
            if (scope == null || scope.kind == WarrantScopeKind.Unknown) { failure = "Warrant scope is missing."; return false; }
            if (definition == null || !definition.AllowedScopes.Contains(scope.kind)) { failure = "Warrant scope is not supported by definition."; return false; }
            if (string.IsNullOrWhiteSpace(scope.targetId) && CrimeModelUtility.C(scope.territoryIds).Length == 0 && CrimeModelUtility.C(scope.jurisdictionIds).Length == 0) { failure = "Warrant scope must identify a target, territory, or jurisdiction."; return false; }
            return true;
        }

        private bool ValidateScopeReferences(WarrantScopeData scope, out string failure)
        {
            failure = string.Empty;
            foreach (string territoryId in CrimeModelUtility.C(scope.territoryIds)) if (governments == null || !governments.TryGetTerritory(territoryId, out _)) { failure = $"Warrant scope references missing territory '{territoryId}'."; return false; }
            foreach (string jurisdictionId in CrimeModelUtility.C(scope.jurisdictionIds)) if (governments == null || !governments.TryGetJurisdiction(jurisdictionId, out _)) { failure = $"Warrant scope references missing jurisdiction '{jurisdictionId}'."; return false; }
            return true;
        }

        private bool ValidateWantedScope(string subjectId, string subjectType, string jurisdictionId, string territoryId, long before, out CrimeOperationResult failure)
        {
            failure = null;
            string type = CrimeModelUtility.N(subjectType);
            if (string.IsNullOrEmpty(type)) { failure = Fail(CrimeOperationCode.InvalidRequest, "Wanted subject type is required.", before); return false; }
            if (string.Equals(type, "Person", StringComparison.Ordinal) && !ValidatePerson(subjectId)) { failure = Fail(CrimeOperationCode.InvalidReference, $"Wanted Person '{subjectId}' is missing.", before); return false; }
            string jurisdiction = CrimeModelUtility.N(jurisdictionId);
            string territory = CrimeModelUtility.N(territoryId);
            if (!string.IsNullOrEmpty(jurisdiction))
            {
                if (governments == null || !governments.TryGetJurisdiction(jurisdiction, out JurisdictionRecordData jurisdictionRecord)) { failure = Fail(CrimeOperationCode.MissingJurisdiction, $"Wanted jurisdiction '{jurisdiction}' is missing.", before); return false; }
                if (!string.IsNullOrEmpty(territory) && jurisdictionRecord.territoryIds != null && jurisdictionRecord.territoryIds.Length > 0 && !jurisdictionRecord.territoryIds.Contains(territory, StringComparer.Ordinal)) { failure = Fail(CrimeOperationCode.InvalidReference, $"Wanted territory '{territory}' is outside jurisdiction '{jurisdiction}'.", before); return false; }
            }
            if (!string.IsNullOrEmpty(territory) && (governments == null || !governments.TryGetTerritory(territory, out _))) { failure = Fail(CrimeOperationCode.InvalidReference, $"Wanted territory '{territory}' is missing.", before); return false; }
            return true;
        }

        private bool HasAuthority(string grantOrReviewerId)
        {
            string id = CrimeModelUtility.N(grantOrReviewerId);
            if (string.IsNullOrEmpty(id)) return false;
            return authority != null && authority.TryGetGrant(id, out OrganizationAuthoritySnapshot grant) && grant.LifecycleState == OrganizationAuthorityGrantLifecycleState.Active;
        }

        private void TransitionDerivedWantedForWarrant(string warrantId, WarrantLifecycleState warrantState, double worldTime)
        {
            foreach (WantedStatusRecordData wanted in wantedStatuses.Values.Where(item => item.warrantId == warrantId && item.derivedFromWarrant && item.lifecycleState == WantedStatusLifecycleState.Active).OrderBy(item => item.wantedStatusId, StringComparer.Ordinal))
            {
                if (warrantState == WarrantLifecycleState.Satisfied || warrantState == WarrantLifecycleState.Quashed || warrantState == WarrantLifecycleState.Withdrawn) wanted.lifecycleState = WantedStatusLifecycleState.Cleared;
                else if (warrantState == WarrantLifecycleState.Expired) wanted.lifecycleState = WantedStatusLifecycleState.Expired;
                else if (warrantState == WarrantLifecycleState.Suspended) wanted.lifecycleState = WantedStatusLifecycleState.Suspended;
                else continue;
                wanted.correctionReason = $"Derived from warrant lifecycle '{warrantState}' at {worldTime}.";
                wanted.revision++;
            }
        }

        private static WantedPurposeCategory PurposeFor(WarrantCategory category) => category switch { WarrantCategory.Arrest => WantedPurposeCategory.Arrest, WarrantCategory.Questioning => WantedPurposeCategory.Questioning, WarrantCategory.InternalOrganizationProcess => WantedPurposeCategory.InternalOrganizationProcess, WarrantCategory.MilitaryApprehension => WantedPurposeCategory.MilitaryApprehension, _ => WantedPurposeCategory.Locate };
        private static string WantedDefinitionFor(WarrantCategory category) => category switch { WarrantCategory.Arrest => PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId, WarrantCategory.Questioning => PrototypeCrimeDefinitionFactory.WantedForQuestioningDefinitionId, WarrantCategory.MilitaryApprehension => PrototypeCrimeDefinitionFactory.MilitaryApprehensionWantedDefinitionId, _ => PrototypeCrimeDefinitionFactory.WantedForLocationDefinitionId };
        private static int WantedPurposeRank(WantedPurposeCategory value) => value switch { WantedPurposeCategory.Arrest => 5, WantedPurposeCategory.MilitaryApprehension => 5, WantedPurposeCategory.Questioning => 3, WantedPurposeCategory.Locate => 2, WantedPurposeCategory.InternalOrganizationProcess => 2, WantedPurposeCategory.MissingPerson => 1, _ => 0 };
        private static int WantedRiskRank(WantedRiskAssessment value) => value switch { WantedRiskAssessment.High => 5, WantedRiskAssessment.PotentiallyArmed => 4, WantedRiskAssessment.ConflictingInformation => 3, WantedRiskAssessment.Nonviolent => 2, WantedRiskAssessment.Low => 1, _ => 0 };
        private static EvidenceSufficiencyState Max(EvidenceSufficiencyState first, EvidenceSufficiencyState second) => CompareSufficiency(first, second) >= 0 ? first : second;
        private static int CompareSufficiency(EvidenceSufficiencyState first, EvidenceSufficiencyState second) => Rank(first).CompareTo(Rank(second));
        private static int Rank(EvidenceSufficiencyState value) => value switch { EvidenceSufficiencyState.None => 0, EvidenceSufficiencyState.Weak => 1, EvidenceSufficiencyState.Partial => 2, EvidenceSufficiencyState.Substantial => 3, EvidenceSufficiencyState.ThresholdMet => 4, EvidenceSufficiencyState.Contradicted => -1, EvidenceSufficiencyState.Disputed => -1, _ => 0 };
        private bool ValidatePerson(string personId) => !string.IsNullOrWhiteSpace(personId) && (personIds.Count == 0 || personIds.Contains(personId.Trim()));
        private bool Ready(out CrimeOperationResult failure) { if (disposed) { failure = Fail(CrimeOperationCode.Disposed, "Crime runtime is disposed.", Revision); return false; } if (registry == null || governments == null || laws == null) { failure = Fail(CrimeOperationCode.InvalidState, "Crime runtime dependencies are not ready.", Revision); return false; } failure = null; return true; }
        private bool Def<T>(string id, out T definition) where T : class, IGameDefinition { definition = null; return registry != null && registry.TryGet(CrimeModelUtility.N(id), out definition); }
        private bool Duplicate(string tx, string operation, string subject, long before, out CrimeOperationResult result) { tx = CrimeModelUtility.N(tx); result = null; if (string.IsNullOrEmpty(tx) || !transactions.TryGetValue(tx, out CrimeTransactionRecordData existing)) return false; result = existing.operation == operation && existing.subjectId == subject ? CrimeOperationResult.Success("Duplicate crime transaction ignored.", before, before, subject, duplicate: true) : Fail(CrimeOperationCode.InvalidRequest, $"Transaction '{tx}' has different identity.", before); return true; }
        private void Complete(string tx, string operation, string subject) { tx = CrimeModelUtility.N(tx); if (!string.IsNullOrEmpty(tx)) transactions[tx] = new CrimeTransactionRecordData { transactionId = tx, operation = operation, subjectId = subject, revision = Revision + 1 }; }
        private CrimeOperationResult Commit(CrimeOperationResult result) { Action<CrimeOperationResult> handlers = OperationCommitted; if (handlers != null) foreach (Action<CrimeOperationResult> handler in handlers.GetInvocationList()) try { handler(result); } catch (Exception exception) { Debug.LogException(exception); } CrimeMutationEvent change = new CrimeMutationEvent(transactions.Values.Where(item => item.revision == Revision && item.subjectId == result.SubjectId).OrderBy(item => item.transactionId, StringComparer.Ordinal).FirstOrDefault()?.operation ?? string.Empty, result.SubjectId, Revision, result); Action<CrimeMutationEvent> changeHandlers = StateChanged; if (changeHandlers != null) foreach (Action<CrimeMutationEvent> handler in changeHandlers.GetInvocationList()) try { handler(change); } catch (Exception exception) { Debug.LogException(exception); } return result; }
        private CrimeOperationResult Fail(CrimeOperationCode code, string message, long revision) => CrimeOperationResult.Failure(code, message, revision);
        private static bool Unique(IEnumerable<string> values, string label, out string failure) { string[] ids = (values ?? Array.Empty<string>()).Select(CrimeModelUtility.N).ToArray(); if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) { failure = $"Crime save contains invalid or duplicate {label} IDs."; return false; } failure = string.Empty; return true; }
        private void RestoreInternal(CrimeRuntimeSaveData source) { Reset(); CrimeRuntimeSaveData data = source.Clone(); worldId = data.worldId; Revision = data.revision; foreach (var item in data.incidents) incidents[item.incidentId] = item; foreach (var item in data.reports) reports[item.reportId] = item; foreach (var item in data.allegations) allegations[item.allegationId] = item; foreach (var item in data.suspects) suspects[item.suspectId] = item; foreach (var item in data.evidenceLinks) evidenceLinks[item.evidenceLinkId] = item; foreach (var item in data.potentialOffenses) potentialOffenses[item.potentialOffenseId] = item; foreach (var item in data.investigations) investigations[item.investigationId] = item; foreach (var item in data.warrantRequests) warrantRequests[item.warrantRequestId] = item; foreach (var item in data.warrants) warrants[item.warrantId] = item; foreach (var item in data.wantedStatuses) wantedStatuses[item.wantedStatusId] = item; foreach (var item in data.wantedNotices) wantedNotices[item.noticeId] = item; foreach (var item in data.transactions) transactions[item.transactionId] = item; }
        private static bool TryClone<T>(IDictionary<string, T> source, string id, Func<T, T> clone, out T value) where T : class { if (source.TryGetValue(CrimeModelUtility.N(id), out T found)) { value = clone(found); return true; } value = null; return false; }
    }
}
