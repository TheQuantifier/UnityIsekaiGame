using UnityEngine;

namespace UnityIsekaiGame.StatusEffects
{
    public interface IStatusEffectReceiver
    {
        StatusApplicationResult CanApplyStatus(StatusEffectApplicationRequest request);
        StatusApplicationResult ApplyStatus(StatusEffectApplicationRequest request);
        bool RemoveStatus(string applicationId);
        bool RemoveStatusesByDefinition(string definitionId);
        StatusEffectController StatusController { get; }
        GameObject gameObject { get; }
    }
}
