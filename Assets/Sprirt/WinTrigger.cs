using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinTrigger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool autoReturnToThuyTinh = true;
    [SerializeField] private float returnDelay = 3f; // Đợi 3 giây trước khi về
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"🔔 WinTrigger OnTriggerEnter! Object: {other.gameObject.name}");
        
        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        
        // Chỉ xử lý cho local player (IsOwner)
        if (player != null && player.IsOwner)
        {
            ulong clientId = player.OwnerClientId;
            string currentScene = gameObject.scene.name;
            
            Debug.Log($"🏆 Local player WIN in scene {currentScene}!");
            
            // Call ServerRpc to mark scene completed
            if (MultiplayerGameManager.Instance != null)
            {
                MultiplayerGameManager.Instance.MarkSceneCompletedServerRpc(clientId, currentScene);
            }
            
            // Show local win message
            Debug.Log($"🎉 YOU WIN {currentScene}! Returning to Thủy Tinh in {returnDelay} seconds...");
            
            // Auto return về Thủy Tinh nếu enable
            if (autoReturnToThuyTinh)
            {
                StartCoroutine(ReturnPlayerAfterDelay(clientId, returnDelay));
            }
        }
    }
    
    private IEnumerator ReturnPlayerAfterDelay(ulong clientId, float delay)
    {
        Debug.Log($"⏱️ Waiting {delay} seconds before returning...");
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"✅ Delay complete, returning to Thủy Tinh");
        ReturnToThuyTinh(clientId);
    }
    
    private void ReturnToThuyTinh(ulong clientId)
    {
        Debug.Log("🔙 Returning to Thủy Tinh...");
        
        // Lấy vị trí đã lưu để về
        Vector3 returnPosition = Vector3.zero;
        if (PlayerSpawnManager.Instance != null)
        {
            returnPosition = PlayerSpawnManager.Instance.GetSavedPosition(clientId);
        }
        
        string currentScene = gameObject.scene.name;
        
        // Load Thủy Tinh scene
        Scene targetScene = SceneManager.GetSceneByName("Thuy Tinh");
        if (targetScene.isLoaded)
        {
            SceneManager.SetActiveScene(targetScene);
            TeleportToPosition(returnPosition);
            
            // Notify server that player left Son Tinh scene
            if (MultiplayerGameManager.Instance != null)
            {
                MultiplayerGameManager.Instance.PlayerLeftSonTinhSceneServerRpc(clientId);
            }
            
            // Unload Sơn Tinh scene
            SceneManager.UnloadSceneAsync(currentScene);
        }
        else
        {
            // Load Thủy Tinh nếu chưa load
            AsyncOperation loadOp = SceneManager.LoadSceneAsync("Thuy Tinh", LoadSceneMode.Additive);
            if (loadOp != null)
            {
                loadOp.completed += (op) =>
                {
                    Scene thuyTinhScene = SceneManager.GetSceneByName("Thuy Tinh");
                    SceneManager.SetActiveScene(thuyTinhScene);
                    TeleportToPosition(returnPosition);
                    
                    // Notify server
                    if (MultiplayerGameManager.Instance != null)
                    {
                        MultiplayerGameManager.Instance.PlayerLeftSonTinhSceneServerRpc(clientId);
                    }
                    
                    SceneManager.UnloadSceneAsync(currentScene);
                };
            }
        }
    }
    
    private void TeleportToPosition(Vector3 position)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;

        NetworkObject playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (playerObject == null) return;

        // Sử dụng teleport trực tiếp qua NetworkPlayerController
        NetworkPlayerController playerController = playerObject.GetComponent<NetworkPlayerController>();
        if (playerController != null)
        {
            playerController.SetPosition(position);
            Debug.Log($"🎯 [WinTrigger] Teleport to: {position}");
        }
        else
        {
            Debug.LogWarning("⚠️ NetworkPlayerController không tìm thấy, dùng fallback");
            CharacterController cc = playerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerObject.transform.position = position;
            if (cc != null) cc.enabled = true;
            Debug.Log($"🎯 [WinTrigger] Teleport (fallback) to: {position}");
        }
    }
}

