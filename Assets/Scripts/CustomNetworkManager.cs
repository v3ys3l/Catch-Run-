using Mirror;
using UnityEngine;

// Custom NetworkManager that inherits from Mirror.NetworkManager
// This allows us to override specific event methods.
public class CustomNetworkManager : NetworkManager
{
    private MainMenuManager mainMenuManager;

    public override void OnStartHost()
    {
        base.OnStartHost(); // Call the base method
        Debug.Log("[CustomNetworkManager] OnStartHost called.");
        if (mainMenuManager == null)
            mainMenuManager = FindObjectOfType<MainMenuManager>();
        
        if (mainMenuManager != null)
            mainMenuManager.HandleHostStarted();
        else
            Debug.LogError("[CustomNetworkManager] MainMenuManager not found in scene!");
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        Debug.Log("[CustomNetworkManager] OnStopHost called.");
        if (mainMenuManager == null)
            mainMenuManager = FindObjectOfType<MainMenuManager>();

        if (mainMenuManager != null)
            mainMenuManager.HandleHostStopped();
        else
            Debug.LogError("[CustomNetworkManager] MainMenuManager not found in scene on StopHost!");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[CustomNetworkManager] OnClientConnect called.");
        if (mainMenuManager == null)
            mainMenuManager = FindObjectOfType<MainMenuManager>();

        if (mainMenuManager != null)
            mainMenuManager.HandleClientConnected();
        else
            Debug.LogError("[CustomNetworkManager] MainMenuManager not found in scene on ClientConnect!");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[CustomNetworkManager] OnClientDisconnect called.");
        if (mainMenuManager == null)
            mainMenuManager = FindObjectOfType<MainMenuManager>();
        
        if (mainMenuManager != null)
            mainMenuManager.HandleClientDisconnected();
        else
            Debug.LogError("[CustomNetworkManager] MainMenuManager not found in scene on ClientDisconnect!");
    }

    // Optional: If you need to handle server-side client connection/disconnection
    // public override void OnServerConnect(NetworkConnectionToClient conn)
    // {
    //     base.OnServerConnect(conn);
    //     Debug.Log($"[CustomNetworkManager] Client connected to server: {conn.connectionId}");
    //     // You might want to update player lists or other server-side logic here
    // }

    // public override void OnServerDisconnect(NetworkConnectionToClient conn)
    // {
    //     base.OnServerDisconnect(conn);
    //     Debug.Log($"[CustomNetworkManager] Client disconnected from server: {conn.connectionId}");
    //     // Update player lists, handle player leaving, etc.
    // }
}
