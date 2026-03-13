using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Netcode;

namespace MiniGame
{
    /// <summary>
    /// Gắn vào MiniGameCanvas.
    /// Khi mở: chuyển sang Screen Space - Camera (để DragDrop hoạt động)
    /// Khi đóng: trả về World Space (nằm trong scene 3D)
    /// Tự động unlock cursor khi mở, lock lại khi đóng.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class MiniGameCanvasSetup : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Mở khóa chuột khi mini game đang mở để player có thể kéo thả")]
        [SerializeField] private bool unlockCursorWhenOpen = true;

        [Tooltip("Khoảng cách canvas với camera khi ở chế độ Screen Space - Camera")]
        [SerializeField] private float planeDistance = 1f;

        private Canvas canvas;
        private RenderMode originalRenderMode;
        private float originalPlaneDistance;
        private Camera originalWorldCamera;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;

        private NetworkPlayerController localPlayer;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            // Lưu trạng thái ban đầu
            originalRenderMode    = canvas.renderMode;
            originalPlaneDistance = canvas.planeDistance;
            originalWorldCamera   = canvas.worldCamera;
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // KHÔNG thay đổi renderMode thành ScreenSpaceCamera nữa
            // Chỉ cần gán Camera để hệ thống biết tia Raycast từ đâu tới
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
                Debug.Log($"[MiniGameCanvasSetup] Gán worldCamera = {Camera.main.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[MiniGameCanvasSetup] Camera.main == null. Raycast UI sẽ không hoạt động!");
            }

            // Tìm local player (chỉ một lần)
            if (localPlayer == null)
            {
                var players = FindObjectsOfType<NetworkPlayerController>();
                foreach (var p in players)
                {
                    if (p.IsOwner)
                    {
                        localPlayer = p;
                        break;
                    }
                }
            }

            if (localPlayer != null)
            {
                localPlayer.EnterUiMode();
            }

            if (unlockCursorWhenOpen)
            {
                previousCursorLockMode = Cursor.lockState;
                previousCursorVisible  = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }

        private void OnDisable()
        {
            if (localPlayer != null)
            {
                localPlayer.ExitUiMode();
            }

            // Chỉ cần khôi phục Cursor
            if (unlockCursorWhenOpen)
            {
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible   = previousCursorVisible;
            }
        }

        /// <summary>Mở mini game cho local player.</summary>
        public void OpenForLocalPlayer()
        {
            gameObject.SetActive(true);
            Debug.Log("[MiniGameCanvas] Mở mini game!");
        }

        /// <summary>Đóng mini game.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
            Debug.Log("[MiniGameCanvas] Đóng mini game.");
        }
    }
}