using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

/// <summary>
/// Quản lý Settings Panel: âm thanh, độ phân giải, độ nhạy chuột, ngắt kết nối.
/// Gắn script này lên cùng GameObject với MultiplayerUIManager.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;   // Panel Settings (tạo trong Unity)

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;  // 0–1
    [SerializeField] private Slider musicVolumeSlider;   // 0–1
    [SerializeField] private Slider sfxVolumeSlider;     // 0–1
    [SerializeField] private AudioMixer audioMixer;      // Gán AudioMixer nếu có (không bắt buộc)
    [SerializeField] private TMP_Text masterVolumeLabel;
    [SerializeField] private TMP_Text musicVolumeLabel;
    [SerializeField] private TMP_Text sfxVolumeLabel;

    [Header("Resolution")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Mouse Sensitivity")]
    [SerializeField] private Slider sensitivitySlider;   // 0.01 – 1.0
    [SerializeField] private TMP_Text sensitivityLabel;

    [Header("Disconnect (chuyển từ GamePanel)")]
    [SerializeField] private Button disconnectButton;    // Nút ngắt kết nối trong Settings

    [Header("Buttons")]
    [SerializeField] private Button closeSettingsButton; // Nút đóng Settings
    [SerializeField] private Button openSettingsButton;  // Nút mở Settings (gán từ GamePanel hoặc Intro)

    // ==========================================
    // Hằng số lưu PlayerPrefs
    // ==========================================
    private const string KEY_MASTER  = "vol_master";
    private const string KEY_MUSIC   = "vol_music";
    private const string KEY_SFX     = "vol_sfx";
    private const string KEY_SENS    = "sensitivity";
    private const string KEY_FULL    = "fullscreen";
    private const string KEY_RES_IDX = "resolution_idx";

    // Giá trị nhạy chuột được đọc bởi NetworkPlayerController
    public static float MouseSensitivity { get; private set; } = 0.1f;

    private Resolution[] _resolutions;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        LoadResolutions();
        LoadSavedSettings();
        SetupListeners();

        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void Update()
    {
        // Nhấn ESC để mở / đóng Settings
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
                CloseSettings();
            else
                OpenSettings();
        }
    }

    // ==========================================
    // SETUP
    // ==========================================
    private void LoadResolutions()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<string>();
        int currentIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            options.Add($"{_resolutions[i].width} x {_resolutions[i].height}");
            if (_resolutions[i].width  == Screen.currentResolution.width &&
                _resolutions[i].height == Screen.currentResolution.height)
                currentIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        int savedIdx = PlayerPrefs.GetInt(KEY_RES_IDX, currentIndex);
        resolutionDropdown.value = savedIdx;
        resolutionDropdown.RefreshShownValue();
    }

    private void LoadSavedSettings()
    {
        // Volume
        float master = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float music  = PlayerPrefs.GetFloat(KEY_MUSIC,  0.8f);
        float sfx    = PlayerPrefs.GetFloat(KEY_SFX,    1f);

        if (masterVolumeSlider != null) { masterVolumeSlider.value = master; ApplyMasterVolume(master); }
        if (musicVolumeSlider  != null) { musicVolumeSlider.value  = music;  ApplyMusicVolume(music);   }
        if (sfxVolumeSlider    != null) { sfxVolumeSlider.value    = sfx;    ApplySfxVolume(sfx);       }

        // Sensitivity
        float sens = PlayerPrefs.GetFloat(KEY_SENS, 0.1f);
        if (sensitivitySlider != null) { sensitivitySlider.value = sens; ApplySensitivity(sens); }

        // Fullscreen
        bool fullscreen = PlayerPrefs.GetInt(KEY_FULL, 1) == 1;
        if (fullscreenToggle != null) { fullscreenToggle.isOn = fullscreen; Screen.fullScreen = fullscreen; }

        UpdateAllLabels();
    }

    private void SetupListeners()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(v => { ApplyMasterVolume(v); UpdateAllLabels(); });
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(v => { ApplyMusicVolume(v); UpdateAllLabels(); });
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(v => { ApplySfxVolume(v); UpdateAllLabels(); });
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(v => { ApplySensitivity(v); UpdateAllLabels(); });
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(ApplyFullscreen);

        // Buttons
        if (openSettingsButton  != null) openSettingsButton.onClick.AddListener(OpenSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
        if (disconnectButton    != null) disconnectButton.onClick.AddListener(OnDisconnect);
    }

    // ==========================================
    // OPEN / CLOSE
    // ==========================================
    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);

        // Unlock cursor khi mở settings
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void CloseSettings()
    {
        SaveSettings();
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Lock lại cursor nếu đang chơi game
        if (MultiplayerUIManager.Instance != null && MultiplayerUIManager.Instance.IsInGame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ==========================================
    // APPLY SETTINGS
    // ==========================================
    private void ApplyMasterVolume(float value)
    {
        AudioListener.volume = value;
        if (audioMixer != null)
            audioMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    private void ApplyMusicVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
        // Nếu không có AudioMixer, chỉnh thẳng AudioSource của nhạc nền nếu cần
    }

    private void ApplySfxVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    private void ApplySensitivity(float value)
    {
        MouseSensitivity = value;
        // Cập nhật trực tiếp vào NetworkPlayerController nếu đang chơi
        var player = FindFirstObjectByType<NetworkPlayerController>();
        if (player != null) player.mouseSensitivity = value;
    }

    private void ApplyResolution(int index)
    {
        if (_resolutions == null || index >= _resolutions.Length) return;
        Resolution res = _resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }

    private void ApplyFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    // ==========================================
    // LABELS
    // ==========================================
    private void UpdateAllLabels()
    {
        if (masterVolumeLabel  != null && masterVolumeSlider  != null)
            masterVolumeLabel.text  = $"Âm lượng: {Mathf.RoundToInt(masterVolumeSlider.value * 100)}%";
        if (musicVolumeLabel   != null && musicVolumeSlider   != null)
            musicVolumeLabel.text   = $"Nhạc nền: {Mathf.RoundToInt(musicVolumeSlider.value * 100)}%";
        if (sfxVolumeLabel     != null && sfxVolumeSlider     != null)
            sfxVolumeLabel.text     = $"Hiệu ứng: {Mathf.RoundToInt(sfxVolumeSlider.value * 100)}%";
        if (sensitivityLabel   != null && sensitivitySlider   != null)
            sensitivityLabel.text   = $"Độ nhạy chuột: {sensitivitySlider.value:F2}";
    }

    // ==========================================
    // SAVE
    // ==========================================
    private void SaveSettings()
    {
        if (masterVolumeSlider  != null) PlayerPrefs.SetFloat(KEY_MASTER,  masterVolumeSlider.value);
        if (musicVolumeSlider   != null) PlayerPrefs.SetFloat(KEY_MUSIC,   musicVolumeSlider.value);
        if (sfxVolumeSlider     != null) PlayerPrefs.SetFloat(KEY_SFX,     sfxVolumeSlider.value);
        if (sensitivitySlider   != null) PlayerPrefs.SetFloat(KEY_SENS,    sensitivitySlider.value);
        if (fullscreenToggle    != null) PlayerPrefs.SetInt(KEY_FULL,      fullscreenToggle.isOn ? 1 : 0);
        if (resolutionDropdown  != null) PlayerPrefs.SetInt(KEY_RES_IDX,   resolutionDropdown.value);
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit() => SaveSettings();

    // ==========================================
    // DISCONNECT (chuyển từ GamePanel)
    // ==========================================
    private void OnDisconnect()
    {
        CloseSettings();

        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.Disconnect();

        if (MultiplayerUIManager.Instance != null)
            MultiplayerUIManager.Instance.OnDisconnectedFromSettings();
    }
}
