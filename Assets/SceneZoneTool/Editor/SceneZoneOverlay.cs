using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneZoneTool.Editor
{
    /// <summary>
    /// Dockable/floating Scene view controls for the coordinate-only zone authoring tool.
    /// The overlay owns no scene data; all persistent geometry remains in the selected layer asset.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Scene Zone Tools", defaultDisplay = false)]
    public sealed class SceneZoneOverlay : Overlay
    {
        internal const string OverlayId = "SceneZoneTool.SceneZoneOverlay";

        private SceneZoneFreeSelectController controller;

        private SceneZoneFreeSelectController Controller
        {
            get
            {
                if (controller == null) controller = SceneZoneFreeSelectController.CreateOverlayController();
                controller.SetOverlayContext(containerWindow as SceneView, displayed);
                return controller;
            }
        }

        public override void OnCreated()
        {
            base.OnCreated();
            displayedChanged += OnDisplayedChanged;
            if (displayed) Controller.SetOverlayContext(containerWindow as SceneView, true);
        }

        public override void OnWillBeDestroyed()
        {
            displayedChanged -= OnDisplayedChanged;
            if (controller != null)
            {
                controller.SetOverlayContext(null, false);
                Object.DestroyImmediate(controller);
                controller = null;
            }
            base.OnWillBeDestroyed();
        }

        public override VisualElement CreatePanelContent()
        {
            IMGUIContainer container = new IMGUIContainer(DrawToolbar)
            {
                name = "scene-zone-tools"
            };
            container.style.minWidth = 245f;
            container.style.maxWidth = 340f;
            // The overlay sits over the Scene View. Without stopping pointer bubbling, Unity can
            // deliver the toolbar click to duringSceneGui as well, placing a polygon vertex at the
            // toolbar's screen position before the pointer ever enters the scene canvas.
            container.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            container.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            container.RegisterCallback<PointerMoveEvent>(evt => evt.StopPropagation());
            return container;
        }

        private void DrawToolbar() => Controller.DrawOverlayToolbar();

        private void OnDisplayedChanged(bool visible)
        {
            if (visible) Controller.SetOverlayContext(containerWindow as SceneView, true);
            else controller?.SetOverlayContext(null, false);
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Scene Zone Tools/Open Authoring Overlay", false, 120)]
        private static void OpenFromToolsMenu()
        {
            if (!TryGetTargetOverlay(out SceneView sceneView, out SceneZoneOverlay overlay)) return;
            if (!overlay.EnsureFirstRunSetup()) return;
            ShowOverlay(sceneView, overlay);
        }

        [MenuItem("Tools/Scene Zone Tools/Reset Authoring Overlay Position", false, 121)]
        private static void ResetAuthoringOverlayPosition()
        {
            if (!TryGetTargetOverlay(out SceneView sceneView, out SceneZoneOverlay overlay)) return;
            if (!overlay.EnsureFirstRunSetup()) return;
            sceneView.overlayCanvas.ResetOverlay(overlay);
            ShowOverlay(sceneView, overlay);
            sceneView.ShowNotification(new GUIContent("Scene Zone Tools overlay position reset."));
        }

        internal static void Open(SceneZoneLayerAsset layer)
        {
            if (!TryGetTargetOverlay(out SceneView sceneView, out SceneZoneOverlay overlay)) return;
            if (layer != null) overlay.Controller.UseLayer(layer);
            else if (!overlay.EnsureFirstRunSetup()) return;
            ShowOverlay(sceneView, overlay);
        }

        private bool EnsureFirstRunSetup()
        {
            SceneZoneLayerAsset preferredLayer = SceneZoneToolProjectSettings.instance.LoadOrDiscoverLayerForActiveScene();
            if (preferredLayer != null)
            {
                Controller.UseLayer(preferredLayer);
                return true;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Scene Zone Tools Setup",
                "Choose where this scene's zone data should be stored. Create a new zones.asset file or select an existing Scene Zone layer.",
                "Create New",
                "Use Existing",
                "Cancel");

            return choice switch
            {
                0 => Controller.PromptCreateLayer(),
                1 => Controller.PromptSelectExistingLayer(),
                _ => false
            };
        }

        private static bool TryGetTargetOverlay(out SceneView sceneView, out SceneZoneOverlay zoneOverlay)
        {
            sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            zoneOverlay = null;
            if (!sceneView.TryGetOverlay(OverlayId, out Overlay overlay)) return false;
            zoneOverlay = overlay as SceneZoneOverlay;
            return zoneOverlay != null;
        }

        private static void ShowOverlay(SceneView sceneView, SceneZoneOverlay overlay)
        {
            overlay.displayed = true;
            overlay.collapsed = false;
            overlay.Controller.SetOverlayContext(sceneView, true);
            sceneView.Repaint();
            sceneView.Focus();
        }
    }
}
