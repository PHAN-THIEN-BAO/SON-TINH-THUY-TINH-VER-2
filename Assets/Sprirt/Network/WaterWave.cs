using Unity.Netcode;
using UnityEngine;

public class WaterWave : NetworkBehaviour
{
    [SerializeField] private float moveSpeed    = 15f;
    [SerializeField] private float lifetime     = 3f;

    [Header("Kéo về phía caster")]
    [SerializeField] private float pullForce    = 25f;  // Lực kéo về phía caster
    [SerializeField] private float pullUpward   = 0.15f; // Lực đẩy nhẹ lên trên

    [Header("Làm chậm khi bị kéo")]
    [SerializeField] private float slowMultiplier = 0.6f; // Giảm còn 60% tốc độ
    [SerializeField] private float slowDuration   = 1.2f;

    private ulong   ownerClientId;
    private Vector3 spawnPosition; // Vị trí caster khi bắn sóng (để tính hướng kéo)

    /// <summary>Gọi từ CharacterAbility sau khi Instantiate để truyền vị trí caster.</summary>
    public void Initialize(Vector3 casterPosition, ulong casterId)
    {
        spawnPosition  = casterPosition;
        ownerClientId  = casterId;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ownerClientId = OwnerClientId;
            // Nếu Initialize chưa được gọi, fallback về vị trí hiện tại
            if (spawnPosition == Vector3.zero)
                spawnPosition = transform.position;
            Invoke(nameof(DestroyWave), lifetime);
        }
    }

    private void Update()
    {
        // Chạy trên TẤT CẢ clients để thấy animation di chuyển
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    private void DestroyWave()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (!other.CompareTag("Player")) return;

        NetworkPlayerController player = other.GetComponentInParent<NetworkPlayerController>();
        if (player == null || player.OwnerClientId == ownerClientId) return;

        // Hướng kéo: từ vị trí địch về phía caster (ngược chiều sóng)
        Vector3 towardCaster = (spawnPosition - player.transform.position);
        towardCaster.y = 0f;
        towardCaster   = towardCaster.normalized;
        towardCaster.y = pullUpward; // Nhấc nhẹ lên để không cắm xuống đất

        player.ApplyKnockback(towardCaster * pullForce);
        player.ApplySlow(slowMultiplier, slowDuration); // Làm chậm thêm khi bị cuốn vào sóng

        Debug.Log($"🌊 WaterWave: Kéo địch về phía caster, lực {pullForce}!");

        // Sóng tự hủy sau khi trúng địch
        DestroyWave();
    }
}
