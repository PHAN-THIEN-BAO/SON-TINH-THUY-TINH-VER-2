using UnityEngine;

namespace MiniGame
{
    public class PuzzlePiece : MonoBehaviour
    {
        // Example configuration in Inspector for an L-shape piece:
        // [1, 0]
        // [1, 0]
        // [1, 1]
        
        [System.Serializable]
        public class RowData { public int[] row; }
        public RowData[] shapeDefinition;

        private int[,] currentShape;

        private void Start()
        {
            // Convert Inspector data to 2D array
            int rows = shapeDefinition.Length;
            int cols = shapeDefinition[0].row.Length;
            currentShape = new int[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    currentShape[r, c] = shapeDefinition[r].row[c];
                }
            }
        }

        public int[,] GetShape() => currentShape;

        // Rotates the 2D matrix 90 degrees clockwise and recalculates UI position
        public void Rotate()
        {
            int rows = currentShape.GetLength(0);
            int cols = currentShape.GetLength(1);
            int[,] rotated = new int[cols, rows];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 90 degree clockwise rotation formula for array
                    rotated[c, rows - 1 - r] = currentShape[r, c];
                }
            }
            
            // Lấy thông tin chiều cao (thay vì rộng) để bù trừ
            // Vì khi quay 90 độ, Dòng trở thành Cột, Cột trở thành Dòng.
            // Do Pivot ở (0,1) Top-Left, khi quay 90 độ thuận chiều kim đồng hồ,
            // Toàn bộ khối hình sẽ văng VỀ BÊN TRÁI 1 khoảng đúng bằng chiều Cao (quy ra pixel)
            
            float cellSize = GetComponent<DragDrop>().cellSize;
            RectTransform rt = GetComponent<RectTransform>();
            
            // XíCH HÌNH ẢNH TRỞ LẠI GỐC TỌA ĐỘ
            // Thay vì cố gắng tính toán gia tốc bằng toán học mập mờ,
            // ta sẽ đo trực tiếp Khung Bao Phủ (Bounding Box) của tất cả thằng con
            // và dời chúng sao cho ôm sát khít vào gốc tọa độ (0,0) của thẻ Cha!
            
            rt.Rotate(0, 0, -90);

            // Tìm giới hạn (Bound) nhỏ nhất và lớn nhất của tụi con sau khi đã xoay móp méo
            float minX = float.MaxValue;
            float maxY = float.MinValue; 

            foreach (Transform child in rt)
            {
                // Quy đổi vị trí Local hiện tại của từng cục sang tọa độ tương đối thực của Cha
                RectTransform childRt = child as RectTransform;
                if (childRt != null)
                {
                    // Lấy điểm trên cùng bên trái của từng ô gạch
                    Vector3 localPos = rt.InverseTransformPoint(childRt.TransformPoint(Vector3.zero));
                    
                    if (localPos.x < minX) minX = localPos.x;
                    // Y trong UI đi ngược xuống, nên điểm cao nhất là maxY
                    if (localPos.y > maxY) maxY = localPos.y;
                }
            }

            // Tính quãng đường để bưng toàn bộ ổ con dán vào góc trên bên trái (0,0)
            Vector3 offsetToOrigin = new Vector3(-minX, -maxY, 0);

            // Dời toàn bộ tụi con
            foreach (Transform child in rt)
            {
                child.localPosition += offsetToOrigin;
            }

            // Cập nhật ma trận xương toán học
            currentShape = rotated;
        }

        private void Update()
        {
            // If user is dragging this piece and presses R, rotate it
            if (Input.GetKeyDown(KeyCode.R) && DragDrop.isDraggingItem == this)
            {
                Rotate();
            }
        }
    }
}
