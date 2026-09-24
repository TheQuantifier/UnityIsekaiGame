using System;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Observation
{
    public sealed class ObservationService
    {
        private readonly DefinitionRegistry registry;

        public ObservationService(DefinitionRegistry registry)
        {
            this.registry = registry;
        }

        public ObservationResult Observe(PersonKnowledgeRuntime observerKnowledge, ObservationContext context, ObservableProjection projection, bool preview)
        {
            if (!ValidateBase(observerKnowledge, context, projection, out ObservationResult failure))
            {
                return failure;
            }

            if (!registry.TryGet(context.MethodId, out ObservationMethodDefinition method))
            {
                return Failure(ObservationOutcomeCode.MissingMethod, "Observation Method is missing.", context, context?.MethodId, 0, 0, preview);
            }

            if (!method.Active)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, $"Observation Method '{method.Id}' is not active.", context, method.Id, 0, 0, preview);
            }

            if (!ValidateObservationRequirements(method, context, out string requirementFailure))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, requirementFailure, context, method.Id, 0, 0, preview);
            }

            if (!method.SensoryChannels.Contains(context.SensoryChannel))
            {
                return Failure(ObservationOutcomeCode.InvalidContext, $"Method '{method.Id}' does not support sensory channel '{context.SensoryChannel}'.", context, method.Id, 0, 0, preview);
            }

            if (!projection.Channels.Contains(context.SensoryChannel))
            {
                return Failure(ObservationOutcomeCode.InvalidContext, $"Projection '{projection.ProjectionId}' does not expose sensory channel '{context.SensoryChannel}'.", context, method.Id, 0, 0, preview);
            }

            if (!method.TargetTypes.Contains(context.TargetType) || projection.TargetType != context.TargetType)
            {
                return Failure(ObservationOutcomeCode.InvalidContext, $"Method '{method.Id}' does not support target type '{context.TargetType}'.", context, method.Id, 0, 0, preview);
            }

            if (method.RequiresConsent && !ConsentAllows(context.Consent, context.PrivateAccessAuthorized))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, $"Method '{method.Id}' requires consent or authorized access.", context, method.Id, 0, 0, preview);
            }

            if (!ProjectionRevisionMatches(context, projection.Proposition, out string staleReason))
            {
                return Failure(ObservationOutcomeCode.StaleTarget, staleReason, context, method.Id, 0, 0, preview);
            }

            int quality = CalculateQuality(method.BaseObservationQuality, context, method.PrivacyBypass);
            if (quality < projection.MinimumQuality)
            {
                return Failure(ObservationOutcomeCode.BelowThreshold, $"Observation quality {quality} is below required {projection.MinimumQuality}.", context, method.Id, quality, 0, preview);
            }

            if (!AccessAllows(context, projection.Visibility, method.PrivacyBypass, out ObservationOutcomeCode accessCode, out string accessReason))
            {
                return Failure(accessCode, accessReason, context, method.Id, quality, 0, preview);
            }

            int evidenceStrength = CalculateEvidenceStrength(projection.BaseEvidenceStrength, method.EvidenceStrengthMultiplier, quality);
            return ApplyKnowledge(observerKnowledge, context, projection, method.Id, quality, evidenceStrength, preview, IdentificationResultState.Unresolved, DiagnosticResultState.Unresolved, Array.Empty<DiagnosticHypothesis>());
        }

        public ObservationResult Identify(PersonKnowledgeRuntime observerKnowledge, ObservationContext context, ObservableProjection projection, IdentificationMethodDefinition method, bool preview)
        {
            if (!ValidateBase(observerKnowledge, context, projection, out ObservationResult failure))
            {
                return failure;
            }

            if (method == null)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, "Identification Method is missing.", context, context?.MethodId, 0, 0, preview);
            }

            if (!method.Active)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, $"Identification Method '{method.Id}' is not active.", context, method.Id, 0, 0, preview);
            }

            if (method.TargetType != ObservationTargetType.Unknown && method.TargetType != context.TargetType)
            {
                return Failure(ObservationOutcomeCode.InvalidContext, $"Identification Method '{method.Id}' does not support target type '{context.TargetType}'.", context, method.Id, 0, 0, preview);
            }

            if (!context.HasSkill(method.RequiredSkillId))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, $"Identification Method '{method.Id}' requires skill '{method.RequiredSkillId}'.", context, method.Id, 0, 0, preview);
            }

            if (method.CandidateTags.Count > 0 && !method.CandidateTags.Any(tag => context.Tags.Contains(tag, StringComparer.Ordinal)))
            {
                return Failure(ObservationOutcomeCode.InvalidContext, $"Identification Method '{method.Id}' has no candidate matching the target tags.", context, method.Id, 0, 0, preview);
            }

            int quality = CalculateQuality(method.ExactThreshold, context, privacyBypass: false);
            IdentificationResultState state = quality >= method.ExactThreshold
                ? IdentificationResultState.Exact
                : quality >= method.PartialThreshold
                    ? IdentificationResultState.Partial
                    : IdentificationResultState.Unresolved;
            if (state == IdentificationResultState.Unresolved)
            {
                return Failure(ObservationOutcomeCode.BelowThreshold, $"Identification quality {quality} did not meet partial threshold {method.PartialThreshold}.", context, method.Id, quality, 0, preview);
            }

            int evidenceStrength = state == IdentificationResultState.Exact ? method.ExactThreshold : method.PartialThreshold;
            return ApplyKnowledge(observerKnowledge, context, projection, method.Id, quality, evidenceStrength, preview, state, DiagnosticResultState.Unresolved, Array.Empty<DiagnosticHypothesis>());
        }

        public ObservationResult Examine(PersonKnowledgeRuntime observerKnowledge, ObservationContext context, ExaminationProjection projection, ExaminationMethodDefinition method, bool preview)
        {
            if (observerKnowledge == null)
            {
                return Failure(ObservationOutcomeCode.MissingKnowledgeRuntime, "Observer Knowledge runtime is missing.", context, method?.Id, 0, 0, preview);
            }

            if (context == null || string.IsNullOrWhiteSpace(context.ObserverPersonId) || string.IsNullOrWhiteSpace(context.TransactionId))
            {
                return Failure(ObservationOutcomeCode.InvalidContext, "Examination requires observer Person and transaction IDs.", context, method?.Id, 0, 0, preview);
            }

            if (method == null)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, "Examination Method is missing.", context, context.MethodId, 0, 0, preview);
            }

            if (!method.Active)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, $"Examination Method '{method.Id}' is not active.", context, method.Id, 0, 0, preview);
            }

            if (method.TargetType != context.TargetType)
            {
                return Failure(ObservationOutcomeCode.InvalidContext, $"Examination Method '{method.Id}' does not support target type '{context.TargetType}'.", context, method.Id, 0, 0, preview);
            }

            if (context.ActualDistance > method.RequiredProximity)
            {
                return Failure(ObservationOutcomeCode.AccessDenied, $"Examination Method '{method.Id}' requires proximity {method.RequiredProximity:0.##}; actual distance is {context.ActualDistance:0.##}.", context, method.Id, 0, 0, preview);
            }

            if (method.RequiresConsent && !ConsentAllows(context.Consent, context.PrivateAccessAuthorized))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, $"Examination Method '{method.Id}' requires consent or authorized access.", context, method.Id, 0, 0, preview);
            }

            if (!context.HasSkill(method.RequiredSkillId) || !context.HasEquipmentTag(method.RequiredToolTagId))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, $"Examination Method '{method.Id}' requirements are not satisfied.", context, method.Id, 0, 0, preview);
            }

            if (projection == null || projection.Indicators.Count == 0)
            {
                return Failure(ObservationOutcomeCode.MissingProjection, "Examination projection has no indicators.", context, method.Id, 0, 0, preview);
            }

            ObservableProjection indicator = projection.Indicators.FirstOrDefault(value => value.TargetType == method.TargetType);
            if (indicator == null)
            {
                return Failure(ObservationOutcomeCode.MissingProjection, $"Examination projection has no '{method.TargetType}' indicator.", context, method.Id, 0, 0, preview);
            }

            if (!ProjectionRevisionMatches(context, indicator.Proposition, out string staleReason))
            {
                return Failure(ObservationOutcomeCode.StaleTarget, staleReason, context, method.Id, 0, 0, preview);
            }

            int quality = CalculateQuality(method.BasePrecision, context, privacyBypass: false);
            if (quality < indicator.MinimumQuality)
            {
                return Failure(ObservationOutcomeCode.BelowThreshold, $"Examination quality {quality} is below required {indicator.MinimumQuality}.", context, method.Id, quality, 0, preview);
            }

            if (!AccessLevelAllows(context.AccessLevel, method.RequiredAccess, context.PrivateAccessAuthorized, out string accessReason))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, accessReason, context, method.Id, quality, 0, preview);
            }

            if (!AccessAllows(context, method.PrivacyClassification, privacyBypass: false, out ObservationOutcomeCode accessCode, out accessReason))
            {
                return Failure(accessCode, accessReason, context, method.Id, quality, 0, preview);
            }

            int evidenceStrength = CalculateEvidenceStrength(indicator.BaseEvidenceStrength, 1000, quality);
            return ApplyKnowledge(observerKnowledge, context, indicator, method.Id, quality, evidenceStrength, preview, IdentificationResultState.Unresolved, DiagnosticResultState.Unresolved, Array.Empty<DiagnosticHypothesis>());
        }

        public ObservationResult Diagnose(PersonKnowledgeRuntime observerKnowledge, ObservationContext context, DiagnosticProjection projection, DiagnosticMethodDefinition method, bool preview)
        {
            if (observerKnowledge == null)
            {
                return Failure(ObservationOutcomeCode.MissingKnowledgeRuntime, "Observer Knowledge runtime is missing.", context, method?.Id, 0, 0, preview);
            }

            if (context == null || string.IsNullOrWhiteSpace(context.ObserverPersonId) || string.IsNullOrWhiteSpace(context.TransactionId))
            {
                return Failure(ObservationOutcomeCode.InvalidContext, "Diagnosis requires observer Person and transaction IDs.", context, method?.Id, 0, 0, preview);
            }

            if (projection == null || projection.Hypotheses.Count == 0)
            {
                return Failure(ObservationOutcomeCode.MissingProjection, "Diagnostic projection has no hypotheses.", context, method?.Id, 0, 0, preview);
            }

            if (method == null)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, "Diagnostic Method is missing.", context, context.MethodId, 0, 0, preview);
            }

            if (!method.Active)
            {
                return Failure(ObservationOutcomeCode.MissingMethod, $"Diagnostic Method '{method.Id}' is not active.", context, method.Id, 0, 0, preview);
            }

            if (!context.HasSkill(method.RequiredSkillId)
                || !context.HasEquipmentTag(method.RequiredToolTagId)
                || !context.HasKnowledgeDomain(method.RequiredKnowledgeDomain))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, $"Diagnostic Method '{method.Id}' requirements are not satisfied.", context, method.Id, 0, 0, preview);
            }

            if (!AccessLevelAllows(context.AccessLevel, method.RequiredAccess, context.PrivateAccessAuthorized, out string diagnosticAccessFailure))
            {
                return Failure(ObservationOutcomeCode.AccessDenied, diagnosticAccessFailure, context, method.Id, 0, 0, preview);
            }

            int quality = CalculateQuality(method.ConfidenceCeiling, context, privacyBypass: false);
            DiagnosticHypothesis best = projection.Hypotheses.First();
            int cappedConfidence = Math.Min(method.ConfidenceCeiling, Math.Min(quality, best.Confidence));
            DiagnosticResultState state = cappedConfidence >= method.ExactDiagnosisThreshold && best.ExactCandidate
                ? DiagnosticResultState.Exact
                : cappedConfidence >= method.DifferentialHypothesisThreshold
                    ? DiagnosticResultState.Differential
                    : DiagnosticResultState.Unresolved;
            if (state == DiagnosticResultState.Unresolved)
            {
                return Failure(ObservationOutcomeCode.BelowThreshold, $"Diagnostic confidence {cappedConfidence} did not meet differential threshold {method.DifferentialHypothesisThreshold}.", context, method.Id, quality, 0, preview);
            }

            ObservableProjection evidenceProjection = new ObservableProjection(
                projection.ProjectionId,
                ObservationTargetType.BiologicalCondition,
                new KnowledgePropositionData
                {
                    factDefinitionId = method.FactDefinitionId,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = string.IsNullOrWhiteSpace(context.TargetBodyId) ? context.TargetSubjectId : context.TargetBodyId,
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = state == DiagnosticResultState.Exact ? best.CandidateId : best.FamilyId,
                    bodyContextId = context.TargetBodyId,
                    sourceContextId = projection.ProjectionId,
                    sourceRevision = context.ExpectedConditionRevision
                },
                KnowledgeVisibility.Private,
                method.DifferentialHypothesisThreshold,
                cappedConfidence,
                new[] { context.SensoryChannel },
                mechanicallyRelevant: true,
                privacyReason: "Diagnosis is private medical Knowledge.",
                tags: new[] { "diagnosis", method.Category.ToString() });

            return ApplyKnowledge(observerKnowledge, context, evidenceProjection, method.Id, quality, cappedConfidence, preview, IdentificationResultState.Unresolved, state, projection.Hypotheses.ToArray());
        }

        public static int CalculateQuality(int baseQuality, ObservationContext context, bool privacyBypass)
        {
            if (context == null)
            {
                return 0;
            }

            int quality = KnowledgeConfidence.Clamp(baseQuality);
            quality = Combine(quality, context.DistanceQuality);
            quality = Combine(quality, context.EnvironmentalQuality);
            quality = Combine(quality, context.LightingQuality);
            quality = Combine(quality, context.NoiseQuality);
            quality = Combine(quality, context.ObstructionQuality);
            quality = Combine(quality, context.ExpertiseQuality);
            quality = Combine(quality, context.ToolQuality);
            quality -= VisibilityPenalty(context.Visibility);
            quality -= ConcealmentPenalty(context.Concealment, privacyBypass);
            return KnowledgeConfidence.Clamp(quality);
        }

        private ObservationResult ApplyKnowledge(PersonKnowledgeRuntime observerKnowledge, ObservationContext context, ObservableProjection projection, string methodId, int quality, int evidenceStrength, bool preview, IdentificationResultState identificationState, DiagnosticResultState diagnosticState, DiagnosticHypothesis[] hypotheses)
        {
            if (!ShouldTrack(context.TrackingPolicy, context.MechanicallyRelevant && projection.MechanicallyRelevant))
            {
                return new ObservationResult(true, ObservationOutcomeCode.NotTracked, "Observation succeeded but tracking policy skipped Knowledge mutation.", context, methodId, quality, evidenceStrength, preview, tracked: false, identificationState, diagnosticState, null, hypotheses);
            }

            KnowledgeObservationRequest request = new KnowledgeObservationRequest
            {
                PersonId = context.ObserverPersonId,
                TransactionId = context.TransactionId,
                Proposition = projection.Proposition,
                AcquisitionSource = AcquisitionSourceFor(context.SensoryChannel),
                Provenance = ProvenanceFor(context.SensoryChannel),
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = evidenceStrength,
                Credibility = quality,
                GameTimeSeconds = context.GameTimeSeconds,
                SourceId = string.IsNullOrWhiteSpace(context.TargetBodyId) ? context.TargetSubjectId : context.TargetBodyId,
                Visibility = projection.Visibility,
                EvidenceId = StableObservationEvidenceId(context.ObserverPersonId, methodId, projection.Proposition),
                PrivateAccessAuthorized = context.PrivateAccessAuthorized || context.AccessLevel == ObservationAccessLevel.Medical || context.AccessLevel == ObservationAccessLevel.Diagnostic || context.AccessLevel == ObservationAccessLevel.Development,
                TruthAuthorization = context.TruthAuthorization,
                RelatedEventId = context.TargetEventId,
                Tags = context.Tags.Concat(projection.Tags).Concat(new[] { methodId, context.SensoryChannel.ToString() }).ToArray()
            };

            KnowledgeOperationResult knowledgeResult = preview
                ? observerKnowledge.PreviewObservation(request)
                : observerKnowledge.RecordObservation(request);
            if (!knowledgeResult.Succeeded)
            {
                return new ObservationResult(false, ObservationOutcomeCode.KnowledgeRejected, knowledgeResult.Message, context, methodId, quality, evidenceStrength, preview, tracked: true, identificationState, diagnosticState, knowledgeResult, hypotheses);
            }

            ObservationOutcomeCode code = knowledgeResult.Duplicate ? ObservationOutcomeCode.Duplicate : preview ? ObservationOutcomeCode.Preview : ObservationOutcomeCode.Success;
            return new ObservationResult(true, code, knowledgeResult.Message, context, methodId, quality, evidenceStrength, preview, tracked: true, identificationState, diagnosticState, knowledgeResult, hypotheses);
        }

        private static bool ValidateBase(PersonKnowledgeRuntime observerKnowledge, ObservationContext context, ObservableProjection projection, out ObservationResult failure)
        {
            failure = null;
            if (observerKnowledge == null)
            {
                failure = Failure(ObservationOutcomeCode.MissingKnowledgeRuntime, "Observer Knowledge runtime is missing.", context, context?.MethodId, 0, 0, preview: false);
                return false;
            }

            if (context == null || string.IsNullOrWhiteSpace(context.ObserverPersonId) || string.IsNullOrWhiteSpace(context.TransactionId) || string.IsNullOrWhiteSpace(context.MethodId))
            {
                failure = Failure(ObservationOutcomeCode.InvalidContext, "Observation requires observer Person, transaction, and method IDs.", context, context?.MethodId, 0, 0, preview: false);
                return false;
            }

            if (projection == null || projection.Proposition == null)
            {
                failure = Failure(ObservationOutcomeCode.MissingProjection, "Observable projection is missing.", context, context.MethodId, 0, 0, preview: false);
                return false;
            }

            return true;
        }

        private static bool AccessAllows(ObservationContext context, KnowledgeVisibility visibility, bool privacyBypass, out ObservationOutcomeCode code, out string reason)
        {
            code = ObservationOutcomeCode.Success;
            reason = string.Empty;
            if (visibility == KnowledgeVisibility.Public || visibility == KnowledgeVisibility.PersonallyObservable)
            {
                return true;
            }

            if (privacyBypass || context.PrivateAccessAuthorized || context.AccessLevel == ObservationAccessLevel.AuthorizedTruth || context.AccessLevel == ObservationAccessLevel.Development)
            {
                return true;
            }

            if ((visibility == KnowledgeVisibility.Private || visibility == KnowledgeVisibility.Confidential) && context.AccessLevel == ObservationAccessLevel.Medical)
            {
                return true;
            }

            code = visibility == KnowledgeVisibility.DiagnosticOnly ? ObservationOutcomeCode.PrivacyBlocked : ObservationOutcomeCode.AccessDenied;
            reason = string.IsNullOrWhiteSpace(context.AuthorityContext) ? $"Visibility '{visibility}' blocks observation." : context.AuthorityContext;
            return false;
        }

        private static bool AccessLevelAllows(ObservationAccessLevel actual, ObservationAccessLevel required, bool privateAuthorized, out string reason)
        {
            reason = string.Empty;
            if (privateAuthorized || actual == ObservationAccessLevel.Development || actual == ObservationAccessLevel.AuthorizedTruth)
            {
                return true;
            }

            if (actual == required || required == ObservationAccessLevel.Public)
            {
                return true;
            }

            if (required == ObservationAccessLevel.Medical && (actual == ObservationAccessLevel.Diagnostic || actual == ObservationAccessLevel.Medical))
            {
                return true;
            }

            reason = $"Access '{actual}' does not satisfy required examination access '{required}'.";
            return false;
        }

        private static bool ConsentAllows(ObservationConsentState consent, bool privateAuthorized)
        {
            return privateAuthorized || consent == ObservationConsentState.Granted || consent == ObservationConsentState.NotRequired || consent == ObservationConsentState.IncapacitatedAccess;
        }

        private static bool ValidateObservationRequirements(ObservationMethodDefinition method, ObservationContext context, out string failure)
        {
            failure = string.Empty;
            if (context.ActualDistance > method.MaximumRange)
            {
                failure = $"Observation Method '{method.Id}' maximum range is {method.MaximumRange:0.##}; actual distance is {context.ActualDistance:0.##}.";
                return false;
            }

            if (method.RequiresLineOfSight && !context.HasLineOfSight)
            {
                failure = $"Observation Method '{method.Id}' requires line of sight.";
                return false;
            }

            if (!context.HasCapability(method.RequiredCapabilityId)
                || !context.HasTrait(method.RequiredTraitId)
                || !context.HasSkill(method.RequiredSkillId)
                || !context.HasEquipmentTag(method.RequiredEquipmentTagId))
            {
                failure = $"Observation Method '{method.Id}' observer requirements are not satisfied.";
                return false;
            }

            return true;
        }

        private static bool ShouldTrack(KnowledgeTrackingPolicy policy, bool mechanicallyRelevant)
        {
            return policy switch
            {
                KnowledgeTrackingPolicy.NpcFullTracking => true,
                KnowledgeTrackingPolicy.PlayerMechanicalOnly => mechanicallyRelevant,
                KnowledgeTrackingPolicy.RemotePlayerMechanicalOnly => mechanicallyRelevant,
                KnowledgeTrackingPolicy.DevelopmentObserverNoMutation => false,
                _ => false
            };
        }

        private static bool ProjectionRevisionMatches(ObservationContext context, KnowledgePropositionData proposition, out string reason)
        {
            reason = string.Empty;
            if (context == null || proposition == null || proposition.sourceRevision <= 0)
            {
                return true;
            }

            long expected = ExpectedRevisionFor(context, proposition);
            if (expected <= 0 || expected == proposition.sourceRevision)
            {
                return true;
            }

            reason = $"Projection source revision {proposition.sourceRevision} does not match current target revision {expected}.";
            return false;
        }

        private static long ExpectedRevisionFor(ObservationContext context, KnowledgePropositionData proposition)
        {
            string factId = proposition.factDefinitionId ?? string.Empty;
            if (string.Equals(factId, BuiltInKnowledgeFacts.BodyInjury, StringComparison.Ordinal)
                || string.Equals(factId, BuiltInKnowledgeFacts.BodySymptom, StringComparison.Ordinal))
            {
                return context.ExpectedConditionRevision;
            }

            if (string.Equals(factId, BuiltInKnowledgeFacts.SpeciesIdentity, StringComparison.Ordinal)
                || string.Equals(factId, BuiltInKnowledgeFacts.BodyTransformation, StringComparison.Ordinal))
            {
                return context.ExpectedBodyRevision;
            }

            return Math.Max(context.ExpectedBodyRevision, context.ExpectedConditionRevision);
        }

        private static string StableObservationEvidenceId(string observerPersonId, string methodId, KnowledgePropositionData proposition)
        {
            string identity = KnowledgeProposition.BuildIdentity(proposition);
            return $"evidence.observation.{StableHash($"{observerPersonId}|{methodId}|{identity}")}";
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("x16");
            }
        }

        private static int CalculateEvidenceStrength(int baseStrength, int multiplier, int quality)
        {
            int scaled = (baseStrength * Math.Max(0, multiplier)) / 1000;
            return KnowledgeConfidence.Clamp((scaled + quality) / 2);
        }

        private static int Combine(int current, int next)
        {
            return (current + KnowledgeConfidence.Clamp(next)) / 2;
        }

        private static int VisibilityPenalty(ObservationVisibilityState visibility)
        {
            return visibility switch
            {
                ObservationVisibilityState.Dim => 100,
                ObservationVisibilityState.Obstructed => 250,
                ObservationVisibilityState.Hidden => 700,
                ObservationVisibilityState.DiagnosticOnly => 500,
                _ => 0
            };
        }

        private static int ConcealmentPenalty(ConcealmentState concealment, bool privacyBypass)
        {
            if (privacyBypass)
            {
                return 0;
            }

            return concealment switch
            {
                ConcealmentState.Minor => 75,
                ConcealmentState.Moderate => 175,
                ConcealmentState.Major => 350,
                ConcealmentState.Complete => 800,
                ConcealmentState.Disguised => 300,
                ConcealmentState.TransformedAppearance => 250,
                _ => 0
            };
        }

        private static KnowledgeAcquisitionSource AcquisitionSourceFor(SensoryChannel channel)
        {
            return channel switch
            {
                SensoryChannel.Testimony => KnowledgeAcquisitionSource.Testimony,
                SensoryChannel.RecordReading => KnowledgeAcquisitionSource.WrittenSource,
                SensoryChannel.PainSensationFoundation or SensoryChannel.Proprioception or SensoryChannel.TemperatureSensation => KnowledgeAcquisitionSource.BodySensation,
                SensoryChannel.ToolSensorFoundation or SensoryChannel.MagicalDetectionFoundation => KnowledgeAcquisitionSource.Examination,
                _ => KnowledgeAcquisitionSource.DirectObservation
            };
        }

        private static KnowledgeProvenance ProvenanceFor(SensoryChannel channel)
        {
            return channel switch
            {
                SensoryChannel.Testimony => KnowledgeProvenance.Testimony,
                SensoryChannel.RecordReading => KnowledgeProvenance.Document,
                SensoryChannel.PainSensationFoundation or SensoryChannel.Proprioception or SensoryChannel.TemperatureSensation => KnowledgeProvenance.SelfSensation,
                SensoryChannel.MagicalDetectionFoundation => KnowledgeProvenance.MagicalDetectionFoundation,
                SensoryChannel.ToolSensorFoundation => KnowledgeProvenance.Examination,
                _ => KnowledgeProvenance.DirectObservation
            };
        }

        private static ObservationResult Failure(ObservationOutcomeCode code, string message, ObservationContext context, string methodId, int quality, int evidenceStrength, bool preview)
        {
            return new ObservationResult(false, code, message, context, methodId, quality, evidenceStrength, preview, tracked: false, IdentificationResultState.Unresolved, DiagnosticResultState.Unresolved, null);
        }
    }
}
