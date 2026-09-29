using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class LocalDedicatedServer : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string listenAddress = LocalServerEndpoint.DefaultListenAddress;
        [SerializeField] private int serverPort = LocalServerEndpoint.DefaultPort;
        [SerializeField, Min(1)] private int maximumPlayers = 8;
        [SerializeField] private bool startAutomaticallyInServerBuild = true;
        [SerializeField] private GameObject playerActorPrefab;

        private readonly Dictionary<ulong, string> connectedPlayerIds = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, ConnectionRequestPayload> pendingConnections = new Dictionary<ulong, ConnectionRequestPayload>();
        private readonly Dictionary<ulong, NetworkPlayerActor> playerActors = new Dictionary<ulong, NetworkPlayerActor>();
        private readonly PlayerSessionRegistry playerSessions = new PlayerSessionRegistry();
        private bool ownsServerSession;
        private LocalConnectionStatus status = new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server is offline.");

        public event Action<LocalConnectionStatus> StatusChanged;
        public event Action<PlayerSessionSnapshot, NetworkPlayerActor> PlayerSessionStarted;
        public event Action<PlayerSessionSnapshot> PlayerSessionEnded;

        public LocalConnectionStatus Status => status;
        public IReadOnlyDictionary<ulong, string> ConnectedPlayerIds => connectedPlayerIds;
        public IReadOnlyList<PlayerSessionSnapshot> PlayerSessions => playerSessions.ActiveSessions;
        public GameObject PlayerActorPrefab => playerActorPrefab;
        public bool StartAutomaticallyInServerBuild => startAutomaticallyInServerBuild;

        private void Awake()
        {
            ResolveReferences();
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

        public void Configure(NetworkManager manager, string address = LocalServerEndpoint.DefaultListenAddress, int port = LocalServerEndpoint.DefaultPort, int maxPlayers = 8)
        {
            networkManager = manager;
            listenAddress = address;
            serverPort = port;
            maximumPlayers = Math.Max(1, maxPlayers);
        }

        public void ConfigurePlayerActorPrefab(GameObject prefab)
        {
            playerActorPrefab = prefab;
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
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            transport.SetConnectionData(LocalServerEndpoint.DefaultClientAddress, endpoint.Port, endpoint.Address);
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.StartingServer, $"Starting local server on {endpoint}.", endpoint));

            ownsServerSession = networkManager.StartServer();
            if (!ownsServerSession)
            {
                Unsubscribe();
                return Fail($"Could not start the local server on {endpoint}.", endpoint);
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Listening, $"Local server is listening on {endpoint}.", endpoint));
            return true;
        }

        public void StopServer()
        {
            if (!ownsServerSession || networkManager == null)
            {
                connectedPlayerIds.Clear();
                pendingConnections.Clear();
                playerActors.Clear();
                playerSessions.Clear();
                SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server is offline."));
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Disconnecting, "Stopping the local server.", status.Endpoint));
            Unsubscribe();
            networkManager.Shutdown();
            ownsServerSession = false;
            connectedPlayerIds.Clear();
            pendingConnections.Clear();
            playerActors.Clear();
            playerSessions.Clear();
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server stopped.", status.Endpoint));
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            ConnectionAdmissionResult admission = LocalConnectionAdmission.Evaluate(
                request.Payload,
                connectedPlayerIds.Count,
                maximumPlayers,
                connectedPlayerIds.Values);

            response.Approved = admission.Approved;
            response.CreatePlayerObject = false;
            response.PlayerPrefabHash = null;
            response.Pending = false;
            response.Reason = admission.Reason;
            if (admission.Approved)
            {
                connectedPlayerIds[request.ClientNetworkId] = admission.Request.PlayerId;
                pendingConnections[request.ClientNetworkId] = admission.Request;
                Debug.Log($"[Local Server] Approved client {request.ClientNetworkId} as player '{admission.Request.PlayerId}'.", this);
            }
            else
            {
                Debug.LogWarning($"[Local Server] Rejected client {request.ClientNetworkId}: {admission.Reason}", this);
            }
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
            if (!playerSessions.TryOpen(clientId, request, out PlayerSessionSnapshot session, out string failure))
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
            if (prefabNetworkObject == null || prefabActor == null)
            {
                failure = "The server player actor prefab must contain NetworkObject and NetworkPlayerActor components.";
                return false;
            }

            GameObject instance = Instantiate(playerActorPrefab);
            instance.name = $"Network Player Actor ({session.PlayerId})";
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            actor = instance.GetComponent<NetworkPlayerActor>();
            try
            {
                actor.ConfigureServer(session);
                networkObject.SpawnAsPlayerObject(session.ClientId, true);
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

        private bool ValidatePlayerActorPrefab(out string failure)
        {
            if (playerActorPrefab == null)
            {
                failure = "The server player actor prefab is not configured.";
                return false;
            }

            if (playerActorPrefab.GetComponent<NetworkObject>() == null || playerActorPrefab.GetComponent<NetworkPlayerActor>() == null)
            {
                failure = "The server player actor prefab must contain NetworkObject and NetworkPlayerActor components.";
                return false;
            }

            foreach (NetworkPrefabsList list in networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (list != null && list.Contains(playerActorPrefab))
                {
                    failure = string.Empty;
                    return true;
                }
            }

            failure = "The server player actor prefab is not registered in the NetworkManager prefab lists.";
            return false;
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

            Configure(networkManager, options.ListenAddress, options.Port, options.MaximumPlayers);
            StartServer();
        }
    }
}
