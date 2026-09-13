using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The opening, played in the world. Dusk. The camera starts wide over the
/// forest on a lone camp fire, drifts toward home while a stalker crosses
/// between the trees, and pans onto the house. Cut to black, then inside:
/// she is sitting in the armchair by the fire. On the last line she stands
/// and turns to the door. Any key skips. Edit the lines below for the story;
/// voice clips Resources/Audio/Intro1..5 stretch the timing to fit.
/// </summary>
public class Intro : MonoBehaviour
{
    public static bool Playing { get; private set; }

    public static readonly string Title = "HOME";
    public static readonly string[] Lines =
    {
        "The snow came early this year. Something came down with it.",
        "The rest of us ran for the forest.",
        "I made it back here. They come as far as the fence and no further. I don't know why.",
        "Out past the trees, fires are still burning. Every night there are fewer.",
        "I'm going to find them, and I'm going to bring them home.",
    };
    public static readonly string Hint = "Light finds them. Light finds you. Be home before dark.";

    // Where she sits: the armchair by the fire, facing into the room.
    private static readonly Vector3 ChairSpot = new Vector3(1.85f, 1.0f, -1.5f);
    private static readonly Vector3 ChairFacing = Vector3.left;

    // Timeline (seconds)
    private const float ShotA = 6f, ShotB = 13f;
    private readonly float[] LineTimes = { 1f, 5f, 9f, 13f, 17f };
    private float cutStart, cut, storyEnd, end;
    private const float FadeToBlack = 0.8f, FadeFromBlack = 0.8f;
    private const float StandAfter = 1.2f, TurnAfter = 2.2f, TurnSeconds = 0.6f;

    private float t;
    private bool done, insideNow, stood, controllerRestored;
    private Camera cam;
    private IsoCameraFollow follow;
    private Vector3 camOffset;
    private Transform player;
    private Animator playerAnim;
    private RuntimeAnimatorController playerController;
    private Behaviour[] playerScripts;
    private GameObject cameo;
    private Animator cameoAnim;
    private Vector3 cameoFrom, cameoTo;
    private AudioClip[] voice;
    private AudioSource voiceSource;
    private int nextVoiceLine;
    private GUIStyle title, sub, hintStyle;
    private Texture2D black;
    private static readonly int SittingHash = Animator.StringToHash("Sitting");

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

        // She is home, in the chair, while the outside plays. The house shows its outside until the cut.
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = ChairSpot;
        player.rotation = Quaternion.LookRotation(ChairFacing, Vector3.up);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
        playerScripts = new Behaviour[] { player.GetComponent<PlayerMovement>(), player.GetComponent<Pistol>(), player.GetComponent<Listen>() };
        foreach (var b in playerScripts) if (b != null) b.enabled = false;

        // Borrow the survivors' animator (it has the sitting pose) for the chair.
        playerAnim = player.GetComponentInChildren<Animator>();
        if (playerAnim != null)
        {
            playerController = playerAnim.runtimeAnimatorController;
            RuntimeAnimatorController sit = null;
            foreach (var s in Survivor.All) { var a = s.GetComponentInChildren<Animator>(); if (a != null && a.runtimeAnimatorController != null) { sit = a.runtimeAnimatorController; break; } }
            if (sit != null) { playerAnim.runtimeAnimatorController = sit; playerAnim.SetBool(SittingHash, true); }
        }

        HouseView.ForceOutside = true;
        if (HouseView.Instance != null) HouseView.Instance.RefreshView();
        StalkerDirector.Suppressed = true;
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTime(0.72f);
        AudioListener.volume = 0f;

        Campfire fire = FirstFire();
        if (fire != null) fire.Flare();

        // Voice: one clip per line; each line holds for its clip, or long enough to read.
        voice = new AudioClip[Lines.Length];
        bool anyVoice = false;
        for (int i = 0; i < Lines.Length; i++) { voice[i] = Resources.Load<AudioClip>("Audio/Intro" + (i + 1)); anyVoice |= voice[i] != null; }
        if (anyVoice) { voiceSource = gameObject.AddComponent<AudioSource>(); voiceSource.spatialBlend = 0f; voiceSource.playOnAwake = false; }
        float tt = 1f;
        for (int i = 0; i < Lines.Length; i++)
        {
            LineTimes[i] = tt;
            int words = Lines[i].Split(' ').Length;
            float dur = voice[i] != null ? voice[i].length + 0.6f : words * 0.34f + 1.2f;
            tt += Mathf.Max(dur, 3f);
        }
        cut = LineTimes[Lines.Length - 1];
        cutStart = cut - FadeToBlack;
        storyEnd = tt;
        end = storyEnd + 3.5f;
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
        if (press)
        {
            if (t < cutStart) t = cutStart;
            else if (t < storyEnd) { t = storyEnd; if (voiceSource != null) voiceSource.Stop(); }
        }

        AudioListener.volume = Mathf.Clamp01(t / 1f);

        if (voiceSource != null && nextVoiceLine < Lines.Length && t >= LineTimes[nextVoiceLine] && t < storyEnd)
        {
            if (voice[nextVoiceLine] != null) { voiceSource.clip = voice[nextVoiceLine]; voiceSource.Play(); }
            nextVoiceLine++;
        }

        if (t < cutStart) DriveCamera();
        DriveCameo();

        if (t >= cut && !insideNow) CutInside();
        if (insideNow)
        {
            if (!stood && t >= cut + StandAfter) { stood = true; if (playerAnim != null) playerAnim.SetBool(SittingHash, false); }
            if (t >= cut + TurnAfter)
            {
                Vector3 toDoor = DoorPosition() - player.position; toDoor.y = 0f;
                if (toDoor.sqrMagnitude > 0.01f)
                {
                    float k = Ease((t - (cut + TurnAfter)) / TurnSeconds);
                    Quaternion want = Quaternion.LookRotation(toDoor.normalized, Vector3.up);
                    player.rotation = Quaternion.Slerp(Quaternion.LookRotation(ChairFacing, Vector3.up), want, k);
                }
            }
            if (t >= storyEnd && !controllerRestored) RestoreControl();
        }

        if (t >= end) { done = true; Playing = false; Destroy(this); }
    }

    private static Vector3 DoorPosition()
    {
        var door = FindFirstObjectByType<DoorOpener>();
        return door != null ? door.transform.position + new Vector3(0.55f, 0f, 0f) : new Vector3(0f, 0f, -3f);
    }

    private void CutInside()
    {
        insideNow = true;
        Snowfall.FollowOverride = null;
        if (cameo != null) Destroy(cameo);
        HouseView.ForceOutside = false;
        if (HouseView.Instance != null) HouseView.Instance.RefreshView();
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTime(0.28f);
        cam.transform.position = player.position + camOffset;
        cam.orthographicSize = 7f;
    }

    private void RestoreControl()
    {
        controllerRestored = true;
        if (playerAnim != null && playerController != null) playerAnim.runtimeAnimatorController = playerController;
        foreach (var b in playerScripts) if (b != null) b.enabled = true;
        if (follow != null) follow.enabled = true;
        StalkerDirector.Suppressed = false;
    }

    private void DriveCamera()
    {
        Vector3 fire = FireTarget();
        Vector3 toHome = (Vector3.zero - fire).normalized;
        Vector3 target; float size;
        if (t < ShotA)
        {
            float k = Ease(t / ShotA);
            target = Vector3.Lerp(fire, fire + toHome * 3.5f, k);
            size = Mathf.Lerp(20f, 14f, k);
        }
        else if (t < ShotB)
        {
            float k = Ease((t - ShotA) / (ShotB - ShotA));
            target = Vector3.Lerp(fire + toHome * 3.5f, Vector3.Lerp(fire, Vector3.zero, 0.6f), k);
            size = Mathf.Lerp(14f, 13f, k);
        }
        else
        {
            float k = Ease((t - ShotB) / Mathf.Max(0.1f, cutStart - ShotB));
            target = Vector3.Lerp(Vector3.Lerp(fire, Vector3.zero, 0.6f), new Vector3(0f, 0f, -1.5f), k);
            size = Mathf.Lerp(13f, 9f, k);
        }
        cam.transform.position = target + camOffset;
        cam.orthographicSize = size;
        Snowfall.FollowOverride = target;
    }

    private void DriveCameo()
    {
        // A stalker walks across the second shot, out past the fence and toward screen-right.
        float start = ShotA + 0.5f, endT = ShotB - 0.5f;
        if (t < start || t > endT) { if (cameo != null && t > endT) Destroy(cameo); return; }
        if (cameo == null)
        {
            var prefab = StalkerDirector.Instance != null ? StalkerDirector.Instance.StalkerPrefab : null;
            if (prefab == null) return;
            Vector3 fire = FireTarget();
            Vector3 toHome = (Vector3.zero - fire).normalized;
            Vector3 mid = Vector3.Lerp(fire, Vector3.zero, 0.45f);          // well outside the fence
            Vector3 across = Vector3.Cross(Vector3.up, toHome);
            Vector3 screenRight = cam.transform.right; screenRight.y = 0f; screenRight.Normalize();
            Vector3 centre = mid + screenRight * 6f;
            cameoFrom = centre + across * 8f + Vector3.up * 1.0f;
            cameoTo = centre - across * 8f + Vector3.up * 1.0f;
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

    private static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

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

        // Black: opening fade-in, the cut inside, then clear.
        float overlay;
        if (t < 2f) overlay = 1f - t / 2f;
        else if (t < cutStart) overlay = 0f;
        else if (t < cut) overlay = (t - cutStart) / FadeToBlack;
        else if (t < cut + FadeFromBlack) overlay = 1f - (t - cut) / FadeFromBlack;
        else overlay = 0f;
        float bars = t < storyEnd ? 1f : Mathf.Clamp01(1f - (t - storyEnd) / 1.5f);
        GUI.color = Color.black;
        float barH = Screen.height * 0.11f * bars;
        if (barH > 0f) { GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), black); GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), black); }
        GUI.color = new Color(0f, 0f, 0f, overlay);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);

        float w = Mathf.Min(1000f, Screen.width - 80f);
        float x = (Screen.width - w) * 0.5f;

        float titleA = t < 1f ? 0f : t < 2.5f ? (t - 1f) / 1.5f : t < 5f ? 1f : Mathf.Clamp01(1f - (t - 5f) / 1f);
        GUI.color = new Color(1f, 1f, 1f, titleA);
        GUI.Label(new Rect(x, Screen.height * 0.3f, w, 100), Title, title);

        // Subtitles: each line holds until the next starts; the last through the story's end.
        for (int i = 0; i < Lines.Length; i++)
        {
            float s = LineTimes[i], e = i + 1 < Lines.Length ? LineTimes[i + 1] : storyEnd;
            float a = t < s ? 0f : t < s + 0.3f ? (t - s) / 0.3f : t < e ? 1f : 0f;
            if (a <= 0f) continue;
            DrawLegible(new Rect(x, Screen.height * 0.6f, w, 70), Lines[i], sub, a);
        }

        float hintA = t < storyEnd ? 0f : t < storyEnd + 0.6f ? (t - storyEnd) / 0.6f : t < end - 0.8f ? 1f : Mathf.Clamp01((end - t) / 0.8f);
        DrawLegible(new Rect(x, Screen.height * 0.6f, w, 60), Hint, hintStyle, hintA);

        if (t < storyEnd) { GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.Label(new Rect(x, Screen.height - 50f, w, 30), "any key to skip", hintStyle); }
        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        Playing = false;
        Snowfall.FollowOverride = null;
        HouseView.ForceOutside = false;
        if (HouseView.Instance != null) HouseView.Instance.RefreshView();
        AudioListener.volume = 1f;
        StalkerDirector.Suppressed = false;
        if (!controllerRestored) RestoreControl();
        if (cameo != null) Destroy(cameo);
    }
}
