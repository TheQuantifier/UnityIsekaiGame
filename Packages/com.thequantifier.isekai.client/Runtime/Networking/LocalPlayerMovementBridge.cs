using System;
using System.Collections.Generic;
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
        [SerializeField, Min(0.5f)] private float hardSnapDistance = 4f;
        [SerializeField, Range(16, 512)] private int predictionHistoryCapacity = 256;

        private NetworkPlayerMovement networkMovement;
        private NetworkPlayerVitals networkVitals;
        private bool localMotorWasEnabled;
        private bool controlsOverridden;
        private double inputSendAccumulator;
        private double lastInputSampleAt;
        private Vector2 lastSubmittedMove;
        private bool lastSubmittedSprint;
        private bool predictionSprintExhausted;
        private bool predictionSprintAllowed = true;
        private uint pendingStopSequence;
        private readonly List<PredictedMovementSample> predictionHistory = new List<PredictedMovementSample>(256);
        private readonly List<uint> pendingPredictedJumpSequences = new List<uint>(4);
        private uint pendingPredictionCaptureSequence;
        private ulong lastReconciledSimulationTick;
        private bool movementTraceEnabled;
        private bool movementTraceDirty;
        private string pendingMovementTracePhase;
        private double nextMovementTraceAt;
        private bool traceCorrectionWasActive;
        private bool traceRequestedSprint;
        private float traceSubmittedYaw;
        private bool smokeInputEnabled;
        private bool vitalsSmokeEnabled;
        private bool vitalsSmokeStarted;
        private double smokeInputEndsAt;
        private Vector3 smokeStartPosition;
        private bool smokeResultLogged;
        private bool jumpPending;

        private struct PredictedMovementSample
        {
            public NetworkMovementInput Input;
            public Vector3 Position;
            public bool Captured;
        }

        public NetworkPlayerMovement BoundMovement => networkMovement;
        public bool IsServerAuthorityActive => networkMovement != null && networkMovement.IsSpawned;

        private void Awake()
        {
            ResolveReferences();
            smokeInputEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, MovementSmokeFlag, StringComparison.OrdinalIgnoreCase));
            vitalsSmokeEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, LocalPlayerVitalsBridge.VitalsSmokeFlag, StringComparison.OrdinalIgnoreCase));
            movementTraceEnabled = NetworkMovementTrace.IsRequested();
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
            RefreshMovementTraceState();
            if (networkMovement == null || !networkMovement.IsSpawned || !networkMovement.IsOwner)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            UpdatePredictionSprintAuthorization();
            if (input != null && input.JumpPressedThisFrame)
            {
                jumpPending = true;
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
            bool wasSubmittingMovement = lastSubmittedMove.sqrMagnitude > 0.0001f;
            bool wasSubmittingSprint = lastSubmittedSprint;
            lastSubmittedMove = move;
            lastSubmittedSprint = sprint && predictionSprintAllowed;
            uint expectedSequence = networkMovement.LastSubmittedSequence + 1u;
            if (expectedSequence == 0u) expectedSequence = 1u;
            string deliveryTimingUtc = movementTraceEnabled && NetworkMovementTrace.ShouldSampleDeliveryTiming(expectedSequence)
                ? NetworkMovementTrace.TimestampUtc()
                : null;
            bool submitted = networkMovement.SubmitLocalInput(move, sprint, jump, yaw);
            bool isSubmittingMovement = move.sqrMagnitude > 0.0001f;
            if (submitted)
            {
                uint sequence = networkMovement.LastSubmittedSequence;
                if (deliveryTimingUtc != null && sequence == expectedSequence)
                {
                    NetworkPlayerActor traceActor = networkMovement.GetComponent<NetworkPlayerActor>();
                    Debug.Log(
                        $"[Movement Timing][ClientSend] utc={deliveryTimingUtc} " +
                        $"actor={traceActor?.ActorId ?? "unknown"} seq={sequence}",
                        this);
                }
                predictionHistory.Add(new PredictedMovementSample
                {
                    Input = new NetworkMovementInput(sequence, move, yaw, lastSubmittedSprint, jump),
                    Position = presentationRoot == null ? networkMovement.AuthoritativePosition : presentationRoot.position,
                    Captured = false
                });
                pendingPredictionCaptureSequence = sequence;
                if (jump)
                {
                    pendingPredictedJumpSequences.Add(sequence);
                }

                TrimPredictionHistory();
            }
            if (submitted && wasSubmittingMovement && !isSubmittingMovement)
            {
                pendingStopSequence = networkMovement.LastSubmittedSequence;
            }

            if (movementTraceEnabled && submitted)
            {
                traceRequestedSprint = sprint;
                traceSubmittedYaw = yaw;
                if (wasSubmittingMovement != isSubmittingMovement)
                {
                    MarkMovementTrace(isSubmittingMovement ? "input-start" : "input-stop");
                }
                else if (jump)
                {
                    MarkMovementTrace("jump");
                }
                else if (wasSubmittingSprint != lastSubmittedSprint)
                {
                    MarkMovementTrace("sprint-change");
                }
            }
        }

        private void LateUpdate()
        {
            if (networkMovement == null || !networkMovement.IsSpawned || presentationRoot == null)
            {
                return;
            }

            CaptureLatestPredictedSample();
            NetworkMovementState authoritativeState = networkMovement.AuthoritativeState;
            Vector3 authoritativePosition = networkMovement.AuthoritativePosition;
            Vector3 reconciliationError = Vector3.zero;
            Vector3 appliedCorrection = Vector3.zero;
            ulong roundTripTimeMilliseconds = GetRoundTripTimeMilliseconds();
            bool airborne = localMotor != null && !localMotor.IsGrounded;
            bool hardSnapApplied = false;
            if (localMotor != null && localMotor.enabled)
            {
                ReconcileAuthoritativeState(
                    authoritativeState,
                    out reconciliationError,
                    out appliedCorrection,
                    out hardSnapApplied);
            }
            else
            {
                presentationRoot.position = authoritativePosition;
            }

            if (movementTraceEnabled && !hardSnapApplied)
            {
                bool correctionActive = ShouldApplyControllerCorrection(appliedCorrection);
                if (correctionActive != traceCorrectionWasActive)
                {
                    MarkMovementTrace(correctionActive ? "correction-start" : "correction-end");
                }
                traceCorrectionWasActive = correctionActive;
            }
            TraceClientMovement(
                authoritativePosition,
                reconciliationError,
                appliedCorrection,
                roundTripTimeMilliseconds,
                airborne,
                false,
                false,
                hardSnapApplied);
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
            lastSubmittedMove = Vector2.zero;
            lastSubmittedSprint = false;
            predictionSprintExhausted = networkVitals != null
                && networkVitals.HasState
                && networkVitals.CurrentState.Stamina <= 0.0001f;
            predictionSprintAllowed = true;
            pendingStopSequence = 0u;
            predictionHistory.Clear();
            pendingPredictedJumpSequences.Clear();
            pendingPredictionCaptureSequence = 0u;
            lastReconciledSimulationTick = 0ul;
            movementTraceDirty = movementTraceEnabled;
            pendingMovementTracePhase = movementTraceEnabled ? "bind" : null;
            nextMovementTraceAt = 0d;
            traceCorrectionWasActive = false;
            traceRequestedSprint = false;
            traceSubmittedYaw = 0f;
            smokeStartPosition = networkMovement.AuthoritativePosition;
            smokeInputEndsAt = Time.realtimeSinceStartupAsDouble + 1.5d;
            smokeResultLogged = false;
            vitalsSmokeStarted = false;
            jumpPending = false;
            if (presentationRoot != null)
            {
                SetPresentationPosition(networkMovement.AuthoritativePosition);
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

        public static float CalculateMovementPredictionGrace(
            float minimumSeconds,
            float maximumSeconds,
            ulong roundTripTimeMilliseconds)
        {
            float minimum = Mathf.Max(0f, minimumSeconds);
            float maximum = Mathf.Max(minimum, maximumSeconds);
            return Mathf.Clamp(minimum + roundTripTimeMilliseconds / 1000f, minimum, maximum);
        }

        public static bool HasAcknowledgedSequence(uint acknowledgedSequence, uint expectedSequence)
        {
            return expectedSequence != 0u
                && (acknowledgedSequence == expectedSequence
                    || NetworkMovementInputValidator.IsNewer(acknowledgedSequence, expectedSequence));
        }

        public static float CalculateStopSettlementGrace(
            float maximumSpeed,
            float deceleration,
            ulong roundTripTimeMilliseconds,
            float sendRate)
        {
            float brakingSeconds = Mathf.Max(0f, maximumSpeed) / Mathf.Max(0.01f, deceleration);
            float networkSeconds = Mathf.Min(roundTripTimeMilliseconds / 1000f, 0.5f);
            float snapshotAllowance = 2f / Mathf.Max(1f, sendRate);
            return Mathf.Clamp(brakingSeconds + networkSeconds + snapshotAllowance, 0.1f, 0.75f);
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

        private void CaptureLatestPredictedSample()
        {
            if (pendingPredictionCaptureSequence == 0u || presentationRoot == null)
            {
                return;
            }

            for (int index = predictionHistory.Count - 1; index >= 0; index--)
            {
                PredictedMovementSample sample = predictionHistory[index];
                if (sample.Input.Sequence != pendingPredictionCaptureSequence)
                {
                    continue;
                }

                sample.Position = presentationRoot.position;
                sample.Captured = true;
                predictionHistory[index] = sample;
                break;
            }

            pendingPredictionCaptureSequence = 0u;
        }

        private void ReconcileAuthoritativeState(
            NetworkMovementState state,
            out Vector3 reconciliationError,
            out Vector3 appliedCorrection,
            out bool hardSnapApplied)
        {
            reconciliationError = Vector3.zero;
            appliedCorrection = Vector3.zero;
            hardSnapApplied = false;
            if (!state.IsInitialized || state.SimulationTick == 0ul || state.SimulationTick == lastReconciledSimulationTick)
            {
                return;
            }

            lastReconciledSimulationTick = state.SimulationTick;
            bool rejectedJump = ResolvePredictedJumpOutcomes(state);
            if (pendingStopSequence != 0u && HasAcknowledgedSequence(state.InputSequence, pendingStopSequence))
            {
                pendingStopSequence = 0u;
                MarkMovementTrace("stop-reconciled");
            }

            int acknowledgedIndex = FindPredictionSample(state.InputSequence);
            if (acknowledgedIndex < 0 || !predictionHistory[acknowledgedIndex].Captured)
            {
                if (rejectedJump && localMotor != null)
                {
                    localMotor.RestoreNetworkPredictionVerticalMotion(
                        state.VerticalVelocity,
                        state.Grounded);
                }
                return;
            }

            PredictedMovementSample acknowledged = predictionHistory[acknowledgedIndex];
            reconciliationError = state.Position - acknowledged.Position;
            Vector3 correctionError = CalculateCorrectionError(
                reconciliationError,
                reconciliationDeadZone,
                reconciliationDeadZone,
                false,
                false);
            if (rejectedJump)
            {
                // A rejected predicted jump must stop its local arc immediately. Retaining a
                // vertical dead zone here is what previously allowed metres of divergence.
                correctionError.y = reconciliationError.y;
            }

            if (reconciliationError.sqrMagnitude >= hardSnapDistance * hardSnapDistance)
            {
                appliedCorrection = reconciliationError;
                hardSnapApplied = true;
                MarkMovementTrace("hard-snap");
            }
            else
            {
                float blend = rejectedJump
                    ? 1f
                    : 1f - Mathf.Exp(-reconciliationSharpness * Time.unscaledDeltaTime);
                appliedCorrection = correctionError * blend;
            }

            if (ShouldApplyControllerCorrection(appliedCorrection))
            {
                SetPresentationPosition(presentationRoot.position + appliedCorrection);
                ShiftPendingPredictions(acknowledgedIndex + 1, appliedCorrection);
            }

            if (rejectedJump && localMotor != null)
            {
                localMotor.RestoreNetworkPredictionVerticalMotion(
                    state.VerticalVelocity,
                    state.Grounded);
                MarkMovementTrace("jump-rejected-reconciled");
            }

            PruneAcknowledgedPredictions(state.InputSequence);
        }

        private bool ResolvePredictedJumpOutcomes(NetworkMovementState state)
        {
            bool rejected = false;
            for (int index = pendingPredictedJumpSequences.Count - 1; index >= 0; index--)
            {
                uint predictedSequence = pendingPredictedJumpSequences[index];
                if (!HasAcknowledgedSequence(state.LastProcessedJumpSequence, predictedSequence))
                {
                    continue;
                }

                if (state.LastExecutedJumpSequence != predictedSequence)
                {
                    rejected = true;
                }
                pendingPredictedJumpSequences.RemoveAt(index);
            }

            return rejected;
        }

        private int FindPredictionSample(uint sequence)
        {
            for (int index = predictionHistory.Count - 1; index >= 0; index--)
            {
                if (predictionHistory[index].Input.Sequence == sequence)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ShiftPendingPredictions(int firstIndex, Vector3 correction)
        {
            for (int index = Mathf.Max(0, firstIndex); index < predictionHistory.Count; index++)
            {
                PredictedMovementSample sample = predictionHistory[index];
                sample.Position += correction;
                predictionHistory[index] = sample;
            }
        }

        private void PruneAcknowledgedPredictions(uint acknowledgedSequence)
        {
            int removeCount = 0;
            while (removeCount < predictionHistory.Count
                   && HasAcknowledgedSequence(acknowledgedSequence, predictionHistory[removeCount].Input.Sequence))
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                predictionHistory.RemoveRange(0, removeCount);
            }
        }

        private void TrimPredictionHistory()
        {
            int capacity = Mathf.Clamp(predictionHistoryCapacity, 16, 512);
            if (predictionHistory.Count > capacity)
            {
                predictionHistory.RemoveRange(0, predictionHistory.Count - capacity);
                MarkMovementTrace("prediction-history-trimmed");
            }
        }

        public static Vector3 CalculateReplayedPosition(
            Vector3 currentPredictedPosition,
            Vector3 authoritativeAcknowledgedPosition,
            Vector3 predictedAcknowledgedPosition)
        {
            return currentPredictedPosition + authoritativeAcknowledgedPosition - predictedAcknowledgedPosition;
        }

        public static bool IsPredictedJumpRejected(
            uint lastProcessedJumpSequence,
            uint lastExecutedJumpSequence,
            uint predictedJumpSequence)
        {
            return HasAcknowledgedSequence(lastProcessedJumpSequence, predictedJumpSequence)
                && lastExecutedJumpSequence != predictedJumpSequence;
        }

        private void MarkMovementTrace(string phase)
        {
            if (!movementTraceEnabled)
            {
                return;
            }

            movementTraceDirty = true;
            pendingMovementTracePhase = phase;
        }

        private void RefreshMovementTraceState()
        {
            bool requested = NetworkMovementTrace.IsRequested();
            if (requested == movementTraceEnabled) return;

            movementTraceEnabled = requested;
            movementTraceDirty = requested;
            pendingMovementTracePhase = requested ? "trace-enabled" : null;
            nextMovementTraceAt = 0d;
            if (!requested) traceCorrectionWasActive = false;
        }

        private void TraceClientMovement(
            Vector3 authoritativePosition,
            Vector3 reconciliationError,
            Vector3 appliedCorrection,
            ulong roundTripTimeMilliseconds,
            bool airborne,
            bool suppressHorizontal,
            bool suppressVertical,
            bool hardSnapApplied)
        {
            if (!movementTraceEnabled)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            bool active = lastSubmittedMove.sqrMagnitude > 0.0001f
                || pendingStopSequence != 0u
                || (localMotor != null && localMotor.CurrentHorizontalSpeed > 0.01f)
                || airborne
                || reconciliationError.sqrMagnitude > reconciliationDeadZone * reconciliationDeadZone;
            if (!movementTraceDirty && (!active || now < nextMovementTraceAt))
            {
                return;
            }

            string phase = string.IsNullOrWhiteSpace(pendingMovementTracePhase)
                ? "sample"
                : pendingMovementTracePhase;
            movementTraceDirty = false;
            pendingMovementTracePhase = null;
            nextMovementTraceAt = now + NetworkMovementTrace.SampleIntervalSeconds;
            NetworkPlayerActor actor = networkMovement.GetComponent<NetworkPlayerActor>();
            NetworkMovementState authoritativeState = networkMovement.AuthoritativeState;
            Debug.Log(
                $"[Movement Trace][Client] utc={NetworkMovementTrace.TimestampUtc()} phase={phase} " +
                $"actor={actor?.ActorId ?? networkMovement.name} seq={networkMovement.LastSubmittedSequence} " +
                $"ack={networkMovement.LastAcceptedSequence} authTick={authoritativeState.SimulationTick} " +
                $"move={NetworkMovementTrace.Format(lastSubmittedMove)} " +
                $"yaw={traceSubmittedYaw:F2} requestedSprint={traceRequestedSprint} allowedSprint={lastSubmittedSprint} " +
                $"predicted={NetworkMovementTrace.Format(presentationRoot.position)} " +
                $"authority={NetworkMovementTrace.Format(authoritativePosition)} " +
                $"error={NetworkMovementTrace.Format(reconciliationError)} " +
                $"correction={NetworkMovementTrace.Format(appliedCorrection)} " +
                $"speed={(localMotor == null ? 0f : localMotor.CurrentHorizontalSpeed):F3} " +
                $"verticalSpeed={(localMotor == null ? 0f : localMotor.VerticalVelocity):F3} " +
                $"grounded={localMotor != null && localMotor.IsGrounded} airborne={airborne} " +
                $"suppressHorizontal={suppressHorizontal} suppressVertical={suppressVertical} " +
                $"pendingStop={pendingStopSequence} history={predictionHistory.Count} " +
                $"processedJump={authoritativeState.LastProcessedJumpSequence} executedJump={authoritativeState.LastExecutedJumpSequence} " +
                $"pendingPredictedJumps={pendingPredictedJumpSequences.Count} hardSnap={hardSnapApplied} rttMs={roundTripTimeMilliseconds}",
                this);
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
