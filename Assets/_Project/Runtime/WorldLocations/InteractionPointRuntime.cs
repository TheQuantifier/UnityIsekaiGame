using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class InteractionPointRuntime : IDisposable
    {
        private readonly Dictionary<string, InteractionPointRecordData> pointsById = new Dictionary<string, InteractionPointRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionPointHostAssignmentData> hostAssignmentsById = new Dictionary<string, InteractionPointHostAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionSubjectLinkData> subjectLinksById = new Dictionary<string, InteractionSubjectLinkData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionProviderAssignmentData> providerAssignmentsById = new Dictionary<string, InteractionProviderAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionReservationData> reservationsById = new Dictionary<string, InteractionReservationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionUseSessionData> sessionsById = new Dictionary<string, InteractionUseSessionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InteractionPointTransactionRecordData> transactionsById = new Dictionary<string, InteractionPointTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> pointIdsByHostLocationId = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> pointIdsByServiceDefinitionId = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> linkIdsByPointId = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> providerIdsByPointId = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> reservationIdsByPointId = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> sessionIdsByPointId = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private LocationRuntime locations;
        private EntityLocationRuntime entityLocations;
        private IInteractionRequirementResolver requirementResolver;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public event Action<InteractionPointOperationResult> OperationCommitted;
        public event Action<InteractionInvocationResult> InvocationCompleted;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => registry != null && locations != null && !string.IsNullOrWhiteSpace(worldId) && !disposed;
        public bool IsDisposed => disposed;
        public string WorldId => worldId;
        public int PointCount => pointsById.Count;
        public int HostAssignmentCount => hostAssignmentsById.Count;
        public int SubjectLinkCount => subjectLinksById.Count;
        public int ProviderAssignmentCount => providerAssignmentsById.Count;
        public int ReservationCount => reservationsById.Count;
        public int SessionCount => sessionsById.Count;
        public IReadOnlyList<InteractionPointSnapshot> Points => pointsById.Values.OrderBy(item => item.interactionPointId, StringComparer.Ordinal).Select(BuildPointSnapshot).ToArray();
        public IReadOnlyList<InteractionHostAssignmentSnapshot> HostAssignments => hostAssignmentsById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(BuildHostSnapshot).ToArray();
        public IReadOnlyList<InteractionSubjectLinkSnapshot> SubjectLinks => subjectLinksById.Values.OrderBy(item => item.linkId, StringComparer.Ordinal).Select(BuildLinkSnapshot).ToArray();
        public IReadOnlyList<InteractionProviderAssignmentSnapshot> ProviderAssignments => providerAssignmentsById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(BuildProviderSnapshot).ToArray();
        public IReadOnlyList<InteractionReservationSnapshot> Reservations => reservationsById.Values.OrderBy(item => item.reservationId, StringComparer.Ordinal).Select(BuildReservationSnapshot).ToArray();
        public IReadOnlyList<InteractionUseSessionSnapshot> UseSessions => sessionsById.Values.OrderBy(item => item.sessionId, StringComparer.Ordinal).Select(BuildSessionSnapshot).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, LocationRuntime locationRuntime, EntityLocationRuntime entityLocationRuntime, string runtimeWorldId = PersistenceService.LocalWorldId, IInteractionRequirementResolver declarativeRequirementResolver = null)
        {
            registry = definitionRegistry ?? registry;
            locations = locationRuntime ?? locations;
            entityLocations = entityLocationRuntime ?? entityLocations;
            requirementResolver = declarativeRequirementResolver ?? requirementResolver;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? worldId : runtimeWorldId.Trim();
            disposed = false;
        }

        public InteractionPointOperationResult CreatePoint(InteractionPointCreateRequest request)
        {
            request ??= new InteractionPointCreateRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;

            string pointId = N(request.interactionPointId);
            string tx = N(request.transactionId);
            if (TryDuplicate(tx, pointId, "point.create", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(pointId) || string.IsNullOrWhiteSpace(request.interactionPointDefinitionId))
            {
                return Fail(InteractionPointOperationStatus.InvalidRequest, "Interaction point ID and definition ID are required.", before);
            }

            if (pointsById.TryGetValue(pointId, out InteractionPointRecordData existing))
            {
                if (string.Equals(existing.interactionPointDefinitionId, N(request.interactionPointDefinitionId), StringComparison.Ordinal))
                {
                    return InteractionPointOperationResult.Success(BuildPointSnapshot(existing), "Interaction point already exists.", before, before, duplicate: true);
                }

                return Fail(InteractionPointOperationStatus.InvalidRequest, $"Interaction point '{pointId}' already exists with a different definition.", before);
            }

            if (!TryGetPointDefinition(request.interactionPointDefinitionId, before, out InteractionPointDefinition definition, out InteractionPointOperationResult failure)) return failure;
            if (!ValidateHost(request.hostLocationId, definition, before, out LocationSnapshot host, out failure)) return failure;
            string[] services = Clean(request.serviceDefinitionIds);
            foreach (string serviceId in services)
            {
                if (!ValidateServiceForPoint(definition, serviceId, before, out _, out failure)) return failure;
            }

            int capacity = request.capacityOverride == 0 ? definition.SimultaneousUserCapacity : request.capacityOverride;
            if (capacity == 0 || capacity < -1)
            {
                return Fail(InteractionPointOperationStatus.InvalidRequest, $"Invalid capacity '{capacity}'.", before);
            }

            InteractionPointRecordData record = new InteractionPointRecordData
            {
                interactionPointId = pointId,
                interactionPointDefinitionId = N(definition.Id),
                worldId = worldId,
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                lifecycleState = InteractionPointLifecycleState.Active,
                activeHostAssignmentId = string.IsNullOrWhiteSpace(request.hostAssignmentId) ? BuildHostAssignmentId(pointId, host.LocationId, hostAssignmentsById.Count + 1) : N(request.hostAssignmentId),
                activeHostLocationId = host.LocationId,
                serviceDefinitionIds = services,
                capacityOverride = request.capacityOverride,
                exclusiveUseOverride = request.exclusiveUseOverride,
                hasExclusiveUseOverride = request.hasExclusiveUseOverride,
                useState = InteractionPointUseState.Free,
                visibility = request.visibility,
                sceneBindingKey = N(request.sceneBindingKey),
                sceneBindingCategory = request.sceneBindingCategory,
                createdWorldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            InteractionPointHostAssignmentData assignment = CreateHostAssignment(record.activeHostAssignmentId, pointId, host.LocationId, worldId, request.worldTime, "initial", request.visibility, request.sourceEventId, request.sourceRecordId, request.provenanceId);

            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(record), "Interaction point create preview.", before, before, BuildHostSnapshot(assignment), preview: true);
            }

            pointsById.Add(record.interactionPointId, record);
            hostAssignmentsById.Add(assignment.assignmentId, assignment);
            RebuildIndexes();
            Complete(tx, "point.create", pointId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(record), "Interaction point created.", before, Revision, BuildHostSnapshot(assignment)));
        }

        public InteractionPointOperationResult ReassignHost(InteractionPointHostReassignmentRequest request)
        {
            request ??= new InteractionPointHostReassignmentRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string pointId = N(request.interactionPointId);
            if (TryDuplicate(N(request.transactionId), pointId, "host.reassign", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Fail(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", before);
            if (!TryGetPointDefinition(point.interactionPointDefinitionId, before, out InteractionPointDefinition definition, out InteractionPointOperationResult failure)) return failure;
            if (!ValidateHost(request.newHostLocationId, definition, before, out LocationSnapshot host, out failure)) return failure;
            if (string.Equals(point.activeHostLocationId, host.LocationId, StringComparison.Ordinal))
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Interaction point already has requested host.", before, before, duplicate: true);
            }

            InteractionPointRuntimeSaveData rollback = CreateSaveData();
            InteractionPointRecordData changed = point.Clone();
            InteractionPointHostAssignmentData previous = !string.IsNullOrWhiteSpace(changed.activeHostAssignmentId) && hostAssignmentsById.TryGetValue(changed.activeHostAssignmentId, out InteractionPointHostAssignmentData found) ? found.Clone() : null;
            InteractionPointHostAssignmentData next = CreateHostAssignment(string.IsNullOrWhiteSpace(request.newHostAssignmentId) ? BuildHostAssignmentId(pointId, host.LocationId, hostAssignmentsById.Count + 1) : N(request.newHostAssignmentId), pointId, host.LocationId, worldId, request.worldTime, request.assignmentCategory, point.visibility, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            changed.activeHostAssignmentId = next.assignmentId;
            changed.activeHostLocationId = next.hostLocationId;
            changed.revision++;

            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(changed), "Interaction point host reassignment preview.", before, before, BuildHostSnapshot(next), preview: true);
            }

            if (previous != null)
            {
                previous.endWorldTime = request.worldTime;
                previous.revision++;
                hostAssignmentsById[previous.assignmentId] = previous;
            }

            pointsById[pointId] = changed;
            hostAssignmentsById[next.assignmentId] = next;
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(InteractionPointOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            RebuildIndexes();
            Complete(N(request.transactionId), "host.reassign", pointId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(changed), "Interaction point host reassigned.", before, Revision, BuildHostSnapshot(next)));
        }

        public InteractionPointOperationResult TransitionLifecycle(InteractionPointLifecycleRequest request)
        {
            request ??= new InteractionPointLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string pointId = N(request.interactionPointId);
            if (TryDuplicate(N(request.transactionId), pointId, "point.lifecycle", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Fail(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", before);
            if (!CanTransition(point.lifecycleState, request.targetState)) return Fail(InteractionPointOperationStatus.InvalidLifecycleTransition, $"Cannot transition interaction point from '{point.lifecycleState}' to '{request.targetState}'.", before);
            if (point.lifecycleState == request.targetState)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Interaction point lifecycle already matches requested state.", before, before, duplicate: true);
            }

            InteractionPointRecordData changed = point.Clone();
            changed.lifecycleState = request.targetState;
            if (IsEndedState(request.targetState))
            {
                changed.endedWorldTime = request.worldTime;
            }

            changed.sourceEventId = First(request.sourceEventId, changed.sourceEventId);
            changed.sourceRecordId = First(request.sourceRecordId, changed.sourceRecordId);
            changed.provenanceId = First(request.provenanceId, changed.provenanceId);
            changed.revision++;
            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(changed), "Interaction point lifecycle preview.", before, before, preview: true);
            }

            pointsById[pointId] = changed;
            Complete(N(request.transactionId), "point.lifecycle", pointId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(changed), "Interaction point lifecycle changed.", before, Revision));
        }

        public InteractionPointOperationResult BindService(InteractionServiceBindingRequest request)
        {
            request ??= new InteractionServiceBindingRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string pointId = N(request.interactionPointId);
            string serviceId = N(request.serviceDefinitionId);
            string target = $"{pointId}:{serviceId}";
            if (TryDuplicate(N(request.transactionId), target, "service.bind", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Fail(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", before);
            if (!TryGetPointDefinition(point.interactionPointDefinitionId, before, out InteractionPointDefinition pointDefinition, out InteractionPointOperationResult failure)) return failure;
            if (!ValidateServiceForPoint(pointDefinition, serviceId, before, out _, out failure)) return failure;
            if ((point.serviceDefinitionIds ?? Array.Empty<string>()).Contains(serviceId, StringComparer.Ordinal))
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Interaction service is already bound.", before, before, duplicate: true);
            }

            InteractionPointRecordData changed = point.Clone();
            changed.serviceDefinitionIds = Clean((changed.serviceDefinitionIds ?? Array.Empty<string>()).Concat(new[] { serviceId }));
            changed.revision++;
            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(changed), "Interaction service binding preview.", before, before, preview: true);
            }

            pointsById[pointId] = changed;
            RebuildIndexes();
            Complete(N(request.transactionId), "service.bind", target);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(changed), "Interaction service bound.", before, Revision));
        }

        public InteractionPointOperationResult AddSubjectLink(InteractionSubjectLinkRequest request)
        {
            request ??= new InteractionSubjectLinkRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string pointId = N(request.interactionPointId);
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Fail(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", before);
            if (!TryGetPointDefinition(point.interactionPointDefinitionId, before, out InteractionPointDefinition definition, out InteractionPointOperationResult failure)) return failure;
            if (request.role == InteractionSubjectLinkRole.Unknown || !definition.SupportsSubjectLinkRole(request.role)) return Fail(InteractionPointOperationStatus.InvalidSubjectLink, $"Subject-link role '{request.role}' is not supported by '{definition.Id}'.", before);
            InteractionSubjectReferenceData subject = request.subject?.Clone();
            if (subject == null || string.IsNullOrWhiteSpace(subject.subjectType) || string.IsNullOrWhiteSpace(subject.subjectId)) return Fail(InteractionPointOperationStatus.InvalidSubjectLink, "Subject link requires a concrete typed subject.", before);
            if (!WorldMatches(subject.worldId)) return Fail(InteractionPointOperationStatus.WrongWorld, $"Subject link world '{subject.worldId}' does not match '{worldId}'.", before);
            string linkId = string.IsNullOrWhiteSpace(request.linkId) ? BuildSubjectLinkId(pointId, request.role, subject.subjectId, subjectLinksById.Count + 1) : N(request.linkId);
            if (TryDuplicate(N(request.transactionId), linkId, "subject.link", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (subjectLinksById.TryGetValue(linkId, out InteractionSubjectLinkData existing))
            {
                if (existing.interactionPointId == pointId && existing.role == request.role && existing.subject.StableKey == subject.StableKey)
                {
                    return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Subject link already exists.", before, before, link: BuildLinkSnapshot(existing), duplicate: true);
                }

                return Fail(InteractionPointOperationStatus.InvalidSubjectLink, $"Subject link '{linkId}' already exists with different data.", before);
            }

            InteractionSubjectLinkData link = new InteractionSubjectLinkData
            {
                linkId = linkId,
                interactionPointId = pointId,
                role = request.role,
                subject = subject,
                startWorldTime = request.worldTime,
                visibility = request.visibility,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Subject link preview.", before, before, link: BuildLinkSnapshot(link), preview: true);
            }

            subjectLinksById[linkId] = link;
            point.revision++;
            RebuildIndexes();
            Complete(N(request.transactionId), "subject.link", linkId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Subject link added.", before, Revision, link: BuildLinkSnapshot(link)));
        }

        public InteractionPointOperationResult AssignProvider(InteractionProviderAssignmentRequest request)
        {
            request ??= new InteractionProviderAssignmentRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string pointId = N(request.interactionPointId);
            string serviceId = N(request.serviceDefinitionId);
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Fail(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", before);
            if (!TryGetServiceDefinition(serviceId, before, out InteractionServiceDefinition service, out InteractionPointOperationResult failure)) return failure;
            if (!(point.serviceDefinitionIds ?? Array.Empty<string>()).Contains(serviceId, StringComparer.Ordinal)) return Fail(InteractionPointOperationStatus.InvalidServiceBinding, $"Service '{serviceId}' is not bound to point '{pointId}'.", before);
            EntityLocationReferenceData provider = request.providerEntity?.Clone();
            if (request.requirementKind == InteractionProviderRequirementKind.AssignedPerson && (provider == null || string.IsNullOrWhiteSpace(provider.entityId) || provider.entityType != service.RequiredProviderType)) return Fail(InteractionPointOperationStatus.InvalidProvider, "Assigned provider must match the service provider entity type.", before);
            if (provider != null && !WorldMatches(provider.worldId)) return Fail(InteractionPointOperationStatus.WrongWorld, $"Provider world '{provider.worldId}' does not match '{worldId}'.", before);
            string assignmentId = string.IsNullOrWhiteSpace(request.assignmentId) ? BuildProviderAssignmentId(pointId, serviceId, provider?.entityId, providerAssignmentsById.Count + 1) : N(request.assignmentId);
            if (TryDuplicate(N(request.transactionId), assignmentId, "provider.assign", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (providerAssignmentsById.TryGetValue(assignmentId, out InteractionProviderAssignmentData existing))
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Provider assignment already exists.", before, before, provider: BuildProviderSnapshot(existing), duplicate: true);
            }

            InteractionProviderAssignmentData assignment = new InteractionProviderAssignmentData
            {
                assignmentId = assignmentId,
                interactionPointId = pointId,
                serviceDefinitionId = serviceId,
                requirementKind = request.requirementKind == InteractionProviderRequirementKind.Unknown ? service.ProviderRequirement : request.requirementKind,
                providerEntity = provider,
                providerOfficeId = N(request.providerOfficeId),
                providerOrganizationId = N(request.providerOrganizationId),
                presencePolicy = request.presencePolicy == InteractionPhysicalPresencePolicy.Unknown ? service.ProviderPresencePolicy : request.presencePolicy,
                startWorldTime = request.worldTime,
                lifecycleState = InteractionPointLifecycleState.Active,
                visibility = request.visibility,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Provider assignment preview.", before, before, provider: BuildProviderSnapshot(assignment), preview: true);
            }

            providerAssignmentsById[assignmentId] = assignment;
            point.revision++;
            RebuildIndexes();
            Complete(N(request.transactionId), "provider.assign", assignmentId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Provider assigned.", before, Revision, provider: BuildProviderSnapshot(assignment)));
        }

        public InteractionPointOperationResult Reserve(InteractionReservationRequest request)
        {
            request ??= new InteractionReservationRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string pointId = N(request.interactionPointId);
            string serviceId = N(request.serviceDefinitionId);
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Fail(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", before);
            if (!TryGetPointDefinition(point.interactionPointDefinitionId, before, out InteractionPointDefinition definition, out InteractionPointOperationResult failure)) return failure;
            if (!definition.SupportsReservation) return Fail(InteractionPointOperationStatus.InvalidRequest, $"Interaction point definition '{definition.Id}' does not support reservations.", before);
            if (!TryGetServiceDefinition(serviceId, before, out _, out failure)) return failure;
            if (!(point.serviceDefinitionIds ?? Array.Empty<string>()).Contains(serviceId, StringComparer.Ordinal)) return Fail(InteractionPointOperationStatus.InvalidServiceBinding, $"Service '{serviceId}' is not bound to point '{pointId}'.", before);
            if (request.endWorldTime >= 0d && request.endWorldTime < request.startWorldTime) return Fail(InteractionPointOperationStatus.InvalidRequest, "Reservation cannot end before it starts.", before);
            if (HasActiveReservationConflict(pointId, serviceId, request.startWorldTime, request.endWorldTime)) return Fail(InteractionPointOperationStatus.ReservationConflict, "Active reservation conflicts with requested reservation window.", before);

            string reservationId = string.IsNullOrWhiteSpace(request.reservationId) ? BuildReservationId(pointId, serviceId, reservationsById.Count + 1) : N(request.reservationId);
            if (TryDuplicate(N(request.transactionId), reservationId, "reservation.create", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (reservationsById.TryGetValue(reservationId, out InteractionReservationData existing))
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Reservation already exists.", before, before, reservation: BuildReservationSnapshot(existing), duplicate: true);
            }

            InteractionReservationData reservation = new InteractionReservationData
            {
                reservationId = reservationId,
                interactionPointId = pointId,
                serviceDefinitionId = serviceId,
                reservingSubject = request.reservingSubject?.Clone() ?? new InteractionSubjectReferenceData(),
                startWorldTime = request.startWorldTime,
                endWorldTime = request.endWorldTime,
                priority = request.priority,
                lifecycleState = InteractionReservationLifecycle.Active,
                visibility = request.visibility,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview)
            {
                return InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Reservation preview.", before, before, reservation: BuildReservationSnapshot(reservation), preview: true);
            }

            reservationsById[reservationId] = reservation;
            RebuildIndexes();
            Complete(N(request.transactionId), "reservation.create", reservationId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(point), "Reservation created.", before, Revision, reservation: BuildReservationSnapshot(reservation)));
        }

        public InteractionEligibilityResult EvaluateEligibility(InteractionEligibilityRequest request)
        {
            request ??= new InteractionEligibilityRequest();
            long pointRevision = Revision;
            long locationRevision = locations?.Revision ?? 0L;
            long entityRevision = entityLocations?.Revision ?? 0L;
            List<string> reasons = new List<string>();
            string pointId = N(request.interactionPointId);
            string serviceId = N(request.serviceDefinitionId);
            if (disposed) return Eligibility(InteractionPointOperationStatus.Disposed, "Interaction point runtime is disposed.", null, serviceId, request.consumerEntity, request.providerEntity, string.Empty, null, reasons.Append("runtime.disposed"), pointRevision, entityRevision, locationRevision);
            if (!pointsById.TryGetValue(pointId, out InteractionPointRecordData point)) return Eligibility(InteractionPointOperationStatus.MissingPoint, $"Interaction point '{pointId}' is missing.", null, serviceId, request.consumerEntity, request.providerEntity, string.Empty, null, reasons.Append("point.missing"), pointRevision, entityRevision, locationRevision);
            InteractionPointSnapshot snapshot = BuildPointSnapshot(point);
            if (!IsVisibleToRequest(point.visibility, request.privilegedVisibility)) return Eligibility(InteractionPointOperationStatus.VisibilityDenied, "Interaction point is not visible to this requester.", snapshot, serviceId, request.consumerEntity, request.providerEntity, string.Empty, null, reasons.Append("visibility.denied"), pointRevision, entityRevision, locationRevision);
            if (!IsUsableLifecycle(point.lifecycleState)) reasons.Add($"point.lifecycle.{point.lifecycleState}");
            if (!TryGetPointDefinition(point.interactionPointDefinitionId, pointRevision, out InteractionPointDefinition pointDefinition, out _)) reasons.Add("point.definition.missing");
            if (!TryGetServiceDefinition(serviceId, pointRevision, out InteractionServiceDefinition service, out _)) reasons.Add("service.definition.missing");
            if (service != null && !(point.serviceDefinitionIds ?? Array.Empty<string>()).Contains(serviceId, StringComparer.Ordinal)) reasons.Add("service.not-bound");
            if (locations == null || !locations.TryGetSnapshot(point.activeHostLocationId, out LocationSnapshot host)) reasons.Add("host.missing");
            else if (!IsHostUsable(host.LifecycleState)) reasons.Add($"host.lifecycle.{host.LifecycleState}");
            if (request.requireDestinationRuntimeReady && service != null && service.DestinationRuntime == InteractionDestinationRuntime.Unknown) reasons.Add("destination-runtime.unknown");
            InteractionCapacityResult capacity = EvaluateCapacity(point, pointDefinition);
            if (!capacity.HasCapacity) reasons.Add("capacity.full");

            EntityLocationReferenceData consumer = request.consumerEntity?.Clone();
            if (service != null && service.RequiredConsumerType != LocationOccupantEntityType.Unknown && consumer != null && consumer.entityType != service.RequiredConsumerType) reasons.Add("consumer.type-mismatch");
            if (!request.physicalPresenceVerified && service != null && service.ConsumerPresencePolicy != InteractionPhysicalPresencePolicy.NotRequired && service.ConsumerPresencePolicy != InteractionPhysicalPresencePolicy.RemoteAllowed)
            {
                if (!EvaluatePresence(consumer, point.activeHostLocationId, service.ConsumerPresencePolicy, out string consumerFailure)) reasons.Add($"consumer.{consumerFailure}");
            }

            EntityLocationReferenceData provider = request.providerEntity?.Clone();
            InteractionProviderAssignmentSnapshot assignedProvider = FindProvider(pointId, serviceId);
            if (provider == null)
            {
                provider = assignedProvider?.ProviderEntity;
            }

            bool providerRequired = service != null && service.ProviderRequirement != InteractionProviderRequirementKind.NoProvider && service.ProviderRequirement != InteractionProviderRequirementKind.AutomatedService;
            if (providerRequired)
            {
                if (provider == null || string.IsNullOrWhiteSpace(provider.entityId)) reasons.Add("provider.missing");
                else if (service.RequiredProviderType != LocationOccupantEntityType.Unknown && provider.entityType != service.RequiredProviderType) reasons.Add("provider.type-mismatch");
                else
                {
                    InteractionPhysicalPresencePolicy policy = assignedProvider?.PresencePolicy == InteractionPhysicalPresencePolicy.Unknown || assignedProvider == null ? service.ProviderPresencePolicy : assignedProvider.PresencePolicy;
                    if (!EvaluatePresence(provider, point.activeHostLocationId, policy, out string providerFailure)) reasons.Add($"provider.{providerFailure}");
                }
            }

            if (service != null && service.HasDeclarativeRequirements)
            {
                if (requirementResolver == null)
                {
                    reasons.Add("requirements.resolver-unavailable");
                }
                else
                {
                    InteractionRequirementResolution resolution = requirementResolver.Evaluate(new InteractionRequirementContext
                    {
                        Point = snapshot,
                        Service = service,
                        Consumer = consumer,
                        Provider = provider,
                        WorldTime = request.worldTime
                    });
                    if (resolution == null)
                    {
                        reasons.Add("requirements.resolver-invalid");
                    }
                    else
                    {
                        reasons.AddRange(resolution.UnmetRequirementIds.Select(value => $"requirement.{value}"));
                    }
                }
            }
            InteractionPointOperationStatus status = reasons.Count == 0 ? InteractionPointOperationStatus.Succeeded : ClassifyEligibilityFailure(reasons);
            string message = reasons.Count == 0 ? "Interaction service is eligible." : $"Interaction service is not eligible: {string.Join(", ", reasons.OrderBy(value => value, StringComparer.Ordinal))}.";
            return Eligibility(status, message, snapshot, serviceId, consumer, provider, point.activeHostLocationId, capacity, reasons, pointRevision, entityRevision, locationRevision);
        }

        public InteractionPointOperationResult StartSession(InteractionSessionStartRequest request)
        {
            request ??= new InteractionSessionStartRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            InteractionEligibilityResult eligibility = EvaluateEligibility(new InteractionEligibilityRequest
            {
                interactionPointId = request.interactionPointId,
                serviceDefinitionId = request.serviceDefinitionId,
                consumerEntity = request.consumerEntity,
                providerEntity = request.providerEntity,
                worldTime = request.startWorldTime
            });
            if (!eligibility.Eligible) return Fail(eligibility.Status, eligibility.Message, before);
            string sessionId = string.IsNullOrWhiteSpace(request.sessionId) ? BuildSessionId(request.interactionPointId, request.serviceDefinitionId, sessionsById.Count + 1) : N(request.sessionId);
            if (TryDuplicate(N(request.transactionId), sessionId, "session.start", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (sessionsById.TryGetValue(sessionId, out InteractionUseSessionData existing)) return InteractionPointOperationResult.Success(eligibility.Point, "Session already exists.", before, before, session: BuildSessionSnapshot(existing), duplicate: true);
            InteractionUseSessionData session = new InteractionUseSessionData
            {
                sessionId = sessionId,
                interactionPointId = N(request.interactionPointId),
                serviceDefinitionId = N(request.serviceDefinitionId),
                consumerEntity = request.consumerEntity?.Clone(),
                providerEntity = eligibility.Provider?.Clone(),
                reservationId = N(request.reservationId),
                startWorldTime = request.startWorldTime,
                expectedEndWorldTime = request.expectedEndWorldTime,
                lifecycleState = InteractionUseSessionLifecycle.Active,
                visibility = request.visibility,
                eligibilityFingerprint = eligibility.Fingerprint,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview) return InteractionPointOperationResult.Success(eligibility.Point, "Session start preview.", before, before, session: BuildSessionSnapshot(session), preview: true);
            sessionsById[sessionId] = session;
            if (pointsById.TryGetValue(session.interactionPointId, out InteractionPointRecordData point))
            {
                point.useState = EvaluateCapacity(point, null).HasCapacity ? InteractionPointUseState.InUse : InteractionPointUseState.Full;
                point.revision++;
            }

            RebuildIndexes();
            Complete(N(request.transactionId), "session.start", sessionId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(BuildPointSnapshot(pointsById[session.interactionPointId]), "Session started.", before, Revision, session: BuildSessionSnapshot(session)));
        }

        public InteractionPointOperationResult TransitionSession(InteractionSessionTransitionRequest request)
        {
            request ??= new InteractionSessionTransitionRequest();
            long before = Revision;
            if (!Ready(before, out InteractionPointOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out InteractionPointOperationResult revisionFailure)) return revisionFailure;
            string sessionId = N(request.sessionId);
            if (TryDuplicate(N(request.transactionId), sessionId, "session.transition", before, out InteractionPointOperationResult duplicate)) return duplicate;
            if (!sessionsById.TryGetValue(sessionId, out InteractionUseSessionData session)) return Fail(InteractionPointOperationStatus.InvalidRequest, $"Session '{sessionId}' is missing.", before);
            if (!CanTransitionSession(session.lifecycleState, request.targetState)) return Fail(InteractionPointOperationStatus.InvalidLifecycleTransition, $"Cannot transition session from '{session.lifecycleState}' to '{request.targetState}'.", before);
            InteractionUseSessionData changed = session.Clone();
            changed.lifecycleState = request.targetState;
            if (IsEndedSessionState(request.targetState)) changed.endWorldTime = request.worldTime;
            changed.sourceEventId = First(request.sourceEventId, changed.sourceEventId);
            changed.sourceRecordId = First(request.sourceRecordId, changed.sourceRecordId);
            changed.provenanceId = First(request.provenanceId, changed.provenanceId);
            changed.revision++;
            if (request.preview) return InteractionPointOperationResult.Success(pointsById.TryGetValue(changed.interactionPointId, out InteractionPointRecordData point) ? BuildPointSnapshot(point) : null, "Session transition preview.", before, before, session: BuildSessionSnapshot(changed), preview: true);
            sessionsById[sessionId] = changed;
            RebuildIndexes();
            Complete(N(request.transactionId), "session.transition", sessionId);
            Touch();
            return Commit(InteractionPointOperationResult.Success(pointsById.TryGetValue(changed.interactionPointId, out InteractionPointRecordData foundPoint) ? BuildPointSnapshot(foundPoint) : null, "Session transitioned.", before, Revision, session: BuildSessionSnapshot(changed)));
        }

        public InteractionInvocationResult Invoke(InteractionRequest request)
        {
            request ??= new InteractionRequest();
            long before = Revision;
            InteractionEligibilityResult eligibility = EvaluateEligibility(new InteractionEligibilityRequest
            {
                interactionPointId = request.interactionPointId,
                serviceDefinitionId = request.serviceDefinitionId,
                consumerEntity = request.consumerEntity,
                providerEntity = request.providerEntity,
                worldTime = request.worldTime,
                physicalPresenceVerified = request.physicalPresenceVerified
            });
            if (!eligibility.Eligible)
            {
                return new InteractionInvocationResult(request.transactionId, eligibility, InteractionDestinationRuntime.Unknown, string.Empty, false, eligibility.Message, before, before);
            }

            TryGetServiceDefinition(request.serviceDefinitionId, before, out InteractionServiceDefinition service, out _);
            InteractionInvocationResult result = new InteractionInvocationResult(request.transactionId, eligibility, service?.DestinationRuntime ?? InteractionDestinationRuntime.Unknown, $"{N(request.transactionId)}:{N(request.serviceDefinitionId)}", true, request.preview ? "Interaction service previewed; destination runtime was not mutated." : "Interaction service context validated; destination runtime remains authoritative for mutation.", before, before);
            if (!request.preview)
            {
                InvocationCompleted?.Invoke(result);
            }

            return result;
        }

        public bool TryGetPoint(string pointId, out InteractionPointSnapshot snapshot)
        {
            if (pointsById.TryGetValue(N(pointId), out InteractionPointRecordData point))
            {
                snapshot = BuildPointSnapshot(point);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<InteractionPointSnapshot> GetPointsByHost(string hostLocationId, bool includeHidden = false)
        {
            return GetIds(pointIdsByHostLocationId, N(hostLocationId))
                .Select(id => pointsById.TryGetValue(id, out InteractionPointRecordData point) ? point : null)
                .Where(point => point != null && (includeHidden || IsVisibleToRequest(point.visibility, false)))
                .OrderBy(point => point.interactionPointId, StringComparer.Ordinal)
                .Select(BuildPointSnapshot)
                .ToArray();
        }

        public IReadOnlyList<InteractionPointSnapshot> GetPointsWithinLocationSubtree(string locationId, bool includeHidden = false)
        {
            HashSet<string> locationsInScope = new HashSet<string>(locations?.GetDescendants(N(locationId), includeHidden: true).Select(item => item.LocationId) ?? Array.Empty<string>(), StringComparer.Ordinal);
            locationsInScope.Add(N(locationId));
            return pointsById.Values
                .Where(point => locationsInScope.Contains(point.activeHostLocationId) && (includeHidden || IsVisibleToRequest(point.visibility, false)))
                .OrderBy(point => point.interactionPointId, StringComparer.Ordinal)
                .Select(BuildPointSnapshot)
                .ToArray();
        }

        public IReadOnlyList<InteractionPointSnapshot> GetPointsByDefinition(string definitionId) => pointsById.Values.Where(point => point.interactionPointDefinitionId == N(definitionId)).OrderBy(point => point.interactionPointId, StringComparer.Ordinal).Select(BuildPointSnapshot).ToArray();
        public IReadOnlyList<InteractionPointSnapshot> GetPointsByService(string serviceDefinitionId) => GetIds(pointIdsByServiceDefinitionId, N(serviceDefinitionId)).Select(id => pointsById.TryGetValue(id, out InteractionPointRecordData point) ? BuildPointSnapshot(point) : null).Where(item => item != null).ToArray();
        public IReadOnlyList<InteractionSubjectLinkSnapshot> GetSubjectLinks(string pointId, bool includeHidden = false) => GetIds(linkIdsByPointId, N(pointId)).Select(id => subjectLinksById.TryGetValue(id, out InteractionSubjectLinkData link) ? link : null).Where(link => link != null && (includeHidden || IsVisibleToRequest(link.visibility, false))).OrderBy(link => link.role).ThenBy(link => link.subject.StableKey, StringComparer.Ordinal).ThenBy(link => link.linkId, StringComparer.Ordinal).Select(BuildLinkSnapshot).ToArray();
        public IReadOnlyList<InteractionProviderAssignmentSnapshot> GetProviderAssignments(string pointId, bool includeHidden = false) => GetIds(providerIdsByPointId, N(pointId)).Select(id => providerAssignmentsById.TryGetValue(id, out InteractionProviderAssignmentData provider) ? provider : null).Where(provider => provider != null && (includeHidden || IsVisibleToRequest(provider.visibility, false))).OrderBy(provider => provider.serviceDefinitionId, StringComparer.Ordinal).ThenBy(provider => provider.assignmentId, StringComparer.Ordinal).Select(BuildProviderSnapshot).ToArray();
        public IReadOnlyList<InteractionReservationSnapshot> GetReservations(string pointId, bool includeHidden = false) => GetIds(reservationIdsByPointId, N(pointId)).Select(id => reservationsById.TryGetValue(id, out InteractionReservationData reservation) ? reservation : null).Where(reservation => reservation != null && (includeHidden || IsVisibleToRequest(reservation.visibility, false))).OrderBy(reservation => reservation.startWorldTime).ThenBy(reservation => reservation.priority).ThenBy(reservation => reservation.reservationId, StringComparer.Ordinal).Select(BuildReservationSnapshot).ToArray();
        public IReadOnlyList<InteractionUseSessionSnapshot> GetUseSessions(string pointId, bool includeHidden = false) => GetIds(sessionIdsByPointId, N(pointId)).Select(id => sessionsById.TryGetValue(id, out InteractionUseSessionData session) ? session : null).Where(session => session != null && (includeHidden || IsVisibleToRequest(session.visibility, false))).OrderBy(session => session.startWorldTime).ThenBy(session => session.sessionId, StringComparer.Ordinal).Select(BuildSessionSnapshot).ToArray();

        public InteractionPointRuntimeSaveData CreateSaveData()
        {
            return new InteractionPointRuntimeSaveData
            {
                schemaVersion = InteractionPointRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                points = pointsById.Values.OrderBy(item => item.interactionPointId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                hostAssignments = hostAssignmentsById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                subjectLinks = subjectLinksById.Values.OrderBy(item => item.linkId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                providerAssignments = providerAssignmentsById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                reservations = reservationsById.Values.OrderBy(item => item.reservationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                useSessions = sessionsById.Values.OrderBy(item => item.sessionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public InteractionPointOperationResult RestoreFromSaveData(InteractionPointRuntimeSaveData saveData, LocationRuntime locationRuntime, EntityLocationRuntime entityLocationRuntime, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, registry, locationRuntime ?? locations, entityLocationRuntime ?? entityLocations, expectedWorldId, out string failure))
            {
                return Fail(InteractionPointOperationStatus.RestoreFailed, failure, before);
            }

            InteractionPointRuntimeSaveData rollback = CreateSaveData();
            try
            {
                RestoreInternal(saveData);
                locations = locationRuntime ?? locations;
                entityLocations = entityLocationRuntime ?? entityLocations;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? worldId : expectedWorldId.Trim();
                IsDirty = false;
                return InteractionPointOperationResult.Success(null, restoring ? "Interaction points restored." : "Interaction points loaded.", before, Revision);
            }
            catch (Exception ex)
            {
                RestoreInternal(rollback);
                return Fail(InteractionPointOperationStatus.RestoreFailed, $"Interaction point restore failed: {ex.Message}", before);
            }
        }

        public bool ValidateCurrent(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), registry, locations, entityLocations, worldId, out failure);
        }

        public static bool ValidateSaveData(InteractionPointRuntimeSaveData saveData, DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, string expectedWorldId, out string failure)
        {
            List<string> errors = new List<string>();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData == null)
            {
                failure = "Interaction point save data is null.";
                return false;
            }

            if (saveData.schemaVersion != InteractionPointRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported interaction point schema version {saveData.schemaVersion}.");
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId.Trim(), world, StringComparison.Ordinal)) errors.Add($"Interaction point save world '{saveData.worldId}' does not match '{world}'.");

            HashSet<string> pointIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> hostIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> linkIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> providerIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> reservationIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sessionIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> activeSessionsByPoint = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (InteractionPointRecordData point in saveData.points ?? new List<InteractionPointRecordData>())
            {
                if (point == null) continue;
                string pointId = N(point.interactionPointId);
                if (string.IsNullOrWhiteSpace(pointId)) errors.Add("Interaction point record is missing an ID.");
                else if (!pointIds.Add(pointId)) errors.Add($"Duplicate interaction point ID '{pointId}'.");
                if (string.IsNullOrWhiteSpace(point.interactionPointDefinitionId)) errors.Add($"Interaction point '{pointId}' is missing a definition ID.");
                else if (registry != null && !registry.TryGet(point.interactionPointDefinitionId, out InteractionPointDefinition _)) errors.Add($"Interaction point '{pointId}' references missing definition '{point.interactionPointDefinitionId}'.");
                if (!WorldMatchesStatic(point.worldId, world)) errors.Add($"Interaction point '{pointId}' belongs to world '{point.worldId}', not '{world}'.");
                if (string.IsNullOrWhiteSpace(point.activeHostLocationId)) errors.Add($"Interaction point '{pointId}' has no active host location.");
                else if (locations == null || !locations.TryGetSnapshot(point.activeHostLocationId, out _)) errors.Add($"Interaction point '{pointId}' references missing host location '{point.activeHostLocationId}'.");
                if (point.capacityOverride == 0) { }
                else if (point.capacityOverride < -1) errors.Add($"Interaction point '{pointId}' has invalid capacity override '{point.capacityOverride}'.");
                if (!Enum.IsDefined(typeof(InteractionPointLifecycleState), point.lifecycleState) || point.lifecycleState == InteractionPointLifecycleState.Unknown) errors.Add($"Interaction point '{pointId}' has invalid lifecycle '{point.lifecycleState}'.");
                foreach (string serviceId in Clean(point.serviceDefinitionIds))
                {
                    if (registry != null && !registry.TryGet(serviceId, out InteractionServiceDefinition _)) errors.Add($"Interaction point '{pointId}' references missing service '{serviceId}'.");
                }
            }

            foreach (InteractionPointHostAssignmentData assignment in saveData.hostAssignments ?? new List<InteractionPointHostAssignmentData>())
            {
                if (assignment == null) continue;
                string id = N(assignment.assignmentId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Interaction point host assignment is missing an ID.");
                else if (!hostIds.Add(id)) errors.Add($"Duplicate interaction point host assignment '{id}'.");
                if (!pointIds.Contains(N(assignment.interactionPointId))) errors.Add($"Host assignment '{id}' references missing point '{assignment.interactionPointId}'.");
                if (locations == null || !locations.TryGetSnapshot(assignment.hostLocationId, out _)) errors.Add($"Host assignment '{id}' references missing host location '{assignment.hostLocationId}'.");
                if (assignment.endWorldTime >= 0d && assignment.endWorldTime < assignment.startWorldTime) errors.Add($"Host assignment '{id}' ends before it starts.");
            }

            foreach (InteractionSubjectLinkData link in saveData.subjectLinks ?? new List<InteractionSubjectLinkData>())
            {
                if (link == null) continue;
                string id = N(link.linkId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Interaction point subject link is missing an ID.");
                else if (!linkIds.Add(id)) errors.Add($"Duplicate interaction point subject link '{id}'.");
                if (!pointIds.Contains(N(link.interactionPointId))) errors.Add($"Subject link '{id}' references missing point '{link.interactionPointId}'.");
                if (link.role == InteractionSubjectLinkRole.Unknown) errors.Add($"Subject link '{id}' has invalid role.");
                if (link.subject == null || string.IsNullOrWhiteSpace(link.subject.subjectType) || string.IsNullOrWhiteSpace(link.subject.subjectId)) errors.Add($"Subject link '{id}' has invalid subject.");
                if (link.endWorldTime >= 0d && link.endWorldTime < link.startWorldTime) errors.Add($"Subject link '{id}' ends before it starts.");
            }

            foreach (InteractionProviderAssignmentData provider in saveData.providerAssignments ?? new List<InteractionProviderAssignmentData>())
            {
                if (provider == null) continue;
                string id = N(provider.assignmentId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Interaction point provider assignment is missing an ID.");
                else if (!providerIds.Add(id)) errors.Add($"Duplicate interaction point provider assignment '{id}'.");
                if (!pointIds.Contains(N(provider.interactionPointId))) errors.Add($"Provider assignment '{id}' references missing point '{provider.interactionPointId}'.");
                if (registry != null && !string.IsNullOrWhiteSpace(provider.serviceDefinitionId) && !registry.TryGet(provider.serviceDefinitionId, out InteractionServiceDefinition _)) errors.Add($"Provider assignment '{id}' references missing service '{provider.serviceDefinitionId}'.");
                if (provider.requirementKind == InteractionProviderRequirementKind.AssignedPerson && (provider.providerEntity == null || string.IsNullOrWhiteSpace(provider.providerEntity.entityId))) errors.Add($"Provider assignment '{id}' requires a provider entity.");
            }

            foreach (InteractionReservationData reservation in saveData.reservations ?? new List<InteractionReservationData>())
            {
                if (reservation == null) continue;
                string id = N(reservation.reservationId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Interaction reservation is missing an ID.");
                else if (!reservationIds.Add(id)) errors.Add($"Duplicate interaction reservation '{id}'.");
                if (!pointIds.Contains(N(reservation.interactionPointId))) errors.Add($"Reservation '{id}' references missing point '{reservation.interactionPointId}'.");
                if (reservation.endWorldTime >= 0d && reservation.endWorldTime < reservation.startWorldTime) errors.Add($"Reservation '{id}' ends before it starts.");
            }

            foreach (InteractionUseSessionData session in saveData.useSessions ?? new List<InteractionUseSessionData>())
            {
                if (session == null) continue;
                string id = N(session.sessionId);
                string pointId = N(session.interactionPointId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Interaction use session is missing an ID.");
                else if (!sessionIds.Add(id)) errors.Add($"Duplicate interaction use session '{id}'.");
                if (!pointIds.Contains(pointId)) errors.Add($"Use session '{id}' references missing point '{session.interactionPointId}'.");
                if (session.endWorldTime >= 0d && session.endWorldTime < session.startWorldTime) errors.Add($"Use session '{id}' ends before it starts.");
                if (IsActiveSessionState(session.lifecycleState)) activeSessionsByPoint[pointId] = activeSessionsByPoint.TryGetValue(pointId, out int count) ? count + 1 : 1;
            }

            foreach (InteractionPointTransactionRecordData tx in saveData.transactions ?? new List<InteractionPointTransactionRecordData>())
            {
                if (tx == null) continue;
                if (string.IsNullOrWhiteSpace(tx.transactionId)) errors.Add("Interaction point transaction is missing an ID.");
                else if (!transactionIds.Add(tx.transactionId.Trim())) errors.Add($"Duplicate interaction point transaction '{tx.transactionId}'.");
            }

            foreach (InteractionPointRecordData point in saveData.points ?? new List<InteractionPointRecordData>())
            {
                if (point == null) continue;
                string pointId = N(point.interactionPointId);
                int capacity = point.capacityOverride;
                if (capacity <= 0 && registry != null && registry.TryGet(point.interactionPointDefinitionId, out InteractionPointDefinition definition))
                {
                    capacity = definition.SimultaneousUserCapacity;
                }

                if (capacity > 0 && activeSessionsByPoint.TryGetValue(pointId, out int active) && active > capacity) errors.Add($"Interaction point '{pointId}' has {active} active sessions over capacity {capacity}.");
            }

            failure = errors.Count == 0
                ? "Interaction point validation succeeded."
                : $"Interaction point validation failed with {errors.Count} error(s): {string.Join(" | ", errors)}";
            return errors.Count == 0;
        }

        public void Reset()
        {
            pointsById.Clear();
            hostAssignmentsById.Clear();
            subjectLinksById.Clear();
            providerAssignmentsById.Clear();
            reservationsById.Clear();
            sessionsById.Clear();
            transactionsById.Clear();
            pointIdsByHostLocationId.Clear();
            pointIdsByServiceDefinitionId.Clear();
            linkIdsByPointId.Clear();
            providerIdsByPointId.Clear();
            reservationIdsByPointId.Clear();
            sessionIdsByPointId.Clear();
            Revision = 0L;
            IsDirty = false;
            disposed = false;
        }

        public void Dispose()
        {
            Reset();
            disposed = true;
        }

        private bool Ready(long before, out InteractionPointOperationResult failure)
        {
            if (disposed)
            {
                failure = Fail(InteractionPointOperationStatus.Disposed, "Interaction point runtime is disposed.", before);
                return false;
            }

            if (registry == null || locations == null)
            {
                failure = Fail(InteractionPointOperationStatus.InvalidRequest, "Interaction point runtime requires definitions and locations.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool ValidateRevision(long expectedRevision, long before, out InteractionPointOperationResult failure)
        {
            if (expectedRevision >= 0L && expectedRevision != Revision)
            {
                failure = Fail(InteractionPointOperationStatus.RevisionConflict, $"Expected interaction point revision {expectedRevision}, but current revision is {Revision}.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool TryGetPointDefinition(string definitionId, long before, out InteractionPointDefinition definition, out InteractionPointOperationResult failure)
        {
            definition = null;
            if (registry == null || !registry.TryGet(N(definitionId), out definition))
            {
                failure = Fail(InteractionPointOperationStatus.MissingDefinition, $"Interaction point definition '{definitionId}' is missing.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool TryGetServiceDefinition(string serviceId, long before, out InteractionServiceDefinition definition, out InteractionPointOperationResult failure)
        {
            definition = null;
            if (registry == null || !registry.TryGet(N(serviceId), out definition))
            {
                failure = Fail(InteractionPointOperationStatus.MissingService, $"Interaction service definition '{serviceId}' is missing.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool ValidateHost(string hostLocationId, InteractionPointDefinition definition, long before, out LocationSnapshot host, out InteractionPointOperationResult failure)
        {
            host = null;
            string id = N(hostLocationId);
            if (string.IsNullOrWhiteSpace(id) || locations == null || !locations.TryGetSnapshot(id, out host))
            {
                failure = Fail(InteractionPointOperationStatus.MissingHostLocation, $"Host location '{hostLocationId}' is missing.", before);
                return false;
            }

            if (!WorldMatches(host.WorldId))
            {
                failure = Fail(InteractionPointOperationStatus.WrongWorld, $"Host location '{host.LocationId}' belongs to world '{host.WorldId}', not '{worldId}'.", before);
                return false;
            }

            if (registry != null && registry.TryGet(host.LocationDefinitionId, out LocationDefinition locationDefinition) && !locationDefinition.ValidInteractionPointHost)
            {
                failure = Fail(InteractionPointOperationStatus.InvalidHostLocation, $"Host location definition '{locationDefinition.Id}' cannot host interaction points.", before);
                return false;
            }

            LocationCategory category = LocationCategory.Unknown;
            if (registry != null && registry.TryGet(host.LocationDefinitionId, out LocationDefinition resolved))
            {
                category = resolved.Category;
            }

            if (definition != null && category != LocationCategory.Unknown && !definition.SupportsHostCategory(category))
            {
                failure = Fail(InteractionPointOperationStatus.InvalidHostLocation, $"Interaction point definition '{definition.Id}' does not support host category '{category}'.", before);
                return false;
            }

            if (!IsHostUsable(host.LifecycleState))
            {
                failure = Fail(InteractionPointOperationStatus.InvalidHostLocation, $"Host location '{host.LocationId}' is not active enough for interaction points ({host.LifecycleState}).", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool ValidateServiceForPoint(InteractionPointDefinition pointDefinition, string serviceId, long before, out InteractionServiceDefinition serviceDefinition, out InteractionPointOperationResult failure)
        {
            if (!TryGetServiceDefinition(serviceId, before, out serviceDefinition, out failure)) return false;
            if (!pointDefinition.SupportsServiceCategory(serviceDefinition.Category) || !serviceDefinition.SupportsInteractionPointDefinition(pointDefinition.Id))
            {
                failure = Fail(InteractionPointOperationStatus.InvalidServiceBinding, $"Service '{serviceId}' is incompatible with interaction point definition '{pointDefinition.Id}'.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool EvaluatePresence(EntityLocationReferenceData entity, string hostLocationId, InteractionPhysicalPresencePolicy policy, out string failure)
        {
            failure = string.Empty;
            if (policy == InteractionPhysicalPresencePolicy.NotRequired || policy == InteractionPhysicalPresencePolicy.RemoteAllowed)
            {
                return true;
            }

            if (entity == null || string.IsNullOrWhiteSpace(entity.entityId))
            {
                failure = "entity-missing";
                return false;
            }

            EntityLocationResolutionResult resolved = entityLocations?.ResolvePhysicalLocation(entity);
            if (resolved == null || !resolved.Succeeded || string.IsNullOrWhiteSpace(resolved.LocationId))
            {
                failure = "location-unresolved";
                return false;
            }

            if (policy == InteractionPhysicalPresencePolicy.SameExactLocation || policy == InteractionPhysicalPresencePolicy.ProviderAndConsumerSameLocation)
            {
                bool same = string.Equals(resolved.LocationId, N(hostLocationId), StringComparison.Ordinal);
                failure = same ? string.Empty : $"wrong-location:{resolved.LocationId}";
                return same;
            }

            if (policy == InteractionPhysicalPresencePolicy.WithinHostLocation)
            {
                bool contained = IsSameOrDescendant(resolved.LocationId, hostLocationId);
                failure = contained ? string.Empty : $"outside-host:{resolved.LocationId}";
                return contained;
            }

            if (policy == InteractionPhysicalPresencePolicy.WithinImmediateParent)
            {
                LocationContainmentSnapshot parent = locations?.GetActiveParentLink(resolved.LocationId);
                bool sameParent = string.Equals(resolved.LocationId, N(hostLocationId), StringComparison.Ordinal) || string.Equals(parent?.ParentLocationId, N(hostLocationId), StringComparison.Ordinal);
                failure = sameParent ? string.Empty : $"outside-immediate-parent:{resolved.LocationId}";
                return sameParent;
            }

            failure = "presence-policy-unsupported";
            return false;
        }

        private bool IsSameOrDescendant(string locationId, string hostLocationId)
        {
            string resolved = N(locationId);
            string host = N(hostLocationId);
            if (string.Equals(resolved, host, StringComparison.Ordinal)) return true;
            return locations?.GetAncestors(resolved).Any(item => item.LocationId == host) == true;
        }

        private InteractionCapacityResult EvaluateCapacity(InteractionPointRecordData point, InteractionPointDefinition definition)
        {
            definition ??= registry != null && registry.TryGet(point.interactionPointDefinitionId, out InteractionPointDefinition found) ? found : null;
            int limit = point.capacityOverride == 0 ? definition?.SimultaneousUserCapacity ?? 1 : point.capacityOverride;
            bool exclusive = point.hasExclusiveUseOverride ? point.exclusiveUseOverride : definition?.ExclusiveUse ?? true;
            int active = GetIds(sessionIdsByPointId, point.interactionPointId).Count(id => sessionsById.TryGetValue(id, out InteractionUseSessionData session) && IsActiveSessionState(session.lifecycleState));
            bool blocked = exclusive && active > 0;
            int available = limit < 0 ? int.MaxValue : Math.Max(0, limit - active);
            if (blocked) available = 0;
            return new InteractionCapacityResult(limit, active, blocked, available, blocked ? "Exclusive interaction point already has an active session." : active >= limit && limit > 0 ? "Interaction point is full." : "Interaction point has capacity.");
        }

        private bool HasActiveReservationConflict(string pointId, string serviceId, double start, double end)
        {
            foreach (InteractionReservationData reservation in reservationsById.Values.Where(item => item.lifecycleState == InteractionReservationLifecycle.Active && item.interactionPointId == N(pointId) && item.serviceDefinitionId == N(serviceId)))
            {
                double existingEnd = reservation.endWorldTime < 0d ? double.MaxValue : reservation.endWorldTime;
                double requestedEnd = end < 0d ? double.MaxValue : end;
                if (start <= existingEnd && reservation.startWorldTime <= requestedEnd)
                {
                    return true;
                }
            }

            return false;
        }

        private InteractionProviderAssignmentSnapshot FindProvider(string pointId, string serviceId)
        {
            return GetProviderAssignments(pointId, includeHidden: true)
                .Where(item => item.IsActive && (string.IsNullOrWhiteSpace(item.ServiceDefinitionId) || item.ServiceDefinitionId == N(serviceId)))
                .OrderBy(item => item.ServiceDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.AssignmentId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private void RestoreInternal(InteractionPointRuntimeSaveData saveData)
        {
            Reset();
            worldId = string.IsNullOrWhiteSpace(saveData.worldId) ? worldId : saveData.worldId.Trim();
            foreach (InteractionPointRecordData point in saveData.points ?? new List<InteractionPointRecordData>()) pointsById[N(point.interactionPointId)] = point.Clone();
            foreach (InteractionPointHostAssignmentData assignment in saveData.hostAssignments ?? new List<InteractionPointHostAssignmentData>()) hostAssignmentsById[N(assignment.assignmentId)] = assignment.Clone();
            foreach (InteractionSubjectLinkData link in saveData.subjectLinks ?? new List<InteractionSubjectLinkData>()) subjectLinksById[N(link.linkId)] = link.Clone();
            foreach (InteractionProviderAssignmentData provider in saveData.providerAssignments ?? new List<InteractionProviderAssignmentData>()) providerAssignmentsById[N(provider.assignmentId)] = provider.Clone();
            foreach (InteractionReservationData reservation in saveData.reservations ?? new List<InteractionReservationData>()) reservationsById[N(reservation.reservationId)] = reservation.Clone();
            foreach (InteractionUseSessionData session in saveData.useSessions ?? new List<InteractionUseSessionData>()) sessionsById[N(session.sessionId)] = session.Clone();
            foreach (InteractionPointTransactionRecordData tx in saveData.transactions ?? new List<InteractionPointTransactionRecordData>()) transactionsById[N(tx.transactionId)] = tx.Clone();
            Revision = Math.Max(0L, saveData.revision);
            RebuildIndexes();
            IsDirty = false;
        }

        private void RebuildIndexes()
        {
            pointIdsByHostLocationId.Clear();
            pointIdsByServiceDefinitionId.Clear();
            linkIdsByPointId.Clear();
            providerIdsByPointId.Clear();
            reservationIdsByPointId.Clear();
            sessionIdsByPointId.Clear();
            foreach (InteractionPointRecordData point in pointsById.Values)
            {
                AddIndex(pointIdsByHostLocationId, point.activeHostLocationId, point.interactionPointId);
                foreach (string serviceId in Clean(point.serviceDefinitionIds)) AddIndex(pointIdsByServiceDefinitionId, serviceId, point.interactionPointId);
            }

            foreach (InteractionSubjectLinkData link in subjectLinksById.Values) AddIndex(linkIdsByPointId, link.interactionPointId, link.linkId);
            foreach (InteractionProviderAssignmentData provider in providerAssignmentsById.Values) AddIndex(providerIdsByPointId, provider.interactionPointId, provider.assignmentId);
            foreach (InteractionReservationData reservation in reservationsById.Values) AddIndex(reservationIdsByPointId, reservation.interactionPointId, reservation.reservationId);
            foreach (InteractionUseSessionData session in sessionsById.Values) AddIndex(sessionIdsByPointId, session.interactionPointId, session.sessionId);
        }

        private void Complete(string transactionId, string operation, string targetId)
        {
            string tx = N(transactionId);
            if (string.IsNullOrWhiteSpace(tx)) return;
            transactionsById[tx] = new InteractionPointTransactionRecordData { transactionId = tx, operation = operation ?? string.Empty, targetId = N(targetId), revision = Revision + 1L };
        }

        private bool TryDuplicate(string transactionId, string targetId, string operation, long before, out InteractionPointOperationResult result)
        {
            result = null;
            string txId = N(transactionId);
            if (string.IsNullOrWhiteSpace(txId) || !transactionsById.TryGetValue(txId, out InteractionPointTransactionRecordData tx)) return false;
            if (!string.Equals(tx.operation, operation, StringComparison.Ordinal) || !string.Equals(tx.targetId, N(targetId), StringComparison.Ordinal))
            {
                result = Fail(InteractionPointOperationStatus.InvalidRequest, $"Transaction '{txId}' already exists for a different interaction-point operation.", before);
                return true;
            }

            InteractionPointSnapshot point = pointsById.TryGetValue(N(targetId), out InteractionPointRecordData pointData) ? BuildPointSnapshot(pointData) : null;
            result = InteractionPointOperationResult.Success(point, "Interaction point operation already processed.", before, before, duplicate: true);
            return true;
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private InteractionPointOperationResult Commit(InteractionPointOperationResult result)
        {
            OperationCommitted?.Invoke(result);
            return result;
        }

        private static void AddIndex(Dictionary<string, SortedSet<string>> index, string key, string value)
        {
            key = N(key);
            value = N(value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
            if (!index.TryGetValue(key, out SortedSet<string> set))
            {
                set = new SortedSet<string>(StringComparer.Ordinal);
                index[key] = set;
            }

            set.Add(value);
        }

        private static IReadOnlyList<string> GetIds(Dictionary<string, SortedSet<string>> index, string key)
        {
            return index.TryGetValue(N(key), out SortedSet<string> ids) ? ids.ToArray() : Array.Empty<string>();
        }

        private static InteractionPointOperationResult Fail(InteractionPointOperationStatus status, string message, long before) => InteractionPointOperationResult.Failure(status, message, before);
        private static InteractionEligibilityResult Eligibility(InteractionPointOperationStatus status, string message, InteractionPointSnapshot point, string serviceId, EntityLocationReferenceData consumer, EntityLocationReferenceData provider, string hostLocationId, InteractionCapacityResult capacity, IEnumerable<string> reasons, long pointRevision, long entityRevision, long locationRevision) => new InteractionEligibilityResult(status, message, point, serviceId, consumer, provider, hostLocationId, capacity, reasons, pointRevision, entityRevision, locationRevision);
        private InteractionPointSnapshot BuildPointSnapshot(InteractionPointRecordData data) => new InteractionPointSnapshot(data);
        private InteractionHostAssignmentSnapshot BuildHostSnapshot(InteractionPointHostAssignmentData data) => new InteractionHostAssignmentSnapshot(data);
        private InteractionSubjectLinkSnapshot BuildLinkSnapshot(InteractionSubjectLinkData data) => new InteractionSubjectLinkSnapshot(data);
        private InteractionProviderAssignmentSnapshot BuildProviderSnapshot(InteractionProviderAssignmentData data) => new InteractionProviderAssignmentSnapshot(data);
        private InteractionReservationSnapshot BuildReservationSnapshot(InteractionReservationData data) => new InteractionReservationSnapshot(data);
        private InteractionUseSessionSnapshot BuildSessionSnapshot(InteractionUseSessionData data) => new InteractionUseSessionSnapshot(data);
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string First(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? N(fallback) : value.Trim();
        private bool WorldMatches(string value) => string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), worldId, StringComparison.Ordinal);
        private static bool WorldMatchesStatic(string value, string world) => string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), world, StringComparison.Ordinal);
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static bool IsVisibleToRequest(InteractionPointVisibility visibility, bool privileged) => privileged || visibility != InteractionPointVisibility.Hidden && visibility != InteractionPointVisibility.Secret;
        private static bool IsUsableLifecycle(InteractionPointLifecycleState state) => state == InteractionPointLifecycleState.Active;
        private static bool IsHostUsable(LocationLifecycleState state) => state == LocationLifecycleState.Active || state == LocationLifecycleState.Proposed;
        private static bool IsEndedState(InteractionPointLifecycleState state) => state == InteractionPointLifecycleState.Destroyed || state == InteractionPointLifecycleState.Historical || state == InteractionPointLifecycleState.Invalid;
        private static bool IsActiveSessionState(InteractionUseSessionLifecycle state) => state == InteractionUseSessionLifecycle.Active || state == InteractionUseSessionLifecycle.Proposed || state == InteractionUseSessionLifecycle.Suspended;
        private static bool IsEndedSessionState(InteractionUseSessionLifecycle state) => state == InteractionUseSessionLifecycle.Completed || state == InteractionUseSessionLifecycle.Cancelled || state == InteractionUseSessionLifecycle.Expired || state == InteractionUseSessionLifecycle.Historical || state == InteractionUseSessionLifecycle.Invalid;
        private static bool CanTransition(InteractionPointLifecycleState from, InteractionPointLifecycleState to) => to != InteractionPointLifecycleState.Unknown && from != InteractionPointLifecycleState.Destroyed && from != InteractionPointLifecycleState.Historical && from != InteractionPointLifecycleState.Invalid;
        private static bool CanTransitionSession(InteractionUseSessionLifecycle from, InteractionUseSessionLifecycle to) => to != InteractionUseSessionLifecycle.Unknown && !IsEndedSessionState(from);
        private static InteractionPointOperationStatus ClassifyEligibilityFailure(IReadOnlyList<string> reasons)
        {
            if (reasons.Any(item => item.Contains("provider.missing") || item.Contains("provider.location"))) return InteractionPointOperationStatus.ProviderAbsent;
            if (reasons.Any(item => item.Contains("consumer."))) return InteractionPointOperationStatus.ConsumerAbsent;
            if (reasons.Any(item => item.Contains("capacity"))) return InteractionPointOperationStatus.CapacityFull;
            if (reasons.Any(item => item.Contains("visibility"))) return InteractionPointOperationStatus.VisibilityDenied;
            if (reasons.Any(item => item.Contains("requirements"))) return InteractionPointOperationStatus.RequirementFailed;
            return InteractionPointOperationStatus.InvalidRequest;
        }

        private static InteractionPointHostAssignmentData CreateHostAssignment(string assignmentId, string pointId, string locationId, string worldId, double worldTime, string category, InteractionPointVisibility visibility, string sourceEventId, string sourceRecordId, string provenanceId)
        {
            return new InteractionPointHostAssignmentData
            {
                assignmentId = N(assignmentId),
                interactionPointId = N(pointId),
                hostLocationId = N(locationId),
                worldId = N(worldId),
                assignmentCategory = N(category),
                startWorldTime = worldTime,
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = 1L
            };
        }

        private string BuildHostAssignmentId(string pointId, string locationId, int ordinal) => $"interaction-host.{N(pointId)}.{N(locationId)}.{ordinal:0000}";
        private string BuildSubjectLinkId(string pointId, InteractionSubjectLinkRole role, string subjectId, int ordinal) => $"interaction-subject.{N(pointId)}.{role.ToString().ToLowerInvariant()}.{N(subjectId)}.{ordinal:0000}";
        private string BuildProviderAssignmentId(string pointId, string serviceId, string providerId, int ordinal) => $"interaction-provider.{N(pointId)}.{N(serviceId)}.{N(providerId)}.{ordinal:0000}";
        private string BuildReservationId(string pointId, string serviceId, int ordinal) => $"interaction-reservation.{N(pointId)}.{N(serviceId)}.{ordinal:0000}";
        private string BuildSessionId(string pointId, string serviceId, int ordinal) => $"interaction-session.{N(pointId)}.{N(serviceId)}.{ordinal:0000}";
    }
}
