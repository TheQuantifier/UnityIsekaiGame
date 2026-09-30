using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Networking.Client
{
    /// <summary>
    /// Builds a presentation-only copy of an authored pickup prefab. Gameplay behaviours,
    /// identity registration, and colliders deliberately stay on the authoritative network
    /// pickup rather than being duplicated on the client.
    /// </summary>
    public static class WorldItemPickupVisualFactory
    {
        public static bool TryCreate(ItemDefinition item, Transform parent, out GameObject visual)
        {
            visual = null;
            if (item == null || item.WorldPickupPrefab == null || parent == null)
            {
                return false;
            }

            Transform sourceRoot = item.WorldPickupPrefab.transform;
            var transformMap = new Dictionary<Transform, Transform>();
            visual = new GameObject($"{item.DisplayName} Pickup Presentation");
            visual.transform.SetParent(parent, false);
            CopyRootTransform(sourceRoot, visual.transform, parent);
            transformMap[sourceRoot] = visual.transform;
            CloneChildTransforms(sourceRoot, visual.transform, transformMap);

            int rendererCount = 0;
            foreach (KeyValuePair<Transform, Transform> pair in transformMap)
            {
                CopyMeshFilter(pair.Key.gameObject, pair.Value.gameObject);
                rendererCount += CopyRenderers(pair.Key, pair.Value, transformMap);
                pair.Value.gameObject.SetActive(pair.Key.gameObject.activeSelf);
            }

            if (rendererCount > 0)
            {
                return true;
            }

            Object.Destroy(visual);
            visual = null;
            return false;
        }

        private static void CopyRootTransform(Transform source, Transform target, Transform parent)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            Vector3 parentScale = parent.lossyScale;
            target.localScale = new Vector3(
                SafeDivide(source.localScale.x, parentScale.x),
                SafeDivide(source.localScale.y, parentScale.y),
                SafeDivide(source.localScale.z, parentScale.z));
        }

        private static void CloneChildTransforms(
            Transform sourceParent,
            Transform targetParent,
            IDictionary<Transform, Transform> transformMap)
        {
            for (int i = 0; i < sourceParent.childCount; i++)
            {
                Transform source = sourceParent.GetChild(i);
                var clone = new GameObject(source.name);
                clone.layer = source.gameObject.layer;
                Transform target = clone.transform;
                target.SetParent(targetParent, false);
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
                transformMap[source] = target;
                CloneChildTransforms(source, target, transformMap);
            }
        }

        private static void CopyMeshFilter(GameObject source, GameObject target)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                return;
            }

            target.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
        }

        private static int CopyRenderers(
            Transform source,
            Transform target,
            IReadOnlyDictionary<Transform, Transform> transformMap)
        {
            int count = 0;
            MeshRenderer sourceMesh = source.GetComponent<MeshRenderer>();
            if (sourceMesh != null)
            {
                MeshRenderer clone = target.gameObject.AddComponent<MeshRenderer>();
                CopyRendererSettings(sourceMesh, clone);
                count++;
            }

            SpriteRenderer sourceSprite = source.GetComponent<SpriteRenderer>();
            if (sourceSprite != null)
            {
                SpriteRenderer clone = target.gameObject.AddComponent<SpriteRenderer>();
                CopyRendererSettings(sourceSprite, clone);
                clone.sprite = sourceSprite.sprite;
                clone.color = sourceSprite.color;
                clone.flipX = sourceSprite.flipX;
                clone.flipY = sourceSprite.flipY;
                clone.drawMode = sourceSprite.drawMode;
                clone.size = sourceSprite.size;
                clone.maskInteraction = sourceSprite.maskInteraction;
                count++;
            }

            SkinnedMeshRenderer sourceSkinned = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinned != null)
            {
                SkinnedMeshRenderer clone = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                CopyRendererSettings(sourceSkinned, clone);
                clone.sharedMesh = sourceSkinned.sharedMesh;
                clone.quality = sourceSkinned.quality;
                clone.updateWhenOffscreen = sourceSkinned.updateWhenOffscreen;
                clone.skinnedMotionVectors = sourceSkinned.skinnedMotionVectors;
                clone.localBounds = sourceSkinned.localBounds;
                clone.rootBone = ResolveMappedTransform(sourceSkinned.rootBone, transformMap);
                Transform[] sourceBones = sourceSkinned.bones;
                var mappedBones = new Transform[sourceBones.Length];
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    mappedBones[i] = ResolveMappedTransform(sourceBones[i], transformMap);
                }

                clone.bones = mappedBones;
                count++;
            }

            return count;
        }

        private static void CopyRendererSettings(Renderer source, Renderer target)
        {
            target.enabled = source.enabled;
            target.sharedMaterials = source.sharedMaterials;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.renderingLayerMask = source.renderingLayerMask;
            target.rendererPriority = source.rendererPriority;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
        }

        private static Transform ResolveMappedTransform(
            Transform source,
            IReadOnlyDictionary<Transform, Transform> transformMap)
        {
            return source != null && transformMap.TryGetValue(source, out Transform mapped) ? mapped : null;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= 0.00001f ? value : value / divisor;
        }
    }
}
