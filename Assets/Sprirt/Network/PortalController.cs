using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Portal đưa player từ scene Thủy Tinh sang scene Sơn Tinh.
/// Luồng: Client chạm portal → ServerRpc → Server xử lý logic → ClientRpc → Client load scene + teleport
/// </summary>
public class PortalController : NetworkBehaviour
{
    [Header("Portal Settings")]
    [SerializeField] private List<string> sonTinhScenes = new List<string>()
    {
        "Son tinh 1",
        "Son tinh 2",
        "Son tinh 3"
    };
    [SerializeField] private bool isSonTinhPortal = true; // true = portal dẫn vào Son Tinh scenes
    [SerializeField] private bool oneTimeUse = false;

    [Header("Visual")]
    [SerializeField] private GameObject portalEffect;
    [SerializeField] private Color activeColor = Color.cyan;
    [SerializeField] private Color inactiveColor = Color.gray;

    private NetworkVariable<bool> isPortalActive = new NetworkVariable<bool>(true);

    // Track which players have used this portal (server only)
    private NetworkList<ulong> usedByPlayers;

    // Cooldown để tránh trigger nhiều lần
    private bool isTeleporting = false;

    private void Awake()
    {
        usedByPlayers = new NetworkList<ulong>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isPortalActive.OnValueChanged += OnPortalActiveChanged;
        UpdatePortalVisual();
    }

    private void OnPortalActiveChanged(bool previous, bool current)
    {
        UpdatePortalVisual();
    }

    private void UpdatePortalVisual()
    {
        if (portalEffect != null)
        {
            portalEffect.SetActive(isPortalActive.Value);
            Renderer r = portalEffect.GetComponent<Renderer>();
            if (r != null)
                r.material.color = isPortalActive.Value ? activeColor : inactiveColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"🔍 [Portal] OnTriggerEnter detected: {other.gameObject.name}");
        
        // Chỉ xử lý khi đã spawn trên network
        if (!IsSpawned)
        {
            Debug.Log($"⚠️ Portal chưa spawn trên network!");
            return;
        }

        // Chỉ owner của player mới được gửi request
        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        if (player == null)
        {
            Debug.Log($"⚠️ Object {other.gameObject.name} không có NetworkPlayerController");
            return;
        }
        
        if (!player.IsOwner)
        {
            Debug.Log($"⚠️ Player không phải owner (Client {NetworkManager.Singleton.LocalClientId})");
            return;
        }

        // Tránh trigger nhiều lần
        if (isTeleporting)
        {
            Debug.Log($"⚠️ Đang trong cooldown teleport");
            return;
        }
        
        isTeleporting = true;

        ulong clientId = player.OwnerClientId;
        Vector3 currentPos = player.transform.position;

        Debug.Log($"🌀 [Client {NetworkManager.Singleton.LocalClientId}] Player {clientId} chạm portal, gửi request lên server...");
        RequestPortalTeleportServerRpc(clientId, currentPos);

        // Reset cooldown sau 2 giây
        StartCoroutine(ResetTeleportingFlag());
    }

    private IEnumerator ResetTeleportingFlag()
    {
        yield return new WaitForSeconds(2f);
        isTeleporting = false;
    }

    // ─────────────────────────────────────────────
    // SERVER: Xử lý toàn bộ logic portal
    // ─────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void RequestPortalTeleportServerRpc(ulong clientId, Vector3 currentPosition)
    {
        Debug.Log($"📨 [Server] Nhận portal request từ client {clientId}");

        // Kiểm tra portal active
        if (!isPortalActive.Value)
        {
            Debug.Log("⛔ Portal không hoạt động!");
            PortalDeniedClientRpc(clientId);
            return;
        }

        // Kiểm tra oneTimeUse
        if (oneTimeUse && usedByPlayers.Contains(clientId))
        {
            Debug.Log($"⛔ Player {clientId} đã dùng portal này rồi!");
            PortalDeniedClientRpc(clientId);
            return;
        }

        // Lưu vị trí hiện tại của player (để quay về sau)
        string currentScene = SceneManager.GetActiveScene().name;
        if (PlayerSpawnManager.Instance != null)
        {
            PlayerSpawnManager.Instance.SavePlayerPosition(clientId, currentPosition);
            Debug.Log($"💾 Lưu vị trí player {clientId}: {currentPosition} ở cảnh {currentScene}");
        }

        // Lấy danh sách scene khả dụng
        if (MultiplayerGameManager.Instance == null)
        {
            Debug.LogError("❌ MultiplayerGameManager không tồn tại!");
            return;
        }

        List<string> available = MultiplayerGameManager.Instance.GetAvailableSonTinhScenes(clientId, sonTinhScenes);
        if (available.Count == 0)
        {
            Debug.LogWarning($"⛔ Không còn scene Sơn Tinh khả dụng cho player {clientId}!");
            NoSceneAvailableClientRpc(clientId);
            return;
        }

        // Chọn ngẫu nhiên scene
        string targetScene = available[Random.Range(0, available.Count)];
        Debug.Log($"🎲 Chọn scene: {targetScene} cho player {clientId}");

        // Đánh dấu player đang ở trong scene đó
        // Gọi server-only methods trực tiếp (tránh lỗi ServerRpc gọi ServerRpc)
        MultiplayerGameManager.Instance.PlayerEnteredSonTinhSceneServer(clientId, targetScene);
        MultiplayerGameManager.Instance.PlayerEnteredSceneServer(targetScene, clientId, currentPosition);

        // Đánh dấu portal đã dùng
        if (oneTimeUse && !usedByPlayers.Contains(clientId))
        {
            usedByPlayers.Add(clientId);
            MultiplayerGameManager.Instance.SetPortalUsedServer(clientId, true);
        }

        // Gửi lệnh cho đúng client để load scene và teleport
        Debug.Log($"📤 [Server] Gửi TeleportToSonTinhClientRpc cho client {clientId} đến scene '{targetScene}'");
        TeleportToSonTinhClientRpc(clientId, targetScene);
    }

    // ─────────────────────────────────────────────
    // CLIENT: Nhận lệnh từ server, load scene + teleport
    // ─────────────────────────────────────────────

    [ClientRpc]
    private void TeleportToSonTinhClientRpc(ulong targetClientId, string targetScene)
    {
        Debug.Log($"📨 [Client {NetworkManager.Singleton.LocalClientId}] Nhận TeleportToSonTinhClientRpc - targetClientId={targetClientId}, targetScene={targetScene}");
        
        // Chỉ client được chỉ định mới thực hiện
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
        {
            Debug.Log($"⏩ [Client {NetworkManager.Singleton.LocalClientId}] Bỏ qua, không phải target {targetClientId}");
            return;
        }

        Debug.Log($"✅ [Client {targetClientId}] Đúng target! Chuẩn bị sang cảnh: {targetScene}");
        StartCoroutine(LoadSceneAndTeleport(targetScene));
    }

    [ClientRpc]
    private void PortalDeniedClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        isTeleporting = false; // Cho phép thử lại
        Debug.Log("⛔ Portal từ chối teleport.");
    }

    [ClientRpc]
    private void NoSceneAvailableClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        isTeleporting = false;
        Debug.LogWarning("⛔ Không còn cảnh Sơn Tinh nào khả dụng! (Đã hoàn thành hết hoặc đang có người chơi khác)");
        // TODO: Hiện UI thông báo cho người chơi
    }

    // ─────────────────────────────────────────────
    // COROUTINE: Load scene Additive + Teleport
    // ─────────────────────────────────────────────

    private IEnumerator LoadSceneAndTeleport(string targetScene)
    {
        // Ghi nhớ scene đang active TRƯỚC khi chuyển (Persistent không bao giờ bị unload)
        // ⚠️ FIX BUG: Chỉ ghi nhớ scene nếu đó là Son Tinh scene để KHÔNG unload Thuy Tinh nhầm
        string previousScene = SceneManager.GetActiveScene().name;
        // Tìm scene Son Tinh đang loaded (nếu có) — đây mới là scene cần unload sau khi teleport
        string sonTinhSceneToUnload = "";
        foreach (string st in sonTinhScenes)
        {
            Scene stScene = SceneManager.GetSceneByName(st);
            if (stScene.isLoaded && st != targetScene)
            {
                sonTinhSceneToUnload = st;
                break;
            }
        }
        Debug.Log($"🚀 [Portal] Bắt đầu teleport: '{previousScene}' → '{targetScene}', Son Tinh scene cần unload: '{sonTinhSceneToUnload}'");

        // ── 1. Load scene đích nếu chưa load ──
        Scene existingScene = SceneManager.GetSceneByName(targetScene);
        if (!existingScene.isLoaded)
        {
            Debug.Log($"⏳ Đang load cảnh: {targetScene}...");
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Debug.LogError($"❌ Không thể load cảnh '{targetScene}'! Kiểm tra tên cảnh trong Build Settings.");
                isTeleporting = false;
                yield break;
            }
            while (!loadOp.isDone) yield return null;
            Debug.Log($"✅ Load cảnh '{targetScene}' hoàn tất");
        }
        else
        {
            Debug.Log($"ℹ️ Scene '{targetScene}' đã load sẵn");
        }

        // Chờ 2 frames để Unity validate scene
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        // ── 2. Set scene mới làm active ──
        Scene newScene = SceneManager.GetSceneByName(targetScene);
        Debug.Log($"🔍 Scene '{targetScene}': isLoaded={newScene.isLoaded}, isValid={newScene.IsValid()}");
        if (newScene.isLoaded && newScene.IsValid())
        {
            bool setResult = SceneManager.SetActiveScene(newScene);
            Debug.Log($"🔀 SetActiveScene('{targetScene}') → {setResult}");
        }
        yield return null;

        // ── 3. Đảm bảo player KHÔNG bị destroy khi chuyển scene ──
        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        GameObject playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject?.gameObject;
        if (playerObj != null && playerObj.scene.name != "DontDestroyOnLoad" && playerObj.scene.name != "Persistent")
        {
            DontDestroyOnLoad(playerObj);
            Debug.Log($"🛡️ Set DontDestroyOnLoad cho player object");
        }
        yield return null;

        // ── 4. ✅ QUAN TRỌNG: Move player vào target scene để world-space đồng nhất ──
        if (playerObj != null && newScene.isLoaded && newScene.IsValid() && 
            playerObj.scene.name != newScene.name)
        {
            SceneManager.MoveGameObjectToScene(playerObj, newScene);
            Debug.Log($"🏠 [Portal] Moved player vào scene '{targetScene}' để tọa độ đồng nhất");
        }
        yield return null;

        // ── 5. Gửi ServerRpc để server tự tìm spawn point trong scene đích ──
        Debug.Log($"📤 Gửi RequestServerTeleportServerRpc cho client {myClientId} đến scene '{targetScene}' (server sẽ tự tìm spawn point)");
        RequestServerTeleportServerRpc(myClientId, targetScene);

        // ── 7. Unload scene Son Tinh cũ (KHÔNG BAO GIỜ unload Thuy Tinh) ──
        // Chỉ unload nếu có Son Tinh scene đang loaded trước đó và không phải target hiện tại
        if (!string.IsNullOrEmpty(sonTinhSceneToUnload))
        {
            Debug.Log($"🗑️ Unload cảnh Sơn Tinh cũ: {sonTinhSceneToUnload}");
            SceneManager.UnloadSceneAsync(sonTinhSceneToUnload);
        }
        else
        {
            Debug.Log($"ℹ️ Không có cảnh Sơn Tinh cũ cần unload (previousScene='{previousScene}')");
        }

        isTeleporting = false;
        Debug.Log($"✅ Teleport sang '{targetScene}' hoàn tất!");
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    // ── Gửi ServerRpc để Server tìm SpawnPoint xong thì lệnh cho Client tự Teleport ──
    [ServerRpc(RequireOwnership = false)]
    private void RequestServerTeleportServerRpc(ulong clientId, string targetSceneName, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"📨 [Server] Nhận yêu cầu: client {clientId} cần teleport vào scene '{targetSceneName}' (server sẽ tự tìm spawn point)");
        StartCoroutine(ServerLoadAndTeleport(clientId, targetSceneName));
    }

    private IEnumerator ServerLoadAndTeleport(ulong clientId, string targetSceneName)
    {
        // ── Server tự load scene nếu chưa có (Additive) ──
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.Log($"⏳ [Server] Scene '{targetSceneName}' chưa load, đang load additive...");
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Debug.LogError($"❌ [Server] Không thể load scene '{targetSceneName}'! Kiểm tra Build Settings.");
                yield break;
            }
            while (!loadOp.isDone) yield return null;
            // Chờ thêm 2 frames để Unity validate
            yield return null;
            yield return null;
            targetScene = SceneManager.GetSceneByName(targetSceneName);
            Debug.Log($"✅ [Server] Load scene '{targetSceneName}' hoàn tất. isLoaded={targetScene.isLoaded}");
        }
        else
        {
            Debug.Log($"ℹ️ [Server] Scene '{targetSceneName}' đã có sẵn trên server.");
        }

        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError($"❌ [Server] Scene '{targetSceneName}' vẫn không hợp lệ sau khi load!");
            yield break;
        }

        Vector3 spawnPos = Vector3.zero;
        NetworkObject playerNetObj = null;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var connectedClient))
        {
            playerNetObj = connectedClient.PlayerObject;
        }

        if (playerNetObj != null)
        {
            if (playerNetObj.transform.parent != null)
            {
                Transform oldParent = playerNetObj.transform.parent;
                playerNetObj.transform.SetParent(null, true);
                Debug.Log($"🔓 [Server] Removed parent '{oldParent.name}' từ player {clientId}");
            }

            spawnPos = FindSpawnPointInScene(targetScene, isSonTinhPortal);
            SceneManager.MoveGameObjectToScene(playerNetObj.gameObject, targetScene);
            Debug.Log($"🏠 [Server] Moved player {clientId} vào scene '{targetSceneName}'");

            CharacterController cc = playerNetObj.GetComponent<CharacterController>();
            if (cc != null)
            {
                spawnPos.y += cc.height / 2f;
                Debug.Log($"✨ [Server] Offset Y = {cc.height / 2f} (CharacterController height = {cc.height})");
            }
            else
            {
                spawnPos.y += 1f;
                Debug.Log($"⚠️ [Server] Không có CharacterController, dùng offset Y = 1");
            }

            if (cc != null) cc.enabled = false;
            playerNetObj.transform.position = spawnPos;

            var rb = playerNetObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                Debug.Log($"🔄 [Server] Reset Rigidbody velocity cho player {clientId}");
            }
            if (cc != null) cc.enabled = true;

            var networkTransform = playerNetObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.Teleport(spawnPos, playerNetObj.transform.rotation, playerNetObj.transform.localScale);
                Debug.Log($"🌀 [Server] NetworkTransform.Teleport({clientId}) → {spawnPos}");
            }
            else
            {
                Debug.LogWarning($"⚠️ [Server] Player {clientId} không có NetworkTransform, chỉ set transform.position = {spawnPos}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ [Server] Không tìm thấy PlayerObject cho client {clientId}");
        }

        // Gửi lệnh cho client để đảm bảo client-side cũng set position
        TeleportCommandClientRpc(spawnPos, targetSceneName, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        });
    }


    // ── CLIENT: Nhận tọa độ từ Server, tự mình Teleport ──
    [ClientRpc]
    private void TeleportCommandClientRpc(Vector3 targetPosition, string targetSceneName, ClientRpcParams rpcParams = default)
    {
        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        Debug.Log($"📨 [Client {myClientId}] Sếp bảo tự diễn! Đang bay đến: {targetPosition} trong scene '{targetSceneName}'");

        GameObject playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject?.gameObject;
        if (playerObj != null)
        {
            StartCoroutine(SelfTeleportCoroutine(playerObj, targetPosition, targetSceneName, myClientId));
        }
        else
        {
            Debug.LogError($"❌ [Client {myClientId}] Không tìm thấy PlayerObject để teleport");
        }
    }

    private IEnumerator SelfTeleportCoroutine(GameObject playerObj, Vector3 targetPosition, string targetSceneName, ulong myClientId)
    {
        var cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Debug.Log($"🔁 [Client {myClientId}] SelfTeleportCoroutine bắt đầu");
        Debug.Log($"   - targetSceneName: {targetSceneName}");
        Debug.Log($"   - targetPosition: {targetPosition}");
        Debug.Log($"   - current scene: {playerObj.scene.name}");
        Debug.Log($"   - current position (trước teleport): {playerObj.transform.position}");
        Debug.Log($"   - CharacterController.enabled: {cc != null && cc.enabled}");

        Transform originalParent = playerObj.transform.parent;
        if (originalParent != null)
        {
            playerObj.transform.SetParent(null, true); // true = keep world position
            Debug.Log($"🔓 [Client {myClientId}] Removed parent '{originalParent.name}' để tọa độ world-space thuần túy");
        }

        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (targetScene.isLoaded && targetScene.IsValid() &&
            playerObj.scene.name != targetScene.name)
        {
            SceneManager.MoveGameObjectToScene(playerObj, targetScene);
            Debug.Log($"🏠 [Client {myClientId}] Moved player vào scene '{targetSceneName}' để tọa độ đồng nhất");
        }
        yield return null;

        Debug.Log($"   - scene sau MoveGameObjectToScene: {playerObj.scene.name}");

        // ⚠️ FIX BUG TREO TRÊN KHÔNG: Dùng SetPosition() của NetworkPlayerController
        // SetPosition() sẽ reset yVelocity = 0 để tránh player bị treo/bay sau teleport
        var playerController = playerObj.GetComponent<NetworkPlayerController>();
        if (playerController != null)
        {
            playerController.SetPosition(targetPosition);
            Debug.Log($"   - SetPosition() lần 1 (reset yVelocity): {playerObj.transform.position}");
        }
        else
        {
            playerObj.transform.position = targetPosition;
            Debug.Log($"   - position sau set lần 1 (fallback): {playerObj.transform.position}");
        }

        yield return null;

        // Set lần 2 để chắc chắn
        if (playerController != null)
        {
            playerController.SetPosition(targetPosition);
            Debug.Log($"   - SetPosition() lần 2: {playerObj.transform.position}");
        }
        else
        {
            playerObj.transform.position = targetPosition;
            Debug.Log($"   - position sau set lần 2 (fallback): {playerObj.transform.position}");
        }

        var networkTransform = playerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.Teleport(targetPosition, playerObj.transform.rotation, playerObj.transform.localScale);
            Debug.Log($"   - NetworkTransform.Teleport gọi với targetPosition: {targetPosition}");
        }

        // Chờ thêm 1 frame SAU NetworkTransform.Teleport để tránh race condition
        yield return null;

        // Set position lần 3 sau Teleport để đảm bảo đúng vị trí
        if (playerController != null)
        {
            playerController.SetPosition(targetPosition);
            Debug.Log($"   - SetPosition() lần 3 (post-Teleport): {playerObj.transform.position}");
        }
        else
        {
            playerObj.transform.position = targetPosition;
            Debug.Log($"   - position sau set lần 3 (post-Teleport fallback): {playerObj.transform.position}");
        }

        yield return new WaitForSeconds(0.2f);

        if (cc != null) cc.enabled = true;

        Debug.Log($"   - final scene: {playerObj.scene.name}");
        Debug.Log($"   - final position: {playerObj.transform.position}");
        Debug.Log($"✅ [Client {myClientId}] Đã TỰ teleport thành công đến: {playerObj.transform.position}");
    }

    // ── CLIENT: Reset local state sau khi teleport ──
    [ClientRpc]
    private void ResetPlayerStateClientRpc(ulong targetClientId, Vector3 newPosition)
    {
        Debug.Log($"📨 [Client {NetworkManager.Singleton.LocalClientId}] Nhận ResetPlayerStateClientRpc - targetClientId={targetClientId}, position={newPosition}");
        
        // Chỉ owner tự reset state của mình
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
        {
            Debug.Log($"⏩ [Client {NetworkManager.Singleton.LocalClientId}] Bỏ qua, không phải target {targetClientId}");
            return;
        }

        Debug.Log($"✅ [Client {targetClientId}] Đúng target! Đang reset state...");
        StartCoroutine(ForceClientPosition(targetClientId, newPosition));
    }

    // Coroutine để force position trên client
    private IEnumerator ForceClientPosition(ulong targetClientId, Vector3 newPosition)
    {
        GameObject playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject?.gameObject;
        if (playerObj != null)
        {
            // Disable CharacterController
            var controller = playerObj.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            // Force set position trên client nhiều lần để chắc chắn
            playerObj.transform.position = newPosition;
            Debug.Log($"🎯 [Client {targetClientId}] Set transform.position = {newPosition}");

            // Chờ 1 frame
            yield return null;
            
            // Set lại một lần nữa để chắc chắn
            playerObj.transform.position = newPosition;
            Debug.Log($"🎯 [Client {targetClientId}] Set position lần 2 = {newPosition}");

            // Nếu có NetworkTransform, cũng phải update
            var networkTransform = playerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (networkTransform != null)
            {
                Debug.Log($"🌐 [Client {targetClientId}] Có NetworkTransform component");
            }

            // Chờ thêm để network sync
            yield return new WaitForSeconds(0.2f);

            // Enable lại CharacterController
            if (controller != null) controller.enabled = true;

            Debug.Log($"✅ [Client {targetClientId}] Reset state tại {newPosition} hoàn tất");
            Debug.Log($"   - Final position: {playerObj.transform.position}");
        }
        else
        {
            Debug.LogError($"❌ [Client {targetClientId}] Không tìm thấy PlayerObject");
        }
    }

    private void TeleportLocalPlayer(Vector3 position)
    {
        // Phương thức này giữ lại cho ReturnPortal dùng nếu cần
        // Với portal chính, dùng RequestServerTeleportServerRpc thay thế
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;
        NetworkObject playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (playerObj == null) return;
        NetworkPlayerController playerController = playerObj.GetComponent<NetworkPlayerController>();
        if (playerController != null)
        {
            StartCoroutine(SafeTeleport(playerController, position));
        }
        else
        {
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerObj.transform.position = position;
            if (cc != null) cc.enabled = true;
            Debug.Log($"🎯 Teleport (fallback) đến {position}");
        }
    }

    private IEnumerator SafeTeleport(NetworkPlayerController playerController, Vector3 position)
    {
        CharacterController cc = playerController.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerController.transform.position = position;
        yield return null; // Chờ 1 frame

        if (cc != null) cc.enabled = true;

        playerController.SetPosition(position);
        Debug.Log($"🎯 SafeTeleport đến {position}");
    }

    // Tìm spawn point trong một scene cụ thể (FIX: tránh tìm sai scene)
    private Vector3 FindSpawnPointInScene(Scene targetScene, bool findSonTinhSpawn)
    {
        string tag = findSonTinhSpawn ? "SonTinhSpawn" : "ThuyTinhSpawn";
        string searchName = findSonTinhSpawn ? "SonTinhSpawnPoint" : "ThuyTinhSpawnPoint";

        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError($"❌ Scene '{targetScene.name}' không hợp lệ hoặc chưa load!");
            return new Vector3(0, 2, 0);
        }

        // Lấy tất cả root GameObjects trong scene
        GameObject[] rootObjects = targetScene.GetRootGameObjects();
        Debug.Log($"🔍 Tìm spawn point trong scene '{targetScene.name}' (có {rootObjects.Length} root objects)");

        // Tìm theo tag trong scene này
        foreach (GameObject root in rootObjects)
        {
            // Tìm trong chính root object
            if (root.CompareTag(tag))
            {
                Vector3 pos = root.transform.position;
                Debug.Log($"✅ Spawn point (tag '{tag}') tại {pos} trong scene '{targetScene.name}'");
                Debug.Log($"   - GameObject name: {root.name}");
                Debug.Log($"   - Local position: {root.transform.localPosition}");
                Debug.Log($"   - World position: {root.transform.position}");
                
                // Kiểm tra xem có Renderer không (để xem bounds)
                Renderer renderer = root.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Debug.Log($"   - Renderer bounds center: {renderer.bounds.center}");
                    Debug.Log($"   - Renderer bounds size: {renderer.bounds.size}");
                }
                
                return pos;
            }

            // Tìm trong các children
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.CompareTag(tag))
                {
                    Vector3 pos = child.position;
                    Debug.Log($"✅ Spawn point (tag '{tag}') tại {pos} trong scene '{targetScene.name}'");
                    Debug.Log($"   - GameObject name: {child.name}");
                    Debug.Log($"   - Local position: {child.localPosition}");
                    Debug.Log($"   - World position: {child.position}");
                    Debug.Log($"   - Parent: {(child.parent != null ? child.parent.name : "none")}");
                    
                    // Kiểm tra xem có Renderer không
                    Renderer renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Debug.Log($"   - Renderer bounds center: {renderer.bounds.center}");
                        Debug.Log($"   - Renderer bounds size: {renderer.bounds.size}");
                    }
                    
                    return pos;
                }
            }
        }

        // Tìm theo tên trong scene này
        foreach (GameObject root in rootObjects)
        {
            if (root.name == searchName)
            {
                Vector3 pos = root.transform.position;
                Debug.Log($"✅ Spawn point (tên '{searchName}') tại {pos} trong scene '{targetScene.name}'");
                Debug.Log($"   - Local position: {root.transform.localPosition}");
                return pos;
            }

            Transform found = root.transform.Find(searchName);
            if (found != null)
            {
                Vector3 pos = found.position;
                Debug.Log($"✅ Spawn point (tên '{searchName}') tại {pos} trong scene '{targetScene.name}'");
                Debug.Log($"   - Local position: {found.localPosition}");
                Debug.Log($"   - Parent: {(found.parent != null ? found.parent.name : "none")}");
                return pos;
            }

            // Deep search
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == searchName)
                {
                    Vector3 pos = child.position;
                    Debug.Log($"✅ Spawn point (tên '{searchName}') tại {pos} trong scene '{targetScene.name}'");
                    Debug.Log($"   - Local position: {child.localPosition}");
                    Debug.Log($"   - Parent: {(child.parent != null ? child.parent.name : "none")}");
                    return pos;
                }
            }
        }

        Debug.LogWarning($"⚠️ Không tìm thấy spawn point (tag='{tag}' hoặc tên='{searchName}') trong scene '{targetScene.name}', dùng mặc định (0, 2, 0)");
        return new Vector3(0, 2, 0);
    }

    // Legacy method - giữ lại cho các trường hợp khác dùng
    private Vector3 FindSpawnPoint(bool findSonTinhSpawn)
    {
        string tag = findSonTinhSpawn ? "SonTinhSpawn" : "ThuyTinhSpawn";
        string searchName = findSonTinhSpawn ? "SonTinhSpawnPoint" : "ThuyTinhSpawnPoint";

        // Ưu tiên tìm theo tag
        GameObject[] byTag = GameObject.FindGameObjectsWithTag(tag);
        if (byTag != null && byTag.Length > 0)
        {
            Vector3 pos = byTag[0].transform.position + Vector3.up * 0.5f;
            Debug.Log($"✅ Spawn point (tag '{tag}') tại {pos}");
            return pos;
        }

        // Tìm theo tên
        GameObject byName = GameObject.Find(searchName);
        if (byName != null)
        {
            Vector3 pos = byName.transform.position + Vector3.up * 0.5f;
            Debug.Log($"✅ Spawn point (tên '{searchName}') tại {pos}");
            return pos;
        }

        Debug.LogWarning($"⚠️ Không tìm thấy spawn point (tag='{tag}'), dùng mặc định (0, 2, 0)");
        return new Vector3(0, 2, 0);
    }



    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    public void SetPortalActiveServerRpc(bool active)
    {
        isPortalActive.Value = active;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetPortalForPlayerServerRpc(ulong clientId)
    {
        if (usedByPlayers.Contains(clientId))
        {
            usedByPlayers.Remove(clientId);
            Debug.Log($"🔄 Portal reset cho player {clientId}");
        }
    }

    public bool IsPortalActive() => isPortalActive.Value;
    public bool HasPlayerUsed(ulong clientId) => usedByPlayers.Contains(clientId);
}
