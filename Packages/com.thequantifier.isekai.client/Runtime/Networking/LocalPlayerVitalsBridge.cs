using System;
using UnityEngine;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerVitalsBridge : MonoBehaviour
    {
        public const string VitalsSmokeFlag = "--vitals-smoke-sprint";

        [SerializeField] private LocalGameClient client;
        [SerializeField] private CharacterResourceCollection localResources;
        [SerializeField] private PlayerInputReader input;

        private NetworkPlayerVitals networkVitals;
        private bool smokeEnabled;
        private float smokeInitialStamina;
        private float smokeLowestStamina;
        private double smokeRecoveryCheckAt;
        private bool smokeSpendLogged;
        private bool smokeRecoveryLogged;
        private bool smokeInitialized;

        public NetworkPlayerVitals BoundVitals => networkVitals;
        public bool IsServerAuthorityActive => networkVitals != null && networkVitals.IsSpawned;

        private void Awake()
        {
            ResolveReferences();
            smokeEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, VitalsSmokeFlag, StringComparison.OrdinalIgnoreCase));
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
            if (!smokeEnabled || !smokeInitialized || networkVitals == null || !networkVitals.IsSpawned)
            {
                return;
            }

            NetworkVitalsState state = networkVitals.CurrentState;
            smokeLowestStamina = Mathf.Min(smokeLowestStamina, state.Stamina);
            if (!smokeSpendLogged && state.Stamina < smokeInitialStamina - 0.01f)
            {
                smokeSpendLogged = true;
                smokeRecoveryCheckAt = Time.realtimeSinceStartupAsDouble + 2d;
                Debug.Log($"[Network Vitals] Client observed authoritative stamina spend: {smokeInitialStamina:F2} -> {state.Stamina:F2}.", this);
            }
            else if (smokeSpendLogged
                     && !smokeRecoveryLogged
                     && Time.realtimeSinceStartupAsDouble >= smokeRecoveryCheckAt
                     && state.Stamina > smokeLowestStamina + 0.01f)
            {
                smokeRecoveryLogged = true;
                Debug.Log($"[Network Vitals] Client observed server-owned recovery: {smokeLowestStamina:F2} -> {state.Stamina:F2}/{state.MaximumStamina:F2} stamina.", this);
            }
        }

        public void Configure(LocalGameClient localClient, CharacterResourceCollection resources, PlayerInputReader inputReader)
        {
            client = localClient;
            localResources = resources;
            input = inputReader;
        }

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            Bind(actor == null ? null : actor.GetComponent<NetworkPlayerVitals>());
        }

        private void Bind(NetworkPlayerVitals vitals)
        {
            if (ReferenceEquals(networkVitals, vitals))
            {
                return;
            }

            if (networkVitals != null)
            {
                networkVitals.StateChanged -= OnStateChanged;
            }

            if (localResources != null)
            {
                localResources.SetExternalReplicaAuthority(false);
            }

            networkVitals = vitals;
            input?.SetDefeatedInputBlocked(false);
            if (networkVitals == null)
            {
                return;
            }

            networkVitals.StateChanged += OnStateChanged;
            if (localResources != null)
            {
                localResources.SetExternalReplicaAuthority(true);
            }

            smokeSpendLogged = false;
            smokeRecoveryLogged = false;
            smokeInitialized = false;
            if (networkVitals.HasState)
            {
                Apply(networkVitals.CurrentState);
                InitializeSmoke(networkVitals.CurrentState);
            }
        }

        private void OnStateChanged(NetworkVitalsState previous, NetworkVitalsState current)
        {
            Apply(current);
            if (!smokeInitialized && current.Revision != 0u)
            {
                InitializeSmoke(current);
            }
        }

        private void InitializeSmoke(NetworkVitalsState state)
        {
            smokeInitialStamina = state.Stamina;
            smokeLowestStamina = smokeInitialStamina;
            smokeInitialized = true;
        }

        private void Apply(NetworkVitalsState state)
        {
            if (localResources != null && localResources.ExternalReplicaAuthorityActive)
            {
                localResources.ApplyExternalReplicaSnapshot(ResourceIds.Health, state.Health, state.MaximumHealth);
                localResources.ApplyExternalReplicaSnapshot(ResourceIds.Stamina, state.Stamina, state.MaximumStamina);
                localResources.ApplyExternalReplicaSnapshot(ResourceIds.Mana, state.Mana, state.MaximumMana);
                localResources.CompleteExternalReplicaSnapshot();
            }

            input?.SetDefeatedInputBlocked(state.IsDefeated);
        }

        private void ResolveReferences()
        {
            client = client == null ? GetComponent<LocalGameClient>() : client;
            input = input == null ? FindAnyObjectByType<PlayerInputReader>() : input;
            localResources = localResources == null && input != null
                ? input.GetComponent<CharacterResourceCollection>()
                : localResources;
        }
    }
}
