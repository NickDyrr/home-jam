using UnityEngine;

/// <summary>
/// The thing outside. Stands dormant in the dark until the player or an
/// escorted survivor comes within aggroRadius, then hunts whichever of them
/// is nearest. Gives up and goes dormant again if the nearest one gets past
/// loseRadius. It gets faster the longer a chase lasts, and slower for every bullet it takes.
/// A pistol hit staggers it, knocks it back and slows it for good; it never dies.
/// Light scares it and sound draws it: the muzzle flash sends any near it running for a
/// while, the bang wakes those further out, and a burning fire is ground it will not cross.
/// Never seen clearly; it is a silhouette with a cold glow.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Stalker : MonoBehaviour
{
    public enum State { Dormant, Hunting, Retreating, Stunned, Grabbing, Striking, Lured }

    private Vector3 lurePos;
    private float lureUntil;

    /// <summary>The bell: every stalker walks toward a point for a while, stopping at the fence, then goes quiet out there.</summary>
    public static void Lure(Vector3 pos, float seconds)
    {
        foreach (Stalker s in All)
        {
            if (s.dismissed || s.CurrentState == State.Grabbing) continue;
            if (s.CurrentState == State.Striking) { s.strikeTarget = null; s.strikeTransform = null; }
            s.lurePos = pos; s.lureUntil = Time.time + seconds;
            s.huntStart = -1f; s.phase = Phase.Swipe;
            s.CurrentState = State.Lured;
        }
    }

    /// <summary>Every live stalker in the scene.</summary>
    public static readonly System.Collections.Generic.List<Stalker> All = new System.Collections.Generic.List<Stalker>();

    /// <summary>A noise at a point: every stalker within radius that is not busy with someone goes to look, then stands there quiet.</summary>
    public static void LureNear(Vector3 pos, float radius, float seconds)
    {
        float sq = radius * radius;
        foreach (Stalker s in All)
        {
            if (s.dismissed || s.CurrentState == State.Grabbing || s.CurrentState == State.Striking) continue;
            Vector3 d = s.transform.position - pos; d.y = 0f;
            if (d.sqrMagnitude > sq) continue;
            s.lurePos = pos; s.lureUntil = Time.time + seconds;
            s.huntStart = -1f; s.phase = Phase.Swipe;
            s.CurrentState = State.Lured;
        }
    }

    /// <summary>Caught in a trap: held where it stands for a while, then it carries on.</summary>
    public void Trapped(float seconds)
    {
        if (CurrentState == State.Grabbing) ReleasePlayer();
        strikeTarget = null; strikeTransform = null;
        CancelSwing();
        knock = Vector3.zero;
        stunUntil = Time.time + seconds;
        CurrentState = State.Stunned;
        if (AudioManager.Instance != null) AudioManager.Instance.Play(AudioManager.Instance.Growl, transform.position, 1f, 40f, 0.6f);
    }

    [Header("Light")]
    [Tooltip("A burst of light (the muzzle flash) sends them running for this long, then they stand quiet out there.")]
    [SerializeField] private float scareSeconds = 4f;
    [Tooltip("After a scare, seconds before it will notice her again unless she walks right into it.")]
    [SerializeField] private float spookedSeconds = 5f;
    private float spookedUntil;

    /// <summary>
    /// Light. Every stalker within radius of the flash turns and runs from it, then goes quiet
    /// where it stops. It only holds while she keeps away; linger, or come back, and it wakes.
    /// One holding her is not let go by light alone; that takes a round in it.
    /// </summary>
    public static void Scare(Vector3 pos, float radius)
    {
        float sq = radius * radius;
        foreach (Stalker s in All)
        {
            if (s.dismissed || s.CurrentState == State.Grabbing) continue;
            Vector3 d = s.transform.position - pos; d.y = 0f;
            if (d.sqrMagnitude > sq) continue;
            s.spookedUntil = Time.time + s.scareSeconds + s.spookedSeconds;
            if (s.CurrentState == State.Stunned) continue;     // it runs when it can stand again
            s.Flee(pos);
        }
    }

    [Tooltip("Seconds for the run to come up to speed after it turns.")]
    [SerializeField] private float fleeWindup = 0.7f;
    [Tooltip("The stagger on a hit before it turns and runs. Short: the bullet is a shove, not a stun.")]
    [SerializeField] private float hitStagger = 0.25f;
    private float fleeStart;
    private float huntSpeed;

    /// <summary>It leaves at the same pace it arrived: the speed of its last approach. (The round that sent it off no longer caps this; it slows the next approach instead.)</summary>
    private float FleeSpeed => huntSpeed > 0f ? huntSpeed : baseSpeed * SpeedFactor;

    /// <summary>Turn away from a point and run, then stand dormant out there.</summary>
    private void Flee(Vector3 from)
    {
        strikeTarget = null; strikeTransform = null;
        retreatDir = transform.position - from; retreatDir.y = 0f;
        retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
        fleeStart = Time.time; phase = Phase.Swipe;   // the turn is a real turn and the run builds from a walk; the next chase starts with a swipe
        retreatUntil = Time.time + scareSeconds;
        retreatToDormant = true;
        huntStart = -1f;
        CurrentState = State.Retreating;
    }

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
    [SerializeField] private float grabSeconds = 0.8f;

    private float grabUntil;
    private PlayerMovement grabbedMovement;

    [Header("Strike")]
    [Tooltip("Seconds from the swing starting to it landing. A hit in this window saves the survivor.")]
    [SerializeField] private float strikeWindup = 0.55f;
    [Tooltip("The target must still be this close when the swing lands.")]
    [SerializeField] private float strikeReach = 2.3f;
    [Tooltip("Moving faster than this when the swipe lands, and it misses. (The grab, second time round, lands anyway.)")]
    [SerializeField] private float swipeDodgeSpeed = 0.5f;
    private Transform strikeTransform;      // whoever the swing is for: her or a survivor
    private Survivor strikeTarget;          // the survivor, when it is one
    private float strikeAt;

    /// <summary>
    /// A chase has three phases. Chase; then the first time it reaches someone it swipes, which
    /// misses anyone still moving; then, having missed, it grabs, and a grab holds even a runner.
    /// The phase resets when the chase ends.
    /// </summary>
    private enum Phase { Swipe, Grab }
    private Phase phase = Phase.Swipe;

    [Header("Swing")]
    [Tooltip("Speed of the swipe clip up to the moment it lands. The grab and strike windows shrink by the same factor.")]
    [SerializeField] private float swingWindupSpeed = 1.25f;
    [Tooltip("Speed of the rest of the swipe, once it has landed or missed. It stands still until the swing is done, so keep this quick.")]
    [SerializeField] private float swingRecoverSpeed = 3f;
    private const float SwipeLands = 0.8f;            // seconds into the swipe clip, at speed 1, where the blow lands
    private const float SwipeExit = 0.9f;             // the controller leaves the Swipe state at this fraction of the clip
    private float swipeLength = 2.43f;                // read from the clip in Awake; this is the Mixamo swipe
    private float swingUntil;                         // it stands where it is until then
    private bool hasSwipeSpeed;

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
    private static readonly int SwipeSpeedHash = Animator.StringToHash("SwipeSpeed");

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
        if (animator != null)
        {
            foreach (var prm in animator.parameters) if (prm.nameHash == SwipeSpeedHash) hasSwipeSpeed = true;
            if (animator.runtimeAnimatorController != null)
                foreach (var c in animator.runtimeAnimatorController.animationClips) if (c != null && c.name == "swiping") swipeLength = c.length;
        }
    }

    /// <summary>The swing starts: the clip runs at the windup speed until it lands.</summary>
    private void BeginSwing()
    {
        if (animator == null) return;
        if (hasSwipeSpeed) animator.SetFloat(SwipeSpeedHash, swingWindupSpeed);
        animator.SetTrigger(AttackHash);
        swingUntil = Time.time + SwipeLands / swingWindupSpeed + (swipeLength * SwipeExit - SwipeLands) / swingRecoverSpeed;
    }

    /// <summary>The blow has landed or missed: the rest of the swing plays fast, and it stands until that is done.</summary>
    private void SwingLanded()
    {
        if (animator != null && hasSwipeSpeed) animator.SetFloat(SwipeSpeedHash, swingRecoverSpeed);
        swingUntil = Time.time + (swipeLength * SwipeExit - SwipeLands) / swingRecoverSpeed;
    }

    /// <summary>Something cut the swing short (a round, a trap): nothing left to stand still for.</summary>
    private void CancelSwing() { swingUntil = 0f; }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>Dawn. It turns away from the house and walks off into the trees, then is gone.</summary>
    public void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        if (CurrentState == State.Grabbing) ReleasePlayer();
        Vector3 home = Home.Instance != null ? Home.Instance.transform.position : Vector3.zero; home.y = 0f;
        retreatDir = transform.position - home; retreatDir.y = 0f;
        retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
        retreatUntil = Time.time + dismissSeconds;
        retreatToDormant = false;
        CurrentState = State.Retreating;
    }
    private bool dismissed;
    [SerializeField] private float dismissSeconds = 7f;

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
                if (t != null && Refuge.Shelters(t.position)) break;   // inside the fence, a refuge or firelight, nothing to them
                if (Time.time < spookedUntil) reach = Mathf.Min(reach, 4f);   // just scared off: only if she walks into it
                if (t != null && dist <= reach)
                    Wake();
                break;
            }

            case State.Hunting:
            {
                Transform t = Nearest(out float dist);
                if (t == null || dist > loseRadius * HomeBonuses.StalkerLoseMultiplier)
                {
                    CurrentState = State.Dormant; huntStart = -1f; hitsThisChase = 0; phase = Phase.Swipe;   // lost her: the chase clock resets
                    break;
                }
                if (Refuge.Shelters(t.position))
                {
                    // They made the fence, the shack or a burning fire. It will not follow into the
                    // light: it turns and walks back into the trees, and goes quiet out there.
                    retreatDir = transform.position - t.position; retreatDir.y = 0f;
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
                huntSpeed = speed;                        // remembered: it runs off at the pace it came in at
                move = to.normalized * speed;
                break;
            }

            case State.Retreating:
                if (Time.time >= retreatUntil)
                {
                    if (dismissed) { Destroy(gameObject); return; }
                    CurrentState = retreatToDormant ? State.Dormant : State.Hunting; if (retreatToDormant) huntStart = -1f; retreatToDormant = false;
                }
                else if (dismissed) move = retreatDir * 1.8f;                                              // dawn: a walk, not a rout
                else if (Time.time < spookedUntil)
                {
                    // Scared: it turns, then the run builds up over the first moments and holds.
                    float up = Mathf.SmoothStep(0f, 1f, (Time.time - fleeStart) / Mathf.Max(0.05f, fleeWindup));
                    move = retreatDir * Mathf.Lerp(1.5f, FleeSpeed, up);
                }
                else move = retreatDir * (retreatSpeed * SpeedFactor);
                break;

            case State.Stunned:
                knock = Vector3.Lerp(knock, Vector3.zero, 1f - Mathf.Exp(-knockbackDamping * Time.deltaTime));
                move = knock;
                if (Time.time >= stunUntil)
                {
                    // Back on its feet. If the flash reached it, it runs from where she stood.
                    if (Time.time < spookedUntil) Flee(player.position);
                    else CurrentState = State.Hunting;
                }
                break;

            case State.Lured:
            {
                // Drawn to the bell. Walks to the edge of the yard and stands there until the
                // pull fades, then turns back into the trees.
                Vector3 toBell = lurePos - transform.position; toBell.y = 0f;
                bool atFence = Refuge.Shelters(transform.position + toBell.normalized * 2.5f, 0f);
                if (Time.time >= lureUntil)
                {
                    retreatDir = -toBell.normalized; retreatUntil = Time.time + yardRetreatSeconds; retreatToDormant = true;
                    CurrentState = State.Retreating;
                }
                else if (!atFence && toBell.magnitude > 2f) move = toBell.normalized * Mathf.Min(maxSpeed, baseSpeed * 1.3f) * SpeedFactor;
                break;
            }

            case State.Striking:
            {
                if (strikeTransform == null || (strikeTarget != null && strikeTarget.CurrentState == Survivor.State.Dead)) { strikeTransform = null; strikeTarget = null; CurrentState = State.Hunting; break; }
                Vector3 toS = strikeTransform.position - transform.position; toS.y = 0f;
                if (toS.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toS.normalized, Vector3.up);
                if (Time.time >= strikeAt)
                {
                    SwingLanded();
                    // A swipe only lands on someone standing still; the grab lands on anyone in reach.
                    bool lands = toS.magnitude <= strikeReach && (phase == Phase.Grab || !IsMoving(strikeTransform));
                    if (lands && strikeTarget != null)
                    {
                        FaceMe(strikeTarget.transform);   // the blow comes from the front, so the body flies away from it
                        strikeTarget.Taken();
                        retreatDir = transform.position - player.position; retreatDir.y = 0f;
                        retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
                        retreatUntil = Time.time + retreatSeconds;
                        CurrentState = State.Retreating;
                    }
                    else if (lands) GrabPlayer(false);      // she stood still: it has her
                    else { phase = Phase.Grab; CurrentState = State.Hunting; }   // missed: next time it grabs
                    strikeTransform = null; strikeTarget = null;
                }
                break;
            }

            case State.Grabbing:
            {
                // Holding her. Face her; if the time runs out she is taken.
                Vector3 toP = player.position - transform.position; toP.y = 0f;
                if (toP.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toP.normalized, Vector3.up);
                if (Time.time >= grabUntil)
                {
                    SwingLanded();
                    ReleasePlayer();
                    FaceMe(player);   // she goes down away from it, not through it
                    if (StalkerDirector.Instance != null) StalkerDirector.Instance.PlayerCaught();
                    // Done with her. It backs off into the dark and leaves her where she fell.
                    retreatDir = transform.position - player.position; retreatDir.y = 0f;
                    retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
                    retreatUntil = Time.time + yardRetreatSeconds; retreatToDormant = true; huntStart = -1f; phase = Phase.Swipe;
                    CurrentState = State.Retreating;
                }
                break;
            }
        }

        // Mid-swing it stands its ground; it used to run on with the swipe still playing and slide at her.
        if (Time.time < swingUntil && CurrentState != State.Stunned && !dismissed) move = Vector3.zero;

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

    /// <summary>
    /// Pistol hit. Shoved back along the shot for a beat, then it wheels and runs from her; slower
    /// from now on. Breaks a grab. Never kills.
    /// </summary>
    public void Hit(Vector3 impulse, int weight = 1)
    {
        hits += weight; hitsThisChase += weight;
        if (CurrentState == State.Grabbing) ReleasePlayer();
        CancelSwing();
        knock = impulse;
        knock.y = 0f;
        stunUntil = Time.time + hitStagger;
        // They learn. Early on one round sends it running; later it takes two, then three in the same chase.
        if (hitsThisChase >= ShotsToDriveOff)
        {
            spookedUntil = Mathf.Max(spookedUntil, Time.time + scareSeconds + spookedSeconds);
            hitsThisChase = 0;
        }
        CurrentState = State.Stunned;
        if (glow != null) glow.intensity = huntingGlow * 2f;
    }

    private int hitsThisChase;

    /// <summary>Rounds it takes, in one chase, to make it run: one on the first night, two late that night and on the second, three from the third.</summary>
    public static int ShotsToDriveOff
    {
        get
        {
            int nights = StalkerDirector.Instance != null ? StalkerDirector.Instance.Nights : 0;
            float progress = DayNightCycle.Instance != null ? DayNightCycle.Instance.NightProgress : 0f;
            return Mathf.Clamp(1 + nights + (progress > 0.7f ? 1 : 0), 1, 3);
        }
    }

    /// <summary>Nearest of: the player, any survivor currently following. Flat distance.</summary>
    /// <summary>
    /// The one it goes for. A survivor she is escorting comes first: the nearest of them, if any is
    /// within loseRadius. Only with nobody to take does it turn on her.
    /// </summary>
    private Transform Nearest(out float distance)
    {
        Transform best = null;
        float bestSq = float.MaxValue;

        foreach (Survivor s in Survivor.All)
            if (s.CurrentState == Survivor.State.Following || s.CurrentState == Survivor.State.Panicked)
                Consider(s.transform, ref best, ref bestSq);
        float lose = loseRadius * HomeBonuses.StalkerLoseMultiplier;
        if (best == null || bestSq > lose * lose)
        {
            best = null; bestSq = float.MaxValue;
            Consider(player, ref best, ref bestSq);
        }

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
        Survivor s = target.GetComponent<Survivor>();
        if (phase == Phase.Swipe || s != null)
        {
            // The swing takes a moment to land. A swipe misses anyone who keeps moving; a grab
            // (the second time it reaches a survivor) lands on anyone still in reach. A round in
            // the stalker before it lands breaks it off.
            BeginSwing();
            strikeTransform = target; strikeTarget = s;
            strikeAt = Time.time + strikeWindup / swingWindupSpeed;
            CurrentState = State.Striking;
            return;
        }
        GrabPlayer(true);   // grab phase, and it is her: it has her, running or not
    }

    /// <summary>Turns a victim to face this stalker, flat, so the flying-back death carries them away from it.</summary>
    private void FaceMe(Transform victim)
    {
        if (victim == null) return;
        Vector3 to = transform.position - victim.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f) victim.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    /// <summary>Faster than a shuffle, flat.</summary>
    private bool IsMoving(Transform t)
    {
        var cc = t.GetComponent<CharacterController>();
        Vector3 v = cc != null && cc.enabled ? cc.velocity : Vector3.zero; v.y = 0f;
        return v.magnitude > swipeDodgeSpeed;
    }

    /// <summary>It has her. She has a moment to put a round in it; then she is taken.</summary>
    private void GrabPlayer(bool swing)
    {
        if (CurrentState == State.Grabbing) return;
        if (swing) BeginSwing();
        CurrentState = State.Grabbing;
        grabUntil = Time.time + grabSeconds / swingWindupSpeed;
        grabbedMovement = player.GetComponent<PlayerMovement>();
        if (grabbedMovement != null) grabbedMovement.Grabbed = true;
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
