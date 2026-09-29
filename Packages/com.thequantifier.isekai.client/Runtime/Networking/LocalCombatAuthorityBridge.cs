using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    public sealed class LocalCombatAuthorityBridge : MonoBehaviour
    {
        public const string CombatSmokeFlag = "--combat-smoke";
        [SerializeField] private LocalGameClient client;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMeleeCombat localMeleeCombat;
        [SerializeField] private PlayerSpellcaster localSpellcaster;
        [SerializeField] private PlayerSpellLoadout spellLoadout;
        [SerializeField] private Transform aimOrigin;

        private readonly List<NetworkCombatantState> snapshot = new List<NetworkCombatantState>();
        private readonly Dictionary<string, ClientCombatantBinding> combatants = new Dictionary<string, ClientCombatantBinding>(StringComparer.Ordinal);
        private NetworkPlayerCombat networkCombat;
        private NetworkCombatWorldState worldState;
        private uint pendingSnapshotRevision;
        private uint appliedSnapshotRevision;
        private double nextWorldStateSearchAt;
        private bool smokeEnabled;
        private bool smokeRequested;
        private bool smokeCompleted;
        private float smokeInitialMana;
        private float smokeInitialTargetHealth;
        private string smokeTargetId = string.Empty;

        public event Action<string> FeedbackReceived;
        public NetworkPlayerCombat BoundCombat => networkCombat;
        public NetworkCombatWorldState BoundWorldState => worldState;
        public bool IsServerAuthorityActive => networkCombat != null && networkCombat.IsSpawned && networkCombat.IsOwner;

        private void Awake()
        {
            ResolveReferences();
            smokeEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, CombatSmokeFlag, StringComparison.OrdinalIgnoreCase));
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (client == null) return;
            client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
            OnLocalPlayerActorChanged(client.LocalPlayerActor);
        }

        private void OnDisable()
        {
            if (client != null) client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            BindCombat(null);
            BindWorldState(null);
            RestoreLocalCombatants();
        }

        private void Update()
        {
            if (networkCombat == null || !networkCombat.IsSpawned || !networkCombat.IsOwner) return;
            ResolveWorldStateWhenNeeded();
            AdvanceSmoke();
            if (input == null || input.GameplayInputBlocked) return;

            Vector3 aim = ResolveAimDirection();
            if (input.ConsumeAttack()) networkCombat.RequestPrimaryAttack(aim);
            if (input.ConsumeCastPrimarySpell())
            {
                SpellDefinition spell = spellLoadout == null ? null : spellLoadout.SelectedSpell;
                if (spell == null)
                {
                    PublishFeedback("No spell is selected.", warning: true);
                }
                else
                {
                    networkCombat.RequestAbility(spell.Id, aim);
                }
            }
        }

        private void LateUpdate()
        {
            if (pendingSnapshotRevision == 0u) return;
            uint revision = pendingSnapshotRevision;
            pendingSnapshotRevision = 0u;
            ApplySnapshot(revision);
        }

        public void Configure(
            LocalGameClient localClient,
            PlayerInputReader inputReader,
            PlayerMeleeCombat meleeCombat,
            PlayerSpellcaster spellcaster,
            PlayerSpellLoadout loadout,
            Transform configuredAimOrigin)
        {
            client = localClient;
            input = inputReader;
            localMeleeCombat = meleeCombat;
            localSpellcaster = spellcaster;
            spellLoadout = loadout;
            aimOrigin = configuredAimOrigin;
        }

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            BindCombat(actor == null ? null : actor.GetComponent<NetworkPlayerCombat>());
        }

        private void BindCombat(NetworkPlayerCombat combat)
        {
            if (ReferenceEquals(networkCombat, combat)) return;
            if (networkCombat != null) networkCombat.CommandResultChanged -= OnCommandResultChanged;
            networkCombat = combat;
            smokeRequested = false;
            smokeCompleted = false;
            smokeTargetId = string.Empty;
            bool authoritative = networkCombat != null;
            localMeleeCombat?.SetExternalAuthority(authoritative);
            localSpellcaster?.SetExternalAuthority(authoritative);
            if (networkCombat == null)
            {
                BindWorldState(null);
                RestoreLocalCombatants();
                return;
            }

            networkCombat.CommandResultChanged += OnCommandResultChanged;
            nextWorldStateSearchAt = 0d;
            ResolveWorldStateWhenNeeded();
        }

        private void ResolveWorldStateWhenNeeded()
        {
            if (worldState != null && worldState.IsSpawned) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextWorldStateSearchAt) return;
            nextWorldStateSearchAt = now + 0.25d;
            NetworkCombatWorldState found = FindAnyObjectByType<NetworkCombatWorldState>();
            if (found != null && found.IsSpawned) BindWorldState(found);
        }

        private void BindWorldState(NetworkCombatWorldState state)
        {
            if (ReferenceEquals(worldState, state)) return;
            if (worldState != null) worldState.SnapshotChanged -= OnSnapshotChanged;
            worldState = state;
            pendingSnapshotRevision = 0u;
            appliedSnapshotRevision = 0u;
            if (worldState == null) return;
            DiscoverLocalCombatants();
            worldState.SnapshotChanged += OnSnapshotChanged;
            if (worldState.HasSnapshot) QueueSnapshot(worldState.SnapshotRevision);
        }

        private void OnSnapshotChanged(uint revision) => QueueSnapshot(revision);

        private void QueueSnapshot(uint revision)
        {
            if (revision != 0u && NetworkInventoryCommandValidator.IsNewer(revision, appliedSnapshotRevision))
                pendingSnapshotRevision = revision;
        }

        private void DiscoverLocalCombatants()
        {
            RestoreLocalCombatants();
            EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealth health = enemies[i];
                WorldEntityIdentity identity = health == null ? null : health.GetComponentInParent<WorldEntityIdentity>();
                CharacterResourceCollection resources = health == null ? null : health.GetComponent<CharacterResourceCollection>();
                string entityId = identity == null ? string.Empty : identity.EntityId;
                if (health == null || resources == null || string.IsNullOrWhiteSpace(entityId) || combatants.ContainsKey(entityId)) continue;
                combatants.Add(entityId, new ClientCombatantBinding(health, resources));
            }
        }

        private void ApplySnapshot(uint revision)
        {
            if (worldState == null) return;
            worldState.CopySnapshotTo(snapshot);
            if (!NetworkCombatantSnapshotValidator.TryValidate(snapshot, out string failure))
            {
                Debug.LogError($"[Network Combat] Client rejected world snapshot {revision}: {failure}", this);
                return;
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                NetworkCombatantState state = snapshot[i];
                if (!combatants.TryGetValue(state.EntityIdText, out ClientCombatantBinding binding)) continue;
                binding.Apply(state);
            }

            appliedSnapshotRevision = revision;
            AdvanceSmoke();
        }

        private void RestoreLocalCombatants()
        {
            foreach (ClientCombatantBinding binding in combatants.Values) binding.Restore();
            combatants.Clear();
        }

        private void OnCommandResultChanged(NetworkCombatCommandResult result)
        {
            PublishFeedback(result.MessageText, !result.Succeeded);
            if (smokeEnabled && result.Succeeded && result.AppliedAmount > 0f)
            {
                AdvanceSmoke();
            }
        }

        private void AdvanceSmoke()
        {
            if (!smokeEnabled || smokeCompleted || networkCombat == null || !networkCombat.IsSpawned || !networkCombat.IsOwner
                || worldState == null || !worldState.HasSnapshot) return;

            NetworkPlayerVitals vitals = networkCombat.GetComponent<NetworkPlayerVitals>();
            SpellDefinition spell = spellLoadout == null ? null : spellLoadout.SelectedSpell;
            if (vitals == null || !vitals.HasState || spell == null) return;

            if (!smokeRequested)
            {
                worldState.CopySnapshotTo(snapshot);
                if (snapshot.Count == 0) return;
                smokeTargetId = snapshot[0].EntityIdText;
                smokeInitialTargetHealth = snapshot[0].Health;
                smokeInitialMana = vitals.CurrentState.Mana;
                if (networkCombat.RequestAbility(spell.Id, networkCombat.transform.forward))
                {
                    smokeRequested = true;
                    Debug.Log($"[Network Combat] Client requested authoritative {spell.DisplayName} smoke cast at '{smokeTargetId}'.", this);
                }
                return;
            }

            worldState.CopySnapshotTo(snapshot);
            NetworkCombatantState current = snapshot.Find(value => string.Equals(value.EntityIdText, smokeTargetId, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(current.EntityIdText)
                || current.Health >= smokeInitialTargetHealth - 0.01f
                || vitals.CurrentState.Mana >= smokeInitialMana - 0.01f) return;

            smokeCompleted = true;
            Debug.Log($"[Network Combat] Authoritative combat smoke completed: mana {smokeInitialMana:0.##} -> {vitals.CurrentState.Mana:0.##}, target Health {smokeInitialTargetHealth:0.##} -> {current.Health:0.##}.", this);
            client?.Disconnect();
        }

        private void PublishFeedback(string message, bool warning)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (warning) Debug.LogWarning($"[Network Combat] {message}", this);
            else Debug.Log($"[Network Combat] {message}", this);
            GameHudMessageBus.Show(message, warning ? GameHudMessageTone.Warning : GameHudMessageTone.Information);
            FeedbackReceived?.Invoke(message);
        }

        private Vector3 ResolveAimDirection()
        {
            Transform origin = aimOrigin;
            if (origin == null && Camera.main != null) origin = Camera.main.transform;
            Vector3 direction = origin == null ? transform.forward : origin.forward;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private void ResolveReferences()
        {
            client = client == null ? GetComponent<LocalGameClient>() : client;
            input = input == null ? FindAnyObjectByType<PlayerInputReader>() : input;
            localMeleeCombat = localMeleeCombat == null && input != null ? input.GetComponent<PlayerMeleeCombat>() : localMeleeCombat;
            localSpellcaster = localSpellcaster == null && input != null ? input.GetComponent<PlayerSpellcaster>() : localSpellcaster;
            spellLoadout = spellLoadout == null && input != null ? input.GetComponent<PlayerSpellLoadout>() : spellLoadout;
            if (aimOrigin == null && Camera.main != null) aimOrigin = Camera.main.transform;
        }

        private sealed class ClientCombatantBinding
        {
            private readonly EnemyHealth health;
            private readonly CharacterResourceCollection resources;
            private readonly PrototypeEnemyController controller;
            private readonly PrototypeEnemyPatrolController patrol;
            private readonly EnemyMeleeAttack attack;
            private readonly CharacterController characterController;
            private readonly bool controllerEnabled;
            private readonly bool patrolEnabled;
            private readonly bool attackEnabled;
            private readonly bool characterControllerEnabled;

            public ClientCombatantBinding(EnemyHealth targetHealth, CharacterResourceCollection targetResources)
            {
                health = targetHealth;
                resources = targetResources;
                controller = health.GetComponent<PrototypeEnemyController>();
                patrol = health.GetComponent<PrototypeEnemyPatrolController>();
                attack = health.GetComponent<EnemyMeleeAttack>();
                characterController = health.GetComponent<CharacterController>();
                controllerEnabled = controller != null && controller.enabled;
                patrolEnabled = patrol != null && patrol.enabled;
                attackEnabled = attack != null && attack.enabled;
                characterControllerEnabled = characterController != null && characterController.enabled;
                if (controller != null) controller.enabled = false;
                if (patrol != null) patrol.enabled = false;
                if (attack != null) attack.enabled = false;
                if (characterController != null) characterController.enabled = false;
                resources.SetExternalReplicaAuthority(true);
            }

            public void Apply(NetworkCombatantState state)
            {
                if (health == null || resources == null) return;
                health.transform.SetPositionAndRotation(state.Position, state.Rotation);
                resources.ApplyExternalReplicaSnapshot(ResourceIds.Health, state.Health, state.MaximumHealth);
                resources.CompleteExternalReplicaSnapshot();
            }

            public void Restore()
            {
                if (resources != null) resources.SetExternalReplicaAuthority(false);
                if (controller != null) controller.enabled = controllerEnabled;
                if (patrol != null) patrol.enabled = patrolEnabled;
                if (attack != null) attack.enabled = attackEnabled;
                if (characterController != null) characterController.enabled = characterControllerEnabled;
            }
        }
    }
}
