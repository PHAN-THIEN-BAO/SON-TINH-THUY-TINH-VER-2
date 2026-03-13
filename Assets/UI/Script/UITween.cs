using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UITween — thay thế LeanTween bằng Unity Coroutine thuần túy.
/// Không cần cài thư viện ngoài.
/// </summary>
public class UITween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject pannelUI;
    [SerializeField] public float duration   = 1f;
    [SerializeField] public float delay      = 0.5f;
    [SerializeField] public float rootSize   = 1f;
    [SerializeField] public float hoverScale    = 1.05f;
    [SerializeField] public float hoverDuration = 0.3f;

    private Vector3 originalScale;
    private Coroutine hoverCoroutine;

    // ── Easing functions (tương đương LeanTweenType) ──────────────────

    private static float EaseOutElastic(float t)
    {
        if (t == 0 || t == 1) return t;
        float p = 0.3f;
        float s = p / 4f;
        return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - s) * (2 * Mathf.PI) / p) + 1f;
    }

    private static float EaseOutBounce(float t)
    {
        if (t < 1f / 2.75f)       return 7.5625f * t * t;
        if (t < 2f / 2.75f)       { t -= 1.5f   / 2.75f; return 7.5625f * t * t + 0.75f;   }
        if (t < 2.5f / 2.75f)     { t -= 2.25f  / 2.75f; return 7.5625f * t * t + 0.9375f; }
        t -= 2.625f / 2.75f;       return 7.5625f * t * t + 0.984375f;
    }

    private static float EaseOutQuint(float t)  => 1f - Mathf.Pow(1f - t, 5f);
    private static float EaseOutQuad(float t)   => t * (2f - t);
    private static float EaseInQuad(float t)    => t * t;

    // ── Awake ─────────────────────────────────────────────────────────

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    // ── Scale open animations ─────────────────────────────────────────

    public void OnOpenScaleEaseOutElastic()
    {
        StartCoroutine(ScaleCoroutine(pannelUI, Vector3.zero,
            Vector3.one * rootSize, duration, delay, EaseOutElastic));
    }

    public void OnOpenScaleEaseOutBounce()
    {
        StartCoroutine(ScaleCoroutine(pannelUI, Vector3.zero,
            Vector3.one * rootSize, duration, delay, EaseOutBounce));
    }

    public void OnOpenScaleEaseOutQuint()
    {
        StartCoroutine(ScaleCoroutine(pannelUI, Vector3.zero,
            Vector3.one * rootSize, duration, delay, EaseOutQuint));
    }

    // ── Fade in ───────────────────────────────────────────────────────

    public void OnOpenFadeInPanel()
    {
        CanvasGroup canvasGroup = pannelUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = pannelUI.AddComponent<CanvasGroup>();
        StartCoroutine(FadeCoroutine(canvasGroup, 0f, 1f, duration, delay));
    }

    // ── Close animation ───────────────────────────────────────────────

    public void OnCloseScaleEaseInElastic(GameObject closePanel)
    {
        StartCoroutine(ScaleCoroutine(pannelUI, pannelUI.transform.localScale,
            Vector3.zero, duration, delay, EaseInQuad,
            onComplete: () => closePanel.SetActive(false)));
    }

    // ── Hover (IPointerEnter / IPointerExit) ──────────────────────────

    public void OnPointerEnter(PointerEventData eventData) => StartHover(gameObject, originalScale * hoverScale);
    public void OnPointerExit(PointerEventData eventData)  => StartHover(gameObject, originalScale);

    // ── Hover với tham số GameObject ─────────────────────────────────

    public void OnHoverEnter(GameObject targetObject) => StartHover(targetObject, originalScale * hoverScale);
    public void OnHoverExit(GameObject targetObject)  => StartHover(targetObject, originalScale);

    public void OnHoverEnterCustom(GameObject targetObject, float customScale)
        => StartHover(targetObject, originalScale * customScale);

    // ── Helpers ───────────────────────────────────────────────────────

    private void StartHover(GameObject target, Vector3 targetScale)
    {
        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(
            ScaleCoroutine(target, target.transform.localScale, targetScale, hoverDuration, 0f, EaseOutQuad));
    }

    // ── Coroutines ────────────────────────────────────────────────────

    private IEnumerator ScaleCoroutine(GameObject target, Vector3 from, Vector3 to,
        float dur, float del, System.Func<float, float> easeFunc,
        System.Action onComplete = null)
    {
        if (target == null) yield break;

        target.transform.localScale = from;

        if (del > 0f) yield return new WaitForSeconds(del);

        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            target.transform.localScale = Vector3.LerpUnclamped(from, to, easeFunc(t));
            yield return null;
        }

        if (target != null)
            target.transform.localScale = to;

        onComplete?.Invoke();
    }

    private IEnumerator FadeCoroutine(CanvasGroup cg, float from, float to, float dur, float del)
    {
        cg.alpha = from;
        pannelUI.SetActive(true);

        if (del > 0f) yield return new WaitForSeconds(del);

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / dur);
            yield return null;
        }
        cg.alpha = to;
    }
}