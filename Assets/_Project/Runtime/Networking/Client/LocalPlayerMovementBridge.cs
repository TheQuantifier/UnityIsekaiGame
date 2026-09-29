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
        [SerializeField, Range(10f, 60f)] private float inputSendRate = 30f;

        private NetworkPlayerMovement networkMovement;
        private bool localMotorWasEnabled;
        private bool controlsOverridden;
        private double nextInputSendAt;
        private bool smokeInputEnabled;
        private double smokeInputEndsAt;
        private Vector3 smokeStartPosition;
        private bool smokeResultLogged;

        public NetworkPlayerMovement BoundMovement => networkMovement;
        public bool IsServerAuthorityActive => networkMovement != null && networkMovement.IsSpawned;

        private void Awake()
        {
            ResolveReferences();
            smokeInputEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, MovementSmokeFlag, StringComparison.OrdinalIgnoreCase));
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
            if (now < nextInputSendAt)
            {
                return;
            }

            nextInputSendAt = now + 1d / Mathf.Max(10f, inputSendRate);
            bool smokeMoving = smokeInputEnabled && now < smokeInputEndsAt;
            Vector2 move = smokeMoving ? Vector2.up : input == null ? Vector2.zero : input.Move;
            bool sprint = !smokeMoving && input != null && input.SprintHeld;
            bool jump = !smokeMoving && input != null && input.ConsumeJump();
            float yaw = presentationRoot == null ? networkMovement.transform.eulerAngles.y : presentationRoot.eulerAngles.y;
            networkMovement.SubmitLocalInput(move, sprint, jump, yaw);
        }

        private void LateUpdate()
        {
            if (networkMovement == null || !networkMovement.IsSpawned || presentationRoot == null)
            {
                return;
            }

            presentationRoot.position = networkMovement.transform.position;
            if (smokeInputEnabled && !smokeResultLogged && Time.realtimeSinceStartupAsDouble >= smokeInputEndsAt)
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
                localMotor.enabled = false;
                controlsOverridden = true;
            }

            nextInputSendAt = 0d;
            smokeStartPosition = networkMovement.transform.position;
            smokeInputEndsAt = Time.realtimeSinceStartupAsDouble + 1.5d;
            smokeResultLogged = false;
            if (presentationRoot != null)
            {
                presentationRoot.position = networkMovement.transform.position;
            }
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
