using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Anything thrown: a low arc from her hand to a point, then whatever it is lands.</summary>
public class Thrown : MonoBehaviour
{
    private Vector3 from, to; private float t, seconds; private Action<Vector3> onLand;

    public static void Launch(GameObject visual, Vector3 from, Vector3 to, float seconds, Action<Vector3> onLand)
    {
        var th = visual.AddComponent<Thrown>();
        th.from = from; th.to = to; th.seconds = Mathf.Max(0.1f, seconds); th.onLand = onLand;
        visual.transform.position = from;
    }

    private void Update()
    {
        t += Time.deltaTime / seconds;
        float k = Mathf.Clamp01(t);
        Vector3 p = Vector3.Lerp(from, to, k);
        p.y = Mathf.Lerp(from.y, to.y, k) + Mathf.Sin(k * Mathf.PI) * 1.6f;
        transform.position = p;
        transform.rotation = Quaternion.Euler(k * 540f, 0f, k * 360f) * transform.rotation;
        if (k >= 1f) { var cb = onLand; onLand = null; Destroy(gameObject); cb?.Invoke(to); }
    }
}

/// <summary>
/// A lit flare on the snow: twenty seconds of red light that they will not come into. A small
/// refuge she can throw ahead of an escort, or drop behind her.
/// </summary>
public class Flare : MonoBehaviour
{
    public static readonly List<Flare> All = new List<Flare>();
    public const float LightRadius = 6f;
    public float BurnsUntil { get; private set; }
    public bool Lit => Time.time < BurnsUntil;
    public Vector3 Position => transform.position;
    /// <summary>How far its light holds them off.</summary>
    public float Radius { get; private set; } = LightRadius;

    private Light light_;
    private Renderer body;
    private static Material stick, hot;

    public static Flare Ignite(Vector3 at, float seconds)
    {
        if (stick == null) { stick = new Material(Shader.Find("Universal Render Pipeline/Lit")); stick.SetColor("_BaseColor", new Color(0.8f, 0.12f, 0.1f)); }
        if (hot == null) { hot = new Material(Shader.Find("Universal Render Pipeline/Unlit")); hot.SetColor("_BaseColor", new Color(1f, 0.55f, 0.25f)); }
        var go = new GameObject("Flare");
        go.transform.position = new Vector3(at.x, 0.03f, at.z);
        var f = go.AddComponent<Flare>();
        f.BurnsUntil = Time.time + seconds;
        var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(rod.GetComponent<Collider>());
        rod.transform.SetParent(go.transform, false); rod.transform.localRotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
        rod.transform.localScale = new Vector3(0.05f, 0.16f, 0.05f); rod.GetComponent<Renderer>().sharedMaterial = stick;
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(go.transform, false); tip.transform.localPosition = rod.transform.localRotation * new Vector3(0f, 0.17f, 0f);
        tip.transform.localScale = Vector3.one * 0.12f; f.body = tip.GetComponent<Renderer>(); f.body.sharedMaterial = hot;
        var lgo = new GameObject("Light"); lgo.transform.SetParent(go.transform, false); lgo.transform.localPosition = Vector3.up * 0.5f;
        f.light_ = lgo.AddComponent<Light>(); f.light_.type = LightType.Point; f.light_.color = new Color(1f, 0.4f, 0.2f); f.light_.range = 9f; f.light_.intensity = 3.5f; f.light_.shadows = LightShadows.None;
        return f;
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Update()
    {
        float left = BurnsUntil - Time.time;
        if (left <= 0f)
        {
            if (light_ != null) light_.enabled = false;
            if (body != null) body.enabled = false;
            if (left < -60f) Destroy(gameObject);   // the spent stick, or the scorch, stays a while
            return;
        }
        // Sputter, and gutter out over the last three seconds.
        float k = Mathf.Clamp01(left / 3f);
        if (light_ != null) light_.intensity = (3.0f + 0.8f * Mathf.PerlinNoise(Time.time * 9f, 0.3f)) * k;
        if (body != null) body.transform.localScale = Vector3.one * (0.10f + 0.04f * Mathf.PerlinNoise(Time.time * 12f, 0.7f)) * Mathf.Lerp(0.5f, 1f, k);
    }

    /// <summary>True within a burning flare's light.</summary>
    public static bool Shelters(Vector3 p, float margin)
    {
        foreach (var f in All)
        {
            if (f == null || !f.Lit) continue;
            float reach = f.Radius - margin;
            Vector3 d = f.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude <= reach * reach) return true;
        }
        return false;
    }
}

/// <summary>A tin can thrown into the dark. It lands with a clatter and everything nearby goes to look.</summary>
public static class Noisemaker
{
    public const float Radius = 35f;
    public const float Seconds = 12f;

    public static void Land(Vector3 at)
    {
        Vector3 p = new Vector3(at.x, 0.05f, at.z);
        if (AudioManager.Instance != null && AudioManager.Instance.Bell != null)
            AudioManager.Instance.Play(AudioManager.Instance.Bell, p, 0.35f, 60f, 2.6f);
        Stalker.LureNear(p, Radius, Seconds);
        // The can stays where it fell.
        var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder); UnityEngine.Object.Destroy(can.GetComponent<Collider>());
        can.name = "Can"; can.transform.position = p; can.transform.rotation = Quaternion.Euler(85f, UnityEngine.Random.Range(0f, 360f), 0f);
        can.transform.localScale = new Vector3(0.1f, 0.07f, 0.1f);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", new Color(0.7f, 0.72f, 0.76f)); m.SetFloat("_Metallic", 0.8f); m.SetFloat("_Smoothness", 0.6f);
        can.GetComponent<Renderer>().sharedMaterial = m;
    }
}

/// <summary>A bear trap set in the snow. The first stalker to step in it is held fast for a while.</summary>
public class BearTrap : MonoBehaviour
{
    public const float HoldSeconds = 5f;
    private const float Reach = 1.1f;
    private bool sprung;

    public static BearTrap Set(Vector3 at)
    {
        var go = new GameObject("BearTrap");
        go.transform.position = new Vector3(at.x, 0f, at.z);
        var t = go.AddComponent<BearTrap>();
        Material steel = new Material(Shader.Find("Universal Render Pipeline/Lit")); steel.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.36f)); steel.SetFloat("_Metallic", 0.9f); steel.SetFloat("_Smoothness", 0.5f);
        Material bright = new Material(Shader.Find("Universal Render Pipeline/Lit")); bright.SetColor("_BaseColor", new Color(0.62f, 0.64f, 0.7f)); bright.SetFloat("_Metallic", 0.9f); bright.SetFloat("_Smoothness", 0.6f);
        void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var g = GameObject.CreatePrimitive(type); Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(go.transform, false); g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = rot;
            g.GetComponent<Renderer>().sharedMaterial = mat;
        }
        Part(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.015f, 0.6f), Quaternion.identity, steel);
        Part(PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), new Vector3(0.38f, 0.01f, 0.38f), Quaternion.identity, bright);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Part(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.27f, 0.06f, Mathf.Sin(a) * 0.27f), new Vector3(0.03f, 0.08f, 0.03f), Quaternion.identity, bright);
        }
        return t;
    }

    private void Update()
    {
        if (sprung) return;
        foreach (var s in Stalker.All)
        {
            if (s == null) continue;
            Vector3 d = s.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > Reach * Reach) continue;
            sprung = true;
            s.Trapped(HoldSeconds);
            FloatingText.Show(transform.position + Vector3.up * 1.8f, "It's caught.", 2f);
            // Jaws shut: the teeth lean in.
            foreach (Transform k in transform) if (k.localScale.y > 0.05f) k.localRotation = Quaternion.LookRotation(-new Vector3(k.localPosition.x, 0f, k.localPosition.z).normalized, Vector3.up) * Quaternion.Euler(60f, 0f, 0f);
            Destroy(gameObject, HoldSeconds + 1f);
            break;
        }
    }
}

/// <summary>On the player: the keys that use what she carries. F flare, G can, V trap.</summary>
public class ItemUse : MonoBehaviour
{
    [SerializeField] private float throwRange = 9f;
    [SerializeField] private float flareSeconds = 20f;
    private Camera cam;

    private void Start() { cam = Camera.main; }

    private Vector3 AimPoint(float maxRange)
    {
        Vector3 fallback = transform.position + transform.forward * maxRange * 0.6f;
        if (cam == null || UnityEngine.InputSystem.Mouse.current == null) return fallback;
        Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        var ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!ground.Raycast(ray, out float enter)) return fallback;
        Vector3 p = ray.GetPoint(enter); Vector3 d = p - transform.position; d.y = 0f;
        if (d.magnitude > maxRange) d = d.normalized * maxRange;
        return transform.position + d;
    }

    public static ItemUse Instance { get; private set; }
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private bool CanAct()
    {
        if (Intro.Playing || GameHUD.Paused || Encounter.Active) return false;
        var pm = GetComponent<PlayerMovement>(); if (pm != null && (pm.CutsceneLocked || pm.Grabbed)) return false;
        if (Home.Instance != null && Home.Instance.Ended) return false;
        return true;
    }

    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !CanAct()) return;
        if (kb.gKey.wasPressedThisFrame) Use(ItemKind.Noisemaker, AimPoint(throwRange * 1.5f));
        if (kb.vKey.wasPressedThisFrame) Use(ItemKind.Trap, transform.position);
        if (kb.mKey.wasPressedThisFrame) Use(ItemKind.MapScrap, transform.position);
    }

    /// <summary>A click on the item bar: throwables go the way she is facing; anything else does nothing.</summary>
    public void UseFromBar(ItemKind kind)
    {
        if (!CanAct()) return;
        switch (kind)
        {
            case ItemKind.Noisemaker: Use(kind, transform.position + transform.forward * throwRange); break;
            case ItemKind.Trap: Use(kind, transform.position); break;
            case ItemKind.MapScrap: Use(kind, transform.position); break;
        }
    }

    /// <summary>Spend one and put it in the world. Returns false if she has none.</summary>
    public bool Use(ItemKind kind, Vector3 target)
    {
        if (!Inventory.Take(kind)) return false;
        Vector3 from = transform.position + Vector3.up * 1.1f + transform.forward * 0.4f;
        switch (kind)
        {
            case ItemKind.Noisemaker:
            {
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(vis.GetComponent<Collider>());
                vis.transform.localScale = new Vector3(0.1f, 0.07f, 0.1f);
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", new Color(0.7f, 0.72f, 0.76f)); m.SetFloat("_Metallic", 0.8f); vis.GetComponent<Renderer>().sharedMaterial = m;
                Thrown.Launch(vis, from, new Vector3(target.x, 0.05f, target.z), 0.8f, Noisemaker.Land);
                FacePoint(target);
                return true;
            }
            case ItemKind.Trap:
                BearTrap.Set(transform.position + transform.forward * 0.9f);
                FloatingText.Show(transform.position + Vector3.up * 1.6f, "Trap set.", 1.5f);
                return true;
            case ItemKind.MapScrap:
            {
                // Read it: the nearest camp she has not reached goes on the compass until she gets there.
                Survivor pick = null; float best = float.MaxValue;
                foreach (var s in Survivor.All)
                {
                    if (s.CurrentState != Survivor.State.Waiting || Inventory.Revealed.Contains(s)) continue;
                    float dd = (s.transform.position - transform.position).sqrMagnitude;
                    if (dd < best) { best = dd; pick = s; }
                }
                if (pick == null) { Inventory.Add(kind); FloatingText.Show(transform.position + Vector3.up * 1.6f, "Nothing on it you don't already know.", 2.5f); return false; }
                Inventory.Revealed.Add(pick);
                FloatingText.Show(transform.position + Vector3.up * 1.6f, "A camp. It's on the compass.", 2.5f);
                return true;
            }
        }
        Inventory.Add(kind);   // not something she uses by hand: put it back
        return false;
    }

    private void FacePoint(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }
}
