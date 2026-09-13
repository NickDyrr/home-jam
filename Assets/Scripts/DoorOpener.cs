using UnityEngine;

/// <summary>
/// A hinged door that swings open when the player or a following survivor
/// comes near, from either side, and closes again when they leave.
/// Put this on the hinge pivot; the door mesh is a child offset so its
/// hinge edge sits on the pivot.
/// </summary>
public class DoorOpener : MonoBehaviour
{
    [SerializeField] private float openDistance = 2.3f;
    [SerializeField] private float openAngle = -100f;
    [SerializeField] private float openSpeed = 7f;
    [SerializeField] private float closeSpeed = 4f;

    private Transform player;
    private Quaternion closedRot;
    private Quaternion openRot;
    private float t;

    public bool IsOpen => t > 0.5f;

    /// <summary>Set by the intro: doors stay shut no matter who is near.</summary>
    public static bool HoldClosed;

    private void Awake()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        bool near = false;
        float d2 = openDistance * openDistance;
        Vector3 here = transform.position;

        if (player != null && Flat(player.position - here) < d2) near = true;
        if (!near)
        {
            foreach (Survivor s in Survivor.All)
            {
                if (s.CurrentState != Survivor.State.Following && s.CurrentState != Survivor.State.Settling) continue;
                if (Flat(s.transform.position - here) < d2) { near = true; break; }
            }
        }

        if (HoldClosed) near = false;
        float target = near ? 1f : 0f;
        float speed = near ? openSpeed : closeSpeed;
        t = Mathf.MoveTowards(t, target, speed * Time.deltaTime * 0.25f);
        float eased = t * t * (3f - 2f * t);
        transform.localRotation = Quaternion.Slerp(closedRot, openRot, eased);
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude;
    }
}
