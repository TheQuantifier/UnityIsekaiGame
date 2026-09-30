using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(CharacterController))]
    [RequireComponent(typeof(NetworkPlayerVitals))]
    public sealed class NetworkPlayerMovement : NetworkBehaviour
    {
        private const float InputTimeoutSeconds = 0.35f;

        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Range(1f, 2f)] private float sprintMultiplier = 1.6666667f;
        [SerializeField, Min(0f)] private float acceleration = 60f;
        [SerializeField, Min(0f)] private float deceleration = 72f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0f)] private float gravity = 30f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;
        [SerializeField, Min(1f)] private float fallRecoveryDistance = 50f;

        private readonly NetworkVariable<uint> lastAcceptedSequence = new NetworkVariable<uint>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController controller;
        private NetworkTransform networkTransform;
        private NetworkPlayerVitals vitals;
        private NetworkMovementInput latestInput;
        private uint localSequence;
        private float horizontalSpeed;
        private float verticalVelocity;
        private double lastInputReceivedAt;
        private bool jumpRequested;
        private bool movementLogged;
        private bool originalInterpolation;
        private Vector3 spawnPosition;
        // The owner intentionally publishes its latest input at 60 Hz. Keep a little
        // timing headroom so normal render-frame jitter is not mistaken for flooding.
        private readonly TokenBucketRateLimiter movementRateLimiter = new TokenBucketRateLimiter(120d, 90d);
        private readonly TokenBucketRateLimiter jumpRateLimiter = new TokenBucketRateLimiter(3d, 3d);

        public uint LastAcceptedSequence => lastAcceptedSequence.Value;
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => walkSpeed * sprintMultiplier;
        public float SprintMultiplier => sprintMultiplier;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float GroundedStickForce => groundedStickForce;
        public float FallRecoveryDistance => fallRecoveryDistance;
        public Vector3 AuthoritativePosition => transform.position;

        public void ConfigureTuning(
            float configuredWalkSpeed,
            float configuredSprintMultiplier,
            float configuredAcceleration,
            float configuredDeceleration,
            float configuredJumpHeight,
            float configuredGravity,
            float configuredGroundedStickForce)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Movement tuning cannot change after the network actor is spawned.");
            }

            walkSpeed = Mathf.Max(0f, configuredWalkSpeed);
            sprintMultiplier = Mathf.Clamp(configuredSprintMultiplier, 1f, 2f);
            acceleration = Mathf.Max(0f, configuredAcceleration);
            deceleration = Mathf.Max(0f, configuredDeceleration);
            jumpHeight = Mathf.Max(0f, configuredJumpHeight);
            gravity = Mathf.Max(0f, configuredGravity);
            groundedStickForce = Mathf.Max(0f, configuredGroundedStickForce);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            networkTransform = GetComponent<NetworkTransform>();
            originalInterpolation = networkTransform != null && networkTransform.Interpolate;
            vitals = GetComponent<NetworkPlayerVitals>();
        }

        public override void OnNetworkSpawn()
        {
            controller.enabled = IsServer;
            spawnPosition = transform.position;
            latestInput = new NetworkMovementInput(0, Vector2.zero, transform.eulerAngles.y, false);
            lastInputReceivedAt = Time.realtimeSinceStartupAsDouble;
            if (networkTransform != null && IsClient && IsOwner && !IsServer)
            {
                // Remote actors benefit from interpolation, but interpolating the locally owned
                // authoritative actor deliberately renders it behind the newest server state.
                networkTransform.Interpolate = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (controller != null)
            {
                controller.enabled = false;
            }

            latestInput = default;
            jumpRequested = false;
            horizontalSpeed = 0f;
            verticalVelocity = 0f;
            if (networkTransform != null) networkTransform.Interpolate = originalInterpolation;
            movementRateLimiter.Reset();
            jumpRateLimiter.Reset();
        }

        public void ConfigureSpawnServer(Vector3 position, float yawDegrees)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("The authoritative movement spawn must be configured before network spawn.");
            }

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Mathf.Repeat(yawDegrees, 360f), 0f));
            spawnPosition = position;
        }

        public bool SubmitLocalInput(Vector2 move, bool sprint, bool jump, float yawDegrees)
        {
            if (!IsSpawned || !IsOwner || IsServer)
            {
                return false;
            }

            localSequence++;
            if (localSequence == 0)
            {
                localSequence = 1;
            }

            SubmitMovementInputRpc(new NetworkMovementInput(localSequence, move, yawDegrees, sprint));
            if (jump)
            {
                RequestJumpRpc();
            }

            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void SubmitMovementInputRpc(NetworkMovementInput requested, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }
            if (!movementRateLimiter.TryConsume(Time.realtimeSinceStartupAsDouble)) return;

            if (!NetworkMovementInputValidator.TryNormalize(requested, lastAcceptedSequence.Value, out NetworkMovementInput normalized, out _))
            {
                return;
            }

            latestInput = normalized;
            lastAcceptedSequence.Value = normalized.Sequence;
            lastInputReceivedAt = Time.realtimeSinceStartupAsDouble;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void RequestJumpRpc(RpcParams rpcParams = default)
        {
            if (IsServer
                && rpcParams.Receive.SenderClientId == OwnerClientId
                && jumpRateLimiter.TryConsume(Time.realtimeSinceStartupAsDouble))
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || controller == null || !controller.enabled)
            {
                return;
            }

            NetworkMovementInput input = latestInput;
            if (Time.realtimeSinceStartupAsDouble - lastInputReceivedAt > InputTimeoutSeconds)
            {
                input.Move = Vector2.zero;
                input.Sprint = false;
                jumpRequested = false;
            }

            Simulate(input, Time.fixedDeltaTime);
        }

        private void Simulate(NetworkMovementInput input, float deltaTime)
        {
            Vector2 planarInput = Vector2.ClampMagnitude(input.Move, 1f);
            bool moving = planarInput.sqrMagnitude > 0.0001f;
            if (vitals != null && vitals.IsDefeated)
            {
                planarInput = Vector2.zero;
                moving = false;
                jumpRequested = false;
            }

            bool sprintAllowed = vitals != null
                ? vitals.EvaluateSprintServer(input.Sprint, moving, deltaTime)
                : input.Sprint;
            float targetSpeed = moving ? (sprintAllowed ? SprintSpeed : walkSpeed) : 0f;
            float changeRate = targetSpeed > horizontalSpeed ? acceleration : deceleration;
            horizontalSpeed = Mathf.MoveTowards(horizontalSpeed, targetSpeed, changeRate * deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }

            if (controller.isGrounded && jumpRequested)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }

            jumpRequested = false;
            verticalVelocity -= gravity * deltaTime;

            Quaternion yaw = Quaternion.Euler(0f, input.YawDegrees, 0f);
            Vector3 direction = yaw * new Vector3(planarInput.x, 0f, planarInput.y);
            controller.Move((direction * horizontalSpeed + Vector3.up * verticalVelocity) * deltaTime);
            transform.rotation = yaw;

            if (transform.position.y < spawnPosition.y - Mathf.Max(1f, fallRecoveryDistance))
            {
                RecoverFromInvalidFall();
                return;
            }

            if (!movementLogged && (transform.position - spawnPosition).sqrMagnitude >= 0.25f)
            {
                movementLogged = true;
                NetworkPlayerActor actor = GetComponent<NetworkPlayerActor>();
                Debug.Log($"[Network Movement] Server moved actor '{actor?.ActorId ?? name}' to {transform.position} after accepting sequence {lastAcceptedSequence.Value}.", this);
            }
        }

        private void RecoverFromInvalidFall()
        {
            Vector3 invalidPosition = transform.position;
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, transform.rotation);
            controller.enabled = true;
            latestInput.Move = Vector2.zero;
            latestInput.Sprint = false;
            horizontalSpeed = 0f;
            verticalVelocity = 0f;
            jumpRequested = false;
            NetworkPlayerActor actor = GetComponent<NetworkPlayerActor>();
            Debug.LogError(
                $"[Network Movement] Recovered actor '{actor?.ActorId ?? name}' from invalid fall position {invalidPosition} " +
                $"to authoritative spawn {spawnPosition}. Verify server collision if this repeats.",
                this);
        }
    }
}
