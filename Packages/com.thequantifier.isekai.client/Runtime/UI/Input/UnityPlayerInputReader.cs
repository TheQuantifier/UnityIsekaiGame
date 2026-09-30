using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.Input
{
    public sealed class UnityPlayerInputReader : PlayerInputReader
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string playerActionMap = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string castPrimarySpellActionName = "CastPrimarySpell";
        [SerializeField] private string selectSpellSlot1ActionName = "SelectSpellSlot1";
        [SerializeField] private string selectSpellSlot2ActionName = "SelectSpellSlot2";
        [SerializeField] private string selectSpellSlot3ActionName = "SelectSpellSlot3";
        [SerializeField] private string selectSpellSlot4ActionName = "SelectSpellSlot4";
        [SerializeField] private string previousSpellActionName = "PreviousSpell";
        [SerializeField] private string nextSpellActionName = "NextSpell";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string inventoryActionName = "Inventory";
        [SerializeField] private string prototypeResetActionName = "PrototypeReset";
        [SerializeField] private string uiActionMap = "UI";
        [SerializeField] private string cancelActionName = "Cancel";
        [SerializeField] private string navigateActionName = "Navigate";
        [SerializeField] private string inventoryUseActionName = "InventoryUse";
        [SerializeField] private string dialogueAdvanceActionName = "DialogueAdvance";

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction attackAction;
        private InputAction castPrimarySpellAction;
        private InputAction selectSpellSlot1Action;
        private InputAction selectSpellSlot2Action;
        private InputAction selectSpellSlot3Action;
        private InputAction selectSpellSlot4Action;
        private InputAction previousSpellAction;
        private InputAction nextSpellAction;
        private InputAction interactAction;
        private InputAction inventoryAction;
        private InputAction prototypeResetAction;
        private InputAction cancelAction;
        private InputAction navigateAction;
        private InputAction inventoryUseAction;
        private InputAction dialogueAdvanceAction;
        private bool jumpQueued;
        private bool attackQueued;
        private bool castPrimarySpellQueued;
        private int queuedSpellSlotSelection = -1;
        private int queuedSpellCycleDirection;
        private bool interactQueued;
        private bool inventoryQueued;
        private bool prototypeResetQueued;
        private bool cancelQueued;
        private bool inventoryUseQueued;
        private bool dialogueAdvanceQueued;
        private Vector2 inventoryNavigateQueued;
        private bool pointerLook;
        private bool gameplayInputBlocked;
        private bool defeatedInputBlocked;
        private readonly HashSet<UnityEngine.Object> menuInputBlockOwners = new HashSet<UnityEngine.Object>();

        public override Vector2 Move => GameplayInputBlocked || moveAction == null ? Vector2.zero : moveAction.ReadValue<Vector2>();
        public override Vector2 Look => GameplayInputBlocked || !PlayerCursorMode.IsMouseLookActive || lookAction == null ? Vector2.zero : lookAction.ReadValue<Vector2>();
        public override bool SprintHeld => !GameplayInputBlocked && sprintAction != null && sprintAction.IsPressed();
        public override bool IsPointerLook => pointerLook;
        public override bool GameplayInputBlocked
        {
            get
            {
                menuInputBlockOwners.RemoveWhere(owner => owner == null);
                return gameplayInputBlocked || defeatedInputBlocked || menuInputBlockOwners.Count > 0;
            }
        }
        public override bool MouseLookEnabled => PlayerCursorMode.MouseLookEnabled;
        public override bool TravelTogglePressedThisFrame => Keyboard.current?.tKey.wasPressedThisFrame == true;
        public override bool ChatTogglePressedThisFrame => Keyboard.current?.slashKey.wasPressedThisFrame == true;
        public override bool ProfessionTogglePressedThisFrame => Keyboard.current?.f7Key.wasPressedThisFrame == true;
        public override bool SocialDebugTogglePressedThisFrame => Keyboard.current?.f8Key.wasPressedThisFrame == true;

        private void Awake()
        {
            if (inputActions == null)
            {
                throw new InvalidOperationException($"{nameof(PlayerInputReader)} requires an InputActionAsset.");
            }

            InputActionMap map = inputActions.FindActionMap(playerActionMap, true);
            moveAction = map.FindAction(moveActionName, true);
            lookAction = map.FindAction(lookActionName, true);
            jumpAction = map.FindAction(jumpActionName, true);
            sprintAction = map.FindAction(sprintActionName, false);
            attackAction = map.FindAction(attackActionName, false);
            castPrimarySpellAction = map.FindAction(castPrimarySpellActionName, false);
            selectSpellSlot1Action = map.FindAction(selectSpellSlot1ActionName, false);
            selectSpellSlot2Action = map.FindAction(selectSpellSlot2ActionName, false);
            selectSpellSlot3Action = map.FindAction(selectSpellSlot3ActionName, false);
            selectSpellSlot4Action = map.FindAction(selectSpellSlot4ActionName, false);
            previousSpellAction = map.FindAction(previousSpellActionName, false);
            nextSpellAction = map.FindAction(nextSpellActionName, false);
            interactAction = map.FindAction(interactActionName, true);
            inventoryAction = map.FindAction(inventoryActionName, true);
            prototypeResetAction = map.FindAction(prototypeResetActionName, false);

            InputActionMap uiMap = inputActions.FindActionMap(uiActionMap, false);
            cancelAction = uiMap?.FindAction(cancelActionName, false);
            navigateAction = uiMap?.FindAction(navigateActionName, false);
            inventoryUseAction = uiMap?.FindAction(inventoryUseActionName, false);
            dialogueAdvanceAction = uiMap?.FindAction(dialogueAdvanceActionName, false);
        }

        private void OnEnable()
        {
            EnableAction(moveAction);
            EnableAction(lookAction);
            EnableAction(jumpAction);
            EnableAction(sprintAction);
            EnableAction(attackAction);
            EnableAction(castPrimarySpellAction);
            EnableAction(selectSpellSlot1Action);
            EnableAction(selectSpellSlot2Action);
            EnableAction(selectSpellSlot3Action);
            EnableAction(selectSpellSlot4Action);
            EnableAction(previousSpellAction);
            EnableAction(nextSpellAction);
            EnableAction(interactAction);
            EnableAction(inventoryAction);
            EnableAction(prototypeResetAction);
            EnableAction(cancelAction);
            EnableAction(navigateAction);
            EnableAction(inventoryUseAction);
            EnableAction(dialogueAdvanceAction);

            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
            }

            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
            }

            if (attackAction != null)
            {
                attackAction.performed += OnAttackPerformed;
            }

            if (castPrimarySpellAction != null)
            {
                castPrimarySpellAction.performed += OnCastPrimarySpellPerformed;
            }

            SubscribeSpellSelectionActions();

            if (inventoryAction != null)
            {
                inventoryAction.performed += OnInventoryPerformed;
            }

            if (prototypeResetAction != null)
            {
                prototypeResetAction.performed += OnPrototypeResetPerformed;
            }

            if (cancelAction != null)
            {
                cancelAction.performed += OnCancelPerformed;
            }

            if (navigateAction != null)
            {
                navigateAction.performed += OnNavigatePerformed;
            }

            if (inventoryUseAction != null)
            {
                inventoryUseAction.performed += OnInventoryUsePerformed;
            }

            if (dialogueAdvanceAction != null)
            {
                dialogueAdvanceAction.performed += OnDialogueAdvancePerformed;
            }

            if (lookAction != null)
            {
                lookAction.performed += OnLookPerformed;
            }

            PlayerCursorMode.ApplyCursorState();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (PlayerCursorMode.TryCloseTopMenu())
                {
                    ClearCancel();
                    return;
                }

                bool wasLockedForLook = PlayerCursorMode.MouseLookEnabled;
                PlayerCursorMode.UnlockMouse();
                if (wasLockedForLook) GameHudMessageBus.Show("Mouse unlocked. Press L to enable mouse look.");
                return;
            }

            if (keyboard.lKey.wasPressedThisFrame && !IsTypingInUiField())
            {
                PlayerCursorMode.ToggleMouseLook();
                GameHudMessageBus.Show(PlayerCursorMode.MouseLookEnabled
                    ? "Mouse look enabled."
                    : "Mouse unlocked. Press L to enable mouse look.");
            }
        }

        private void OnDisable()
        {
            if (jumpAction != null)
            {
                jumpAction.performed -= OnJumpPerformed;
            }

            if (lookAction != null)
            {
                lookAction.performed -= OnLookPerformed;
            }

            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
            }

            if (attackAction != null)
            {
                attackAction.performed -= OnAttackPerformed;
            }

            if (castPrimarySpellAction != null)
            {
                castPrimarySpellAction.performed -= OnCastPrimarySpellPerformed;
            }

            UnsubscribeSpellSelectionActions();

            if (inventoryAction != null)
            {
                inventoryAction.performed -= OnInventoryPerformed;
            }

            if (prototypeResetAction != null)
            {
                prototypeResetAction.performed -= OnPrototypeResetPerformed;
            }

            if (cancelAction != null)
            {
                cancelAction.performed -= OnCancelPerformed;
            }

            if (navigateAction != null)
            {
                navigateAction.performed -= OnNavigatePerformed;
            }

            if (inventoryUseAction != null)
            {
                inventoryUseAction.performed -= OnInventoryUsePerformed;
            }

            if (dialogueAdvanceAction != null)
            {
                dialogueAdvanceAction.performed -= OnDialogueAdvancePerformed;
            }

            DisableAction(dialogueAdvanceAction);
            DisableAction(inventoryUseAction);
            DisableAction(navigateAction);
            DisableAction(cancelAction);
            DisableAction(prototypeResetAction);
            DisableAction(inventoryAction);
            DisableAction(interactAction);
            DisableAction(nextSpellAction);
            DisableAction(previousSpellAction);
            DisableAction(selectSpellSlot4Action);
            DisableAction(selectSpellSlot3Action);
            DisableAction(selectSpellSlot2Action);
            DisableAction(selectSpellSlot1Action);
            DisableAction(castPrimarySpellAction);
            DisableAction(attackAction);
            DisableAction(sprintAction);
            DisableAction(jumpAction);
            DisableAction(lookAction);
            DisableAction(moveAction);
        }

        public override bool ConsumeJump()
        {
            if (GameplayInputBlocked)
            {
                jumpQueued = false;
                return false;
            }

            if (!jumpQueued)
            {
                return false;
            }

            jumpQueued = false;
            return true;
        }

        public override bool JumpPressedThisFrame => !GameplayInputBlocked && jumpAction != null && jumpAction.WasPressedThisFrame();

        public override bool ConsumeInteract()
        {
            if (GameplayInputBlocked)
            {
                interactQueued = false;
                return false;
            }

            if (!interactQueued)
            {
                return false;
            }

            interactQueued = false;
            return true;
        }

        public override bool ConsumeAttack()
        {
            if (GameplayInputBlocked)
            {
                attackQueued = false;
                return false;
            }

            if (!attackQueued)
            {
                return false;
            }

            attackQueued = false;
            return true;
        }

        public override bool ConsumeCastPrimarySpell()
        {
            if (GameplayInputBlocked)
            {
                castPrimarySpellQueued = false;
                return false;
            }

            if (!castPrimarySpellQueued)
            {
                return false;
            }

            castPrimarySpellQueued = false;
            return true;
        }

        public override bool ConsumeSpellSlotSelection(out int slotIndex)
        {
            slotIndex = queuedSpellSlotSelection;
            if (GameplayInputBlocked)
            {
                queuedSpellSlotSelection = -1;
                return false;
            }

            if (queuedSpellSlotSelection < 0)
            {
                return false;
            }

            queuedSpellSlotSelection = -1;
            return true;
        }

        public override bool ConsumeSpellCycle(out int direction)
        {
            direction = queuedSpellCycleDirection;
            if (GameplayInputBlocked)
            {
                queuedSpellCycleDirection = 0;
                return false;
            }

            if (queuedSpellCycleDirection == 0)
            {
                return false;
            }

            queuedSpellCycleDirection = 0;
            return true;
        }

        public override bool ConsumeInventory()
        {
            if (!inventoryQueued)
            {
                return false;
            }

            inventoryQueued = false;
            return true;
        }

        public override bool ConsumePrototypeReset()
        {
            if (gameplayInputBlocked)
            {
                prototypeResetQueued = false;
                return false;
            }

            if (!prototypeResetQueued)
            {
                return false;
            }

            prototypeResetQueued = false;
            return true;
        }

        public override bool ConsumeCancel()
        {
            if (!cancelQueued)
            {
                return false;
            }

            cancelQueued = false;
            return true;
        }

        public override bool ConsumeInventoryUse()
        {
            if (!inventoryUseQueued)
            {
                return false;
            }

            inventoryUseQueued = false;
            return true;
        }

        public override bool ConsumeDialogueAdvance()
        {
            if (!dialogueAdvanceQueued)
            {
                return false;
            }

            dialogueAdvanceQueued = false;
            return true;
        }

        public override bool ConsumeDialogueCancel()
        {
            return ConsumeCancel();
        }

        public override bool ConsumeInventoryNavigate(out Vector2 direction)
        {
            direction = inventoryNavigateQueued;
            if (direction == Vector2.zero)
            {
                return false;
            }

            inventoryNavigateQueued = Vector2.zero;
            return true;
        }

        public override void SetGameplayInputBlocked(bool blocked)
        {
            gameplayInputBlocked = blocked;
            ClearGameplayActionQueues();
        }

        public override void SetMenuInputBlocked(UnityEngine.Object owner, bool blocked, Action closeRequested = null)
        {
            if (owner != null)
            {
                if (blocked) menuInputBlockOwners.Add(owner);
                else menuInputBlockOwners.Remove(owner);
            }

            PlayerCursorMode.SetMenuOpen(owner, blocked, closeRequested);
            ClearGameplayActionQueues();
            ClearInventoryUiActions();
        }

        public override void SetDefeatedInputBlocked(bool blocked)
        {
            defeatedInputBlocked = blocked;
            ClearGameplayActionQueues();
        }

        public override void ClearGameplayActionQueues()
        {
            jumpQueued = false;
            attackQueued = false;
            castPrimarySpellQueued = false;
            queuedSpellSlotSelection = -1;
            queuedSpellCycleDirection = 0;
            interactQueued = false;
        }

        public override void ClearCancel()
        {
            cancelQueued = false;
        }

        public override void ClearInventoryUiActions()
        {
            inventoryUseQueued = false;
            inventoryNavigateQueued = Vector2.zero;
        }

        public override void ClearDialogueActions()
        {
            dialogueAdvanceQueued = false;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            jumpQueued = true;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            interactQueued = true;
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            attackQueued = true;
        }

        private void OnCastPrimarySpellPerformed(InputAction.CallbackContext context)
        {
            if (IsTypingInUiField())
            {
                return;
            }

            castPrimarySpellQueued = true;
        }

        private void OnSelectSpellSlot1Performed(InputAction.CallbackContext context)
        {
            QueueSpellSlotSelection(0);
        }

        private void OnSelectSpellSlot2Performed(InputAction.CallbackContext context)
        {
            QueueSpellSlotSelection(1);
        }

        private void OnSelectSpellSlot3Performed(InputAction.CallbackContext context)
        {
            QueueSpellSlotSelection(2);
        }

        private void OnSelectSpellSlot4Performed(InputAction.CallbackContext context)
        {
            QueueSpellSlotSelection(3);
        }

        private void OnPreviousSpellPerformed(InputAction.CallbackContext context)
        {
            QueueSpellCycle(-1);
        }

        private void OnNextSpellPerformed(InputAction.CallbackContext context)
        {
            QueueSpellCycle(1);
        }

        private void QueueSpellSlotSelection(int slotIndex)
        {
            if (!IsTypingInUiField())
            {
                queuedSpellSlotSelection = slotIndex;
            }
        }

        private void QueueSpellCycle(int direction)
        {
            if (!IsTypingInUiField())
            {
                queuedSpellCycleDirection = direction;
            }
        }

        private void OnInventoryPerformed(InputAction.CallbackContext context)
        {
            inventoryQueued = true;
        }

        private void OnPrototypeResetPerformed(InputAction.CallbackContext context)
        {
            if (IsTypingInUiField())
            {
                return;
            }

            prototypeResetQueued = true;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            cancelQueued = true;
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            inventoryNavigateQueued = context.ReadValue<Vector2>();
        }

        private void OnInventoryUsePerformed(InputAction.CallbackContext context)
        {
            inventoryUseQueued = true;
        }

        private void OnDialogueAdvancePerformed(InputAction.CallbackContext context)
        {
            if (!IsTypingInUiField())
            {
                dialogueAdvanceQueued = true;
            }
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            pointerLook = context.control != null && context.control.device is Pointer;
        }

        private static void EnableAction(InputAction action)
        {
            if (action != null)
            {
                action.Enable();
            }
        }

        private static void DisableAction(InputAction action)
        {
            if (action != null)
            {
                action.Disable();
            }
        }

        private void SubscribeSpellSelectionActions()
        {
            if (selectSpellSlot1Action != null)
            {
                selectSpellSlot1Action.performed += OnSelectSpellSlot1Performed;
            }

            if (selectSpellSlot2Action != null)
            {
                selectSpellSlot2Action.performed += OnSelectSpellSlot2Performed;
            }

            if (selectSpellSlot3Action != null)
            {
                selectSpellSlot3Action.performed += OnSelectSpellSlot3Performed;
            }

            if (selectSpellSlot4Action != null)
            {
                selectSpellSlot4Action.performed += OnSelectSpellSlot4Performed;
            }

            if (previousSpellAction != null)
            {
                previousSpellAction.performed += OnPreviousSpellPerformed;
            }

            if (nextSpellAction != null)
            {
                nextSpellAction.performed += OnNextSpellPerformed;
            }
        }

        private void UnsubscribeSpellSelectionActions()
        {
            if (selectSpellSlot1Action != null)
            {
                selectSpellSlot1Action.performed -= OnSelectSpellSlot1Performed;
            }

            if (selectSpellSlot2Action != null)
            {
                selectSpellSlot2Action.performed -= OnSelectSpellSlot2Performed;
            }

            if (selectSpellSlot3Action != null)
            {
                selectSpellSlot3Action.performed -= OnSelectSpellSlot3Performed;
            }

            if (selectSpellSlot4Action != null)
            {
                selectSpellSlot4Action.performed -= OnSelectSpellSlot4Performed;
            }

            if (previousSpellAction != null)
            {
                previousSpellAction.performed -= OnPreviousSpellPerformed;
            }

            if (nextSpellAction != null)
            {
                nextSpellAction.performed -= OnNextSpellPerformed;
            }
        }

        private static bool IsTypingInUiField()
        {
            if (PrototypeTextChatPanel.IsChatOpen)
            {
                return true;
            }

            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            return selected != null && selected.GetComponent<InputField>() != null;
        }
    }
}

