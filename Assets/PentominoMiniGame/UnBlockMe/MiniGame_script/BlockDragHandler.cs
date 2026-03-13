using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public UnblockMeManager.BlockData data;
    
    private UnblockMeManager manager;
    private RectTransform rectTransform;
    private Image blockImage;

    private float cellSize;
    private float minPosAllowed;
    private float maxPosAllowed;
    
    private Vector2 grabOffset;

    public void Setup(UnblockMeManager manager, UnblockMeManager.BlockData data, float cellSize)
    {
        this.manager = manager;
        this.data = data;
        this.cellSize = cellSize;
        
        rectTransform = GetComponent<RectTransform>();
        blockImage = GetComponent<Image>();

        // Set colors matching original game
        blockImage.color = data.isMain ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.8f, 0.5f, 0.2f);

        // Standardize anchors so we can position easily
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1); // Top-left Pivot

        UpdateVisualPosition();
    }

    private void UpdateVisualPosition()
    {
        rectTransform.anchoredPosition = new Vector2(data.x * cellSize + 2, -(data.y * cellSize) - 2);
        rectTransform.sizeDelta = new Vector2(
            (data.isHorizontal ? data.length * cellSize : cellSize) - 4,
            (data.isHorizontal ? cellSize : data.length * cellSize) - 4
        );
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        manager.GetLimits(this, out minPosAllowed, out maxPosAllowed);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            manager.boardRect, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 localPointerPosition
        );
        
        // Grab offset from the block's current rendered position so it doesn't snap to pointer center
        grabOffset = localPointerPosition - rectTransform.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            manager.boardRect, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 localPointerPosition
        );

        Vector2 targetPos = localPointerPosition - grabOffset;

        if (data.isHorizontal)
        {
            float clampedX = Mathf.Clamp(targetPos.x, minPosAllowed, maxPosAllowed);
            rectTransform.anchoredPosition = new Vector2(clampedX, rectTransform.anchoredPosition.y);
        }
        else
        {
            // Y goes down in negative values in Unity's Top-Left Pivot.
            // Meaning maxPosAllowed (e.g., -400) is numerically less than minPosAllowed (0).
            float clampedY = Mathf.Clamp(targetPos.y, maxPosAllowed, minPosAllowed);
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, clampedY);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        int oldX = data.x;
        int oldY = data.y;

        if (data.isHorizontal)
        {
            data.x = Mathf.RoundToInt(rectTransform.anchoredPosition.x / cellSize);
        }
        else
        {
            data.y = Mathf.RoundToInt(-rectTransform.anchoredPosition.y / cellSize);
        }

        UpdateVisualPosition();
        manager.UpdateGrid(this, oldX, oldY);
    }
}
