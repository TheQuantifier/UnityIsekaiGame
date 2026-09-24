using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "OverallLevelConfiguration", menuName = "Unity Isekai Game/Progression/Overall Level Configuration")]
    public sealed class OverallLevelConfiguration : ScriptableObject, IGameDefinition
    {
        [SerializeField] private string configurationId = "overall-level.prototype";
        [SerializeField] private string displayName = "Prototype Overall Level";
        [SerializeField, Range(0f, 1f)] private float activityWeight = 0.75f;
        [SerializeField, Range(0f, 1f)] private float statWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float successWeight = 0.55f;
        [SerializeField, Range(0f, 1f)] private float playtimeWeight = 0.15f;
        [SerializeField, Range(0f, 1f)] private float accountAgeWeight = 0.10f;
        [SerializeField, Range(0f, 1f)] private float participationWeight = 0.20f;
        [SerializeField, Range(0f, 1f)] private float successPercentageWeight = 0.60f;
        [SerializeField, Range(0f, 1f)] private float successDifficultyWeight = 0.40f;
        [SerializeField, Min(1)] private int minimumMeaningfulActivitySamples = 5;
        [SerializeField, Min(1f)] private float activePlaytimeTargetSeconds = 36000f;
        [SerializeField, Min(1f)] private float accountAgeTargetDays = 30f;
        [SerializeField, Min(1)] private int participationTargetCount = 20;
        [SerializeField, Min(1f)] private float persistentStatTargetTotal = 100f;
        [SerializeField, Min(1f)] private float attributeNormalizationConstant = 20f;
        [SerializeField] private AttributeDefinition[] eligiblePersistentAttributes;

        public string Id => configurationId;
        public string DisplayName => displayName;
        public float ActivityWeight => Mathf.Clamp01(activityWeight);
        public float StatWeight => Mathf.Clamp01(statWeight);
        public float SuccessWeight => Mathf.Clamp01(successWeight);
        public float PlaytimeWeight => Mathf.Clamp01(playtimeWeight);
        public float AccountAgeWeight => Mathf.Clamp01(accountAgeWeight);
        public float ParticipationWeight => Mathf.Clamp01(participationWeight);
        public float SuccessPercentageWeight => Mathf.Clamp01(successPercentageWeight);
        public float SuccessDifficultyWeight => Mathf.Clamp01(successDifficultyWeight);
        public int MinimumMeaningfulActivitySamples => Mathf.Max(1, minimumMeaningfulActivitySamples);
        public float ActivePlaytimeTargetSeconds => Mathf.Max(1f, activePlaytimeTargetSeconds);
        public float AccountAgeTargetDays => Mathf.Max(1f, accountAgeTargetDays);
        public int ParticipationTargetCount => Mathf.Max(1, participationTargetCount);
        public float PersistentStatTargetTotal => Mathf.Max(1f, persistentStatTargetTotal);
        public float AttributeNormalizationConstant => Mathf.Max(1f, attributeNormalizationConstant);
        public AttributeDefinition[] EligiblePersistentAttributes => eligiblePersistentAttributes ?? System.Array.Empty<AttributeDefinition>();
    }
}
