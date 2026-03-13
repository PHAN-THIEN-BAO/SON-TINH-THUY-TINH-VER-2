using UnityEngine;
using UnityEngine.Video;
using System.Collections;

public class PlayVideoIntro : MonoBehaviour
{
    [Header("Video Settings")]
    public VideoClip videoClip;
    
    [Header("Audio Settings")]
    public AudioClip audioClip;
    
    [Header("Canvas Settings")]
    [Tooltip("Canvas sẽ ẩn khi video chạy và hiện lại sau khi video kết thúc")]
    public CanvasGroup canvasGroup;
    
    [Tooltip("Nếu không gán CanvasGroup, script sẽ tự tìm Canvas trong scene")]
    public bool autoFindCanvas = true;
    
    [Tooltip("Thời gian delay trước khi Canvas hiện lại (giây)")]
    public float delayBeforeShowCanvas = 2f;
    
    [Tooltip("Thời gian fade in của Canvas (giây)")]
    public float canvasFadeInDuration = 0.8f;
    
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private Camera mainCamera;
    private bool isPlaying = false;
    private bool hasPlayed = false; // Đảm bảo chỉ phát một lần

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Lấy hoặc thêm VideoPlayer component
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }
        
        // Thiết lập VideoPlayer - không loop
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false; // Đảm bảo không lặp lại
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        
        // Lấy hoặc thêm AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.playOnAwake = false;
        audioSource.loop = false; // Đảm bảo không lặp lại
        
        // Lấy Camera component
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        // Tự động tìm CanvasGroup nếu chưa được gán
        if (canvasGroup == null && autoFindCanvas)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                canvasGroup = canvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    // Tự động thêm CanvasGroup nếu Canvas chưa có
                    canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                    Debug.Log("Đã tự động thêm CanvasGroup vào Canvas.");
                }
                Debug.Log("Đã tìm thấy Canvas và CanvasGroup.");
            }
            else
            {
                Debug.LogWarning("Không tìm thấy Canvas trong scene!");
            }
        }
        
        // Đăng ký sự kiện khi video kết thúc
        videoPlayer.loopPointReached += OnVideoFinished;
        
        // Ẩn Canvas khi bắt đầu (nếu có)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        // Tự động phát video và audio khi Start (chỉ một lần)
        if (!hasPlayed)
        {
            StartCoroutine(PrepareAndPlayVideoAndAudio());
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Có thể nhấn Space để phát lại nếu cần (tùy chọn - có thể bỏ)
        // Bỏ comment dòng dưới nếu muốn chặn không cho phát lại
        /*
        if (Input.GetKeyDown(KeyCode.Space) && !isPlaying && !hasPlayed)
        {
            StartCoroutine(PrepareAndPlayVideoAndAudio());
        }
        */
    }

    private IEnumerator PrepareAndPlayVideoAndAudio()
    {
        // Ngăn không cho phát lại nếu đã phát rồi
        if (hasPlayed)
        {
            Debug.Log("Video và audio đã được phát một lần rồi.");
            yield break;
        }
        
        if (videoClip == null)
        {
            Debug.LogWarning("Video clip chưa được gán!");
            yield break;
        }

        hasPlayed = true; // Đánh dấu đã phát
        
        // Đảm bảo Canvas bị ẩn khi video bắt đầu
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        // Thiết lập video clip
        videoPlayer.clip = videoClip;
        
        // Prepare video trước (load video vào bộ nhớ)
        videoPlayer.Prepare();
        
        // Đợi cho video sẵn sàng
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }
        
        Debug.Log("Video đã sẵn sàng, bắt đầu phát đồng bộ...");
        
        // Bây giờ phát cả video và audio cùng lúc
        isPlaying = true;
        videoPlayer.Play();
        
        // Phát audio ngay sau khi video play
        if (audioClip != null)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("Audio clip chưa được gán!");
        }
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        // Video đã kết thúc, trở về camera bình thường
        videoPlayer.Stop();
        isPlaying = false;
        
        Debug.Log("Video đã phát xong. Camera trở về bình thường.");
        
        // Audio sẽ tiếp tục phát nếu còn (không dừng audioSource)
        if (audioSource.isPlaying)
        {
            Debug.Log("Audio vẫn đang phát...");
        }
        
        // Bắt đầu hiển thị Canvas với delay và fade in
        if (canvasGroup != null)
        {
            StartCoroutine(ShowCanvasWithDelay());
        }
    }
    
    private IEnumerator ShowCanvasWithDelay()
    {
        // Delay trước khi hiển thị Canvas
        Debug.Log($"Đợi {delayBeforeShowCanvas} giây trước khi hiển thị Canvas...");
        yield return new WaitForSeconds(delayBeforeShowCanvas);
        
        Debug.Log("Bắt đầu fade in Canvas...");
        
        // Bật tương tác cho Canvas
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        
        // Fade in Canvas từ alpha 0 đến 1
        float elapsedTime = 0f;
        while (elapsedTime < canvasFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / canvasFadeInDuration);
            yield return null;
        }
        
        // Đảm bảo alpha = 1 khi kết thúc
        canvasGroup.alpha = 1f;
        Debug.Log("Canvas đã hiển thị hoàn toàn.");
        
        // Thông báo cho MultiplayerUIManager hiện intro panel
        if (MultiplayerUIManager.Instance != null)
        {
            MultiplayerUIManager.Instance.ShowIntroAfterVideo();
            Debug.Log("✅ Đã gọi MultiplayerUIManager.ShowIntroAfterVideo()");
        }
        else
        {
            Debug.LogWarning("⚠️ Không tìm thấy MultiplayerUIManager.Instance");
        }
    }

    public void PlayVideoAndAudio()
    {
        // Method này để có thể gọi từ bên ngoài nếu cần
        StartCoroutine(PrepareAndPlayVideoAndAudio());
    }

    void OnDestroy()
    {
        // Hủy đăng ký sự kiện
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}
