using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private GameObject lobbyManagerPrefab;
    [SerializeField] private GameObject playerSpawnManagerPrefab;

    private string joinCode;
    private UnityTransport transport;
    private bool isInLobby = false;
    private bool subscribedToLobby = false; // Track subscription status

    public event Action OnClientConnected;
    public event Action OnClientDisconnected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        transport = FindObjectOfType<UnityTransport>();
    }

    private async void Start()
    {
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"✅ Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Failed to initialize Unity Services: {e}");
        }
    }

    public async Task<string> StartHostWithRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedCallback;

            // Spawn LobbyManager thay vì load scene ngay
            SpawnLobbyManager();
            isInLobby = true;

            Debug.Log($"🎮 Host started with Join Code: {joinCode}");
            Debug.Log($"🏠 Lobby created - waiting for players to select characters");
            
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Failed to start host: {e}");
            return null;
        }
    }

    public async Task<bool> JoinGameWithRelay(string code)
    {
        try
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);

            transport.SetClientRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedCallback;

            // Client sẽ nhận LobbyManager sync từ server
            isInLobby = true;

            // Show lobby UI cho client
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.ShowLobby();
                Debug.Log("🏠 Client: Hiển thị lobby UI");
            }

            // Subscribe to LobbyManager events (chờ LobbyManager sync từ server)
            StartCoroutine(WaitForLobbyManagerAndSubscribe());

            Debug.Log($"🎮 Joined game with code: {code}");
            Debug.Log($"🏠 Joined lobby - select your character");
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Failed to join game: {e}");
            return false;
        }
    }

    private void SpawnLobbyManager()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Spawn PlayerSpawnManager first (needed for spawning players)
        if (playerSpawnManagerPrefab != null)
        {
            GameObject spawnManagerObj = Instantiate(playerSpawnManagerPrefab);
            NetworkObject spawnNetObj = spawnManagerObj.GetComponent<NetworkObject>();
            
            if (spawnNetObj != null)
            {
                spawnNetObj.Spawn();
                Debug.Log("✅ PlayerSpawnManager spawned");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ PlayerSpawnManager prefab not assigned! Creating temporary instance...");
            GameObject tempSpawnManager = new GameObject("PlayerSpawnManager");
            PlayerSpawnManager spawnManager = tempSpawnManager.AddComponent<PlayerSpawnManager>();
            NetworkObject spawnNetObj = tempSpawnManager.AddComponent<NetworkObject>();
            spawnNetObj.Spawn();
            Debug.Log("✅ Temporary PlayerSpawnManager created and spawned");
        }

        // Spawn LobbyManager prefab
        if (lobbyManagerPrefab != null)
        {
            GameObject lobbyObj = Instantiate(lobbyManagerPrefab);
            NetworkObject netObj = lobbyObj.GetComponent<NetworkObject>();
            
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log("✅ LobbyManager spawned");

                // Chờ một chút để LobbyManager.Instance được set trong OnNetworkSpawn
                StartCoroutine(WaitAndSubscribeToLobby());
            }
        }
        else
        {
            Debug.LogWarning("⚠️ LobbyManager prefab not assigned! Creating temporary instance...");
            GameObject tempLobby = new GameObject("LobbyManager");
            LobbyManager lobbyManager = tempLobby.AddComponent<LobbyManager>();
            NetworkObject netObj = tempLobby.AddComponent<NetworkObject>();
            netObj.Spawn();
            
            // Chờ một chút để LobbyManager.Instance được set
            StartCoroutine(WaitAndSubscribeToLobby());
        }

        // Show lobby UI
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.ShowLobby(joinCode);
        }
    }

    /// <summary>
    /// Chờ LobbyManager.Instance được set và subscribe event (cho cả host và client)
    /// </summary>
    private System.Collections.IEnumerator WaitAndSubscribeToLobby()
    {
        yield return new WaitForEndOfFrame(); // Chờ OnNetworkSpawn hoàn tất
        
        if (LobbyManager.Instance != null && !subscribedToLobby)
        {
            LobbyManager.Instance.OnGameStarting += OnLobbyGameStarting;
            subscribedToLobby = true;
            Debug.Log("✅ Subscribed to LobbyManager.OnGameStarting");
        }
        else if (subscribedToLobby)
        {
            Debug.Log("⚠️ Already subscribed to LobbyManager");
        }
        else
        {
            Debug.LogError("❌ LobbyManager.Instance is null after spawn!");
        }
    }

    /// <summary>
    /// Coroutine chờ LobbyManager sync từ server và subscribe event (cho client)
    /// </summary>
    private System.Collections.IEnumerator WaitForLobbyManagerAndSubscribe()
    {
        Debug.Log("⏳ Client: Chờ LobbyManager sync từ server...");
        
        float timeout = 10f;
        float elapsed = 0f;
        
        while (LobbyManager.Instance == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (LobbyManager.Instance != null && !subscribedToLobby)
        {
            LobbyManager.Instance.OnGameStarting += OnLobbyGameStarting;
            subscribedToLobby = true;
            Debug.Log("✅ Client: Đã subscribe vào LobbyManager.OnGameStarting");
        }
        else if (subscribedToLobby)
        {
            Debug.Log("⚠️ Client: Already subscribed to LobbyManager");
        }
        else
        {
            Debug.LogError("❌ Client: Timeout chờ LobbyManager sync!");
        }
    }

    private void OnLobbyGameStarting()
    {
        Debug.Log("🚀 Starting game from lobby...");
        isInLobby = false;

        // ✅ Ẩn Lobby UI và hiện Game Panel trên TẤT CẢ clients ngay lập tức
        HideLobbyUIClientRpc();
        ShowGamePanelClientRpc();

        // Load game scene
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
        {
            Debug.Log($"🎮 [Server] About to load Thuy Tinh scene. PlayerSpawnManager exists: {PlayerSpawnManager.Instance != null}");
            
            var status = NetworkManager.Singleton.SceneManager.LoadScene("Thuy Tinh", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            Debug.Log($"🌍 Scene load status: {status}");
            
            if (status == Unity.Netcode.SceneEventProgressStatus.Started)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadEventCompleted;
                Debug.Log("✅ Subscribed to OnLoadEventCompleted");
            }
            else
            {
                Debug.LogError($"❌ Failed to start scene load! Status: {status}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Cannot load scene - IsServer: {NetworkManager.Singleton?.IsServer}, SceneManager: {NetworkManager.Singleton?.SceneManager != null}");
        }
    }

    [ClientRpc]
    private void HideLobbyUIClientRpc()
    {
        Debug.Log($"🙈 [ClientRpc] HideLobbyUIClientRpc called on client {NetworkManager.Singleton.LocalClientId}");
        
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.HideAllPanels();
            Debug.Log("✅ [ClientRpc] Lobby UI hidden successfully");
        }
        else
        {
            Debug.LogWarning("⚠️ [ClientRpc] LobbyUIManager.Instance is null!");
        }
    }

    [ClientRpc]
    private void ShowGamePanelClientRpc()
    {
        Debug.Log($"🎮 [ClientRpc] ShowGamePanelClientRpc called on client {NetworkManager.Singleton.LocalClientId}");
        
        if (MultiplayerUIManager.Instance != null)
        {
            MultiplayerUIManager.Instance.ShowGamePanelFromLobby();
            Debug.Log("✅ [ClientRpc] Game Panel shown successfully");
        }
        else
        {
            Debug.LogWarning("⚠️ [ClientRpc] MultiplayerUIManager.Instance is null!");
        }
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        Debug.Log($"✅ Client {clientId} connected");
        OnClientConnected?.Invoke();
    }

    private void OnClientDisconnectedCallback(ulong clientId)
    {
        Debug.Log($"⚠️ Client {clientId} disconnected");
        
        // Nếu local client disconnect → ẩn UI lobby
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"🔌 Local client disconnected, hiding lobby UI...");
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.HideAllPanels();
            }
        }
        
        OnClientDisconnected?.Invoke();
    }

    private void OnSceneLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"✅ Scene '{sceneName}' loaded for {clientsCompleted.Count} clients (IsServer: {NetworkManager.Singleton.IsServer})");
        Debug.Log($"   Clients completed: {string.Join(", ", clientsCompleted)}");
        Debug.Log($"   Clients timed out: {string.Join(", ", clientsTimedOut)}");
        
        // Chỉ server spawn players
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("⚠️ Not server, skipping player spawn");
            return;
        }
        
        // Spawn players sau khi scene load xong
        if (sceneName == "Thuy Tinh")
        {
            Debug.Log($"🎮 Attempting to spawn players in Thuy Tinh...");
            Debug.Log($"   PlayerSpawnManager.Instance: {PlayerSpawnManager.Instance != null}");
            Debug.Log($"   LobbyManager.Instance: {LobbyManager.Instance != null}");
            
            // Check NetworkChunkManager
            NetworkChunkManager chunkManager = FindObjectOfType<NetworkChunkManager>();
            if (chunkManager != null)
            {
                Debug.Log($"✅ Found NetworkChunkManager in scene");
                
                // Nếu chưa có NetworkObject hoặc chưa spawned, cần spawn manually
                NetworkObject netObj = chunkManager.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    Debug.Log($"📦 Spawning NetworkChunkManager manually...");
                    netObj.Spawn();
                }
            }
            else
            {
                Debug.LogError("❌ NetworkChunkManager NOT FOUND in Thuy Tinh scene!");
            }
            
            if (PlayerSpawnManager.Instance == null)
            {
                Debug.LogError("❌ PlayerSpawnManager.Instance is NULL!");
                return;
            }
            
            // Spawn tất cả clients trong clientsCompleted
            foreach (ulong clientId in clientsCompleted)
            {
                Debug.Log($"🎯 Spawning player {clientId}...");
                PlayerSpawnManager.Instance.SpawnPlayerInScene(clientId, sceneName);
            }
            
            // Đảm bảo spawn cho tất cả connected clients (trường hợp có client đã connect trước đó)
            if (NetworkManager.Singleton != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (!clientsCompleted.Contains(client))
                    {
                        Debug.Log($"🎯 Ensuring player {client} is spawned...");
                        PlayerSpawnManager.Instance.SpawnPlayerInScene(client, sceneName);
                    }
                }
            }
        }
        
        // Unsubscribe sau khi spawn xong
        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
        }
    }

    public void Disconnect()
    {
        Debug.Log("🔌 Disconnecting from network...");
        
        // Ẩn lobby UI trước khi disconnect
        if (LobbyUIManager.Instance != null)
        {
            Debug.Log("🙈 Hiding lobby UI before disconnect...");
            LobbyUIManager.Instance.HideAllPanels();
        }
        
        StartCoroutine(DisconnectAndCleanup());
    }

    private System.Collections.IEnumerator DisconnectAndCleanup()
    {
        // 1. Shutdown network
        if (NetworkManager.Singleton != null)
        {
            // Unsubscribe events
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedCallback;
            
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
            }
            
            if (NetworkManager.Singleton.IsHost)
            {
                Debug.Log("🛑 Shutting down as Host...");
                NetworkManager.Singleton.Shutdown();
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                Debug.Log("🛑 Disconnecting as Client...");
                NetworkManager.Singleton.Shutdown();
            }
        }
        
        // Unsubscribe from LobbyManager và reset flags
        if (LobbyManager.Instance != null && subscribedToLobby)
        {
            LobbyManager.Instance.OnGameStarting -= OnLobbyGameStarting;
            Debug.Log("🔓 Unsubscribed from LobbyManager");
        }
        subscribedToLobby = false;  // Reset subscription flag
        isInLobby = false;           // Reset lobby state
        Debug.Log("🔄 Reset subscription and lobby flags");
        
        yield return new WaitForSeconds(0.5f);
        
        // 2. Destroy all DontDestroyOnLoad objects (players)
        Debug.Log("🧹 Cleaning up DontDestroyOnLoad objects...");
        GameObject[] ddolObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in ddolObjects)
        {
            if (obj.scene.name == null || obj.scene.name == "DontDestroyOnLoad")
            {
                // Không destroy NetworkManager và các manager cần thiết
                if (obj != gameObject && 
                    !obj.name.Contains("NetworkManager") && 
                    !obj.name.Contains("Persistent"))
                {
                    Debug.Log($"  🗑️ Destroying: {obj.name}");
                    Destroy(obj);
                }
            }
        }
        
        // 3. Unload tất cả scenes đã load (trừ Persistent)
        Debug.Log("🗑️ Unloading all loaded scenes...");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !scene.name.Contains("Persistent"))
            {
                Debug.Log($"  📤 Unloading scene: {scene.name}");
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            }
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // 4. Clear managers
        Debug.Log("🔄 Clearing all managers...");
        
        if (PlayerSpawnManager.Instance != null)
        {
            Debug.Log("  🔄 Clearing PlayerSpawnManager...");
            PlayerSpawnManager.Instance.ClearAll();
        }
        
        NetworkChunkManager chunkManager = FindObjectOfType<NetworkChunkManager>();
        if (chunkManager != null)
        {
            Debug.Log("  🔄 Clearing NetworkChunkManager...");
            chunkManager.ClearAll();
        }
        
        if (MultiplayerGameManager.Instance != null)
        {
            Debug.Log("  🔄 Resetting MultiplayerGameManager...");
            // MultiplayerGameManager sẽ tự reset khi scene change
        }
        
        // 5. Clear join code
        joinCode = null;
        
        Debug.Log("✅ Disconnected and cleaned up successfully");
        
        // 6. Reload Persistent scene để reset hoàn toàn về trạng thái ban đầu
        Debug.Log("🔄 Reloading Persistent scene...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Persistent");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedCallback;
            
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
            }
        }
        
        // Unsubscribe from LobbyManager
        if (LobbyManager.Instance != null && subscribedToLobby)
        {
            LobbyManager.Instance.OnGameStarting -= OnLobbyGameStarting;
            subscribedToLobby = false;
        }
    }

    public string GetJoinCode() => joinCode;
    public bool IsHost() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    public bool IsClient() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
}
