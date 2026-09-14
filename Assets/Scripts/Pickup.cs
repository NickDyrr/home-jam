using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something small lying in the snow. Walk over it to take it. The look is built here from
/// primitives if the object has no renderer of its own, so a pickup in the scene can be an
/// empty object with a kind on it. A faint gold breath, like the crates, so it can be spotted.
/// </summary>
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    [SerializeField] private ItemKind kind = ItemKind.Flare;
    [SerializeField] private int amount = 1;
    [TextArea] [SerializeField] private string pageText;
    [SerializeField] private float pickupRadius = 1.3f;

    public ItemKind Kind => kind;
    public Vector3 Position => transform.position;

    private Transform player;
    private bool taken;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Light glow;
    private float phase;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Gold = new Color(1f, 0.78f, 0.25f);

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Awake() { Setup(); }

    private void Setup()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (GetComponentInChildren<Renderer>() == null) BuildVisual();
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            var m = r.material; m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        block = new MaterialPropertyBlock();
        var existing = transform.Find("Glow");
        var lightGo = existing != null ? existing.gameObject : new GameObject("Glow");
        lightGo.transform.SetParent(transform, false); lightGo.transform.localPosition = Vector3.up * 0.6f;
        glow = lightGo.GetComponent<Light>(); if (glow == null) glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = Gold; glow.range = 2f; glow.intensity = 0f; glow.shadows = LightShadows.None;
        phase = Random.value;
    }

    private static Material Lit(Color c, float smooth = 0.3f, float metal = 0f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        return m;
    }

    private GameObject Part(PrimitiveType type, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat, string name = "Part")
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name; Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(transform, false);
        g.transform.localPosition = localPos; g.transform.localRotation = rot; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    /// <summary>Small, plain, readable from above. Nothing here is precious; a real model can replace any of it.</summary>
    private void BuildVisual()
    {
        Quaternion flat = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        switch (kind)
        {
            case ItemKind.Flare:
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.05f, 0.16f, 0.05f), flat, Lit(new Color(0.8f, 0.12f, 0.1f)));
                Part(PrimitiveType.Cylinder, flat * new Vector3(0f, 0.17f, 0f) + new Vector3(0f, 0.03f, 0f), new Vector3(0.052f, 0.03f, 0.052f), flat, Lit(new Color(0.95f, 0.9f, 0.8f)));
                break;
            case ItemKind.Noisemaker:
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.07f, 0.1f), Quaternion.Euler(80f, Random.Range(0f, 360f), 0f), Lit(new Color(0.7f, 0.72f, 0.76f), 0.6f, 0.8f));
                break;
            case ItemKind.Trap:
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.015f, 0.5f), Quaternion.identity, Lit(new Color(0.3f, 0.32f, 0.36f), 0.5f, 0.9f));
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), new Vector3(0.32f, 0.01f, 0.32f), Quaternion.identity, Lit(new Color(0.6f, 0.58f, 0.5f), 0.2f, 0f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.52f, 0.03f, 0.04f), Quaternion.Euler(0f, Random.Range(0f, 180f), 0f), Lit(new Color(0.6f, 0.62f, 0.68f), 0.6f, 0.9f));
                break;
            case ItemKind.MapScrap:
                Part(PrimitiveType.Quad, new Vector3(0f, 0.015f, 0f), new Vector3(0.36f, 0.28f, 1f), Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), Lit(new Color(0.85f, 0.76f, 0.58f), 0.1f));
                break;
            case ItemKind.Page:
                Part(PrimitiveType.Quad, new Vector3(0f, 0.015f, 0f), new Vector3(0.24f, 0.32f, 1f), Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), Lit(new Color(0.93f, 0.92f, 0.86f), 0.1f));
                break;
            case ItemKind.Medkit:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.16f, 0.26f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.93f, 0.93f, 0.93f), 0.4f), "Box");
                Part(PrimitiveType.Cube, new Vector3(0f, 0.165f, 0f), new Vector3(0.2f, 0.005f, 0.06f), Quaternion.identity, Lit(new Color(0.85f, 0.1f, 0.1f)));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.165f, 0f), new Vector3(0.06f, 0.005f, 0.2f), Quaternion.identity, Lit(new Color(0.85f, 0.1f, 0.1f)));
                break;
            case ItemKind.Blanket:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.44f, 0.12f, 0.3f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.5f, 0.3f, 0.18f), 0.05f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.125f, 0f), new Vector3(0.45f, 0.006f, 0.05f), Quaternion.identity, Lit(new Color(0.85f, 0.7f, 0.45f), 0.05f));
                break;
            case ItemKind.Scrap:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0.9f, 0.05f, 0.14f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.55f, 0.38f, 0.22f), 0.1f));
                Part(PrimitiveType.Cube, new Vector3(0.1f, 0.085f, 0.05f), new Vector3(0.8f, 0.05f, 0.14f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.48f, 0.33f, 0.19f), 0.1f));
                break;
            case ItemKind.Lantern:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.16f, 0.3f, 0.16f), Quaternion.identity, Lit(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.8f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.12f, 0.2f, 0.12f), Quaternion.identity, Lit(new Color(1f, 0.85f, 0.45f), 0.8f));
                break;
            case ItemKind.Rifle:
            {
                Quaternion lay = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.04f, 0.2f), new Vector3(0.035f, 0.04f, 0.62f), lay, Lit(new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.8f));
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.035f, -0.2f), new Vector3(0.045f, 0.07f, 0.34f), lay, Lit(new Color(0.36f, 0.24f, 0.14f), 0.3f));
                break;
            }
            case ItemKind.Bow:
            {
                Quaternion lay = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Lerp(-60f, 60f, i / 5f) * Mathf.Deg2Rad;
                    Vector3 p = lay * new Vector3(0f, Mathf.Sin(a) * 0.55f, Mathf.Cos(a) * 0.55f - 0.4f);
                    Part(PrimitiveType.Cylinder, p + Vector3.up * 0.03f, new Vector3(0.025f, 0.12f, 0.025f), lay * Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f), Lit(new Color(0.3f, 0.2f, 0.12f), 0.3f));
                }
                break;
            }
            case ItemKind.RifleAmmo:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.18f, 0.1f, 0.12f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.35f, 0.3f, 0.2f), 0.2f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.105f, 0f), new Vector3(0.12f, 0.004f, 0.06f), Quaternion.identity, Lit(new Color(0.8f, 0.62f, 0.25f), 0.5f, 0.8f));
                break;
            case ItemKind.Arrow:
            {
                Quaternion lay = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                for (int i = 0; i < Mathf.Min(3, Mathf.Max(1, amount)); i++)
                    Part(PrimitiveType.Cylinder, new Vector3(i * 0.05f - 0.05f, 0.02f, 0f), new Vector3(0.018f, 0.36f, 0.018f), lay * Quaternion.Euler(0f, 0f, i * 6f), Lit(new Color(0.55f, 0.42f, 0.25f), 0.3f));
                break;
            }
            case ItemKind.Shotgun:
            {
                Quaternion lay = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.04f, 0.15f), new Vector3(0.06f, 0.04f, 0.5f), lay, Lit(new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.8f));
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.035f, -0.22f), new Vector3(0.05f, 0.08f, 0.3f), lay, Lit(new Color(0.45f, 0.28f, 0.15f), 0.3f));
                break;
            }
            case ItemKind.FlareGun:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.09f, 0.08f, 0.24f), Quaternion.Euler(0f, Random.Range(0f, 360f), 90f), Lit(new Color(0.9f, 0.4f, 0.12f), 0.4f));
                break;
            case ItemKind.AutoRifle:
            {
                Quaternion lay = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.04f, 0.18f), new Vector3(0.035f, 0.04f, 0.55f), lay, Lit(new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.8f));
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.04f, -0.15f), new Vector3(0.05f, 0.07f, 0.35f), lay, Lit(new Color(0.1f, 0.1f, 0.12f), 0.4f, 0.6f));
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.09f, -0.02f), new Vector3(0.04f, 0.12f, 0.06f), lay * Quaternion.Euler(15f, 0f, 0f), Lit(new Color(0.1f, 0.1f, 0.12f), 0.4f, 0.6f));   // magazine, sticking up as it lies
                break;
            }
            case ItemKind.Sniper:
            {
                Quaternion lay = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.035f, 0.3f), new Vector3(0.03f, 0.03f, 0.8f), lay, Lit(new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.8f));
                Part(PrimitiveType.Cube, lay * new Vector3(0f, 0.035f, -0.25f), new Vector3(0.045f, 0.07f, 0.4f), lay, Lit(new Color(0.32f, 0.22f, 0.13f), 0.3f));
                Part(PrimitiveType.Cylinder, lay * new Vector3(0f, 0.09f, -0.05f), new Vector3(0.035f, 0.12f, 0.035f), lay * Quaternion.Euler(90f, 0f, 0f), Lit(new Color(0.2f, 0.21f, 0.25f), 0.5f, 0.8f));   // scope
                break;
            }
            case ItemKind.Shells:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.16f, 0.1f, 0.12f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.55f, 0.15f, 0.12f), 0.2f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.105f, 0f), new Vector3(0.1f, 0.004f, 0.06f), Quaternion.identity, Lit(new Color(0.8f, 0.62f, 0.25f), 0.5f, 0.8f));
                break;
            case ItemKind.AutoAmmo:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.08f, 0.12f, 0.2f), Quaternion.Euler(0f, Random.Range(0f, 360f), 75f), Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f, 0.6f));
                break;
            case ItemKind.SniperAmmo:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.08f, 0.1f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.3f, 0.26f, 0.2f), 0.2f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.085f, 0f), new Vector3(0.08f, 0.004f, 0.05f), Quaternion.identity, Lit(new Color(0.8f, 0.62f, 0.25f), 0.5f, 0.8f));
                break;
            case ItemKind.Backpack:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.36f, 0.26f), Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), 70f), Lit(new Color(0.42f, 0.38f, 0.28f), 0.05f));
                Part(PrimitiveType.Cube, new Vector3(0.1f, 0.12f, 0.1f), new Vector3(0.22f, 0.18f, 0.12f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Lit(new Color(0.32f, 0.28f, 0.2f), 0.05f));
                break;
        }
    }

    private void Update()
    {
        if (taken) return;
        if (block == null || renderers == null) Setup();

        float t = 0.5f - 0.5f * Mathf.Cos((Time.time / 2f + phase) * Mathf.PI * 2f);
        block.SetColor(EmissionColor, Gold * (0.45f * t));
        foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);

        if (player == null) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        float dist = d.magnitude;
        if (glow != null)
        {
            bool near = dist < 40f;
            if (glow.enabled != near) glow.enabled = near;
            if (near) glow.intensity = 0.4f * t;
        }
        if (Intro.Playing || dist > pickupRadius) return;
        taken = true;
        Collect();
        Destroy(gameObject);
    }

    private void Collect()
    {
        Vector3 at = transform.position + Vector3.up * 1.6f;
        switch (kind)
        {
            case ItemKind.Page:
                break;   // pages are gone from the game; any left in a scene just vanish
            case ItemKind.MapScrap:
                Inventory.Add(ItemKind.MapScrap);
                FloatingText.Show(at, "A torn map. Use it to mark a camp.", 2.5f);
                break;
            case ItemKind.Backpack:
                if (Pistol.Instance != null) Pistol.Instance.AddReserve(3);
                Inventory.Add(ItemKind.Flare);
                Inventory.Add(ItemKind.MapScrap);
                FloatingText.Show(at, "Their pack. 3 rounds, a flare, a torn map.", 3f);
                break;
            case ItemKind.Lantern:
                Inventory.BetterLantern = true; Inventory.Add(ItemKind.Lantern);
                FloatingText.Show(at, "The hunter's lantern. Your light reaches further.", 3f);
                break;
            case ItemKind.Rifle:
                if (Pistol.Instance != null) Pistol.Instance.Give(WeaponKind.Rifle, Mathf.Max(amount, 1));
                FloatingText.Show(at, "A rifle, and " + Mathf.Max(amount, 1) + " rounds. Loud.", 3f);
                break;
            case ItemKind.Bow:
                if (Pistol.Instance != null) Pistol.Instance.Give(WeaponKind.Bow, Mathf.Max(amount, 1));
                FloatingText.Show(at, "A bow, and " + Mathf.Max(amount, 1) + " arrows. Quiet.", 3f);
                break;
            case ItemKind.RifleAmmo:
                if (Pistol.Instance != null) Pistol.Instance.AddAmmo(WeaponKind.Rifle, amount);
                FloatingText.Show(at, "+" + amount + " rifle rounds", 2.5f);
                break;
            case ItemKind.Arrow:
                if (Pistol.Instance != null) Pistol.Instance.AddAmmo(WeaponKind.Bow, amount);
                FloatingText.Show(at, "+" + amount + (amount == 1 ? " arrow" : " arrows"), 2f);
                break;
            case ItemKind.Shotgun:
                if (Pistol.Instance != null) Pistol.Instance.Give(WeaponKind.Shotgun, Mathf.Max(amount, 1));
                FloatingText.Show(at, "A shotgun, and " + Mathf.Max(amount, 1) + " shells. Loud as anything.", 3f);
                break;
            case ItemKind.FlareGun:
                if (Pistol.Instance != null) Pistol.Instance.Give(WeaponKind.FlareGun, 0);
                Inventory.Add(ItemKind.Flare, Mathf.Max(amount, 0));
                FloatingText.Show(at, "A flare gun" + (amount > 0 ? ", and " + amount + " flares." : "."), 3f);
                break;
            case ItemKind.AutoRifle:
                if (Pistol.Instance != null) Pistol.Instance.Give(WeaponKind.AutoRifle, Mathf.Max(amount, 1));
                FloatingText.Show(at, "An auto rifle, and " + Mathf.Max(amount, 1) + " rounds. Hold to fire.", 3f);
                break;
            case ItemKind.Sniper:
                if (Pistol.Instance != null) Pistol.Instance.Give(WeaponKind.Sniper, Mathf.Max(amount, 1));
                FloatingText.Show(at, "A sniper rifle, and " + Mathf.Max(amount, 1) + " rounds.", 3f);
                break;
            case ItemKind.Shells:
                if (Pistol.Instance != null) Pistol.Instance.AddAmmo(WeaponKind.Shotgun, amount);
                FloatingText.Show(at, "+" + amount + " shells", 2.5f);
                break;
            case ItemKind.AutoAmmo:
                if (Pistol.Instance != null) Pistol.Instance.AddAmmo(WeaponKind.AutoRifle, amount);
                FloatingText.Show(at, "+" + amount + " auto rounds", 2.5f);
                break;
            case ItemKind.SniperAmmo:
                if (Pistol.Instance != null) Pistol.Instance.AddAmmo(WeaponKind.Sniper, amount);
                FloatingText.Show(at, "+" + amount + " sniper rounds", 2.5f);
                break;
            default:
                Inventory.Add(kind, amount);
                FloatingText.Show(at, "+" + amount + " " + ItemInfo.Name(kind).ToLower(), 2.5f);
                break;
        }
    }

    /// <summary>Make one in the world at runtime (used by the editor placer too).</summary>
    public static Pickup Spawn(ItemKind kind, Vector3 at, int amount = 1, string pageText = null, Transform parent = null)
    {
        var go = new GameObject("Pickup " + kind);
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = at;
        var p = go.AddComponent<Pickup>();
        p.kind = kind; p.amount = amount; p.pageText = pageText;
        return p;
    }
}
