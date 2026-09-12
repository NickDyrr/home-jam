using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// What the player feels before she sees. A vignette closes in and a
/// heartbeat quickens with the nearest hunting stalker, and the screen
/// tightens further with the lantern off at night. The volume and profile
/// are made at runtime so no asset changes are needed.
/// </summary>
public class Dread : MonoBehaviour
{
    [SerializeField] private float nearDistance = 6f;
    [SerializeField] private float farDistance = 26f;
    [SerializeField] private float baseVignette = 0.18f;
    [SerializeField] private float darkVignette = 0.42f;
    [SerializeField] private float dreadVignette = 0.3f;

    private Vignette vignette;
    private float level;
    private float nextBeat;
    private Transform player;

    private void Start()
    {
        var go = new GameObject("DreadVolume");
        go.transform.SetParent(transform);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 10f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(baseVignette);
        vignette.smoothness.Override(0.55f);
        vignette.color.Override(new Color(0.02f, 0.02f, 0.05f));
        vol.sharedProfile = profile;
        var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (player == null) return;

        // Nearest hunting stalker, flat distance.
        float nearest = float.MaxValue;
        foreach (Stalker s in Stalker.All)
        {
            if (s.CurrentState != Stalker.State.Hunting) continue;
            Vector3 d = s.transform.position - player.position; d.y = 0f;
            nearest = Mathf.Min(nearest, d.magnitude);
        }
        float target = nearest == float.MaxValue ? 0f : 1f - Mathf.Clamp01((nearest - nearDistance) / (farDistance - nearDistance));
        level = Mathf.Lerp(level, target, 1f - Mathf.Exp(-3f * Time.deltaTime));

        bool home = HomeZone.Instance != null && HomeZone.Instance.PlayerIsHome;
        float night = DayNightCycle.Instance != null ? 1f - DayNightCycle.Instance.Daylight : 1f;
        float dark = (!Lantern.IsOn && !home) ? night : 0f;

        if (vignette != null)
            vignette.intensity.value = Mathf.Clamp01(baseVignette + dark * (darkVignette - baseVignette) + level * dreadVignette);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetWind(home ? 0.12f : 0.35f + 0.15f * night);
            if (level > 0.05f && Time.time >= nextBeat)
            {
                float bpm = Mathf.Lerp(55f, 150f, level);
                nextBeat = Time.time + 60f / bpm;
                AudioManager.Instance.Play(AudioManager.Instance.Heartbeat, player.position, 0.25f + 0.55f * level, 1000f, Mathf.Lerp(0.9f, 1.15f, level));
            }
        }
    }
}
