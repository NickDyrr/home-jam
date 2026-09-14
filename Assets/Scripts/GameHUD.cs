using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Almost nothing on screen: a warm glow at the screen edge toward any
/// burning fire nearby, the wait-for-dark
/// prompt when it applies, the pause screen with the controls, and the end
/// screen with Restart and Quit buttons. Plain OnGUI, like the ammo counter.
/// </summary>
public class GameHUD : MonoBehaviour
{
    private const float GlowRange = 40f;

    private GUIStyle label, small, warn, big, mid, huge, small2, button;
    private Texture2D white, glow;
    private bool restarting;

    private void Build()
    {
        label = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        Intro.SetTextColor(label, Color.white);
        small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        Intro.SetTextColor(small, new Color(1f, 1f, 1f, 0.75f));
        warn = new GUIStyle(label); Intro.SetTextColor(warn, new Color(1f, 0.6f, 0.35f));
        big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        Intro.SetTextColor(big, new Color(0.97f, 0.96f, 0.93f));
        mid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        Intro.SetTextColor(mid, new Color(0.97f, 0.96f, 0.93f));
        huge = new GUIStyle(big) { fontSize = 72 };
        small2 = new GUIStyle(mid) { fontSize = 17, fontStyle = FontStyle.Normal };
        button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        Intro.SetTextColor(button, new Color(0.97f, 0.96f, 0.93f));
        white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();

        // Soft radial blob for the fire glow.
        const int n = 64;
        glow = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f - 0.5f, n * 0.5f - 0.5f)) / (n * 0.5f);
            float a = Mathf.Clamp01(1f - d); a = a * a;
            glow.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        glow.Apply();
    }

    /// <summary>Esc pauses with the controls on screen; Esc or any key resumes. Quit and Restart are buttons.</summary>
    public static bool Paused { get; private set; }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (Paused) SetPaused(false);                 // Esc toggles; Quit is a button now
            else if (!Intro.Playing) SetPaused(true);
            return;
        }
        if (Paused)
        {
            if (kb.anyKey.wasPressedThisFrame) SetPaused(false);   // keys resume; the mouse is for the buttons
            return;
        }

        // Wait for dark: only at home, only by day.
        var dn = DayNightCycle.Instance;
        if (dn != null && !Intro.Playing && kb.tKey.wasPressedThisFrame && dn.IsDayWindow && !dn.FastForwarding
            && HomeZone.Instance != null && HomeZone.Instance.PlayerIsHome)
            dn.FastForwardTo(0.735f);

        if (Home.Instance != null && Home.Instance.Ended && kb.rKey.wasPressedThisFrame) Restart();
    }

    private void SetPaused(bool on)
    {
        Paused = on;
        Time.timeScale = on ? 0f : 1f;
        AudioListener.pause = on;
    }

    private void OnDestroy()
    {
        if (Paused) SetPaused(false);
    }

    private static readonly string[] ControlLines =
    {
        "W A S D   move",
        "Shift   sprint (loud)",
        "Mouse   aim      Left click   shoot      R   reload",
        "T   wait for dark (at home, by day)",
        "B   ring the bell (second house level, from the yard)",
        "Esc   pause / resume",
    };

    private void DrawPause()
    {
        if (!Paused) return;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = Color.white;
        float w = Mathf.Min(900f, Screen.width - 80f), x = (Screen.width - w) * 0.5f, y = Screen.height * 0.2f;
        GUI.Label(new Rect(x, y, w, 60), "Paused", big); y += 80f;
        GUI.Label(new Rect(x, y, w, 40), "Find their fires after dark. Follow the tracks. Bring them home.", mid); y += 60f;
        foreach (var line in ControlLines) { GUI.Label(new Rect(x, y, w, 32), line, mid); y += 32f; }
        y += 24f;
        GUI.Label(new Rect(x, y, w, 30), "Any key to resume", small2); y += 44f;
        int n = CanQuit ? 3 : 2;
        if (Button(x, y, w, "Resume", 0, n)) SetPaused(false);
        if (Button(x, y, w, "Restart", 1, n)) Restart();
        if (CanQuit && Button(x, y, w, "Quit", 2, n)) Quit();
    }

    /// <summary>One of n buttons in a row across width w at height y. Returns true when clicked.</summary>
    private bool Button(float x, float y, float w, string text, int index, int n)
    {
        float bw = Mathf.Min(220f, (w - (n - 1) * 20f) / n);
        float total = n * bw + (n - 1) * 20f;
        float bx = x + (w - total) * 0.5f + index * (bw + 20f);
        return GUI.Button(new Rect(bx, y, bw, 48f), text, button);
    }

    private void Restart()
    {
        if (restarting) return;
        restarting = true;
        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>A browser tab cannot close itself, so the web build shows no Quit.</summary>
    private static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnGUI()
    {
        if (Intro.Playing) return;
        if (label == null) Build();

        // The only standing prompt: waiting for dark, when it applies.
        var dn = DayNightCycle.Instance;
        if (dn != null && dn.IsDayWindow && !dn.FastForwarding && HomeZone.Instance != null && HomeZone.Instance.PlayerIsHome && !Paused)
        {
            float w = 400f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height - 46f, w, 30), "T   wait for dark", mid);
            GUI.color = Color.white;
        }
        // The bell, when it is hers to ring.
        if (Bell.Instance != null && Bell.Instance.CanRing && !Paused)
        {
            float w = 400f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height - 80f, w, 30), "B   ring the bell", mid);
            GUI.color = Color.white;
        }

        DrawFireGlow();
        if (!Paused && !(Home.Instance != null && Home.Instance.Ended)) DrawCompass();
        DrawEnd();
        DrawPause();
    }

    private Texture2D rose, needle;
    private GUIStyle compassNorth, compassSmall;

    /// <summary>Coverage helper: 1 inside, 0 outside, soft over one pixel.</summary>
    private static float Cov(float d, float px) => Mathf.Clamp01(0.5f - d / px);

    /// <summary>
    /// The rose, drawn once: a thin outer ring with ticks at the eight points, a four-point star with a
    /// lit and a shadowed half on each arm, shorter diagonal points, a soft dark ground. Antialiased.
    /// </summary>
    private static Texture2D MakeRose()
    {
        const int n = 256; float px = 2f / n;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color bone = new Color(0.94f, 0.91f, 0.84f), slate = new Color(0.52f, 0.58f, 0.7f), ground = new Color(0.03f, 0.04f, 0.07f);
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            Color c = new Color(0f, 0f, 0f, 0f);
            void Put(Color col, float a)
            {
                a = Mathf.Clamp01(a);
                c = new Color(Mathf.Lerp(c.r, col.r, a), Mathf.Lerp(c.g, col.g, a), Mathf.Lerp(c.b, col.b, a), c.a + a * (1f - c.a));
            }

            // Ground: a soft dark disc.
            Put(ground, 0.55f * Cov(r - 0.90f, 0.06f));
            // Outer ring.
            Put(bone, 0.85f * Cov(Mathf.Abs(r - 0.93f) - 0.012f, px));
            // Ticks at the eight points.
            float ang = Mathf.Atan2(u, v);   // 0 at north (up), clockwise
            for (int k = 0; k < 8; k++)
            {
                float a0 = k * Mathf.PI * 0.25f;
                float da = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, a0 * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                float across = Mathf.Sin(da) * r;                          // distance from the tick centre line
                bool major = k % 2 == 0;
                float inner = major ? 0.80f : 0.86f;
                float along = r >= inner && r <= 0.91f ? 0f : Mathf.Max(inner - r, r - 0.91f);
                if (da > Mathf.PI * 0.5f) continue;
                float d = Mathf.Max(Mathf.Abs(across) - (major ? 0.014f : 0.009f), along);
                Put(bone, 0.9f * Cov(d, px));
            }
            // Star: four long arms, four short. Each arm is a kite; the half toward the next
            // point clockwise is shadowed.
            for (int k = 0; k < 8; k++)
            {
                bool major = k % 2 == 0;
                float a0 = k * Mathf.PI * 0.25f;
                float len = major ? 0.74f : 0.40f, half = major ? 0.115f : 0.075f;
                // Local frame: t along the arm, s across (positive = clockwise side).
                float t = u * Mathf.Sin(a0) + v * Mathf.Cos(a0);
                float s = u * Mathf.Cos(a0) - v * Mathf.Sin(a0);
                if (t < 0f) continue;
                float width = half * (1f - t / len);                       // narrows to the tip
                float d = Mathf.Max(Mathf.Abs(s) - width, t - len);
                float cov = Cov(d, px);
                if (cov <= 0f) continue;
                Color lit = major ? bone : Color.Lerp(bone, slate, 0.35f);
                Color dark = major ? slate : Color.Lerp(slate, ground, 0.35f);
                Put(s > 0f ? dark : lit, cov);
                // A hairline between the halves.
                Put(ground, 0.5f * Cov(Mathf.Abs(s) - 0.004f, px) * Cov(t - len + 0.02f, px));
            }
            // Centre boss.
            Put(ground, Cov(r - 0.05f, px));
            Put(bone, Cov(r - 0.032f, px));
            pixels[y * n + x] = c;
        }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }

    /// <summary>A slim tapered needle, gold, with a dark edge so it reads over the star.</summary>
    private static Texture2D MakeNeedle()
    {
        const int w = 96, h = 24; float px = 2f / h;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var cols = new Color[w * h];
        Color gold = new Color(1f, 0.82f, 0.38f), edge = new Color(0.25f, 0.15f, 0.02f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float t = (x + 0.5f) / w;                          // 0 at the tail, 1 at the tip
            float s = ((y + 0.5f) / h) * 2f - 1f;              // -1..1 across
            float width = t < 0.15f ? t / 0.15f * 0.55f : 0.55f * (1f - (t - 0.15f) / 0.85f);
            float d = Mathf.Abs(s) - width;
            float body = Mathf.Clamp01(0.5f - (d + 0.12f) / px), rim = Mathf.Clamp01(0.5f - d / px);
            Color c = Color.Lerp(edge, gold, body); c.a = rim;
            cols[y * w + x] = c;
        }
        tex.SetPixels(cols); tex.Apply();
        return tex;
    }

    /// <summary>
    /// Top left: the compass rose turned so its points lie where those world directions run on
    /// screen, N marked, and a gold needle that points home whenever she is outside the yard.
    /// </summary>
    private void DrawCompass()
    {
        var cam = Camera.main; var p = GameObject.FindWithTag("Player");
        if (cam == null || p == null) return;
        if (rose == null)
        {
            rose = MakeRose(); needle = MakeNeedle();
            compassNorth = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Intro.SetTextColor(compassNorth, new Color(0.98f, 0.5f, 0.38f));
            compassSmall = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Intro.SetTextColor(compassSmall, new Color(0.94f, 0.91f, 0.84f, 0.8f));
        }

        float size = 108f, radius = size * 0.5f;
        Vector2 c = new Vector2(18f + radius, 18f + radius);
        Vector3 pp = p.transform.position;

        Vector2 OnScreen(Vector3 dir)
        {
            Vector3 a = cam.WorldToScreenPoint(pp), b = cam.WorldToScreenPoint(pp + dir);
            Vector2 v = new Vector2(b.x - a.x, -(b.y - a.y));
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.up;
        }
        Vector2 north = OnScreen(Vector3.forward);
        float northAng = Mathf.Atan2(north.x, -north.y) * Mathf.Rad2Deg;   // turns texture-up onto screen north

        // The rose, turned to the world.
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(northAng, c);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(c.x - radius, c.y - radius, size, size), rose);
        GUI.matrix = m;

        // Letters stay upright, just past the arm tips.
        float r = radius * 0.62f;
        DrawAt(c + north * r, "N", compassNorth);
        DrawAt(c + OnScreen(Vector3.right) * r, "E", compassSmall);
        DrawAt(c + OnScreen(Vector3.back) * r, "S", compassSmall);
        DrawAt(c + OnScreen(Vector3.left) * r, "W", compassSmall);

        // The needle: home, when she is away from it.
        if (Home.Instance != null && !(HomeZone.Instance != null && HomeZone.Instance.PlayerIsHome))
        {
            Vector3 toHome = Home.Instance.transform.position - pp; toHome.y = 0f;
            if (toHome.sqrMagnitude > 4f)
            {
                Vector2 v = OnScreen(toHome.normalized);
                float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                m = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, c);
                float len = radius * 0.78f, th = 12f;
                GUI.DrawTexture(new Rect(c.x - len * 0.22f, c.y - th * 0.5f, len, th), needle);
                GUI.matrix = m;
            }
        }
    }

    private static void DrawAt(Vector2 at, string text, GUIStyle style)
    {
        GUI.Label(new Rect(at.x - 12f, at.y - 11f, 24f, 22f), text, style);
    }

    private const float CrateGlowRange = 26f;

    /// <summary>
    /// Warm glow at the screen edge toward each burning fire that is near but off screen, and a
    /// smaller gold one toward any crate close by.
    /// </summary>
    private void DrawFireGlow()
    {
        var cam = Camera.main; var p = GameObject.FindWithTag("Player");
        if (cam == null || p == null) return;
        Vector3 pp = p.transform.position;
        foreach (var f in Campfire.All)
        {
            if (!f.Lit) continue;
            EdgeGlow(cam, pp, f.Position, GlowRange, new Color(1f, 0.62f, 0.3f), 160f, 320f, 0.85f);
        }
        foreach (var b in AmmoBox.All)
        {
            if (b == null) continue;
            EdgeGlow(cam, pp, b.Position, CrateGlowRange, new Color(1f, 0.8f, 0.3f), 90f, 170f, 0.7f);
        }
        GUI.color = Color.white;
    }

    /// <summary>A soft blob at the screen edge toward a world point, if it is within range and off screen.</summary>
    private void EdgeGlow(Camera cam, Vector3 from, Vector3 target, float range, Color colour, float minSize, float maxSize, float alpha)
    {
        Vector3 d = target - from; d.y = 0f;
        float dist = d.magnitude;
        if (dist > range) return;
        Vector3 sp = cam.WorldToScreenPoint(target);
        if (sp.z < 0f) return;
        float sx = sp.x, sy = Screen.height - sp.y;
        bool onScreen = sx > 0f && sx < Screen.width && sy > 0f && sy < Screen.height;
        if (onScreen) return;
        // Slide the point to the screen edge along the line from the centre.
        Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 v = new Vector2(sx, sy) - c;
        float k = Mathf.Min(c.x / Mathf.Max(1f, Mathf.Abs(v.x)), c.y / Mathf.Max(1f, Mathf.Abs(v.y)));
        Vector2 e = c + v * k;
        float a = Mathf.Clamp01(1f - dist / range);
        float size = Mathf.Lerp(minSize, maxSize, a);
        GUI.color = new Color(colour.r, colour.g, colour.b, alpha * a);
        GUI.DrawTexture(new Rect(e.x - size * 0.5f, e.y - size * 0.5f, size, size), glow);
    }


    private void DrawEnd()
    {
        if (Home.Instance == null || !Home.Instance.Ended) return;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = Color.white;
        float w = Mathf.Min(1000f, Screen.width - 80f), x = (Screen.width - w) * 0.5f;
        int nights = StalkerDirector.Instance != null ? StalkerDirector.Instance.Nights : 0;
        var h = Home.Instance;
        float y = Screen.height * 0.3f;
        GUI.Label(new Rect(x, y, w, 70), h.EndText, big); y += 90f;
        // The reveal: it was a race all along.
        GUI.Label(new Rect(x, y, w, 40), h.Won ? "Your time" : "Time", mid); y += 40f;
        GUI.Label(new Rect(x, y, w, 80), Home.FormatTime(h.RunSeconds), huge); y += 90f;
        if (h.Won && h.NewBest) { GUI.Label(new Rect(x, y, w, 40), "New best", mid); y += 40f; }
        else if (h.BestSeconds >= 0f) { GUI.Label(new Rect(x, y, w, 40), "Best: " + Home.FormatTime(h.BestSeconds), mid); y += 40f; }
        GUI.Label(new Rect(x, y, w, 40), $"Nights: {nights}", mid); y += 60f;
        if (Button(x, y, w, "Restart", 0, CanQuit ? 2 : 1)) Restart();
        if (CanQuit && Button(x, y, w, "Quit", 1, 2)) Quit();
    }
}
