using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    [Serializable]
    public sealed class LocationConnectionAccessContextData
    {
        public EntityLocationReferenceData actor;
        public string personId;
        public string[] organizationIds = Array.Empty<string>();
        public string[] rankIds = Array.Empty<string>();
        public string[] officeIds = Array.Empty<string>();
        public string[] authorityIds = Array.Empty<string>();
        public string[] employmentIds = Array.Empty<string>();
        public string[] propertyIds = Array.Empty<string>();
        public string[] permitIds = Array.Empty<string>();
        public string[] warrantIds = Array.Empty<string>();
        public string[] custodyRoleIds = Array.Empty<string>();
        public string[] keyInstanceIds = Array.Empty<string>();
        public string[] keyDefinitionIds = Array.Empty<string>();
        public string[] credentialIds = Array.Empty<string>();
        public string[] explicitGrantIds = Array.Empty<string>();
        public bool privileged;
        public bool developmentView;

        public LocationConnectionAccessContextData Clone()
        {
            return new LocationConnectionAccessContextData
            {
                actor = actor?.Clone(),
                personId = N(personId),
                organizationIds = C(organizationIds),
                rankIds = C(rankIds),
                officeIds = C(officeIds),
                authorityIds = C(authorityIds),
                employmentIds = C(employmentIds),
                propertyIds = C(propertyIds),
                permitIds = C(permitIds),
                warrantIds = C(warrantIds),
                custodyRoleIds = C(custodyRoleIds),
                keyInstanceIds = C(keyInstanceIds),
                keyDefinitionIds = C(keyDefinitionIds),
                credentialIds = C(credentialIds),
                explicitGrantIds = C(explicitGrantIds),
                privileged = privileged,
                developmentView = developmentView
            };
        }

        internal HashSet<string> Set(IEnumerable<string> values) => new HashSet<string>(C(values), StringComparer.Ordinal);
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class LocationConnectionRecordData
    {
        public string connectionId;
        public string connectionDefinitionId;
        public string worldId;
        public string displayName;
        public string sourceLocationId;
        public string destinationLocationId;
        public LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Bidirectional;
        public LocationConnectionLifecycleState lifecycleState = LocationConnectionLifecycleState.Active;
        public LocationConnectionOpenState openState = LocationConnectionOpenState.Open;
        public LocationConnectionLockState lockState = LocationConnectionLockState.NotLockable;
        public LocationConnectionBlockageState blockageState = LocationConnectionBlockageState.Clear;
        public LocationConnectionVisibility visibility = LocationConnectionVisibility.Public;
        public string[] accessPolicyDefinitionIds = Array.Empty<string>();
        public string sourceEndpointId;
        public string destinationEndpointId;
        public string[] interactionPointIds = Array.Empty<string>();
        public string semanticIdentityId;
        public string sceneBindingKey;
        public LocationConnectionSceneBindingCategory sceneBindingCategory = LocationConnectionSceneBindingCategory.None;
        public double createdWorldTime;
        public double endedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public LocationConnectionRecordData Clone()
        {
            return new LocationConnectionRecordData
            {
                connectionId = N(connectionId),
                connectionDefinitionId = N(connectionDefinitionId),
                worldId = N(worldId),
                displayName = N(displayName),
                sourceLocationId = N(sourceLocationId),
                destinationLocationId = N(destinationLocationId),
                directionality = directionality,
                lifecycleState = lifecycleState,
                openState = openState,
                lockState = lockState,
                blockageState = blockageState,
                visibility = visibility,
                accessPolicyDefinitionIds = C(accessPolicyDefinitionIds),
                sourceEndpointId = N(sourceEndpointId),
                destinationEndpointId = N(destinationEndpointId),
                interactionPointIds = C(interactionPointIds),
                semanticIdentityId = N(semanticIdentityId),
                sceneBindingKey = N(sceneBindingKey),
                sceneBindingCategory = sceneBindingCategory,
                createdWorldTime = createdWorldTime,
                endedWorldTime = endedWorldTime,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class LocationConnectionEndpointData
    {
        public string endpointId;
        public string connectionId;
        public string locationId;
        public LocationConnectionEndpointRole role = LocationConnectionEndpointRole.Source;
        public string[] sideAccessPolicyDefinitionIds = Array.Empty<string>();
        public string interactionPointId;
        public LocationConnectionVisibility visibility = LocationConnectionVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public LocationConnectionEndpointData Clone()
        {
            return new LocationConnectionEndpointData
            {
                endpointId = N(endpointId),
                connectionId = N(connectionId),
                locationId = N(locationId),
                role = role,
                sideAccessPolicyDefinitionIds = C(sideAccessPolicyDefinitionIds),
                interactionPointId = N(interactionPointId),
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class LocationConnectionStateHistoryData
    {
        public string historyId;
        public string connectionId;
        public string operation;
        public LocationConnectionLifecycleState lifecycleState;
        public LocationConnectionOpenState openState;
        public LocationConnectionLockState lockState;
        public LocationConnectionBlockageState blockageState;
        public double worldTime;
        public string actorKey;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision;

        public LocationConnectionStateHistoryData Clone()
        {
            return new LocationConnectionStateHistoryData
            {
                historyId = N(historyId),
                connectionId = N(connectionId),
                operation = N(operation),
                lifecycleState = lifecycleState,
                openState = openState,
                lockState = lockState,
                blockageState = blockageState,
                worldTime = worldTime,
                actorKey = N(actorKey),
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class LocationAccessGrantData
    {
        public string grantId;
        public string connectionId;
        public string endpointId;
        public EntityLocationReferenceData grantee;
        public LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Bidirectional;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public LocationAccessGrantLifecycleState lifecycleState = LocationAccessGrantLifecycleState.Active;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public LocationAccessGrantData Clone()
        {
            return new LocationAccessGrantData
            {
                grantId = N(grantId),
                connectionId = N(connectionId),
                endpointId = N(endpointId),
                grantee = grantee?.Clone(),
                directionality = directionality,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                lifecycleState = lifecycleState,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class LocationConnectionTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string connectionId;
        public string resultReferenceId;
        public long revision;

        public LocationConnectionTransactionRecordData Clone()
        {
            return new LocationConnectionTransactionRecordData
            {
                transactionId = N(transactionId),
                operation = N(operation),
                connectionId = N(connectionId),
                resultReferenceId = N(resultReferenceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class LocationConnectionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public LocationConnectionRecordData[] connections = Array.Empty<LocationConnectionRecordData>();
        public LocationConnectionEndpointData[] endpoints = Array.Empty<LocationConnectionEndpointData>();
        public LocationAccessGrantData[] grants = Array.Empty<LocationAccessGrantData>();
        public LocationConnectionStateHistoryData[] history = Array.Empty<LocationConnectionStateHistoryData>();
        public LocationConnectionTransactionRecordData[] transactions = Array.Empty<LocationConnectionTransactionRecordData>();

        public LocationConnectionRuntimeSaveData Clone()
        {
            return new LocationConnectionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                connections = CloneArray(connections),
                endpoints = CloneArray(endpoints),
                grants = CloneArray(grants),
                history = CloneArray(history),
                transactions = CloneArray(transactions)
            };
        }

        private static T[] CloneArray<T>(IEnumerable<T> source) where T : class
        {
            return (source ?? Array.Empty<T>()).Select(CloneDynamic).Where(item => item != null).ToArray();
        }

        private static T CloneDynamic<T>(T item) where T : class
        {
            return item switch
            {
                LocationConnectionRecordData value => value.Clone() as T,
                LocationConnectionEndpointData value => value.Clone() as T,
                LocationAccessGrantData value => value.Clone() as T,
                LocationConnectionStateHistoryData value => value.Clone() as T,
                LocationConnectionTransactionRecordData value => value.Clone() as T,
                _ => null
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class LocationConnectionSnapshot
    {
        private readonly LocationConnectionRecordData data;

        public LocationConnectionSnapshot(LocationConnectionRecordData record)
        {
            data = record?.Clone() ?? new LocationConnectionRecordData();
        }

        public string ConnectionId => data.connectionId ?? string.Empty;
        public string ConnectionDefinitionId => data.connectionDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public string DisplayName => data.displayName ?? string.Empty;
        public string SourceLocationId => data.sourceLocationId ?? string.Empty;
        public string DestinationLocationId => data.destinationLocationId ?? string.Empty;
        public LocationConnectionDirectionality Directionality => data.directionality;
        public LocationConnectionLifecycleState LifecycleState => data.lifecycleState;
        public LocationConnectionOpenState OpenState => data.openState;
        public LocationConnectionLockState LockState => data.lockState;
        public LocationConnectionBlockageState BlockageState => data.blockageState;
        public LocationConnectionVisibility Visibility => data.visibility;
        public IReadOnlyList<string> AccessPolicyDefinitionIds => (data.accessPolicyDefinitionIds ?? Array.Empty<string>()).ToArray();
        public string SourceEndpointId => data.sourceEndpointId ?? string.Empty;
        public string DestinationEndpointId => data.destinationEndpointId ?? string.Empty;
        public IReadOnlyList<string> InteractionPointIds => (data.interactionPointIds ?? Array.Empty<string>()).ToArray();
        public string SemanticIdentityId => data.semanticIdentityId ?? string.Empty;
        public string SceneBindingKey => data.sceneBindingKey ?? string.Empty;
        public LocationConnectionSceneBindingCategory SceneBindingCategory => data.sceneBindingCategory;
        public double CreatedWorldTime => data.createdWorldTime;
        public double EndedWorldTime => data.endedWorldTime;
        public long Revision => data.revision;
        public LocationConnectionRecordData ToSaveData() => data.Clone();
    }

    public sealed class LocationConnectionEndpointSnapshot
    {
        private readonly LocationConnectionEndpointData data;

        public LocationConnectionEndpointSnapshot(LocationConnectionEndpointData endpoint)
        {
            data = endpoint?.Clone() ?? new LocationConnectionEndpointData();
        }

        public string EndpointId => data.endpointId ?? string.Empty;
        public string ConnectionId => data.connectionId ?? string.Empty;
        public string LocationId => data.locationId ?? string.Empty;
        public LocationConnectionEndpointRole Role => data.role;
        public IReadOnlyList<string> SideAccessPolicyDefinitionIds => (data.sideAccessPolicyDefinitionIds ?? Array.Empty<string>()).ToArray();
        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public LocationConnectionVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public LocationConnectionEndpointData ToSaveData() => data.Clone();
    }

    public sealed class LocationAccessGrantSnapshot
    {
        private readonly LocationAccessGrantData data;

        public LocationAccessGrantSnapshot(LocationAccessGrantData grant)
        {
            data = grant?.Clone() ?? new LocationAccessGrantData();
        }

        public string GrantId => data.grantId ?? string.Empty;
        public string ConnectionId => data.connectionId ?? string.Empty;
        public string EndpointId => data.endpointId ?? string.Empty;
        public EntityLocationReferenceData Grantee => data.grantee?.Clone();
        public LocationConnectionDirectionality Directionality => data.directionality;
        public double StartWorldTime => data.startWorldTime;
        public double EndWorldTime => data.endWorldTime;
        public LocationAccessGrantLifecycleState LifecycleState => data.lifecycleState;
        public long Revision => data.revision;
        public LocationAccessGrantData ToSaveData() => data.Clone();
    }

    public sealed class LocationConnectionOperationResult
    {
        private LocationConnectionOperationResult(LocationConnectionOperationStatus status, string message, long beforeRevision, long afterRevision, LocationConnectionSnapshot connection, bool preview = false, bool duplicate = false, EntityLocationOperationResult placementResult = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Connection = connection;
            Preview = preview;
            Duplicate = duplicate;
            PlacementResult = placementResult;
        }

        public LocationConnectionOperationStatus Status { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public LocationConnectionSnapshot Connection { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public EntityLocationOperationResult PlacementResult { get; }
        public bool Succeeded => Status == LocationConnectionOperationStatus.Succeeded || Status == LocationConnectionOperationStatus.Preview || Duplicate;

        public static LocationConnectionOperationResult Success(LocationConnectionSnapshot connection, string message, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false, EntityLocationOperationResult placementResult = null)
        {
            LocationConnectionOperationStatus status = preview
                ? LocationConnectionOperationStatus.Preview
                : duplicate
                    ? LocationConnectionOperationStatus.Duplicate
                    : LocationConnectionOperationStatus.Succeeded;
            return new LocationConnectionOperationResult(status, message, beforeRevision, afterRevision, connection, preview, duplicate, placementResult);
        }

        public static LocationConnectionOperationResult Failure(LocationConnectionOperationStatus status, string message, long beforeRevision)
        {
            return new LocationConnectionOperationResult(status, message, beforeRevision, beforeRevision, null);
        }
    }

    public sealed class LocationConnectionAccessResult
    {
        public string connectionId;
        public string fromLocationId;
        public string toLocationId;
        public EntityLocationReferenceData actor;
        public LocationConnectionAccessState accessState = LocationConnectionAccessState.Unknown;
        public LocationConnectionLifecycleState lifecycleState;
        public LocationConnectionOpenState openState;
        public LocationConnectionLockState lockState;
        public LocationConnectionBlockageState blockageState;
        public bool directionAllowed;
        public bool policyAllowed;
        public bool membershipSatisfied;
        public bool rankSatisfied;
        public bool officeSatisfied;
        public bool authoritySatisfied;
        public bool employmentSatisfied;
        public bool ownershipSatisfied;
        public bool legalPermissionSatisfied;
        public bool warrantSatisfied;
        public bool custodySatisfied;
        public bool keySatisfied;
        public bool explicitGrantSatisfied;
        public long connectionRevision;
        public long entityLocationRevision;
        public string[] denialReasons = Array.Empty<string>();
        public string diagnostics;
        public bool Allowed => accessState == LocationConnectionAccessState.Allowed;

        public LocationConnectionAccessResult Clone()
        {
            return new LocationConnectionAccessResult
            {
                connectionId = N(connectionId),
                fromLocationId = N(fromLocationId),
                toLocationId = N(toLocationId),
                actor = actor?.Clone(),
                accessState = accessState,
                lifecycleState = lifecycleState,
                openState = openState,
                lockState = lockState,
                blockageState = blockageState,
                directionAllowed = directionAllowed,
                policyAllowed = policyAllowed,
                membershipSatisfied = membershipSatisfied,
                rankSatisfied = rankSatisfied,
                officeSatisfied = officeSatisfied,
                authoritySatisfied = authoritySatisfied,
                employmentSatisfied = employmentSatisfied,
                ownershipSatisfied = ownershipSatisfied,
                legalPermissionSatisfied = legalPermissionSatisfied,
                warrantSatisfied = warrantSatisfied,
                custodySatisfied = custodySatisfied,
                keySatisfied = keySatisfied,
                explicitGrantSatisfied = explicitGrantSatisfied,
                connectionRevision = connectionRevision,
                entityLocationRevision = entityLocationRevision,
                denialReasons = C(denialReasons),
                diagnostics = diagnostics ?? string.Empty
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class LocationConnectionCreateRequest
    {
        public string transactionId;
        public string connectionId;
        public string connectionDefinitionId;
        public string displayName;
        public string sourceLocationId;
        public string destinationLocationId;
        public LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Unknown;
        public LocationConnectionOpenState openState = LocationConnectionOpenState.Unknown;
        public LocationConnectionLockState lockState = LocationConnectionLockState.Unknown;
        public LocationConnectionBlockageState blockageState = LocationConnectionBlockageState.Clear;
        public LocationConnectionVisibility visibility = LocationConnectionVisibility.Public;
        public string[] accessPolicyDefinitionIds = Array.Empty<string>();
        public string[] interactionPointIds = Array.Empty<string>();
        public string semanticIdentityId;
        public string sceneBindingKey;
        public LocationConnectionSceneBindingCategory sceneBindingCategory = LocationConnectionSceneBindingCategory.None;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class LocationConnectionStateMutationRequest
    {
        public string transactionId;
        public string connectionId;
        public LocationConnectionLifecycleState lifecycleState = LocationConnectionLifecycleState.Unknown;
        public LocationConnectionOpenState openState = LocationConnectionOpenState.Unknown;
        public LocationConnectionLockState lockState = LocationConnectionLockState.Unknown;
        public LocationConnectionBlockageState blockageState = LocationConnectionBlockageState.Unknown;
        public LocationConnectionAccessContextData accessContext;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class LocationAccessGrantRequest
    {
        public string transactionId;
        public string grantId;
        public string connectionId;
        public string endpointId;
        public EntityLocationReferenceData grantee;
        public LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Bidirectional;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class LocationConnectionTraversalRequest
    {
        public string transactionId;
        public string connectionId;
        public EntityLocationReferenceData actor;
        public string fromLocationId;
        public string toLocationId;
        public LocationConnectionAccessContextData accessContext;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
        public long expectedEntityLocationRevision = -1L;
    }
}
