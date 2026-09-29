using UnityEngine;

namespace UnityIsekaiGame.Persistence
{
    public static class SpawnGroundingUtility
    {
        private const float ProbeStartPadding = 0.05f;
        private const float GroundingTolerance = 0.001f;
        private const float WorldProbeLimit = 10000f;
        private const float FallbackClearance = 0.05f;

        public static bool HasSolidColliderBelow(Vector3 position, Transform ignoredRoot)
        {
            if (!IsFinite(position))
            {
                return false;
            }

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
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (IsUsableGroundHit(hitCollider, ignoredRoot) && hits[i].point.y <= position.y + GroundingTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGroundWhenUnsupported(Vector3 position, Transform targetRoot, out Vector3 groundedPosition, out string reason)
        {
            groundedPosition = position;
            reason = string.Empty;

            if (TryFindNearestSolidSurfaceAbove(position, targetRoot, out RaycastHit raisedSurface))
            {
                groundedPosition = new Vector3(
                    position.x,
                    raisedSurface.point.y + CalculateRootGroundClearance(targetRoot),
                    position.z);
                reason = $"GroundedToRaisedCollider:{raisedSurface.collider.name}";
                return true;
            }

            if (HasSolidColliderBelow(position, targetRoot))
            {
                return true;
            }

            if (!TryFindLowestSolidSurface(position, targetRoot, out RaycastHit surface))
            {
                reason = "NoColliderAtSpawnColumn";
                return false;
            }

            groundedPosition = new Vector3(
                position.x,
                surface.point.y + CalculateRootGroundClearance(targetRoot),
                position.z);
            reason = $"GroundedToLowestCollider:{surface.collider.name}";
            return true;
        }

        public static bool TrySnapToNearestSolidSurface(Vector3 position, Transform targetRoot, out Vector3 groundedPosition, out string reason)
        {
            groundedPosition = position;
            reason = string.Empty;

            if (!TryFindNearestSolidSurface(position, targetRoot, out RaycastHit surface, out float desiredY))
            {
                reason = "NoColliderAtSpawnColumn";
                return false;
            }

            groundedPosition = new Vector3(position.x, desiredY, position.z);
            reason = $"SnappedToCollider:{surface.collider.name}";
            return true;
        }

        public static float CalculateRootGroundClearance(Transform targetRoot)
        {
            if (targetRoot != null && targetRoot.TryGetComponent(out CharacterController controller))
            {
                return Mathf.Max(FallbackClearance, (controller.height * 0.5f) - controller.center.y + controller.skinWidth);
            }

            return FallbackClearance;
        }

        private static bool TryFindLowestSolidSurface(Vector3 position, Transform ignoredRoot, out RaycastHit lowest)
        {
            lowest = default;
            if (!IsFinite(position))
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                new Vector3(position.x, WorldProbeLimit, position.z),
                Vector3.down,
                WorldProbeLimit * 2f,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float lowestY = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsUsableGroundHit(hit.collider, ignoredRoot))
                {
                    continue;
                }

                if (!found || hit.point.y < lowestY)
                {
                    found = true;
                    lowestY = hit.point.y;
                    lowest = hit;
                }
            }

            return found;
        }

        private static bool TryFindNearestSolidSurface(Vector3 position, Transform ignoredRoot, out RaycastHit nearest, out float desiredRootY)
        {
            nearest = default;
            desiredRootY = position.y;
            if (!IsFinite(position))
            {
                return false;
            }

            float clearance = CalculateRootGroundClearance(ignoredRoot);
            RaycastHit[] hits = Physics.RaycastAll(
                new Vector3(position.x, WorldProbeLimit, position.z),
                Vector3.down,
                WorldProbeLimit * 2f,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsUsableGroundHit(hit.collider, ignoredRoot))
                {
                    continue;
                }

                float candidateRootY = hit.point.y + clearance;
                float distance = Mathf.Abs(candidateRootY - position.y);
                if (!found || distance < nearestDistance)
                {
                    found = true;
                    nearestDistance = distance;
                    desiredRootY = candidateRootY;
                    nearest = hit;
                }
            }

            return found;
        }

        private static bool TryFindNearestSolidSurfaceAbove(Vector3 position, Transform ignoredRoot, out RaycastHit nearest)
        {
            nearest = default;
            if (!IsFinite(position))
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                new Vector3(position.x, WorldProbeLimit, position.z),
                Vector3.down,
                WorldProbeLimit * 2f,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearestY = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsUsableGroundHit(hit.collider, ignoredRoot)
                    || hit.point.y <= position.y + GroundingTolerance)
                {
                    continue;
                }

                if (!found || hit.point.y < nearestY)
                {
                    found = true;
                    nearestY = hit.point.y;
                    nearest = hit;
                }
            }

            return found;
        }

        private static bool IsUsableGroundHit(Collider candidate, Transform ignoredRoot)
        {
            return candidate != null
                && !candidate.isTrigger
                && (ignoredRoot == null || (candidate.transform != ignoredRoot && !candidate.transform.IsChildOf(ignoredRoot)));
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
