using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace MiniGame
{
    public class PuzzleBoard : MonoBehaviour
    {
        public const int Width = 5;
        public const int Height = 5;
        
        // 0 = empty, 1 = filled
        private int[,] grid = new int[Width, Height]; 

        [Header("Win UI")]
        [SerializeField] private GameObject winPanel;      // Panel chứa hình + text
        [SerializeField] private Image rewardImage;             // Ảnh phần thưởng
        [SerializeField] private TextMeshProUGUI winMessageText; // Dòng chữ chúc mừng (TextMeshPro)
        [SerializeField] private Sprite rewardSprite;      // Sprite của "vật gì đó"

        [Header("Win VFX")]
        [Tooltip("Prefab VFX spawn khi hoàn thành puzzle (ví dụ: WaterSpell, pháo hoa...)")]
        [SerializeField] private GameObject winVFXPrefab;
        [Tooltip("VFX tự hủy sau bao nhiêu giây")]
        [SerializeField] private float winVFXDuration = 3f;
        [Tooltip("Spawn VFX tại đây (để trống = spawn tại Board)")]
        [SerializeField] private Transform winVFXSpawnPoint;

        private void Start()
        {
            // Ẩn panel khi mới vào game
            if (winPanel != null)
                winPanel.SetActive(false);
        }

        public bool IsPositionValid(PuzzlePiece piece, int gridX, int gridY)
        {
            int[,] shape = piece.GetShape();
            int rows = shape.GetLength(0);
            int cols = shape.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (shape[r, c] == 1)
                    {
                        int targetX = gridX + c;
                        int targetY = gridY + r;

                        // Out of bounds
                        if (targetX < 0 || targetX >= Width || targetY < 0 || targetY >= Height)
                            return false;

                        // Overlapping
                        if (grid[targetX, targetY] == 1)
                            return false;
                    }
                }
            }
            return true;
        }

        public void PlacePiece(PuzzlePiece piece, int gridX, int gridY)
        {
            int[,] shape = piece.GetShape();
            for (int r = 0; r < shape.GetLength(0); r++)
            {
                for (int c = 0; c < shape.GetLength(1); c++)
                {
                    if (shape[r, c] == 1)
                    {
                        grid[gridX + c, gridY + r] = 1;
                    }
                }
            }
            CheckWinCondition();
        }

        public void RemovePiece(PuzzlePiece piece, int gridX, int gridY)
        {
            int[,] shape = piece.GetShape();
            for (int r = 0; r < shape.GetLength(0); r++)
            {
                for (int c = 0; c < shape.GetLength(1); c++)
                {
                    if (shape[r, c] == 1)
                    {
                        grid[gridX + c, gridY + r] = 0;
                    }
                }
            }
        }

        private void CheckWinCondition()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (grid[x, y] == 0) return; // Not full yet
                }
            }
            Debug.Log("YOU WIN!");

            // ✅ Đánh dấu scene Sơn Tinh đã hoàn thành → portal sẽ không tele lại scene này nữa
            if (MultiplayerGameManager.Instance != null && NetworkManager.Singleton != null)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                string completedScene = gameObject.scene.name;
                MultiplayerGameManager.Instance.MarkSceneCompletedServerRpc(clientId, completedScene);
                Debug.Log($"[PuzzleBoard] Marked scene '{completedScene}' completed for player {clientId}");
            }

            ShowWinUI();
        }

        private void ShowWinUI()
        {
            // Hiện panel chúc mừng
            if (winPanel != null)
                winPanel.SetActive(true);

            // Đặt sprite phần thưởng nếu có
            if (rewardImage != null && rewardSprite != null)
                rewardImage.sprite = rewardSprite;

            // Đặt câu chữ chúc mừng
            if (winMessageText != null)
                winMessageText.text = "Chúc mừng bạn đã nhận được cái Trống đồng";

            // Spawn VFX khi mở thành công
            SpawnWinVFX();
        }

        private void SpawnWinVFX()
        {
            if (winVFXPrefab == null) return;

            // Vị trí spawn: ưu tiên winVFXSpawnPoint, fallback về vị trí Board
            Vector3 spawnPos = winVFXSpawnPoint != null
                ? winVFXSpawnPoint.position
                : transform.position;

            GameObject vfx = Instantiate(winVFXPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[PuzzleBoard] Spawn Win VFX tại {spawnPos}");

            if (winVFXDuration > 0f)
                Destroy(vfx, winVFXDuration);
        }
    }
}
