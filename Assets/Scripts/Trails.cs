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
    [Tooltip("How far from home every trail begins: just past the fence, so a short walk in any direction finds one.")]
    [SerializeField] private float startFromHome = 28f;
    [SerializeField] private float wobble = 6f;

    public static Trails Instance { get; private set; }
    private readonly System.Collections.Generic.Dictionary<Survivor, Vector3> starts = new System.Collections.Generic.Dictionary<Survivor, Vector3>();

    /// <summary>Where this survivor's trail begins, if one was laid.</summary>
    public bool StartOf(Survivor s, out Vector3 start) => starts.TryGetValue(s, out start);

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    [Tooltip("Trails that lead to a dark patch of snow and nothing else: someone who did not make it. Set in world XZ.")]
    [SerializeField] private Vector2[] deadEnds = { new Vector2(52f, 74f), new Vector2(-96f, -18f) };
    [SerializeField] private Material stainMaterial;

    private void Start()
    {
        var fm = FootprintManager.Instance;
        if (fm == null) return;
        var rng = new System.Random(seed);
        Vector3 home = Home.Instance != null ? Home.Instance.transform.position : Vector3.zero; home.y = 0f;

        // The ones who did not make it: a trail from the home side out to a dark patch, and no fire.
        foreach (var e in deadEnds)
        {
            Vector3 end = new Vector3(e.x, 0f, e.y);
            Vector3 toHome = home - end; float dist = toHome.magnitude; if (dist < 20f) continue;
            toHome /= dist;
            Vector3 side = Vector3.Cross(Vector3.up, toHome);
            // From just outside the yard, like every other set of tracks: nothing marks this one as a lie.
            Vector3 start = end + toHome * (dist - startFromHome) + side * (((float)rng.NextDouble() * 2f - 1f) * 5f);
            LayTrail(fm, rng, start, end, 0f);
            var stain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stain.name = "Stain"; Destroy(stain.GetComponent<Collider>());
            stain.transform.SetParent(transform, false);
            stain.transform.position = end + Vector3.up * 0.015f;
            stain.transform.localScale = new Vector3(2.6f, 0.005f, 2.1f);
            stain.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var r = stain.GetComponent<Renderer>();
            if (stainMaterial != null) r.sharedMaterial = stainMaterial;
            else { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", new Color(0.16f, 0.08f, 0.09f)); m.SetFloat("_Smoothness", 0.05f); r.material = m; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The landmarks, so every trail runs right past whatever stands on its way.
        var landmarks = new System.Collections.Generic.List<Vector3>();
        var lmRoot = GameObject.Find("Landmarks");
        if (lmRoot != null) foreach (Transform c in lmRoot.transform) { Vector3 p = c.position; p.y = 0f; landmarks.Add(p); }

        foreach (var s in Survivor.All)
        {
            if (s.CurrentState != Survivor.State.Waiting) continue;
            Vector3 camp = s.transform.position; camp.y = 0f;
            Vector3 toHome = home - camp; float dist = toHome.magnitude; if (dist < 1f) continue;
            toHome /= dist;
            Vector3 side = Vector3.Cross(Vector3.up, toHome);

            // Anything standing near the straight line between camp and home is a waypoint: the
            // trail begins beyond the farthest one and passes each on its way in.
            var vias = new System.Collections.Generic.List<(float along, Vector3 pos)>();
            foreach (var l in landmarks)
            {
                Vector3 d = l - camp; float along = Vector3.Dot(d, toHome), lat = Vector3.Dot(d, side);
                if (along > 12f && along < dist - 20f && Mathf.Abs(lat) < 18f) vias.Add((along, l));
            }
            vias.Sort((a, b) => b.along.CompareTo(a.along));   // farthest from the camp first

            // Every trail begins just outside the yard: they all ran from this house. Pick one at the
            // fence and follow it out; what it passes, and where it ends, is the search.
            float length = dist - startFromHome;
            if (length < 12f) continue;
            float lateral = ((float)rng.NextDouble() * 2f - 1f) * 5f;
            Vector3 start = camp + toHome * length + side * lateral;
            for (int guard = 0; guard < 10 && Home.Instance != null && Home.Instance.InYard(start, -3f); guard++)
                start = camp + toHome * (length -= 5f) + side * lateral;

            starts[s] = start;
            Vector3 from = start;
            foreach (var v in vias)
            {
                // Pass beside it, not through it: three metres to the side nearer the line.
                float lat = Vector3.Dot(v.pos - camp, side);
                Vector3 pass = v.pos - side * Mathf.Sign(lat == 0f ? 1f : lat) * 3.5f;
                LayTrail(fm, rng, from, pass, 0f);
                from = pass;
            }
            LayTrail(fm, rng, from, camp, 2.2f);
        }
    }

    /// <summary>A wandering line of prints from start to end, stopping short of the end by stopShort.</summary>
    private void LayTrail(FootprintManager fm, System.Random rng, Vector3 start, Vector3 end, float stopShort)
    {
        Vector3 line = end - start; float total = line.magnitude; if (total < 1f) return;
        Vector3 dir = line / total;
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
            if (Vector3.Distance(p, end) > stopShort)
                fm.StampPermanent(p + across, stepDir, left, printSize, tint, cutoff);
            left = !left;
            prev = p;
        }
    }
}
