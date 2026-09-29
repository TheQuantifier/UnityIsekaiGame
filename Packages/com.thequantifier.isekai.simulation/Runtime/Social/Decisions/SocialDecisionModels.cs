using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Social.Decisions
{
    public interface ISocialDecisionModifierSource
    {
        int ResolveSocialDecisionScoreModifier(string actorPersonId, string targetPersonId, string intentionDefinitionId, string interactionDefinitionId, double worldTime, out string sourceModifierId);
    }

    [Serializable]
    public sealed class SocialDecisionCooldownData
    {
        public string cooldownKey;
        public double lastWorldTime;
        public string sourceDecisionId;

        public SocialDecisionCooldownData Clone() => new SocialDecisionCooldownData { cooldownKey = cooldownKey ?? string.Empty, lastWorldTime = lastWorldTime, sourceDecisionId = sourceDecisionId ?? string.Empty };
    }

    [Serializable]
    public sealed class SocialDecisionHistoryEntryData
    {
        public string decisionId;
        public string actorPersonId;
        public string targetPersonId;
        public string intentionDefinitionId;
        public string interactionDefinitionId;
        public SocialDecisionStatus status;
        public SocialDecisionLifecycleState lifecycleState;
        public int score;
        public double evaluationWorldTime;
        public string executionInteractionRecordId;
        public string failureReason;
        public long revision;

        public SocialDecisionHistoryEntryData Clone() => new SocialDecisionHistoryEntryData
        {
            decisionId = decisionId ?? string.Empty,
            actorPersonId = actorPersonId ?? string.Empty,
            targetPersonId = targetPersonId ?? string.Empty,
            intentionDefinitionId = intentionDefinitionId ?? string.Empty,
            interactionDefinitionId = interactionDefinitionId ?? string.Empty,
            status = status,
            lifecycleState = lifecycleState,
            score = score,
            evaluationWorldTime = evaluationWorldTime,
            executionInteractionRecordId = executionInteractionRecordId ?? string.Empty,
            failureReason = failureReason ?? string.Empty,
            revision = revision
        };
    }

    [Serializable]
    public sealed class SocialDecisionPersonStateData
    {
        public string personId;
        public string decisionProfileId;
        public string activeDecisionId;
        public string activeIntentionDefinitionId;
        public string activeTargetPersonId;
        public string selectedInteractionDefinitionId;
        public string pendingInteractionReferenceId;
        public SocialDecisionLifecycleState lifecycleState = SocialDecisionLifecycleState.Idle;
        public double intentionStartWorldTime = -1d;
        public double lastEvaluationWorldTime = -1d;
        public double nextEligibleEvaluationWorldTime = -1d;
        public int repetitionCount;
        public long revision = 1L;
        public List<SocialDecisionCooldownData> cooldowns = new List<SocialDecisionCooldownData>();
        public List<SocialDecisionHistoryEntryData> recentHistory = new List<SocialDecisionHistoryEntryData>();

        public SocialDecisionPersonStateData Clone() => new SocialDecisionPersonStateData
        {
            personId = personId ?? string.Empty,
            decisionProfileId = decisionProfileId ?? string.Empty,
            activeDecisionId = activeDecisionId ?? string.Empty,
            activeIntentionDefinitionId = activeIntentionDefinitionId ?? string.Empty,
            activeTargetPersonId = activeTargetPersonId ?? string.Empty,
            selectedInteractionDefinitionId = selectedInteractionDefinitionId ?? string.Empty,
            pendingInteractionReferenceId = pendingInteractionReferenceId ?? string.Empty,
            lifecycleState = lifecycleState,
            intentionStartWorldTime = intentionStartWorldTime,
            lastEvaluationWorldTime = lastEvaluationWorldTime,
            nextEligibleEvaluationWorldTime = nextEligibleEvaluationWorldTime,
            repetitionCount = repetitionCount,
            revision = revision,
            cooldowns = cooldowns == null ? new List<SocialDecisionCooldownData>() : cooldowns.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            recentHistory = recentHistory == null ? new List<SocialDecisionHistoryEntryData>() : recentHistory.Select(item => item?.Clone()).Where(item => item != null).ToList()
        };
    }

    [Serializable]
    public sealed class SocialDecisionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long decisionSequence;
        public List<SocialDecisionPersonStateData> personStates = new List<SocialDecisionPersonStateData>();

        public SocialDecisionRuntimeSaveData Clone() => new SocialDecisionRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            revision = revision,
            decisionSequence = decisionSequence,
            personStates = personStates == null ? new List<SocialDecisionPersonStateData>() : personStates.Select(item => item?.Clone()).Where(item => item != null).ToList()
        };
    }

    public sealed class SocialDecisionRequest
    {
        public string ActorPersonId { get; set; }
        public string DecisionProfileId { get; set; }
        public IReadOnlyList<string> AvailableTargetPersonIds { get; set; } = Array.Empty<string>();
        public string ExplicitTargetPersonId { get; set; }
        public string ExplicitIntentionDefinitionId { get; set; }
        public string ExplicitInteractionDefinitionId { get; set; }
        public string PendingInteractionId { get; set; }
        public string PlaceId { get; set; }
        public string AudienceId { get; set; }
        public IReadOnlyList<string> WitnessPersonIds { get; set; } = Array.Empty<string>();
        public double WorldTime { get; set; }
        public string DeterministicSeed { get; set; }
        public SocialDecisionExecutionMode? ExecutionMode { get; set; }
        public SocialDecisionActorControlPolicy ActorControlPolicy { get; set; } = SocialDecisionActorControlPolicy.AutonomousNpc;
        public bool ForceEvaluate { get; set; }
        public bool CommitDecisionState { get; set; } = true;
        public int? MaximumTargetsOverride { get; set; }
        public int? MaximumCandidatesOverride { get; set; }
    }

    public sealed class SocialDecisionTargetCandidateData
    {
        public string personId;
        public SocialDecisionTargetSource source;
        public int priority;
        public bool accepted = true;
        public string reason;

        public SocialDecisionTargetCandidateData Clone() => new SocialDecisionTargetCandidateData { personId = personId ?? string.Empty, source = source, priority = priority, accepted = accepted, reason = reason ?? string.Empty };
    }

    public sealed class SocialDecisionConsiderationResultData
    {
        public string considerationId;
        public SocialDecisionConsiderationInput input;
        public int rawValue;
        public int normalizedValue;
        public int weightedScore;
        public bool missingData;
        public bool rejected;
        public string diagnostics;

        public SocialDecisionConsiderationResultData Clone() => new SocialDecisionConsiderationResultData
        {
            considerationId = considerationId ?? string.Empty,
            input = input,
            rawValue = rawValue,
            normalizedValue = normalizedValue,
            weightedScore = weightedScore,
            missingData = missingData,
            rejected = rejected,
            diagnostics = diagnostics ?? string.Empty
        };
    }

    public sealed class SocialDecisionActionCandidateData
    {
        public string candidateKey;
        public string intentionDefinitionId;
        public SocialIntentionCategory intentionCategory;
        public string targetPersonId;
        public string interactionDefinitionId;
        public int basePriority;
        public int urgency;
        public int considerationScore;
        public int cooldownPenalty;
        public int repetitionPenalty;
        public int externalModifier;
        public int finalScore;
        public bool hardRequirementsPassed;
        public bool selected;
        public bool noInteraction;
        public string rejectionReason;
        public string previewStatus;
        public string previewMessage;
        public SocialDecisionConsiderationResultData[] considerations = Array.Empty<SocialDecisionConsiderationResultData>();

        public SocialDecisionActionCandidateData Clone() => new SocialDecisionActionCandidateData
        {
            candidateKey = candidateKey ?? string.Empty,
            intentionDefinitionId = intentionDefinitionId ?? string.Empty,
            intentionCategory = intentionCategory,
            targetPersonId = targetPersonId ?? string.Empty,
            interactionDefinitionId = interactionDefinitionId ?? string.Empty,
            basePriority = basePriority,
            urgency = urgency,
            considerationScore = considerationScore,
            cooldownPenalty = cooldownPenalty,
            repetitionPenalty = repetitionPenalty,
            externalModifier = externalModifier,
            finalScore = finalScore,
            hardRequirementsPassed = hardRequirementsPassed,
            selected = selected,
            noInteraction = noInteraction,
            rejectionReason = rejectionReason ?? string.Empty,
            previewStatus = previewStatus ?? string.Empty,
            previewMessage = previewMessage ?? string.Empty,
            considerations = considerations == null ? Array.Empty<SocialDecisionConsiderationResultData>() : considerations.Select(item => item?.Clone()).Where(item => item != null).ToArray()
        };
    }

    public sealed class SocialDecisionResult
    {
        private readonly SocialDecisionActionCandidateData[] candidates;
        private readonly SocialDecisionTargetCandidateData[] targets;
        private readonly string[] diagnostics;

        public SocialDecisionResult(bool succeeded, SocialDecisionStatus status, string message, string decisionId, string actorPersonId, SocialDecisionActionCandidateData selectedCandidate, IEnumerable<SocialDecisionActionCandidateData> candidateDiagnostics, IEnumerable<SocialDecisionTargetCandidateData> targetDiagnostics, SocialInteractionResult executionResult, bool truncated, long beforeRevision, long afterRevision, IEnumerable<string> diagnosticMessages)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            DecisionId = decisionId ?? string.Empty;
            ActorPersonId = actorPersonId ?? string.Empty;
            SelectedCandidate = selectedCandidate?.Clone();
            candidates = (candidateDiagnostics ?? Array.Empty<SocialDecisionActionCandidateData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            targets = (targetDiagnostics ?? Array.Empty<SocialDecisionTargetCandidateData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            ExecutionResult = executionResult;
            Truncated = truncated;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            diagnostics = Clean(diagnosticMessages);
        }

        public bool Succeeded { get; }
        public SocialDecisionStatus Status { get; }
        public string Message { get; }
        public string DecisionId { get; }
        public string ActorPersonId { get; }
        public SocialDecisionActionCandidateData SelectedCandidate { get; }
        public IReadOnlyList<SocialDecisionActionCandidateData> Candidates => candidates.Select(item => item.Clone()).ToArray();
        public IReadOnlyList<SocialDecisionTargetCandidateData> Targets => targets.Select(item => item.Clone()).ToArray();
        public SocialInteractionResult ExecutionResult { get; }
        public bool Truncated { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class SocialDecisionPersonStateSnapshot
    {
        private readonly SocialDecisionPersonStateData data;

        public SocialDecisionPersonStateSnapshot(SocialDecisionPersonStateData data)
        {
            this.data = data?.Clone() ?? new SocialDecisionPersonStateData();
        }

        public SocialDecisionPersonStateData Data => data.Clone();
        public string PersonId => data.personId ?? string.Empty;
        public string DecisionProfileId => data.decisionProfileId ?? string.Empty;
        public string ActiveDecisionId => data.activeDecisionId ?? string.Empty;
        public string ActiveIntentionDefinitionId => data.activeIntentionDefinitionId ?? string.Empty;
        public string ActiveTargetPersonId => data.activeTargetPersonId ?? string.Empty;
        public SocialDecisionLifecycleState LifecycleState => data.lifecycleState;
        public double NextEligibleEvaluationWorldTime => data.nextEligibleEvaluationWorldTime;
        public IReadOnlyList<SocialDecisionHistoryEntryData> RecentHistory => data.recentHistory == null ? Array.Empty<SocialDecisionHistoryEntryData>() : data.recentHistory.Select(item => item.Clone()).ToArray();
    }
}
