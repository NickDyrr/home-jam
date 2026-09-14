using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Her notebook, left on the box by the front door. A floating prompt shows
/// while she is near (like the workbench); E opens a page of the things she
/// has worked out, which is to say the rules of the game. Any key closes it.
/// Movement is held while it is open.
/// </summary>
public class Notebook : MonoBehaviour
{
    [SerializeField] private float readRadius = 2.0f;
    [SerializeField] private TextMesh prompt;

    public static readonly string[] Page =
    {
        "Things I know.",
        "",
        "They only come after dark, and they stop at the fence.",
        "When it came, the others ran for the trees. They hide by day. After dark they light their fires, and the fires keep them back.",
        "Light scares them away. Sound draws them closer.",
        "Bullets don't kill them. Each one slows them down, and the flash sends them running. I have twelve. Everyone I bring back has a few to spare.",
        "Running is loud, and loud brings them. Walking is much safer.",
        "They go for whoever is with me first. If they get me, that is the end.",
        "The more people I bring home, the more we can expand.",
    };

    private Transform player;
    private PlayerMovement movement;
    private Transform cam;
    private bool open;
    private GUIStyle title, body, close;
    private Texture2D black;

    private void Awake()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) { player = p.transform; movement = p.GetComponent<PlayerMovement>(); }
        if (Camera.main != null) cam = Camera.main.transform;
        if (prompt == null) prompt = GetComponentInChildren<TextMesh>(true);
        if (prompt != null) { prompt.text = "E   Read"; prompt.gameObject.SetActive(false); }
    }

    private bool Near
    {
        get
        {
            if (player == null) return false;
            Vector3 d = player.position - transform.position; d.y = 0f;
            return d.sqrMagnitude <= readRadius * readRadius;
        }
    }

    private void Update()
    {
        bool near = Near && !Intro.Playing && !open;
        if (prompt != null)
        {
            prompt.gameObject.SetActive(near);
            if (near && cam != null) prompt.transform.rotation = cam.rotation;
        }

        var kb = Keyboard.current;
        if (kb == null || Intro.Playing || GameHUD.Paused) return;
        if (open)
        {
            if (kb.anyKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) SetOpen(false);
            return;
        }
        if (Near && kb.eKey.wasPressedThisFrame) SetOpen(true);
    }

    private void SetOpen(bool on)
    {
        open = on;
        if (movement != null) movement.CutsceneLocked = on;
    }

    private void OnDisable() { if (open) SetOpen(false); }

    private void OnGUI()
    {
        if (!open) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Intro.SetTextColor(title, new Color(0.97f, 0.96f, 0.93f));
            body = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = true };
            Intro.SetTextColor(body, new Color(0.97f, 0.96f, 0.93f));
            close = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Intro.SetTextColor(close, new Color(0.97f, 0.96f, 0.93f));
            black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply();
        }
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
        GUI.color = Color.white;
        float w = Mathf.Min(820f, Screen.width - 80f), x = (Screen.width - w) * 0.5f, y = Screen.height * 0.16f;
        GUI.Label(new Rect(x, y, w, 50), Page[0], title); y += 70f;
        for (int i = 1; i < Page.Length; i++)
        {
            if (Page[i].Length == 0) { y += 8f; continue; }
            float h = body.CalcHeight(new GUIContent(Page[i]), w);
            GUI.Label(new Rect(x, y, w, h), Page[i], body); y += h + 14f;
        }
        GUI.Label(new Rect(x, Screen.height - 70f, w, 40), "any key to close", close);
    }
}
