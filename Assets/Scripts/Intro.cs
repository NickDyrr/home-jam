using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The opening: black screen, the title, then the story a line at a time.
/// Time is stopped underneath. Any key or click skips to the end of the
/// text; another finishes the fade. Edit the lines below to change the story.
/// </summary>
public class Intro : MonoBehaviour
{
    public static readonly string Title = "HOME";

    public static readonly string[] Lines =
    {
        "The pass closed in October. The village burned the week after.",
        "We ran for the trees when they came. The tall quiet ones. They only walk after dark.",
        "I found the old cabin. They stop at the door. I don't know why. Maybe it's the hearth.",
        "The others are still out there in the snow, keeping their fires lit so someone will find them.",
        "Someone should. Bring them home.",
    };

    public static readonly string Hint = "Light finds them. Light finds you. Be back before dark.";

    private const float LineSeconds = 2.6f;    // per line, including its fade-in
    private const float HoldSeconds = 2.5f;    // after the last line
    private const float FadeOut = 1.6f;

    private float t;               // unscaled seconds since start
    private bool skipped;
    private bool done;
    private float fadeOutStart = -1f;
    private GUIStyle title, body, hint;
    private Texture2D black;

    private void Start()
    {
        Time.timeScale = 0f;
        AudioListener.volume = 0f;
    }

    private void Update()
    {
        if (done) return;
        t += Time.unscaledDeltaTime;

        bool press = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                     (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        float textEnd = 1.5f + Lines.Length * LineSeconds;
        if (press)
        {
            if (!skipped && t < textEnd) { skipped = true; t = textEnd; }
            else if (fadeOutStart < 0f) fadeOutStart = t;
        }
        if (fadeOutStart < 0f && t >= textEnd + HoldSeconds) fadeOutStart = t;

        if (fadeOutStart >= 0f)
        {
            float k = Mathf.Clamp01((t - fadeOutStart) / FadeOut);
            AudioListener.volume = k;
            if (k >= 1f)
            {
                done = true;
                Time.timeScale = 1f;
                AudioListener.volume = 1f;
                Destroy(this);
            }
        }
    }

    private void OnGUI()
    {
        if (done) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.95f, 0.9f, 0.8f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            body.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            hint = new GUIStyle(body) { fontSize = 18, fontStyle = FontStyle.Italic };
            hint.normal.textColor = new Color(0.75f, 0.8f, 0.95f);
            black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply();
        }

        float overlay = fadeOutStart < 0f ? 1f : 1f - Mathf.Clamp01((t - fadeOutStart) / FadeOut);
        GUI.color = new Color(0f, 0f, 0f, overlay);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);

        float w = Mathf.Min(900f, Screen.width - 80f);
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;

        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 1.2f) * overlay);
        GUI.Label(new Rect(x, y, w, 90), Title, title);
        y += 120f;

        for (int i = 0; i < Lines.Length; i++)
        {
            float start = 1.5f + i * LineSeconds;
            float a = Mathf.Clamp01((t - start) / 1.0f);
            GUI.color = new Color(1f, 1f, 1f, a * overlay);
            GUI.Label(new Rect(x, y, w, 60), Lines[i], body);
            y += 56f;
        }

        float hintA = Mathf.Clamp01((t - (1.5f + Lines.Length * LineSeconds)) / 1.0f);
        GUI.color = new Color(1f, 1f, 1f, hintA * overlay);
        GUI.Label(new Rect(x, y + 30f, w, 40), Hint, hint);
        GUI.color = new Color(1f, 1f, 1f, 0.5f * overlay);
        GUI.Label(new Rect(x, Screen.height - 60f, w, 30), "press any key", hint);
        GUI.color = Color.white;
    }
}
