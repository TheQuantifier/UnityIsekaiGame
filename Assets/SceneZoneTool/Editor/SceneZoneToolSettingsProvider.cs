using UnityEditor;
using UnityEngine;

namespace SceneZoneTool.Editor
{
    internal sealed class SceneZoneToolSettingsProvider : SettingsProvider
    {
        private string saveFolder;
        private float displayHeight;
        private bool allowDrag;
        private float dragSpacing;

        private SceneZoneToolSettingsProvider(string path, SettingsScope scope) : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SceneZoneToolSettingsProvider("Project/Scene Zone Tool", SettingsScope.Project)
            {
                label = "Scene Zone Tool",
                keywords = new[] { "zone", "layer", "polygon", "circle", "scene", "map" }
            };
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            SceneZoneToolProjectSettings settings = SceneZoneToolProjectSettings.instance;
            saveFolder = settings.DefaultSaveFolder;
            displayHeight = settings.DefaultDisplayHeight;
            allowDrag = settings.AllowDragDrawing;
            dragSpacing = settings.DragPointSpacing;
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.LabelField("Authoring Defaults", EditorStyles.boldLabel);
            saveFolder = EditorGUILayout.TextField(new GUIContent("Save Folder", "Project-relative folder used by Create New Layer."), saveFolder);
            displayHeight = EditorGUILayout.FloatField(new GUIContent("Display Height", "Editor-only Y plane used for handles and previews."), displayHeight);
            allowDrag = EditorGUILayout.Toggle(new GUIContent("Allow Drag Drawing", "Allow freehand-style polygon point placement while dragging."), allowDrag);
            using (new EditorGUI.DisabledScope(!allowDrag))
                dragSpacing = Mathf.Max(0.25f, EditorGUILayout.FloatField("Drag Point Spacing", dragSpacing));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Coordinates are always saved as world X/Z values rounded to three decimal places. Display Height is never serialized into zone geometry.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Settings"))
                    SceneZoneToolProjectSettings.instance.SetAuthoringDefaults(saveFolder, displayHeight, allowDrag, dragSpacing);
                if (GUILayout.Button("Repair Scene Bindings"))
                    SceneZoneToolProjectSettings.instance.RepairMissingReferences();
            }
        }
    }
}
