using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Organizations
{
    public sealed class OrganizationDecisionRuntime : IDisposable
    {
        private readonly Dictionary<string, OrganizationGoalRecordData> goalsById = new Dictionary<string, OrganizationGoalRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationPolicyRecordData> policiesById = new Dictionary<string, OrganizationPolicyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationProposalRecordData> proposalsById = new Dictionary<string, OrganizationProposalRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationAmendmentRecordData> amendmentsById = new Dictionary<string, OrganizationAmendmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationVoterRollRecordData> voterRollsById = new Dictionary<string, OrganizationVoterRollRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationVoteRecordData> votesById = new Dictionary<string, OrganizationVoteRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationResolutionRecordData> resolutionsById = new Dictionary<string, OrganizationResolutionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationDecisionExecutionRecordData> executionsById = new Dictionary<string, OrganizationDecisionExecutionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationDecisionTransactionRecordData> transactionsById = new Dictionary<string, OrganizationDecisionTransactionRecordData>(StringComparer.Ordinal);
        private readonly List<string> eventDeliveryDiagnostics = new List<string>();
        private readonly HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private OrganizationMembershipRuntime memberships;
        private OrganizationAuthorityRuntime authority;
        private OrganizationResourceRuntime resources;
        private EconomyRuntime economy;
        private string worldId = string.Empty;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => !disposed && registry != null && organizations != null && memberships != null && authority != null && !string.IsNullOrWhiteSpace(worldId);
        public int GoalCount => goalsById.Count;
        public int PolicyCount => policiesById.Count;
        public int ProposalCount => proposalsById.Count;
        public int VoteCount => votesById.Count;
        public int ResolutionCount => resolutionsById.Count;
        public int ExecutionCount => executionsById.Count;
        public IReadOnlyList<OrganizationGoalRecordData> Goals => Ordered(goalsById.Values, item => item.createdWorldTime, item => item.goalId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationPolicyRecordData> Policies => Ordered(policiesById.Values, item => item.effectiveStartWorldTime, item => item.policyId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationProposalRecordData> Proposals => Ordered(proposalsById.Values, item => item.submittedWorldTime, item => item.proposalId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationAmendmentRecordData> Amendments => Ordered(amendmentsById.Values, item => item.proposedWorldTime, item => item.amendmentId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationVoterRollRecordData> VoterRolls => Ordered(voterRollsById.Values, item => item.createdWorldTime, item => item.voterRollId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationVoteRecordData> Votes => Ordered(votesById.Values, item => item.castWorldTime, item => item.voteId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationResolutionRecordData> Resolutions => Ordered(resolutionsById.Values, item => item.adoptedWorldTime, item => item.resolutionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationDecisionExecutionRecordData> Executions => Ordered(executionsById.Values, item => item.preparedWorldTime, item => item.executionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<string> EventDeliveryDiagnostics => eventDeliveryDiagnostics.ToArray();
        public event Action<OrganizationDecisionCommittedEvent> OperationCommitted;

        public void Configure(DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationResourceRuntime resourceRuntime, string world, IEnumerable<string> persons, EconomyRuntime economyRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            organizations = organizationRuntime ?? organizations;
            memberships = membershipRuntime ?? memberships;
            authority = authorityRuntime ?? authority;
            resources = resourceRuntime ?? resources;
            economy = economyRuntime ?? economy;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            knownPersonIds.Clear();
            foreach (string person in OrganizationModelUtility.Clean(persons)) knownPersonIds.Add(person);
            disposed = false;
        }

        public OrganizationDecisionOperationResult CreateGoal(OrganizationGoalRequest request)
        {
            request ??= new OrganizationGoalRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateBase(request.transactionId, request.goalId, request.organizationId, out string failure)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, failure, request.preview);
            if (!TryActiveOrganization(request.organizationId, out failure)) return Fail(OrganizationDecisionOperationCode.MissingOrganization, failure, request.preview);
            if (!registry.TryGet(request.goalDefinitionId, out OrganizationGoalDefinition definition)) return Fail(OrganizationDecisionOperationCode.MissingDefinition, $"Goal definition '{request.goalDefinitionId}' is missing.", request.preview);
            if (!DefinitionAllowsOrganization(definition.ValidOrganizationDefinitionIds, definition.ValidOrganizationCategories, request.organizationId)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Goal definition does not apply to this Organization.", request.preview);
            if (request.deadlineWorldTime >= 0d && (!definition.DeadlineAllowed || request.deadlineWorldTime <= request.worldTime)) return Fail(OrganizationDecisionOperationCode.InvalidWindow, "Goal deadline is invalid for this definition.", request.preview);
            long target = request.targetValue > 0L ? request.targetValue : definition.TargetValue;
            if (!definition.AllowMultipleActiveInstances && goalsById.Values.Any(item => item.organizationId == request.organizationId && item.goalDefinitionId == request.goalDefinitionId && item.lifecycleState == OrganizationGoalLifecycleState.Active)) return Fail(OrganizationDecisionOperationCode.InvalidConflict, "An active goal of this definition already exists.", request.preview);
            if (goalsById.TryGetValue(request.goalId, out OrganizationGoalRecordData existing)) return Duplicate("create-goal", request.transactionId, request.goalId, existing.organizationId == request.organizationId && existing.goalDefinitionId == request.goalDefinitionId, before, request.preview, goal: existing);

            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId), request.goalId, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationGoalRecordData record = new OrganizationGoalRecordData
            {
                goalId = request.goalId.Trim(),
                organizationId = request.organizationId.Trim(),
                goalDefinitionId = request.goalDefinitionId.Trim(),
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                lifecycleState = OrganizationGoalLifecycleState.Active,
                targetSubject = request.targetSubject?.Clone(),
                targetValue = target,
                currentValue = 0L,
                priority = Math.Max(definition.MinimumPriority, Math.Min(definition.MaximumPriority, request.priority)),
                createdWorldTime = request.worldTime,
                activeStartWorldTime = request.worldTime,
                deadlineWorldTime = request.deadlineWorldTime,
                sourceProposalId = request.sourceProposalId ?? string.Empty,
                sourceResolutionId = request.sourceResolutionId ?? string.Empty,
                visibility = request.visibility
            };
            EvaluateGoalRecord(record, definition, request.worldTime);
            if (request.preview) return OrganizationDecisionOperationResult.Success("Goal creation preview succeeded.", before, before, preview: true, subjectId: record.goalId, authorization: authorization, goal: record);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId), request.goalId, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            goalsById.Add(record.goalId, record);
            Commit(request.transactionId, "create-goal", record.goalId, record.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Organization goal created.", before, Revision, subjectId: record.goalId, authorization: authorization, goal: record);
        }

        public OrganizationDecisionOperationResult AddGoalProgress(OrganizationGoalProgressRequest request)
        {
            request ??= new OrganizationGoalProgressRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.goalId) || string.IsNullOrWhiteSpace(request.contributionId) || request.units < 0L) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Transaction, goal, contribution, and nonnegative units are required.", request.preview);
            if (!goalsById.TryGetValue(request.goalId, out OrganizationGoalRecordData goal)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, $"Goal '{request.goalId}' is missing.", request.preview);
            if (goal.progressContributions.Any(item => item.contributionId == request.contributionId)) return Duplicate("goal-progress", request.transactionId, request.contributionId, true, before, request.preview, goal: goal);
            if (goal.lifecycleState != OrganizationGoalLifecycleState.Active) return Fail(OrganizationDecisionOperationCode.InvalidLifecycle, $"Goal '{goal.goalId}' is {goal.lifecycleState}.", request.preview);
            OrganizationGoalRecordData updated = goal.Clone();
            List<OrganizationGoalProgressContributionData> contributions = updated.progressContributions.ToList();
            contributions.Add(new OrganizationGoalProgressContributionData { contributionId = request.contributionId.Trim(), units = request.units, sourceRecordId = request.sourceRecordId ?? string.Empty, worldTime = request.worldTime });
            updated.progressContributions = contributions.OrderBy(item => item.worldTime).ThenBy(item => item.contributionId, StringComparer.Ordinal).ToArray();
            if (registry.TryGet(updated.goalDefinitionId, out OrganizationGoalDefinition definition)) EvaluateGoalRecord(updated, definition, request.worldTime);
            if (request.preview) return OrganizationDecisionOperationResult.Success("Goal progress preview succeeded.", before, before, preview: true, subjectId: updated.goalId, goal: updated);
            goalsById[updated.goalId] = updated;
            Commit(request.transactionId, "goal-progress", request.contributionId, updated.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Goal progress recorded.", before, Revision, subjectId: updated.goalId, goal: updated);
        }

        public OrganizationDecisionOperationResult CreatePolicy(OrganizationPolicyRequest request)
        {
            request ??= new OrganizationPolicyRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateBase(request.transactionId, request.policyId, request.organizationId, out string failure)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, failure, request.preview);
            if (!TryActiveOrganization(request.organizationId, out failure)) return Fail(OrganizationDecisionOperationCode.MissingOrganization, failure, request.preview);
            if (!registry.TryGet(request.policyDefinitionId, out OrganizationPolicyDefinition definition)) return Fail(OrganizationDecisionOperationCode.MissingDefinition, $"Policy definition '{request.policyDefinitionId}' is missing.", request.preview);
            OrganizationPolicyScopeData scope = request.scope?.Clone() ?? OrganizationPolicyScopeData.EntireOrganization(request.organizationId);
            if (string.IsNullOrWhiteSpace(scope.organizationId)) scope.organizationId = request.organizationId;
            if (!definition.AllowedScopes.Contains(scope.scopeType)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Policy scope is not allowed by its definition.", request.preview);
            if (!ValidatePolicyParameters(definition, request.parameters, out failure)) return Fail(OrganizationDecisionOperationCode.InvalidParameter, failure, request.preview);
            if (request.effectiveEndWorldTime >= 0d && request.effectiveEndWorldTime <= request.effectiveStartWorldTime) return Fail(OrganizationDecisionOperationCode.InvalidWindow, "Policy end must be after its start.", request.preview);
            if (policiesById.TryGetValue(request.policyId, out OrganizationPolicyRecordData existing)) return Duplicate("create-policy", request.transactionId, request.policyId, existing.organizationId == request.organizationId && existing.policyDefinitionId == request.policyDefinitionId, before, request.preview, policy: existing);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId), request.policyId, request.adoptedWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);

            OrganizationPolicyRecordData record = new OrganizationPolicyRecordData
            {
                policyId = request.policyId.Trim(),
                organizationId = request.organizationId.Trim(),
                policyDefinitionId = request.policyDefinitionId.Trim(),
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                lifecycleState = request.effectiveStartWorldTime > request.adoptedWorldTime ? OrganizationPolicyLifecycleState.Scheduled : OrganizationPolicyLifecycleState.Active,
                scope = scope,
                parameters = MergeParameters(definition, request.parameters),
                priority = request.priority == 0 ? definition.Priority : request.priority,
                adoptedWorldTime = request.adoptedWorldTime,
                effectiveStartWorldTime = request.effectiveStartWorldTime,
                effectiveEndWorldTime = request.effectiveEndWorldTime,
                sourceProposalId = request.sourceProposalId ?? string.Empty,
                sourceResolutionId = request.sourceResolutionId ?? string.Empty,
                supersedesPolicyId = request.supersedesPolicyId ?? string.Empty,
                visibility = request.visibility
            };
            OrganizationPolicyRecordData[] superseded = ConflictingPolicies(record, definition).ToArray();
            if (superseded.Length > 0 && definition.AllowMultipleScopedInstances && string.IsNullOrWhiteSpace(request.supersedesPolicyId)) return Fail(OrganizationDecisionOperationCode.InvalidConflict, "Conflicting active policies require explicit supersession.", request.preview);
            if (request.preview) return OrganizationDecisionOperationResult.Success("Policy adoption preview succeeded.", before, before, preview: true, subjectId: record.policyId, authorization: authorization, policy: record);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId), request.policyId, request.adoptedWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            foreach (OrganizationPolicyRecordData old in superseded)
            {
                old.lifecycleState = OrganizationPolicyLifecycleState.Superseded;
                old.supersededByPolicyId = record.policyId;
                old.effectiveEndWorldTime = request.effectiveStartWorldTime;
                old.revision++;
            }
            policiesById.Add(record.policyId, record);
            Commit(request.transactionId, "create-policy", record.policyId, record.organizationId, request.adoptedWorldTime);
            return OrganizationDecisionOperationResult.Success("Organization policy adopted.", before, Revision, subjectId: record.policyId, authorization: authorization, policy: record);
        }

        public OrganizationPolicyResolutionResult ResolvePolicies(OrganizationPolicyQuery query)
        {
            query ??= new OrganizationPolicyQuery();
            string organizationId = query.organizationId ?? string.Empty;
            OrganizationPolicyScopeData scope = query.scope?.Clone();
            OrganizationPolicyRecordData[] active = policiesById.Values
                .Where(item => item.organizationId == organizationId)
                .Where(item => string.IsNullOrWhiteSpace(query.policyDefinitionId) || item.policyDefinitionId == query.policyDefinitionId)
                .Where(item => item.IsActiveAt(query.worldTime))
                .Where(item => ScopeMatches(item.scope, scope))
                .OrderByDescending(item => item.scope?.Specificity ?? 0)
                .ThenByDescending(item => item.priority)
                .ThenBy(item => item.effectiveStartWorldTime)
                .ThenBy(item => item.policyId, StringComparer.Ordinal)
                .ToArray();
            OrganizationPolicyRecordData effective = active.FirstOrDefault();
            return new OrganizationPolicyResolutionResult(active, effective, active.Skip(1));
        }

        public OrganizationDecisionOperationResult SubmitProposal(OrganizationProposalRequest request)
        {
            request ??= new OrganizationProposalRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateBase(request.transactionId, request.proposalId, request.organizationId, out string failure)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, failure, request.preview);
            if (!TryActiveOrganization(request.organizationId, out failure)) return Fail(OrganizationDecisionOperationCode.MissingOrganization, failure, request.preview);
            if (!registry.TryGet(request.proposalDefinitionId, out OrganizationProposalDefinition definition)) return Fail(OrganizationDecisionOperationCode.MissingDefinition, $"Proposal definition '{request.proposalDefinitionId}' is missing.", request.preview);
            if (!registry.TryGet(definition.DecisionProcedureDefinitionId, out OrganizationDecisionProcedureDefinition procedure)) return Fail(OrganizationDecisionOperationCode.MissingProcedure, $"Decision procedure '{definition.DecisionProcedureDefinitionId}' is missing.", request.preview);
            foreach (OrganizationDecisionExecutionOperationData operation in request.requestedExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationData>())
            {
                if (!definition.SupportedExecutionOperations.Contains(operation.kind)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, $"Proposal definition does not support execution operation '{operation.kind}'.", request.preview);
            }
            if (!definition.DuplicateActiveProposalsAllowed && proposalsById.Values.Any(item => item.organizationId == request.organizationId && item.proposalDefinitionId == request.proposalDefinitionId && item.lifecycleState == OrganizationProposalLifecycleState.OpenForVoting)) return Fail(OrganizationDecisionOperationCode.InvalidConflict, "An active proposal of this type already exists.", request.preview);
            if (proposalsById.TryGetValue(request.proposalId, out OrganizationProposalRecordData existing)) return Duplicate("submit-proposal", request.transactionId, request.proposalId, existing.organizationId == request.organizationId && existing.proposalDefinitionId == request.proposalDefinitionId, before, request.preview, proposal: existing);
            string actionId = string.IsNullOrWhiteSpace(definition.RequiredSubmitActionDefinitionId) ? PrototypeOrganizationAuthorityDefinitionFactory.SubmitDecisionProposalActionId : definition.RequiredSubmitActionDefinitionId;
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.proposerPersonId, request.organizationId, actionId, request.proposalId, request.submittedWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);

            double start = request.votingStartWorldTime <= 0d ? request.submittedWorldTime : request.votingStartWorldTime;
            double end = request.votingEndWorldTime > start ? request.votingEndWorldTime : start + Math.Max(1d, definition.VotingDuration);
            string rollId = $"voter-roll.{request.proposalId}";
            OrganizationVoterRollRecordData roll = BuildVoterRoll(rollId, request.proposalId, request.organizationId, procedure, request.submittedWorldTime);
            OrganizationProposalRecordData proposal = new OrganizationProposalRecordData
            {
                proposalId = request.proposalId.Trim(),
                organizationId = request.organizationId.Trim(),
                proposalDefinitionId = request.proposalDefinitionId.Trim(),
                title = string.IsNullOrWhiteSpace(request.title) ? definition.DisplayName : request.title.Trim(),
                proposerPersonId = request.proposerPersonId ?? string.Empty,
                lifecycleState = start <= request.submittedWorldTime ? OrganizationProposalLifecycleState.OpenForVoting : OrganizationProposalLifecycleState.Submitted,
                requestedExecutionOperations = (request.requestedExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
                voterRollId = roll.voterRollId,
                submittedWorldTime = request.submittedWorldTime,
                votingStartWorldTime = start,
                votingEndWorldTime = end,
                visibility = request.visibility,
                tags = OrganizationModelUtility.Clean(request.tags)
            };
            if (request.preview) return OrganizationDecisionOperationResult.Success("Proposal submission preview succeeded.", before, before, preview: true, subjectId: proposal.proposalId, authorization: authorization, proposal: proposal);
            authorization = Authorize(request.transactionId, request.proposerPersonId, request.organizationId, actionId, request.proposalId, request.submittedWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            proposalsById.Add(proposal.proposalId, proposal);
            voterRollsById.Add(roll.voterRollId, roll);
            Commit(request.transactionId, "submit-proposal", proposal.proposalId, proposal.organizationId, request.submittedWorldTime);
            return OrganizationDecisionOperationResult.Success("Organization proposal submitted.", before, Revision, subjectId: proposal.proposalId, authorization: authorization, proposal: proposal);
        }

        public OrganizationDecisionOperationResult SubmitAmendment(OrganizationAmendmentRequest request)
        {
            request ??= new OrganizationAmendmentRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.amendmentId) || string.IsNullOrWhiteSpace(request.proposalId)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Transaction, amendment, and proposal IDs are required.", request.preview);
            if (!proposalsById.TryGetValue(request.proposalId, out OrganizationProposalRecordData proposal)) return Fail(OrganizationDecisionOperationCode.MissingProposal, $"Proposal '{request.proposalId}' is missing.", request.preview);
            if (!registry.TryGet(proposal.proposalDefinitionId, out OrganizationProposalDefinition definition) || !definition.AmendmentAllowed) return Fail(OrganizationDecisionOperationCode.InvalidLifecycle, "This proposal cannot be amended.", request.preview);
            if (proposal.lifecycleState != OrganizationProposalLifecycleState.OpenForVoting && proposal.lifecycleState != OrganizationProposalLifecycleState.Submitted) return Fail(OrganizationDecisionOperationCode.InvalidLifecycle, $"Proposal '{proposal.proposalId}' is {proposal.lifecycleState}.", request.preview);
            if (amendmentsById.TryGetValue(request.amendmentId, out OrganizationAmendmentRecordData existing)) return Duplicate("submit-amendment", request.transactionId, request.amendmentId, existing.proposalId == request.proposalId, before, request.preview, proposal: proposal);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.proposerPersonId, proposal.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.AmendDecisionProposalActionId, request.amendmentId, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationAmendmentRecordData amendment = new OrganizationAmendmentRecordData
            {
                amendmentId = request.amendmentId.Trim(),
                proposalId = proposal.proposalId,
                organizationId = proposal.organizationId,
                proposerPersonId = request.proposerPersonId ?? string.Empty,
                targetProposalVersion = proposal.version,
                summary = request.summary ?? string.Empty,
                replacementExecutionOperations = (request.replacementExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
                lifecycleState = request.acceptImmediately ? OrganizationAmendmentLifecycleState.Accepted : OrganizationAmendmentLifecycleState.Proposed,
                proposedWorldTime = request.worldTime,
                resolvedWorldTime = request.acceptImmediately ? request.worldTime : -1d
            };
            if (request.preview) return OrganizationDecisionOperationResult.Success("Amendment preview succeeded.", before, before, preview: true, subjectId: amendment.amendmentId, authorization: authorization, proposal: proposal);
            authorization = Authorize(request.transactionId, request.proposerPersonId, proposal.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.AmendDecisionProposalActionId, request.amendmentId, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            amendmentsById.Add(amendment.amendmentId, amendment);
            if (request.acceptImmediately)
            {
                proposal.acceptedAmendmentId = amendment.amendmentId;
                proposal.requestedExecutionOperations = amendment.replacementExecutionOperations.Select(item => item.Clone()).ToArray();
                proposal.version++;
                proposal.revision++;
            }
            Commit(request.transactionId, "submit-amendment", amendment.amendmentId, proposal.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Organization proposal amended.", before, Revision, subjectId: amendment.amendmentId, authorization: authorization, proposal: proposal);
        }

        public OrganizationDecisionOperationResult CastVote(OrganizationVoteRequest request)
        {
            request ??= new OrganizationVoteRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.voteId) || string.IsNullOrWhiteSpace(request.proposalId) || string.IsNullOrWhiteSpace(request.voterPersonId)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Transaction, vote, proposal, and voter IDs are required.", request.preview);
            if (!proposalsById.TryGetValue(request.proposalId, out OrganizationProposalRecordData proposal)) return Fail(OrganizationDecisionOperationCode.MissingProposal, $"Proposal '{request.proposalId}' is missing.", request.preview);
            if (!proposal.IsVoteOpenAt(request.worldTime)) return Fail(OrganizationDecisionOperationCode.InvalidWindow, "Proposal voting is not open.", request.preview);
            if (!voterRollsById.TryGetValue(proposal.voterRollId, out OrganizationVoterRollRecordData roll)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Proposal voter roll is missing.", request.preview);
            if (!roll.eligiblePersonIds.Contains(request.voterPersonId ?? string.Empty, StringComparer.Ordinal)) return Fail(OrganizationDecisionOperationCode.IneligibleVoter, "Voter is not on the voter roll.", request.preview);
            if (!registry.TryGet(roll.procedureDefinitionId, out OrganizationDecisionProcedureDefinition procedure)) return Fail(OrganizationDecisionOperationCode.MissingProcedure, "Decision procedure is missing.", request.preview);
            OrganizationVoteRecordData priorActive = votesById.Values.Where(item => item.proposalId == proposal.proposalId && item.voterPersonId == request.voterPersonId && item.lifecycleState == OrganizationVoteLifecycleState.Active).OrderBy(item => item.castWorldTime).ThenBy(item => item.voteId, StringComparer.Ordinal).LastOrDefault();
            if (priorActive != null && !procedure.AllowVoteReplacement) return Fail(OrganizationDecisionOperationCode.DuplicateVote, "Voter has already cast an active vote.", request.preview);
            if (votesById.TryGetValue(request.voteId, out OrganizationVoteRecordData existing)) return Duplicate("cast-vote", request.transactionId, request.voteId, existing.proposalId == request.proposalId && existing.voterPersonId == request.voterPersonId, before, request.preview, vote: existing);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.voterPersonId, proposal.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.CastOrganizationVoteActionId, request.voteId, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationVoteRecordData vote = new OrganizationVoteRecordData { voteId = request.voteId.Trim(), proposalId = proposal.proposalId, voterRollId = roll.voterRollId, voterPersonId = request.voterPersonId.Trim(), choice = request.choice, weight = VoteWeight(procedure, request.voterPersonId), castWorldTime = request.worldTime, replacesVoteId = priorActive?.voteId ?? string.Empty };
            if (request.preview) return OrganizationDecisionOperationResult.Success("Vote preview succeeded.", before, before, preview: true, subjectId: vote.voteId, authorization: authorization, vote: vote);
            authorization = Authorize(request.transactionId, request.voterPersonId, proposal.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.CastOrganizationVoteActionId, request.voteId, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            if (priorActive != null)
            {
                priorActive.lifecycleState = OrganizationVoteLifecycleState.Replaced;
                priorActive.revision++;
            }
            votesById.Add(vote.voteId, vote);
            Commit(request.transactionId, "cast-vote", vote.voteId, proposal.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Vote cast.", before, Revision, subjectId: vote.voteId, authorization: authorization, vote: vote);
        }

        public OrganizationDecisionTallySnapshot TallyProposal(string proposalId)
        {
            if (!proposalsById.TryGetValue(proposalId ?? string.Empty, out OrganizationProposalRecordData proposal) || !voterRollsById.TryGetValue(proposal.voterRollId, out OrganizationVoterRollRecordData roll)) return new OrganizationDecisionTallySnapshot(null, Array.Empty<OrganizationVoteRecordData>());
            return new OrganizationDecisionTallySnapshot(roll, votesById.Values.Where(item => item.proposalId == proposal.proposalId));
        }

        public OrganizationDecisionOperationResult CloseVote(OrganizationCloseVoteRequest request)
        {
            request ??= new OrganizationCloseVoteRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.proposalId) || string.IsNullOrWhiteSpace(request.resolutionId)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Transaction, proposal, and resolution IDs are required.", request.preview);
            if (!proposalsById.TryGetValue(request.proposalId, out OrganizationProposalRecordData proposal)) return Fail(OrganizationDecisionOperationCode.MissingProposal, $"Proposal '{request.proposalId}' is missing.", request.preview);
            if (proposal.lifecycleState != OrganizationProposalLifecycleState.OpenForVoting) return Fail(OrganizationDecisionOperationCode.InvalidLifecycle, $"Proposal '{proposal.proposalId}' is {proposal.lifecycleState}.", request.preview);
            if (!registry.TryGet(proposal.proposalDefinitionId, out OrganizationProposalDefinition proposalDefinition) || !registry.TryGet(proposalDefinition.DecisionProcedureDefinitionId, out OrganizationDecisionProcedureDefinition procedure)) return Fail(OrganizationDecisionOperationCode.MissingProcedure, "Decision procedure is missing.", request.preview);
            if (resolutionsById.TryGetValue(request.resolutionId, out OrganizationResolutionRecordData existing)) return Duplicate("close-vote", request.transactionId, request.resolutionId, existing.proposalId == proposal.proposalId, before, request.preview, resolution: existing);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, proposal.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.CloseOrganizationVoteActionId, request.resolutionId, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationDecisionTallySnapshot tally = TallyProposal(proposal.proposalId);
            bool quorum = QuorumMet(procedure, tally);
            bool threshold = quorum && ThresholdMet(procedure, tally);
            OrganizationResolutionOutcome outcome = !quorum ? OrganizationResolutionOutcome.FailedQuorum : threshold ? OrganizationResolutionOutcome.Adopted : tally.ApproveWeight == tally.RejectWeight ? OrganizationResolutionOutcome.Tied : OrganizationResolutionOutcome.Rejected;
            OrganizationResolutionLifecycleState resolutionState = outcome == OrganizationResolutionOutcome.Adopted
                ? proposal.requestedExecutionOperations.Length > 0 ? OrganizationResolutionLifecycleState.ExecutionPending : OrganizationResolutionLifecycleState.Adopted
                : OrganizationResolutionLifecycleState.Rejected;
            OrganizationResolutionRecordData resolution = new OrganizationResolutionRecordData
            {
                resolutionId = request.resolutionId.Trim(),
                proposalId = proposal.proposalId,
                organizationId = proposal.organizationId,
                outcome = outcome,
                lifecycleState = resolutionState,
                approveWeight = tally.ApproveWeight,
                rejectWeight = tally.RejectWeight,
                abstainWeight = tally.AbstainWeight,
                eligibleCount = tally.EligibleCount,
                participatingCount = tally.ParticipatingCount,
                adoptedWorldTime = request.worldTime
            };
            if (request.preview) return OrganizationDecisionOperationResult.Success("Vote close preview succeeded.", before, before, preview: true, subjectId: resolution.resolutionId, authorization: authorization, resolution: resolution);
            authorization = Authorize(request.transactionId, request.actorPersonId, proposal.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.CloseOrganizationVoteActionId, request.resolutionId, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            proposal.lifecycleState = outcome == OrganizationResolutionOutcome.Adopted ? (proposal.requestedExecutionOperations.Length > 0 ? OrganizationProposalLifecycleState.ExecutionPending : OrganizationProposalLifecycleState.Passed) : OrganizationProposalLifecycleState.Failed;
            proposal.closedWorldTime = request.worldTime;
            proposal.resolutionId = resolution.resolutionId;
            proposal.revision++;
            resolutionsById.Add(resolution.resolutionId, resolution);
            Commit(request.transactionId, "close-vote", resolution.resolutionId, proposal.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Vote closed.", before, Revision, subjectId: resolution.resolutionId, authorization: authorization, resolution: resolution);
        }

        public OrganizationDecisionOperationResult VetoResolution(OrganizationResolutionActionRequest request)
        {
            request ??= new OrganizationResolutionActionRequest();
            return ChangeResolution(request, "veto-resolution", PrototypeOrganizationAuthorityDefinitionFactory.VetoOrganizationResolutionActionId, OrganizationResolutionLifecycleState.Vetoed, OrganizationResolutionOutcome.Vetoed);
        }

        public OrganizationDecisionOperationResult OverrideVeto(OrganizationResolutionActionRequest request)
        {
            request ??= new OrganizationResolutionActionRequest();
            return ChangeResolution(request, "override-veto", PrototypeOrganizationAuthorityDefinitionFactory.OverrideOrganizationVetoActionId, OrganizationResolutionLifecycleState.ExecutionPending, OrganizationResolutionOutcome.OverrideSucceeded);
        }

        public OrganizationDecisionOperationResult ExecuteResolution(OrganizationDecisionExecutionRequest request)
        {
            request ??= new OrganizationDecisionExecutionRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.executionId) || string.IsNullOrWhiteSpace(request.resolutionId)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Transaction, execution, and resolution IDs are required.", request.preview);
            if (!resolutionsById.TryGetValue(request.resolutionId, out OrganizationResolutionRecordData resolution)) return Fail(OrganizationDecisionOperationCode.MissingResolution, $"Resolution '{request.resolutionId}' is missing.", request.preview);
            if (resolution.lifecycleState != OrganizationResolutionLifecycleState.ExecutionPending && resolution.lifecycleState != OrganizationResolutionLifecycleState.Adopted) return Fail(OrganizationDecisionOperationCode.InvalidLifecycle, $"Resolution '{resolution.resolutionId}' is {resolution.lifecycleState}.", request.preview);
            if (!proposalsById.TryGetValue(resolution.proposalId, out OrganizationProposalRecordData proposal)) return Fail(OrganizationDecisionOperationCode.MissingProposal, "Resolution proposal is missing.", request.preview);
            if (executionsById.TryGetValue(request.executionId, out OrganizationDecisionExecutionRecordData existing)) return Duplicate("execute-resolution", request.transactionId, request.executionId, existing.resolutionId == resolution.resolutionId, before, request.preview, execution: existing);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, resolution.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId, request.executionId, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationDecisionExecutionRecordData execution = new OrganizationDecisionExecutionRecordData
            {
                executionId = request.executionId.Trim(),
                resolutionId = resolution.resolutionId,
                organizationId = resolution.organizationId,
                operations = proposal.requestedExecutionOperations.Select(item => item.Clone()).ToArray(),
                lifecycleState = request.preview ? OrganizationDecisionExecutionState.Previewed : OrganizationDecisionExecutionState.Pending,
                preparedWorldTime = request.worldTime
            };
            OrganizationDecisionRuntimeSaveData localRollback = CreateSaveData();
            OrganizationResourceRuntimeSaveData resourceRollback = resources?.CreateSaveData();
            bool success = ApplyExecutionOperations(execution, request, resolution, request.preview, out string failure);
            if (request.preview)
            {
                RestoreLocal(localRollback);
                if (resources != null && resourceRollback != null) resources.RestoreFromSaveData(resourceRollback, registry, organizations, authority, economy, worldId);
                return success
                    ? OrganizationDecisionOperationResult.Success("Execution preview succeeded.", before, before, preview: true, subjectId: execution.executionId, authorization: authorization, execution: execution)
                    : Fail(OrganizationDecisionOperationCode.ExecutionFailed, failure, true, authorization);
            }

            if (!success)
            {
                RestoreLocal(localRollback);
                if (resources != null && resourceRollback != null) resources.RestoreFromSaveData(resourceRollback, registry, organizations, authority, economy, worldId);
                return Fail(OrganizationDecisionOperationCode.ExecutionFailed, failure, false, authorization);
            }

            authorization = Authorize(request.transactionId, request.actorPersonId, resolution.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionActionId, request.executionId, request.worldTime, false, true);
            if (!authorization.Succeeded)
            {
                RestoreLocal(localRollback);
                if (resources != null && resourceRollback != null) resources.RestoreFromSaveData(resourceRollback, registry, organizations, authority, economy, worldId);
                return Unauthorized(authorization, false);
            }
            execution.lifecycleState = OrganizationDecisionExecutionState.Succeeded;
            execution.executedWorldTime = request.worldTime;
            resolution.lifecycleState = OrganizationResolutionLifecycleState.Executed;
            resolution.revision++;
            proposal.lifecycleState = OrganizationProposalLifecycleState.Executed;
            proposal.revision++;
            executionsById.Add(execution.executionId, execution);
            Commit(request.transactionId, "execute-resolution", execution.executionId, execution.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Resolution executed.", before, Revision, subjectId: execution.executionId, authorization: authorization, resolution: resolution, execution: execution);
        }

        public int ProcessScheduled(double worldTime, int maxOperations = 64)
        {
            int processed = 0;
            foreach (OrganizationPolicyRecordData policy in policiesById.Values.OrderBy(item => item.effectiveStartWorldTime).ThenBy(item => item.policyId, StringComparer.Ordinal).ToArray())
            {
                if (processed >= maxOperations) break;
                if (policy.lifecycleState == OrganizationPolicyLifecycleState.Scheduled && policy.effectiveStartWorldTime <= worldTime) { policy.lifecycleState = OrganizationPolicyLifecycleState.Active; policy.revision++; Revision++; processed++; }
                if (policy.lifecycleState == OrganizationPolicyLifecycleState.Active && policy.effectiveEndWorldTime >= 0d && policy.effectiveEndWorldTime <= worldTime) { policy.lifecycleState = OrganizationPolicyLifecycleState.Expired; policy.revision++; Revision++; processed++; }
            }
            foreach (OrganizationGoalRecordData goal in goalsById.Values.OrderBy(item => item.deadlineWorldTime).ThenBy(item => item.goalId, StringComparer.Ordinal).ToArray())
            {
                if (processed >= maxOperations) break;
                if (registry.TryGet(goal.goalDefinitionId, out OrganizationGoalDefinition definition)) EvaluateGoalRecord(goal, definition, worldTime);
                if (goal.lifecycleState == OrganizationGoalLifecycleState.Active && goal.deadlineWorldTime >= 0d && goal.deadlineWorldTime <= worldTime && goal.currentValue < goal.targetValue) { goal.lifecycleState = OrganizationGoalLifecycleState.Expired; goal.revision++; Revision++; processed++; }
                if (goal.lifecycleState == OrganizationGoalLifecycleState.Active && goal.currentValue >= goal.targetValue && definition.CompletionPolicy == OrganizationGoalCompletionPolicy.Automatic) { goal.lifecycleState = OrganizationGoalLifecycleState.Completed; goal.completedWorldTime = worldTime; goal.revision++; Revision++; processed++; }
            }
            if (processed > 0) IsDirty = true;
            return processed;
        }

        public OrganizationDecisionProjection GetProposalProjection(string proposalId, OrganizationDecisionProjectionAccess access)
        {
            if (!proposalsById.TryGetValue(proposalId ?? string.Empty, out OrganizationProposalRecordData proposal)) return new OrganizationDecisionProjection(OrganizationDecisionProjectionAccess.Denied, proposalId, false, null, null, null, null);
            OrganizationProposalRecordData projected = proposal.Clone();
            bool redacted = access == OrganizationDecisionProjectionAccess.Redacted;
            if (redacted)
            {
                projected.proposerPersonId = string.Empty;
                projected.requestedExecutionOperations = Array.Empty<OrganizationDecisionExecutionOperationData>();
            }
            return new OrganizationDecisionProjection(access, proposal.proposalId, redacted, null, null, projected, null);
        }

        public OrganizationDecisionRuntimeSaveData CreateSaveData() => new OrganizationDecisionRuntimeSaveData
        {
            worldId = worldId,
            revision = Revision,
            goals = Goals.Select(item => item.Clone()).ToList(),
            policies = Policies.Select(item => item.Clone()).ToList(),
            proposals = Proposals.Select(item => item.Clone()).ToList(),
            amendments = Amendments.Select(item => item.Clone()).ToList(),
            voterRolls = VoterRolls.Select(item => item.Clone()).ToList(),
            votes = Votes.Select(item => item.Clone()).ToList(),
            resolutions = Resolutions.Select(item => item.Clone()).ToList(),
            executions = Executions.Select(item => item.Clone()).ToList(),
            transactions = transactionsById.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToList()
        };

        public OrganizationDecisionOperationResult RestoreFromSaveData(OrganizationDecisionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationResourceRuntime resourceRuntime, string world, IEnumerable<string> persons, bool restoring = true)
        {
            long before = Revision;
            OrganizationDecisionRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry oldRegistry = registry;
            OrganizationRuntime oldOrganizations = organizations;
            OrganizationMembershipRuntime oldMemberships = memberships;
            OrganizationAuthorityRuntime oldAuthority = authority;
            OrganizationResourceRuntime oldResources = resources;
            string oldWorld = worldId;
            string[] oldPersons = knownPersonIds.ToArray();
            Configure(definitionRegistry, organizationRuntime, membershipRuntime, authorityRuntime, resourceRuntime, world, persons);
            if (!ValidateSaveData(saveData, registry, organizations, memberships, authority, resources, worldId, knownPersonIds, out string failure))
            {
                registry = oldRegistry; organizations = oldOrganizations; memberships = oldMemberships; authority = oldAuthority; resources = oldResources; worldId = oldWorld; knownPersonIds.Clear(); foreach (string person in oldPersons) knownPersonIds.Add(person);
                return OrganizationDecisionOperationResult.Failure(OrganizationDecisionOperationCode.RestoreFailed, failure, before);
            }
            try
            {
                RestoreLocal(saveData);
                IsDirty = false;
                return OrganizationDecisionOperationResult.Success("Organization decisions restored.", before, Revision);
            }
            catch (Exception ex)
            {
                RestoreLocal(rollback);
                registry = oldRegistry; organizations = oldOrganizations; memberships = oldMemberships; authority = oldAuthority; resources = oldResources; worldId = oldWorld; knownPersonIds.Clear(); foreach (string person in oldPersons) knownPersonIds.Add(person);
                return OrganizationDecisionOperationResult.Failure(OrganizationDecisionOperationCode.RestoreFailed, ex.Message, before);
            }
        }

        public static bool ValidateSaveData(OrganizationDecisionRuntimeSaveData saveData, DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationMembershipRuntime memberships, OrganizationAuthorityRuntime authority, OrganizationResourceRuntime resources, string world, IEnumerable<string> persons, out string failure)
        {
            failure = string.Empty;
            if (saveData == null) return Invalid("Organization decision save data is missing.", out failure);
            if (saveData.schemaVersion != OrganizationDecisionRuntimeSaveData.CurrentSchemaVersion) return Invalid($"Unsupported organization decision schema version {saveData.schemaVersion}.", out failure);
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.IsNullOrWhiteSpace(world) && saveData.worldId != world) return Invalid("Organization decision save world does not match participant owner.", out failure);
            if (!Unique(saveData.goals, item => item.goalId, "goal", out failure) || !Unique(saveData.policies, item => item.policyId, "policy", out failure) || !Unique(saveData.proposals, item => item.proposalId, "proposal", out failure) || !Unique(saveData.amendments, item => item.amendmentId, "amendment", out failure) || !Unique(saveData.voterRolls, item => item.voterRollId, "voter roll", out failure) || !Unique(saveData.votes, item => item.voteId, "vote", out failure) || !Unique(saveData.resolutions, item => item.resolutionId, "resolution", out failure) || !Unique(saveData.executions, item => item.executionId, "execution", out failure) || !Unique(saveData.transactions, item => item.transactionId, "transaction", out failure)) return false;
            foreach (OrganizationGoalRecordData goal in saveData.goals ?? new List<OrganizationGoalRecordData>())
            {
                if (!ExistsOrganization(organizations, goal.organizationId)) return Invalid($"Goal '{goal.goalId}' references missing Organization '{goal.organizationId}'.", out failure);
                if (registry == null || !registry.TryGet(goal.goalDefinitionId, out OrganizationGoalDefinition _)) return Invalid($"Goal '{goal.goalId}' references missing goal definition '{goal.goalDefinitionId}'.", out failure);
            }
            foreach (OrganizationPolicyRecordData policy in saveData.policies ?? new List<OrganizationPolicyRecordData>())
            {
                if (!ExistsOrganization(organizations, policy.organizationId)) return Invalid($"Policy '{policy.policyId}' references missing Organization '{policy.organizationId}'.", out failure);
                if (registry == null || !registry.TryGet(policy.policyDefinitionId, out OrganizationPolicyDefinition definition)) return Invalid($"Policy '{policy.policyId}' references missing policy definition '{policy.policyDefinitionId}'.", out failure);
                if (!ValidatePolicyParameters(definition, policy.parameters, out failure)) return false;
            }
            HashSet<string> proposals = new HashSet<string>((saveData.proposals ?? new List<OrganizationProposalRecordData>()).Select(item => item.proposalId), StringComparer.Ordinal);
            HashSet<string> rolls = new HashSet<string>((saveData.voterRolls ?? new List<OrganizationVoterRollRecordData>()).Select(item => item.voterRollId), StringComparer.Ordinal);
            foreach (OrganizationProposalRecordData proposal in saveData.proposals ?? new List<OrganizationProposalRecordData>())
            {
                if (!ExistsOrganization(organizations, proposal.organizationId)) return Invalid($"Proposal '{proposal.proposalId}' references missing Organization '{proposal.organizationId}'.", out failure);
                if (registry == null || !registry.TryGet(proposal.proposalDefinitionId, out OrganizationProposalDefinition definition)) return Invalid($"Proposal '{proposal.proposalId}' references missing proposal definition '{proposal.proposalDefinitionId}'.", out failure);
                if (!rolls.Contains(proposal.voterRollId)) return Invalid($"Proposal '{proposal.proposalId}' references missing voter roll '{proposal.voterRollId}'.", out failure);
                if (!ValidateExecutionOperations(proposal, definition, registry, resources, out failure)) return false;
            }
            foreach (OrganizationVoteRecordData vote in saveData.votes ?? new List<OrganizationVoteRecordData>())
            {
                if (!proposals.Contains(vote.proposalId) || !rolls.Contains(vote.voterRollId)) return Invalid($"Vote '{vote.voteId}' references an invalid proposal or voter roll.", out failure);
            }
            foreach (OrganizationResolutionRecordData resolution in saveData.resolutions ?? new List<OrganizationResolutionRecordData>())
            {
                if (!proposals.Contains(resolution.proposalId)) return Invalid($"Resolution '{resolution.resolutionId}' references missing proposal '{resolution.proposalId}'.", out failure);
            }
            return true;
        }

        public void Reset()
        {
            ClearOwnedState();
            Revision = 0L;
            IsDirty = false;
        }

        public void Dispose()
        {
            disposed = true;
            ClearOwnedState();
            OperationCommitted = null;
        }

        private bool ApplyExecutionOperations(OrganizationDecisionExecutionRecordData execution, OrganizationDecisionExecutionRequest request, OrganizationResolutionRecordData resolution, bool preview, out string failure)
        {
            failure = string.Empty;
            foreach (OrganizationDecisionExecutionOperationData operation in execution.operations)
            {
                if (operation.kind == OrganizationDecisionExecutionOperationKind.AdoptPolicy)
                {
                    OrganizationPolicyRecordData policy = operation.policyPayload?.Clone();
                    if (policy == null) { if (operation.required) return Invalid("Policy payload is missing.", out failure); operation.state = OrganizationDecisionExecutionState.SkippedOptional; continue; }
                    OrganizationDecisionOperationResult result = CreatePolicy(new OrganizationPolicyRequest
                    {
                        transactionId = $"{request.transactionId}.{operation.operationId}.policy",
                        policyId = string.IsNullOrWhiteSpace(policy.policyId) ? operation.targetId : policy.policyId,
                        organizationId = resolution.organizationId,
                        policyDefinitionId = policy.policyDefinitionId,
                        displayName = policy.displayName,
                        scope = policy.scope,
                        parameters = policy.parameters,
                        priority = policy.priority,
                        actorPersonId = request.actorPersonId,
                        adoptedWorldTime = request.worldTime,
                        effectiveStartWorldTime = policy.effectiveStartWorldTime <= 0d ? request.worldTime : policy.effectiveStartWorldTime,
                        effectiveEndWorldTime = policy.effectiveEndWorldTime,
                        sourceResolutionId = resolution.resolutionId,
                        visibility = policy.visibility,
                        preview = preview
                    });
                    operation.state = result.Succeeded ? OrganizationDecisionExecutionState.Succeeded : OrganizationDecisionExecutionState.Failed;
                    operation.message = result.Message;
                    if (!result.Succeeded && operation.required) { failure = result.Message; return false; }
                }
                else if (operation.kind == OrganizationDecisionExecutionOperationKind.EstablishGoal)
                {
                    OrganizationGoalRecordData goal = operation.goalPayload?.Clone();
                    if (goal == null) { if (operation.required) return Invalid("Goal payload is missing.", out failure); operation.state = OrganizationDecisionExecutionState.SkippedOptional; continue; }
                    OrganizationDecisionOperationResult result = CreateGoal(new OrganizationGoalRequest
                    {
                        transactionId = $"{request.transactionId}.{operation.operationId}.goal",
                        goalId = string.IsNullOrWhiteSpace(goal.goalId) ? operation.targetId : goal.goalId,
                        organizationId = resolution.organizationId,
                        goalDefinitionId = goal.goalDefinitionId,
                        displayName = goal.displayName,
                        targetSubject = goal.targetSubject,
                        targetValue = goal.targetValue,
                        priority = goal.priority,
                        actorPersonId = request.actorPersonId,
                        worldTime = request.worldTime,
                        deadlineWorldTime = goal.deadlineWorldTime,
                        sourceResolutionId = resolution.resolutionId,
                        visibility = goal.visibility,
                        preview = preview
                    });
                    operation.state = result.Succeeded ? OrganizationDecisionExecutionState.Succeeded : OrganizationDecisionExecutionState.Failed;
                    operation.message = result.Message;
                    if (!result.Succeeded && operation.required) { failure = result.Message; return false; }
                }
                else if (operation.kind == OrganizationDecisionExecutionOperationKind.ApproveBudget)
                {
                    if (resources == null) { if (operation.required) return Invalid("Organization resource runtime is unavailable.", out failure); operation.state = OrganizationDecisionExecutionState.SkippedOptional; continue; }
                    OrganizationResourceOperationResult result = resources.CreateBudget(new OrganizationBudgetRequest
                    {
                        transactionId = $"{request.transactionId}.{operation.operationId}.budget",
                        budgetId = string.IsNullOrWhiteSpace(operation.targetId) ? $"organization-budget.{execution.executionId}.{operation.operationId}" : operation.targetId,
                        organizationId = resolution.organizationId,
                        treasuryId = operation.treasuryId,
                        accountId = operation.accountId,
                        currencyDefinitionId = operation.currencyDefinitionId,
                        authorizedUnits = operation.units,
                        purpose = operation.purpose,
                        actorPersonId = request.actorPersonId,
                        startWorldTime = request.worldTime,
                        preview = preview
                    });
                    operation.destinationTransactionId = $"{request.transactionId}.{operation.operationId}.budget";
                    operation.state = result.Succeeded ? OrganizationDecisionExecutionState.Succeeded : OrganizationDecisionExecutionState.Failed;
                    operation.message = result.Message;
                    if (!result.Succeeded && operation.required) { failure = result.Message; return false; }
                }
                else
                {
                    operation.state = operation.required ? OrganizationDecisionExecutionState.Failed : OrganizationDecisionExecutionState.SkippedOptional;
                    operation.message = $"Unsupported execution operation '{operation.kind}'.";
                    if (operation.required) { failure = operation.message; return false; }
                }
            }
            return true;
        }

        private OrganizationDecisionOperationResult ChangeResolution(OrganizationResolutionActionRequest request, string operation, string actionId, OrganizationResolutionLifecycleState targetState, OrganizationResolutionOutcome targetOutcome)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationDecisionOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.resolutionId)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, "Transaction and resolution IDs are required.", request.preview);
            if (!resolutionsById.TryGetValue(request.resolutionId, out OrganizationResolutionRecordData resolution)) return Fail(OrganizationDecisionOperationCode.MissingResolution, $"Resolution '{request.resolutionId}' is missing.", request.preview);
            if (transactionsById.ContainsKey(request.transactionId)) return OrganizationDecisionOperationResult.Success("Resolution operation already applied.", before, before, duplicate: true, subjectId: resolution.resolutionId, resolution: resolution);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, resolution.organizationId, actionId, resolution.resolutionId, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationResolutionRecordData preview = resolution.Clone();
            preview.lifecycleState = targetState;
            preview.outcome = targetOutcome;
            if (targetOutcome == OrganizationResolutionOutcome.Vetoed) { preview.vetoedByPersonId = request.actorPersonId ?? string.Empty; preview.vetoedWorldTime = request.worldTime; }
            if (request.preview) return OrganizationDecisionOperationResult.Success("Resolution action preview succeeded.", before, before, preview: true, subjectId: preview.resolutionId, authorization: authorization, resolution: preview);
            authorization = Authorize(request.transactionId, request.actorPersonId, resolution.organizationId, actionId, resolution.resolutionId, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            resolution.lifecycleState = targetState;
            resolution.outcome = targetOutcome;
            if (targetOutcome == OrganizationResolutionOutcome.Vetoed) { resolution.vetoedByPersonId = request.actorPersonId ?? string.Empty; resolution.vetoedWorldTime = request.worldTime; }
            resolution.revision++;
            Commit(request.transactionId, operation, resolution.resolutionId, resolution.organizationId, request.worldTime);
            return OrganizationDecisionOperationResult.Success("Resolution action recorded.", before, Revision, subjectId: resolution.resolutionId, authorization: authorization, resolution: resolution);
        }

        private bool CanMutate(out OrganizationDecisionOperationResult failure)
        {
            failure = null;
            if (disposed) { failure = OrganizationDecisionOperationResult.Failure(OrganizationDecisionOperationCode.Disposed, "Organization decision runtime is disposed.", Revision); return false; }
            if (!IsReady) { failure = OrganizationDecisionOperationResult.Failure(OrganizationDecisionOperationCode.MissingDependency, "Organization decision runtime dependencies are missing.", Revision); return false; }
            return true;
        }

        private bool TryActiveOrganization(string organizationId, out string failure)
        {
            failure = string.Empty;
            if (!organizations.TryGetSnapshot(organizationId ?? string.Empty, out OrganizationSnapshot snapshot)) return Invalid($"Organization '{organizationId}' was not found.", out failure);
            if (snapshot.LifecycleState != OrganizationLifecycleState.Active) return Invalid($"Organization '{organizationId}' is {snapshot.LifecycleState}.", out failure);
            return true;
        }

        private OrganizationAuthorizationResult Authorize(string operationId, string actorId, string organizationId, string actionId, string targetRecordId, double worldTime, bool preview, bool consume)
        {
            return authority.EvaluateAuthorization(new OrganizationAuthorizationRequest
            {
                operationId = operationId ?? string.Empty,
                actorPersonId = actorId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                actionDefinitionId = actionId ?? string.Empty,
                scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                targetRecordId = targetRecordId ?? string.Empty,
                worldTime = worldTime,
                preview = preview,
                consumeApprovals = consume
            });
        }

        private OrganizationDecisionOperationResult Unauthorized(OrganizationAuthorizationResult authorization, bool preview) => Fail(OrganizationDecisionOperationCode.Unauthorized, authorization?.Message ?? "Organization authority denied the operation.", preview, authorization);
        private OrganizationDecisionOperationResult Fail(OrganizationDecisionOperationCode code, string message, bool preview, OrganizationAuthorizationResult authorization = null) => OrganizationDecisionOperationResult.Failure(code, message, Revision, preview, authorization);

        private OrganizationDecisionOperationResult Duplicate(string operation, string transactionId, string subjectId, bool same, long before, bool preview, OrganizationGoalRecordData goal = null, OrganizationPolicyRecordData policy = null, OrganizationProposalRecordData proposal = null, OrganizationVoteRecordData vote = null, OrganizationResolutionRecordData resolution = null, OrganizationDecisionExecutionRecordData execution = null)
        {
            if (!same) return Fail(OrganizationDecisionOperationCode.InvalidRequest, $"'{subjectId}' already exists with different data.", preview);
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out OrganizationDecisionTransactionRecordData transaction)) return Fail(OrganizationDecisionOperationCode.InvalidRequest, $"'{subjectId}' already exists; replay requires its original transaction ID.", preview);
            if (transaction.operation != operation || transaction.subjectId != subjectId) return Fail(OrganizationDecisionOperationCode.InvalidRequest, $"Transaction '{transactionId}' was already used for another operation.", preview);
            return OrganizationDecisionOperationResult.Success("Organization decision operation already applied.", before, before, duplicate: true, subjectId: subjectId, goal: goal, policy: policy, proposal: proposal, vote: vote, resolution: resolution, execution: execution);
        }

        private void Commit(string transactionId, string operation, string subjectId, string organizationId, double worldTime)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) throw new InvalidOperationException("Stable transaction ID is required.");
            if (transactionsById.ContainsKey(transactionId)) throw new InvalidOperationException($"Transaction '{transactionId}' was already committed.");
            OrganizationDecisionTransactionRecordData transaction = new OrganizationDecisionTransactionRecordData { transactionId = transactionId.Trim(), operation = operation ?? string.Empty, subjectId = subjectId ?? string.Empty, organizationId = organizationId ?? string.Empty, worldTime = worldTime, revision = Revision + 1L };
            transactionsById.Add(transaction.transactionId, transaction);
            Revision++;
            IsDirty = true;
            PublishCommitted(transaction);
        }

        private void RestoreLocal(OrganizationDecisionRuntimeSaveData saveData)
        {
            ClearOwnedState();
            OrganizationDecisionRuntimeSaveData clean = saveData?.Clone() ?? new OrganizationDecisionRuntimeSaveData();
            foreach (OrganizationGoalRecordData item in clean.goals) goalsById.Add(item.goalId, item);
            foreach (OrganizationPolicyRecordData item in clean.policies) policiesById.Add(item.policyId, item);
            foreach (OrganizationProposalRecordData item in clean.proposals) proposalsById.Add(item.proposalId, item);
            foreach (OrganizationAmendmentRecordData item in clean.amendments) amendmentsById.Add(item.amendmentId, item);
            foreach (OrganizationVoterRollRecordData item in clean.voterRolls) voterRollsById.Add(item.voterRollId, item);
            foreach (OrganizationVoteRecordData item in clean.votes) votesById.Add(item.voteId, item);
            foreach (OrganizationResolutionRecordData item in clean.resolutions) resolutionsById.Add(item.resolutionId, item);
            foreach (OrganizationDecisionExecutionRecordData item in clean.executions) executionsById.Add(item.executionId, item);
            foreach (OrganizationDecisionTransactionRecordData item in clean.transactions) transactionsById.Add(item.transactionId, item);
            Revision = Math.Max(0L, clean.revision);
        }

        private void ClearOwnedState()
        {
            goalsById.Clear(); policiesById.Clear(); proposalsById.Clear(); amendmentsById.Clear(); voterRollsById.Clear(); votesById.Clear(); resolutionsById.Clear(); executionsById.Clear(); transactionsById.Clear(); eventDeliveryDiagnostics.Clear();
        }

        private void PublishCommitted(OrganizationDecisionTransactionRecordData transaction)
        {
            Action<OrganizationDecisionCommittedEvent> handlers = OperationCommitted;
            if (handlers == null) return;
            OrganizationDecisionCommittedEvent payload = new OrganizationDecisionCommittedEvent(transaction, Revision);
            foreach (Action<OrganizationDecisionCommittedEvent> handler in handlers.GetInvocationList().Cast<Action<OrganizationDecisionCommittedEvent>>())
            {
                try { handler(payload); }
                catch (Exception ex)
                {
                    eventDeliveryDiagnostics.Add($"{transaction.transactionId}:{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}:{ex.GetType().Name}:{ex.Message}");
                    if (eventDeliveryDiagnostics.Count > 32) eventDeliveryDiagnostics.RemoveAt(0);
                }
            }
        }

        private void EvaluateGoalRecord(OrganizationGoalRecordData goal, OrganizationGoalDefinition definition, double worldTime)
        {
            if (definition.ProgressSourceKind == OrganizationGoalProgressSourceKind.ExplicitContribution)
            {
                goal.currentValue = goal.progressContributions.Sum(item => item.units);
            }
            else if (definition.ProgressSourceKind == OrganizationGoalProgressSourceKind.ActiveMembershipCount)
            {
                goal.currentValue = memberships.QueryMemberships(organizationId: goal.organizationId, activeOnly: true).Count;
            }
            else if (definition.ProgressSourceKind == OrganizationGoalProgressSourceKind.TreasuryBalance && resources != null && goal.targetSubject != null)
            {
                goal.currentValue = Math.Max(0L, resources.GetBalance(goal.targetSubject.subjectId, worldTime)?.BalanceUnits ?? 0L);
            }
            if (goal.currentValue >= goal.targetValue && definition.CompletionPolicy == OrganizationGoalCompletionPolicy.Automatic)
            {
                goal.lifecycleState = OrganizationGoalLifecycleState.Completed;
                goal.completedWorldTime = worldTime;
            }
        }

        private OrganizationVoterRollRecordData BuildVoterRoll(string rollId, string proposalId, string organizationId, OrganizationDecisionProcedureDefinition procedure, double worldTime)
        {
            IEnumerable<string> voters = procedure.VoterEligibility == OrganizationVoterEligibilityKind.ActiveMembers
                ? memberships.QueryMemberships(organizationId: organizationId, activeOnly: true).Select(item => item.PersonId)
                : procedure.VoterEligibility == OrganizationVoterEligibilityKind.AuthorityPermissionHolders
                    ? knownPersonIds.Where(person => procedure.EligiblePermissionDefinitionIds.Any(permission => HasPermission(person, organizationId, permission, worldTime)))
                    : knownPersonIds;
            return new OrganizationVoterRollRecordData
            {
                voterRollId = rollId,
                proposalId = proposalId,
                organizationId = organizationId,
                procedureDefinitionId = procedure.Id,
                eligiblePersonIds = OrganizationModelUtility.Clean(voters),
                secretBallot = procedure.SecretBallot,
                createdWorldTime = worldTime
            };
        }

        private bool HasPermission(string personId, string organizationId, string permissionId, double worldTime)
        {
            OrganizationAuthorizationResult result = authority.EvaluateAuthorization(new OrganizationAuthorizationRequest
            {
                operationId = $"decision.permission-preview.{personId}.{permissionId}",
                actorPersonId = personId,
                organizationId = organizationId,
                requiredPermissionIds = new[] { permissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                worldTime = worldTime,
                preview = true
            });
            return result.Succeeded;
        }

        private static bool ValidatePolicyParameters(OrganizationPolicyDefinition definition, IEnumerable<OrganizationPolicyParameterValueData> values, out string failure)
        {
            failure = string.Empty;
            Dictionary<string, OrganizationPolicyParameterValueData> byId = (values ?? Array.Empty<OrganizationPolicyParameterValueData>()).Where(item => item != null).GroupBy(item => item.parameterId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (OrganizationPolicyParameterSchemaData schema in definition.ParameterSchema)
            {
                if (schema.required && !byId.ContainsKey(schema.parameterId)) return Invalid($"Required policy parameter '{schema.parameterId}' is missing.", out failure);
                if (!byId.TryGetValue(schema.parameterId, out OrganizationPolicyParameterValueData value)) continue;
                if (value.type != schema.type) return Invalid($"Policy parameter '{schema.parameterId}' type '{value.type}' does not match schema '{schema.type}'.", out failure);
                if (schema.allowedValues != null && schema.allowedValues.Length > 0 && !schema.allowedValues.Contains(value.stringValue ?? string.Empty, StringComparer.Ordinal)) return Invalid($"Policy parameter '{schema.parameterId}' has a value not allowed by definition.", out failure);
            }
            return true;
        }

        private static OrganizationPolicyParameterValueData[] MergeParameters(OrganizationPolicyDefinition definition, IEnumerable<OrganizationPolicyParameterValueData> values)
        {
            Dictionary<string, OrganizationPolicyParameterValueData> provided = (values ?? Array.Empty<OrganizationPolicyParameterValueData>()).Where(item => item != null).ToDictionary(item => item.parameterId, item => item.Clone(), StringComparer.Ordinal);
            foreach (OrganizationPolicyParameterSchemaData schema in definition.ParameterSchema)
            {
                if (provided.ContainsKey(schema.parameterId) || string.IsNullOrWhiteSpace(schema.defaultValue)) continue;
                provided[schema.parameterId] = new OrganizationPolicyParameterValueData { parameterId = schema.parameterId, type = schema.type, stringValue = schema.defaultValue };
            }
            return provided.Values.OrderBy(item => item.parameterId, StringComparer.Ordinal).ToArray();
        }

        private IEnumerable<OrganizationPolicyRecordData> ConflictingPolicies(OrganizationPolicyRecordData record, OrganizationPolicyDefinition definition)
        {
            return policiesById.Values.Where(item => item.organizationId == record.organizationId && item.policyDefinitionId == record.policyDefinitionId && item.IsActiveAt(record.effectiveStartWorldTime) && item.scope.StableKey == record.scope.StableKey && item.policyId != record.policyId);
        }

        private static bool ValidateExecutionOperations(OrganizationProposalRecordData proposal, OrganizationProposalDefinition definition, DefinitionRegistry registry, OrganizationResourceRuntime resources, out string failure)
        {
            failure = string.Empty;
            HashSet<string> operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OrganizationDecisionExecutionOperationData operation in proposal.requestedExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationData>())
            {
                if (operation == null) return Invalid($"Proposal '{proposal.proposalId}' contains a null execution operation.", out failure);
                if (string.IsNullOrWhiteSpace(operation.operationId)) return Invalid($"Proposal '{proposal.proposalId}' contains an execution operation without a stable ID.", out failure);
                if (!operationIds.Add(operation.operationId)) return Invalid($"Proposal '{proposal.proposalId}' contains duplicate execution operation '{operation.operationId}'.", out failure);
                if (operation.kind == OrganizationDecisionExecutionOperationKind.Unknown) return Invalid($"Proposal '{proposal.proposalId}' contains an unknown execution operation.", out failure);
                if (!definition.SupportedExecutionOperations.Contains(operation.kind)) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' uses unsupported operation kind '{operation.kind}'.", out failure);
                if (!ValidateExecutionOperationPayload(proposal, operation, registry, resources, out failure)) return false;
            }
            return true;
        }

        private static bool ValidateExecutionOperationPayload(OrganizationProposalRecordData proposal, OrganizationDecisionExecutionOperationData operation, DefinitionRegistry registry, OrganizationResourceRuntime resources, out string failure)
        {
            failure = string.Empty;
            if (operation.kind == OrganizationDecisionExecutionOperationKind.AdoptPolicy)
            {
                if (operation.required && operation.policyPayload == null) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' requires a policy payload.", out failure);
                if (operation.policyPayload != null)
                {
                    if (registry == null || !registry.TryGet(operation.policyPayload.policyDefinitionId, out OrganizationPolicyDefinition policyDefinition)) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' references missing policy definition '{operation.policyPayload.policyDefinitionId}'.", out failure);
                    if (!ValidatePolicyParameters(policyDefinition, operation.policyPayload.parameters, out failure)) return false;
                }
            }
            else if (operation.kind == OrganizationDecisionExecutionOperationKind.EstablishGoal)
            {
                if (operation.required && operation.goalPayload == null) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' requires a goal payload.", out failure);
                if (operation.goalPayload != null && (registry == null || !registry.TryGet(operation.goalPayload.goalDefinitionId, out OrganizationGoalDefinition _))) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' references missing goal definition '{operation.goalPayload.goalDefinitionId}'.", out failure);
            }
            else if (operation.kind == OrganizationDecisionExecutionOperationKind.ApproveBudget || operation.kind == OrganizationDecisionExecutionOperationKind.AuthorizeExpense)
            {
                if (string.IsNullOrWhiteSpace(operation.treasuryId) || string.IsNullOrWhiteSpace(operation.accountId) || string.IsNullOrWhiteSpace(operation.currencyDefinitionId) || operation.units < 0L) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' has an invalid resource scope or amount.", out failure);
                if (registry == null || !registry.TryGet(operation.currencyDefinitionId, out CurrencyDefinition _)) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' references missing currency '{operation.currencyDefinitionId}'.", out failure);
                if (resources != null)
                {
                    if (!resources.TryGetTreasury(operation.treasuryId, out OrganizationTreasuryRecordData treasury) || treasury.organizationId != proposal.organizationId) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' references missing or foreign treasury '{operation.treasuryId}'.", out failure);
                    if (!resources.TryGetAccount(operation.accountId, out OrganizationAccountRecordData account) || account.organizationId != proposal.organizationId || account.treasuryId != operation.treasuryId || account.currencyDefinitionId != operation.currencyDefinitionId) return Invalid($"Proposal '{proposal.proposalId}' operation '{operation.operationId}' references an invalid account scope.", out failure);
                }
            }
            return true;
        }

        private bool DefinitionAllowsOrganization(IReadOnlyList<string> organizationDefinitionIds, IReadOnlyList<OrganizationCategory> categories, string organizationId)
        {
            if (organizationDefinitionIds.Count == 0 && categories.Count == 0) return true;
            if (!organizations.TryGetSnapshot(organizationId, out OrganizationSnapshot snapshot)) return false;
            if (organizationDefinitionIds.Contains(snapshot.DefinitionId, StringComparer.Ordinal)) return true;
            return registry.TryGet(snapshot.DefinitionId, out OrganizationDefinition definition) && categories.Contains(definition.Category);
        }

        private static bool ScopeMatches(OrganizationPolicyScopeData candidate, OrganizationPolicyScopeData query)
        {
            if (query == null) return true;
            if (candidate == null) return false;
            if (candidate.scopeType == OrganizationPolicyScopeType.EntireOrganization) return candidate.organizationId == query.organizationId;
            if (candidate.scopeType != query.scopeType) return false;
            return candidate.StableKey == query.StableKey;
        }

        private static bool QuorumMet(OrganizationDecisionProcedureDefinition procedure, OrganizationDecisionTallySnapshot tally)
        {
            if (procedure.QuorumKind == OrganizationQuorumKind.None) return true;
            if (procedure.QuorumKind == OrganizationQuorumKind.MinimumCount) return tally.ParticipatingCount >= procedure.QuorumCount;
            if (procedure.QuorumKind == OrganizationQuorumKind.PercentageEligible) return tally.EligibleCount == 0 ? false : tally.ParticipatingCount * 10000 / tally.EligibleCount >= procedure.QuorumPercentageBasisPoints;
            return tally.ParticipatingCount >= procedure.QuorumCount;
        }

        private static bool ThresholdMet(OrganizationDecisionProcedureDefinition procedure, OrganizationDecisionTallySnapshot tally)
        {
            long decisive = tally.ApproveWeight + tally.RejectWeight;
            if (decisive <= 0L) return false;
            if (procedure.ThresholdKind == OrganizationPassageThresholdKind.SimpleMajorityVotesCast) return tally.ApproveWeight > tally.RejectWeight;
            if (procedure.ThresholdKind == OrganizationPassageThresholdKind.AbsoluteMajorityVotesCast) return tally.ApproveWeight * 10000 / decisive >= Math.Max(5001, procedure.ThresholdBasisPoints);
            if (procedure.ThresholdKind == OrganizationPassageThresholdKind.TwoThirdsVotesCast) return tally.ApproveWeight * 10000 / decisive >= 6667;
            if (procedure.ThresholdKind == OrganizationPassageThresholdKind.MajorityOfEligible) return tally.EligibleCount > 0 && tally.ApproveWeight * 10000 / tally.EligibleCount >= Math.Max(5001, procedure.ThresholdBasisPoints);
            if (procedure.ThresholdKind == OrganizationPassageThresholdKind.Unanimity) return tally.RejectWeight == 0L && tally.ApproveWeight > 0L;
            if (procedure.ThresholdKind == OrganizationPassageThresholdKind.FixedWeightedThreshold) return tally.ApproveWeight >= procedure.FixedWeightThreshold;
            return tally.ApproveWeight > tally.RejectWeight;
        }

        private static long VoteWeight(OrganizationDecisionProcedureDefinition procedure, string voterPersonId) => procedure.VoteWeight == OrganizationVoteWeightKind.FixedWeight ? Math.Max(1L, procedure.FixedWeightThreshold) : 1L;
        private static string Action(string explicitAction, string fallback) => string.IsNullOrWhiteSpace(explicitAction) ? fallback : explicitAction.Trim();
        private static bool ValidateBase(string transactionId, string subjectId, string organizationId, out string failure)
        {
            failure = string.Empty;
            if (!string.IsNullOrWhiteSpace(transactionId) && !string.IsNullOrWhiteSpace(subjectId) && !string.IsNullOrWhiteSpace(organizationId)) return true;
            failure = "Transaction, subject, and Organization IDs are required.";
            return false;
        }

        private static bool ExistsOrganization(OrganizationRuntime runtime, string organizationId) => runtime != null && runtime.TryGetSnapshot(organizationId ?? string.Empty, out _);
        private static IEnumerable<T> Ordered<T>(IEnumerable<T> source, Func<T, double> worldTime, Func<T, string> id) => (source ?? Array.Empty<T>()).OrderBy(worldTime).ThenBy(id, StringComparer.Ordinal);
        private static bool Invalid(string message, out string failure) { failure = message; return false; }

        private static bool Unique<T>(IEnumerable<T> source, Func<T, string> id, string label, out string failure) where T : class
        {
            failure = string.Empty;
            T[] values = (source ?? Array.Empty<T>()).ToArray();
            if (values.Any(item => item == null || string.IsNullOrWhiteSpace(id(item)))) return Invalid($"Every {label} requires a stable ID.", out failure);
            string duplicate = values.GroupBy(id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
            return string.IsNullOrWhiteSpace(duplicate) || Invalid($"Duplicate {label} ID '{duplicate}'.", out failure);
        }
    }
}
