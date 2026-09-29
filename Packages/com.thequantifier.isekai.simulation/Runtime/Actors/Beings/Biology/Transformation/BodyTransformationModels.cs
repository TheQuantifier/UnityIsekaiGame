using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology.Compatibility;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    public sealed class BodyTransformationRequest
    {
        public BodyTransformationRequest(
            string methodDefinitionId,
            string transactionId,
            string personId,
            string sourceActorId,
            string sourceActorBodyId,
            string targetActorBodyId = "",
            string targetSpeciesId = "",
            string targetBodyFormId = "",
            string targetAnatomyNodeId = "",
            string replacementDefinitionId = "",
            string sourceId = "",
            string authorityContext = "",
            string reason = "",
            bool preview = false,
            float requestedDurationSeconds = 0f,
            long expectedBodyRevision = 0L,
            long expectedAnatomyRevision = 0L,
            long expectedCompatibilityRevision = 0L,
            IReadOnlyList<string> tags = null)
        {
            MethodDefinitionId = methodDefinitionId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceActorBodyId = sourceActorBodyId ?? string.Empty;
            TargetActorBodyId = targetActorBodyId ?? string.Empty;
            TargetSpeciesId = targetSpeciesId ?? string.Empty;
            TargetBodyFormId = targetBodyFormId ?? string.Empty;
            TargetAnatomyNodeId = targetAnatomyNodeId ?? string.Empty;
            ReplacementDefinitionId = replacementDefinitionId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            AuthorityContext = authorityContext ?? string.Empty;
            Reason = reason ?? string.Empty;
            Preview = preview;
            RequestedDurationSeconds = Math.Max(0f, requestedDurationSeconds);
            ExpectedBodyRevision = Math.Max(0L, expectedBodyRevision);
            ExpectedAnatomyRevision = Math.Max(0L, expectedAnatomyRevision);
            ExpectedCompatibilityRevision = Math.Max(0L, expectedCompatibilityRevision);
            Tags = tags == null ? Array.Empty<string>() : tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
        }

        public string MethodDefinitionId { get; }
        public string TransactionId { get; }
        public string PersonId { get; }
        public string SourceActorId { get; }
        public string SourceActorBodyId { get; }
        public string TargetActorBodyId { get; }
        public string TargetSpeciesId { get; }
        public string TargetBodyFormId { get; }
        public string TargetAnatomyNodeId { get; }
        public string ReplacementDefinitionId { get; }
        public string SourceId { get; }
        public string AuthorityContext { get; }
        public string Reason { get; }
        public bool Preview { get; }
        public float RequestedDurationSeconds { get; }
        public long ExpectedBodyRevision { get; }
        public long ExpectedAnatomyRevision { get; }
        public long ExpectedCompatibilityRevision { get; }
        public IReadOnlyList<string> Tags { get; }

        public BodyTransformationRequest AsPreview()
        {
            return new BodyTransformationRequest(MethodDefinitionId, TransactionId, PersonId, SourceActorId, SourceActorBodyId, TargetActorBodyId, TargetSpeciesId, TargetBodyFormId, TargetAnatomyNodeId, ReplacementDefinitionId, SourceId, AuthorityContext, Reason, preview: true, RequestedDurationSeconds, ExpectedBodyRevision, ExpectedAnatomyRevision, ExpectedCompatibilityRevision, Tags);
        }
    }

    public sealed class BodyTransformationDecision
    {
        public BodyTransformationDecision(string stateName, TransformationStateOwnership ownership, TransformationReconciliationPolicy policy, bool transfers, string targetId, string message)
        {
            StateName = stateName ?? string.Empty;
            Ownership = ownership;
            Policy = policy;
            Transfers = transfers;
            TargetId = targetId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string StateName { get; }
        public TransformationStateOwnership Ownership { get; }
        public TransformationReconciliationPolicy Policy { get; }
        public bool Transfers { get; }
        public string TargetId { get; }
        public string Message { get; }
    }

    public sealed class BodyTransformationPlan
    {
        public BodyTransformationPlan(
            BodyTransformationRequest request,
            TransformationMethodDefinition method,
            BodySnapshot sourceBody,
            BodySnapshot targetBody,
            SpeciesDefinition targetSpecies,
            BodyFormDefinition targetBodyForm,
            BiologicalInteractionEvaluationResult compatibility,
            TransformationPlanFlags flags,
            IReadOnlyList<BodyTransformationDecision> decisions,
            bool eligible,
            TransformationResultCode code,
            string message)
        {
            Request = request;
            Method = method;
            SourceBody = sourceBody;
            TargetBody = targetBody;
            TargetSpecies = targetSpecies;
            TargetBodyForm = targetBodyForm;
            Compatibility = compatibility;
            Flags = flags;
            Decisions = decisions == null ? Array.Empty<BodyTransformationDecision>() : decisions.ToArray();
            Eligible = eligible;
            Code = code;
            Message = message ?? string.Empty;
        }

        public BodyTransformationRequest Request { get; }
        public TransformationMethodDefinition Method { get; }
        public BodySnapshot SourceBody { get; }
        public BodySnapshot TargetBody { get; }
        public SpeciesDefinition TargetSpecies { get; }
        public BodyFormDefinition TargetBodyForm { get; }
        public BiologicalInteractionEvaluationResult Compatibility { get; }
        public TransformationPlanFlags Flags { get; }
        public IReadOnlyList<BodyTransformationDecision> Decisions { get; }
        public bool Eligible { get; }
        public TransformationResultCode Code { get; }
        public string Message { get; }
        public bool IsTemporary => Method != null && Method.Temporary;
        public bool ChangesSpecies => (Flags & TransformationPlanFlags.SpeciesChange) != 0;
        public bool ChangesBodyForm => (Flags & TransformationPlanFlags.BodyFormChange) != 0;
    }

    public sealed class BodyTransformationResult
    {
        private BodyTransformationResult(
            bool succeeded,
            TransformationResultCode code,
            string message,
            bool preview,
            bool duplicate,
            BodyTransformationPlan plan,
            BodyTransformationSnapshot snapshot,
            BodyOperationResult bodyOperation)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Plan = plan;
            Snapshot = snapshot;
            BodyOperation = bodyOperation;
        }

        public bool Succeeded { get; }
        public TransformationResultCode Code { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BodyTransformationPlan Plan { get; }
        public BodyTransformationSnapshot Snapshot { get; }
        public BodyOperationResult BodyOperation { get; }

        public static BodyTransformationResult Success(string message, BodyTransformationPlan plan, BodyTransformationSnapshot snapshot, BodyOperationResult bodyOperation = null, bool preview = false, bool duplicate = false)
        {
            return new BodyTransformationResult(true, duplicate ? TransformationResultCode.Duplicate : preview ? TransformationResultCode.Preview : TransformationResultCode.Success, message, preview, duplicate, plan, snapshot, bodyOperation);
        }

        public static BodyTransformationResult Failure(TransformationResultCode code, string message, BodyTransformationPlan plan = null, BodyTransformationSnapshot snapshot = null, BodyOperationResult bodyOperation = null, bool preview = false)
        {
            return new BodyTransformationResult(false, code, message, preview, false, plan, snapshot, bodyOperation);
        }
    }
}
