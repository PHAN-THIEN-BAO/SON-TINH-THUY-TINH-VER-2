using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayMusicBackGround : MonoBehaviour
{
    [Tooltip("Danh sách các file âm thanh (mp3) có th? kéo th? vào Inspector")]
    public List<AudioClip> playlist = new List<AudioClip>();

    [Tooltip("N?u true: nút này s? d?ng phát nh?c. N?u false: nút này s? b?t ??u phát playlist ? trên.")]
    public bool isStopButton = false;

    [Tooltip("N?u true thì l?p l?i playlist sau khi k?t thúc.")]
    public bool loopPlaylist = false;

    [Range(0f, 1f)]
    public float volume = 1f;

    // Shared audio source so different buttons control the same playback
    private static AudioSource sharedAudioSource;
    private static Coroutine playingCoroutine;
    private static GameObject audioHolder;

    void Awake()
    {
        EnsureAudioSourceExists();
    }

    void EnsureAudioSourceExists()
    {
        if (sharedAudioSource != null)
            return;

        // Create a persistent GameObject to host the AudioSource so audio continues even if button object changes
        audioHolder = new GameObject("GlobalMusicPlayer");
        DontDestroyOnLoad(audioHolder);
        sharedAudioSource = audioHolder.AddComponent<AudioSource>();
        sharedAudioSource.playOnAwake = false;
        sharedAudioSource.loop = false;
        sharedAudioSource.volume = volume;
    }

    // G?i method này t? Button OnClick() trong Inspector
    public void HandleButtonClick()
    {
        if (isStopButton)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    public void StartPlayback()
    {
        if (playlist == null || playlist.Count == 0)
        {
            Debug.LogWarning("PlayMusicBackGround: playlist tr?ng ho?c ch?a ???c gán.");
            return;
        }

        EnsureAudioSourceExists();
        sharedAudioSource.volume = volume;

        // Stop any existing playing coroutine first
        if (playingCoroutine != null)
        {
            StopCoroutine(playingCoroutine);
            playingCoroutine = null;
        }

        playingCoroutine = StartCoroutine(PlayPlaylistCoroutine(playlist, loopPlaylist));
    }

    public void StopPlayback()
    {
        if (playingCoroutine != null)
        {
            StopCoroutine(playingCoroutine);
            playingCoroutine = null;
        }

        if (sharedAudioSource != null && sharedAudioSource.isPlaying)
        {
            sharedAudioSource.Stop();
        }
    }

    private IEnumerator PlayPlaylistCoroutine(List<AudioClip> clips, bool loop)
    {
        if (clips == null || clips.Count == 0)
            yield break;

        do
        {
            for (int i = 0; i < clips.Count; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                    continue;

                sharedAudioSource.clip = clip;
                sharedAudioSource.Play();

                // Wait until clip finishes or until stopped
                float elapsed = 0f;
                while (sharedAudioSource.isPlaying && elapsed < clip.length + 0.1f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Small safety yield
                yield return null;
            }
        } while (loop);

        playingCoroutine = null;
    }
}
