using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    [Serializable]
    public sealed class InteractionSubjectReferenceData
    {
        public string subjectType;
        public string subjectId;
        public string worldId;

        public string StableKey => $"{Normalize(subjectType)}:{Normalize(subjectId)}:{Normalize(worldId)}";

        public InteractionSubjectReferenceData Clone()
        {
            return new InteractionSubjectReferenceData
            {
                subjectType = Normalize(subjectType),
                subjectId = Normalize(subjectId),
                worldId = Normalize(worldId)
            };
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionPointRecordData
    {
        public string interactionPointId;
        public string interactionPointDefinitionId;
        public string worldId;
        public string displayName;
        public InteractionPointLifecycleState lifecycleState = InteractionPointLifecycleState.Active;
        public string activeHostAssignmentId;
        public string activeHostLocationId;
        public string[] serviceDefinitionIds = Array.Empty<string>();
        public int capacityOverride = 0;
        public bool exclusiveUseOverride;
        public bool hasExclusiveUseOverride;
        public InteractionPointUseState useState = InteractionPointUseState.Free;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sceneBindingKey;
        public InteractionSceneBindingCategory sceneBindingCategory = InteractionSceneBindingCategory.None;
        public double createdWorldTime;
        public double endedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public InteractionPointRecordData Clone()
        {
            return new InteractionPointRecordData
            {
                interactionPointId = N(interactionPointId),
                interactionPointDefinitionId = N(interactionPointDefinitionId),
                worldId = N(worldId),
                displayName = N(displayName),
                lifecycleState = lifecycleState,
                activeHostAssignmentId = N(activeHostAssignmentId),
                activeHostLocationId = N(activeHostLocationId),
                serviceDefinitionIds = C(serviceDefinitionIds),
                capacityOverride = capacityOverride,
                exclusiveUseOverride = exclusiveUseOverride,
                hasExclusiveUseOverride = hasExclusiveUseOverride,
                useState = useState,
                visibility = visibility,
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
    public sealed class InteractionPointHostAssignmentData
    {
        public string assignmentId;
        public string interactionPointId;
        public string hostLocationId;
        public string worldId;
        public string assignmentCategory;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public InteractionPointHostAssignmentData Clone()
        {
            return new InteractionPointHostAssignmentData
            {
                assignmentId = N(assignmentId),
                interactionPointId = N(interactionPointId),
                hostLocationId = N(hostLocationId),
                worldId = N(worldId),
                assignmentCategory = N(assignmentCategory),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionSubjectLinkData
    {
        public string linkId;
        public string interactionPointId;
        public InteractionSubjectLinkRole role;
        public InteractionSubjectReferenceData subject = new InteractionSubjectReferenceData();
        public double startWorldTime;
        public double endWorldTime = -1d;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public InteractionSubjectLinkData Clone()
        {
            return new InteractionSubjectLinkData
            {
                linkId = N(linkId),
                interactionPointId = N(interactionPointId),
                role = role,
                subject = subject?.Clone() ?? new InteractionSubjectReferenceData(),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionProviderAssignmentData
    {
        public string assignmentId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public InteractionProviderRequirementKind requirementKind = InteractionProviderRequirementKind.AssignedPerson;
        public EntityLocationReferenceData providerEntity;
        public string providerOfficeId;
        public string providerOrganizationId;
        public InteractionPhysicalPresencePolicy presencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public InteractionPointLifecycleState lifecycleState = InteractionPointLifecycleState.Active;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public InteractionProviderAssignmentData Clone()
        {
            return new InteractionProviderAssignmentData
            {
                assignmentId = N(assignmentId),
                interactionPointId = N(interactionPointId),
                serviceDefinitionId = N(serviceDefinitionId),
                requirementKind = requirementKind,
                providerEntity = providerEntity?.Clone(),
                providerOfficeId = N(providerOfficeId),
                providerOrganizationId = N(providerOrganizationId),
                presencePolicy = presencePolicy,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                lifecycleState = lifecycleState,
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionReservationData
    {
        public string reservationId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public InteractionSubjectReferenceData reservingSubject = new InteractionSubjectReferenceData();
        public double startWorldTime;
        public double endWorldTime = -1d;
        public int priority;
        public InteractionReservationLifecycle lifecycleState = InteractionReservationLifecycle.Active;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public InteractionReservationData Clone()
        {
            return new InteractionReservationData
            {
                reservationId = N(reservationId),
                interactionPointId = N(interactionPointId),
                serviceDefinitionId = N(serviceDefinitionId),
                reservingSubject = reservingSubject?.Clone() ?? new InteractionSubjectReferenceData(),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                priority = priority,
                lifecycleState = lifecycleState,
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionUseSessionData
    {
        public string sessionId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public EntityLocationReferenceData consumerEntity;
        public EntityLocationReferenceData providerEntity;
        public string reservationId;
        public double startWorldTime;
        public double expectedEndWorldTime = -1d;
        public double endWorldTime = -1d;
        public InteractionUseSessionLifecycle lifecycleState = InteractionUseSessionLifecycle.Active;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string eligibilityFingerprint;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public InteractionUseSessionData Clone()
        {
            return new InteractionUseSessionData
            {
                sessionId = N(sessionId),
                interactionPointId = N(interactionPointId),
                serviceDefinitionId = N(serviceDefinitionId),
                consumerEntity = consumerEntity?.Clone(),
                providerEntity = providerEntity?.Clone(),
                reservationId = N(reservationId),
                startWorldTime = startWorldTime,
                expectedEndWorldTime = expectedEndWorldTime,
                endWorldTime = endWorldTime,
                lifecycleState = lifecycleState,
                visibility = visibility,
                eligibilityFingerprint = N(eligibilityFingerprint),
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionPointTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string targetId;
        public long revision;

        public InteractionPointTransactionRecordData Clone()
        {
            return new InteractionPointTransactionRecordData
            {
                transactionId = N(transactionId),
                operation = N(operation),
                targetId = N(targetId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class InteractionPointRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<InteractionPointRecordData> points = new List<InteractionPointRecordData>();
        public List<InteractionPointHostAssignmentData> hostAssignments = new List<InteractionPointHostAssignmentData>();
        public List<InteractionSubjectLinkData> subjectLinks = new List<InteractionSubjectLinkData>();
        public List<InteractionProviderAssignmentData> providerAssignments = new List<InteractionProviderAssignmentData>();
        public List<InteractionReservationData> reservations = new List<InteractionReservationData>();
        public List<InteractionUseSessionData> useSessions = new List<InteractionUseSessionData>();
        public List<InteractionPointTransactionRecordData> transactions = new List<InteractionPointTransactionRecordData>();

        public InteractionPointRuntimeSaveData Clone()
        {
            return new InteractionPointRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                points = (points ?? new List<InteractionPointRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                hostAssignments = (hostAssignments ?? new List<InteractionPointHostAssignmentData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                subjectLinks = (subjectLinks ?? new List<InteractionSubjectLinkData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                providerAssignments = (providerAssignments ?? new List<InteractionProviderAssignmentData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                reservations = (reservations ?? new List<InteractionReservationData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                useSessions = (useSessions ?? new List<InteractionUseSessionData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<InteractionPointTransactionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class InteractionPointSnapshot
    {
        private readonly InteractionPointRecordData data;

        public InteractionPointSnapshot(InteractionPointRecordData record)
        {
            data = record?.Clone() ?? new InteractionPointRecordData();
        }

        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public string InteractionPointDefinitionId => data.interactionPointDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public string DisplayName => data.displayName ?? string.Empty;
        public InteractionPointLifecycleState LifecycleState => data.lifecycleState;
        public string ActiveHostAssignmentId => data.activeHostAssignmentId ?? string.Empty;
        public string ActiveHostLocationId => data.activeHostLocationId ?? string.Empty;
        public IReadOnlyList<string> ServiceDefinitionIds => (data.serviceDefinitionIds ?? Array.Empty<string>()).ToArray();
        public int CapacityOverride => data.capacityOverride;
        public bool ExclusiveUseOverride => data.exclusiveUseOverride;
        public bool HasExclusiveUseOverride => data.hasExclusiveUseOverride;
        public InteractionPointUseState UseState => data.useState;
        public InteractionPointVisibility Visibility => data.visibility;
        public string SceneBindingKey => data.sceneBindingKey ?? string.Empty;
        public InteractionSceneBindingCategory SceneBindingCategory => data.sceneBindingCategory;
        public double CreatedWorldTime => data.createdWorldTime;
        public double EndedWorldTime => data.endedWorldTime;
        public long Revision => data.revision;
        public bool IsActive => LifecycleState == InteractionPointLifecycleState.Active;
        public InteractionPointRecordData ToSaveData() => data.Clone();
    }

    public sealed class InteractionHostAssignmentSnapshot
    {
        private readonly InteractionPointHostAssignmentData data;

        public InteractionHostAssignmentSnapshot(InteractionPointHostAssignmentData assignment)
        {
            data = assignment?.Clone() ?? new InteractionPointHostAssignmentData();
        }

        public string AssignmentId => data.assignmentId ?? string.Empty;
        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public string HostLocationId => data.hostLocationId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public double StartWorldTime => data.startWorldTime;
        public double EndWorldTime => data.endWorldTime;
        public InteractionPointVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public bool IsActive => EndWorldTime < 0d;
        public InteractionPointHostAssignmentData ToSaveData() => data.Clone();
    }

    public sealed class InteractionSubjectLinkSnapshot
    {
        private readonly InteractionSubjectLinkData data;

        public InteractionSubjectLinkSnapshot(InteractionSubjectLinkData link)
        {
            data = link?.Clone() ?? new InteractionSubjectLinkData();
        }

        public string LinkId => data.linkId ?? string.Empty;
        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public InteractionSubjectLinkRole Role => data.role;
        public InteractionSubjectReferenceData Subject => data.subject?.Clone() ?? new InteractionSubjectReferenceData();
        public double StartWorldTime => data.startWorldTime;
        public double EndWorldTime => data.endWorldTime;
        public InteractionPointVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public bool IsActive => EndWorldTime < 0d;
        public InteractionSubjectLinkData ToSaveData() => data.Clone();
    }

    public sealed class InteractionProviderAssignmentSnapshot
    {
        private readonly InteractionProviderAssignmentData data;

        public InteractionProviderAssignmentSnapshot(InteractionProviderAssignmentData assignment)
        {
            data = assignment?.Clone() ?? new InteractionProviderAssignmentData();
        }

        public string AssignmentId => data.assignmentId ?? string.Empty;
        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public string ServiceDefinitionId => data.serviceDefinitionId ?? string.Empty;
        public InteractionProviderRequirementKind RequirementKind => data.requirementKind;
        public EntityLocationReferenceData ProviderEntity => data.providerEntity?.Clone();
        public string ProviderOfficeId => data.providerOfficeId ?? string.Empty;
        public string ProviderOrganizationId => data.providerOrganizationId ?? string.Empty;
        public InteractionPhysicalPresencePolicy PresencePolicy => data.presencePolicy;
        public InteractionPointLifecycleState LifecycleState => data.lifecycleState;
        public InteractionPointVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public bool IsActive => LifecycleState == InteractionPointLifecycleState.Active && data.endWorldTime < 0d;
        public InteractionProviderAssignmentData ToSaveData() => data.Clone();
    }

    public sealed class InteractionReservationSnapshot
    {
        private readonly InteractionReservationData data;

        public InteractionReservationSnapshot(InteractionReservationData reservation)
        {
            data = reservation?.Clone() ?? new InteractionReservationData();
        }

        public string ReservationId => data.reservationId ?? string.Empty;
        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public string ServiceDefinitionId => data.serviceDefinitionId ?? string.Empty;
        public InteractionSubjectReferenceData ReservingSubject => data.reservingSubject?.Clone() ?? new InteractionSubjectReferenceData();
        public double StartWorldTime => data.startWorldTime;
        public double EndWorldTime => data.endWorldTime;
        public int Priority => data.priority;
        public InteractionReservationLifecycle LifecycleState => data.lifecycleState;
        public InteractionPointVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public bool IsActive => LifecycleState == InteractionReservationLifecycle.Active;
        public InteractionReservationData ToSaveData() => data.Clone();
    }

    public sealed class InteractionUseSessionSnapshot
    {
        private readonly InteractionUseSessionData data;

        public InteractionUseSessionSnapshot(InteractionUseSessionData session)
        {
            data = session?.Clone() ?? new InteractionUseSessionData();
        }

        public string SessionId => data.sessionId ?? string.Empty;
        public string InteractionPointId => data.interactionPointId ?? string.Empty;
        public string ServiceDefinitionId => data.serviceDefinitionId ?? string.Empty;
        public EntityLocationReferenceData ConsumerEntity => data.consumerEntity?.Clone();
        public EntityLocationReferenceData ProviderEntity => data.providerEntity?.Clone();
        public string ReservationId => data.reservationId ?? string.Empty;
        public double StartWorldTime => data.startWorldTime;
        public double ExpectedEndWorldTime => data.expectedEndWorldTime;
        public double EndWorldTime => data.endWorldTime;
        public InteractionUseSessionLifecycle LifecycleState => data.lifecycleState;
        public InteractionPointVisibility Visibility => data.visibility;
        public string EligibilityFingerprint => data.eligibilityFingerprint ?? string.Empty;
        public long Revision => data.revision;
        public bool IsActive => LifecycleState == InteractionUseSessionLifecycle.Active || LifecycleState == InteractionUseSessionLifecycle.Proposed || LifecycleState == InteractionUseSessionLifecycle.Suspended;
        public InteractionUseSessionData ToSaveData() => data.Clone();
    }

    public sealed class InteractionCapacityResult
    {
        public InteractionCapacityResult(int capacityLimit, int activeSessions, bool exclusiveBlocked, int availableSlots, string message)
        {
            CapacityLimit = capacityLimit;
            ActiveSessions = activeSessions;
            ExclusiveBlocked = exclusiveBlocked;
            AvailableSlots = availableSlots;
            Message = message ?? string.Empty;
        }

        public int CapacityLimit { get; }
        public int ActiveSessions { get; }
        public bool ExclusiveBlocked { get; }
        public int AvailableSlots { get; }
        public string Message { get; }
        public bool HasCapacity => CapacityLimit < 0 || AvailableSlots > 0;
    }

    public sealed class InteractionEligibilityResult
    {
        public InteractionEligibilityResult(
            InteractionPointOperationStatus status,
            string message,
            InteractionPointSnapshot point,
            string serviceDefinitionId,
            EntityLocationReferenceData consumer,
            EntityLocationReferenceData provider,
            string hostLocationId,
            InteractionCapacityResult capacity,
            IEnumerable<string> reasons,
            long pointRevision,
            long entityLocationRevision,
            long locationRevision)
        {
            Status = status;
            Message = message ?? string.Empty;
            Point = point;
            ServiceDefinitionId = serviceDefinitionId ?? string.Empty;
            Consumer = consumer?.Clone();
            Provider = provider?.Clone();
            HostLocationId = hostLocationId ?? string.Empty;
            Capacity = capacity;
            FailureReasons = (reasons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            PointRevision = pointRevision;
            EntityLocationRevision = entityLocationRevision;
            LocationRevision = locationRevision;
        }

        public InteractionPointOperationStatus Status { get; }
        public string Message { get; }
        public InteractionPointSnapshot Point { get; }
        public string ServiceDefinitionId { get; }
        public EntityLocationReferenceData Consumer { get; }
        public EntityLocationReferenceData Provider { get; }
        public string HostLocationId { get; }
        public InteractionCapacityResult Capacity { get; }
        public IReadOnlyList<string> FailureReasons { get; }
        public long PointRevision { get; }
        public long EntityLocationRevision { get; }
        public long LocationRevision { get; }
        public bool Eligible => Status == InteractionPointOperationStatus.Succeeded || Status == InteractionPointOperationStatus.Preview;
        public string Fingerprint => $"{Point?.InteractionPointId}:{ServiceDefinitionId}:{HostLocationId}:{PointRevision}:{EntityLocationRevision}:{LocationRevision}:{string.Join(",", FailureReasons)}";
    }

    public sealed class InteractionPointOperationResult
    {
        private InteractionPointOperationResult(InteractionPointOperationStatus status, string message, InteractionPointSnapshot point, InteractionHostAssignmentSnapshot hostAssignment, InteractionSubjectLinkSnapshot subjectLink, InteractionProviderAssignmentSnapshot providerAssignment, InteractionReservationSnapshot reservation, InteractionUseSessionSnapshot session, bool duplicate, bool preview, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Point = point;
            HostAssignment = hostAssignment;
            SubjectLink = subjectLink;
            ProviderAssignment = providerAssignment;
            Reservation = reservation;
            Session = session;
            Duplicate = duplicate;
            Preview = preview;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public InteractionPointOperationStatus Status { get; }
        public string Message { get; }
        public InteractionPointSnapshot Point { get; }
        public InteractionHostAssignmentSnapshot HostAssignment { get; }
        public InteractionSubjectLinkSnapshot SubjectLink { get; }
        public InteractionProviderAssignmentSnapshot ProviderAssignment { get; }
        public InteractionReservationSnapshot Reservation { get; }
        public InteractionUseSessionSnapshot Session { get; }
        public bool Succeeded => Status == InteractionPointOperationStatus.Succeeded || Status == InteractionPointOperationStatus.Preview || Status == InteractionPointOperationStatus.Duplicate;
        public bool Duplicate { get; }
        public bool Preview { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }

        public static InteractionPointOperationResult Success(InteractionPointSnapshot point, string message, long before, long after, InteractionHostAssignmentSnapshot host = null, InteractionSubjectLinkSnapshot link = null, InteractionProviderAssignmentSnapshot provider = null, InteractionReservationSnapshot reservation = null, InteractionUseSessionSnapshot session = null, bool duplicate = false, bool preview = false)
        {
            return new InteractionPointOperationResult(preview ? InteractionPointOperationStatus.Preview : duplicate ? InteractionPointOperationStatus.Duplicate : InteractionPointOperationStatus.Succeeded, message, point, host, link, provider, reservation, session, duplicate, preview, before, after);
        }

        public static InteractionPointOperationResult Failure(InteractionPointOperationStatus status, string message, long before)
        {
            return new InteractionPointOperationResult(status, message, null, null, null, null, null, null, false, false, before, before);
        }
    }

    public sealed class InteractionPointCreateRequest
    {
        public string transactionId;
        public string interactionPointId;
        public string interactionPointDefinitionId;
        public string displayName;
        public string hostLocationId;
        public string hostAssignmentId;
        public IEnumerable<string> serviceDefinitionIds;
        public int capacityOverride;
        public bool exclusiveUseOverride;
        public bool hasExclusiveUseOverride;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sceneBindingKey;
        public InteractionSceneBindingCategory sceneBindingCategory;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionPointHostReassignmentRequest
    {
        public string transactionId;
        public string interactionPointId;
        public string newHostAssignmentId;
        public string newHostLocationId;
        public string assignmentCategory;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionPointLifecycleRequest
    {
        public string transactionId;
        public string interactionPointId;
        public InteractionPointLifecycleState targetState;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionServiceBindingRequest
    {
        public string transactionId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionSubjectLinkRequest
    {
        public string transactionId;
        public string linkId;
        public string interactionPointId;
        public InteractionSubjectLinkRole role;
        public InteractionSubjectReferenceData subject;
        public double worldTime;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionProviderAssignmentRequest
    {
        public string transactionId;
        public string assignmentId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public InteractionProviderRequirementKind requirementKind = InteractionProviderRequirementKind.AssignedPerson;
        public EntityLocationReferenceData providerEntity;
        public string providerOfficeId;
        public string providerOrganizationId;
        public InteractionPhysicalPresencePolicy presencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation;
        public double worldTime;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionReservationRequest
    {
        public string transactionId;
        public string reservationId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public InteractionSubjectReferenceData reservingSubject;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public int priority;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionEligibilityRequest
    {
        public string interactionPointId;
        public string serviceDefinitionId;
        public EntityLocationReferenceData consumerEntity;
        public EntityLocationReferenceData providerEntity;
        public double worldTime;
        public bool privilegedVisibility;
        public bool requireDestinationRuntimeReady = true;
        public bool physicalPresenceVerified;
    }

    public sealed class InteractionSessionStartRequest
    {
        public string transactionId;
        public string sessionId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public EntityLocationReferenceData consumerEntity;
        public EntityLocationReferenceData providerEntity;
        public string reservationId;
        public double startWorldTime;
        public double expectedEndWorldTime = -1d;
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
        public bool physicalPresenceVerified;
    }

    public sealed class InteractionSessionTransitionRequest
    {
        public string transactionId;
        public string sessionId;
        public InteractionUseSessionLifecycle targetState;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class InteractionRequest
    {
        public string transactionId;
        public string interactionPointId;
        public string serviceDefinitionId;
        public EntityLocationReferenceData consumerEntity;
        public EntityLocationReferenceData providerEntity;
        public InteractionSubjectReferenceData targetSubject;
        public string worldId = PersistenceService.LocalWorldId;
        public double worldTime;
        public string sourceContextId;
        public string[] parameterIds = Array.Empty<string>();
        public InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        public long expectedRevision = -1L;
        public string provenanceId;
        public bool preview;
        public bool physicalPresenceVerified;
    }

    public sealed class InteractionInvocationResult
    {
        public InteractionInvocationResult(string requestId, InteractionEligibilityResult eligibility, InteractionDestinationRuntime destinationRuntime, string destinationOperationReferenceId, bool success, string message, long before, long after)
        {
            RequestId = requestId ?? string.Empty;
            Eligibility = eligibility;
            DestinationRuntime = destinationRuntime;
            DestinationOperationReferenceId = destinationOperationReferenceId ?? string.Empty;
            Success = success;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public string RequestId { get; }
        public InteractionEligibilityResult Eligibility { get; }
        public InteractionDestinationRuntime DestinationRuntime { get; }
        public string DestinationOperationReferenceId { get; }
        public bool Success { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
    }
}
