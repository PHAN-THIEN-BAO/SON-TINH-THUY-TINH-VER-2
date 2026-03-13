using UnityEngine;

/// <summary>
/// Đảm bảo NetworkManager GameObject không bị destroy khi chuyển scene
/// Add script này vào NetworkManager GameObject (parent)
/// </summary>
public class PersistentNetworkManager : MonoBehaviour
{
    private void Awake()
    {
        // Đảm bảo GameObject này và tất cả children không bị destroy
        DontDestroyOnLoad(gameObject);
        Debug.Log("✅ NetworkManager set to DontDestroyOnLoad");
    }
}
