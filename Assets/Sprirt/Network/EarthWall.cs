using Unity.Netcode;
using UnityEngine;

public class EarthWall : NetworkBehaviour
{
    [SerializeField] private float lifetime = 5f;

    [Header("On Impact")]
    [SerializeField] private float knockbackForce = 18f;
    [SerializeField] private float stunDuration = 0.8f;

    [Header("Overlap Detection (bypass CharacterController)")]
    [SerializeField] private Vector3 overlapBoxSize = new Vector3(1f, 2f, 0.5f); // Khớp với kích thước wall prefab
    [SerializeField] private float checkInterval = 0.1f; // Kiểm tra 10 lần/giây, đủ nhanh

    private ulong ownerClientId;
    private float checkTimer = 0f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ownerClientId = OwnerClientId;
            Invoke(nameof(DestroyWall), lifetime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        // Tìm tất cả player đang chồng lên tường
        Collider[] hits = Physics.OverlapBox(
            transform.position,
            overlapBoxSize * 0.5f,
            transform.rotation,
            LayerMask.GetMask("Default") // Đổi thành Layer của Player nếu cần
        );

        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player")) continue;

            NetworkPlayerController player = col.GetComponentInParent<NetworkPlayerController>();
            if (player == null || player.OwnerClientId == ownerClientId) continue;

            // Hướng đẩy: từ tâm tường ra phía địch
            Vector3 pushDir = (col.transform.position - transform.position);
            pushDir.y = 0.3f; // Nảy lên nhẹ
            pushDir.Normalize();

            player.ApplyKnockback(pushDir * knockbackForce);
            player.ApplyStun(stunDuration);

            Debug.Log($"⛰️ EarthWall: Đẩy địch ra ngoài + stun {stunDuration}s!");
        }
    }

    private void DestroyWall()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
