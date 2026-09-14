using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything she can pick up and carry. Ammo stays with the pistol; these are the rest.</summary>
public enum ItemKind { Flare, Noisemaker, Trap, MapScrap, Page, Medkit, Blanket, Scrap, Lantern, Backpack, Pistol, Rifle, Bow, RifleAmmo, Arrow, Shotgun, FlareGun, Shells, AutoRifle, Sniper, AutoAmmo, SniperAmmo }

/// <summary>What she is carrying. Static so the HUD and the world can both read it; reset on boot.</summary>
public static class Inventory
{
    private static readonly int[] counts = new int[Enum.GetValues(typeof(ItemKind)).Length];
    public static event Action Changed;

    /// <summary>Journal pages she has found, in the order found.</summary>
    public static readonly List<string> Pages = new List<string>();
    /// <summary>Camps a map scrap has marked on the compass.</summary>
    public static readonly List<Survivor> Revealed = new List<Survivor>();
    /// <summary>The hunter's lantern: a longer reach for hers.</summary>
    public static bool BetterLantern;

    public static int Count(ItemKind k) => counts[(int)k];

    public static void Add(ItemKind k, int n = 1)
    {
        counts[(int)k] = Mathf.Max(0, counts[(int)k] + n);
        Changed?.Invoke();
    }

    public static bool Take(ItemKind k, int n = 1)
    {
        if (counts[(int)k] < n) return false;
        counts[(int)k] -= n;
        Changed?.Invoke();
        return true;
    }

    public static void Reset()
    {
        Array.Clear(counts, 0, counts.Length);
        Pages.Clear(); Revealed.Clear(); BetterLantern = false;
        Changed?.Invoke();
    }

    /// <summary>The kinds shown in the bar, in this order. Pages and scraps count too.</summary>
    public static readonly ItemKind[] BarOrder = { ItemKind.MapScrap, ItemKind.Noisemaker, ItemKind.Trap, ItemKind.Medkit, ItemKind.Scrap };
}

/// <summary>Names and one-line hints for the bar.</summary>
public static class ItemInfo
{
    public static string Name(ItemKind k)
    {
        switch (k)
        {
            case ItemKind.Flare: return "Flares";
            case ItemKind.AutoRifle: return "Auto rifle";
            case ItemKind.Sniper: return "Sniper rifle";
            case ItemKind.AutoAmmo: return "Auto rounds";
            case ItemKind.SniperAmmo: return "Sniper rounds";
            case ItemKind.Noisemaker: return "Tin can";
            case ItemKind.Trap: return "Bear trap";
            case ItemKind.MapScrap: return "Map scrap";
            case ItemKind.Page: return "Page";

            case ItemKind.Medkit: return "Medkit";
            case ItemKind.Blanket: return "Blanket";
            case ItemKind.Scrap: return "Scrap";
            case ItemKind.Lantern: return "Hunter's lantern";
            case ItemKind.Backpack: return "Backpack";
            case ItemKind.Pistol: return "Pistol";
            case ItemKind.Rifle: return "Rifle";
            case ItemKind.Bow: return "Bow";
            case ItemKind.RifleAmmo: return "Rifle rounds";
            case ItemKind.Arrow: return "Arrows";
            case ItemKind.Shotgun: return "Shotgun";
            case ItemKind.FlareGun: return "Flare gun";
            case ItemKind.Shells: return "Shells";
        }
        return k.ToString();
    }

    /// <summary>The key that uses it, or empty if it is used by itself.</summary>
    public static string Key(ItemKind k)
    {
        switch (k)
        {
            case ItemKind.Noisemaker: return "G";
            case ItemKind.Trap: return "V";
            case ItemKind.MapScrap: return "M";
            case ItemKind.Pistol: return "1";
            case ItemKind.Rifle: return "2";
            case ItemKind.Bow: return "3";
            case ItemKind.Shotgun: return "4";
            case ItemKind.FlareGun: return "5";
            case ItemKind.AutoRifle: return "6";
            case ItemKind.Sniper: return "7";
        }
        return "";
    }

    public static string Hint(ItemKind k)
    {
        switch (k)
        {
            case ItemKind.Pistol: return "Left click to shoot, R to reload. Bullets slow them; a hit sends one running.";
            case ItemKind.Rifle: return "Left click to shoot, R to reload. Loud, long reach, hits for two.";
            case ItemKind.Bow: return "Left click to loose, R to nock. Silent. Arrows in the snow can be picked back up.";
            case ItemKind.Shotgun: return "Left click, R to reload. Two shells, short reach, a wide spread, and the loudest thing you own.";
            case ItemKind.FlareGun: return "Left click to put a flare thirty metres out. Burns twenty seconds; they will not come into its light.";
            case ItemKind.AutoRifle: return "Hold to fire. Thirty rounds, a wide spray, a hit every few. Loud.";
            case ItemKind.Sniper: return "Left click, R to reload. Three rounds, seventy metres, one hit sends anything running. Louder than anything.";
            case ItemKind.Flare: return "Rounds for the flare gun.";
            case ItemKind.Noisemaker: return "G  throw. Every one of them nearby goes to see what made the noise.";
            case ItemKind.Trap: return "V  set it at your feet. Holds the first one that steps in it.";
            case ItemKind.MapScrap: return "M  or click: marks the nearest camp you have not reached on the compass, until you get there.";
            case ItemKind.Page: return "In the notebook.";
            case ItemKind.Medkit: return "Used by itself on a hurt survivor the moment you reach them.";
            case ItemKind.Scrap: return "For the workbench.";
        }
        return "";
    }
}

/// <summary>
/// Icons for the bar. A PNG in Resources/Icons named after the kind (Flare.png, Medkit.png...)
/// wins; otherwise a small flat drawing is made once here so the bar never shows a blank.
/// </summary>
public static class ItemIcons
{
    private static readonly Dictionary<ItemKind, Texture2D> cache = new Dictionary<ItemKind, Texture2D>();
    private const int N = 64;

    public static Texture2D Get(ItemKind k)
    {
        if (cache.TryGetValue(k, out var t) && t != null) return t;
        var loaded = Resources.Load<Texture2D>("Icons/" + k);
        t = loaded != null ? loaded : Draw(k);
        cache[k] = t;
        return t;
    }

    // --- tiny painter: everything below is signed distance on a 64 px canvas ---
    private static Color[] px;
    private static float Px => 2f / N;
    private static float Cov(float d) => Mathf.Clamp01(0.5f - d / Px);
    private static void Put(int i, Color col, float a)
    {
        a = Mathf.Clamp01(a); if (a <= 0f) return;
        Color c = px[i];
        px[i] = new Color(Mathf.Lerp(c.r, col.r, a), Mathf.Lerp(c.g, col.g, a), Mathf.Lerp(c.b, col.b, a), c.a + a * (1f - c.a));
    }
    private static float Box(Vector2 p, Vector2 c, Vector2 half, float round = 0f)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
    }
    private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
    private static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
    {
        Vector2 pa = p - a, ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude - w;
    }
    private static Vector2 Rot(Vector2 p, float deg) { float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c); }

    private static void Fill(Func<Vector2, float> sdf, Color col, float alpha = 1f)
    {
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
            Put(y * N + x, col, alpha * Cov(sdf(p)));
        }
    }

    private static Texture2D Draw(ItemKind k)
    {
        px = new Color[N * N];
        Color ink = new Color(0.08f, 0.08f, 0.1f);
        switch (k)
        {
            case ItemKind.Flare:
            {
                Color red = new Color(0.85f, 0.15f, 0.12f), cap = new Color(0.95f, 0.92f, 0.85f), fire = new Color(1f, 0.6f, 0.2f);
                Fill(p => Circle(p, new Vector2(0.45f, 0.45f), 0.42f), fire, 0.35f);
                Fill(p => Circle(p, new Vector2(0.45f, 0.45f), 0.22f), new Color(1f, 0.9f, 0.5f), 0.8f);
                Fill(p => Box(Rot(p - new Vector2(-0.05f, -0.05f), -45f), Vector2.zero, new Vector2(0.11f, 0.5f), 0.05f) + 0.03f, ink);
                Fill(p => Box(Rot(p - new Vector2(-0.05f, -0.05f), -45f), Vector2.zero, new Vector2(0.11f, 0.5f), 0.05f), red);
                Fill(p => Box(Rot(p - new Vector2(0.3f, 0.3f), -45f), Vector2.zero, new Vector2(0.11f, 0.08f)), cap);
                break;
            }
            case ItemKind.Noisemaker:
            {
                Color tin = new Color(0.72f, 0.74f, 0.78f), dark = new Color(0.45f, 0.47f, 0.52f);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.32f, 0.5f), 0.06f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.32f, 0.5f), 0.06f), tin);
                Fill(p => Box(p, new Vector2(0.18f, -0.05f), new Vector2(0.09f, 0.48f)), dark, 0.5f);
                Fill(p => Box(p, new Vector2(0f, 0.3f), new Vector2(0.34f, 0.05f)), dark);
                Fill(p => Box(p, new Vector2(0f, -0.4f), new Vector2(0.34f, 0.05f)), dark);
                Fill(p => Box(p, new Vector2(0f, 0.45f), new Vector2(0.3f, 0.06f)), new Color(0.85f, 0.87f, 0.9f));
                break;
            }
            case ItemKind.Trap:
            {
                Color steel = new Color(0.35f, 0.37f, 0.42f), bright = new Color(0.7f, 0.72f, 0.78f);
                Fill(p => Mathf.Abs(Circle(p, Vector2.zero, 0.62f)) - 0.1f + 0.03f, ink);
                Fill(p => Mathf.Abs(Circle(p, Vector2.zero, 0.62f)) - 0.1f, steel);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 36f;
                    Vector2 dir = Rot(Vector2.up, a);
                    Fill(p => Seg(p, dir * 0.52f, dir * 0.3f, 0.045f), bright);
                }
                Fill(p => Circle(p, Vector2.zero, 0.14f), bright);
                Fill(p => Box(p, Vector2.zero, new Vector2(0.72f, 0.045f)), steel);
                break;
            }
            case ItemKind.MapScrap:
            {
                Color paper = new Color(0.86f, 0.78f, 0.6f), line = new Color(0.35f, 0.25f, 0.15f);
                Fill(p => Box(Rot(p, 8f), Vector2.zero, new Vector2(0.55f, 0.45f), 0.04f) + 0.03f, ink);
                Fill(p => Box(Rot(p, 8f), Vector2.zero, new Vector2(0.55f, 0.45f), 0.04f), paper);
                Fill(p => Seg(p, new Vector2(-0.4f, 0.2f), new Vector2(0.1f, 0.05f), 0.03f), line);
                Fill(p => Seg(p, new Vector2(0.1f, 0.05f), new Vector2(0.35f, -0.25f), 0.03f), line);
                Fill(p => Seg(p, new Vector2(-0.35f, -0.25f), new Vector2(-0.05f, -0.15f), 0.025f), line, 0.7f);
                Fill(p => Mathf.Min(Seg(p, new Vector2(0.28f, -0.18f), new Vector2(0.42f, -0.32f), 0.04f), Seg(p, new Vector2(0.42f, -0.18f), new Vector2(0.28f, -0.32f), 0.04f)), new Color(0.75f, 0.15f, 0.1f));
                break;
            }
            case ItemKind.Page:
            {
                Color paper = new Color(0.93f, 0.92f, 0.86f), line = new Color(0.45f, 0.45f, 0.5f);
                Fill(p => Box(Rot(p, -6f), Vector2.zero, new Vector2(0.4f, 0.55f), 0.03f) + 0.03f, ink);
                Fill(p => Box(Rot(p, -6f), Vector2.zero, new Vector2(0.4f, 0.55f), 0.03f), paper);
                for (int i = 0; i < 5; i++)
                {
                    float y = 0.35f - i * 0.16f; float len = i == 4 ? 0.15f : 0.28f;
                    Fill(p => Seg(Rot(p, -6f), new Vector2(-0.28f, y), new Vector2(-0.28f + len * 2f, y), 0.025f), line, 0.85f);
                }
                break;
            }
            case ItemKind.Medkit:
            {
                Color white = new Color(0.95f, 0.95f, 0.95f), red = new Color(0.85f, 0.12f, 0.12f);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.55f, 0.42f), 0.08f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.55f, 0.42f), 0.08f), white);
                Fill(p => Box(p, new Vector2(0f, 0.42f), new Vector2(0.2f, 0.08f), 0.04f), ink);
                Fill(p => Mathf.Min(Box(p, new Vector2(0f, -0.05f), new Vector2(0.28f, 0.09f)), Box(p, new Vector2(0f, -0.05f), new Vector2(0.09f, 0.28f))), red);
                break;
            }
            case ItemKind.Blanket:
            {
                Color wool = new Color(0.55f, 0.32f, 0.2f), stripe = new Color(0.85f, 0.7f, 0.45f);
                Fill(p => Box(p, new Vector2(0f, -0.1f), new Vector2(0.58f, 0.38f), 0.1f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0f, -0.1f), new Vector2(0.58f, 0.38f), 0.1f), wool);
                Fill(p => Box(p, new Vector2(0f, 0.05f), new Vector2(0.58f, 0.05f)), stripe);
                Fill(p => Box(p, new Vector2(0f, -0.25f), new Vector2(0.58f, 0.05f)), stripe);
                Fill(p => Box(p, new Vector2(0f, 0.25f), new Vector2(0.5f, 0.09f), 0.06f), new Color(0.62f, 0.38f, 0.24f));
                break;
            }
            case ItemKind.Scrap:
            {
                Color wood = new Color(0.6f, 0.42f, 0.24f), wood2 = new Color(0.5f, 0.34f, 0.19f), nail = new Color(0.75f, 0.76f, 0.8f);
                Fill(p => Box(Rot(p, 25f), Vector2.zero, new Vector2(0.62f, 0.13f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, 25f), Vector2.zero, new Vector2(0.62f, 0.13f)), wood);
                Fill(p => Box(Rot(p, -35f), new Vector2(0f, 0f), new Vector2(0.6f, 0.13f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, -35f), new Vector2(0f, 0f), new Vector2(0.6f, 0.13f)), wood2);
                Fill(p => Circle(p, new Vector2(0.3f, -0.15f), 0.05f), nail);
                Fill(p => Circle(p, new Vector2(-0.32f, 0.2f), 0.05f), nail);
                break;
            }
            case ItemKind.Lantern:
            {
                Color glass = new Color(1f, 0.85f, 0.4f), frame = new Color(0.2f, 0.2f, 0.22f);
                Fill(p => Circle(p, new Vector2(0f, -0.05f), 0.6f), new Color(1f, 0.8f, 0.3f), 0.3f);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.3f, 0.42f), 0.06f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.3f, 0.42f), 0.06f), frame);
                Fill(p => Box(p, new Vector2(0f, -0.05f), new Vector2(0.2f, 0.3f), 0.04f), glass);
                Fill(p => Box(p, new Vector2(0f, 0.45f), new Vector2(0.12f, 0.06f)), frame);
                Fill(p => Mathf.Abs(Circle(p, new Vector2(0f, 0.6f), 0.14f)) - 0.035f, frame);
                break;
            }
            case ItemKind.Pistol:
            {
                Color steel = new Color(0.22f, 0.23f, 0.27f), grip = new Color(0.32f, 0.22f, 0.14f), edge = new Color(0.6f, 0.62f, 0.68f);
                // Slide and barrel, pointing right.
                Fill(p => Box(p, new Vector2(0.02f, 0.18f), new Vector2(0.62f, 0.15f), 0.04f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0.02f, 0.18f), new Vector2(0.62f, 0.15f), 0.04f), steel);
                Fill(p => Box(p, new Vector2(0.02f, 0.29f), new Vector2(0.58f, 0.025f)), edge, 0.7f);
                // Grip, raked back.
                Fill(p => Box(Rot(p - new Vector2(-0.3f, -0.22f), 18f), Vector2.zero, new Vector2(0.13f, 0.3f), 0.05f) + 0.03f, ink);
                Fill(p => Box(Rot(p - new Vector2(-0.3f, -0.22f), 18f), Vector2.zero, new Vector2(0.13f, 0.3f), 0.05f), grip);
                // Trigger guard and trigger.
                Fill(p => Mathf.Abs(Circle(p, new Vector2(0.02f, -0.1f), 0.14f)) - 0.03f, steel);
                Fill(p => Box(p, new Vector2(0.03f, -0.08f), new Vector2(0.025f, 0.08f)), edge);
                break;
            }
            case ItemKind.Rifle:
            {
                Color steel = new Color(0.2f, 0.21f, 0.25f), wood = new Color(0.4f, 0.27f, 0.16f), edge = new Color(0.6f, 0.62f, 0.68f);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.1f, 0.08f), new Vector2(0.75f, 0.06f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.1f, 0.08f), new Vector2(0.75f, 0.06f)), steel);          // barrel
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.35f, -0.02f), new Vector2(0.38f, 0.1f), 0.04f) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.35f, -0.02f), new Vector2(0.38f, 0.1f), 0.04f), wood);  // stock
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.62f, -0.18f), new Vector2(0.14f, 0.2f), 0.05f), wood);   // butt
                Fill(p => Box(Rot(p, -20f), new Vector2(0.1f, 0.16f), new Vector2(0.04f, 0.05f)), edge);            // sight
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.05f, -0.2f), new Vector2(0.03f, 0.08f)), steel);         // trigger
                break;
            }
            case ItemKind.Bow:
            {
                Color limb = new Color(0.42f, 0.28f, 0.16f), str = new Color(0.9f, 0.88f, 0.8f);
                Fill(p => Mathf.Abs(Circle(p, new Vector2(-0.55f, 0f), 0.95f)) - 0.06f + Mathf.Max(0f, Mathf.Abs(p.y) - 0.68f) + 0.03f, ink);
                Fill(p => Mathf.Abs(Circle(p, new Vector2(-0.55f, 0f), 0.95f)) - 0.06f + Mathf.Max(0f, Mathf.Abs(p.y) - 0.68f) * 4f, limb);
                Fill(p => Seg(p, new Vector2(0.11f, 0.66f), new Vector2(0.11f, -0.66f), 0.02f), str);
                Fill(p => Seg(p, new Vector2(-0.45f, 0f), new Vector2(0.55f, 0f), 0.022f), new Color(0.6f, 0.46f, 0.28f));   // an arrow nocked
                Fill(p => Mathf.Min(Seg(p, new Vector2(0.55f, 0f), new Vector2(0.42f, 0.09f), 0.03f), Seg(p, new Vector2(0.55f, 0f), new Vector2(0.42f, -0.09f), 0.03f)), new Color(0.75f, 0.76f, 0.8f));
                break;
            }
            case ItemKind.RifleAmmo:
            {
                Color brass = new Color(0.8f, 0.62f, 0.25f), tip = new Color(0.65f, 0.5f, 0.4f);
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 0.32f;
                    Fill(p => Box(p, new Vector2(x, -0.1f), new Vector2(0.1f, 0.42f), 0.04f) + 0.03f, ink);
                    Fill(p => Box(p, new Vector2(x, -0.1f), new Vector2(0.1f, 0.42f), 0.04f), brass);
                    Fill(p => Box(p, new Vector2(x, 0.42f), new Vector2(0.07f, 0.16f), 0.06f), tip);
                }
                break;
            }
            case ItemKind.Arrow:
            {
                Color shaft = new Color(0.6f, 0.46f, 0.28f), head = new Color(0.75f, 0.76f, 0.8f), feather = new Color(0.9f, 0.88f, 0.82f);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 o = new Vector2(i * 0.1f - 0.05f, i * 0.28f - 0.14f);
                    Fill(p => Seg(Rot(p - o, 35f), new Vector2(-0.7f, 0f), new Vector2(0.7f, 0f), 0.035f) + 0.03f, ink);
                    Fill(p => Seg(Rot(p - o, 35f), new Vector2(-0.7f, 0f), new Vector2(0.7f, 0f), 0.035f), shaft);
                    Fill(p => Mathf.Min(Seg(Rot(p - o, 35f), new Vector2(0.72f, 0f), new Vector2(0.56f, 0.09f), 0.03f), Seg(Rot(p - o, 35f), new Vector2(0.72f, 0f), new Vector2(0.56f, -0.09f), 0.03f)), head);
                    Fill(p => Box(Rot(p - o, 35f), new Vector2(-0.6f, 0f), new Vector2(0.12f, 0.07f)), feather);
                }
                break;
            }
            case ItemKind.Shotgun:
            {
                Color steel = new Color(0.2f, 0.21f, 0.25f), wood = new Color(0.45f, 0.28f, 0.15f);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, 0.12f), new Vector2(0.7f, 0.05f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, 0.12f), new Vector2(0.7f, 0.05f)), steel);          // top barrel
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, 0.02f), new Vector2(0.7f, 0.05f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, 0.02f), new Vector2(0.7f, 0.05f)), steel);          // bottom barrel
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.4f, -0.05f), new Vector2(0.32f, 0.11f), 0.04f) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.4f, -0.05f), new Vector2(0.32f, 0.11f), 0.04f), wood);  // stock
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.66f, -0.2f), new Vector2(0.13f, 0.2f), 0.05f), wood);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, -0.08f), new Vector2(0.28f, 0.05f), 0.02f), wood);  // forend
                break;
            }
            case ItemKind.AutoRifle:
            {
                Color steel = new Color(0.18f, 0.19f, 0.22f), dark = new Color(0.1f, 0.1f, 0.12f), edge = new Color(0.6f, 0.62f, 0.68f);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, 0.1f), new Vector2(0.72f, 0.06f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.15f, 0.1f), new Vector2(0.72f, 0.06f)), steel);          // barrel
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.3f, 0f), new Vector2(0.4f, 0.11f), 0.03f) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.3f, 0f), new Vector2(0.4f, 0.11f), 0.03f), dark);       // receiver
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.7f, -0.12f), new Vector2(0.14f, 0.12f), 0.03f), steel);  // stock
                Fill(p => Box(Rot(p, 15f), new Vector2(-0.12f, -0.36f), new Vector2(0.08f, 0.24f), 0.03f) + 0.03f, ink);
                Fill(p => Box(Rot(p, 15f), new Vector2(-0.12f, -0.36f), new Vector2(0.08f, 0.24f), 0.03f), dark);    // curved magazine
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.05f, 0.22f), new Vector2(0.14f, 0.04f)), edge);           // carry rail
                break;
            }
            case ItemKind.FlareGun:
            {
                Color orange = new Color(0.9f, 0.4f, 0.12f), dark = new Color(0.25f, 0.12f, 0.06f);
                Fill(p => Box(p, new Vector2(0.05f, 0.2f), new Vector2(0.55f, 0.2f), 0.08f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0.05f, 0.2f), new Vector2(0.55f, 0.2f), 0.08f), orange);              // fat barrel
                Fill(p => Circle(p, new Vector2(0.6f, 0.2f), 0.12f), dark);                                        // muzzle
                Fill(p => Box(Rot(p - new Vector2(-0.3f, -0.25f), 18f), Vector2.zero, new Vector2(0.14f, 0.32f), 0.05f) + 0.03f, ink);
                Fill(p => Box(Rot(p - new Vector2(-0.3f, -0.25f), 18f), Vector2.zero, new Vector2(0.14f, 0.32f), 0.05f), dark);
                Fill(p => Mathf.Abs(Circle(p, new Vector2(0f, -0.12f), 0.13f)) - 0.03f, dark);
                break;
            }
            case ItemKind.Sniper:
            {
                Color steel = new Color(0.2f, 0.21f, 0.25f), wood = new Color(0.32f, 0.22f, 0.13f), glass = new Color(0.5f, 0.75f, 0.9f);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.2f, 0.06f), new Vector2(0.78f, 0.045f)) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(0.2f, 0.06f), new Vector2(0.78f, 0.045f)), steel);         // long barrel
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.35f, -0.04f), new Vector2(0.42f, 0.09f), 0.03f) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.35f, -0.04f), new Vector2(0.42f, 0.09f), 0.03f), wood);  // stock
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.7f, -0.2f), new Vector2(0.13f, 0.18f), 0.05f), wood);
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.15f, 0.24f), new Vector2(0.28f, 0.07f), 0.05f) + 0.03f, ink);
                Fill(p => Box(Rot(p, -20f), new Vector2(-0.15f, 0.24f), new Vector2(0.28f, 0.07f), 0.05f), steel);  // scope
                Fill(p => Circle(Rot(p, -20f), new Vector2(0.12f, 0.24f), 0.05f), glass);
                break;
            }
            case ItemKind.Shells:
            {
                Color red = new Color(0.8f, 0.15f, 0.1f), brass = new Color(0.8f, 0.62f, 0.25f);
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 0.34f;
                    Fill(p => Box(p, new Vector2(x, 0f), new Vector2(0.13f, 0.5f), 0.04f) + 0.03f, ink);
                    Fill(p => Box(p, new Vector2(x, 0.08f), new Vector2(0.13f, 0.42f), 0.04f), red);
                    Fill(p => Box(p, new Vector2(x, -0.36f), new Vector2(0.14f, 0.14f), 0.03f), brass);
                }
                break;
            }
            case ItemKind.AutoAmmo:
            {
                Color brass = new Color(0.8f, 0.62f, 0.25f), dark = new Color(0.12f, 0.12f, 0.14f);
                Fill(p => Box(Rot(p, 15f), new Vector2(0f, -0.05f), new Vector2(0.2f, 0.62f), 0.05f) + 0.03f, ink);
                Fill(p => Box(Rot(p, 15f), new Vector2(0f, -0.05f), new Vector2(0.2f, 0.62f), 0.05f), dark);       // a magazine
                for (int i = 0; i < 4; i++) { float y = 0.45f - i * 0.22f; Fill(p => Box(Rot(p, 15f), new Vector2(0f, y), new Vector2(0.13f, 0.045f), 0.02f), brass); }
                break;
            }
            case ItemKind.SniperAmmo:
            {
                Color brass = new Color(0.8f, 0.62f, 0.25f), tip = new Color(0.55f, 0.45f, 0.4f);
                for (int i = -1; i <= 0; i++)
                {
                    float x = i * 0.36f + 0.18f;
                    Fill(p => Box(p, new Vector2(x, -0.18f), new Vector2(0.09f, 0.5f), 0.04f) + 0.03f, ink);
                    Fill(p => Box(p, new Vector2(x, -0.18f), new Vector2(0.09f, 0.5f), 0.04f), brass);
                    Fill(p => Box(p, new Vector2(x, 0.48f), new Vector2(0.06f, 0.2f), 0.06f), tip);
                }
                break;
            }
            case ItemKind.Backpack:
            {
                Color canvas = new Color(0.45f, 0.4f, 0.3f);
                Fill(p => Box(p, new Vector2(0f, -0.08f), new Vector2(0.42f, 0.5f), 0.14f) + 0.03f, ink);
                Fill(p => Box(p, new Vector2(0f, -0.08f), new Vector2(0.42f, 0.5f), 0.14f), canvas);
                Fill(p => Box(p, new Vector2(0f, -0.25f), new Vector2(0.3f, 0.18f), 0.06f), new Color(0.35f, 0.3f, 0.22f));
                Fill(p => Mathf.Abs(Circle(p, new Vector2(0f, 0.5f), 0.16f)) - 0.04f, ink);
                break;
            }
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels(px); tex.Apply();
        px = null;
        return tex;
    }
}
