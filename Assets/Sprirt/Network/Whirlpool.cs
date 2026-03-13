using Unity.Netcode;
using UnityEngine;

public class Whirlpool : NetworkBehaviour
{
    [SerializeField] private float lifetime = 4f;

    [Header("Pull toward center")]
    [SerializeField] private float pullRadius = 7f;       // Bán kính hút
    [SerializeField] private float pullForce = 14f;       // Lực hút về tâm
    [SerializeField] private float pullInterval = 0.12f;  // Hút mỗi 0.12s

    [Header("Stun khi vào sâu tâm")]
    [SerializeField] private float stunRadius = 2f;       // Bán kính stun (vùng trong cùng)
    [SerializeField] private float stunDuration = 0.8f;
    [SerializeField] private float stunCooldown = 1.5f;

    private ulong ownerClientId;
    private float pullTimer = 0f;
    private float lastStunTime = -99f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ownerClientId = OwnerClientId;
            Invoke(nameof(DestroyWhirlpool), lifetime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        pullTimer += Time.deltaTime;
        if (pullTimer < pullInterval) return;
        pullTimer = 0f;

        // Tìm tất cả player trong bán kính hút
        Collider[] hits = Physics.OverlapSphere(transform.position, pullRadius);
        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            NetworkPlayerController player = col.GetComponentInParent<NetworkPlayerController>();
            if (player == null || player.OwnerClientId == ownerClientId) continue;

            Vector3 toCenter = transform.position - col.transform.position;
            float dist = toCenter.magnitude;

            if (dist < 0.1f) continue; // Đã ở tâm rồi

            // Lực hút tăng dần khi càng gần tâm (tạo cảm giác xoáy)
            float forceMagnitude = pullForce * (1f + (pullRadius - dist) / pullRadius);
            Vector3 pullDir = toCenter.normalized;
            pullDir.y = 0.05f; // Hút ngang, chỉ nhấc nhẹ

            player.ApplyKnockback(pullDir * forceMagnitude);

            // Stun thêm khi địch bị hút vào tâm
            if (dist <= stunRadius && Time.time >= lastStunTime + stunCooldown)
            {
                player.ApplyStun(stunDuration);
                lastStunTime = Time.time;
                Debug.Log($"🌀 Whirlpool: Địch vào vùng tâm, bị stun {stunDuration}s!");
            }
        }
    }

    private void DestroyWhirlpool()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
