using UnityEngine;
using Mirror;
using Mirror.Discovery;

public class SimpleDiscoveryTest : MonoBehaviour
{
    public NetworkManager networkManager;
    public NetworkDiscovery networkDiscovery;

    void Start()
    {
        if (networkManager == null)
        {
            Debug.LogError("[SimpleDiscoveryTest] NetworkManager not assigned in the Inspector!");
            enabled = false; // Disable this script if not set up
            return;
        }
        if (networkDiscovery == null)
        {
            Debug.LogError("[SimpleDiscoveryTest] NetworkDiscovery not assigned in the Inspector!");
            enabled = false; // Disable this script if not set up
            return;
        }
        networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
        Debug.Log("[SimpleDiscoveryTest] Script started and subscribed to OnServerFound.");
    }

    void OnDestroy()
    {
        if (networkDiscovery != null)
        {
            networkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
            if (NetworkServer.active || NetworkClient.active) // Stop discovery if we were doing something
            {
                networkDiscovery.StopDiscovery();
            }
        }
    }

    public void StartHostAndAdvertise()
    {
        if (networkManager.isNetworkActive)
        {
            Debug.LogWarning("[SimpleDiscoveryTest] Network is already active. Cannot start host.");
            return;
        }
        Debug.Log("[SimpleDiscoveryTest] Starting Host...");
        networkManager.StartHost();
        Debug.Log("[SimpleDiscoveryTest] Advertising Server...");
        networkDiscovery.AdvertiseServer();
    }

    public void DiscoverServers()
    {
        if (networkManager.isNetworkActive)
        {
            Debug.LogWarning("[SimpleDiscoveryTest] Network is already active. Cannot start discovery as client if already host/client.");
            // Or, if it's a host, it shouldn't discover itself this way.
            // If it's a client, it should stop being a client first.
            return;
        }
        Debug.Log("[SimpleDiscoveryTest] Clearing previously discovered servers and starting Discovery...");
        // You might want a dictionary to store discovered servers if you plan to list them
        // For this simple test, we just log.
        networkDiscovery.StartDiscovery();
    }

    void OnDiscoveredServer(ServerResponse info)
    {
        Debug.Log($"[SimpleDiscoveryTest] Discovered server! Address: {info.EndPoint.Address}, Port: {info.EndPoint.Port}, URI: {info.uri}, ServerID: {info.serverId}");
        // Optional: try to connect automatically for testing
        // Debug.Log("[SimpleDiscoveryTest] Stopping discovery and attempting to connect to first found server.");
        // networkDiscovery.StopDiscovery();
        // networkManager.StartClient(info.uri);
    }

    // Simple OnGUI for testing without complex UI setup
    void OnGUI()
    {
        if (networkManager == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 200));
        GUILayout.Label("Simple Discovery Test");

        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            if (GUILayout.Button("Start Host & Advertise"))
            {
                StartHostAndAdvertise();
            }
            if (GUILayout.Button("Discover Servers"))
            {
                DiscoverServers();
            }
        }
        else
        {
            if (NetworkServer.active)
            {
                // Assuming TelepathyTransport is the active one and has a 'port' property
                // Transport.active should give the currently active transport instance.
                Transport currentTransport = Transport.active;
                string portString = "N/A";
                if (currentTransport is TelepathyTransport telepathy)
                {
                    portString = telepathy.port.ToString();
                }
                else if (currentTransport != null) // Fallback for other transports if they have a way to get port
                {
                    // Try to get port via a common method or property if one exists,
                    // otherwise, this part might need adjustment for different transports.
                    // For now, we'll just indicate the transport type.
                    portString = $"({currentTransport.GetType().Name})";
                }
                GUILayout.Label($"Host Running (Port: {portString})");
                if (GUILayout.Button("Stop Host"))
                {
                    networkManager.StopHost();
                    // AdvertiseServer stops automatically when host stops, but explicit StopDiscovery is good practice
                    if(networkDiscovery != null) networkDiscovery.StopDiscovery(); 
                }
            }
            else if (NetworkClient.isConnected)
            {
                GUILayout.Label($"Client Connected to {networkManager.networkAddress}");
                if (GUILayout.Button("Stop Client"))
                {
                    networkManager.StopClient();
                     if(networkDiscovery != null) networkDiscovery.StopDiscovery(); // Stop discovery if it was running
                }
            }
        }
        GUILayout.EndArea();
    }
}
