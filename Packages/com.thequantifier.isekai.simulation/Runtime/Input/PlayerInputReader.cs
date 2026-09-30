using System;
using UnityEngine;

namespace UnityIsekaiGame.Input
{
    /// <summary>
    /// Presentation-neutral input contract consumed by gameplay. The graphical client supplies
    /// the Input System implementation; the dedicated server compiles only this contract.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public virtual Vector2 Move => Vector2.zero;
        public virtual Vector2 Look => Vector2.zero;
        public virtual bool SprintHeld => false;
        public virtual bool IsPointerLook => false;
        public virtual bool GameplayInputBlocked => false;
        public virtual bool MouseLookEnabled => false;
        public virtual bool JumpPressedThisFrame => false;
        public virtual bool TravelTogglePressedThisFrame => false;
        public virtual bool ChatTogglePressedThisFrame => false;
        public virtual bool ProfessionTogglePressedThisFrame => false;
        public virtual bool SocialDebugTogglePressedThisFrame => false;

        public virtual bool ConsumeJump() => false;
        public virtual bool ConsumeInteract() => false;
        public virtual bool ConsumeAttack() => false;
        public virtual bool ConsumeCastPrimarySpell() => false;
        public virtual bool ConsumeSpellSlotSelection(out int slotIndex) { slotIndex = -1; return false; }
        public virtual bool ConsumeSpellCycle(out int direction) { direction = 0; return false; }
        public virtual bool ConsumeInventory() => false;
        public virtual bool ConsumePrototypeReset() => false;
        public virtual bool ConsumeCancel() => false;
        public virtual bool ConsumeInventoryUse() => false;
        public virtual bool ConsumeDialogueAdvance() => false;
        public virtual bool ConsumeDialogueCancel() => false;
        public virtual bool ConsumeInventoryNavigate(out Vector2 direction) { direction = Vector2.zero; return false; }
        public virtual void SetGameplayInputBlocked(bool blocked) { }
        public virtual void SetMenuInputBlocked(UnityEngine.Object owner, bool blocked, Action closeRequested = null) { }
        public virtual void SetDefeatedInputBlocked(bool blocked) { }
        public virtual void ClearGameplayActionQueues() { }
        public virtual void ClearCancel() { }
        public virtual void ClearInventoryUiActions() { }
        public virtual void ClearDialogueActions() { }
    }
}
