using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class MultiplayerUIManager : MonoBehaviour
{
    public static MultiplayerUIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;

    /// <summary>True khi đang hiển thị Game Panel (dùng bởi SettingsManager).</summary>
    public bool IsInGame => gamePanel != null && gamePanel.activeSelf;

    [Header("Intro UI")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text introTitleText;
    [SerializeField] private Button settingsButtonInIntro; // Nút ⚙️ cài đặt ở màn hình chính

    [Header("Menu UI")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button backToIntroButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button settingsButtonInMenu; // Nút ⚙️ cài đặt ở menu

    [Header("Game UI")]
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text sceneInfoText;
    [SerializeField] private Button openSettingsButtonInGame; // Nút ⚙️ mở Settings trong game
    // ✔ disconnectButton đã chuyển vào SettingsPanel (SettingsManager.cs)
    
    [Header("Scene Objects to Hide/Show")]
    [SerializeField] private GameObject island;           // Island object
    [SerializeField] private GameObject finalIsland;      // FinalIsland object
    
    private AudioSource audioIntro;      // AudioIntro (tự động tìm từ VideoPlayer)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Kiểm tra các panels
        if (introPanel == null)
            Debug.LogError("❌ IntroPanel chưa được gán trong Inspector!");
        if (menuPanel == null)
            Debug.LogError("❌ MenuPanel chưa được gán trong Inspector!");
        if (gamePanel == null)
            Debug.LogError("❌ GamePanel chưa được gán trong Inspector!");
            
        SetupUI();
        SetupButtonListeners();
    }

    private void SetupUI()
    {
        // ✅ FIX: Chỉ tìm VideoPlayer đang ACTIVE — FindObjectsInactive.Include
        // khiến tìm thấy VideoPlayer bị tắt trong scene, ẩn hết panel và không ai
        // gọi ShowIntroAfterVideo() → màn hình đen khi build EXE
        VideoPlayer videoPlayer = FindFirstObjectByType<VideoPlayer>();

        bool hasActiveVideo = videoPlayer != null
                              && videoPlayer.isActiveAndEnabled
                              && videoPlayer.clip != null;

        if (hasActiveVideo)
        {
            // Tự động tìm AudioSource từ cùng GameObject với VideoPlayer
            audioIntro = videoPlayer.GetComponent<AudioSource>();
            if (audioIntro != null)
                Debug.Log("✅ Đã tìm thấy AudioIntro từ VideoPlayer GameObject");

            if (introPanel != null) introPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);
            if (gamePanel != null) gamePanel.SetActive(false);
            Debug.Log("🎬 Có video intro - Đợi video xong");
        }
        else
        {
            // Không có video active, hiển thị intro panel ngay
            ShowIntroPanel();
            Debug.Log("✅ Không có video active - Hiển thị Intro Panel ngay");
        }
    }


    private void SetupButtonListeners()
    {
        // Intro buttons
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);
        else
            Debug.LogWarning("⚠️ StartButton chưa được gán!");
        
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);
        else
            Debug.LogWarning("⚠️ ExitButton chưa được gán!");

        // Menu buttons
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);
        else
            Debug.LogWarning("⚠️ CreateRoomButton chưa được gán!");
        
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnJoinRoomButtonClicked);
        else
            Debug.LogWarning("⚠️ JoinRoomButton chưa được gán!");
        
        if (backToIntroButton != null)
            backToIntroButton.onClick.AddListener(OnBackToIntroClicked);
        else
            Debug.LogWarning("⚠️ BackToIntroButton chưa được gán!");
        
        // Settings buttons (mở được từ mọi menu)
        void OpenSettings() { if (SettingsManager.Instance != null) SettingsManager.Instance.OpenSettings(); }
        if (settingsButtonInIntro  != null) settingsButtonInIntro.onClick.AddListener(OpenSettings);
        if (settingsButtonInMenu   != null) settingsButtonInMenu.onClick.AddListener(OpenSettings);
        if (openSettingsButtonInGame != null) openSettingsButtonInGame.onClick.AddListener(OpenSettings);
    }

    // ========== INTRO PANEL ==========
    private void ShowIntroPanel()
    {
        if (introPanel != null) introPanel.SetActive(true);
        if (menuPanel != null) menuPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        
        // Hiện lại các scene objects
        ShowSceneObjects();
        
        Debug.Log("📺 Hiển thị Intro Panel");
    }
    
    /// <summary>
    /// Được gọi từ PlayVideoIntro sau khi video kết thúc
    /// </summary>
    public void ShowIntroAfterVideo()
    {
        Debug.Log("🎬 Video đã xong - Hiển thị Intro Panel");
        ShowIntroPanel();
    }

    private void OnStartButtonClicked()
    {
        Debug.Log("▶️ Nhấn nút Bắt đầu");
        ShowMenuPanel();
    }

    private void OnExitButtonClicked()
    {
        Debug.Log("👋 Thoát game");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // ========== MENU PANEL ==========
    private void ShowMenuPanel()
    {
        if (introPanel != null) introPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);
        UpdateStatus("Chọn tạo phòng hoặc vào phòng");
        Debug.Log("🎮 Hiển thị Menu Panel");
    }

    private void OnBackToIntroClicked()
    {
        Debug.Log("⬅️ Quay lại Intro");
        ShowIntroPanel();
    }

    private async void OnCreateRoomButtonClicked()
    {
        UpdateStatus("Đang tạo phòng...");
        
        if (createRoomButton != null) createRoomButton.interactable = false;
        if (joinRoomButton != null) joinRoomButton.interactable = false;

        string joinCode = await NetworkGameManager.Instance.StartHostWithRelay();
        
        if (!string.IsNullOrEmpty(joinCode))
        {
            UpdateStatus($"✅ Đã tạo phòng! Join Code: {joinCode}");
            ShowLobbyCanvasForCharacterSelection();
        }
        else
        {
            UpdateStatus("❌ Không thể tạo phòng!");
            if (createRoomButton != null) createRoomButton.interactable = true;
            if (joinRoomButton != null) joinRoomButton.interactable = true;
        }
    }

    private async void OnJoinRoomButtonClicked()
    {
        if (joinCodeInput == null || string.IsNullOrEmpty(joinCodeInput.text))
        {
            UpdateStatus("❌ Vui lòng nhập mã phòng!");
            return;
        }

        UpdateStatus("Đang vào phòng...");
        
        if (createRoomButton != null) createRoomButton.interactable = false;
        if (joinRoomButton != null) joinRoomButton.interactable = false;

        string code = joinCodeInput.text.Trim().ToUpper();
        bool success = await NetworkGameManager.Instance.JoinGameWithRelay(code);

        if (success)
        {
            UpdateStatus($"✅ Đã vào phòng: {code}");
            ShowLobbyCanvasForCharacterSelection();
        }
        else
        {
            UpdateStatus("❌ Không thể vào phòng! Kiểm tra lại mã.");
            if (createRoomButton != null) createRoomButton.interactable = true;
            if (joinRoomButton != null) joinRoomButton.interactable = true;
        }
    }

    private void ShowLobbyCanvasForCharacterSelection()
    {
        // Ẩn Menu Panel
        if (menuPanel != null) menuPanel.SetActive(false);
        
        // Hiện LobbyCanvas cho character selection
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.ShowLobbyUI();
            Debug.Log("🎭 Chuyển sang Character Selection (LobbyCanvas)");
        }
        else
        {
            // Thử tìm LobbyUIManager trong scene nếu Instance chưa có
            LobbyUIManager lobbyManager = FindFirstObjectByType<LobbyUIManager>(FindObjectsInactive.Include);
            if (lobbyManager != null)
            {
                Debug.Log("✅ Đã tìm thấy LobbyUIManager - Kích hoạt...");
                lobbyManager.ShowLobbyUI();
            }
            else
            {
                Debug.LogWarning("⚠️ Không tìm thấy LobbyUIManager trong scene!");
                // Fallback: Hiện GamePanel nếu không có Lobby
                ShowGamePanel();
            }
        }
    }

    // ========== GAME PANEL ==========
    private void ShowGamePanel()
    {   
        if (introPanel != null) introPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
        
        // Ẩn các scene objects khi vào game
        HideSceneObjects();
        
        Debug.Log("🎯 Hiển thị Game Panel");
    }

    /// <summary>
    /// Gọi bởi SettingsManager khi người dùng nhấn Disconnect trong Settings Panel.
    /// </summary>
    public void OnDisconnectedFromSettings()
    {
        UpdateStatus("Đã ngắt kết nối.");
        ShowSceneObjects();
        ShowMenuPanel();
        if (createRoomButton != null) createRoomButton.interactable = true;
        if (joinRoomButton   != null) joinRoomButton.interactable   = true;
        if (joinCodeInput    != null) joinCodeInput.text = "";
    }

    // ========== HELPER METHODS ==========
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log(message);
    }

    private void Update()
    {
        UpdateGameInfo();
    }

    private void UpdateGameInfo()
    {
        if (gamePanel == null || !gamePanel.activeSelf) return;

        // Cập nhật số lượng players
        if (playerCountText != null && MultiplayerGameManager.Instance != null)
        {
            int thuyTinh = MultiplayerGameManager.Instance.PlayersInThuyTinh;
            int sonTinh = MultiplayerGameManager.Instance.PlayersInSonTinh;
            playerCountText.text = $"Players - Thủy Tinh: {thuyTinh} | Sơn Tinh: {sonTinh}";
        }

        // Cập nhật thông tin scene
        if (sceneInfoText != null && SceneSyncManager.Instance != null)
        {
            string sceneName = SceneSyncManager.Instance.GetCurrentSceneName();
            sceneInfoText.text = $"Scene: {sceneName}";
        }
    }

    public void UpdatePlayerCount(int thuyTinh, int sonTinh)
    {
        if (playerCountText != null)
        {
            playerCountText.text = $"Players - Thủy Tinh: {thuyTinh} | Sơn Tinh: {sonTinh}";
        }
    }

    /// <summary>
    /// Được gọi từ LobbyUIManager khi cả 2 người chơi đã ready và game bắt đầu
    /// </summary>
    public void ShowGamePanelFromLobby()
    {
        Debug.Log("🎮 Game bắt đầu - Chuyển từ Lobby sang Game Panel");
        ShowGamePanel();
    }
    
    /// <summary>
    /// Được gọi từ nút "Hủy trận" trong Lobby — ngắt kết nối và quay về màn hình Intro
    /// </summary>
    public void BackToIntroFromLobby()
    {
        Debug.Log("⬅️ [Lobby] Hủy trận - quay về Intro");

        // Ẩn Lobby UI trước
        if (LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.HideAllPanels();

        // Ngắt kết nối Network
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            Debug.Log("🔌 NetworkManager shutdown");
        }

        // Reset button states
        if (createRoomButton != null) createRoomButton.interactable = true;
        if (joinRoomButton   != null) joinRoomButton.interactable   = true;
        if (joinCodeInput    != null) joinCodeInput.text = "";

        // Hiện lại scene objects và hiển thị Intro Panel
        ShowSceneObjects();
        ShowIntroPanel();
    }
    
    // ========== SCENE OBJECTS MANAGEMENT ==========
    private void HideSceneObjects()
    {
        if (island != null)
        {
            island.SetActive(false);
            Debug.Log("🏝️ Ẩn Island");
        }
        
        if (finalIsland != null)
        {
            finalIsland.SetActive(false);
            Debug.Log("🏝️ Ẩn FinalIsland");
        }
        
        if (audioIntro != null)
        {
            audioIntro.Stop();
            audioIntro.enabled = false;
            Debug.Log("🔇 Tắt AudioIntro");
        }
    }
    
    private void ShowSceneObjects()
    {
        if (island != null)
        {
            island.SetActive(true);
            Debug.Log("🏝️ Hiện Island");
        }
        
        if (finalIsland != null)
        {
            finalIsland.SetActive(true);
            Debug.Log("🏝️ Hiện FinalIsland");
        }
        
        if (audioIntro != null)
        {
            audioIntro.enabled = true;
            if (!audioIntro.isPlaying)
            {
                audioIntro.Play();
            }
            Debug.Log("🔊 Bật AudioIntro");
        }
    }
}


