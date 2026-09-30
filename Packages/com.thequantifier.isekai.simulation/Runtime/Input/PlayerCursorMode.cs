using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.Input
{
    /// <summary>
    /// Owns the relationship between the operating-system cursor and first-person
    /// mouse look. Menus register as owners so closing one menu cannot relock the
    /// cursor while another menu is still open.
    /// </summary>
    public static class PlayerCursorMode
    {
        private static readonly HashSet<UnityEngine.Object> MenuOwners = new HashSet<UnityEngine.Object>();
        private static readonly List<UnityEngine.Object> MenuOwnerOrder = new List<UnityEngine.Object>();
        private static readonly Dictionary<UnityEngine.Object, Action> MenuCloseRequests = new Dictionary<UnityEngine.Object, Action>();
        private static bool mouseLookEnabled = true;

        public static bool MouseLookEnabled => mouseLookEnabled;
        public static bool HasOpenMenu
        {
            get
            {
                PruneDestroyedOwners();
                return MenuOwners.Count > 0;
            }
        }
        public static bool IsMouseLookActive => mouseLookEnabled && !HasOpenMenu;
        public static int OpenMenuCount
        {
            get
            {
                PruneDestroyedOwners();
                return MenuOwnerOrder.Count;
            }
        }
        public static UnityEngine.Object TopMenuOwner
        {
            get
            {
                PruneDestroyedOwners();
                return MenuOwnerOrder.Count == 0 ? null : MenuOwnerOrder[MenuOwnerOrder.Count - 1];
            }
        }

        public static event Action<bool> MouseLookChanged;
        public static event Action<int> MenuStackChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            MenuOwners.Clear();
            MenuOwnerOrder.Clear();
            MenuCloseRequests.Clear();
            mouseLookEnabled = true;
            MouseLookChanged = null;
            MenuStackChanged = null;
        }

        public static void SetMouseLookEnabled(bool enabled)
        {
            bool changed = mouseLookEnabled != enabled;
            mouseLookEnabled = enabled;
            ApplyCursorState();
            if (changed) MouseLookChanged?.Invoke(mouseLookEnabled);
        }

        public static void ToggleMouseLook()
        {
            SetMouseLookEnabled(!mouseLookEnabled);
        }

        public static void UnlockMouse()
        {
            SetMouseLookEnabled(false);
        }

        public static void SetMenuOpen(UnityEngine.Object owner, bool open, Action closeRequested = null)
        {
            if (owner == null)
            {
                ApplyCursorState();
                return;
            }

            int previousCount = MenuOwnerOrder.Count;
            UnityEngine.Object previousTop = TopMenuOwner;
            if (open)
            {
                MenuOwners.Add(owner);
                MenuOwnerOrder.Remove(owner);
                MenuOwnerOrder.Add(owner);
                if (closeRequested != null)
                {
                    MenuCloseRequests[owner] = closeRequested;
                }
            }
            else
            {
                MenuOwners.Remove(owner);
                MenuOwnerOrder.Remove(owner);
                MenuCloseRequests.Remove(owner);
            }

            ApplyCursorState();
            if (previousCount != MenuOwnerOrder.Count || previousTop != TopMenuOwner)
            {
                MenuStackChanged?.Invoke(MenuOwnerOrder.Count);
            }
        }

        /// <summary>
        /// Sends cancel to the most recently focused menu. A registered menu always consumes the
        /// request, even when it relies on its own input polling, so Escape cannot unlock mouse look
        /// underneath a still-open modal window.
        /// </summary>
        public static bool TryCloseTopMenu()
        {
            PruneDestroyedOwners();
            if (MenuOwnerOrder.Count == 0)
            {
                return false;
            }

            UnityEngine.Object owner = MenuOwnerOrder[MenuOwnerOrder.Count - 1];
            if (owner != null && MenuCloseRequests.TryGetValue(owner, out Action closeRequested))
            {
                closeRequested?.Invoke();
            }

            return true;
        }

        public static void ApplyCursorState()
        {
            bool lockForLook = IsMouseLookActive;
            Cursor.lockState = lockForLook ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockForLook;
        }

        private static void PruneDestroyedOwners()
        {
            MenuOwners.RemoveWhere(owner => owner == null);
            for (int i = MenuOwnerOrder.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object owner = MenuOwnerOrder[i];
                if (owner != null)
                {
                    continue;
                }

                MenuOwnerOrder.RemoveAt(i);
                MenuCloseRequests.Remove(owner);
            }
        }
    }
}
