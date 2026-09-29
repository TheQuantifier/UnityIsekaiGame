using System;
using UnityIsekaiGame.Beings.Biology.Integration;

namespace UnityIsekaiGame.Knowledge
{
    public enum KnowledgeObservationAccess
    {
        OrdinaryObservation,
        SelfSensation,
        Examination,
        AuthorizedDevelopmentTruth
    }

    public static class KnowledgeObservationProjection
    {
        public static KnowledgeObservationRequest VisibleSpecies(
            BodyBiologySnapshot snapshot,
            string observerPersonId,
            string transactionId,
            double gameTimeSeconds,
            KnowledgeObservationAccess access)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new KnowledgeObservationRequest
            {
                PersonId = observerPersonId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.SpeciesIdentity,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = snapshot.ActorBodyId,
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = access == KnowledgeObservationAccess.AuthorizedDevelopmentTruth ? snapshot.SpeciesId : VisibleSpeciesValue(snapshot.SpeciesId),
                    bodyContextId = snapshot.ActorBodyId,
                    sourceRevision = snapshot.Revisions.BodyRevision
                },
                AcquisitionSource = access == KnowledgeObservationAccess.Examination ? KnowledgeAcquisitionSource.Examination : KnowledgeAcquisitionSource.DirectObservation,
                Provenance = access == KnowledgeObservationAccess.Examination ? KnowledgeProvenance.Examination : KnowledgeProvenance.DirectObservation,
                Strength = access == KnowledgeObservationAccess.AuthorizedDevelopmentTruth ? KnowledgeConfidence.DefaultTrustedEvidence : KnowledgeConfidence.DefaultObservation,
                Credibility = ObservationCredibility(access),
                GameTimeSeconds = gameTimeSeconds,
                SourceId = snapshot.ActorBodyId,
                Visibility = KnowledgeVisibility.Public,
                PrivateAccessAuthorized = access == KnowledgeObservationAccess.AuthorizedDevelopmentTruth,
                TruthAuthorization = access == KnowledgeObservationAccess.AuthorizedDevelopmentTruth
                    ? KnowledgeTruthAuthorization.CreateTrusted("knowledge.visible-species.authorized-development-truth")
                    : null
            };
        }

        public static KnowledgeObservationRequest SelfSymptom(
            BodyBiologySnapshot snapshot,
            string transactionId,
            string symptom,
            int strength,
            double gameTimeSeconds)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new KnowledgeObservationRequest
            {
                PersonId = snapshot.PersonId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.BodySymptom,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = snapshot.ActorBodyId,
                    valueType = KnowledgeValueType.Qualitative,
                    qualitativeValue = string.IsNullOrWhiteSpace(symptom) ? "unwell" : symptom,
                    bodyContextId = snapshot.ActorBodyId,
                    sourceRevision = snapshot.Revisions.BodyRevision
                },
                AcquisitionSource = KnowledgeAcquisitionSource.BodySensation,
                Provenance = KnowledgeProvenance.SelfSensation,
                Strength = strength,
                Credibility = KnowledgeConfidence.Maximum,
                GameTimeSeconds = gameTimeSeconds,
                SourceId = snapshot.ActorBodyId,
                Visibility = KnowledgeVisibility.PersonallyObservable,
                PrivateAccessAuthorized = true
            };
        }

        public static KnowledgeObservationRequest VisibleInjury(
            BodyBiologySnapshot snapshot,
            string observerPersonId,
            string transactionId,
            string visibleInjuryValue,
            double gameTimeSeconds,
            KnowledgeObservationAccess access)
        {
            if (snapshot == null)
            {
                return null;
            }

            bool exact = access == KnowledgeObservationAccess.Examination || access == KnowledgeObservationAccess.AuthorizedDevelopmentTruth;
            return new KnowledgeObservationRequest
            {
                PersonId = observerPersonId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.BodyInjury,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = snapshot.ActorBodyId,
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = exact ? visibleInjuryValue : "injury.visible-wound",
                    bodyContextId = snapshot.ActorBodyId,
                    sourceRevision = snapshot.Revisions.ConditionRevision
                },
                AcquisitionSource = exact ? KnowledgeAcquisitionSource.Examination : KnowledgeAcquisitionSource.DirectObservation,
                Provenance = exact ? KnowledgeProvenance.Examination : KnowledgeProvenance.DirectObservation,
                Strength = exact ? KnowledgeConfidence.DefaultTrustedEvidence : KnowledgeConfidence.DefaultObservation,
                Credibility = ObservationCredibility(access),
                GameTimeSeconds = gameTimeSeconds,
                SourceId = snapshot.ActorBodyId,
                Visibility = exact ? KnowledgeVisibility.PersonallyObservable : KnowledgeVisibility.Public,
                PrivateAccessAuthorized = exact
            };
        }

        public static KnowledgeObservationRequest PreviousBodyHistory(
            string personId,
            string previousBodyId,
            string transactionId,
            double gameTimeSeconds)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = personId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.BodyPreviousBody,
                    subjectType = KnowledgeSubjectType.Person,
                    subjectId = personId,
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = previousBodyId ?? string.Empty,
                    bodyContextId = previousBodyId ?? string.Empty
                },
                AcquisitionSource = KnowledgeAcquisitionSource.PersonalExperience,
                Provenance = KnowledgeProvenance.Memory,
                Strength = KnowledgeConfidence.DefaultTrustedEvidence,
                Credibility = KnowledgeConfidence.DefaultTrustedEvidence,
                GameTimeSeconds = gameTimeSeconds,
                SourceId = previousBodyId ?? string.Empty,
                Visibility = KnowledgeVisibility.Private,
                PrivateAccessAuthorized = true
            };
        }

        private static string VisibleSpeciesValue(string speciesId)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                return "species.appears-unknown";
            }

            return speciesId switch
            {
                "species.basic-spirit" => "species.appears-spirit-like",
                "species.basic-construct" => "species.appears-construct-like",
                _ => speciesId
            };
        }

        private static int ObservationCredibility(KnowledgeObservationAccess access)
        {
            return access == KnowledgeObservationAccess.OrdinaryObservation
                || access == KnowledgeObservationAccess.SelfSensation
                || access == KnowledgeObservationAccess.Examination
                || access == KnowledgeObservationAccess.AuthorizedDevelopmentTruth
                ? KnowledgeConfidence.Maximum
                : KnowledgeConfidence.DefaultObservation;
        }
    }
}
