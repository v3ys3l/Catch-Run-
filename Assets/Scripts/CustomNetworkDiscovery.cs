using Mirror;
using Mirror.Discovery; // Ensure this is present
using System.Net;
using UnityEngine;
using System; // Added for UriBuilder

// Define the request and response messages
// These must implement NetworkMessage
public struct MyDiscoveryRequest : NetworkMessage 
{
    // Add any data you want to send from client to server in the request
}

public struct MyDiscoveryResponse : NetworkMessage
{
    // The server that sent this.
    // this is a property so that it is not serialized, but the
    // client fills this up after we receive it
    public IPEndPoint EndPoint { get; set; }

    public System.Uri uri;
    public long serverId;
    public string hostNickname;
    public int maxPlayers;
    public int currentPlayerCount;
}

// Attempting to use NetworkDiscoveryBase again.
// If CS0246 (type/namespace not found) occurs for NetworkDiscoveryBase<,>,
// then this base class might not exist or is named differently in this Mirror version.
public class CustomNetworkDiscovery : NetworkDiscoveryBase<MyDiscoveryRequest, MyDiscoveryResponse>
{
    [Tooltip("Nickname to broadcast for this server. If empty, PlayerPrefs 'PlayerNickname' will be used.")]
    public string serverNicknameToAdvertise;

    // serverId is inherited from NetworkDiscoveryBase if it exists there and is accessible.
    // If not, we might need to generate it.
    // Let's assume serverId is accessible from the base or we generate it.
    // For now, we will rely on the inherited serverId from NetworkDiscoveryBase.
    // The error CS0117 suggests it's not directly accessible as 'base.serverId' in the way I thought,
    // or it's not in NetworkDiscoveryBase itself.
    // NetworkDiscovery (the non-generic one) has 'public static long serverId { get; private set; }'
    // which is NOT what we want for an instance.
    // NetworkDiscoveryBase has 'public long serverId { get; protected set; }'
    // This should be accessible as 'this.serverId' or just 'serverId'.

    // Removed Awake override as it might not be virtual in the base class,
    // or NetworkDiscoveryBase itself is not being correctly identified.
    // We will assume serverId is initialized by the base class's own Awake/Start.

    #region Server
    protected override MyDiscoveryResponse ProcessRequest(MyDiscoveryRequest request, IPEndPoint clientEndPoint)
    {
        // Process the request from a client and return a response.
        string nickname = string.IsNullOrEmpty(serverNicknameToAdvertise) 
                          ? PlayerPrefs.GetString("PlayerNickname", "Unknown Host") 
                          : serverNicknameToAdvertise;

        return new MyDiscoveryResponse
        {
            // serverId should be available from NetworkDiscoveryBase after its Awake/Start
            serverId = this.ServerId,
            uri = transport.ServerUri(),
            hostNickname = nickname,
            maxPlayers = NetworkManager.singleton != null ? NetworkManager.singleton.maxConnections : 0,
            currentPlayerCount = NetworkManager.singleton != null ? NetworkManager.singleton.numPlayers : 0
            // EndPoint is set by the client upon receiving the response
        };
    }
    #endregion

    #region Client
    protected override MyDiscoveryRequest GetRequest()
    {
        // Create a request message to broadcast.
        return new MyDiscoveryRequest();
    }

    protected override void ProcessResponse(MyDiscoveryResponse response, IPEndPoint serverEndPoint)
    {
        // We received a response from a server.
        response.EndPoint = serverEndPoint; // The endpoint of the server that sent the response.

        // Ensure the URI is valid and uses the server's actual IP address.
        // (This logic is similar to what's in Mirror's NetworkDiscovery)
        UriBuilder realUri = new UriBuilder(response.uri)
        {
            Host = response.EndPoint.Address.ToString()
        };
        response.uri = realUri.Uri;

        // Invoke the event with the custom response.
        // The base class NetworkDiscoveryBase<TRequest, TResponse> has:
        // public readonly UnityEvent<TResponse> OnServerFound = new UnityEvent<TResponse>();
        // So, this should work directly.
        OnServerFound.Invoke(response);
    }
    #endregion

    // Call this method in MainMenuManager when starting the host,
    // typically after setting PlayerPrefs for "PlayerNickname".
    public void PrepareAdvertisingNickname()
    {
        // This method can be used to explicitly set serverNicknameToAdvertise if needed,
        // but ProcessRequest already tries to get it from PlayerPrefs if this field is empty.
        // For clarity, MainMenuManager can set this field directly on the component before calling AdvertiseServer.
        // Example: networkDiscovery.serverNicknameToAdvertise = PlayerPrefs.GetString("PlayerNickname");
    }
}
