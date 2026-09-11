using UnityEngine;

/// <summary>
/// Drops a footprint every stride of ground covered, alternating left and
/// right, offset to either side of the path. Distance-based so it works for
/// the animated player and the capsule survivors and stalkers alike.
/// </summary>
public class FootprintEmitter : MonoBehaviour
{
    [SerializeField] private float stride = 0.7f;
    [SerializeField] private float sideOffset = 0.14f;
    [SerializeField] private float printSize = 0.34f;
    [SerializeField] private Color tint = new Color(0.55f, 0.62f, 0.76f, 1f);
    [Tooltip("Ignore moves bigger than this in one frame (teleports, respawns).")]
    [SerializeField] private float teleportThreshold = 3f;

    private Vector3 lastPrintPos;
    private Vector3 lastPos;
    private bool leftNext;
    private float travelled;

    private void OnEnable()
    {
        lastPrintPos = transform.position;
        lastPos = transform.position;
        travelled = 0f;
    }

    private void LateUpdate()
    {
        if (FootprintManager.Instance == null) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        delta.y = 0f;
        lastPos = pos;

        float d = delta.magnitude;
        if (d > teleportThreshold) { lastPrintPos = pos; travelled = 0f; return; }
        if (d < 0.0005f) return;

        travelled += d;
        if (travelled < stride) return;
        travelled = 0f;

        Vector3 dir = pos - lastPrintPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();
        lastPrintPos = pos;

        Vector3 side = Vector3.Cross(Vector3.up, dir) * (leftNext ? -sideOffset : sideOffset);
        FootprintManager.Instance.Stamp(pos + side, dir, leftNext, printSize, tint);
        leftNext = !leftNext;
    }
}
