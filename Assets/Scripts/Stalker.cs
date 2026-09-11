using UnityEngine;

/// <summary>
/// The thing outside. Stands dormant in the dark until the player or an
/// escorted survivor comes within aggroRadius, then hunts whichever of them
/// is nearest. Gives up and goes dormant again if the nearest one gets past
/// loseRadius. Speed ramps with time spent outside.
/// A pistol hit stuns it and knocks it back; it never dies.
/// Never seen clearly; it is a silhouette with a cold glow.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Stalker : MonoBehaviour
{
    public enum State { Dormant, Hunting, Retreating, Stunned }

    [Header("Awareness")]
    [SerializeField] private float aggroRadius = 14f;
    [SerializeField] private float loseRadius = 22f;

    [Header("Movement")]
    [SerializeField] private float baseSpeed = 4f;
    [SerializeField] private float speedPerSecondOutside = 0.06f;
    [SerializeField] private float maxSpeed = 6.5f;
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float catchRadius = 1.3f;
    [SerializeField] private float retreatSeconds = 2.5f;
    [SerializeField] private float retreatSpeed = 5f;

    [Header("Hit")]
    [SerializeField] private float stunSeconds = 3f;
    [SerializeField] private float knockbackDamping = 6f;

    [Header("Glow")]
    [SerializeField] private Light glow;
    [SerializeField] private float dormantGlow = 0.35f;
    [SerializeField] private float huntingGlow = 1.6f;
    [SerializeField] private float glowLerp = 4f;

    public State CurrentState { get; private set; } = State.Dormant;

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
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 move = Vector3.zero;

        switch (CurrentState)
        {
            case State.Dormant:
            {
                Transform t = Nearest(out float dist);
                if (t != null && dist <= aggroRadius)
                    CurrentState = State.Hunting;
                break;
            }

            case State.Hunting:
            {
                Transform t = Nearest(out float dist);
                if (t == null || dist > loseRadius)
                {
                    CurrentState = State.Dormant;
                    break;
                }

                if (dist <= catchRadius)
                {
                    Catch(t);
                    break;
                }

                Vector3 to = t.position - transform.position;
                to.y = 0f;
                float outside = StalkerDirector.Instance != null ? StalkerDirector.Instance.TimeOutside : 0f;
                float speed = Mathf.Min(maxSpeed, baseSpeed + outside * speedPerSecondOutside);
                move = to.normalized * speed;
                break;
            }

            case State.Retreating:
                if (Time.time >= retreatUntil) CurrentState = State.Hunting;
                else move = retreatDir * retreatSpeed;
                break;

            case State.Stunned:
                knock = Vector3.Lerp(knock, Vector3.zero, 1f - Mathf.Exp(-knockbackDamping * Time.deltaTime));
                move = knock;
                if (Time.time >= stunUntil) CurrentState = State.Hunting;
                break;
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

    /// <summary>Pistol hit. Knocked back along the shot, stunned for a few seconds.</summary>
    public void Hit(Vector3 impulse)
    {
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
            if (s.CurrentState == Survivor.State.Following)
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

        if (StalkerDirector.Instance != null) StalkerDirector.Instance.PlayerCaught();
    }
}
