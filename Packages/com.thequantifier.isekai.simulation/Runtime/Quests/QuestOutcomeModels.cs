using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using static UnityIsekaiGame.Quests.QuestOutcomeModelUtility;

namespace UnityIsekaiGame.Quests
{
    [Serializable]
    public sealed class QuestCompletionPolicyData
    {
        public QuestCompletionPolicy policy = QuestCompletionPolicy.AutoCompleteWhenRequiredObjectivesSatisfied;
        public string requiredInteractionPointId;
        public string requiredIssuerId;
        public bool requireAllRequiredObjectives = true;
        public bool allowOptionalBonusRewards = true;

        public QuestCompletionPolicyData Clone()
        {
            return new QuestCompletionPolicyData
            {
                policy = policy,
                requiredInteractionPointId = N(requiredInteractionPointId),
                requiredIssuerId = N(requiredIssuerId),
                requireAllRequiredObjectives = requireAllRequiredObjectives,
                allowOptionalBonusRewards = allowOptionalBonusRewards
            };
        }
    }

    [Serializable]
    public sealed class QuestDeadlineDefinitionData
    {
        public string deadlineDefinitionId;
        public QuestDeadlineStartKind startKind = QuestDeadlineStartKind.AssignmentAccepted;
        public QuestDeadlineExpirationPolicy expirationPolicy = QuestDeadlineExpirationPolicy.FailAssignment;
        public double absoluteWorldTime = -1d;
        public double durationFromStart = -1d;
        public bool hidden;

        public QuestDeadlineDefinitionData Clone()
        {
            return new QuestDeadlineDefinitionData
            {
                deadlineDefinitionId = N(deadlineDefinitionId),
                startKind = startKind,
                expirationPolicy = expirationPolicy,
                absoluteWorldTime = absoluteWorldTime,
                durationFromStart = durationFromStart,
                hidden = hidden
            };
        }
    }

    [Serializable]
    public sealed class QuestFailureConditionDefinitionData
    {
        public string failureConditionId;
        public QuestFailureReasonCode reasonCode = QuestFailureReasonCode.Custom;
        public QuestFailureTriggerKind triggerKind = QuestFailureTriggerKind.DomainEvent;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public bool hidden;

        public QuestFailureConditionDefinitionData Clone()
        {
            return new QuestFailureConditionDefinitionData
            {
                failureConditionId = N(failureConditionId),
                reasonCode = reasonCode,
                triggerKind = triggerKind,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                hidden = hidden
            };
        }
    }

    [Serializable]
    public sealed class QuestRewardDefinitionData
    {
        public string rewardDefinitionId;
        public QuestRewardCategory category = QuestRewardCategory.Custom;
        public string targetDefinitionId;
        public string secondaryTargetId;
        public int quantity = 1;
        public bool optional;
        public bool hidden;
        public string metadata;

        public QuestRewardDefinitionData Clone()
        {
            return new QuestRewardDefinitionData
            {
                rewardDefinitionId = N(rewardDefinitionId),
                category = category,
                targetDefinitionId = N(targetDefinitionId),
                secondaryTargetId = N(secondaryTargetId),
                quantity = quantity,
                optional = optional,
                hidden = hidden,
                metadata = metadata ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class QuestRewardPackageDefinitionData
    {
        public string rewardPackageId;
        public QuestRewardDeliveryPolicy deliveryPolicy = QuestRewardDeliveryPolicy.ClaimAfterCompletion;
        public QuestRewardPackageAtomicityPolicy atomicityPolicy = QuestRewardPackageAtomicityPolicy.AllOrNothing;
        public QuestRewardDefinitionData[] rewards = Array.Empty<QuestRewardDefinitionData>();

        public QuestRewardPackageDefinitionData Clone()
        {
            return new QuestRewardPackageDefinitionData
            {
                rewardPackageId = N(rewardPackageId),
                deliveryPolicy = deliveryPolicy,
                atomicityPolicy = atomicityPolicy,
                rewards = (rewards ?? Array.Empty<QuestRewardDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class QuestConsequenceDefinitionData
    {
        public string consequenceDefinitionId;
        public QuestTerminalOutcomeKind appliesTo = QuestTerminalOutcomeKind.Failed;
        public QuestRewardCategory category = QuestRewardCategory.Custom;
        public string targetDefinitionId;
        public int magnitude;
        public bool hidden;

        public QuestConsequenceDefinitionData Clone()
        {
            return new QuestConsequenceDefinitionData
            {
                consequenceDefinitionId = N(consequenceDefinitionId),
                appliesTo = appliesTo,
                category = category,
                targetDefinitionId = N(targetDefinitionId),
                magnitude = magnitude,
                hidden = hidden
            };
        }
    }

    [Serializable]
    public sealed class QuestTerminalOutcomeRecordData
    {
        public string outcomeId;
        public string terminalOutcomeId;
        public string questId;
        public string questDefinitionId;
        public string assignmentId;
        public string worldId;
        public QuestOutcomeScope scope = QuestOutcomeScope.Assignment;
        public QuestTerminalOutcomeKind outcomeKind = QuestTerminalOutcomeKind.Completed;
        public QuestFailureReasonCode failureReason = QuestFailureReasonCode.Unknown;
        public QuestFailureTriggerKind triggerKind = QuestFailureTriggerKind.ExplicitRequest;
        public string sourceEventId;
        public string actorPersonId;
        public string issuerId;
        public string interactionPointId;
        public string locationId;
        public double worldTime;
        public string provenanceId;
        public bool hidden;
        public long revision = 1L;

        public QuestTerminalOutcomeRecordData Clone()
        {
            return new QuestTerminalOutcomeRecordData
            {
                outcomeId = N(outcomeId),
                terminalOutcomeId = N(terminalOutcomeId),
                questId = N(questId),
                questDefinitionId = N(questDefinitionId),
                assignmentId = N(assignmentId),
                worldId = N(worldId),
                scope = scope,
                outcomeKind = outcomeKind,
                failureReason = failureReason,
                triggerKind = triggerKind,
                sourceEventId = N(sourceEventId),
                actorPersonId = N(actorPersonId),
                issuerId = N(issuerId),
                interactionPointId = N(interactionPointId),
                locationId = N(locationId),
                worldTime = worldTime,
                provenanceId = N(provenanceId),
                hidden = hidden,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestDeadlineRecordData
    {
        public string deadlineId;
        public string deadlineDefinitionId;
        public string questId;
        public string questDefinitionId;
        public string assignmentId;
        public string worldId;
        public QuestDeadlineStartKind startKind = QuestDeadlineStartKind.AssignmentAccepted;
        public QuestDeadlineExpirationPolicy expirationPolicy = QuestDeadlineExpirationPolicy.FailAssignment;
        public double startWorldTime;
        public double deadlineWorldTime = -1d;
        public bool expired;
        public bool handled;
        public string terminalOutcomeId;
        public bool hidden;
        public long revision = 1L;

        public QuestDeadlineRecordData Clone()
        {
            return new QuestDeadlineRecordData
            {
                deadlineId = N(deadlineId),
                deadlineDefinitionId = N(deadlineDefinitionId),
                questId = N(questId),
                questDefinitionId = N(questDefinitionId),
                assignmentId = N(assignmentId),
                worldId = N(worldId),
                startKind = startKind,
                expirationPolicy = expirationPolicy,
                startWorldTime = startWorldTime,
                deadlineWorldTime = deadlineWorldTime,
                expired = expired,
                handled = handled,
                terminalOutcomeId = N(terminalOutcomeId),
                hidden = hidden,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestRewardEntitlementRecordData
    {
        public string entitlementId;
        public string rewardPackageId;
        public string rewardDefinitionId;
        public string terminalOutcomeId;
        public string questId;
        public string assignmentId;
        public string recipientPersonId;
        public string worldId;
        public QuestRewardCategory category = QuestRewardCategory.Custom;
        public QuestRewardDeliveryPolicy deliveryPolicy = QuestRewardDeliveryPolicy.ClaimAfterCompletion;
        public string targetDefinitionId;
        public string secondaryTargetId;
        public int quantity;
        public bool optional;
        public bool hidden;
        public QuestRewardEntitlementState state = QuestRewardEntitlementState.Pending;
        public string lastGrantId;
        public string failureReason;
        public double createdWorldTime;
        public double grantedWorldTime = -1d;
        public long revision = 1L;

        public QuestRewardEntitlementRecordData Clone()
        {
            return new QuestRewardEntitlementRecordData
            {
                entitlementId = N(entitlementId),
                rewardPackageId = N(rewardPackageId),
                rewardDefinitionId = N(rewardDefinitionId),
                terminalOutcomeId = N(terminalOutcomeId),
                questId = N(questId),
                assignmentId = N(assignmentId),
                recipientPersonId = N(recipientPersonId),
                worldId = N(worldId),
                category = category,
                deliveryPolicy = deliveryPolicy,
                targetDefinitionId = N(targetDefinitionId),
                secondaryTargetId = N(secondaryTargetId),
                quantity = quantity,
                optional = optional,
                hidden = hidden,
                state = state,
                lastGrantId = N(lastGrantId),
                failureReason = failureReason ?? string.Empty,
                createdWorldTime = createdWorldTime,
                grantedWorldTime = grantedWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestRewardGrantRecordData
    {
        public string grantId;
        public string entitlementId;
        public string terminalOutcomeId;
        public string recipientPersonId;
        public string worldId;
        public QuestRewardCategory category = QuestRewardCategory.Custom;
        public string ownerRuntimeId;
        public string ownerRecordId;
        public QuestRewardGrantState state = QuestRewardGrantState.Prepared;
        public string failureReason;
        public double worldTime;
        public long revision = 1L;

        public QuestRewardGrantRecordData Clone()
        {
            return new QuestRewardGrantRecordData
            {
                grantId = N(grantId),
                entitlementId = N(entitlementId),
                terminalOutcomeId = N(terminalOutcomeId),
                recipientPersonId = N(recipientPersonId),
                worldId = N(worldId),
                category = category,
                ownerRuntimeId = N(ownerRuntimeId),
                ownerRecordId = N(ownerRecordId),
                state = state,
                failureReason = failureReason ?? string.Empty,
                worldTime = worldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestOutcomeTransactionData
    {
        public string transactionId;
        public string operation;
        public string assignmentId;
        public string questId;
        public string terminalOutcomeId;
        public string entitlementId;
        public long runtimeRevision;

        public QuestOutcomeTransactionData Clone()
        {
            return new QuestOutcomeTransactionData
            {
                transactionId = N(transactionId),
                operation = N(operation),
                assignmentId = N(assignmentId),
                questId = N(questId),
                terminalOutcomeId = N(terminalOutcomeId),
                entitlementId = N(entitlementId),
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestOutcomeEventData
    {
        public string eventId;
        public string transactionId;
        public QuestOutcomeEventKind eventKind;
        public string questId;
        public string assignmentId;
        public string terminalOutcomeId;
        public string rewardEntitlementId;
        public string sourceEventId;
        public double worldTime;
        public long runtimeRevision;

        public QuestOutcomeEventData Clone()
        {
            return new QuestOutcomeEventData
            {
                eventId = N(eventId),
                transactionId = N(transactionId),
                eventKind = eventKind,
                questId = N(questId),
                assignmentId = N(assignmentId),
                terminalOutcomeId = N(terminalOutcomeId),
                rewardEntitlementId = N(rewardEntitlementId),
                sourceEventId = N(sourceEventId),
                worldTime = worldTime,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestOutcomeRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<QuestTerminalOutcomeRecordData> terminalOutcomes = new List<QuestTerminalOutcomeRecordData>();
        public List<QuestDeadlineRecordData> deadlines = new List<QuestDeadlineRecordData>();
        public List<QuestRewardEntitlementRecordData> rewardEntitlements = new List<QuestRewardEntitlementRecordData>();
        public List<QuestRewardGrantRecordData> rewardGrants = new List<QuestRewardGrantRecordData>();
        public List<QuestOutcomeTransactionData> transactions = new List<QuestOutcomeTransactionData>();
        public List<QuestOutcomeEventData> events = new List<QuestOutcomeEventData>();

        public QuestOutcomeRuntimeSaveData Clone()
        {
            return new QuestOutcomeRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                terminalOutcomes = (terminalOutcomes ?? new List<QuestTerminalOutcomeRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                deadlines = (deadlines ?? new List<QuestDeadlineRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                rewardEntitlements = (rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                rewardGrants = (rewardGrants ?? new List<QuestRewardGrantRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<QuestOutcomeTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                events = (events ?? new List<QuestOutcomeEventData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    public class QuestCompletionEvaluationRequest
    {
        public string assignmentId;
        public string requesterPersonId;
        public string interactionPointId;
        public string issuerId;
        public string locationId;
        public double worldTime;
        public QuestVisibilityAccess access = QuestVisibilityAccess.PrivilegedDiagnostic;
        public bool preview;
    }

    public sealed class QuestCompletionRequest : QuestCompletionEvaluationRequest
    {
        public string transactionId;
        public string sourceEventId;
        public string provenanceId;
        public bool forceSystemCompletion;
    }

    public sealed class QuestFailureRequest
    {
        public string transactionId;
        public string assignmentId;
        public string questId;
        public string actorPersonId;
        public QuestFailureReasonCode reasonCode = QuestFailureReasonCode.Custom;
        public QuestFailureTriggerKind triggerKind = QuestFailureTriggerKind.ExplicitRequest;
        public string sourceEventId;
        public string provenanceId;
        public double worldTime;
        public bool preview;
    }

    public sealed class QuestRewardClaimRequest
    {
        public string transactionId;
        public string entitlementId;
        public string claimantPersonId;
        public double worldTime;
        public bool preview;
    }

    public sealed class QuestOutcomeQuery
    {
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public string questId;
        public string assignmentId;
        public string terminalOutcomeId;
        public QuestTerminalOutcomeKind? outcomeKind;
        public bool includeHidden;
        public string worldId;
    }

    public sealed class QuestRewardQuery
    {
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public string entitlementId;
        public string questId;
        public string assignmentId;
        public string recipientPersonId;
        public bool includeTerminal;
        public bool includeHidden;
        public string worldId;
    }

    public sealed class QuestCompletionEvaluationResult
    {
        public QuestCompletionEvaluationResult(QuestOutcomeOperationStatus status, string message, QuestAssignmentSnapshot assignment, QuestAssignmentObjectiveSummary objectiveSummary, QuestCompletionPolicyData policy, QuestDeadlineSnapshot blockingDeadline, bool ready, bool preview)
        {
            Status = status;
            Message = message ?? string.Empty;
            Assignment = assignment;
            ObjectiveSummary = objectiveSummary;
            Policy = policy?.Clone() ?? new QuestCompletionPolicyData();
            BlockingDeadline = blockingDeadline;
            Ready = ready;
            Preview = preview;
        }

        public QuestOutcomeOperationStatus Status { get; }
        public string Message { get; }
        public QuestAssignmentSnapshot Assignment { get; }
        public QuestAssignmentObjectiveSummary ObjectiveSummary { get; }
        public QuestCompletionPolicyData Policy { get; }
        public QuestDeadlineSnapshot BlockingDeadline { get; }
        public bool Ready { get; }
        public bool Preview { get; }
        public bool Succeeded => Status == QuestOutcomeOperationStatus.Succeeded || Status == QuestOutcomeOperationStatus.Preview;
    }

    public sealed class QuestTerminalOutcomeSnapshot
    {
        private readonly QuestTerminalOutcomeRecordData data;

        public QuestTerminalOutcomeSnapshot(QuestTerminalOutcomeRecordData record, bool redacted = false)
        {
            data = record?.Clone() ?? new QuestTerminalOutcomeRecordData();
            Redacted = redacted;
        }

        public string OutcomeId => data.outcomeId ?? string.Empty;
        public string TerminalOutcomeId => data.terminalOutcomeId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string QuestDefinitionId => data.questDefinitionId ?? string.Empty;
        public string AssignmentId => data.assignmentId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public QuestOutcomeScope Scope => data.scope;
        public QuestTerminalOutcomeKind OutcomeKind => data.outcomeKind;
        public QuestFailureReasonCode FailureReason => Redacted ? QuestFailureReasonCode.Unknown : data.failureReason;
        public QuestFailureTriggerKind TriggerKind => Redacted ? QuestFailureTriggerKind.Unknown : data.triggerKind;
        public string SourceEventId => Redacted ? string.Empty : data.sourceEventId ?? string.Empty;
        public string ActorPersonId => Redacted ? string.Empty : data.actorPersonId ?? string.Empty;
        public string IssuerId => Redacted ? string.Empty : data.issuerId ?? string.Empty;
        public string InteractionPointId => Redacted ? string.Empty : data.interactionPointId ?? string.Empty;
        public string LocationId => Redacted ? string.Empty : data.locationId ?? string.Empty;
        public double WorldTime => data.worldTime;
        public bool Hidden => data.hidden;
        public bool Redacted { get; }
        public long Revision => data.revision;
        public QuestTerminalOutcomeRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestDeadlineSnapshot
    {
        private readonly QuestDeadlineRecordData data;

        public QuestDeadlineSnapshot(QuestDeadlineRecordData record, bool redacted = false)
        {
            data = record?.Clone() ?? new QuestDeadlineRecordData();
            Redacted = redacted;
        }

        public string DeadlineId => data.deadlineId ?? string.Empty;
        public string DeadlineDefinitionId => data.deadlineDefinitionId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string AssignmentId => data.assignmentId ?? string.Empty;
        public double DeadlineWorldTime => Redacted ? -1d : data.deadlineWorldTime;
        public bool Expired => data.expired;
        public bool Handled => data.handled;
        public string TerminalOutcomeId => data.terminalOutcomeId ?? string.Empty;
        public bool Hidden => data.hidden;
        public bool Redacted { get; }
        public QuestDeadlineRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestRewardEntitlementSnapshot
    {
        private readonly QuestRewardEntitlementRecordData data;

        public QuestRewardEntitlementSnapshot(QuestRewardEntitlementRecordData record, bool redacted = false)
        {
            data = record?.Clone() ?? new QuestRewardEntitlementRecordData();
            Redacted = redacted;
        }

        public string EntitlementId => data.entitlementId ?? string.Empty;
        public string RewardDefinitionId => data.rewardDefinitionId ?? string.Empty;
        public string TerminalOutcomeId => data.terminalOutcomeId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string AssignmentId => data.assignmentId ?? string.Empty;
        public string RecipientPersonId => data.recipientPersonId ?? string.Empty;
        public QuestRewardCategory Category => Redacted ? QuestRewardCategory.Unknown : data.category;
        public string TargetDefinitionId => Redacted ? string.Empty : data.targetDefinitionId ?? string.Empty;
        public int Quantity => Redacted ? 0 : data.quantity;
        public bool Optional => data.optional;
        public bool Hidden => data.hidden;
        public QuestRewardEntitlementState State => data.state;
        public string FailureReason => Redacted ? string.Empty : data.failureReason ?? string.Empty;
        public bool Redacted { get; }
        public QuestRewardEntitlementRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestOutcomeOperationResult
    {
        private QuestOutcomeOperationResult(QuestOutcomeOperationStatus status, string message, QuestTerminalOutcomeSnapshot outcome, IReadOnlyList<QuestRewardEntitlementSnapshot> rewards, QuestRewardEntitlementSnapshot reward, QuestCompletionEvaluationResult evaluation, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Outcome = outcome;
            Rewards = rewards ?? Array.Empty<QuestRewardEntitlementSnapshot>();
            Reward = reward ?? Rewards.FirstOrDefault();
            Evaluation = evaluation;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public QuestOutcomeOperationStatus Status { get; }
        public string Message { get; }
        public QuestTerminalOutcomeSnapshot Outcome { get; }
        public IReadOnlyList<QuestRewardEntitlementSnapshot> Rewards { get; }
        public QuestRewardEntitlementSnapshot Reward { get; }
        public QuestCompletionEvaluationResult Evaluation { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == QuestOutcomeOperationStatus.Succeeded || Status == QuestOutcomeOperationStatus.Preview || Status == QuestOutcomeOperationStatus.Duplicate;

        public static QuestOutcomeOperationResult Success(string message, long before, long after, QuestTerminalOutcomeRecordData outcome = null, IEnumerable<QuestRewardEntitlementRecordData> rewards = null, QuestRewardEntitlementRecordData reward = null, QuestCompletionEvaluationResult evaluation = null, bool preview = false, bool duplicate = false)
        {
            return new QuestOutcomeOperationResult(preview ? QuestOutcomeOperationStatus.Preview : duplicate ? QuestOutcomeOperationStatus.Duplicate : QuestOutcomeOperationStatus.Succeeded, message, outcome == null ? null : new QuestTerminalOutcomeSnapshot(outcome), (rewards ?? Array.Empty<QuestRewardEntitlementRecordData>()).Select(value => new QuestRewardEntitlementSnapshot(value)).ToArray(), reward == null ? null : new QuestRewardEntitlementSnapshot(reward), evaluation, preview, duplicate, before, after);
        }

        public static QuestOutcomeOperationResult Failure(QuestOutcomeOperationStatus status, string message, long revision, QuestCompletionEvaluationResult evaluation = null)
        {
            return new QuestOutcomeOperationResult(status, message, null, Array.Empty<QuestRewardEntitlementSnapshot>(), null, evaluation, false, false, revision, revision);
        }
    }

    public sealed class QuestRewardEffectRequest
    {
        public string grantId;
        public string entitlementId;
        public string terminalOutcomeId;
        public string questId;
        public string assignmentId;
        public string recipientPersonId;
        public QuestRewardCategory category;
        public string targetDefinitionId;
        public string secondaryTargetId;
        public int quantity;
        public double worldTime;
    }

    public sealed class QuestRewardEffectResult
    {
        private QuestRewardEffectResult(bool succeeded, bool duplicate, bool unsupported, string ownerRuntimeId, string ownerRecordId, string message)
        {
            Succeeded = succeeded;
            Duplicate = duplicate;
            IsUnsupported = unsupported;
            OwnerRuntimeId = ownerRuntimeId ?? string.Empty;
            OwnerRecordId = ownerRecordId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Duplicate { get; }
        public bool IsUnsupported { get; }
        public string OwnerRuntimeId { get; }
        public string OwnerRecordId { get; }
        public string Message { get; }

        public static QuestRewardEffectResult Success(string ownerRuntimeId, string ownerRecordId = "", bool duplicate = false) => new QuestRewardEffectResult(true, duplicate, false, ownerRuntimeId, ownerRecordId, duplicate ? "Reward already granted." : "Reward granted.");
        public static QuestRewardEffectResult Unsupported(string message) => new QuestRewardEffectResult(false, false, true, string.Empty, string.Empty, message);
        public static QuestRewardEffectResult Failure(string message) => new QuestRewardEffectResult(false, false, false, string.Empty, string.Empty, message);
    }

    public interface IQuestRewardEffectExecutor
    {
        QuestRewardEffectResult Execute(QuestRewardEffectRequest request);
    }

    public sealed class QuestOutcomeValidationReport
    {
        public QuestOutcomeValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Quest outcome validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";
    }

    public static class QuestOutcomeModelUtility
    {
        public static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
