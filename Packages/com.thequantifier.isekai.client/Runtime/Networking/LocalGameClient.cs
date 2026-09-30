using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class LocalGameClient : MonoBehaviour
    {
        private const int LocalClientTargetFrameRate = 60;

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string serverAddress = LocalServerEndpoint.DefaultClientAddress;
        [SerializeField] private int serverPort = LocalServerEndpoint.DefaultPort;
        [SerializeField] private string localPlayerId = "local-player";
        [SerializeField] private string authenticationToken = string.Empty;
        [SerializeField] private bool connectAutomatically;
        [SerializeField] private PrototypePersistenceServiceBehaviour simulation;

        private string clientInstanceId;
        private bool ownsClientSession;
        private Coroutine playerActorResolution;
        private NetworkPlayerActor localPlayerActor;
        private LocalConnectionStatus status = new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client is offline.");

        public event Action<LocalConnectionStatus> StatusChanged;
        public event Action<NetworkPlayerActor> LocalPlayerActorChanged;

        public LocalConnectionStatus Status => status;
        public bool IsConnected => ownsClientSession && networkManager != null && networkManager.IsConnectedClient;
        public NetworkPlayerActor LocalPlayerActor => localPlayerActor;

        private void Awake()
        {
#if !UNITY_SERVER && !UNITY_EDITOR
            // Local multiplayer frequently runs several graphical clients on one machine. An
            // uncapped background window can otherwise monopolize a CPU core/GPU queue long
            // enough for its transport heartbeat to time out. Keep networking alive in the
            // background and apply a predictable render budget to every standalone client.
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = LocalClientTargetFrameRate;
#endif
            ResolveReferences();
            clientInstanceId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void Start()
        {
#if !UNITY_SERVER
            if (!LocalNetworkCommandLine.TryParse(
                    Environment.GetCommandLineArgs(),
                    false,
                    out LocalNetworkLaunchOptions options,
                    out string failure))
            {
                Fail($"Invalid local-network command line: {failure}");
                return;
            }

            if (options.Mode == LocalNetworkLaunchMode.Client)
            {
                Connect(options.ServerAddress, options.Port, options.PlayerId, options.AuthenticationToken);
            }
            else if (connectAutomatically && options.Mode == LocalNetworkLaunchMode.None)
            {
                Connect();
            }
#endif
        }

        private void OnDisable()
        {
            StopPlayerActorResolution();
            SetLocalPlayerActor(null);
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnApplicationQuit()
        {
            if (ownsClientSession)
            {
                Disconnect();
            }
        }

        public void Configure(NetworkManager manager, string address = LocalServerEndpoint.DefaultClientAddress, int port = LocalServerEndpoint.DefaultPort, bool autoConnect = false)
        {
            networkManager = manager;
            serverAddress = address;
            serverPort = port;
            connectAutomatically = autoConnect;
        }

        public bool Connect()
        {
            return Connect(serverAddress, serverPort, localPlayerId, authenticationToken);
        }

        public bool Connect(string address, int port, string playerId, string token = "")
        {
            ResolveReferences();
            if (networkManager == null)
            {
                return Fail("A NetworkManager is required before connecting.");
            }

            if (networkManager.IsListening)
            {
                return Fail("The NetworkManager is already running.");
            }

            if (!LocalServerEndpoint.TryCreate(address, port, out LocalServerEndpoint endpoint, out string endpointFailure))
            {
                return Fail(endpointFailure);
            }

            ConnectionRequestPayload request = new ConnectionRequestPayload(
                string.IsNullOrWhiteSpace(clientInstanceId) ? Guid.NewGuid().ToString("N") : clientInstanceId,
                playerId,
                Application.version,
                token);
            if (!LocalConnectionProtocol.TryEncode(request, out byte[] payload, out string payloadFailure))
            {
                return Fail(payloadFailure);
            }

            if (!(networkManager.NetworkConfig.NetworkTransport is UnityTransport transport))
            {
                return Fail("The NetworkManager must use Unity Transport.");
            }

            serverAddress = endpoint.Address;
            serverPort = endpoint.Port;
            localPlayerId = playerId;
            authenticationToken = token ?? string.Empty;
            networkManager.NetworkConfig.TickRate = LocalServerEndpoint.DefaultTickRate;
            networkManager.NetworkConfig.ConnectionData = payload;
            transport.SetConnectionData(endpoint.Address, endpoint.Port);
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Connecting, $"Connecting to {endpoint}.", endpoint));
            SetSimulationRole(SimulationRuntimeRole.NetworkClientReplica);

            ownsClientSession = networkManager.StartClient();
            if (!ownsClientSession)
            {
                SetSimulationRole(SimulationRuntimeRole.StandaloneAuthoritative);
                return Fail($"Could not start a client connection to {endpoint}.", endpoint);
            }

            return true;
        }

        public void Disconnect()
        {
            if (!ownsClientSession || networkManager == null)
            {
                SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client is offline."));
                SetSimulationRole(SimulationRuntimeRole.StandaloneAuthoritative);
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Disconnecting, "Disconnecting from the local server.", status.Endpoint));
            StopPlayerActorResolution();
            SetLocalPlayerActor(null);
            networkManager.Shutdown();
            ownsClientSession = false;
            SetSimulationRole(SimulationRuntimeRole.StandaloneAuthoritative);
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client disconnected.", status.Endpoint));
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!ownsClientSession || networkManager == null || clientId != networkManager.LocalClientId)
            {
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Connected, $"Connected to {status.Endpoint}.", status.Endpoint));
            StopPlayerActorResolution();
            playerActorResolution = StartCoroutine(ResolveLocalPlayerActor());
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!ownsClientSession || networkManager == null || clientId != networkManager.LocalClientId)
            {
                return;
            }

            string reason = networkManager.DisconnectReason;
            ownsClientSession = false;
            SetSimulationRole(SimulationRuntimeRole.StandaloneAuthoritative);
            StopPlayerActorResolution();
            SetLocalPlayerActor(null);
            SetStatus(string.IsNullOrWhiteSpace(reason)
                ? new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client disconnected.", status.Endpoint)
                : new LocalConnectionStatus(LocalConnectionPhase.Failed, reason, status.Endpoint));
        }

        private IEnumerator ResolveLocalPlayerActor()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (ownsClientSession && networkManager != null && Time.realtimeSinceStartup < deadline)
            {
                NetworkObject playerObject = networkManager.LocalClient?.PlayerObject;
                NetworkPlayerActor actor = playerObject == null ? null : playerObject.GetComponent<NetworkPlayerActor>();
                if (actor != null && actor.IsSpawned && actor.HasIdentity)
                {
                    SetLocalPlayerActor(actor);
                    Debug.Log($"[Local Client] Local player actor '{actor.ActorId}' is ready for player '{actor.PlayerId}'.", this);
                    playerActorResolution = null;
                    yield break;
                }

                yield return null;
            }

            playerActorResolution = null;
            if (ownsClientSession)
            {
                Debug.LogError("[Local Client] Connected, but the server did not provide a local player actor within 10 seconds.", this);
            }
        }

        private void StopPlayerActorResolution()
        {
            if (playerActorResolution == null)
            {
                return;
            }

            StopCoroutine(playerActorResolution);
            playerActorResolution = null;
        }

        private void SetLocalPlayerActor(NetworkPlayerActor actor)
        {
            if (ReferenceEquals(localPlayerActor, actor))
            {
                return;
            }

            localPlayerActor = actor;
            LocalPlayerActorChanged?.Invoke(localPlayerActor);
        }

        private void ResolveReferences()
        {
            networkManager = networkManager == null ? GetComponent<NetworkManager>() : networkManager;
            simulation = simulation == null
                ? FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include)
                : simulation;
        }

        private void SetSimulationRole(SimulationRuntimeRole role)
        {
            if (simulation == null) ResolveReferences();
            simulation?.SetRuntimeRole(role);
        }

        private bool Fail(string message, LocalServerEndpoint endpoint = default)
        {
            ownsClientSession = false;
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
                Debug.LogError($"[Local Client] {next.Message}", this);
            }
            else
            {
                Debug.Log($"[Local Client] {next.Message}", this);
            }

            StatusChanged?.Invoke(status);
        }
    }
}
