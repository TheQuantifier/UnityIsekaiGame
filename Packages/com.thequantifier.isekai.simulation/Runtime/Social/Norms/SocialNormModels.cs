using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Social.Norms
{
    [Serializable]
    public sealed class SocialNormContextConditionData
    {
        public string conditionId;
        public string actorRoleId;
        public string targetRoleId;
        public string relationshipDefinitionId;
        public string placeId;
        public string audienceId;
        public string requiredTag;
        public bool hasVisibility;
        public SocialInteractionVisibility visibility;
        public bool hasChannel;
        public SocialInteractionCommunicationChannel channel;
        public bool requiresWitness;
        public bool optional;

        public SocialNormContextConditionData Clone()
        {
            return new SocialNormContextConditionData
            {
                conditionId = conditionId ?? string.Empty,
                actorRoleId = actorRoleId ?? string.Empty,
                targetRoleId = targetRoleId ?? string.Empty,
                relationshipDefinitionId = relationshipDefinitionId ?? string.Empty,
                placeId = placeId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                requiredTag = requiredTag ?? string.Empty,
                hasVisibility = hasVisibility,
                visibility = visibility,
                hasChannel = hasChannel,
                channel = channel,
                requiresWitness = requiresWitness,
                optional = optional
            };
        }
    }

    [Serializable]
    public sealed class SocialNormExceptionDefinitionData
    {
        public string exceptionId;
        public SocialNormExceptionKind kind;
        public SocialNormExceptionEffect effect;
        public int severityDelta;
        public string requiredTag;
        public string redirectNormId;
        public bool suppressConsequences;

        public SocialNormExceptionDefinitionData Clone()
        {
            return new SocialNormExceptionDefinitionData
            {
                exceptionId = exceptionId ?? string.Empty,
                kind = kind,
                effect = effect,
                severityDelta = severityDelta,
                requiredTag = requiredTag ?? string.Empty,
                redirectNormId = redirectNormId ?? string.Empty,
                suppressConsequences = suppressConsequences
            };
        }
    }

    [Serializable]
    public sealed class SocialNormConsequenceDefinitionData
    {
        public string consequenceId;
        public SocialNormConsequenceTargetRuntime targetRuntime;
        public SocialNormConsequenceOperation operation;
        public SocialNormConsequencePolicy policy;
        public string dimensionId;
        public string audienceId;
        public string relationshipDefinitionId;
        public string rumorDefinitionId;
        public string rumorChannelId;
        public int amount;
        public SocialNormAssessmentClassification[] appliesToClassifications = Array.Empty<SocialNormAssessmentClassification>();
        public bool observersOnly;
        public bool publicOnly;

        public SocialNormConsequenceDefinitionData Clone()
        {
            return new SocialNormConsequenceDefinitionData
            {
                consequenceId = consequenceId ?? string.Empty,
                targetRuntime = targetRuntime,
                operation = operation,
                policy = policy,
                dimensionId = dimensionId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                relationshipDefinitionId = relationshipDefinitionId ?? string.Empty,
                rumorDefinitionId = rumorDefinitionId ?? string.Empty,
                rumorChannelId = rumorChannelId ?? string.Empty,
                amount = amount,
                appliesToClassifications = appliesToClassifications == null ? Array.Empty<SocialNormAssessmentClassification>() : appliesToClassifications.ToArray(),
                observersOnly = observersOnly,
                publicOnly = publicOnly
            };
        }
    }

    [Serializable]
    public sealed class SocialNormConditionEvaluationData
    {
        public string conditionId;
        public bool passed;
        public bool optional;
        public string reason;

        public SocialNormConditionEvaluationData Clone()
        {
            return new SocialNormConditionEvaluationData
            {
                conditionId = conditionId ?? string.Empty,
                passed = passed,
                optional = optional,
                reason = reason ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialNormExceptionResultData
    {
        public string exceptionId;
        public SocialNormExceptionKind kind;
        public SocialNormExceptionEffect effect;
        public bool applied;
        public string reason;

        public SocialNormExceptionResultData Clone()
        {
            return new SocialNormExceptionResultData
            {
                exceptionId = exceptionId ?? string.Empty,
                kind = kind,
                effect = effect,
                applied = applied,
                reason = reason ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialNormObserverResultData
    {
        public string observerPersonId;
        public string audienceId;
        public SocialNormObserverAwarenessState awareness;
        public SocialNormActorKnowledgeState normKnowledge;
        public SocialNormAssessmentClassification classification;
        public int severity;
        public string interpretation;

        public SocialNormObserverResultData Clone()
        {
            return new SocialNormObserverResultData
            {
                observerPersonId = observerPersonId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                awareness = awareness,
                normKnowledge = normKnowledge,
                classification = classification,
                severity = severity,
                interpretation = interpretation ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialNormConflictResultData
    {
        public string winnerNormId;
        public string suppressedNormId;
        public string reason;
        public int order;

        public SocialNormConflictResultData Clone()
        {
            return new SocialNormConflictResultData
            {
                winnerNormId = winnerNormId ?? string.Empty,
                suppressedNormId = suppressedNormId ?? string.Empty,
                reason = reason ?? string.Empty,
                order = order
            };
        }
    }

    [Serializable]
    public sealed class SocialNormConsequenceRecordData
    {
        public string consequenceId;
        public SocialNormConsequenceTargetRuntime targetRuntime;
        public SocialNormConsequenceOperation operation;
        public SocialNormConsequencePolicy policy;
        public string sourceAssessmentId;
        public string transactionId;
        public string observerPersonId;
        public string subjectPersonId;
        public string dimensionId;
        public string audienceId;
        public string affectedRecordId;
        public int amount;
        public bool committed;
        public string status;
        public string message;

        public SocialNormConsequenceRecordData Clone()
        {
            return new SocialNormConsequenceRecordData
            {
                consequenceId = consequenceId ?? string.Empty,
                targetRuntime = targetRuntime,
                operation = operation,
                policy = policy,
                sourceAssessmentId = sourceAssessmentId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                observerPersonId = observerPersonId ?? string.Empty,
                subjectPersonId = subjectPersonId ?? string.Empty,
                dimensionId = dimensionId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                affectedRecordId = affectedRecordId ?? string.Empty,
                amount = amount,
                committed = committed,
                status = status ?? string.Empty,
                message = message ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialNormAssessmentRecordData
    {
        public string assessmentRecordId;
        public string transactionId;
        public string normDefinitionId;
        public string actorPersonId;
        public string targetPersonId;
        public string interactionRecordId;
        public string interactionDefinitionId;
        public string historicalEventId;
        public string promiseId;
        public SocialInteractionSubjectData subject = new SocialInteractionSubjectData();
        public string placeId;
        public string audienceId;
        public string[] witnessPersonIds = Array.Empty<string>();
        public string[] contextTags = Array.Empty<string>();
        public SocialNormApplicabilityStatus applicability;
        public SocialNormAssessmentClassification classification;
        public SocialNormActorKnowledgeState actorKnowledge;
        public SocialNormVisibility visibility;
        public int severity;
        public int priority;
        public double occurrenceWorldTime;
        public double evaluationWorldTime;
        public SocialNormConditionEvaluationData[] conditions = Array.Empty<SocialNormConditionEvaluationData>();
        public SocialNormExceptionResultData[] exceptions = Array.Empty<SocialNormExceptionResultData>();
        public SocialNormObserverResultData[] observers = Array.Empty<SocialNormObserverResultData>();
        public SocialNormConflictResultData[] conflicts = Array.Empty<SocialNormConflictResultData>();
        public SocialNormConsequenceRecordData[] consequences = Array.Empty<SocialNormConsequenceRecordData>();
        public string[] diagnostics = Array.Empty<string>();
        public long revision = 1L;

        public SocialNormAssessmentRecordData Clone()
        {
            return new SocialNormAssessmentRecordData
            {
                assessmentRecordId = assessmentRecordId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                normDefinitionId = normDefinitionId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                targetPersonId = targetPersonId ?? string.Empty,
                interactionRecordId = interactionRecordId ?? string.Empty,
                interactionDefinitionId = interactionDefinitionId ?? string.Empty,
                historicalEventId = historicalEventId ?? string.Empty,
                promiseId = promiseId ?? string.Empty,
                subject = subject?.Clone() ?? new SocialInteractionSubjectData(),
                placeId = placeId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                witnessPersonIds = Clean(witnessPersonIds),
                contextTags = Clean(contextTags),
                applicability = applicability,
                classification = classification,
                actorKnowledge = actorKnowledge,
                visibility = visibility,
                severity = severity,
                priority = priority,
                occurrenceWorldTime = occurrenceWorldTime,
                evaluationWorldTime = evaluationWorldTime,
                conditions = conditions == null ? Array.Empty<SocialNormConditionEvaluationData>() : conditions.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                exceptions = exceptions == null ? Array.Empty<SocialNormExceptionResultData>() : exceptions.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                observers = observers == null ? Array.Empty<SocialNormObserverResultData>() : observers.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                conflicts = conflicts == null ? Array.Empty<SocialNormConflictResultData>() : conflicts.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                consequences = consequences == null ? Array.Empty<SocialNormConsequenceRecordData>() : consequences.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                diagnostics = Clean(diagnostics),
                revision = revision
            };
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class SocialNormProcessedTransactionData
    {
        public string transactionId;
        public string[] assessmentRecordIds = Array.Empty<string>();
        public SocialNormOperationStatus status;
        public long revision;

        public SocialNormProcessedTransactionData Clone()
        {
            return new SocialNormProcessedTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                assessmentRecordIds = (assessmentRecordIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                status = status,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class SocialNormRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<SocialNormAssessmentRecordData> assessments = new List<SocialNormAssessmentRecordData>();
        public List<SocialNormProcessedTransactionData> processedTransactions = new List<SocialNormProcessedTransactionData>();

        public SocialNormRuntimeSaveData Clone()
        {
            return new SocialNormRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                assessments = assessments == null ? new List<SocialNormAssessmentRecordData>() : assessments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedTransactions = processedTransactions == null ? new List<SocialNormProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class SocialNormEvaluationRequest
    {
        public string TransactionId { get; set; }
        public string AssessmentRecordId { get; set; }
        public string ActorPersonId { get; set; }
        public string TargetPersonId { get; set; }
        public string InteractionRecordId { get; set; }
        public string InteractionDefinitionId { get; set; }
        public string HistoricalEventId { get; set; }
        public string PromiseId { get; set; }
        public SocialInteractionSubjectData Subject { get; set; } = new SocialInteractionSubjectData();
        public string PlaceId { get; set; }
        public string AudienceId { get; set; }
        public string[] WitnessPersonIds { get; set; } = Array.Empty<string>();
        public string[] ContextTags { get; set; } = Array.Empty<string>();
        public string[] RequestedNormIds { get; set; } = Array.Empty<string>();
        public SocialInteractionVisibility Visibility { get; set; } = SocialInteractionVisibility.Private;
        public SocialInteractionCommunicationChannel Channel { get; set; } = SocialInteractionCommunicationChannel.Conversation;
        public SocialNormAssessmentClassification ConductClassification { get; set; } = SocialNormAssessmentClassification.Unknown;
        public SocialNormActorKnowledgeState ActorKnowledge { get; set; } = SocialNormActorKnowledgeState.Unknown;
        public double OccurrenceWorldTime { get; set; }
        public double EvaluationWorldTime { get; set; }
        public string DeterministicSeed { get; set; }
        public bool Preview { get; set; }

        public SocialNormEvaluationRequest Clone()
        {
            return new SocialNormEvaluationRequest
            {
                TransactionId = TransactionId ?? string.Empty,
                AssessmentRecordId = AssessmentRecordId ?? string.Empty,
                ActorPersonId = ActorPersonId ?? string.Empty,
                TargetPersonId = TargetPersonId ?? string.Empty,
                InteractionRecordId = InteractionRecordId ?? string.Empty,
                InteractionDefinitionId = InteractionDefinitionId ?? string.Empty,
                HistoricalEventId = HistoricalEventId ?? string.Empty,
                PromiseId = PromiseId ?? string.Empty,
                Subject = Subject?.Clone() ?? new SocialInteractionSubjectData(),
                PlaceId = PlaceId ?? string.Empty,
                AudienceId = AudienceId ?? string.Empty,
                WitnessPersonIds = Clean(WitnessPersonIds),
                ContextTags = Clean(ContextTags),
                RequestedNormIds = Clean(RequestedNormIds),
                Visibility = Visibility,
                Channel = Channel,
                ConductClassification = ConductClassification,
                ActorKnowledge = ActorKnowledge,
                OccurrenceWorldTime = OccurrenceWorldTime,
                EvaluationWorldTime = EvaluationWorldTime,
                DeterministicSeed = DeterministicSeed ?? string.Empty,
                Preview = Preview
            };
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public sealed class SocialNormAssessmentSnapshot
    {
        public SocialNormAssessmentSnapshot(SocialNormAssessmentRecordData data)
        {
            Data = data?.Clone() ?? new SocialNormAssessmentRecordData();
        }

        public SocialNormAssessmentRecordData Data { get; }
        public string AssessmentRecordId => Data.assessmentRecordId ?? string.Empty;
        public string NormDefinitionId => Data.normDefinitionId ?? string.Empty;
        public string ActorPersonId => Data.actorPersonId ?? string.Empty;
        public string TargetPersonId => Data.targetPersonId ?? string.Empty;
        public string InteractionRecordId => Data.interactionRecordId ?? string.Empty;
        public string PromiseId => Data.promiseId ?? string.Empty;
        public SocialNormApplicabilityStatus Applicability => Data.applicability;
        public SocialNormAssessmentClassification Classification => Data.classification;
        public SocialNormActorKnowledgeState ActorKnowledge => Data.actorKnowledge;
        public int Severity => Data.severity;
        public IReadOnlyList<SocialNormObserverResultData> Observers => Data.observers ?? Array.Empty<SocialNormObserverResultData>();
        public IReadOnlyList<SocialNormConflictResultData> Conflicts => Data.conflicts ?? Array.Empty<SocialNormConflictResultData>();
        public IReadOnlyList<SocialNormConsequenceRecordData> Consequences => Data.consequences ?? Array.Empty<SocialNormConsequenceRecordData>();
        public long Revision => Data.revision;
    }

    public sealed class SocialNormEvaluationResult
    {
        private SocialNormEvaluationResult(bool succeeded, SocialNormOperationStatus status, string message, string transactionId, bool preview, bool duplicate, IReadOnlyList<SocialNormAssessmentSnapshot> assessments, IReadOnlyList<SocialNormAssessmentSnapshot> candidates, long beforeRevision, long afterRevision)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Assessments = (assessments ?? Array.Empty<SocialNormAssessmentSnapshot>()).ToArray();
            CandidateAssessments = (candidates ?? Array.Empty<SocialNormAssessmentSnapshot>()).ToArray();
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
        }

        public bool Succeeded { get; }
        public SocialNormOperationStatus Status { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public IReadOnlyList<SocialNormAssessmentSnapshot> Assessments { get; }
        public IReadOnlyList<SocialNormAssessmentSnapshot> CandidateAssessments { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }

        public static SocialNormEvaluationResult Success(SocialNormOperationStatus status, string message, string transactionId, IReadOnlyList<SocialNormAssessmentSnapshot> assessments, IReadOnlyList<SocialNormAssessmentSnapshot> candidates, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false)
        {
            return new SocialNormEvaluationResult(true, status, message, transactionId, preview, duplicate, assessments, candidates, beforeRevision, afterRevision);
        }

        public static SocialNormEvaluationResult Failure(SocialNormOperationStatus status, string message, string transactionId = "", long revision = 0L)
        {
            return new SocialNormEvaluationResult(false, status, message, transactionId, false, false, Array.Empty<SocialNormAssessmentSnapshot>(), Array.Empty<SocialNormAssessmentSnapshot>(), revision, revision);
        }
    }
}
