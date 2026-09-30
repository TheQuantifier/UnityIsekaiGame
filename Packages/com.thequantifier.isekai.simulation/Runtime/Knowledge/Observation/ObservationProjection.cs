using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Knowledge.Observation
{
    public sealed class ObservableProjection
    {
        public ObservableProjection(
            string projectionId,
            ObservationTargetType targetType,
            KnowledgePropositionData proposition,
            KnowledgeVisibility visibility,
            int minimumQuality,
            int baseEvidenceStrength,
            SensoryChannel[] channels,
            bool mechanicallyRelevant = true,
            string privacyReason = "",
            string[] tags = null)
        {
            ProjectionId = projectionId ?? string.Empty;
            TargetType = targetType;
            Proposition = proposition?.Clone();
            Visibility = visibility;
            MinimumQuality = KnowledgeConfidence.Clamp(minimumQuality);
            BaseEvidenceStrength = KnowledgeConfidence.Clamp(baseEvidenceStrength);
            Channels = (channels ?? Array.Empty<SensoryChannel>()).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
            MechanicallyRelevant = mechanicallyRelevant;
            PrivacyReason = privacyReason ?? string.Empty;
            Tags = (tags ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public string ProjectionId { get; }
        public ObservationTargetType TargetType { get; }
        public KnowledgePropositionData Proposition { get; }
        public KnowledgeVisibility Visibility { get; }
        public int MinimumQuality { get; }
        public int BaseEvidenceStrength { get; }
        public IReadOnlyList<SensoryChannel> Channels { get; }
        public bool MechanicallyRelevant { get; }
        public string PrivacyReason { get; }
        public IReadOnlyList<string> Tags { get; }
    }

    public sealed class ExaminationProjection
    {
        public ExaminationProjection(string projectionId, ObservableProjection[] indicators, string[] exposedCategories)
        {
            ProjectionId = projectionId ?? string.Empty;
            Indicators = (indicators ?? Array.Empty<ObservableProjection>()).OrderBy(value => value.ProjectionId, StringComparer.Ordinal).ToArray();
            ExposedCategories = (exposedCategories ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public string ProjectionId { get; }
        public IReadOnlyList<ObservableProjection> Indicators { get; }
        public IReadOnlyList<string> ExposedCategories { get; }
    }

    public sealed class DiagnosticHypothesis
    {
        public DiagnosticHypothesis(string candidateId, string familyId, int confidence, string[] supportingProjectionIds, bool exactCandidate = false)
        {
            CandidateId = candidateId ?? string.Empty;
            FamilyId = familyId ?? string.Empty;
            Confidence = KnowledgeConfidence.Clamp(confidence);
            SupportingProjectionIds = (supportingProjectionIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            ExactCandidate = exactCandidate;
        }

        public string CandidateId { get; }
        public string FamilyId { get; }
        public int Confidence { get; }
        public IReadOnlyList<string> SupportingProjectionIds { get; }
        public bool ExactCandidate { get; }
    }

    public sealed class DiagnosticProjection
    {
        public DiagnosticProjection(string projectionId, DiagnosticHypothesis[] hypotheses)
        {
            ProjectionId = projectionId ?? string.Empty;
            Hypotheses = (hypotheses ?? Array.Empty<DiagnosticHypothesis>()).OrderByDescending(value => value.Confidence).ThenBy(value => value.CandidateId, StringComparer.Ordinal).ToArray();
        }

        public string ProjectionId { get; }
        public IReadOnlyList<DiagnosticHypothesis> Hypotheses { get; }
    }

    public sealed class ObservationResult
    {
        public ObservationResult(
            bool succeeded,
            ObservationOutcomeCode code,
            string message,
            ObservationContext context,
            string methodId,
            int quality,
            int evidenceStrength,
            bool preview,
            bool tracked,
            IdentificationResultState identificationState,
            DiagnosticResultState diagnosticState,
            KnowledgeOperationResult knowledgeResult,
            IReadOnlyList<DiagnosticHypothesis> hypotheses = null)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Context = context;
            MethodId = methodId ?? string.Empty;
            Quality = KnowledgeConfidence.Clamp(quality);
            EvidenceStrength = KnowledgeConfidence.Clamp(evidenceStrength);
            Preview = preview;
            Tracked = tracked;
            IdentificationState = identificationState;
            DiagnosticState = diagnosticState;
            KnowledgeResult = knowledgeResult;
            Hypotheses = (hypotheses ?? Array.Empty<DiagnosticHypothesis>()).OrderByDescending(value => value.Confidence).ThenBy(value => value.CandidateId, StringComparer.Ordinal).ToArray();
        }

        public bool Succeeded { get; }
        public ObservationOutcomeCode Code { get; }
        public string Message { get; }
        public ObservationContext Context { get; }
        public string MethodId { get; }
        public int Quality { get; }
        public int EvidenceStrength { get; }
        public bool Preview { get; }
        public bool Tracked { get; }
        public IdentificationResultState IdentificationState { get; }
        public DiagnosticResultState DiagnosticState { get; }
        public KnowledgeOperationResult KnowledgeResult { get; }
        public IReadOnlyList<DiagnosticHypothesis> Hypotheses { get; }
    }
}
