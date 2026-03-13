using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkChunkManager : NetworkBehaviour
{
    [Header("Players")]
    public Transform player1;
    public Transform player2; 

    [Header("Chunk Root")]
    public Transform chunkRoot;

    [Header("Prefabs")]
    public List<GameObject> straightChunks;
    public List<GameObject> turnLeftChunks;
    public List<GameObject> turnRightChunks;
    public List<GameObject> upChunks;
    public List<GameObject> downChunks; 
    
    [Header("Spawn Settings")]
    public int startChunk = 20; // Spawn nhiều chunks ban đầu cho gameplay mượt
    public int minStraightBeforeTurn = 10;
    public int minStraightAfterTurn = 10;
    public float spawnDistance = 100f; // Spawn xa để luôn có đường phía trước (legacy, không dùng nữa)
    public int minChunksAhead = 10; // Spawn khi còn 10 chunks phía trước player
    public int maxChunk = 300;

    [Header("Cleanup")]
    public int maxAliveChunks = 30;
    public bool enableCleanup = true; // Kiểm soát cleanup

    [Header("Win")]
    public GameObject winChunkPrefab;
    public int winAtChunk = 100;
    private bool spawnedWin = false;

    [Header("Cleanup By Distance")]
    public float despawnDistance = 60f;

    private Vector3 nextSpawnPos = Vector3.zero;
    private Quaternion nextSpawnRot = Quaternion.identity;

    private int spawnedCount = 0;
    private int straightCount = 0;
    private int straightAfterTurn = 0;
    private bool lockTurn = false;

    private int chunksSinceUpDown = 0;
    private bool nextIsUp = true;
    private bool needUpDown = false;

    private Queue<GameObject> aliveChunks = new Queue<GameObject>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"🌍 NetworkChunkManager spawned on {(IsServer ? "Server" : "Client")}");

        // Chỉ server spawn chunks
        if (IsServer)
        {
            Debug.Log($"🎮 Spawning {startChunk} initial chunks...");
            for (int i = 1; i < startChunk; i++)
            {
                SpawnChunk();
            }
            Debug.Log($"✅ Spawned {startChunk} chunks. Next spawn at: {nextSpawnPos}");
            
            // Tìm players sau 1 giây (đợi players spawn xong)
            InvokeRepeating(nameof(TryFindPlayers), 1f, 1f);
        }
    }
    
    private void TryFindPlayers()
    {
        // Luôn tìm players, không dựa vào manual assignment
        FindNetworkPlayers();
        
        // Dừng tìm sau khi tìm thấy ít nhất 1 player
        if (player1 != null)
        {
            CancelInvoke(nameof(TryFindPlayers));
            Debug.Log("✅ At least 1 player found. Stopped searching. Chunk spawning active!");
        }
    }

    private void Update()
    {
        if (!IsServer) return; // Chỉ server xử lý logic

        if (player1 == null)
        {
            // Không spam logs, chỉ log mỗi giây
            return;
        }

        // Kiểm tra điều kiện cleanup từ MultiplayerGameManager
        UpdateCleanupState();

        // Spawn win chunk
        if (!spawnedWin && spawnedCount >= winAtChunk)
        {
            SpawnChunk();
            return;
        }

        if (spawnedCount >= maxChunk) return;

        // Đếm số chunks còn phía trước player
        int chunksAhead = CountChunksAheadOfPlayer();

        // Debug log định kỳ (mỗi 3 giây) 
        if (Time.frameCount % 180 == 0)
        {
            Debug.Log($"📊 ChunkManager active - Player1 at {player1.position}, Chunks ahead: {chunksAhead}/{minChunksAhead}");
        }

        // Debug log khi chunks sắp hết (mỗi 2 giây)
        if (chunksAhead <= minChunksAhead + 5 && Time.frameCount % 120 == 0)
        {
            Debug.Log($"📏 Chunks running low! {chunksAhead} chunks ahead, threshold: {minChunksAhead}");
        }

        // Spawn khi còn ít hơn minChunksAhead chunks phía trước
        if (chunksAhead < minChunksAhead)
        {
            Debug.Log($"🎯 Only {chunksAhead} chunks ahead (< {minChunksAhead}). Spawning chunk {spawnedCount + 1}/{maxChunk}");
            SpawnChunk();
        }

        // Cleanup chỉ khi điều kiện thỏa mãn
        if (enableCleanup)
        {
            CleanupChunks();
        }
    }

    private void FindNetworkPlayers()
    {
        // ✅ Tìm trong TẤT CẢ scenes bao gồm DontDestroyOnLoad
        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>(true);
        
        Debug.Log($"🔍 Finding players in all scenes... Found {players.Length} NetworkPlayerController(s)");
        
        if (players.Length > 0)
        {
            player1 = players[0].transform;
            string sceneName = player1.gameObject.scene.IsValid() ? player1.gameObject.scene.name : "DontDestroyOnLoad";
            Debug.Log($"✅ Player 1 assigned: {player1.name} at {player1.position} (Scene: {sceneName})");
            Debug.Log($"⚙️ Spawn settings - Distance: {spawnDistance}, Next spawn at: {nextSpawnPos}");
            
            if (players.Length > 1)
            {
                player2 = players[1].transform;
                string scene2 = player2.gameObject.scene.IsValid() ? player2.gameObject.scene.name : "DontDestroyOnLoad";
                Debug.Log($"✅ Player 2 assigned: {player2.name} at {player2.position} (Scene: {scene2})");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ No NetworkPlayerController found in any scene!");
        }
    }

    private int CountChunksAheadOfPlayer()
    {
        if (player1 == null) return 0;

        // Lấy vị trí player (dùng player gần nhất nếu có 2 players)
        float playerZ = player1.position.z;
        if (player2 != null)
        {
            playerZ = Mathf.Max(player1.position.z, player2.position.z); // Player đi trước nhất
        }

        // Đếm chunks có Z position lớn hơn player (phía trước)
        int count = 0;
        foreach (GameObject chunk in aliveChunks)
        {
            if (chunk != null && chunk.transform.position.z > playerZ)
            {
                count++;
            }
        }

        return count;
    }

    private void UpdateCleanupState()
    {
        if (MultiplayerGameManager.Instance != null)
        {
            // Cả 2 player ở Thủy Tinh: cleanup active
            // Chỉ 1 player: cleanup off, spawn vẫn hoạt động
            enableCleanup = MultiplayerGameManager.Instance.ShouldCleanupChunks();
        }
    }

    private void SpawnChunk()
    {
        GameObject prefab;

        if (!spawnedWin && spawnedCount >= winAtChunk)
        {
            SpawnWinChunk();
            spawnedWin = true;
            return;
        }

        chunksSinceUpDown++;
        if (chunksSinceUpDown >= 10)
        {
            needUpDown = true;
        }

        // Spawn up hoặc down
        if (needUpDown)
        {
            if (nextIsUp)
            {
                prefab = upChunks[Random.Range(0, upChunks.Count)];
            }
            else
            {
                prefab = downChunks[Random.Range(0, downChunks.Count)];
            }
            
            needUpDown = false;
            chunksSinceUpDown = 0;
            nextIsUp = !nextIsUp;
        }
        else if (lockTurn)
        {
            prefab = straightChunks[Random.Range(0, straightChunks.Count)];
            straightAfterTurn++;

            if (straightAfterTurn >= minStraightAfterTurn)
            {
                lockTurn = false;
                straightAfterTurn = 0;
                straightCount = minStraightBeforeTurn;
            }
        }
        else
        {
            bool canTurn = straightCount >= minStraightBeforeTurn;
            int roll = Random.Range(0, 100);

            if (canTurn && roll < 25)
            {
                prefab = Random.value < 0.5f
                    ? turnLeftChunks[Random.Range(0, turnLeftChunks.Count)]
                    : turnRightChunks[Random.Range(0, turnRightChunks.Count)];

                lockTurn = true;
                straightCount = 0;
            }
            else
            {
                prefab = straightChunks[Random.Range(0, straightChunks.Count)];
                straightCount++;
            }
        }

        GameObject chunk = Instantiate(prefab, nextSpawnPos, nextSpawnRot, chunkRoot);
        
        // Spawn chunk trên network
        NetworkObject networkObject = chunk.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            Debug.Log($"✅ Chunk spawned successfully: {chunk.name} at {nextSpawnPos} (Count: {spawnedCount + 1})");
            
            // Spawn tất cả NetworkObject con (portal, etc.)
            NetworkObject[] childNetworkObjects = chunk.GetComponentsInChildren<NetworkObject>(true);
            foreach (NetworkObject childNetObj in childNetworkObjects)
            {
                // Bỏ qua chính chunk NetworkObject
                if (childNetObj != networkObject && !childNetObj.IsSpawned)
                {
                    childNetObj.Spawn();
                    Debug.Log($"    ↳ Child NetworkObject spawned: {childNetObj.gameObject.name}");
                }
            }
        }
        else
        {
            Debug.LogError($"❌ Chunk prefab missing NetworkObject component: {chunk.name}");
        }

        spawnedCount++;
        aliveChunks.Enqueue(chunk);

        Chunk data = chunk.GetComponent<Chunk>();
        if (data == null)
        {
            Debug.LogError("Chunk prefab thiếu script Chunk");
            enabled = false;
            return;
        }

        switch (data.type)
        {
            case ChunkType.Straight:
                RequireSocket(data.forward, "Forward", chunk);
                nextSpawnPos = data.forward.position;
                nextSpawnRot = data.forward.rotation;
                break;

            case ChunkType.TurnLeft:
                RequireSocket(data.left, "Left", chunk);
                nextSpawnPos = data.left.position;
                nextSpawnRot = data.left.rotation;
                break;

            case ChunkType.TurnRight:
                RequireSocket(data.right, "Right", chunk);
                nextSpawnPos = data.right.position;
                nextSpawnRot = data.right.rotation;
                break;

            case ChunkType.Up:
                RequireSocket(data.up, "Up", chunk);
                nextSpawnPos = data.up.position;
                nextSpawnRot = data.up.rotation;
                break;

            case ChunkType.Down:
                RequireSocket(data.down, "Down", chunk);
                nextSpawnPos = data.down.position;
                nextSpawnRot = data.down.rotation;
                break;
        }
    }

    private void CleanupChunks()
    {
        if (aliveChunks.Count == 0) return;

        Vector3 p1 = player1.position;
        Vector3 p2 = player2 != null ? player2.position : p1;

        while (aliveChunks.Count > 0)
        {
            GameObject chunk = aliveChunks.Peek();
            if (chunk == null)
            {
                aliveChunks.Dequeue();
                continue;
            }

            float d1 = Vector3.Distance(p1, chunk.transform.position);
            float d2 = Vector3.Distance(p2, chunk.transform.position);

            if (d1 > despawnDistance && d2 > despawnDistance)
            {
                GameObject toDestroy = aliveChunks.Dequeue();
                
                // Despawn tất cả child NetworkObject trước
                NetworkObject[] childNetworkObjects = toDestroy.GetComponentsInChildren<NetworkObject>(true);
                foreach (NetworkObject childNetObj in childNetworkObjects)
                {
                    if (childNetObj.IsSpawned && childNetObj.gameObject != toDestroy)
                    {
                        childNetObj.Despawn();
                    }
                }
                
                // Despawn chunk chính từ network
                NetworkObject networkObject = toDestroy.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }
                
                Destroy(toDestroy);
            }
            else
            {
                break;
            }
        }
    }

    private void RequireSocket(Transform socket, string name, GameObject chunk)
    {
        if (socket == null)
        {
            Debug.LogError($"{chunk.name} thiếu socket {name}");
            enabled = false;
        }
    }

    private void SpawnWinChunk()
    {
        if (winChunkPrefab == null)
        {
            Debug.LogError("Chưa gán Win Chunk Prefab");
            return;
        }

        GameObject winChunk = Instantiate(
            winChunkPrefab,
            nextSpawnPos,
            nextSpawnRot,
            chunkRoot
        );

        NetworkObject networkObject = winChunk.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            
            // Spawn tất cả NetworkObject con (portal, etc.)
            NetworkObject[] childNetworkObjects = winChunk.GetComponentsInChildren<NetworkObject>(true);
            foreach (NetworkObject childNetObj in childNetworkObjects)
            {
                // Bỏ qua chính chunk NetworkObject
                if (childNetObj != networkObject && !childNetObj.IsSpawned)
                {
                    childNetObj.Spawn();
                    Debug.Log($"    ↳ Win chunk child NetworkObject spawned: {childNetObj.gameObject.name}");
                }
            }
        }

        Debug.Log("🎉 WIN CHUNK SPAWNED!");
    }

    // Public methods để control từ bên ngoài
    public void SetCleanupEnabled(bool enabled)
    {
        enableCleanup = enabled;
        Debug.Log($"🧹 Chunk cleanup: {(enabled ? "ENABLED" : "DISABLED")}");
    }

    public bool IsCleanupEnabled() => enableCleanup;

    /// <summary>
    /// Clear tất cả chunks và reset state khi disconnect
    /// </summary>
    public void ClearAll()
    {
        Debug.Log("🧹 NetworkChunkManager: Clearing all chunks...");
        
        // Despawn và destroy tất cả chunks
        while (aliveChunks.Count > 0)
        {
            GameObject chunk = aliveChunks.Dequeue();
            if (chunk != null)
            {
                NetworkObject netObj = chunk.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                Destroy(chunk);
            }
        }
        
        // Reset counters
        spawnedCount = 0;
        straightCount = 0;
        straightAfterTurn = 0;
        lockTurn = false;
        chunksSinceUpDown = 0;
        nextIsUp = true;
        needUpDown = false;
        spawnedWin = false;
        
        // Reset spawn position
        nextSpawnPos = Vector3.zero;
        nextSpawnRot = Quaternion.identity;
        
        Debug.Log("✅ NetworkChunkManager cleared");
    }
}
