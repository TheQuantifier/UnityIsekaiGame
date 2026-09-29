using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;

namespace UnityIsekaiGame.WorldLocations
{
    internal static class PoliticalTravelModelUtility
    {
        public static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        public static string[] C(IEnumerable<string> values) => PoliticalModelUtility.Clean(values);
        public static bool Active(double start, double end, double time) => time >= start && (end < 0d || time <= end);
    }

    [Serializable]
    public sealed class BorderCheckpointRecordData
    {
        public string checkpointId;
        public string worldId;
        public string displayName;
        public string locationId;
        public string routeSegmentId;
        public string sourceTerritoryId;
        public string destinationTerritoryId;
        public string governingGovernmentId;
        public string jurisdictionId;
        public BorderCheckpointPolicy policy = BorderCheckpointPolicy.RequireInspection;
        public BorderCheckpointLifecycleState lifecycleState = BorderCheckpointLifecycleState.Active;
        public string[] requiredActionIds = Array.Empty<string>();
        public string[] requiredPermitIds = Array.Empty<string>();
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public BorderCheckpointRecordData Clone()
        {
            return new BorderCheckpointRecordData
            {
                checkpointId = PoliticalTravelModelUtility.N(checkpointId),
                worldId = PoliticalTravelModelUtility.N(worldId),
                displayName = displayName ?? string.Empty,
                locationId = PoliticalTravelModelUtility.N(locationId),
                routeSegmentId = PoliticalTravelModelUtility.N(routeSegmentId),
                sourceTerritoryId = PoliticalTravelModelUtility.N(sourceTerritoryId),
                destinationTerritoryId = PoliticalTravelModelUtility.N(destinationTerritoryId),
                governingGovernmentId = PoliticalTravelModelUtility.N(governingGovernmentId),
                jurisdictionId = PoliticalTravelModelUtility.N(jurisdictionId),
                policy = policy,
                lifecycleState = lifecycleState,
                requiredActionIds = PoliticalTravelModelUtility.C(requiredActionIds),
                requiredPermitIds = PoliticalTravelModelUtility.C(requiredPermitIds),
                visibility = visibility,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                sourceEventId = PoliticalTravelModelUtility.N(sourceEventId),
                sourceRecordId = PoliticalTravelModelUtility.N(sourceRecordId),
                provenanceId = PoliticalTravelModelUtility.N(provenanceId),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TravelCrossingAuthorizationRecordData
    {
        public string authorizationId;
        public string worldId;
        public string travelerPersonId;
        public string checkpointId;
        public string territoryId;
        public string jurisdictionId;
        public string issuingGovernmentId;
        public string[] authorizedActionIds = Array.Empty<string>();
        public string sourceEntitlementId;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public bool revoked;
        public PoliticalVisibility visibility = PoliticalVisibility.Restricted;
        public long revision = 1L;

        public TravelCrossingAuthorizationRecordData Clone()
        {
            return new TravelCrossingAuthorizationRecordData
            {
                authorizationId = PoliticalTravelModelUtility.N(authorizationId),
                worldId = PoliticalTravelModelUtility.N(worldId),
                travelerPersonId = PoliticalTravelModelUtility.N(travelerPersonId),
                checkpointId = PoliticalTravelModelUtility.N(checkpointId),
                territoryId = PoliticalTravelModelUtility.N(territoryId),
                jurisdictionId = PoliticalTravelModelUtility.N(jurisdictionId),
                issuingGovernmentId = PoliticalTravelModelUtility.N(issuingGovernmentId),
                authorizedActionIds = PoliticalTravelModelUtility.C(authorizedActionIds),
                sourceEntitlementId = PoliticalTravelModelUtility.N(sourceEntitlementId),
                effectiveWorldTime = effectiveWorldTime,
                expirationWorldTime = expirationWorldTime,
                revoked = revoked,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PoliticalTravelCrossingRecordData
    {
        public string crossingId;
        public string worldId;
        public string travelerPersonId;
        public string originLocationId;
        public string destinationLocationId;
        public string routeSegmentId;
        public string sourceTerritoryId;
        public string destinationTerritoryId;
        public string sourceJurisdictionId;
        public string destinationJurisdictionId;
        public string checkpointId;
        public string authorizationId;
        public PoliticalTravelCrossingClassification classification = PoliticalTravelCrossingClassification.Unknown;
        public PoliticalTravelLegalState legalState = PoliticalTravelLegalState.NotEvaluated;
        public PhysicalLegalTravelState combinedState = PhysicalLegalTravelState.Unknown;
        public PoliticalTravelCrossingLifecycleState lifecycleState = PoliticalTravelCrossingLifecycleState.Completed;
        public bool illegalCrossing;
        public bool enforcementOpportunity;
        public string[] visibleWantedStatusIds = Array.Empty<string>();
        public string[] visibleWarrantIds = Array.Empty<string>();
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public PoliticalTravelCrossingRecordData Clone()
        {
            return new PoliticalTravelCrossingRecordData
            {
                crossingId = PoliticalTravelModelUtility.N(crossingId),
                worldId = PoliticalTravelModelUtility.N(worldId),
                travelerPersonId = PoliticalTravelModelUtility.N(travelerPersonId),
                originLocationId = PoliticalTravelModelUtility.N(originLocationId),
                destinationLocationId = PoliticalTravelModelUtility.N(destinationLocationId),
                routeSegmentId = PoliticalTravelModelUtility.N(routeSegmentId),
                sourceTerritoryId = PoliticalTravelModelUtility.N(sourceTerritoryId),
                destinationTerritoryId = PoliticalTravelModelUtility.N(destinationTerritoryId),
                sourceJurisdictionId = PoliticalTravelModelUtility.N(sourceJurisdictionId),
                destinationJurisdictionId = PoliticalTravelModelUtility.N(destinationJurisdictionId),
                checkpointId = PoliticalTravelModelUtility.N(checkpointId),
                authorizationId = PoliticalTravelModelUtility.N(authorizationId),
                classification = classification,
                legalState = legalState,
                combinedState = combinedState,
                lifecycleState = lifecycleState,
                illegalCrossing = illegalCrossing,
                enforcementOpportunity = enforcementOpportunity,
                visibleWantedStatusIds = PoliticalTravelModelUtility.C(visibleWantedStatusIds),
                visibleWarrantIds = PoliticalTravelModelUtility.C(visibleWarrantIds),
                worldTime = worldTime,
                sourceEventId = PoliticalTravelModelUtility.N(sourceEventId),
                sourceRecordId = PoliticalTravelModelUtility.N(sourceRecordId),
                provenanceId = PoliticalTravelModelUtility.N(provenanceId),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PoliticalTravelTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public long revision;

        public PoliticalTravelTransactionRecordData Clone()
        {
            return new PoliticalTravelTransactionRecordData
            {
                transactionId = PoliticalTravelModelUtility.N(transactionId),
                operation = operation ?? string.Empty,
                subjectId = PoliticalTravelModelUtility.N(subjectId),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PoliticalTravelRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public BorderCheckpointRecordData[] checkpoints = Array.Empty<BorderCheckpointRecordData>();
        public TravelCrossingAuthorizationRecordData[] authorizations = Array.Empty<TravelCrossingAuthorizationRecordData>();
        public PoliticalTravelCrossingRecordData[] crossings = Array.Empty<PoliticalTravelCrossingRecordData>();
        public PoliticalTravelTransactionRecordData[] transactions = Array.Empty<PoliticalTravelTransactionRecordData>();

        public PoliticalTravelRuntimeSaveData Clone()
        {
            return new PoliticalTravelRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = PoliticalTravelModelUtility.N(worldId),
                revision = revision,
                checkpoints = (checkpoints ?? Array.Empty<BorderCheckpointRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                authorizations = (authorizations ?? Array.Empty<TravelCrossingAuthorizationRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                crossings = (crossings ?? Array.Empty<PoliticalTravelCrossingRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                transactions = (transactions ?? Array.Empty<PoliticalTravelTransactionRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray()
            };
        }
    }

    public sealed class BorderCheckpointSnapshot
    {
        private readonly BorderCheckpointRecordData data;
        public BorderCheckpointSnapshot(BorderCheckpointRecordData record) { data = record?.Clone() ?? new BorderCheckpointRecordData(); }
        public string CheckpointId => data.checkpointId ?? string.Empty;
        public string LocationId => data.locationId ?? string.Empty;
        public string RouteSegmentId => data.routeSegmentId ?? string.Empty;
        public string SourceTerritoryId => data.sourceTerritoryId ?? string.Empty;
        public string DestinationTerritoryId => data.destinationTerritoryId ?? string.Empty;
        public string GoverningGovernmentId => data.governingGovernmentId ?? string.Empty;
        public BorderCheckpointPolicy Policy => data.policy;
        public BorderCheckpointLifecycleState LifecycleState => data.lifecycleState;
        public PoliticalVisibility Visibility => data.visibility;
        public IReadOnlyList<string> RequiredActionIds => (data.requiredActionIds ?? Array.Empty<string>()).ToArray();
        public BorderCheckpointRecordData ToSaveData() => data.Clone();
    }

    public sealed class PoliticalTravelWantedSummary
    {
        public PoliticalTravelWantedSummary(IEnumerable<string> visibleWantedStatusIds, IEnumerable<string> visibleWarrantIds, bool hiddenRestrictedInformation)
        {
            VisibleWantedStatusIds = PoliticalTravelModelUtility.C(visibleWantedStatusIds);
            VisibleWarrantIds = PoliticalTravelModelUtility.C(visibleWarrantIds);
            HiddenRestrictedInformation = hiddenRestrictedInformation;
        }

        public IReadOnlyList<string> VisibleWantedStatusIds { get; }
        public IReadOnlyList<string> VisibleWarrantIds { get; }
        public bool HiddenRestrictedInformation { get; }
    }

    public sealed class PoliticalTravelTerritoryResolution
    {
        public PoliticalTravelTerritoryResolution(string locationId, PoliticalTerritoryRecordData territory, bool contested, string diagnostics)
        {
            LocationId = PoliticalTravelModelUtility.N(locationId);
            Territory = territory?.Clone();
            Contested = contested;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string LocationId { get; }
        public PoliticalTerritoryRecordData Territory { get; }
        public string TerritoryId => Territory?.territoryId ?? string.Empty;
        public string PrimaryGovernmentId => Territory?.primaryGovernmentId ?? string.Empty;
        public bool Contested { get; }
        public string Diagnostics { get; }
    }

    public sealed class PoliticalTravelLegalityResult
    {
        public PoliticalTravelLegalityResult(PoliticalTravelLegalState state, LegalApplicabilityResult applicability, IReadOnlyList<string> requiredActionIds, string diagnostics)
        {
            State = state;
            Applicability = applicability;
            RequiredActionIds = (requiredActionIds ?? Array.Empty<string>()).ToArray();
            Diagnostics = diagnostics ?? string.Empty;
        }

        public PoliticalTravelLegalState State { get; }
        public LegalApplicabilityResult Applicability { get; }
        public IReadOnlyList<string> RequiredActionIds { get; }
        public string Diagnostics { get; }
    }

    public sealed class BorderCheckpointEvaluationResult
    {
        public BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState state, IEnumerable<BorderCheckpointSnapshot> checkpoints, IEnumerable<string> missingActionIds, string authorizationId, string diagnostics)
        {
            State = state;
            Checkpoints = (checkpoints ?? Array.Empty<BorderCheckpointSnapshot>()).Where(item => item != null).ToArray();
            MissingActionIds = PoliticalTravelModelUtility.C(missingActionIds);
            AuthorizationId = PoliticalTravelModelUtility.N(authorizationId);
            Diagnostics = diagnostics ?? string.Empty;
        }

        public BorderCheckpointEvaluationState State { get; }
        public IReadOnlyList<BorderCheckpointSnapshot> Checkpoints { get; }
        public IReadOnlyList<string> MissingActionIds { get; }
        public string AuthorizationId { get; }
        public string Diagnostics { get; }
    }

    public sealed class PoliticalTravelEvaluationResult
    {
        public bool Succeeded { get; private set; }
        public PoliticalTravelOperationCode Code { get; private set; }
        public PoliticalTravelCrossingClassification Classification { get; private set; }
        public PhysicalLegalTravelState CombinedState { get; private set; }
        public PoliticalTravelTerritoryResolution OriginTerritory { get; private set; }
        public PoliticalTravelTerritoryResolution DestinationTerritory { get; private set; }
        public JurisdictionResolutionResult OriginJurisdiction { get; private set; }
        public JurisdictionResolutionResult DestinationJurisdiction { get; private set; }
        public PoliticalTravelLegalityResult Legal { get; private set; }
        public BorderCheckpointEvaluationResult Checkpoint { get; private set; }
        public PoliticalTravelWantedSummary Wanted { get; private set; }
        public bool PhysicalTravelPossible { get; private set; }
        public bool IllegalCrossing { get; private set; }
        public bool EnforcementOpportunity { get; private set; }
        public string Message { get; private set; }
        public long Revision { get; private set; }

        public static PoliticalTravelEvaluationResult Create(bool succeeded, PoliticalTravelOperationCode code, PoliticalTravelCrossingClassification classification, PhysicalLegalTravelState combinedState, PoliticalTravelTerritoryResolution origin, PoliticalTravelTerritoryResolution destination, JurisdictionResolutionResult originJurisdiction, JurisdictionResolutionResult destinationJurisdiction, PoliticalTravelLegalityResult legal, BorderCheckpointEvaluationResult checkpoint, PoliticalTravelWantedSummary wanted, bool physicalTravelPossible, bool illegalCrossing, bool enforcementOpportunity, string message, long revision)
        {
            return new PoliticalTravelEvaluationResult
            {
                Succeeded = succeeded,
                Code = code,
                Classification = classification,
                CombinedState = combinedState,
                OriginTerritory = origin,
                DestinationTerritory = destination,
                OriginJurisdiction = originJurisdiction,
                DestinationJurisdiction = destinationJurisdiction,
                Legal = legal,
                Checkpoint = checkpoint,
                Wanted = wanted,
                PhysicalTravelPossible = physicalTravelPossible,
                IllegalCrossing = illegalCrossing,
                EnforcementOpportunity = enforcementOpportunity,
                Message = message ?? string.Empty,
                Revision = revision
            };
        }
    }

    public sealed class PoliticalTravelOperationResult
    {
        private PoliticalTravelOperationResult(PoliticalTravelOperationCode code, string message, long before, long after, BorderCheckpointSnapshot checkpoint = null, TravelCrossingAuthorizationRecordData authorization = null, PoliticalTravelCrossingRecordData crossing = null, PoliticalTravelEvaluationResult evaluation = null, bool preview = false, bool duplicate = false)
        {
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
            Checkpoint = checkpoint;
            Authorization = authorization?.Clone();
            Crossing = crossing?.Clone();
            Evaluation = evaluation;
            Preview = preview;
            Duplicate = duplicate;
        }

        public PoliticalTravelOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public BorderCheckpointSnapshot Checkpoint { get; }
        public TravelCrossingAuthorizationRecordData Authorization { get; }
        public PoliticalTravelCrossingRecordData Crossing { get; }
        public PoliticalTravelEvaluationResult Evaluation { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Code == PoliticalTravelOperationCode.Succeeded || Code == PoliticalTravelOperationCode.Preview || Code == PoliticalTravelOperationCode.Duplicate;
        public static PoliticalTravelOperationResult Success(string message, long before, long after, BorderCheckpointSnapshot checkpoint = null, TravelCrossingAuthorizationRecordData authorization = null, PoliticalTravelCrossingRecordData crossing = null, PoliticalTravelEvaluationResult evaluation = null, bool preview = false, bool duplicate = false) => new PoliticalTravelOperationResult(preview ? PoliticalTravelOperationCode.Preview : duplicate ? PoliticalTravelOperationCode.Duplicate : PoliticalTravelOperationCode.Succeeded, message, before, after, checkpoint, authorization, crossing, evaluation, preview, duplicate);
        public static PoliticalTravelOperationResult Failure(PoliticalTravelOperationCode code, string message, long revision, PoliticalTravelEvaluationResult evaluation = null) => new PoliticalTravelOperationResult(code, message, revision, revision, evaluation: evaluation);
    }

    public sealed class BorderCheckpointCreateRequest
    {
        public string transactionId;
        public string checkpointId;
        public string displayName;
        public string locationId;
        public string routeSegmentId;
        public string sourceTerritoryId;
        public string destinationTerritoryId;
        public string governingGovernmentId;
        public string jurisdictionId;
        public BorderCheckpointPolicy policy = BorderCheckpointPolicy.RequireInspection;
        public BorderCheckpointLifecycleState lifecycleState = BorderCheckpointLifecycleState.Active;
        public string[] requiredActionIds = Array.Empty<string>();
        public string[] requiredPermitIds = Array.Empty<string>();
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
    }

    public sealed class TravelCrossingAuthorizationRequest
    {
        public string transactionId;
        public string authorizationId;
        public string travelerPersonId;
        public string checkpointId;
        public string territoryId;
        public string jurisdictionId;
        public string issuingGovernmentId;
        public string[] authorizedActionIds = Array.Empty<string>();
        public string sourceEntitlementId;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Restricted;
        public bool preview;
    }

    public class PoliticalTravelEvaluationRequest
    {
        public EntityLocationReferenceData traveler;
        public string travelerPersonId;
        public string originLocationId;
        public string destinationLocationId;
        public string routeSegmentId;
        public bool physicalTravelPossible = true;
        public TravelLegalComplianceMode legalComplianceMode = TravelLegalComplianceMode.StructuralOnlyDevelopment;
        public PoliticalTravelVisibilityMode visibilityMode = PoliticalTravelVisibilityMode.TravelerSafe;
        public double worldTime;
        public string[] legalStatusDefinitionIds = Array.Empty<string>();
        public string[] knownCheckpointIds = Array.Empty<string>();
    }

    public sealed class PoliticalTravelCrossingRequest : PoliticalTravelEvaluationRequest
    {
        public string transactionId;
        public string crossingId;
        public string authorizationId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }
}
