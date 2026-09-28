using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class PrototypeEnemyPatrolController : MonoBehaviour
    {
        [SerializeField] private PrototypeEnemyController enemyController;
        [SerializeField] private EnemyHealth health;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform[] waypoints = System.Array.Empty<Transform>();
        [SerializeField, Min(0f)] private float moveSpeed = 1.25f;
        [SerializeField, Min(0.05f)] private float waypointRadius = 0.35f;
        [SerializeField, Min(0f)] private float waypointWaitSeconds = 0.4f;
        [SerializeField, Min(0f)] private float turnSpeed = 10f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool snapToGround = true;

        private int waypointIndex;
        private float waitRemaining;

        public int WaypointCount => waypoints == null ? 0 : waypoints.Length;
        public int CurrentWaypointIndex => waypointIndex;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            waypointRadius = Mathf.Max(0.05f, waypointRadius);
            waypointWaitSeconds = Mathf.Max(0f, waypointWaitSeconds);
            turnSpeed = Mathf.Max(0f, turnSpeed);
        }

        private void Update()
        {
            if (GameUiModalState.IsModalActive
                || health != null && health.IsDefeated
                || enemyController != null && enemyController.IsTargetWithinDetectionRadius())
            {
                return;
            }

            Transform waypoint = GetCurrentWaypoint();
            if (waypoint == null || moveSpeed <= 0f)
            {
                return;
            }

            if (waitRemaining > 0f)
            {
                waitRemaining = Mathf.Max(0f, waitRemaining - Time.deltaTime);
                return;
            }

            Vector3 toWaypoint = waypoint.position - transform.position;
            toWaypoint.y = 0f;
            float distance = toWaypoint.magnitude;
            if (distance <= waypointRadius)
            {
                AdvanceWaypoint();
                return;
            }

            FaceDirection(toWaypoint);

            Vector3 direction = toWaypoint.normalized;
            float step = Mathf.Min(moveSpeed * Time.deltaTime, Mathf.Max(0f, distance - waypointRadius));
            Move(direction * step);
            SnapToGround();
        }

        public void SetWaypoints(Transform[] patrolWaypoints)
        {
            waypoints = patrolWaypoints ?? System.Array.Empty<Transform>();
            waypointIndex = Mathf.Clamp(waypointIndex, 0, Mathf.Max(0, WaypointCount - 1));
        }

        public void ResetPatrol()
        {
            waypointIndex = 0;
            waitRemaining = 0f;
            SnapToGround();
        }

        private void ResolveReferences()
        {
            if (enemyController == null)
            {
                enemyController = GetComponent<PrototypeEnemyController>();
            }

            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        private Transform GetCurrentWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return null;
            }

            if (waypointIndex < 0 || waypointIndex >= waypoints.Length)
            {
                waypointIndex = 0;
            }

            return waypoints[waypointIndex];
        }

        private void AdvanceWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            if (waypointIndex < waypoints.Length - 1)
            {
                waypointIndex++;
            }
            else if (loop)
            {
                waypointIndex = 0;
            }

            waitRemaining = waypointWaitSeconds;
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = turnSpeed <= 0f
                ? targetRotation
                : Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private bool SnapToGround()
        {
            if (!snapToGround)
            {
                return false;
            }

            if (!SpawnGroundingUtility.TrySnapToNearestSolidSurface(transform.position, transform, out Vector3 groundedPosition, out _))
            {
                return false;
            }

            transform.position = groundedPosition;
            return true;
        }

        private void Move(Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
            {
                characterController.Move(delta);
                return;
            }

            transform.position += delta;
        }
    }
}
