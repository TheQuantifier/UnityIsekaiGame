using System.Collections.Generic;
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

        public override void OnCreated()
        {
            base.OnCreated();
            displayedChanged += OnDisplayedChanged;
            SceneZoneOverlayController.SetActive(this, displayed);
        }

        public override void OnWillBeDestroyed()
        {
            displayedChanged -= OnDisplayedChanged;
            SceneZoneOverlayController.SetActive(this, false);
            base.OnWillBeDestroyed();
        }

        public override VisualElement CreatePanelContent()
        {
            IMGUIContainer container = new IMGUIContainer(SceneZoneOverlayController.DrawToolbar)
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

        private void OnDisplayedChanged(bool visible) => SceneZoneOverlayController.SetActive(this, visible);

        [MenuItem("Tools/Scene Zone Tools", false, 120)]
        private static void OpenFromToolsMenu()
        {
            if (!SceneZoneOverlayController.EnsureFirstRunSetup()) return;
            ShowOverlay();
        }

        internal static void Open(SceneZoneLayerAsset layer)
        {
            if (layer != null) SceneZoneOverlayController.UseLayer(layer);
            ShowOverlay();
        }

        private static void ShowOverlay()
        {
            SceneView sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            if (sceneView.TryGetOverlay(OverlayId, out Overlay overlay))
            {
                overlay.displayed = true;
                overlay.collapsed = false;
                sceneView.Focus();
            }
        }
    }

    internal static class SceneZoneOverlayController
    {
        private static SceneZoneFreeSelectController controller;
        private static readonly HashSet<SceneView> ActiveSceneViews = new HashSet<SceneView>();

        private static SceneZoneFreeSelectController Controller
        {
            get
            {
                if (controller == null) controller = SceneZoneFreeSelectController.CreateOverlayController();
                return controller;
            }
        }

        internal static void DrawToolbar() => Controller.DrawOverlayToolbar();

        internal static void SetActive(SceneZoneOverlay overlay, bool active)
        {
            if (overlay == null) return;
            SceneView sceneView = overlay.containerWindow as SceneView;
            if (sceneView == null) return;
            if (active) ActiveSceneViews.Add(sceneView);
            else ActiveSceneViews.Remove(sceneView);
            controller?.HandleOverlayVisibilityChanged(ActiveSceneViews.Count > 0);
            SceneView.RepaintAll();
        }

        internal static bool IsActiveFor(SceneView sceneView) => sceneView != null && ActiveSceneViews.Contains(sceneView);

        internal static bool EnsureFirstRunSetup()
        {
            SceneZoneLayerAsset preferredLayer = SceneZoneToolProjectSettings.instance.LoadLayerForActiveScene();
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

            bool configured = choice switch
            {
                0 => Controller.PromptCreateLayer(),
                1 => Controller.PromptSelectExistingLayer(),
                _ => false
            };
            return configured;
        }

        internal static void UseLayer(SceneZoneLayerAsset layer)
        {
            Controller.UseLayer(layer);
            SceneView.RepaintAll();
        }
    }
}
