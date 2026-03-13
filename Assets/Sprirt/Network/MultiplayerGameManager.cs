using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerGameManager : NetworkBehaviour
{
    public static MultiplayerGameManager Instance { get; private set; }

    [Header("Scene Names")]
    public string thuyTinhSceneName = "Thuy Tinh";
    public string sonTinhSceneName = "Son tinh";

    [Header("Player Tracking")]
    private NetworkVariable<int> playersInThuyTinh = new NetworkVariable<int>(0);
    private NetworkVariable<int> playersInSonTinh = new NetworkVariable<int>(0);

    // Track player positions before portal
    private Dictionary<ulong, Vector3> playerLastPositions = new Dictionary<ulong, Vector3>();
    private Dictionary<ulong, bool> playerPortalUsed = new Dictionary<ulong, bool>();
    
    // Track player current Son Tinh scene
    private Dictionary<ulong, string> playerCurrentSonTinhScene = new Dictionary<ulong, string>();
    
    // Track completed Son Tinh scenes per player
    private Dictionary<ulong, HashSet<string>> playerCompletedScenes = new Dictionary<ulong, HashSet<string>>();

    public int PlayersInThuyTinh => playersInThuyTinh.Value;
    public int PlayersInSonTinh => playersInSonTinh.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            // Reset toàn bộ tracking khi game bắt đầu mới
            playerLastPositions.Clear();
            playerPortalUsed.Clear();
            playerCurrentSonTinhScene.Clear();
            playerCompletedScenes.Clear();
            
            // Cả 2 player bắt đầu ở Thủy Tinh
            playersInThuyTinh.Value = NetworkManager.Singleton.ConnectedClients.Count;
            playersInSonTinh.Value = 0;
            
            // Lắng nghe disconnect để xóa stale data
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            Debug.Log("🔄 MultiplayerGameManager: Reset tất cả tracking data");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        // Xóa stale data của player disconnect để tránh scene bị "Occupied" mãi
        playerCurrentSonTinhScene.Remove(clientId);
        playerLastPositions.Remove(clientId);
        playerPortalUsed.Remove(clientId);
        Debug.Log($"🧹 Cleared tracking data for disconnected client {clientId}");
    }

    /// <summary>Reset tracking data cho 1 player cụ thể (dùng khi debug hoặc respawn)</summary>
    [ServerRpc(RequireOwnership = false)]
    public void ResetPlayerTrackingServerRpc(ulong clientId)
    {
        playerCurrentSonTinhScene.Remove(clientId);
        playerPortalUsed.Remove(clientId);
        Debug.Log($"🔄 Reset tracking for player {clientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerEnteredSceneServerRpc(string sceneName, ulong clientId, Vector3 lastPosition)
    {
        if (!IsServer) return;
        PlayerEnteredSceneServer(sceneName, clientId, lastPosition);
    }

    /// <summary>Gọi trực tiếp từ server (không qua ServerRpc) để tránh lỗi double-RPC.</summary>
    public void PlayerEnteredSceneServer(string sceneName, ulong clientId, Vector3 lastPosition)
    {
        if (!IsServer) return;
        playerLastPositions[clientId] = lastPosition;

        if (sceneName == thuyTinhSceneName)
        {
            playersInThuyTinh.Value++;
            if (playersInSonTinh.Value > 0)
                playersInSonTinh.Value--;
        }
        else if (sceneName == sonTinhSceneName)
        {
            playersInSonTinh.Value++;
            if (playersInThuyTinh.Value > 0)
                playersInThuyTinh.Value--;
        }

        Debug.Log($"📊 Scene status - Thủy Tinh: {playersInThuyTinh.Value}, Sơn Tinh: {playersInSonTinh.Value}");
        UpdateChunkManagerState();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPortalUsedServerRpc(ulong clientId, bool used)
    {
        if (!IsServer) return;
        SetPortalUsedServer(clientId, used);
    }

    /// <summary>Gọi trực tiếp từ server.</summary>
    public void SetPortalUsedServer(ulong clientId, bool used)
    {
        if (!IsServer) return;
        playerPortalUsed[clientId] = used;
    }

    public bool HasPlayerUsedPortal(ulong clientId)
    {
        return playerPortalUsed.ContainsKey(clientId) && playerPortalUsed[clientId];
    }

    public Vector3 GetPlayerLastPosition(ulong clientId)
    {
        if (playerLastPositions.ContainsKey(clientId))
        {
            return playerLastPositions[clientId];
        }
        return Vector3.zero;
    }

    private void UpdateChunkManagerState()
    {
        if (!IsServer) return;

        // Tìm ChunkManager trong scene Thủy Tinh
        ChunkManager chunkManager = FindObjectOfType<ChunkManager>();
        
        if (chunkManager != null)
        {
            bool shouldBeActive = playersInThuyTinh.Value >= 2;
            
            // Nếu cả 2 player ở Thủy Tinh: clean chunk active
            // Nếu chỉ 1 player: clean off, spawn vẫn hoạt động
            UpdateChunkManagerClientRpc(shouldBeActive);
        }
    }

    [ClientRpc]
    private void UpdateChunkManagerClientRpc(bool cleanupActive)
    {
        ChunkManager chunkManager = FindObjectOfType<ChunkManager>();
        if (chunkManager != null)
        {
            // Cập nhật logic cleanup
            if (cleanupActive)
            {
                Debug.Log("✅ Cả 2 player ở Thủy Tinh - Clean chunk ACTIVE");
            }
            else
            {
                Debug.Log("⚠️ Chỉ 1 player ở Thủy Tinh - Clean chunk OFF, spawn vẫn hoạt động");
            }
        }
    }

    public bool ShouldCleanupChunks()
    {
        // Chỉ cleanup khi cả 2 player ở Thủy Tinh
        return playersInThuyTinh.Value >= 2;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetPlayerPortalServerRpc(ulong clientId)
    {
        if (playerPortalUsed.ContainsKey(clientId))
        {
            playerPortalUsed[clientId] = false;
        }
    }
    
    // ===== SƠN TINH SCENE MANAGEMENT =====
    
    [ServerRpc(RequireOwnership = false)]
    public void PlayerEnteredSonTinhSceneServerRpc(ulong clientId, string sonTinhSceneName)
    {
        if (!IsServer) return;
        PlayerEnteredSonTinhSceneServer(clientId, sonTinhSceneName);
    }

    /// <summary>Gọi trực tiếp từ server.</summary>
    public void PlayerEnteredSonTinhSceneServer(ulong clientId, string sonTinhSceneName)
    {
        if (!IsServer) return;
        playerCurrentSonTinhScene[clientId] = sonTinhSceneName;
        Debug.Log($"📍 Player {clientId} entered {sonTinhSceneName}");
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void PlayerLeftSonTinhSceneServerRpc(ulong clientId)
    {
        if (!IsServer) return;
        PlayerLeftSonTinhSceneServer(clientId);
    }

    /// <summary>Gọi trực tiếp từ server.</summary>
    public void PlayerLeftSonTinhSceneServer(ulong clientId)
    {
        if (!IsServer) return;
        if (playerCurrentSonTinhScene.ContainsKey(clientId))
        {
            string sceneName = playerCurrentSonTinhScene[clientId];
            playerCurrentSonTinhScene.Remove(clientId);
            Debug.Log($"🚪 Player {clientId} left {sceneName}");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void MarkSceneCompletedServerRpc(ulong clientId, string sonTinhSceneName)
    {
        if (!playerCompletedScenes.ContainsKey(clientId))
        {
            playerCompletedScenes[clientId] = new HashSet<string>();
        }
        
        playerCompletedScenes[clientId].Add(sonTinhSceneName);
        Debug.Log($"✅ Player {clientId} completed {sonTinhSceneName}");
        
        // Remove player khỏi scene hiện tại (dùng server-only method, không gọi ServerRpc từ ServerRpc)
        PlayerLeftSonTinhSceneServer(clientId);
    }
    
    public bool IsSceneOccupied(string sonTinhSceneName)
    {
        if (!IsServer) return false;
        
        foreach (var kvp in playerCurrentSonTinhScene)
        {
            if (kvp.Value == sonTinhSceneName)
            {
                return true; // Scene đang có player
            }
        }
        return false;
    }
    
    public bool HasPlayerCompletedScene(ulong clientId, string sonTinhSceneName)
    {
        if (!IsServer) return false;
        
        if (playerCompletedScenes.ContainsKey(clientId))
        {
            return playerCompletedScenes[clientId].Contains(sonTinhSceneName);
        }
        return false;
    }
    
    public List<string> GetAvailableSonTinhScenes(ulong clientId, List<string> allScenes)
    {
        if (!IsServer) return new List<string>();
        
        List<string> availableScenes = new List<string>();
        
        foreach (string scene in allScenes)
        {
            // Chỉ chặn scene mà CHÍNH player đó đã hoàn thành
            // KHÔNG chặn theo Occupied - cho phép cả 2 player vào cùng 1 scene
            bool isCompleted = HasPlayerCompletedScene(clientId, scene);
            
            if (!isCompleted)
            {
                availableScenes.Add(scene);
            }
            else
            {
                Debug.Log($"⛔ Scene {scene} unavailable for player {clientId} - already Completed");
            }
        }
        
        Debug.Log($"✅ Player {clientId} has {availableScenes.Count}/{allScenes.Count} available scenes");
        return availableScenes;
    }
}
