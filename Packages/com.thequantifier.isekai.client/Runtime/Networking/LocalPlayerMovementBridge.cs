using System;
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
        [SerializeField, Min(1f)] private float reconciliationSharpness = 14f;
        [SerializeField, Min(0.5f)] private float hardSnapDistance = 4f;

        private NetworkPlayerMovement networkMovement;
        private bool localMotorWasEnabled;
        private bool controlsOverridden;
        private double nextInputSendAt;
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
            if (input != null && input.JumpPressedThisFrame) jumpPending = true;
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

            if (now < nextInputSendAt)
            {
                return;
            }

            nextInputSendAt = now + 1d / Mathf.Max(10f, inputSendRate);
            bool smokeMoving = (smokeInputEnabled || vitalsSmokeStarted) && now < smokeInputEndsAt;
            Vector2 move = smokeMoving ? Vector2.up : input == null ? Vector2.zero : input.Move;
            bool sprint = smokeMoving ? vitalsSmokeEnabled : input != null && input.SprintHeld;
            bool jump = !smokeMoving && jumpPending;
            if (jump) jumpPending = false;
            float yaw = presentationRoot == null ? networkMovement.transform.eulerAngles.y : presentationRoot.eulerAngles.y;
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
                    Vector3 correction = error * (1f - Mathf.Exp(-reconciliationSharpness * Time.unscaledDeltaTime));
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

            nextInputSendAt = 0d;
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

        private void ResolveReferences()
        {
            client = client == null ? GetComponent<LocalGameClient>() : client;
            input = input == null ? FindAnyObjectByType<PlayerInputReader>() : input;
            localMotor = localMotor == null ? FindAnyObjectByType<FirstPersonCharacterMotor>() : localMotor;
            presentationRoot = presentationRoot == null && localMotor != null ? localMotor.transform : presentationRoot;
        }
    }
}
