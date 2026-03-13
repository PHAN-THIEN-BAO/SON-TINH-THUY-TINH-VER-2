using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Script tự động xây dựng UI Lobby theo format:
/// - Title "Chọn Nhân Vật" ở trên
/// - 2 thẻ nhân vật (Sơn Tinh - vàng/đất | Thủy Tinh - xanh/nước) đối diện nhau  
/// - "VS" ở giữa
/// - Nút "Chọn" dưới mỗi card
/// - Nút "Bắt đầu" ở giữa
/// - Nút "Hủy" phía dưới cùng
///
/// Cách dùng trong Editor:
///   1. Gắn script này vào Canvas chứa LobbyUIManager
///   2. Gán Sprite cho Son Tinh và Thuy Tinh trong Inspector
///   3. Bấm nút "Build Lobby UI" trong Inspector
///   4. Script sẽ tự xóa UI cũ và tạo UI mới hoàn chỉnh
///   5. Sau khi build xong, tháo script này ra, LobbyUIManager sẽ tự auto-link
/// </summary>
public class LobbyUIAutoSetup : MonoBehaviour
{
    [Header("Sprites (Kéo từ Project vào đây)")]
    [Tooltip("Sprite nhân vật Sơn Tinh (ảnh nhân vật, không phải background card)")]
    public Sprite sonTinhSprite;
    [Tooltip("Sprite nhân vật Thủy Tinh")]
    public Sprite thuyTinhSprite;
    [Tooltip("Sprite background card Sơn Tinh (khung thẻ vàng). Để trống = dùng màu đặc)")]
    public Sprite sonTinhCardFrame;
    [Tooltip("Sprite background card Thủy Tinh (khung thẻ xanh). Để trống = dùng màu đặc)")]
    public Sprite thuyTinhCardFrame;
    [Tooltip("Sprite cho chữ VS ở giữa. Để trống = dùng text)")]
    public Sprite vsSprite;
    [Tooltip("Sprite background tổng thể (núi non/mờ). Để trống = dùng màu tối)")]
    public Sprite backgroundSprite;

    [Header("Font (tuỳ chọn)")]
    [Tooltip("Font chữ phong cách (VD: UTM Vni-Dong Nai, Cinzel...). Để trống = dùng font mặc định)")]
    public TMP_FontAsset titleFont;
    [Tooltip("Font chữ cho nút và text thường")]
    public TMP_FontAsset bodyFont;

    [Header("Màu sắc")]
    public Color sonTinhCardColor = new Color(0.60f, 0.40f, 0.10f, 1f);   // vàng đất
    public Color thuyTinhCardColor = new Color(0.10f, 0.40f, 0.65f, 1f);  // xanh nước
    public Color sonTinhButtonColor = new Color(0.85f, 0.70f, 0.10f, 1f); // vàng
    public Color thuyTinhButtonColor = new Color(0.20f, 0.75f, 0.90f, 1f); // cyan
    public Color startButtonColor = new Color(0.55f, 0.30f, 0.70f, 1f);   // tím
    public Color cancelButtonColor = new Color(0.50f, 0.50f, 0.55f, 1f);  // xám

    // ── Kết quả build ── (được tự gán sau khi build, dùng để link vào LobbyUIManager)
    [Header("─── Auto-generated References (đừng chỉnh tay) ───")]
    public GameObject builtLobbyPanel;
    public Button builtSonTinhButton;
    public Button builtThuyTinhButton;
    public Button builtReadyButton;
    public Button builtCancelButton;
    public Image builtSonTinhPreview;
    public Image builtThuyTinhPreview;
    public GameObject builtSonTinhSelectedIndicator;
    public GameObject builtThuyTinhSelectedIndicator;
    public TextMeshProUGUI builtStatusText;

#if UNITY_EDITOR
    [ContextMenu("Build Lobby UI")]
    public void BuildLobbyUI()
    {
        Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("❌ Không tìm thấy Canvas! Gắn script này vào Canvas.");
            return;
        }

        // Xóa panel cũ nếu có
        Transform oldPanel = canvas.transform.Find("LobbyPanel_Auto");
        if (oldPanel != null)
        {
            DestroyImmediate(oldPanel.gameObject);
            Debug.Log("🗑️ Đã xóa panel cũ");
        }

        // ── ROOT PANEL ──────────────────────────────────────────────────
        GameObject lobbyPanel = CreatePanel("LobbyPanel_Auto", canvas.transform,
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero);
        builtLobbyPanel = lobbyPanel;

        // Background
        Image bgImg = lobbyPanel.AddComponent<Image>();
        bgImg.sprite = backgroundSprite;
        bgImg.color = backgroundSprite != null ? Color.white : new Color(0.07f, 0.05f, 0.12f, 1f);

        // ── TITLE ───────────────────────────────────────────────────────
        GameObject titleObj = CreateText("TitleText", lobbyPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.88f),
            "Chọn Nhân Vật", 52, titleFont ?? bodyFont, new Color(0.98f, 0.93f, 0.75f));

        // Glow/shadow cho title
        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (titleTmp != null)
        {
            titleTmp.fontStyle = FontStyles.Bold;
            // Thêm outline để giống chữ chalk/bảng
            titleTmp.outlineWidth = 0.2f;
            titleTmp.outlineColor = new Color32(40, 20, 5, 200);
        }

        // ── VS CENTER ───────────────────────────────────────────────────
        if (vsSprite != null)
        {
            GameObject vsImgObj = CreateUIObject("VS_Image", lobbyPanel.transform);
            RectTransform vsRect = vsImgObj.GetComponent<RectTransform>();
            SetAnchorCenter(vsRect, new Vector2(0.5f, 0.5f));
            vsRect.sizeDelta = new Vector2(120f, 120f);
            Image vsImg = vsImgObj.AddComponent<Image>();
            vsImg.sprite = vsSprite;
            vsImg.preserveAspect = true;
        }
        else
        {
            GameObject vsTextObj = CreateText("VS_Text", lobbyPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                "VS", 70, titleFont ?? bodyFont, Color.white);
            var vsTmp = vsTextObj.GetComponent<TextMeshProUGUI>();
            if (vsTmp != null)
            {
                vsTmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
                vsTmp.outlineWidth = 0.3f;
                vsTmp.outlineColor = new Color32(150, 50, 0, 255);
            }
        }

        // ── SƠN TINH CARD (BÊN TRÁI) ─────────────────────────────────
        BuildCharacterCard(lobbyPanel.transform,
            isSonTinh: true,
            anchorX: new Vector2(0.08f, 0.46f),
            anchorY: new Vector2(0.12f, 0.85f));

        // ── THỦY TINH CARD (BÊN PHẢI) ────────────────────────────────
        BuildCharacterCard(lobbyPanel.transform,
            isSonTinh: false,
            anchorX: new Vector2(0.54f, 0.92f),
            anchorY: new Vector2(0.12f, 0.85f));

        // ── NÚT BẮT ĐẦU ──────────────────────────────────────────────
        GameObject startBtnObj = CreateButton("StartButton", lobbyPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.10f),
            new Vector2(200f, 50f),
            "Bắt đầu", startButtonColor, bodyFont);
        builtReadyButton = startBtnObj.GetComponent<Button>();

        // Làm tròn góc nút (nếu dùng Image thường)
        var startImg = startBtnObj.GetComponent<Image>();
        if (startImg != null) startImg.type = Image.Type.Sliced;

        // ── NÚT HỦY ──────────────────────────────────────────────────
        GameObject cancelBtnObj = CreateButton("CancelButton", lobbyPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.03f),
            new Vector2(100f, 36f),
            "Hủy", cancelButtonColor, bodyFont);
        builtCancelButton = cancelBtnObj.GetComponent<Button>();

        // ── STATUS TEXT ───────────────────────────────────────────────
        GameObject statusTextObj = CreateText("StatusText", lobbyPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.065f),
            "Chọn nhân vật của bạn", 18, bodyFont, new Color(0.9f, 0.9f, 0.9f, 0.9f));
        // Ẩn status khỏi start chút để nhường chỗ
        var statusRect = statusTextObj.GetComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(500f, 30f);
        builtStatusText = statusTextObj.GetComponent<TextMeshProUGUI>();

        // ── AUTO-LINK VÀO LobbyUIManager ─────────────────────────────
        LobbyUIManager lobbyUI = FindObjectOfType<LobbyUIManager>();
        if (lobbyUI != null)
        {
            SerializedObject so = new SerializedObject(lobbyUI);

            TrySetProp(so, "lobbyPanel",            lobbyPanel);
            TrySetProp(so, "characterSelectionPanel", lobbyPanel);
            TrySetProp(so, "sonTinhButton",         builtSonTinhButton);
            TrySetProp(so, "thuyTinhButton",        builtThuyTinhButton);
            TrySetProp(so, "readyButton",           builtReadyButton);
            TrySetProp(so, "cancelButton",          builtCancelButton);
            TrySetProp(so, "sonTinhPreview",        builtSonTinhPreview);
            TrySetProp(so, "thuyTinhPreview",       builtThuyTinhPreview);
            TrySetProp(so, "sonTinhSelectedIndicator", builtSonTinhSelectedIndicator);
            TrySetProp(so, "thuyTinhSelectedIndicator", builtThuyTinhSelectedIndicator);
            TrySetProp(so, "statusText",            builtStatusText);
            TrySetProp(so, "sonTinhSprite",         sonTinhSprite);
            TrySetProp(so, "thuyTinhSprite",        thuyTinhSprite);

            so.ApplyModifiedProperties();
            Debug.Log("✅ Auto-linked LobbyUIManager references!");
        }
        else
        {
            Debug.LogWarning("⚠️ Không tìm thấy LobbyUIManager trong scene. Hãy link thủ công.");
        }

        // Mark dirty để Unity save scene
        EditorUtility.SetDirty(gameObject);
        Debug.Log("✅ Build hoàn tất! Lobby UI đã được tạo.");
    }

    // ── Tạo card nhân vật ──────────────────────────────────────────────
    private void BuildCharacterCard(Transform parent, bool isSonTinh,
        Vector2 anchorX, Vector2 anchorY)
    {
        string prefix     = isSonTinh ? "SonTinh" : "ThuyTinh";
        string charName   = isSonTinh ? "Sơn\nTinh" : "Thủy\nTinh";
        Color  cardColor  = isSonTinh ? sonTinhCardColor  : thuyTinhCardColor;
        Color  btnColor   = isSonTinh ? sonTinhButtonColor : thuyTinhButtonColor;
        Sprite cardFrame  = isSonTinh ? sonTinhCardFrame  : thuyTinhCardFrame;
        Sprite charSprite = isSonTinh ? sonTinhSprite     : thuyTinhSprite;

        // ── Card container ──────────────
        GameObject card = CreateUIObject(prefix + "Card", parent);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(anchorX.x, anchorY.x);
        cardRect.anchorMax = new Vector2(anchorX.y, anchorY.y);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;

        // Card background
        Image cardImg = card.AddComponent<Image>();
        if (cardFrame != null)
        {
            cardImg.sprite = cardFrame;
            cardImg.type   = Image.Type.Sliced;
            cardImg.color  = Color.white;
        }
        else
        {
            // Màu gradient giả bằng màu đặc
            cardImg.color = cardColor;
        }

        // ── Tên nhân vật (bên ngoài card, phía cạnh) ──
        bool isLeft = isSonTinh;
        GameObject nameLabel = CreateText(prefix + "NameLabel", parent,
            new Vector2(0.5f, 0.5f),
            new Vector2(isLeft ? anchorX.x - 0.10f : anchorX.y + 0.05f, (anchorY.x + anchorY.y) * 0.5f),
            charName, 38, titleFont ?? bodyFont, Color.white);
        var nameTmp = nameLabel.GetComponent<TextMeshProUGUI>();
        if (nameTmp != null)
        {
            nameTmp.fontStyle   = FontStyles.Bold;
            nameTmp.alignment   = isLeft ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            nameTmp.outlineWidth = 0.2f;
            nameTmp.outlineColor = new Color32(20, 10, 5, 200);
        }
        var nameRect = nameLabel.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(110f, 120f);

        // ── Ảnh nhân vật ──
        GameObject previewObj = CreateUIObject(prefix + "Preview", card.transform);
        RectTransform previewRect = previewObj.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.05f, 0.20f);
        previewRect.anchorMax = new Vector2(0.95f, 0.95f);
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero;
        Image previewImg = previewObj.AddComponent<Image>();
        previewImg.sprite = charSprite;
        previewImg.preserveAspect = true;
        if (charSprite == null) previewImg.color = new Color(1, 1, 1, 0.15f);

        // ── "Đã chọn" indicator (highlight frame) ──
        GameObject selectedIndicator = CreateUIObject(prefix + "SelectedIndicator", card.transform);
        RectTransform selRect = selectedIndicator.GetComponent<RectTransform>();
        selRect.anchorMin = Vector2.zero;
        selRect.anchorMax = Vector2.one;
        selRect.offsetMin = new Vector2(-4, -4);
        selRect.offsetMax = new Vector2(4, 4);
        Image selImg = selectedIndicator.AddComponent<Image>();
        selImg.color = isSonTinh
            ? new Color(1f, 0.85f, 0f, 0.6f)   // vàng chói
            : new Color(0f, 0.90f, 1f, 0.6f);   // cyan chói
        selImg.sprite = null;
        selectedIndicator.SetActive(false); // Ẩn mặc định

        if (isSonTinh) builtSonTinhSelectedIndicator = selectedIndicator;
        else           builtThuyTinhSelectedIndicator = selectedIndicator;

        // ── Nút "Chọn" dưới card ──
        // Nút nằm bên ngoài card, phía dưới
        GameObject btnObj = CreateButton(prefix + "Button", parent,
            new Vector2(0.5f, 0.5f),
            new Vector2((anchorX.x + anchorX.y) * 0.5f, anchorY.x - 0.06f),
            new Vector2(140f, 44f),
            "Chọn", btnColor, bodyFont);

        // Assign references
        if (isSonTinh) { builtSonTinhButton = btnObj.GetComponent<Button>(); builtSonTinhPreview = previewImg; }
        else           { builtThuyTinhButton = btnObj.GetComponent<Button>(); builtThuyTinhPreview = previewImg; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private GameObject CreatePanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.pivot      = pivot != Vector2.zero ? pivot : new Vector2(0.5f, 0.5f);
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
        return go;
    }

    private GameObject CreateText(string name, Transform parent,
        Vector2 pivot, Vector2 anchorPos,
        string text, float fontSize, TMP_FontAsset font, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.pivot = pivot;
        rt.anchorMin = rt.anchorMax = anchorPos;
        rt.sizeDelta = new Vector2(600f, 70f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        return go;
    }

    private GameObject CreateButton(string name, Transform parent,
        Vector2 pivot, Vector2 anchorPos,
        Vector2 size, string label, Color bgColor, TMP_FontAsset font)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.pivot     = pivot;
        rt.anchorMin = rt.anchorMax = anchorPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.3f);
        cb.pressedColor     = Color.Lerp(bgColor, Color.black, 0.3f);
        cb.normalColor      = bgColor;
        btn.colors          = cb;

        // Label text
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 18f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        if (font != null) tmp.font = font;

        return go;
    }

    private void SetAnchorCenter(RectTransform rt, Vector2 anchorPos)
    {
        rt.anchorMin = rt.anchorMax = anchorPos;
    }

    private void TrySetProp(SerializedObject so, string propName, Object value)
    {
        if (value == null) return;
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            Debug.Log($"   ✅ Linked '{propName}'");
        }
        else
        {
            Debug.LogWarning($"   ⚠️ Property '{propName}' not found on LobbyUIManager");
        }
    }
#endif

    // Ở runtime, script này không làm gì — chỉ dùng trong Editor
    private void Start()
    {
#if UNITY_EDITOR
        Debug.Log("[LobbyUIAutoSetup] Script chỉ dùng trong Editor để build UI. Có thể xóa sau khi đã build.");
#endif
    }
}
