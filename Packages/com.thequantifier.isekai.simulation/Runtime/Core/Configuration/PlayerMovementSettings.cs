using UnityEngine;

namespace UnityIsekaiGame.Configuration
{
    [CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Unity Isekai Game/Player Movement Settings")]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        private const float MinimumSprintSpeedMultiplier = 1f;
        private const float MaximumSprintSpeedMultiplier = 2f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [SerializeField, Range(MinimumSprintSpeedMultiplier, MaximumSprintSpeedMultiplier)] private float sprintSpeedMultiplier = 1.6666667f;
        [SerializeField, HideInInspector, Min(0f)] private float sprintSpeed = 5f;
        [SerializeField, Min(0f)] private float acceleration = 30f;
        [SerializeField, Min(0f)] private float deceleration = 36f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.25f;
        [SerializeField, Min(0f)] private float gravity = 24f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(0f)] private float gamepadSensitivity = 140f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-85f, 85f);

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => WalkSpeed * SprintSpeedMultiplier;
        public float SprintSpeedMultiplier => Mathf.Clamp(
            sprintSpeedMultiplier > 0f ? sprintSpeedMultiplier : walkSpeed > 0f ? sprintSpeed / walkSpeed : MinimumSprintSpeedMultiplier,
            MinimumSprintSpeedMultiplier,
            MaximumSprintSpeedMultiplier);
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float GroundedStickForce => groundedStickForce;
        public float MouseSensitivity => mouseSensitivity;
        public float GamepadSensitivity => gamepadSensitivity;
        public Vector2 PitchLimits => pitchLimits;

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeedMultiplier = Mathf.Clamp(
                sprintSpeedMultiplier > 0f ? sprintSpeedMultiplier : walkSpeed > 0f ? sprintSpeed / walkSpeed : MinimumSprintSpeedMultiplier,
                MinimumSprintSpeedMultiplier,
                MaximumSprintSpeedMultiplier);
            sprintSpeed = walkSpeed * sprintSpeedMultiplier;
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            gravity = Mathf.Max(0f, gravity);
            groundedStickForce = Mathf.Max(0f, groundedStickForce);
            mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
            gamepadSensitivity = Mathf.Max(0f, gamepadSensitivity);
        }
    }
}
