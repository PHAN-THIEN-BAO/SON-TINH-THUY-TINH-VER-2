using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Portal đưa player từ scene Sơn Tinh quay về scene Thủy Tinh.
///
/// ⚠️ QUAN TRỌNG VỀ KIẾN TRÚC:
///   ReturnPortal là MonoBehaviour (KHÔNG phải NetworkBehaviour) vì:
///   - Scene Sơn Tinh được load bằng SceneManager.LoadSceneAsync (client-side)
///   - Không đi qua NetworkManager.SceneManager → NGO không Spawn scene objects
///   - Gọi ServerRpc từ NetworkBehaviour chưa Spawn → NullReferenceException
///
///   Giải pháp: Route tất cả RPC qua PlayerSpawnManager (đã được Spawn đúng chuẩn).
///   Luồng: Client chạm → PlayerSpawnManager.HandleReturnPortalServerRpc
///         → Server lấy saved position → PlayerSpawnManager.HandleReturnPortalClientRpc
///         → Client chạy coroutine load Thủy Tinh + teleport
///         → PlayerSpawnManager.TeleportToSceneServerRpc (final position sync)
/// </summary>
public class ReturnPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    [SerializeField] private string returnSceneName = "Thuy Tinh";

    [Header("Visual")]
    [SerializeField] private GameObject portalEffect;
    [SerializeField] private Color portalColor = new Color(0.5f, 1f, 0.5f);

    [Header("Return VFX")]
    [Tooltip("Prefab VFX sẽ spawn tại vị trí portal trong scene Thủy Tinh khi player quay về (ví dụ: WaterSpell prefab)")]
    [SerializeField] private GameObject portalVFXPrefab;
    [Tooltip("VFX tự hủy sau bao nhiêu giây (0 = không tự hủy)")]
    [SerializeField] private float portalVFXDuration = 3f;
    [Tooltip("Vị trí bù (offset) của VFX so với vị trí portal khi spawn")]
    [SerializeField] private Vector3 portalVFXOffset = Vector3.zero;

    // Cooldown tránh trigger nhiều lần
    private bool isTeleporting = false;

    // Scene của chính portal này (Son tinh 1/2/3)
    private string mySceneName;

    private void Start()
    {
        mySceneName = gameObject.scene.name;
        Debug.Log($"🌀 [ReturnPortal] Khởi tạo trong scene: '{mySceneName}'");

        if (portalEffect != null)
        {
            Renderer r = portalEffect.GetComponent<Renderer>();
            if (r != null) r.material.color = portalColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning("⚠️ [ReturnPortal] NetworkManager chưa kết nối.");
            return;
        }

        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        if (player == null || !player.IsOwner) return;

        if (isTeleporting) return;
        isTeleporting = true;

        ulong clientId = player.OwnerClientId;

        // Lấy scene name từ chính portal (không dùng GetActiveScene() vì có thể trả về Persistent)
        string fromScene = string.IsNullOrEmpty(mySceneName) ? gameObject.scene.name : mySceneName;

        Debug.Log($"🔙 Player {clientId} chạm ReturnPortal. Portal scene='{fromScene}', về '{returnSceneName}'");

        // ✅ Route RPC qua PlayerSpawnManager (đã được Spawn đúng chuẩn)
        if (PlayerSpawnManager.Instance != null)
        {
            PlayerSpawnManager.Instance.HandleReturnPortalServerRpc(clientId, fromScene);
        }
        else
        {
            Debug.LogError("❌ [ReturnPortal] PlayerSpawnManager.Instance null! Không thể gửi RPC.");
            isTeleporting = false;
            return;
        }

        StartCoroutine(ResetTeleportingFlag());
    }

    private IEnumerator ResetTeleportingFlag()
    {
        yield return new WaitForSeconds(3f);
        isTeleporting = false;
    }

    // ─────────────────────────────────────────────
    // Được gọi bởi PlayerSpawnManager.HandleReturnPortalClientRpc
    // ─────────────────────────────────────────────

    public void StartReturnFlow(string fromScene, Vector3 savedPosition)
    {
        Debug.Log($"📍 [ReturnPortal] StartReturnFlow: fromScene='{fromScene}', savedPos={savedPosition}");
        StartCoroutine(LoadReturnSceneAndTeleport(fromScene, savedPosition));
    }

    // ─────────────────────────────────────────────
    // COROUTINE: Load scene Thủy Tinh + Teleport
    // ─────────────────────────────────────────────

    private IEnumerator LoadReturnSceneAndTeleport(string fromScene, Vector3 savedPosition)
    {
        // ── 1. Đưa player vào DontDestroyOnLoad để không bị destroy khi unload Sơn Tinh ──
        GameObject playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject?.gameObject;
        if (playerObj != null && playerObj.scene.name != "DontDestroyOnLoad" && playerObj.scene.name != "Persistent")
        {
            DontDestroyOnLoad(playerObj);
            Debug.Log($"🛡️ [ReturnPortal] Đưa player vào DontDestroyOnLoad");
        }

        // ── 2. Load scene Thủy Tinh nếu chưa load ──
        Scene existingScene = SceneManager.GetSceneByName(returnSceneName);
        if (!existingScene.isLoaded)
        {
            Debug.Log($"⏳ Đang load cảnh về: {returnSceneName}...");
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(returnSceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Debug.LogError($"❌ Không thể load cảnh '{returnSceneName}'! Kiểm tra tên trong Build Settings.");
                isTeleporting = false;
                yield break;
            }
            yield return loadOp;
            Debug.Log($"✅ Load cảnh '{returnSceneName}' xong");
        }
        else
        {
            Debug.Log($"ℹ️ Scene '{returnSceneName}' đã load sẵn");
        }

        // ── 3. Đặt scene Thủy Tinh làm active ──
        Scene returnScene = SceneManager.GetSceneByName(returnSceneName);
        if (returnScene.isLoaded && returnScene.IsValid())
        {
            SceneManager.SetActiveScene(returnScene);
            Debug.Log($"🔀 SetActiveScene('{returnSceneName}')");
        }
        else
        {
            Debug.LogError($"❌ Scene '{returnSceneName}' không hợp lệ sau khi load!");
            isTeleporting = false;
            yield break;
        }

        // Chờ 2 frame
        yield return null;
        yield return null;

        // ── 4. Đưa player vào scene Thủy Tinh để world-space align đúng ──
        playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject?.gameObject;
        if (playerObj != null)
        {
            if (playerObj.transform.parent != null)
            {
                playerObj.transform.SetParent(null, true);
                Debug.Log($"🔓 [ReturnPortal] Removed parent");
            }

            if (returnScene.isLoaded && returnScene.IsValid() &&
                playerObj.scene.name != returnScene.name)
            {
                SceneManager.MoveGameObjectToScene(playerObj, returnScene);
                Debug.Log($"🏠 [ReturnPortal] Moved player into scene '{returnSceneName}'");
            }
        }
        yield return null;

        // ── 5. Xác định vị trí teleport ──
        Vector3 teleportPos = savedPosition;
        if (teleportPos == Vector3.zero)
        {
            teleportPos = FindReturnSpawnPoint();
            Debug.Log($"📍 Không có saved position, dùng spawn point: {teleportPos}");
        }
        Debug.Log($"📍 [ReturnPortal] Teleport về: {teleportPos}");

        // ── 6. Gọi ServerRpc qua PlayerSpawnManager để teleport ──
        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        if (PlayerSpawnManager.Instance != null)
        {
            PlayerSpawnManager.Instance.TeleportToSceneServerRpc(myClientId, teleportPos, returnSceneName);
        }

        // ── 7. Chờ 1s rồi unload Sơn Tinh (đảm bảo server teleport xong trước) ──
        bool isPersistentScene = string.IsNullOrEmpty(fromScene)
                                 || fromScene == "Persistent"
                                 || fromScene.Contains("Persistent")
                                 || fromScene == "DontDestroyOnLoad";

        if (!isPersistentScene && fromScene != returnSceneName)
        {
            yield return new WaitForSeconds(1f);
            Scene sonTinhScene = SceneManager.GetSceneByName(fromScene);
            if (sonTinhScene.isLoaded)
            {
                Debug.Log($"🗑️ Unload cảnh Sơn Tinh: '{fromScene}'");
                SceneManager.UnloadSceneAsync(fromScene);
            }
            else
            {
                Debug.LogWarning($"⚠️ Scene '{fromScene}' không còn loaded, bỏ qua unload");
            }
        }

        isTeleporting = false;
        Debug.Log($"✅ Đã về '{returnSceneName}' hoàn tất!");

        // ── 8. Spawn VFX locally (không cần network sync cho particle effect đơn giản) ──
        if (portalVFXPrefab != null && teleportPos != Vector3.zero)
        {
            Vector3 vfxPos = teleportPos + portalVFXOffset;
            Debug.Log($"✨ [ReturnPortal] Spawn VFX local tại {vfxPos}");
            GameObject vfxObj = Instantiate(portalVFXPrefab, vfxPos, Quaternion.identity);

            // Đưa VFX vào scene Thủy Tinh
            Scene thuyTinhScene = SceneManager.GetSceneByName(returnSceneName);
            if (thuyTinhScene.isLoaded && thuyTinhScene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(vfxObj, thuyTinhScene);
            }

            if (portalVFXDuration > 0f)
            {
                Destroy(vfxObj, portalVFXDuration);
            }
        }
    }

    private Vector3 FindReturnSpawnPoint()
    {
        GameObject[] byTag = GameObject.FindGameObjectsWithTag("ThuyTinhSpawn");
        if (byTag != null && byTag.Length > 0)
        {
            Vector3 pos = byTag[0].transform.position + Vector3.up * 0.5f;
            Debug.Log($"✅ Spawn point (tag 'ThuyTinhSpawn') tại {pos}");
            return pos;
        }

        GameObject byName = GameObject.Find("ThuyTinhSpawnPoint");
        if (byName != null)
        {
            Vector3 pos = byName.transform.position + Vector3.up * 0.5f;
            Debug.Log($"✅ Spawn point (tên 'ThuyTinhSpawnPoint') tại {pos}");
            return pos;
        }

        Debug.LogWarning("⚠️ Không tìm thấy ThuyTinhSpawnPoint, dùng mặc định (0, 2, 0)");
        return new Vector3(0, 2, 0);
    }
}
