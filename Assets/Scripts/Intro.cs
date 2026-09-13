using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The opening, played in the world. Night. The camera holds wide on the house and
/// its fence while a stalker comes up to the fence and turns back; then it tracks along
/// the first survivor's trail in the snow to their fire. Cut to black, then inside: she
/// is in the armchair, stands, walks, and turns to the door. Any key skips. Edit the
/// lines below for the story; voice clips Resources/Audio/Intro1..5 stretch the timing.
/// </summary>
public class Intro : MonoBehaviour
{
    public static bool Playing { get; private set; }

    public static readonly string Title = "HOME";
    public static readonly string[] Lines =
    {
        "The snow came early this year. Something came down with it.",
        "They come as far as my fence and no further. I don't know why.",
        "Bullets won't kill them. They only slow them down.",
        "The others are out in the woods. They hide by day and light their fires at night.",
        "I'll find them. One at a time. And I'll bring them home.",
    };

    // Where she sits: the armchair by the fire, facing into the room.
    private static readonly Vector3 ChairSpot = new Vector3(1.45f, 1.0f, -1.5f);
    private static readonly Vector3 ChairFacing = Vector3.left;
    private const float StepsForward = 1.7f;     // metres she walks after standing, before turning to the door
    private const float StepSpeed = 1.4f;

    // Timeline (seconds). Shot A (the house and its fence) runs through all but the last two
    // lines, with a stalker coming up to the fence and turning back on line 2. Shot B (the
    // second-to-last line) tracks along the first survivor's trail to their fire. The last
    // line is inside.
    private readonly float[] LineTimes = { 1f, 5f, 9f, 13f, 17f };
    private float shotB;                      // when shot B starts (= second-to-last line)
    private float cutStart, cut, storyEnd, end;
    private const float FadeToBlack = 0.8f, FadeFromBlack = 0.8f;
    private const float StandAfter = 1.2f;
    private const float RiseSeconds = 0.7f;                       // speed and height ease in over this as she rises
    private const float TurnSeconds = 0.6f;                       // the turn to the door once her steps are done
    private float turnStart = -1f;
    private const float StandY = 1.18f;                           // player pivot height when standing on the cabin floor

    private float t;
    private bool done, insideNow, stood, controllerRestored;
    private float walked;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
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
    private System.Collections.Generic.List<Vector3> cameoPath;
    private float cameoLength;
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
        if (cc != null) cc.enabled = false;                       // stays off until control is restored
        player.position = ChairSpot;
        player.rotation = Quaternion.LookRotation(ChairFacing, Vector3.up);
        DoorOpener.HoldClosed = true;                             // she is near the door, but it stays shut until the intro is over
        playerScripts = new Behaviour[] { player.GetComponent<PlayerMovement>(), player.GetComponent<Pistol>() };
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
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTime(0.75f);
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
        shotB = LineTimes[Lines.Length - 2];
        cut = LineTimes[Lines.Length - 1];
        cutStart = cut - FadeToBlack;
        storyEnd = tt;
        end = storyEnd + 1.5f;
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
            if (stood)
            {
                // She gets up and walks off in one motion: the walk cycle starts as she rises and
                // her speed eases in, so the legs animate out of the chair instead of sliding. She
                // rises to standing height over the first moments, then turns to face the door.
                // The controller is off for the whole intro; she is moved by hand.
                float since = t - (cut + StandAfter);
                bool walking = walked < StepsForward;
                if (walking) walked = Mathf.Min(StepsForward, walked + StepSpeed * Ease(since / RiseSeconds) * Time.deltaTime);
                Vector3 pos = ChairSpot + ChairFacing * walked;
                pos.y = Mathf.Lerp(ChairSpot.y, StandY, Ease(since / RiseSeconds));
                player.position = pos;
                if (playerAnim != null) playerAnim.SetFloat(SpeedHash, walking ? 1f : 0f, 0.1f, Time.deltaTime);

                if (!walking)
                {
                    // At the end of her steps: turn to the door.
                    if (turnStart < 0f) turnStart = t;
                    Vector3 toDoor = DoorPosition() - player.position; toDoor.y = 0f;
                    if (toDoor.sqrMagnitude > 0.01f)
                    {
                        float k = Ease((t - turnStart) / TurnSeconds);
                        player.rotation = Quaternion.Slerp(Quaternion.LookRotation(ChairFacing, Vector3.up), Quaternion.LookRotation(toDoor.normalized, Vector3.up), k);
                    }
                }
            }
            if (t >= storyEnd && !controllerRestored) RestoreControl();
        }

        if (t >= end) { done = true; Playing = false; DoorOpener.HoldClosed = false; Destroy(this); }
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
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.SetTime(0.55f);   // late afternoon: about 70 s of light, then the first night
        cam.transform.position = player.position + camOffset;
        cam.orthographicSize = 7f;
    }

    private void RestoreControl()
    {
        controllerRestored = true;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) { Vector3 p = player.position; p.y = Mathf.Max(p.y, StandY); player.position = p; cc.enabled = true; Physics.SyncTransforms(); }
        if (playerAnim != null && playerController != null) playerAnim.runtimeAnimatorController = playerController;
        foreach (var b in playerScripts) if (b != null) b.enabled = true;
        if (follow != null) follow.enabled = true;
        StalkerDirector.Suppressed = false;
    }

    private void DriveCamera()
    {
        Vector3 target; float size;
        if (t < shotB)
        {
            // Shot A: home, wide, with the fence in frame; a slow drift in over two lines.
            float k = t / shotB;
            target = Vector3.Lerp(new Vector3(-3f, 0f, -4f), new Vector3(-2.5f, 0f, -3f), k);
            size = Mathf.Lerp(14.5f, 12f, k);
        }
        else
        {
            // Shot B: along the first survivor's tracks to their fire, arriving as the line ends.
            Vector3 fire = FireTarget();
            float k = Ease((t - shotB) / Mathf.Max(0.1f, cutStart - shotB));
            target = Vector3.Lerp(TrailStart(), fire, k);
            size = Mathf.Lerp(8.5f, 7.5f, k);
        }
        cam.transform.position = target + camOffset;
        cam.orthographicSize = size;
        Snowfall.FollowOverride = target;
    }

    /// <summary>Where the first survivor's trail begins (fallback: 20 m toward home from the fire).</summary>
    private Vector3 TrailStart()
    {
        Vector3 fire = FireTarget();
        Survivor nearest = null; float best = float.MaxValue;
        foreach (var s in Survivor.All) { float d = (s.transform.position - fire).sqrMagnitude; if (d < best) { best = d; nearest = s; } }
        if (nearest != null && Trails.Instance != null && Trails.Instance.StartOf(nearest, out Vector3 start)) return start;
        return fire + (Vector3.zero - fire).normalized * 20f;
    }

    /// <summary>Screen axes on the ground: right, and "up" (away from the camera).</summary>
    private void ScreenAxes(out Vector3 right, out Vector3 up)
    {
        right = cam.transform.right; right.y = 0f; right.Normalize();
        up = cam.transform.forward; up.y = 0f; up.Normalize();
    }

    /// <summary>First tree trunk within clearance of the segment a-b (flat), or null.</summary>
    public static Transform FirstTreeOn(Vector3 a, Vector3 b, float clearance)
    {
        var forest = GameObject.Find("Forest");
        if (forest == null) return null;
        Vector3 ab = b - a; ab.y = 0f; float len = ab.magnitude; if (len < 0.01f) return null;
        Vector3 dir = ab / len;
        Transform best = null; float bestAlong = float.MaxValue;
        foreach (Transform tr in forest.transform)
        {
            Vector3 ap = tr.position - a; ap.y = 0f;
            float along = Mathf.Clamp(Vector3.Dot(ap, dir), 0.01f, len - 0.01f);
            Vector3 closest = a + dir * along; closest.y = 0f;
            Vector3 p = tr.position; p.y = 0f;
            if ((p - closest).sqrMagnitude < clearance * clearance && along < bestAlong) { bestAlong = along; best = tr; }
        }
        return best;
    }

    /// <summary>
    /// A path from a to b that bobs and weaves between trunks. The route is split into
    /// stations; at each one the walker may sit in any of several lanes to the side of the
    /// straight line. The cheapest chain of lanes whose every segment is clear of trees (and
    /// of the yard) wins, so the monster drifts left and right around the trunks instead of
    /// through them. Falls back to the straight line if no clear chain exists.
    /// </summary>
    public static System.Collections.Generic.List<Vector3> WeavePath(Vector3 a, Vector3 b, float clearance)
    {
        var pts = new System.Collections.Generic.List<Vector3>();
        Vector3 ab = b - a; ab.y = 0f; float len = ab.magnitude;
        if (len < 0.01f) { pts.Add(a); pts.Add(b); return pts; }
        Vector3 dir = ab / len;
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        // Only trunks near the corridor matter; gather them once so the lane search stays cheap.
        var trunks = new System.Collections.Generic.List<Vector3>();
        var forestRoot = GameObject.Find("Forest");
        if (forestRoot != null)
            foreach (Transform tr in forestRoot.transform)
            {
                Vector3 p = tr.position; p.y = 0f;
                Vector3 rel = p - a; rel.y = 0f;
                float along = Vector3.Dot(rel, dir), across = Vector3.Dot(rel, side);
                if (along > -clearance - 1f && along < len + clearance + 1f && Mathf.Abs(across) < 7f) trunks.Add(p);
            }

        const float stationStep = 2.0f, laneStep = 0.75f;
        const int halfLanes = 7;                                   // lanes at -5.25 .. +5.25 m
        int stations = Mathf.Max(2, Mathf.CeilToInt(len / stationStep));
        int lanes = halfLanes * 2 + 1;

        Vector3 P(int k, int l) => a + dir * (len * k / stations) + side * ((l - halfLanes) * laneStep);

        var cost = new float[stations + 1, lanes];
        var from = new int[stations + 1, lanes];
        for (int k = 0; k <= stations; k++) for (int l = 0; l < lanes; l++) cost[k, l] = float.PositiveInfinity;
        // Start and end are off screen, so any lane will do there (slight preference for the centre).
        for (int l = 0; l < lanes; l++) cost[0, l] = Mathf.Abs(l - halfLanes) * 0.3f;
        for (int k = 1; k <= stations; k++)
        {
            for (int l = 0; l < lanes; l++)
            {
                Vector3 pk = P(k, l);
                if (InYard(pk, 1.5f)) continue;
                for (int pl = Mathf.Max(0, l - 3); pl <= Mathf.Min(lanes - 1, l + 3); pl++)
                {
                    if (float.IsInfinity(cost[k - 1, pl])) continue;
                    if (SegmentHits(trunks, P(k - 1, pl), pk, clearance)) continue;
                    float c = cost[k - 1, pl] + Mathf.Abs(l - pl) * 0.6f + Mathf.Abs(l - halfLanes) * 0.15f;
                    if (c < cost[k, l]) { cost[k, l] = c; from[k, l] = pl; }
                }
            }
        }
        int endLane = -1; float endCost = float.PositiveInfinity;
        for (int l = 0; l < lanes; l++) { float c = cost[stations, l] + Mathf.Abs(l - halfLanes) * 0.3f; if (c < endCost) { endCost = c; endLane = l; } }
        if (endLane < 0) { pts.Add(a); pts.Add(b); return pts; }

        var laneAt = new int[stations + 1];
        laneAt[stations] = endLane;
        for (int k = stations; k > 0; k--) laneAt[k - 1] = from[k, laneAt[k]];
        for (int k = 0; k <= stations; k++) pts.Add(P(k, laneAt[k]));
        return pts;
    }

    /// <summary>True if any of the given trunks sits within clearance of the flat segment a-b.</summary>
    private static bool SegmentHits(System.Collections.Generic.List<Vector3> trunks, Vector3 a, Vector3 b, float clearance)
    {
        Vector3 ab = b - a; ab.y = 0f; float len = ab.magnitude; if (len < 0.01f) return false;
        Vector3 dir = ab / len; a.y = 0f;
        float sq = clearance * clearance;
        for (int i = 0; i < trunks.Count; i++)
        {
            Vector3 ap = trunks[i] - a;
            float along = Mathf.Clamp(Vector3.Dot(ap, dir), 0f, len);
            if ((ap - dir * along).sqrMagnitude < sq) return true;
        }
        return false;
    }

    /// <summary>True inside the fenced yard, grown by margin.</summary>
    private static bool InYard(Vector3 p, float margin)
    {
        return Mathf.Abs(p.x) < 9.9f + margin && p.z > -9.9f - margin && p.z < 7.7f + margin;
    }

    private static float PathLength(System.Collections.Generic.List<Vector3> pts)
    {
        float l = 0f; for (int i = 0; i < pts.Count - 1; i++) l += Vector3.Distance(pts[i], pts[i + 1]); return l;
    }

    private static Vector3 PointAlong(System.Collections.Generic.List<Vector3> pts, float dist, out Vector3 dir)
    {
        dir = Vector3.forward;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            float seg = Vector3.Distance(pts[i], pts[i + 1]);
            if (dist <= seg || i == pts.Count - 2)
            {
                dir = (pts[i + 1] - pts[i]).normalized;
                return Vector3.Lerp(pts[i], pts[i + 1], seg > 0f ? Mathf.Clamp01(dist / seg) : 1f);
            }
            dist -= seg;
        }
        return pts[pts.Count - 1];
    }

    private void DriveCameo()
    {
        // On "they come as far as my fence": a stalker runs in from off the bottom of the screen,
        // pulls up short of the west fence, stands facing the house, then turns and walks back
        // into the trees. Gone by the cut to shot B. The path weaves around trunks.
        float arrive = LineTimes[1] + 2.0f, start = arrive - 4.5f, leave = arrive + 2.2f;
        if (t < start || t >= shotB) { if (cameo != null && t >= shotB) Destroy(cameo); return; }
        if (cameo == null)
        {
            var prefab = StalkerDirector.Instance != null ? StalkerDirector.Instance.StalkerPrefab : null;
            if (prefab == null) return;
            ScreenAxes(out Vector3 right, out Vector3 up);
            Vector3 stop = new Vector3(-12.3f, 0f, -1f);                  // in the clear strip just outside the west fence
            Vector3 from = stop - up * 22f;                                // off the bottom edge of shot A
            cameoPath = WeavePath(from, stop, 1.4f);
            for (int i = 0; i < cameoPath.Count; i++) cameoPath[i] = new Vector3(cameoPath[i].x, 1.0f, cameoPath[i].z);
            cameoLength = PathLength(cameoPath);
            cameoFrom = cameoPath[0]; cameoTo = cameoPath[cameoPath.Count - 1];
            cameo = Instantiate(prefab, cameoFrom, Quaternion.LookRotation(cameoPath[1] - cameoFrom, Vector3.up));
            var s = cameo.GetComponent<Stalker>(); if (s != null) s.enabled = false;
            var e = cameo.GetComponent<FootprintEmitter>(); if (e != null) e.enabled = false;
            var c = cameo.GetComponent<CharacterController>(); if (c != null) c.enabled = false;
            cameoAnim = cameo.GetComponentInChildren<Animator>();
        }

        Vector3 pos, dir; float speed;
        if (t < arrive)
        {
            float k = (t - start) / (arrive - start);
            pos = PointAlong(cameoPath, k * cameoLength, out dir);
            speed = cameoLength / (arrive - start);
        }
        else if (t < leave)
        {
            pos = cameoTo; dir = Vector3.zero - cameoTo; speed = 0f;      // stands at the fence, facing the house
        }
        else
        {
            float back = 1.8f * (t - leave);                              // walks back the way it came
            pos = PointAlong(cameoPath, Mathf.Max(0f, cameoLength - back), out dir);
            dir = -dir; speed = 1.8f;
            if (back >= cameoLength) { Destroy(cameo); return; }
        }
        cameo.transform.position = pos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            cameo.transform.rotation = Quaternion.Slerp(cameo.transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 1f - Mathf.Exp(-6f * Time.deltaTime));
        if (cameoAnim != null) cameoAnim.SetFloat("Speed", speed);
    }

    private static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }


    /// <summary>White text with a soft shadow so it reads on snow and on the dark.</summary>
    public static void DrawLegible(Rect r, string text, GUIStyle style, float alpha)
    {
        if (alpha <= 0f) return;
        Color saved = style.normal.textColor;
        style.normal.textColor = new Color(saved.r, saved.g, saved.b, 1f);
        Color shadow = new Color(0f, 0f, 0f, 0.55f * alpha);
        style.normal.textColor = shadow; GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        style.normal.textColor = new Color(saved.r, saved.g, saved.b, 1f);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(r, text, style);
        style.normal.textColor = saved;
        GUI.color = Color.white;
    }

    private void OnGUI()
    {
        if (done) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.95f, 0.9f, 0.8f);
            sub = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            sub.normal.textColor = new Color(0.97f, 0.96f, 0.93f);
            hintStyle = new GUIStyle(sub) { fontSize = 21, fontStyle = FontStyle.BoldAndItalic };
            hintStyle.normal.textColor = new Color(0.97f, 0.96f, 0.93f);
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
        GUI.color = Color.white;   // never let the title's fade bleed into the subtitles

        // Subtitles: each line holds until the next starts; the last through the story's end.
        for (int i = 0; i < Lines.Length; i++)
        {
            float s = LineTimes[i], e = i + 1 < Lines.Length ? LineTimes[i + 1] : storyEnd;
            float a = t < s ? 0f : t < s + 0.3f ? (t - s) / 0.3f : t < e ? 1f : 0f;
            if (a <= 0f) continue;
            DrawLegible(new Rect(x, Screen.height * 0.6f, w, 70), Lines[i], sub, a);
        }


        if (t < storyEnd) { GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.Label(new Rect(x, Screen.height - 50f, w, 30), "any key to skip", hintStyle); }
        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        Playing = false;
        DoorOpener.HoldClosed = false;
        Snowfall.FollowOverride = null;
        HouseView.ForceOutside = false;
        if (HouseView.Instance != null) HouseView.Instance.RefreshView();
        AudioListener.volume = 1f;
        StalkerDirector.Suppressed = false;
        if (!controllerRestored) RestoreControl();
        if (cameo != null) Destroy(cameo);
    }
}
