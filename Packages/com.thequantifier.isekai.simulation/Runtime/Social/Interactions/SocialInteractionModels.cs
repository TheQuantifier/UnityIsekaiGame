using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Social.Interactions
{
    [Serializable]
    public sealed class SocialInteractionParticipantData
    {
        public SocialInteractionRole role;
        public string personId;

        public SocialInteractionParticipantData Clone()
        {
            return new SocialInteractionParticipantData
            {
                role = role,
                personId = personId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialInteractionSubjectData
    {
        public SocialInteractionSubjectKind kind;
        public string subjectId;
        public string parentSubjectId;
        public string ownerPersonId;
        public string[] tags = Array.Empty<string>();

        public SocialInteractionSubjectData Clone()
        {
            return new SocialInteractionSubjectData
            {
                kind = kind,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = Clean(tags)
            };
        }

        public InformationSubjectReferenceData ToInformationSubject(string interactionRecordId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = string.IsNullOrWhiteSpace(subjectId) ? interactionRecordId ?? string.Empty : subjectId,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = Clean((tags ?? Array.Empty<string>()).Concat(new[] { "social-interaction", kind.ToString() }))
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
    public sealed class SocialConsequenceRecordData
    {
        public string consequenceId;
        public SocialConsequenceTargetRuntime targetRuntime;
        public SocialConsequenceOperation operation;
        public string sourceId;
        public string actorPersonId;
        public string subjectPersonId;
        public string dimensionId;
        public string audienceId;
        public string relationshipDefinitionId;
        public string rumorDefinitionId;
        public string rumorChannelId;
        public string affectedRecordId;
        public string transactionId;
        public int amount;
        public bool required;
        public bool committed;
        public string status;
        public string message;

        public SocialConsequenceRecordData Clone()
        {
            return new SocialConsequenceRecordData
            {
                consequenceId = consequenceId ?? string.Empty,
                targetRuntime = targetRuntime,
                operation = operation,
                sourceId = sourceId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                subjectPersonId = subjectPersonId ?? string.Empty,
                dimensionId = dimensionId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                relationshipDefinitionId = relationshipDefinitionId ?? string.Empty,
                rumorDefinitionId = rumorDefinitionId ?? string.Empty,
                rumorChannelId = rumorChannelId ?? string.Empty,
                affectedRecordId = affectedRecordId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                amount = amount,
                required = required,
                committed = committed,
                status = status ?? string.Empty,
                message = message ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialInteractionRecordData
    {
        public string interactionRecordId;
        public string transactionId;
        public string interactionDefinitionId;
        public string initiatorPersonId;
        public string targetPersonId;
        public SocialInteractionParticipantData[] participants = Array.Empty<SocialInteractionParticipantData>();
        public SocialInteractionSubjectData subject = new SocialInteractionSubjectData();
        public string placeId;
        public string audienceId;
        public SocialInteractionCommunicationChannel channel;
        public SocialInteractionVisibility visibility;
        public SocialInteractionResponse response;
        public SocialInteractionOutcome outcome;
        public double worldTime;
        public string deterministicSeed;
        public int deterministicRoll;
        public string pendingInteractionId;
        public string promiseId;
        public string historicalEventId;
        public string memoryReferenceId;
        public string rumorTransmissionId;
        public SocialConsequenceRecordData[] consequences = Array.Empty<SocialConsequenceRecordData>();
        public string[] diagnostics = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public SocialInteractionRecordData Clone()
        {
            return new SocialInteractionRecordData
            {
                interactionRecordId = interactionRecordId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                interactionDefinitionId = interactionDefinitionId ?? string.Empty,
                initiatorPersonId = initiatorPersonId ?? string.Empty,
                targetPersonId = targetPersonId ?? string.Empty,
                participants = participants == null ? Array.Empty<SocialInteractionParticipantData>() : participants.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                subject = subject?.Clone() ?? new SocialInteractionSubjectData(),
                placeId = placeId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                channel = channel,
                visibility = visibility,
                response = response,
                outcome = outcome,
                worldTime = worldTime,
                deterministicSeed = deterministicSeed ?? string.Empty,
                deterministicRoll = deterministicRoll,
                pendingInteractionId = pendingInteractionId ?? string.Empty,
                promiseId = promiseId ?? string.Empty,
                historicalEventId = historicalEventId ?? string.Empty,
                memoryReferenceId = memoryReferenceId ?? string.Empty,
                rumorTransmissionId = rumorTransmissionId ?? string.Empty,
                consequences = consequences == null ? Array.Empty<SocialConsequenceRecordData>() : consequences.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                diagnostics = Clean(diagnostics),
                tags = Clean(tags),
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
    public sealed class SocialPendingInteractionData
    {
        public string pendingInteractionId;
        public string interactionRecordId;
        public string transactionId;
        public string interactionDefinitionId;
        public string initiatorPersonId;
        public string targetPersonId;
        public SocialInteractionSubjectData subject = new SocialInteractionSubjectData();
        public SocialInteractionResponse[] availableResponses = Array.Empty<SocialInteractionResponse>();
        public SocialInteractionStatus status = SocialInteractionStatus.Pending;
        public double createdWorldTime;
        public double expirationWorldTime = -1d;
        public long revision = 1L;

        public SocialPendingInteractionData Clone()
        {
            return new SocialPendingInteractionData
            {
                pendingInteractionId = pendingInteractionId ?? string.Empty,
                interactionRecordId = interactionRecordId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                interactionDefinitionId = interactionDefinitionId ?? string.Empty,
                initiatorPersonId = initiatorPersonId ?? string.Empty,
                targetPersonId = targetPersonId ?? string.Empty,
                subject = subject?.Clone() ?? new SocialInteractionSubjectData(),
                availableResponses = availableResponses == null ? Array.Empty<SocialInteractionResponse>() : availableResponses.ToArray(),
                status = status,
                createdWorldTime = createdWorldTime,
                expirationWorldTime = expirationWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class SocialPromiseData
    {
        public string promiseId;
        public string sourceInteractionRecordId;
        public string promisorPersonId;
        public string promiseePersonId;
        public SocialInteractionSubjectData subject = new SocialInteractionSubjectData();
        public SocialPromiseStatus status = SocialPromiseStatus.Proposed;
        public double createdWorldTime;
        public double resolvedWorldTime = -1d;
        public string resolvedByInteractionRecordId;
        public long revision = 1L;

        public SocialPromiseData Clone()
        {
            return new SocialPromiseData
            {
                promiseId = promiseId ?? string.Empty,
                sourceInteractionRecordId = sourceInteractionRecordId ?? string.Empty,
                promisorPersonId = promisorPersonId ?? string.Empty,
                promiseePersonId = promiseePersonId ?? string.Empty,
                subject = subject?.Clone() ?? new SocialInteractionSubjectData(),
                status = status,
                createdWorldTime = createdWorldTime,
                resolvedWorldTime = resolvedWorldTime,
                resolvedByInteractionRecordId = resolvedByInteractionRecordId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class SocialInteractionProcessedTransactionData
    {
        public string transactionId;
        public string interactionRecordId;
        public SocialInteractionStatus status;
        public long revision;

        public SocialInteractionProcessedTransactionData Clone()
        {
            return new SocialInteractionProcessedTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                interactionRecordId = interactionRecordId ?? string.Empty,
                status = status,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class SocialInteractionCooldownData
    {
        public string cooldownKey;
        public double lastWorldTime;
        public string sourceInteractionRecordId;

        public SocialInteractionCooldownData Clone()
        {
            return new SocialInteractionCooldownData
            {
                cooldownKey = cooldownKey ?? string.Empty,
                lastWorldTime = lastWorldTime,
                sourceInteractionRecordId = sourceInteractionRecordId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class SocialInteractionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<SocialInteractionRecordData> records = new List<SocialInteractionRecordData>();
        public List<SocialPendingInteractionData> pendingInteractions = new List<SocialPendingInteractionData>();
        public List<SocialPromiseData> promises = new List<SocialPromiseData>();
        public List<SocialInteractionProcessedTransactionData> processedTransactions = new List<SocialInteractionProcessedTransactionData>();
        public List<SocialInteractionCooldownData> cooldowns = new List<SocialInteractionCooldownData>();

        public SocialInteractionRuntimeSaveData Clone()
        {
            return new SocialInteractionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<SocialInteractionRecordData>() : records.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                pendingInteractions = pendingInteractions == null ? new List<SocialPendingInteractionData>() : pendingInteractions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                promises = promises == null ? new List<SocialPromiseData>() : promises.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedTransactions = processedTransactions == null ? new List<SocialInteractionProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                cooldowns = cooldowns == null ? new List<SocialInteractionCooldownData>() : cooldowns.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class SocialInteractionRequest
    {
        public string TransactionId { get; set; }
        public string InteractionRecordId { get; set; }
        public string InteractionDefinitionId { get; set; }
        public string InitiatorPersonId { get; set; }
        public string TargetPersonId { get; set; }
        public IReadOnlyList<string> WitnessPersonIds { get; set; } = Array.Empty<string>();
        public string AudienceId { get; set; }
        public string PlaceId { get; set; }
        public SocialInteractionSubjectData Subject { get; set; } = new SocialInteractionSubjectData();
        public SocialInteractionResponse Response { get; set; } = SocialInteractionResponse.None;
        public SocialInteractionCommunicationChannel Channel { get; set; } = SocialInteractionCommunicationChannel.Conversation;
        public SocialInteractionVisibility? VisibilityOverride { get; set; }
        public double WorldTime { get; set; }
        public string DeterministicSeed { get; set; }
        public string OriginatingReferenceId { get; set; }
        public bool Preview { get; set; }

        public SocialInteractionRequest Clone()
        {
            return new SocialInteractionRequest
            {
                TransactionId = TransactionId ?? string.Empty,
                InteractionRecordId = InteractionRecordId ?? string.Empty,
                InteractionDefinitionId = InteractionDefinitionId ?? string.Empty,
                InitiatorPersonId = InitiatorPersonId ?? string.Empty,
                TargetPersonId = TargetPersonId ?? string.Empty,
                WitnessPersonIds = Clean(WitnessPersonIds),
                AudienceId = AudienceId ?? string.Empty,
                PlaceId = PlaceId ?? string.Empty,
                Subject = Subject?.Clone() ?? new SocialInteractionSubjectData(),
                Response = Response,
                Channel = Channel,
                VisibilityOverride = VisibilityOverride,
                WorldTime = WorldTime,
                DeterministicSeed = DeterministicSeed ?? string.Empty,
                OriginatingReferenceId = OriginatingReferenceId ?? string.Empty,
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

    public sealed class SocialInteractionSnapshot
    {
        public SocialInteractionSnapshot(SocialInteractionRecordData data)
        {
            Data = data?.Clone() ?? new SocialInteractionRecordData();
        }

        public SocialInteractionRecordData Data { get; }
        public string InteractionRecordId => Data.interactionRecordId ?? string.Empty;
        public string TransactionId => Data.transactionId ?? string.Empty;
        public string InteractionDefinitionId => Data.interactionDefinitionId ?? string.Empty;
        public string InitiatorPersonId => Data.initiatorPersonId ?? string.Empty;
        public string TargetPersonId => Data.targetPersonId ?? string.Empty;
        public IReadOnlyList<SocialInteractionParticipantData> Participants => Data.participants ?? Array.Empty<SocialInteractionParticipantData>();
        public SocialInteractionOutcome Outcome => Data.outcome;
        public SocialInteractionResponse Response => Data.response;
        public int DeterministicRoll => Data.deterministicRoll;
        public IReadOnlyList<SocialConsequenceRecordData> Consequences => Data.consequences ?? Array.Empty<SocialConsequenceRecordData>();
        public long Revision => Data.revision;
    }

    public sealed class SocialPendingInteractionSnapshot
    {
        public SocialPendingInteractionSnapshot(SocialPendingInteractionData data)
        {
            Data = data?.Clone() ?? new SocialPendingInteractionData();
        }

        public SocialPendingInteractionData Data { get; }
        public string PendingInteractionId => Data.pendingInteractionId ?? string.Empty;
        public string InteractionRecordId => Data.interactionRecordId ?? string.Empty;
        public SocialInteractionStatus Status => Data.status;
        public IReadOnlyList<SocialInteractionResponse> AvailableResponses => Data.availableResponses ?? Array.Empty<SocialInteractionResponse>();
    }

    public sealed class SocialPromiseSnapshot
    {
        public SocialPromiseSnapshot(SocialPromiseData data)
        {
            Data = data?.Clone() ?? new SocialPromiseData();
        }

        public SocialPromiseData Data { get; }
        public string PromiseId => Data.promiseId ?? string.Empty;
        public SocialPromiseStatus Status => Data.status;
        public string SourceInteractionRecordId => Data.sourceInteractionRecordId ?? string.Empty;
    }

    public sealed class SocialInteractionResult
    {
        private SocialInteractionResult(bool succeeded, SocialInteractionStatus status, string message, string transactionId, bool preview, bool duplicate, SocialInteractionSnapshot record, SocialPendingInteractionSnapshot pending, SocialPromiseSnapshot promise, IReadOnlyList<SocialConsequenceRecordData> plan, long beforeRevision, long afterRevision)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Record = record;
            Pending = pending;
            Promise = promise;
            ConsequencePlan = (plan ?? Array.Empty<SocialConsequenceRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
        }

        public bool Succeeded { get; }
        public SocialInteractionStatus Status { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public SocialInteractionSnapshot Record { get; }
        public SocialPendingInteractionSnapshot Pending { get; }
        public SocialPromiseSnapshot Promise { get; }
        public IReadOnlyList<SocialConsequenceRecordData> ConsequencePlan { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }

        public static SocialInteractionResult Success(SocialInteractionStatus status, string message, string transactionId, SocialInteractionSnapshot record, SocialPendingInteractionSnapshot pending, SocialPromiseSnapshot promise, IReadOnlyList<SocialConsequenceRecordData> plan, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false)
        {
            return new SocialInteractionResult(true, status, message, transactionId, preview, duplicate, record, pending, promise, plan, beforeRevision, afterRevision);
        }

        public static SocialInteractionResult Failure(SocialInteractionStatus status, string message, string transactionId = "", long revision = 0L)
        {
            return new SocialInteractionResult(false, status, message, transactionId, false, false, null, null, null, Array.Empty<SocialConsequenceRecordData>(), revision, revision);
        }
    }
}
