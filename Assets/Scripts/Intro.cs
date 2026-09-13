using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The opening, played in the world. Dusk. The camera starts wide over the
/// forest on a lone camp fire, drifts toward home while a stalker crosses
/// between the trees, and settles on the player standing at her own door as
/// the story plays as subtitles. Fades to black, dawn, and the game begins.
/// Any key skips to the end. Edit the lines below to change the story.
/// </summary>
public class Intro : MonoBehaviour
{
    public static bool Playing { get; private set; }

    public static readonly string Title = "HOME";
    public static readonly string[] Lines =
    {
        "The snow came early this year. Something came down with it. Nobody saw them arrive, only what they left behind.",
        "The rest of us ran for the forest.",
        "I made it back here. They come as far as the fence and no further. I don't know why. I've stopped asking.",
        "Out past the trees, fires are still burning. Every night there are fewer.",
        "I'm going to find them, and I'm going to bring them home.",
    };
    public static readonly string Hint = "Light finds them. Light finds you. Be home before dark.";

    // Timeline (seconds). With voice clips in Resources/Audio (Intro1..Intro5, IntroHint) the
    // line times stretch so each line stays up while its clip plays.
    private const float ShotA = 6f, ShotB = 13f;
    private float ShotC = 20f, FadeOutEnd = 21.5f, FadeInEnd = 23f, End = 27.5f;
    private readonly float[] LineTimes = { 1f, 5f, 9f, 13f, 17f };
    private AudioClip[] voice;
    private AudioClip hintVoice;
    private AudioSource voiceSource;
    private int nextVoiceLine;
    private bool hintSpoken;

    private float t;
    private bool done;
    private Camera cam;
    private IsoCameraFollow follow;
    private Vector3 camOffset;
    private Transform player;
    private Behaviour[] playerScripts;
    private Vector3 doorStep;
    private GameObject cameo;
    private Animator cameoAnim;
    private bool dawnSet;
    private GUIStyle title, sub, hintStyle;
    private Texture2D black;

    private void Start()
    {
        cam = Camera.main;
        var p = GameObject.FindWithTag("Player");
        if (cam == null || p == null) { Destroy(this); return; }
        player = p.transform;
        Playing = true;

        follow = cam.GetComponent<IsoCameraFollow>();
        camOffset = cam.transform.position - player.position;
        if (follow != null) follow.enabled = false;

        // She stands outside her own door, lantern lit, facing it.
        doorStep = DoorStep();
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = doorStep;
        player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
        playerScripts = new Behaviour[] { player.GetComponent<PlayerMovement>(), player.GetComponent<Pistol>(), player.GetComponent<Listen>() };
        foreach (var b in playerScripts) if (b != null) b.enabled = false;

        StalkerDirector.Suppressed = true;
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTime(0.72f);   // dusk, going dark as the lines play
        AudioListener.volume = 0f;

        Campfire fire = FirstFire();
        if (fire != null) fire.Flare();

        // Optional voice-over: one clip per line. Timing stretches to fit whatever is there.
        voice = new AudioClip[Lines.Length];
        bool anyVoice = false;
        for (int i = 0; i < Lines.Length; i++) { voice[i] = Resources.Load<AudioClip>("Audio/Intro" + (i + 1)); anyVoice |= voice[i] != null; }
        hintVoice = Resources.Load<AudioClip>("Audio/IntroHint");
        if (anyVoice || hintVoice != null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.spatialBlend = 0f; voiceSource.volume = 1f; voiceSource.playOnAwake = false;
        }
        // Each line stays up for its clip, or long enough to read it.
        {
            float tt = 1f;
            for (int i = 0; i < Lines.Length; i++)
            {
                LineTimes[i] = tt;
                int words = Lines[i].Split(' ').Length;
                float dur = voice[i] != null ? voice[i].length + 0.6f : words * 0.34f + 1.2f;
                tt += Mathf.Max(dur, 3f);
            }
            ShotC = tt;
            FadeOutEnd = ShotC + 1.5f;
            FadeInEnd = FadeOutEnd + 1.5f;
            End = FadeInEnd + (hintVoice != null ? hintVoice.length + 1.5f : 4.5f);
        }
    }

    private Vector3 DoorStep()
    {
        // In front of the current level's door, just outside the home zone.
        var door = FindFirstObjectByType<DoorOpener>();   // the current level's door (inactive ones are skipped)
        Vector3 basePos = door != null ? door.transform.position : new Vector3(0f, 0f, -3f);
        Vector3 spot = basePos + new Vector3(0.55f, 0f, -1.3f);
        spot.y = 1.0f;
        return spot;
    }

    private static Campfire FirstFire()
    {
        Campfire best = null; float bestD = float.MaxValue;
        foreach (var c in Campfire.All) { float d = c.transform.position.magnitude; if (d < bestD) { bestD = d; best = c; } }
        return best;
    }

    private Vector3 FireTarget()
    {
        Campfire f = FirstFire();
        return f != null ? new Vector3(f.transform.position.x, 0f, f.transform.position.z) : new Vector3(10f, 0f, -36f);
    }

    private void Update()
    {
        if (done) return;
        t += Time.deltaTime;

        bool press = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                     (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (press && t < ShotC) t = ShotC;

        // Audio comes up with the first shot.
        AudioListener.volume = Mathf.Clamp01(t / 1f);

        // Voice lines fire as their subtitle appears.
        if (voiceSource != null)
        {
            if (nextVoiceLine < Lines.Length && t >= LineTimes[nextVoiceLine] && t < ShotC)
            {
                if (voice[nextVoiceLine] != null) { voiceSource.clip = voice[nextVoiceLine]; voiceSource.Play(); }
                nextVoiceLine++;
            }
            if (!hintSpoken && hintVoice != null && t >= FadeInEnd) { hintSpoken = true; voiceSource.clip = hintVoice; voiceSource.Play(); }
            if (press && voiceSource.isPlaying && t >= ShotC) voiceSource.Stop();
        }

        if (t < ShotC) DriveCamera();
        DriveCameo();

        if (t >= FadeOutEnd - 0.5f && !dawnSet)
        {
            dawnSet = true;
            Snowfall.FollowOverride = null;
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTime(0.28f);
            if (cameo != null) Destroy(cameo);
            // Hand the camera back exactly where the follow camera would be.
            cam.transform.position = player.position + camOffset;
            cam.orthographicSize = 9f;
            if (follow != null) follow.enabled = true;
            foreach (var b in playerScripts) if (b != null) b.enabled = true;
            StalkerDirector.Suppressed = false;
        }

        if (t >= End)
        {
            done = true;
            Playing = false;
            Destroy(this);
        }
    }

    private void DriveCamera()
    {
        Vector3 fire = FireTarget();
        Vector3 target; float size;
        if (t < ShotA)
        {
            float k = Ease(t / ShotA);
            target = Vector3.Lerp(fire, fire + (Vector3.zero - fire).normalized * 3.5f, k);
            size = Mathf.Lerp(20f, 14f, k);
        }
        else if (t < ShotB)
        {
            float k = Ease((t - ShotA) / (ShotB - ShotA));
            Vector3 mid = Vector3.Lerp(fire, Vector3.zero, 0.7f);
            target = Vector3.Lerp(fire + (Vector3.zero - fire).normalized * 3.5f, mid, k);
            size = Mathf.Lerp(14f, 13f, k);
        }
        else
        {
            float k = Ease((t - ShotB) / (ShotC - ShotB));
            Vector3 mid = Vector3.Lerp(fire, Vector3.zero, 0.7f);
            Vector3 end = new Vector3(doorStep.x, 0f, doorStep.z + 1.5f);
            target = Vector3.Lerp(mid, end, k);
            size = Mathf.Lerp(13f, 9f, k);
        }
        cam.transform.position = target + camOffset;
        cam.orthographicSize = size;
        Snowfall.FollowOverride = target;
    }

    private void DriveCameo()
    {
        // A stalker walks across the second shot, between the trees, glow and all.
        float start = ShotA + 0.5f, endT = ShotB - 0.5f;
        if (t < start || t > endT) { if (cameo != null && t > endT) Destroy(cameo); return; }
        if (cameo == null)
        {
            var prefab = StalkerDirector.Instance != null ? StalkerDirector.Instance.StalkerPrefab : null;
            if (prefab == null) return;
            Vector3 fire = FireTarget();
            Vector3 mid = Vector3.Lerp(fire, Vector3.zero, 0.7f);
            Vector3 right = Vector3.Cross(Vector3.up, (Vector3.zero - fire).normalized);
            cameoFrom = mid + right * 9f + Vector3.up * 1.0f;
            cameoTo = mid - right * 8f + Vector3.up * 1.0f;
            cameo = Instantiate(prefab, cameoFrom, Quaternion.LookRotation(cameoTo - cameoFrom, Vector3.up));
            var s = cameo.GetComponent<Stalker>(); if (s != null) s.enabled = false;
            var e = cameo.GetComponent<FootprintEmitter>(); if (e != null) e.enabled = false;
            var c = cameo.GetComponent<CharacterController>(); if (c != null) c.enabled = false;
            cameoAnim = cameo.GetComponentInChildren<Animator>();
        }
        float k = (t - start) / (endT - start);
        cameo.transform.position = Vector3.Lerp(cameoFrom, cameoTo, k);
        if (cameoAnim != null) cameoAnim.SetFloat("Speed", 2.0f);
    }
    private Vector3 cameoFrom, cameoTo;

    private static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    /// <summary>Black text with a soft pale halo so it reads on the night scene.</summary>
    private static void DrawLegible(Rect r, string text, GUIStyle style, float alpha)
    {
        if (alpha <= 0f) return;
        Color saved = style.normal.textColor;
        style.normal.textColor = new Color(1f, 1f, 1f, 0.55f * alpha);
        foreach (var o in new[] { new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f), new Vector2(0f, -1.5f), new Vector2(0f, 1.5f), new Vector2(-1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f) })
            GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), text, style);
        style.normal.textColor = new Color(saved.r, saved.g, saved.b, alpha);
        GUI.Label(r, text, style);
        style.normal.textColor = saved;
    }

    private void OnGUI()
    {
        if (done) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.95f, 0.9f, 0.8f);
            sub = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            sub.normal.textColor = new Color(0.05f, 0.05f, 0.07f);
            hintStyle = new GUIStyle(sub) { fontSize = 21, fontStyle = FontStyle.BoldAndItalic };
            hintStyle.normal.textColor = new Color(0.05f, 0.05f, 0.07f);
            black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply();
        }

        // Black: opening fade-in, the cut to dawn, and the final fade-in.
        float overlay;
        if (t < 2f) overlay = 1f - t / 2f;
        else if (t < ShotC) overlay = 0f;
        else if (t < FadeOutEnd) overlay = (t - ShotC) / (FadeOutEnd - ShotC);
        else if (t < FadeInEnd) overlay = 1f - (t - FadeOutEnd) / (FadeInEnd - FadeOutEnd);
        else overlay = 0f;
        // Letterbox bars while the camera is ours.
        float bars = t < ShotC ? 1f : Mathf.Clamp01(1f - (t - ShotC) / 1.5f);
        GUI.color = new Color(0f, 0f, 0f, 1f);
        float barH = Screen.height * 0.11f * bars;
        if (barH > 0f) { GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), black); GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), black); }
        GUI.color = new Color(0f, 0f, 0f, overlay);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);

        float w = Mathf.Min(1000f, Screen.width - 80f);
        float x = (Screen.width - w) * 0.5f;

        // Title over the first shot.
        float titleA = t < 1f ? 0f : t < 2.5f ? (t - 1f) / 1.5f : t < 5f ? 1f : Mathf.Clamp01(1f - (t - 5f) / 1f);
        GUI.color = new Color(1f, 1f, 1f, titleA);
        GUI.Label(new Rect(x, Screen.height * 0.3f, w, 100), Title, title);

        // Subtitles, one at a time.
        for (int i = 0; i < Lines.Length; i++)
        {
            float s = LineTimes[i], e = i + 1 < Lines.Length ? LineTimes[i + 1] : ShotC;
            float a = t < s ? 0f : t < s + 0.7f ? (t - s) / 0.7f : t < e - 0.4f ? 1f : Mathf.Clamp01((e - t) / 0.4f);
            if (a <= 0f) continue;
            DrawLegible(new Rect(x, Screen.height * 0.6f, w, 70), Lines[i], sub, a);
        }

        // Hint after dawn.
        float hintA = t < FadeInEnd ? 0f : t < FadeInEnd + 0.8f ? (t - FadeInEnd) / 0.8f : t < End - 1f ? 1f : Mathf.Clamp01(End - t);
        DrawLegible(new Rect(x, Screen.height * 0.6f, w, 60), Hint, hintStyle, hintA);

        if (t < ShotC) { GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.Label(new Rect(x, Screen.height - 50f, w, 30), "any key to skip", hintStyle); }
        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        Playing = false;
        Snowfall.FollowOverride = null;
        Time.timeScale = 1f;
        AudioListener.volume = 1f;
        StalkerDirector.Suppressed = false;
        if (follow != null) follow.enabled = true;
        if (playerScripts != null) foreach (var b in playerScripts) if (b != null) b.enabled = true;
        if (cameo != null) Destroy(cameo);
    }
}
