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

    /// <summary>Doors currently in use (the current house level's).</summary>
    public static readonly System.Collections.Generic.List<DoorOpener> Active = new System.Collections.Generic.List<DoorOpener>();

    /// <summary>Ground point in the middle of the doorway, and the direction that leads outside.</summary>
    public Vector3 Doorway { get; private set; }
    public Vector3 Outward { get; private set; }

    private void Awake()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);

        // The door is closed at this point, so its mesh spans the doorway.
        Bounds b = new Bounds(transform.position, Vector3.zero); bool first = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        Doorway = new Vector3(b.center.x, 0f, b.center.z);
        Vector3 f = transform.forward; f.y = 0f;
        Outward = -f.normalized;   // every door here has its outside on the far side of its forward axis
    }

    private void OnEnable()  { Active.Add(this); }
    private void OnDisable() { Active.Remove(this); }

    /// <summary>Waypoints that take a survivor from the yard in through the current front door.</summary>
    public static System.Collections.Generic.List<Vector3> HomeRoute()
    {
        var route = new System.Collections.Generic.List<Vector3>();
        if (Active.Count == 0) return route;
        DoorOpener d = Active[0];
        route.Add(d.Doorway + d.Outward * 1.8f);
        route.Add(d.Doorway - d.Outward * 1.2f);
        return route;
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
                if (s.CurrentState != Survivor.State.Following && s.CurrentState != Survivor.State.Entering && s.CurrentState != Survivor.State.Settling) continue;
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
