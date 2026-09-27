using UnityEditor;
using UnityEngine;

namespace SceneZoneTool.Editor
{
    internal sealed class SceneZoneAboutWindow : EditorWindow
    {
        private static readonly Color Accent = new Color(1f, 0.72f, 0.08f, 1f);

        internal static void ShowWindow()
        {
            SceneZoneAboutWindow window = GetWindow<SceneZoneAboutWindow>(true, "About Scene Zone Tool", true);
            window.minSize = new Vector2(480f, 390f);
            window.maxSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            Rect header = EditorGUILayout.GetControlRect(false, 82f);
            EditorGUI.DrawRect(header, new Color(0.09f, 0.11f, 0.15f, 1f));
            EditorGUI.DrawRect(new Rect(header.x, header.yMax - 4f, header.width, 4f), Accent);

            GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };
            GUIStyle subtitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.78f, 0.82f, 0.9f, 1f) }
            };
            GUI.Label(new Rect(header.x + 20f, header.y + 14f, header.width - 40f, 30f), "Scene Zone Tool", title);
            GUI.Label(new Rect(header.x + 21f, header.y + 47f, header.width - 140f, 20f), "Circle and polygon zones, authored directly in the Scene view", subtitle);
            GUI.Label(new Rect(header.xMax - 105f, header.y + 18f, 85f, 24f), $"v{SceneZoneToolInfo.Version}", EditorStyles.whiteLargeLabel);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Authoring at a glance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Draw, name, organize, validate, and query two-dimensional world zones without terrain, colliders, or a render-pipeline dependency. Zone data is stored as reusable Unity assets using world X/Z coordinates.",
                MessageType.None);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Included in 1.0", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("• Circle and polygon authoring with live overlap diagnostics");
                EditorGUILayout.LabelField("• Multi-layer visibility, shared coordinates, redraw, move, and rename tools");
                EditorGUILayout.LabelField("• Stable IDs, scene bindings, runtime queries, and optional line rendering");
                EditorGUILayout.LabelField("• Basic Usage sample plus EditMode and PlayMode validation coverage");
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tool version", SceneZoneToolInfo.Version);
            EditorGUILayout.LabelField("Unity editor", Application.unityVersion);
            EditorGUILayout.LabelField("Platform", Application.platform.ToString());

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Documentation", GUILayout.Height(28f))) SceneZoneToolMenu.OpenDocumentation();
                if (GUILayout.Button("Project Settings", GUILayout.Height(28f))) SceneZoneToolMenu.OpenSettings();
                if (GUILayout.Button("Validate Project", GUILayout.Height(28f))) SceneZoneToolMenu.ValidateProject();
            }
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Scene Zone Tool 1.0 • Editor authoring and runtime query support", EditorStyles.centeredGreyMiniLabel);
        }
    }
}
