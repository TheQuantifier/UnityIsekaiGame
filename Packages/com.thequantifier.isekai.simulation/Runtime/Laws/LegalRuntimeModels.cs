using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Governments;

namespace UnityIsekaiGame.Laws
{
    internal static class LegalModelUtility { public static string N(string value) => PoliticalModelUtility.Normalize(value); public static string[] C(IEnumerable<string> values) => PoliticalModelUtility.Clean(values); }

    [Serializable] public sealed class LegalConditionData { public LegalConditionKind kind; public string key; public string value; public bool negate; public LegalConditionData Clone() => new LegalConditionData { kind = kind, key = LegalModelUtility.N(key), value = LegalModelUtility.N(value), negate = negate }; }
    [Serializable] public sealed class LegalProvisionVersionData
    {
        public int version = 1; public LegalEffectCategory effect; public string actionId; public string subjectMatterId; public string[] personIds = Array.Empty<string>(); public string[] organizationIds = Array.Empty<string>(); public string[] territoryIds = Array.Empty<string>(); public string[] placeIds = Array.Empty<string>(); public string[] propertyIds = Array.Empty<string>(); public string[] officeIds = Array.Empty<string>(); public string[] professionIds = Array.Empty<string>(); public string[] legalStatusDefinitionIds = Array.Empty<string>(); public LegalConditionData[] conditions = Array.Empty<LegalConditionData>(); public string[] exceptionProvisionIds = Array.Empty<string>(); public string effectValue; public double effectiveWorldTime; public double endedWorldTime = -1d; public string sourceAmendmentId; public string provenanceId;
        public LegalProvisionVersionData Clone() => new LegalProvisionVersionData { version = Math.Max(1, version), effect = effect, actionId = LegalModelUtility.N(actionId), subjectMatterId = LegalModelUtility.N(subjectMatterId), personIds = LegalModelUtility.C(personIds), organizationIds = LegalModelUtility.C(organizationIds), territoryIds = LegalModelUtility.C(territoryIds), placeIds = LegalModelUtility.C(placeIds), propertyIds = LegalModelUtility.C(propertyIds), officeIds = LegalModelUtility.C(officeIds), professionIds = LegalModelUtility.C(professionIds), legalStatusDefinitionIds = LegalModelUtility.C(legalStatusDefinitionIds), conditions = (conditions ?? Array.Empty<LegalConditionData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), exceptionProvisionIds = LegalModelUtility.C(exceptionProvisionIds), effectValue = effectValue ?? string.Empty, effectiveWorldTime = effectiveWorldTime, endedWorldTime = endedWorldTime, sourceAmendmentId = LegalModelUtility.N(sourceAmendmentId), provenanceId = LegalModelUtility.N(provenanceId) };
    }
    [Serializable] public sealed class LegalProvisionRecordData
    {
        public string provisionId; public string instrumentId; public string provisionDefinitionId; public string citation; public LegalProvisionLifecycleState lifecycleState = LegalProvisionLifecycleState.Active; public LegalProvisionVersionData[] versions = Array.Empty<LegalProvisionVersionData>(); public PoliticalVisibility visibility; public long revision = 1;
        public LegalProvisionRecordData Clone() => new LegalProvisionRecordData { provisionId = LegalModelUtility.N(provisionId), instrumentId = LegalModelUtility.N(instrumentId), provisionDefinitionId = LegalModelUtility.N(provisionDefinitionId), citation = citation ?? string.Empty, lifecycleState = lifecycleState, versions = (versions ?? Array.Empty<LegalProvisionVersionData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.version).ToArray(), visibility = visibility, revision = Math.Max(1, revision) };
        public LegalProvisionVersionData VersionAt(double time) => (versions ?? Array.Empty<LegalProvisionVersionData>()).Where(item => item != null && time >= item.effectiveWorldTime && (item.endedWorldTime < 0d || time <= item.endedWorldTime)).OrderByDescending(item => item.version).FirstOrDefault()?.Clone();
    }
    [Serializable] public sealed class LegalInstrumentRecordData
    {
        public string instrumentId; public string instrumentDefinitionId; public string authorityDefinitionId; public string title; public string shortTitle; public string citation; public string governmentId; public string organizationId; public string officeId; public string sourceAuthorityGrantId; public string sourceResolutionId; public string sourceAgreementId; public string[] jurisdictionIds = Array.Empty<string>(); public string[] provisionIds = Array.Empty<string>(); public string[] amendmentIds = Array.Empty<string>(); public string[] predecessorInstrumentIds = Array.Empty<string>(); public string[] successorInstrumentIds = Array.Empty<string>(); public LegalInstrumentLifecycleState lifecycleState; public bool published; public bool promulgated; public double enactmentWorldTime; public double publicationWorldTime = -1d; public double effectiveWorldTime; public double expirationWorldTime = -1d; public int precedence; public LegalConflictPolicy conflictPolicy; public PoliticalVisibility visibility; public string provenanceId; public long revision = 1;
        public LegalInstrumentRecordData Clone() => new LegalInstrumentRecordData { instrumentId = LegalModelUtility.N(instrumentId), instrumentDefinitionId = LegalModelUtility.N(instrumentDefinitionId), authorityDefinitionId = LegalModelUtility.N(authorityDefinitionId), title = title ?? string.Empty, shortTitle = shortTitle ?? string.Empty, citation = citation ?? string.Empty, governmentId = LegalModelUtility.N(governmentId), organizationId = LegalModelUtility.N(organizationId), officeId = LegalModelUtility.N(officeId), sourceAuthorityGrantId = LegalModelUtility.N(sourceAuthorityGrantId), sourceResolutionId = LegalModelUtility.N(sourceResolutionId), sourceAgreementId = LegalModelUtility.N(sourceAgreementId), jurisdictionIds = LegalModelUtility.C(jurisdictionIds), provisionIds = LegalModelUtility.C(provisionIds), amendmentIds = LegalModelUtility.C(amendmentIds), predecessorInstrumentIds = LegalModelUtility.C(predecessorInstrumentIds), successorInstrumentIds = LegalModelUtility.C(successorInstrumentIds), lifecycleState = lifecycleState, published = published, promulgated = promulgated, enactmentWorldTime = enactmentWorldTime, publicationWorldTime = publicationWorldTime, effectiveWorldTime = effectiveWorldTime, expirationWorldTime = expirationWorldTime, precedence = precedence, conflictPolicy = conflictPolicy, visibility = visibility, provenanceId = LegalModelUtility.N(provenanceId), revision = Math.Max(1, revision) };
    }
    [Serializable] public sealed class LegalEntitlementRecordData
    {
        public string entitlementId; public LegalEffectCategory effect; public string provisionId; public string personId; public string organizationId; public string actionId; public string territoryId; public string propertyId; public string sourceAuthorityGrantId; public LegalEntitlementLifecycleState lifecycleState = LegalEntitlementLifecycleState.Active; public double effectiveWorldTime; public double expirationWorldTime = -1d; public PoliticalVisibility visibility; public string provenanceId; public long revision = 1;
        public LegalEntitlementRecordData Clone() => new LegalEntitlementRecordData { entitlementId = LegalModelUtility.N(entitlementId), effect = effect, provisionId = LegalModelUtility.N(provisionId), personId = LegalModelUtility.N(personId), organizationId = LegalModelUtility.N(organizationId), actionId = LegalModelUtility.N(actionId), territoryId = LegalModelUtility.N(territoryId), propertyId = LegalModelUtility.N(propertyId), sourceAuthorityGrantId = LegalModelUtility.N(sourceAuthorityGrantId), lifecycleState = lifecycleState, effectiveWorldTime = effectiveWorldTime, expirationWorldTime = expirationWorldTime, visibility = visibility, provenanceId = LegalModelUtility.N(provenanceId), revision = Math.Max(1, revision) };
    }
    [Serializable] public sealed class PersonLegalStatusRecordData
    {
        public string statusId; public string statusDefinitionId; public string citizenshipDefinitionId; public string personId; public string polityId; public string recognizingGovernmentId; public string residencePlaceId; public LegalStatusCategory category; public CitizenshipAcquisitionRoute acquisitionRoute; public LegalStatusLifecycleState lifecycleState = LegalStatusLifecycleState.Active; public bool consentGiven; public string sourceInstrumentId; public string sourceDecisionId; public string sourceAuthorityGrantId; public string recognitionRelationId; public double effectiveWorldTime; public double endedWorldTime = -1d; public PoliticalVisibility visibility = PoliticalVisibility.Restricted; public string provenanceId; public long revision = 1;
        public PersonLegalStatusRecordData Clone() => new PersonLegalStatusRecordData { statusId = LegalModelUtility.N(statusId), statusDefinitionId = LegalModelUtility.N(statusDefinitionId), citizenshipDefinitionId = LegalModelUtility.N(citizenshipDefinitionId), personId = LegalModelUtility.N(personId), polityId = LegalModelUtility.N(polityId), recognizingGovernmentId = LegalModelUtility.N(recognizingGovernmentId), residencePlaceId = LegalModelUtility.N(residencePlaceId), category = category, acquisitionRoute = acquisitionRoute, lifecycleState = lifecycleState, consentGiven = consentGiven, sourceInstrumentId = LegalModelUtility.N(sourceInstrumentId), sourceDecisionId = LegalModelUtility.N(sourceDecisionId), sourceAuthorityGrantId = LegalModelUtility.N(sourceAuthorityGrantId), recognitionRelationId = LegalModelUtility.N(recognitionRelationId), effectiveWorldTime = effectiveWorldTime, endedWorldTime = endedWorldTime, visibility = visibility, provenanceId = LegalModelUtility.N(provenanceId), revision = Math.Max(1, revision) };
    }
    [Serializable] public sealed class LegalTransitionPlanRecordData { public string transitionId; public LegalTransitionKind kind; public string sourceInstrumentId; public string targetInstrumentId; public string sourcePolityId; public string targetPolityId; public string[] statusIds = Array.Empty<string>(); public string sourceDecisionId; public double plannedWorldTime; public bool executed; public double executedWorldTime = -1d; public string diagnostics; public long revision = 1; public LegalTransitionPlanRecordData Clone() => new LegalTransitionPlanRecordData { transitionId = LegalModelUtility.N(transitionId), kind = kind, sourceInstrumentId = LegalModelUtility.N(sourceInstrumentId), targetInstrumentId = LegalModelUtility.N(targetInstrumentId), sourcePolityId = LegalModelUtility.N(sourcePolityId), targetPolityId = LegalModelUtility.N(targetPolityId), statusIds = LegalModelUtility.C(statusIds), sourceDecisionId = LegalModelUtility.N(sourceDecisionId), plannedWorldTime = plannedWorldTime, executed = executed, executedWorldTime = executedWorldTime, diagnostics = diagnostics ?? string.Empty, revision = Math.Max(1, revision) }; }
    [Serializable] public sealed class LegalTransactionRecordData { public string transactionId; public string operation; public string subjectId; public long revision; public LegalTransactionRecordData Clone() => new LegalTransactionRecordData { transactionId = LegalModelUtility.N(transactionId), operation = operation ?? string.Empty, subjectId = LegalModelUtility.N(subjectId), revision = revision }; }
    [Serializable] public sealed class LegalRuntimeSaveData
    {
        public int schemaVersion = 1; public string worldId; public long revision; public LegalInstrumentRecordData[] instruments = Array.Empty<LegalInstrumentRecordData>(); public LegalProvisionRecordData[] provisions = Array.Empty<LegalProvisionRecordData>(); public LegalEntitlementRecordData[] entitlements = Array.Empty<LegalEntitlementRecordData>(); public PersonLegalStatusRecordData[] statuses = Array.Empty<PersonLegalStatusRecordData>(); public LegalTransitionPlanRecordData[] transitions = Array.Empty<LegalTransitionPlanRecordData>(); public LegalTransactionRecordData[] transactions = Array.Empty<LegalTransactionRecordData>();
        public LegalRuntimeSaveData Clone() => new LegalRuntimeSaveData { schemaVersion = Math.Max(1, schemaVersion), worldId = LegalModelUtility.N(worldId), revision = revision, instruments = (instruments ?? Array.Empty<LegalInstrumentRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), provisions = (provisions ?? Array.Empty<LegalProvisionRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), entitlements = (entitlements ?? Array.Empty<LegalEntitlementRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), statuses = (statuses ?? Array.Empty<PersonLegalStatusRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), transitions = (transitions ?? Array.Empty<LegalTransitionPlanRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(), transactions = (transactions ?? Array.Empty<LegalTransactionRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray() };
    }

    public sealed class LegalOperationResult
    {
        public bool Succeeded { get; private set; } public LegalOperationCode Code { get; private set; } public bool Preview { get; private set; } public bool Duplicate { get; private set; } public string SubjectId { get; private set; } public string Message { get; private set; } public long RevisionBefore { get; private set; } public long RevisionAfter { get; private set; }
        public static LegalOperationResult Success(string message, long before, long after, string subject = "", bool preview = false, bool duplicate = false) => new LegalOperationResult { Succeeded = true, Code = preview ? LegalOperationCode.Preview : duplicate ? LegalOperationCode.Duplicate : LegalOperationCode.Succeeded, Preview = preview, Duplicate = duplicate, SubjectId = subject ?? string.Empty, Message = message ?? string.Empty, RevisionBefore = before, RevisionAfter = after };
        public static LegalOperationResult Failure(LegalOperationCode code, string message, long revision) => new LegalOperationResult { Code = code, Message = message ?? string.Empty, RevisionBefore = revision, RevisionAfter = revision };
    }

    public sealed class LegalMutationEvent
    {
        public LegalMutationEvent(string operation, string subjectId, long revision, LegalOperationResult result)
        {
            Operation = operation ?? string.Empty;
            SubjectId = LegalModelUtility.N(subjectId);
            Revision = revision;
            Result = result;
        }

        public string Operation { get; }
        public string SubjectId { get; }
        public long Revision { get; }
        public LegalOperationResult Result { get; }
    }
    public sealed class LegalApplicabilityRequest { public string personId; public string organizationId; public string governmentId; public string jurisdictionId; public string[] jurisdictionIds = Array.Empty<string>(); public string territoryId; public string placeId; public string propertyId; public string officeId; public string professionId; public string actionId; public string subjectMatterId; public string[] legalStatusDefinitionIds = Array.Empty<string>(); public double worldTime; }
    public sealed class LegalApplicabilityResult
    {
        public LegalApplicabilityStatus Status { get; private set; } public IReadOnlyList<LegalProvisionRecordData> ApplicableProvisions { get; private set; } public IReadOnlyList<string> ConflictProvisionIds { get; private set; } public string Message { get; private set; }
        public static LegalApplicabilityResult Create(LegalApplicabilityStatus status, IEnumerable<LegalProvisionRecordData> provisions, IEnumerable<string> conflicts, string message) => new LegalApplicabilityResult { Status = status, ApplicableProvisions = (provisions ?? Array.Empty<LegalProvisionRecordData>()).Select(item => item.Clone()).ToArray(), ConflictProvisionIds = LegalModelUtility.C(conflicts), Message = message ?? string.Empty };
    }

    public sealed class LegalActionAuthorizationResult
    {
        public LegalActionAuthorizationResult(Organizations.OrganizationAuthorizationResult institutionalAuthorization, LegalApplicabilityResult legalApplicability)
        {
            InstitutionalAuthorization = institutionalAuthorization;
            LegalApplicability = legalApplicability ?? LegalApplicabilityResult.Create(LegalApplicabilityStatus.InvalidRequest, null, null, "Legal applicability was not evaluated.");
        }

        public Organizations.OrganizationAuthorizationResult InstitutionalAuthorization { get; }
        public LegalApplicabilityResult LegalApplicability { get; }
        public bool InstitutionallyAuthorized => InstitutionalAuthorization?.Succeeded == true;
        public bool LegallyAllowed => LegalApplicability.Status != LegalApplicabilityStatus.Prohibited
            && LegalApplicability.Status != LegalApplicabilityStatus.Conflict
            && LegalApplicability.Status != LegalApplicabilityStatus.AccessDenied
            && LegalApplicability.Status != LegalApplicabilityStatus.InvalidRequest;
        public bool Allowed => InstitutionallyAuthorized && LegallyAllowed;
    }

    public sealed class LegalProjectionResult<TRecord> where TRecord : class
    {
        public bool Succeeded { get; private set; }
        public bool Redacted { get; private set; }
        public TRecord Record { get; private set; }
        public string Message { get; private set; }
        public static LegalProjectionResult<TRecord> Success(TRecord record, bool redacted, string message) => new LegalProjectionResult<TRecord> { Succeeded = true, Redacted = redacted, Record = record, Message = message ?? string.Empty };
        public static LegalProjectionResult<TRecord> Denied(string message) => new LegalProjectionResult<TRecord> { Message = message ?? string.Empty };
    }
}
