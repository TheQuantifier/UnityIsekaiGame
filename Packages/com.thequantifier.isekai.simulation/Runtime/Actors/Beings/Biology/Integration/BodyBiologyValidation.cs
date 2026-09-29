using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Transformation;

namespace UnityIsekaiGame.Beings.Biology.Integration
{
    public enum BodyBiologyValidationCode
    {
        Valid,
        MissingBody,
        RuntimeNotReady,
        MissingActorBody,
        MissingPerson,
        MissingSpecies,
        IncoherentSnapshot,
        RevisionMismatch,
        OwnershipMismatch
    }

    public sealed class BodyBiologyValidationResult
    {
        private BodyBiologyValidationResult(bool succeeded, BodyBiologyValidationCode code, string message, BodyBiologySnapshot snapshot, IReadOnlyList<string> diagnostics)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic)).ToArray();
        }

        public bool Succeeded { get; }
        public BodyBiologyValidationCode Code { get; }
        public string Message { get; }
        public BodyBiologySnapshot Snapshot { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static BodyBiologyValidationResult Success(BodyBiologySnapshot snapshot, IReadOnlyList<string> diagnostics = null)
        {
            return new BodyBiologyValidationResult(true, BodyBiologyValidationCode.Valid, "Body biology integration is coherent.", snapshot, diagnostics);
        }

        public static BodyBiologyValidationResult Failure(BodyBiologyValidationCode code, string message, BodyBiologySnapshot snapshot = null, IReadOnlyList<string> diagnostics = null)
        {
            return new BodyBiologyValidationResult(false, code, message, snapshot, diagnostics);
        }
    }

    public static class BodyBiologyValidator
    {
        public static BodyBiologyValidationResult Validate(BodyBiologySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return BodyBiologyValidationResult.Failure(BodyBiologyValidationCode.MissingBody, "Body biology snapshot is missing.");
            }

            List<string> diagnostics = new List<string>();
            if (string.IsNullOrWhiteSpace(snapshot.ActorBodyId))
            {
                diagnostics.Add("Actor/body ID is missing.");
            }

            if (string.IsNullOrWhiteSpace(snapshot.PersonId))
            {
                diagnostics.Add("Person ID is missing.");
            }

            if (string.IsNullOrWhiteSpace(snapshot.SpeciesId))
            {
                diagnostics.Add("Species definition ID is missing.");
            }

            if (snapshot.Readiness != BodyReadinessState.Ready)
            {
                diagnostics.Add($"Body readiness is {snapshot.Readiness}, expected Ready.");
            }

            ValidateBodySnapshot(snapshot, diagnostics);
            ValidateBiologicalConditions(snapshot, diagnostics);
            ValidateTransformation(snapshot, diagnostics);

            if (!snapshot.Coherent)
            {
                diagnostics.AddRange(snapshot.Diagnostics);
            }

            if (diagnostics.Count == 0)
            {
                return BodyBiologyValidationResult.Success(snapshot);
            }

            BodyBiologyValidationCode code = diagnostics.Any(diagnostic => diagnostic.IndexOf("readiness", StringComparison.OrdinalIgnoreCase) >= 0)
                ? BodyBiologyValidationCode.RuntimeNotReady
                : diagnostics.Any(diagnostic => diagnostic.IndexOf("revision", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? BodyBiologyValidationCode.RevisionMismatch
                    : diagnostics.Any(diagnostic => diagnostic.IndexOf("Actor/body ID", StringComparison.OrdinalIgnoreCase) >= 0)
                        ? BodyBiologyValidationCode.MissingActorBody
                        : diagnostics.Any(diagnostic => diagnostic.IndexOf("Person ID", StringComparison.OrdinalIgnoreCase) >= 0)
                            ? BodyBiologyValidationCode.MissingPerson
                            : diagnostics.Any(diagnostic => diagnostic.IndexOf("Species", StringComparison.OrdinalIgnoreCase) >= 0)
                                ? BodyBiologyValidationCode.MissingSpecies
                                : BodyBiologyValidationCode.IncoherentSnapshot;

            return BodyBiologyValidationResult.Failure(code, string.Join(" ", diagnostics), snapshot, diagnostics);
        }

        private static void ValidateBodySnapshot(BodyBiologySnapshot snapshot, List<string> diagnostics)
        {
            BodySnapshot body = snapshot.Body;
            if (body == null)
            {
                diagnostics.Add("Body snapshot is missing.");
                return;
            }

            if (!string.Equals(snapshot.ActorBodyId, body.ActorBodyId, StringComparison.Ordinal))
            {
                diagnostics.Add($"Body snapshot actor/body '{body.ActorBodyId}' does not match aggregate '{snapshot.ActorBodyId}'.");
            }

            if (!string.Equals(snapshot.PersonId, body.PersonId, StringComparison.Ordinal))
            {
                diagnostics.Add($"Body snapshot person '{body.PersonId}' does not match aggregate '{snapshot.PersonId}'.");
            }

            if (!body.Coherent)
            {
                diagnostics.AddRange(body.Diagnostics);
            }

            if (body.Anatomy == null || body.Anatomy.BodyRevision != body.BodyRevision)
            {
                diagnostics.Add("Anatomy snapshot body revision does not match body revision.");
            }

            if (body.Condition == null || body.Condition.BodyRevision != body.BodyRevision)
            {
                diagnostics.Add("Body condition snapshot body revision does not match body revision.");
            }

            if (body.VitalProcesses == null || body.VitalProcesses.BodyRevision != body.BodyRevision)
            {
                diagnostics.Add("Vital process snapshot body revision does not match body revision.");
            }

            if (body.BiologicalHazards == null || body.BiologicalHazards.BodyRevision != body.BodyRevision)
            {
                diagnostics.Add("Biological hazard snapshot body revision does not match body revision.");
            }

            if (body.BiologicalCompatibility == null || body.BiologicalCompatibility.BodyRevision != body.BodyRevision)
            {
                diagnostics.Add("Biological compatibility snapshot body revision does not match body revision.");
            }

            if (body.BiologicalRecovery == null || body.BiologicalRecovery.BodyRevision != body.BodyRevision)
            {
                diagnostics.Add("Biological recovery snapshot body revision does not match body revision.");
            }
        }

        private static void ValidateBiologicalConditions(BodyBiologySnapshot snapshot, List<string> diagnostics)
        {
            BiologicalConditionRuntimeSnapshot conditions = snapshot.BiologicalConditions;
            if (conditions == null)
            {
                diagnostics.Add("Biological Condition snapshot is missing.");
                return;
            }

            if (!string.Equals(snapshot.ActorBodyId, conditions.ActorBodyId, StringComparison.Ordinal))
            {
                diagnostics.Add($"Biological Condition actor/body '{conditions.ActorBodyId}' does not match aggregate '{snapshot.ActorBodyId}'.");
            }

            if (!conditions.Coherent)
            {
                diagnostics.AddRange(conditions.Diagnostics);
            }
        }

        private static void ValidateTransformation(BodyBiologySnapshot snapshot, List<string> diagnostics)
        {
            BodyTransformationSnapshot transformation = snapshot.Transformation;
            if (transformation == null)
            {
                diagnostics.Add("Transformation snapshot is missing.");
                return;
            }

            if (!string.Equals(snapshot.ActorBodyId, transformation.ActorBodyId, StringComparison.Ordinal))
            {
                diagnostics.Add($"Transformation actor/body '{transformation.ActorBodyId}' does not match aggregate '{snapshot.ActorBodyId}'.");
            }

            if (!string.Equals(snapshot.PersonId, transformation.PersonId, StringComparison.Ordinal))
            {
                diagnostics.Add($"Transformation person '{transformation.PersonId}' does not match aggregate '{snapshot.PersonId}'.");
            }

            if (!transformation.Coherent)
            {
                diagnostics.AddRange(transformation.Diagnostics);
            }
        }
    }
}
