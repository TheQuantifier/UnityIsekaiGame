using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Combat.CombatState
{
    public sealed class ActorCombatStateSnapshot
    {
        public ActorCombatStateSnapshot(string actorId, CombatStateValue state, string encounterId, float enteredAt, float lastActivityAt, float disengageAt, int participantCount, int activeEngagementCount, long revision, string transitionReason)
        {
            ActorId = actorId ?? string.Empty;
            State = state;
            EncounterId = encounterId ?? string.Empty;
            EnteredAt = enteredAt;
            LastActivityAt = lastActivityAt;
            DisengageEligibleAt = disengageAt;
            ParticipantCount = participantCount;
            ActiveEngagementCount = activeEngagementCount;
            Revision = revision;
            TransitionReason = transitionReason ?? string.Empty;
        }

        public string ActorId { get; }
        public CombatStateValue State { get; }
        public string EncounterId { get; }
        public float EnteredAt { get; }
        public float LastActivityAt { get; }
        public float DisengageEligibleAt { get; }
        public int ParticipantCount { get; }
        public int ActiveEngagementCount { get; }
        public long Revision { get; }
        public string TransitionReason { get; }
        public bool IsInCombat => State == CombatStateValue.EnteringCombat || State == CombatStateValue.InCombat || State == CombatStateValue.Disengaging;
    }

    public sealed class CombatEngagementSnapshot
    {
        public CombatEngagementSnapshot(string engagementId, string sourceActorId, string targetActorId, string encounterId, float createdAt, float lastRefreshedAt, CombatActivityClassification classification, string originatingId, bool active, CombatExitReason endReason, long revision)
        {
            EngagementId = engagementId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            CreatedAt = createdAt;
            LastRefreshedAt = lastRefreshedAt;
            Classification = classification;
            OriginatingId = originatingId ?? string.Empty;
            Active = active;
            EndReason = endReason;
            Revision = revision;
        }

        public string EngagementId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public string EncounterId { get; }
        public float CreatedAt { get; }
        public float LastRefreshedAt { get; }
        public CombatActivityClassification Classification { get; }
        public string OriginatingId { get; }
        public bool Active { get; }
        public CombatExitReason EndReason { get; }
        public long Revision { get; }
    }

    public sealed class CombatEncounterSnapshot
    {
        public CombatEncounterSnapshot(string encounterId, bool active, float createdAt, float lastActivityAt, IReadOnlyList<string> participantIds, IReadOnlyList<CombatEngagementSnapshot> engagements, long revision, CombatEncounterCompletionReason completionReason)
        {
            EncounterId = encounterId ?? string.Empty;
            Active = active;
            CreatedAt = createdAt;
            LastActivityAt = lastActivityAt;
            ParticipantIds = participantIds == null ? Array.Empty<string>() : new List<string>(participantIds).AsReadOnly();
            Engagements = engagements == null ? Array.Empty<CombatEngagementSnapshot>() : new List<CombatEngagementSnapshot>(engagements).AsReadOnly();
            Revision = revision;
            CompletionReason = completionReason;
        }

        public string EncounterId { get; }
        public bool Active { get; }
        public float CreatedAt { get; }
        public float LastActivityAt { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public IReadOnlyList<CombatEngagementSnapshot> Engagements { get; }
        public long Revision { get; }
        public CombatEncounterCompletionReason CompletionReason { get; }
    }

    public sealed class CombatEntryResult
    {
        private CombatEntryResult(bool succeeded, bool preview, bool duplicate, string code, string message, string transactionId, string sourceActorId, string targetActorId, ActorCombatStateSnapshot sourcePrevious, ActorCombatStateSnapshot targetPrevious, ActorCombatStateSnapshot sourceResulting, ActorCombatStateSnapshot targetResulting, string engagementId, string encounterId, bool encounterCreated, bool sourceParticipantAdded, bool targetParticipantAdded, bool encounterMerged, float activityTimestamp)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatStateResultCode.Success : CombatStateResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            SourcePreviousState = sourcePrevious;
            TargetPreviousState = targetPrevious;
            SourceResultingState = sourceResulting;
            TargetResultingState = targetResulting;
            EngagementId = engagementId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            EncounterCreated = encounterCreated;
            SourceParticipantAdded = sourceParticipantAdded;
            TargetParticipantAdded = targetParticipantAdded;
            EncounterMerged = encounterMerged;
            ActivityTimestamp = activityTimestamp;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public ActorCombatStateSnapshot SourcePreviousState { get; }
        public ActorCombatStateSnapshot TargetPreviousState { get; }
        public ActorCombatStateSnapshot SourceResultingState { get; }
        public ActorCombatStateSnapshot TargetResultingState { get; }
        public string EngagementId { get; }
        public string EncounterId { get; }
        public bool EncounterCreated { get; }
        public bool SourceParticipantAdded { get; }
        public bool TargetParticipantAdded { get; }
        public bool EncounterMerged { get; }
        public float ActivityTimestamp { get; }

        public static CombatEntryResult Success(bool preview, bool duplicate, string code, string message, string transactionId, string sourceActorId, string targetActorId, ActorCombatStateSnapshot sourcePrevious, ActorCombatStateSnapshot targetPrevious, ActorCombatStateSnapshot sourceResulting, ActorCombatStateSnapshot targetResulting, string engagementId, string encounterId, bool encounterCreated, bool sourceAdded, bool targetAdded, bool merged, float timestamp)
        {
            return new CombatEntryResult(true, preview, duplicate, code, message, transactionId, sourceActorId, targetActorId, sourcePrevious, targetPrevious, sourceResulting, targetResulting, engagementId, encounterId, encounterCreated, sourceAdded, targetAdded, merged, timestamp);
        }

        public static CombatEntryResult Failure(bool preview, string code, string message, string transactionId, string sourceActorId, string targetActorId)
        {
            return new CombatEntryResult(false, preview, false, code, message, transactionId, sourceActorId, targetActorId, null, null, null, null, string.Empty, string.Empty, false, false, false, false, 0f);
        }
    }

    public sealed class CombatExitResult
    {
        private CombatExitResult(bool succeeded, bool preview, bool duplicate, string code, string message, string transactionId, string actorId, string encounterId, ActorCombatStateSnapshot previousState, ActorCombatStateSnapshot resultingState, IReadOnlyList<string> engagementsEnded, CombatExitReason reason, float timestamp)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatStateResultCode.Success : CombatStateResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            PreviousState = previousState;
            ResultingState = resultingState;
            EngagementsEnded = engagementsEnded == null ? Array.Empty<string>() : new List<string>(engagementsEnded).AsReadOnly();
            Reason = reason;
            Timestamp = timestamp;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string ActorId { get; }
        public string EncounterId { get; }
        public ActorCombatStateSnapshot PreviousState { get; }
        public ActorCombatStateSnapshot ResultingState { get; }
        public IReadOnlyList<string> EngagementsEnded { get; }
        public CombatExitReason Reason { get; }
        public float Timestamp { get; }

        public static CombatExitResult Success(bool preview, bool duplicate, string code, string message, string transactionId, string actorId, string encounterId, ActorCombatStateSnapshot previousState, ActorCombatStateSnapshot resultingState, IReadOnlyList<string> engagementsEnded, CombatExitReason reason, float timestamp)
        {
            return new CombatExitResult(true, preview, duplicate, code, message, transactionId, actorId, encounterId, previousState, resultingState, engagementsEnded, reason, timestamp);
        }

        public static CombatExitResult Failure(bool preview, string code, string message, string transactionId, string actorId, string encounterId)
        {
            return new CombatExitResult(false, preview, false, code, message, transactionId, actorId, encounterId, null, null, null, CombatExitReason.Explicit, 0f);
        }
    }

    public sealed class CombatEncounterSplitComponentSnapshot
    {
        public CombatEncounterSplitComponentSnapshot(string encounterId, IReadOnlyList<string> participantIds, IReadOnlyList<string> engagementIds, bool retainedOriginalEncounterId, float timestamp, bool active)
        {
            EncounterId = encounterId ?? string.Empty;
            ParticipantIds = participantIds == null ? Array.Empty<string>() : new List<string>(participantIds).AsReadOnly();
            EngagementIds = engagementIds == null ? Array.Empty<string>() : new List<string>(engagementIds).AsReadOnly();
            RetainedOriginalEncounterId = retainedOriginalEncounterId;
            Timestamp = timestamp;
            Active = active;
        }

        public string EncounterId { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public IReadOnlyList<string> EngagementIds { get; }
        public bool RetainedOriginalEncounterId { get; }
        public float Timestamp { get; }
        public bool Active { get; }
    }

    public sealed class CombatParticipantReassignmentResult
    {
        public CombatParticipantReassignmentResult(string actorId, string previousEncounterId, string resultingEncounterId, ActorCombatStateSnapshot previousState, ActorCombatStateSnapshot resultingState)
        {
            ActorId = actorId ?? string.Empty;
            PreviousEncounterId = previousEncounterId ?? string.Empty;
            ResultingEncounterId = resultingEncounterId ?? string.Empty;
            PreviousState = previousState;
            ResultingState = resultingState;
        }

        public string ActorId { get; }
        public string PreviousEncounterId { get; }
        public string ResultingEncounterId { get; }
        public ActorCombatStateSnapshot PreviousState { get; }
        public ActorCombatStateSnapshot ResultingState { get; }
    }

    public sealed class CombatEncounterSplitResult
    {
        public CombatEncounterSplitResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            string originalEncounterId,
            string survivingEncounterId,
            IReadOnlyList<string> createdEncounterIds,
            IReadOnlyList<string> previousParticipantIds,
            IReadOnlyList<CombatEncounterSplitComponentSnapshot> components,
            IReadOnlyList<string> endedEngagementIds,
            IReadOnlyList<string> participantsLeftCombat,
            IReadOnlyList<CombatExitResult> exitResults,
            IReadOnlyList<CombatEncounterSnapshot> endedEncounters,
            IReadOnlyList<CombatParticipantReassignmentResult> reassignments,
            CombatExitReason reason,
            long originalRevisionBefore,
            long originalRevisionAfter,
            float timestamp)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? CombatStateResultCode.Success : CombatStateResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            OriginalEncounterId = originalEncounterId ?? string.Empty;
            SurvivingEncounterId = survivingEncounterId ?? string.Empty;
            CreatedEncounterIds = createdEncounterIds == null ? Array.Empty<string>() : new List<string>(createdEncounterIds).AsReadOnly();
            PreviousParticipantIds = previousParticipantIds == null ? Array.Empty<string>() : new List<string>(previousParticipantIds).AsReadOnly();
            Components = components == null ? Array.Empty<CombatEncounterSplitComponentSnapshot>() : new List<CombatEncounterSplitComponentSnapshot>(components).AsReadOnly();
            EndedEngagementIds = endedEngagementIds == null ? Array.Empty<string>() : new List<string>(endedEngagementIds).AsReadOnly();
            ParticipantsLeftCombat = participantsLeftCombat == null ? Array.Empty<string>() : new List<string>(participantsLeftCombat).AsReadOnly();
            ExitResults = exitResults == null ? Array.Empty<CombatExitResult>() : new List<CombatExitResult>(exitResults).AsReadOnly();
            EndedEncounters = endedEncounters == null ? Array.Empty<CombatEncounterSnapshot>() : new List<CombatEncounterSnapshot>(endedEncounters).AsReadOnly();
            Reassignments = reassignments == null ? Array.Empty<CombatParticipantReassignmentResult>() : new List<CombatParticipantReassignmentResult>(reassignments).AsReadOnly();
            Reason = reason;
            OriginalRevisionBefore = originalRevisionBefore;
            OriginalRevisionAfter = originalRevisionAfter;
            Timestamp = timestamp;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string OriginalEncounterId { get; }
        public string SurvivingEncounterId { get; }
        public IReadOnlyList<string> CreatedEncounterIds { get; }
        public IReadOnlyList<string> PreviousParticipantIds { get; }
        public IReadOnlyList<CombatEncounterSplitComponentSnapshot> Components { get; }
        public IReadOnlyList<string> EndedEngagementIds { get; }
        public IReadOnlyList<string> ParticipantsLeftCombat { get; }
        public IReadOnlyList<CombatExitResult> ExitResults { get; }
        public IReadOnlyList<CombatEncounterSnapshot> EndedEncounters { get; }
        public IReadOnlyList<CombatParticipantReassignmentResult> Reassignments { get; }
        public CombatExitReason Reason { get; }
        public long OriginalRevisionBefore { get; }
        public long OriginalRevisionAfter { get; }
        public float Timestamp { get; }
        public bool SplitOccurred => Components.Count > 1 || CreatedEncounterIds.Count > 0 || ParticipantsLeftCombat.Count > 0 || EndedEncounters.Count > 0;
    }

    public sealed class CombatStateIntegrityResult
    {
        public CombatStateIntegrityResult(bool succeeded, IReadOnlyList<string> diagnostics)
        {
            Succeeded = succeeded;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : new List<string>(diagnostics).AsReadOnly();
        }

        public bool Succeeded { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    public sealed class CombatStateProcessResult
    {
        public CombatStateProcessResult(float deltaSeconds, int processedExits, bool capped, IReadOnlyList<CombatExitResult> exitResults, IReadOnlyList<CombatEncounterSnapshot> endedEncounters, IReadOnlyList<CombatEncounterSplitResult> splitResults = null)
        {
            DeltaSeconds = deltaSeconds;
            ProcessedExits = processedExits;
            Capped = capped;
            ExitResults = exitResults == null ? Array.Empty<CombatExitResult>() : new List<CombatExitResult>(exitResults).AsReadOnly();
            EndedEncounters = endedEncounters == null ? Array.Empty<CombatEncounterSnapshot>() : new List<CombatEncounterSnapshot>(endedEncounters).AsReadOnly();
            SplitResults = splitResults == null ? Array.Empty<CombatEncounterSplitResult>() : new List<CombatEncounterSplitResult>(splitResults).AsReadOnly();
        }

        public float DeltaSeconds { get; }
        public int ProcessedExits { get; }
        public bool Capped { get; }
        public IReadOnlyList<CombatExitResult> ExitResults { get; }
        public IReadOnlyList<CombatEncounterSnapshot> EndedEncounters { get; }
        public IReadOnlyList<CombatEncounterSplitResult> SplitResults { get; }
    }
}
