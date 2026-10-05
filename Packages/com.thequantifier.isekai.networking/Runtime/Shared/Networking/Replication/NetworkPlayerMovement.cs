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
        private const float JumpBufferSeconds = 0.12f;
        private const float JumpCoyoteSeconds = 0.08f;

        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Range(1f, 2f)] private float sprintMultiplier = 1.6666667f;
        [SerializeField, Min(0f)] private float acceleration = 60f;
        [SerializeField, Min(0f)] private float deceleration = 72f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0f)] private float gravity = 30f;
        [SerializeField, Min(0f)] private float groundedStickForce = 2f;
        [SerializeField, Min(1f)] private float fallRecoveryDistance = 50f;

        // Server-local validation cursor. Clients receive the acknowledged sequence only as
        // part of authoritativeState, atomically paired with the position it produced.
        private uint lastAcceptedSequence;
        private uint lastProcessedJumpSequence;
        private uint lastExecutedJumpSequence;
        private uint pendingJumpSequence;

        private readonly NetworkVariable<NetworkMovementState> authoritativeState = new NetworkVariable<NetworkMovementState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController controller;
        private NetworkTransform networkTransform;
        private NetworkPlayerActor actor;
        private NetworkPlayerVitals vitals;
        private NetworkMovementInput latestInput;
        private uint localSequence;
        private bool localInputWasMoving;
        private float horizontalSpeed;
        private float verticalVelocity;
        private double lastInputReceivedAt;
        private double pendingJumpExpiresAt;
        private double lastGroundedAt;
        private bool movementLogged;
        private bool originalInterpolation;
        private Vector3 spawnPosition;
        private bool movementTraceEnabled;
        private bool traceInputWasMoving;
        private bool traceInputWasSprinting;
        private bool traceSimulationDirty;
        private bool traceInputTimedOut;
        private double nextTraceReceiveAt;
        private double nextTraceSimulationAt;
        private ulong traceSimulationTick;
        private ulong authoritativeSimulationTick;
        // The owner intentionally publishes its latest input at 60 Hz. Keep a little
        // timing headroom so normal render-frame jitter is not mistaken for flooding.
        private readonly TokenBucketRateLimiter movementRateLimiter = new TokenBucketRateLimiter(120d, 90d);
        private readonly TokenBucketRateLimiter jumpRateLimiter = new TokenBucketRateLimiter(4d, 5d);

        public uint LastAcceptedSequence => authoritativeState.Value.IsInitialized
            ? authoritativeState.Value.InputSequence
            : lastAcceptedSequence;
        public uint LastSubmittedSequence => localSequence;
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => walkSpeed * sprintMultiplier;
        public float SprintMultiplier => sprintMultiplier;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float GroundedStickForce => groundedStickForce;
        public float FallRecoveryDistance => fallRecoveryDistance;
        public bool HasAuthoritativeState => authoritativeState.Value.IsInitialized;
        public NetworkMovementState AuthoritativeState => authoritativeState.Value;
        public Vector3 AuthoritativePosition => authoritativeState.Value.IsInitialized
            ? authoritativeState.Value.Position
            : transform.position;

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
            actor = GetComponent<NetworkPlayerActor>();
            vitals = GetComponent<NetworkPlayerVitals>();
            movementTraceEnabled = NetworkMovementTrace.IsRequested();
        }

        public override void OnNetworkSpawn()
        {
            if (actor != null) actor.WorldParticipationStateChanged += OnWorldParticipationStateChanged;
            controller.enabled = IsServer && (actor == null || actor.IsWorldParticipationActive);
            spawnPosition = transform.position;
            latestInput = new NetworkMovementInput(0, Vector2.zero, transform.eulerAngles.y, false);
            localInputWasMoving = false;
            lastInputReceivedAt = Time.realtimeSinceStartupAsDouble;
            traceInputWasMoving = false;
            traceInputWasSprinting = false;
            traceSimulationDirty = movementTraceEnabled;
            traceInputTimedOut = false;
            nextTraceReceiveAt = 0d;
            nextTraceSimulationAt = 0d;
            traceSimulationTick = 0ul;
            authoritativeSimulationTick = 0ul;
            if (IsServer)
            {
                lastAcceptedSequence = 0u;
                lastProcessedJumpSequence = 0u;
                lastExecutedJumpSequence = 0u;
                pendingJumpSequence = 0u;
                pendingJumpExpiresAt = 0d;
                lastGroundedAt = Time.realtimeSinceStartupAsDouble;
                PublishAuthoritativeState(latestInput.Sequence);
            }
            if (movementTraceEnabled && IsServer)
            {
                Debug.Log(
                    $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=spawn " +
                    $"actor={TraceActorId} client={OwnerClientId} seq={lastAcceptedSequence} " +
                    $"position={NetworkMovementTrace.Format(transform.position)}",
                    this);
            }
            if (networkTransform != null && IsClient && IsOwner && !IsServer)
            {
                // Remote actors benefit from interpolation, but interpolating the locally owned
                // authoritative actor deliberately renders it behind the newest server state.
                networkTransform.Interpolate = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (actor != null) actor.WorldParticipationStateChanged -= OnWorldParticipationStateChanged;
            if (controller != null)
            {
                controller.enabled = false;
            }

            latestInput = default;
            localInputWasMoving = false;
            pendingJumpSequence = 0u;
            pendingJumpExpiresAt = 0d;
            horizontalSpeed = 0f;
            verticalVelocity = 0f;
            traceSimulationDirty = false;
            traceInputTimedOut = false;
            authoritativeSimulationTick = 0ul;
            lastProcessedJumpSequence = 0u;
            lastExecutedJumpSequence = 0u;
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
            if (!IsSpawned || !IsOwner || IsServer || actor?.IsPausedProtected == true)
            {
                return false;
            }

            localSequence++;
            if (localSequence == 0)
            {
                localSequence = 1;
            }

            var requested = new NetworkMovementInput(localSequence, move, yawDegrees, sprint, jump);
            bool isMoving = move.sqrMagnitude > 0.0001f;
            if ((localInputWasMoving && !isMoving) || jump)
            {
                // Continuous input can be lossy, but state-changing edges cannot. Stops and jumps
                // must be paired with an authoritative acknowledgement for prediction replay.
                SubmitMovementActionRpc(requested);
            }
            else
            {
                SubmitMovementInputRpc(requested);
            }

            localInputWasMoving = isMoving;
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void SubmitMovementInputRpc(NetworkMovementInput requested, RpcParams rpcParams = default)
        {
            TryAcceptMovementInput(requested, rpcParams.Receive.SenderClientId, "unreliable");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void SubmitMovementActionRpc(NetworkMovementInput requested, RpcParams rpcParams = default)
        {
            TryAcceptMovementInput(
                requested,
                rpcParams.Receive.SenderClientId,
                requested.Jump ? "reliable-jump" : "reliable-stop");
        }

        private void FixedUpdate()
        {
            RefreshMovementTraceState();
            if (!IsSpawned || !IsServer || controller == null || !controller.enabled)
            {
                return;
            }

            using NetworkMovementTrace.ServerPhaseScope phase =
                NetworkMovementTrace.MeasureServerPhase("MovementSimulation");

            double now = Time.realtimeSinceStartupAsDouble;
            NetworkMovementInput input = latestInput;
            double inputAge = now - lastInputReceivedAt;
            if (inputAge > InputTimeoutSeconds)
            {
                input.Move = Vector2.zero;
                input.Sprint = false;
                ExpirePendingJump(now, "input-timeout");
                if (movementTraceEnabled && !traceInputTimedOut && latestInput.Move.sqrMagnitude > 0.0001f)
                {
                    traceInputTimedOut = true;
                    traceSimulationDirty = true;
                    Debug.Log(
                        $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=input-timeout " +
                        $"actor={TraceActorId} client={OwnerClientId} seq={lastAcceptedSequence} " +
                        $"inputAgeMs={inputAge * 1000d:F1} position={NetworkMovementTrace.Format(transform.position)}",
                        this);
                }
            }

            Simulate(input, Time.fixedDeltaTime);
        }

        private void OnWorldParticipationStateChanged(
            NetworkPlayerActor _,
            NetworkPlayerWorldParticipationState __,
            NetworkPlayerWorldParticipationState current)
        {
            if (!IsServer || controller == null)
            {
                return;
            }

            bool active = current == NetworkPlayerWorldParticipationState.Active;
            if (!active) StopAuthoritativeMotion();
            controller.enabled = active;
        }

        private void StopAuthoritativeMotion()
        {
            latestInput.Move = Vector2.zero;
            latestInput.Sprint = false;
            horizontalSpeed = 0f;
            verticalVelocity = 0f;
            ExpirePendingJump(Time.realtimeSinceStartupAsDouble, "participation-state");
        }

        private void TryAcceptMovementInput(NetworkMovementInput requested, ulong senderClientId, string delivery)
        {
            RefreshMovementTraceState();
            if (!IsServer || senderClientId != OwnerClientId)
            {
                return;
            }

            using NetworkMovementTrace.ServerPhaseScope phase =
                NetworkMovementTrace.MeasureServerPhase("NetworkReceiveMovement");
            if (actor?.IsPausedProtected == true)
            {
                StopAuthoritativeMotion();
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (!NetworkMovementInputValidator.TryNormalize(
                    requested,
                    lastAcceptedSequence,
                    out NetworkMovementInput normalized,
                    out string failure))
            {
                TraceRejectedInput(requested, senderClientId, delivery, failure);
                return;
            }
            if (string.Equals(delivery, "unreliable", StringComparison.Ordinal)
                && !movementRateLimiter.TryConsume(now))
            {
                TraceRejectedInput(requested, senderClientId, delivery, "rate-limited");
                return;
            }

            bool moving = normalized.Move.sqrMagnitude > 0.0001f;
            bool transition = moving != traceInputWasMoving || normalized.Sprint != traceInputWasSprinting;
            if (normalized.Jump)
            {
                if (!jumpRateLimiter.TryConsume(now))
                {
                    lastProcessedJumpSequence = normalized.Sequence;
                    normalized.Jump = false;
                    Debug.LogWarning(
                        $"[Jump Trace][Server] action=rate-limited client={OwnerClientId} " +
                        $"sequence={requested.Sequence} grounded={controller != null && controller.isGrounded} " +
                        $"position={transform.position} serverTime={now:F6}",
                        this);
                }
                else
                {
                    if (pendingJumpSequence != 0u)
                    {
                        lastProcessedJumpSequence = pendingJumpSequence;
                    }

                    pendingJumpSequence = normalized.Sequence;
                    pendingJumpExpiresAt = now + JumpBufferSeconds;
                    traceSimulationDirty = true;
                    if (movementTraceEnabled)
                    {
                        Debug.Log(
                            $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=jump-buffered " +
                            $"actor={TraceActorId} client={OwnerClientId} seq={normalized.Sequence} " +
                            $"position={NetworkMovementTrace.Format(transform.position)} grounded={controller != null && controller.isGrounded}",
                            this);
                    }
                }
            }
            latestInput = normalized;
            lastAcceptedSequence = normalized.Sequence;
            lastInputReceivedAt = now;
            traceInputTimedOut = false;
            NetworkMovementTrace.RecordServerInputReceived(
                normalized.Sequence,
                !string.Equals(delivery, "unreliable", StringComparison.Ordinal));
            if (movementTraceEnabled && NetworkMovementTrace.ShouldSampleDeliveryTiming(normalized.Sequence))
            {
                Debug.Log(
                    $"[Movement Timing][ServerReceive] utc={NetworkMovementTrace.TimestampUtc()} " +
                    $"actor={TraceActorId} client={senderClientId} seq={normalized.Sequence} delivery={delivery}",
                    this);
            }
            if (movementTraceEnabled && (transition || (moving && now >= nextTraceReceiveAt)))
            {
                nextTraceReceiveAt = now + NetworkMovementTrace.SampleIntervalSeconds;
                Debug.Log(
                    $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=receive " +
                    $"actor={TraceActorId} client={senderClientId} seq={normalized.Sequence} delivery={delivery} " +
                    $"move={NetworkMovementTrace.Format(normalized.Move)} yaw={normalized.YawDegrees:F2} sprint={normalized.Sprint} jump={normalized.Jump} " +
                    $"position={NetworkMovementTrace.Format(transform.position)} speed={horizontalSpeed:F3}",
                    this);
            }

            traceInputWasMoving = moving;
            traceInputWasSprinting = normalized.Sprint;
            traceSimulationDirty |= transition;
        }

        private void RefreshMovementTraceState()
        {
            bool requested = NetworkMovementTrace.IsRequested();
            if (requested == movementTraceEnabled) return;

            movementTraceEnabled = requested;
            traceSimulationDirty = requested;
            traceInputTimedOut = false;
            nextTraceReceiveAt = 0d;
            nextTraceSimulationAt = 0d;
            if (requested && IsSpawned && IsServer)
            {
                Debug.Log(
                    $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=trace-enabled " +
                    $"actor={TraceActorId} client={OwnerClientId} seq={lastAcceptedSequence} " +
                    $"position={NetworkMovementTrace.Format(transform.position)}",
                    this);
            }
        }

        private void Simulate(NetworkMovementInput input, float deltaTime)
        {
            Vector2 planarInput = Vector2.ClampMagnitude(input.Move, 1f);
            bool moving = planarInput.sqrMagnitude > 0.0001f;
            if (vitals != null && vitals.IsDefeated)
            {
                planarInput = Vector2.zero;
                moving = false;
                ExpirePendingJump(Time.realtimeSinceStartupAsDouble, "defeated");
            }

            bool sprintAllowed = vitals != null
                ? vitals.EvaluateSprintServer(input.Sprint, moving, deltaTime)
                : input.Sprint;
            float targetSpeed = moving ? (sprintAllowed ? SprintSpeed : walkSpeed) : 0f;
            float changeRate = targetSpeed > horizontalSpeed ? acceleration : deceleration;
            float previousSpeed = horizontalSpeed;
            horizontalSpeed = Mathf.MoveTowards(horizontalSpeed, targetSpeed, changeRate * deltaTime);
            if (movementTraceEnabled && previousSpeed > 0.01f && horizontalSpeed <= 0.01f)
            {
                traceSimulationDirty = true;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            bool groundedBeforeMove = controller.isGrounded;
            if (groundedBeforeMove)
            {
                lastGroundedAt = now;
            }

            if (groundedBeforeMove && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }

            bool jumpBuffered = pendingJumpSequence != 0u && now <= pendingJumpExpiresAt;
            bool withinCoyoteWindow = groundedBeforeMove || now - lastGroundedAt <= JumpCoyoteSeconds;
            bool jumpExecuted = jumpBuffered && withinCoyoteWindow;
            if (jumpExecuted)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                lastProcessedJumpSequence = pendingJumpSequence;
                lastExecutedJumpSequence = pendingJumpSequence;
                pendingJumpSequence = 0u;
                pendingJumpExpiresAt = 0d;
                traceSimulationDirty = true;
            }
            else if (pendingJumpSequence != 0u && now > pendingJumpExpiresAt)
            {
                ExpirePendingJump(now, "buffer-expired");
            }

            verticalVelocity -= gravity * deltaTime;

            Quaternion yaw = Quaternion.Euler(0f, input.YawDegrees, 0f);
            Vector3 direction = yaw * new Vector3(planarInput.x, 0f, planarInput.y);
            Vector3 positionBeforeMove = transform.position;
            CollisionFlags collisionFlags = controller.Move((direction * horizontalSpeed + Vector3.up * verticalVelocity) * deltaTime);
            if ((collisionFlags & CollisionFlags.Below) != 0)
            {
                lastGroundedAt = now;
            }
            transform.rotation = yaw;
            authoritativeSimulationTick++;
            PublishAuthoritativeState(input.Sequence);

            TraceAuthoritativeSimulation(input, sprintAllowed, moving, jumpExecuted, deltaTime,
                transform.position - positionBeforeMove, collisionFlags);

            if (transform.position.y < spawnPosition.y - Mathf.Max(1f, fallRecoveryDistance))
            {
                RecoverFromInvalidFall();
                return;
            }

            if (!movementLogged && (transform.position - spawnPosition).sqrMagnitude >= 0.25f)
            {
                movementLogged = true;
                NetworkPlayerActor actor = GetComponent<NetworkPlayerActor>();
                Debug.Log($"[Network Movement] Server moved actor '{actor?.ActorId ?? name}' to {transform.position} after accepting sequence {lastAcceptedSequence}.", this);
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
            ExpirePendingJump(Time.realtimeSinceStartupAsDouble, "fall-recovery");
            authoritativeSimulationTick++;
            PublishAuthoritativeState(lastAcceptedSequence);
            NetworkPlayerActor actor = GetComponent<NetworkPlayerActor>();
            Debug.LogError(
                $"[Network Movement] Recovered actor '{actor?.ActorId ?? name}' from invalid fall position {invalidPosition} " +
                $"to authoritative spawn {spawnPosition}. Verify server collision if this repeats.",
                this);
        }

        private void PublishAuthoritativeState(uint inputSequence)
        {
            if (!IsServer)
            {
                return;
            }

            authoritativeState.Value = new NetworkMovementState(
                true,
                authoritativeSimulationTick,
                inputSequence,
                transform.position,
                transform.eulerAngles.y,
                horizontalSpeed,
                verticalVelocity,
                controller != null && controller.isGrounded,
                lastProcessedJumpSequence,
                lastExecutedJumpSequence);
        }

        private void ExpirePendingJump(double now, string reason)
        {
            if (pendingJumpSequence == 0u)
            {
                return;
            }

            uint rejectedSequence = pendingJumpSequence;
            lastProcessedJumpSequence = rejectedSequence;
            pendingJumpSequence = 0u;
            pendingJumpExpiresAt = 0d;
            traceSimulationDirty = true;
            if (movementTraceEnabled)
            {
                Debug.Log(
                    $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=jump-rejected " +
                    $"actor={TraceActorId} client={OwnerClientId} seq={rejectedSequence} reason={reason} " +
                    $"position={NetworkMovementTrace.Format(transform.position)} serverTime={now:F6}",
                    this);
            }
        }

        private string TraceActorId => actor?.ActorId ?? name;

        private void TraceRejectedInput(
            NetworkMovementInput requested,
            ulong senderClientId,
            string delivery,
            string reason)
        {
            if (!movementTraceEnabled)
            {
                return;
            }

            Debug.LogWarning(
                $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=reject " +
                $"actor={TraceActorId} client={senderClientId} seq={requested.Sequence} delivery={delivery} " +
                $"move={NetworkMovementTrace.Format(requested.Move)} sprint={requested.Sprint} " +
                $"acceptedSeq={lastAcceptedSequence} reason=\"{reason}\"",
                this);
        }

        private void TraceAuthoritativeSimulation(
            NetworkMovementInput input,
            bool sprintAllowed,
            bool moving,
            bool jumpExecuted,
            float deltaTime,
            Vector3 displacement,
            CollisionFlags collisionFlags)
        {
            if (!movementTraceEnabled)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            traceSimulationTick++;
            bool active = moving
                || horizontalSpeed > 0.01f
                || Mathf.Abs(verticalVelocity) > groundedStickForce + 0.01f
                || !controller.isGrounded;
            if (!traceSimulationDirty && (!active || now < nextTraceSimulationAt))
            {
                return;
            }

            traceSimulationDirty = false;
            nextTraceSimulationAt = now + NetworkMovementTrace.SampleIntervalSeconds;
            Debug.Log(
                $"[Movement Trace][Server] utc={NetworkMovementTrace.TimestampUtc()} phase=simulate " +
                $"actor={TraceActorId} client={OwnerClientId} seq={lastAcceptedSequence} " +
                $"tick={traceSimulationTick} dt={deltaTime:F4} move={NetworkMovementTrace.Format(input.Move)} " +
                $"yaw={input.YawDegrees:F2} requestedSprint={input.Sprint} " +
                $"allowedSprint={sprintAllowed} position={NetworkMovementTrace.Format(transform.position)} " +
                $"displacement={NetworkMovementTrace.Format(displacement)} collisions={collisionFlags} jumpExecuted={jumpExecuted} " +
                $"processedJump={lastProcessedJumpSequence} executedJump={lastExecutedJumpSequence} pendingJump={pendingJumpSequence} " +
                $"speed={horizontalSpeed:F3} verticalSpeed={verticalVelocity:F3} grounded={controller.isGrounded} " +
                $"inputAgeMs={(now - lastInputReceivedAt) * 1000d:F1}",
                this);
        }
    }
}
