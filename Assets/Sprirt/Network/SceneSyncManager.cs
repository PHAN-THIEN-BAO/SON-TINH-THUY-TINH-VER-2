using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSyncManager : NetworkBehaviour
{
    public static SceneSyncManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private string thuyTinhSceneName = "Thuy Tinh";
    [SerializeField] private string sonTinhSceneName = "Son tinh";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void LoadSceneForPlayer(ulong clientId, string sceneName, Vector3 returnPosition)
    {
        if (!IsServer) return;

        // Lưu vị trí trước khi chuyển scene
        if (PlayerSpawnManager.Instance != null)
        {
            PlayerSpawnManager.Instance.SavePlayerPosition(clientId, returnPosition);
        }

        // Load scene cho client cụ thể
        LoadSceneClientRpc(clientId, sceneName);
    }

    [ClientRpc]
    private void LoadSceneClientRpc(ulong targetClientId, string sceneName)
    {
        // Chỉ client được chỉ định mới load scene
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        Debug.Log($"🌍 Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void ReturnPlayerToThuyTinh(ulong clientId)
    {
        if (!IsServer) return;

        // Lấy vị trí đã lưu
        Vector3 returnPosition = Vector3.zero;
        if (PlayerSpawnManager.Instance != null)
        {
            returnPosition = PlayerSpawnManager.Instance.GetSavedPosition(clientId);
        }

        // Load scene Thủy Tinh
        LoadSceneClientRpc(clientId, thuyTinhSceneName);

        // Spawn player ở vị trí cũ sau 1 frame
        StartCoroutine(RestorePlayerPositionCoroutine(clientId, returnPosition));
    }

    private System.Collections.IEnumerator RestorePlayerPositionCoroutine(ulong clientId, Vector3 position)
    {
        yield return new WaitForSeconds(0.5f); // Đợi scene load xong

        if (PlayerSpawnManager.Instance != null && position != Vector3.zero)
        {
            PlayerSpawnManager.Instance.TeleportPlayer(clientId, position);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void NotifyPlayerSceneChangeServerRpc(ulong clientId, string fromScene, string toScene)
    {
        Debug.Log($"📍 Player {clientId} changed from {fromScene} to {toScene}");

        // Cập nhật MultiplayerGameManager
        if (MultiplayerGameManager.Instance != null)
        {
            Vector3 currentPos = Vector3.zero;
            if (PlayerSpawnManager.Instance != null)
            {
                NetworkPlayerController player = PlayerSpawnManager.Instance.GetPlayer(clientId);
                if (player != null)
                {
                    currentPos = player.GetPosition();
                }
            }

            MultiplayerGameManager.Instance.PlayerEnteredSceneServerRpc(toScene, clientId, currentPos);
        }
    }

    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    public bool IsInThuyTinh()
    {
        return GetCurrentSceneName().Contains("Thuy");
    }

    public bool IsInSonTinh()
    {
        return GetCurrentSceneName().Contains("Son");
    }
}
