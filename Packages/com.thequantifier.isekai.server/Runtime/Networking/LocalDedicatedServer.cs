using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class LocalDedicatedServer : MonoBehaviour
    {
        private const int DedicatedServerTargetFrameRate = 60;
        private const float DedicatedServerFixedDeltaTime = 1f / LocalServerEndpoint.DefaultTickRate;
        private const float SpawnGroundProbeHeight = 25f;
        private const float SpawnGroundProbeDistance = 100f;
        private const float MaximumSpawnSurfaceRise = 2f;
        private const float SpawnGroundClearance = 0.02f;

        public const string InventorySmokeSeedFlag = "--inventory-smoke-seed";
        public const string CombatSmokeSeedFlag = "--combat-smoke-seed";
        public const string NarrativeSmokeSeedFlag = "--narrative-smoke-seed";
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string listenAddress = LocalServerEndpoint.DefaultListenAddress;
        [SerializeField] private int serverPort = LocalServerEndpoint.DefaultPort;
        [SerializeField, Min(1)] private int maximumPlayers = 8;
        [SerializeField] private string authenticationToken = string.Empty;
        [SerializeField] private bool startAutomaticallyInServerBuild = true;
        [SerializeField] private GameObject playerActorPrefab;
        [SerializeField] private GameObject combatWorldStatePrefab;
        [SerializeField] private GameObject worldItemPickupPrefab;
        [SerializeField] private Vector3 playerSpawnPosition = new Vector3(-29.2f, 1.1f, 69f);
        [SerializeField] private float playerSpawnYaw;
        [SerializeField] private CharacterController prototypePlayerController;
        [SerializeField] private MonoBehaviour prototypePlayerMotor;
        [SerializeField] private CharacterResourceCollection prototypePlayerResources;
        [SerializeField] private PlayerInventory prototypePlayerInventory;
        [SerializeField] private PlayerEquipment prototypePlayerEquipment;
        [SerializeField] private DefinitionCatalog definitionCatalog;
        [SerializeField] private PlayerMeleeCombat prototypePlayerMeleeCombat;
        [SerializeField] private PlayerSpellLoadout prototypePlayerSpellLoadout;
        [SerializeField] private PrototypePersistenceServiceBehaviour prototypePersistence;
        [SerializeField, Min(1f)] private float playerProfileAutosaveSeconds = 15f;
        [SerializeField, Min(5f)] private float worldCheckpointAutosaveSeconds = 60f;

        private readonly Dictionary<ulong, string> connectedPlayerIds = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, ConnectionRequestPayload> pendingConnections = new Dictionary<ulong, ConnectionRequestPayload>();
        private readonly Dictionary<ulong, NetworkPlayerActor> playerActors = new Dictionary<ulong, NetworkPlayerActor>();
        private readonly PlayerSessionRegistry playerSessions = new PlayerSessionRegistry();
        private readonly Dictionary<string, ServerPlayerProfileData> playerProfiles = new Dictionary<string, ServerPlayerProfileData>(StringComparer.OrdinalIgnoreCase);
        private ServerPlayerProfileStore playerProfileStore;
        private ServerPlayerProfileWriteQueue playerProfileWriteQueue;
        private ServerAccountStore accountStore;
        private float nextPlayerProfileAutosaveAt;
        private float nextWorldCheckpointAutosaveAt;
        private bool serverPersistenceReady;
        private bool ownsServerSession;
        private bool prototypeMovementSuppressed;
        private bool inventorySmokeSeeded;
        private bool prototypeControllerWasEnabled;
        private bool prototypeMotorWasEnabled;
        private bool fixedSimulationRateOverridden;
        private float previousFixedDeltaTime;
        private NetworkCombatWorldState combatWorldState;
        private ServerCombatWorldAuthority combatWorldAuthority;
        private ServerWorldItemAuthority worldItemAuthority;
        private LocalConnectionStatus status = new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server is offline.");

        public event Action<LocalConnectionStatus> StatusChanged;
        public event Action<PlayerSessionSnapshot, NetworkPlayerActor> PlayerSessionStarted;
        public event Action<PlayerSessionSnapshot> PlayerSessionEnded;

        public LocalConnectionStatus Status => status;
        public IReadOnlyDictionary<ulong, string> ConnectedPlayerIds => connectedPlayerIds;
        public IReadOnlyList<PlayerSessionSnapshot> PlayerSessions => playerSessions.ActiveSessions;
        public GameObject PlayerActorPrefab => playerActorPrefab;
        public GameObject CombatWorldStatePrefab => combatWorldStatePrefab;
        public GameObject WorldItemPickupPrefab => worldItemPickupPrefab;
        public Vector3 PlayerSpawnPosition => playerSpawnPosition;
        public float PlayerSpawnYaw => playerSpawnYaw;
        public bool StartAutomaticallyInServerBuild => startAutomaticallyInServerBuild;

        private void Awake()
        {
#if UNITY_SERVER && !UNITY_EDITOR
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = DedicatedServerTargetFrameRate;
#endif
            ResolveReferences();
            prototypePersistence?.SetRuntimeRole(SimulationRuntimeRole.DedicatedServerAuthoritative);
        }

        private void Start()
        {
#if UNITY_SERVER && !UNITY_EDITOR
            TryStartFromCommandLine(startAutomaticallyInServerBuild);
#else
            TryStartFromCommandLine(false);
#endif
        }

        private void OnDisable()
        {
            if (ownsServerSession)
            {
                StopServer();
            }
        }

        private void Update()
        {
            DrainProfileWriteResults();
            if (!ownsServerSession || networkManager == null || !networkManager.IsServer || !serverPersistenceReady) return;
            float now = Time.unscaledTime;
            if (now >= nextPlayerProfileAutosaveAt)
            {
                SaveAllConnectedPlayerProfiles("Scheduled autosave");
                nextPlayerProfileAutosaveAt = now + Mathf.Max(1f, playerProfileAutosaveSeconds);
            }

            if (now >= nextWorldCheckpointAutosaveAt)
            {
                SaveWorldCheckpoint("Scheduled autosave");
                nextWorldCheckpointAutosaveAt = now + Mathf.Max(5f, worldCheckpointAutosaveSeconds);
            }
        }

        public void Configure(NetworkManager manager, string address = LocalServerEndpoint.DefaultListenAddress, int port = LocalServerEndpoint.DefaultPort, int maxPlayers = 8, string token = "")
        {
            networkManager = manager;
            listenAddress = address;
            serverPort = port;
            maximumPlayers = Math.Max(1, maxPlayers);
            authenticationToken = token ?? string.Empty;
        }

        public void ConfigurePlayerActorPrefab(GameObject prefab)
        {
            playerActorPrefab = prefab;
        }

        public void ConfigureCombatWorldStatePrefab(GameObject prefab)
        {
            combatWorldStatePrefab = prefab;
        }

        public void ConfigureWorldItemPickupPrefab(GameObject prefab)
        {
            worldItemPickupPrefab = prefab;
        }

        public void ConfigurePlayerSpawn(Vector3 position, float yawDegrees)
        {
            playerSpawnPosition = position;
            playerSpawnYaw = Mathf.Repeat(yawDegrees, 360f);
        }

        public void ConfigurePrototypePlayerMovement(CharacterController controller, MonoBehaviour motor)
        {
            prototypePlayerController = controller;
            prototypePlayerMotor = motor;
        }

        public void ConfigurePrototypePlayerVitals(CharacterResourceCollection resources)
        {
            prototypePlayerResources = resources;
        }

        public void ConfigurePrototypePlayerInventory(PlayerInventory inventory, PlayerEquipment equipment, DefinitionCatalog catalog)
        {
            prototypePlayerInventory = inventory;
            prototypePlayerEquipment = equipment;
            definitionCatalog = catalog;
        }

        public void ConfigurePrototypeCombat(PlayerMeleeCombat meleeCombat, PlayerSpellLoadout spellLoadout)
        {
            prototypePlayerMeleeCombat = meleeCombat;
            prototypePlayerSpellLoadout = spellLoadout;
        }

        public void ConfigurePrototypeNarrative(PrototypePersistenceServiceBehaviour persistence)
        {
            prototypePersistence = persistence;
            prototypePersistence?.SetRuntimeRole(SimulationRuntimeRole.DedicatedServerAuthoritative);
        }

        public void ConfigurePlayerProfileStore(ServerPlayerProfileStore store)
        {
            playerProfileStore = store;
        }

        public void ConfigureAccountStore(ServerAccountStore store)
        {
            accountStore = store;
        }

        public bool StartServer()
        {
            ResolveReferences();
            if (networkManager == null)
            {
                return Fail("A NetworkManager is required before starting the server.");
            }

            if (networkManager.IsListening)
            {
                return Fail("The NetworkManager is already running.");
            }

            if (!LocalServerEndpoint.TryCreate(listenAddress, serverPort, out LocalServerEndpoint endpoint, out string endpointFailure))
            {
                return Fail(endpointFailure);
            }

            if (!(networkManager.NetworkConfig.NetworkTransport is UnityTransport transport))
            {
                return Fail("The NetworkManager must use Unity Transport.");
            }

            if (!ValidatePlayerActorPrefab(out string prefabFailure))
            {
                return Fail(prefabFailure);
            }

            listenAddress = endpoint.Address;
            serverPort = endpoint.Port;
            connectedPlayerIds.Clear();
            pendingConnections.Clear();
            playerActors.Clear();
            playerSessions.Clear();
            playerProfiles.Clear();
            combatWorldState = null;
            combatWorldAuthority = null;
            worldItemAuthority = null;
            inventorySmokeSeeded = false;
            playerProfileStore ??= new ServerPlayerProfileStore();
            accountStore ??= new ServerAccountStore();
            playerProfileWriteQueue?.Dispose();
            playerProfileWriteQueue = new ServerPlayerProfileWriteQueue(playerProfileStore);
            serverPersistenceReady = TryLoadWorldCheckpoint();
            nextPlayerProfileAutosaveAt = Time.unscaledTime + Mathf.Max(1f, playerProfileAutosaveSeconds);
            nextWorldCheckpointAutosaveAt = Time.unscaledTime + Mathf.Max(5f, worldCheckpointAutosaveSeconds);
            networkManager.NetworkConfig.TickRate = LocalServerEndpoint.DefaultTickRate;
            OverrideFixedSimulationRate();
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            transport.SetConnectionData(LocalServerEndpoint.DefaultClientAddress, endpoint.Port, endpoint.Address);
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.StartingServer, $"Starting local server on {endpoint}.", endpoint));
            SuppressPrototypeMovement();

            ownsServerSession = networkManager.StartServer();
            if (!ownsServerSession)
            {
                Unsubscribe();
                RestorePrototypeMovement();
                RestoreFixedSimulationRate();
                return Fail($"Could not start the local server on {endpoint}.", endpoint);
            }

            try
            {
                worldItemAuthority = GetComponent<ServerWorldItemAuthority>();
                if (worldItemAuthority == null) worldItemAuthority = gameObject.AddComponent<ServerWorldItemAuthority>();
                worldItemAuthority.Configure(networkManager, worldItemPickupPrefab, ResolvePlayerInventoryAuthority);
            }
            catch (Exception exception)
            {
                Unsubscribe();
                networkManager.Shutdown();
                ownsServerSession = false;
                RestorePrototypeMovement();
                RestoreFixedSimulationRate();
                return Fail($"The server could not initialize world item authority: {exception.Message}", endpoint);
            }

            if (!TrySpawnCombatWorldState(out string combatWorldFailure))
            {
                Unsubscribe();
                networkManager.Shutdown();
                ownsServerSession = false;
                RestorePrototypeMovement();
                RestoreFixedSimulationRate();
                return Fail(combatWorldFailure, endpoint);
            }

            if (!worldItemAuthority.TrySpawnScenePickups(out int scenePickupCount, out string scenePickupFailure))
            {
                Unsubscribe();
                networkManager.Shutdown();
                ownsServerSession = false;
                RestorePrototypeMovement();
                RestoreFixedSimulationRate();
                return Fail($"The server could not publish scene pickups: {scenePickupFailure}", endpoint);
            }

            Debug.Log($"[World Items] Published {scenePickupCount} scene-authored pickup(s) through server authority.", this);

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Listening, $"Local server is listening on {endpoint}.", endpoint));
            return true;
        }

        public void StopServer()
        {
            if (!ownsServerSession || networkManager == null)
            {
                DisposeProfileWriteQueue();
                worldItemAuthority?.RestoreScenePickupSources();
                connectedPlayerIds.Clear();
                pendingConnections.Clear();
                playerActors.Clear();
                playerSessions.Clear();
                playerProfiles.Clear();
                serverPersistenceReady = false;
                inventorySmokeSeeded = false;
                combatWorldState = null;
                combatWorldAuthority = null;
                worldItemAuthority = null;
                RestorePrototypeMovement();
                RestoreFixedSimulationRate();
                SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server is offline."));
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Disconnecting, "Stopping the local server.", status.Endpoint));
            SaveAllConnectedPlayerProfiles("Server shutdown");
            SaveWorldCheckpoint("Server shutdown");
            FlushProfileWrites("Server shutdown");
            Unsubscribe();
            networkManager.Shutdown();
            worldItemAuthority?.RestoreScenePickupSources();
            ownsServerSession = false;
            connectedPlayerIds.Clear();
            pendingConnections.Clear();
            playerActors.Clear();
            playerSessions.Clear();
            playerProfiles.Clear();
            serverPersistenceReady = false;
            inventorySmokeSeeded = false;
            combatWorldState = null;
            combatWorldAuthority = null;
            worldItemAuthority = null;
            DisposeProfileWriteQueue();
            RestorePrototypeMovement();
            RestoreFixedSimulationRate();
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server stopped.", status.Endpoint));
        }

        private void OverrideFixedSimulationRate()
        {
            if (!fixedSimulationRateOverridden)
            {
                previousFixedDeltaTime = Time.fixedDeltaTime;
                fixedSimulationRateOverridden = true;
            }

            Time.fixedDeltaTime = DedicatedServerFixedDeltaTime;
        }

        private void RestoreFixedSimulationRate()
        {
            if (!fixedSimulationRateOverridden) return;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            fixedSimulationRateOverridden = false;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            ConnectionAdmissionResult admission = LocalConnectionAdmission.Evaluate(
                request.Payload,
                connectedPlayerIds.Count,
                maximumPlayers,
                connectedPlayerIds.Values,
                Application.version,
                authenticationToken);

            bool approved = admission.Approved;
            string rejectionReason = admission.Reason;
            if (approved)
            {
                accountStore ??= new ServerAccountStore();
                ServerAccountAuthenticationResult accountResult = accountStore.Authenticate(admission.Request);
                approved = accountResult.Succeeded;
                rejectionReason = approved ? string.Empty : accountResult.Message;
                if (approved)
                {
                    admission.Request.PlayerId = accountResult.PlayerId;
                }
            }

            // Authentication is complete at this point. Session admission does not need the
            // plaintext credential, so never retain it in either approved or rejected requests.
            admission.Request?.ClearPassword();

            if (approved)
            {
                // Publish the admission record before completing the NGO response. Some transports can
                // dispatch OnClientConnected as soon as the response is marked non-pending, including
                // re-entrantly during this callback.
                connectedPlayerIds[request.ClientNetworkId] = admission.Request.PlayerId;
                pendingConnections[request.ClientNetworkId] = admission.Request;
                Debug.Log($"[Local Server] Authenticated client {request.ClientNetworkId} as player '{admission.Request.PlayerId}'.", this);
            }
            else
            {
                Debug.LogWarning($"[Local Server] Rejected client {request.ClientNetworkId}: {rejectionReason}", this);
            }

            response.CreatePlayerObject = false;
            response.PlayerPrefabHash = null;
            response.Reason = rejectionReason;
            response.Approved = approved;
            response.Pending = false;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer || clientId == NetworkManager.ServerClientId)
            {
                return;
            }

            if (!pendingConnections.TryGetValue(clientId, out ConnectionRequestPayload request))
            {
                DisconnectClient(clientId, "The approved connection request could not be resolved.");
                return;
            }

            pendingConnections.Remove(clientId);
            bool sessionOpened = playerSessions.TryOpen(clientId, request, out PlayerSessionSnapshot session, out string failure);
            request.ClearPassword();
            if (!sessionOpened)
            {
                DisconnectClient(clientId, failure);
                return;
            }

            if (!TrySpawnPlayerActor(session, out NetworkPlayerActor actor, out failure))
            {
                playerSessions.TryClose(clientId, out _);
                DisconnectClient(clientId, failure);
                return;
            }

            playerActors[clientId] = actor;
            Debug.Log($"[Local Server] Started session '{session.SessionId}' with actor '{session.ActorId}' for player '{session.PlayerId}'.", this);
            PlayerSessionStarted?.Invoke(session, actor);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            SavePlayerProfile(clientId, "Client disconnect");
            combatWorldAuthority?.UnregisterPlayerTarget(clientId);
            pendingConnections.Remove(clientId);
            playerActors.Remove(clientId);
            if (connectedPlayerIds.Remove(clientId))
            {
                Debug.Log($"[Local Server] Client {clientId} disconnected.", this);
            }

            if (playerSessions.TryClose(clientId, out PlayerSessionSnapshot closedSession))
            {
                Debug.Log($"[Local Server] Ended session '{closedSession.SessionId}' for player '{closedSession.PlayerId}'.", this);
                PlayerSessionEnded?.Invoke(closedSession);
            }
        }

        private bool TrySpawnPlayerActor(PlayerSessionSnapshot session, out NetworkPlayerActor actor, out string failure)
        {
            actor = null;
            if (playerActorPrefab == null)
            {
                failure = "The server player actor prefab is not configured.";
                return false;
            }

            NetworkObject prefabNetworkObject = playerActorPrefab.GetComponent<NetworkObject>();
            NetworkPlayerActor prefabActor = playerActorPrefab.GetComponent<NetworkPlayerActor>();
            NetworkPlayerMovement prefabMovement = playerActorPrefab.GetComponent<NetworkPlayerMovement>();
            NetworkPlayerVitals prefabVitals = playerActorPrefab.GetComponent<NetworkPlayerVitals>();
            NetworkPlayerInventory prefabInventory = playerActorPrefab.GetComponent<NetworkPlayerInventory>();
            NetworkPlayerCombat prefabCombat = playerActorPrefab.GetComponent<NetworkPlayerCombat>();
            NetworkPlayerNarrative prefabNarrative = playerActorPrefab.GetComponent<NetworkPlayerNarrative>();
            CharacterController prefabController = playerActorPrefab.GetComponent<CharacterController>();
            if (prefabNetworkObject == null || prefabActor == null || prefabMovement == null || prefabVitals == null ||
                prefabInventory == null || prefabCombat == null || prefabNarrative == null || prefabController == null)
            {
                failure = "The server player actor prefab must contain NetworkObject, CharacterController, and all player identity, movement, vitals, inventory, combat, and narrative replication components.";
                return false;
            }

            if (prototypePlayerInventory == null || prototypePlayerEquipment == null || definitionCatalog == null
                || prototypePlayerMeleeCombat == null || prototypePlayerSpellLoadout == null
                || prototypePersistence == null || prototypePersistence.NarrativeCoordinator == null)
            {
                failure = "The server requires prototype inventory, equipment, combat, narrative, spell-loadout, and definition-catalog state for session initialization.";
                return false;
            }

            if (!TryCreateInitialVitalsState(out NetworkVitalsState prototypeVitals))
            {
                failure = "The prototype player vitals are incomplete and cannot seed a new server profile.";
                return false;
            }

            ServerPlayerProfileData profile = GetOrCreatePlayerProfile(session, prototypeVitals);
            Vector3 effectiveSpawnPosition = profile.Position;
            float effectiveSpawnYaw = profile.yawDegrees;
            if (profile.revision == 1L && HasCommandLineFlag(NarrativeSmokeSeedFlag))
            {
                InteractionPointSceneBinding smokeBinding = FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(value =>
                    {
                        QuestSourceSceneBinding destination = value == null ? null : value.GetComponent<QuestSourceSceneBinding>();
                        return destination != null && !destination.OpensConversation && !destination.IsGuildDeskSurface;
                    });
                if (smokeBinding != null)
                {
                    Vector3 away = Vector3.ProjectOnPlane(smokeBinding.BindingTransform.forward, Vector3.up).normalized;
                    if (away.sqrMagnitude <= 0.0001f) away = Vector3.forward;
                    effectiveSpawnPosition = smokeBinding.BindingTransform.position - away * 1.25f;
                    Vector3 toward = smokeBinding.BindingTransform.position - effectiveSpawnPosition;
                    effectiveSpawnYaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(toward, Vector3.up), Vector3.up).eulerAngles.y;
                }
            }

            Physics.SyncTransforms();
            Vector3 requestedSpawnPosition = effectiveSpawnPosition;
            if (!TryResolveGroundedSpawn(effectiveSpawnPosition, prefabController, out effectiveSpawnPosition))
            {
                if (!TryResolveGroundedSpawn(playerSpawnPosition, prefabController, out effectiveSpawnPosition))
                {
                    failure =
                        $"The server could not find authoritative ground below the saved position {requestedSpawnPosition} " +
                        $"or fallback spawn {playerSpawnPosition}. Verify the server scene collision data.";
                    return false;
                }

                effectiveSpawnYaw = playerSpawnYaw;
                Debug.LogWarning(
                    $"[Local Server] Player '{session.PlayerId}' had an unsupported saved position {requestedSpawnPosition}; " +
                    $"using grounded fallback {effectiveSpawnPosition}.",
                    this);
            }

            GameObject instance = Instantiate(playerActorPrefab, effectiveSpawnPosition, Quaternion.Euler(0f, effectiveSpawnYaw, 0f));
            instance.name = $"Network Player Actor ({session.PlayerId})";
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            actor = instance.GetComponent<NetworkPlayerActor>();
            NetworkPlayerMovement movement = instance.GetComponent<NetworkPlayerMovement>();
            NetworkPlayerVitals vitals = instance.GetComponent<NetworkPlayerVitals>();
            NetworkPlayerInventory replicatedInventory = instance.GetComponent<NetworkPlayerInventory>();
            NetworkPlayerCombat replicatedCombat = instance.GetComponent<NetworkPlayerCombat>();
            NetworkPlayerNarrative replicatedNarrative = instance.GetComponent<NetworkPlayerNarrative>();
            try
            {
                actor.ConfigureServer(session);
                movement.ConfigureSpawnServer(effectiveSpawnPosition, effectiveSpawnYaw);
                vitals.ConfigureInitialStateServer(profile.vitals);
                vitals.ConfigureCombatDefenseServer(CombatStatUtility.GetDefense(prototypePlayerMeleeCombat.gameObject));

                ServerPlayerInventoryAuthority inventoryAuthority = instance.AddComponent<ServerPlayerInventoryAuthority>();
                inventoryAuthority.Configure(
                    replicatedInventory,
                    vitals,
                    definitionCatalog.CreateRegistry(),
                    worldItemAuthority,
                    profile.inventory,
                    profile.equipment,
                    (inventorySave, equipmentSave) => PersistInventoryProfile(session.PlayerId, inventorySave, equipmentSave));

                if (combatWorldAuthority == null)
                {
                    throw new InvalidOperationException("The server combat world and prototype combat profile must be configured before spawning players.");
                }

                ServerPlayerCombatAuthority combatAuthority = instance.AddComponent<ServerPlayerCombatAuthority>();
                combatAuthority.Configure(
                    replicatedCombat,
                    vitals,
                    inventoryAuthority,
                    combatWorldAuthority,
                    definitionCatalog.CreateRegistry(),
                    prototypePlayerMeleeCombat.UnarmedAttack,
                    prototypePlayerSpellLoadout.KnownSpells,
                    CombatStatUtility.GetAttackPower(prototypePlayerMeleeCombat.gameObject));

                ServerPlayerNarrativeAuthority narrativeAuthority = instance.AddComponent<ServerPlayerNarrativeAuthority>();
                narrativeAuthority.Configure(actor, replicatedNarrative, prototypePersistence, inventoryAuthority);

                networkObject.SpawnAsPlayerObject(session.ClientId, true);
                narrativeAuthority.PublishProjection();
                combatWorldAuthority.RegisterPlayerTarget(session.ClientId, instance.transform);
                SavePlayerProfile(session.ClientId, "Session start");
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Destroy(instance);
                actor = null;
                failure = $"The server could not spawn the player actor: {exception.Message}";
                return false;
            }
        }

        private bool TryResolveGroundedSpawn(
            Vector3 requestedPosition,
            CharacterController playerController,
            out Vector3 groundedPosition)
        {
            groundedPosition = requestedPosition;
            if (playerController == null)
            {
                return false;
            }

            Vector3 origin = requestedPosition + Vector3.up * SpawnGroundProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                SpawnGroundProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit[] validHits = hits
                .Where(hit => hit.collider != null &&
                    !(hit.collider is CharacterController) &&
                    hit.point.y <= requestedPosition.y + MaximumSpawnSurfaceRise)
                .OrderByDescending(hit => hit.point.y)
                .ToArray();
            if (validHits.Length == 0)
            {
                return false;
            }

            float controllerBottomOffset = playerController.center.y - playerController.height * 0.5f;
            groundedPosition.y = validHits[0].point.y - controllerBottomOffset +
                Mathf.Max(SpawnGroundClearance, playerController.skinWidth);
            return true;
        }

        private bool ValidatePlayerActorPrefab(out string failure)
        {
            if (playerActorPrefab == null)
            {
                failure = "The server player actor prefab is not configured.";
                return false;
            }

            if (playerActorPrefab.GetComponent<NetworkObject>() == null
                || playerActorPrefab.GetComponent<NetworkPlayerActor>() == null
                || playerActorPrefab.GetComponent<NetworkPlayerMovement>() == null
                || playerActorPrefab.GetComponent<NetworkPlayerVitals>() == null
                || playerActorPrefab.GetComponent<NetworkPlayerInventory>() == null
                || playerActorPrefab.GetComponent<NetworkPlayerCombat>() == null
                || playerActorPrefab.GetComponent<NetworkPlayerNarrative>() == null)
            {
                failure = "The server player actor prefab must contain NetworkObject and all player identity, movement, vitals, inventory, combat, and narrative replication components.";
                return false;
            }

            if (combatWorldStatePrefab == null
                || combatWorldStatePrefab.GetComponent<NetworkObject>() == null
                || combatWorldStatePrefab.GetComponent<NetworkCombatWorldState>() == null)
            {
                failure = "The combat world state prefab must contain NetworkObject and NetworkCombatWorldState components.";
                return false;
            }

            if (worldItemPickupPrefab == null
                || worldItemPickupPrefab.GetComponent<NetworkObject>() == null
                || worldItemPickupPrefab.GetComponent<NetworkWorldItemPickup>() == null
                || worldItemPickupPrefab.GetComponent<Collider>() == null)
            {
                failure = "The world item pickup prefab must contain NetworkObject, NetworkWorldItemPickup, and a Collider.";
                return false;
            }

            foreach (NetworkPrefabsList list in networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (list != null
                    && list.Contains(playerActorPrefab)
                    && list.Contains(combatWorldStatePrefab)
                    && list.Contains(worldItemPickupPrefab))
                {
                    failure = string.Empty;
                    return true;
                }
            }

            failure = "The player actor, combat world state, and world item pickup prefabs must all be registered in the NetworkManager prefab lists.";
            return false;
        }

        private bool TrySpawnCombatWorldState(out string failure)
        {
            if (combatWorldStatePrefab == null)
            {
                failure = "The combat world state prefab is not configured.";
                return false;
            }

            GameObject instance = Instantiate(combatWorldStatePrefab);
            instance.name = "Network Combat World State";
            try
            {
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                combatWorldState = instance.GetComponent<NetworkCombatWorldState>();
                if (networkObject == null || combatWorldState == null)
                    throw new InvalidOperationException("Combat world state prefab is missing required network components.");
                combatWorldAuthority = instance.AddComponent<ServerCombatWorldAuthority>();
                combatWorldAuthority.Configure(combatWorldState, HasCommandLineFlag(CombatSmokeSeedFlag));
                networkObject.Spawn(true);
                combatWorldAuthority.PublishSnapshotNow();
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Destroy(instance);
                combatWorldState = null;
                combatWorldAuthority = null;
                failure = $"The server could not spawn the combat world authority: {exception.Message}";
                return false;
            }
        }

        private void DisconnectClient(ulong clientId, string reason)
        {
            connectedPlayerIds.Remove(clientId);
            pendingConnections.Remove(clientId);
            Debug.LogError($"[Local Server] Disconnecting client {clientId}: {reason}", this);
            networkManager?.DisconnectClient(clientId, reason);
        }

        private void ResolveReferences()
        {
            networkManager = networkManager == null ? GetComponent<NetworkManager>() : networkManager;
            if (worldItemPickupPrefab == null && networkManager != null)
            {
                foreach (NetworkPrefabsList list in networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
                {
                    if (list == null) continue;
                    NetworkPrefab entry = list.PrefabList.FirstOrDefault(value =>
                        value?.Prefab != null && value.Prefab.GetComponent<NetworkWorldItemPickup>() != null);
                    if (entry?.Prefab == null) continue;
                    worldItemPickupPrefab = entry.Prefab;
                    break;
                }
            }
        }

        private ServerPlayerInventoryAuthority ResolvePlayerInventoryAuthority(ulong clientId)
        {
            return playerActors.TryGetValue(clientId, out NetworkPlayerActor actor) && actor != null
                ? actor.GetComponent<ServerPlayerInventoryAuthority>()
                : null;
        }

        private ServerPlayerProfileData GetOrCreatePlayerProfile(PlayerSessionSnapshot session, NetworkVitalsState prototypeVitals)
        {
            if (playerProfiles.TryGetValue(session.PlayerId, out ServerPlayerProfileData existing))
            {
                return existing;
            }

            EnsureInventorySmokeSeed();
            if (playerProfileStore.TryLoad(session, out ServerPlayerProfileData loaded, out string loadMessage))
            {
                playerProfiles[session.PlayerId] = loaded;
                Debug.Log($"[Server Persistence] {loadMessage}", this);
                return loaded;
            }

            ServerPlayerProfileData created = ServerPlayerProfileData.Create(
                session,
                playerSpawnPosition,
                playerSpawnYaw,
                prototypeVitals,
                prototypePlayerInventory.CreateSaveData(),
                prototypePlayerEquipment.CreateSaveData());
            playerProfiles[session.PlayerId] = created;
            Debug.Log($"[Server Persistence] Created a new authoritative profile for '{session.PlayerId}'. {loadMessage}", this);
            return created;
        }

        private void PersistInventoryProfile(string playerId, InventorySaveData inventory, EquipmentSaveData equipment)
        {
            if (!playerProfiles.TryGetValue(playerId, out ServerPlayerProfileData profile)) return;
            profile.inventory = inventory;
            profile.equipment = equipment;
            profile.revision = checked(profile.revision + 1L);
            PersistProfile(profile, "Inventory mutation");
        }

        private void SaveAllConnectedPlayerProfiles(string reason)
        {
            foreach (ulong clientId in playerActors.Keys.ToArray()) SavePlayerProfile(clientId, reason);
        }

        private void SavePlayerProfile(ulong clientId, string reason)
        {
            if (!playerActors.TryGetValue(clientId, out NetworkPlayerActor actor)
                || actor == null
                || !playerProfiles.TryGetValue(actor.PlayerId, out ServerPlayerProfileData profile)) return;

            NetworkPlayerVitals vitals = actor.GetComponent<NetworkPlayerVitals>();
            ServerPlayerInventoryAuthority inventory = actor.GetComponent<ServerPlayerInventoryAuthority>();
            profile.positionX = actor.transform.position.x;
            profile.positionY = actor.transform.position.y;
            profile.positionZ = actor.transform.position.z;
            profile.yawDegrees = Mathf.Repeat(actor.transform.eulerAngles.y, 360f);
            if (vitals != null && vitals.HasState) profile.vitals = vitals.CurrentState;
            if (inventory != null)
            {
                profile.inventory = inventory.CreateInventorySaveData();
                profile.equipment = inventory.CreateEquipmentSaveData();
            }

            profile.revision = checked(profile.revision + 1L);
            PersistProfile(profile, reason);
        }

        private void PersistProfile(ServerPlayerProfileData profile, string reason)
        {
            playerProfileStore ??= new ServerPlayerProfileStore();
            playerProfileWriteQueue ??= new ServerPlayerProfileWriteQueue(playerProfileStore);
            if (playerProfileWriteQueue.TryEnqueue(profile, out string message))
                Debug.Log($"[Server Persistence] {message} Reason={reason}.", this);
            else
                Debug.LogError($"[Server Persistence] {message} Reason={reason}.", this);
        }

        private void DrainProfileWriteResults()
        {
            if (playerProfileWriteQueue == null) return;
            while (playerProfileWriteQueue.TryDequeueResult(out ServerPlayerProfileWriteResult result))
            {
                if (result.Succeeded) Debug.Log($"[Server Persistence] {result.Message}", this);
                else Debug.LogError($"[Server Persistence] {result.Message}", this);
            }
        }

        private void FlushProfileWrites(string reason)
        {
            if (playerProfileWriteQueue == null) return;
            if (!playerProfileWriteQueue.Flush(TimeSpan.FromSeconds(5)))
            {
                Debug.LogError($"[Server Persistence] Timed out flushing profile writes during {reason}.", this);
            }

            DrainProfileWriteResults();
        }

        private void DisposeProfileWriteQueue()
        {
            if (playerProfileWriteQueue == null) return;
            playerProfileWriteQueue.Dispose();
            DrainProfileWriteResults();
            playerProfileWriteQueue = null;
        }

        private bool TryLoadWorldCheckpoint()
        {
            if (prototypePersistence == null || !prototypePersistence.IsInitialized) return false;
            int restoredRuntimePersonCount = prototypePersistence.RegisterAuthoritativeRuntimePersonsFromWorldCheckpoint(
                PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId);
            if (restoredRuntimePersonCount > 0)
            {
                Debug.Log($"[Server Persistence] Restored {restoredRuntimePersonCount} dynamic player identity registration(s) from the world checkpoint envelope.", this);
            }

            PersistenceValidationResult validation = prototypePersistence.WorldService.ValidateSlot(PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId);
            if (validation.Status == PersistenceValidationStatus.FileMissing)
            {
                Debug.Log("[Server Persistence] No world checkpoint exists yet; starting from initialized world state.", this);
                return true;
            }

            if (!validation.Succeeded)
            {
                Debug.LogError($"[Server Persistence] World checkpoint validation failed: {validation.Message}", this);
                return false;
            }

            PersistenceLoadResult result = prototypePersistence.LoadWorldCheckpoint();
            if (!result.Succeeded) Debug.LogError($"[Server Persistence] World checkpoint load failed: {result.Message}", this);
            else Debug.Log($"[Server Persistence] {result.Message}", this);
            return result.Succeeded;
        }

        private void SaveWorldCheckpoint(string reason)
        {
            if (!serverPersistenceReady || prototypePersistence == null) return;
            PersistenceSaveResult result = prototypePersistence.SaveWorldCheckpoint(reason);
            if (!result.Succeeded) Debug.LogError($"[Server Persistence] World checkpoint save failed: {result.Message}", this);
        }

        private void EnsureInventorySmokeSeed()
        {
            if (inventorySmokeSeeded
                || !HasCommandLineFlag(InventorySmokeSeedFlag))
            {
                return;
            }

            inventorySmokeSeeded = true;
            DefinitionRegistry definitions = definitionCatalog.CreateRegistry();
            if (!definitions.TryGet("item.wood-log", out ItemDefinition dropItem)
                || !definitions.TryGet("item.prototype-sword", out ItemDefinition equipItem)
                || !definitions.TryGet("item.stamina-potion", out ItemDefinition useItem))
            {
                Debug.LogWarning("[Network Inventory] One or more inventory smoke seed items are unavailable.", this);
                return;
            }

            InventoryAddResult dropped = prototypePlayerInventory.AddItem(dropItem, 2);
            InventoryAddResult equipped = prototypePlayerInventory.AddItemOrInstances(equipItem, 1);
            InventoryAddResult used = prototypePlayerInventory.AddItem(useItem, 2);
            Debug.Log(
                $"[Network Inventory] Seeded smoke inventory: {dropped.AddedQuantity} {dropItem.DisplayName}, "
                + $"{equipped.AddedQuantity} {equipItem.DisplayName}, and {used.AddedQuantity} {useItem.DisplayName}.",
                this);
        }

        private bool TryCreateInitialVitalsState(out NetworkVitalsState state)
        {
            if (prototypePlayerResources == null
                || !prototypePlayerResources.IsConfigured
                || !prototypePlayerResources.HasResource(ResourceIds.Health)
                || !prototypePlayerResources.HasResource(ResourceIds.Stamina)
                || !prototypePlayerResources.HasResource(ResourceIds.Mana))
            {
                state = default;
                return false;
            }

            float health = prototypePlayerResources.GetCurrent(ResourceIds.Health);
            float maximumStamina = prototypePlayerResources.GetMaximum(ResourceIds.Stamina);
            float stamina = prototypePlayerResources.GetCurrent(ResourceIds.Stamina);
            if (HasCommandLineFlag(InventorySmokeSeedFlag))
            {
                stamina = Mathf.Max(0f, maximumStamina - 50f);
            }

            state = new NetworkVitalsState(
                health,
                prototypePlayerResources.GetMaximum(ResourceIds.Health),
                stamina,
                maximumStamina,
                prototypePlayerResources.GetCurrent(ResourceIds.Mana),
                prototypePlayerResources.GetMaximum(ResourceIds.Mana),
                health <= CharacterResourceCollection.Epsilon ? NetworkActorLifeState.Defeated : NetworkActorLifeState.Active,
                1u);
            return true;
        }

        private static bool HasCommandLineFlag(string flag)
        {
            return Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, flag, StringComparison.OrdinalIgnoreCase));
        }

        private void SuppressPrototypeMovement()
        {
            if (prototypeMovementSuppressed)
            {
                return;
            }

            prototypeControllerWasEnabled = prototypePlayerController != null && prototypePlayerController.enabled;
            prototypeMotorWasEnabled = prototypePlayerMotor != null && prototypePlayerMotor.enabled;
            if (prototypePlayerMotor != null)
            {
                prototypePlayerMotor.enabled = false;
            }

            if (prototypePlayerController != null)
            {
                prototypePlayerController.enabled = false;
            }

            prototypeMovementSuppressed = true;
        }

        private void RestorePrototypeMovement()
        {
            if (!prototypeMovementSuppressed)
            {
                return;
            }

            if (prototypePlayerController != null)
            {
                prototypePlayerController.enabled = prototypeControllerWasEnabled;
            }

            if (prototypePlayerMotor != null)
            {
                prototypePlayerMotor.enabled = prototypeMotorWasEnabled;
            }

            prototypeMovementSuppressed = false;
        }

        private void Unsubscribe()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.ConnectionApprovalCallback = null;
        }

        private bool Fail(string message, LocalServerEndpoint endpoint = default)
        {
            ownsServerSession = false;
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Failed, message, endpoint));
            return false;
        }

        private void SetStatus(LocalConnectionStatus next)
        {
            if (status.Equals(next))
            {
                return;
            }

            status = next;
            if (next.Phase == LocalConnectionPhase.Failed)
            {
                Debug.LogError($"[Local Server] {next.Message}", this);
            }
            else
            {
                Debug.Log($"[Local Server] {next.Message}", this);
            }

            StatusChanged?.Invoke(status);
        }

        private void TryStartFromCommandLine(bool dedicatedServerBuild)
        {
            if (!LocalNetworkCommandLine.TryParse(
                    Environment.GetCommandLineArgs(),
                    dedicatedServerBuild,
                    out LocalNetworkLaunchOptions options,
                    out string failure))
            {
                Fail($"Invalid local-network command line: {failure}");
                return;
            }

            if (options.Mode != LocalNetworkLaunchMode.Server)
            {
                return;
            }

            Configure(networkManager, options.ListenAddress, options.Port, options.MaximumPlayers, options.AuthenticationToken);
            StartServer();
        }

    }
}
