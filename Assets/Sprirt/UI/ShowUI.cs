using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ShowUI : MonoBehaviour
{
    // Audio clip to loop. Gán trong Inspector.
    [SerializeField] private AudioClip clip;

    // N?u true s? t? play khi Start
    [SerializeField] private bool playOnStart = true;

    // Âm l??ng (0-1)
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false; // ki?m soát b?ng script
        audioSource.volume = volume;

        if (clip != null)
            audioSource.clip = clip;
    }

    void Start()
    {
        if (playOnStart && audioSource.clip != null)
            audioSource.Play();
    }

    // Public helper ?? g?i t? khác ho?c gán vào UI Button
    public void Play()
    {
        if (audioSource.clip == null) return;
        if (!audioSource.isPlaying) audioSource.Play();
    }

    public void Stop()
    {
        if (audioSource.isPlaying) audioSource.Stop();
    }

    public void SetClip(AudioClip newClip)
    {
        audioSource.clip = newClip;
    }
}
