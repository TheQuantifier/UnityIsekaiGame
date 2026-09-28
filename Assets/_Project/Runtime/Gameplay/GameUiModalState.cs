using System;
using UnityEngine;

namespace UnityIsekaiGame.Gameplay
{
    /// <summary>
    /// Shared gameplay pause signal for narrative UI that is not represented by a player menu
    /// controller. Menu cursor and cancel ownership live in PlayerCursorMode.
    /// </summary>
    public static class GameUiModalState
    {
        private static bool dialogueActive;
        private static bool contractMenuActive;
        private static bool narrativeActive;

        public static bool DialogueActive => dialogueActive;
        public static bool ContractMenuActive => contractMenuActive;
        public static bool NarrativeActive => narrativeActive;
        public static bool IsModalActive => dialogueActive || contractMenuActive || narrativeActive;

        public static event Action<bool> DialogueActiveChanged;
        public static event Action<bool> ContractMenuActiveChanged;
        public static event Action<bool> NarrativeActiveChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            dialogueActive = false;
            contractMenuActive = false;
            narrativeActive = false;
            DialogueActiveChanged = null;
            ContractMenuActiveChanged = null;
            NarrativeActiveChanged = null;
        }

        public static void SetDialogueActive(bool active)
        {
            if (dialogueActive == active)
            {
                return;
            }

            dialogueActive = active;
            DialogueActiveChanged?.Invoke(dialogueActive);
        }

        public static void SetContractMenuActive(bool active)
        {
            if (contractMenuActive == active)
            {
                return;
            }

            contractMenuActive = active;
            ContractMenuActiveChanged?.Invoke(contractMenuActive);
        }

        public static void SetNarrativeActive(bool active)
        {
            if (narrativeActive == active) return;
            narrativeActive = active;
            NarrativeActiveChanged?.Invoke(narrativeActive);
        }
    }
}
