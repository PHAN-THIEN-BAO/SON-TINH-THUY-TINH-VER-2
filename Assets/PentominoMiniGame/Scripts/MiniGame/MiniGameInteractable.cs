using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace MiniGame
{
    /// <summary>
    /// Gắn vào vật thể 3D trong world (ví dụ: trống đồng, hòm báu vật...).
    /// - Khi local player bước vào vùng trigger → hiện prompt "Bấm F để mở"
    /// - Nhấn F → mở MiniGameCanvas
    /// - Ra khỏi vùng → ẩn prompt, đóng mini game
    ///
    /// Setup:
    ///   1. Gắn script này lên GameObject vật thể
    ///   2. Thêm Collider (set Is Trigger = true) vào vật thể
    ///   3. Tạo GameObject con làm prompt (TextMeshPro), kéo vào field PromptObject
    ///   4. Kéo MiniGameCanvas vào field MiniGameCanvas
    /// </summary>
    public class MiniGameInteractable : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("GameObject con hiện chữ 'Bấm F để mở' — thường là TextMeshPro World Space")]
        [SerializeField] private GameObject promptObject;

        [Tooltip("Canvas của mini game cần mở")]
        [SerializeField] private MiniGameCanvasSetup miniGameCanvas;

        [Header("Settings")]
        [Tooltip("Phím bấm để mở mini game")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        [Tooltip("Nội dung chữ hiện trên prompt")]
        [SerializeField] private string promptText = "Bấm F để mở";

        // Trạng thái
        private bool isPlayerInRange = false;
        private bool isMiniGameOpen = false;

        private void Start()
        {
            // Ẩn prompt lúc đầu
            if (promptObject != null)
            {
                // Set text nếu có TMP
                TextMeshPro tmp = promptObject.GetComponentInChildren<TextMeshPro>();
                if (tmp != null) tmp.text = promptText;

                promptObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!isPlayerInRange) return;

            // Nhấn F → toggle mini game
            if (Input.GetKeyDown(interactKey))
            {
                if (isMiniGameOpen)
                    CloseMiniGame();
                else
                    OpenMiniGame();
            }

            // Billboard effect: prompt luôn xoay mặt về phía camera
            if (promptObject != null && promptObject.activeSelf && Camera.main != null)
            {
                promptObject.transform.LookAt(
                    promptObject.transform.position + Camera.main.transform.rotation * Vector3.forward,
                    Camera.main.transform.rotation * Vector3.up
                );
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Chỉ phản ứng với local player
            NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
            if (player == null || !player.IsOwner) return;

            isPlayerInRange = true;
            ShowPrompt(true);
            Debug.Log("[MiniGameInteractable] Local player vào vùng tương tác");
        }

        private void OnTriggerExit(Collider other)
        {
            NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
            if (player == null || !player.IsOwner) return;

            isPlayerInRange = false;
            ShowPrompt(false);

            // Tự động đóng mini game khi đi ra
            if (isMiniGameOpen) CloseMiniGame();

            Debug.Log("[MiniGameInteractable] Local player rời vùng tương tác");
        }

        private void OpenMiniGame()
        {
            if (miniGameCanvas == null)
            {
                Debug.LogError("[MiniGameInteractable] Chưa gán MiniGameCanvas!");
                return;
            }

            miniGameCanvas.OpenForLocalPlayer();
            isMiniGameOpen = true;

            // Ẩn prompt khi đang chơi
            ShowPrompt(false);
        }

        private void CloseMiniGame()
        {
            if (miniGameCanvas != null)
                miniGameCanvas.Close();

            isMiniGameOpen = false;

            // Hiện lại prompt nếu vẫn trong vùng
            if (isPlayerInRange)
                ShowPrompt(true);
        }

        private void ShowPrompt(bool show)
        {
            if (promptObject != null)
                promptObject.SetActive(show);
        }
    }
}