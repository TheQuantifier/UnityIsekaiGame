using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.Gameplay
{
    /// <summary>
    /// Shared UI-focus signal for narrative UI that is not represented by a player menu
    /// controller. It prevents conflicting local interactions but never pauses world simulation.
    /// Menu cursor and cancel ownership live in PlayerCursorMode.
    /// </summary>
    public static class GameUiModalState
    {
        private static bool dialogueActive;
        private static bool contractMenuActive;
        private static readonly HashSet<UnityEngine.Object> narrativeOwners = new HashSet<UnityEngine.Object>();

        public static bool DialogueActive => dialogueActive;
        public static bool ContractMenuActive => contractMenuActive;
        public static bool NarrativeActive => PruneAndCountNarrativeOwners() > 0;
        public static bool IsModalActive => dialogueActive || contractMenuActive || NarrativeActive;

        public static event Action<bool> DialogueActiveChanged;
        public static event Action<bool> ContractMenuActiveChanged;
        public static event Action<bool> NarrativeActiveChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            dialogueActive = false;
            contractMenuActive = false;
            narrativeOwners.Clear();
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
            SetNarrativeActive(null, active);
        }

        public static void SetNarrativeActive(UnityEngine.Object owner, bool active)
        {
            bool wasActive = NarrativeActive;
            if (owner == null)
            {
                narrativeOwners.Clear();
                if (active) narrativeOwners.Add(NarrativeFallbackOwner.Instance);
            }
            else if (active)
            {
                narrativeOwners.Add(owner);
            }
            else
            {
                narrativeOwners.Remove(owner);
            }

            bool isActive = NarrativeActive;
            if (wasActive != isActive) NarrativeActiveChanged?.Invoke(isActive);
        }

        private static int PruneAndCountNarrativeOwners()
        {
            narrativeOwners.RemoveWhere(owner => owner == null);
            return narrativeOwners.Count;
        }

        private sealed class NarrativeFallbackOwner : ScriptableObject
        {
            private static NarrativeFallbackOwner instance;
            public static NarrativeFallbackOwner Instance
            {
                get
                {
                    if (instance == null)
                    {
                        instance = CreateInstance<NarrativeFallbackOwner>();
                        instance.hideFlags = HideFlags.HideAndDontSave;
                    }

                    return instance;
                }
            }
        }
    }
}
