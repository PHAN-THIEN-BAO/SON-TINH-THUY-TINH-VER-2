using Unity.Netcode;
using UnityEngine;

public class Quicksand : NetworkBehaviour
{
    [Header("Zone Duration")]
    [SerializeField] private float lifetime = 3.5f;

    [Header("Rối Loạn Điều Khiển")]
    [SerializeField] private float confuseDuration = 2.5f;    // Thời gian đảo input
    [SerializeField] private float confuseCooldown = 2.8f;    // Áp lại sau bao lâu nếu vẫn đứng trong
    
    [Header("Slow đi kèm")]
    [SerializeField] private float slowMultiplier = 0.5f;     // 50% tốc độ
    [SerializeField] private float slowDuration = 0.4f;       // Làm chậm liên tục

    [Header("Overlap Detection")]
    [SerializeField] private float zoneRadius = 2.5f;         // Bán kính vùng cát lún
    [SerializeField] private float checkInterval = 0.15f;

    private ulong ownerClientId;
    private float checkTimer = 0f;
    private float lastConfuseTime = -99f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ownerClientId = OwnerClientId;
            Invoke(nameof(DestroyQuicksand), lifetime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        // Kiểm tra player trong vùng cát lún
        Collider[] hits = Physics.OverlapSphere(transform.position, zoneRadius);
        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            NetworkPlayerController player = col.GetComponentInParent<NetworkPlayerController>();
            if (player == null || player.OwnerClientId == ownerClientId) continue;

            // Slow liên tục khi đứng trong vùng
            player.ApplySlow(slowMultiplier, slowDuration);

            // Rối loạn điều khiển: áp dụng lần đầu và lặp lại nếu vẫn đứng trong
            if (Time.time >= lastConfuseTime + confuseCooldown)
            {
                player.ApplyConfuse(confuseDuration);
                lastConfuseTime = Time.time;
                Debug.Log($"🕳️ Quicksand: Địch bị rối loạn điều khiển {confuseDuration}s!");
            }
        }
    }

    private void DestroyQuicksand()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
