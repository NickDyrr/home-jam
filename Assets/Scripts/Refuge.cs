using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A patch of ground they will not enter: the yard is one, the ruined shack is
/// another. A stalker chasing her breaks off when she steps into one, and
/// dormant ones ignore her there. Nothing else happens in a refuge; it is a
/// place to breathe and stage the next leg.
/// </summary>
public class Refuge : MonoBehaviour
{
    public static readonly List<Refuge> All = new List<Refuge>();

    [Tooltip("Half-size of the safe box on the ground, in metres.")]
    [SerializeField] private Vector2 halfSize = new Vector2(5f, 5f);

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    public bool Contains(Vector3 p, float margin)
    {
        Vector3 local = transform.InverseTransformPoint(p);
        return Mathf.Abs(local.x) <= halfSize.x - margin && Mathf.Abs(local.z) <= halfSize.y - margin;
    }

    /// <summary>True inside the fenced yard (grown by the porch lantern's grace) or any refuge.</summary>
    public static bool Shelters(Vector3 p, float margin = 0f)
    {
        if (Home.Instance != null && Home.Instance.InYard(p, margin - Home.Instance.YardGrace)) return true;
        foreach (var r in All) if (r.Contains(p, margin)) return true;
        return false;
    }
}
