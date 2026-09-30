using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.UI
{
    /// <summary>
    /// Keeps the direct content roots of a screen-space canvas inside the platform safe area.
    /// Original anchors are retained, so corner HUD elements and stretched menu roots preserve
    /// their authored relationship while adapting to notches and overscan.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GameUiSafeArea : MonoBehaviour
    {
        [Serializable]
        private sealed class TargetLayout
        {
            public RectTransform target;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
        }

        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private List<TargetLayout> targetLayouts = new List<TargetLayout>();

        private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
        private bool applying;

        public int TrackedTargetCount => targetLayouts.Count;

        private void OnEnable()
        {
            ResolveContentRoot();
            SynchronizeTargets();
            ApplyCurrentSafeArea();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            {
                ApplyCurrentSafeArea();
            }
        }

        private void OnTransformChildrenChanged()
        {
            if (applying)
            {
                return;
            }

            SynchronizeTargets();
            ApplyCurrentSafeArea();
        }

        public void ConfigureForCanvas(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                return;
            }

            RectTransform canvasRoot = canvas.transform as RectTransform;
            if (contentRoot != canvasRoot)
            {
                contentRoot = canvasRoot;
                targetLayouts.Clear();
            }

            SynchronizeTargets();
            ApplyCurrentSafeArea();
        }

        [ContextMenu("Capture Current UI Layout")]
        public void CaptureCurrentLayout()
        {
            ResolveContentRoot();
            targetLayouts.Clear();
            SynchronizeTargets();
            ApplyCurrentSafeArea();
        }

        public void ApplySafeArea(Rect safeArea, Vector2Int screenSize)
        {
            ResolveContentRoot();
            SynchronizeTargets();
            applying = true;
            try
            {
                for (int i = targetLayouts.Count - 1; i >= 0; i--)
                {
                    TargetLayout layout = targetLayouts[i];
                    if (layout == null || layout.target == null)
                    {
                        targetLayouts.RemoveAt(i);
                        continue;
                    }

                    CalculateMappedAnchors(
                        safeArea,
                        screenSize,
                        layout.anchorMin,
                        layout.anchorMax,
                        out Vector2 mappedMin,
                        out Vector2 mappedMax);
                    layout.target.anchorMin = mappedMin;
                    layout.target.anchorMax = mappedMax;
                }
            }
            finally
            {
                applying = false;
            }

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
        }

        public static void CalculateMappedAnchors(
            Rect safeArea,
            Vector2Int screenSize,
            Vector2 originalMin,
            Vector2 originalMax,
            out Vector2 mappedMin,
            out Vector2 mappedMax)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                mappedMin = originalMin;
                mappedMax = originalMax;
                return;
            }

            Vector2 safeMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
            Vector2 safeMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
            Vector2 safeSize = safeMax - safeMin;
            mappedMin = safeMin + Vector2.Scale(originalMin, safeSize);
            mappedMax = safeMin + Vector2.Scale(originalMax, safeSize);
        }

        private void ResolveContentRoot()
        {
            if (contentRoot == null)
            {
                contentRoot = transform as RectTransform;
            }
        }

        private void SynchronizeTargets()
        {
            if (contentRoot == null)
            {
                return;
            }

            targetLayouts.RemoveAll(value => value == null || value.target == null || value.target.parent != contentRoot);
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                if (!(contentRoot.GetChild(i) is RectTransform child) || Contains(child))
                {
                    continue;
                }

                targetLayouts.Add(new TargetLayout
                {
                    target = child,
                    anchorMin = child.anchorMin,
                    anchorMax = child.anchorMax
                });
            }
        }

        private bool Contains(RectTransform target)
        {
            for (int i = 0; i < targetLayouts.Count; i++)
            {
                if (targetLayouts[i] != null && targetLayouts[i].target == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyCurrentSafeArea()
        {
            ApplySafeArea(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }
    }
}
