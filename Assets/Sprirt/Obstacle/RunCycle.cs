using UnityEngine;

public class RunCycle : MonoBehaviour
{
    [Header("Circular Movement Settings")]
    [Tooltip("Bán kính vòng tròn")]
    [SerializeField] private float radius = 5f;
    
    [Tooltip("T?c ?? di chuy?n (vòng/giây)")]
    [SerializeField] private float speed = 1f;
    
    [Tooltip("Góc b?t ??u trong vòng tròn (??)")]
    [SerializeField] private float startAngle = 0f;
    
    [Tooltip("Rotation c?a vòng tròn")]
    [SerializeField] private Vector3 circleRotation = Vector3.zero;
    
    [Tooltip("Tâm vòng tròn")]
    [SerializeField] private Transform centerPoint;
    
    [Header("Optional Settings")]
    [Tooltip("Chi?u di chuy?n (ng??c chi?u kim ??ng h?)")]
    [SerializeField] private bool counterClockwise = true;

    private float currentAngle;
    private Vector3 centerPosition;
    private Quaternion initialRotation; // L?u rotation ban ??u c?a object t? Inspector
    private Quaternion circleRotationQuat; // L?u rotation c?a vòng tròn

    void Start()
    {
        // L?u rotation ban ??u c?a object t? Inspector
        initialRotation = transform.rotation;
        
        // Tính rotation c?a vòng tròn (c? ??nh, không thay ??i)
        circleRotationQuat = Quaternion.Euler(circleRotation);
        
        // N?u không có centerPoint ???c gán, s? d?ng v? trí hi?n t?i làm tâm
        if (centerPoint == null)
        {
            centerPosition = transform.position;
        }
        else
        {
            centerPosition = centerPoint.position;
        }
        
        // Kh?i t?o góc b?t ??u
        currentAngle = startAngle;
        
        // ??t v? trí ban ??u
        UpdatePosition();
    }

    void Update()
    {
        // Tính toán góc m?i d?a trên t?c ??
        float angleSpeed = speed * 360f * Time.deltaTime;
        
        if (counterClockwise)
        {
            currentAngle += angleSpeed;
        }
        else
        {
            currentAngle -= angleSpeed;
        }
        
        // Gi? góc trong kho?ng 0-360
        if (currentAngle >= 360f) currentAngle -= 360f;
        if (currentAngle < 0f) currentAngle += 360f;
        
        // C?p nh?t v? trí
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        // C?p nh?t tâm n?u centerPoint thay ??i
        if (centerPoint != null)
        {
            centerPosition = centerPoint.position;
        }
        
        // Tính toán v? trí m?i trên vòng tròn d?ng ??ng (m?t ph?ng XY)
        float angleInRadians = currentAngle * Mathf.Deg2Rad;
        
        // V? trí trên vòng tròn tr??c khi xoay
        Vector3 localPosition = new Vector3(
            radius * Mathf.Cos(angleInRadians),
            radius * Mathf.Sin(angleInRadians),
            0f
        );
        
        // Áp d?ng rotation c?a vòng tròn cho v? trí
        Vector3 rotatedPosition = circleRotationQuat * localPosition;
        
        transform.position = centerPosition + rotatedPosition;
        
        // Xoay v?t th? theo h??ng di chuy?n
        RotateTowardsMovement();
    }

    private void RotateTowardsMovement()
    {
        // Tính h??ng di chuy?n trên vòng tròn ban ??u (m?t ph?ng XY)
        float angleInRadians = currentAngle * Mathf.Deg2Rad;
        
        Vector3 tangentDirection;
        if (counterClockwise)
        {
            // H??ng ti?p tuy?n theo chi?u ng??c kim ??ng h?
            tangentDirection = new Vector3(-Mathf.Sin(angleInRadians), Mathf.Cos(angleInRadians), 0f);
        }
        else
        {
            // H??ng ti?p tuy?n theo chi?u kim ??ng h?
            tangentDirection = new Vector3(Mathf.Sin(angleInRadians), -Mathf.Cos(angleInRadians), 0f);
        }
        
        // Áp d?ng rotation c?a vòng tròn cho h??ng di chuy?n
        Vector3 worldTangent = circleRotationQuat * tangentDirection;
        
        // Tính rotation ?? v?t th? nhìn theo h??ng di chuy?n
        if (worldTangent.sqrMagnitude > 0.001f)
        {
            // S? d?ng LookRotation ?? v?t th? luôn nhìn theo h??ng ?i
            // Vector3.forward là h??ng m?c ??nh c?a v?t th?
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, worldTangent);
            
            // K?t h?p v?i rotation ban ??u t? Inspector
            transform.rotation = targetRotation * Quaternion.Euler(0, 0, -90f) * initialRotation;
        }
    }

    // V? vòng tròn trong Scene view ?? d? visualize
    private void OnDrawGizmosSelected()
    {
        Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
        
        Gizmos.color = Color.cyan;
        
        // V? vòng tròn
        int segments = 50;
        float angleStep = 360f / segments;
        
        // S? d?ng rotation c?a vòng tròn
        Quaternion gizmoRotation = Quaternion.Euler(circleRotation);
        
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
            
            Vector3 point1 = new Vector3(
                radius * Mathf.Cos(angle1),
                radius * Mathf.Sin(angle1),
                0f
            );
            
            Vector3 point2 = new Vector3(
                radius * Mathf.Cos(angle2),
                radius * Mathf.Sin(angle2),
                0f
            );
            
            // Áp d?ng rotation c?a vòng tròn
            point1 = gizmoRotation * point1 + center;
            point2 = gizmoRotation * point2 + center;
            
            Gizmos.DrawLine(point1, point2);
        }
        
        // V? tâm vòng tròn
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center, 0.2f);
    }
}
