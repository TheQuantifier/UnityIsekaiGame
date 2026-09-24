using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Observation
{
    public sealed class ObservationAuthoritySnapshot
    {
        public string[] CapabilityIds { get; set; } = Array.Empty<string>();
        public string[] TraitIds { get; set; } = Array.Empty<string>();
        public string[] SkillIds { get; set; } = Array.Empty<string>();
        public string[] EquipmentTagIds { get; set; } = Array.Empty<string>();
        public string[] KnowledgeDomainIds { get; set; } = Array.Empty<string>();
        public int ExpertiseQuality { get; set; } = KnowledgeConfidence.DefaultObservation;
        public int ToolQuality { get; set; } = KnowledgeConfidence.DefaultObservation;
    }

    public sealed class GameplayObservationRequest
    {
        public string TransactionId { get; set; }
        public string MethodId { get; set; }
        public Transform ObserverOrigin { get; set; }
        public Transform Target { get; set; }
        public ObservationTargetType TargetType { get; set; }
        public string TargetSubjectId { get; set; }
        public string TargetPersonId { get; set; }
        public string TargetActorId { get; set; }
        public string TargetBodyId { get; set; }
        public string TargetItemId { get; set; }
        public string TargetLocationId { get; set; }
        public string TargetEventId { get; set; }
        public SensoryChannel SensoryChannel { get; set; } = SensoryChannel.Vision;
        public ObservationVisibilityState Visibility { get; set; } = ObservationVisibilityState.Clear;
        public ConcealmentState Concealment { get; set; } = ConcealmentState.None;
        public ObservationAccessLevel AccessLevel { get; set; } = ObservationAccessLevel.Public;
        public ObservationConsentState Consent { get; set; } = ObservationConsentState.NotRequired;
        public int EnvironmentalQuality { get; set; } = KnowledgeConfidence.Maximum;
        public int LightingQuality { get; set; } = KnowledgeConfidence.Maximum;
        public int NoiseQuality { get; set; } = KnowledgeConfidence.Maximum;
        public double WorldTimeSeconds { get; set; }
        public bool PrivateAccessAuthorized { get; set; }
        public int ObstructionMask { get; set; } = ~0;
        public long ExpectedBodyRevision { get; set; }
        public long ExpectedConditionRevision { get; set; }
        public string[] TargetTags { get; set; } = Array.Empty<string>();
    }

    public sealed class GameplayObservationCoordinator
    {
        private readonly DefinitionRegistry registry;
        private readonly ObservationService observationService;

        public GameplayObservationCoordinator(DefinitionRegistry definitionRegistry, ObservationService service = null)
        {
            registry = definitionRegistry;
            observationService = service ?? new ObservationService(definitionRegistry);
        }

        public ObservationResult Observe(
            PersonKnowledgeRuntime observerKnowledge,
            string observerPersonId,
            string observerActorId,
            string observerBodyId,
            GameplayObservationRequest request,
            ObservableProjection projection,
            ObservationAuthoritySnapshot authority,
            bool preview = false)
        {
            if (request?.ObserverOrigin == null || request.Target == null)
            {
                return new ObservationResult(false, ObservationOutcomeCode.InvalidContext, "Gameplay observation requires concrete observer and target transforms.", null, request?.MethodId, 0, 0, preview, tracked: false, IdentificationResultState.Unresolved, DiagnosticResultState.Unresolved, null);
            }

            ObservationMethodDefinition method = null;
            registry?.TryGet(request.MethodId, out method);
            float distance = Vector3.Distance(request.ObserverOrigin.position, request.Target.position);
            float maximumRange = method == null ? 0f : method.MaximumRange;
            int distanceQuality = maximumRange <= 0f || distance > maximumRange
                ? 0
                : KnowledgeConfidence.Clamp(Mathf.RoundToInt((1f - distance / maximumRange) * 500f) + 500);
            bool hasLineOfSight = HasLineOfSight(request.ObserverOrigin, request.Target, request.ObstructionMask);
            int obstructionQuality = hasLineOfSight ? KnowledgeConfidence.Maximum : 0;
            ObservationAuthoritySnapshot resolvedAuthority = authority ?? new ObservationAuthoritySnapshot();
            ObservationContext context = new ObservationContext(
                observerPersonId,
                request.TransactionId,
                request.MethodId,
                request.SensoryChannel,
                request.TargetType,
                request.TargetSubjectId,
                observerActorId,
                observerBodyId,
                request.TargetPersonId,
                request.TargetActorId,
                request.TargetBodyId,
                request.TargetItemId,
                request.TargetLocationId,
                request.TargetEventId,
                distanceQuality,
                request.Visibility,
                request.Concealment,
                request.AccessLevel,
                request.Consent,
                request.EnvironmentalQuality,
                request.LightingQuality,
                request.NoiseQuality,
                obstructionQuality,
                resolvedAuthority.ExpertiseQuality,
                resolvedAuthority.ToolQuality,
                request.WorldTimeSeconds,
                KnowledgeTrackingPolicy.PlayerMechanicalOnly,
                mechanicallyRelevant: true,
                privateAccessAuthorized: request.PrivateAccessAuthorized,
                expectedBodyRevision: request.ExpectedBodyRevision,
                expectedConditionRevision: request.ExpectedConditionRevision,
                tags: request.TargetTags,
                actualDistance: distance,
                hasLineOfSight: hasLineOfSight,
                capabilityIds: resolvedAuthority.CapabilityIds,
                traitIds: resolvedAuthority.TraitIds,
                skillIds: resolvedAuthority.SkillIds,
                equipmentTagIds: resolvedAuthority.EquipmentTagIds,
                knowledgeDomainIds: resolvedAuthority.KnowledgeDomainIds);
            return observationService.Observe(observerKnowledge, context, projection, preview);
        }

        public static bool HasLineOfSight(Transform observerOrigin, Transform target, int obstructionMask)
        {
            Vector3 direction = target.position - observerOrigin.position;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(observerOrigin.position, direction / distance, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits.OrderBy(value => value.distance))
            {
                Transform hitTransform = hit.collider == null ? null : hit.collider.transform;
                if (hitTransform == null || hitTransform == observerOrigin || hitTransform.IsChildOf(observerOrigin))
                {
                    continue;
                }

                return hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform);
            }

            return true;
        }
    }
}
