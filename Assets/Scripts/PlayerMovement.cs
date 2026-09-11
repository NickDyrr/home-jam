using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WASD / arrow-key movement on the XZ plane, relative to the camera's yaw.
/// Uses a CharacterController so the player collides with greybox walls.
/// Drives the Animator's Speed parameter (0..1) from movement input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private Animator animator;

    /// <summary>While true, something else (the pistol) owns the facing direction.</summary>
    public bool FacingLocked { get; set; }

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

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
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
    }

    private void Update()
    {
        Vector2 input = ReadInput();

        // Build screen-relative axes on the ground plane.
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 move = (forward * input.y + right * input.x);
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Simple gravity so the controller stays grounded.
        Vector3 velocity = move * moveSpeed;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        if (!FacingLocked && move.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (animator != null) animator.SetFloat(SpeedHash, move.magnitude, 0.08f, Time.deltaTime);
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
