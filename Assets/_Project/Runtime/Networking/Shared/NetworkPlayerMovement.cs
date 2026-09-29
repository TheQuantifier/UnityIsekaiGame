using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(CharacterController))]
    public sealed class NetworkPlayerMovement : NetworkBehaviour
    {
        private const float InputTimeoutSeconds = 0.35f;

        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [SerializeField, Range(1f, 2f)] private float sprintMultiplier = 1.6666667f;
        [SerializeField, Min(0f)] private float acceleration = 30f;
        [SerializeField, Min(0f)] private float deceleration = 36f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.25f;
        [SerializeField, Min(0f)] private float gravity = 24f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;

        private readonly NetworkVariable<uint> lastAcceptedSequence = new NetworkVariable<uint>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController controller;
        private NetworkMovementInput latestInput;
        private uint localSequence;
        private float horizontalSpeed;
        private float verticalVelocity;
        private double lastInputReceivedAt;
        private bool jumpRequested;
        private bool movementLogged;
        private Vector3 spawnPosition;

        public uint LastAcceptedSequence => lastAcceptedSequence.Value;
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => walkSpeed * sprintMultiplier;
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
        }

        public override void OnNetworkSpawn()
        {
            controller.enabled = IsServer;
            spawnPosition = transform.position;
            latestInput = new NetworkMovementInput(0, Vector2.zero, transform.eulerAngles.y, false);
            lastInputReceivedAt = Time.realtimeSinceStartupAsDouble;
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
            if (IsServer && rpcParams.Receive.SenderClientId == OwnerClientId)
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
            float targetSpeed = moving ? (input.Sprint ? SprintSpeed : walkSpeed) : 0f;
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

            if (!movementLogged && (transform.position - spawnPosition).sqrMagnitude >= 0.25f)
            {
                movementLogged = true;
                NetworkPlayerActor actor = GetComponent<NetworkPlayerActor>();
                Debug.Log($"[Network Movement] Server moved actor '{actor?.ActorId ?? name}' to {transform.position} after accepting sequence {lastAcceptedSequence.Value}.", this);
            }
        }
    }
}
