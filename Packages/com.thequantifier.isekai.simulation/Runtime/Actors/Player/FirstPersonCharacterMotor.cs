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
                : input.SprintHeld && isMoving;
            float targetSpeed = isMoving ? ResolveHorizontalSpeed(sprinting) : 0f;
            float speedChangeRate = targetSpeed > currentHorizontalSpeed ? movementSettings.Acceleration : movementSettings.Deceleration;
            currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, speedChangeRate * Time.deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -movementSettings.GroundedStickForce;
            }

            if (controller.isGrounded && input.ConsumeJump())
            {
                verticalVelocity = Mathf.Sqrt(2f * movementSettings.Gravity * movementSettings.JumpHeight);
            }

            verticalVelocity -= movementSettings.Gravity * Time.deltaTime;

            Vector3 horizontalVelocity = transform.TransformDirection(localMove) * currentHorizontalSpeed;
            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        private float ResolveHorizontalSpeed(bool sprinting)
        {
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
            if (!enabled) ResetTransientMotionForPersistenceRestore();
        }
    }
}
