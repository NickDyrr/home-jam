using UnityEngine;

/// <summary>
/// The clue that makes the search fair. Everyone who ran from the house left
/// tracks in the snow, and those tracks are still there: a wandering line of
/// prints that starts on the home side of each camp and leads to it. Cross a
/// trail while you search, follow it, and you find the fire. Built once at
/// start from a fixed seed, as permanent prints.
/// </summary>
public class Trails : MonoBehaviour
{
    [SerializeField] private int seed = 11;
    [SerializeField] private float stride = 0.7f;
    [SerializeField] private float sideOffset = 0.14f;
    [SerializeField] private float printSize = 0.42f;
    [Tooltip("Old tracks: greyer than fresh ones and part filled in.")]
    [SerializeField] private Color tint = new Color(0.47f, 0.53f, 0.68f, 1f);
    [SerializeField] private float cutoff = 0.62f;
    [Tooltip("How far from the camp the trail begins, toward home.")]
    [SerializeField] private float minLength = 45f, maxLength = 70f;
    [SerializeField] private float wobble = 6f;

    public static Trails Instance { get; private set; }
    private readonly System.Collections.Generic.Dictionary<Survivor, Vector3> starts = new System.Collections.Generic.Dictionary<Survivor, Vector3>();

    /// <summary>Where this survivor's trail begins, if one was laid.</summary>
    public bool StartOf(Survivor s, out Vector3 start) => starts.TryGetValue(s, out start);

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        var fm = FootprintManager.Instance;
        if (fm == null) return;
        var rng = new System.Random(seed);
        Vector3 home = Home.Instance != null ? Home.Instance.transform.position : Vector3.zero; home.y = 0f;

        foreach (var s in Survivor.All)
        {
            if (s.CurrentState != Survivor.State.Waiting) continue;
            Vector3 camp = s.transform.position; camp.y = 0f;
            Vector3 toHome = home - camp; float dist = toHome.magnitude; if (dist < 1f) continue;
            toHome /= dist;
            Vector3 side = Vector3.Cross(Vector3.up, toHome);

            // Start on the home side, off the straight line, but never inside the yard.
            float length = Mathf.Min(Mathf.Lerp(minLength, maxLength, (float)rng.NextDouble()), dist - 16f);
            if (length < 12f) continue;
            float lateral = ((float)rng.NextDouble() * 2f - 1f) * 15f;
            Vector3 start = camp + toHome * length + side * lateral;
            for (int guard = 0; guard < 10 && Home.Instance != null && Home.Instance.InYard(start, -3f); guard++)
                start = camp + toHome * (length -= 5f) + side * lateral;

            starts[s] = start;

            // Walk from start to camp with a wandering sideways drift.
            Vector3 line = camp - start; float total = line.magnitude; Vector3 dir = line / total;
            Vector3 lat = Vector3.Cross(Vector3.up, dir);
            float phase1 = (float)rng.NextDouble() * 6.28f, phase2 = (float)rng.NextDouble() * 6.28f;
            int steps = Mathf.CeilToInt(total / stride);
            Vector3 prev = start;
            bool left = false;
            for (int i = 1; i <= steps; i++)
            {
                float u = i / (float)steps;
                float drift = (Mathf.Sin(u * 9f + phase1) * 0.6f + Mathf.Sin(u * 23f + phase2) * 0.4f) * wobble * Mathf.Sin(u * Mathf.PI);
                Vector3 p = start + dir * (u * total) + lat * drift;
                Vector3 stepDir = p - prev; if (stepDir.sqrMagnitude < 0.0001f) stepDir = dir;
                stepDir.Normalize();
                Vector3 across = Vector3.Cross(Vector3.up, stepDir) * (left ? -sideOffset : sideOffset);
                if (Vector3.Distance(p, camp) > 2.2f)
                    fm.StampPermanent(p + across, stepDir, left, printSize, tint, cutoff);
                left = !left;
                prev = p;
            }
        }
    }
}
