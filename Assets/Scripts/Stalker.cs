using UnityEngine;

/// <summary>
/// The thing outside. Walks toward whoever is slowest: a following survivor
/// if there is one, otherwise the player. Speed ramps with time spent outside.
/// Never seen clearly; it is a silhouette with a cold glow.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Stalker : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 4f;
    [SerializeField] private float speedPerSecondOutside = 0.06f;
    [SerializeField] private float maxSpeed = 6.5f;
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float catchRadius = 1.3f;
    [SerializeField] private float retreatSeconds = 2.5f;
    [SerializeField] private float retreatSpeed = 5f;

    private CharacterController controller;
    private Transform player;
    private float retreatUntil;
    private Vector3 retreatDir;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 move;

        if (Time.time < retreatUntil)
        {
            move = retreatDir * retreatSpeed;
        }
        else
        {
            Transform target = PickTarget();
            Vector3 to = target.position - transform.position;
            to.y = 0f;

            if (to.magnitude <= catchRadius)
            {
                Catch(target);
                return;
            }

            float outside = StalkerDirector.Instance != null ? StalkerDirector.Instance.TimeOutside : 0f;
            float speed = Mathf.Min(maxSpeed, baseSpeed + outside * speedPerSecondOutside);
            move = to.normalized * speed;
        }

        Vector3 velocity = move;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        Vector3 flat = move; flat.y = 0f;
        if (flat.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
    }

    private Transform PickTarget()
    {
        Survivor best = null;
        float bestDist = float.MaxValue;
        foreach (Survivor s in Survivor.All)
        {
            if (s.CurrentState != Survivor.State.Following) continue;
            float d = (s.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best != null ? best.transform : player;
    }

    private void Catch(Transform target)
    {
        Survivor s = target.GetComponent<Survivor>();
        if (s != null)
        {
            s.Taken();
            retreatDir = (transform.position - player.position);
            retreatDir.y = 0f;
            retreatDir = retreatDir.sqrMagnitude > 0.01f ? retreatDir.normalized : -transform.forward;
            retreatUntil = Time.time + retreatSeconds;
            return;
        }

        if (StalkerDirector.Instance != null) StalkerDirector.Instance.PlayerCaught();
    }
}
