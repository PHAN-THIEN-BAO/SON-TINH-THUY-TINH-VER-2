using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class ShowUI : MonoBehaviour
{
    // Singleton ?? tránh duplicate khi s? d?ng DontDestroyOnLoad
    private static ShowUI instance;

    // Button và Panel có th? kéo th? trong Inspector
    [SerializeField] private Button button;
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private float delay = 3f;

    // Tùy ch?n: tên object ?? t? tìm l?i sau khi load scene (n?u b?n không kéo th? reference m?i scene)
    [SerializeField] private string panelObjectName = "IntroPanel";
    [SerializeField] private string buttonObjectName = "StartButton";

    private bool isShowing = false;
    private Coroutine showCoroutine;

    void Awake()
    {
        // Thi?t l?p singleton: n?u ?ã có instance thì h?y b?n m?i
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("ShowUI: created singleton instance and marked DontDestroyOnLoad: " + gameObject.name);
        }
        else if (instance != this)
        {
            Debug.Log("ShowUI: duplicate instance detected, destroying: " + gameObject.name);
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // ??ng ký listener an toàn
        RegisterButtonListener();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterButtonListener();
    }

    void Start()
    {
        // ?n panel ban ??u n?u ?ã gán
        if (uiPanel != null)
            uiPanel.SetActive(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("ShowUI: Scene loaded: " + scene.name + ". Rebinding UI references...");

        // N?u reference panel ho?c button b? null (b? destroy khi load), tìm l?i theo tên
        if (uiPanel == null && !string.IsNullOrEmpty(panelObjectName))
        {
            var found = FindInSceneByName(panelObjectName);
            if (found != null)
            {
                uiPanel = found;
                Debug.Log("ShowUI: Found panel by name: " + panelObjectName);
            }
            else
            {
                Debug.LogWarning("ShowUI: Could not find panel with name: " + panelObjectName);
            }
        }

        if (button == null && !string.IsNullOrEmpty(buttonObjectName))
        {
            var btnObj = FindInSceneByName(buttonObjectName);
            if (btnObj != null)
            {
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    button = btn;
                    Debug.Log("ShowUI: Found button by name: " + buttonObjectName);
                }
                else
                {
                    Debug.LogWarning("ShowUI: Object found but has no Button component: " + buttonObjectName);
                }
            }
            else
            {
                Debug.LogWarning("ShowUI: Could not find button with name: " + buttonObjectName);
            }
        }

        // ?n panel trên scene m?i n?u tìm ???c
        if (uiPanel != null)
            uiPanel.SetActive(false);

        // ??m b?o listener ???c c?p nh?t
        RegisterButtonListener();
    }

    private GameObject FindInSceneByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var activeScene = SceneManager.GetActiveScene();
        var roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var t = FindRecursive(roots[i].transform, name);
            if (t != null)
                return t.gameObject;
        }

        return null;
    }

    private Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }

        return null;
    }

    private void RegisterButtonListener()
    {
        if (button == null) return;
        // Remove then add to avoid duplicate subscriptions
        button.onClick.RemoveListener(OnButtonPressed);
        button.onClick.AddListener(OnButtonPressed);
        Debug.Log("ShowUI: Registered OnButtonPressed to button: " + button.name);
    }

    private void UnregisterButtonListener()
    {
        if (button == null) return;
        button.onClick.RemoveListener(OnButtonPressed);
        Debug.Log("ShowUI: Unregistered OnButtonPressed from button: " + button.name);
    }

    // G?i ph??ng th?c này khi nút ???c nh?n (có th? gán tr?c ti?p t? Inspector ho?c t? ??ng qua Start)
    public void OnButtonPressed()
    {
        Debug.Log("ShowUI: OnButtonPressed called");
        if (isShowing) return;
        showCoroutine = StartCoroutine(ShowPanelAfterDelay());
    }

    private IEnumerator ShowPanelAfterDelay()
    {
        isShowing = true;
        yield return new WaitForSeconds(delay);
        if (uiPanel != null)
        {
            uiPanel.SetActive(true);
            Debug.Log("ShowUI: Panel shown: " + uiPanel.name);
        }
        else
        {
            Debug.LogWarning("ShowUI: uiPanel is null when attempting to show");
        }
        isShowing = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
