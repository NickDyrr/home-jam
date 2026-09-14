using UnityEngine;

/// <summary>
/// Drops a footprint every stride of ground covered, alternating left and
/// right, offset to either side of the path. Distance-based so it works for
/// the animated player and the capsule survivors and stalkers alike.
/// </summary>
public class FootprintEmitter : MonoBehaviour
{
    [SerializeField] private float stride = 0.7f;
    [SerializeField] private float sideOffset = 0.14f;
    [SerializeField] private float printSize = 0.34f;
    [SerializeField] private Color tint = new Color(0.55f, 0.62f, 0.76f, 1f);
    [Tooltip("Ignore moves bigger than this in one frame (teleports, respawns).")]
    [SerializeField] private float teleportThreshold = 3f;
    [Tooltip("Print look for this walker. Leave empty for the manager's default (a boot).")]
    [SerializeField] private Material printMaterial;

    [Header("Sound")]
    [Tooltip("Loudness of this walker's steps.")]
    [SerializeField] private float stepVolume = 0.45f;
    [Tooltip("Pitch of the step loop at a run (1 = walk).")]
    [SerializeField] private float runPitch = 1.3f;

    private Vector3 lastPrintPos;
    private Vector3 lastPos;
    private bool leftNext;
    private float travelled;
    private AudioSource stepLoop;
    private float speed;

    private void OnEnable()
    {
        lastPrintPos = transform.position;
        lastPos = transform.position;
        travelled = 0f;
    }

    private void OnDisable()
    {
        if (stepLoop != null) stepLoop.volume = 0f;
    }

    /// <summary>
    /// A recording of someone walking (longer than a second or two) is looped while this walker
    /// moves, pitched up at a run. A short clip is played once per print instead.
    /// </summary>
    private bool UseLoop(AudioClip clip) => clip != null && clip.length > 1.5f;

    private void LateUpdate()
    {
        if (FootprintManager.Instance == null) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        delta.y = 0f;
        lastPos = pos;

        float d = delta.magnitude;
        bool teleported = d > teleportThreshold;
        speed = Mathf.Lerp(speed, teleported ? 0f : d / Mathf.Max(Time.deltaTime, 0.0001f), 1f - Mathf.Exp(-8f * Time.deltaTime));

        // Looping footsteps follow the walker's speed.
        if (AudioManager.Instance != null)
        {
            bool heavy = printSize > 0.5f;
            var clip = heavy ? AudioManager.Instance.StepHeavy : AudioManager.Instance.Step;
            if (UseLoop(clip))
            {
                if (stepLoop == null)
                {
                    // Own source, so this script controls the volume every frame (the manager's
                    // looped sources hold a fixed level).
                    stepLoop = gameObject.AddComponent<AudioSource>();
                    stepLoop.clip = clip; stepLoop.loop = true; stepLoop.spatialBlend = 0f; stepLoop.playOnAwake = false; stepLoop.volume = 0f;
                    stepLoop.time = Random.Range(0f, clip.length);
                    stepLoop.Play();
                }
                bool moving = speed > 0.4f;
                // Fade with distance from the player, the way the manager's one-shots do.
                float dist = PlayerMovement.Instance != null ? Vector3.Distance(PlayerMovement.Instance.transform.position, pos) : 0f;
                float near = 1f - Mathf.Clamp01(dist / 24f) * Mathf.Clamp01(dist / 24f);
                float wantVol = moving ? stepVolume * (heavy ? 1.4f : 1f) * near : 0f;
                float wantPitch = (heavy ? 0.8f : 1f) * Mathf.Lerp(1f, runPitch, Mathf.InverseLerp(4.5f, 6.5f, speed));
                stepLoop.volume = Mathf.Lerp(stepLoop.volume, wantVol, 1f - Mathf.Exp(-10f * Time.deltaTime));
                stepLoop.pitch = Mathf.Lerp(stepLoop.pitch, wantPitch, 1f - Mathf.Exp(-6f * Time.deltaTime));
                if (AudioListener.pause == false && !stepLoop.isPlaying) stepLoop.Play();
            }
        }

        if (teleported) { lastPrintPos = pos; travelled = 0f; return; }
        if (d < 0.0005f) return;

        travelled += d;
        if (travelled < stride) return;
        travelled = 0f;

        Vector3 dir = pos - lastPrintPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();
        lastPrintPos = pos;

        Vector3 side = Vector3.Cross(Vector3.up, dir) * (leftNext ? -sideOffset : sideOffset);
        FootprintManager.Instance.Stamp(pos + side, dir, leftNext, printSize, tint, printMaterial);
        leftNext = !leftNext;

        if (AudioManager.Instance != null)
        {
            bool heavy = printSize > 0.5f;
            var clip = heavy ? AudioManager.Instance.StepHeavy : AudioManager.Instance.Step;
            if (!UseLoop(clip))
                AudioManager.Instance.Play(clip, pos, heavy ? 0.8f : 0.35f, heavy ? 30f : 18f, Random.Range(0.9f, 1.1f));
        }
    }
}
