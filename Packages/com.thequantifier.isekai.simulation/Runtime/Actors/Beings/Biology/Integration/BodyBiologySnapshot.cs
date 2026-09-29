using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Transformation;

namespace UnityIsekaiGame.Beings.Biology.Integration
{
    public enum BodyBiologySnapshotDetail
    {
        Summary,
        Full
    }

    public sealed class BodyBiologyRevisionSet
    {
        public BodyBiologyRevisionSet(
            long bodyRevision,
            long anatomyRevision,
            long conditionRevision,
            long vitalRevision,
            long hazardRevision,
            long compatibilityRevision,
            long recoveryRevision,
            long biologicalConditionRevision,
            long transformationRevision)
        {
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            ConditionRevision = conditionRevision;
            VitalRevision = vitalRevision;
            HazardRevision = hazardRevision;
            CompatibilityRevision = compatibilityRevision;
            RecoveryRevision = recoveryRevision;
            BiologicalConditionRevision = biologicalConditionRevision;
            TransformationRevision = transformationRevision;
        }

        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public long ConditionRevision { get; }
        public long VitalRevision { get; }
        public long HazardRevision { get; }
        public long CompatibilityRevision { get; }
        public long RecoveryRevision { get; }
        public long BiologicalConditionRevision { get; }
        public long TransformationRevision { get; }

        public bool SameAs(BodyBiologyRevisionSet other)
        {
            return other != null
                && BodyRevision == other.BodyRevision
                && AnatomyRevision == other.AnatomyRevision
                && ConditionRevision == other.ConditionRevision
                && VitalRevision == other.VitalRevision
                && HazardRevision == other.HazardRevision
                && CompatibilityRevision == other.CompatibilityRevision
                && RecoveryRevision == other.RecoveryRevision
                && BiologicalConditionRevision == other.BiologicalConditionRevision
                && TransformationRevision == other.TransformationRevision;
        }

        public override string ToString()
        {
            return $"Body={BodyRevision} Anatomy={AnatomyRevision} Condition={ConditionRevision} Vital={VitalRevision} Hazard={HazardRevision} Compatibility={CompatibilityRevision} Recovery={RecoveryRevision} BiologicalConditions={BiologicalConditionRevision} Transformation={TransformationRevision}";
        }
    }

    public sealed class BodyBiologySnapshot
    {
        public BodyBiologySnapshot(
            string actorBodyId,
            string personId,
            string speciesId,
            string classificationId,
            string bodyFormId,
            BodyReadinessState readiness,
            BodySnapshot body,
            BiologicalConditionRuntimeSnapshot biologicalConditions,
            BodyTransformationSnapshot transformation,
            BodyBiologyRevisionSet revisions,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            BiologicalClassificationId = classificationId ?? string.Empty;
            BodyFormId = bodyFormId ?? string.Empty;
            Readiness = readiness;
            Body = body;
            BiologicalConditions = biologicalConditions;
            Transformation = transformation;
            Revisions = revisions ?? new BodyBiologyRevisionSet(0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L);
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic)).ToArray();
        }

        public string ActorBodyId { get; }
        public string PersonId { get; }
        public string SpeciesId { get; }
        public string BiologicalClassificationId { get; }
        public string BodyFormId { get; }
        public BodyReadinessState Readiness { get; }
        public BodySnapshot Body { get; }
        public BiologicalConditionRuntimeSnapshot BiologicalConditions { get; }
        public BodyTransformationSnapshot Transformation { get; }
        public BodyBiologyRevisionSet Revisions { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool Ready => Readiness == BodyReadinessState.Ready && Coherent;
    }
}
