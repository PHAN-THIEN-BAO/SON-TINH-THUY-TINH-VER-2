using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Quản lý lobby và character selection cho multiplayer game
/// Giống hệ thống Fireboy & Watergirl - cho phép player chọn nhân vật
/// </summary>
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxPlayers = 2;

    // Enum cho character types
    public enum CharacterType
    {
        None = 0,
        SonTinh = 1,   
        ThuyTinh = 2   
    }

    // Struct để lưu player data trong lobby
    [System.Serializable]
    public struct PlayerLobbyData : INetworkSerializable, IEquatable<PlayerLobbyData>
    {
        public ulong clientId;
        public CharacterType selectedCharacter;
        public bool isReady;
        public FixedString64Bytes playerName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref selectedCharacter);
            serializer.SerializeValue(ref isReady);
            serializer.SerializeValue(ref playerName);
        }

        public bool Equals(PlayerLobbyData other)
        {
            return clientId == other.clientId &&
                   selectedCharacter == other.selectedCharacter &&
                   isReady == other.isReady &&
                   playerName == other.playerName;
        }
    }

    // NetworkList để sync player data giữa tất cả clients
    private NetworkList<PlayerLobbyData> playersInLobby;

    // Events
    public event Action<PlayerLobbyData> OnPlayerJoinedLobby;
    public event Action<ulong> OnPlayerLeftLobby;
    public event Action<PlayerLobbyData> OnPlayerDataChanged;
    public event Action OnGameStarting;

    private void Awake()
    {
        playersInLobby = new NetworkList<PlayerLobbyData>();
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Set Instance sau khi network spawn - đảm bảo cả client và host dùng chung instance
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"⚠️ Multiple LobbyManager instances detected! Keeping the networked one.");
            if (Instance.gameObject != this.gameObject)
            {
                Destroy(Instance.gameObject);
            }
        }
        Instance = this;
        Debug.Log($"✅ LobbyManager Instance set (IsServer: {IsServer}, IsClient: {IsClient})");

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToLobby;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedFromLobby;
            
            // Add host vào lobby (host không trigger OnClientConnectedCallback cho chính nó)
            ulong hostClientId = NetworkManager.Singleton.LocalClientId;
            var hostData = new PlayerLobbyData
            {
                clientId = hostClientId,
                selectedCharacter = CharacterType.None,
                isReady = false,
                playerName = $"Player {hostClientId}"
            };
            playersInLobby.Add(hostData);
            Debug.Log($"🎮 Host (Client {hostClientId}) added to lobby");
            Debug.Log($"📋 Total players in lobby: {playersInLobby.Count}");
        }

        // Subscribe to lobby changes
        playersInLobby.OnListChanged += OnLobbyListChanged;

        Debug.Log($"✅ LobbyManager spawned (IsServer: {IsServer})");
        
        // Nếu là client, request sync lobby state từ server ngay sau khi spawn
        if (IsClient && !IsServer)
        {
            RequestLobbyStateSyncServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToLobby;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromLobby;
        }

        playersInLobby.OnListChanged -= OnLobbyListChanged;
    }

    private void OnClientConnectedToLobby(ulong clientId)
    {
        Debug.Log($"🎮 Client {clientId} connected to lobby");

        // Thêm player vào lobby với default data
        var playerData = new PlayerLobbyData
        {
            clientId = clientId,
            selectedCharacter = CharacterType.None,
            isReady = false,
            playerName = $"Player {clientId}"
        };

        playersInLobby.Add(playerData);
        Debug.Log($"📋 Total players in lobby: {playersInLobby.Count}");
    }

    private void OnClientDisconnectedFromLobby(ulong clientId)
    {
        Debug.Log($"⚠️ Client {clientId} disconnected from lobby");

        for (int i = 0; i < playersInLobby.Count; i++)
        {
            if (playersInLobby[i].clientId == clientId)
            {
                playersInLobby.RemoveAt(i);
                break;
            }
        }

        OnPlayerLeftLobby?.Invoke(clientId);
    }

    private void OnLobbyListChanged(NetworkListEvent<PlayerLobbyData> changeEvent)
    {
        string clientType = IsServer ? "Server" : "Client";
        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        
        Debug.Log($"📢 [{clientType} {myClientId}] OnLobbyListChanged triggered: Type={changeEvent.Type}");
        
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerLobbyData>.EventType.Add:
                OnPlayerJoinedLobby?.Invoke(changeEvent.Value);
                Debug.Log($"➕ [{clientType} {myClientId}] Player {changeEvent.Value.clientId} joined lobby");
                
                // Force refresh UI khi có player mới join
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ForceRefreshUI();
                }
                break;

            case NetworkListEvent<PlayerLobbyData>.EventType.Remove:
                Debug.Log($"➖ [{clientType} {myClientId}] Player removed from lobby");
                
                // Force refresh UI khi có player leave
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ForceRefreshUI();
                }
                break;

            case NetworkListEvent<PlayerLobbyData>.EventType.Value:
                Debug.Log($"🔄 [{clientType} {myClientId}] Player {changeEvent.Value.clientId} data updated: Character={changeEvent.Value.selectedCharacter}, Ready={changeEvent.Value.isReady}");
                OnPlayerDataChanged?.Invoke(changeEvent.Value);
                
                // Force refresh UI khi có data thay đổi
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ForceRefreshUI();
                }
                break;
        }
    }

    /// <summary>
    /// Client gọi để chọn character
    /// </summary>
    public void SelectCharacter(CharacterType character)
    {
        if (!IsClient) return;

        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        Debug.Log($"🎯 Requesting character selection: {character} for client {myClientId}");

        SelectCharacterServerRpc(myClientId, character);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SelectCharacterServerRpc(ulong clientId, CharacterType character)
    {
        Debug.Log($"📥 [Server] Received SelectCharacter request from Client {clientId}: {character}");
        
        // Kiểm tra xem character đã được chọn bởi người khác chưa
        if (character != CharacterType.None && IsCharacterTaken(character, clientId))
        {
            Debug.LogWarning($"⚠️ Character {character} is already taken!");
            NotifyCharacterTakenClientRpc(clientId, character);
            return;
        }

        // Update player data
        for (int i = 0; i < playersInLobby.Count; i++)
        {
            if (playersInLobby[i].clientId == clientId)
            {
                var data = playersInLobby[i];
                var oldCharacter = data.selectedCharacter;
                data.selectedCharacter = character;
                
                // Nếu cancel character (None), reset ready status
                if (character == CharacterType.None)
                {
                    data.isReady = false;
                    Debug.Log($"❌ [Server] Client {clientId} canceled character selection, reset ready status");
                }
                
                playersInLobby[i] = data;
                Debug.Log($"✅ [Server] Client {clientId} character updated: {oldCharacter} -> {character}");
                Debug.Log($"📋 [Server] NetworkList[{i}] updated, this will trigger OnLobbyListChanged on ALL clients");
                
                // Log toàn bộ lobby state để debug
                LogLobbyState();
                
                // GỬI CLIENTRPC ĐỂ CONFIRM NGAY LẬP TỨC - đảm bảo UI update nhanh
                ConfirmCharacterSelectionClientRpc(clientId, character, data.isReady);
                break;
            }
        }
    }
    
    /// <summary>
    /// ClientRpc để confirm character selection ngay lập tức cho TẤT CẢ clients
    /// Đảm bảo đồng bộ nhanh mà không cần chờ NetworkList sync
    /// </summary>
    [ClientRpc]
    private void ConfirmCharacterSelectionClientRpc(ulong clientId, CharacterType character, bool isReady)
    {
        Debug.Log($"✅ [Client {NetworkManager.Singleton.LocalClientId}] Received confirmation: Client {clientId} selected {character}, Ready={isReady}");
        
        // Trigger UI update ngay lập tức
        if (LobbyUIManager.Instance != null)
        {
            // Force refresh UI ngay lập tức
            LobbyUIManager.Instance.ForceRefreshUI();
        }
    }
    
    /// <summary>
    /// Debug helper - log toàn bộ lobby state
    /// </summary>
    private void LogLobbyState()
    {
        Debug.Log($"📊 [Server] Current Lobby State ({playersInLobby.Count} players):");
        for (int i = 0; i < playersInLobby.Count; i++)
        {
            var p = playersInLobby[i];
            Debug.Log($"   Player {i}: ClientID={p.clientId}, Character={p.selectedCharacter}, Ready={p.isReady}");
        }
    }

    [ClientRpc]
    private void NotifyCharacterTakenClientRpc(ulong clientId, CharacterType character)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.LogWarning($"❌ Character {character} đã có người sẵn sàng với nhân vật này rồi! Chọn nhân vật khác.");
            
            // Update UI status nếu có
            if (LobbyUIManager.Instance != null)
            {
                string charName = character == CharacterType.SonTinh ? "Sơn Tinh" : "Thủy Tinh";
                LobbyUIManager.Instance.ShowStatus($"❌ {charName} đã có người sẵn sàng rồi!");
            }
        }
    }

    /// <summary>
    /// Toggle ready status
    /// </summary>
    public void ToggleReady()
    {
        if (!IsClient) return;

        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        ToggleReadyServerRpc(myClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ulong clientId)
    {
        Debug.Log($"📥 [Server] Received ToggleReady request from Client {clientId}");
        
        for (int i = 0; i < playersInLobby.Count; i++)
        {
            if (playersInLobby[i].clientId == clientId)
            {
                var data = playersInLobby[i];
                
                // Chỉ cho phép ready nếu đã chọn character
                if (data.selectedCharacter == CharacterType.None)
                {
                    Debug.LogWarning($"⚠️ [Server] Client {clientId} cannot ready without selecting a character");
                    return;
                }

                // Nếu đang toggle từ false → true (ready), check xem character có bị chiếm chưa
                if (!data.isReady && IsCharacterTaken(data.selectedCharacter, clientId))
                {
                    Debug.LogWarning($"⚠️ [Server] Character {data.selectedCharacter} is already taken by another ready player!");
                    NotifyCharacterTakenClientRpc(clientId, data.selectedCharacter);
                    return;
                }

                bool oldReadyState = data.isReady;
                data.isReady = !data.isReady;
                playersInLobby[i] = data;
                Debug.Log($"✅ [Server] Client {clientId} ready status: {oldReadyState} -> {data.isReady}");
                
                // Log lobby state
                LogLobbyState();
                
                // GỬI CLIENTRPC ĐỂ CONFIRM NGAY - đảm bảo UI update đồng bộ
                ConfirmReadyStatusClientRpc(clientId, data.isReady);
                break;
            }
        }

        // Kiểm tra xem có thể start game không
        CheckIfCanStartGame();
    }
    
    /// <summary>
    /// ClientRpc để confirm ready status ngay lập tức
    /// </summary>
    [ClientRpc]
    private void ConfirmReadyStatusClientRpc(ulong clientId, bool isReady)
    {
        Debug.Log($"✅ [Client {NetworkManager.Singleton.LocalClientId}] Received confirmation: Client {clientId} ready status = {isReady}");
        
        // Trigger UI update ngay lập tức
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.ForceRefreshUI();
        }
    }

    private void CheckIfCanStartGame()
    {
        if (!IsServer) return;

        // Cần đủ 2 players và tất cả đều ready
        if (playersInLobby.Count < 2)
        {
            Debug.Log($"⚠️ Not enough players: {playersInLobby.Count}/2");
            return;
        }

        foreach (var player in playersInLobby)
        {
            if (!player.isReady || player.selectedCharacter == CharacterType.None)
            {
                Debug.Log($"⚠️ Player {player.clientId} not ready or no character selected");
                return;
            }
        }

        // Tất cả đều ready - start game!
        Debug.Log("🎮 All players ready! Starting game...");
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        OnGameStarting?.Invoke();
        Debug.Log("🚀 Game starting!");
    }

    /// <summary>
    /// Get character selection của một client
    /// </summary>
    public CharacterType GetPlayerCharacter(ulong clientId)
    {
        foreach (var player in playersInLobby)
        {
            if (player.clientId == clientId)
            {
                return player.selectedCharacter;
            }
        }
        return CharacterType.None;
    }

    /// <summary>
    /// Kiểm tra xem character đã có người chọn VÀ READY chưa
    /// Chỉ lock character khi player đã ready
    /// </summary>
    private bool IsCharacterTaken(CharacterType character, ulong excludeClientId)
    {
        if (character == CharacterType.None) return false;

        foreach (var player in playersInLobby)
        {
            // Character chỉ bị lock nếu người chọn đã READY
            if (player.clientId != excludeClientId && 
                player.selectedCharacter == character && 
                player.isReady)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get all players in lobby
    /// </summary>
    public List<PlayerLobbyData> GetPlayersInLobby()
    {
        List<PlayerLobbyData> players = new List<PlayerLobbyData>();
        foreach (var player in playersInLobby)
        {
            players.Add(player);
        }
        return players;
    }

    /// <summary>
    /// Kiểm tra xem có thể start game không
    /// </summary>
    public bool CanStartGame()
    {
        if (playersInLobby.Count < 2) return false;

        foreach (var player in playersInLobby)
        {
            if (!player.isReady || player.selectedCharacter == CharacterType.None)
            {
                return false;
            }
        }
        return true;
    }
    
    /// <summary>
    /// Host gọi để bắt đầu game ngay (khi cả 2 đã chọn nhân vật)
    /// </summary>
    public void StartGame()
    {
        if (!IsClient) return;
        StartGameServerRpc();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc(ServerRpcParams rpcParams = default)
    {
        // Chỉ Host mới được phép bắt đầu
        if (rpcParams.Receive.SenderClientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogWarning($"⚠️ [Server] Non-host client tried to start game!");
            return;
        }
        
        // Kiểm tra cả 2 player đã chọn nhân vật
        if (playersInLobby.Count < 2)
        {
            Debug.LogWarning("⚠️ [Server] Cannot start: not enough players");
            return;
        }
        
        foreach (var player in playersInLobby)
        {
            if (player.selectedCharacter == CharacterType.None)
            {
                Debug.LogWarning($"⚠️ [Server] Cannot start: player {player.clientId} has not selected a character");
                return;
            }
        }
        
        Debug.Log("🚀 [Server] Host force-starting game!");
        StartGameClientRpc();
    }
    
    /// <summary>
    /// Client request sync lobby state từ server khi mới join
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestLobbyStateSyncServerRpc(ulong clientId)
    {
        Debug.Log($"📥 [Server] Client {clientId} requested lobby state sync");
        
        // Gửi toàn bộ lobby state cho client
        SyncLobbyStateClientRpc(clientId);
    }
    
    /// <summary>
    /// Server gửi lobby state cho client cụ thể
    /// </summary>
    [ClientRpc]
    private void SyncLobbyStateClientRpc(ulong targetClientId)
    {
        // Chỉ client được chỉ định mới xử lý
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        
        Debug.Log($"✅ [Client {targetClientId}] Received lobby state sync from server");
        
        // Force refresh UI để đồng bộ
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.ForceRefreshUI();
        }
    }
}
