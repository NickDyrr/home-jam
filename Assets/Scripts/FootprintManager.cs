using UnityEngine;

/// <summary>
/// Pool of footprint stamps on the snow. Emitters call Stamp(); the oldest
/// print is recycled when the pool is full. Prints erode over lifetime by
/// raising the alpha cutoff, so the snow appears to fill them back in.
/// Only stamps outside the HomeZone: there is no snow indoors.
/// </summary>
public class FootprintManager : MonoBehaviour
{
    public static FootprintManager Instance { get; private set; }

    [SerializeField] private Material printMaterial;
    [SerializeField] private int poolSize = 600;
    [SerializeField] private float lifetime = 90f;
    [SerializeField] private float groundY = 0.012f;
    [SerializeField] private float startCutoff = 0.45f;
    [SerializeField] private float endCutoff = 1.0f;

    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    private struct Print
    {
        public Transform t;
        public MeshRenderer r;
        public MeshFilter f;
        public float born;
        public bool live;
        public Color tint;
    }

    private Print[] pool;
    private int next;
    private Mesh meshRight, meshLeft;
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        Instance = this;
        mpb = new MaterialPropertyBlock();
        meshRight = BuildQuad(false);
        meshLeft = BuildQuad(true);

        pool = new Print[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            GameObject g = new GameObject("Print");
            g.transform.SetParent(transform, false);
            MeshFilter f = g.AddComponent<MeshFilter>();
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = printMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            g.SetActive(false);
            pool[i] = new Print { t = g.transform, r = r, f = f, live = false };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].live) continue;
            float age = (now - pool[i].born) / lifetime;
            if (age >= 1f)
            {
                pool[i].live = false;
                pool[i].t.gameObject.SetActive(false);
                continue;
            }
            mpb.SetFloat(CutoffId, Mathf.Lerp(startCutoff, endCutoff, age * age));
            mpb.SetColor(ColorId, pool[i].tint);
            pool[i].r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// Stamp one print. dir is the walking direction (flat). left picks the
    /// mirrored mesh. material overrides the default print look (null = boot).
    /// </summary>
    public void Stamp(Vector3 pos, Vector3 dir, bool left, float size, Color tint, Material material = null)
    {
        if (HomeZone.Instance != null && HomeZone.Instance.Contains(pos)) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.y = 0f;

        Print p = pool[next];
        next = (next + 1) % pool.Length;

        Material want = material != null ? material : printMaterial;
        if (p.r.sharedMaterial != want) p.r.sharedMaterial = want;
        p.t.position = new Vector3(pos.x, groundY, pos.z);
        p.t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        p.t.localScale = Vector3.one * size;
        p.f.sharedMesh = left ? meshLeft : meshRight;
        p.born = Time.time;
        p.live = true;
        p.tint = tint;
        p.t.gameObject.SetActive(true);
        mpb.SetFloat(CutoffId, startCutoff);
        mpb.SetColor(ColorId, tint);
        p.r.SetPropertyBlock(mpb);
        pool[(next + pool.Length - 1) % pool.Length] = p;
    }

    // 1x1 quad on the XZ plane, normal up, +Z is the toe direction.
    private static Mesh BuildQuad(bool mirrorU)
    {
        Mesh m = new Mesh();
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f,  0.5f), new Vector3(0.5f, 0f,  0.5f),
        };
        float u0 = mirrorU ? 1f : 0f, u1 = mirrorU ? 0f : 1f;
        m.uv = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }
}
