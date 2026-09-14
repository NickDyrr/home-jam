using UnityEngine;

/// <summary>
/// The shot you can see: a thin bright streak from the muzzle to wherever the round
/// stopped, gone in a tenth of a second, and a small puff of snow where it hit. Opaque
/// unlit pieces that shrink away rather than fade, so nothing needs a transparent shader.
/// </summary>
public class Tracer : MonoBehaviour
{
    private const float StreakSeconds = 0.09f;
    private const float PuffSeconds = 0.35f;
    private static Material streakMat, puffMat;

    private float born, life;
    private Vector3 fullScale, velocity;
    private bool falls;

    private static Material Unlit(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        return m;
    }

    private static GameObject Piece(Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    /// <summary>A streak from a to b, and a puff at b if the round hit snow rather than something that bleeds.</summary>
    public static void Spawn(Vector3 from, Vector3 to, bool hitSomething)
    {
        if (streakMat == null) streakMat = Unlit(new Color(1f, 0.93f, 0.7f));
        if (puffMat == null) puffMat = Unlit(new Color(0.92f, 0.94f, 1f));

        Vector3 d = to - from; float len = d.magnitude; if (len < 0.05f) return;
        var s = Piece(streakMat);
        s.name = "Tracer";
        s.transform.position = (from + to) * 0.5f;
        s.transform.rotation = Quaternion.LookRotation(d / len, Vector3.up);
        var t = s.AddComponent<Tracer>();
        t.fullScale = new Vector3(0.035f, 0.035f, len);
        t.life = StreakSeconds;
        s.transform.localScale = t.fullScale;

        // Where it lands: a few grains of snow thrown up, or a darker spatter on a hit.
        int n = hitSomething ? 3 : 5;
        for (int i = 0; i < n; i++)
        {
            var p = Piece(hitSomething ? streakMat : puffMat);
            p.name = "Puff";
            p.transform.position = to;
            p.transform.rotation = Random.rotation;
            var pt = p.AddComponent<Tracer>();
            pt.fullScale = Vector3.one * Random.Range(0.06f, 0.12f);
            pt.life = PuffSeconds;
            pt.falls = true;
            Vector3 back = -d / len;
            pt.velocity = (back * 1.2f + Random.insideUnitSphere * 1.4f + Vector3.up * 1.6f);
            p.transform.localScale = pt.fullScale;
        }
    }

    private void Start() { born = Time.time; }

    private void Update()
    {
        float k = (Time.time - born) / life;
        if (k >= 1f) { Destroy(gameObject); return; }
        transform.localScale = fullScale * (1f - k);
        if (falls)
        {
            velocity += Vector3.down * 9.81f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
        }
    }
}
