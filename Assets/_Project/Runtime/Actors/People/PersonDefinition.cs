using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Beings;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.People
{
    public enum PersonLifeStage
    {
        Infant = 0,
        Child = 1,
        Adolescent = 2,
        Adult = 3,
        Elder = 4
    }

    [CreateAssetMenu(fileName = "Person", menuName = "Unity Isekai Game/People/Person")]
    public sealed class PersonDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string personId;
        [SerializeField] private string displayName;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 4)] private string shortDescription;
        [SerializeField] private Sprite portrait;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private BeingDefinition beingDefinition;
        [SerializeField] private ActorProfileDefinition actorProfile;
        [SerializeField] private PlaceDefinition homePlace;
        [SerializeField] private FactionDefinition primaryFaction;
        [SerializeField] private string publicRoleTitle;
        [SerializeField] private FactionDefinition leadershipOfFaction;
        [SerializeField] private PersonImportance importance = PersonImportance.Standard;
        [SerializeField, Min(0)] private int chronologicalAgeYears = 18;
        [SerializeField] private PersonLifeStage lifeStage = PersonLifeStage.Adult;

        public string PersonId => personId;
        public string Id => personId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Title => title;
        public string ShortDescription => shortDescription;
        public Sprite Portrait => portrait;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Person;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public BeingDefinition BeingDefinition => beingDefinition;
        public ActorProfileDefinition ActorProfile => actorProfile;
        public PlaceDefinition HomePlace => homePlace;
        public FactionDefinition PrimaryFaction => primaryFaction;
        public string PublicRoleTitle => publicRoleTitle;
        public FactionDefinition LeadershipOfFaction => leadershipOfFaction;
        public PersonImportance Importance => importance;
        public int ChronologicalAgeYears => Mathf.Max(0, chronologicalAgeYears);
        public PersonLifeStage LifeStage => lifeStage;
        public bool IsAdult => lifeStage == PersonLifeStage.Adult || lifeStage == PersonLifeStage.Elder;
        public bool HasValidPersonId => !string.IsNullOrWhiteSpace(personId);

        public void DevelopmentConfigure(string id, string name, int ageYears, PersonLifeStage stage, PersonImportance personImportance = PersonImportance.Standard, string roleTitle = "")
        {
            personId = id?.Trim() ?? string.Empty;
            displayName = name?.Trim() ?? string.Empty;
            chronologicalAgeYears = Mathf.Max(0, ageYears);
            lifeStage = stage;
            importance = personImportance;
            publicRoleTitle = roleTitle?.Trim() ?? string.Empty;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || report == null)
            {
                return;
            }

            if (beingDefinition != null
                && (!definitionsById.TryGetValue(beingDefinition.Id, out IGameDefinition being) || !(being is BeingDefinition)))
            {
                report.AddError($"PersonDefinition '{DisplayName}' references being '{beingDefinition.Id}', which is not in the configured catalog.");
            }

            if (actorProfile != null)
            {
                if (!definitionsById.TryGetValue(actorProfile.Id, out IGameDefinition profile) || !(profile is ActorProfileDefinition))
                {
                    report.AddError($"PersonDefinition '{DisplayName}' references actor profile '{actorProfile.Id}', which is not in the configured catalog.");
                }
                else if (beingDefinition != null && actorProfile.BeingDefinition != null && actorProfile.BeingDefinition.Id != beingDefinition.Id)
                {
                    report.AddWarning($"PersonDefinition '{DisplayName}' references being '{beingDefinition.Id}' but actor profile '{actorProfile.Id}' references being '{actorProfile.BeingDefinition.Id}'.");
                }
            }

            if (homePlace != null
                && (!definitionsById.TryGetValue(homePlace.Id, out IGameDefinition place) || !(place is PlaceDefinition)))
            {
                report.AddError($"PersonDefinition '{DisplayName}' references home place '{homePlace.Id}', which is not in the configured catalog.");
            }

            ValidateFactionReference(primaryFaction, nameof(PrimaryFaction), definitionsById, report);
            ValidateFactionReference(leadershipOfFaction, nameof(LeadershipOfFaction), definitionsById, report);
        }

        private void ValidateFactionReference(
            FactionDefinition faction,
            string label,
            IReadOnlyDictionary<string, IGameDefinition> definitionsById,
            DefinitionValidationReport report)
        {
            if (faction == null)
            {
                return;
            }

            if (!definitionsById.TryGetValue(faction.Id, out IGameDefinition found) || found is not FactionDefinition)
            {
                report.AddError($"PersonDefinition '{DisplayName}' references {label} '{faction.Id}', which is not in the configured catalog.");
            }
        }
    }
}
