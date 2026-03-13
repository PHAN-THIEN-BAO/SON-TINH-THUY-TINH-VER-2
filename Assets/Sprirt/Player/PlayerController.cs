// using UnityEngine;

// public class PlayerController : MonoBehaviour
// {
//     public float moveSpeed = 5f;          // tốc độ di chuyển
//     public Animator animator;             // Animator của nhân vật
//     private Rigidbody rb;
//     int isWalkingHash = Animator.StringToHash("isWalking");

//     void Start()
//     {
//         animator = GetComponent<Animator>();
//     }

//     void Update()
//     {
//         bool forwardPressed = Input.GetKey(KeyCode.W);
//         bool isWalking = animator.GetBool(isWalkingHash);
//         if (!isWalking && forwardPressed)
//         {
//             animator.SetBool(isWalkingHash, true);
//         }
//         else if (isWalking && !forwardPressed)
//         {
//             animator.SetBool(isWalkingHash, false);
//         }
//     }
// }
