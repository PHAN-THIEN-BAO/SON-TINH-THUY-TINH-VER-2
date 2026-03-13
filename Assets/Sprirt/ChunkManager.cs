using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
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
    public int startChunk = 10;
    public int minStraightBeforeTurn = 10;
    public int minStraightAfterTurn = 10;
    public float spawnDistance = 40f;
    public int maxChunk = 300;

    [Header("Cleanup")]
    public int maxAliveChunks = 30;

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
    private bool nextIsUp = true; // true = spawn up, false = spawn down
    private bool needUpDown = false;

    private Queue<GameObject> aliveChunks = new();

    void Start()
    {
        for (int i = 0; i < startChunk; i++)
        {
            SpawnChunk();
        }
    }

    void Update()
{
    if (player1 == null) return;

    // ✅ Cho phép spawn win dù đã tới maxChunk
    if (!spawnedWin && spawnedCount >= winAtChunk)
    {
        SpawnChunk();
        return;
    }

    if (spawnedCount >= maxChunk) return;

    float nearest = Vector3.Distance(player1.position, nextSpawnPos);

    if (player2 != null)
    {
        float p2 = Vector3.Distance(player2.position, nextSpawnPos);
        nearest = Mathf.Min(nearest, p2);
    }

    if (nearest < spawnDistance)
    {
        SpawnChunk();
    }

    CleanupChunks();
}


    void SpawnChunk()
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
            nextIsUp = !nextIsUp; // Đổi qua up/down tiếp theo
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
    void CleanupChunks()
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
            Destroy(aliveChunks.Dequeue());
        }
        else
        {
            break;
        }
    }
}

    void RequireSocket(Transform socket, string name, GameObject chunk)
    {
        if (socket == null)
        {
            Debug.LogError($" {chunk.name} thiếu socket {name}");
            enabled = false;
        }
    }

    void SpawnWinChunk()
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

    Debug.Log("🎉 WIN CHUNK SPAWNED!");
}

}
