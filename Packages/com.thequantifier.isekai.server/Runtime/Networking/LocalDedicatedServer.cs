using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
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
        [SerializeField, Min(0f)] private float worldCheckpointAutosaveOffsetSeconds = 5f;
        [SerializeField, Min(0.1f)] private float captureWarningMilliseconds = 8f;
        [SerializeField, Min(0.1f)] private float captureCriticalMilliseconds = 16.667f;
        [SerializeField, Min(1024)] private long captureAllocationWarningBytes = 1048576L;
        [SerializeField, Min(0f)] private float idleShutdownSeconds = ServerIdleShutdownPolicy.DefaultTimeoutSeconds;

        private readonly Dictionary<ulong, string> connectedPlayerIds = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, ConnectionRequestPayload> pendingConnections = new Dictionary<ulong, ConnectionRequestPayload>();
        private readonly Dictionary<ulong, double> pendingConnectionAcceptedAt = new Dictionary<ulong, double>();
        private readonly Dictionary<ulong, int> failedAuthenticationCounts = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, float> nextAuthenticationAt = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> authenticationInFlight = new HashSet<ulong>();
        private readonly Dictionary<ulong, double> authenticationStartedAt = new Dictionary<ulong, double>();
        private readonly ConcurrentQueue<AccountAuthenticationWorkResult> authenticationResults = new ConcurrentQueue<AccountAuthenticationWorkResult>();
        private readonly ConcurrentQueue<PlayerProfileLoadWorkResult> playerProfileLoadResults = new ConcurrentQueue<PlayerProfileLoadWorkResult>();
        private readonly ServerIdleShutdownPolicy idleShutdownPolicy = new ServerIdleShutdownPolicy();
        private readonly Dictionary<ulong, NetworkPlayerActor> playerActors = new Dictionary<ulong, NetworkPlayerActor>();
        private readonly PlayerSessionRegistry playerSessions = new PlayerSessionRegistry();
        private readonly Dictionary<string, ServerPlayerProfileData> playerProfiles = new Dictionary<string, ServerPlayerProfileData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ServerPlayerProfileData> lastQueuedPlayerProfiles = new Dictionary<string, ServerPlayerProfileData>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> completedPlayerProfileLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> playerProfileLoadMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private ServerPlayerProfileStore playerProfileStore;
        private ServerPlayerProfileWriteQueue playerProfileWriteQueue;
        private ServerWorldCheckpointWriteQueue worldCheckpointWriteQueue;
        private PreparedPersistenceSaveCapture worldCheckpointCapture;
        private string worldCheckpointCaptureReason = string.Empty;
        private double worldCheckpointCaptureCpuMilliseconds;
        private double worldCheckpointMaximumCaptureTickMilliseconds;
        private string worldCheckpointMaximumCaptureParticipant = string.Empty;
        private int worldCheckpointCaptureFrames;
        private readonly List<ServerPersistenceCaptureSample> worldCheckpointCaptureSamples =
            new List<ServerPersistenceCaptureSample>();
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
        private int serverGeneration;
        private float previousFixedDeltaTime;
        private NetworkCombatWorldState combatWorldState;
        private ServerCombatWorldAuthority combatWorldAuthority;
        private ServerWorldItemAuthority worldItemAuthority;
        private DefinitionRegistry cachedDefinitionRegistry;
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
        public float IdleShutdownSeconds => idleShutdownSeconds;

        private void Awake()
        {
#if UNITY_SERVER && !UNITY_EDITOR
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = DedicatedServerTargetFrameRate;
#endif
            if (GetComponent<ServerFrameStallMonitor>() == null)
            {
                gameObject.AddComponent<ServerFrameStallMonitor>();
            }
            ResolveReferences();
            cachedDefinitionRegistry = definitionCatalog == null ? null : definitionCatalog.CreateRegistry();
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
            using (NetworkMovementTrace.MeasureServerPhase("ProfileWriteResults"))
            {
                DrainProfileWriteResults();
            }
            using (NetworkMovementTrace.MeasureServerPhase("WorldCheckpointWriteResults"))
            {
                DrainWorldCheckpointWriteResults();
            }
            using (NetworkMovementTrace.MeasureServerPhase("WorldCheckpointCapture"))
            {
                AdvanceWorldCheckpointCapture(1);
            }
            using (NetworkMovementTrace.MeasureServerPhase("AuthenticationResults"))
            {
                DrainAuthenticationResults();
                DrainPlayerProfileLoadResults();
            }
            if (!ownsServerSession || networkManager == null || !networkManager.IsServer) return;
            if (AdvanceIdleShutdown()) return;
            using (NetworkMovementTrace.MeasureServerPhase("ConnectionExpiry"))
            {
                ExpireUnauthenticatedConnections();
            }
            if (!serverPersistenceReady) return;
            float now = Time.unscaledTime;
            bool capturedProfilesThisFrame = false;
            if (now >= nextPlayerProfileAutosaveAt)
            {
                using (NetworkMovementTrace.MeasureServerPhase("PlayerProfileCapture"))
                {
                    SaveAllConnectedPlayerProfiles("Scheduled autosave");
                }
                nextPlayerProfileAutosaveAt = now + Mathf.Max(1f, playerProfileAutosaveSeconds);
                capturedProfilesThisFrame = true;
            }

            if (now >= nextWorldCheckpointAutosaveAt)
            {
                if (capturedProfilesThisFrame)
                {
                    nextWorldCheckpointAutosaveAt = now + 0.25f;
                }
                else
                {
                    using (NetworkMovementTrace.MeasureServerPhase("WorldCheckpointStart"))
                    {
                        SaveWorldCheckpoint("Scheduled autosave");
                    }
                    nextWorldCheckpointAutosaveAt = now + Mathf.Max(5f, worldCheckpointAutosaveSeconds);
                }
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
            pendingConnectionAcceptedAt.Clear();
            failedAuthenticationCounts.Clear();
            nextAuthenticationAt.Clear();
            authenticationInFlight.Clear();
            authenticationStartedAt.Clear();
            while (authenticationResults.TryDequeue(out _)) { }
            while (playerProfileLoadResults.TryDequeue(out _)) { }
            serverGeneration = checked(serverGeneration + 1);
            playerActors.Clear();
            playerSessions.Clear();
            playerProfiles.Clear();
            lastQueuedPlayerProfiles.Clear();
            completedPlayerProfileLoads.Clear();
            playerProfileLoadMessages.Clear();
            combatWorldState = null;
            combatWorldAuthority = null;
            worldItemAuthority = null;
            inventorySmokeSeeded = false;
            playerProfileStore ??= new ServerPlayerProfileStore();
            accountStore ??= new ServerAccountStore();
            playerProfileWriteQueue?.Dispose();
            playerProfileWriteQueue = new ServerPlayerProfileWriteQueue(playerProfileStore);
            DisposeWorldCheckpointWriteQueue();
            serverPersistenceReady = TryLoadWorldCheckpoint();
            if (serverPersistenceReady && prototypePersistence?.WorldService != null)
            {
                worldCheckpointWriteQueue = new ServerWorldCheckpointWriteQueue(prototypePersistence.WorldService);
            }
            nextPlayerProfileAutosaveAt = Time.unscaledTime + Mathf.Max(1f, playerProfileAutosaveSeconds);
            nextWorldCheckpointAutosaveAt = Time.unscaledTime
                + Mathf.Max(5f, worldCheckpointAutosaveSeconds)
                + Mathf.Max(0f, worldCheckpointAutosaveOffsetSeconds);
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

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                AccountAuthenticationProtocol.RequestMessageName,
                OnAccountAuthenticationRequest);

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
            idleShutdownPolicy.Reset();
            return true;
        }

        public void StopServer()
        {
            idleShutdownPolicy.Reset();
            if (!ownsServerSession || networkManager == null)
            {
                DisposeProfileWriteQueue();
                DisposeWorldCheckpointWriteQueue();
                worldItemAuthority?.RestoreScenePickupSources();
                connectedPlayerIds.Clear();
                pendingConnections.Clear();
                pendingConnectionAcceptedAt.Clear();
                failedAuthenticationCounts.Clear();
                nextAuthenticationAt.Clear();
                authenticationInFlight.Clear();
                authenticationStartedAt.Clear();
                serverGeneration = checked(serverGeneration + 1);
                playerActors.Clear();
                playerSessions.Clear();
                playerProfiles.Clear();
                lastQueuedPlayerProfiles.Clear();
                completedPlayerProfileLoads.Clear();
                playerProfileLoadMessages.Clear();
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
            CompleteWorldCheckpointCaptureImmediately();
            FlushWorldCheckpointWrites("pending autosave before server shutdown");
            SaveWorldCheckpoint("Server shutdown");
            CompleteWorldCheckpointCaptureImmediately();
            FlushWorldCheckpointWrites("server shutdown");
            FlushProfileWrites("Server shutdown");
            Unsubscribe();
            networkManager.Shutdown();
            worldItemAuthority?.RestoreScenePickupSources();
            ownsServerSession = false;
            connectedPlayerIds.Clear();
            pendingConnections.Clear();
            pendingConnectionAcceptedAt.Clear();
            failedAuthenticationCounts.Clear();
            nextAuthenticationAt.Clear();
            authenticationInFlight.Clear();
            authenticationStartedAt.Clear();
            serverGeneration = checked(serverGeneration + 1);
            playerActors.Clear();
            playerSessions.Clear();
            playerProfiles.Clear();
            lastQueuedPlayerProfiles.Clear();
            completedPlayerProfileLoads.Clear();
            playerProfileLoadMessages.Clear();
            serverPersistenceReady = false;
            inventorySmokeSeeded = false;
            combatWorldState = null;
            combatWorldAuthority = null;
            worldItemAuthority = null;
            DisposeProfileWriteQueue();
            DisposeWorldCheckpointWriteQueue();
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
                connectedPlayerIds.Count + pendingConnections.Count,
                maximumPlayers,
                connectedPlayerIds.Values,
                Application.version,
                authenticationToken);

            bool approved = admission.Approved;
            string rejectionReason = admission.Reason;
            if (approved)
            {
                // Publish the admission record before completing the NGO response. Some transports can
                // dispatch OnClientConnected as soon as the response is marked non-pending, including
                // re-entrantly during this callback.
                pendingConnections[request.ClientNetworkId] = admission.Request;
                pendingConnectionAcceptedAt[request.ClientNetworkId] = Time.realtimeSinceStartupAsDouble;
                Debug.Log($"[Local Server] App-authenticated client {request.ClientNetworkId}; awaiting account login.", this);
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

            idleShutdownPolicy.Reset();
            if (!pendingConnections.TryGetValue(clientId, out ConnectionRequestPayload request))
            {
                DisconnectClient(clientId, "The approved connection request could not be resolved.");
                return;
            }

            Debug.Log($"[Local Server] Client app {clientId} connected and is awaiting account authentication.", this);
        }

        private void OnAccountAuthenticationRequest(ulong clientId, FastBufferReader reader)
        {
            if (!pendingConnections.ContainsKey(clientId)
                || connectedPlayerIds.ContainsKey(clientId))
            {
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty,
                    "The client is not eligible to authenticate an account."));
                return;
            }

            if (nextAuthenticationAt.TryGetValue(clientId, out float allowedAt) && Time.unscaledTime < allowedAt)
            {
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty,
                    "Too many login attempts. Please wait a moment and try again."));
                return;
            }

            if (!authenticationInFlight.Add(clientId))
            {
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty,
                    "An account authentication request is already in progress."));
                return;
            }

            authenticationStartedAt[clientId] = Time.realtimeSinceStartupAsDouble;

            if (!TryReadAuthenticationPayload(reader, out byte[] payload, out string failure)
                || !AccountAuthenticationProtocol.TryDecodeRequest(payload, out AccountAuthenticationRequest request, out failure))
            {
                authenticationInFlight.Remove(clientId);
                authenticationStartedAt.Remove(clientId);
                RegisterAuthenticationFailure(clientId);
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty, failure));
                return;
            }

            NetworkActionTrace.ServerReceive(
                NetworkActionTraceCategory.Authentication,
                request.Mode == AccountAuthenticationMode.CreateAccount ? "CreateAccount" : "Login",
                request.Mode == AccountAuthenticationMode.CreateAccount ? "authentication-create" : "authentication-login",
                clientId,
                context: this);

            accountStore ??= new ServerAccountStore();
            ServerAccountStore store = accountStore;
            int generation = serverGeneration;
            _ = Task.Run(() =>
            {
                ServerAccountAuthenticationResult result;
                try { result = store.Authenticate(request); }
                catch (Exception)
                {
                    request.ClearPassword();
                    result = new ServerAccountAuthenticationResult(
                        ServerAccountAuthenticationStatus.StorageFailure,
                        string.Empty,
                        string.Empty,
                        "Account services are temporarily unavailable. Please try again.");
                }
                authenticationResults.Enqueue(new AccountAuthenticationWorkResult(generation, clientId, result));
            });
        }

        private void DrainAuthenticationResults()
        {
            while (authenticationResults.TryDequeue(out AccountAuthenticationWorkResult work))
            {
                if (work.ServerGeneration != serverGeneration) continue;
                CompleteAccountAuthentication(work.ClientId, work.Result);
            }
        }

        private void CompleteAccountAuthentication(ulong clientId, ServerAccountAuthenticationResult result)
        {
            if (!ownsServerSession
                || !pendingConnections.TryGetValue(clientId, out ConnectionRequestPayload appRequest)
                || connectedPlayerIds.ContainsKey(clientId)) return;

            if (!result.Succeeded)
            {
                CompleteAuthenticationWork(clientId);
                RegisterAuthenticationFailure(clientId);
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty, result.Message));
                return;
            }

            if (connectedPlayerIds.Values.Any(id => string.Equals(id, result.UserId, StringComparison.Ordinal)))
            {
                CompleteAuthenticationWork(clientId);
                RegisterAuthenticationFailure(clientId);
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty,
                    "That account is already signed in."));
                return;
            }

            if (!playerSessions.TryOpen(clientId, appRequest.ClientInstanceId, result.UserId, out PlayerSessionSnapshot session, out string failure))
            {
                CompleteAuthenticationWork(clientId);
                playerSessions.TryClose(clientId, out _);
                SendAuthenticationResponse(clientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty, failure));
                return;
            }

            playerProfileStore ??= new ServerPlayerProfileStore();
            ServerPlayerProfileStore profileStore = playerProfileStore;
            int generation = serverGeneration;
            _ = Task.Run(() =>
            {
                ServerPlayerProfileData profile = null;
                bool loaded = false;
                string loadMessage;
                try
                {
                    loaded = profileStore.TryLoad(session, out profile, out loadMessage);
                }
                catch (Exception exception)
                {
                    loadMessage = $"Server profile loading failed: {exception.Message}";
                }

                playerProfileLoadResults.Enqueue(new PlayerProfileLoadWorkResult(
                    generation,
                    clientId,
                    result,
                    session,
                    loaded,
                    profile,
                    loadMessage));
            });
        }

        private void DrainPlayerProfileLoadResults()
        {
            while (playerProfileLoadResults.TryDequeue(out PlayerProfileLoadWorkResult work))
            {
                if (work.ServerGeneration != serverGeneration) continue;
                CompleteAuthenticationWork(work.ClientId);
                if (!ownsServerSession
                    || !pendingConnections.ContainsKey(work.ClientId)
                    || !playerSessions.TryGetByClientId(work.ClientId, out PlayerSessionSnapshot activeSession)
                    || !string.Equals(activeSession.SessionId, work.Session.SessionId, StringComparison.Ordinal))
                {
                    playerSessions.TryClose(work.ClientId, out _);
                    continue;
                }

                completedPlayerProfileLoads.Add(work.Session.PlayerId);
                playerProfileLoadMessages[work.Session.PlayerId] = work.LoadMessage;
                if (work.Loaded && work.Profile != null)
                {
                    playerProfiles[work.Session.PlayerId] = work.Profile;
                    lastQueuedPlayerProfiles[work.Session.PlayerId] = work.Profile.Clone();
                    Debug.Log($"[Server Persistence] {work.LoadMessage}", this);
                }

                if (!TrySpawnPlayerActor(work.Session, out NetworkPlayerActor actor, out string failure))
                {
                    playerSessions.TryClose(work.ClientId, out _);
                    SendAuthenticationResponse(work.ClientId, new AccountAuthenticationResponse(false, false, string.Empty, string.Empty, failure));
                    continue;
                }

                failedAuthenticationCounts.Remove(work.ClientId);
                nextAuthenticationAt.Remove(work.ClientId);
                pendingConnections.Remove(work.ClientId);
                pendingConnectionAcceptedAt.Remove(work.ClientId);
                connectedPlayerIds[work.ClientId] = work.Authentication.UserId;
                playerActors[work.ClientId] = actor;
                SendAuthenticationResponse(work.ClientId, new AccountAuthenticationResponse(
                true,
                    work.Authentication.Status == ServerAccountAuthenticationStatus.Created,
                    work.Authentication.UserId,
                    work.Authentication.Username,
                    work.Authentication.Message));
                Debug.Log($"[Local Server] Started session '{work.Session.SessionId}' for account '{work.Authentication.Username}' ({work.Authentication.UserId}).", this);
                PlayerSessionStarted?.Invoke(work.Session, actor);
            }
        }

        private void CompleteAuthenticationWork(ulong clientId)
        {
            authenticationInFlight.Remove(clientId);
            authenticationStartedAt.Remove(clientId);
        }

        private void RegisterAuthenticationFailure(ulong clientId)
        {
            int failures = failedAuthenticationCounts.TryGetValue(clientId, out int count) ? count + 1 : 1;
            failedAuthenticationCounts[clientId] = failures;
            nextAuthenticationAt[clientId] = Time.unscaledTime + Mathf.Min(10f, 0.5f * Mathf.Pow(2f, Mathf.Min(failures - 1, 5)));
        }

        private void SendAuthenticationResponse(ulong clientId, AccountAuthenticationResponse response)
        {
            if (networkManager?.CustomMessagingManager == null) return;
            if (!AccountAuthenticationProtocol.TryEncodeResponse(response, out byte[] payload, out string failure))
            {
                Debug.LogError($"[Local Server] Could not encode account response: {failure}", this);
                return;
            }

            using var writer = new FastBufferWriter(sizeof(int) + payload.Length, Allocator.Temp);
            writer.WriteValueSafe(payload.Length);
            writer.WriteBytesSafe(payload);
            networkManager.CustomMessagingManager.SendNamedMessage(
                AccountAuthenticationProtocol.ResponseMessageName,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private static bool TryReadAuthenticationPayload(FastBufferReader reader, out byte[] payload, out string failure)
        {
            payload = Array.Empty<byte>();
            try
            {
                reader.ReadValueSafe(out int length);
                if (length < 1 || length > AccountAuthenticationProtocol.MaximumPayloadBytes)
                {
                    failure = "The account request has an invalid size.";
                    return false;
                }
                payload = new byte[length];
                reader.ReadBytesSafe(ref payload, length);
                failure = string.Empty;
                return true;
            }
            catch (Exception)
            {
                failure = "The account request is malformed.";
                return false;
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            SavePlayerProfile(clientId, "Client disconnect");
            combatWorldAuthority?.UnregisterPlayerTarget(clientId);
            worldItemAuthority?.ForgetClient(clientId);
            pendingConnections.Remove(clientId);
            pendingConnectionAcceptedAt.Remove(clientId);
            failedAuthenticationCounts.Remove(clientId);
            nextAuthenticationAt.Remove(clientId);
            authenticationInFlight.Remove(clientId);
            authenticationStartedAt.Remove(clientId);
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
                    GetDefinitionRegistry(),
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
                    GetDefinitionRegistry(),
                    prototypePlayerMeleeCombat.UnarmedAttack,
                    prototypePlayerSpellLoadout.KnownSpells,
                    CombatStatUtility.GetAttackPower(prototypePlayerMeleeCombat.gameObject));

                ServerPlayerNarrativeAuthority narrativeAuthority = instance.AddComponent<ServerPlayerNarrativeAuthority>();
                narrativeAuthority.Configure(
                    actor,
                    replicatedNarrative,
                    prototypePersistence,
                    inventoryAuthority,
                    definitionRegistry: GetDefinitionRegistry());

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
            pendingConnectionAcceptedAt.Remove(clientId);
            failedAuthenticationCounts.Remove(clientId);
            nextAuthenticationAt.Remove(clientId);
            authenticationInFlight.Remove(clientId);
            authenticationStartedAt.Remove(clientId);
            Debug.LogError($"[Local Server] Disconnecting client {clientId}: {reason}", this);
            networkManager?.DisconnectClient(clientId, reason);
        }

        private void ExpireUnauthenticatedConnections()
        {
            if (pendingConnectionAcceptedAt.Count == 0) return;
            double now = Time.realtimeSinceStartupAsDouble;
            List<(ulong ClientId, string Reason)> expired = null;
            foreach (KeyValuePair<ulong, double> entry in pendingConnectionAcceptedAt)
            {
                if (connectedPlayerIds.ContainsKey(entry.Key))
                {
                    continue;
                }

                bool requestInFlight = authenticationInFlight.Contains(entry.Key);
                double timeoutStartedAt = requestInFlight
                    && authenticationStartedAt.TryGetValue(entry.Key, out double startedAt)
                        ? startedAt
                        : entry.Value;
                AccountAuthenticationExpiration expiration = AccountAuthenticationTimeoutPolicy.Evaluate(
                    entry.Value,
                    timeoutStartedAt,
                    requestInFlight,
                    now);
                if (expiration == AccountAuthenticationExpiration.None)
                {
                    continue;
                }

                expired ??= new List<(ulong ClientId, string Reason)>();
                expired.Add((
                    entry.Key,
                    expiration == AccountAuthenticationExpiration.AuthenticationRequest
                        ? "The account authentication request timed out. Please try again."
                        : "The login connection expired after being idle. Press Login to reconnect."));
            }

            if (expired == null) return;
            for (int i = 0; i < expired.Count; i++)
            {
                DisconnectClient(expired[i].ClientId, expired[i].Reason);
            }
        }

        private bool AdvanceIdleShutdown()
        {
            bool hasRemoteClients = HasRemoteClients();
            bool wasCountingDown = idleShutdownPolicy.IsCountingDown;
            double now = Time.realtimeSinceStartupAsDouble;
            bool shouldShutdown = idleShutdownPolicy.Observe(now, hasRemoteClients, idleShutdownSeconds);
            if (!wasCountingDown && idleShutdownPolicy.IsCountingDown)
            {
                Debug.Log(
                    $"[Local Server] No remote clients are connected. The server will stop after "
                    + $"{idleShutdownSeconds:0.#} seconds of inactivity.",
                    this);
            }

            if (!shouldShutdown)
            {
                return false;
            }

            Debug.LogWarning(
                $"[Local Server] No remote clients connected for {idleShutdownSeconds:0.#} seconds. "
                + "Saving authoritative state and shutting down the idle server.",
                this);
            StopServer();
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
            return true;
        }

        private bool HasRemoteClients()
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return false;
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    return true;
                }
            }

            return false;
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
            string loadMessage = playerProfileLoadMessages.TryGetValue(session.PlayerId, out string backgroundMessage)
                ? backgroundMessage
                : string.Empty;
            if (!completedPlayerProfileLoads.Contains(session.PlayerId)
                && playerProfileStore.TryLoad(session, out ServerPlayerProfileData loaded, out loadMessage))
            {
                playerProfiles[session.PlayerId] = loaded;
                lastQueuedPlayerProfiles[session.PlayerId] = loaded.Clone();
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

            PersistProfile(profile, reason);
        }

        private void PersistProfile(ServerPlayerProfileData profile, string reason)
        {
            if (profile == null) return;
            if (lastQueuedPlayerProfiles.TryGetValue(profile.playerId, out ServerPlayerProfileData previous)
                && profile.HasSamePersistentState(previous))
            {
                return;
            }

            profile.revision = checked(Math.Max(0L, profile.revision) + 1L);
            playerProfileStore ??= new ServerPlayerProfileStore();
            playerProfileWriteQueue ??= new ServerPlayerProfileWriteQueue(playerProfileStore);
            if (playerProfileWriteQueue.TryEnqueue(profile, out string message))
            {
                lastQueuedPlayerProfiles[profile.playerId] = profile.Clone();
                Debug.Log($"[Server Persistence] {message} Reason={reason}.", this);
            }
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
            PersistenceService service = prototypePersistence.WorldService;
            if (service == null) return;

            worldCheckpointWriteQueue ??= new ServerWorldCheckpointWriteQueue(service);
            if (worldCheckpointCapture != null) return;
            if (!service.TryBeginSaveCapture(
                    PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId,
                    $"World Checkpoint ({reason})",
                    out worldCheckpointCapture,
                    out PersistenceSaveResult failure))
            {
                if (failure?.Status != PersistenceSaveStatus.OperationAlreadyRunning)
                {
                    Debug.LogError($"[Server Persistence] World checkpoint snapshot failed: {failure?.Message ?? "Unknown failure."}", this);
                }
                return;
            }

            worldCheckpointCaptureReason = reason ?? string.Empty;
            worldCheckpointCaptureCpuMilliseconds = 0d;
            worldCheckpointMaximumCaptureTickMilliseconds = 0d;
            worldCheckpointMaximumCaptureParticipant = string.Empty;
            worldCheckpointCaptureFrames = 0;
            worldCheckpointCaptureSamples.Clear();
        }

        private void AdvanceWorldCheckpointCapture(int maximumParticipants)
        {
            if (worldCheckpointCapture == null || prototypePersistence?.WorldService == null) return;
            PersistenceService service = prototypePersistence.WorldService;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);
            var tickTimer = System.Diagnostics.Stopwatch.StartNew();
            bool continued = service.TryContinueSaveCapture(
                worldCheckpointCapture,
                maximumParticipants,
                out PreparedPersistenceSave prepared,
                out PersistenceSaveResult failure);
            tickTimer.Stop();
            double tickMilliseconds = tickTimer.Elapsed.TotalMilliseconds;
            long threadAllocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            long managedGrowthBytes = Math.Max(0L, GC.GetTotalMemory(forceFullCollection: false) - managedBefore);
            long allocatedBytes = threadAllocatedBytes > 0L ? threadAllocatedBytes : managedGrowthBytes;
            worldCheckpointCaptureCpuMilliseconds += tickMilliseconds;
            worldCheckpointCaptureSamples.Add(new ServerPersistenceCaptureSample(
                worldCheckpointCaptureFrames + 1,
                worldCheckpointCapture.LastCapturedParticipantKey,
                tickMilliseconds,
                allocatedBytes));
            if (tickMilliseconds > worldCheckpointMaximumCaptureTickMilliseconds)
            {
                worldCheckpointMaximumCaptureTickMilliseconds = tickMilliseconds;
                worldCheckpointMaximumCaptureParticipant = worldCheckpointCapture.LastCapturedParticipantKey;
            }
            worldCheckpointCaptureFrames++;

            if (!continued)
            {
                Debug.LogError($"[Server Persistence] World checkpoint snapshot failed: {failure?.Message ?? "Unknown failure."}", this);
                ClearWorldCheckpointCapture();
                return;
            }

            if (prepared == null) return;
            string reason = worldCheckpointCaptureReason;
            double captureCpuMilliseconds = worldCheckpointCaptureCpuMilliseconds;
            double maximumTickMilliseconds = worldCheckpointMaximumCaptureTickMilliseconds;
            string maximumCaptureParticipant = worldCheckpointMaximumCaptureParticipant;
            int captureFrames = worldCheckpointCaptureFrames;
            ServerPersistenceCaptureSample[] captureSamples = worldCheckpointCaptureSamples.ToArray();
            ClearWorldCheckpointCapture();

            if (!worldCheckpointWriteQueue.TryEnqueue(prepared, out string message))
            {
                PersistenceSaveResult enqueueFailure = PersistenceSaveResult.Failure(
                    PersistenceSaveStatus.OperationAlreadyRunning,
                    prepared.SlotId,
                    prepared.Path,
                    message,
                    transactionId: prepared.TransactionId);
                service.CompletePreparedSave(prepared, enqueueFailure);
                Debug.LogError($"[Server Persistence] World checkpoint queue failed: {message}", this);
                return;
            }

            ServerPersistenceCaptureSummary summary = ServerPersistenceCaptureMetrics.Summarize(captureSamples);
            Debug.Log(
                $"[Server Persistence] Utc={DateTime.UtcNow:o} {message} Reason={reason}. "
                + $"CaptureCpuMs={captureCpuMilliseconds:F1} MeanCaptureTickMs={summary.MeanMilliseconds:F3} "
                + $"MedianCaptureTickMs={summary.MedianMilliseconds:F3} ModeCaptureTickMs={summary.ModeBucketMilliseconds:F1} "
                + $"P95CaptureTickMs={summary.Percentile95Milliseconds:F3} P99CaptureTickMs={summary.Percentile99Milliseconds:F3} "
                + $"MaxCaptureTickMs={maximumTickMilliseconds:F3} MaxCaptureParticipant={maximumCaptureParticipant} "
                + $"CaptureFrames={captureFrames} CaptureAllocatedBytes={summary.TotalAllocatedBytes} "
                + $"MaxCaptureAllocatedBytes={summary.MaximumAllocatedBytes} MaxAllocationParticipant={summary.MaximumAllocationParticipantKey} "
                + $"ReusedParticipants={prepared.ReusedParticipantCount}.",
                this);
            Debug.Log(
                $"[Server Persistence Capture] Utc={DateTime.UtcNow:o} Transaction={prepared.TransactionId} "
                + "Format=frame|participant|elapsedMs|allocatedBytes "
                + $"Samples={ServerPersistenceCaptureMetrics.FormatSamples(captureSamples)}",
                this);
            if (summary.MaximumMilliseconds >= Math.Max(0.1f, captureCriticalMilliseconds))
            {
                Debug.LogWarning(
                    $"[Server Persistence Budget] CRITICAL capture frame {summary.MaximumMilliseconds:F3} ms "
                    + $"for '{summary.MaximumParticipantKey}' exceeded {captureCriticalMilliseconds:F3} ms.",
                    this);
            }
            else if (summary.Percentile95Milliseconds >= Math.Max(0.1f, captureWarningMilliseconds)
                || summary.MaximumMilliseconds >= Math.Max(0.1f, captureWarningMilliseconds))
            {
                Debug.LogWarning(
                    $"[Server Persistence Budget] Capture p95={summary.Percentile95Milliseconds:F3} ms, "
                    + $"max={summary.MaximumMilliseconds:F3} ms ('{summary.MaximumParticipantKey}'); "
                    + $"warning budget={captureWarningMilliseconds:F3} ms.",
                    this);
            }

            if (summary.MaximumAllocatedBytes >= Math.Max(1024L, captureAllocationWarningBytes))
            {
                Debug.LogWarning(
                    $"[Server Persistence Budget] Capture frame for '{summary.MaximumAllocationParticipantKey}' allocated "
                    + $"{summary.MaximumAllocatedBytes} bytes; warning budget={captureAllocationWarningBytes} bytes.",
                    this);
            }
        }

        private void CompleteWorldCheckpointCaptureImmediately()
        {
            while (worldCheckpointCapture != null)
            {
                AdvanceWorldCheckpointCapture(int.MaxValue);
            }
        }

        private void ClearWorldCheckpointCapture()
        {
            worldCheckpointCapture = null;
            worldCheckpointCaptureReason = string.Empty;
            worldCheckpointCaptureCpuMilliseconds = 0d;
            worldCheckpointMaximumCaptureTickMilliseconds = 0d;
            worldCheckpointMaximumCaptureParticipant = string.Empty;
            worldCheckpointCaptureFrames = 0;
            worldCheckpointCaptureSamples.Clear();
        }

        private void DrainWorldCheckpointWriteResults()
        {
            if (worldCheckpointWriteQueue == null || prototypePersistence?.WorldService == null) return;
            while (worldCheckpointWriteQueue.TryDequeueResult(out ServerWorldCheckpointWriteResult completed))
            {
                PersistenceSaveResult result = prototypePersistence.WorldService.CompletePreparedSave(completed.Prepared, completed.Result);
                if (result.Succeeded)
                {
                    Debug.Log(
                        $"[Server Persistence Write] Utc={DateTime.UtcNow:o} Transaction={completed.Prepared.TransactionId} "
                        + $"DeferredSerializationMs={completed.Prepared.DeferredSerializationMilliseconds:F3} "
                        + $"EnvelopeSerializationMs={completed.Prepared.EnvelopeSerializationMilliseconds:F3} "
                        + $"AtomicWriteMs={completed.Prepared.AtomicWriteMilliseconds:F3} "
                        + $"TotalWriteMs={completed.Prepared.TotalWriteMilliseconds:F3} "
                        + $"SerializedBytes={completed.Prepared.SerializedBytes} "
                        + $"ReusedParticipants={completed.Prepared.ReusedParticipantCount}. {result.Message}",
                        this);
                }
                else Debug.LogError($"[Server Persistence] World checkpoint save failed: {result.Message}", this);
            }
        }

        private void FlushWorldCheckpointWrites(string reason)
        {
            if (worldCheckpointWriteQueue == null) return;
            if (!worldCheckpointWriteQueue.Flush(TimeSpan.FromSeconds(30)))
            {
                Debug.LogError($"[Server Persistence] Timed out flushing world checkpoint writes during {reason}.", this);
            }

            DrainWorldCheckpointWriteResults();
        }

        private void DisposeWorldCheckpointWriteQueue()
        {
            if (worldCheckpointWriteQueue == null) return;
            worldCheckpointWriteQueue.Dispose();
            DrainWorldCheckpointWriteResults();
            worldCheckpointWriteQueue = null;
        }

        private DefinitionRegistry GetDefinitionRegistry()
        {
            if (cachedDefinitionRegistry == null)
            {
                if (definitionCatalog == null)
                {
                    throw new InvalidOperationException("The server definition catalog is not configured.");
                }

                cachedDefinitionRegistry = definitionCatalog.CreateRegistry();
            }

            return cachedDefinitionRegistry;
        }

        private void EnsureInventorySmokeSeed()
        {
            if (inventorySmokeSeeded
                || !HasCommandLineFlag(InventorySmokeSeedFlag))
            {
                return;
            }

            inventorySmokeSeeded = true;
            DefinitionRegistry definitions = GetDefinitionRegistry();
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
            networkManager.CustomMessagingManager?.UnregisterNamedMessageHandler(AccountAuthenticationProtocol.RequestMessageName);
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

        private readonly struct AccountAuthenticationWorkResult
        {
            public AccountAuthenticationWorkResult(int serverGeneration, ulong clientId, ServerAccountAuthenticationResult result)
            {
                ServerGeneration = serverGeneration;
                ClientId = clientId;
                Result = result;
            }

            public int ServerGeneration { get; }
            public ulong ClientId { get; }
            public ServerAccountAuthenticationResult Result { get; }
        }

        private readonly struct PlayerProfileLoadWorkResult
        {
            public PlayerProfileLoadWorkResult(
                int serverGeneration,
                ulong clientId,
                ServerAccountAuthenticationResult authentication,
                PlayerSessionSnapshot session,
                bool loaded,
                ServerPlayerProfileData profile,
                string loadMessage)
            {
                ServerGeneration = serverGeneration;
                ClientId = clientId;
                Authentication = authentication;
                Session = session;
                Loaded = loaded;
                Profile = profile;
                LoadMessage = loadMessage ?? string.Empty;
            }

            public int ServerGeneration { get; }
            public ulong ClientId { get; }
            public ServerAccountAuthenticationResult Authentication { get; }
            public PlayerSessionSnapshot Session { get; }
            public bool Loaded { get; }
            public ServerPlayerProfileData Profile { get; }
            public string LoadMessage { get; }
        }

    }
}
