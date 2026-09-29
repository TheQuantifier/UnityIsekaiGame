using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class LocalGameClient : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string serverAddress = LocalServerEndpoint.DefaultClientAddress;
        [SerializeField] private int serverPort = LocalServerEndpoint.DefaultPort;
        [SerializeField] private string localPlayerId = "local-player";
        [SerializeField] private bool connectAutomatically;

        private string clientInstanceId;
        private bool ownsClientSession;
        private LocalConnectionStatus status = new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client is offline.");

        public event Action<LocalConnectionStatus> StatusChanged;

        public LocalConnectionStatus Status => status;
        public bool IsConnected => ownsClientSession && networkManager != null && networkManager.IsConnectedClient;

        private void Awake()
        {
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
                Connect(options.ServerAddress, options.Port, options.PlayerId);
            }
            else if (connectAutomatically && options.Mode == LocalNetworkLaunchMode.None)
            {
                Connect();
            }
#endif
        }

        private void OnDisable()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
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
            return Connect(serverAddress, serverPort, localPlayerId);
        }

        public bool Connect(string address, int port, string playerId)
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
                Application.version);
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
            networkManager.NetworkConfig.ConnectionData = payload;
            transport.SetConnectionData(endpoint.Address, endpoint.Port);
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Connecting, $"Connecting to {endpoint}.", endpoint));

            ownsClientSession = networkManager.StartClient();
            if (!ownsClientSession)
            {
                return Fail($"Could not start a client connection to {endpoint}.", endpoint);
            }

            return true;
        }

        public void Disconnect()
        {
            if (!ownsClientSession || networkManager == null)
            {
                SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client is offline."));
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Disconnecting, "Disconnecting from the local server.", status.Endpoint));
            networkManager.Shutdown();
            ownsClientSession = false;
            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client disconnected.", status.Endpoint));
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!ownsClientSession || networkManager == null || clientId != networkManager.LocalClientId)
            {
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Connected, $"Connected to {status.Endpoint}.", status.Endpoint));
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!ownsClientSession || networkManager == null || clientId != networkManager.LocalClientId)
            {
                return;
            }

            string reason = networkManager.DisconnectReason;
            ownsClientSession = false;
            SetStatus(string.IsNullOrWhiteSpace(reason)
                ? new LocalConnectionStatus(LocalConnectionPhase.Offline, "Client disconnected.", status.Endpoint)
                : new LocalConnectionStatus(LocalConnectionPhase.Failed, reason, status.Endpoint));
        }

        private void ResolveReferences()
        {
            networkManager = networkManager == null ? GetComponent<NetworkManager>() : networkManager;
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
