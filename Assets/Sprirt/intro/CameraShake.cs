using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [Header("Float Settings")]
    [Tooltip("T?c ?? dao ??ng (càng cao càng nhanh)")]
    public float floatSpeed = 1f;
    
    [Tooltip("C??ng ?? dao ??ng v? trí (càng cao càng m?nh)")]
    public float floatAmplitude = 0.1f;
    
    [Tooltip("C??ng ?? dao ??ng xoay (??)")]
    public float rotationAmplitude = 1f;
    
    [Header("Wave Pattern")]
    [Tooltip("T?n s? sóng X")]
    public float waveFrequencyX = 2.4f;
    
    [Tooltip("T?n s? sóng Y")]
    public float waveFrequencyY = 1.6f;
    
    [Tooltip("T?n s? sóng Z")]
    public float waveFrequencyZ = 3f;
    
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float timeOffset;

    void Start()
    {
        // L?u v? trí và rotation ban ??u
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        
        // Offset ng?u nhiên ?? m?i camera có pattern khác nhau
        timeOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        // Tính toán th?i gian v?i offset
        float time = (Time.time + timeOffset) * floatSpeed;
        
        // Dao ??ng v? trí theo 3 tr?c (mô ph?ng sóng n??c)
        float offsetX = Mathf.Sin(time * waveFrequencyX) * floatAmplitude;
        float offsetY = Mathf.Sin(time * waveFrequencyY) * floatAmplitude * 0.5f; // Y nh? h?n (lên xu?ng)
        float offsetZ = Mathf.Cos(time * waveFrequencyZ) * floatAmplitude * 0.7f;
        
        Vector3 floatOffset = new Vector3(offsetX, offsetY, offsetZ);
        transform.localPosition = originalPosition + floatOffset;
        
        // Dao ??ng rotation nh? (nghiêng nh? trên n??c)
        float rotX = Mathf.Sin(time * waveFrequencyX * 0.8f) * rotationAmplitude;
        float rotZ = Mathf.Cos(time * waveFrequencyZ * 0.6f) * rotationAmplitude;
        
        Quaternion floatRotation = Quaternion.Euler(rotX, 0f, rotZ);
        transform.localRotation = originalRotation * floatRotation;
    }
    
    // Ph??ng th?c ?? reset v? v? trí ban ??u n?u c?n
    public void ResetToOriginal()
    {
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
    }
    
    // Ph??ng th?c ?? b?t/t?t hi?u ?ng
    public void SetFloatEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled)
        {
            ResetToOriginal();
        }
    }
}
