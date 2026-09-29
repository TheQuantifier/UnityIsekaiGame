using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Social.Influence
{
    [Serializable]
    public sealed class SocialInfluenceSubjectData
    {
        public SocialInfluenceSubjectKind kind;
        public string subjectId;
        public string parentSubjectId;
        public string ownerPersonId;
        public string[] tags = Array.Empty<string>();

        public SocialInfluenceSubjectData Clone() => new SocialInfluenceSubjectData
        {
            kind = kind,
            subjectId = subjectId ?? string.Empty,
            parentSubjectId = parentSubjectId ?? string.Empty,
            ownerPersonId = ownerPersonId ?? string.Empty,
            tags = Clean(tags)
        };

        public SocialInteractionSubjectData ToInteractionSubject() => new SocialInteractionSubjectData
        {
            kind = kind switch
            {
                SocialInfluenceSubjectKind.Person => SocialInteractionSubjectKind.Person,
                SocialInfluenceSubjectKind.HistoricalEvent => SocialInteractionSubjectKind.HistoricalEvent,
                SocialInfluenceSubjectKind.RelationshipRecord => SocialInteractionSubjectKind.Relationship,
                SocialInfluenceSubjectKind.Rumor => SocialInteractionSubjectKind.Rumor,
                SocialInfluenceSubjectKind.Promise => SocialInteractionSubjectKind.Promise,
                SocialInfluenceSubjectKind.Claim => SocialInteractionSubjectKind.Claim,
                SocialInfluenceSubjectKind.Item => SocialInteractionSubjectKind.Item,
                SocialInfluenceSubjectKind.Place => SocialInteractionSubjectKind.Place,
                _ => SocialInteractionSubjectKind.Custom
            },
            subjectId = subjectId ?? string.Empty,
            parentSubjectId = parentSubjectId ?? string.Empty,
            ownerPersonId = ownerPersonId ?? string.Empty,
            tags = Clean((tags ?? Array.Empty<string>()).Concat(new[] { "social-influence", kind.ToString() }))
        };

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialInfluenceEvidenceReferenceData
    {
        public string evidenceId;
        public string sourceId;
        public int strength;
        public int credibility;
        public bool fabricated;
        public string[] tags = Array.Empty<string>();

        public SocialInfluenceEvidenceReferenceData Clone() => new SocialInfluenceEvidenceReferenceData
        {
            evidenceId = evidenceId ?? string.Empty,
            sourceId = sourceId ?? string.Empty,
            strength = strength,
            credibility = credibility,
            fabricated = fabricated,
            tags = Clean(tags)
        };

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialInfluenceArgumentData
    {
        public string argumentId;
        public string premise;
        public string conclusion;
        public int clarity;
        public int emotionalIntensity;
        public bool coercive;

        public SocialInfluenceArgumentData Clone() => new SocialInfluenceArgumentData
        {
            argumentId = argumentId ?? string.Empty,
            premise = premise ?? string.Empty,
            conclusion = conclusion ?? string.Empty,
            clarity = clarity,
            emotionalIntensity = emotionalIntensity,
            coercive = coercive
        };
    }

    [Serializable]
    public sealed class SocialInfluenceDecisionModifierData
    {
        public string modifierId;
        public string sourceAttemptId;
        public string targetPersonId;
        public string actorPersonId;
        public string intentionDefinitionId;
        public string interactionDefinitionId;
        public string subjectId;
        public int scoreDelta;
        public double createdWorldTime;
        public double expirationWorldTime = -1d;
        public bool active = true;
        public long revision = 1L;

        public SocialInfluenceDecisionModifierData Clone() => new SocialInfluenceDecisionModifierData
        {
            modifierId = modifierId ?? string.Empty,
            sourceAttemptId = sourceAttemptId ?? string.Empty,
            targetPersonId = targetPersonId ?? string.Empty,
            actorPersonId = actorPersonId ?? string.Empty,
            intentionDefinitionId = intentionDefinitionId ?? string.Empty,
            interactionDefinitionId = interactionDefinitionId ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            scoreDelta = scoreDelta,
            createdWorldTime = createdWorldTime,
            expirationWorldTime = expirationWorldTime,
            active = active,
            revision = revision
        };

        public bool IsActiveAt(double worldTime) => active && (expirationWorldTime < 0d || worldTime <= expirationWorldTime);
    }

    [Serializable]
    public sealed class SocialInfluenceAttemptRecordData
    {
        public string attemptId;
        public string transactionId;
        public string methodDefinitionId;
        public string speakerPersonId;
        public string targetPersonId;
        public string witnessPersonIdsCsv;
        public SocialInfluenceIntent intent;
        public SocialInfluenceSubjectData subject = new SocialInfluenceSubjectData();
        public KnowledgePropositionData claim;
        public SocialInfluenceTruthStatus truthStatus;
        public SocialInfluenceSpeakerBeliefState speakerBeliefState;
        public SocialInfluenceHonestyClassification honesty;
        public SocialInfluenceDeceptionMode deceptionMode;
        public SocialInfluenceBeliefOutcome beliefOutcome;
        public SocialInfluenceComplianceOutcome complianceOutcome;
        public SocialInfluenceDetectionOutcome detectionOutcome;
        public SocialInfluenceMarginClass marginClass;
        public SocialInfluenceVisibility visibility;
        public int influenceScore;
        public int resistanceScore;
        public int margin;
        public int deterministicRoll;
        public string knowledgeEvidenceId;
        public string knowledgeBeliefId;
        public string interactionRecordId;
        public string decisionModifierId;
        public double worldTime;
        public string deterministicSeed;
        public string[] diagnostics = Array.Empty<string>();
        public long revision = 1L;

        public SocialInfluenceAttemptRecordData Clone() => new SocialInfluenceAttemptRecordData
        {
            attemptId = attemptId ?? string.Empty,
            transactionId = transactionId ?? string.Empty,
            methodDefinitionId = methodDefinitionId ?? string.Empty,
            speakerPersonId = speakerPersonId ?? string.Empty,
            targetPersonId = targetPersonId ?? string.Empty,
            witnessPersonIdsCsv = witnessPersonIdsCsv ?? string.Empty,
            intent = intent,
            subject = subject?.Clone() ?? new SocialInfluenceSubjectData(),
            claim = claim?.Clone(),
            truthStatus = truthStatus,
            speakerBeliefState = speakerBeliefState,
            honesty = honesty,
            deceptionMode = deceptionMode,
            beliefOutcome = beliefOutcome,
            complianceOutcome = complianceOutcome,
            detectionOutcome = detectionOutcome,
            marginClass = marginClass,
            visibility = visibility,
            influenceScore = influenceScore,
            resistanceScore = resistanceScore,
            margin = margin,
            deterministicRoll = deterministicRoll,
            knowledgeEvidenceId = knowledgeEvidenceId ?? string.Empty,
            knowledgeBeliefId = knowledgeBeliefId ?? string.Empty,
            interactionRecordId = interactionRecordId ?? string.Empty,
            decisionModifierId = decisionModifierId ?? string.Empty,
            worldTime = worldTime,
            deterministicSeed = deterministicSeed ?? string.Empty,
            diagnostics = Clean(diagnostics),
            revision = revision
        };

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class SocialInfluenceProcessedTransactionData
    {
        public string transactionId;
        public string attemptId;
        public SocialInfluenceStatus status;

        public SocialInfluenceProcessedTransactionData Clone() => new SocialInfluenceProcessedTransactionData { transactionId = transactionId ?? string.Empty, attemptId = attemptId ?? string.Empty, status = status };
    }

    [Serializable]
    public sealed class SocialInfluenceCooldownData
    {
        public string cooldownKey;
        public double lastWorldTime;
        public string sourceAttemptId;

        public SocialInfluenceCooldownData Clone() => new SocialInfluenceCooldownData { cooldownKey = cooldownKey ?? string.Empty, lastWorldTime = lastWorldTime, sourceAttemptId = sourceAttemptId ?? string.Empty };
    }

    [Serializable]
    public sealed class SocialInfluenceRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long attemptSequence;
        public List<SocialInfluenceAttemptRecordData> attempts = new List<SocialInfluenceAttemptRecordData>();
        public List<SocialInfluenceProcessedTransactionData> processedTransactions = new List<SocialInfluenceProcessedTransactionData>();
        public List<SocialInfluenceCooldownData> cooldowns = new List<SocialInfluenceCooldownData>();
        public List<SocialInfluenceDecisionModifierData> decisionModifiers = new List<SocialInfluenceDecisionModifierData>();

        public SocialInfluenceRuntimeSaveData Clone() => new SocialInfluenceRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            revision = revision,
            attemptSequence = attemptSequence,
            attempts = attempts == null ? new List<SocialInfluenceAttemptRecordData>() : attempts.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            processedTransactions = processedTransactions == null ? new List<SocialInfluenceProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            cooldowns = cooldowns == null ? new List<SocialInfluenceCooldownData>() : cooldowns.Select(item => item?.Clone()).Where(item => item != null).ToList(),
            decisionModifiers = decisionModifiers == null ? new List<SocialInfluenceDecisionModifierData>() : decisionModifiers.Select(item => item?.Clone()).Where(item => item != null).ToList()
        };
    }

    public sealed class SocialInfluenceRequest
    {
        public string TransactionId { get; set; }
        public string AttemptId { get; set; }
        public string MethodDefinitionId { get; set; }
        public string SpeakerPersonId { get; set; }
        public string TargetPersonId { get; set; }
        public IReadOnlyList<string> WitnessPersonIds { get; set; } = Array.Empty<string>();
        public SocialInfluenceIntent Intent { get; set; }
        public SocialInfluenceSubjectData Subject { get; set; } = new SocialInfluenceSubjectData();
        public KnowledgePropositionData Claim { get; set; }
        public IReadOnlyList<SocialInfluenceEvidenceReferenceData> EvidencePackage { get; set; } = Array.Empty<SocialInfluenceEvidenceReferenceData>();
        public IReadOnlyList<SocialInfluenceArgumentData> Arguments { get; set; } = Array.Empty<SocialInfluenceArgumentData>();
        public SocialInfluenceTruthStatus TruthStatus { get; set; } = SocialInfluenceTruthStatus.Unknown;
        public SocialInfluenceSpeakerBeliefState SpeakerBeliefState { get; set; } = SocialInfluenceSpeakerBeliefState.Unknown;
        public SocialInfluenceDeceptionMode DeceptionMode { get; set; } = SocialInfluenceDeceptionMode.NoDeception;
        public SocialInfluenceVisibility Visibility { get; set; } = SocialInfluenceVisibility.Private;
        public int SpeakerResolve { get; set; } = 500;
        public int TargetResistance { get; set; } = 500;
        public int EvidenceStrength { get; set; }
        public int RelationshipModifier { get; set; }
        public int ReputationModifier { get; set; }
        public int Difficulty { get; set; }
        public double WorldTime { get; set; }
        public string DeterministicSeed { get; set; }
        public string IntentionDefinitionId { get; set; }
        public string InteractionDefinitionId { get; set; }
        public bool CommitBeliefEvidence { get; set; } = true;
        public bool CommitDecisionModifier { get; set; } = true;
        public bool PlayerTargetRequiresExternalConsent { get; set; }
        public bool PlayerConsentGranted { get; set; }
        public bool Preview { get; set; }
    }

    public sealed class SocialInfluenceAttemptSnapshot
    {
        private readonly SocialInfluenceAttemptRecordData data;
        public SocialInfluenceAttemptSnapshot(SocialInfluenceAttemptRecordData data) => this.data = data?.Clone() ?? new SocialInfluenceAttemptRecordData();
        public SocialInfluenceAttemptRecordData Data => data.Clone();
        public string AttemptId => data.attemptId ?? string.Empty;
        public string MethodDefinitionId => data.methodDefinitionId ?? string.Empty;
        public string SpeakerPersonId => data.speakerPersonId ?? string.Empty;
        public string TargetPersonId => data.targetPersonId ?? string.Empty;
        public SocialInfluenceBeliefOutcome BeliefOutcome => data.beliefOutcome;
        public SocialInfluenceComplianceOutcome ComplianceOutcome => data.complianceOutcome;
        public SocialInfluenceDetectionOutcome DetectionOutcome => data.detectionOutcome;
        public int Margin => data.margin;
        public long Revision => data.revision;
    }

    public sealed class SocialInfluenceResult
    {
        private readonly string[] diagnostics;

        public SocialInfluenceResult(bool succeeded, SocialInfluenceStatus status, string message, SocialInfluenceAttemptRecordData attempt, SocialInfluenceDecisionModifierData modifier, KnowledgeOperationResult knowledge, SocialInteractionResult interaction, bool preview, bool duplicate, long beforeRevision, long afterRevision, IEnumerable<string> diagnosticMessages)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            Attempt = attempt?.Clone();
            DecisionModifier = modifier?.Clone();
            KnowledgeResult = knowledge;
            InteractionResult = interaction;
            Preview = preview;
            Duplicate = duplicate;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            diagnostics = Clean(diagnosticMessages);
        }

        public bool Succeeded { get; }
        public SocialInfluenceStatus Status { get; }
        public string Message { get; }
        public SocialInfluenceAttemptRecordData Attempt { get; }
        public SocialInfluenceDecisionModifierData DecisionModifier { get; }
        public KnowledgeOperationResult KnowledgeResult { get; }
        public SocialInteractionResult InteractionResult { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public IReadOnlyList<string> Diagnostics => diagnostics.ToArray();

        public static SocialInfluenceResult Failure(SocialInfluenceStatus status, string message, string attemptId, long revision, IEnumerable<string> diagnostics = null)
        {
            SocialInfluenceAttemptRecordData attempt = string.IsNullOrWhiteSpace(attemptId) ? null : new SocialInfluenceAttemptRecordData { attemptId = attemptId, diagnostics = Clean(diagnostics) };
            return new SocialInfluenceResult(false, status, message, attempt, null, null, null, false, false, revision, revision, diagnostics);
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
