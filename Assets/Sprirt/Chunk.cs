using UnityEngine;

public enum ChunkType
{
    Straight,
    TurnLeft,
    TurnRight,
    Up,
    Down
}

public class Chunk : MonoBehaviour
{
    public ChunkType type;

    public Transform forward; // socket đi thẳng
    public Transform left;    // socket rẽ trái
    public Transform right;   // socket rẽ phải
    public Transform up;      // socket đi 
    public Transform down;    // socket đi xuống
    [HideInInspector]

    public Transform activeExit;
}
