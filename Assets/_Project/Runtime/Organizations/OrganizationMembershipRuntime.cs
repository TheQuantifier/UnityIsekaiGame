using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Organizations
{
    public sealed class OrganizationMembershipRuntime
    {
        private readonly Dictionary<string, OrganizationMembershipRecordData> membershipsById = new Dictionary<string, OrganizationMembershipRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationRankAssignmentRecordData> rankAssignmentsById = new Dictionary<string, OrganizationRankAssignmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationOfficeRecordData> officesById = new Dictionary<string, OrganizationOfficeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationOfficeAssignmentRecordData> officeAssignmentsById = new Dictionary<string, OrganizationOfficeAssignmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationMembershipTransactionRecordData> transactionsById = new Dictionary<string, OrganizationMembershipTransactionRecordData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private string worldId = string.Empty;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownOrganizationIds = new HashSet<string>(StringComparer.Ordinal);

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public int MembershipCount => membershipsById.Count;
        public int OfficeCount => officesById.Count;
        public IReadOnlyList<OrganizationMembershipSnapshot> Memberships => membershipsById.Values.OrderBy(item => item.membershipId, StringComparer.Ordinal).Select(BuildMembershipSnapshot).ToArray();
        public IReadOnlyList<OrganizationOfficeSnapshot> Offices => officesById.Values.OrderBy(item => item.officeId, StringComparer.Ordinal).Select(BuildOfficeSnapshot).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, string world, IEnumerable<string> persons = null, IEnumerable<string> organizationIds = null)
        {
            registry = definitionRegistry ?? registry;
            organizations = organizationRuntime ?? organizations;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            knownPersonIds = new HashSet<string>(Clean(persons), StringComparer.Ordinal);
            knownOrganizationIds = new HashSet<string>(Clean(organizationIds).Concat((organizations?.Snapshots ?? Array.Empty<OrganizationSnapshot>()).Select(item => item.OrganizationId)), StringComparer.Ordinal);
        }

        public OrganizationMembershipOperationResult ApplyMembership(OrganizationMembershipRequest request)
        {
            request ??= new OrganizationMembershipRequest();
            long before = Revision;
            string membershipId = Normalize(request.membershipId);
            string tx = Normalize(request.transactionId);

            if (TryDuplicate(tx, membershipId, "membership", before, out OrganizationMembershipOperationResult duplicate))
            {
                return duplicate;
            }

            OrganizationMembershipRuntimeSaveData rollback = CreateSaveData();
            bool creating = !membershipsById.TryGetValue(membershipId, out OrganizationMembershipRecordData record);
            if (creating)
            {
                if (string.IsNullOrWhiteSpace(membershipId))
                {
                    membershipId = $"organization-membership.{Normalize(request.organizationId)}.{Normalize(request.personId)}.{Normalize(request.membershipDefinitionId)}";
                }

                if (membershipsById.ContainsKey(membershipId))
                {
                    return Fail(OrganizationMembershipOperationStatus.DuplicateRecordId, $"Membership '{membershipId}' already exists.", before);
                }

                if (!ValidateMembershipCreate(request, membershipId, before, out OrganizationMembershipDefinition definition, out OrganizationSnapshot organization, out OrganizationMembershipOperationResult failure))
                {
                    return failure;
                }

                record = new OrganizationMembershipRecordData
                {
                    membershipId = membershipId,
                    organizationId = Normalize(request.organizationId),
                    personId = Normalize(request.personId),
                    membershipDefinitionId = Normalize(request.membershipDefinitionId),
                    status = request.targetStatus == OrganizationMembershipStatus.Unknown ? definition.InitialStatus : request.targetStatus,
                    sourceKind = request.sourceKind,
                    parentMembershipId = Normalize(request.parentMembershipId),
                    branchOrganizationId = Normalize(request.branchOrganizationId),
                    employmentId = Normalize(request.employmentId),
                    sourceEventId = Normalize(request.sourceEventId),
                    sourceRecordId = Normalize(request.sourceRecordId),
                    provenanceId = Normalize(request.provenanceId),
                    visibility = request.visibility,
                    tags = Clean(request.tags),
                    revision = 1L
                };
                ApplyMembershipTimes(record, record.status, request.worldTime);
                membershipsById.Add(record.membershipId, record);
            }
            else
            {
                if (!ValidateMembershipTransition(record, request, before, out OrganizationMembershipDefinition definition, out OrganizationMembershipOperationResult failure))
                {
                    return failure;
                }

                if (IsEndingStatus(request.targetStatus) && HasActiveAssignments(record) && request.endingPolicy == OrganizationMembershipEndingPolicy.FailIfActiveAssignments)
                {
                    RestoreInternal(rollback);
                    return Fail(OrganizationMembershipOperationStatus.ActiveAssignmentsBlockEnding, "Membership has active rank or office assignments.", before);
                }

                if (IsEndingStatus(request.targetStatus))
                {
                    ResolveEndingConsequences(record, request);
                }

                record.status = request.targetStatus;
                ApplyMembershipTimes(record, request.targetStatus, request.worldTime);
                record.sourceEventId = First(request.sourceEventId, record.sourceEventId);
                record.sourceRecordId = First(request.sourceRecordId, record.sourceRecordId);
                record.provenanceId = First(request.provenanceId, record.provenanceId);
                record.revision++;
            }

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationMembershipOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            OrganizationMembershipSnapshot snapshot = BuildMembershipSnapshot(record);
            if (request.preview)
            {
                RestoreInternal(rollback);
                return Succeed(snapshot, null, null, null, "Membership change previewed.", before, before, preview: true);
            }

            CompleteTransaction(tx, "membership", membershipId);
            Touch();
            return Succeed(BuildMembershipSnapshot(record), null, null, null, creating ? "Membership created." : "Membership updated.", before, Revision);
        }

        public OrganizationMembershipOperationResult AssignRank(OrganizationRankAssignmentRequest request)
        {
            request ??= new OrganizationRankAssignmentRequest();
            long before = Revision;
            string assignmentId = Normalize(request.rankAssignmentId);
            if (string.IsNullOrWhiteSpace(assignmentId))
            {
                assignmentId = $"organization-rank-assignment.{Normalize(request.membershipId)}.{Normalize(request.rankDefinitionId)}";
            }

            if (TryDuplicate(Normalize(request.transactionId), assignmentId, "rank", before, out OrganizationMembershipOperationResult duplicate))
            {
                return duplicate;
            }

            if (rankAssignmentsById.ContainsKey(assignmentId))
            {
                return Fail(OrganizationMembershipOperationStatus.DuplicateRecordId, $"Rank assignment '{assignmentId}' already exists.", before);
            }

            if (!membershipsById.TryGetValue(Normalize(request.membershipId), out OrganizationMembershipRecordData membership) || !membership.IsActive)
            {
                return Fail(OrganizationMembershipOperationStatus.MissingMembership, "Active membership is required before assigning organization rank.", before);
            }

            if (!TryGetDefinition(membership.membershipDefinitionId, out OrganizationMembershipDefinition membershipDefinition))
            {
                return Fail(OrganizationMembershipOperationStatus.MissingDefinition, $"Membership definition '{membership.membershipDefinitionId}' is missing.", before);
            }

            if (!membershipDefinition.SupportsRanks)
            {
                return Fail(OrganizationMembershipOperationStatus.InvalidDependency, "Membership definition does not support ranks.", before);
            }

            if (!TryGetDefinition(request.rankDefinitionId, out OrganizationRankDefinition rankDefinition))
            {
                return Fail(OrganizationMembershipOperationStatus.MissingRank, $"Organization rank '{request.rankDefinitionId}' is missing.", before);
            }

            if (!TryGetDefinition(rankDefinition.RankTrackDefinitionId, out OrganizationRankTrackDefinition trackDefinition))
            {
                return Fail(OrganizationMembershipOperationStatus.MissingRankTrack, $"Organization rank track '{rankDefinition.RankTrackDefinitionId}' is missing.", before);
            }

            if (trackDefinition.SupportedMembershipDefinitionIds.Count > 0 && !trackDefinition.SupportedMembershipDefinitionIds.Contains(membership.membershipDefinitionId))
            {
                return Fail(OrganizationMembershipOperationStatus.Ineligible, "Membership definition is not supported by the requested rank track.", before);
            }

            if (!rankDefinition.AppliesToOrganization(membership.organizationId))
            {
                return Fail(OrganizationMembershipOperationStatus.Ineligible, "Organization rank is not available in this organization.", before);
            }

            if (rankDefinition.PriorRankDefinitionIds.Count > 0
                && !rankAssignmentsById.Values.Any(item =>
                    string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal)
                    && rankDefinition.PriorRankDefinitionIds.Contains(item.rankDefinitionId)))
            {
                return Fail(OrganizationMembershipOperationStatus.InvalidTransition, "The immediately preceding organization rank must be earned before this rank can be assigned.", before);
            }

            OrganizationMembershipRuntimeSaveData rollback = CreateSaveData();
            OrganizationRankAssignmentRecordData previous = rankAssignmentsById.Values
                .Where(item => item.IsActive && string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal) && string.Equals(item.rankTrackDefinitionId, rankDefinition.RankTrackDefinitionId, StringComparison.Ordinal))
                .OrderBy(item => item.rankAssignmentId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (previous != null && !trackDefinition.AllowMultipleActiveRanks)
            {
                if (!request.replaceCurrentTrackRank)
                {
                    return Fail(OrganizationMembershipOperationStatus.InvalidTransition, "Rank track already has an active rank assignment.", before);
                }

                previous.state = OrganizationRankAssignmentState.Superseded;
                previous.endWorldTime = request.worldTime;
                previous.replacedByRankAssignmentId = assignmentId;
                previous.revision++;
            }

            OrganizationRankAssignmentRecordData assignment = new OrganizationRankAssignmentRecordData
            {
                rankAssignmentId = assignmentId,
                membershipId = membership.membershipId,
                organizationId = membership.organizationId,
                personId = membership.personId,
                rankTrackDefinitionId = rankDefinition.RankTrackDefinitionId,
                rankDefinitionId = Normalize(request.rankDefinitionId),
                state = OrganizationRankAssignmentState.Active,
                assignedWorldTime = request.worldTime,
                effectiveWorldTime = request.worldTime,
                assignedById = Normalize(request.assignedById),
                replacesRankAssignmentId = previous?.rankAssignmentId ?? string.Empty,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };
            rankAssignmentsById.Add(assignment.rankAssignmentId, assignment);
            membership.rankAssignmentIds = Clean(membership.rankAssignmentIds.Concat(new[] { assignment.rankAssignmentId }));
            membership.revision++;

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationMembershipOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            if (request.preview)
            {
                OrganizationRankAssignmentRecordData preview = assignment.Clone();
                RestoreInternal(rollback);
                return Succeed(BuildMembershipSnapshot(membership), null, preview, null, "Rank assignment previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "rank", assignmentId);
            Touch();
            return Succeed(BuildMembershipSnapshot(membership), null, assignment, null, "Rank assigned.", before, Revision);
        }

        public OrganizationMembershipOperationResult CreateOffice(OrganizationOfficeRequest request)
        {
            request ??= new OrganizationOfficeRequest();
            long before = Revision;
            string officeId = Normalize(request.officeId);
            if (string.IsNullOrWhiteSpace(officeId))
            {
                officeId = $"organization-office-record.{Normalize(request.organizationId)}.{Normalize(request.officeDefinitionId)}";
            }

            if (TryDuplicate(Normalize(request.transactionId), officeId, "office", before, out OrganizationMembershipOperationResult duplicate))
            {
                return duplicate;
            }

            if (officesById.ContainsKey(officeId))
            {
                return Fail(OrganizationMembershipOperationStatus.DuplicateRecordId, $"Office '{officeId}' already exists.", before);
            }

            if (!TryActiveOrganization(request.organizationId, before, out OrganizationSnapshot organization, out OrganizationMembershipOperationResult organizationFailure))
            {
                return organizationFailure;
            }

            if (!TryGetDefinition(request.officeDefinitionId, out OrganizationOfficeDefinition definition))
            {
                return Fail(OrganizationMembershipOperationStatus.MissingDefinition, $"Office definition '{request.officeDefinitionId}' is missing.", before);
            }

            if (!string.IsNullOrWhiteSpace(definition.OrganizationDefinitionId) && !string.Equals(definition.OrganizationDefinitionId, organization.DefinitionId, StringComparison.Ordinal))
            {
                return Fail(OrganizationMembershipOperationStatus.Ineligible, "Office definition is not supported by this organization definition.", before);
            }

            OrganizationMembershipRuntimeSaveData rollback = CreateSaveData();
            OrganizationOfficeRecordData office = new OrganizationOfficeRecordData
            {
                officeId = officeId,
                organizationId = organization.OrganizationId,
                officeDefinitionId = Normalize(request.officeDefinitionId),
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                state = OrganizationOfficeState.Active,
                maximumActiveHolders = request.maximumActiveHolders <= 0 ? definition.MaximumActiveHolders : request.maximumActiveHolders,
                vacancyAllowed = request.vacancyAllowed && definition.AllowVacancy,
                createdWorldTime = request.worldTime,
                linkedPositionInstanceId = Normalize(request.linkedPositionInstanceId),
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                visibility = request.visibility,
                revision = 1L
            };
            officesById.Add(office.officeId, office);

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationMembershipOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            if (request.preview)
            {
                OrganizationOfficeSnapshot preview = BuildOfficeSnapshot(office);
                RestoreInternal(rollback);
                return Succeed(null, preview, null, null, "Office creation previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "office", officeId);
            Touch();
            return Succeed(null, BuildOfficeSnapshot(office), null, null, "Office created.", before, Revision);
        }

        public OrganizationMembershipOperationResult AssignOffice(OrganizationOfficeAssignmentRequest request)
        {
            request ??= new OrganizationOfficeAssignmentRequest();
            long before = Revision;
            string assignmentId = Normalize(request.officeAssignmentId);
            if (string.IsNullOrWhiteSpace(assignmentId))
            {
                assignmentId = $"organization-office-assignment.{Normalize(request.officeId)}.{Normalize(request.membershipId)}";
            }

            if (TryDuplicate(Normalize(request.transactionId), assignmentId, "office-assignment", before, out OrganizationMembershipOperationResult duplicate))
            {
                return duplicate;
            }

            if (officeAssignmentsById.ContainsKey(assignmentId))
            {
                return Fail(OrganizationMembershipOperationStatus.DuplicateRecordId, $"Office assignment '{assignmentId}' already exists.", before);
            }

            if (!officesById.TryGetValue(Normalize(request.officeId), out OrganizationOfficeRecordData office) || !office.IsActive)
            {
                return Fail(OrganizationMembershipOperationStatus.MissingOffice, "Active office is required.", before);
            }

            if (!membershipsById.TryGetValue(Normalize(request.membershipId), out OrganizationMembershipRecordData membership) || !membership.IsActive)
            {
                return Fail(OrganizationMembershipOperationStatus.MissingMembership, "Active membership is required before assigning office.", before);
            }

            if (!string.Equals(office.organizationId, membership.organizationId, StringComparison.Ordinal))
            {
                return Fail(OrganizationMembershipOperationStatus.InvalidDependency, "Office and membership belong to different organizations.", before);
            }

            if (!TryGetDefinition(office.officeDefinitionId, out OrganizationOfficeDefinition definition))
            {
                return Fail(OrganizationMembershipOperationStatus.MissingDefinition, $"Office definition '{office.officeDefinitionId}' is missing.", before);
            }

            if (request.acting && request.proposed)
            {
                return Fail(OrganizationMembershipOperationStatus.InvalidRequest, "An office assignment cannot be both proposed and acting.", before);
            }

            if (request.acting && !definition.AllowActingHolders)
            {
                return Fail(OrganizationMembershipOperationStatus.InvalidRequest, "Office definition does not allow acting holders.", before);
            }

            if (!request.proposed && !definition.AllowJointHolders && ActiveOfficeHolderCount(office.officeId) > 0)
            {
                return Fail(OrganizationMembershipOperationStatus.CapacityFull, "Office does not allow joint holders.", before);
            }

            if (!request.proposed && ActiveOfficeHolderCount(office.officeId) >= office.maximumActiveHolders)
            {
                return Fail(OrganizationMembershipOperationStatus.CapacityFull, "Office holder capacity is full.", before);
            }

            if (definition.RequiredMembershipDefinitionIds.Count > 0 && !definition.RequiredMembershipDefinitionIds.Contains(membership.membershipDefinitionId))
            {
                return Fail(OrganizationMembershipOperationStatus.Ineligible, "Membership definition is not eligible for this office.", before);
            }

            if (definition.RequiredRankDefinitionIds.Count > 0 && !rankAssignmentsById.Values.Any(item => item.IsActive && string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal) && definition.RequiredRankDefinitionIds.Contains(item.rankDefinitionId)))
            {
                return Fail(OrganizationMembershipOperationStatus.Ineligible, "Required organization rank is missing for this office.", before);
            }

            OrganizationMembershipRuntimeSaveData rollback = CreateSaveData();
            OrganizationOfficeAssignmentRecordData assignment = new OrganizationOfficeAssignmentRecordData
            {
                officeAssignmentId = assignmentId,
                officeId = office.officeId,
                membershipId = membership.membershipId,
                organizationId = membership.organizationId,
                personId = membership.personId,
                state = request.proposed ? OrganizationOfficeAssignmentState.Proposed : request.acting ? OrganizationOfficeAssignmentState.Acting : OrganizationOfficeAssignmentState.Active,
                acting = request.acting,
                assignedWorldTime = request.worldTime,
                effectiveStartWorldTime = request.worldTime,
                expectedEndWorldTime = request.expectedEndWorldTime,
                appointedById = Normalize(request.appointedById),
                linkedEmploymentId = Normalize(request.linkedEmploymentId),
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };
            officeAssignmentsById.Add(assignment.officeAssignmentId, assignment);
            office.officeAssignmentIds = Clean(office.officeAssignmentIds.Concat(new[] { assignment.officeAssignmentId }));
            membership.officeAssignmentIds = Clean(membership.officeAssignmentIds.Concat(new[] { assignment.officeAssignmentId }));
            office.revision++;
            membership.revision++;

            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationMembershipOperationStatus.PersistenceInvalid, validationFailure, before);
            }

            if (request.preview)
            {
                OrganizationOfficeAssignmentRecordData preview = assignment.Clone();
                RestoreInternal(rollback);
                return Succeed(BuildMembershipSnapshot(membership), BuildOfficeSnapshot(office), null, preview, "Office assignment previewed.", before, before, preview: true);
            }

            CompleteTransaction(request.transactionId, "office-assignment", assignmentId);
            Touch();
            return Succeed(BuildMembershipSnapshot(membership), BuildOfficeSnapshot(office), null, assignment, "Office assigned.", before, Revision);
        }

        public OrganizationMembershipOperationResult TransitionOfficeAssignment(OrganizationOfficeAssignmentTransitionRequest request)
        {
            request ??= new OrganizationOfficeAssignmentTransitionRequest();
            long before = Revision;
            string assignmentId = Normalize(request.officeAssignmentId);
            if (TryDuplicate(Normalize(request.transactionId), assignmentId, "office-assignment-transition", before, out OrganizationMembershipOperationResult duplicate)) return duplicate;
            if (!officeAssignmentsById.TryGetValue(assignmentId, out OrganizationOfficeAssignmentRecordData assignment)) return Fail(OrganizationMembershipOperationStatus.InvalidDependency, $"Office assignment '{assignmentId}' is missing.", before);
            if (request.targetState is OrganizationOfficeAssignmentState.Unknown or OrganizationOfficeAssignmentState.Proposed) return Fail(OrganizationMembershipOperationStatus.InvalidRequest, "Office assignment transition target is invalid.", before);
            if (assignment.state == request.targetState) return Succeed(null, null, null, assignment, "Office assignment already has the requested state.", before, before, preview: request.preview, duplicate: true);
            if (!officesById.TryGetValue(assignment.officeId, out OrganizationOfficeRecordData office) || !membershipsById.TryGetValue(assignment.membershipId, out OrganizationMembershipRecordData membership)) return Fail(OrganizationMembershipOperationStatus.InvalidDependency, "Office assignment dependencies are missing.", before);

            bool activating = request.targetState is OrganizationOfficeAssignmentState.Active or OrganizationOfficeAssignmentState.Acting;
            if (activating)
            {
                if (!office.IsActive || !membership.IsActive) return Fail(OrganizationMembershipOperationStatus.InvalidDependency, "An active office and membership are required to activate an assignment.", before);
                if (!TryGetDefinition(office.officeDefinitionId, out OrganizationOfficeDefinition definition)) return Fail(OrganizationMembershipOperationStatus.MissingDefinition, $"Office definition '{office.officeDefinitionId}' is missing.", before);
                if (request.targetState == OrganizationOfficeAssignmentState.Acting && !definition.AllowActingHolders) return Fail(OrganizationMembershipOperationStatus.InvalidRequest, "Office definition does not allow acting holders.", before);
                int otherActiveHolders = officeAssignmentsById.Values.Count(item => item.officeId == office.officeId && item.officeAssignmentId != assignmentId && item.IsActive);
                if (!definition.AllowJointHolders && otherActiveHolders > 0 || otherActiveHolders >= office.maximumActiveHolders) return Fail(OrganizationMembershipOperationStatus.CapacityFull, "Office holder capacity is full.", before);
            }

            OrganizationMembershipRuntimeSaveData rollback = CreateSaveData();
            assignment.state = request.targetState;
            assignment.acting = request.targetState == OrganizationOfficeAssignmentState.Acting;
            if (activating) assignment.effectiveStartWorldTime = request.worldTime;
            if (request.targetState is OrganizationOfficeAssignmentState.Ended or OrganizationOfficeAssignmentState.Removed or OrganizationOfficeAssignmentState.Historical) assignment.endWorldTime = request.worldTime;
            if (!string.IsNullOrWhiteSpace(request.sourceEventId)) assignment.sourceEventId = request.sourceEventId.Trim();
            assignment.revision++; office.revision++; membership.revision++;
            if (!ValidateCurrent(out string validationFailure))
            {
                RestoreInternal(rollback);
                return Fail(OrganizationMembershipOperationStatus.PersistenceInvalid, validationFailure, before);
            }
            if (request.preview)
            {
                OrganizationOfficeAssignmentRecordData projected = assignment.Clone();
                RestoreInternal(rollback);
                return Succeed(null, null, null, projected, "Office assignment transition previewed.", before, before, preview: true);
            }
            CompleteTransaction(request.transactionId, "office-assignment-transition", assignmentId);
            Touch();
            return Succeed(BuildMembershipSnapshot(membership), BuildOfficeSnapshot(office), null, assignment, "Office assignment transitioned.", before, Revision);
        }

        public int CompareRanks(string leftRankDefinitionId, string rightRankDefinitionId)
        {
            if (!TryGetDefinition(leftRankDefinitionId, out OrganizationRankDefinition left) || !TryGetDefinition(rightRankDefinitionId, out OrganizationRankDefinition right))
            {
                return string.Compare(Normalize(leftRankDefinitionId), Normalize(rightRankDefinitionId), StringComparison.Ordinal);
            }

            int track = string.Compare(left.RankTrackDefinitionId, right.RankTrackDefinitionId, StringComparison.Ordinal);
            return track != 0 ? track : left.RankOrder.CompareTo(right.RankOrder);
        }

        public IReadOnlyList<OrganizationMembershipSnapshot> QueryMemberships(string personId = "", string organizationId = "", bool activeOnly = false)
        {
            string person = Normalize(personId);
            string organization = Normalize(organizationId);
            return membershipsById.Values
                .Where(item => (string.IsNullOrWhiteSpace(person) || string.Equals(item.personId, person, StringComparison.Ordinal))
                    && (string.IsNullOrWhiteSpace(organization) || string.Equals(item.organizationId, organization, StringComparison.Ordinal))
                    && (!activeOnly || item.IsActive))
                .OrderBy(item => item.organizationId, StringComparer.Ordinal)
                .ThenBy(item => item.personId, StringComparer.Ordinal)
                .ThenBy(item => item.membershipId, StringComparer.Ordinal)
                .Select(BuildMembershipSnapshot)
                .ToArray();
        }

        public bool TryGetMembership(string membershipId, out OrganizationMembershipSnapshot snapshot)
        {
            if (membershipsById.TryGetValue(Normalize(membershipId), out OrganizationMembershipRecordData record))
            {
                snapshot = BuildMembershipSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetOffice(string officeId, out OrganizationOfficeSnapshot snapshot)
        {
            if (officesById.TryGetValue(Normalize(officeId), out OrganizationOfficeRecordData record))
            {
                snapshot = BuildOfficeSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public OrganizationMembershipProjection ProjectMembership(string membershipId, string requesterPersonId, bool privileged = false)
        {
            InformationSubjectReferenceData subject = BuildMembershipSubject(membershipId, string.Empty, string.Empty);
            if (!membershipsById.TryGetValue(Normalize(membershipId), out OrganizationMembershipRecordData record))
            {
                return new OrganizationMembershipProjection(OrganizationMembershipProjectionAccess.Denied, subject, null, "Membership does not exist.");
            }

            subject = BuildMembershipSubject(record.membershipId, record.organizationId, record.personId);
            if (privileged || string.Equals(record.personId, Normalize(requesterPersonId), StringComparison.Ordinal) || record.visibility == OrganizationVisibility.Public)
            {
                return new OrganizationMembershipProjection(OrganizationMembershipProjectionAccess.Full, subject, BuildMembershipSnapshot(record), "Membership visible.");
            }

            if (record.visibility == OrganizationVisibility.Hidden)
            {
                return new OrganizationMembershipProjection(OrganizationMembershipProjectionAccess.Concealed, new InformationSubjectReferenceData { subjectType = InformationSubjectType.Affiliation, parentSubjectId = record.organizationId }, null, "Membership concealed.");
            }

            OrganizationMembershipRecordData redacted = record.Clone();
            redacted.sourceEventId = string.Empty;
            redacted.sourceRecordId = string.Empty;
            redacted.provenanceId = string.Empty;
            redacted.parentMembershipId = string.Empty;
            redacted.rankAssignmentIds = Array.Empty<string>();
            redacted.officeAssignmentIds = Array.Empty<string>();
            OrganizationMembershipSnapshot snapshot = new OrganizationMembershipSnapshot(redacted, Array.Empty<OrganizationRankAssignmentRecordData>(), Array.Empty<OrganizationOfficeAssignmentRecordData>());
            return new OrganizationMembershipProjection(OrganizationMembershipProjectionAccess.Redacted, subject, snapshot, "Membership redacted.");
        }

        public OrganizationMembershipRuntimeSaveData CreateSaveData()
        {
            return new OrganizationMembershipRuntimeSaveData
            {
                schemaVersion = OrganizationMembershipRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                memberships = membershipsById.Values.OrderBy(item => item.membershipId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                ranks = rankAssignmentsById.Values.OrderBy(item => item.rankAssignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                offices = officesById.Values.OrderBy(item => item.officeId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                officeAssignments = officeAssignmentsById.Values.OrderBy(item => item.officeAssignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToList()
            };
        }

        public OrganizationMembershipOperationResult RestoreFromSaveData(OrganizationMembershipRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, string world, IEnumerable<string> persons = null, IEnumerable<string> organizationIds = null, bool restoring = true)
        {
            long before = Revision;
            OrganizationMembershipRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry previousRegistry = registry;
            OrganizationRuntime previousOrganizations = organizations;
            string previousWorld = worldId;
            HashSet<string> previousPersons = knownPersonIds;
            HashSet<string> previousOrganizationsSet = knownOrganizationIds;

            Configure(definitionRegistry, organizationRuntime, world, persons, organizationIds);
            if (!ValidateSaveData(saveData, registry, organizations, worldId, knownPersonIds, knownOrganizationIds, out string failure))
            {
                registry = previousRegistry;
                organizations = previousOrganizations;
                worldId = previousWorld;
                knownPersonIds = previousPersons;
                knownOrganizationIds = previousOrganizationsSet;
                return Fail(OrganizationMembershipOperationStatus.RestoreFailed, failure, before);
            }

            try
            {
                RestoreInternal(saveData);
                IsDirty = false;
                return Succeed(null, null, null, null, "Organization membership state restored.", before, Revision);
            }
            catch (Exception ex)
            {
                RestoreInternal(rollback);
                registry = previousRegistry;
                organizations = previousOrganizations;
                worldId = previousWorld;
                knownPersonIds = previousPersons;
                knownOrganizationIds = previousOrganizationsSet;
                return Fail(OrganizationMembershipOperationStatus.RestoreFailed, ex.Message, before);
            }
        }

        public static bool ValidateSaveData(OrganizationMembershipRuntimeSaveData saveData, DefinitionRegistry registry, OrganizationRuntime organizations, string world, IEnumerable<string> persons, IEnumerable<string> organizationIds, out string failure)
        {
            failure = string.Empty;
            if (!ValidateSaveShape(saveData, out failure))
            {
                return false;
            }

            OrganizationMembershipRuntime runtime = new OrganizationMembershipRuntime();
            runtime.Configure(registry, organizations, world, persons, organizationIds);
            runtime.RestoreInternal((saveData ?? new OrganizationMembershipRuntimeSaveData()).Clone());
            return runtime.ValidateCurrent(out failure);
        }

        private static bool ValidateSaveShape(OrganizationMembershipRuntimeSaveData saveData, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Organization membership save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != OrganizationMembershipRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported organization membership schema version {saveData.schemaVersion}.";
                return false;
            }

            return !HasDuplicateIds(saveData.memberships?.Select(item => item?.membershipId), "membership", out failure)
                && !HasDuplicateIds(saveData.ranks?.Select(item => item?.rankAssignmentId), "rank assignment", out failure)
                && !HasDuplicateIds(saveData.offices?.Select(item => item?.officeId), "office", out failure)
                && !HasDuplicateIds(saveData.officeAssignments?.Select(item => item?.officeAssignmentId), "office assignment", out failure)
                && !HasDuplicateIds(saveData.transactions?.Select(item => item?.transactionId), "transaction", out failure);
        }

        private static bool HasDuplicateIds(IEnumerable<string> ids, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                string normalized = Normalize(id);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!seen.Add(normalized))
                {
                    failure = $"Duplicate organization membership {label} ID '{normalized}'.";
                    return true;
                }
            }

            return false;
        }

        private bool ValidateMembershipCreate(OrganizationMembershipRequest request, string membershipId, long before, out OrganizationMembershipDefinition definition, out OrganizationSnapshot organization, out OrganizationMembershipOperationResult failure)
        {
            definition = null;
            organization = null;
            failure = null;
            if (string.IsNullOrWhiteSpace(membershipId) || string.IsNullOrWhiteSpace(request.organizationId) || string.IsNullOrWhiteSpace(request.personId) || string.IsNullOrWhiteSpace(request.membershipDefinitionId))
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidRequest, "Membership, organization, person, and definition IDs are required.", before);
                return false;
            }

            if (!knownPersonIds.Contains(Normalize(request.personId)))
            {
                failure = Fail(OrganizationMembershipOperationStatus.MissingPerson, $"Person '{request.personId}' is not known.", before);
                return false;
            }

            if (!TryActiveOrganization(request.organizationId, before, out organization, out failure))
            {
                return false;
            }

            if (!TryGetDefinition(request.membershipDefinitionId, out definition))
            {
                failure = Fail(OrganizationMembershipOperationStatus.MissingDefinition, $"Membership definition '{request.membershipDefinitionId}' is missing.", before);
                return false;
            }

            if (!TryGetDefinition(organization.DefinitionId, out OrganizationDefinition organizationDefinition) || !definition.AppliesTo(organizationDefinition))
            {
                failure = Fail(OrganizationMembershipOperationStatus.Ineligible, "Membership definition is not applicable to this organization.", before);
                return false;
            }

            if (!ValidateSourceAndConsent(definition, request, before, out failure))
            {
                return false;
            }

            if (ViolatesMultiplicity(definition, request.personId, request.organizationId, request.membershipDefinitionId))
            {
                failure = Fail(OrganizationMembershipOperationStatus.DuplicateActiveMembership, "An active or pending membership already exists for this multiplicity policy.", before);
                return false;
            }

            if (definition.RequireParentMembershipForBranch && string.IsNullOrWhiteSpace(request.parentMembershipId))
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidDependency, "Branch membership requires a parent membership reference.", before);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.parentMembershipId) && !membershipsById.ContainsKey(Normalize(request.parentMembershipId)))
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidDependency, $"Parent membership '{request.parentMembershipId}' does not exist.", before);
                return false;
            }

            return true;
        }

        private bool ValidateMembershipTransition(OrganizationMembershipRecordData record, OrganizationMembershipRequest request, long before, out OrganizationMembershipDefinition definition, out OrganizationMembershipOperationResult failure)
        {
            definition = null;
            failure = null;
            if (!TryGetDefinition(record.membershipDefinitionId, out definition))
            {
                failure = Fail(OrganizationMembershipOperationStatus.MissingDefinition, $"Membership definition '{record.membershipDefinitionId}' is missing.", before);
                return false;
            }

            OrganizationMembershipStatus target = request.targetStatus;
            if (target == OrganizationMembershipStatus.Unknown || record.IsEnded || record.status == target)
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidTransition, $"Invalid membership transition {record.status}->{target}.", before);
                return false;
            }

            bool valid = record.status switch
            {
                OrganizationMembershipStatus.Applied => target == OrganizationMembershipStatus.Active || target == OrganizationMembershipStatus.Provisional || target == OrganizationMembershipStatus.Removed,
                OrganizationMembershipStatus.Invited => target == OrganizationMembershipStatus.Active || target == OrganizationMembershipStatus.PendingAcceptance || target == OrganizationMembershipStatus.Expired,
                OrganizationMembershipStatus.PendingAcceptance => target == OrganizationMembershipStatus.Active || target == OrganizationMembershipStatus.Expired,
                OrganizationMembershipStatus.Provisional => target == OrganizationMembershipStatus.Active || target == OrganizationMembershipStatus.Suspended || IsEndingStatus(target),
                OrganizationMembershipStatus.Active => target == OrganizationMembershipStatus.Suspended || IsEndingStatus(target),
                OrganizationMembershipStatus.Suspended => target == OrganizationMembershipStatus.Active || IsEndingStatus(target),
                OrganizationMembershipStatus.Inactive => target == OrganizationMembershipStatus.Active || IsEndingStatus(target),
                _ => false
            };

            if (!valid)
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidTransition, $"Invalid membership transition {record.status}->{target}.", before);
                return false;
            }

            if (target == OrganizationMembershipStatus.Active && definition.RequiresExplicitAcceptance && !request.explicitConsent && (record.status == OrganizationMembershipStatus.Invited || record.status == OrganizationMembershipStatus.PendingAcceptance))
            {
                failure = Fail(OrganizationMembershipOperationStatus.ConsentRequired, "Explicit acceptance is required before invited membership becomes active.", before);
                return false;
            }

            return true;
        }

        private bool ValidateSourceAndConsent(OrganizationMembershipDefinition definition, OrganizationMembershipRequest request, long before, out OrganizationMembershipOperationResult failure)
        {
            failure = null;
            OrganizationMembershipStatus target = request.targetStatus == OrganizationMembershipStatus.Unknown ? definition.InitialStatus : request.targetStatus;
            if (request.sourceKind == OrganizationMembershipSourceKind.Application && !definition.AllowApplication)
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidRequest, "Membership definition does not allow applications.", before);
                return false;
            }

            if (request.sourceKind == OrganizationMembershipSourceKind.Invitation && !definition.AllowInvitation)
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidRequest, "Membership definition does not allow invitations.", before);
                return false;
            }

            if (target == OrganizationMembershipStatus.Active && definition.RequiresExplicitAcceptance && request.sourceKind == OrganizationMembershipSourceKind.Invitation && !request.explicitConsent)
            {
                failure = Fail(OrganizationMembershipOperationStatus.ConsentRequired, "Invitation does not equal acceptance.", before);
                return false;
            }

            return true;
        }

        private bool TryActiveOrganization(string organizationId, long before, out OrganizationSnapshot snapshot, out OrganizationMembershipOperationResult failure)
        {
            string id = Normalize(organizationId);
            if (organizations == null || !organizations.TryGetSnapshot(id, out snapshot))
            {
                snapshot = null;
                failure = Fail(OrganizationMembershipOperationStatus.MissingOrganization, $"Organization '{id}' does not exist.", before);
                return false;
            }

            if (snapshot.LifecycleState != OrganizationLifecycleState.Active)
            {
                failure = Fail(OrganizationMembershipOperationStatus.InvalidDependency, $"Organization '{id}' is not active.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool ViolatesMultiplicity(OrganizationMembershipDefinition definition, string personId, string organizationId, string membershipDefinitionId)
        {
            string person = Normalize(personId);
            string organization = Normalize(organizationId);
            string membershipDefinition = Normalize(membershipDefinitionId);
            return membershipsById.Values.Any(item => !item.IsEnded
                && string.Equals(item.personId, person, StringComparison.Ordinal)
                && MatchesMultiplicity(definition.MultiplicityPolicy, item, organization, membershipDefinition));
        }

        private static bool MatchesMultiplicity(OrganizationMembershipMultiplicityPolicy policy, OrganizationMembershipRecordData item, string organizationId, string membershipDefinitionId)
        {
            return policy switch
            {
                OrganizationMembershipMultiplicityPolicy.OneActivePerPersonOrganization => string.Equals(item.organizationId, organizationId, StringComparison.Ordinal),
                OrganizationMembershipMultiplicityPolicy.OneActivePerPersonOrganizationDefinition => string.Equals(item.organizationId, organizationId, StringComparison.Ordinal) && string.Equals(item.membershipDefinitionId, membershipDefinitionId, StringComparison.Ordinal),
                OrganizationMembershipMultiplicityPolicy.MultipleHistoricalOnly => string.Equals(item.organizationId, organizationId, StringComparison.Ordinal),
                _ => false
            };
        }

        private bool HasActiveAssignments(OrganizationMembershipRecordData membership)
        {
            return rankAssignmentsById.Values.Any(item => item.IsActive && string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal))
                || officeAssignmentsById.Values.Any(item => item.IsActive && string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal));
        }

        private void ResolveEndingConsequences(OrganizationMembershipRecordData membership, OrganizationMembershipRequest request)
        {
            if (request.endingPolicy == OrganizationMembershipEndingPolicy.FailIfActiveAssignments)
            {
                return;
            }

            foreach (OrganizationRankAssignmentRecordData rank in rankAssignmentsById.Values.Where(item => item.IsActive && string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal)))
            {
                rank.state = request.endingPolicy == OrganizationMembershipEndingPolicy.SuspendActiveAssignments ? OrganizationRankAssignmentState.Suspended : OrganizationRankAssignmentState.Ended;
                rank.endWorldTime = request.worldTime;
                rank.revision++;
            }

            foreach (OrganizationOfficeAssignmentRecordData office in officeAssignmentsById.Values.Where(item => item.IsActive && string.Equals(item.membershipId, membership.membershipId, StringComparison.Ordinal)))
            {
                office.state = request.endingPolicy == OrganizationMembershipEndingPolicy.SuspendActiveAssignments ? OrganizationOfficeAssignmentState.Suspended : OrganizationOfficeAssignmentState.Ended;
                office.endWorldTime = request.worldTime;
                office.revision++;
            }
        }

        private int ActiveOfficeHolderCount(string officeId)
        {
            return officeAssignmentsById.Values.Count(item => item.IsActive && string.Equals(item.officeId, officeId, StringComparison.Ordinal));
        }

        private bool ValidateCurrent(out string failure)
        {
            failure = string.Empty;
            OrganizationMembershipRuntimeSaveData saveData = CreateSaveData();
            if (saveData.schemaVersion != OrganizationMembershipRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = "Unsupported organization membership schema version.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.IsNullOrWhiteSpace(worldId) && !string.Equals(saveData.worldId, worldId, StringComparison.Ordinal))
            {
                failure = "Organization membership world ID does not match runtime owner.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OrganizationMembershipRecordData membership in saveData.memberships ?? new List<OrganizationMembershipRecordData>())
            {
                if (membership == null || string.IsNullOrWhiteSpace(membership.membershipId) || !ids.Add(membership.membershipId))
                {
                    failure = "Organization membership records require unique non-empty IDs.";
                    return false;
                }

                if (!TryGetDefinition(membership.membershipDefinitionId, out OrganizationMembershipDefinition _))
                {
                    failure = $"Membership '{membership.membershipId}' references missing Membership Definition '{membership.membershipDefinitionId}'.";
                    return false;
                }

                if (!knownPersonIds.Contains(membership.personId))
                {
                    failure = $"Membership '{membership.membershipId}' references missing Person '{membership.personId}'.";
                    return false;
                }

                if (organizations == null || !organizations.TryGetSnapshot(membership.organizationId, out _))
                {
                    failure = $"Membership '{membership.membershipId}' references missing Organization '{membership.organizationId}'.";
                    return false;
                }
            }

            ids.Clear();
            foreach (OrganizationRankAssignmentRecordData rank in saveData.ranks ?? new List<OrganizationRankAssignmentRecordData>())
            {
                if (rank == null || string.IsNullOrWhiteSpace(rank.rankAssignmentId) || !ids.Add(rank.rankAssignmentId))
                {
                    failure = "Organization rank assignment records require unique non-empty IDs.";
                    return false;
                }

                if (!membershipsById.ContainsKey(rank.membershipId) || !TryGetDefinition(rank.rankDefinitionId, out OrganizationRankDefinition _))
                {
                    failure = $"Rank assignment '{rank.rankAssignmentId}' has invalid membership or rank reference.";
                    return false;
                }
            }

            ids.Clear();
            foreach (OrganizationOfficeRecordData office in saveData.offices ?? new List<OrganizationOfficeRecordData>())
            {
                if (office == null || string.IsNullOrWhiteSpace(office.officeId) || !ids.Add(office.officeId))
                {
                    failure = "Organization office records require unique non-empty IDs.";
                    return false;
                }

                if (!TryGetDefinition(office.officeDefinitionId, out OrganizationOfficeDefinition _))
                {
                    failure = $"Office '{office.officeId}' references missing office definition '{office.officeDefinitionId}'.";
                    return false;
                }

                if (organizations == null || !organizations.TryGetSnapshot(office.organizationId, out _))
                {
                    failure = $"Office '{office.officeId}' references missing Organization '{office.organizationId}'.";
                    return false;
                }
            }

            ids.Clear();
            foreach (OrganizationOfficeAssignmentRecordData assignment in saveData.officeAssignments ?? new List<OrganizationOfficeAssignmentRecordData>())
            {
                if (assignment == null || string.IsNullOrWhiteSpace(assignment.officeAssignmentId) || !ids.Add(assignment.officeAssignmentId))
                {
                    failure = "Organization office assignment records require unique non-empty IDs.";
                    return false;
                }

                if (!officesById.ContainsKey(assignment.officeId) || !membershipsById.ContainsKey(assignment.membershipId))
                {
                    failure = $"Office assignment '{assignment.officeAssignmentId}' has invalid office or membership reference.";
                    return false;
                }
            }

            return true;
        }

        private OrganizationMembershipSnapshot BuildMembershipSnapshot(OrganizationMembershipRecordData record)
        {
            return new OrganizationMembershipSnapshot(
                record,
                rankAssignmentsById.Values.Where(item => string.Equals(item.membershipId, record.membershipId, StringComparison.Ordinal)),
                officeAssignmentsById.Values.Where(item => string.Equals(item.membershipId, record.membershipId, StringComparison.Ordinal)));
        }

        private OrganizationOfficeSnapshot BuildOfficeSnapshot(OrganizationOfficeRecordData record)
        {
            return new OrganizationOfficeSnapshot(record, officeAssignmentsById.Values.Where(item => string.Equals(item.officeId, record.officeId, StringComparison.Ordinal)));
        }

        private bool TryGetDefinition<TDefinition>(string definitionId, out TDefinition definition)
            where TDefinition : class, IGameDefinition
        {
            definition = null;
            return registry != null && registry.TryGet(Normalize(definitionId), out definition);
        }

        private bool TryDuplicate(string transactionId, string subjectId, string operation, long before, out OrganizationMembershipOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!transactionsById.TryGetValue(transactionId, out OrganizationMembershipTransactionRecordData previous))
            {
                return false;
            }

            if (!string.Equals(previous.operation, operation, StringComparison.Ordinal) || !string.Equals(previous.subjectId, subjectId, StringComparison.Ordinal))
            {
                result = Fail(OrganizationMembershipOperationStatus.InvalidRequest, $"Transaction '{transactionId}' was already used for a different {previous.operation} operation.", before);
                return true;
            }

            OrganizationMembershipSnapshot membership = membershipsById.TryGetValue(previous.subjectId, out OrganizationMembershipRecordData membershipRecord) ? BuildMembershipSnapshot(membershipRecord) : null;
            OrganizationOfficeSnapshot office = officesById.TryGetValue(previous.subjectId, out OrganizationOfficeRecordData officeRecord) ? BuildOfficeSnapshot(officeRecord) : null;
            OrganizationRankAssignmentRecordData rank = rankAssignmentsById.TryGetValue(previous.subjectId, out OrganizationRankAssignmentRecordData rankRecord) ? rankRecord : null;
            OrganizationOfficeAssignmentRecordData assignment = officeAssignmentsById.TryGetValue(previous.subjectId, out OrganizationOfficeAssignmentRecordData assignmentRecord) ? assignmentRecord : null;
            result = Succeed(membership, office, rank, assignment, "Duplicate transaction ignored.", before, before, duplicate: true);
            return true;
        }

        private void CompleteTransaction(string transactionId, string operation, string subjectId)
        {
            transactionId = Normalize(transactionId);
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                transactionsById[transactionId] = new OrganizationMembershipTransactionRecordData
                {
                    transactionId = transactionId,
                    operation = operation ?? string.Empty,
                    subjectId = subjectId ?? string.Empty,
                    revision = Revision + 1L
                };
            }
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private void RestoreInternal(OrganizationMembershipRuntimeSaveData saveData)
        {
            OrganizationMembershipRuntimeSaveData data = saveData?.Clone() ?? new OrganizationMembershipRuntimeSaveData { worldId = worldId };
            membershipsById.Clear();
            rankAssignmentsById.Clear();
            officesById.Clear();
            officeAssignmentsById.Clear();
            transactionsById.Clear();
            foreach (OrganizationMembershipRecordData record in data.memberships ?? new List<OrganizationMembershipRecordData>())
            {
                membershipsById[record.membershipId ?? string.Empty] = record.Clone();
            }

            foreach (OrganizationRankAssignmentRecordData rank in data.ranks ?? new List<OrganizationRankAssignmentRecordData>())
            {
                rankAssignmentsById[rank.rankAssignmentId ?? string.Empty] = rank.Clone();
            }

            foreach (OrganizationOfficeRecordData office in data.offices ?? new List<OrganizationOfficeRecordData>())
            {
                officesById[office.officeId ?? string.Empty] = office.Clone();
            }

            foreach (OrganizationOfficeAssignmentRecordData assignment in data.officeAssignments ?? new List<OrganizationOfficeAssignmentRecordData>())
            {
                officeAssignmentsById[assignment.officeAssignmentId ?? string.Empty] = assignment.Clone();
            }

            foreach (OrganizationMembershipTransactionRecordData tx in data.transactions ?? new List<OrganizationMembershipTransactionRecordData>())
            {
                transactionsById[tx.transactionId ?? string.Empty] = tx.Clone();
            }

            Revision = Math.Max(0L, data.revision);
            worldId = data.worldId ?? worldId;
        }

        private static InformationSubjectReferenceData BuildMembershipSubject(string membershipId, string organizationId, string personId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Affiliation,
                subjectId = membershipId ?? string.Empty,
                parentSubjectId = organizationId ?? string.Empty,
                ownerPersonId = personId ?? string.Empty
            };
        }

        private static void ApplyMembershipTimes(OrganizationMembershipRecordData record, OrganizationMembershipStatus status, double worldTime)
        {
            if (status == OrganizationMembershipStatus.Applied)
            {
                record.appliedWorldTime = worldTime;
            }
            else if (status == OrganizationMembershipStatus.Invited || status == OrganizationMembershipStatus.PendingAcceptance)
            {
                record.invitedWorldTime = worldTime;
            }
            else if (status == OrganizationMembershipStatus.Active || status == OrganizationMembershipStatus.Provisional)
            {
                record.acceptedWorldTime = record.acceptedWorldTime < 0d ? worldTime : record.acceptedWorldTime;
                record.activeStartWorldTime = record.activeStartWorldTime < 0d ? worldTime : record.activeStartWorldTime;
            }
            else if (status == OrganizationMembershipStatus.Suspended)
            {
                record.suspendedWorldTime = worldTime;
            }
            else if (IsEndingStatus(status))
            {
                record.endWorldTime = worldTime;
            }
        }

        private static bool IsEndingStatus(OrganizationMembershipStatus status)
        {
            return status == OrganizationMembershipStatus.Resigned
                || status == OrganizationMembershipStatus.Removed
                || status == OrganizationMembershipStatus.Expelled
                || status == OrganizationMembershipStatus.Expired
                || status == OrganizationMembershipStatus.Historical;
        }

        private static OrganizationMembershipOperationResult Succeed(OrganizationMembershipSnapshot membership, OrganizationOfficeSnapshot office, OrganizationRankAssignmentRecordData rank, OrganizationOfficeAssignmentRecordData assignment, string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return OrganizationMembershipOperationResult.Success(membership, office, rank, assignment, message, before, after, preview, duplicate);
        }

        private static OrganizationMembershipOperationResult Fail(OrganizationMembershipOperationStatus status, string message, long before)
        {
            return OrganizationMembershipOperationResult.Failure(status, message, before);
        }

        private static string First(string candidate, string fallback)
        {
            return string.IsNullOrWhiteSpace(candidate) ? fallback ?? string.Empty : candidate.Trim();
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return OrganizationModelUtility.Clean(values);
        }
    }
}
