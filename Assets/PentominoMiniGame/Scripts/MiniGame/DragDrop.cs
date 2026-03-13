using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniGame
{
    public class DragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static PuzzlePiece isDraggingItem;
        
        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector2 originalPosition;
        private PuzzlePiece piece;
        
        [SerializeField] private PuzzleBoard board;
        public float cellSize = 100f; // Set this to match your Grid cell size
        
        private bool isOnBoard = false;
        private int placedGridX, placedGridY; // Saves coordinates if placed on board
        private bool wasOnBoardAtDragStart;   // Track state at drag begin

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            piece = GetComponent<PuzzlePiece>();
            
            if (canvasGroup == null)
            {
                // Ensure CanvasGroup exists for blocking raycasts during drag
                canvasGroup = gameObject.AddComponent<CanvasGroup>(); 
            }
        }

        // Lưu lại khoảng cách từ chuột đến điểm gốc (0,0) của ô ghép
        private Vector2 pickUpOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDraggingItem = piece;
            originalPosition = rectTransform.anchoredPosition;
            canvasGroup.blocksRaycasts = false; // Pierce through to check snap
            
            // Ghi lại trạng thái ban đầu của miếng ghép
            wasOnBoardAtDragStart = isOnBoard;

            // Nếu đang nằm trên board thì tạm thời gỡ ra khỏi lưới để tránh chồng lấn
            if (isOnBoard)
            {
                board.RemovePiece(piece, placedGridX, placedGridY);
                isOnBoard = false;
            }
            
            // Bring to front while dragging
            rectTransform.SetAsLastSibling();

            // Tính toán khoảng lệch: Chuột đang nằm ở đâu so với gốc Toạ độ (0,1) của khối hình?
            // Điều này giải quyết triệt để lỗi người dùng cầm khúc DƯỚI của chữ L nhưng code 
            // lại tính tọa độ rớt mạng dựa vào khúc TRÊN CÙNG của chữ L.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out pickUpOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect != null)
            {
                Vector2 localPointerPosition;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPointerPosition))
                {
                    // Lấy vị trí chuột trừ đi cái độ vẩu (offset) lúc bốc lên
                    // Để giữ nguyên cái ngón tay người chơi dính chặt ở vị trí họ click ban đầu
                    rectTransform.localPosition = localPointerPosition - pickUpOffset; 
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDraggingItem = null;
            canvasGroup.blocksRaycasts = true;

            // Tìm tự động mặt phẳng chứa lưới hệ trục (có Grid Layout Group)
            UnityEngine.UI.GridLayoutGroup gridGroup = board.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>();
            RectTransform gridRect = gridGroup != null ? gridGroup.GetComponent<RectTransform>() : board.GetComponent<RectTransform>();
            Vector2 dropLocalPos;
            
            // Lấy TỌA ĐỘ TRỰC TIẾP CỦA GÓC TRÊN CÙNG BÊN TRÁI của Piece_L 
            // chiếu lên mặt phẳng Grid (thay vì lấy tọa độ đầu mũi chuột như cũ)
            // Đây là chìa khóa vàng: Dù user cầm ở khúc nào, cái đi đem đi so sánh 
            // vẫn luôn là gốc tọa độ (0,0) thuần túy của cái màn hình!
            Vector3 worldOriginPoint = rectTransform.position; // Position luôn lấy theo pivot (0,1)
            dropLocalPos = gridRect.InverseTransformPoint(worldOriginPoint);

            // Ensure Pivot of board is top-left (0, 1) for this calculation
            ConvertLocalPosToGridCoordinates(dropLocalPos, out int gridX, out int gridY);

            // THUẬT TOÁN NEAREST-SNAP THÔNG MINH
            // Đầu tiên kiểm tra chính xác ngay tại vị trí chuột thả
            if (board.IsPositionValid(piece, gridX, gridY))
            {
                PlaceAndSnapPiece(gridRect, gridX, gridY);
                return;
            }
            
            // Nếu thả lệch (bị vướng viền, va chạm), hệ thống tự động dò tìm
            // 8 ô xung quanh bán kính (Radius = 1) để ráng "nhét" nó vào dùm người chơi
            // Tạo cảm giác game rất mượt và hít nam châm
            int[] searchOffsetX = { 0, 1, -1, 0, 0, 1, -1, 1, -1 };
            int[] searchOffsetY = { 0, 0, 0, 1, -1, 1, 1, -1, -1 };

            for (int i = 1; i < searchOffsetX.Length; i++) // Bỏ qua ô 0 (đã check)
            {
                int tryX = gridX + searchOffsetX[i];
                int tryY = gridY + searchOffsetY[i];

                // Kiểm tra xem vị trí lân cận này có thả lọt khối hình không
                if (board.IsPositionValid(piece, tryX, tryY))
                {
                    Debug.Log($"[DragDrop] Smart Snap Activated! Dời từ [{gridX},{gridY}] sang [{tryX},{tryY}]");
                    PlaceAndSnapPiece(gridRect, tryX, tryY);
                    return; // Successfully placed using Smart Snap
                }
            }
            
            Debug.Log($"[DragDrop] Drop failed. Grid Cell [{gridX}, {gridY}] and neighbors are invalid.");
            
            // Kiểm tra con trỏ có đang nằm bên trong vùng board không
            bool isPointerOverBoard = RectTransformUtility.RectangleContainsScreenPoint(
                gridRect,
                eventData.position,
                eventData.pressEventCamera
            );

            if (isPointerOverBoard)
            {
                // Nếu vẫn đang ở trên mặt board mà không snap được ô hợp lệ,
                // ta trả về vị trí cũ.
                rectTransform.anchoredPosition = originalPosition;

                // Và nếu lúc bắt đầu drag miếng này vốn đang nằm trên board,
                // ta đặt lại logic lưới cho đúng.
                if (wasOnBoardAtDragStart)
                {
                    board.PlacePiece(piece, placedGridX, placedGridY);
                    isOnBoard = true;
                }
            }
            // Ngược lại: nếu chuột thả ra ngoài vùng board
            // -> giữ nguyên vị trí hiện tại (cho phép kéo miếng ra khỏi GridBox).
        }

        // Hàm Tách Tái Cấu Trúc Để Tái Sử Dụng Gọn Gàng
        private void PlaceAndSnapPiece(RectTransform gridRect, int gridX, int gridY)
        {
            SnapToGridUI(gridRect, gridX, gridY);
            board.PlacePiece(piece, gridX, gridY);
            isOnBoard = true;
            placedGridX = gridX;
            placedGridY = gridY;
        }

        private void ConvertLocalPosToGridCoordinates(Vector2 localPos, out int gridX, out int gridY)
        {
            // If the Board's Pivot is Top-Left (0, 1) setup:
            // x goes deep to the right (+)
            // y goes deep to the bottom (-)
            
            // Dùng RoundToInt thay vì FloorToInt để tạo độ "tương đối".
            // Người chơi chỉ cần rê miếng ghép chạm vào "Nửa ô" là hệ thống 
            // sẽ tự hiểu và hút nó vào chính giữa ô đó.
            gridX = Mathf.RoundToInt(localPos.x / cellSize);
            gridY = Mathf.RoundToInt(-localPos.y / cellSize);
        }
        
        private void SnapToGridUI(RectTransform gridRect, int x, int y)
        {
            // BỎ qua lệnh SetParent(gridRect) vì Component "Grid Layout Group" trên GridBox
            // sẽ CƯỠNG CHẾ chiếm quyền và nhét cái hình L này vào vị trí ô thứ 26 (ra ngoài lề dưới cùng)
            
            // Tính toán tọa độ Local ảo của góc [x,y] trên lưới 5x5
            Vector2 targetLocalPos = new Vector2(x * cellSize, -y * cellSize);
            
            // Lấy tọa độ World Space (Hệ trục màn hình thật) của cái góc lưới đó
            Vector3 worldPos = gridRect.TransformPoint(targetLocalPos);
            
            // Đặt Object L vào đúng cái tọa độ thực tế đó, KHÔNG đổi Parent
            rectTransform.position = worldPos;
        }
    }
}
