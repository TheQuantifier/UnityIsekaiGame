using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SceneZoneTool
{
    /// <summary>Creates transient unlit materials that match the project's active render pipeline.</summary>
    public static class SceneZoneMaterialFactory
    {
        /// <summary>
        /// Creates a non-persistent unlit material for the currently active render pipeline.
        /// The caller owns the returned material and should destroy it when no longer required.
        /// </summary>
        public static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = FindCompatibleUnlitShader();
            if (shader == null)
            {
                Debug.LogError("Scene Zone Tool could not find a compatible unlit shader for the active render pipeline.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = "Scene Zone Runtime Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            ApplyColor(material, color);
            return material;
        }

        /// <summary>Finds an installed unlit shader appropriate for the active render pipeline.</summary>
        public static Shader FindCompatibleUnlitShader()
        {
            string pipelineType = GraphicsSettings.currentRenderPipeline?.GetType().FullName ?? string.Empty;
            if (pipelineType.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
                return FindFirst("HDRP/Unlit")
                    ?? FindFirst("Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit")
                    ?? FindFirst("Sprites/Default", "Unlit/Color");
            if (pipelineType.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
                return FindFirst("Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit")
                    ?? FindFirst("HDRP/Unlit")
                    ?? FindFirst("Sprites/Default", "Unlit/Color");
            return FindFirst("Sprites/Default", "Unlit/Color")
                ?? FindFirst("Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit")
                ?? FindFirst("HDRP/Unlit");
        }

        /// <summary>Applies a color across the common Built-In, URP, and HDRP unlit color properties.</summary>
        public static void ApplyColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static Shader FindFirst(params string[] shaderNames)
        {
            foreach (string shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null && shader.isSupported) return shader;
            }
            return null;
        }
    }
}
