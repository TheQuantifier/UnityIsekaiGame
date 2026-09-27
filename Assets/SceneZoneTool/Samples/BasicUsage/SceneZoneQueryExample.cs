using UnityEngine;

namespace SceneZoneTool.Samples
{
    /// <summary>Minimal example that reports when this object crosses a zone boundary.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SceneZoneQueryExample : MonoBehaviour
    {
        [SerializeField] private SceneZoneLayerAsset layer;
        [SerializeField] private bool logChanges = true;
        [SerializeField] private bool previewInEditMode = true;
        [SerializeField] private bool allowKeyboardMovement = true;
        [SerializeField, Min(0.1f)] private float movementSpeed = 8f;

        private SceneZoneAsset currentZone;
        private SceneZoneLayerAsset lastQueriedLayer;
        private Vector3 lastQueriedPosition;
        private bool hasQueried;
        private bool moveLeft;
        private bool moveRight;
        private bool moveForward;
        private bool moveBackward;
        private Renderer markerRenderer;
        private Material markerMaterial;

        public SceneZoneAsset CurrentZone => currentZone;

        private void OnEnable()
        {
            EnsureMarkerMaterial();
            hasQueried = false;
            QueryCurrentPosition();
        }

        private void Update()
        {
            if (Application.isPlaying && allowKeyboardMovement)
            {
                Vector3 movement = new Vector3(
                    (moveRight ? 1f : 0f) - (moveLeft ? 1f : 0f),
                    0f,
                    (moveForward ? 1f : 0f) - (moveBackward ? 1f : 0f));
                if (movement.sqrMagnitude > 1f) movement.Normalize();
                transform.position += movement * (movementSpeed * Time.deltaTime);
            }

            if (!Application.isPlaying && !previewInEditMode) return;
            if (hasQueried && lastQueriedLayer == layer && lastQueriedPosition == transform.position) return;
            QueryCurrentPosition();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            Event current = Event.current;
            if (current.isKey && (current.type == EventType.KeyDown || current.type == EventType.KeyUp))
            {
                bool pressed = current.type == EventType.KeyDown;
                switch (current.keyCode)
                {
                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        moveLeft = pressed;
                        break;
                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        moveRight = pressed;
                        break;
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                        moveForward = pressed;
                        break;
                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        moveBackward = pressed;
                        break;
                }
            }

            string zoneName = currentZone == null ? "Outside all zones" : currentZone.DisplayName;
            GUI.Box(new Rect(16f, 16f, 300f, 58f), $"WASD / Arrow Keys: move query marker\nCurrent zone: {zoneName}");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ClearMovementInput();
        }

        private void OnDisable()
        {
            ClearMovementInput();
            ReleaseMarkerMaterial();
        }

        private void EnsureMarkerMaterial()
        {
            if (markerRenderer == null) markerRenderer = GetComponentInChildren<Renderer>(true);
            if (markerRenderer == null || markerMaterial != null) return;
            markerMaterial = SceneZoneMaterialFactory.CreateUnlitMaterial(new Color(1f, 0.72f, 0.08f, 1f));
            if (markerMaterial != null) markerRenderer.sharedMaterial = markerMaterial;
        }

        private void ReleaseMarkerMaterial()
        {
            if (markerMaterial == null) return;
            if (markerRenderer != null && markerRenderer.sharedMaterial == markerMaterial)
                markerRenderer.sharedMaterial = null;
            if (Application.isPlaying) Destroy(markerMaterial);
            else DestroyImmediate(markerMaterial);
            markerMaterial = null;
        }

        private void ClearMovementInput()
        {
            moveLeft = false;
            moveRight = false;
            moveForward = false;
            moveBackward = false;
        }

        [ContextMenu("Query Current Position")]
        public void QueryCurrentPosition()
        {
            if (!Application.isPlaying && !previewInEditMode) return;

            lastQueriedLayer = layer;
            lastQueriedPosition = transform.position;
            bool firstQuery = !hasQueried;
            hasQueried = true;

            if (layer == null)
            {
                currentZone = null;
                if (firstQuery && logChanges)
                    Debug.LogWarning("Scene Zone Tool: no zone layer is assigned.", this);
                return;
            }

            SceneZoneAsset next = layer.GetZoneAt(transform.position);
            if (!firstQuery && next == currentZone) return;
            currentZone = next;
            if (logChanges)
                Debug.Log(currentZone == null
                    ? "Scene Zone Tool: outside every zone."
                    : $"Scene Zone Tool: entered {currentZone.DisplayName} ({currentZone.ZoneId}).",
                    this);
        }
    }
}
