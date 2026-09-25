using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.People
{
    public sealed class PersonIdentity : MonoBehaviour
    {
        [SerializeField] private PersonDefinition definition;

        public PersonDefinition Definition => definition;
        public string PersonId => definition == null ? string.Empty : definition.PersonId;
        public string DisplayName => definition == null ? name : definition.DisplayName;
        public string Title => definition == null ? string.Empty : definition.Title;
        public string ShortDescription => definition == null ? string.Empty : definition.ShortDescription;
        public Sprite Portrait => definition == null ? null : definition.Portrait;
        public IReadOnlyList<TagDefinition> Tags => definition == null ? System.Array.Empty<TagDefinition>() : definition.Tags;
        public string FactionIdPlaceholder => definition == null ? string.Empty : definition.FactionIdPlaceholder;
        public string SettlementIdPlaceholder => definition == null ? string.Empty : definition.SettlementIdPlaceholder;
        public string HomePlaceId => definition == null || definition.HomePlace == null ? string.Empty : definition.HomePlace.Id;
        public PersonImportance Importance => definition == null ? PersonImportance.Background : definition.Importance;
        public int ChronologicalAgeYears => definition == null ? 0 : definition.ChronologicalAgeYears;
        public PersonLifeStage LifeStage => definition == null ? PersonLifeStage.Adult : definition.LifeStage;
        public bool IsAdult => definition != null && definition.IsAdult;
        public bool HasValidIdentity => definition != null && definition.HasValidPersonId;

        private void OnEnable()
        {
            PersonRegistry.Register(this);
        }

        private void OnDisable()
        {
            PersonRegistry.Unregister(this);
        }
    }
}
