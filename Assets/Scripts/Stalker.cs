using UnityEngine;

/// <summary>
/// The thing outside. Stands dormant in the dark until the player or an
/// escorted survivor comes within aggroRadius, then hunts whichever of them
/// is nearest. Gives up and goes dormant again if the nearest one gets past
/// loseRadius. It gets faster the longer a chase lasts, and slower for every bullet it takes.
/// A pistol hit staggers it, knocks it back and slows it for good; it never dies.
/// Never seen clearly; it is a silhouette with a cold glow.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Stalker : MonoBehaviour
{
    public enum State { Dormant, Hunting, Retreating, Stunned, Grabbing }

    /// <summary>Every live stalker in the scene.</summary>
    public static readonly System.Collections.Generic.List<Stalker> All = new System.Collections.Generic.List<Stalker>();

    [Header("Awareness")]
    [SerializeField] private float aggroRadius = 14f;
    [SerializeField] private float loseRadius = 22f;

    [Header("Movement")]
    [SerializeField] private float baseSpeed = 4f;
    [Tooltip("Once it has her, it gains this much speed for every second of the chase. Running only buys time; a bullet buys distance.")]
    [SerializeField] private float huntAccel = 0.45f;
    [SerializeField] private float maxSpeed = 8.5f;
    private float huntStart = -1f;
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float catchRadius = 1.3f;
    [SerializeField] private float retreatSeconds = 2.5f;
    [SerializeField] private float retreatSpeed = 5f;
    [Tooltip("How long it walks back into the trees once she reaches the fence, before going dormant out there.")]
    [SerializeField] private float yardRetreatSeconds = 6f;
    private bool retreatToDormant;

    [Header("Hit")]
    [Tooltip("Brief stagger on a hit. The lasting effect is the slow below.")]
    [SerializeField] private float stunSeconds = 1.2f;
    [SerializeField] private float knockbackDamping = 6f;
    [Tooltip("Each bullet takes this much off its speed, for good. Bullets never kill it.")]
    [SerializeField] private float slowPerHit = 0.18f;
    [SerializeField] private float minSpeedFactor = 0.3f;

    private int hits;
    /// <summary>How much of its speed is left after the bullets it has taken.</summary>
    public float SpeedFactor => Mathf.Max(minSpeedFactor, 1f - hits * slowPerHit);

    [Header("Grab")]
    [Tooltip("Seconds it holds the player before she is taken. A pistol hit in that window breaks the grab.")]
    [SerializeField] private float grabSeconds = 2.2f;

    private float grabUntil;
    private PlayerMovement grabbedMovement;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;

    [Header("Glow")]
    [SerializeField] private Light glow;
    [SerializeField] private float dormantGlow = 0.35f;
    [SerializeField] private float huntingGlow = 1.6f;
    [SerializeField] private float glowLerp = 4f;

    public State CurrentState { get; private set; } = State.Dormant;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int StunnedHash = Animator.StringToHash("Stunned");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private CharacterController controller;
    private Transform player;
    private float retreatUntil;
    private Vector3 retreatDir;
    private float stunUntil;
    private Vector3 knock;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (glow == null) glow = GetComponentInChildren<Light>();
        if (glow != null) glow.intensity = dormantGlow;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>Something screamed nearby. A dormant stalker wakes and hunts.</summary>
    public void Alert()
    {
        if (CurrentState != State.Dormant) return;
        Wake();
    }

    /// <summary>A noise at a point (a shot, running feet, a scream) wakes dormant stalkers within radius.</summary>
    public static void Noise(Vector3 pos, float radius)
    {
        float sq = radius * radius;
        foreach (Stalker s in All)
        {
            Vector3 d = s.transform.position - pos; d.y = 0f;
            if (d.sqrMagnitude <= sq) s.Alert();
        }
    }

    private void Wake()
    {
        CurrentState = State.Hunting;
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.Instance.Growl, transform.position, 0.9f, 40f, Random.Range(0.85f, 1.1f));
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 move = Vector3.zero;

        switch (CurrentState)
        {
            case State.Dormant:
            {
                // Walking, she has to almost stumble into one. Sprinting is loud and seen from far off
                // (and Noise() wakes them from further still).
                Transform t = Nearest(out float dist);
                float reach = aggroRadius;
                if (t == player) reach *= (PlayerMovement.Instance != null && PlayerMovement.Instance.IsRunning) ? 1.2f : 0.5f;
                if (t == player && Home.Instance != null && Home.Instance.InYard(player.position, 0f)) break;   // inside the fence she is nothing to them
                if (t != null && dist <= reach)
                    Wake();
                break;
            }

            case State.Hunting:
            {
                Transform t = Nearest(out float dist);
                if (t == null || dist > loseRadius * HomeBonuses.StalkerLoseMultiplier)
                {
                    CurrentState = State.Dormant; huntStart = -1f;   // lost her: the chase clock resets
                    break;
                }
                if (t == player && Home.Instance != null && Home.Instance.InYard(player.position, 0f))
                {
                    // She made the fence. Whatever it is, it will not follow her in: it turns and
                    // walks back into the trees, and goes quiet out there.
                    Vector3 home = Home.Instance.transform.position; home.y = 0f;
                    retreatDir = transform.position - home; retreatDir.y = 0f;
                    retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
                    retreatUntil = Time.time + yardRetreatSeconds;
                    retreatToDormant = true;
                    CurrentState = State.Retreating;
                    break;
                }

                if (dist <= catchRadius)
                {
                    Catch(t);
                    break;
                }

                Vector3 to = t.position - transform.position;
                to.y = 0f;
                if (huntStart < 0f) huntStart = Time.time;
                float chase = Time.time - huntStart;
                float speed = Mathf.Min(maxSpeed, baseSpeed + chase * huntAccel) * SpeedFactor;
                move = to.normalized * speed;
                break;
            }

            case State.Retreating:
                if (Time.time >= retreatUntil) { CurrentState = retreatToDormant ? State.Dormant : State.Hunting; if (retreatToDormant) huntStart = -1f; retreatToDormant = false; }
                else move = retreatDir * retreatSpeed * SpeedFactor;
                break;

            case State.Stunned:
                knock = Vector3.Lerp(knock, Vector3.zero, 1f - Mathf.Exp(-knockbackDamping * Time.deltaTime));
                move = knock;
                if (Time.time >= stunUntil) CurrentState = State.Hunting;
                break;

            case State.Grabbing:
            {
                // Holding her. Face her; if the time runs out she is taken.
                Vector3 toP = player.position - transform.position; toP.y = 0f;
                if (toP.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toP.normalized, Vector3.up);
                if (Time.time >= grabUntil)
                {
                    ReleasePlayer();
                    CurrentState = State.Dormant; huntStart = -1f;   // lost her: the chase clock resets
                    if (StalkerDirector.Instance != null) StalkerDirector.Instance.PlayerCaught();
                }
                break;
            }
        }

        Vector3 velocity = move;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        Vector3 flat = move; flat.y = 0f;
        if (CurrentState != State.Stunned && flat.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, flat.magnitude, 0.1f, Time.deltaTime);
            animator.SetBool(StunnedHash, CurrentState == State.Stunned);
        }

        if (glow != null)
        {
            float want;
            if (CurrentState == State.Stunned)
                want = huntingGlow * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 40f))); // flicker
            else
                want = CurrentState == State.Dormant ? dormantGlow : huntingGlow;
            glow.intensity = Mathf.Lerp(glow.intensity, want, 1f - Mathf.Exp(-glowLerp * Time.deltaTime));
        }
    }

    /// <summary>Pistol hit. Knocked back along the shot, staggered briefly, and slower from now on. Breaks a grab. Never kills.</summary>
    public void Hit(Vector3 impulse)
    {
        hits++;
        if (CurrentState == State.Grabbing) ReleasePlayer();
        knock = impulse;
        knock.y = 0f;
        stunUntil = Time.time + stunSeconds;
        CurrentState = State.Stunned;
        if (glow != null) glow.intensity = huntingGlow * 2f;
    }

    /// <summary>Nearest of: the player, any survivor currently following. Flat distance.</summary>
    private Transform Nearest(out float distance)
    {
        Transform best = null;
        float bestSq = float.MaxValue;

        Consider(player, ref best, ref bestSq);
        foreach (Survivor s in Survivor.All)
            if (s.CurrentState == Survivor.State.Following || s.CurrentState == Survivor.State.Panicked)
                Consider(s.transform, ref best, ref bestSq);

        distance = best != null ? Mathf.Sqrt(bestSq) : float.MaxValue;
        return best;
    }

    private void Consider(Transform t, ref Transform best, ref float bestSq)
    {
        if (t == null) return;
        Vector3 d = t.position - transform.position;
        d.y = 0f;
        float sq = d.sqrMagnitude;
        if (sq < bestSq) { bestSq = sq; best = t; }
    }

    private void Catch(Transform target)
    {
        if (animator != null) animator.SetTrigger(AttackHash);
        Survivor s = target.GetComponent<Survivor>();
        if (s != null)
        {
            s.Taken();
            retreatDir = transform.position - player.position;
            retreatDir.y = 0f;
            retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
            retreatUntil = Time.time + retreatSeconds;
            CurrentState = State.Retreating;
            return;
        }

        // The player: grab her. She has a moment to put a round in it.
        if (CurrentState == State.Grabbing) return;
        CurrentState = State.Grabbing;
        grabUntil = Time.time + grabSeconds;
        grabbedMovement = player.GetComponent<PlayerMovement>();
        if (grabbedMovement != null) grabbedMovement.Grabbed = true;
        if (animator != null) animator.SetTrigger(AttackHash);
        if (AudioManager.Instance != null) AudioManager.Instance.Play(AudioManager.Instance.Growl, transform.position, 1f, 40f, 0.7f);
    }

    private void ReleasePlayer()
    {
        if (grabbedMovement != null) grabbedMovement.Grabbed = false;
        grabbedMovement = null;
    }

    private void OnDestroy()
    {
        ReleasePlayer();
    }
}
