using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.CharacterSystem
{
    public sealed class CharacterQueryService
    {
        private readonly CharacterSystemCoordinator character;

        public CharacterQueryService(CharacterSystemCoordinator character)
        {
            this.character = character;
        }

        public bool IsReady => character != null && character.IsReady;
        public long Revision => character == null ? 0L : character.Revision;

        public float GetBaseAttribute(string attributeId)
        {
            return character?.Attributes == null ? 0f : character.Attributes.GetValue(attributeId);
        }

        public float GetCalculatedStat(string statId)
        {
            return character?.CalculatedStats == null ? 0f : character.CalculatedStats.GetValue(statId);
        }

        public ResourceSnapshot GetResource(string resourceId)
        {
            if (character?.Resources != null && character.Resources.TryGetResource(resourceId, out ResourceSnapshot snapshot))
            {
                return snapshot;
            }

            return default;
        }

        public ActorLifecycleState GetLifecycleState()
        {
            ActorLifecycleController lifecycle = character == null ? null : character.GetComponent<ActorLifecycleController>();
            return lifecycle == null ? ActorLifecycleState.Active : lifecycle.State;
        }

        public bool CanAct()
        {
            ActorLifecycleController lifecycle = character == null ? null : character.GetComponent<ActorLifecycleController>();
            return lifecycle == null || lifecycle.CanAct;
        }

        public SkillGrade GetSkillGrade(string skillId)
        {
            return character?.Skills == null ? SkillGrade.F : character.Skills.GetGrade(skillId);
        }

        public bool HasTrait(string traitId, bool includeDormant = false, bool includeSuppressed = false)
        {
            return character?.Traits != null && character.Traits.HasTrait(traitId, includeDormant, includeSuppressed);
        }

        public CapabilitySnapshot GetCapability(string capabilityId)
        {
            return character?.Capabilities == null ? null : character.Capabilities.Evaluate(capabilityId);
        }

        public BodySnapshot GetBodySnapshot()
        {
            return character?.Body == null ? null : character.Body.CreateSnapshot();
        }

        public SpeciesDefinition GetSpecies()
        {
            return character?.Body == null ? null : character.Body.Species;
        }

        public BiologicalClassificationDefinition GetBiologicalClassification()
        {
            return character?.Body == null ? null : character.Body.BiologicalClassification;
        }

        public BodyFormDefinition GetBodyForm()
        {
            return character?.Body == null ? null : character.Body.BodyForm;
        }

        public AnatomySnapshot GetAnatomySnapshot()
        {
            return character?.Body == null ? null : character.Body.CreateAnatomySnapshot();
        }

        public AnatomyDefinition GetAnatomyDefinition()
        {
            return character?.Body == null ? null : character.Body.Species?.AnatomyDefinition;
        }

        public AnatomyNodeSnapshot GetAnatomyNode(string nodeId)
        {
            AnatomySnapshot snapshot = GetAnatomySnapshot();
            if (snapshot == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            foreach (AnatomyNodeSnapshot node in snapshot.Nodes)
            {
                if (node.NodeId == nodeId)
                {
                    return node;
                }
            }

            return null;
        }

        public bool IsAnatomyReady()
        {
            return character?.Body != null && character.Body.Anatomy.IsReady;
        }

        public bool IsStructurePresent(string nodeId)
        {
            AnatomyNodeSnapshot node = GetAnatomyNode(nodeId);
            return node != null && node.Present;
        }

        public bool HasBiologicalCapability(string capabilityKey)
        {
            CapabilitySnapshot snapshot = GetCapability(capabilityKey);
            return snapshot != null && snapshot.BooleanValue;
        }

        public bool IsBodyReady()
        {
            return character?.Body != null && character.Body.IsReady;
        }

        public BodyOperationResult ValidateBody()
        {
            if (character?.Body == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingActorBody, "Body runtime is missing.");
            }

            return character.Body.ValidateBody(out string failureReason)
                ? BodyOperationResult.Success("Body is ready.", character.Body.CreateSnapshot())
                : BodyOperationResult.Failure(BodyOperationResultCode.InvalidConfiguration, failureReason, snapshot: character.Body.CreateSnapshot());
        }

        public RequirementEvaluationResult EvaluateRequirement(RequirementSetDefinition requirementSet)
        {
            if (!IsReady)
            {
                RequirementEvaluationResult notReady = new RequirementEvaluationResult
                {
                    Passed = false,
                    RequirementSetId = requirementSet == null ? string.Empty : requirementSet.Id
                };
                notReady.NodeResults.Add(new RequirementNodeResult
                {
                    Passed = false,
                    InternalReason = "Character System is not Ready.",
                    PlayerFacingReason = "Character is not ready.",
                    FailureVisibility = RequirementFailureVisibility.Visible
                });
                return notReady;
            }

            return CapabilityRequirementEvaluator.Evaluate(requirementSet, character.CreateRequirementContext());
        }
    }
}
