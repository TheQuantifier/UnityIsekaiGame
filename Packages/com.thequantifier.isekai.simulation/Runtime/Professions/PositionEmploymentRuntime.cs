using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    public sealed class PositionEmploymentRuntime
    {
        private readonly Dictionary<string, PositionInstanceData> positionsById = new Dictionary<string, PositionInstanceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PositionApplicationData> applicationsById = new Dictionary<string, PositionApplicationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EmploymentRecordData> employmentsById = new Dictionary<string, EmploymentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DutyAssignmentData> dutiesById = new Dictionary<string, DutyAssignmentData>(StringComparer.Ordinal);
        private readonly List<PositionEmploymentHistoryHookData> historyHooks = new List<PositionEmploymentHistoryHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private TrainingRuntime training;
        private ProfessionalActivityRuntime activities;
        private CredentialRuntime credentials;
        private ProfessionalRankRuntime ranks;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownOrganizationIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownAuthorityIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int PositionCount => positionsById.Count;
        public int ApplicationCount => applicationsById.Count;
        public int EmploymentCount => employmentsById.Count;
        public int DutyCount => dutiesById.Count;
        public IReadOnlyList<PositionEmploymentHistoryHookData> HistoryHooks => historyHooks.Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PositionInstanceData> Positions => positionsById.Values.OrderBy(item => item.positionInstanceId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PositionApplicationData> Applications => applicationsById.Values.OrderBy(item => item.requestId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EmploymentRecordData> Employments => employmentsById.Values.OrderBy(item => item.employmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<DutyAssignmentData> Duties => dutiesById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, IEnumerable<string> persons = null, IEnumerable<string> organizations = null, IEnumerable<string> authorities = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            training = trainingRuntime;
            activities = activityRuntime;
            credentials = credentialRuntime;
            ranks = rankRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownOrganizationIds = new HashSet<string>((organizations ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownAuthorityIds = new HashSet<string>((authorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public bool TryGetPosition(string positionInstanceId, out PositionInstanceData position)
        {
            if (!string.IsNullOrWhiteSpace(positionInstanceId) && positionsById.TryGetValue(positionInstanceId, out PositionInstanceData found))
            {
                position = found.Clone();
                return true;
            }

            position = null;
            return false;
        }

        public bool TryGetEmployment(string employmentId, out EmploymentRecordData employment)
        {
            if (!string.IsNullOrWhiteSpace(employmentId) && employmentsById.TryGetValue(employmentId, out EmploymentRecordData found))
            {
                employment = found.Clone();
                return true;
            }

            employment = null;
            return false;
        }

        public bool TryGetDuty(string dutyAssignmentId, out DutyAssignmentData duty)
        {
            if (!string.IsNullOrWhiteSpace(dutyAssignmentId) && dutiesById.TryGetValue(dutyAssignmentId, out DutyAssignmentData found))
            {
                duty = found.Clone();
                return true;
            }

            duty = null;
            return false;
        }

        public IReadOnlyList<PositionInstanceData> QueryPositionsByOrganization(string organizationId, bool openOnly = false)
        {
            return positionsById.Values
                .Where(item => string.Equals(item.organizationId, organizationId ?? string.Empty, StringComparison.Ordinal))
                .Where(item => !openOnly || IsOpenPosition(item.state))
                .OrderBy(item => item.positionDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.positionInstanceId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public IReadOnlyList<EmploymentRecordData> QueryEmploymentByPerson(string personId, bool activeOnly = false)
        {
            return employmentsById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal))
                .Where(item => !activeOnly || IsActiveEmployment(item.state))
                .OrderBy(item => item.employerOrganizationId, StringComparer.Ordinal)
                .ThenBy(item => item.positionInstanceId, StringComparer.Ordinal)
                .ThenBy(item => item.employmentId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public IReadOnlyList<DutyAssignmentData> QueryDutiesByEmployment(string employmentId, bool activeOnly = false)
        {
            return dutiesById.Values
                .Where(item => string.Equals(item.employmentId, employmentId ?? string.Empty, StringComparison.Ordinal))
                .Where(item => !activeOnly || item.state == DutyAssignmentState.Assigned || item.state == DutyAssignmentState.Active || item.state == DutyAssignmentState.Delegated)
                .OrderBy(item => item.priority)
                .ThenBy(item => item.dutyDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.assignmentId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public PositionEmploymentOperationResult CreatePosition(PositionInstanceData request, string transactionId, bool preview = false)
        {
            long before = revision;
            PositionInstanceData position = request?.Clone();
            PositionEmploymentOperationStatus failureStatus = ValidatePositionForMutation(position, out string failure);
            if (failureStatus != PositionEmploymentOperationStatus.Succeeded)
            {
                return PositionEmploymentOperationResult.Failure(failureStatus, failure, before);
            }

            if (positionsById.TryGetValue(position.positionInstanceId, out PositionInstanceData existing))
            {
                if (Equivalent(existing, position))
                {
                    return PositionEmploymentOperationResult.Success("Position already exists.", before, before, position: existing, duplicate: true);
                }

                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.Duplicate, $"Position instance '{position.positionInstanceId}' already exists with different data.", before);
            }

            if (preview)
            {
                return PositionEmploymentOperationResult.Success("Position previewed.", before, before, position: position, preview: true);
            }

            ApplyPositionDerivedState(position);
            positionsById[position.positionInstanceId] = position;
            MarkMutated();
            AddHook(PositionEmploymentHistoryHookKind.PositionCreated, position.positionInstanceId, string.Empty, string.Empty, string.Empty, string.Empty, position.organizationId, string.Empty, position.createdWorldTime, transactionId);
            return PositionEmploymentOperationResult.Success("Position created.", before, revision, position: position);
        }

        public PositionEligibilityResult EvaluateEligibility(string personId, string positionInstanceId, bool perceived = false, bool privilegedDiagnostics = false)
        {
            List<string> satisfied = new List<string>();
            List<string> blockers = new List<string>();
            List<string> redacted = new List<string>();
            List<string> alternatives = new List<string>();
            PositionInstanceData position = null;
            PositionDefinition definition = null;

            if (!KnownPerson(personId))
            {
                blockers.Add("person.missing");
            }

            if (!positionsById.TryGetValue(positionInstanceId ?? string.Empty, out position))
            {
                blockers.Add("position.missing");
            }
            else
            {
                if (!TryPositionDefinition(position.positionDefinitionId, out definition))
                {
                    blockers.Add($"position-definition:{position.positionDefinitionId}");
                }
                else
                {
                    if (definition.Secret && !privilegedDiagnostics)
                    {
                        redacted.Add("position.secret");
                    }

                    if (!IsOpenPosition(position.state))
                    {
                        blockers.Add($"position-state:{position.state}");
                    }

                    if (position.holderPersonIds.Contains(personId ?? string.Empty))
                    {
                        blockers.Add("position.duplicate-holder");
                    }

                    if (position.holderPersonIds.Length >= position.maximumHolders)
                    {
                        blockers.Add("position.capacity");
                    }

                    EvaluateDefinitionRequirements(personId, definition, perceived, privilegedDiagnostics, satisfied, blockers, redacted);
                    EvaluateEmploymentConflicts(personId, position, definition, satisfied, blockers);
                }
            }

            string hash = string.Join("|",
                personId ?? string.Empty,
                positionInstanceId ?? string.Empty,
                perceived ? "perceived" : "authoritative",
                professions?.Revision.ToString() ?? "0",
                training?.Revision.ToString() ?? "0",
                activities?.Revision.ToString() ?? "0",
                credentials?.Revision.ToString() ?? "0",
                ranks?.Revision.ToString() ?? "0",
                revision.ToString(),
                string.Join(",", PositionEligibilitySnapshotData.Clean(satisfied)),
                string.Join(",", PositionEligibilitySnapshotData.Clean(blockers)),
                string.Join(",", PositionEligibilitySnapshotData.Clean(redacted)));

            PositionEligibilitySnapshotData snapshot = new PositionEligibilitySnapshotData
            {
                personId = personId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                positionDefinitionId = position?.positionDefinitionId ?? string.Empty,
                employerOrganizationId = position?.organizationId ?? string.Empty,
                authoritativeEligible = blockers.Count == 0,
                perceivedEligible = perceived ? blockers.Count == 0 && redacted.Count == 0 : blockers.Count == 0,
                satisfiedRequirementIds = PositionEligibilitySnapshotData.Clean(satisfied),
                blockingRequirementIds = PositionEligibilitySnapshotData.Clean(blockers),
                redactedRequirementIds = PositionEligibilitySnapshotData.Clean(redacted),
                alternativePositionInstanceIds = PositionEligibilitySnapshotData.Clean(alternatives),
                professionRevision = professions?.Revision ?? 0L,
                trainingRevision = training?.Revision ?? 0L,
                activityRevision = activities?.Revision ?? 0L,
                credentialRevision = credentials?.Revision ?? 0L,
                rankRevision = ranks?.Revision ?? 0L,
                employmentRevision = revision,
                evaluationHash = hash,
                privilegedDiagnostics = privilegedDiagnostics ? string.Join("; ", blockers.Concat(redacted).OrderBy(item => item, StringComparer.Ordinal)) : string.Empty,
                redactedDiagnostics = string.Join("; ", blockers.Where(item => !item.Contains("secret", StringComparison.OrdinalIgnoreCase)).OrderBy(item => item, StringComparer.Ordinal))
            };

            return new PositionEligibilityResult(snapshot);
        }

        public PositionEmploymentOperationResult SubmitApplication(string requestId, string applicantPersonId, string positionInstanceId, PositionEligibilitySnapshotData eligibilitySnapshot, string worldTime, string transactionId)
        {
            long before = revision;
            if (applicationsById.ContainsKey(requestId ?? string.Empty))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.Duplicate, $"Position application '{requestId}' already exists.", before);
            }

            PositionEligibilityResult current = EvaluateEligibility(applicantPersonId, positionInstanceId, privilegedDiagnostics: true);
            PositionEmploymentOperationStatus validation = ValidateEligibilityForCommit(current, eligibilitySnapshot, out string failure);
            if (validation != PositionEmploymentOperationStatus.Succeeded)
            {
                return PositionEmploymentOperationResult.Failure(validation, failure, before, current);
            }

            if (applicationsById.Values.Any(item => string.Equals(item.applicantPersonId, applicantPersonId ?? string.Empty, StringComparison.Ordinal) && string.Equals(item.positionInstanceId, positionInstanceId ?? string.Empty, StringComparison.Ordinal) && IsOpenApplication(item.state)))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.InvalidState, "An active application already exists for this applicant and position.", before, current);
            }

            PositionApplicationData application = new PositionApplicationData
            {
                requestId = requestId ?? string.Empty,
                applicantPersonId = applicantPersonId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                positionDefinitionId = current.Snapshot.positionDefinitionId,
                employerOrganizationId = current.Snapshot.employerOrganizationId,
                requestType = PositionRequestType.Application,
                submissionWorldTime = worldTime ?? string.Empty,
                evaluationSnapshot = eligibilitySnapshot?.Clone() ?? new PositionEligibilitySnapshotData(),
                state = PositionRequestState.Submitted,
                revision = 1L
            };

            applicationsById[application.requestId] = application;
            MarkMutated();
            AddHook(PositionEmploymentHistoryHookKind.PersonApplied, positionInstanceId, application.requestId, string.Empty, string.Empty, applicantPersonId, application.employerOrganizationId, string.Empty, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success("Position application submitted.", before, revision, current, application: application);
        }

        public PositionEmploymentOperationResult OfferPosition(string requestId, string authorityId, string worldTime, string transactionId)
        {
            return UpdateApplicationState(requestId, PositionRequestState.Offered, authorityId, worldTime, "Position offered.", PositionEmploymentHistoryHookKind.OfferMade, transactionId);
        }

        public PositionEmploymentOperationResult AcceptOffer(string requestId, string applicantPersonId, string worldTime, string transactionId)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(requestId ?? string.Empty, out PositionApplicationData application))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingApplication, $"Application '{requestId}' does not exist.", before);
            }

            if (!string.Equals(application.applicantPersonId, applicantPersonId ?? string.Empty, StringComparison.Ordinal) || application.state != PositionRequestState.Offered)
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.InvalidState, "Only the applicant can accept an offered position.", before);
            }

            PositionApplicationData updated = application.Clone();
            updated.state = PositionRequestState.Accepted;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            applicationsById[updated.requestId] = updated;
            MarkMutated();
            return PositionEmploymentOperationResult.Success("Position offer accepted.", before, revision, application: updated);
        }

        public PositionEmploymentOperationResult RejectApplication(string requestId, string authorityId, string reason, string worldTime, string transactionId)
        {
            return UpdateApplicationState(requestId, PositionRequestState.Rejected, authorityId, worldTime, reason, PositionEmploymentHistoryHookKind.Corrected, transactionId);
        }

        public PositionEmploymentOperationResult WithdrawApplication(string requestId, string applicantPersonId, string worldTime, string transactionId)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(requestId ?? string.Empty, out PositionApplicationData application))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingApplication, $"Application '{requestId}' does not exist.", before);
            }

            if (!string.Equals(application.applicantPersonId, applicantPersonId ?? string.Empty, StringComparison.Ordinal))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.UnauthorizedAuthority, "Only the applicant can withdraw this application.", before);
            }

            PositionApplicationData updated = application.Clone();
            updated.state = PositionRequestState.Withdrawn;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            applicationsById[updated.requestId] = updated;
            MarkMutated();
            return PositionEmploymentOperationResult.Success("Application withdrawn.", before, revision, application: updated);
        }

        public PositionEmploymentOperationResult AppointPerson(string employmentId, string requestId, string personId, string positionInstanceId, string authorityId, PositionEligibilitySnapshotData eligibilitySnapshot, string worldTime, string transactionId, EmploymentClassification? classification = null)
        {
            long before = revision;
            if (employmentsById.ContainsKey(employmentId ?? string.Empty))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.Duplicate, $"Employment '{employmentId}' already exists.", before);
            }

            if (!positionsById.TryGetValue(positionInstanceId ?? string.Empty, out PositionInstanceData position))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingPosition, $"Position '{positionInstanceId}' does not exist.", before);
            }

            if (!TryPositionDefinition(position.positionDefinitionId, out PositionDefinition definition))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingDefinition, $"Position definition '{position.positionDefinitionId}' is missing.", before);
            }

            if (!AuthorityAllowed(definition.AuthorityGrantIds, authorityId))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.UnauthorizedAuthority, $"Authority '{authorityId}' cannot appoint to '{definition.Id}'.", before);
            }

            PositionEligibilityResult current = EvaluateEligibility(personId, positionInstanceId, privilegedDiagnostics: true);
            PositionEmploymentOperationStatus validation = ValidateEligibilityForCommit(current, eligibilitySnapshot, out string failure);
            if (validation != PositionEmploymentOperationStatus.Succeeded)
            {
                return PositionEmploymentOperationResult.Failure(validation, failure, before, current);
            }

            if (position.holderPersonIds.Length >= position.maximumHolders)
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.CapacityExceeded, "Position capacity is full.", before, current);
            }

            PositionEmploymentRuntimeSaveData rollback = CreateSaveData();
            PositionInstanceData updatedPosition = position.Clone();
            updatedPosition.holderPersonIds = PositionEligibilitySnapshotData.Clean(updatedPosition.holderPersonIds.Concat(new[] { personId ?? string.Empty }));
            updatedPosition.revision++;
            ApplyPositionDerivedState(updatedPosition);

            EmploymentRecordData employment = new EmploymentRecordData
            {
                employmentId = employmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                employerOrganizationId = position.organizationId,
                positionInstanceId = position.positionInstanceId,
                positionDefinitionId = position.positionDefinitionId,
                classification = classification ?? definition.DefaultClassification,
                state = EmploymentState.Active,
                startWorldTime = worldTime ?? string.Empty,
                appointmentAuthorityId = authorityId ?? string.Empty,
                supervisorPositionInstanceId = updatedPosition.supervisorPositionInstanceId,
                workLocationFoundationId = updatedPosition.locationFoundationId,
                compensationPolicyId = definition.CompensationPolicyId,
                paymentScheduleFoundationId = definition.PaymentScheduleFoundationId,
                wageOrSalaryFoundationId = definition.WageOrSalaryFoundationId,
                benefitsFoundationId = definition.BenefitsFoundationId,
                employerCostCenterFoundationId = definition.EmployerCostCenterFoundationId,
                contractTermsFoundationId = definition.ContractTermsFoundationId,
                commissionOrProfitShareFoundationId = definition.CommissionOrProfitShareFoundationId,
                accessPolicyId = definition.AccessPolicyId,
                revision = 1L
            };

            positionsById[updatedPosition.positionInstanceId] = updatedPosition;
            employmentsById[employment.employmentId] = employment;
            if (!string.IsNullOrWhiteSpace(requestId) && applicationsById.TryGetValue(requestId, out PositionApplicationData application))
            {
                PositionApplicationData updatedApplication = application.Clone();
                updatedApplication.state = PositionRequestState.Approved;
                updatedApplication.reviewerOrAppointingAuthorityId = authorityId ?? string.Empty;
                updatedApplication.decisionWorldTime = worldTime ?? string.Empty;
                updatedApplication.revision++;
                applicationsById[updatedApplication.requestId] = updatedApplication;
            }

            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, knownOrganizationIds, knownAuthorityIds, out string validationFailure))
            {
                RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, knownPersonIds, knownOrganizationIds, knownAuthorityIds, restoring: true);
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.ValidationFailed, validationFailure, before);
            }

            MarkMutated();
            AddHook(PositionEmploymentHistoryHookKind.PersonAppointed, positionInstanceId, requestId, employment.employmentId, string.Empty, personId, position.organizationId, authorityId, worldTime, transactionId);
            AddHook(PositionEmploymentHistoryHookKind.EmploymentBegan, positionInstanceId, requestId, employment.employmentId, string.Empty, personId, position.organizationId, authorityId, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success("Person appointed to position.", before, revision, current, updatedPosition, employment: employment);
        }

        public PositionEmploymentOperationResult AssignDuty(string assignmentId, string employmentId, string dutyDefinitionId, string worldTime, string transactionId)
        {
            long before = revision;
            if (dutiesById.ContainsKey(assignmentId ?? string.Empty))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.Duplicate, $"Duty assignment '{assignmentId}' already exists.", before);
            }

            if (!employmentsById.TryGetValue(employmentId ?? string.Empty, out EmploymentRecordData employment) || !IsActiveEmployment(employment.state))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingEmployment, $"Active employment '{employmentId}' does not exist.", before);
            }

            if (!TryDutyDefinition(dutyDefinitionId, out DutyDefinition dutyDefinition))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingDuty, $"Duty definition '{dutyDefinitionId}' is missing.", before);
            }

            if (!string.Equals(dutyDefinition.PositionDefinitionId, employment.positionDefinitionId, StringComparison.Ordinal))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.InvalidRequest, "Duty definition does not belong to the employment position.", before);
            }

            DutyAssignmentData duty = new DutyAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                employmentId = employment.employmentId,
                positionInstanceId = employment.positionInstanceId,
                dutyDefinitionId = dutyDefinition.Id,
                assignedPersonId = employment.personId,
                startWorldTime = worldTime ?? string.Empty,
                required = dutyDefinition.Required,
                priority = dutyDefinition.Priority,
                state = DutyAssignmentState.Assigned,
                accessPolicyId = dutyDefinition.AccessPolicyId,
                revision = 1L
            };

            EmploymentRecordData updatedEmployment = employment.Clone();
            updatedEmployment.dutyAssignmentIds = PositionEligibilitySnapshotData.Clean(updatedEmployment.dutyAssignmentIds.Concat(new[] { duty.assignmentId }));
            updatedEmployment.revision++;
            dutiesById[duty.assignmentId] = duty;
            employmentsById[updatedEmployment.employmentId] = updatedEmployment;
            MarkMutated();
            AddHook(PositionEmploymentHistoryHookKind.DutyAssigned, employment.positionInstanceId, string.Empty, employment.employmentId, duty.assignmentId, employment.personId, employment.employerOrganizationId, employment.appointmentAuthorityId, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success("Duty assigned.", before, revision, duty: duty, employment: updatedEmployment);
        }

        public PositionEmploymentOperationResult CompleteDutyWithEvidence(string assignmentId, IEnumerable<string> evidenceIds, string worldTime, string transactionId)
        {
            long before = revision;
            if (!dutiesById.TryGetValue(assignmentId ?? string.Empty, out DutyAssignmentData duty))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingDuty, $"Duty assignment '{assignmentId}' does not exist.", before);
            }

            if (!TryDutyDefinition(duty.dutyDefinitionId, out DutyDefinition definition))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingDefinition, $"Duty definition '{duty.dutyDefinitionId}' is missing.", before);
            }

            string[] cleanEvidence = PositionEligibilitySnapshotData.Clean(evidenceIds);
            if (definition.CompletionEvidenceRequired && cleanEvidence.Length == 0)
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingRequirement, "Duty completion requires evidence.", before);
            }

            foreach (string evidenceId in cleanEvidence)
            {
                if (activities == null || !activities.TryGetEvidence(evidenceId, out _))
                {
                    return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.InvalidRequest, $"Duty evidence '{evidenceId}' is not a known professional activity evidence record.", before);
                }
            }

            DutyAssignmentData updated = duty.Clone();
            updated.completionEvidenceReferenceIds = cleanEvidence;
            updated.state = DutyAssignmentState.Completed;
            updated.endWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            dutiesById[updated.assignmentId] = updated;
            MarkMutated();
            return PositionEmploymentOperationResult.Success("Duty completed with evidence.", before, revision, duty: updated);
        }

        public PositionEmploymentOperationResult DelegateDuty(string assignmentId, string delegatePersonId, string supervisorPersonId, string worldTime, string transactionId)
        {
            long before = revision;
            if (!dutiesById.TryGetValue(assignmentId ?? string.Empty, out DutyAssignmentData duty))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingDuty, $"Duty assignment '{assignmentId}' does not exist.", before);
            }

            if (!KnownPerson(delegatePersonId))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingPerson, $"Delegate Person '{delegatePersonId}' is unknown.", before);
            }

            if (!TryDutyDefinition(duty.dutyDefinitionId, out DutyDefinition definition) || !definition.DelegationAllowed)
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.InvalidRequest, "Duty cannot be delegated by policy.", before);
            }

            DutyAssignmentData updated = duty.Clone();
            updated.delegatedToPersonId = delegatePersonId ?? string.Empty;
            updated.supervisorPersonId = supervisorPersonId ?? string.Empty;
            updated.state = DutyAssignmentState.Delegated;
            updated.revision++;
            dutiesById[updated.assignmentId] = updated;
            MarkMutated();
            return PositionEmploymentOperationResult.Success("Duty delegated.", before, revision, duty: updated);
        }

        public PositionEmploymentOperationResult AssignSupervisor(string positionInstanceId, string supervisorPositionInstanceId, string transactionId)
        {
            long before = revision;
            if (!positionsById.TryGetValue(positionInstanceId ?? string.Empty, out PositionInstanceData position) || !positionsById.TryGetValue(supervisorPositionInstanceId ?? string.Empty, out PositionInstanceData supervisor))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingPosition, "Position or supervisor position is missing.", before);
            }

            if (string.Equals(position.positionInstanceId, supervisor.positionInstanceId, StringComparison.Ordinal))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.ReportingCycle, "A position cannot supervise itself.", before);
            }

            PositionInstanceData updated = position.Clone();
            updated.supervisorPositionInstanceId = supervisor.positionInstanceId;
            updated.revision++;
            positionsById[updated.positionInstanceId] = updated;
            if (HasReportingCycle(updated.positionInstanceId))
            {
                positionsById[position.positionInstanceId] = position;
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.ReportingCycle, "Reporting relationship would create a cycle.", before);
            }

            PositionInstanceData updatedSupervisor = supervisor.Clone();
            updatedSupervisor.subordinatePositionInstanceIds = PositionEligibilitySnapshotData.Clean(updatedSupervisor.subordinatePositionInstanceIds.Concat(new[] { updated.positionInstanceId }));
            updatedSupervisor.revision++;
            positionsById[updatedSupervisor.positionInstanceId] = updatedSupervisor;
            MarkMutated();
            return PositionEmploymentOperationResult.Success("Supervisor assigned.", before, revision, position: updated);
        }

        public bool HasActiveAuthority(string personId, string organizationId, string authorityGrantId)
        {
            return employmentsById.Values.Any(employment => string.Equals(employment.personId, personId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(employment.employerOrganizationId, organizationId ?? string.Empty, StringComparison.Ordinal)
                && IsActiveEmployment(employment.state)
                && TryPositionDefinition(employment.positionDefinitionId, out PositionDefinition definition)
                && definition.AuthorityGrantIds.Contains(authorityGrantId ?? string.Empty));
        }

        public PositionEmploymentOperationResult SuspendEmployment(string employmentId, string worldTime, string transactionId)
        {
            return UpdateEmploymentState(employmentId, EmploymentState.Suspended, worldTime, "Employment suspended.", PositionEmploymentHistoryHookKind.EmploymentSuspended, transactionId);
        }

        public PositionEmploymentOperationResult ReinstateEmployment(string employmentId, string worldTime, string transactionId)
        {
            return UpdateEmploymentState(employmentId, EmploymentState.Active, worldTime, "Employment reinstated.", PositionEmploymentHistoryHookKind.Corrected, transactionId, terminal: false);
        }

        public PositionEmploymentOperationResult Resign(string employmentId, string worldTime, string transactionId)
        {
            return EndEmployment(employmentId, EmploymentState.Resigned, worldTime, "Employment resigned.", PositionEmploymentHistoryHookKind.PersonResigned, transactionId);
        }

        public PositionEmploymentOperationResult Dismiss(string employmentId, string worldTime, string transactionId)
        {
            return EndEmployment(employmentId, EmploymentState.Dismissed, worldTime, "Employment dismissed.", PositionEmploymentHistoryHookKind.PersonDismissed, transactionId);
        }

        public PositionEmploymentOperationResult EndContract(string employmentId, string worldTime, string transactionId)
        {
            return EndEmployment(employmentId, EmploymentState.ContractEnded, worldTime, "Contract ended.", PositionEmploymentHistoryHookKind.Corrected, transactionId);
        }

        public PositionEmploymentOperationResult Retire(string employmentId, string worldTime, string transactionId)
        {
            return EndEmployment(employmentId, EmploymentState.Retired, worldTime, "Employment retired.", PositionEmploymentHistoryHookKind.PersonRetired, transactionId);
        }

        public PositionEmploymentOperationResult TransferPerson(string employmentId, string newEmploymentId, string targetPositionInstanceId, string authorityId, PositionEligibilitySnapshotData eligibilitySnapshot, string worldTime, string transactionId)
        {
            long before = revision;
            PositionEmploymentRuntimeSaveData rollback = CreateSaveData();
            if (!employmentsById.TryGetValue(employmentId ?? string.Empty, out EmploymentRecordData current))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingEmployment, $"Employment '{employmentId}' does not exist.", before);
            }

            PositionEligibilityResult preTransferEligibility = EvaluateEligibility(current.personId, targetPositionInstanceId, privilegedDiagnostics: true);
            if (eligibilitySnapshot == null)
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingRequirement, "Eligibility snapshot is required.", before, preTransferEligibility);
            }

            if (!preTransferEligibility.Snapshot.SemanticallyEquals(eligibilitySnapshot))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.StaleEvaluation, "Eligibility snapshot is stale.", before, preTransferEligibility);
            }

            PositionEmploymentOperationResult ended = EndEmployment(employmentId, EmploymentState.Former, worldTime, transactionId);
            if (!ended.Succeeded)
            {
                RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, knownPersonIds, knownOrganizationIds, knownAuthorityIds, restoring: true);
                return ended;
            }

            PositionEligibilityResult postTransferEligibility = EvaluateEligibility(current.personId, targetPositionInstanceId, privilegedDiagnostics: true);
            if (!postTransferEligibility.AuthoritativeEligible)
            {
                RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, knownPersonIds, knownOrganizationIds, knownAuthorityIds, restoring: true);
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingRequirement, $"Eligibility failed: {string.Join(",", postTransferEligibility.BlockingFailures)}", before, postTransferEligibility);
            }

            PositionEmploymentOperationResult appointed = AppointPerson(newEmploymentId, string.Empty, current.personId, targetPositionInstanceId, authorityId, postTransferEligibility.Snapshot, worldTime, transactionId, current.classification);
            if (!appointed.Succeeded)
            {
                RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, knownPersonIds, knownOrganizationIds, knownAuthorityIds, restoring: true);
                return PositionEmploymentOperationResult.Failure(appointed.Status, appointed.Message, before, appointed.Eligibility);
            }

            AddHook(PositionEmploymentHistoryHookKind.PersonTransferred, targetPositionInstanceId, string.Empty, appointed.Employment?.employmentId, string.Empty, current.personId, appointed.Employment?.employerOrganizationId, authorityId, worldTime, transactionId);
            return appointed;
        }

        public PositionEmploymentOperationResult ClosePosition(string positionInstanceId, string worldTime, string transactionId, bool forceEndActiveEmployment = false)
        {
            long before = revision;
            if (!positionsById.TryGetValue(positionInstanceId ?? string.Empty, out PositionInstanceData position))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingPosition, $"Position '{positionInstanceId}' does not exist.", before);
            }

            EmploymentRecordData[] active = employmentsById.Values.Where(item => string.Equals(item.positionInstanceId, positionInstanceId ?? string.Empty, StringComparison.Ordinal) && IsActiveEmployment(item.state)).ToArray();
            if (active.Length > 0 && !forceEndActiveEmployment)
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.InvalidState, "Cannot close a position with active holders without an explicit transition policy.", before);
            }

            foreach (EmploymentRecordData employment in active)
            {
                EndEmployment(employment.employmentId, EmploymentState.Former, worldTime, transactionId);
            }

            PositionInstanceData updated = positionsById[positionInstanceId].Clone();
            updated.state = PositionInstanceState.Closed;
            updated.closedWorldTime = worldTime ?? string.Empty;
            updated.holderPersonIds = Array.Empty<string>();
            updated.revision++;
            positionsById[updated.positionInstanceId] = updated;
            MarkMutated();
            AddHook(PositionEmploymentHistoryHookKind.PositionClosed, positionInstanceId, string.Empty, string.Empty, string.Empty, string.Empty, updated.organizationId, string.Empty, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success("Position closed.", before, revision, position: updated);
        }

        public PositionEmploymentProjection<EmploymentRecordData> ProjectEmployment(string employmentId, PositionEmploymentProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!employmentsById.TryGetValue(employmentId ?? string.Empty, out EmploymentRecordData employment))
            {
                return new PositionEmploymentProjection<EmploymentRecordData>(null, audience, decision, false, true, Array.Empty<string>(), PositionEmploymentInformationSubject.ProtectedFields);
            }

            bool privileged = audience == PositionEmploymentProjectionAudience.Employee || audience == PositionEmploymentProjectionAudience.Employer || audience == PositionEmploymentProjectionAudience.Supervisor || audience == PositionEmploymentProjectionAudience.PrivilegedDebug;
            bool denied = !privileged && decision != null && decision.Denied;
            if (denied)
            {
                return new PositionEmploymentProjection<EmploymentRecordData>(null, audience, decision, false, true, Array.Empty<string>(), PositionEmploymentInformationSubject.ProtectedFields);
            }

            EmploymentRecordData projected = employment.Clone();
            bool redacted = !privileged && (decision != null || IsSecretPosition(employment.positionDefinitionId));
            if (redacted)
            {
                projected.personId = string.Empty;
                projected.supervisorPersonId = string.Empty;
                projected.appointmentAuthorityId = string.Empty;
                projected.dutyAssignmentIds = Array.Empty<string>();
                projected.provenance = string.Empty;
                projected.compensationPolicyId = string.Empty;
                projected.paymentScheduleFoundationId = string.Empty;
                projected.wageOrSalaryFoundationId = string.Empty;
                projected.benefitsFoundationId = string.Empty;
            }

            return new PositionEmploymentProjection<EmploymentRecordData>(projected, audience, decision, redacted, false, redacted ? new[] { "position", "state", "organization" } : new[] { "all" }, redacted ? PositionEmploymentInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public PositionEmploymentProjection<DutyAssignmentData> ProjectDuty(string assignmentId, PositionEmploymentProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!dutiesById.TryGetValue(assignmentId ?? string.Empty, out DutyAssignmentData duty))
            {
                return new PositionEmploymentProjection<DutyAssignmentData>(null, audience, decision, false, true, Array.Empty<string>(), PositionEmploymentInformationSubject.ProtectedFields);
            }

            bool privileged = audience == PositionEmploymentProjectionAudience.Employee || audience == PositionEmploymentProjectionAudience.Employer || audience == PositionEmploymentProjectionAudience.Supervisor || audience == PositionEmploymentProjectionAudience.PrivilegedDebug;
            bool denied = !privileged && decision != null && decision.Denied;
            if (denied)
            {
                return new PositionEmploymentProjection<DutyAssignmentData>(null, audience, decision, false, true, Array.Empty<string>(), PositionEmploymentInformationSubject.ProtectedFields);
            }

            DutyAssignmentData projected = duty.Clone();
            bool redacted = !privileged && (decision != null || IsSecretDuty(duty.dutyDefinitionId));
            if (redacted)
            {
                projected.assignedPersonId = string.Empty;
                projected.delegatedToPersonId = string.Empty;
                projected.supervisorPersonId = string.Empty;
                projected.completionEvidenceReferenceIds = Array.Empty<string>();
                projected.provenance = string.Empty;
            }

            return new PositionEmploymentProjection<DutyAssignmentData>(projected, audience, decision, redacted, false, redacted ? new[] { "duty", "state" } : new[] { "all" }, redacted ? PositionEmploymentInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public PositionEmploymentRuntimeSaveData CreateSaveData()
        {
            return new PositionEmploymentRuntimeSaveData
            {
                revision = revision,
                positions = positionsById.Values.OrderBy(item => item.positionInstanceId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                applications = applicationsById.Values.OrderBy(item => item.requestId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                employments = employmentsById.Values.OrderBy(item => item.employmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                duties = dutiesById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public PositionEmploymentOperationResult RestoreFromSaveData(PositionEmploymentRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, ProfessionalRankRuntime rankRuntime, IEnumerable<string> persons, IEnumerable<string> organizations, IEnumerable<string> authorities, bool restoring = true)
        {
            PositionEmploymentRuntimeSaveData incoming = saveData?.Clone() ?? new PositionEmploymentRuntimeSaveData();
            PositionEmploymentRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry previousRegistry = registry;
            PersonProfessionRuntime previousProfessions = professions;
            TrainingRuntime previousTraining = training;
            ProfessionalActivityRuntime previousActivities = activities;
            CredentialRuntime previousCredentials = credentials;
            ProfessionalRankRuntime previousRanks = ranks;
            HashSet<string> previousPersons = new HashSet<string>(knownPersonIds, StringComparer.Ordinal);
            HashSet<string> previousOrganizations = new HashSet<string>(knownOrganizationIds, StringComparer.Ordinal);
            HashSet<string> previousAuthorities = new HashSet<string>(knownAuthorityIds, StringComparer.Ordinal);

            Configure(definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, rankRuntime, persons, organizations, authorities);
            if (!ValidateAll(incoming, registry, knownPersonIds, knownOrganizationIds, knownAuthorityIds, out string failure))
            {
                Configure(previousRegistry, previousProfessions, previousTraining, previousActivities, previousCredentials, previousRanks, previousPersons, previousOrganizations, previousAuthorities);
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.CorruptSave, failure, revision);
            }

            positionsById.Clear();
            applicationsById.Clear();
            employmentsById.Clear();
            dutiesById.Clear();
            historyHooks.Clear();

            foreach (PositionInstanceData position in incoming.positions ?? new List<PositionInstanceData>())
            {
                positionsById[position.positionInstanceId] = position.Clone();
            }

            foreach (PositionApplicationData application in incoming.applications ?? new List<PositionApplicationData>())
            {
                applicationsById[application.requestId] = application.Clone();
            }

            foreach (EmploymentRecordData employment in incoming.employments ?? new List<EmploymentRecordData>())
            {
                employmentsById[employment.employmentId] = employment.Clone();
            }

            foreach (DutyAssignmentData duty in incoming.duties ?? new List<DutyAssignmentData>())
            {
                dutiesById[duty.assignmentId] = duty.Clone();
            }

            revision = Math.Max(0L, incoming.revision);
            dirty = false;

            if (!ValidateAll(CreateSaveData(), registry, knownPersonIds, knownOrganizationIds, knownAuthorityIds, out string validationFailure))
            {
                RestoreFromSaveData(rollback, previousRegistry, previousProfessions, previousTraining, previousActivities, previousCredentials, previousRanks, previousPersons, previousOrganizations, previousAuthorities, restoring);
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.ValidationFailed, validationFailure, revision);
            }

            return PositionEmploymentOperationResult.Success(restoring ? "Position and employment state restored." : "Position and employment state loaded.", revision, revision);
        }

        public static bool ValidateSaveData(PositionEmploymentRuntimeSaveData saveData, DefinitionRegistry registry, PersonProfessionRuntime professions, TrainingRuntime training, ProfessionalActivityRuntime activities, CredentialRuntime credentials, ProfessionalRankRuntime ranks, IEnumerable<string> persons, IEnumerable<string> organizations, IEnumerable<string> authorities, out string failure)
        {
            PositionEmploymentRuntime runtime = new PositionEmploymentRuntime();
            runtime.Configure(registry, professions, training, activities, credentials, ranks, persons, organizations, authorities);
            return runtime.ValidateAll(saveData ?? new PositionEmploymentRuntimeSaveData(), registry, new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal), new HashSet<string>((organizations ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal), new HashSet<string>((authorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal), out failure);
        }

        private PositionEmploymentOperationResult UpdateApplicationState(string requestId, PositionRequestState state, string authorityId, string worldTime, string reason, PositionEmploymentHistoryHookKind hookKind, string transactionId)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(requestId ?? string.Empty, out PositionApplicationData application))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingApplication, $"Application '{requestId}' does not exist.", before);
            }

            if (!KnownAuthority(authorityId))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.UnauthorizedAuthority, $"Authority '{authorityId}' is unknown.", before);
            }

            PositionApplicationData updated = application.Clone();
            updated.state = state;
            updated.reviewerOrAppointingAuthorityId = authorityId ?? string.Empty;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.decisionReason = reason ?? string.Empty;
            updated.revision++;
            applicationsById[updated.requestId] = updated;
            MarkMutated();
            AddHook(hookKind, updated.positionInstanceId, updated.requestId, string.Empty, string.Empty, updated.applicantPersonId, updated.employerOrganizationId, authorityId, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success(reason ?? "Application updated.", before, revision, application: updated);
        }

        private PositionEmploymentOperationResult UpdateEmploymentState(string employmentId, EmploymentState state, string worldTime, string message, PositionEmploymentHistoryHookKind hookKind, string transactionId, bool terminal = true)
        {
            long before = revision;
            if (!employmentsById.TryGetValue(employmentId ?? string.Empty, out EmploymentRecordData employment))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingEmployment, $"Employment '{employmentId}' does not exist.", before);
            }

            EmploymentRecordData updated = employment.Clone();
            updated.state = state;
            if (terminal)
            {
                updated.endWorldTime = worldTime ?? string.Empty;
            }

            updated.revision++;
            employmentsById[updated.employmentId] = updated;
            MarkMutated();
            AddHook(hookKind, updated.positionInstanceId, string.Empty, updated.employmentId, string.Empty, updated.personId, updated.employerOrganizationId, updated.appointmentAuthorityId, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success(message, before, revision, employment: updated);
        }

        private PositionEmploymentOperationResult EndEmployment(string employmentId, EmploymentState state, string worldTime, string transactionId)
        {
            return EndEmployment(employmentId, state, worldTime, "Employment ended.", PositionEmploymentHistoryHookKind.Corrected, transactionId);
        }

        private PositionEmploymentOperationResult EndEmployment(string employmentId, EmploymentState state, string worldTime, string message, PositionEmploymentHistoryHookKind hookKind, string transactionId)
        {
            long before = revision;
            if (!employmentsById.TryGetValue(employmentId ?? string.Empty, out EmploymentRecordData employment))
            {
                return PositionEmploymentOperationResult.Failure(PositionEmploymentOperationStatus.MissingEmployment, $"Employment '{employmentId}' does not exist.", before);
            }

            EmploymentRecordData updated = employment.Clone();
            if (IsTerminalEmployment(updated.state) && updated.state == state)
            {
                return PositionEmploymentOperationResult.Success("Employment terminal state already applied.", before, before, employment: updated, duplicate: true);
            }

            updated.state = state;
            updated.endWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            employmentsById[updated.employmentId] = updated;
            if (positionsById.TryGetValue(updated.positionInstanceId, out PositionInstanceData position))
            {
                PositionInstanceData updatedPosition = position.Clone();
                updatedPosition.holderPersonIds = PositionEligibilitySnapshotData.Clean(updatedPosition.holderPersonIds.Where(id => !string.Equals(id, updated.personId, StringComparison.Ordinal)));
                updatedPosition.revision++;
                ApplyPositionDerivedState(updatedPosition);
                positionsById[updatedPosition.positionInstanceId] = updatedPosition;
            }

            foreach (DutyAssignmentData duty in dutiesById.Values.Where(item => string.Equals(item.employmentId, updated.employmentId, StringComparison.Ordinal) && item.state != DutyAssignmentState.Completed && item.state != DutyAssignmentState.Archived).ToArray())
            {
                DutyAssignmentData closedDuty = duty.Clone();
                closedDuty.state = DutyAssignmentState.Suspended;
                closedDuty.endWorldTime = worldTime ?? string.Empty;
                closedDuty.revision++;
                dutiesById[closedDuty.assignmentId] = closedDuty;
            }

            MarkMutated();
            AddHook(hookKind, updated.positionInstanceId, string.Empty, updated.employmentId, string.Empty, updated.personId, updated.employerOrganizationId, updated.appointmentAuthorityId, worldTime, transactionId);
            return PositionEmploymentOperationResult.Success(message, before, revision, employment: updated);
        }

        private void EvaluateDefinitionRequirements(string personId, PositionDefinition definition, bool perceived, bool privilegedDiagnostics, List<string> satisfied, List<string> blockers, List<string> redacted)
        {
            foreach (string professionId in definition.RelatedProfessionIds)
            {
                bool hasProfession = professions != null && professions.QueryByProfession(professionId, activeOnly: true).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal));
                AddRequirement(hasProfession, $"profession:{professionId}", satisfied, blockers);
            }

            foreach (string specializationId in definition.RelatedSpecializationIds)
            {
                bool hasSpecialization = professions != null && professions.QueryByPerson(personId, activeOnly: true).Any(item => item.Data.specializationIds.Contains(specializationId));
                AddRequirement(hasSpecialization, $"specialization:{specializationId}", satisfied, blockers);
            }

            foreach (string rankId in definition.RequiredRankDefinitionIds)
            {
                bool hasRank = ranks != null && ranks.HasActiveRank(personId, rankId);
                AddRequirement(hasRank, $"rank:{rankId}", satisfied, blockers);
            }

            foreach (string credentialId in definition.RequiredCredentialDefinitionIds)
            {
                bool hasCredential = credentials != null && credentials.QueryByRecipient(personId, activeOnly: true).Any(item => string.Equals(item.credentialDefinitionId, credentialId, StringComparison.Ordinal));
                AddRequirement(hasCredential, $"credential:{credentialId}", satisfied, blockers);
            }

            foreach (string trainingId in definition.RequiredTrainingProgramIds)
            {
                bool hasTraining = training != null && training.QueryByPerson(personId).Any(item => string.Equals(item.ProgramId, trainingId, StringComparison.Ordinal) && item.State == TrainingEnrollmentState.Completed);
                AddRequirement(hasTraining, $"training:{trainingId}", satisfied, blockers);
            }

            ProfessionalExperienceRequirementData experience = definition.ExperienceRequirement;
            if (experience != null && experience.minimumValidatedActivities > 0)
            {
                bool hasExperience = activities != null && activities.EvaluateExperienceRequirement(personId, experience, out _);
                AddRequirement(hasExperience, $"experience:{experience.professionId}:{experience.minimumValidatedActivities}", satisfied, blockers);
            }

            foreach (string skillId in definition.RequiredSkillIds)
            {
                AddRequirement(false, $"skill:{skillId}", satisfied, blockers);
            }

            foreach (string knowledgeId in definition.RequiredKnowledgeFactIds)
            {
                if (perceived && !privilegedDiagnostics)
                {
                    redacted.Add($"knowledge:{knowledgeId}");
                }
                else
                {
                    blockers.Add($"knowledge:{knowledgeId}");
                }
            }

            foreach (string capabilityId in definition.RequiredCapabilityIds)
            {
                blockers.Add($"capability:{capabilityId}");
            }
        }

        private void EvaluateEmploymentConflicts(string personId, PositionInstanceData targetPosition, PositionDefinition targetDefinition, List<string> satisfied, List<string> blockers)
        {
            EmploymentRecordData[] active = employmentsById.Values.Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal) && IsActiveEmployment(item.state)).ToArray();
            if (active.Length == 0)
            {
                satisfied.Add("employment-conflict:none");
                return;
            }

            if (active.Any(item => string.Equals(item.positionInstanceId, targetPosition.positionInstanceId, StringComparison.Ordinal)))
            {
                blockers.Add("employment-conflict:same-position");
            }

            if (targetDefinition.ExclusiveFullTime && targetDefinition.DefaultClassification == EmploymentClassification.FullTime && active.Any(item => item.classification == EmploymentClassification.FullTime && TryPositionDefinition(item.positionDefinitionId, out PositionDefinition existingDefinition) && existingDefinition.ExclusiveFullTime))
            {
                blockers.Add("employment-conflict:exclusive-full-time");
            }
            else
            {
                satisfied.Add("employment-conflict:compatible");
            }
        }

        private PositionEmploymentOperationStatus ValidateEligibilityForCommit(PositionEligibilityResult current, PositionEligibilitySnapshotData expected, out string failure)
        {
            if (current == null || expected == null)
            {
                failure = "Eligibility snapshot is required.";
                return PositionEmploymentOperationStatus.MissingRequirement;
            }

            if (!current.AuthoritativeEligible)
            {
                failure = $"Eligibility failed: {string.Join(",", current.BlockingFailures)}";
                return PositionEmploymentOperationStatus.MissingRequirement;
            }

            if (!current.Snapshot.SemanticallyEquals(expected))
            {
                failure = "Eligibility snapshot is stale.";
                return PositionEmploymentOperationStatus.StaleEvaluation;
            }

            failure = string.Empty;
            return PositionEmploymentOperationStatus.Succeeded;
        }

        private PositionEmploymentOperationStatus ValidatePositionForMutation(PositionInstanceData position, out string failure)
        {
            if (position == null || string.IsNullOrWhiteSpace(position.positionInstanceId))
            {
                failure = "Position instance ID is required.";
                return PositionEmploymentOperationStatus.InvalidRequest;
            }

            if (!TryPositionDefinition(position.positionDefinitionId, out PositionDefinition definition))
            {
                failure = $"Position definition '{position?.positionDefinitionId}' is missing.";
                return PositionEmploymentOperationStatus.MissingDefinition;
            }

            if (!KnownOrganization(position.organizationId))
            {
                failure = $"Organization '{position.organizationId}' is unknown.";
                return PositionEmploymentOperationStatus.MissingOrganization;
            }

            if (!string.IsNullOrWhiteSpace(definition.RequiredOrganizationTypeId) && !string.Equals(definition.RequiredOrganizationTypeId, position.organizationTypeId ?? string.Empty, StringComparison.Ordinal))
            {
                failure = $"Position requires organization type '{definition.RequiredOrganizationTypeId}'.";
                return PositionEmploymentOperationStatus.InvalidRequest;
            }

            if (position.maximumHolders <= 0 || position.maximumHolders > definition.MaximumSimultaneousHolders)
            {
                failure = "Position holder capacity is invalid for its definition.";
                return PositionEmploymentOperationStatus.CapacityExceeded;
            }

            if (position.holderPersonIds.Length > position.maximumHolders)
            {
                failure = "Position holder count exceeds capacity.";
                return PositionEmploymentOperationStatus.CapacityExceeded;
            }

            foreach (string holder in position.holderPersonIds)
            {
                if (!KnownPerson(holder))
                {
                    failure = $"Position holder '{holder}' is unknown.";
                    return PositionEmploymentOperationStatus.MissingPerson;
                }
            }

            failure = string.Empty;
            return PositionEmploymentOperationStatus.Succeeded;
        }

        private bool ValidateAll(PositionEmploymentRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, ISet<string> persons, ISet<string> organizations, ISet<string> authorities, out string failure)
        {
            saveData ??= new PositionEmploymentRuntimeSaveData();
            if (saveData.schemaVersion != PositionEmploymentRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported position employment schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> positionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PositionInstanceData position in saveData.positions ?? new List<PositionInstanceData>())
            {
                if (position == null || string.IsNullOrWhiteSpace(position.positionInstanceId) || !positionIds.Add(position.positionInstanceId))
                {
                    failure = "Position save data has a missing or duplicate position ID.";
                    return false;
                }

                if (!TryDefinition(definitionRegistry, position.positionDefinitionId, out PositionDefinition definition))
                {
                    failure = $"Position save data references missing definition '{position.positionDefinitionId}'.";
                    return false;
                }

                if (!organizations.Contains(position.organizationId ?? string.Empty))
                {
                    failure = $"Position save data references unknown organization '{position.organizationId}'.";
                    return false;
                }

                if (position.holderPersonIds.Length > Math.Max(1, position.maximumHolders) || position.maximumHolders > definition.MaximumSimultaneousHolders)
                {
                    failure = $"Position '{position.positionInstanceId}' has invalid capacity.";
                    return false;
                }
            }

            foreach (PositionInstanceData position in saveData.positions ?? new List<PositionInstanceData>())
            {
                if (!string.IsNullOrWhiteSpace(position.supervisorPositionInstanceId) && !positionIds.Contains(position.supervisorPositionInstanceId))
                {
                    failure = $"Position '{position.positionInstanceId}' references missing supervisor position '{position.supervisorPositionInstanceId}'.";
                    return false;
                }
            }

            if (HasCycle(saveData.positions ?? new List<PositionInstanceData>()))
            {
                failure = "Position save data contains a reporting cycle.";
                return false;
            }

            HashSet<string> applicationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PositionApplicationData application in saveData.applications ?? new List<PositionApplicationData>())
            {
                if (application == null || string.IsNullOrWhiteSpace(application.requestId) || !applicationIds.Add(application.requestId))
                {
                    failure = "Position application save data has a missing or duplicate request ID.";
                    return false;
                }

                if (!persons.Contains(application.applicantPersonId ?? string.Empty) || !positionIds.Contains(application.positionInstanceId ?? string.Empty))
                {
                    failure = $"Position application '{application.requestId}' has invalid applicant or position reference.";
                    return false;
                }
            }

            HashSet<string> employmentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EmploymentRecordData employment in saveData.employments ?? new List<EmploymentRecordData>())
            {
                if (employment == null || string.IsNullOrWhiteSpace(employment.employmentId) || !employmentIds.Add(employment.employmentId))
                {
                    failure = "Employment save data has a missing or duplicate employment ID.";
                    return false;
                }

                if (!persons.Contains(employment.personId ?? string.Empty) || !organizations.Contains(employment.employerOrganizationId ?? string.Empty) || !positionIds.Contains(employment.positionInstanceId ?? string.Empty))
                {
                    failure = $"Employment '{employment.employmentId}' has invalid Person, organization, or position reference.";
                    return false;
                }
            }

            HashSet<string> dutyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DutyAssignmentData duty in saveData.duties ?? new List<DutyAssignmentData>())
            {
                if (duty == null || string.IsNullOrWhiteSpace(duty.assignmentId) || !dutyIds.Add(duty.assignmentId))
                {
                    failure = "Duty save data has a missing or duplicate assignment ID.";
                    return false;
                }

                if (!employmentIds.Contains(duty.employmentId ?? string.Empty) || !positionIds.Contains(duty.positionInstanceId ?? string.Empty) || !TryDefinition(definitionRegistry, duty.dutyDefinitionId, out DutyDefinition _))
                {
                    failure = $"Duty '{duty.assignmentId}' has invalid employment, position, or duty reference.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private bool KnownPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        }

        private bool KnownOrganization(string organizationId)
        {
            return !string.IsNullOrWhiteSpace(organizationId) && (knownOrganizationIds.Count == 0 || knownOrganizationIds.Contains(organizationId));
        }

        private bool KnownAuthority(string authorityId)
        {
            return !string.IsNullOrWhiteSpace(authorityId) && (knownAuthorityIds.Count == 0 || knownAuthorityIds.Contains(authorityId));
        }

        private bool AuthorityAllowed(IReadOnlyList<string> allowedAuthorities, string authorityId)
        {
            string clean = authorityId ?? string.Empty;
            return KnownAuthority(clean) && (allowedAuthorities == null || allowedAuthorities.Count == 0 || allowedAuthorities.Contains(clean));
        }

        private bool TryPositionDefinition(string id, out PositionDefinition definition)
        {
            return TryDefinition(registry, id, out definition);
        }

        private bool TryDutyDefinition(string id, out DutyDefinition definition)
        {
            return TryDefinition(registry, id, out definition);
        }

        private static bool TryDefinition<T>(DefinitionRegistry registry, string id, out T definition) where T : class, IGameDefinition
        {
            if (registry != null && !string.IsNullOrWhiteSpace(id) && registry.TryGet(id, out T found))
            {
                definition = found;
                return true;
            }

            definition = null;
            return false;
        }

        private static void AddRequirement(bool satisfiedValue, string requirementId, List<string> satisfied, List<string> blockers)
        {
            if (satisfiedValue)
            {
                satisfied.Add(requirementId);
            }
            else
            {
                blockers.Add(requirementId);
            }
        }

        private static bool IsOpenPosition(PositionInstanceState state)
        {
            return state == PositionInstanceState.Planned || state == PositionInstanceState.Vacant || state == PositionInstanceState.RecruitingFoundation || state == PositionInstanceState.PartiallyFilled;
        }

        private static bool IsActiveEmployment(EmploymentState state)
        {
            return state == EmploymentState.Active || state == EmploymentState.Probationary || state == EmploymentState.Accepted || state == EmploymentState.OnLeaveFoundation;
        }

        private static bool IsOpenApplication(PositionRequestState state)
        {
            return state == PositionRequestState.Draft || state == PositionRequestState.Submitted || state == PositionRequestState.UnderReview || state == PositionRequestState.Offered || state == PositionRequestState.Accepted;
        }

        private static bool IsTerminalEmployment(EmploymentState state)
        {
            return state == EmploymentState.Resigned || state == EmploymentState.Dismissed || state == EmploymentState.ContractEnded || state == EmploymentState.Retired || state == EmploymentState.Former || state == EmploymentState.Cancelled || state == EmploymentState.DeceasedFoundation;
        }

        private void ApplyPositionDerivedState(PositionInstanceData position)
        {
            if (position.state == PositionInstanceState.Closed || position.state == PositionInstanceState.Abolished || position.state == PositionInstanceState.Suspended || position.state == PositionInstanceState.Frozen || position.state == PositionInstanceState.Invalid)
            {
                return;
            }

            int holders = position.holderPersonIds?.Length ?? 0;
            position.state = holders == 0 ? PositionInstanceState.Vacant : holders >= position.maximumHolders ? PositionInstanceState.Filled : PositionInstanceState.PartiallyFilled;
        }

        private void MarkMutated()
        {
            revision++;
            dirty = true;
        }

        private void AddHook(PositionEmploymentHistoryHookKind kind, string positionId, string applicationId, string employmentId, string dutyId, string personId, string organizationId, string authorityId, string worldTime, string transactionId)
        {
            historyHooks.Add(new PositionEmploymentHistoryHookData
            {
                kind = kind,
                positionInstanceId = positionId ?? string.Empty,
                applicationId = applicationId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                dutyAssignmentId = dutyId ?? string.Empty,
                personId = personId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            });
        }

        private bool IsSecretPosition(string positionDefinitionId)
        {
            return TryPositionDefinition(positionDefinitionId, out PositionDefinition definition) && definition.Secret;
        }

        private bool IsSecretDuty(string dutyDefinitionId)
        {
            return TryDutyDefinition(dutyDefinitionId, out DutyDefinition definition) && definition.Secret;
        }

        private bool HasReportingCycle(string startPositionId)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            string current = startPositionId;
            while (!string.IsNullOrWhiteSpace(current) && positionsById.TryGetValue(current, out PositionInstanceData position))
            {
                if (!visited.Add(current))
                {
                    return true;
                }

                current = position.supervisorPositionInstanceId;
            }

            return false;
        }

        private static bool HasCycle(IEnumerable<PositionInstanceData> positions)
        {
            Dictionary<string, string> supervisors = (positions ?? Array.Empty<PositionInstanceData>())
                .Where(item => item != null)
                .ToDictionary(item => item.positionInstanceId ?? string.Empty, item => item.supervisorPositionInstanceId ?? string.Empty, StringComparer.Ordinal);

            foreach (string start in supervisors.Keys)
            {
                HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
                string current = start;
                while (!string.IsNullOrWhiteSpace(current) && supervisors.TryGetValue(current, out string supervisor))
                {
                    if (!visited.Add(current))
                    {
                        return true;
                    }

                    current = supervisor;
                }
            }

            return false;
        }

        private static bool Equivalent(PositionInstanceData left, PositionInstanceData right)
        {
            return left != null
                && right != null
                && string.Equals(left.positionInstanceId, right.positionInstanceId, StringComparison.Ordinal)
                && string.Equals(left.positionDefinitionId, right.positionDefinitionId, StringComparison.Ordinal)
                && string.Equals(left.organizationId, right.organizationId, StringComparison.Ordinal)
                && left.maximumHolders == right.maximumHolders
                && left.holderPersonIds.SequenceEqual(right.holderPersonIds);
        }
    }
}
