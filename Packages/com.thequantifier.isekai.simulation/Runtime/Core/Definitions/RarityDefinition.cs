using UnityEngine;

namespace UnityIsekaiGame.GameData
{
    [CreateAssetMenu(fileName = "Rarity", menuName = "Unity Isekai Game/Game Data/Rarity")]
    public sealed class RarityDefinition : ScriptableObject, IGameDefinition
    {
        [SerializeField] private string rarityId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField, Min(0)] private int rank;
        [SerializeField] private bool defaultRarity;
        [SerializeField] private Color displayColor = Color.white;
        [SerializeField] private Sprite icon;

        public string RarityId => rarityId;
        public string Id => rarityId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int Rank => rank;
        public bool IsDefault => defaultRarity;
        public Color DisplayColor => displayColor;
        public Sprite Icon => icon;

        private void OnValidate()
        {
            rank = Mathf.Max(0, rank);
        }
    }
}
