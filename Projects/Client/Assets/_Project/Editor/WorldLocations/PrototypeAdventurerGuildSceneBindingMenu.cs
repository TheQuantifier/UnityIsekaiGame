#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Editor.PrototypeIntegration;

namespace UnityIsekaiGame.Editor.WorldLocations
{
    public static class PrototypeAdventurerGuildSceneBindingMenu
    {
        [Obsolete("Use Tools/Project Maintenance/Phase 2 Prototype Integration/Apply Prototype Scene Integration.")]
        [MenuItem("Tools/World Locations/Prototype Scene/Adventurer Guild/Apply Step 14 Scene Bindings")]
        public static void ApplyToPrototypeSceneMenu()
        {
            PrototypeScenePhase2IntegrationMenu.ApplyPrototypeSceneIntegration();
            Debug.Log("Legacy Adventurer Guild binding menu delegated to Phase 2 Prototype Integration.");
        }

        [Obsolete("Use Tools/Project Maintenance/Phase 2 Prototype Integration/Apply Prototype Scene Integration.")]
        public static void ApplyToPrefabAndScene()
        {
            ApplyToPrototypeSceneMenu();
        }

        [Obsolete("Use Tools/Project Maintenance/Phase 2 Prototype Integration/Apply Prototype Scene Integration.")]
        public static int ApplyToPrototypeScene()
        {
            PrototypeScenePhase2IntegrationMenu.ApplyPrototypeSceneIntegration();
            return 0;
        }

        [Obsolete("Use Tools/Project Maintenance/Phase 2 Prototype Integration/Validate Prototype Scene Integration.")]
        [MenuItem("Tools/World Locations/Prototype Scene/Adventurer Guild/Validate Step 14 Scene Bindings")]
        public static void ValidatePrototypeSceneBindings()
        {
            PrototypeScenePhase2IntegrationMenu.ValidatePrototypeSceneIntegrationMenu();
        }
    }
}
#endif
