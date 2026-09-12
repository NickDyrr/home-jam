using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Waits in the dark, terrified. When the player gets close it follows,
/// walking, or running when it falls behind. The moment it is inside the
/// HomeZone it counts as home and walks to its settle spot, then sits.
///
/// Escorting is a job: when a stalker gets close the survivor panics, freezes
/// and screams (which wakes nearby stalkers). They only move again once the
/// player comes right up to them for a moment. Each survivor has a job that
/// gives the home a bonus once they are through the door.
///
/// Animation is optional: if a humanoid Animator is found in children it is
/// driven with Speed (0..1), Run, Scared and Sitting parameters.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Survivor : MonoBehaviour
{
    public enum State { Waiting, Following, Panicked, Settling, Home }

    /// <summary>Every live survivor in the scene.</summary>
    public static readonly List<Survivor> All = new List<Survivor>();

    [Header("Who")]
    [SerializeField] private SurvivorJob job = SurvivorJob.None;

    [Header("Behaviour")]
    [SerializeField] private float noticeRadius = 2.5f;
    [SerializeField] private float followDistance = 1.6f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float runSpeed = 7.5f;
    [Tooltip("Beyond this distance from the player a following survivor runs to catch up.")]
    [SerializeField] private float runCatchUpDistance = 4f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float settleTolerance = 0.25f;

    [Header("Panic")]
    [Tooltip("A hunting stalker this close makes the survivor freeze.")]
    [SerializeField] private float panicRadius = 8f;
    [Tooltip("The player must be this close to get them moving again.")]
    [SerializeField] private float calmRadius = 2.2f;
    [SerializeField] private float calmSeconds = 0.5f;
    [Tooltip("After calming down, seconds before they can panic again.")]
    [SerializeField] private float panicCooldown = 5f;
    [Tooltip("The scream wakes dormant stalkers within this range.")]
    [SerializeField] private float screamRadius = 18f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    public State CurrentState { get; private set; } = State.Waiting;
    public SurvivorJob Job => job;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int ScaredHash = Animator.StringToHash("Scared");
    private static readonly int SittingHash = Animator.StringToHash("Sitting");
    private static readonly int PanicHash = Animator.StringToHash("Panic");

    private CharacterController controller;
    private Transform player;
    private Vector3 settleSpot;
    private bool running;
    private float calmTimer;
    private float panicOkAfter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>The stalker got this one. Unrealized value, gone, and their camp fire dies.</summary>
    public void Taken()
    {
        Debug.Log($"Survivor '{name}' was taken.");
        Campfire fire = Campfire.Nearest(transform.position, 6f);
        if (fire != null) fire.PutOut();
        if (Home.Instance != null) Home.Instance.SurvivorLost(this);
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
        float speed = moveSpeed * HomeBonuses.SurvivorSpeedMultiplier;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        switch (CurrentState)
        {
            case State.Waiting:
                if (toPlayer.magnitude <= noticeRadius)
                {
                    CurrentState = State.Following;
                    FloatingText.Show(transform.position + Vector3.up * 2.5f, HomeBonuses.Greeting(job), 4.5f);
                }
                break;

            case State.Following:
            {
                if (Time.time >= panicOkAfter && StalkerNear(panicRadius))
                {
                    Panic();
                    break;
                }

                float d = toPlayer.magnitude;
                // Hysteresis so it does not flicker between walk and run.
                if (d > runCatchUpDistance) running = true;
                else if (d < runCatchUpDistance * 0.6f) running = false;
                speed = (running ? runSpeed : moveSpeed) * HomeBonuses.SurvivorSpeedMultiplier;
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

            case State.Panicked:
            {
                // Frozen. Turn toward the player, and only move again once they are right here.
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
                }
                if (toPlayer.magnitude <= calmRadius) calmTimer += Time.deltaTime;
                else calmTimer = 0f;
                if (calmTimer >= calmSeconds)
                {
                    CurrentState = State.Following;
                    running = false;
                    panicOkAfter = Time.time + panicCooldown;
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

        if (CurrentState != State.Panicked && move.sqrMagnitude > 0.001f)
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
            animator.SetBool(PanicHash, CurrentState == State.Panicked);
            animator.SetBool(SittingHash, CurrentState == State.Home);
        }
    }

    private bool StalkerNear(float radius)
    {
        float sq = radius * radius;
        foreach (Stalker s in Stalker.All)
        {
            if (s.CurrentState == Stalker.State.Stunned) continue;
            Vector3 d = s.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude <= sq) return true;
        }
        return false;
    }

    private void Panic()
    {
        CurrentState = State.Panicked;
        running = false;
        calmTimer = 0f;
        Debug.Log($"Survivor '{name}' panics.");
        if (AudioManager.Instance != null) AudioManager.Instance.Play(AudioManager.Instance.Yell, transform.position, 1f, 40f, Random.Range(0.9f, 1.15f));
        // The scream carries. Dormant stalkers nearby wake up.
        float sq = screamRadius * screamRadius;
        foreach (Stalker s in Stalker.All)
        {
            Vector3 d = s.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude <= sq) s.Alert();
        }
    }
}
