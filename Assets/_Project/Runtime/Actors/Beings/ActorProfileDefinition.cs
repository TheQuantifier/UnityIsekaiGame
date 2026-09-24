using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Beings
{
    [CreateAssetMenu(fileName = "ActorProfile", menuName = "Unity Isekai Game/Beings/Actor Profile")]
    public sealed class ActorProfileDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string actorProfileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BeingDefinition beingDefinition;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private ActorProfileStatContribution[] statContributions;
        [SerializeField] private ResistanceModifierDefinition[] baseResistances;
        [SerializeField] private string futureSensesPlaceholder;
        [SerializeField] private string futureMovementProfilePlaceholder;

        public string ActorProfileId => actorProfileId;
        public string Id => actorProfileId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public BeingDefinition BeingDefinition => beingDefinition;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Being;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<ActorProfileStatContribution> StatContributions => statContributions ?? Array.Empty<ActorProfileStatContribution>();
        public IReadOnlyList<ResistanceModifierDefinition> BaseResistances => baseResistances ?? System.Array.Empty<ResistanceModifierDefinition>();
        public string FutureSensesPlaceholder => futureSensesPlaceholder;
        public string FutureMovementProfilePlaceholder => futureMovementProfilePlaceholder;

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || report == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(Id) && !Id.StartsWith("actor-profile."))
            {
                report.AddWarning($"ActorProfileDefinition '{DisplayName}' should use the 'actor-profile.' namespace prefix.");
            }

            if (beingDefinition == null)
            {
                report.AddError($"ActorProfileDefinition '{DisplayName}' is missing a BeingDefinition reference.");
            }
            else if (!definitionsById.TryGetValue(beingDefinition.Id, out IGameDefinition being) || !(being is BeingDefinition))
            {
                report.AddError($"ActorProfileDefinition '{DisplayName}' references being '{beingDefinition.Id}', which is not in the configured catalog.");
            }

            ValidateStatContributions(definitionsById, report);
            ValidateBaseResistances(definitionsById, report);

            if (primaryCategory == null && beingDefinition != null && beingDefinition.PrimaryCategory == null)
            {
                report.AddWarning($"ActorProfileDefinition '{DisplayName}' has no profile category and its being has no category.");
            }
        }

        private void ValidateStatContributions(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < StatContributions.Count; i++)
            {
                ActorProfileStatContribution contribution = StatContributions[i];
                if (contribution == null || !contribution.IsValid)
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' has an invalid stat contribution at index {i}.");
                    continue;
                }

                if (!ids.Add(contribution.Stat.Id))
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' contributes to '{contribution.Stat.Id}' more than once.");
                }

                if (definitionsById == null || !definitionsById.TryGetValue(contribution.Stat.Id, out IGameDefinition found) || found is not CalculatedStatDefinition)
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' references calculated stat '{contribution.Stat.Id}' outside the configured catalog.");
                }
            }
        }

        private void ValidateBaseResistances(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (baseResistances == null)
            {
                return;
            }

            HashSet<string> seenDamageTypes = new HashSet<string>();
            for (int i = 0; i < baseResistances.Length; i++)
            {
                ResistanceModifierDefinition resistance = baseResistances[i];
                if (resistance == null)
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' has a null base resistance at index {i}.");
                    continue;
                }

                if (resistance.DamageType == null)
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' has a base resistance with no damage type at index {i}.");
                    continue;
                }

                if (!RuntimeResistanceCollection.IsSupportedResistance(resistance.Resistance))
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' has resistance {resistance.Resistance:0.###} for '{resistance.DamageType.Id}', outside -1 to 1.");
                }

                if (!seenDamageTypes.Add(resistance.DamageType.Id))
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' has duplicate base resistance for '{resistance.DamageType.Id}'.");
                }

                if (definitionsById == null
                    || !definitionsById.TryGetValue(resistance.DamageType.Id, out IGameDefinition found)
                    || found is not DamageTypeDefinition)
                {
                    report.AddError($"ActorProfileDefinition '{DisplayName}' references damage type '{resistance.DamageType.Id}', which is not in the configured catalog.");
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class ActorProfileStatContribution
    {
        [SerializeField] private CalculatedStatDefinition stat;
        [SerializeField] private CalculatedStatContributionKind kind = CalculatedStatContributionKind.Flat;
        [SerializeField] private CalculatedStatContributionDirection direction = CalculatedStatContributionDirection.Improve;
        [SerializeField, Min(0f)] private float magnitude;
        [SerializeField] private int priority;

        public CalculatedStatDefinition Stat => stat;
        public CalculatedStatContributionKind Kind => kind;
        public CalculatedStatContributionDirection Direction => direction;
        public float Magnitude => Mathf.Max(0f, magnitude);
        public int Priority => priority;
        public bool IsValid => stat != null && IsFinite(magnitude) && magnitude >= 0f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
