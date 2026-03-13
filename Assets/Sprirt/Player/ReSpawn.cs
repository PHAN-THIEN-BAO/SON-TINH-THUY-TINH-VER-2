using UnityEngine;

public class ReSpawn : MonoBehaviour
{
    [Tooltip("The names to match in the player's hierarchy (root or any child). Also requires the collided object to have tag 'Player'.")]
    public string[] requiredNames = new string[] { "Player" };

    [Tooltip("Manual respawn position used for all scenes (set in the inspector)")]
    public Vector3 respawnPosition = Vector3.zero;

    private void Reset()
    {
        if (requiredNames == null || requiredNames.Length == 0)
            requiredNames = new string[] { "Player" };
    }

    // If the collider on this object is a trigger
    private void OnTriggerEnter(Collider other)
    {
        TryRespawn(other.gameObject);
    }

    // If the collider on this object is not a trigger
    private void OnCollisionEnter(Collision collision)
    {
        TryRespawn(collision.gameObject);
    }

    private void TryRespawn(GameObject other)
    {
        if (other == null) return;

        // Require the other object to have the Player tag
        if (!other.CompareTag("Player")) return;

        // Check if any of the required names exists on the object, any of its children, or parents
        if (!HasAnyNameInHierarchy(other, requiredNames)) return;

        // Teleport the root of the collided object (so entire player GameObject moves)
        Transform root = other.transform.root ?? other.transform;
        root.position = respawnPosition;

        // Reset Rigidbody velocities if present on root or any child
        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = root.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // If there's a CharacterController, briefly disable to avoid unwanted movement after teleport
        var cc = root.GetComponent<CharacterController>();
        if (cc == null) cc = root.GetComponentInChildren<CharacterController>();
        if (cc != null)
        {
            // disable and re-enable next frame to ensure internal state resets
            cc.enabled = false;
            StartCoroutine(ReenableCharacterControllerNextFrame(cc));
        }
    }

    private System.Collections.IEnumerator ReenableCharacterControllerNextFrame(CharacterController cc)
    {
        yield return null;
        if (cc != null) cc.enabled = true;
    }

    private bool HasAnyNameInHierarchy(GameObject go, string[] names)
    {
        if (names == null || names.Length == 0) return false;

        // Check root
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            if (go.name == n) return true;
        }

        // Check children recursively
        foreach (Transform child in go.transform)
        {
            if (HasNameInChildren(child, names)) return true;
        }

        // Check parents
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                if (parent.name == n) return true;
            }
            parent = parent.parent;
        }

        return false;
    }

    private bool HasNameInChildren(Transform t, string[] names)
    {
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            if (t.name == n) return true;
        }
        foreach (Transform child in t)
        {
            if (HasNameInChildren(child, names)) return true;
        }
        return false;
    }
}
