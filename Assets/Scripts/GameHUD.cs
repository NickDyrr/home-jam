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

        DrawFireGlow();
        DrawEnd();
        DrawPause();
    }

    /// <summary>Warm glow at the screen edge toward each burning fire that is near but off screen.</summary>
    private void DrawFireGlow()
    {
        var cam = Camera.main; var p = GameObject.FindWithTag("Player");
        if (cam == null || p == null) return;
        Vector3 pp = p.transform.position;
        foreach (var f in Campfire.All)
        {
            if (!f.Lit) continue;
            Vector3 d = f.Position - pp; d.y = 0f;
            float dist = d.magnitude;
            if (dist > GlowRange) continue;
            Vector3 sp = cam.WorldToScreenPoint(f.Position);
            if (sp.z < 0f) continue;
            float sx = sp.x, sy = Screen.height - sp.y;
            bool onScreen = sx > 0f && sx < Screen.width && sy > 0f && sy < Screen.height;
            if (onScreen) continue;
            // Slide the point to the screen edge along the line from the centre.
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 v = new Vector2(sx, sy) - c;
            float k = Mathf.Min(c.x / Mathf.Max(1f, Mathf.Abs(v.x)), c.y / Mathf.Max(1f, Mathf.Abs(v.y)));
            Vector2 e = c + v * k;
            float a = Mathf.Clamp01(1f - dist / GlowRange);
            float size = Mathf.Lerp(160f, 320f, a);
            GUI.color = new Color(1f, 0.62f, 0.3f, 0.85f * a);
            GUI.DrawTexture(new Rect(e.x - size * 0.5f, e.y - size * 0.5f, size, size), glow);
        }
        GUI.color = Color.white;
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
