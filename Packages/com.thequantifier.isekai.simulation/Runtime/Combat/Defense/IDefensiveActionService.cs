using System;

namespace UnityIsekaiGame.Combat.Defense
{
    public interface IDefensiveActionService
    {
        event Action<DefenseActivationResult> DefenseActivated;
        event Action<DefenseCancellationResult> DefenseCancelled;
        event Action<DefenseResolutionResult> DefenseAttempted;
        event Action<DefenseResolutionResult> AttackDodged;
        event Action<DefenseResolutionResult> AttackParried;
        event Action<DefenseResolutionResult> AttackBlockedByDefense;
        event Action<DefenseResolutionResult> AttackGuardReduced;
        event Action<DefenseResolutionResult> DefenseConsumed;

        DefenseActivationResult PreviewActivate(DefenseActivationRequest request);
        DefenseActivationResult Activate(DefenseActivationRequest request);
        DefenseCancellationResult PreviewCancel(DefenseCancellationRequest request);
        DefenseCancellationResult Cancel(DefenseCancellationRequest request);
        DefenseResolutionResult PreviewResolve(DefenseResolutionRequest request);
        DefenseResolutionResult Resolve(DefenseResolutionRequest request);
        bool TryGetActiveDefense(string defenderActorId, out DefensiveActionStateSnapshot snapshot);
        void ClearTransientStateForRestore(string defenderActorId = "");
    }
}
