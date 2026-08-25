using Mirror;
using UnityEngine;
using TMPro; 
using System.Linq;
using System.Collections; 

public class PlayerLobbyInfo : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNicknameChanged))]
    public string playerNickname = "Player";

    [SyncVar(hook = nameof(OnIsItChanged))]
    public bool isIt = false;

    [SyncVar(hook = nameof(OnStunStatusChanged))]
    public bool isStunned = false;

    [Header("UI Elements on Prefab")]
    public TMP_Text nicknameDisplayText; 
    public SpriteRenderer playerSpriteRenderer; // Used to change the sprite
    public Sprite itSprite; // Sprite to use when player is IT
    public Sprite notItSprite; // Sprite to use when player is NOT IT

    [Header("Tagging Settings")]
    public float tagDistance = 1.5f; 
    public float tagRadius = 0.5f; 
    public LayerMask playerLayerMask; 

    [Header("Stun Settings")]
    public float stunDuration = 3f;

    public AudioSource AudioSource;

    void OnIsItChanged(bool oldIsIt, bool newIsIt)
    {
        Debug.Log($"[PlayerLobbyInfo OnIsItChanged HOOK] Player {playerNickname} (NetID: {netId}, isLocalPlayer: {isLocalPlayer}) - Old isIt: {oldIsIt}, New isIt: {newIsIt}. Calling UpdatePlayerVisuals.");
        UpdatePlayerVisuals();
    }
    
    void OnStunStatusChanged(bool oldStun, bool newStun)
    {
        Debug.Log($"[PlayerLobbyInfo OnStunStatusChanged HOOK] Player {playerNickname} (NetID: {netId}, isLocalPlayer: {isLocalPlayer}) Stun status changed from {oldStun} to {newStun}.");
    }

    [Server] 
    public void ServerUpdateItStatus(uint currentItNetId)
    {
        bool newIsItState = (this.netId == currentItNetId);
        if (isIt != newIsItState) 
        {
            isIt = newIsItState;
            Debug.Log($"[PlayerLobbyInfo ServerUpdateItStatus SERVER] Player {playerNickname} (NetID: {netId}) 'isIt' SyncVar set to {isIt}.");
        }
    }

    void UpdatePlayerVisuals()
    {
        AudioSource.Play();
        if (playerSpriteRenderer != null)
        {
            if (isIt && itSprite != null)
            {
                playerSpriteRenderer.sprite = itSprite;
                Debug.Log($"[PlayerLobbyInfo UpdatePlayerVisuals] Player {playerNickname} (NetID: {netId}) sprite set to IT sprite.");
            }
            else if (!isIt && notItSprite != null)
            {
                playerSpriteRenderer.sprite = notItSprite;
                Debug.Log($"[PlayerLobbyInfo UpdatePlayerVisuals] Player {playerNickname} (NetID: {netId}) sprite set to NOT IT sprite.");
            }
            else
            {
                // Fallback or warning if sprites are not assigned
                if (isIt) Debug.LogWarning($"[PlayerLobbyInfo UpdatePlayerVisuals] itSprite not assigned for {playerNickname}.");
                else Debug.LogWarning($"[PlayerLobbyInfo UpdatePlayerVisuals] notItSprite not assigned for {playerNickname}.");
            }
        }
        else
        {
            if(isLocalPlayer || isServer) Debug.LogWarning($"[PlayerLobbyInfo UpdatePlayerVisuals] PlayerSpriteRenderer not assigned on {gameObject.name} (NetID: {netId}). Cannot change sprite.");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (GameManager.instance != null)
        {
            GameManager.instance.AddPlayerToList(this);
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        string name = PlayerPrefs.GetString("PlayerNickname", $"Player_{netId}");
        CmdSetNickname(name);
        if (playerLayerMask == 0) playerLayerMask = ~LayerMask.GetMask("Ignore Raycast");
    }

    [Command]
    void CmdSetNickname(string newName)
    {
        playerNickname = newName;
    }

    void OnNicknameChanged(string oldName, string newName)
    {
        gameObject.name = $"Player [{newName}]"; 
        if (nicknameDisplayText != null) nicknameDisplayText.text = newName;
        MainMenuManager mainMenuUiManager = FindObjectOfType<MainMenuManager>();
        if (mainMenuUiManager != null) mainMenuUiManager.UpdateLobbyPlayerNames(); 
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        gameObject.name = $"Player [{playerNickname}]"; 
        if (nicknameDisplayText != null)
        {
            nicknameDisplayText.text = playerNickname; 
            nicknameDisplayText.gameObject.SetActive(!isLocalPlayer); 
        }
        UpdatePlayerVisuals(); 
        MainMenuManager mainMenuUiManager = FindObjectOfType<MainMenuManager>();
        if (mainMenuUiManager != null) mainMenuUiManager.UpdateLobbyPlayerNames();
    }
    
    void OnDestroy()
    {
        // Log when OnDestroy is called, and on which instance (client/server, local/remote)
        string context = $"isLocalPlayer: {isLocalPlayer}, isClient: {isClient}, isServer: {isServer}, hasAuthority: {authority}";
        Debug.Log($"[PlayerLobbyInfo OnDestroy] Player {playerNickname} (NetID: {netId}) - Context: {context} - OnDestroy CALLED.");

        if ((NetworkServer.active || NetworkClient.active) && gameObject.scene.isLoaded)
        {
            if (isServer && GameManager.instance != null) 
            {
                GameManager.instance.RemovePlayerFromList(this);
            }
            
            // Attempt to find MainMenuManager and update UI
            // This will run on all clients where this PlayerLobbyInfo object is destroyed
            MainMenuManager mainMenuUiManager = FindObjectOfType<MainMenuManager>();
            if (mainMenuUiManager != null)
            {
                if (mainMenuUiManager.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[PlayerLobbyInfo OnDestroy] Player {playerNickname} (NetID: {netId}) - Found active MainMenuManager. Starting DelayedLobbyUpdate.");
                    mainMenuUiManager.StartCoroutine(DelayedLobbyUpdate(mainMenuUiManager));
                }
                else
                {
                    Debug.LogWarning($"[PlayerLobbyInfo OnDestroy] Player {playerNickname} (NetID: {netId}) - Found MainMenuManager but it's not active in hierarchy. Cannot update lobby list.");
                }
            }
            else
            {
                 Debug.LogWarning($"[PlayerLobbyInfo OnDestroy] Player {playerNickname} (NetID: {netId}) - MainMenuManager NOT FOUND. Cannot update lobby list.");
            }
        }
        else
        {
            Debug.Log($"[PlayerLobbyInfo OnDestroy] Player {playerNickname} (NetID: {netId}) - Not updating lobby: Network not active or scene not loaded.");
        }
    }

    System.Collections.IEnumerator DelayedLobbyUpdate(MainMenuManager manager)
    {
        yield return null; 
        if(manager != null && manager.gameObject.activeInHierarchy) manager.UpdateLobbyPlayerNames();
        yield break; 
    }

    void Update()
    {
        if (!isLocalPlayer) return; 
        
        // Reverted to KeyCode.Space for tagging
        if (isIt && !isStunned && Input.GetKeyDown(KeyCode.Space)) 
        {
            AttemptTagWithRaycast();
        }

        // TEST: Press T to stun self (for testing stun logic)
        if (Input.GetKeyDown(KeyCode.T))
        {
            CmdRequestStunSelf();
        }
    }

    void AttemptTagWithRaycast()
    {
        Vector2 castDirection = transform.up; 
        RaycastHit2D hit = Physics2D.CircleCast(transform.position + (Vector3)castDirection * 0.1f, tagRadius, castDirection, tagDistance, playerLayerMask);
        if (hit.collider != null)
        {
            PlayerLobbyInfo targetToTag = hit.collider.GetComponent<PlayerLobbyInfo>();
            if (targetToTag != null && !targetToTag.isIt && targetToTag.netId != this.netId && !targetToTag.isStunned) 
            {
                CmdAttemptTagPlayer(targetToTag.netId);
            }
        }
    }

    [Command]
    void CmdAttemptTagPlayer(uint targetNetId)
    {
        if (GameManager.instance == null || !isIt || isStunned) return;
        if (NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            PlayerLobbyInfo taggedPlayerInfo = targetIdentity.GetComponent<PlayerLobbyInfo>();
            if (taggedPlayerInfo != null && !taggedPlayerInfo.isIt && !taggedPlayerInfo.isStunned) 
            {
                GameManager.instance.PlayerTagged(this, taggedPlayerInfo);
            }
        }
    }
    
    [Command]
    void CmdRequestStunSelf()
    {
        Debug.Log($"[PlayerLobbyInfo CmdRequestStunSelf] Server received request from NetID: {netId} to stun self.");
        ApplyStun(); 
    }

    [Server] 
    public void ApplyStun()
    {
        if (isStunned) return; 
        StartCoroutine(StunCoroutine());
    }

    [Server]
    private IEnumerator StunCoroutine()
    {
        isStunned = true; 
        Debug.Log($"[PlayerLobbyInfo STUN_COROUTINE SERVER] {playerNickname} (NetID: {netId}) is STUNNED for {stunDuration}s.");
        yield return new WaitForSeconds(stunDuration);
        isStunned = false; 
        Debug.Log($"[PlayerLobbyInfo STUN_COROUTINE SERVER] {playerNickname} (NetID: {netId}) is NO LONGER STUNNED.");
    }
}
