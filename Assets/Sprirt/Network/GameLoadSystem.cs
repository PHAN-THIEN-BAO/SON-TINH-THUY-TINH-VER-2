using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameLoadSystem (MonoBehaviour) - Không phải NetworkBehaviour để tránh lỗi spawn timing.
/// Flow:
///   1. LobbyManager.OnGameStarting → ShowLoading (bắt đầu load scene)
///   2. Poll cho đến khi đủ 2 NetworkPlayerController spawn trong scene
///   3. Kiểm tra network ổn định (ConnectedClients == 2)
///   4. Chờ thêm buffer → HideLoading → Unlock input
/// </summary>
public class GameLoadSystem : MonoBehaviour
{
    public static GameLoadSystem Instance { get; private set; }

    [Header("UI Loading")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Image loadingBar;            // Fill Type = Filled, Method = Horizontal

    [Header("Settings")]
    [SerializeField] private int expectedPlayerCount = 2;
    [SerializeField] private float stabilityBuffer = 1.5f;  // Chờ thêm sau khi đủ player
    [SerializeField] private float pollInterval = 0.3f;     // Kiểm tra mỗi 0.3s
    [SerializeField] private float timeout = 30f;           // Timeout tối đa

    // State
    private bool isLoading = false;
    private bool gameReady = false;
    private bool subscribedToLobby = false; // Tránh subscribe nhiều lần

    // ==========================================
    // PUBLIC: Kiểm tra từ CharacterAbility, PlayerController
    // ==========================================
    public static bool Ready => Instance == null || Instance.gameReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Panel bắt đầu inactive (không hiện ở menu)
        HidePanel();
    }

    private void Start()
    {
        // Subscribe vào LobbyManager - có thể chưa spawn ngay, Update() sẽ retry
        TrySubscribeToLobby();
    }

    private void Update()
    {
        // Retry subscribe nếu LobbyManager chưa có khi Start() chạy
        if (!subscribedToLobby)
        {
            TrySubscribeToLobby();
        }
    }

    private void TrySubscribeToLobby()
    {
        if (LobbyManager.Instance != null && !subscribedToLobby)
        {
            LobbyManager.Instance.OnGameStarting += OnGameStarting;
            subscribedToLobby = true;
            Debug.Log("✅ [GameLoadSystem] Subscribed to LobbyManager.OnGameStarting");
        }
    }

    // ==========================================
    // Khi lobby bắt đầu load scene game
    // ==========================================
    private void OnGameStarting()
    {
        if (isLoading) return;
        isLoading = true;
        gameReady = false;

        ShowPanel("Đang tải game...");
        SetBarFill(0.1f);

        StartCoroutine(WaitForPlayersRoutine());
    }

    // ==========================================
    // Poll đến khi đủ player và network ổn định
    // ==========================================
    private IEnumerator WaitForPlayersRoutine()
    {
        float elapsed = 0f;

        // Giai đoạn 1: Chờ scene load (NetworkManager connected)
        SetStatus("Đang kết nối server...");
        while (!IsNetworkReady() && elapsed < timeout)
        {
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
            SetBarFill(Mathf.Lerp(0.1f, 0.4f, elapsed / 10f));
        }

        // Giai đoạn 2: Chờ player spawn
        SetStatus($"Đang chờ người chơi... (0/{expectedPlayerCount})");
        elapsed = 0f;
        while (elapsed < timeout)
        {
            int spawnedCount = CountSpawnedPlayers();
            SetBarFill(Mathf.Lerp(0.4f, 0.85f, (float)spawnedCount / expectedPlayerCount));
            SetStatus($"Đang tải nhân vật... ({spawnedCount}/{expectedPlayerCount})");

            if (spawnedCount >= expectedPlayerCount) break;

            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning("⚠️ [GameLoadSystem] Timeout! Bắt đầu game dù chưa đủ player.");
        }

        // Giai đoạn 3: Buffer ổn định
        SetStatus("Đồng bộ dữ liệu...");
        SetBarFill(0.9f);
        yield return new WaitForSeconds(stabilityBuffer);

        // Giai đoạn 4: Sẵn sàng!
        SetBarFill(1f);
        SetStatus("Sẵn sàng!");
        yield return new WaitForSeconds(0.3f);

        // Unlock game và ẩn loading
        gameReady = true;
        isLoading = false;
        Debug.Log("🎮 [GameLoadSystem] Game sẵn sàng!");

        StartCoroutine(FadeAndHide());
    }

    // ==========================================
    // Helpers
    // ==========================================

    private bool IsNetworkReady()
    {
        if (NetworkManager.Singleton == null) return false;
        return NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost;
    }

    private int CountSpawnedPlayers()
    {
        // Đếm NetworkPlayerController đã spawn trong scene
        var players = FindObjectsOfType<NetworkPlayerController>();
        int count = 0;
        foreach (var p in players)
        {
            if (p.IsSpawned) count++;
        }
        return count;
    }

    private IEnumerator FadeAndHide()
    {
        if (loadingPanel == null) yield break;

        CanvasGroup cg = loadingPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = loadingPanel.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        float duration = 0.6f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        HidePanel();
        if (cg != null) cg.alpha = 1f; // Reset alpha cho lần sau
    }

    private void ShowPanel(string message)
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        SetStatus(message);
    }

    private void HidePanel()
    {
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    private void SetStatus(string msg)
    {
        if (loadingText != null) loadingText.text = msg;
    }

    private void SetBarFill(float fill)
    {
        if (loadingBar != null) loadingBar.fillAmount = fill;
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnGameStarting -= OnGameStarting;
    }

    // ==========================================
    // PUBLIC: Gọi thủ công nếu cần (debug)
    // ==========================================
    public void ForceReady()
    {
        StopAllCoroutines();
        gameReady = true;
        isLoading = false;
        HidePanel();
        Debug.Log("⚡ [GameLoadSystem] ForceReady called!");
    }
}
