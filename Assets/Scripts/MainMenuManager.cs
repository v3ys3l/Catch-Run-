using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using Mirror.Discovery; 
using System.Collections;
using System.Collections.Generic;
using System.Linq; 

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel; 
    public GameObject hostLobbyPanel;
    public GameObject tutorialPanel;
    public GameObject settingsPanel; // Added Settings Panel

    [Header("Main Menu UI Elements")]
    public TMP_InputField nicknameInputField;
    public Button createRoomButton; 
    public Button findRoomButton;
    public Button tutorialButton;
    public Button settingsButton; // Added Settings Button
    public Button quitButton;
    public GameObject lobbyListPanel;
    public TMP_Text statusText;
    public TMP_Text titleText; 

    [Header("Title Animation Settings (Optional)")]
    public bool animateTitleText = true;
    public float titleAnimationMinScale = 0.95f;
    public float titleAnimationMaxScale = 1.05f;
    public float titleAnimationSpeed = 1.5f;
    private Coroutine titleAnimationCoroutine;
    private Vector3 initialTitleScale; 

    [Header("Tutorial Panel UI Elements (Optional)")] 
    public Button closeTutorialButton; 
    public Image tutorialDisplayImage;    
    public Button nextTutorialImageButton; 
    public Button prevTutorialImageButton;
    public Sprite[] tutorialSprites;
    private int currentTutorialImageIndex = 0;

    [Header("Settings Panel UI Elements")]
    public Button closeSettingsButton;
    public Slider volumeSlider;
    public Slider redSlider;
    public Slider greenSlider;
    public Slider blueSlider;
    // public Image mainMenuBackground; // Removed: We will use the Image component on mainMenuPanel directly

    [Header("Host Lobby UI Elements")]
    public Button leaveLobbyButton; 
    public Button startGameButton;
    public Transform playerNamesListParent;
    public GameObject playerNameTextPrefab;

    [Header("Lobby List UI")]
    public GameObject serverEntryPrefab;
    public Transform serverListContent;

    [Header("Settings")]
    public float statusMessageDuration = 3f;
    public string gameSceneName = "GameScene";
    public string volumePrefKey = "MasterVolume"; // PlayerPrefs key for volume
    public string colorRPrefKey = "MenuBgColorR"; // PlayerPrefs key for background red
    public string colorGPrefKey = "MenuBgColorG"; // PlayerPrefs key for background green
    public string colorBPrefKey = "MenuBgColorB"; // PlayerPrefs key for background blue

    private NetworkManager networkManager;
    private NetworkDiscovery networkDiscovery; 
    private Coroutine statusCoroutine;
    private readonly Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>(); 

    void Awake()
    {
        networkManager = FindObjectOfType<NetworkManager>(); 
        if (networkManager == null) { Debug.LogError("[MainMenuManager Awake] NetworkManager NOT FOUND!"); enabled = false; return; }
        networkDiscovery = networkManager.GetComponent<NetworkDiscovery>();
        if (networkDiscovery == null) Debug.LogError($"[MainMenuManager Awake] NetworkDiscovery component NOT FOUND on {networkManager.gameObject.name}!");
        else networkDiscovery.OnServerFound.AddListener(HandleServerFound);
        if (titleText != null) initialTitleScale = titleText.transform.localScale;
    }

    void Start()
    {
        if (networkManager == null) return; 
        if (networkDiscovery == null && findRoomButton != null) findRoomButton.interactable = false;
        
        if (createRoomButton) createRoomButton.onClick.AddListener(CreateRoom);
        if (findRoomButton) findRoomButton.onClick.AddListener(ToggleLobbyListPanel);
        if (tutorialButton) tutorialButton.onClick.AddListener(ShowTutorialPanel);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
        if (leaveLobbyButton) leaveLobbyButton.onClick.AddListener(LeaveLobby);
        if (startGameButton) startGameButton.onClick.AddListener(StartGame);
        if (closeTutorialButton) closeTutorialButton.onClick.AddListener(HideTutorialPanel);
        if (nextTutorialImageButton) nextTutorialImageButton.onClick.AddListener(NextTutorialImage);
        if (prevTutorialImageButton) prevTutorialImageButton.onClick.AddListener(PreviousTutorialImage);
        if (settingsButton) settingsButton.onClick.AddListener(ShowSettingsPanel); // Added listener for settings button
        if (closeSettingsButton) closeSettingsButton.onClick.AddListener(HideSettingsPanel); // Added listener for close settings button
        if (volumeSlider) volumeSlider.onValueChanged.AddListener(OnVolumeChanged); // Added listener for volume slider
        if (redSlider) redSlider.onValueChanged.AddListener((_) => OnColorChanged()); // Added listener for red slider
        if (greenSlider) greenSlider.onValueChanged.AddListener((_) => OnColorChanged()); // Added listener for green slider
        if (blueSlider) blueSlider.onValueChanged.AddListener((_) => OnColorChanged()); // Added listener for blue slider

        LoadAndApplySettings(); // Load settings on start

        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        OnMainMenuBecameActive();

        if (hostLobbyPanel) hostLobbyPanel.SetActive(false);
        if (lobbyListPanel) lobbyListPanel.SetActive(false);
        if (tutorialPanel) tutorialPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false); // Hide settings panel initially
        if (statusText) statusText.gameObject.SetActive(false);
        if (startGameButton) startGameButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (networkDiscovery != null) networkDiscovery.OnServerFound.RemoveListener(HandleServerFound);
    }

    private void SetCoreMainMenuInteractable(bool interactable)
    {
        if (nicknameInputField) nicknameInputField.interactable = interactable;
        if (createRoomButton) createRoomButton.interactable = interactable;
        if (quitButton) quitButton.interactable = interactable;
        if (settingsButton) settingsButton.interactable = interactable; // Added settingsButton here
    }

    public void HandleHostStarted() { OnMainMenuBecameInactive(); if (lobbyListPanel && lobbyListPanel.activeSelf) lobbyListPanel.SetActive(false); if (hostLobbyPanel) hostLobbyPanel.SetActive(true); if (startGameButton) startGameButton.gameObject.SetActive(true); UpdateLobbyPlayerNames(); if (networkDiscovery != null) networkDiscovery.AdvertiseServer(); }
    public void HandleHostStopped() { if (hostLobbyPanel) hostLobbyPanel.SetActive(false); OnMainMenuBecameActive(); SetCoreMainMenuInteractable(true); if (tutorialButton) tutorialButton.interactable = true; if (findRoomButton && networkDiscovery != null) findRoomButton.interactable = true; else if (findRoomButton) findRoomButton.interactable = false; UpdateLobbyPlayerNames(); if (networkDiscovery != null) networkDiscovery.StopDiscovery(); }
    public void HandleClientConnected() { OnMainMenuBecameInactive(); if (lobbyListPanel && lobbyListPanel.activeSelf) lobbyListPanel.SetActive(false); if (hostLobbyPanel) hostLobbyPanel.SetActive(true); if (startGameButton) startGameButton.gameObject.SetActive(NetworkServer.active); UpdateLobbyPlayerNames(); }
    public void HandleClientDisconnected() { ShowStatusMessage("Disconnected from server.", statusMessageDuration * 1.5f); if (hostLobbyPanel) hostLobbyPanel.SetActive(false); OnMainMenuBecameActive(); SetCoreMainMenuInteractable(true); if (tutorialButton) tutorialButton.interactable = true; if (findRoomButton && networkDiscovery != null) findRoomButton.interactable = true; else if (findRoomButton) findRoomButton.interactable = false; UpdateLobbyPlayerNames(); }
    public void CreateRoom() { if (!IsNicknameEmpty()) { PlayerPrefs.SetString("PlayerNickname", nicknameInputField.text); if (networkManager) networkManager.StartHost(); }}
    public void LeaveLobby() { if (NetworkServer.active && NetworkClient.isConnected) networkManager.StopHost(); else if (NetworkClient.isConnected) networkManager.StopClient(); }
    public void StartGame() { if (NetworkServer.active) networkManager.ServerChangeScene(gameSceneName); }

    public void ToggleLobbyListPanel()
    {
        if (lobbyListPanel == null || networkDiscovery == null) return;
        bool isActive = !lobbyListPanel.activeSelf;
        lobbyListPanel.SetActive(isActive);
        SetCoreMainMenuInteractable(!isActive); // This will now also handle settingsButton
        if (tutorialButton) tutorialButton.interactable = !isActive;
        // findRoomButton should always be interactable if networkDiscovery is available
        if (findRoomButton) findRoomButton.interactable = (networkDiscovery != null);

        if (isActive)
        {
            OnMainMenuBecameInactive();
            discoveredServers.Clear();
            UpdateServerListUI();
            networkDiscovery.StartDiscovery();
        }
        else
        {
            OnMainMenuBecameActive();
            networkDiscovery.StopDiscovery();
        }
    }
    public void JoinRoom(ServerResponse serverResponse) { if (!IsNicknameEmpty()) { PlayerPrefs.SetString("PlayerNickname", nicknameInputField.text); if (networkDiscovery != null) networkDiscovery.StopDiscovery(); if (networkManager) { networkManager.networkAddress = serverResponse.uri.Host; networkManager.StartClient(serverResponse.uri); } } }
    private bool IsNicknameEmpty() { if (nicknameInputField == null || string.IsNullOrWhiteSpace(nicknameInputField.text)) { ShowStatusMessage("Nickname is empty.", statusMessageDuration); return true; } return false; }
    private void HandleServerFound(ServerResponse r) { if (r.uri == null || discoveredServers.ContainsKey(r.serverId)) return; discoveredServers[r.serverId] = r; UpdateServerListUI(); }
    private void UpdateServerListUI() { if (serverListContent==null || serverEntryPrefab==null) return; foreach (Transform c in serverListContent) Destroy(c.gameObject); foreach (var s in discoveredServers.Values) { var go=Instantiate(serverEntryPrefab,serverListContent); go.GetComponentInChildren<Button>()?.onClick.AddListener(()=>JoinRoom(s)); go.GetComponentInChildren<TMP_Text>()?.SetText($"Server @ {s.EndPoint.Address}"); } }
    
    public void UpdateLobbyPlayerNames() 
    {
        Debug.Log($"[MainMenuManager UpdateLobbyPlayerNames] Method CALLED. playerNamesListParent: {(playerNamesListParent == null ? "NULL" : "Assigned")}, playerNameTextPrefab: {(playerNameTextPrefab == null ? "NULL" : "Assigned")}");
        if (playerNamesListParent == null || playerNameTextPrefab == null) 
        {
            Debug.LogWarning("[MainMenuManager UpdateLobbyPlayerNames] Parent or Prefab is null, cannot update list.");
            return;
        }

        Debug.Log($"[MainMenuManager UpdateLobbyPlayerNames] Clearing {playerNamesListParent.childCount} existing player name entries.");
        foreach (Transform child in playerNamesListParent) 
        {
            Destroy(child.gameObject);
        }

        PlayerLobbyInfo[] players = FindObjectsOfType<PlayerLobbyInfo>();
        Debug.Log($"[MainMenuManager UpdateLobbyPlayerNames] Found {players.Length} PlayerLobbyInfo objects in scene to list.");
        
        foreach (PlayerLobbyInfo player in players.OrderBy(p => p.netId)) 
        {
            Debug.Log($"[MainMenuManager UpdateLobbyPlayerNames] Creating UI entry for player: {player.playerNickname} (NetID: {player.netId})");
            GameObject entryGo = Instantiate(playerNameTextPrefab, playerNamesListParent);
            TMP_Text nameText = entryGo.GetComponentInChildren<TMP_Text>();
            if (nameText != null) 
            {
                nameText.text = player.playerNickname;
            }
            else
            {
                Debug.LogWarning($"[MainMenuManager UpdateLobbyPlayerNames] PlayerNameTextPrefab for {player.playerNickname} is missing a TMP_Text component in its children.");
            }
        }
    }

    private void ShowStatusMessage(string m, float d) { if(statusText==null) return; if(statusCoroutine!=null) StopCoroutine(statusCoroutine); statusCoroutine=StartCoroutine(ShowStatusCoroutine(m,d)); }
    private IEnumerator ShowStatusCoroutine(string m,float d) { if(statusText){statusText.text=m;statusText.gameObject.SetActive(true);yield return new WaitForSeconds(d);statusText.gameObject.SetActive(false);} statusCoroutine=null; }

    public void ShowTutorialPanel() { if(tutorialPanel){ OnMainMenuBecameInactive(); SetCoreMainMenuInteractable(false); if(tutorialButton)tutorialButton.interactable=false; if(findRoomButton) findRoomButton.interactable = false; /*settingsButton is handled by SetCoreMainMenuInteractable*/ tutorialPanel.SetActive(true); currentTutorialImageIndex=0; UpdateTutorialImageDisplay(); }}
    public void HideTutorialPanel() { if(tutorialPanel){ tutorialPanel.SetActive(false); OnMainMenuBecameActive(); SetCoreMainMenuInteractable(true); if(tutorialButton)tutorialButton.interactable=true; if(findRoomButton && networkDiscovery != null) findRoomButton.interactable = true; else if (findRoomButton) findRoomButton.interactable = false; /*settingsButton is handled by SetCoreMainMenuInteractable*/ }}
    public void NextTutorialImage() { if(tutorialSprites==null||tutorialSprites.Length==0)return; currentTutorialImageIndex=(currentTutorialImageIndex+1)%tutorialSprites.Length; UpdateTutorialImageDisplay(); }
    public void PreviousTutorialImage() { if(tutorialSprites==null||tutorialSprites.Length==0)return; currentTutorialImageIndex--; if(currentTutorialImageIndex<0)currentTutorialImageIndex=tutorialSprites.Length-1; UpdateTutorialImageDisplay(); }
    private void UpdateTutorialImageDisplay() { if(tutorialDisplayImage==null||tutorialSprites==null||tutorialSprites.Length==0){if(tutorialDisplayImage)tutorialDisplayImage.gameObject.SetActive(false);if(prevTutorialImageButton)prevTutorialImageButton.interactable=false;if(nextTutorialImageButton)nextTutorialImageButton.interactable=false;return;} tutorialDisplayImage.gameObject.SetActive(true);tutorialDisplayImage.sprite=tutorialSprites[currentTutorialImageIndex];bool multi=tutorialSprites.Length>1;if(prevTutorialImageButton)prevTutorialImageButton.interactable=multi;if(nextTutorialImageButton)nextTutorialImageButton.interactable=multi;}
    
    public void QuitGame() 
    { 
        Debug.Log("[MainMenuManager] QuitGame called.");
        Application.Quit(); 
        #if UNITY_EDITOR 
        UnityEditor.EditorApplication.isPlaying = false; 
        #endif 
    }

    private IEnumerator AnimateTitleTextCoroutine() { if(titleText==null)yield break; while(animateTitleText && mainMenuPanel != null && mainMenuPanel.activeSelf){titleText.transform.localScale=initialTitleScale*Mathf.Lerp(titleAnimationMinScale,titleAnimationMaxScale,(Mathf.Sin(Time.time*titleAnimationSpeed)+1f)/2f);yield return null;}if(titleText)titleText.transform.localScale=initialTitleScale;titleAnimationCoroutine=null;}
    public void OnMainMenuBecameActive() 
    { 
        // mainMenuPanel.SetActive(true); // MainMenuPanel should always be active, only its buttons' interactability changes
        if(animateTitleText && titleText != null && titleAnimationCoroutine == null)
        {
            if (mainMenuPanel != null && mainMenuPanel.activeSelf) 
            {
                 titleAnimationCoroutine = StartCoroutine(AnimateTitleTextCoroutine());
            }
        }
    }
    public void OnMainMenuBecameInactive()
    {
        // mainMenuPanel.SetActive(false); // MainMenuPanel should always be active
        if(titleAnimationCoroutine != null)
        {
            StopCoroutine(titleAnimationCoroutine);
            if(titleText != null) titleText.transform.localScale = initialTitleScale;
            titleAnimationCoroutine = null;
        }
    }

    // --- Settings Panel Logic ---

    private void LoadAndApplySettings()
    {
        // Load Volume
        float volume = PlayerPrefs.GetFloat(volumePrefKey, 1f); // Default to 1 (max volume)
        if (volumeSlider) volumeSlider.value = volume;
        ApplyVolume(volume);

        // Load Background Color
        float r = PlayerPrefs.GetFloat(colorRPrefKey, 1f); // Default to white
        float g = PlayerPrefs.GetFloat(colorGPrefKey, 1f);
        float b = PlayerPrefs.GetFloat(colorBPrefKey, 1f);
        if (redSlider) redSlider.value = r;
        if (greenSlider) greenSlider.value = g;
        if (blueSlider) blueSlider.value = b;
        ApplyColor(new Color(r, g, b));
    }

    public void ShowSettingsPanel()
    {
        if (settingsPanel)
        {
            OnMainMenuBecameInactive(); 
            SetCoreMainMenuInteractable(false); // This will disable settingsButton itself
            if (tutorialButton) tutorialButton.interactable = false;
            if (findRoomButton) findRoomButton.interactable = false;
            settingsPanel.SetActive(true);
        }
    }

    public void HideSettingsPanel()
    {
        if (settingsPanel)
        {
            settingsPanel.SetActive(false);
            OnMainMenuBecameActive(); 
            SetCoreMainMenuInteractable(true); // This will re-enable settingsButton
            if (tutorialButton) tutorialButton.interactable = true;
            if (findRoomButton && networkDiscovery != null) findRoomButton.interactable = true;
            else if (findRoomButton) findRoomButton.interactable = false;
        }
    }

    private void OnVolumeChanged(float value)
    {
        ApplyVolume(value);
        PlayerPrefs.SetFloat(volumePrefKey, value);
        PlayerPrefs.Save(); // Save immediately
    }

    private void ApplyVolume(float value)
    {
        AudioListener.volume = value; // Apply volume globally
    }

    private void OnColorChanged()
    {
        if (redSlider == null || greenSlider == null || blueSlider == null) return;

        Color newColor = new Color(redSlider.value, greenSlider.value, blueSlider.value);
        ApplyColor(newColor);

        // Save color components
        PlayerPrefs.SetFloat(colorRPrefKey, newColor.r);
        PlayerPrefs.SetFloat(colorGPrefKey, newColor.g);
        PlayerPrefs.SetFloat(colorBPrefKey, newColor.b);
        PlayerPrefs.Save(); // Save immediately
    }

    private void ApplyColor(Color color)
    {
        if (mainMenuPanel != null)
        {
            Image panelImage = mainMenuPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = color;
            }
            else
            {
                Debug.LogWarning("[MainMenuManager] mainMenuPanel does not have an Image component to change its color.");
            }
        }
    }
}
