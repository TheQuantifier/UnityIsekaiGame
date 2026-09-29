using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public enum Step15NarrativeReadinessState
    {
        Uninitialized,
        Restoring,
        Reconciling,
        Ready,
        Degraded,
        Failed,
        Resetting,
        Disposed
    }

    public enum Step15NarrativeRestorePhase
    {
        ReadEnvelope,
        ValidateSchema,
        DeserializeCandidate,
        ResolveDefinitions,
        ResolveDependencies,
        PrepareIndexes,
        CrossValidate,
        CommitAuthoritativeState,
        RebuildDerivedState,
        RestoreScheduler,
        RestoreSubscriptions,
        Reconcile,
        ValidateFinalState,
        PublishReady,
        SceneRebind
    }

    public enum NarrativeHistoricalAccessMode
    {
        Development,
        PersonSafe,
        Institutional
    }

    public enum NarrativeTimelineCategory
    {
        QuestInstantiated,
        QuestLifecycleChanged,
        QuestOffered,
        QuestOfferChanged,
        QuestAccepted,
        QuestAssignmentChanged,
        ObjectiveActivated,
        ObjectiveProgressed,
        ObjectiveSatisfied,
        QuestCompleted,
        QuestFailed,
        RewardEntitled,
        RewardClaimed,
        QuestListed,
        QuestListingChanged,
        ConversationStarted,
        ConversationChanged,
        DialogueNodeEntered,
        DialogueChoiceSelected,
        NarrativeEventTriggered,
        NarrativeActionCommitted,
        NarrativeSignalEmitted,
        NarrativeStateTransitioned,
        ArcStarted,
        ArcStageActivated,
        ArcStageCompleted,
        ArcCompleted
    }

    public enum NarrativeHistoricalGapKind
    {
        None,
        MissingRecord,
        BeforeCreation,
        AfterRetirement,
        HiddenByAccess,
        ConflictingHistory
    }

    public enum NarrativeRecoveryIssueKind
    {
        MissingSchedulerTask,
        DuplicateSchedulerTask,
        StaleSchedulerTask,
        DuplicateSubscription,
        StaleDerivedIndex,
        MissingRequiredBinding,
        AuthoritativeCorruption
    }

    public sealed class Step15NarrativePersistenceSnapshot
    {
        public string WorldId { get; set; } = string.Empty;
        public string SaveSlotId { get; set; } = string.Empty;
        public double SaveWorldTime { get; set; }
        public QuestRuntimeSaveData Quests { get; set; }
        public QuestParticipationRuntimeSaveData Participation { get; set; }
        public QuestObjectiveProgressRuntimeSaveData Objectives { get; set; }
        public QuestOutcomeRuntimeSaveData Outcomes { get; set; }
        public QuestSourceRuntimeSaveData Sources { get; set; }
        public ConversationRuntimeSaveData Conversations { get; set; }
        public DialogueFlowRuntimeSaveData DialogueFlows { get; set; }
        public NarrativeEventRuntimeSaveData NarrativeEvents { get; set; }
        public NarrativeStateRuntimeSaveData NarrativeStates { get; set; }
        public NarrativeArcRuntimeSaveData NarrativeArcs { get; set; }

        public Step15NarrativePersistenceSnapshot Clone()
        {
            return new Step15NarrativePersistenceSnapshot
            {
                WorldId = N(WorldId),
                SaveSlotId = N(SaveSlotId),
                SaveWorldTime = SaveWorldTime,
                Quests = Quests?.Clone(),
                Participation = Participation?.Clone(),
                Objectives = Objectives?.Clone(),
                Outcomes = Outcomes?.Clone(),
                Sources = Sources?.Clone(),
                Conversations = Conversations?.Clone(),
                DialogueFlows = DialogueFlows?.Clone(),
                NarrativeEvents = NarrativeEvents?.Clone(),
                NarrativeStates = NarrativeStates?.Clone(),
                NarrativeArcs = NarrativeArcs?.Clone()
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    public sealed class Step15NarrativeOwnershipEntry
    {
        public Step15NarrativeOwnershipEntry(string category, string authoritativeOwner, string participantKey, bool derived, string notes)
        {
            Category = category ?? string.Empty;
            AuthoritativeOwner = authoritativeOwner ?? string.Empty;
            ParticipantKey = participantKey ?? string.Empty;
            Derived = derived;
            Notes = notes ?? string.Empty;
        }

        public string Category { get; }
        public string AuthoritativeOwner { get; }
        public string ParticipantKey { get; }
        public bool Derived { get; }
        public string Notes { get; }
    }

    public sealed class Step15NarrativePersistenceManifest
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string WorldId { get; set; } = string.Empty;
        public string SaveSlotId { get; set; } = string.Empty;
        public double SaveWorldTime { get; set; }
        public Step15NarrativeReadinessState Readiness { get; set; } = Step15NarrativeReadinessState.Uninitialized;
        public IReadOnlyList<Step15NarrativeRestorePhase> RestorePhases { get; set; } = Array.Empty<Step15NarrativeRestorePhase>();
        public IReadOnlyList<Step15NarrativeOwnershipEntry> Ownership { get; set; } = Array.Empty<Step15NarrativeOwnershipEntry>();
        public IReadOnlyDictionary<string, int> ParticipantSchemaVersions { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, int> RecordCounts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public string DeterministicFingerprint { get; set; } = string.Empty;
    }

    public sealed class Step15NarrativeValidationReport
    {
        public Step15NarrativeValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings, IEnumerable<NarrativeRecoveryIssue> recoveryIssues)
        {
            Errors = Clean(errors);
            Warnings = Clean(warnings);
            RecoveryIssues = (recoveryIssues ?? Array.Empty<NarrativeRecoveryIssue>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyList<NarrativeRecoveryIssue> RecoveryIssues { get; }
        public bool Succeeded => Errors.Count == 0;
        public bool HasRecoverableIssues => RecoveryIssues.Any(value => value.Recoverable);
        public string Summary => $"Step 15 narrative validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s), {RecoveryIssues.Count} recovery issue(s).";

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        }
    }

    public sealed class NarrativeRecoveryIssue
    {
        public NarrativeRecoveryIssueKind Kind { get; set; }
        public string SourceRuntime { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool Recoverable { get; set; }

        public NarrativeRecoveryIssue Clone()
        {
            return new NarrativeRecoveryIssue
            {
                Kind = Kind,
                SourceRuntime = N(SourceRuntime),
                SourceId = N(SourceId),
                Message = Message ?? string.Empty,
                Recoverable = Recoverable
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    public sealed class NarrativeTimelineEntry
    {
        public double WorldTime { get; set; }
        public long Sequence { get; set; }
        public NarrativeTimelineCategory Category { get; set; }
        public string SourceRuntime { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string StableSourceReference { get; set; } = string.Empty;
        public string QuestId { get; set; } = string.Empty;
        public string AssignmentId { get; set; } = string.Empty;
        public string OfferId { get; set; } = string.Empty;
        public string ObjectiveId { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string NarrativeEventId { get; set; } = string.Empty;
        public string NarrativeStateId { get; set; } = string.Empty;
        public string NarrativeArcId { get; set; } = string.Empty;
        public string PersonId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Cursor => $"{WorldTime:0000000000.000000}:{Sequence:000000000000}:{(int)Category:000}:{StableSourceReference}";

        public NarrativeTimelineEntry Clone()
        {
            return (NarrativeTimelineEntry)MemberwiseClone();
        }
    }

    public sealed class NarrativeTimelineQuery
    {
        public NarrativeHistoricalAccessMode AccessMode { get; set; } = NarrativeHistoricalAccessMode.PersonSafe;
        public string RequesterPersonId { get; set; } = string.Empty;
        public string PersonId { get; set; } = string.Empty;
        public string QuestId { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string NarrativeEventId { get; set; } = string.Empty;
        public string NarrativeStateId { get; set; } = string.Empty;
        public string NarrativeArcId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public NarrativeTimelineCategory? Category { get; set; }
        public double StartWorldTime { get; set; } = double.MinValue;
        public double EndWorldTime { get; set; } = double.MaxValue;
        public string AfterCursor { get; set; } = string.Empty;
        public int Limit { get; set; } = 100;
    }

    public sealed class NarrativeTimelinePage
    {
        public NarrativeTimelinePage(IEnumerable<NarrativeTimelineEntry> entries, string nextCursor, bool hasMore)
        {
            Entries = (entries ?? Array.Empty<NarrativeTimelineEntry>()).Select(value => value.Clone()).ToArray();
            NextCursor = nextCursor ?? string.Empty;
            HasMore = hasMore;
        }

        public IReadOnlyList<NarrativeTimelineEntry> Entries { get; }
        public string NextCursor { get; }
        public bool HasMore { get; }
    }

    public sealed class HistoricalQuestSnapshot
    {
        public string QuestId { get; set; } = string.Empty;
        public bool Existed { get; set; }
        public QuestRuntimeLifecycleState Lifecycle { get; set; } = QuestRuntimeLifecycleState.Unknown;
        public IReadOnlyList<QuestOfferLifecycleAtTime> Offers { get; set; } = Array.Empty<QuestOfferLifecycleAtTime>();
        public IReadOnlyList<QuestAssignmentLifecycleAtTime> Assignments { get; set; } = Array.Empty<QuestAssignmentLifecycleAtTime>();
        public IReadOnlyList<QuestObjectiveProgressAtTime> Objectives { get; set; } = Array.Empty<QuestObjectiveProgressAtTime>();
        public QuestTerminalOutcomeKind Outcome { get; set; } = QuestTerminalOutcomeKind.Unknown;
        public IReadOnlyList<QuestRewardStateAtTime> Rewards { get; set; } = Array.Empty<QuestRewardStateAtTime>();
        public IReadOnlyList<string> ActiveListingIds { get; set; } = Array.Empty<string>();
        public NarrativeHistoricalGapKind Gap { get; set; } = NarrativeHistoricalGapKind.None;
    }

    public sealed class HistoricalPersonQuestSnapshot
    {
        public string PersonId { get; set; } = string.Empty;
        public IReadOnlyList<string> PendingOfferIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ActiveAssignmentIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> CompletedQuestIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FailedQuestIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ClaimableRewardIds { get; set; } = Array.Empty<string>();
    }

    public sealed class QuestOfferLifecycleAtTime
    {
        public string OfferId { get; set; } = string.Empty;
        public QuestOfferLifecycleState State { get; set; } = QuestOfferLifecycleState.Unknown;
    }

    public sealed class QuestAssignmentLifecycleAtTime
    {
        public string AssignmentId { get; set; } = string.Empty;
        public QuestAssignmentLifecycleState State { get; set; } = QuestAssignmentLifecycleState.Unknown;
    }

    public sealed class QuestObjectiveProgressAtTime
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public QuestObjectiveLifecycleState State { get; set; } = QuestObjectiveLifecycleState.Locked;
        public int CurrentValue { get; set; }
        public int TargetValue { get; set; }
        public bool Satisfied { get; set; }
    }

    public sealed class QuestRewardStateAtTime
    {
        public string EntitlementId { get; set; } = string.Empty;
        public QuestRewardEntitlementState State { get; set; } = QuestRewardEntitlementState.Pending;
    }

    public sealed class HistoricalConversationSnapshot
    {
        public string ConversationId { get; set; } = string.Empty;
        public bool Existed { get; set; }
        public ConversationLifecycleState Lifecycle { get; set; } = ConversationLifecycleState.Unknown;
        public string ActiveDialogueNodeId { get; set; } = string.Empty;
        public string LatestChoiceId { get; set; } = string.Empty;
        public IReadOnlyList<string> ParticipantPersonIds { get; set; } = Array.Empty<string>();
        public NarrativeHistoricalGapKind Gap { get; set; } = NarrativeHistoricalGapKind.None;
    }

    public sealed class HistoricalNarrativeStateSnapshot
    {
        public string NarrativeStateId { get; set; } = string.Empty;
        public string StateDefinitionId { get; set; } = string.Empty;
        public IReadOnlyDictionary<string, string> VariableValues { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public NarrativeHistoricalGapKind Gap { get; set; } = NarrativeHistoricalGapKind.None;
    }

    public sealed class HistoricalNarrativeArcSnapshot
    {
        public string NarrativeArcId { get; set; } = string.Empty;
        public bool Existed { get; set; }
        public NarrativeArcLifecycle Lifecycle { get; set; } = NarrativeArcLifecycle.Unknown;
        public IReadOnlyList<string> ActiveStageDefinitionIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> CompletedStageDefinitionIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> BoundQuestIds { get; set; } = Array.Empty<string>();
        public NarrativeHistoricalGapKind Gap { get; set; } = NarrativeHistoricalGapKind.None;
    }
}
