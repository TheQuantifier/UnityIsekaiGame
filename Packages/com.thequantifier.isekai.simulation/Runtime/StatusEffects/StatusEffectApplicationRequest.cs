using UnityEngine;

namespace UnityIsekaiGame.StatusEffects
{
    public readonly struct StatusEffectApplicationRequest
    {
        public StatusEffectApplicationRequest(
            StatusEffectDefinition definition,
            GameObject source,
            string sourceId,
            float durationOverride,
            string applicationId,
            float now)
        {
            Definition = definition;
            Source = source;
            SourceId = string.IsNullOrWhiteSpace(sourceId) ? string.Empty : sourceId;
            DurationOverride = durationOverride;
            ApplicationId = string.IsNullOrWhiteSpace(applicationId) ? string.Empty : applicationId;
            Now = now;
        }

        public StatusEffectDefinition Definition { get; }
        public GameObject Source { get; }
        public string SourceId { get; }
        public float DurationOverride { get; }
        public string ApplicationId { get; }
        public float Now { get; }
    }
}
