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

        public static event Action<bool> MouseLookChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            MenuOwners.Clear();
            mouseLookEnabled = true;
            MouseLookChanged = null;
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

        public static void SetMenuOpen(UnityEngine.Object owner, bool open)
        {
            if (owner == null)
            {
                ApplyCursorState();
                return;
            }

            if (open) MenuOwners.Add(owner);
            else MenuOwners.Remove(owner);
            ApplyCursorState();
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
        }
    }
}
