using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Editor
{
    [CustomEditor(typeof(ItemDefinition))]
    public sealed class ItemDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script") GUI.enabled = false;
                if (iterator.name != "equipment") EditorGUILayout.PropertyField(iterator, true);
                GUI.enabled = true;
            }
            ItemDefinition item = (ItemDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capabilities", EditorStyles.boldLabel);
            if (!item.IsEquippable)
            {
                serializedObject.ApplyModifiedProperties();
                if (GUILayout.Button("Add Equipment Capability"))
                {
                    Change(item, item.EnableEquipmentCapability);
                    GUIUtility.ExitGUI();
                }
                return;
            }

            SerializedProperty equipment = serializedObject.FindProperty("equipment");
            EditorGUILayout.PropertyField(equipment.FindPropertyRelative("slotType"));
            EditorGUILayout.PropertyField(equipment.FindPropertyRelative("statModifiers"), true);
            EditorGUILayout.PropertyField(equipment.FindPropertyRelative("resistanceModifiers"), true);
            EditorGUILayout.PropertyField(equipment.FindPropertyRelative("view"), true);

            bool hasMelee = item.Equipment.MeleeWeapon?.IsWeapon == true;
            bool hasRanged = item.Equipment.RangedWeapon?.IsWeapon == true;
            if (hasMelee)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(equipment.FindPropertyRelative("meleeWeapon"), true);
            }
            if (hasRanged)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(equipment.FindPropertyRelative("rangedWeapon"), true);
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            System.Action capabilityChange = null;
            EditorGUILayout.BeginHorizontal();
            if (!hasMelee && GUILayout.Button("Add Melee Weapon"))
            {
                capabilityChange = item.EnableMeleeWeaponCapability;
            }
            if (hasMelee && GUILayout.Button("Remove Melee Weapon"))
            {
                capabilityChange = item.RemoveMeleeWeaponCapability;
            }
            if (!hasRanged && GUILayout.Button("Add Ranged Weapon"))
            {
                capabilityChange = item.EnableRangedWeaponCapability;
            }
            if (hasRanged && GUILayout.Button("Remove Ranged Weapon"))
            {
                capabilityChange = item.RemoveRangedWeaponCapability;
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Remove Equipment Capability"))
            {
                capabilityChange = item.RemoveEquipmentCapability;
            }
            if (capabilityChange != null)
            {
                Change(item, capabilityChange);
                GUIUtility.ExitGUI();
            }
        }

        private static void Change(ItemDefinition item, System.Action change)
        {
            Undo.RecordObject(item, "Change Item Capability");
            change();
            EditorUtility.SetDirty(item);
        }
    }
}
