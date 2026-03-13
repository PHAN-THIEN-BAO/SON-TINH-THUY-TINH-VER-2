using Unity.Netcode;
using UnityEngine;

public enum CharacterType
{
    SonTinh,  // Sơn Tinh - Thần núi
    ThuyTinh  // Thủy Tinh - Thần nước
}

public class CharacterAbility : NetworkBehaviour
{
    [Header("Character Type")]
    [SerializeField] private CharacterType characterType = CharacterType.ThuyTinh;

    [Header("Son Tinh Abilities (Thần Núi)")]
    [SerializeField] private GameObject earthWallPrefab;          // E
    [SerializeField] private GameObject quicksandPrefab;          // Q
    [SerializeField] private float leapForce          = 60f;      // R - lực nhảy lên thẳng đứng
    // R - Ba vòng sóng chấn động:
    [SerializeField] private float quakeInnerRadius   = 2.5f;     // Vòng trong: stun + tung lên
    [SerializeField] private float quakeMiddleRadius  = 5f;       // Vòng giữa: stun + đẩy ra
    [SerializeField] private float quakeOuterRadius   = 8f;       // Vòng ngoài: chậm
    [SerializeField] private float quakeInnerStun     = 2.5f;
    [SerializeField] private float quakeMiddleStun    = 1.2f;
    [SerializeField] private float quakeKnockUp       = 28f;      // Lực tung lên vòng trong
    [SerializeField] private float quakeKnockOut      = 22f;      // Lực đẩy ra vòng giữa
    // E - Burst khi tường xuất hiện:
    [SerializeField] private float wallBurstForce     = 22f;      // Lực tung lên người bị trúng
    [SerializeField] private float wallBurstStun      = 0.6f;     // Stun ngắn khi bị bắt

    [Header("Thuy Tinh Abilities (Thần Nước)")]
    [SerializeField] private GameObject whirlpoolPrefab;       // E
    [SerializeField] private GameObject waterWavePrefab;       // Q
    [SerializeField] private float dashForce        = 55f;     // R - lực lao về phía trước
    [SerializeField] private float ultimateImpactRadius = 4f;  // R - bán kính phát hiện địch
    [SerializeField] private float ultimateLaunchForce  = 40f; // R - lực tung địch lên không
    [SerializeField] private float ultimateStunDuration = 1.8f;// R - stun khi bị tung lên

    [Header("Cooldowns")]
    [SerializeField] private float abilityE_Cooldown = 8f; // Skill 1
    [SerializeField] private float abilityQ_Cooldown = 6f; // Skill 2
    [SerializeField] private float abilityR_Cooldown = 15f; // Skill 3 (Ultimate)

    // Client-side timers (cho UI và kiểm tra input)
    private float abilityE_Timer = 0f;
    private float abilityQ_Timer = 0f;
    private float abilityR_Timer = 0f;
    
    // Server-side timers (chống spam RPC) - chạy độc lập trên server
    private float serverE_Timer = 0f;
    private float serverQ_Timer = 0f;
    private float serverR_Timer = 0f;

    [Header("--- Âm Thanh Skill (Kéo file audio vào đây) ---")]
    [Header("Sơn Tinh Sounds")]
    [SerializeField] private AudioClip sfx_sonE;   // Âm thanh Tường Vách Núi
    [SerializeField] private AudioClip sfx_sonQ;   // Âm thanh Cát Lún
    [SerializeField] private AudioClip sfx_sonR;   // Âm thanh ĐẠI ĐỊ A CHẤN
    [SerializeField] private AudioClip sfx_sonRSlam; // Âm thanh khi cắm xuống
    [Header("Thủy Tinh Sounds")]
    [SerializeField] private AudioClip sfx_tuyE;   // Âm thanh Lốc Xoáy
    [SerializeField] private AudioClip sfx_tuyQ;   // Âm thanh Sóng Nước
    [SerializeField] private AudioClip sfx_tuyR;   // Âm thanh Thủy Long Đột Kích
    [SerializeField] private AudioClip sfx_tuyRImpact; // Âm thanh va chạm
    [SerializeField] private float sfxVolume = 1f;

    private AudioSource audioSource;
    private NetworkPlayerController playerController;

    private void Start()
    {
        playerController = GetComponent<NetworkPlayerController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D sound
    }

    // Tìm đối thủ trên server (player không phải mình)
    private NetworkPlayerController FindEnemyPlayer()
    {
        NetworkPlayerController closest = null;
        float closestDist = float.MaxValue;
        foreach (var pc in FindObjectsOfType<NetworkPlayerController>())
        {
            if (pc.OwnerClientId == OwnerClientId) continue;
            float d = Vector3.Distance(transform.position, pc.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = pc;
            }
        }
        return closest;
    }

    private void Update()
    {
        // --- Cập nhật cooldown timer client-side (cho UI) ---
        if (abilityE_Timer > 0) abilityE_Timer -= Time.deltaTime;
        if (abilityQ_Timer > 0) abilityQ_Timer -= Time.deltaTime;
        if (abilityR_Timer > 0) abilityR_Timer -= Time.deltaTime;
        
        // --- Cập nhật cooldown timer server-side ---
        if (IsServer)
        {
            if (serverE_Timer > 0) serverE_Timer -= Time.deltaTime;
            if (serverQ_Timer > 0) serverQ_Timer -= Time.deltaTime;
            if (serverR_Timer > 0) serverR_Timer -= Time.deltaTime;
        }

        if (!IsOwner) return;

        // Block input cho đến khi game load xong
        if (!GameLoadSystem.Ready) return;

        // Input handling - client gọi ServerRpc và tự set cooldown để UI phản hồi ngay
        if (Input.GetKeyDown(KeyCode.E) && abilityE_Timer <= 0)
        {
            UseAbilityEServerRpc();
            abilityE_Timer = abilityE_Cooldown; // Set SAU khi gọi RPC để tránh lỗi cooldown check
        }

        if (Input.GetKeyDown(KeyCode.Q) && abilityQ_Timer <= 0)
        {
            UseAbilityQServerRpc();
            abilityQ_Timer = abilityQ_Cooldown;
        }
        
        if (Input.GetKeyDown(KeyCode.R) && abilityR_Timer <= 0)
        {
            UseAbilityRServerRpc();
            abilityR_Timer = abilityR_Cooldown;
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void UseAbilityEServerRpc()
    {
        // Server kiểm tra timer riêng để chống spam (không dùng timer của client)
        if (serverE_Timer > 0f) return;
        serverE_Timer = abilityE_Cooldown;

        switch (characterType)
        {
            case CharacterType.SonTinh:
                SpawnEarthWall();
                PlaySoundClientRpc(0); // sfx_sonE
                break;

            case CharacterType.ThuyTinh:
                SpawnWhirlpool();
                PlaySoundClientRpc(3); // sfx_tuyE
                break;
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void UseAbilityQServerRpc()
    {
        if (serverQ_Timer > 0f) return;
        serverQ_Timer = abilityQ_Cooldown;

        switch (characterType)
        {
            case CharacterType.SonTinh:
                SpawnQuicksand();
                PlaySoundClientRpc(1); // sfx_sonQ
                break;

            case CharacterType.ThuyTinh:
                SpawnWaterWave();
                PlaySoundClientRpc(4); // sfx_tuyQ
                break;
        }
    }
    
    [ServerRpc(RequireOwnership = true)]
    private void UseAbilityRServerRpc()
    {
        if (serverR_Timer > 0f) return;
        serverR_Timer = abilityR_Cooldown;

        switch (characterType)
        {
            case CharacterType.SonTinh:
                // === GLOBAL PHASE: Gây khó chịu TOÀN BẢN ĐỔ người địch từc thì ===
                NetworkPlayerController sonEnemy = FindEnemyPlayer();
                if (sonEnemy != null)
                {
                    sonEnemy.ApplySlow(0.35f, 2.5f);    // Chậm 65% trong khi chờ sàn xuống
                    sonEnemy.ApplyConfuse(1.5f);          // Rối loạn điều khiển - không chạy thoát kịp
                }
                // Phase 1: Nhảy THẰẾ ĐỨNG lên cao
                if (playerController != null)
                {
                    Vector3 jumpUp = (Vector3.up * 1.8f + transform.forward * 0.3f).normalized;
                    playerController.ApplyKnockback(jumpUp * leapForce);
                }
                DebugLogClientRpc("⛰️⚡ Sơn Tinh: ĐẠI ĐỊ A CHẤN — Đất truyện khắp nơi!");
                PlaySoundClientRpc(2); // sfx_sonR (tiếng nhảy lên)
                // Phase 2: Cắm xuống sau 1.2s
                Invoke(nameof(SonTinhEarthquakeSlam), 1.2f);
                break;

            case CharacterType.ThuyTinh:
                // === GLOBAL PHASE: Gây khó chịu TUẦN TỨ dù ở khắt nơi ===
                NetworkPlayerController tuyEnemy = FindEnemyPlayer();
                if (tuyEnemy != null)
                {
                    tuyEnemy.ApplySlow(0.3f, 3f);        // Chậm 70% — bị trói bước khi sóng lập lại
                    tuyEnemy.ApplyConfuse(1.2f);           // Rối loạn lúửng lự đường chạy
                }
                // Phase 1: Dash tự ngắm thẳng vào địch (auto-aim)
                if (playerController != null)
                {
                    Vector3 dashDir;
                    if (tuyEnemy != null)
                    {
                        // Tự hướng về phía địch dù nhìn đâu
                        dashDir = (tuyEnemy.transform.position - transform.position);
                        dashDir.y = 0f;
                        dashDir    = (dashDir.normalized + Vector3.up * 0.25f).normalized;
                    }
                    else
                    {
                        dashDir = (transform.forward + Vector3.up * 0.25f).normalized;
                    }
                    playerController.ApplyKnockback(dashDir * dashForce);
                }
                DebugLogClientRpc("🌊⚡ Thủy Tinh: THỦY LONG ĐỘT KÍCH — Nước cuốn khắp nơi!");
                PlaySoundClientRpc(5); // sfx_tuyR (tiếng lao đi)
                // Phase 2 & 3 sau 0.2s (khi caster đã di chuyển)
                Invoke(nameof(ThuyTinhUltimateImpact), 0.2f);
                break;
        }
    }

    // ==========================================
    // SƠN TINH ABILITIES (Server Logic)
    // ==========================================

    private void SpawnEarthWall()
    {
        if (earthWallPrefab == null)
        {
            Debug.LogError("🚨 LỖI: Chưa gán EarthWallPrefab!");
            return;
        }
        NetworkPlayerController enemy = FindEnemyPlayer();
        Vector3 spawnPos = enemy != null
            ? enemy.transform.position
            : transform.position + transform.forward * 2f;
        Quaternion spawnRot = enemy != null
            ? Quaternion.LookRotation(transform.position - spawnPos)
            : transform.rotation;

        GameObject wall = Instantiate(earthWallPrefab, spawnPos, spawnRot);
        wall.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);

        // Núi Lửa: Tường xuất hiện = đất nổ tức thì dưới chân địch
        if (enemy != null)
        {
            Vector3 burstDir = (Vector3.up * 1.5f - (enemy.transform.position - transform.position).normalized * 0.3f).normalized;
            enemy.ApplyKnockback(burstDir * wallBurstForce);
            enemy.ApplyStun(wallBurstStun);
        }
        DebugLogClientRpc("⛰️💥 Sơn Tinh: Tường Vách Núi Nổ!");
    }

    private void SpawnQuicksand()
    {
        if (quicksandPrefab == null)
        {
            Debug.LogError("🚨 LỖI: Chưa gán QuicksandPrefab!");
            return;
        }
        NetworkPlayerController enemy = FindEnemyPlayer();
        Vector3 spawnPos = enemy != null
            ? enemy.transform.position
            : transform.position + transform.forward * 3f;
        GameObject quicksand = Instantiate(quicksandPrefab, spawnPos, Quaternion.identity);
        quicksand.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);

        // Hố Tử Thần: Rối loạn + chậm NGAY LẬP TỨC khi địch bị phủ bức
        if (enemy != null)
        {
            enemy.ApplyConfuse(1.5f);
            enemy.ApplySlow(0.5f, 1.0f);
        }
        DebugLogClientRpc("🕳️⚡ Sơn Tinh: Hố Tử Thần — Rối loạn tức thì!");
    }
    
    /// <summary>
    /// ĐẠI ĐỊ A CHẤN - Phase 2: Cắm xuống tạo 3 vòng sóng chấn động
    /// Vòng trong (2.5m) : tung lên + stun 2.5s
    /// Vòng giữa (5m)   : đẩy ra + stun 1.2s  
    /// Vòng ngoài (8m)   : làm chậm 50% trong 3s
    /// </summary>
    private void SonTinhEarthquakeSlam()
    {
        Vector3 center = transform.position;
        Collider[] all = Physics.OverlapSphere(center, quakeOuterRadius);

        foreach (Collider col in all)
        {
            if (!col.CompareTag("Player")) continue;
            NetworkPlayerController targetPlayer = col.GetComponentInParent<NetworkPlayerController>();
            if (targetPlayer == null || targetPlayer.OwnerClientId == OwnerClientId) continue;

            float dist = Vector3.Distance(center, col.transform.position);

            if (dist <= quakeInnerRadius)
            {
                // Vòng trong: Tung thẳng lên không + stun dài
                targetPlayer.ApplyKnockback(Vector3.up * quakeKnockUp);
                targetPlayer.ApplyStun(quakeInnerStun);
                targetPlayer.ApplySlow(0.4f, 3f);
            }
            else if (dist <= quakeMiddleRadius)
            {
                // Vòng giữa: Đẩy ra xa + stun vừa
                Vector3 pushDir = (col.transform.position - center).normalized;
                pushDir.y = 0.4f;
                targetPlayer.ApplyKnockback(pushDir.normalized * quakeKnockOut);
                targetPlayer.ApplyStun(quakeMiddleStun);
                targetPlayer.ApplySlow(0.55f, 2f);
            }
            else
            {
                // Vòng ngoài: Chậm đơn thuần
                targetPlayer.ApplySlow(0.5f, 3f);
            }
        }

        // Spawn EarthWall tại địch + thêm tường phụ 2 bên để bắt cầm toàn diện
        if (earthWallPrefab != null)
        {
            NetworkPlayerController enemy = FindEnemyPlayer();
            if (enemy != null)
            {
                Vector3 ePos = enemy.transform.position;
                // Tường chính: giữa
                Quaternion wallRot = Quaternion.LookRotation(ePos - center);
                GameObject wall0 = Instantiate(earthWallPrefab, ePos, wallRot);
                wall0.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
                // Tường phụ: trái + phải (xoay 90°)
                Vector3 right = Quaternion.Euler(0, 90, 0) * wallRot * Vector3.forward * 1.5f;
                GameObject wall1 = Instantiate(earthWallPrefab, ePos + right,  Quaternion.LookRotation(right));
                wall1.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
                GameObject wall2 = Instantiate(earthWallPrefab, ePos - right, Quaternion.LookRotation(-right));
                wall2.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
                // Thêm stun toàn cầu sau slam: địch dù xa vẫn bị stun nhẹ
                enemy.ApplyStun(0.8f);
                enemy.ApplySlow(0.45f, 3f);
            }
        }

        DebugLogClientRpc("⛰️💥 ĐẠI ĐỊ A CHẤN: Ba vòng sóng chấn nổ ra!");
        PlaySoundClientRpc(7); // sfx_sonRSlam (tiếng cắm xuống)
    }

    // ==========================================
    // THỦY TINH ABILITIES (Server Logic)
    // ==========================================

    private void SpawnWhirlpool()
    {
        if (whirlpoolPrefab != null)
        {
            // Option 1: Spawn TẠI vị trí địch
            NetworkPlayerController enemy = FindEnemyPlayer();
            Vector3 spawnPos = enemy != null
                ? enemy.transform.position
                : transform.position + transform.forward * 2f;
            GameObject whirlpool = Instantiate(whirlpoolPrefab, spawnPos, transform.rotation);
            whirlpool.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
            DebugLogClientRpc("🌀 Thủy Tinh: Lốc Xoáy xuất hiện bên địch!");
        }
        else
        {
            Debug.LogError("🚨 LỖI: Chưa gán WhirlpoolPrefab cho nhân vật Thủy Tinh trong Inspector!");
        }
    }

    private void SpawnWaterWave()
    {
        if (waterWavePrefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            NetworkPlayerController enemy = FindEnemyPlayer();
            Vector3 direction = enemy != null
                ? (enemy.transform.position - spawnPos).normalized
                : transform.forward;
            Quaternion spawnRot = Quaternion.LookRotation(direction);
            GameObject wave = Instantiate(waterWavePrefab, spawnPos, spawnRot);
            var waveScript = wave.GetComponent<WaterWave>();
            if (waveScript != null) waveScript.Initialize(transform.position, OwnerClientId);
            wave.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
            DebugLogClientRpc("🌊 Thủy Tinh: Sóng bay về phía địch!");
        }
        else
        {
            Debug.LogError("🚨 LỖI: Chưa gán WaterWavePrefab cho nhân vật Thủy Tinh trong Inspector!");
        }
    }

    /// <summary>
    /// Phase 2 & 3 của Thủy Long Đột Kích:
    /// - Kiểm tra địch trong vùng trước mặt (cone 120°)
    /// - Nếu trúng: tung địch lên không + stun + slow
    /// - Spawn Whirlpool tại điểm va chạm để kéo địch xuống
    /// </summary>
    private void ThuyTinhUltimateImpact()
    {
        // Vùng phát hiện: hình cầu trước mặt caster
        Vector3 checkCenter = transform.position + transform.forward * (ultimateImpactRadius * 0.6f);
        Collider[] hits = Physics.OverlapSphere(checkCenter, ultimateImpactRadius);

        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            NetworkPlayerController enemy = col.GetComponentInParent<NetworkPlayerController>();
            if (enemy == null || enemy.OwnerClientId == OwnerClientId) continue;

            // Kiểm tra địch có trong vùng cone 120° phía trước không
            Vector3 toEnemy = (enemy.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, toEnemy);
            if (angle > 60f) continue;

            // ✦ Phase 2: Tung địch lên cao + đẩy ra sau
            Vector3 launchDir = (transform.forward * 0.4f + Vector3.up * 1.6f).normalized;
            enemy.ApplyKnockback(launchDir * ultimateLaunchForce);
            enemy.ApplyStun(ultimateStunDuration);
            enemy.ApplySlow(0.45f, 2.5f); // Làm chậm 55% sau khi đáp xuống

            // ✦ Phase 3: Spawn Whirlpool tại điểm địch đang đứng
            if (whirlpoolPrefab != null)
            {
                GameObject vortex = Instantiate(whirlpoolPrefab, enemy.transform.position, Quaternion.identity);
                vortex.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
            }

            DebugLogClientRpc("🌊💥 Thủy Long: Địch bị tung lên không trung & bị hút vào vũng xoáy!");
            PlaySoundClientRpc(6); // sfx_tuyRImpact (tiếng va chạm)
            break; // Single-target ultimate
        }
    }

    // ==========================================
    // UTILITIES
    // ==========================================

    /// <summary>
    /// Phát âm thanh trên tất cả clients. Sound ID:
    /// 0=sonE, 1=sonQ, 2=sonR, 3=tuyE, 4=tuyQ, 5=tuyR, 6=tuyRImpact, 7=sonRSlam
    /// </summary>
    [ClientRpc]
    private void PlaySoundClientRpc(int soundId)
    {
        AudioClip clip = soundId switch
        {
            0 => sfx_sonE,
            1 => sfx_sonQ,
            2 => sfx_sonR,
            3 => sfx_tuyE,
            4 => sfx_tuyQ,
            5 => sfx_tuyR,
            6 => sfx_tuyRImpact,
            7 => sfx_sonRSlam,
            _ => null
        };

        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, sfxVolume);
    }

    [ClientRpc]
    private void DebugLogClientRpc(string msg)
    {
        Debug.Log(msg);
    }

    // Public getters for UI
    public float GetAbilityE_CD() => Mathf.Max(0, abilityE_Timer);
    public float GetAbilityQ_CD() => Mathf.Max(0, abilityQ_Timer);
    public float GetAbilityR_CD() => Mathf.Max(0, abilityR_Timer);
    public CharacterType GetCharacterType() => characterType;
}
