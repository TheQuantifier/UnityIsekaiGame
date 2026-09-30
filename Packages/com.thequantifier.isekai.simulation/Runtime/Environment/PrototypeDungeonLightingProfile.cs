using UnityEngine;
using UnityEngine.Rendering;

namespace UnityIsekaiGame.WorldEnvironment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PrototypeDungeonLightingProfile : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private bool enforceDungeonShadows = true;
        [SerializeField] private Color surfaceColorMultiplier = new(0.32f, 0.30f, 0.26f, 1f);

        private MaterialPropertyBlock propertyBlock;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply Dungeon Lighting Profile")]
        public void Apply()
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            foreach (var renderer in renderers)
            {
                if (!ShouldProfileRenderer(renderer))
                {
                    continue;
                }

                if (enforceDungeonShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                ApplyMaterialBlocks(renderer);
            }
        }

        [ContextMenu("Clear Dungeon Lighting Profile")]
        public void Clear()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!ShouldProfileRenderer(renderer))
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    renderer.SetPropertyBlock(null, i);
                }
            }
        }

        private void ApplyMaterialBlocks(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                {
                    continue;
                }

                propertyBlock ??= new MaterialPropertyBlock();
                propertyBlock.Clear();
                var colorProperty = ResolveColorProperty(material);
                if (colorProperty == 0)
                {
                    continue;
                }

                var baseColor = material.GetColor(colorProperty);
                propertyBlock.SetColor(colorProperty, Multiply(baseColor, surfaceColorMultiplier));
                renderer.SetPropertyBlock(propertyBlock, i);
            }
        }

        private static bool ShouldProfileRenderer(Renderer renderer)
        {
            return renderer is MeshRenderer or SkinnedMeshRenderer;
        }

        private static int ResolveColorProperty(Material material)
        {
            if (material.HasProperty(BaseColorId))
            {
                return BaseColorId;
            }

            return material.HasProperty(ColorId) ? ColorId : 0;
        }

        private static Color Multiply(Color left, Color right)
        {
            return new Color(
                left.r * right.r,
                left.g * right.g,
                left.b * right.b,
                left.a * right.a);
        }
    }
}
