using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Waits in the dark, terrified. When the player gets close it follows,
/// walking, or running when it falls behind. The moment it is inside the
/// HomeZone it counts as home and walks to its settle spot, then sits.
///
/// When a stalker gets close a following survivor runs to stay on her heels.
/// Each survivor has a job that gives the home a bonus once they are through
/// the door.
///
/// Animation is optional: if a humanoid Animator is found in children it is
/// driven with Speed (0..1), Run, Scared and Sitting parameters.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Survivor : MonoBehaviour
{
    public enum State { Waiting, Following, Panicked, Entering, Settling, Home, Dead }

    /// <summary>Every live survivor in the scene.</summary>
    public static readonly List<Survivor> All = new List<Survivor>();

    /// <summary>What makes escorting this one its own problem.</summary>
    public enum Trait
    {
        None,
        Hurt,       // walks slowly and cannot run
        Skittish,   // hides again if a stalker gets close; you have to go back for them
        Stubborn,   // will not leave until you have stood at their fire a few seconds
    }

    [Header("Who")]
    [SerializeField] private SurvivorJob job = SurvivorJob.None;
    [SerializeField] private Trait trait = Trait.None;
    [SerializeField] private float hurtSpeedFactor = 0.62f;
    [SerializeField] private float skittishRadius = 10f;
    [SerializeField] private float stubbornSeconds = 3f;
    public Trait Quirk => trait;
    private float stayTimer;
    private bool met, nudged;

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
    [Tooltip("A stalker this close makes a following survivor run.")]
    [SerializeField] private float panicRadius = 8f;
    [Tooltip("The player must be this close to get them moving again.")]
    [SerializeField] private float calmRadius = 2.2f;
    [SerializeField] private float calmSeconds = 0.5f;
    [Tooltip("After calming down, seconds before they can panic again.")]
    [SerializeField] private float panicCooldown = 5f;
    [Tooltip("The scream wakes dormant stalkers within this range.")]
    [SerializeField] private float screamRadius = 18f;
    [Tooltip("How long they stand and scream before running for the player.")]
    [SerializeField] private float screamSeconds = 1.0f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    public State CurrentState { get; private set; } = State.Waiting;
    public SurvivorJob Job => job;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int ScaredHash = Animator.StringToHash("Scared");
    private static readonly int SittingHash = Animator.StringToHash("Sitting");
    private static readonly int PanicHash = Animator.StringToHash("Panic");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private float panicStart;

    private CharacterController controller;
    private Transform player;
    private Vector3 settleSpot;
    private bool running;
    private float calmTimer;
    private float panicOkAfter;
    private List<Vector3> route;
    private int routeIndex;
    private Renderer[] renderers;
    private bool hiding;
    private Campfire camp;

    /// <summary>By day a waiting survivor is hidden away and cannot be found.</summary>
    public bool IsHiding => hiding;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        camp = Campfire.Nearest(transform.position, 6f);   // their own fire; it dies when they are home or gone
    }

    private void SetHiding(bool hide)
    {
        if (hide == hiding) return;
        hiding = hide;
        foreach (var r in renderers) if (r != null) r.enabled = !hide;
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>
    /// The stalker got this one. They go down where they stand and stay there; their camp fire
    /// dies, and the count moves on without them.
    /// </summary>
    public void Taken()
    {
        if (CurrentState == State.Dead) return;
        Debug.Log($"Survivor '{name}' was taken.");
        CurrentState = State.Dead;
        All.Remove(this);
        Campfire fire = camp != null ? camp : Campfire.Nearest(transform.position, 6f);
        if (fire != null) fire.PutOut();
        if (Home.Instance != null) Home.Instance.SurvivorLost(this);

        if (animator != null)
        {
            animator.SetBool(PanicHash, false);
            animator.SetBool(ScaredHash, false);
            animator.SetBool(RunHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }
        if (controller != null) controller.enabled = false;            // the body is not a wall
        var prints = GetComponent<FootprintEmitter>(); if (prints != null) prints.enabled = false;
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
        if (player == null || CurrentState == State.Dead) return;

        Vector3 move = Vector3.zero;
        float speed = moveSpeed * HomeBonuses.SurvivorSpeedMultiplier;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        switch (CurrentState)
        {
            case State.Waiting:
            {
                // Out of sight by day, at the fire by night. Only findable after dark.
                SetHiding(!StalkerDirector.IsNight);
                bool near = !hiding && toPlayer.magnitude <= noticeRadius;
                if (trait == Trait.Stubborn && !met)
                {
                    // Will not budge until she has stood with them a moment.
                    stayTimer = near ? stayTimer + Time.deltaTime : 0f;
                    if (near && !nudged) { nudged = true; FloatingText.Show(transform.position + Vector3.up * 2.4f, "Stay a moment.", 2.5f); }
                    near = stayTimer >= stubbornSeconds;
                }
                if (near)
                {
                    CurrentState = State.Following;
                    if (!met) { met = true; Encounter.Play(this); }
                }
                break;
            }

            case State.Following:
            {
                SetHiding(false);
                // No panic any more: with something close they just run to keep on her heels.
                bool threatened = StalkerNear(panicRadius);
                if (trait == Trait.Skittish && StalkerNear(skittishRadius))
                {
                    // Gone to ground again. She has to come back for them.
                    CurrentState = State.Waiting;
                    running = false;
                    FloatingText.Show(transform.position + Vector3.up * 2.4f, "Hid again.", 2f);
                    break;
                }
                float d = toPlayer.magnitude;
                // Hysteresis so it does not flicker between walk and run.
                if (d > runCatchUpDistance || threatened) running = true;
                else if (d < runCatchUpDistance * 0.6f) running = false;
                if (trait == Trait.Hurt) running = false;
                speed = (running ? runSpeed : moveSpeed) * HomeBonuses.SurvivorSpeedMultiplier;
                if (trait == Trait.Hurt) speed *= hurtSpeedFactor;
                if (d > followDistance) move = toPlayer.normalized;

                if (HomeZone.Instance != null && HomeZone.Instance.Contains(transform.position))
                {
                    Arrive();
                }
                else if (Home.Instance != null && Home.Instance.InYard(transform.position))
                {
                    // Safe inside the fence: stop shadowing the player and head in through the front door.
                    CurrentState = State.Entering;
                    running = false;
                    route = DoorOpener.HomeRoute();
                    routeIndex = 0;
                }
                break;
            }

            case State.Entering:
            {
                if (HomeZone.Instance != null && HomeZone.Instance.Contains(transform.position)) { Arrive(); break; }
                Vector3 goal = Home.Instance != null ? Home.Instance.transform.position : transform.position;
                if (route != null && routeIndex < route.Count)
                {
                    goal = route[routeIndex];
                    Vector3 toGoal = goal - transform.position; toGoal.y = 0f;
                    if (toGoal.magnitude < 0.3f) { routeIndex++; break; }
                }
                Vector3 toG = goal - transform.position; toG.y = 0f;
                if (toG.sqrMagnitude > 0.01f) move = toG.normalized;
                break;
            }

            case State.Panicked:
            {
                // A scream where they stand, then they bolt for her and stay on her heels until
                // she has been beside them a moment.
                bool screaming = Time.time < panicStart + screamSeconds;
                if (screaming)
                {
                    if (toPlayer.sqrMagnitude > 0.01f)
                    {
                        Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
                    }
                }
                else if (toPlayer.magnitude > followDistance)
                {
                    running = true;
                    speed = runSpeed * HomeBonuses.SurvivorSpeedMultiplier;
                    move = toPlayer.normalized;
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
            {
                // Face the front door rather than whatever wall the spot happens to be near.
                if (DoorOpener.Active.Count > 0)
                {
                    Vector3 toDoor = DoorOpener.Active[0].Doorway - transform.position; toDoor.y = 0f;
                    if (toDoor.sqrMagnitude > 0.01f)
                    {
                        Quaternion look = Quaternion.LookRotation(toDoor.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * 0.5f * Time.deltaTime);
                    }
                }
                break;
            }
        }

        Vector3 velocity = move * speed;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        bool screamingNow = CurrentState == State.Panicked && Time.time < panicStart + screamSeconds;
        if (!screamingNow && move.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (animator != null)
        {
            bool moving = move.sqrMagnitude > 0.001f;
            animator.SetFloat(SpeedHash, moving ? 1f : 0f, 0.1f, Time.deltaTime);
            animator.SetBool(RunHash, moving && running && (CurrentState == State.Following || CurrentState == State.Panicked));
            animator.SetBool(ScaredHash, CurrentState == State.Waiting);
            animator.SetBool(PanicHash, screamingNow);
            animator.SetBool(SittingHash, false);   // they stand and idle at home; nothing to sit on at their spots
        }
    }

    /// <summary>Inside the house: counted, and off to a spot. Their fire out there goes out.</summary>
    private void Arrive()
    {
        CurrentState = State.Settling;
        running = false;
        if (camp != null) camp.PutOut();
        settleSpot = Home.Instance != null ? Home.Instance.SurvivorArrived(this) : transform.position;
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
        panicStart = Time.time;
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
