using UnityEngine;
using System.Collections;

public class Button3DHover : MonoBehaviour
{
    public Material normalMat;
    public Material hoverMat;

    [Header("Scale Effect")]
    public float hoverScale = 1.1f;
    public float scaleSpeed = 8f;

    private Renderer rend;
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    void Start()
    {
        rend = GetComponent<Renderer>();
        rend.material = normalMat;
        originalScale = transform.localScale;
    }

    void OnMouseEnter()
    {
        rend.material = hoverMat;
        StartScale(originalScale * hoverScale);
    }

    void OnMouseExit()
    {
        rend.material = normalMat;
        StartScale(originalScale);
    }

    void StartScale(Vector3 target)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleTo(target));
    }

    IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                target,
                Time.deltaTime * scaleSpeed
            );
            yield return null;
        }
        transform.localScale = target;
    }
}
