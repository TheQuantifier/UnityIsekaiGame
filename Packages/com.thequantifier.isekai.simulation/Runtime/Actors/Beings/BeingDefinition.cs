using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings
{
    [CreateAssetMenu(fileName = "Being", menuName = "Unity Isekai Game/Beings/Being")]
    public sealed class BeingDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string beingId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private Sprite icon;
        [SerializeField] private BeingDefinition parentBeing;
        [SerializeField] private BeingIntelligenceLevel intelligence = BeingIntelligenceLevel.Instinctive;
        [SerializeField] private BeingSocialCapability socialCapability = BeingSocialCapability.Reactive;
        [SerializeField] private BeingLocomotionCapabilities locomotionCapabilities = BeingLocomotionCapabilities.Ground;
        [SerializeField] private BeingNatureFlags nature = BeingNatureFlags.Living;
        [SerializeField] private ActorProfileDefinition defaultActorProfile;

        public string BeingId => beingId;
        public string Id => beingId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Being;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public Sprite Icon => icon;
        public BeingDefinition ParentBeing => parentBeing;
        public BeingIntelligenceLevel Intelligence => intelligence;
        public BeingSocialCapability SocialCapability => socialCapability;
        public BeingLocomotionCapabilities LocomotionCapabilities => locomotionCapabilities;
        public BeingNatureFlags Nature => nature;
        public ActorProfileDefinition DefaultActorProfile => defaultActorProfile;

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddWarning($"BeingDefinition '{name}' has an empty display name.");
            }

            if (!string.IsNullOrWhiteSpace(Id) && !Id.StartsWith("being."))
            {
                report.AddWarning($"BeingDefinition '{DisplayName}' should use the 'being.' namespace prefix.");
            }

            if (parentBeing != null)
            {
                if (ReferenceEquals(this, parentBeing) || parentBeing.Id == Id)
                {
                    report.AddError($"BeingDefinition '{DisplayName}' cannot parent itself.");
                }
                else if (!definitionsById.TryGetValue(parentBeing.Id, out IGameDefinition parent) || !(parent is BeingDefinition))
                {
                    report.AddError($"BeingDefinition '{DisplayName}' references parent being '{parentBeing.Id}', which is not in the configured catalog.");
                }

                if (HasParentCycle())
                {
                    report.AddError($"BeingDefinition '{DisplayName}' has a circular parent being hierarchy.");
                }
            }

            if (locomotionCapabilities == BeingLocomotionCapabilities.None)
            {
                report.AddWarning($"BeingDefinition '{DisplayName}' has no locomotion capability metadata.");
            }

            if (intelligence == BeingIntelligenceLevel.None && socialCapability != BeingSocialCapability.None)
            {
                report.AddWarning($"BeingDefinition '{DisplayName}' has no intelligence but social capability '{socialCapability}'.");
            }

            if (defaultActorProfile != null)
            {
                if (!definitionsById.TryGetValue(defaultActorProfile.Id, out IGameDefinition profile) || !(profile is ActorProfileDefinition))
                {
                    report.AddError($"BeingDefinition '{DisplayName}' references default actor profile '{defaultActorProfile.Id}', which is not in the configured catalog.");
                }
                else if (defaultActorProfile.BeingDefinition != null && defaultActorProfile.BeingDefinition.Id != Id)
                {
                    report.AddWarning($"BeingDefinition '{DisplayName}' default profile '{defaultActorProfile.Id}' references being '{defaultActorProfile.BeingDefinition.Id}'.");
                }
            }
        }

        private bool HasParentCycle()
        {
            HashSet<string> visitedIds = new HashSet<string>();
            BeingDefinition current = this;

            while (current != null)
            {
                if (!visitedIds.Add(current.Id))
                {
                    return true;
                }

                current = current.ParentBeing;
            }

            return false;
        }
    }
}
