using UnityEngine;

public class TeleportOnTouch : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask playerLayers = ~0;
    [SerializeField] private float reTeleportDelay = 0.25f;
    public Transform teleportPoint;

    private Collider trapCollider;
    private float nextTeleportTime;

    private void Awake()
    {
        trapCollider = GetComponent<Collider>();

        if (teleportPoint == null)
        {
            Debug.LogWarning($"{name}: Teleport Point is not assigned.", this);
        }

        if (trapCollider != null && !trapCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: Collider should have Is Trigger enabled for OnTriggerEnter.", this);
        }

        if (trapCollider == null)
        {
            Debug.LogWarning($"{name}: Missing collider on trap object.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTeleport(other);
    }

    private void FixedUpdate()
    {
        if (teleportPoint == null || trapCollider == null || Time.time < nextTeleportTime)
        {
            return;
        }

        Bounds bounds = trapCollider.bounds;
        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            bounds.extents * 0.95f,
            trapCollider.transform.rotation,
            playerLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            if (TryTeleport(hits[i]))
            {
                break;
            }
        }
    }

    private bool TryTeleport(Collider other)
    {
        if (teleportPoint == null || other == null || Time.time < nextTeleportTime)
        {
            return false;
        }

        Transform target = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;

        if (!target.CompareTag(playerTag))
        {
            return false;
        }

        target.position = teleportPoint.position;
        nextTeleportTime = Time.time + reTeleportDelay;
        return true;
    }
}