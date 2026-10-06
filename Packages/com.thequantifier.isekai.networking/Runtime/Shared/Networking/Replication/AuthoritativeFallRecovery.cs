using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    /// <summary>
    /// Resolves a server-authoritative player position back onto solid world collision.
    /// Recovery is deliberately rare, so exhaustive physics queries are preferred over
    /// accepting an unsafe position or relying on a client-provided fallback.
    /// </summary>
    public static class AuthoritativeFallRecovery
    {
        public const float DefaultNearbySearchRadius = 10f;

        private const float ProbeStartPadding = 0.1f;
        private const float SurfaceTolerance = 0.001f;
        private const float WorldProbeLimit = 10000f;
        private const float GroundSeparation = 0.02f;
        private const float NearbySampleSpacing = 1f;

        public static bool TryResolve(
            Vector3 invalidPosition,
            Transform playerRoot,
            float nearbySearchRadius,
            out Vector3 recoveredPosition,
            out string reason)
        {
            recoveredPosition = invalidPosition;
            reason = string.Empty;
            if (!IsFinite(invalidPosition))
            {
                reason = "InvalidPosition";
                return false;
            }

            float rootClearance = CalculateRootGroundClearance(playerRoot);
            if (TryFindSurfaceBelow(invalidPosition, playerRoot, out RaycastHit below))
            {
                recoveredPosition = PositionOnSurface(invalidPosition, below.point.y, rootClearance);
                reason = $"SurfaceBelow:{below.collider.name}";
                return true;
            }

            if (TryFindSurfaceAbove(invalidPosition, playerRoot, out RaycastHit above))
            {
                recoveredPosition = PositionOnSurface(invalidPosition, above.point.y, rootClearance);
                reason = $"SurfaceAbove:{above.collider.name}";
                return true;
            }

            float radius = Mathf.Max(0f, nearbySearchRadius);
            if (radius > SurfaceTolerance
                && TryFindNearbySurface(invalidPosition, playerRoot, radius, rootClearance,
                    out recoveredPosition, out RaycastHit nearby))
            {
                Vector2 offset = new Vector2(
                    recoveredPosition.x - invalidPosition.x,
                    recoveredPosition.z - invalidPosition.z);
                reason = $"NearbySurface:{nearby.collider.name}:Offset={offset.magnitude:F2}m";
                return true;
            }

            reason = $"NoSolidSurfaceWithin{radius:F1}m";
            return false;
        }

        public static float CalculateRootGroundClearance(Transform playerRoot)
        {
            if (playerRoot != null && playerRoot.TryGetComponent(out CharacterController controller))
            {
                // Do not assume that the controller is centered on the root. The prior
                // height/2-center shortcut also clamped legitimate negative offsets and
                // placed actors at the wrong height when a prefab used an offset capsule.
                Vector3 localBottom = controller.center - Vector3.up * (controller.height * 0.5f);
                float worldBottomY = playerRoot.TransformPoint(localBottom).y;
                return playerRoot.position.y - worldBottomY + GroundSeparation;
            }

            return GroundSeparation;
        }

        private static bool TryFindNearbySurface(
            Vector3 origin,
            Transform ignoredRoot,
            float radius,
            float rootClearance,
            out Vector3 recoveredPosition,
            out RaycastHit surface)
        {
            recoveredPosition = origin;
            surface = default;
            bool found = false;
            float bestHorizontalDistance = float.PositiveInfinity;
            float bestVerticalDistance = float.PositiveInfinity;

            // One-metre concentric sampling covers the requested ten-metre neighbourhood
            // without performing this comparatively expensive search during normal motion.
            int ringCount = Mathf.Max(1, Mathf.CeilToInt(radius / NearbySampleSpacing));
            for (int ring = 1; ring <= ringCount; ring++)
            {
                float ringRadius = Mathf.Min(radius, ring * NearbySampleSpacing);
                int sampleCount = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * ringRadius / NearbySampleSpacing));
                for (int sample = 0; sample < sampleCount; sample++)
                {
                    float radians = sample * (2f * Mathf.PI / sampleCount);
                    var column = new Vector3(
                        origin.x + Mathf.Cos(radians) * ringRadius,
                        origin.y,
                        origin.z + Mathf.Sin(radians) * ringRadius);
                    if (!TryFindNearestSurfaceInColumn(column, ignoredRoot, rootClearance, out RaycastHit candidate, out float candidateRootY))
                    {
                        continue;
                    }

                    float horizontalDistance = new Vector2(column.x - origin.x, column.z - origin.z).magnitude;
                    float verticalDistance = Mathf.Abs(candidateRootY - origin.y);
                    if (found
                        && (horizontalDistance > bestHorizontalDistance + SurfaceTolerance
                            || (Mathf.Abs(horizontalDistance - bestHorizontalDistance) <= SurfaceTolerance
                                && verticalDistance >= bestVerticalDistance)))
                    {
                        continue;
                    }

                    found = true;
                    bestHorizontalDistance = horizontalDistance;
                    bestVerticalDistance = verticalDistance;
                    surface = candidate;
                    recoveredPosition = new Vector3(column.x, candidateRootY, column.z);
                }

                // Rings are searched from the player outward. Once a ring supplies a solid
                // column, a farther ring cannot be the safer horizontal correction.
                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindSurfaceBelow(Vector3 position, Transform ignoredRoot, out RaycastHit nearest)
        {
            nearest = default;
            float startY = Mathf.Min(WorldProbeLimit, position.y + ProbeStartPadding);
            float distance = startY + WorldProbeLimit;
            if (distance <= 0f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                new Vector3(position.x, startY, position.z),
                Vector3.down,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            float highestY = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsUsableSurface(hit.collider, ignoredRoot)
                    || hit.point.y > position.y + SurfaceTolerance
                    || (found && hit.point.y <= highestY))
                {
                    continue;
                }

                found = true;
                highestY = hit.point.y;
                nearest = hit;
            }

            return found;
        }

        private static bool TryFindSurfaceAbove(Vector3 position, Transform ignoredRoot, out RaycastHit nearest)
        {
            nearest = default;
            if (!TryGetColumnHits(position.x, position.z, out RaycastHit[] hits))
            {
                return false;
            }

            bool found = false;
            float lowestY = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsUsableSurface(hit.collider, ignoredRoot)
                    || hit.point.y <= position.y + SurfaceTolerance
                    || (found && hit.point.y >= lowestY))
                {
                    continue;
                }

                found = true;
                lowestY = hit.point.y;
                nearest = hit;
            }

            return found;
        }

        private static bool TryFindNearestSurfaceInColumn(
            Vector3 position,
            Transform ignoredRoot,
            float rootClearance,
            out RaycastHit nearest,
            out float nearestRootY)
        {
            nearest = default;
            nearestRootY = position.y;
            if (!TryGetColumnHits(position.x, position.z, out RaycastHit[] hits))
            {
                return false;
            }

            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsUsableSurface(hit.collider, ignoredRoot))
                {
                    continue;
                }

                float candidateRootY = hit.point.y + rootClearance;
                float distance = Mathf.Abs(candidateRootY - position.y);
                if (found && distance >= nearestDistance)
                {
                    continue;
                }

                found = true;
                nearestDistance = distance;
                nearestRootY = candidateRootY;
                nearest = hit;
            }

            return found;
        }

        private static bool TryGetColumnHits(float x, float z, out RaycastHit[] hits)
        {
            hits = Physics.RaycastAll(
                new Vector3(x, WorldProbeLimit, z),
                Vector3.down,
                WorldProbeLimit * 2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            return hits.Length > 0;
        }

        private static Vector3 PositionOnSurface(Vector3 source, float surfaceY, float rootClearance)
        {
            return new Vector3(source.x, surfaceY + rootClearance, source.z);
        }

        private static bool IsUsableSurface(Collider candidate, Transform ignoredRoot)
        {
            return candidate != null
                && !candidate.isTrigger
                && !(candidate is CharacterController)
                && (ignoredRoot == null
                    || (candidate.transform != ignoredRoot && !candidate.transform.IsChildOf(ignoredRoot)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
