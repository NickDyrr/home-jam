using UnityEngine;

/// <summary>
/// A few rounds left at a camp or a landmark. Walk over it to take them. Rounds are scarce
/// now, so these are worth the detour, and they pulse gold so you know it's loot.
/// </summary>
public class AmmoBox : MonoBehaviour
{
    [SerializeField] private int rounds = 3;
    [SerializeField] private float pickupRadius = 1.4f;

    [Header("Glow")]
    [Tooltip("Seconds for one breath of the gold glow, up and back out.")]
    [SerializeField] private float pulseSeconds = 2f;
    [SerializeField] private Color gold = new Color(1f, 0.78f, 0.25f);
    [SerializeField] private float emissionPeak = 2.6f;
    [SerializeField] private float lightPeak = 2.2f;
    [Tooltip("The glow light only runs when she is within this distance.")]
    [SerializeField] private float lightRange = 45f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Transform player;
    private bool taken;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Light glow;
    private float phase;

    private void Awake() { Setup(); }

    /// <summary>Own materials with emission on, and the glow light. Redone if a script reload wiped it.</summary>
    private void Setup()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;

        renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            // Own material so the emission keyword is on without touching the shared asset.
            var m = r.material;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        block = new MaterialPropertyBlock();

        var existing = transform.Find("Glow");
        var lightGo = existing != null ? existing.gameObject : new GameObject("Glow");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.9f;
        glow = lightGo.GetComponent<Light>(); if (glow == null) glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = gold; glow.range = 3.2f; glow.intensity = 0f; glow.shadows = LightShadows.None;
        phase = Random.value;   // the boxes don't all breathe together
    }

    private void Update()
    {
        if (taken) return;
        if (block == null || renderers == null) Setup();

        // The breath: 0 to 1 and back, once per pulse.
        float t = 0.5f - 0.5f * Mathf.Cos((Time.time / Mathf.Max(0.1f, pulseSeconds) + phase) * Mathf.PI * 2f);
        block.SetColor(EmissionColor, gold * (emissionPeak * t));
        foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);

        if (player == null) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        float dist = d.magnitude;
        if (glow != null)
        {
            bool near = dist < lightRange;
            if (glow.enabled != near) glow.enabled = near;
            if (near) glow.intensity = lightPeak * t;
        }

        if (Intro.Playing || dist > pickupRadius) return;
        taken = true;
        if (Pistol.Instance != null) Pistol.Instance.AddReserve(rounds);
        FloatingText.Show(transform.position + Vector3.up * 1.6f, $"+{rounds} rounds", 2.5f);
        Destroy(gameObject);
    }
}
