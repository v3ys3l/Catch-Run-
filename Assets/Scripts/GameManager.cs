using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    [Header("Game Settings")]
    public float initialGameTime = 30f;
    public float timeToAddOnTag = 5f;

    [Header("UI References (Optional)")]
    public TextMeshProUGUI gameTimerDisplayText; 
    public GameObject gameOverPanel;        // Assign a UI Panel for game over
    public TMP_Text winnerDisplayText;
    public TMP_Text loserDisplayText;// Assign a TMP_Text on gameOverPanel to show winner(s)

    [Header("Animation Settings")]
    public bool animateTimerText = true;
    public float timerAnimationMinScale = 0.95f;
    public float timerAnimationMaxScale = 1.05f;
    public float timerAnimationSpeed = 2f;
    private Coroutine timerAnimationCoroutine;


    [SyncVar(hook = nameof(OnCurrentTimeChanged))]
    private float currentGameTime;

    [SyncVar(hook = nameof(OnCurrentItPlayerNetIdChanged))]
    public uint currentItPlayerNetId = 0; // 0 means no one is "it" or game not started

    private List<PlayerLobbyInfo> allPlayers = new List<PlayerLobbyInfo>();
    private bool gameInProgress = false;

    public AudioSource audioSource;

    void Awake()
    {
        Debug.Log("[GameManager Awake] GameManager instance is waking up.");
        if (instance == null)
        {
            instance = this;
            Debug.Log("[GameManager Awake] Singleton instance set.");
        }
        else
        {
            Debug.LogWarning("[GameManager Awake] Multiple GameManager instances found. Destroying this one.");
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[GameManager OnStartServer] OnStartServer called. isServer: " + isServer);
        
        // Delay starting the game slightly to allow players to register
        if(isServer)
        {
            StartCoroutine(DelayedStartGame());
        }
        
        if (gameTimerDisplayText != null) UpdateTimerUI(initialGameTime); 
    }

    private IEnumerator DelayedStartGame()
    {
        // Wait for a short moment to ensure players have a chance to be added by PlayerLobbyInfo.OnStartServer
        Debug.Log("[GameManager DelayedStartGame] Waiting for 0.5 seconds before starting game logic...");
        yield return new WaitForSeconds(0.5f); // Increased delay
        Debug.Log("[GameManager DelayedStartGame] Attempting to start game after 0.5s delay.");
        StartGame();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (gameTimerDisplayText != null)
        {
            UpdateTimerUI(currentGameTime);
            if (animateTimerText && timerAnimationCoroutine == null)
            {
                timerAnimationCoroutine = StartCoroutine(AnimateTimerTextCoroutine());
            }
        }
        Debug.Log("[GameManager OnStartClient] Client started, initial timer UI update.");
    }
    
    [Server]
    public void StartGame() // This should be called by the host (e.g., from MainMenuManager's StartGame button)
    {
        if (gameInProgress)
        {
            Debug.LogWarning("[GameManager StartGame] Attempted to start game, but game is already in progress.");
            return;
        }

        Debug.Log("[GameManager StartGame] StartGame called on server.");
        allPlayers = FindObjectsOfType<PlayerLobbyInfo>().ToList(); // Ensure PlayerLobbyInfo objects are active and findable
        Debug.Log($"[GameManager StartGame] Found {allPlayers.Count} PlayerLobbyInfo objects in scene.");

        if (allPlayers.Count > 0)
        {
            currentGameTime = initialGameTime;
            gameInProgress = true; // This is critical for Update loop
            Debug.Log($"[GameManager StartGame] gameInProgress set to TRUE. Initial time: {currentGameTime}");
            ChooseRandomItPlayer();
            Debug.Log($"[GameManager StartGame] Game actually started. Duration: {currentGameTime}s. Players: {allPlayers.Count}");
        }
        else
        {
            Debug.LogWarning("[GameManager StartGame] No players found to start the game. gameInProgress remains FALSE.");
            gameInProgress = false; // Explicitly set to false if no players
        }
    }

    [Server]
    void ChooseRandomItPlayer()
    {
        if (allPlayers.Count == 0) return;

        int randomIndex = Random.Range(0, allPlayers.Count);
        PlayerLobbyInfo newItPlayer = allPlayers[randomIndex];
        currentItPlayerNetId = newItPlayer.netId;
        Debug.Log($"[GameManager] Player {newItPlayer.playerNickname} (NetID: {currentItPlayerNetId}) is now IT.");
    }

    void Update()
    {
        if (!isServer || !gameInProgress) 
        {
            // Client side can update UI if not using a hook, or if GameManager is also on client
            // if (gameTimerDisplayText != null && gameInProgress) // Ensure game is running
            // {
            //    UpdateTimerUI(currentGameTime); // This would make client poll, hook is better
            // }
            return;
        }

        if (currentGameTime > 0)
        {
            currentGameTime -= Time.deltaTime;
            // SyncVar hook OnCurrentTimeChanged will update clients
            // Debug.Log($"[GameManager Update SERVER] Time: {currentGameTime}"); // Log can be spammy

            if (currentGameTime <= 0)
            {
                currentGameTime = 0; // Ensure it doesn't go negative before hook fires
                EndGame();
            }
        }
    }
    
    void OnCurrentTimeChanged(float oldTime, float newTime)
    {
        UpdateTimerUI(newTime);
    }

    void UpdateTimerUI(float timeToDisplay)
    {
        if (gameTimerDisplayText != null)
        {
            gameTimerDisplayText.text = Mathf.CeilToInt(timeToDisplay).ToString();
        }
    }

    private IEnumerator AnimateTimerTextCoroutine()
    {
        if (gameTimerDisplayText == null) yield break;

        Vector3 originalScale = gameTimerDisplayText.transform.localScale;
        while (animateTimerText && gameInProgress) // Animate only while game is in progress
        {
            float scaleValue = Mathf.Lerp(timerAnimationMinScale, timerAnimationMaxScale, 
                                        (Mathf.Sin(Time.time * timerAnimationSpeed) + 1f) / 2f); // Sin wave between 0 and 1
            gameTimerDisplayText.transform.localScale = originalScale * scaleValue;
            yield return null;
        }
        // Reset scale when animation stops
        if(gameTimerDisplayText != null) gameTimerDisplayText.transform.localScale = originalScale;
        timerAnimationCoroutine = null; // Clear coroutine reference
    }

    [Server]
    void EndGame()
    {
        gameInProgress = false;
        Debug.Log("[GameManager] Game Ended!");

        if (timerAnimationCoroutine != null)
        {
            StopCoroutine(timerAnimationCoroutine);
            timerAnimationCoroutine = null;
            if(gameTimerDisplayText != null && gameTimerDisplayText.transform.parent.gameObject.activeSelf) // Check if parent canvas is active
                 gameTimerDisplayText.transform.localScale = Vector3.one; 
        }

        string winnerNames = "";
        List<string> winners = new List<string>();
        foreach (PlayerLobbyInfo player in allPlayers)
        {
            if (player.netId != currentItPlayerNetId) // Everyone not IT is a winner
            {
                winners.Add(player.playerNickname);
            }
        }

        if (winners.Count > 0)
        {
            winnerNames += string.Join(", ", winners);
        }
        else
        {
            winnerNames = "No winners? Everyone was IT?"; // Should not happen if game ends with an IT player
        }
        
        PlayerLobbyInfo loser = allPlayers.FirstOrDefault(p => p.netId == currentItPlayerNetId);
        string loserName = loser != null ? loser.playerNickname : "N/A";
        Debug.Log($"[GameManager] Loser (IT): {loserName}. Winners: {string.Join(", ", winners)}");

        RpcShowGameOverPanel(winnerNames, loserName);
        StartCoroutine(ReturnToLobbyAfterDelay(4f));
    }

    [ClientRpc]
    void RpcShowGameOverPanel(string winners, string loser)
    {
        Debug.Log($"[Client] GAME OVER! Winners: {winners}, Loser (IT): {loser}");
        if (gameOverPanel != null && winnerDisplayText != null && loserDisplayText != null)
        {
            audioSource.Play();
            winnerDisplayText.text = $"{winners}";
            loserDisplayText.text = $"{loser}";
            gameOverPanel.SetActive(true);
        }
        // Stop timer animation on clients as well
        if (animateTimerText && gameTimerDisplayText != null) 
        {
             if (timerAnimationCoroutine != null) // Coroutine might be client-side if started in OnStartClient
            {
                StopCoroutine(timerAnimationCoroutine);
                timerAnimationCoroutine = null;
            }
             if(gameTimerDisplayText.transform.parent.gameObject.activeSelf)
                gameTimerDisplayText.transform.localScale = Vector3.one;
        }
    }

    [Server]
    private IEnumerator ReturnToLobbyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("[GameManager] Returning to lobby (MainMenu scene).");
        // This will disconnect all clients and load the offline scene for the host.
        // Clients will also be disconnected and return to their offline scene.
        NetworkManager.singleton.StopHost(); 
    }
    
    // Hook for currentItPlayerNetId
    void OnCurrentItPlayerNetIdChanged(uint oldNetId, uint newNetId)
    {
        // This hook runs on all clients when currentItPlayerNetId changes.
        // The actual update of PlayerLobbyInfo.isIt should happen via its own SyncVar mechanism.
        // Server changes PlayerLobbyInfo.isIt, which syncs to clients, triggering PlayerLobbyInfo.OnIsItChanged.
        Debug.Log($"[GameManager Hook CLIENT] IT player SyncVar (currentItPlayerNetId) changed from {oldNetId} to {newNetId}. PlayerLobbyInfo instances should update their visuals via their own 'isIt' SyncVar hook.");
        
        // We need to ensure that when currentItPlayerNetId changes on the SERVER,
        // the corresponding PlayerLobbyInfo objects on the SERVER have their 'isIt' SyncVar updated.
        if (isServer)
        {
            PlayerLobbyInfo[] allServerPlayers = FindObjectsOfType<PlayerLobbyInfo>();
            foreach (PlayerLobbyInfo pInfo in allServerPlayers)
            {
                // This will set the 'isIt' SyncVar on the server instance of PlayerLobbyInfo,
                // which will then sync to clients and trigger their OnIsItChanged hooks.
                pInfo.ServerUpdateItStatus(newNetId); 
            }
        }
    }

    [Server]
    public void PlayerTagged(PlayerLobbyInfo tagger, PlayerLobbyInfo taggedPlayer)
    {
        if (!gameInProgress) return;
        if (tagger.netId != currentItPlayerNetId) return;
        if (tagger.netId == taggedPlayer.netId) return; 

        Debug.Log($"[GameManager] {tagger.playerNickname} tagged {taggedPlayer.playerNickname}!");
        currentItPlayerNetId = taggedPlayer.netId; 
        currentGameTime += timeToAddOnTag; // Add time to the timer
        Debug.Log($"[GameManager] Added {timeToAddOnTag}s to timer. New time: {currentGameTime}");
    }


    // Called by PlayerLobbyInfo to register themselves
    // This is a more robust way than FindObjectsOfType if players can join mid-game (though our current setup doesn't fully support that easily)
    [Server]
    public void AddPlayerToList(PlayerLobbyInfo player)
    {
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);
            Debug.Log($"[GameManager] Player {player.playerNickname} added to list. Total players: {allPlayers.Count}");
        }
    }

    [Server]
    public void RemovePlayerFromList(PlayerLobbyInfo player)
    {
        if (allPlayers.Contains(player))
        {
            allPlayers.Remove(player);
            Debug.Log($"[GameManager] Player {player.playerNickname} removed from list. Total players: {allPlayers.Count}");
            if (player.netId == currentItPlayerNetId && gameInProgress && allPlayers.Count > 0)
            {
                // If the player who was IT disconnects, choose a new IT player
                ChooseRandomItPlayer();
            }
            else if (allPlayers.Count == 0 && gameInProgress)
            {
                Debug.Log("[GameManager] Last player left. Ending game.");
                EndGame(); // Or handle differently
            }
        }
    }

    // UI Methods (Example - you'd call these from a UI script)
    public float GetCurrentGameTime()
    {
        return currentGameTime;
    }

    public bool IsPlayerIt(uint netId)
    {
        return netId == currentItPlayerNetId && gameInProgress;
    }
}
