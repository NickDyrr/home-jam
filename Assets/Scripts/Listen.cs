using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold Q to stand still and listen. The nearest survivor still waiting out
/// there calls back: a ripple pulses on the snow in their direction and their
/// camp fire flares. Costs a couple of seconds of standing in the dark, which
/// is the point. Range grows when the Scout is home.
/// </summary>
public class Listen : MonoBehaviour
{
    [SerializeField] private float range = 60f;
    [SerializeField] private float cueDistance = 4f;
    [SerializeField] private float pulseSeconds = 1.2f;
    [SerializeField] private float flareEvery = 1.2f;
    [SerializeField] private Color cueColor = new Color(1f, 0.75f, 0.4f, 1f);

    public bool IsListening { get; private set; }

    private PlayerMovement movement;
    private Transform cue;
    private MeshRenderer cueRenderer;
    private Light cueLight;
    private Material cueMaterial;
    private MaterialPropertyBlock mpb;
    private float nextFlare;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        BuildCue();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        bool held = kb != null && kb.qKey.isPressed;
        IsListening = held;
        if (movement != null) movement.MovementLocked = held;

        if (!held) { cue.gameObject.SetActive(false); return; }

        Survivor target = NearestWaiting(out float dist);
        if (target == null) { cue.gameObject.SetActive(false); return; }

        Vector3 dir = target.transform.position - transform.position; dir.y = 0f; dir.Normalize();
        cue.gameObject.SetActive(true);
        cue.position = transform.position + dir * cueDistance + Vector3.up * 0.06f;

        // Pulse: expand and fade, faster when they are close.
        float period = Mathf.Lerp(0.5f, pulseSeconds, Mathf.Clamp01(dist / range));
        float t = (Time.time % period) / period;
        cue.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, t);
        Color c = cueColor; c.a = 1f - t;
        mpb.SetColor("_BaseColor", c);
        cueRenderer.SetPropertyBlock(mpb);
        if (cueLight != null) cueLight.intensity = 1.5f * (1f - t);

        if (Time.time >= nextFlare)
        {
            nextFlare = Time.time + flareEvery;
            Campfire fire = Campfire.Nearest(target.transform.position, 5f);
            if (fire != null) fire.Flare();
        }
    }

    private Survivor NearestWaiting(out float distance)
    {
        Survivor best = null; float bestSq = float.MaxValue;
        float r = range * HomeBonuses.ListenRangeMultiplier;
        foreach (Survivor s in Survivor.All)
        {
            if (s.CurrentState != Survivor.State.Waiting) continue;
            Vector3 d = s.transform.position - transform.position; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq && sq <= r * r) { bestSq = sq; best = s; }
        }
        distance = best != null ? Mathf.Sqrt(bestSq) : 0f;
        return best;
    }

    private void BuildCue()
    {
        var go = new GameObject("ListenCue");
        go.transform.SetParent(null);
        cue = go.transform;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildRing(0.72f, 1f, 40);
        cueRenderer = go.AddComponent<MeshRenderer>();
        cueRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cueRenderer.receiveShadows = false;

        cueMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        cueMaterial.SetFloat("_Surface", 1f);
        cueMaterial.SetFloat("_Blend", 0f);
        cueMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        cueMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        cueMaterial.SetFloat("_ZWrite", 0f);
        cueMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        cueMaterial.renderQueue = 3000;
        cueMaterial.SetColor("_BaseColor", cueColor);
        cueRenderer.sharedMaterial = cueMaterial;
        mpb = new MaterialPropertyBlock();

        var lightGo = new GameObject("CueLight");
        lightGo.transform.SetParent(cue, false);
        lightGo.transform.localPosition = Vector3.up * 0.6f;
        cueLight = lightGo.AddComponent<Light>();
        cueLight.type = LightType.Point; cueLight.color = cueColor; cueLight.range = 4f; cueLight.intensity = 0f;

        go.SetActive(false);
    }

    private static Mesh BuildRing(float inner, float outer, int segments)
    {
        var m = new Mesh();
        var verts = new Vector3[segments * 2];
        var uvs = new Vector2[segments * 2];
        var tris = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts[i * 2] = new Vector3(c * inner, 0f, s * inner);
            verts[i * 2 + 1] = new Vector3(c * outer, 0f, s * outer);
            uvs[i * 2] = new Vector2(0f, 0f); uvs[i * 2 + 1] = new Vector2(1f, 0f);
            int n = (i + 1) % segments;
            tris[i * 6 + 0] = i * 2; tris[i * 6 + 1] = n * 2; tris[i * 6 + 2] = i * 2 + 1;
            tris[i * 6 + 3] = n * 2; tris[i * 6 + 4] = n * 2 + 1; tris[i * 6 + 5] = i * 2 + 1;
        }
        m.vertices = verts; m.uv = uvs; m.triangles = tris;
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}
