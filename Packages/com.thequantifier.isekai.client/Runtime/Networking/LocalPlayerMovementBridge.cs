using System;
using Unity.Netcode;
using UnityEngine;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Player;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LocalPlayerMovementBridge : MonoBehaviour
    {
        public const string MovementSmokeFlag = "--movement-smoke-forward";

        [SerializeField] private LocalGameClient client;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private FirstPersonCharacterMotor localMotor;
        [SerializeField] private Transform presentationRoot;
        [SerializeField, Range(10f, 60f)] private float inputSendRate = 60f;
        [SerializeField, Min(1f)] private float reconciliationSharpness = 10f;
        [SerializeField, Min(0f)] private float reconciliationDeadZone = 0.12f;
        [SerializeField, Min(0f)] private float maximumPredictionLead = 1.25f;
        [SerializeField, Min(0f)] private float groundedVerticalTolerance = 0.08f;
        [SerializeField, Min(0f)] private float airborneVerticalTolerance = 0.45f;
        [SerializeField, Min(0f)] private float jumpPredictionGraceSeconds = 0.3f;
        [SerializeField, Min(0f)] private float minimumLandingGraceSeconds = 0.08f;
        [SerializeField, Min(0f)] private float maximumLandingGraceSeconds = 0.25f;
        [SerializeField, Min(0.5f)] private float hardSnapDistance = 4f;

        private NetworkPlayerMovement networkMovement;
        private NetworkPlayerVitals networkVitals;
        private bool localMotorWasEnabled;
        private bool controlsOverridden;
        private double inputSendAccumulator;
        private double lastInputSampleAt;
        private double jumpPredictionGraceUntil;
        private Vector2 lastSubmittedMove;
        private bool lastSubmittedSprint;
        private bool predictionSprintExhausted;
        private bool predictionSprintAllowed = true;
        private uint clientJumpTraceId;
        private uint clientJumpTraceTick;
        private double clientJumpTraceUntil;
        private bool wasPredictionAirborne;
        private double landingPredictionGraceUntil;
        private bool smokeInputEnabled;
        private bool vitalsSmokeEnabled;
        private bool vitalsSmokeStarted;
        private double smokeInputEndsAt;
        private Vector3 smokeStartPosition;
        private bool smokeResultLogged;
        private bool jumpPending;

        public NetworkPlayerMovement BoundMovement => networkMovement;
        public bool IsServerAuthorityActive => networkMovement != null && networkMovement.IsSpawned;

        private void Awake()
        {
            ResolveReferences();
            smokeInputEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, MovementSmokeFlag, StringComparison.OrdinalIgnoreCase));
            vitalsSmokeEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, LocalPlayerVitalsBridge.VitalsSmokeFlag, StringComparison.OrdinalIgnoreCase));
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (client == null)
            {
                return;
            }

            client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
            OnLocalPlayerActorChanged(client.LocalPlayerActor);
        }

        private void OnDisable()
        {
            if (client != null)
            {
                client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            }

            Bind(null);
        }

        private void Update()
        {
            if (networkMovement == null || !networkMovement.IsSpawned || !networkMovement.IsOwner)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            UpdatePredictionSprintAuthorization();
            if (input != null && input.JumpPressedThisFrame)
            {
                clientJumpTraceId = clientJumpTraceId == uint.MaxValue ? 1u : clientJumpTraceId + 1u;
                clientJumpTraceTick = 0u;
                clientJumpTraceUntil = now + 1.25d;
                jumpPending = true;
                jumpPredictionGraceUntil = now + jumpPredictionGraceSeconds;
                Debug.Log(
                    $"[Jump Trace][Client] action=pressed trace={clientJumpTraceId} frame={Time.frameCount} " +
                    $"grounded={localMotor != null && localMotor.IsGrounded} predictedJumpCount={localMotor?.PredictedJumpCount ?? 0u} " +
                    $"move={(input == null ? Vector2.zero : input.Move)} sprint={input != null && input.SprintHeld} " +
                    $"position={(presentationRoot == null ? Vector3.zero : presentationRoot.position)} clientTime={now:F6}",
                    this);
            }
            if (vitalsSmokeEnabled && !vitalsSmokeStarted)
            {
                NetworkPlayerVitals networkVitals = networkMovement.GetComponent<NetworkPlayerVitals>();
                if (networkVitals == null || !networkVitals.HasState)
                {
                    return;
                }

                vitalsSmokeStarted = true;
                smokeStartPosition = networkMovement.transform.position;
                smokeInputEndsAt = now + 1.5d;
            }

            double sampleDelta = lastInputSampleAt <= 0d
                ? 1d / Mathf.Max(10f, inputSendRate)
                : Math.Min(0.25d, Math.Max(0d, now - lastInputSampleAt));
            lastInputSampleAt = now;
            if (!AdvanceInputSendAccumulator(
                    ref inputSendAccumulator,
                    sampleDelta,
                    1d / Mathf.Max(10f, inputSendRate)))
            {
                return;
            }

            bool smokeMoving = (smokeInputEnabled || vitalsSmokeStarted) && now < smokeInputEndsAt;
            Vector2 move = smokeMoving ? Vector2.up : input == null ? Vector2.zero : input.Move;
            bool sprint = smokeMoving ? vitalsSmokeEnabled : input != null && input.SprintHeld;
            bool jump = !smokeMoving && jumpPending;
            if (jump) jumpPending = false;
            float yaw = presentationRoot == null ? networkMovement.transform.eulerAngles.y : presentationRoot.eulerAngles.y;
            lastSubmittedMove = move;
            lastSubmittedSprint = sprint && predictionSprintAllowed;
            bool submitted = networkMovement.SubmitLocalInput(move, sprint, jump, yaw);
            if (jump)
            {
                Debug.Log(
                    $"[Jump Trace][Client] action=sent trace={clientJumpTraceId} frame={Time.frameCount} submitted={submitted} " +
                    $"move={move} sprint={sprint} yaw={yaw:F3} clientTime={now:F6}",
                    this);
            }
        }

        private void LateUpdate()
        {
            if (networkMovement == null || !networkMovement.IsSpawned || presentationRoot == null)
            {
                return;
            }

            Vector3 authoritativePosition = networkMovement.transform.position;
            bool traceJump = Time.realtimeSinceStartupAsDouble <= clientJumpTraceUntil;
            bool hardSnapped = false;
            bool airborne = false;
            bool suppressVertical = false;
            bool suppressHorizontal = false;
            Vector3 appliedCorrection = Vector3.zero;
            if (localMotor != null && localMotor.enabled)
            {
                Vector3 error = authoritativePosition - presentationRoot.position;
                if (error.sqrMagnitude >= hardSnapDistance * hardSnapDistance)
                {
                    hardSnapped = true;
                    SetPresentationPosition(authoritativePosition);
                    localMotor.ResetTransientMotionForPersistenceRestore();
                }
                else
                {
                    CharacterController controller = presentationRoot.GetComponent<CharacterController>();
                    float predictionSpeed = lastSubmittedMove.sqrMagnitude > 0.0001f
                        ? lastSubmittedSprint ? networkMovement.SprintSpeed : networkMovement.WalkSpeed
                        : 0f;
                    ulong roundTripTimeMilliseconds = GetRoundTripTimeMilliseconds();
                    float horizontalTolerance = CalculateHorizontalPredictionTolerance(
                        reconciliationDeadZone,
                        maximumPredictionLead,
                        predictionSpeed,
                        roundTripTimeMilliseconds,
                        inputSendRate);
                    airborne = localMotor != null && !localMotor.IsGrounded;
                    double now = Time.realtimeSinceStartupAsDouble;
                    if (airborne)
                    {
                        wasPredictionAirborne = true;
                    }
                    else if (wasPredictionAirborne)
                    {
                        wasPredictionAirborne = false;
                        landingPredictionGraceUntil = now + CalculateLandingPredictionGrace(
                            minimumLandingGraceSeconds,
                            maximumLandingGraceSeconds,
                            roundTripTimeMilliseconds);
                    }

                    bool landingGraceActive = now < landingPredictionGraceUntil;
                    suppressVertical = airborne || landingGraceActive || now < jumpPredictionGraceUntil;
                    suppressHorizontal = airborne || landingGraceActive;
                    float verticalTolerance = controller != null && controller.isGrounded
                        ? groundedVerticalTolerance
                        : airborneVerticalTolerance;
                    Vector3 correctionError = CalculateCorrectionError(
                        error,
                        horizontalTolerance,
                        verticalTolerance,
                        suppressHorizontal,
                        suppressVertical);
                    Vector3 correction = correctionError * (1f - Mathf.Exp(-reconciliationSharpness * Time.unscaledDeltaTime));
                    appliedCorrection = correction;
                    if (controller != null && controller.enabled)
                    {
                        // CharacterController.isGrounded describes the most recent Move call.
                        // A second zero-distance reconciliation Move would erase the grounded
                        // result produced by the local motor and make the next jump miss locally.
                        if (ShouldApplyControllerCorrection(correction)) controller.Move(correction);
                    }
                    else presentationRoot.position += correction;
                }
            }
            else
            {
                presentationRoot.position = authoritativePosition;
            }
            if (traceJump)
            {
                Vector3 localPosition = presentationRoot.position;
                Debug.Log(
                    $"[Jump Trace][Client] action=tick trace={clientJumpTraceId} tick={clientJumpTraceTick++} frame={Time.frameCount} " +
                    $"grounded={localMotor != null && localMotor.IsGrounded} controllerGrounded={localMotor != null && localMotor.ControllerIsGrounded} airborne={airborne} " +
                    $"predictedJumpCount={localMotor?.PredictedJumpCount ?? 0u} horizontalSpeed={localMotor?.CurrentHorizontalSpeed ?? 0f:F4} " +
                    $"verticalVelocity={localMotor?.VerticalVelocity ?? 0f:F4} suppressHorizontal={suppressHorizontal} " +
                    $"suppressVertical={suppressVertical} hardSnap={hardSnapped} correction={appliedCorrection} " +
                    $"localPosition={localPosition} authorityPosition={authoritativePosition} " +
                    $"error={authoritativePosition - localPosition} rttMs={GetRoundTripTimeMilliseconds()} " +
                    $"clientTime={Time.realtimeSinceStartupAsDouble:F6}",
                    this);
            }
            if ((smokeInputEnabled || vitalsSmokeStarted) && !smokeResultLogged && Time.realtimeSinceStartupAsDouble >= smokeInputEndsAt)
            {
                smokeResultLogged = true;
                float distance = Vector3.Distance(smokeStartPosition, presentationRoot.position);
                Debug.Log($"[Network Movement] Client observed authoritative movement of {distance:F2} meters for actor '{networkMovement.GetComponent<NetworkPlayerActor>()?.ActorId}'.", this);
            }
        }

        public void Configure(
            LocalGameClient localClient,
            PlayerInputReader inputReader,
            FirstPersonCharacterMotor motor,
            Transform root)
        {
            client = localClient;
            input = inputReader;
            localMotor = motor;
            presentationRoot = root;
        }

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            Bind(actor == null ? null : actor.GetComponent<NetworkPlayerMovement>());
        }

        private void Bind(NetworkPlayerMovement movement)
        {
            if (ReferenceEquals(networkMovement, movement))
            {
                return;
            }

            if (controlsOverridden && localMotor != null)
            {
                localMotor.SetNetworkPredictionMode(false);
                localMotor.ResetTransientMotionForPersistenceRestore();
                localMotor.enabled = localMotorWasEnabled;
            }

            controlsOverridden = false;
            networkMovement = movement;
            networkVitals = movement == null ? null : movement.GetComponent<NetworkPlayerVitals>();
            if (networkMovement == null)
            {
                return;
            }

            if (localMotor != null)
            {
                localMotorWasEnabled = localMotor.enabled;
                localMotor.SetNetworkPredictionMode(true);
                localMotor.ConfigureNetworkPredictionTuning(
                    networkMovement.WalkSpeed,
                    networkMovement.SprintMultiplier,
                    networkMovement.Acceleration,
                    networkMovement.Deceleration,
                    networkMovement.JumpHeight,
                    networkMovement.Gravity,
                    networkMovement.GroundedStickForce);
                controlsOverridden = true;
            }

            inputSendAccumulator = 0d;
            lastInputSampleAt = 0d;
            jumpPredictionGraceUntil = 0d;
            lastSubmittedMove = Vector2.zero;
            lastSubmittedSprint = false;
            predictionSprintExhausted = networkVitals != null
                && networkVitals.HasState
                && networkVitals.CurrentState.Stamina <= 0.0001f;
            predictionSprintAllowed = true;
            clientJumpTraceTick = 0u;
            clientJumpTraceUntil = 0d;
            wasPredictionAirborne = false;
            landingPredictionGraceUntil = 0d;
            smokeStartPosition = networkMovement.transform.position;
            smokeInputEndsAt = Time.realtimeSinceStartupAsDouble + 1.5d;
            smokeResultLogged = false;
            vitalsSmokeStarted = false;
            jumpPending = false;
            if (presentationRoot != null)
            {
                SetPresentationPosition(networkMovement.transform.position);
            }
        }

        private void SetPresentationPosition(Vector3 position)
        {
            if (presentationRoot == null) return;
            CharacterController controller = presentationRoot.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            presentationRoot.position = position;
            if (wasEnabled) controller.enabled = true;
        }

        public static bool AdvanceInputSendAccumulator(ref double accumulator, double elapsed, double interval)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0d
                || double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0d)
            {
                return false;
            }

            accumulator = Math.Max(0d, accumulator) + elapsed;
            if (accumulator + 0.0000001d < interval)
            {
                return false;
            }

            // Only the newest input matters. Preserve the fractional remainder so a
            // 60 FPS render loop averages 60 sends instead of drifting toward 30.
            accumulator %= interval;
            return true;
        }

        public static float CalculateHorizontalPredictionTolerance(
            float baseTolerance,
            float maximumTolerance,
            float maximumSpeed,
            ulong roundTripTimeMilliseconds,
            float sendRate)
        {
            float minimum = Mathf.Max(0f, baseTolerance);
            float maximum = Mathf.Max(minimum, maximumTolerance);
            float roundTripSeconds = Mathf.Min(roundTripTimeMilliseconds / 1000f, 0.5f);
            float commandInterval = 1f / Mathf.Max(1f, sendRate);
            float expectedLead = Mathf.Max(0f, maximumSpeed) * (roundTripSeconds + commandInterval * 2f);
            return Mathf.Clamp(minimum + expectedLead, minimum, maximum);
        }

        public static Vector3 CalculateCorrectionError(
            Vector3 error,
            float horizontalTolerance,
            float verticalTolerance,
            bool suppressHorizontal,
            bool suppressVertical)
        {
            Vector2 horizontal = new Vector2(error.x, error.z);
            float horizontalMagnitude = horizontal.magnitude;
            float allowedHorizontal = Mathf.Max(0f, horizontalTolerance);
            Vector2 correctedHorizontal = !suppressHorizontal
                && horizontalMagnitude > allowedHorizontal
                && horizontalMagnitude > 0.000001f
                ? horizontal * ((horizontalMagnitude - allowedHorizontal) / horizontalMagnitude)
                : Vector2.zero;

            float correctedVertical = 0f;
            if (!suppressVertical)
            {
                float allowedVertical = Mathf.Max(0f, verticalTolerance);
                float verticalMagnitude = Mathf.Abs(error.y);
                if (verticalMagnitude > allowedVertical)
                {
                    correctedVertical = Mathf.Sign(error.y) * (verticalMagnitude - allowedVertical);
                }
            }

            return new Vector3(correctedHorizontal.x, correctedVertical, correctedHorizontal.y);
        }

        public static bool EvaluatePredictedSprintAvailability(
            NetworkVitalsState state,
            float restartThreshold,
            ref bool exhausted)
        {
            if (state.Revision == 0u)
            {
                return true;
            }

            const float epsilon = 0.0001f;
            if (state.Stamina <= epsilon)
            {
                exhausted = true;
            }
            else if (exhausted
                     && state.Stamina > Mathf.Min(state.MaximumStamina, Mathf.Max(0f, restartThreshold)) + epsilon)
            {
                exhausted = false;
            }

            return !state.IsDefeated && !exhausted && state.Stamina > epsilon;
        }

        public static float CalculateLandingPredictionGrace(
            float minimumSeconds,
            float maximumSeconds,
            ulong roundTripTimeMilliseconds)
        {
            float minimum = Mathf.Max(0f, minimumSeconds);
            float maximum = Mathf.Max(minimum, maximumSeconds);
            return Mathf.Clamp(minimum + roundTripTimeMilliseconds / 1000f, minimum, maximum);
        }

        public static bool ShouldApplyControllerCorrection(Vector3 correction)
        {
            return correction.sqrMagnitude > 0.00000001f;
        }

        private void UpdatePredictionSprintAuthorization()
        {
            predictionSprintAllowed = networkVitals == null
                || EvaluatePredictedSprintAvailability(
                    networkVitals.CurrentState,
                    networkVitals.SprintRestartThreshold,
                    ref predictionSprintExhausted);
            if (localMotor != null)
            {
                localMotor.SetNetworkPredictionSprintAllowed(predictionSprintAllowed);
            }
        }

        private ulong GetRoundTripTimeMilliseconds()
        {
            NetworkManager manager = networkMovement == null ? null : networkMovement.NetworkManager;
            NetworkTransport transport = manager == null || manager.NetworkConfig == null
                ? null
                : manager.NetworkConfig.NetworkTransport;
            return transport == null ? 0ul : transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        private void ResolveReferences()
        {
            client = client == null ? GetComponent<LocalGameClient>() : client;
            input = input == null ? FindAnyObjectByType<PlayerInputReader>() : input;
            localMotor = localMotor == null ? FindAnyObjectByType<FirstPersonCharacterMotor>() : localMotor;
            presentationRoot = presentationRoot == null && localMotor != null ? localMotor.transform : presentationRoot;
        }
    }
}
