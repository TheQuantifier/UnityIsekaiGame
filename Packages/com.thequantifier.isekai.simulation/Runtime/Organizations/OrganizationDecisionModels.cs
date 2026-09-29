using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Organizations
{
    [Serializable]
    public sealed class OrganizationDecisionSubjectReferenceData
    {
        public string subjectType;
        public string subjectId;
        public string definitionId;
        public string worldId;

        public string StableKey => $"{subjectType ?? string.Empty}:{worldId ?? string.Empty}:{subjectId ?? string.Empty}";

        public OrganizationDecisionSubjectReferenceData Clone() => new OrganizationDecisionSubjectReferenceData
        {
            subjectType = subjectType ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            worldId = worldId ?? string.Empty
        };
    }

    [Serializable]
    public sealed class OrganizationPolicyParameterValueData
    {
        public string parameterId;
        public OrganizationPolicyParameterType type = OrganizationPolicyParameterType.StringIdentifier;
        public string stringValue;
        public long longValue;
        public bool boolValue;
        public OrganizationDecisionSubjectReferenceData subjectValue;

        public OrganizationPolicyParameterValueData Clone() => new OrganizationPolicyParameterValueData
        {
            parameterId = parameterId ?? string.Empty,
            type = type,
            stringValue = stringValue ?? string.Empty,
            longValue = longValue,
            boolValue = boolValue,
            subjectValue = subjectValue?.Clone()
        };
    }

    [Serializable]
    public sealed class OrganizationPolicyScopeData
    {
        public OrganizationPolicyScopeType scopeType = OrganizationPolicyScopeType.EntireOrganization;
        public string organizationId;
        public string branchOrganizationId;
        public string membershipDefinitionId;
        public string rankDefinitionId;
        public string officeDefinitionId;
        public string treasuryId;
        public string accountId;
        public string propertyId;
        public string businessId;
        public string actionDefinitionId;
        public OrganizationDecisionSubjectReferenceData subject;

        public int Specificity =>
            scopeType == OrganizationPolicyScopeType.SpecificSubject ? 120 :
            scopeType == OrganizationPolicyScopeType.SpecificAction ? 110 :
            scopeType == OrganizationPolicyScopeType.SpecificBusiness ? 100 :
            scopeType == OrganizationPolicyScopeType.SpecificProperty ? 90 :
            scopeType == OrganizationPolicyScopeType.SpecificAccount ? 80 :
            scopeType == OrganizationPolicyScopeType.SpecificTreasury ? 70 :
            scopeType == OrganizationPolicyScopeType.SpecificOffice ? 60 :
            scopeType == OrganizationPolicyScopeType.SpecificRankTrack ? 50 :
            scopeType == OrganizationPolicyScopeType.SpecificMembershipType ? 40 :
            scopeType == OrganizationPolicyScopeType.OrganizationSubtree ? 30 :
            scopeType == OrganizationPolicyScopeType.SpecificBranch ? 20 :
            scopeType == OrganizationPolicyScopeType.EntireOrganization ? 10 : 0;

        public string StableKey => $"{scopeType}:{organizationId ?? string.Empty}:{branchOrganizationId ?? string.Empty}:{membershipDefinitionId ?? string.Empty}:{rankDefinitionId ?? string.Empty}:{officeDefinitionId ?? string.Empty}:{treasuryId ?? string.Empty}:{accountId ?? string.Empty}:{propertyId ?? string.Empty}:{businessId ?? string.Empty}:{actionDefinitionId ?? string.Empty}:{subject?.StableKey ?? string.Empty}";

        public OrganizationPolicyScopeData Clone() => new OrganizationPolicyScopeData
        {
            scopeType = scopeType,
            organizationId = organizationId ?? string.Empty,
            branchOrganizationId = branchOrganizationId ?? string.Empty,
            membershipDefinitionId = membershipDefinitionId ?? string.Empty,
            rankDefinitionId = rankDefinitionId ?? string.Empty,
            officeDefinitionId = officeDefinitionId ?? string.Empty,
            treasuryId = treasuryId ?? string.Empty,
            accountId = accountId ?? string.Empty,
            propertyId = propertyId ?? string.Empty,
            businessId = businessId ?? string.Empty,
            actionDefinitionId = actionDefinitionId ?? string.Empty,
            subject = subject?.Clone()
        };

        public static OrganizationPolicyScopeData EntireOrganization(string organizationId) => new OrganizationPolicyScopeData
        {
            scopeType = OrganizationPolicyScopeType.EntireOrganization,
            organizationId = organizationId ?? string.Empty
        };
    }

    [Serializable]
    public sealed class OrganizationGoalProgressContributionData
    {
        public string contributionId;
        public long units;
        public string sourceRecordId;
        public double worldTime;

        public OrganizationGoalProgressContributionData Clone() => new OrganizationGoalProgressContributionData
        {
            contributionId = contributionId ?? string.Empty,
            units = Math.Max(0L, units),
            sourceRecordId = sourceRecordId ?? string.Empty,
            worldTime = worldTime
        };
    }

    [Serializable]
    public sealed class OrganizationGoalRecordData
    {
        public string goalId;
        public string organizationId;
        public string goalDefinitionId;
        public string displayName;
        public OrganizationGoalLifecycleState lifecycleState = OrganizationGoalLifecycleState.Active;
        public OrganizationDecisionSubjectReferenceData targetSubject;
        public long targetValue;
        public long currentValue;
        public int priority;
        public double createdWorldTime;
        public double activeStartWorldTime;
        public double deadlineWorldTime = -1d;
        public double completedWorldTime = -1d;
        public string sourceProposalId;
        public string sourceResolutionId;
        public string supersededByGoalId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public OrganizationGoalProgressContributionData[] progressContributions = Array.Empty<OrganizationGoalProgressContributionData>();
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => lifecycleState == OrganizationGoalLifecycleState.Active && activeStartWorldTime <= worldTime && (deadlineWorldTime < 0d || deadlineWorldTime > worldTime);

        public OrganizationGoalRecordData Clone() => new OrganizationGoalRecordData
        {
            goalId = goalId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            goalDefinitionId = goalDefinitionId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            lifecycleState = lifecycleState,
            targetSubject = targetSubject?.Clone(),
            targetValue = Math.Max(0L, targetValue),
            currentValue = Math.Max(0L, currentValue),
            priority = priority,
            createdWorldTime = createdWorldTime,
            activeStartWorldTime = activeStartWorldTime,
            deadlineWorldTime = deadlineWorldTime,
            completedWorldTime = completedWorldTime,
            sourceProposalId = sourceProposalId ?? string.Empty,
            sourceResolutionId = sourceResolutionId ?? string.Empty,
            supersededByGoalId = supersededByGoalId ?? string.Empty,
            visibility = visibility,
            progressContributions = (progressContributions ?? Array.Empty<OrganizationGoalProgressContributionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationPolicyRecordData
    {
        public string policyId;
        public string organizationId;
        public string policyDefinitionId;
        public string displayName;
        public OrganizationPolicyLifecycleState lifecycleState = OrganizationPolicyLifecycleState.Active;
        public OrganizationPolicyScopeData scope = new OrganizationPolicyScopeData();
        public OrganizationPolicyParameterValueData[] parameters = Array.Empty<OrganizationPolicyParameterValueData>();
        public int priority;
        public double adoptedWorldTime;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string sourceProposalId;
        public string sourceResolutionId;
        public string supersedesPolicyId;
        public string supersededByPolicyId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => lifecycleState == OrganizationPolicyLifecycleState.Active && effectiveStartWorldTime <= worldTime && (effectiveEndWorldTime < 0d || effectiveEndWorldTime > worldTime);

        public OrganizationPolicyRecordData Clone() => new OrganizationPolicyRecordData
        {
            policyId = policyId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            policyDefinitionId = policyDefinitionId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            lifecycleState = lifecycleState,
            scope = scope?.Clone() ?? new OrganizationPolicyScopeData(),
            parameters = (parameters ?? Array.Empty<OrganizationPolicyParameterValueData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
            priority = priority,
            adoptedWorldTime = adoptedWorldTime,
            effectiveStartWorldTime = effectiveStartWorldTime,
            effectiveEndWorldTime = effectiveEndWorldTime,
            sourceProposalId = sourceProposalId ?? string.Empty,
            sourceResolutionId = sourceResolutionId ?? string.Empty,
            supersedesPolicyId = supersedesPolicyId ?? string.Empty,
            supersededByPolicyId = supersededByPolicyId ?? string.Empty,
            visibility = visibility,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationDecisionExecutionOperationData
    {
        public string operationId;
        public OrganizationDecisionExecutionOperationKind kind = OrganizationDecisionExecutionOperationKind.Unknown;
        public string targetId;
        public string definitionId;
        public OrganizationPolicyRecordData policyPayload;
        public OrganizationGoalRecordData goalPayload;
        public string treasuryId;
        public string accountId;
        public string currencyDefinitionId;
        public long units;
        public string purpose;
        public bool required = true;
        public OrganizationDecisionExecutionState state = OrganizationDecisionExecutionState.Planned;
        public string destinationTransactionId;
        public string message;

        public OrganizationDecisionExecutionOperationData Clone() => new OrganizationDecisionExecutionOperationData
        {
            operationId = operationId ?? string.Empty,
            kind = kind,
            targetId = targetId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            policyPayload = policyPayload?.Clone(),
            goalPayload = goalPayload?.Clone(),
            treasuryId = treasuryId ?? string.Empty,
            accountId = accountId ?? string.Empty,
            currencyDefinitionId = currencyDefinitionId ?? string.Empty,
            units = Math.Max(0L, units),
            purpose = purpose ?? string.Empty,
            required = required,
            state = state,
            destinationTransactionId = destinationTransactionId ?? string.Empty,
            message = message ?? string.Empty
        };
    }

    [Serializable]
    public sealed class OrganizationProposalRecordData
    {
        public string proposalId;
        public string organizationId;
        public string proposalDefinitionId;
        public string title;
        public string proposerPersonId;
        public OrganizationProposalLifecycleState lifecycleState = OrganizationProposalLifecycleState.Submitted;
        public int version = 1;
        public string acceptedAmendmentId;
        public OrganizationDecisionExecutionOperationData[] requestedExecutionOperations = Array.Empty<OrganizationDecisionExecutionOperationData>();
        public string voterRollId;
        public string resolutionId;
        public double submittedWorldTime;
        public double votingStartWorldTime;
        public double votingEndWorldTime;
        public double closedWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public bool IsVoteOpenAt(double worldTime) => lifecycleState == OrganizationProposalLifecycleState.OpenForVoting && votingStartWorldTime <= worldTime && worldTime < votingEndWorldTime;

        public OrganizationProposalRecordData Clone() => new OrganizationProposalRecordData
        {
            proposalId = proposalId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            proposalDefinitionId = proposalDefinitionId ?? string.Empty,
            title = title ?? string.Empty,
            proposerPersonId = proposerPersonId ?? string.Empty,
            lifecycleState = lifecycleState,
            version = Math.Max(1, version),
            acceptedAmendmentId = acceptedAmendmentId ?? string.Empty,
            requestedExecutionOperations = (requestedExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
            voterRollId = voterRollId ?? string.Empty,
            resolutionId = resolutionId ?? string.Empty,
            submittedWorldTime = submittedWorldTime,
            votingStartWorldTime = votingStartWorldTime,
            votingEndWorldTime = votingEndWorldTime,
            closedWorldTime = closedWorldTime,
            visibility = visibility,
            tags = OrganizationModelUtility.Clean(tags),
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationAmendmentRecordData
    {
        public string amendmentId;
        public string proposalId;
        public string organizationId;
        public string proposerPersonId;
        public int targetProposalVersion = 1;
        public string summary;
        public OrganizationDecisionExecutionOperationData[] replacementExecutionOperations = Array.Empty<OrganizationDecisionExecutionOperationData>();
        public OrganizationAmendmentLifecycleState lifecycleState = OrganizationAmendmentLifecycleState.Proposed;
        public double proposedWorldTime;
        public double resolvedWorldTime = -1d;
        public long revision = 1L;

        public OrganizationAmendmentRecordData Clone() => new OrganizationAmendmentRecordData
        {
            amendmentId = amendmentId ?? string.Empty,
            proposalId = proposalId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            proposerPersonId = proposerPersonId ?? string.Empty,
            targetProposalVersion = Math.Max(1, targetProposalVersion),
            summary = summary ?? string.Empty,
            replacementExecutionOperations = (replacementExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
            lifecycleState = lifecycleState,
            proposedWorldTime = proposedWorldTime,
            resolvedWorldTime = resolvedWorldTime,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationVoterRollRecordData
    {
        public string voterRollId;
        public string proposalId;
        public string organizationId;
        public string procedureDefinitionId;
        public string[] eligiblePersonIds = Array.Empty<string>();
        public bool secretBallot;
        public double createdWorldTime;
        public long revision = 1L;

        public OrganizationVoterRollRecordData Clone() => new OrganizationVoterRollRecordData
        {
            voterRollId = voterRollId ?? string.Empty,
            proposalId = proposalId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            procedureDefinitionId = procedureDefinitionId ?? string.Empty,
            eligiblePersonIds = OrganizationModelUtility.Clean(eligiblePersonIds),
            secretBallot = secretBallot,
            createdWorldTime = createdWorldTime,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationVoteRecordData
    {
        public string voteId;
        public string proposalId;
        public string voterRollId;
        public string voterPersonId;
        public OrganizationVoteChoice choice = OrganizationVoteChoice.Unknown;
        public long weight = 1L;
        public OrganizationVoteLifecycleState lifecycleState = OrganizationVoteLifecycleState.Active;
        public double castWorldTime;
        public string replacesVoteId;
        public long revision = 1L;

        public OrganizationVoteRecordData Clone() => new OrganizationVoteRecordData
        {
            voteId = voteId ?? string.Empty,
            proposalId = proposalId ?? string.Empty,
            voterRollId = voterRollId ?? string.Empty,
            voterPersonId = voterPersonId ?? string.Empty,
            choice = choice,
            weight = Math.Max(0L, weight),
            lifecycleState = lifecycleState,
            castWorldTime = castWorldTime,
            replacesVoteId = replacesVoteId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationResolutionRecordData
    {
        public string resolutionId;
        public string proposalId;
        public string organizationId;
        public OrganizationResolutionOutcome outcome = OrganizationResolutionOutcome.Unknown;
        public OrganizationResolutionLifecycleState lifecycleState = OrganizationResolutionLifecycleState.Adopted;
        public long approveWeight;
        public long rejectWeight;
        public long abstainWeight;
        public int eligibleCount;
        public int participatingCount;
        public double adoptedWorldTime;
        public string vetoedByPersonId;
        public double vetoedWorldTime = -1d;
        public string overrideResolutionId;
        public long revision = 1L;

        public OrganizationResolutionRecordData Clone() => new OrganizationResolutionRecordData
        {
            resolutionId = resolutionId ?? string.Empty,
            proposalId = proposalId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            outcome = outcome,
            lifecycleState = lifecycleState,
            approveWeight = Math.Max(0L, approveWeight),
            rejectWeight = Math.Max(0L, rejectWeight),
            abstainWeight = Math.Max(0L, abstainWeight),
            eligibleCount = Math.Max(0, eligibleCount),
            participatingCount = Math.Max(0, participatingCount),
            adoptedWorldTime = adoptedWorldTime,
            vetoedByPersonId = vetoedByPersonId ?? string.Empty,
            vetoedWorldTime = vetoedWorldTime,
            overrideResolutionId = overrideResolutionId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationDecisionExecutionRecordData
    {
        public string executionId;
        public string resolutionId;
        public string organizationId;
        public OrganizationDecisionExecutionOperationData[] operations = Array.Empty<OrganizationDecisionExecutionOperationData>();
        public OrganizationDecisionExecutionState lifecycleState = OrganizationDecisionExecutionState.Planned;
        public double preparedWorldTime;
        public double executedWorldTime = -1d;
        public string message;
        public long revision = 1L;

        public OrganizationDecisionExecutionRecordData Clone() => new OrganizationDecisionExecutionRecordData
        {
            executionId = executionId ?? string.Empty,
            resolutionId = resolutionId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            operations = (operations ?? Array.Empty<OrganizationDecisionExecutionOperationData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
            lifecycleState = lifecycleState,
            preparedWorldTime = preparedWorldTime,
            executedWorldTime = executedWorldTime,
            message = message ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationDecisionTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public string organizationId;
        public double worldTime;
        public long revision;

        public OrganizationDecisionTransactionRecordData Clone() => new OrganizationDecisionTransactionRecordData
        {
            transactionId = transactionId ?? string.Empty,
            operation = operation ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            worldTime = worldTime,
            revision = revision
        };
    }

    [Serializable]
    public sealed class OrganizationDecisionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<OrganizationGoalRecordData> goals = new List<OrganizationGoalRecordData>();
        public List<OrganizationPolicyRecordData> policies = new List<OrganizationPolicyRecordData>();
        public List<OrganizationProposalRecordData> proposals = new List<OrganizationProposalRecordData>();
        public List<OrganizationAmendmentRecordData> amendments = new List<OrganizationAmendmentRecordData>();
        public List<OrganizationVoterRollRecordData> voterRolls = new List<OrganizationVoterRollRecordData>();
        public List<OrganizationVoteRecordData> votes = new List<OrganizationVoteRecordData>();
        public List<OrganizationResolutionRecordData> resolutions = new List<OrganizationResolutionRecordData>();
        public List<OrganizationDecisionExecutionRecordData> executions = new List<OrganizationDecisionExecutionRecordData>();
        public List<OrganizationDecisionTransactionRecordData> transactions = new List<OrganizationDecisionTransactionRecordData>();

        public OrganizationDecisionRuntimeSaveData Clone() => new OrganizationDecisionRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            worldId = worldId ?? string.Empty,
            revision = Math.Max(0L, revision),
            goals = goals == null ? new List<OrganizationGoalRecordData>() : goals.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            policies = policies == null ? new List<OrganizationPolicyRecordData>() : policies.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            proposals = proposals == null ? new List<OrganizationProposalRecordData>() : proposals.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            amendments = amendments == null ? new List<OrganizationAmendmentRecordData>() : amendments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            voterRolls = voterRolls == null ? new List<OrganizationVoterRollRecordData>() : voterRolls.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            votes = votes == null ? new List<OrganizationVoteRecordData>() : votes.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            resolutions = resolutions == null ? new List<OrganizationResolutionRecordData>() : resolutions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            executions = executions == null ? new List<OrganizationDecisionExecutionRecordData>() : executions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            transactions = transactions == null ? new List<OrganizationDecisionTransactionRecordData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
        };
    }

    public sealed class OrganizationGoalRequest
    {
        public string transactionId;
        public string goalId;
        public string organizationId;
        public string goalDefinitionId;
        public string displayName;
        public OrganizationDecisionSubjectReferenceData targetSubject;
        public long targetValue;
        public int priority;
        public string actorPersonId;
        public string actionDefinitionId;
        public double worldTime;
        public double deadlineWorldTime = -1d;
        public string sourceProposalId;
        public string sourceResolutionId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public bool preview;
    }

    public sealed class OrganizationGoalProgressRequest
    {
        public string transactionId;
        public string goalId;
        public string contributionId;
        public long units;
        public string sourceRecordId;
        public string actorPersonId;
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationPolicyRequest
    {
        public string transactionId;
        public string policyId;
        public string organizationId;
        public string policyDefinitionId;
        public string displayName;
        public OrganizationPolicyScopeData scope;
        public OrganizationPolicyParameterValueData[] parameters = Array.Empty<OrganizationPolicyParameterValueData>();
        public int priority;
        public string actorPersonId;
        public string actionDefinitionId;
        public double adoptedWorldTime;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string sourceProposalId;
        public string sourceResolutionId;
        public string supersedesPolicyId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public bool preview;
    }

    public sealed class OrganizationPolicyQuery
    {
        public string organizationId;
        public string policyDefinitionId;
        public OrganizationPolicyScopeData scope;
        public double worldTime;
    }

    public sealed class OrganizationProposalRequest
    {
        public string transactionId;
        public string proposalId;
        public string organizationId;
        public string proposalDefinitionId;
        public string title;
        public string proposerPersonId;
        public OrganizationDecisionExecutionOperationData[] requestedExecutionOperations = Array.Empty<OrganizationDecisionExecutionOperationData>();
        public double submittedWorldTime;
        public double votingStartWorldTime;
        public double votingEndWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string[] tags = Array.Empty<string>();
        public bool preview;
    }

    public sealed class OrganizationAmendmentRequest
    {
        public string transactionId;
        public string amendmentId;
        public string proposalId;
        public string proposerPersonId;
        public string summary;
        public OrganizationDecisionExecutionOperationData[] replacementExecutionOperations = Array.Empty<OrganizationDecisionExecutionOperationData>();
        public double worldTime;
        public bool acceptImmediately = true;
        public bool preview;
    }

    public sealed class OrganizationVoteRequest
    {
        public string transactionId;
        public string voteId;
        public string proposalId;
        public string voterPersonId;
        public OrganizationVoteChoice choice = OrganizationVoteChoice.Unknown;
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationCloseVoteRequest
    {
        public string transactionId;
        public string proposalId;
        public string resolutionId;
        public string actorPersonId;
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationResolutionActionRequest
    {
        public string transactionId;
        public string resolutionId;
        public string actorPersonId;
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationDecisionExecutionRequest
    {
        public string transactionId;
        public string executionId;
        public string resolutionId;
        public string actorPersonId;
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationPolicyResolutionResult
    {
        public OrganizationPolicyResolutionResult(IEnumerable<OrganizationPolicyRecordData> activePolicies, OrganizationPolicyRecordData effectivePolicy, IEnumerable<OrganizationPolicyRecordData> suppressedPolicies)
        {
            ActivePolicies = (activePolicies ?? Array.Empty<OrganizationPolicyRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            EffectivePolicy = effectivePolicy?.Clone();
            SuppressedPolicies = (suppressedPolicies ?? Array.Empty<OrganizationPolicyRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
        }

        public IReadOnlyList<OrganizationPolicyRecordData> ActivePolicies { get; }
        public OrganizationPolicyRecordData EffectivePolicy { get; }
        public IReadOnlyList<OrganizationPolicyRecordData> SuppressedPolicies { get; }
    }

    public sealed class OrganizationDecisionTallySnapshot
    {
        public OrganizationDecisionTallySnapshot(OrganizationVoterRollRecordData roll, IEnumerable<OrganizationVoteRecordData> votes)
        {
            VoterRoll = roll?.Clone();
            Votes = (votes ?? Array.Empty<OrganizationVoteRecordData>()).Where(item => item != null && item.lifecycleState == OrganizationVoteLifecycleState.Active).Select(item => item.Clone()).OrderBy(item => item.voteId, StringComparer.Ordinal).ToArray();
            EligibleCount = VoterRoll?.eligiblePersonIds?.Length ?? 0;
            ParticipatingCount = Votes.Select(item => item.voterPersonId).Distinct(StringComparer.Ordinal).Count();
            ApproveWeight = Votes.Where(item => item.choice == OrganizationVoteChoice.Approve).Sum(item => item.weight);
            RejectWeight = Votes.Where(item => item.choice == OrganizationVoteChoice.Reject).Sum(item => item.weight);
            AbstainWeight = Votes.Where(item => item.choice == OrganizationVoteChoice.Abstain).Sum(item => item.weight);
        }

        public OrganizationVoterRollRecordData VoterRoll { get; }
        public IReadOnlyList<OrganizationVoteRecordData> Votes { get; }
        public int EligibleCount { get; }
        public int ParticipatingCount { get; }
        public long ApproveWeight { get; }
        public long RejectWeight { get; }
        public long AbstainWeight { get; }
        public long CastWeight => ApproveWeight + RejectWeight + AbstainWeight;
    }

    public sealed class OrganizationDecisionOperationResult
    {
        private OrganizationDecisionOperationResult(bool succeeded, OrganizationDecisionOperationCode code, string message, long before, long after, bool preview, bool duplicate, string subjectId, OrganizationAuthorizationResult authorization, OrganizationGoalRecordData goal, OrganizationPolicyRecordData policy, OrganizationProposalRecordData proposal, OrganizationVoteRecordData vote, OrganizationResolutionRecordData resolution, OrganizationDecisionExecutionRecordData execution)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
            Preview = preview;
            Duplicate = duplicate;
            SubjectId = subjectId ?? string.Empty;
            Authorization = authorization;
            Goal = goal?.Clone();
            Policy = policy?.Clone();
            Proposal = proposal?.Clone();
            Vote = vote?.Clone();
            Resolution = resolution?.Clone();
            Execution = execution?.Clone();
        }

        public bool Succeeded { get; }
        public OrganizationDecisionOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string SubjectId { get; }
        public OrganizationAuthorizationResult Authorization { get; }
        public OrganizationGoalRecordData Goal { get; }
        public OrganizationPolicyRecordData Policy { get; }
        public OrganizationProposalRecordData Proposal { get; }
        public OrganizationVoteRecordData Vote { get; }
        public OrganizationResolutionRecordData Resolution { get; }
        public OrganizationDecisionExecutionRecordData Execution { get; }

        public static OrganizationDecisionOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, string subjectId = "", OrganizationAuthorizationResult authorization = null, OrganizationGoalRecordData goal = null, OrganizationPolicyRecordData policy = null, OrganizationProposalRecordData proposal = null, OrganizationVoteRecordData vote = null, OrganizationResolutionRecordData resolution = null, OrganizationDecisionExecutionRecordData execution = null) =>
            new OrganizationDecisionOperationResult(true, preview ? OrganizationDecisionOperationCode.Preview : duplicate ? OrganizationDecisionOperationCode.Duplicate : OrganizationDecisionOperationCode.Success, message, before, after, preview, duplicate, subjectId, authorization, goal, policy, proposal, vote, resolution, execution);

        public static OrganizationDecisionOperationResult Failure(OrganizationDecisionOperationCode code, string message, long revision, bool preview = false, OrganizationAuthorizationResult authorization = null) =>
            new OrganizationDecisionOperationResult(false, code, message, revision, revision, preview, false, string.Empty, authorization, null, null, null, null, null, null);
    }

    public sealed class OrganizationDecisionProjection
    {
        public OrganizationDecisionProjection(OrganizationDecisionProjectionAccess access, string subjectId, bool redacted, OrganizationGoalRecordData goal, OrganizationPolicyRecordData policy, OrganizationProposalRecordData proposal, OrganizationResolutionRecordData resolution)
        {
            Access = access;
            SubjectId = subjectId ?? string.Empty;
            Redacted = redacted;
            Goal = goal?.Clone();
            Policy = policy?.Clone();
            Proposal = proposal?.Clone();
            Resolution = resolution?.Clone();
        }

        public OrganizationDecisionProjectionAccess Access { get; }
        public string SubjectId { get; }
        public bool Redacted { get; }
        public OrganizationGoalRecordData Goal { get; }
        public OrganizationPolicyRecordData Policy { get; }
        public OrganizationProposalRecordData Proposal { get; }
        public OrganizationResolutionRecordData Resolution { get; }
        public bool Succeeded => Access == OrganizationDecisionProjectionAccess.Full || Access == OrganizationDecisionProjectionAccess.Redacted;
    }

    public sealed class OrganizationDecisionCommittedEvent
    {
        public OrganizationDecisionCommittedEvent(OrganizationDecisionTransactionRecordData transaction, long revision)
        {
            Transaction = transaction?.Clone() ?? new OrganizationDecisionTransactionRecordData();
            Revision = revision;
        }

        public OrganizationDecisionTransactionRecordData Transaction { get; }
        public long Revision { get; }
    }
}
