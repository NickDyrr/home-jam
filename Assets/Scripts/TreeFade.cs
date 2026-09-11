using UnityEngine;

/// <summary>
/// Lets a tree fade toward transparent while it blocks the camera's view of
/// the player. Swaps to the transparent material set while faded and back
/// to the opaque set when fully solid again, so unfaded trees stay cheap.
/// Driven by TreeOcclusionFader; do not tick this yourself.
/// </summary>
public class TreeFade : MonoBehaviour
{
    [SerializeField] private Material[] opaqueMaterials;
    [SerializeField] private Material[] transparentMaterials;
    [SerializeField] private float fadedAlpha = 0.22f;
    [SerializeField] private float fadeSpeed = 6f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private float alpha = 1f;
    private bool usingTransparent;
    private Color[] baseColors;

    public bool OccludingThisFrame { get; set; }

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        if (opaqueMaterials == null || opaqueMaterials.Length == 0) opaqueMaterials = rend.sharedMaterials;
        baseColors = new Color[opaqueMaterials.Length];
        for (int i = 0; i < opaqueMaterials.Length; i++)
            baseColors[i] = opaqueMaterials[i] != null && opaqueMaterials[i].HasProperty(BaseColorId) ? opaqueMaterials[i].GetColor(BaseColorId) : Color.white;
    }

    /// <summary>Called once per frame by the fader after occlusion has been decided.</summary>
    public void Tick(float dt)
    {
        float target = OccludingThisFrame ? fadedAlpha : 1f;
        OccludingThisFrame = false;
        float next = Mathf.MoveTowards(alpha, target, fadeSpeed * dt);
        if (Mathf.Approximately(next, alpha) && (usingTransparent == (alpha < 0.999f))) return;
        alpha = next;

        bool wantTransparent = alpha < 0.999f;
        if (wantTransparent != usingTransparent)
        {
            usingTransparent = wantTransparent;
            rend.sharedMaterials = wantTransparent ? transparentMaterials : opaqueMaterials;
            if (!wantTransparent) rend.SetPropertyBlock(null);
        }

        if (usingTransparent)
        {
            for (int i = 0; i < transparentMaterials.Length; i++)
            {
                Color c = baseColors[Mathf.Min(i, baseColors.Length - 1)];
                c.a = alpha;
                mpb.Clear();
                mpb.SetColor(BaseColorId, c);
                rend.SetPropertyBlock(mpb, i);
            }
        }
    }
}
