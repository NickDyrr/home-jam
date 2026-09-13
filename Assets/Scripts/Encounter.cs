using UnityEngine;

/// <summary>
/// Meeting a survivor. The camera eases in on the two of them, the survivor
/// says one line as a subtitle (voiced if Resources/Audio/Greet_<Job> exists),
/// then the camera eases back out and the walk home begins. The player is
/// held still for the moment; the world keeps running.
/// </summary>
public class Encounter : MonoBehaviour
{
    public static Encounter Instance { get; private set; }

    [SerializeField] private float zoomSize = 7.5f;   // outside view is 9: just a nudge in
    [SerializeField] private float minSeconds = 2.8f;
    [SerializeField] private float easeIn = 0.6f;
    [SerializeField] private float easeOut = 0.7f;

    private Camera cam;
    private IsoCameraFollow follow;
    private Transform player;
    private PlayerMovement movement;
    private Survivor survivor;
    private string line;
    private float t, duration;
    private float startSize;
    private Vector3 startPos;
    private Vector3 camOffset;
    private bool active;
    private AudioSource voice;
    private GUIStyle style;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Start the meeting scene for this survivor.</summary>
    public static void Play(Survivor s)
    {
        if (Instance == null || Instance.active || s == null) return;
        Instance.Begin(s);
    }

    private void Begin(Survivor s)
    {
        cam = Camera.main;
        var p = GameObject.FindWithTag("Player");
        if (cam == null || p == null) return;
        player = p.transform;
        movement = p.GetComponent<PlayerMovement>();
        follow = cam.GetComponent<IsoCameraFollow>();
        survivor = s;
        line = HomeBonuses.Greeting(s.Job);

        AudioClip clip = Resources.Load<AudioClip>("Audio/Greet_" + s.Job);
        duration = minSeconds;
        if (clip != null)
        {
            if (voice == null) { voice = gameObject.AddComponent<AudioSource>(); voice.spatialBlend = 0f; voice.playOnAwake = false; }
            voice.clip = clip; voice.Play();
            duration = Mathf.Max(minSeconds, clip.length + 0.8f);
        }

        startSize = cam.orthographicSize;
        startPos = cam.transform.position;
        camOffset = cam.transform.position - player.position;
        if (follow != null) follow.enabled = false;
        if (movement != null) movement.CutsceneLocked = true;

        // Face each other.
        Vector3 toS = s.transform.position - player.position; toS.y = 0f;
        if (toS.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(toS.normalized, Vector3.up);

        t = 0f;
        active = true;
    }

    private void Update()
    {
        if (!active) return;
        t += Time.deltaTime;
        if (survivor == null || player == null || cam == null) { End(); return; }

        Vector3 mid = (player.position + survivor.transform.position) * 0.5f; mid.y = 0f;
        float k;
        if (t < easeIn) k = Ease(t / easeIn);
        else if (t < duration - easeOut) k = 1f;
        else k = Ease((duration - t) / easeOut);

        Vector3 goal = mid + camOffset;
        cam.transform.position = Vector3.Lerp(startPos, goal, k);
        cam.orthographicSize = Mathf.Lerp(startSize, zoomSize, k);

        if (t >= duration) End();
    }

    private void End()
    {
        active = false;
        if (movement != null) movement.CutsceneLocked = false;
        if (follow != null) follow.enabled = true;
        survivor = null;
    }

    private static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    private void OnGUI()
    {
        if (!active || string.IsNullOrEmpty(line)) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            style.normal.textColor = new Color(0.05f, 0.05f, 0.07f);
        }
        float a = t < 0.4f ? t / 0.4f : t > duration - 0.4f ? Mathf.Clamp01((duration - t) / 0.4f) : 1f;
        float w = Mathf.Min(900f, Screen.width - 80f);
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, 70);
        Intro.DrawLegible(r, line, style, a);
    }
}
