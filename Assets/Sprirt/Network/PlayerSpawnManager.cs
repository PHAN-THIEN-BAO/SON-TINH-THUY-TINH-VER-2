using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawnManager : NetworkBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Spawn Points")]
    [SerializeField] private Transform[] thuyTinhSpawnPoints;
    [SerializeField] private Transform[] sonTinhSpawnPoints;

    [Header("Player Prefabs - 2 Nhân vật khác nhau")]
    [SerializeField] private GameObject sonTinhPrefab;  // Player 1 - Sơn Tinh
    [SerializeField] private GameObject thuyTinhPrefab; // Player 2 - Thủy Tinh

    private Dictionary<ulong, NetworkPlayerController> spawnedPlayers = new Dictionary<ulong, NetworkPlayerController>();
    private Dictionary<ulong, Vector3> savedPositions = new Dictionary<ulong, Vector3>();

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
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"✅ Client {clientId} connected - will spawn when game scene loads");
        
        // KHÔNG tự động spawn player nữa
        // Player sẽ được spawn bởi NetworkGameManager sau khi:
        // 1. Qua lobby
        // 2. Chọn character
        // 3. Ready và load game scene
    }

    private System.Collections.IEnumerator EnsurePlayerSpawned(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (!IsServer) yield break;
        
        // Kiểm tra xem có đang ở lobby không
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "Persistent")
        {
            Debug.Log($"⏸️ Player {clientId} is in lobby (Persistent scene) - waiting for game to start");
            yield break;
        }
        
        // Chỉ spawn nếu đang ở game scene (Thuy Tinh hoặc Son Tinh)
        if (!spawnedPlayers.ContainsKey(clientId) && IsGameScene(currentScene))
        {
            Debug.Log($"⚠️ Player {clientId} chưa được spawn sau {delay}s, spawn ngay trong scene {currentScene}");
            SpawnPlayerInScene(clientId, currentScene);
        }
        else
        {
            Debug.Log($"✅ Player {clientId} đã được spawn thành công hoặc không phải game scene");
        }
    }
    
    /// <summary>
    /// Kiểm tra xem có phải game scene không (không phải lobby/persistent)
    /// </summary>
    private bool IsGameScene(string sceneName)
    {
        return sceneName.Contains("Thuy") || sceneName.Contains("Son") || 
               sceneName == "Thuy Tinh" || sceneName == "Son Tinh";
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (spawnedPlayers.ContainsKey(clientId))
        {
            NetworkObject playerNetworkObject = spawnedPlayers[clientId].GetComponent<NetworkObject>();
            if (playerNetworkObject != null)
            {
                playerNetworkObject.Despawn();
            }
            spawnedPlayers.Remove(clientId);
        }

        if (savedPositions.ContainsKey(clientId))
        {
            savedPositions.Remove(clientId);
        }
    }

    public void SpawnPlayerInScene(ulong clientId, string sceneName)
    {
        Debug.Log($"🎯 [SpawnPlayerInScene] Called for client {clientId} in scene {sceneName}");
        Debug.Log($"   IsServer: {IsServer}");
        
        if (!IsServer) 
        {
            Debug.LogWarning($"⚠️ Not server, cannot spawn player {clientId}");
            return;
        }

        // Kiểm tra xem player đã được spawn chưa
        if (spawnedPlayers.ContainsKey(clientId))
        {
            Debug.Log($"⚠️ Player {clientId} đã được spawn rồi, bỏ qua");
            return;
        }

        Debug.Log($"📍 Getting spawn position for client {clientId}...");
        Vector3 spawnPosition = GetSpawnPosition(clientId, sceneName);
        Debug.Log($"   Spawn position: {spawnPosition}");
        
        // Lấy character selection từ LobbyManager
        Debug.Log($"🎭 Getting player prefab from lobby for client {clientId}...");
        GameObject prefabToSpawn = GetPlayerPrefabFromLobby(clientId);
        
        if (prefabToSpawn == null)
        {
            Debug.LogError($"❌ Player prefab not assigned for clientId {clientId}!");
            Debug.LogError($"   Make sure Player 1.prefab and Player 2.prefab are assigned in PlayerSpawnManager Inspector!");
            return;
        }

        Debug.Log($"🔨 Instantiating player prefab: {prefabToSpawn.name} at {spawnPosition}");
        GameObject playerInstance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        Debug.Log($"✅ Player instance created: {playerInstance.name}");
        
        // ✅ Đưa player vào DontDestroyOnLoad để persist và dễ tìm
        DontDestroyOnLoad(playerInstance);
        Debug.Log($"📦 Player moved to DontDestroyOnLoad");
        
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            
            NetworkPlayerController playerController = playerInstance.GetComponent<NetworkPlayerController>();
            if (playerController != null)
            {
                spawnedPlayers[clientId] = playerController;
                
                // Lấy tên character từ prefab hoặc LobbyManager
                string characterName = "Unknown";
                if (LobbyManager.Instance != null)
                {
                    var charType = LobbyManager.Instance.GetPlayerCharacter(clientId);
                    characterName = charType == LobbyManager.CharacterType.SonTinh ? "Sơn Tinh" : "Thủy Tinh";
                }
                else
                {
                    characterName = (clientId == 0) ? "Sơn Tinh" : "Thủy Tinh";
                }
                
                Debug.Log($"✅ Player {clientId} ({characterName}) spawned at {spawnPosition} in DontDestroyOnLoad");
            }
        }
    }

    private Vector3 GetSpawnPosition(ulong clientId, string sceneName)
    {
        Debug.Log($"   Scene name: {sceneName}");
        
        // Kiểm tra nếu có vị trí đã lưu (khi quay về từ Sơn Tinh)
        if (savedPositions.ContainsKey(clientId))
        {
            Vector3 savedPos = savedPositions[clientId];
            Debug.Log($"🔄 Restoring saved position for player {clientId}: {savedPos}");
            return savedPos;
        }

        // Tự động tìm spawn points trong scene hiện tại
        Debug.Log($"🔍 Searching for spawn points in {sceneName}...");
        Transform[] spawnPoints = FindSpawnPointsInScene(sceneName);
        
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = (int)clientId % spawnPoints.Length;
            Vector3 pos = spawnPoints[index].position;
            
            Debug.Log($"📍 Using spawn point {index} for player {clientId} in {sceneName} at {pos}");
            return pos;
        }

        // Fallback: vị trí mặc định với offset để tránh spawn cùng chỗ
        Vector3 fallbackPos = new Vector3((float)clientId * 3f, 2f, 0f);
        Debug.LogWarning($"⚠️ No spawn points found in {sceneName}! Using fallback position: {fallbackPos} for player {clientId}");
        return fallbackPos;
    }

    /// <summary>
    /// Tự động tìm spawn points trong scene bằng tag hoặc tên
    /// </summary>
    private Transform[] FindSpawnPointsInScene(string sceneName)
    {
        // Tìm theo tag (khuyến nghị)
        GameObject[] spawnPointObjects = null;
        
        if (sceneName.Contains("Thuy"))
        {
            spawnPointObjects = GameObject.FindGameObjectsWithTag("ThuyTinhSpawn");
        }
        else if (sceneName.Contains("Son"))
        {
            spawnPointObjects = GameObject.FindGameObjectsWithTag("SonTinhSpawn");
        }

        // Nếu không tìm thấy theo tag, tìm theo tên
        if (spawnPointObjects == null || spawnPointObjects.Length == 0)
        {
            string searchName = sceneName.Contains("Thuy") ? "ThuyTinhSpawnPoint" : "SonTinhSpawnPoint";
            var allObjects = GameObject.FindObjectsOfType<Transform>();
            
            List<GameObject> foundObjects = new List<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.Contains(searchName))
                {
                    foundObjects.Add(obj.gameObject);
                }
            }
            spawnPointObjects = foundObjects.ToArray();
        }

        if (spawnPointObjects != null && spawnPointObjects.Length > 0)
        {
            Transform[] transforms = new Transform[spawnPointObjects.Length];
            for (int i = 0; i < spawnPointObjects.Length; i++)
            {
                transforms[i] = spawnPointObjects[i].transform;
            }
            Debug.Log($"✅ Found {transforms.Length} spawn points in {sceneName}");
            return transforms;
        }

        Debug.LogWarning($"⚠️ No spawn points found in {sceneName}!");
        return null;
    }

    public void SavePlayerPosition(ulong clientId, Vector3 position)
    {
        savedPositions[clientId] = position;
        Debug.Log($"💾 Saved position for player {clientId}: {position}");
    }

    
    [ServerRpc(RequireOwnership = false)]
    public void SavePlayerPositionServerRpc(ulong clientId, Vector3 position, string sceneName)
    {
        savedPositions[clientId] = position;
        Debug.Log($"💾 [Server] Saved player {clientId} position: {position} in scene: {sceneName}");
    }

    public Vector3 GetSavedPosition(ulong clientId)
    {
        if (savedPositions.ContainsKey(clientId))
        {
            return savedPositions[clientId];
        }
        return Vector3.zero;
    }

    /// <summary>
    /// Lấy player prefab dựa trên character selection từ LobbyManager
    /// </summary>
    private GameObject GetPlayerPrefabFromLobby(ulong clientId)
    {
        // Nếu có LobbyManager, lấy character từ lobby
        if (LobbyManager.Instance != null)
        {
            LobbyManager.CharacterType selectedCharacter = LobbyManager.Instance.GetPlayerCharacter(clientId);
            
            switch (selectedCharacter)
            {
                case LobbyManager.CharacterType.SonTinh:
                    if (sonTinhPrefab == null)
                    {
                        Debug.LogError($"❌ Son Tinh Prefab chưa được assign trong PlayerSpawnManager! Vào Inspector và kéo Player 1.prefab vào field Son Tinh Prefab");
                        return null;
                    }
                    Debug.Log($"✅ Player {clientId} selected Sơn Tinh from lobby");
                    return sonTinhPrefab;
                
                case LobbyManager.CharacterType.ThuyTinh:
                    if (thuyTinhPrefab == null)
                    {
                        Debug.LogError($"❌ Thuy Tinh Prefab chưa được assign trong PlayerSpawnManager! Vào Inspector và kéo Player 2.prefab vào field Thuy Tinh Prefab");
                        return null;
                    }
                    Debug.Log($"✅ Player {clientId} selected Thủy Tinh from lobby");
                    return thuyTinhPrefab;
                
                default:
                    Debug.LogWarning($"⚠️ Player {clientId} has no character selection (None), using fallback");
                    break;
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ LobbyManager.Instance is null! Cannot get character selection for client {clientId}");
        }
        
        // Fallback: nếu không có lobby hoặc chưa chọn character
        // clientId 0 (Host) = Sơn Tinh, clientId 1 (Client) = Thủy Tinh
        GameObject fallbackPrefab = (clientId == 0) ? sonTinhPrefab : thuyTinhPrefab;
        
        if (fallbackPrefab == null)
        {
            string prefabName = (clientId == 0) ? "Son Tinh Prefab (Player 1)" : "Thuy Tinh Prefab (Player 2)";
            Debug.LogError($"❌ {prefabName} chưa được assign! Vào PlayerSpawnManager Inspector và assign prefabs!");
            return null;
        }
        
        string characterName = (clientId == 0) ? "Sơn Tinh" : "Thủy Tinh";
        Debug.Log($"⚠️ Using fallback character {characterName} for client {clientId}");
        return fallbackPrefab;
    }

    public void ClearSavedPosition(ulong clientId)
    {
        if (savedPositions.ContainsKey(clientId))
        {
            savedPositions.Remove(clientId);
            Debug.Log($"🗑️ Cleared saved position for player {clientId}");
        }
    }

    public NetworkPlayerController GetPlayer(ulong clientId)
    {
        if (spawnedPlayers.ContainsKey(clientId))
        {
            return spawnedPlayers[clientId];
        }
        return null;
    }

    public void TeleportPlayer(ulong clientId, Vector3 position)
    {
        if (!IsServer) return;

        NetworkPlayerController player = GetPlayer(clientId);

        // Fallback: nếu dict chưa có (player ở DontDestroyOnLoad), tìm qua ConnectedClients
        if (player == null)
        {
            Debug.LogWarning($"⚠️ [TeleportPlayer] GetPlayer({clientId}) = null, thử fallback qua ConnectedClients...");
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    player = client.PlayerObject.GetComponent<NetworkPlayerController>();
                    if (player != null)
                    {
                        spawnedPlayers[clientId] = player;
                        Debug.Log($"✅ [TeleportPlayer] Tìm thấy player {clientId} qua ConnectedClients fallback");
                    }
                }
            }
        }

        if (player == null)
        {
            Debug.LogError($"❌ [TeleportPlayer] Không tìm thấy NetworkPlayerController cho client {clientId}! Teleport thất bại.");
            return;
        }

        // ✅ SERVER phải move transform trước — nếu không NetworkTransform sẽ ghi đè position cũ lên client
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = position;
        if (cc != null) cc.enabled = true;

        // Dùng NetworkTransform.Teleport() để đồng bộ ngay tức thì sang tất cả client
        var networkTransform = player.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.Teleport(position, player.transform.rotation, player.transform.localScale);
            Debug.Log($"🌀 [Server] NetworkTransform.Teleport({clientId}) → {position}");
        }

        Debug.Log($"🌀 [Server] TeleportPlayer {clientId} → {position}");

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        TeleportPlayerClientRpc(clientId, position, clientRpcParams);
        Debug.Log($"🌀 [Server] Sent TeleportClientRpc to player {clientId} → {position}");
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong clientId, Vector3 position, ClientRpcParams rpcParams = default)
    {
        // Chỉ owner mới tự xử lý việc di chuyển Transform của nó
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        NetworkPlayerController player = GetPlayer(clientId);
        if (player == null) return;

        // ✅ Đưa player vào scene đang active trước khi set position
        //    Player có thể đang ở DontDestroyOnLoad → tọa độ không align với scene đích
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isLoaded && activeScene.IsValid() &&
            player.gameObject.scene.name != activeScene.name)
        {
            SceneManager.MoveGameObjectToScene(player.gameObject, activeScene);
            Debug.Log($"🏠 [Client {clientId}] PlayerSpawnManager: Moved player into scene '{activeScene.name}'");
        }

        player.SetPosition(position);
        Debug.Log($"✅ [Client {clientId}] TeleportPlayerClientRpc: position = {position}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RETURN PORTAL RELAY — ReturnPortal là MonoBehaviour (không thể gọi RPC trực tiếp)
    // PlayerSpawnManager đóng vai trò bridge vì nó đã được Spawn đúng chuẩn qua NGO
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Được gọi bởi ReturnPortal khi player bước vào cổng quay về Thủy Tinh.
    /// Server xử lý: notify MultiplayerGameManager + lấy saved position + gửi ClientRpc.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void HandleReturnPortalServerRpc(ulong clientId, string fromScene)
    {
        Debug.Log($"📨 [Server] ReturnPortal relay: player {clientId} về từ '{fromScene}'");

        if (MultiplayerGameManager.Instance != null)
        {
            MultiplayerGameManager.Instance.PlayerLeftSonTinhSceneServer(clientId);
        }
        else
        {
            Debug.LogWarning("⚠️ [Server] MultiplayerGameManager.Instance null, bỏ qua notify");
        }

        Vector3 returnPosition = GetSavedPosition(clientId);
        Debug.Log($"📍 [Server] Saved position của player {clientId}: {returnPosition}");

        HandleReturnPortalClientRpc(clientId, fromScene, returnPosition);
    }

    /// <summary>
    /// Gửi tới đúng client để bắt đầu coroutine load Thủy Tinh + teleport.
    /// </summary>
    [ClientRpc]
    private void HandleReturnPortalClientRpc(ulong targetClientId, string fromScene, Vector3 savedPosition)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        Debug.Log($"📍 [Client {targetClientId}] HandleReturnPortalClientRpc: fromScene='{fromScene}', savedPos={savedPosition}");

        // Tìm ReturnPortal trong scene và bắt đầu coroutine
        ReturnPortal[] portals = FindObjectsOfType<ReturnPortal>();
        if (portals.Length > 0)
        {
            portals[0].StartReturnFlow(fromScene, savedPosition);
            Debug.Log($"✅ [Client {targetClientId}] Tìm thấy ReturnPortal, gọi StartReturnFlow");
        }
        else
        {
            Debug.LogError($"❌ [Client {targetClientId}] Không tìm thấy ReturnPortal trong scene!");
        }
    }

    /// <summary>
    /// Được gọi bởi ReturnPortal coroutine sau khi load xong Thủy Tinh.
    /// Server teleport player về đúng vị trí + scene.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TeleportToSceneServerRpc(ulong clientId, Vector3 targetPosition, string targetSceneName)
    {
        Debug.Log($"📨 [Server] TeleportToSceneServerRpc: player {clientId} → {targetPosition} trong '{targetSceneName}'");

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Debug.LogError($"❌ [Server] Không tìm thấy client {clientId}");
            return;
        }

        if (client.PlayerObject == null)
        {
            Debug.LogError($"❌ [Server] PlayerObject của client {clientId} là null");
            return;
        }

        GameObject playerObj = client.PlayerObject.gameObject;

        // Remove parent nếu có
        if (playerObj.transform.parent != null)
        {
            playerObj.transform.SetParent(null, true);
            Debug.Log($"🔓 [Server] Removed parent từ player {clientId}");
        }

        // Move vào đúng scene
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (targetScene.isLoaded && targetScene.IsValid() && playerObj.scene.name != targetScene.name)
        {
            SceneManager.MoveGameObjectToScene(playerObj, targetScene);
            Debug.Log($"🏠 [Server] Moved player {clientId} vào scene '{targetSceneName}'");
        }

        // Set position
        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerObj.transform.position = targetPosition;
        if (cc != null) cc.enabled = true;

        // Sync qua NetworkTransform
        var networkTransform = playerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.Teleport(targetPosition, playerObj.transform.rotation, playerObj.transform.localScale);
            Debug.Log($"✅ [Server] NetworkTransform.Teleport({clientId}) → {targetPosition}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clear tất cả dữ liệu khi disconnect
    /// </summary>
    public void ClearAll()
    {
        Debug.Log("🧹 PlayerSpawnManager: Clearing all data...");
        spawnedPlayers.Clear();
        savedPositions.Clear();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
