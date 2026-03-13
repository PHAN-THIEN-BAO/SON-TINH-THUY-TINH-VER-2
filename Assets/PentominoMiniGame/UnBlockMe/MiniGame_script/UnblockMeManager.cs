using UnityEngine;
using System.Collections.Generic;

public class UnblockMeManager : MonoBehaviour
{
    [System.Serializable]
    public class BlockData
    {
        public bool isMain;
        public bool isHorizontal;
        public int length;
        public int x;
        public int y;
    }

    [Header("UI References")]
    [Tooltip("A 600x600 empty rect transform serving as the playboard")]
    public RectTransform boardRect;
    
    [Tooltip("A UI Image Prefab with BlockDragHandler attached")]
    public GameObject blockPrefab;

    [Tooltip("A UI Panel to show when the player wins")]
    public GameObject winPanel;

    [Tooltip("Kéo Nút Tiếp Tục (Next Level) trong WinPanel vào đây")]
    public GameObject nextButton;

    [Tooltip("Kéo Nút Thoát (Exit) trong WinPanel vào đây")]
    public GameObject exitButton;

    [Tooltip("Kéo thả cái Canvas bọc ngoài Mini Game vào đây để nút Thoát hoạt động")]
    public GameObject tatCaGame;

    [Header("Game Data")]
    public int currentLevelIndex = 0;
    public List<BlockData> levelData;

    // A list of predefined levels
    private List<List<BlockData>> allLevels;

    private BlockDragHandler[,] grid = new BlockDragHandler[6, 6];
    private float cellSize;

    private void Start()
    {
        InitializeLevels();
        LoadLevel(currentLevelIndex);
    }

    private void InitializeLevels()
    {
        allLevels = new List<List<BlockData>>();

        // Level 1: The original beginner level
        allLevels.Add(new List<BlockData>
        {
            new BlockData { isHorizontal = false, length = 3, x = 0, y = 0 },
            new BlockData { isHorizontal = true, length = 2, x = 1, y = 0 },
            new BlockData { isHorizontal = true, length = 2, x = 1, y = 1 },
            new BlockData { isHorizontal = false, length = 2, x = 3, y = 0 },
            new BlockData { isHorizontal = false, length = 3, x = 4, y = 0 },
            new BlockData { isMain = true, isHorizontal = true, length = 2, x = 1, y = 2 }, 
            new BlockData { isHorizontal = false, length = 2, x = 0, y = 3 },
            new BlockData { isHorizontal = true, length = 2, x = 1, y = 3 },
            new BlockData { isHorizontal = false, length = 2, x = 3, y = 2 },
            new BlockData { isHorizontal = true, length = 2, x = 1, y = 4 },
            new BlockData { isHorizontal = false, length = 2, x = 5, y = 3 },
            new BlockData { isHorizontal = true, length = 3, x = 2, y = 5 }
        });

        // Level 2: Medium (Requires 5 logical sliding moves)
        allLevels.Add(new List<BlockData>
        {
            new BlockData { isMain = true, isHorizontal = true, length = 2, x = 0, y = 2 }, 
            new BlockData { isHorizontal = false, length = 2, x = 0, y = 0 },
            new BlockData { isHorizontal = true, length = 2, x = 4, y = 0 },
            new BlockData { isHorizontal = false, length = 2, x = 4, y = 2 },
            new BlockData { isHorizontal = false, length = 3, x = 3, y = 1 },
            new BlockData { isHorizontal = false, length = 2, x = 2, y = 2 },
            new BlockData { isHorizontal = true, length = 3, x = 0, y = 5 },
            new BlockData { isHorizontal = true, length = 2, x = 0, y = 4 },
            new BlockData { isHorizontal = false, length = 2, x = 5, y = 4 }
        });

        // Level 3: Hard (Requires a 9-move chain reaction)
        allLevels.Add(new List<BlockData>
        {
            new BlockData { isMain = true, isHorizontal = true, length = 2, x = 0, y = 2 },
            new BlockData { isHorizontal = false, length = 2, x = 0, y = 0 },
            new BlockData { isHorizontal = true, length = 3, x = 1, y = 0 },
            new BlockData { isHorizontal = false, length = 2, x = 3, y = 1 },
            new BlockData { isHorizontal = false, length = 3, x = 1, y = 3 },
            new BlockData { isHorizontal = true, length = 3, x = 2, y = 5 },
            new BlockData { isHorizontal = false, length = 3, x = 4, y = 2 },
            new BlockData { isHorizontal = false, length = 2, x = 5, y = 4 },
            new BlockData { isHorizontal = true, length = 2, x = 2, y = 4 },
            new BlockData { isHorizontal = false, length = 2, x = 5, y = 0 }
        });

        // Level 4: Expert (Verified Solvable 11-step Masterpiece)
        allLevels.Add(new List<BlockData>
        {
            new BlockData { isMain = true, isHorizontal = true, length = 2, x = 0, y = 2 },
            new BlockData { isHorizontal = true, length = 2, x = 1, y = 0 },
            new BlockData { isHorizontal = true, length = 2, x = 4, y = 0 },
            new BlockData { isHorizontal = false, length = 2, x = 2, y = 1 },
            new BlockData { isHorizontal = false, length = 3, x = 4, y = 1 },
            new BlockData { isHorizontal = false, length = 2, x = 5, y = 1 },
            new BlockData { isHorizontal = false, length = 2, x = 0, y = 3 },
            new BlockData { isHorizontal = true, length = 2, x = 1, y = 3 },
            new BlockData { isHorizontal = false, length = 3, x = 3, y = 3 },
            new BlockData { isHorizontal = true, length = 2, x = 4, y = 4 },
            new BlockData { isHorizontal = true, length = 3, x = 0, y = 5 }
        });
    }

    public void LoadLevel(int index)
    {
        if (index >= 0 && index < allLevels.Count)
        {
            currentLevelIndex = index;
            levelData = allLevels[index];
            
            CloneCurrentLevelData();
        }

        if (winPanel != null) winPanel.SetActive(false);
        cellSize = boardRect.rect.width / 6f;

        // Clear existing blocks
        foreach (Transform child in boardRect)
        {
            Destroy(child.gameObject);
        }
        grid = new BlockDragHandler[6, 6];

        GenerateBoard();
    }

    private void CloneCurrentLevelData()
    {
        List<BlockData> original = allLevels[currentLevelIndex];
        levelData = new List<BlockData>();
        foreach (var data in original)
        {
            levelData.Add(new BlockData
            {
                isMain = data.isMain,
                isHorizontal = data.isHorizontal,
                length = data.length,
                x = data.x,
                y = data.y
            });
        }
    }

    private void GenerateBoard()
    {
        foreach (var data in levelData)
        {
            GameObject go = Instantiate(blockPrefab, boardRect);
            BlockDragHandler handler = go.GetComponent<BlockDragHandler>();
            
            if (handler == null) handler = go.AddComponent<BlockDragHandler>();

            handler.Setup(this, data, cellSize);
            UpdateGrid(handler, -1, -1);
        }
    }

    public void UpdateGrid(BlockDragHandler block, int oldX, int oldY)
    {
        if (oldX != -1 && oldY != -1 && oldX < 6 && oldY < 6)
        {
            for (int i = 0; i < block.data.length; i++)
            {
                if (block.data.isHorizontal && oldX + i < 6) grid[oldX + i, oldY] = null;
                else if (!block.data.isHorizontal && oldY + i < 6) grid[oldX, oldY + i] = null;
            }
        }

        if (block.data.x < 6 && block.data.y < 6)
        {
            for (int i = 0; i < block.data.length; i++)
            {
                if (block.data.isHorizontal && block.data.x + i < 6) grid[block.data.x + i, block.data.y] = block;
                else if (!block.data.isHorizontal && block.data.y + i < 6) grid[block.data.x, block.data.y + i] = block;
            }
        }

        CheckWinCondition();
    }

    public void GetLimits(BlockDragHandler block, out float minLocalPos, out float maxLocalPos)
    {
        int x = block.data.x;
        int y = block.data.y;
        int len = block.data.length;

        int minSteps = 0;
        int maxSteps = 0;

        if (block.data.isHorizontal)
        {
            for (int i = x - 1; i >= 0; i--) { if (grid[i, y] == null) minSteps++; else break; }
            for (int i = x + len; i < 6; i++) { if (grid[i, y] == null) maxSteps++; else break; }
            
            if (block.data.isMain && (x + len + maxSteps == 6))
            {
                maxSteps += 3;
            }

            minLocalPos = (x - minSteps) * cellSize;
            maxLocalPos = (x + maxSteps) * cellSize;
        }
        else
        {
            for (int j = y - 1; j >= 0; j--) { if (grid[x, j] == null) minSteps++; else break; }
            for (int j = y + len; j < 6; j++) { if (grid[x, j] == null) maxSteps++; else break; }
            
            minLocalPos = -(y - minSteps) * cellSize;
            maxLocalPos = -(y + maxSteps) * cellSize;
        }
    }

    private void CheckWinCondition()
    {
        foreach (var block in levelData)
        {
            if (block.isMain && block.x >= 5)
            {
                LevelComplete();
            }
        }
    }

    public void LevelComplete()
    {
        Debug.Log("LEVEL CLEARED!");
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            winPanel.transform.SetAsLastSibling();
            
            // Xử lý ẩn/hiện Nút Thoát và Nút Tiếp Tục
            bool isLastLevel = (currentLevelIndex == allLevels.Count - 1);
            if (nextButton != null) nextButton.SetActive(!isLastLevel); // Chỉ hiện khi chưa phải màn cuối
            if (exitButton != null) exitButton.SetActive(isLastLevel);  // Chỉ hiện màn cuối
        }
    }

    public void QuitGameButton()
    {
        if (tatCaGame != null)
        {
            tatCaGame.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
            if (boardRect != null) boardRect.gameObject.SetActive(false);
        }
    }

    public void NextLevel()
    {
        int nextIndex = currentLevelIndex + 1;
        if (nextIndex >= allLevels.Count)
        {
            // Nếu lỡ bấm Next ở màn cuối thì Quit luôn
            QuitGameButton();
            return;
        }
        LoadLevel(nextIndex);
    }

    public void RestartLevel()
    {
        Debug.Log("Restarting Level...");
        LoadLevel(currentLevelIndex);
    }
}
