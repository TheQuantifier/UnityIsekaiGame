using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Social.Influence;

namespace UnityIsekaiGame.Social.Emotions
{
    [Serializable]
    public sealed class SocialEmotionCauseReferenceData
    {
        public SocialEmotionCauseCategory category;
        public string sourceRecordId;
        public string sourceRuntime;
        public string subjectId;
        public string targetPersonId;
        public SocialEmotionResponsibility responsibility = SocialEmotionResponsibility.Unknown;
        public SocialInfluenceTruthStatus believedTruthStatus = SocialInfluenceTruthStatus.Unknown;
        public SocialInfluenceDetectionOutcome detectionOutcome = SocialInfluenceDetectionOutcome.NotApplicable;
        public string[] tags = Array.Empty<string>();

        public SocialEmotionCauseReferenceData Clone() => new SocialEmotionCauseReferenceData
        {
            category = category,
            sourceRecordId = sourceRecordId ?? string.Empty,
            sourceRuntime = sourceRuntime ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            targetPersonId = targetPersonId ?? string.Empty,
            responsibility = responsibility,
            believedTruthStatus = believedTruthStatus,
            detectionOutcome = detectionOutcome,
            tags = Clean(tags)
        };

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialEmotionEpisodeData
    {
        public string episodeId;
        public string transactionId;
        public string personId;
        public string emotionDefinitionId;
        public string appraisalRuleId;
        public string targetPersonId;
        public string subjectId;
        public SocialEmotionCauseReferenceData cause = new SocialEmotionCauseReferenceData();
        public int baseIntensity;
        public int reinforcementCount = 1;
        public double startWorldTime;
        public double expirationWorldTime = -1d;
        public SocialEmotionVisibility visibility;
        public bool suppressed;
        public bool concealed;
        public bool active = true;
        public string decisionModifierId;
        public string[] diagnostics = Array.Empty<string>();
        public long revision = 1L;

        public SocialEmotionEpisodeData Clone() => new SocialEmotionEpisodeData
        {
            episodeId = episodeId ?? string.Empty,
            transactionId = transactionId ?? string.Empty,
            personId = personId ?? string.Empty,
            emotionDefinitionId = emotionDefinitionId ?? string.Empty,
            appraisalRuleId = appraisalRuleId ?? string.Empty,
            targetPersonId = targetPersonId ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            cause = cause?.Clone() ?? new SocialEmotionCauseReferenceData(),
            baseIntensity = baseIntensity,
            reinforcementCount = reinforcementCount,
            startWorldTime = startWorldTime,
            expirationWorldTime = expirationWorldTime,
            visibility = visibility,
            suppressed = suppressed,
            concealed = concealed,
            active = active,
            decisionModifierId = decisionModifierId ?? string.Empty,
            diagnostics = Clean(diagnostics),
            revision = revision
        };

        public bool IsActiveAt(double worldTime) => active && !suppressed && (expirationWorldTime < 0d || worldTime <= expirationWorldTime);
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialMoodStateData
    {
        public string personId;
        public string moodDimensionId;
        public int value;
        public double lastEvaluatedWorldTime;
        public string[] sourceEpisodeIds = Array.Empty<string>();
        public long revision = 1L;

        public SocialMoodStateData Clone() => new SocialMoodStateData
        {
            personId = personId ?? string.Empty,
            moodDimensionId = moodDimensionId ?? string.Empty,
            value = value,
            lastEvaluatedWorldTime = lastEvaluatedWorldTime,
            sourceEpisodeIds = Clean(sourceEpisodeIds),
            revision = revision
        };

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialEmotionDecisionModifierData
    {
        public string modifierId;
        public string sourceEpisodeId;
        public string actorPersonId;
        public string targetPersonId;
        public string intentionDefinitionId;
        public string interactionDefinitionId;
        public int scoreDelta;
        public double createdWorldTime;
        public double expirationWorldTime = -1d;
        public bool active = true;
        public long revision = 1L;

        public SocialEmotionDecisionModifierData Clone() => new SocialEmotionDecisionModifierData
        {
            modifierId = modifierId ?? string.Empty,
            sourceEpisodeId = sourceEpisodeId ?? string.Empty,
            actorPersonId = actorPersonId ?? string.Empty,
            targetPersonId = targetPersonId ?? string.Empty,
            intentionDefinitionId = intentionDefinitionId ?? string.Empty,
            interactionDefinitionId = interactionDefinitionId ?? string.Empty,
            scoreDelta = scoreDelta,
            createdWorldTime = createdWorldTime,
            expirationWorldTime = expirationWorldTime,
            active = active,
            revision = revision
        };

        public bool IsActiveAt(double worldTime) => active && (expirationWorldTime < 0d || worldTime <= expirationWorldTime);
    }

    [Serializable]
    public sealed class SocialEmotionProcessedTransactionData
    {
        public string transactionId;
        public string episodeId;
        public SocialEmotionStatus status;

        public SocialEmotionProcessedTransactionData Clone() => new SocialEmotionProcessedTransactionData { transactionId = transactionId ?? string.Empty, episodeId = episodeId ?? string.Empty, status = status };
    }

    [Serializable]
    public sealed class SocialEmotionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long episodeSequence;
        public List<SocialEmotionEpisodeData> episodes = new List<SocialEmotionEpisodeData>();
        public List<SocialMoodStateData> moods = new List<SocialMoodStateData>();
        public List<SocialEmotionDecisionModifierData> decisionModifiers = new List<SocialEmotionDecisionModifierData>();
        public List<SocialEmotionProcessedTransactionData> processedTransactions = new List<SocialEmotionProcessedTransactionData>();

        public SocialEmotionRuntimeSaveData Clone() => new SocialEmotionRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            revision = revision,
            episodeSequence = episodeSequence,
            episodes = episodes == null ? new List<SocialEmotionEpisodeData>() : episodes.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            moods = moods == null ? new List<SocialMoodStateData>() : moods.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            decisionModifiers = decisionModifiers == null ? new List<SocialEmotionDecisionModifierData>() : decisionModifiers.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            processedTransactions = processedTransactions == null ? new List<SocialEmotionProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
        };
    }

    public sealed class SocialEmotionTriggerRequest
    {
        public string TransactionId { get; set; }
        public string EpisodeId { get; set; }
        public string PersonId { get; set; }
        public string EmotionDefinitionId { get; set; }
        public string AppraisalRuleId { get; set; }
        public string TargetPersonId { get; set; }
        public string SubjectId { get; set; }
        public SocialEmotionCauseReferenceData Cause { get; set; } = new SocialEmotionCauseReferenceData();
        public int? IntensityOverride { get; set; }
        public double? DurationOverrideSeconds { get; set; }
        public double WorldTime { get; set; }
        public bool Suppressed { get; set; }
        public bool Concealed { get; set; }
        public bool Preview { get; set; }

        public SocialEmotionTriggerRequest Clone() => new SocialEmotionTriggerRequest
        {
            TransactionId = TransactionId,
            EpisodeId = EpisodeId,
            PersonId = PersonId,
            EmotionDefinitionId = EmotionDefinitionId,
            AppraisalRuleId = AppraisalRuleId,
            TargetPersonId = TargetPersonId,
            SubjectId = SubjectId,
            Cause = Cause?.Clone() ?? new SocialEmotionCauseReferenceData(),
            IntensityOverride = IntensityOverride,
            DurationOverrideSeconds = DurationOverrideSeconds,
            WorldTime = WorldTime,
            Suppressed = Suppressed,
            Concealed = Concealed,
            Preview = Preview
        };
    }

    public sealed class SocialEmotionEpisodeSnapshot
    {
        public SocialEmotionEpisodeSnapshot(SocialEmotionEpisodeData data, int currentIntensity)
        {
            EpisodeId = data?.episodeId ?? string.Empty;
            PersonId = data?.personId ?? string.Empty;
            EmotionDefinitionId = data?.emotionDefinitionId ?? string.Empty;
            AppraisalRuleId = data?.appraisalRuleId ?? string.Empty;
            TargetPersonId = data?.targetPersonId ?? string.Empty;
            SubjectId = data?.subjectId ?? string.Empty;
            Cause = data?.cause?.Clone() ?? new SocialEmotionCauseReferenceData();
            BaseIntensity = data?.baseIntensity ?? 0;
            CurrentIntensity = currentIntensity;
            ReinforcementCount = data?.reinforcementCount ?? 0;
            StartWorldTime = data?.startWorldTime ?? 0d;
            ExpirationWorldTime = data?.expirationWorldTime ?? -1d;
            Visibility = data?.visibility ?? SocialEmotionVisibility.Internal;
            Suppressed = data?.suppressed ?? false;
            Concealed = data?.concealed ?? false;
            Active = data?.active ?? false;
            DecisionModifierId = data?.decisionModifierId ?? string.Empty;
            Revision = data?.revision ?? 0L;
        }

        public string EpisodeId { get; }
        public string PersonId { get; }
        public string EmotionDefinitionId { get; }
        public string AppraisalRuleId { get; }
        public string TargetPersonId { get; }
        public string SubjectId { get; }
        public SocialEmotionCauseReferenceData Cause { get; }
        public int BaseIntensity { get; }
        public int CurrentIntensity { get; }
        public int ReinforcementCount { get; }
        public double StartWorldTime { get; }
        public double ExpirationWorldTime { get; }
        public SocialEmotionVisibility Visibility { get; }
        public bool Suppressed { get; }
        public bool Concealed { get; }
        public bool Active { get; }
        public string DecisionModifierId { get; }
        public long Revision { get; }
    }

    public sealed class SocialMoodSnapshot
    {
        public SocialMoodSnapshot(SocialMoodStateData data)
        {
            PersonId = data?.personId ?? string.Empty;
            MoodDimensionId = data?.moodDimensionId ?? string.Empty;
            Value = data?.value ?? 0;
            LastEvaluatedWorldTime = data?.lastEvaluatedWorldTime ?? 0d;
            SourceEpisodeIds = (data?.sourceEpisodeIds ?? Array.Empty<string>()).ToArray();
            Revision = data?.revision ?? 0L;
        }

        public string PersonId { get; }
        public string MoodDimensionId { get; }
        public int Value { get; }
        public double LastEvaluatedWorldTime { get; }
        public IReadOnlyList<string> SourceEpisodeIds { get; }
        public long Revision { get; }
    }

    public sealed class SocialEmotionProjection
    {
        public SocialEmotionProjection(SocialEmotionProjectionAccess access, SocialEmotionEpisodeSnapshot snapshot, string reason)
        {
            Access = access;
            Snapshot = snapshot;
            Reason = reason ?? string.Empty;
        }

        public SocialEmotionProjectionAccess Access { get; }
        public SocialEmotionEpisodeSnapshot Snapshot { get; }
        public string Reason { get; }
        public bool Succeeded => Access == SocialEmotionProjectionAccess.Full || Access == SocialEmotionProjectionAccess.Redacted;
    }

    public sealed class SocialEmotionResult
    {
        public SocialEmotionResult(bool succeeded, SocialEmotionStatus status, string message, SocialEmotionEpisodeSnapshot episode, SocialMoodSnapshot mood, bool preview, bool duplicate, long revisionBefore, long revisionAfter, IReadOnlyList<string> diagnostics = null)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            Episode = episode;
            Mood = mood;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).ToArray();
        }

        public bool Succeeded { get; }
        public SocialEmotionStatus Status { get; }
        public string Message { get; }
        public SocialEmotionEpisodeSnapshot Episode { get; }
        public SocialMoodSnapshot Mood { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static SocialEmotionResult Failure(SocialEmotionStatus status, string message, long before, IReadOnlyList<string> diagnostics = null)
        {
            return new SocialEmotionResult(false, status, message, null, null, false, false, before, before, diagnostics);
        }
    }
}
