using UnityEngine;

namespace UnityIsekaiGame.GameData
{
    [CreateAssetMenu(fileName = "GameDataDefaults", menuName = "Unity Isekai Game/Game Data/Defaults")]
    public sealed class GameDataDefaultsDefinition : ScriptableObject
    {
        [SerializeField] private RarityDefinition defaultRarity;
        [SerializeField] private ScriptableObject defaultQualityTier;
        [SerializeField] private ScriptableObject itemConditionScale;
        [SerializeField, Range(0f, 1f)] private float defaultQualityNormalized = 0.5f;
        [SerializeField, Range(0f, 1f)] private float initialDurabilityNormalized = 1f;

        public RarityDefinition DefaultRarity => defaultRarity;
        public ScriptableObject DefaultQualityTier => defaultQualityTier;
        public ScriptableObject ItemConditionScale => itemConditionScale;
        public float DefaultQualityNormalized => Mathf.Clamp01(defaultQualityNormalized);
        public float InitialDurabilityNormalized => Mathf.Clamp01(initialDurabilityNormalized);
    }
}
