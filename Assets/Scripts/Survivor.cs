using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Waits in the dark, terrified. When the player gets close it follows,
/// walking, or running when it falls behind. The moment it is inside the
/// HomeZone it counts as home and walks to its settle spot, then sits.
///
/// Animation is optional: if a humanoid Animator is found in children it is
/// driven with Speed (0..1), Run, Scared and Sitting parameters. The capsule
/// placeholders have none and work as before.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Survivor : MonoBehaviour
{
    public enum State { Waiting, Following, Settling, Home }

    /// <summary>Every live survivor in the scene.</summary>
    public static readonly List<Survivor> All = new List<Survivor>();

    [Header("Behaviour")]
    [SerializeField] private float noticeRadius = 2.5f;
    [SerializeField] private float followDistance = 1.6f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float runSpeed = 7.5f;
    [Tooltip("Beyond this distance from the player a following survivor runs to catch up.")]
    [SerializeField] private float runCatchUpDistance = 4f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float settleTolerance = 0.25f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    public State CurrentState { get; private set; } = State.Waiting;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int ScaredHash = Animator.StringToHash("Scared");
    private static readonly int SittingHash = Animator.StringToHash("Sitting");

    private CharacterController controller;
    private Transform player;
    private Vector3 settleSpot;
    private bool running;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>The stalker got this one. Unrealized value, gone.</summary>
    public void Taken()
    {
        Debug.Log($"Survivor '{name}' was taken.");
        Destroy(gameObject);
    }

    /// <summary>Already home, but the house changed shape: walk to a new spot.</summary>
    public void Resettle(Vector3 spot)
    {
        if (CurrentState != State.Home && CurrentState != State.Settling) return;
        settleSpot = spot;
        running = false;
        CurrentState = State.Settling;
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 move = Vector3.zero;
        float speed = moveSpeed;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        switch (CurrentState)
        {
            case State.Waiting:
                if (toPlayer.magnitude <= noticeRadius)
                    CurrentState = State.Following;
                break;

            case State.Following:
            {
                float d = toPlayer.magnitude;
                // Hysteresis so it does not flicker between walk and run.
                if (d > runCatchUpDistance) running = true;
                else if (d < runCatchUpDistance * 0.6f) running = false;
                speed = running ? runSpeed : moveSpeed;
                if (d > followDistance) move = toPlayer.normalized;

                if (HomeZone.Instance != null && HomeZone.Instance.Contains(transform.position))
                {
                    CurrentState = State.Settling;
                    running = false;
                    settleSpot = Home.Instance != null
                        ? Home.Instance.SurvivorArrived(this)
                        : transform.position;
                }
                break;
            }

            case State.Settling:
                Vector3 toSpot = settleSpot - transform.position;
                toSpot.y = 0f;
                if (toSpot.magnitude > settleTolerance)
                    move = toSpot.normalized;
                else
                    CurrentState = State.Home;
                break;

            case State.Home:
                break;
        }

        Vector3 velocity = move * speed;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        if (move.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (animator != null)
        {
            bool moving = move.sqrMagnitude > 0.001f;
            animator.SetFloat(SpeedHash, moving ? 1f : 0f, 0.1f, Time.deltaTime);
            animator.SetBool(RunHash, moving && running && CurrentState == State.Following);
            animator.SetBool(ScaredHash, CurrentState == State.Waiting);
            animator.SetBool(SittingHash, CurrentState == State.Home);
        }
    }
}
