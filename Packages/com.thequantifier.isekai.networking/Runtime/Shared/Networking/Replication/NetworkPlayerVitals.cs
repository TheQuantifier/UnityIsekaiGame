using System;
using Unity.Netcode;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerVitals : NetworkBehaviour, IAuthoritativeHealthReceiver
    {
        [Header("Fallback initial state")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(1f)] private float maximumStamina = 100f;
        [SerializeField, Min(1f)] private float maximumMana = 100f;
        [Header("Server simulation")]
        [SerializeField, Min(0f)] private float healthRegenerationPerSecond;
        [SerializeField, Min(0f)] private float staminaRegenerationPerSecond = 15f;
        [SerializeField, Min(0f)] private float manaRegenerationPerSecond = 8f;
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 5f;
        [SerializeField, Min(0f)] private float sprintRestartThreshold = 20f;
        [SerializeField, Min(0f)] private float staminaRegenerationDelayAfterSpend = 1f;
        [SerializeField, Min(0f)] private float manaRegenerationDelayAfterSpend = 1.5f;

        private readonly NetworkVariable<NetworkVitalsState> replicatedState = new NetworkVariable<NetworkVitalsState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private AuthoritativeVitalsModel model;
        private NetworkPlayerActor actor;
        private NetworkVitalsState configuredInitialState;
        private bool hasConfiguredInitialState;
        private float authoritativeDefense;

        public event Action<NetworkVitalsState, NetworkVitalsState> StateChanged;
        public NetworkVitalsState CurrentState => replicatedState.Value;
        public bool HasState => CurrentState.Revision != 0u;
        public bool IsDefeated => CurrentState.IsDefeated;
        public float SprintRestartThreshold => sprintRestartThreshold;
        public bool IsAuthoritativeHealthAvailable => IsSpawned
            && IsServer
            && model != null
            && actor?.IsPausedProtected != true;
        public float AuthoritativeCurrentHealth => CurrentState.Health;
        public bool AuthoritativeDefeated => CurrentState.IsDefeated;

        public void ConfigureFallbackTuning(
            float configuredHealthRegenerationPerSecond,
            float configuredSprintDrainPerSecond,
            float configuredSprintRestartThreshold,
            float configuredStaminaRegenerationPerSecond,
            float configuredManaRegenerationPerSecond,
            float configuredStaminaRegenerationDelay,
            float configuredManaRegenerationDelay)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Vitals tuning cannot change after the network actor is spawned.");
            }

            healthRegenerationPerSecond = Mathf.Max(0f, configuredHealthRegenerationPerSecond);
            sprintDrainPerSecond = Mathf.Max(0f, configuredSprintDrainPerSecond);
            sprintRestartThreshold = Mathf.Max(0f, configuredSprintRestartThreshold);
            staminaRegenerationPerSecond = Mathf.Max(0f, configuredStaminaRegenerationPerSecond);
            manaRegenerationPerSecond = Mathf.Max(0f, configuredManaRegenerationPerSecond);
            staminaRegenerationDelayAfterSpend = Mathf.Max(0f, configuredStaminaRegenerationDelay);
            manaRegenerationDelayAfterSpend = Mathf.Max(0f, configuredManaRegenerationDelay);
        }

        public void ConfigureInitialStateServer(NetworkVitalsState initialState)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Initial vitals must be configured before network spawn.");
            }

            if (!NetworkVitalsStateValidator.TryValidate(initialState, out string failure))
            {
                throw new ArgumentException(failure, nameof(initialState));
            }

            configuredInitialState = initialState;
            hasConfiguredInitialState = true;
        }

        public void ConfigureCombatDefenseServer(float defense)
        {
            if (IsSpawned) throw new InvalidOperationException("Combat defense must be configured before the network actor is spawned.");
            authoritativeDefense = Mathf.Max(0f, defense);
        }

        public override void OnNetworkSpawn()
        {
            actor = GetComponent<NetworkPlayerActor>();
            replicatedState.OnValueChanged += OnReplicatedStateChanged;
            if (!IsServer)
            {
                return;
            }

            NetworkVitalsState initial = hasConfiguredInitialState
                ? configuredInitialState
                : new NetworkVitalsState(maximumHealth, maximumHealth, maximumStamina, maximumStamina, maximumMana, maximumMana, NetworkActorLifeState.Active, 1u);
            model = new AuthoritativeVitalsModel(initial, BuildTuning());
            replicatedState.Value = model.State;
        }

        public override void OnNetworkDespawn()
        {
            replicatedState.OnValueChanged -= OnReplicatedStateChanged;
            model = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || model == null)
            {
                return;
            }

            uint revision = model.State.Revision;
            model.Advance(Time.unscaledDeltaTime, Time.realtimeSinceStartupAsDouble);
            PublishIfChanged(revision);
        }

        public bool EvaluateSprintServer(bool requested, bool moving, float deltaSeconds)
        {
            if (!IsSpawned || !IsServer || model == null || actor?.IsPausedProtected == true)
            {
                return false;
            }

            uint revision = model.State.Revision;
            bool allowed = model.EvaluateSprint(requested, moving, deltaSeconds, Time.realtimeSinceStartupAsDouble);
            PublishIfChanged(revision);
            return allowed;
        }

        public bool TryDamageServer(float amount) => MutateWorldInteraction(value => value.TryDamage(amount));
        public bool TryApplyAuthoritativeDamage(float amount, bool defenseApplies, float minimumDamage, out float appliedAmount)
        {
            if (actor?.IsPausedProtected == true)
            {
                appliedAmount = 0f;
                return false;
            }

            appliedAmount = defenseApplies
                ? Mathf.Max(Mathf.Max(0f, minimumDamage), amount - authoritativeDefense)
                : amount;
            return TryDamageServer(appliedAmount);
        }
        public bool TryHealServer(float amount) => MutateWorldInteraction(value => value.TryHeal(amount));
        public bool TrySpendStaminaServer(float amount) => MutateWorldInteraction(value => value.TrySpendStamina(amount, Time.realtimeSinceStartupAsDouble));
        public bool TrySpendManaServer(float amount) => MutateWorldInteraction(value => value.TrySpendMana(amount, Time.realtimeSinceStartupAsDouble));
        public bool TryRestoreManaServer(float amount) => MutateWorldInteraction(value => value.TryRestoreMana(amount));
        public bool TryRestoreStaminaServer(float amount) => MutateWorldInteraction(value => value.TryRestoreStamina(amount));
        public bool TryReviveServer() => MutateWorldInteraction(value => value.ReviveToMaximum());

        public bool RestoreStateServer(NetworkVitalsState state)
        {
            if (!IsSpawned || !IsServer || !NetworkVitalsStateValidator.TryValidate(state, out _)) return false;
            model = new AuthoritativeVitalsModel(state, BuildTuning());
            replicatedState.Value = model.State;
            return true;
        }

        private bool MutateServer(Func<AuthoritativeVitalsModel, bool> mutation)
        {
            if (!IsSpawned || !IsServer || model == null)
            {
                return false;
            }

            uint revision = model.State.Revision;
            bool changed = mutation(model);
            PublishIfChanged(revision);
            return changed;
        }

        private bool MutateWorldInteraction(Func<AuthoritativeVitalsModel, bool> mutation)
        {
            return actor?.IsPausedProtected == true ? false : MutateServer(mutation);
        }

        private void PublishIfChanged(uint previousRevision)
        {
            if (model.State.Revision != previousRevision)
            {
                replicatedState.Value = model.State;
            }
        }

        private AuthoritativeVitalsTuning BuildTuning()
        {
            return new AuthoritativeVitalsTuning(
                healthRegenerationPerSecond,
                staminaRegenerationPerSecond,
                manaRegenerationPerSecond,
                sprintDrainPerSecond,
                sprintRestartThreshold,
                staminaRegenerationDelayAfterSpend,
                manaRegenerationDelayAfterSpend);
        }

        private void OnReplicatedStateChanged(NetworkVitalsState previous, NetworkVitalsState current)
        {
            StateChanged?.Invoke(previous, current);
        }
    }
}
