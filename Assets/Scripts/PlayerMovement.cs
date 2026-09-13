using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WASD / arrow-key movement on the XZ plane, relative to the camera's yaw.
/// Left Shift runs. Running is not allowed while the pistol is armed (a
/// shot was fired in the last couple of seconds), so firing while running
/// drops you straight into the armed walk.
/// Uses a CharacterController so the player collides with greybox walls.
/// Drives the Animator's Speed (0..1) and Run (bool) parameters.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float runSpeed = 6.5f;
    [SerializeField] private float turnSpeed = 720f;
    [Tooltip("Running wakes dormant stalkers within this distance.")]
    [SerializeField] private float runNoiseRadius = 14f;
    [SerializeField] private Animator animator;

    private float nextNoise;

    /// <summary>While true, something else (the pistol) owns the facing direction.</summary>
    public bool FacingLocked { get; set; }

    /// <summary>True while actually running this frame.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>While true (listening), input is ignored and she stands still.</summary>
    public bool MovementLocked { get; set; }

    /// <summary>A stalker has hold of her. Set by the stalker for the grab window.</summary>
    public bool Grabbed { get; set; }

    /// <summary>A short scene (meeting a survivor) owns her for a moment.</summary>
    public bool CutsceneLocked { get; set; }

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private CharacterController controller;
    private Transform cam;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null) cam = Camera.main.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnDisable()
    {
        IsRunning = false;
        if (animator != null) { animator.SetFloat(SpeedHash, 0f); animator.SetBool(RunHash, false); }
    }

    private void Update()
    {
        Vector2 input = (MovementLocked || Grabbed || CutsceneLocked) ? Vector2.zero : ReadInput();
        Keyboard kb = Keyboard.current;
        bool shift = kb != null && kb.leftShiftKey.isPressed;

        // Build screen-relative axes on the ground plane.
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 move = (forward * input.y + right * input.x);
        if (move.sqrMagnitude > 1f) move.Normalize();

        bool armed = Pistol.Instance != null && Pistol.Instance.IsArmed;
        IsRunning = shift && !armed && move.sqrMagnitude > 0.001f;
        float speed = IsRunning ? runSpeed : moveSpeed;

        // Running is loud: dormant stalkers within earshot wake up.
        if (IsRunning && Time.time >= nextNoise)
        {
            nextNoise = Time.time + 0.5f;
            Stalker.Noise(transform.position, runNoiseRadius);
        }

        // Simple gravity so the controller stays grounded.
        Vector3 velocity = move * speed;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        if (!FacingLocked && move.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, move.magnitude, 0.08f, Time.deltaTime);
            animator.SetBool(RunHash, IsRunning);
        }
    }

    private static Vector2 ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = 0f, y = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        return new Vector2(x, y);
    }
}
