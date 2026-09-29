using UnityEngine;

namespace UnityIsekaiGame.Interaction
{
    [RequireComponent(typeof(Renderer))]
    public sealed class PrototypeToggleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string inactivePrompt = "Press Interact";
        [SerializeField] private string activePrompt = "Press Interact again";
        [SerializeField] private Color inactiveColor = new Color(0.25f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 0.9f, 0.45f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isActive;

        public string InteractionPrompt => isActive ? activePrompt : inactivePrompt;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            ApplyColor();
        }

        public bool CanInteract(in InteractionContext context)
        {
            return enabled && isActiveAndEnabled;
        }

        public void Interact(in InteractionContext context)
        {
            isActive = !isActive;
            ApplyColor();
            Debug.Log($"{name} interaction toggled {(isActive ? "on" : "off")}.");
        }

        private void ApplyColor()
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, isActive ? activeColor : inactiveColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
