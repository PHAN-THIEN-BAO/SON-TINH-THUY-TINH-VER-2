using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Move")]
    [SerializeField] public float speed = 6f;
    [SerializeField] public float runSpeed = 10f;
    [SerializeField] public float jumpForce = 2f;
    [SerializeField] public float gravity = -9.81f;

    [Header("Mouse Look")]
    [SerializeField] public float mouseSensitivity = 0.1f;
    [SerializeField] public Transform cameraTransform;

    [Header("Network")]
    [SerializeField] private GameObject localPlayerCamera;
    [SerializeField] private GameObject localPlayerUI;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    // Tên parameter trong Animator Controller - khớp với Animator window
    [SerializeField] private string speedParam     = "Speed";      // Float - dùng cho Blend Tree
    [SerializeField] private string isWalkingParam = "IsWalking";  // Bool - đang đi
    [SerializeField] private string isRunningParam = "IsRunning";  // Bool - đang chạy

    private CharacterController controller;
    private float yVelocity;
    private float xRotation = 0f;
    private bool isCursorUnlocked = false;

    // --- New Input System ---
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isRunning = false;
    private bool jumpPressed = false;

    private NetworkVariable<Vector3>    networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();

    // --- Animation Sync ---
    private NetworkVariable<float> netSpeed     = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  netIsWalking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  netIsRunning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // --- CC Status ---
    private float stunTimer      = 0f;
    private float slowMultiplier = 1f;
    private float slowTimer      = 0f;
    private float confusedTimer  = 0f;
    
    // Knockback
    private Vector3 knockbackVelocity = Vector3.zero;
    private float   knockbackDecay    = 5f;

    // Khi true: đang hiển thị UI ngoài (mini game, menu...) nên tạm dừng điều khiển nhân vật
    private bool externalUiActive = false;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (IsOwner) LogActiveCameras();
    }

    // ==========================================
    // NEW INPUT SYSTEM CALLBACKS (Send Messages)
    // Tự động gọi bởi Player Input component
    // ==========================================

    private void OnMove(InputValue value)
    {
        if (!IsOwner) return;
        moveInput = value.Get<Vector2>();
    }

    private void OnRun(InputValue value)
    {
        if (!IsOwner) return;
        isRunning = value.isPressed;
    }

    private void OnJump(InputValue value)
    {
        if (!IsOwner) return;
        if (value.isPressed) jumpPressed = true;
    }

    private void OnLook(InputValue value)
    {
        if (!IsOwner) return;
        lookInput = value.Get<Vector2>();
    }

    // ==========================================
    // NETWORK SPAWN
    // ==========================================

    private void LogActiveCameras()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>(true);
        Debug.Log($"🎥 Total cameras in scene: {allCameras.Length}");

        int activeCameraCount = 0;
        foreach (Camera cam in allCameras)
        {
            if (cam.enabled && cam.gameObject.activeInHierarchy)
            {
                activeCameraCount++;
                Debug.Log($"  ✅ Active Camera: {cam.gameObject.name} on {cam.transform.root.name}");
            }
        }

        if (activeCameraCount > 1)
            Debug.LogWarning($"⚠️ Có {activeCameraCount} cameras active! Chỉ nên có 1 camera active cho local player.");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        networkPosition.OnValueChanged += OnPositionChanged;
        networkRotation.OnValueChanged += OnRotationChanged;

        // Subscribe animation sync cho remote players
        if (!IsOwner)
        {
            netSpeed.OnValueChanged     += (_, v) => SetAnimFloat(speedParam, v);
            netIsWalking.OnValueChanged += (_, v) => SetAnimBool(isWalkingParam, v);
            netIsRunning.OnValueChanged += (_, v) => SetAnimBool(isRunningParam, v);
        }

        if (IsOwner)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.gameObject != localPlayerCamera)
            {
                Debug.Log($"⚠️ Disabling Main Camera in scene: {mainCam.gameObject.name}");
                mainCam.gameObject.SetActive(false);
            }

            if (localPlayerCamera != null)
            {
                localPlayerCamera.SetActive(true);
                var camComponent = localPlayerCamera.GetComponent<Camera>();
                if (camComponent != null)
                {
                    camComponent.tag = "MainCamera";
                    Debug.Log($"✅ Enabled local camera for player {OwnerClientId} and set tag MainCamera");
                }
                else
                {
                    Debug.LogWarning("⚠️ localPlayerCamera không có component Camera.");
                }
            }
            else
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    cam.gameObject.SetActive(true);
                    cam.enabled = true;
                    cam.tag = "MainCamera";
                    Debug.Log($"✅ Enabled fallback camera for player {OwnerClientId} and set tag MainCamera");
                }
            }

            if (localPlayerUI != null) localPlayerUI.SetActive(true);

            isCursorUnlocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;

            Debug.Log($"✅ Local player spawned at {transform.position}");

            if (MultiplayerGameManager.Instance != null)
            {
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                MultiplayerGameManager.Instance.PlayerEnteredSceneServerRpc(currentScene, OwnerClientId, transform.position);
            }
        }
        else
        {
            if (localPlayerCamera != null)
            {
                localPlayerCamera.SetActive(false);
                var camComponent = localPlayerCamera.GetComponent<Camera>();
                if (camComponent != null)
                {
                    camComponent.tag = "Untagged";
                }
            }

            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                cam.enabled = false;
                cam.gameObject.SetActive(false);
            }

            AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
            foreach (AudioListener listener in listeners) listener.enabled = false;

            if (localPlayerUI != null) localPlayerUI.SetActive(false);

            if (controller != null) controller.enabled = false;

            // Tắt Player Input cho remote player để tránh nhận input không cần thiết
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            Debug.Log($"👁️ Remote player {OwnerClientId} visible at {transform.position}");
        }
    }

    private void OnPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsOwner) transform.position = newValue;
    }

    private void OnRotationChanged(Quaternion previousValue, Quaternion newValue)
    {
        if (!IsOwner) transform.rotation = newValue;
    }

    public override void OnNetworkDespawn()
    {
        networkPosition.OnValueChanged -= OnPositionChanged;
        networkRotation.OnValueChanged -= OnRotationChanged;

        if (!IsOwner)
        {
            netSpeed.OnValueChanged     -= (_, v) => SetAnimFloat(speedParam, v);
            netIsWalking.OnValueChanged -= (_, v) => SetAnimBool(isWalkingParam, v);
            netIsRunning.OnValueChanged -= (_, v) => SetAnimBool(isRunningParam, v);
        }

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            isCursorUnlocked = false;
        }

        base.OnNetworkDespawn();
    }

    // ==========================================
    // UPDATE LOOP
    // ==========================================

    private void Update()
    {
        if (!IsOwner) return;
        if (!GameLoadSystem.Ready) return;

        if (externalUiActive) return;

        // CC Timers
        if (stunTimer > 0)     stunTimer     -= Time.deltaTime;
        if (confusedTimer > 0) confusedTimer -= Time.deltaTime;

        if (slowTimer > 0)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0) slowMultiplier = 1f;
        }

        // Decay knockback
        if (knockbackVelocity.sqrMagnitude > 0.1f)
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
        else
            knockbackVelocity = Vector3.zero;

        // Alt → unlock/lock cursor
        HandleCursorToggle();

        if (!isCursorUnlocked)
        {
            Move();
            Look();
        }
    }

    public void EnterUiMode()
    {
        externalUiActive = true;
        isCursorUnlocked = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void ExitUiMode()
    {
        externalUiActive = false;
        isCursorUnlocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ==========================================
    // CURSOR
    // ==========================================

    private void HandleCursorToggle()
    {
        bool altHeld = Keyboard.current != null &&
                       (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

        if (altHeld && !isCursorUnlocked)       UnlockCursor();
        else if (!altHeld && isCursorUnlocked)  LockCursor();
    }

    private void UnlockCursor()
    {
        isCursorUnlocked = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Debug.Log("👁️ Cursor unlocked (Alt pressed)");
    }

    private void LockCursor()
    {
        isCursorUnlocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Debug.Log("🔒 Cursor locked (Alt released)");
    }

    // ==========================================
    // MOVEMENT + ANIMATION
    // ==========================================

    private void Move()
    {
        if (controller == null) { Debug.LogWarning("⚠️ Không có CharacterController! Hãy thêm component này vào prefab."); return; }

        Vector3 velocity = Vector3.zero;

        if (stunTimer <= 0)
        {
            float h = moveInput.x;
            float v = moveInput.y;

            // Rối loạn điều khiển: đảo ngược input
            if (confusedTimer > 0) { h = -h; v = -v; }

            Vector3 move = transform.right * h + transform.forward * v;

            // Jump
            if (controller.isGrounded)
            {
                if (yVelocity < 0) yVelocity = -2f;

                if (jumpPressed)
                {
                    yVelocity   = Mathf.Sqrt(jumpForce * -2f * gravity);
                    jumpPressed = false;
                }
            }
            else
            {
                jumpPressed = false;
            }

            float currentSpeed = isRunning ? runSpeed : speed;
            velocity = move * (currentSpeed * slowMultiplier);

            // Update Animator parameters (local + sync lên network)
            bool isMoving   = move.magnitude > 0.1f;
            bool walking    = isMoving && !isRunning;
            bool running    = isMoving && isRunning;
            float animSpeed = move.magnitude * (isRunning ? 1f : 0.5f);

            SetAnimBool(isWalkingParam, walking);
            SetAnimBool(isRunningParam, running);
            SetAnimFloat(speedParam, animSpeed);

            // Sync sang remote players qua NetworkVariable (chỉ Owner write)
            netIsWalking.Value = walking;
            netIsRunning.Value = running;
            netSpeed.Value     = animSpeed;
        }
        else
        {
            // Bị choáng
            if (controller.isGrounded && yVelocity < 0) yVelocity = -2f;
            SetAnimFloat(speedParam, 0f);
            SetAnimBool(isWalkingParam, false);
            SetAnimBool(isRunningParam, false);
            netSpeed.Value     = 0f;
            netIsWalking.Value = false;
            netIsRunning.Value = false;
        }

        yVelocity += gravity * Time.deltaTime;
        velocity.y = yVelocity;

        velocity += knockbackVelocity;
        controller.Move(velocity * Time.deltaTime);

        if (IsOwner) UpdatePositionServerRpc(transform.position, transform.rotation);
    }

    private void Look()
    {
        if (!IsOwner) return;

        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation  = Mathf.Clamp(xRotation, -80f, 80f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    // ==========================================
    // ANIMATOR HELPERS
    // ==========================================

    private void SetAnimFloat(string param, float value)
    {
        if (animator != null && !string.IsNullOrEmpty(param))
            animator.SetFloat(param, value, 0.1f, Time.deltaTime);
    }

    private void SetAnimBool(string param, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(param))
            animator.SetBool(param, value);
    }

    // ==========================================
    // NETWORK RPC
    // ==========================================

    [ServerRpc]
    private void UpdatePositionServerRpc(Vector3 position, Quaternion rotation)
    {
        networkPosition.Value = position;
        networkRotation.Value = rotation;
    }

    public void SetPosition(Vector3 position)
    {
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = position;
            yVelocity          = 0f;
            controller.enabled = true;
            Debug.Log($"✅ SetPosition to {position}, velocity reset");
        }
        else
        {
            transform.position = position;
        }

        if (IsOwner) UpdatePositionServerRpc(position, transform.rotation);
    }

    // ==========================================
    // CROWD CONTROL (CC) METHODS
    // ==========================================

    [ClientRpc]
    public void ApplySlowClientRpc(float multiplier, float duration, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        slowMultiplier = multiplier;
        slowTimer      = duration;
        Debug.Log($"Bị làm chậm {multiplier}x trong {duration}s");
    }

    [ClientRpc]
    public void ApplyStunClientRpc(float duration, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        stunTimer = Mathf.Max(stunTimer, duration);
        Debug.Log($"Bị choáng trong {duration}s");
    }

    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector3 force, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        knockbackVelocity = force;
        if (controller.isGrounded) yVelocity = 2f;
        Debug.Log($"Bị đẩy lùi với lực: {force}");
    }

    [ClientRpc]
    public void ApplyConfuseClientRpc(float duration, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        confusedTimer = Mathf.Max(confusedTimer, duration);
        Debug.Log($"🌀 Bị rối loạn điều khiển trong {duration}s!");
    }

    public void ApplySlow(float multiplier, float duration)
        => ApplySlowClientRpc(multiplier, duration, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } } });

    public void ApplyStun(float duration)
        => ApplyStunClientRpc(duration, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } } });

    public void ApplyKnockback(Vector3 force)
        => ApplyKnockbackClientRpc(force, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } } });

    public void ApplyConfuse(float duration)
        => ApplyConfuseClientRpc(duration, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } } });

    public Vector3 GetPosition()  => transform.position;
    public ulong   GetClientId()  => OwnerClientId;
}