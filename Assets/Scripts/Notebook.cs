using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Her notebook, left on the box by the front door. Stand near it and press E
/// to read: a page of the things she has worked out, which is to say the
/// rules of the game. Any key closes it. Movement is held while it is open.
/// </summary>
public class Notebook : MonoBehaviour
{
    [SerializeField] private float readRadius = 2.0f;

    public static readonly string[] Page =
    {
        "Things I know.",
        "",
        "They only come after dark, and they stop at the fence. I don't know why.",
        "The others hide by day. After dark they light their fires. Follow the tracks in the snow.",
        "Bullets don't kill them. Each one slows them down. I have twelve. Everyone I bring back has a few to spare.",
        "Running is loud, and loud brings them. Walk, and they have to stumble into me.",
        "Two people home and I can build out the house at the table. Four, and again.",
        "If they take me, whoever is with me is gone. The house keeps everyone already inside.",
    };

    private Transform player;
    private PlayerMovement movement;
    private bool open;
    private GUIStyle title, body, prompt;
    private Texture2D black;

    private void Awake()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) { player = p.transform; movement = p.GetComponent<PlayerMovement>(); }
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
        if (Intro.Playing) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Intro.SetTextColor(title, new Color(0.97f, 0.96f, 0.93f));
            body = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = true };
            Intro.SetTextColor(body, new Color(0.97f, 0.96f, 0.93f));
            prompt = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Intro.SetTextColor(prompt, new Color(0.97f, 0.96f, 0.93f));
            black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply();
        }

        if (open)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
            GUI.color = Color.white;
            float w = Mathf.Min(820f, Screen.width - 80f), x = (Screen.width - w) * 0.5f, y = Screen.height * 0.14f;
            GUI.Label(new Rect(x, y, w, 50), Page[0], title); y += 70f;
            for (int i = 1; i < Page.Length; i++)
            {
                if (Page[i].Length == 0) { y += 8f; continue; }
                float h = body.CalcHeight(new GUIContent(Page[i]), w);
                GUI.Label(new Rect(x, y, w, h), Page[i], body); y += h + 14f;
            }
            GUI.Label(new Rect(x, Screen.height - 70f, w, 40), "any key to close", prompt);
        }
        else if (Near && !GameHUD.Paused)
        {
            float w = 300f;
            Intro.DrawLegible(new Rect((Screen.width - w) * 0.5f, Screen.height - 50f, w, 34), "E   read the notebook", prompt, 0.9f);
        }
    }
}
