using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Social.Family;

namespace UnityIsekaiGame.Governments
{
    public sealed partial class GovernmentRuntime
    {
        private readonly Dictionary<string, GovernmentOfficeTenureRecordData> officeTenuresById = new Dictionary<string, GovernmentOfficeTenureRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentElectionRecordData> electionsById = new Dictionary<string, GovernmentElectionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentPermitRecordData> permitsById = new Dictionary<string, GovernmentPermitRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentFiscalMandateRecordData> fiscalMandatesById = new Dictionary<string, GovernmentFiscalMandateRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentLegitimacyRecordData> legitimacyAssessmentsById = new Dictionary<string, GovernmentLegitimacyRecordData>(StringComparer.Ordinal);

        public int OfficeTenureCount => officeTenuresById.Count;
        public int ElectionCount => electionsById.Count;
        public int PermitCount => permitsById.Count;
        public int FiscalMandateCount => fiscalMandatesById.Count;
        public int LegitimacyAssessmentCount => legitimacyAssessmentsById.Count;

        public IReadOnlyList<GovernmentOfficeTenureRecordData> OfficeTenures => officeTenuresById.Values.OrderBy(value => value.startWorldTime).ThenBy(value => value.tenureId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentElectionRecordData> Elections => electionsById.Values.OrderBy(value => value.scheduledWorldTime).ThenBy(value => value.electionId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentPermitRecordData> Permits => permitsById.Values.OrderBy(value => value.issuedWorldTime).ThenBy(value => value.permitId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentFiscalMandateRecordData> FiscalMandates => fiscalMandatesById.Values.OrderBy(value => value.effectiveWorldTime).ThenBy(value => value.mandateId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentLegitimacyRecordData> LegitimacyAssessments => legitimacyAssessmentsById.Values.OrderBy(value => value.assessedWorldTime).ThenBy(value => value.assessmentId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();

        public bool TryGetOfficeTenure(string tenureId, out GovernmentOfficeTenureRecordData record) => TryClone(officeTenuresById, tenureId, out record);
        public bool TryGetElection(string electionId, out GovernmentElectionRecordData record) => TryClone(electionsById, electionId, out record);
        public bool TryGetPermit(string permitId, out GovernmentPermitRecordData record) => TryClone(permitsById, permitId, out record);
        public bool TryGetFiscalMandate(string mandateId, out GovernmentFiscalMandateRecordData record) => TryClone(fiscalMandatesById, mandateId, out record);
        public bool TryGetLegitimacyAssessment(string assessmentId, out GovernmentLegitimacyRecordData record) => TryClone(legitimacyAssessmentsById, assessmentId, out record);

        public GovernmentOfficeTenureRecordData GetActiveOfficeholder(string governmentId, string officeDefinitionId, double worldTime)
        {
            governmentId = PoliticalModelUtility.Normalize(governmentId);
            officeDefinitionId = PoliticalModelUtility.Normalize(officeDefinitionId);
            return officeTenuresById.Values
                .Where(value => value.governmentId == governmentId && value.officeDefinitionId == officeDefinitionId && value.IsActiveAt(worldTime))
                .OrderByDescending(value => value.startWorldTime)
                .ThenBy(value => value.tenureId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .FirstOrDefault();
        }

        public PoliticalOperationResult InstallOfficeholder(GovernmentOfficeInstallationRequest request)
        {
            request ??= new GovernmentOfficeInstallationRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string tenureId = PoliticalModelUtility.Normalize(request.tenureId);
            if (TryDuplicate(request.transactionId, tenureId, "install-government-officeholder", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(tenureId) || string.IsNullOrWhiteSpace(request.holderPersonId)) return Fail(PoliticalOperationCode.InvalidRequest, "Office tenure and holder IDs are required.", before);
            if (officeTenuresById.ContainsKey(tenureId)) return Fail(PoliticalOperationCode.Conflict, $"Office tenure '{tenureId}' already exists.", before);
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (!governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (!TryGetDefinition(request.officeDefinitionId, out GovernmentOfficeDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Government office definition '{request.officeDefinitionId}' is missing.", before);
            if (!definition.Allows(request.selectionMethod)) return Fail(PoliticalOperationCode.InvalidState, $"Selection method '{request.selectionMethod}' is not allowed for '{definition.DisplayName}'.", before);
            if (request.state == GovernmentOfficeTenureState.Unknown || request.state == GovernmentOfficeTenureState.Ended || request.state == GovernmentOfficeTenureState.Historical) return Fail(PoliticalOperationCode.InvalidState, "Initial office tenure state is invalid.", before);
            if (request.state == GovernmentOfficeTenureState.Acting && !definition.PermitsActingHolder) return Fail(PoliticalOperationCode.InvalidState, $"'{definition.DisplayName}' does not permit an acting holder.", before);
            string personId = PoliticalModelUtility.Normalize(request.holderPersonId);
            if (!knownPersonIds.Contains(personId)) return Fail(PoliticalOperationCode.InvalidReference, $"Officeholder person '{personId}' is unknown.", before);
            string lineageAnchorPersonId = PoliticalModelUtility.Normalize(request.lineageAnchorPersonId);
            if (definition.EligibilityRule == GovernmentOfficeEligibilityRule.DirectDescendantOfReigningMonarch)
            {
                if (!knownPersonIds.Contains(lineageAnchorPersonId)) return Fail(PoliticalOperationCode.InvalidReference, "A known reigning monarch is required as the lineage anchor.", before);
                bool reigningMonarch = officeTenuresById.Values.Any(value => value.officeDefinitionId == definition.SuperiorOfficeDefinitionId && value.holderPersonId == lineageAnchorPersonId && value.IsActiveAt(request.worldTime));
                if (!reigningMonarch) return Fail(PoliticalOperationCode.InvalidState, "The lineage anchor is not the active holder of the superior royal office.", before);
                if (familyRelationships == null) return Fail(PoliticalOperationCode.InvalidRequest, "Family relationships must be configured before lineage-restricted offices can be assigned.", before);
                KinshipPathResult lineage = familyRelationships.ResolveKinship(lineageAnchorPersonId, personId, privileged: true);
                bool directBiologicalDescent = lineage.LineageKind == KinshipLineageKind.Biological
                    && lineage.Classification is KinshipClassification.BiologicalParent or KinshipClassification.Grandchild or KinshipClassification.Descendant;
                if (!directBiologicalDescent) return Fail(PoliticalOperationCode.AccessDenied, $"'{definition.DisplayName}' requires direct biological descent from the reigning monarch.", before);
            }
            string organizationOfficeId = PoliticalModelUtility.Normalize(request.organizationOfficeId);
            if (memberships == null || !memberships.TryGetOffice(organizationOfficeId, out OrganizationOfficeSnapshot organizationOffice)) return Fail(PoliticalOperationCode.InvalidReference, $"Organization office '{organizationOfficeId}' is missing.", before);
            if (!string.Equals(organizationOffice.Data.officeDefinitionId, definition.OrganizationOfficeDefinitionId, StringComparison.Ordinal)) return Fail(PoliticalOperationCode.InvalidReference, $"Organization office '{organizationOfficeId}' does not use definition '{definition.OrganizationOfficeDefinitionId}'.", before);
            string assignmentId = PoliticalModelUtility.Normalize(request.officeAssignmentId);
            OrganizationOfficeAssignmentRecordData assignment = organizationOffice.Assignments.FirstOrDefault(value => value.officeAssignmentId == assignmentId);
            bool assignmentStateMatches = request.state == GovernmentOfficeTenureState.Designated
                ? assignment?.state == OrganizationOfficeAssignmentState.Proposed
                : assignment?.IsActive == true;
            if (!assignmentStateMatches || !memberships.TryGetMembership(assignment.membershipId, out OrganizationMembershipSnapshot membership) || membership.PersonId != personId) return Fail(PoliticalOperationCode.InvalidReference, request.state == GovernmentOfficeTenureState.Designated ? "A proposed organization office assignment belonging to the designated successor is required." : "The active organization office assignment does not belong to the requested holder.", before);
            GovernmentOfficeTenureRecordData active = GetActiveOfficeholder(governmentId, definition.Id, request.worldTime);
            if (active != null && request.state != GovernmentOfficeTenureState.Designated && !request.replaceCurrent) return Fail(PoliticalOperationCode.Conflict, $"'{definition.DisplayName}' already has an active holder.", before);
            if (request.state == GovernmentOfficeTenureState.Designated && officeTenuresById.Values.Any(value => value.governmentId == governmentId && value.officeDefinitionId == definition.Id && value.state == GovernmentOfficeTenureState.Designated)) return Fail(PoliticalOperationCode.Conflict, $"'{definition.DisplayName}' already has a designated successor.", before);

            GovernmentOfficeTenureRecordData record = new GovernmentOfficeTenureRecordData
            {
                tenureId = tenureId,
                governmentId = governmentId,
                officeDefinitionId = definition.Id,
                organizationOfficeId = organizationOfficeId,
                officeAssignmentId = assignmentId,
                holderPersonId = personId,
                selectionMethod = request.selectionMethod,
                state = request.state,
                appointingAuthorityId = PoliticalModelUtility.Normalize(request.appointingAuthorityId),
                confirmationSourceId = PoliticalModelUtility.Normalize(request.confirmationSourceId),
                successionSourceId = PoliticalModelUtility.Normalize(request.successionSourceId),
                lineageAnchorPersonId = lineageAnchorPersonId,
                startWorldTime = request.worldTime,
                endWorldTime = request.endWorldTime >= 0d ? request.endWorldTime : definition.DefaultTermDuration > 0d ? request.worldTime + definition.DefaultTermDuration : -1d,
                provenanceId = PoliticalModelUtility.Normalize(request.provenanceId)
            };
            if (request.preview) return PoliticalOperationResult.Success("Government office installation previewed.", before, before, preview: true, subjectId: tenureId);
            if (active != null && request.state != GovernmentOfficeTenureState.Designated)
            {
                OrganizationMembershipOperationResult assignmentEnded = memberships.TransitionOfficeAssignment(new OrganizationOfficeAssignmentTransitionRequest
                {
                    transactionId = $"{request.transactionId}.end-current-assignment",
                    officeAssignmentId = active.officeAssignmentId,
                    targetState = OrganizationOfficeAssignmentState.Ended,
                    worldTime = request.worldTime,
                    sourceEventId = request.provenanceId
                });
                if (!assignmentEnded.Succeeded) return Fail(PoliticalOperationCode.InvalidState, $"Current organization office assignment could not be ended: {assignmentEnded.Message}", before);
                GovernmentOfficeTenureRecordData ended = officeTenuresById[active.tenureId].Clone();
                ended.state = GovernmentOfficeTenureState.Ended;
                ended.endWorldTime = request.worldTime;
                ended.revision++;
                officeTenuresById[ended.tenureId] = ended;
            }
            officeTenuresById[tenureId] = record;
            if (active != null && request.state != GovernmentOfficeTenureState.Designated) ReassignGovernmentOperationalAuthority(active, record);
            ResolveVacancyForInstalledTenure(record, request.worldTime);
            CompleteTransaction(request.transactionId, "install-government-officeholder", tenureId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government officeholder installed.", before, Revision, subjectId: tenureId));
        }

        public PoliticalOperationResult ScheduleElection(GovernmentElectionScheduleRequest request)
        {
            request ??= new GovernmentElectionScheduleRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string electionId = PoliticalModelUtility.Normalize(request.electionId);
            if (TryDuplicate(request.transactionId, electionId, "schedule-government-election", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(electionId) || electionsById.ContainsKey(electionId)) return Fail(PoliticalOperationCode.InvalidRequest, "A unique election ID is required.", before);
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (!governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (!TryGetDefinition(request.officeDefinitionId, out GovernmentOfficeDefinition office) || !office.Allows(request.selectionMethod) || !IsElectionMethod(request.selectionMethod)) return Fail(PoliticalOperationCode.InvalidState, "The office does not permit the requested election method.", before);
            string organizationId = PoliticalModelUtility.Normalize(request.administeringOrganizationId);
            if (organizations == null || !organizations.TryGetSnapshot(organizationId, out _)) return Fail(PoliticalOperationCode.InvalidReference, $"Election administrator '{organizationId}' is missing.", before);
            string[] voters = PoliticalModelUtility.Clean(request.eligibleVoterPersonIds);
            if (voters.Length == 0 || voters.Any(value => !knownPersonIds.Contains(value))) return Fail(PoliticalOperationCode.InvalidReference, "Election voter roll is empty or contains an unknown person.", before);
            if (request.votingEndWorldTime <= request.votingStartWorldTime) return Fail(PoliticalOperationCode.InvalidRequest, "Election voting end must follow voting start.", before);
            GovernmentElectionRecordData record = new GovernmentElectionRecordData
            {
                electionId = electionId, governmentId = governmentId, officeDefinitionId = office.Id, selectionMethod = request.selectionMethod,
                state = GovernmentElectionState.NominationsOpen, administeringOrganizationId = organizationId, eligibleVoterPersonIds = voters,
                linkedProposalId = PoliticalModelUtility.Normalize(request.linkedProposalId), scheduledWorldTime = request.scheduledWorldTime,
                votingStartWorldTime = request.votingStartWorldTime, votingEndWorldTime = request.votingEndWorldTime, provenanceId = PoliticalModelUtility.Normalize(request.provenanceId)
            };
            if (request.preview) return PoliticalOperationResult.Success("Government election previewed.", before, before, preview: true, subjectId: electionId);
            electionsById[electionId] = record;
            CompleteTransaction(request.transactionId, "schedule-government-election", electionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government election scheduled.", before, Revision, subjectId: electionId));
        }

        public PoliticalOperationResult NominateElectionCandidate(GovernmentElectionNominationRequest request)
        {
            request ??= new GovernmentElectionNominationRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string electionId = PoliticalModelUtility.Normalize(request.electionId);
            if (TryDuplicate(request.transactionId, electionId, "nominate-government-candidate", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!electionsById.TryGetValue(electionId, out GovernmentElectionRecordData current)) return Fail(PoliticalOperationCode.InvalidReference, $"Election '{electionId}' is missing.", before);
            if (current.state != GovernmentElectionState.NominationsOpen || request.worldTime >= current.votingStartWorldTime) return Fail(PoliticalOperationCode.InvalidState, "Election nominations are closed.", before);
            string personId = PoliticalModelUtility.Normalize(request.candidatePersonId);
            if (!knownPersonIds.Contains(personId)) return Fail(PoliticalOperationCode.InvalidReference, $"Candidate '{personId}' is unknown.", before);
            if (current.candidates.Any(value => value.personId == personId && !value.withdrawn)) return Fail(PoliticalOperationCode.Conflict, "Candidate is already nominated.", before);
            GovernmentElectionRecordData changed = current.Clone();
            changed.candidates = changed.candidates.Append(new GovernmentElectionCandidateRecordData { personId = personId, nominationSourceId = PoliticalModelUtility.Normalize(request.nominationSourceId), nominatedWorldTime = request.worldTime }).ToArray();
            changed.revision++;
            electionsById[electionId] = changed;
            CompleteTransaction(request.transactionId, "nominate-government-candidate", electionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Election candidate nominated.", before, Revision, subjectId: electionId));
        }

        public PoliticalOperationResult CastElectionBallot(GovernmentElectionBallotRequest request)
        {
            request ??= new GovernmentElectionBallotRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string electionId = PoliticalModelUtility.Normalize(request.electionId);
            if (TryDuplicate(request.transactionId, electionId, "cast-government-ballot", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!electionsById.TryGetValue(electionId, out GovernmentElectionRecordData current)) return Fail(PoliticalOperationCode.InvalidReference, $"Election '{electionId}' is missing.", before);
            if (request.worldTime < current.votingStartWorldTime || request.worldTime >= current.votingEndWorldTime || (current.state != GovernmentElectionState.NominationsOpen && current.state != GovernmentElectionState.VotingOpen)) return Fail(PoliticalOperationCode.InvalidState, "Election voting is not open.", before);
            string voter = PoliticalModelUtility.Normalize(request.voterPersonId);
            string candidate = PoliticalModelUtility.Normalize(request.candidatePersonId);
            if (!current.eligibleVoterPersonIds.Contains(voter, StringComparer.Ordinal)) return Fail(PoliticalOperationCode.AccessDenied, "Person is not on the frozen voter roll.", before);
            if (!current.candidates.Any(value => value.personId == candidate && !value.withdrawn)) return Fail(PoliticalOperationCode.InvalidReference, "Selected candidate is not active.", before);
            if (current.ballots.Any(value => value.voterPersonId == voter)) return Fail(PoliticalOperationCode.Conflict, "Voter has already cast a ballot.", before);
            string ballotId = PoliticalModelUtility.Normalize(request.ballotId);
            if (string.IsNullOrWhiteSpace(ballotId) || current.ballots.Any(value => value.ballotId == ballotId)) return Fail(PoliticalOperationCode.InvalidRequest, "A unique ballot ID is required.", before);
            GovernmentElectionRecordData changed = current.Clone();
            changed.state = GovernmentElectionState.VotingOpen;
            changed.ballots = changed.ballots.Append(new GovernmentElectionBallotRecordData { ballotId = ballotId, voterPersonId = voter, candidatePersonId = candidate, castWorldTime = request.worldTime }).ToArray();
            changed.revision++;
            electionsById[electionId] = changed;
            CompleteTransaction(request.transactionId, "cast-government-ballot", electionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Election ballot accepted.", before, Revision, subjectId: electionId));
        }

        public PoliticalOperationResult CertifyElection(GovernmentElectionCertificationRequest request)
        {
            request ??= new GovernmentElectionCertificationRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string electionId = PoliticalModelUtility.Normalize(request.electionId);
            if (TryDuplicate(request.transactionId, electionId, "certify-government-election", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!electionsById.TryGetValue(electionId, out GovernmentElectionRecordData current)) return Fail(PoliticalOperationCode.InvalidReference, $"Election '{electionId}' is missing.", before);
            if (request.worldTime < current.votingEndWorldTime || (current.state != GovernmentElectionState.NominationsOpen && current.state != GovernmentElectionState.VotingOpen && current.state != GovernmentElectionState.Counting)) return Fail(PoliticalOperationCode.InvalidState, "Election cannot be certified yet.", before);
            IGrouping<string, GovernmentElectionBallotRecordData>[] totals = current.ballots.GroupBy(value => value.candidatePersonId, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).ToArray();
            if (totals.Length == 0) return Fail(PoliticalOperationCode.InvalidState, "Election has no ballots.", before);
            if (totals.Length > 1 && totals[0].Count() == totals[1].Count()) return Fail(PoliticalOperationCode.Conflict, "Election result is tied and must be resolved before certification.", before);
            GovernmentElectionRecordData changed = current.Clone();
            changed.state = GovernmentElectionState.Certified;
            changed.winningPersonId = totals[0].Key;
            changed.linkedResolutionId = PoliticalModelUtility.Normalize(request.linkedResolutionId);
            changed.certifiedWorldTime = request.worldTime;
            changed.revision++;
            electionsById[electionId] = changed;
            CompleteTransaction(request.transactionId, "certify-government-election", electionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government election certified.", before, Revision, subjectId: electionId));
        }

        public PoliticalOperationResult IssuePermit(GovernmentPermitIssueRequest request)
        {
            request ??= new GovernmentPermitIssueRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string permitId = PoliticalModelUtility.Normalize(request.permitId);
            if (TryDuplicate(request.transactionId, permitId, "issue-government-permit", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(permitId) || permitsById.ContainsKey(permitId)) return Fail(PoliticalOperationCode.InvalidRequest, "A unique permit ID is required.", before);
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (!governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (!TryGetDefinition(request.permitDefinitionId, out GovernmentPermitDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Permit definition '{request.permitDefinitionId}' is missing.", before);
            if (!definition.AllowedHolderCategories.Contains(request.holderCategory)) return Fail(PoliticalOperationCode.InvalidState, "Permit holder category is not allowed.", before);
            string holderId = PoliticalModelUtility.Normalize(request.holderId);
            if (!ValidatePermitHolder(request.holderCategory, holderId)) return Fail(PoliticalOperationCode.InvalidReference, $"Permit holder '{holderId}' is unknown.", before);
            string jurisdictionId = PoliticalModelUtility.Normalize(request.jurisdictionId);
            if (!jurisdictionsById.TryGetValue(jurisdictionId, out JurisdictionRecordData jurisdiction) || jurisdiction.governmentId != governmentId) return Fail(PoliticalOperationCode.InvalidReference, "Permit jurisdiction is missing or belongs to another government.", before);
            double effective = Math.Max(request.worldTime, request.effectiveWorldTime);
            double expiration = request.expirationWorldTime >= 0d ? request.expirationWorldTime : definition.DefaultDuration > 0d ? effective + definition.DefaultDuration : -1d;
            if (expiration >= 0d && expiration <= effective) return Fail(PoliticalOperationCode.InvalidRequest, "Permit expiration must follow its effective time.", before);
            GovernmentPermitRecordData record = new GovernmentPermitRecordData
            {
                permitId = permitId, permitDefinitionId = definition.Id, governmentId = governmentId, jurisdictionId = jurisdictionId,
                holderCategory = request.holderCategory, holderId = holderId, state = GovernmentPermitState.Active,
                actionIds = definition.PermittedActionIds.ToArray(), issuedByPersonId = PoliticalModelUtility.Normalize(request.issuedByPersonId),
                sourceAuthorityId = PoliticalModelUtility.Normalize(request.sourceAuthorityId), feeAssessmentId = PoliticalModelUtility.Normalize(request.feeAssessmentId),
                issuedWorldTime = request.worldTime, effectiveWorldTime = effective, expirationWorldTime = expiration, provenanceId = PoliticalModelUtility.Normalize(request.provenanceId)
            };
            if (request.preview) return PoliticalOperationResult.Success("Government permit previewed.", before, before, preview: true, subjectId: permitId);
            permitsById[permitId] = record;
            CompleteTransaction(request.transactionId, "issue-government-permit", permitId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government permit issued.", before, Revision, subjectId: permitId));
        }

        public PoliticalOperationResult ChangePermitState(GovernmentPermitStateRequest request)
        {
            request ??= new GovernmentPermitStateRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string permitId = PoliticalModelUtility.Normalize(request.permitId);
            if (TryDuplicate(request.transactionId, permitId, "change-government-permit-state", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!permitsById.TryGetValue(permitId, out GovernmentPermitRecordData current)) return Fail(PoliticalOperationCode.InvalidReference, $"Permit '{permitId}' is missing.", before);
            if (request.targetState != GovernmentPermitState.Suspended && request.targetState != GovernmentPermitState.Revoked && request.targetState != GovernmentPermitState.Expired && request.targetState != GovernmentPermitState.Active) return Fail(PoliticalOperationCode.InvalidState, "Requested permit state is not a supported transition.", before);
            if (request.targetState == GovernmentPermitState.Revoked && TryGetDefinition(current.permitDefinitionId, out GovernmentPermitDefinition definition) && !definition.Revocable) return Fail(PoliticalOperationCode.InvalidState, "Permit definition does not allow revocation.", before);
            if (request.targetState == GovernmentPermitState.Active && current.state != GovernmentPermitState.Suspended) return Fail(PoliticalOperationCode.InvalidState, "Only a suspended permit can be reactivated.", before);
            if (request.targetState == GovernmentPermitState.Active && current.expirationWorldTime >= 0d && request.worldTime >= current.expirationWorldTime) return Fail(PoliticalOperationCode.InvalidState, "An expired permit cannot be reactivated.", before);
            GovernmentPermitRecordData changed = current.Clone();
            changed.state = request.targetState;
            changed.suspensionOrRevocationReason = request.reason ?? string.Empty;
            if (request.targetState == GovernmentPermitState.Expired && changed.expirationWorldTime < 0d) changed.expirationWorldTime = request.worldTime;
            changed.revision++;
            permitsById[permitId] = changed;
            CompleteTransaction(request.transactionId, "change-government-permit-state", permitId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government permit state changed.", before, Revision, subjectId: permitId));
        }

        public bool HasActivePermit(string holderId, string actionId, string jurisdictionId, double worldTime)
        {
            holderId = PoliticalModelUtility.Normalize(holderId);
            actionId = PoliticalModelUtility.Normalize(actionId);
            jurisdictionId = PoliticalModelUtility.Normalize(jurisdictionId);
            return permitsById.Values.Any(value => value.holderId == holderId && value.jurisdictionId == jurisdictionId && value.actionIds.Contains(actionId, StringComparer.Ordinal) && value.IsActiveAt(worldTime));
        }

        public PoliticalOperationResult EnactFiscalMandate(GovernmentFiscalMandateRequest request)
        {
            request ??= new GovernmentFiscalMandateRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string mandateId = PoliticalModelUtility.Normalize(request.mandateId);
            if (TryDuplicate(request.transactionId, mandateId, "enact-government-fiscal-mandate", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(mandateId) || fiscalMandatesById.ContainsKey(mandateId)) return Fail(PoliticalOperationCode.InvalidRequest, "A unique fiscal mandate ID is required.", before);
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (!governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (!TryGetDefinition(request.fiscalPolicyDefinitionId, out GovernmentFiscalPolicyDefinition policy)) return Fail(PoliticalOperationCode.MissingDefinition, $"Fiscal policy '{request.fiscalPolicyDefinitionId}' is missing.", before);
            foreach (string revenueId in policy.RevenueDefinitionIds) if (!registry.TryGet(revenueId, out InstitutionalRevenueDefinition _)) return Fail(PoliticalOperationCode.MissingDefinition, $"Revenue definition '{revenueId}' is missing.", before);
            string budgetId = PoliticalModelUtility.Normalize(request.organizationBudgetId);
            if (policy.RequiresApprovedBudget && (resources == null || !resources.Budgets.Any(value => value.budgetId == budgetId))) return Fail(PoliticalOperationCode.InvalidReference, $"Required organization budget '{budgetId}' is missing.", before);
            GovernmentFiscalMandateRecordData record = new GovernmentFiscalMandateRecordData
            {
                mandateId = mandateId, fiscalPolicyDefinitionId = policy.Id, governmentId = governmentId, state = GovernmentFiscalMandateState.Active,
                revenueDefinitionIds = policy.RevenueDefinitionIds.ToArray(), treasuryAccountId = policy.DefaultTreasuryAccountId, organizationBudgetId = budgetId,
                sourceAuthorityId = PoliticalModelUtility.Normalize(request.sourceAuthorityId), sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                effectiveWorldTime = request.worldTime, endWorldTime = request.endWorldTime, provenanceId = PoliticalModelUtility.Normalize(request.provenanceId)
            };
            if (request.preview) return PoliticalOperationResult.Success("Government fiscal mandate previewed.", before, before, preview: true, subjectId: mandateId);
            fiscalMandatesById[mandateId] = record;
            CompleteTransaction(request.transactionId, "enact-government-fiscal-mandate", mandateId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government fiscal mandate enacted.", before, Revision, subjectId: mandateId));
        }

        public PoliticalOperationResult AssessLegitimacy(GovernmentLegitimacyAssessmentRequest request)
        {
            request ??= new GovernmentLegitimacyAssessmentRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string assessmentId = PoliticalModelUtility.Normalize(request.assessmentId);
            if (TryDuplicate(request.transactionId, assessmentId, "assess-government-legitimacy", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(assessmentId) || legitimacyAssessmentsById.ContainsKey(assessmentId)) return Fail(PoliticalOperationCode.InvalidRequest, "A unique legitimacy assessment ID is required.", before);
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (!governmentsById.TryGetValue(governmentId, out GovernmentRecordData government)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (!TryGetDefinition(request.policyDefinitionId, out GovernmentLegitimacyPolicyDefinition policy)) return Fail(PoliticalOperationCode.MissingDefinition, $"Legitimacy policy '{request.policyDefinitionId}' is missing.", before);
            if (!TryGetDefinition(government.governmentDefinitionId, out GovernmentDefinition governmentDefinition) || !policy.GovernmentCategories.Contains(governmentDefinition.Category)) return Fail(PoliticalOperationCode.InvalidState, "Legitimacy policy does not apply to this government type.", before);
            GovernmentLegitimacyComponentScore[] inputs = request.components ?? Array.Empty<GovernmentLegitimacyComponentScore>();
            if (inputs.GroupBy(value => value.component).Any(group => group.Count() > 1) || policy.Weights.Any(weight => !inputs.Any(input => input.component == weight.component))) return Fail(PoliticalOperationCode.ValidationFailed, "Legitimacy assessment must provide one score for every weighted component.", before);
            if (inputs.Any(value => value.scoreBasisPoints < 0 || value.scoreBasisPoints > 10000)) return Fail(PoliticalOperationCode.ValidationFailed, "Legitimacy component scores must be between 0 and 10000.", before);
            int total = (int)Math.Round(policy.Weights.Sum(weight => inputs.First(input => input.component == weight.component).scoreBasisPoints * (weight.weightBasisPoints / 10000d)));
            total = Math.Max(0, Math.Min(10000, total));
            GovernmentLegitimacyRecordData record = new GovernmentLegitimacyRecordData
            {
                assessmentId = assessmentId, governmentId = governmentId, policyDefinitionId = policy.Id, components = inputs.ToArray(),
                totalScoreBasisPoints = total, band = policy.GetBand(total), assessedWorldTime = request.worldTime,
                assessorId = PoliticalModelUtility.Normalize(request.assessorId), provenanceId = PoliticalModelUtility.Normalize(request.provenanceId)
            };
            if (request.preview) return PoliticalOperationResult.Success("Government legitimacy assessment previewed.", before, before, preview: true, subjectId: assessmentId);
            legitimacyAssessmentsById[assessmentId] = record;
            CompleteTransaction(request.transactionId, "assess-government-legitimacy", assessmentId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government legitimacy assessed.", before, Revision, subjectId: assessmentId));
        }

        private bool ValidatePermitHolder(GovernmentPermitHolderCategory category, string holderId)
        {
            if (string.IsNullOrWhiteSpace(holderId)) return false;
            if (category == GovernmentPermitHolderCategory.Person) return knownPersonIds.Contains(holderId);
            if (category == GovernmentPermitHolderCategory.Organization) return organizations != null && organizations.TryGetSnapshot(holderId, out _);
            return true;
        }

        private string ProcessAdministrationWorldTime(double worldTime, bool preview)
        {
            GovernmentOfficeTenureRecordData[] endingTenures = officeTenuresById.Values.Where(value => (value.state == GovernmentOfficeTenureState.Active || value.state == GovernmentOfficeTenureState.Acting) && value.endWorldTime >= 0d && worldTime >= value.endWorldTime).ToArray();
            GovernmentPermitRecordData[] expiringPermits = permitsById.Values.Where(value => (value.state == GovernmentPermitState.Active || value.state == GovernmentPermitState.Suspended) && value.expirationWorldTime >= 0d && worldTime >= value.expirationWorldTime).ToArray();
            GovernmentFiscalMandateRecordData[] expiringMandates = fiscalMandatesById.Values.Where(value => value.state == GovernmentFiscalMandateState.Active && value.endWorldTime >= 0d && worldTime >= value.endWorldTime).ToArray();
            GovernmentElectionRecordData[] openingElections = electionsById.Values.Where(value => value.state == GovernmentElectionState.NominationsOpen && worldTime >= value.votingStartWorldTime && worldTime < value.votingEndWorldTime).ToArray();
            GovernmentElectionRecordData[] countingElections = electionsById.Values.Where(value => (value.state == GovernmentElectionState.NominationsOpen || value.state == GovernmentElectionState.VotingOpen) && worldTime >= value.votingEndWorldTime).ToArray();
            if (!preview)
            {
                foreach (GovernmentOfficeTenureRecordData value in endingTenures) EndTenureForVacancy(value, GovernmentOfficeVacancyCause.TermEnded, worldTime, $"world-time.{worldTime:0}");
                foreach (GovernmentPermitRecordData value in expiringPermits) { value.state = GovernmentPermitState.Expired; value.revision++; }
                foreach (GovernmentFiscalMandateRecordData value in expiringMandates) { value.state = GovernmentFiscalMandateState.Expired; value.revision++; }
                foreach (GovernmentElectionRecordData value in openingElections) { value.state = GovernmentElectionState.VotingOpen; value.revision++; }
                foreach (GovernmentElectionRecordData value in countingElections) { value.state = GovernmentElectionState.Counting; value.revision++; }
            }
            return $"{endingTenures.Length} office tenure(s) ended, {expiringPermits.Length} permit(s) expired, {expiringMandates.Length} fiscal mandate(s) expired, {openingElections.Length} election(s) opened, and {countingElections.Length} election(s) entered counting";
        }

        private static bool ValidateAdministrationSaveData(GovernmentRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, ISet<string> governmentIds, ISet<string> jurisdictionIds, ISet<string> personIds, out string failure)
        {
            failure = string.Empty;
            if (!UniqueIds(saveData.officeTenures?.Select(value => value?.tenureId), "office tenure", out failure)
                || !UniqueIds(saveData.elections?.Select(value => value?.electionId), "election", out failure)
                || !UniqueIds(saveData.permits?.Select(value => value?.permitId), "permit", out failure)
                || !UniqueIds(saveData.fiscalMandates?.Select(value => value?.mandateId), "fiscal mandate", out failure)
                || !UniqueIds(saveData.legitimacyAssessments?.Select(value => value?.assessmentId), "legitimacy assessment", out failure)) return false;

            foreach (GovernmentOfficeTenureRecordData tenure in saveData.officeTenures ?? Array.Empty<GovernmentOfficeTenureRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(tenure.governmentId))) { failure = $"Office tenure '{tenure.tenureId}' references missing government '{tenure.governmentId}'."; return false; }
                if (definitionRegistry != null && !definitionRegistry.TryGet(tenure.officeDefinitionId, out GovernmentOfficeDefinition _)) { failure = $"Office tenure '{tenure.tenureId}' references missing office definition '{tenure.officeDefinitionId}'."; return false; }
                if (!personIds.Contains(PoliticalModelUtility.Normalize(tenure.holderPersonId))) { failure = $"Office tenure '{tenure.tenureId}' references unknown holder '{tenure.holderPersonId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(tenure.lineageAnchorPersonId) && !personIds.Contains(PoliticalModelUtility.Normalize(tenure.lineageAnchorPersonId))) { failure = $"Office tenure '{tenure.tenureId}' references unknown lineage anchor '{tenure.lineageAnchorPersonId}'."; return false; }
            }

            foreach (GovernmentElectionRecordData election in saveData.elections ?? Array.Empty<GovernmentElectionRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(election.governmentId))) { failure = $"Election '{election.electionId}' references missing government '{election.governmentId}'."; return false; }
                if (definitionRegistry != null && !definitionRegistry.TryGet(election.officeDefinitionId, out GovernmentOfficeDefinition _)) { failure = $"Election '{election.electionId}' references missing office definition '{election.officeDefinitionId}'."; return false; }
                if (organizationRuntime != null && !organizationRuntime.TryGetSnapshot(election.administeringOrganizationId, out _)) { failure = $"Election '{election.electionId}' references missing administrator '{election.administeringOrganizationId}'."; return false; }
                if ((election.eligibleVoterPersonIds ?? Array.Empty<string>()).Any(value => !personIds.Contains(PoliticalModelUtility.Normalize(value)))) { failure = $"Election '{election.electionId}' contains an unknown voter."; return false; }
                if ((election.candidates ?? Array.Empty<GovernmentElectionCandidateRecordData>()).Any(value => value == null || !personIds.Contains(PoliticalModelUtility.Normalize(value.personId)))) { failure = $"Election '{election.electionId}' contains an unknown candidate."; return false; }
            }

            foreach (GovernmentPermitRecordData permit in saveData.permits ?? Array.Empty<GovernmentPermitRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(permit.governmentId)) || !jurisdictionIds.Contains(PoliticalModelUtility.Normalize(permit.jurisdictionId))) { failure = $"Permit '{permit.permitId}' references missing government or jurisdiction."; return false; }
                if (definitionRegistry != null && !definitionRegistry.TryGet(permit.permitDefinitionId, out GovernmentPermitDefinition _)) { failure = $"Permit '{permit.permitId}' references missing definition '{permit.permitDefinitionId}'."; return false; }
                if (permit.holderCategory == GovernmentPermitHolderCategory.Person && !personIds.Contains(PoliticalModelUtility.Normalize(permit.holderId))) { failure = $"Permit '{permit.permitId}' references unknown person '{permit.holderId}'."; return false; }
                if (permit.holderCategory == GovernmentPermitHolderCategory.Organization && organizationRuntime != null && !organizationRuntime.TryGetSnapshot(permit.holderId, out _)) { failure = $"Permit '{permit.permitId}' references missing organization '{permit.holderId}'."; return false; }
            }

            foreach (GovernmentFiscalMandateRecordData mandate in saveData.fiscalMandates ?? Array.Empty<GovernmentFiscalMandateRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(mandate.governmentId))) { failure = $"Fiscal mandate '{mandate.mandateId}' references missing government '{mandate.governmentId}'."; return false; }
                if (definitionRegistry != null && !definitionRegistry.TryGet(mandate.fiscalPolicyDefinitionId, out GovernmentFiscalPolicyDefinition _)) { failure = $"Fiscal mandate '{mandate.mandateId}' references missing policy '{mandate.fiscalPolicyDefinitionId}'."; return false; }
            }

            foreach (GovernmentLegitimacyRecordData assessment in saveData.legitimacyAssessments ?? Array.Empty<GovernmentLegitimacyRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(assessment.governmentId))) { failure = $"Legitimacy assessment '{assessment.assessmentId}' references missing government '{assessment.governmentId}'."; return false; }
                if (definitionRegistry != null && !definitionRegistry.TryGet(assessment.policyDefinitionId, out GovernmentLegitimacyPolicyDefinition _)) { failure = $"Legitimacy assessment '{assessment.assessmentId}' references missing policy '{assessment.policyDefinitionId}'."; return false; }
                if (assessment.totalScoreBasisPoints < 0 || assessment.totalScoreBasisPoints > 10000) { failure = $"Legitimacy assessment '{assessment.assessmentId}' has an invalid score."; return false; }
            }
            return true;
        }

        private static bool UniqueIds(IEnumerable<string> ids, string label, out string failure)
        {
            string[] values = (ids ?? Array.Empty<string>()).Select(PoliticalModelUtility.Normalize).ToArray();
            if (values.Any(string.IsNullOrWhiteSpace)) { failure = $"Government save contains a {label} without an ID."; return false; }
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length) { failure = $"Government save contains duplicate {label} IDs."; return false; }
            failure = string.Empty;
            return true;
        }

        private static bool IsElectionMethod(GovernmentOfficeSelectionMethod method) => method == GovernmentOfficeSelectionMethod.PopularElection || method == GovernmentOfficeSelectionMethod.NobleElection || method == GovernmentOfficeSelectionMethod.CouncilSelection || method == GovernmentOfficeSelectionMethod.GuildSelection;

        private static bool TryClone(Dictionary<string, GovernmentOfficeTenureRecordData> source, string id, out GovernmentOfficeTenureRecordData record) { if (source.TryGetValue(PoliticalModelUtility.Normalize(id), out GovernmentOfficeTenureRecordData value)) { record = value.Clone(); return true; } record = null; return false; }
        private static bool TryClone(Dictionary<string, GovernmentElectionRecordData> source, string id, out GovernmentElectionRecordData record) { if (source.TryGetValue(PoliticalModelUtility.Normalize(id), out GovernmentElectionRecordData value)) { record = value.Clone(); return true; } record = null; return false; }
        private static bool TryClone(Dictionary<string, GovernmentPermitRecordData> source, string id, out GovernmentPermitRecordData record) { if (source.TryGetValue(PoliticalModelUtility.Normalize(id), out GovernmentPermitRecordData value)) { record = value.Clone(); return true; } record = null; return false; }
        private static bool TryClone(Dictionary<string, GovernmentFiscalMandateRecordData> source, string id, out GovernmentFiscalMandateRecordData record) { if (source.TryGetValue(PoliticalModelUtility.Normalize(id), out GovernmentFiscalMandateRecordData value)) { record = value.Clone(); return true; } record = null; return false; }
        private static bool TryClone(Dictionary<string, GovernmentLegitimacyRecordData> source, string id, out GovernmentLegitimacyRecordData record) { if (source.TryGetValue(PoliticalModelUtility.Normalize(id), out GovernmentLegitimacyRecordData value)) { record = value.Clone(); return true; } record = null; return false; }
    }
}
