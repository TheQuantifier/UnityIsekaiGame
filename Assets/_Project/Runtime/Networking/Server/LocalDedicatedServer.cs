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

        private readonly Dictionary<ulong, string> connectedPlayerIds = new Dictionary<ulong, string>();
        private bool ownsServerSession;
        private LocalConnectionStatus status = new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server is offline.");

        public event Action<LocalConnectionStatus> StatusChanged;

        public LocalConnectionStatus Status => status;
        public IReadOnlyDictionary<ulong, string> ConnectedPlayerIds => connectedPlayerIds;
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

            listenAddress = endpoint.Address;
            serverPort = endpoint.Port;
            connectedPlayerIds.Clear();
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
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
                SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Offline, "Server is offline."));
                return;
            }

            SetStatus(new LocalConnectionStatus(LocalConnectionPhase.Disconnecting, "Stopping the local server.", status.Endpoint));
            Unsubscribe();
            networkManager.Shutdown();
            ownsServerSession = false;
            connectedPlayerIds.Clear();
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
                Debug.Log($"[Local Server] Approved client {request.ClientNetworkId} as player '{admission.Request.PlayerId}'.", this);
            }
            else
            {
                Debug.LogWarning($"[Local Server] Rejected client {request.ClientNetworkId}: {admission.Reason}", this);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (connectedPlayerIds.Remove(clientId))
            {
                Debug.Log($"[Local Server] Client {clientId} disconnected.", this);
            }
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
