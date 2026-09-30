using System;
using Unity.Netcode;
using UnityEngine;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Player;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
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
        [SerializeField, Min(0.5f)] private float hardSnapDistance = 4f;

        private NetworkPlayerMovement networkMovement;
        private bool localMotorWasEnabled;
        private bool controlsOverridden;
        private double inputSendAccumulator;
        private double lastInputSampleAt;
        private double jumpPredictionGraceUntil;
        private Vector2 lastSubmittedMove;
        private bool lastSubmittedSprint;
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
            if (input != null && input.JumpPressedThisFrame)
            {
                jumpPending = true;
                jumpPredictionGraceUntil = now + jumpPredictionGraceSeconds;
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
            lastSubmittedSprint = sprint;
            networkMovement.SubmitLocalInput(move, sprint, jump, yaw);
        }

        private void LateUpdate()
        {
            if (networkMovement == null || !networkMovement.IsSpawned || presentationRoot == null)
            {
                return;
            }

            Vector3 authoritativePosition = networkMovement.transform.position;
            if (localMotor != null && localMotor.enabled)
            {
                Vector3 error = authoritativePosition - presentationRoot.position;
                if (error.sqrMagnitude >= hardSnapDistance * hardSnapDistance)
                {
                    SetPresentationPosition(authoritativePosition);
                    localMotor.ResetTransientMotionForPersistenceRestore();
                }
                else
                {
                    CharacterController controller = presentationRoot.GetComponent<CharacterController>();
                    float predictionSpeed = lastSubmittedMove.sqrMagnitude > 0.0001f
                        ? lastSubmittedSprint ? networkMovement.SprintSpeed : networkMovement.WalkSpeed
                        : 0f;
                    float horizontalTolerance = CalculateHorizontalPredictionTolerance(
                        reconciliationDeadZone,
                        maximumPredictionLead,
                        predictionSpeed,
                        GetRoundTripTimeMilliseconds(),
                        inputSendRate);
                    bool suppressVertical = Time.realtimeSinceStartupAsDouble < jumpPredictionGraceUntil;
                    float verticalTolerance = controller != null && controller.isGrounded
                        ? groundedVerticalTolerance
                        : airborneVerticalTolerance;
                    Vector3 correctionError = CalculateCorrectionError(
                        error,
                        horizontalTolerance,
                        verticalTolerance,
                        suppressVertical);
                    Vector3 correction = correctionError * (1f - Mathf.Exp(-reconciliationSharpness * Time.unscaledDeltaTime));
                    if (controller != null && controller.enabled) controller.Move(correction);
                    else presentationRoot.position += correction;
                }
            }
            else
            {
                presentationRoot.position = authoritativePosition;
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
            if (networkMovement == null)
            {
                return;
            }

            if (localMotor != null)
            {
                localMotorWasEnabled = localMotor.enabled;
                localMotor.SetNetworkPredictionMode(true);
                controlsOverridden = true;
            }

            inputSendAccumulator = 0d;
            lastInputSampleAt = 0d;
            jumpPredictionGraceUntil = 0d;
            lastSubmittedMove = Vector2.zero;
            lastSubmittedSprint = false;
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
            bool suppressVertical)
        {
            Vector2 horizontal = new Vector2(error.x, error.z);
            float horizontalMagnitude = horizontal.magnitude;
            float allowedHorizontal = Mathf.Max(0f, horizontalTolerance);
            Vector2 correctedHorizontal = horizontalMagnitude > allowedHorizontal && horizontalMagnitude > 0.000001f
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
