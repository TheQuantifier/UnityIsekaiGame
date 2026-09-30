using UnityEngine;
using UnityIsekaiGame.Configuration;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonCharacterMotor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMovementSettings movementSettings;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private ActorStats stats;

        private CharacterController controller;
        private float currentHorizontalSpeed;
        private float verticalVelocity;
        private bool networkPredictionMode;
        private bool networkPredictionSprintAllowed = true;
        private bool hasNetworkPredictionTuning;
        private float networkWalkSpeed;
        private float networkSprintMultiplier = 1f;
        private float networkAcceleration;
        private float networkDeceleration;
        private float networkJumpHeight;
        private float networkGravity;
        private float networkGroundedStickForce;

        public PlayerMovementSettings MovementSettings => movementSettings;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (stats == null)
            {
                stats = GetComponent<ActorStats>();
            }
        }

        private void Update()
        {
            if (input == null || movementSettings == null)
            {
                return;
            }

            Vector2 moveInput = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 localMove = new Vector3(moveInput.x, 0f, moveInput.y);
            bool isMoving = localMove.sqrMagnitude > 0.0001f;
            bool sprinting = !networkPredictionMode && stamina != null
                ? stamina.EvaluateSprint(input.SprintHeld, isMoving, input.GameplayInputBlocked, Time.deltaTime)
                : input.SprintHeld && isMoving && networkPredictionSprintAllowed;
            float targetSpeed = isMoving ? ResolveHorizontalSpeed(sprinting) : 0f;
            float acceleration = networkPredictionMode && hasNetworkPredictionTuning
                ? networkAcceleration
                : movementSettings.Acceleration;
            float deceleration = networkPredictionMode && hasNetworkPredictionTuning
                ? networkDeceleration
                : movementSettings.Deceleration;
            float speedChangeRate = targetSpeed > currentHorizontalSpeed ? acceleration : deceleration;
            currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, speedChangeRate * Time.deltaTime);

            float gravity = networkPredictionMode && hasNetworkPredictionTuning
                ? networkGravity
                : movementSettings.Gravity;
            float jumpHeight = networkPredictionMode && hasNetworkPredictionTuning
                ? networkJumpHeight
                : movementSettings.JumpHeight;
            float groundedStickForce = networkPredictionMode && hasNetworkPredictionTuning
                ? networkGroundedStickForce
                : movementSettings.GroundedStickForce;

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }

            if (controller.isGrounded && input.ConsumeJump())
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }

            verticalVelocity -= gravity * Time.deltaTime;

            Vector3 horizontalVelocity = transform.TransformDirection(localMove) * currentHorizontalSpeed;
            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        private float ResolveHorizontalSpeed(bool sprinting)
        {
            if (networkPredictionMode && hasNetworkPredictionTuning)
            {
                return sprinting ? networkWalkSpeed * networkSprintMultiplier : networkWalkSpeed;
            }

            float walkSpeed = movementSettings.WalkSpeed;
            if (stats != null && stats.IsInitialized)
            {
                float statMovementSpeed = stats.MovementSpeed;
                if (statMovementSpeed > 0f)
                {
                    walkSpeed = statMovementSpeed;
                }
            }

            return sprinting ? walkSpeed * movementSettings.SprintSpeedMultiplier : walkSpeed;
        }

        public void ResetTransientMotionForPersistenceRestore()
        {
            currentHorizontalSpeed = 0f;
            verticalVelocity = 0f;
        }

        public void SetNetworkPredictionMode(bool enabled)
        {
            networkPredictionMode = enabled;
            if (!enabled)
            {
                networkPredictionSprintAllowed = true;
                ResetTransientMotionForPersistenceRestore();
            }
        }

        public void ConfigureNetworkPredictionTuning(
            float walkSpeed,
            float sprintMultiplier,
            float acceleration,
            float deceleration,
            float jumpHeight,
            float gravity,
            float groundedStickForce)
        {
            networkWalkSpeed = Mathf.Max(0f, walkSpeed);
            networkSprintMultiplier = Mathf.Clamp(sprintMultiplier, 1f, 2f);
            networkAcceleration = Mathf.Max(0f, acceleration);
            networkDeceleration = Mathf.Max(0f, deceleration);
            networkJumpHeight = Mathf.Max(0f, jumpHeight);
            networkGravity = Mathf.Max(0f, gravity);
            networkGroundedStickForce = Mathf.Max(0f, groundedStickForce);
            hasNetworkPredictionTuning = true;
        }

        public void SetNetworkPredictionSprintAllowed(bool allowed)
        {
            networkPredictionSprintAllowed = allowed;
        }
    }
}
