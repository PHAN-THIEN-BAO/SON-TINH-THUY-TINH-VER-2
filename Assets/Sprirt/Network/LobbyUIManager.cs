using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Quản lý UI cho lobby với character selection
/// Hover vào ảnh → phóng to, click → khóa nhân vật
/// Mỗi ảnh có nút Hủy riêng, không còn nút Ready
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Canvas Reference")]
    [SerializeField] private GameObject lobbyCanvasRoot;

    [Header("UI Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Character Card - Son Tinh")]
    [SerializeField] private Image sonTinhPreview;           // Ảnh nhân vật - click để chọn
    [SerializeField] private Button sonTinhCancelButton;     // Nút Hủy riêng cho Son Tinh
    [SerializeField] private GameObject sonTinhSelectedIndicator;
    [SerializeField] private Sprite sonTinhLockedSprite;     // Sprite khóa — kéo thẳng Sprite vào đây

    [Header("Character Card - Thuy Tinh")]
    [SerializeField] private Image thuyTinhPreview;          // Ảnh nhân vật - click để chọn
    [SerializeField] private Button thuyTinhCancelButton;    // Nút Hủy riêng cho Thuy Tinh
    [SerializeField] private GameObject thuyTinhSelectedIndicator;
    [SerializeField] private Sprite thuyTinhLockedSprite;    // Sprite khóa — kéo thẳng Sprite vào đây

    [Header("Start Game (Host Only)")]
    [SerializeField] private Button startGameButton;         // Chỉ hiện với Host khi cả 2 đã chọn

    [Header("Cancel Match")]
    [SerializeField] private Button cancelLobbyButton;       // Hủy trận - quay về intro

    [Header("Join Code")]
    [SerializeField] private Button copyJoinCodeButton;
    [SerializeField] private Button toggleShowCodeButton;
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private Image toggleButtonImage;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;

    [Header("Player Info Display")]
    [SerializeField] private TextMeshProUGUI player1NameText;
    [SerializeField] private TextMeshProUGUI player2NameText;
    [SerializeField] private TextMeshProUGUI player1CharacterText;
    [SerializeField] private TextMeshProUGUI player2CharacterText;
    [SerializeField] private GameObject player1ReadyIndicator;
    [SerializeField] private GameObject player2ReadyIndicator;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Character Sprites")]
    [SerializeField] private Sprite sonTinhSprite;
    [SerializeField] private Sprite thuyTinhSprite;

    [Header("Hover Scale Settings")]
    [SerializeField] private float hoverScale = 1.08f;       // Tỷ lệ phóng to khi hover
    [SerializeField] private float scaleSpeed = 8f;          // Tốc độ animation scale

    // Private state
    private string currentJoinCode = "";
    private bool isCodeVisible = true;

    // Scale animation coroutines
    private Coroutine sonTinhScaleCoroutine;
    private Coroutine thuyTinhScaleCoroutine;

    // Image overlay tự tạo để hiện sprite khóa
    private Image sonTinhLockOverlay;
    private Image thuyTinhLockOverlay;

    // Constants
    private const float REFRESH_DELAY_SHORT = 0.1f;
    private const float REFRESH_DELAY_MEDIUM = 0.3f;
    private const float REFRESH_DELAY_LONG = 0.5f;

    private ulong MyClientId => Unity.Netcode.NetworkManager.Singleton != null
        ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 999;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.transform.parent == null)
            DontDestroyOnLoad(mainCam.gameObject);
    }

    private void Start()
    {
        // Setup character image interactions (hover + click)
        if (sonTinhPreview != null)
            SetupCharacterImageEvents(sonTinhPreview, LobbyManager.CharacterType.SonTinh);

        if (thuyTinhPreview != null)
            SetupCharacterImageEvents(thuyTinhPreview, LobbyManager.CharacterType.ThuyTinh);

        // Per-character cancel buttons
        if (sonTinhCancelButton != null)
        {
            sonTinhCancelButton.onClick.AddListener(() => CancelCharacter(LobbyManager.CharacterType.SonTinh));
            sonTinhCancelButton.gameObject.SetActive(false);
        }

        if (thuyTinhCancelButton != null)
        {
            thuyTinhCancelButton.onClick.AddListener(() => CancelCharacter(LobbyManager.CharacterType.ThuyTinh));
            thuyTinhCancelButton.gameObject.SetActive(false);
        }

        // Start game button (host only)
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            startGameButton.gameObject.SetActive(false);
        }

        // Nút Hủy trận - quay về intro
        if (cancelLobbyButton != null)
            cancelLobbyButton.onClick.AddListener(OnCancelLobbyButtonClicked);

        // Join code buttons
        if (copyJoinCodeButton != null)
            copyJoinCodeButton.onClick.AddListener(OnCopyJoinCodeButtonClicked);

        if (toggleShowCodeButton != null)
            toggleShowCodeButton.onClick.AddListener(OnToggleShowCodeButtonClicked);

        if (toggleButtonImage == null && toggleShowCodeButton != null)
            toggleButtonImage = toggleShowCodeButton.GetComponent<Image>();

        HideAllPanels();

        // Setup sprites
        if (sonTinhPreview != null && sonTinhSprite != null) sonTinhPreview.sprite = sonTinhSprite;
        if (thuyTinhPreview != null && thuyTinhSprite != null) thuyTinhPreview.sprite = thuyTinhSprite;

        // Tạo lock overlay trên mỗi card (tự động, không cần tạo thủ công trong Unity)
        sonTinhLockOverlay  = CreateLockOverlay(sonTinhPreview,  sonTinhLockedSprite);
        thuyTinhLockOverlay = CreateLockOverlay(thuyTinhPreview, thuyTinhLockedSprite);
    }

    private void OnEnable()
    {
        if (LobbyManager.Instance != null) SubscribeToLobbyEvents();
    }

    private void OnDisable()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnPlayerJoinedLobby   -= OnPlayerJoinedLobby;
            LobbyManager.Instance.OnPlayerLeftLobby     -= OnPlayerLeftLobby;
            LobbyManager.Instance.OnPlayerDataChanged   -= OnPlayerDataChanged;
            LobbyManager.Instance.OnGameStarting        -= OnGameStarting;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Hover + Click Setup (EventTrigger)
    // ─────────────────────────────────────────────────────────────

    private void SetupCharacterImageEvents(Image img, LobbyManager.CharacterType character)
    {
        // Cần Raycast Target = true để nhận sự kiện
        img.raycastTarget = true;

        EventTrigger trigger = img.GetComponent<EventTrigger>() ?? img.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        // Pointer Enter → scale up
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => OnCharacterImageHoverEnter(img, character));
        trigger.triggers.Add(enterEntry);

        // Pointer Exit → scale back
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => OnCharacterImageHoverExit(img, character));
        trigger.triggers.Add(exitEntry);

        // Pointer Click → select / lock
        var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener(_ => OnCharacterImageClicked(character));
        trigger.triggers.Add(clickEntry);
    }

    private void OnCharacterImageHoverEnter(Image img, LobbyManager.CharacterType character)
    {
        // Không hover nếu đã bị người khác khóa
        if (IsCharacterLockedByOther(character)) return;

        if (character == LobbyManager.CharacterType.SonTinh)
        {
            if (sonTinhScaleCoroutine != null) StopCoroutine(sonTinhScaleCoroutine);
            sonTinhScaleCoroutine = StartCoroutine(ScaleTo(img.transform, hoverScale));
        }
        else
        {
            if (thuyTinhScaleCoroutine != null) StopCoroutine(thuyTinhScaleCoroutine);
            thuyTinhScaleCoroutine = StartCoroutine(ScaleTo(img.transform, hoverScale));
        }
    }

    private void OnCharacterImageHoverExit(Image img, LobbyManager.CharacterType character)
    {
        // Giữ scale lớn nếu đây là nhân vật mình đang chọn
        LobbyManager.CharacterType myChar = GetMySelectedCharacter();
        if (myChar == character) return;

        if (character == LobbyManager.CharacterType.SonTinh)
        {
            if (sonTinhScaleCoroutine != null) StopCoroutine(sonTinhScaleCoroutine);
            sonTinhScaleCoroutine = StartCoroutine(ScaleTo(img.transform, 1f));
        }
        else
        {
            if (thuyTinhScaleCoroutine != null) StopCoroutine(thuyTinhScaleCoroutine);
            thuyTinhScaleCoroutine = StartCoroutine(ScaleTo(img.transform, 1f));
        }
    }

    private void OnCharacterImageClicked(LobbyManager.CharacterType character)
    {
        if (!IsManagersValid()) return;

        LobbyManager.CharacterType myChar = GetMySelectedCharacter();

        // Nếu đã chọn cái này rồi → bỏ qua
        if (myChar == character) return;

        // Nếu đã chọn nhân vật khác → phải hủy trước
        if (myChar != LobbyManager.CharacterType.None)
        {
            UpdateStatus($"❌ Hãy hủy {GetCharacterName(myChar)} trước!");
            return;
        }

        // Nếu nhân vật bị khóa bởi người khác → không cho chọn
        if (IsCharacterLockedByOther(character))
        {
            UpdateStatus($"❌ {GetCharacterName(character)} đã có người chọn rồi!");
            return;
        }

        Debug.Log($"🎯 [UI] Client {MyClientId} clicking to select: {character}");
        LobbyManager.Instance.SelectCharacter(character);
        UpdateStatus($"✅ Bạn đã chọn {GetCharacterName(character)}");
    }

    private void CancelCharacter(LobbyManager.CharacterType character)
    {
        if (!IsManagersValid()) return;

        LobbyManager.CharacterType myChar = GetMySelectedCharacter();
        if (myChar != character) return; // Chỉ hủy nhân vật của mình

        Debug.Log($"❌ [UI] Client {MyClientId} canceling {character}");
        LobbyManager.Instance.SelectCharacter(LobbyManager.CharacterType.None);
        UpdateStatus("Chọn nhân vật của bạn!");

        StartCoroutine(RefreshUIWithDelays(new[] { REFRESH_DELAY_SHORT, REFRESH_DELAY_MEDIUM }));
    }

    // ─────────────────────────────────────────────────────────────
    // Scale Animation
    // ─────────────────────────────────────────────────────────────

    private IEnumerator ScaleTo(Transform t, float targetScale)
    {
        Vector3 target = Vector3.one * targetScale;
        while (!Mathf.Approximately(t.localScale.x, targetScale))
        {
            t.localScale = Vector3.Lerp(t.localScale, target, Time.deltaTime * scaleSpeed);
            if (Vector3.Distance(t.localScale, target) < 0.001f) { t.localScale = target; break; }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Show / Hide
    // ─────────────────────────────────────────────────────────────

    public void ShowLobby(string joinCode = null)
    {
        Debug.Log($"👀 [LobbyUIManager] Client {MyClientId}: ShowLobby called");

        lobbyPanel?.SetActive(true);
        characterSelectionPanel?.SetActive(true);
        SetCanvasGroupVisibility(true);

        if (!string.IsNullOrEmpty(joinCode))
        {
            currentJoinCode = joinCode;
            isCodeVisible = true;
            UpdateJoinCodeDisplay();
        }

        SubscribeToLobbyEvents();
        UpdateStatus("Đợi người chơi... Chọn nhân vật của bạn!");
        RefreshLobbyDisplay();

        StartCoroutine(RefreshUIWithDelays(new[] { REFRESH_DELAY_SHORT, REFRESH_DELAY_MEDIUM, REFRESH_DELAY_LONG }));
    }

    public void ShowLobbyUI()
    {
        ActivateLobbyCanvas();
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ShowLobby();
    }

    public void HideAllPanels()
    {
        Debug.Log($"🙈 [LobbyUIManager] HideAllPanels called on client {MyClientId}");
        lobbyPanel?.SetActive(false);
        characterSelectionPanel?.SetActive(false);
        SetCanvasGroupVisibility(false);
    }

    private void ActivateLobbyCanvas()
    {
        if (lobbyCanvasRoot != null) { if (!lobbyCanvasRoot.activeSelf) lobbyCanvasRoot.SetActive(true); return; }

        Canvas rootCanvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
        if (rootCanvas != null) { if (!rootCanvas.gameObject.activeSelf) rootCanvas.gameObject.SetActive(true); return; }

        Canvas[] allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas.gameObject.name == "LobbyCanvas" && canvas.gameObject.scene.isLoaded)
            {
                if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
                return;
            }
        }
        Debug.LogWarning("⚠️ Không tìm thấy LobbyCanvas!");
    }

    private void SetCanvasGroupVisibility(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // ─────────────────────────────────────────────────────────────
    // Event Subscriptions
    // ─────────────────────────────────────────────────────────────

    private void SubscribeToLobbyEvents()
    {
        if (LobbyManager.Instance == null) { Debug.LogWarning("⚠️ LobbyManager.Instance is null"); return; }

        LobbyManager.Instance.OnPlayerJoinedLobby   -= OnPlayerJoinedLobby;
        LobbyManager.Instance.OnPlayerLeftLobby     -= OnPlayerLeftLobby;
        LobbyManager.Instance.OnPlayerDataChanged   -= OnPlayerDataChanged;
        LobbyManager.Instance.OnGameStarting        -= OnGameStarting;

        LobbyManager.Instance.OnPlayerJoinedLobby   += OnPlayerJoinedLobby;
        LobbyManager.Instance.OnPlayerLeftLobby     += OnPlayerLeftLobby;
        LobbyManager.Instance.OnPlayerDataChanged   += OnPlayerDataChanged;
        LobbyManager.Instance.OnGameStarting        += OnGameStarting;

        Debug.Log("✅ LobbyUIManager subscribed to LobbyManager events");
    }

    // ─────────────────────────────────────────────────────────────
    // Lobby Event Handlers
    // ─────────────────────────────────────────────────────────────

    private void OnPlayerJoinedLobby(LobbyManager.PlayerLobbyData playerData)
    {
        Debug.Log($"UI: Player {playerData.clientId} joined lobby");
        RefreshLobbyDisplay();
    }

    private void OnPlayerLeftLobby(ulong clientId)
    {
        Debug.Log($"UI: Player {clientId} left lobby");
        RefreshLobbyDisplay();
    }

    private void OnPlayerDataChanged(LobbyManager.PlayerLobbyData playerData)
    {
        Debug.Log($"🔄 [UI] Player {playerData.clientId} data changed: Character={playerData.selectedCharacter}, Ready={playerData.isReady}");

        if (playerData.clientId == MyClientId)
            UpdateStatusForPlayerData(playerData);

        RefreshLobbyDisplay();
        UpdateCharacterButtons();
    }

    private void OnGameStarting()
    {
        Debug.Log($"🚀 [LobbyUIManager] OnGameStarting triggered");
        UpdateStatus("🚀 Bắt đầu game!");
        HideAllPanels();

        if (MultiplayerUIManager.Instance != null)
            MultiplayerUIManager.Instance.ShowGamePanelFromLobby();
        else
            Debug.LogWarning("⚠️ [LobbyUIManager] MultiplayerUIManager.Instance not found!");
    }

    // ─────────────────────────────────────────────────────────────
    // Start Game Button
    // ─────────────────────────────────────────────────────────────

    private void OnStartGameButtonClicked()
    {
        if (!IsManagersValid()) return;
        if (!Unity.Netcode.NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("⚠️ [UI] Chỉ Host mới có thể bắt đầu game!");
            return;
        }
        Debug.Log("🚀 [UI] Host bắt đầu game!");
        UpdateStatus("🚀 Host đang bắt đầu game...");
        LobbyManager.Instance.StartGame();
    }

    private void OnCancelLobbyButtonClicked()
    {
        Debug.Log("⬅️ [UI] Hủy trận - quay về Intro");
        if (MultiplayerUIManager.Instance != null)
            MultiplayerUIManager.Instance.BackToIntroFromLobby();
        else
            Debug.LogWarning("⚠️ MultiplayerUIManager.Instance not found!");
    }

    // ─────────────────────────────────────────────────────────────
    // Refresh / Display
    // ─────────────────────────────────────────────────────────────

    public void ForceRefreshUI()
    {
        if (LobbyManager.Instance != null)
        {
            RefreshLobbyDisplay();
            UpdateCharacterButtons();
            UnityEngine.Canvas.ForceUpdateCanvases();
        }
    }

    private System.Collections.IEnumerator RefreshUIWithDelays(float[] delays)
    {
        foreach (float delay in delays)
        {
            yield return new WaitForSeconds(delay);
            if (LobbyManager.Instance != null)
            {
                RefreshLobbyDisplay();
                UpdateCharacterButtons();
                UnityEngine.Canvas.ForceUpdateCanvases();
            }
        }
    }

    private void RefreshLobbyDisplay()
    {
        if (!IsManagersValid()) return;

        List<LobbyManager.PlayerLobbyData> players = LobbyManager.Instance.GetPlayersInLobby();
        ResetPlayerDisplay();

        var (local, other) = GetLocalAndOtherPlayer(players);

        if (local.HasValue)
            DisplayPlayerInfo(local.Value, player1NameText, player1CharacterText, player1ReadyIndicator, "You");
        if (other.HasValue)
            DisplayPlayerInfo(other.Value, player2NameText, player2CharacterText, player2ReadyIndicator, "Other Player");

        UpdateCharacterButtons();
    }

    private void UpdateCharacterButtons()
    {
        if (!IsManagersValid()) return;

        List<LobbyManager.PlayerLobbyData> players = LobbyManager.Instance.GetPlayersInLobby();
        LobbyManager.CharacterType myCharacter = GetMySelectedCharacter();

        // Xác định nhân vật nào bị khóa bởi người KHÁC
        bool sonTinhLockedByOther = false;
        bool thuyTinhLockedByOther = false;
        foreach (var p in players)
        {
            if (p.clientId == MyClientId) continue;
            if (p.selectedCharacter == LobbyManager.CharacterType.SonTinh) sonTinhLockedByOther = true;
            if (p.selectedCharacter == LobbyManager.CharacterType.ThuyTinh) thuyTinhLockedByOther = true;
        }

        // ── Son Tinh card ──
        bool iHaveSonTinh = myCharacter == LobbyManager.CharacterType.SonTinh;
        UpdateCardVisual(
            sonTinhPreview,
            sonTinhCancelButton,
            sonTinhSelectedIndicator,
            sonTinhLockOverlay,
            iHaveSonTinh,
            sonTinhLockedByOther,
            ref sonTinhScaleCoroutine
        );

        // ── Thuy Tinh card ──
        bool iHaveThuyTinh = myCharacter == LobbyManager.CharacterType.ThuyTinh;
        UpdateCardVisual(
            thuyTinhPreview,
            thuyTinhCancelButton,
            thuyTinhSelectedIndicator,
            thuyTinhLockOverlay,
            iHaveThuyTinh,
            thuyTinhLockedByOther,
            ref thuyTinhScaleCoroutine
        );

        // ── Start Game button (Host only) ──
        UpdateStartGameButton(players);
    }

    /// <summary>
    /// Cập nhật trạng thái visual của mỗi character card
    /// </summary>
    private void UpdateCardVisual(
        Image img,
        Button cancelBtn,
        GameObject selectedIndicator,
        Image lockOverlay,
        bool isMine,
        bool lockedByOther,
        ref Coroutine scaleCoroutine)
    {
        if (cancelBtn != null)
            cancelBtn.gameObject.SetActive(isMine);

        if (selectedIndicator != null) selectedIndicator.SetActive(isMine);

        // Hiện sprite khóa qua overlay Image
        if (lockOverlay != null)
            lockOverlay.enabled = lockedByOther && !isMine;

        if (img != null)
        {
            float targetScale = isMine ? hoverScale : 1f;
            if (!Mathf.Approximately(img.transform.localScale.x, targetScale))
            {
                if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
                scaleCoroutine = StartCoroutine(ScaleTo(img.transform, targetScale));
            }
        }
    }

    /// <summary>
    /// Tự tạo một Image overlay con của card để hiện sprite lock
    /// </summary>
    private Image CreateLockOverlay(Image parentImg, Sprite lockSprite)
    {
        if (lockSprite == null) return null;

        GameObject go = new GameObject("LockOverlay");
        go.transform.SetParent(parentImg.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image overlay = go.AddComponent<Image>();
        overlay.sprite = lockSprite;
        overlay.raycastTarget = false;  // Không chặn click
        overlay.enabled = false;        // Ẩn mặc định
        return overlay;
    }

    private void UpdateStartGameButton(List<LobbyManager.PlayerLobbyData> players)
    {
        if (startGameButton == null) return;

        bool isHost = Unity.Netcode.NetworkManager.Singleton != null
                      && Unity.Netcode.NetworkManager.Singleton.IsHost;
        if (!isHost) { startGameButton.gameObject.SetActive(false); return; }

        bool bothChosen = players.Count >= 2;
        if (bothChosen)
        {
            foreach (var player in players)
            {
                if (player.selectedCharacter == LobbyManager.CharacterType.None)
                { bothChosen = false; break; }
            }
        }

        startGameButton.gameObject.SetActive(bothChosen);
    }

    // ─────────────────────────────────────────────────────────────
    // Player Info Display Helpers
    // ─────────────────────────────────────────────────────────────

    private (LobbyManager.PlayerLobbyData?, LobbyManager.PlayerLobbyData?) GetLocalAndOtherPlayer(
        List<LobbyManager.PlayerLobbyData> players)
    {
        LobbyManager.PlayerLobbyData? local = null, other = null;
        foreach (var p in players)
        {
            if (p.clientId == MyClientId) local = p;
            else other = p;
        }
        return (local, other);
    }

    private void DisplayPlayerInfo(LobbyManager.PlayerLobbyData player,
        TextMeshProUGUI nameText, TextMeshProUGUI characterText,
        GameObject readyIndicator, string displayName)
    {
        if (nameText != null) nameText.text = displayName;
        if (characterText != null) characterText.text = GetCharacterName(player.selectedCharacter);
        if (readyIndicator != null) readyIndicator.SetActive(player.isReady);
    }

    private void ResetPlayerDisplay()
    {
        void ResetText(TextMeshProUGUI t, string msg) { if (t) { t.text = msg; t.ForceMeshUpdate(); } }

        ResetText(player1NameText, "Đợi...");
        ResetText(player2NameText, "Đợi...");
        ResetText(player1CharacterText, "Chưa chọn");
        ResetText(player2CharacterText, "Chưa chọn");

        player1ReadyIndicator?.SetActive(false);
        player2ReadyIndicator?.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // Join Code
    // ─────────────────────────────────────────────────────────────

    private void OnCopyJoinCodeButtonClicked()
    {
        if (string.IsNullOrEmpty(currentJoinCode)) { UpdateStatus("⚠️ Không có join code!"); return; }
        GUIUtility.systemCopyBuffer = currentJoinCode;
        Debug.Log($"📋 Copied join code: {currentJoinCode}");
    }

    private void OnToggleShowCodeButtonClicked()
    {
        if (string.IsNullOrEmpty(currentJoinCode)) return;
        isCodeVisible = !isCodeVisible;
        UpdateJoinCodeDisplay();
    }

    private void UpdateJoinCodeDisplay()
    {
        if (joinCodeText == null || string.IsNullOrEmpty(currentJoinCode)) return;
        joinCodeText.text = isCodeVisible
            ? $"Join Code: {currentJoinCode}"
            : $"Join Code: {new string('•', currentJoinCode.Length)}";
        UpdateToggleButtonIcon();
    }

    private void UpdateToggleButtonIcon()
    {
        if (toggleButtonImage != null && eyeOpenSprite != null && eyeClosedSprite != null)
            toggleButtonImage.sprite = isCodeVisible ? eyeOpenSprite : eyeClosedSprite;
        else if (toggleShowCodeButton != null)
        {
            var t = toggleShowCodeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = isCodeVisible ? "👁️" : "🙈";
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Status
    // ─────────────────────────────────────────────────────────────

    private void UpdateStatusForPlayerData(LobbyManager.PlayerLobbyData playerData)
    {
        if (playerData.selectedCharacter != LobbyManager.CharacterType.None)
            UpdateStatus($"✅ Bạn đã chọn {GetCharacterName(playerData.selectedCharacter)}");
        else
            UpdateStatus("Chọn nhân vật của bạn!");
    }

    public void ShowStatus(string message) => UpdateStatus(message);

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"📢 Lobby Status: {message}");
    }

    // ─────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────

    private bool IsCharacterLockedByOther(LobbyManager.CharacterType character)
    {
        if (!IsManagersValid()) return false;
        foreach (var p in LobbyManager.Instance.GetPlayersInLobby())
        {
            if (p.clientId != MyClientId && p.selectedCharacter == character) return true;
        }
        return false;
    }

    private LobbyManager.CharacterType GetMySelectedCharacter()
    {
        if (!IsManagersValid()) return LobbyManager.CharacterType.None;
        foreach (var p in LobbyManager.Instance.GetPlayersInLobby())
        {
            if (p.clientId == MyClientId) return p.selectedCharacter;
        }
        return LobbyManager.CharacterType.None;
    }

    private bool IsManagersValid() =>
        LobbyManager.Instance != null && Unity.Netcode.NetworkManager.Singleton != null;

    private string GetCharacterName(LobbyManager.CharacterType character) => character switch
    {
        LobbyManager.CharacterType.SonTinh  => "Sơn Tinh",
        LobbyManager.CharacterType.ThuyTinh => "Thủy Tinh",
        _                                   => "Chưa chọn"
    };
}
