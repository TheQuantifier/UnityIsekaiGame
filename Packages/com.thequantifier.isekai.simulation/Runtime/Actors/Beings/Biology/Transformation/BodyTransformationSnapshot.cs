using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    public sealed class BodyTransformationSnapshot
    {
        public BodyTransformationSnapshot(
            string actorBodyId,
            string personId,
            TransformationReadinessState readiness,
            long transformationRevision,
            bool activeTemporaryTransformation,
            string activeMethodId,
            string activeTransactionId,
            string originalSpeciesId,
            string transformedSpeciesId,
            string targetBodyId,
            IReadOnlyList<string> processedTransactionIds,
            bool dirty,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            Readiness = readiness;
            TransformationRevision = transformationRevision;
            ActiveTemporaryTransformation = activeTemporaryTransformation;
            ActiveMethodId = activeMethodId ?? string.Empty;
            ActiveTransactionId = activeTransactionId ?? string.Empty;
            OriginalSpeciesId = originalSpeciesId ?? string.Empty;
            TransformedSpeciesId = transformedSpeciesId ?? string.Empty;
            TargetBodyId = targetBodyId ?? string.Empty;
            ProcessedTransactionIds = processedTransactionIds == null ? Array.Empty<string>() : processedTransactionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            IsDirty = dirty;
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public string PersonId { get; }
        public TransformationReadinessState Readiness { get; }
        public long TransformationRevision { get; }
        public bool ActiveTemporaryTransformation { get; }
        public string ActiveMethodId { get; }
        public string ActiveTransactionId { get; }
        public string OriginalSpeciesId { get; }
        public string TransformedSpeciesId { get; }
        public string TargetBodyId { get; }
        public IReadOnlyList<string> ProcessedTransactionIds { get; }
        public bool IsDirty { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
