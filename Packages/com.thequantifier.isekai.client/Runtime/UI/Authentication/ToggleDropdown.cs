using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityIsekaiGame.UI.Authentication
{
    /// <summary>
    /// A standard Unity dropdown whose header toggles the popup in both directions.
    /// Unity's built-in Dropdown only calls Show when its header is clicked.
    /// </summary>
    public sealed class ToggleDropdown : Dropdown
    {
        protected override GameObject CreateBlocker(Canvas rootCanvas)
        {
            GameObject blocker = base.CreateBlocker(rootCanvas);
            DropdownHeaderRaycastFilter filter = blocker.AddComponent<DropdownHeaderRaycastFilter>();
            filter.Configure(transform as RectTransform, rootCanvas);
            return blocker;
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            TogglePopup();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            TogglePopup();
        }

        public override void OnCancel(BaseEventData eventData)
        {
            Hide();
        }

        private void TogglePopup()
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            if (HasVisiblePopup())
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        private bool HasVisiblePopup()
        {
            if (template == null || template.parent == null)
            {
                return false;
            }

            Transform parent = template.parent;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null
                    && child.gameObject.activeInHierarchy
                    && child.name == "Dropdown List")
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Keeps the dropdown's full-screen dismiss blocker from swallowing a second click on the
    /// header itself. All other outside clicks continue to dismiss through Unity's blocker.
    /// </summary>
    internal sealed class DropdownHeaderRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform header;
        private Canvas rootCanvas;

        public void Configure(RectTransform dropdownHeader, Canvas canvas)
        {
            header = dropdownHeader;
            rootCanvas = canvas;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (header == null)
            {
                return true;
            }

            Camera camera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : eventCamera;
            return !RectTransformUtility.RectangleContainsScreenPoint(header, screenPoint, camera);
        }
    }
}
