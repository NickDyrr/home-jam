using UnityEngine;

/// <summary>
/// Drives a full day in cycleSeconds. The night look is the tuned scene
/// (dim blue moon, dark ambient and fog); the day look is blended in from
/// sunrise to sunset with a warm tint at the edges. Moves the sun's pitch
/// through the day and keeps the moon still at night.
/// TimeOfDay is 0..1 with 0 = midnight, 0.25 = sunrise, 0.5 = noon.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Clock")]
    [Tooltip("Real seconds the day half (sunrise to sunset) takes.")]
    [SerializeField] private float daySeconds = 180f;
    [Tooltip("Real seconds the night half takes. Night is the game, so it gets most of the cycle.")]
    [SerializeField] private float nightSeconds = 420f;
    [Range(0f, 1f)] [SerializeField] private float startTimeOfDay = 0.73f;
    [Tooltip("Clock speed while waiting for dark at home.")]
    [SerializeField] private float fastForwardRate = 0.16f;

    [Header("Targets")]
    [SerializeField] private Light sun;
    [SerializeField] private Camera mainCamera;

    [Header("Night (the tuned look)")]
    [SerializeField] private float nightIntensity = 0.22f;
    [SerializeField] private Color nightColor = new Color(0.55f, 0.65f, 0.95f);
    [SerializeField] private float moonPitch = 58f;
    [SerializeField] private Color nightAmbient = new Color(0.07f, 0.09f, 0.15f);
    [SerializeField] private Color nightFog = new Color(0.03f, 0.045f, 0.08f);

    [Header("Day")]
    [SerializeField] private float dayIntensity = 1.1f;
    [SerializeField] private Color dayColor = new Color(1.0f, 0.96f, 0.88f);
    [SerializeField] private Color sunsetColor = new Color(1.0f, 0.62f, 0.38f);
    [SerializeField] private Color dayAmbient = new Color(0.45f, 0.5f, 0.6f);
    [SerializeField] private Color dayFog = new Color(0.62f, 0.68f, 0.78f);
    [SerializeField] private float sunYaw = 325f;

    public float TimeOfDay { get; private set; }
    /// <summary>0 at night, 1 in full day.</summary>
    public float Daylight { get; private set; }
    public float CycleSeconds => daySeconds + nightSeconds;
    /// <summary>True while the clock is racing toward dusk (waiting at home).</summary>
    public bool FastForwarding { get; private set; }
    private float fastForwardTarget;

    /// <summary>Sunrise and sunset, as time-of-day fractions.</summary>
    public const float Sunrise = 0.25f, Sunset = 0.75f;
    public bool IsDayWindow => TimeOfDay >= Sunrise && TimeOfDay < Sunset;

    /// <summary>Real seconds until the clock reaches target (ahead of now, wrapping).</summary>
    public float SecondsUntil(float target)
    {
        float t = TimeOfDay, total = 0f;
        for (int guard = 0; guard < 4; guard++)
        {
            bool day = t >= Sunrise && t < Sunset;
            float windowEnd = day ? Sunset : (t < Sunrise ? Sunrise : 1f + Sunrise);
            float rate = 0.5f / (day ? daySeconds : nightSeconds);
            float tt = target < t ? target + 1f : target;
            if (tt <= windowEnd) return total + (tt - t) / rate;
            total += (windowEnd - t) / rate;
            t = Mathf.Repeat(windowEnd, 1f);
            target = Mathf.Repeat(tt, 1f);
            if (Mathf.Approximately(t, target)) return total;
        }
        return total;
    }

    /// <summary>Race the clock to the given time of day (used to wait for dark at home).</summary>
    public void FastForwardTo(float target)
    {
        fastForwardTarget = Mathf.Repeat(target, 1f);
        FastForwarding = true;
    }

    /// <summary>Multiplier on ambient light, driven by the lantern (dark when it is off at night).</summary>
    public static float AmbientScale = 1f;

    /// <summary>Fires once each morning as daylight comes up.</summary>
    public static event System.Action Dawn;
    private bool wasDay, initialised;

    private void Awake()
    {
        Instance = this;
        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (mainCamera == null) mainCamera = Camera.main;
        TimeOfDay = startTimeOfDay;
        Apply();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        float rate = 0.5f / (IsDayWindow ? Mathf.Max(1f, daySeconds) : Mathf.Max(1f, nightSeconds));
        if (FastForwarding)
        {
            float remaining = Mathf.Repeat(fastForwardTarget - TimeOfDay, 1f);
            float step = fastForwardRate * Time.deltaTime;
            if (step >= remaining) { TimeOfDay = fastForwardTarget; FastForwarding = false; }
            else TimeOfDay = Mathf.Repeat(TimeOfDay + step, 1f);
        }
        else TimeOfDay = Mathf.Repeat(TimeOfDay + rate * Time.deltaTime, 1f);
        Apply();
    }

    /// <summary>Jump the clock. Handy for testing.</summary>
    public void SetTime(float timeOfDay)
    {
        TimeOfDay = Mathf.Repeat(timeOfDay, 1f);
        FastForwarding = false;
        Apply();
    }

    private void Apply()
    {
        // Sun elevation: -1 at midnight, 0 at sunrise/sunset, 1 at noon.
        float elevation = Mathf.Sin((TimeOfDay - 0.25f) * Mathf.PI * 2f);
        float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.28f, elevation));
        float edge = 4f * day * (1f - day);   // peaks at dawn and dusk
        Daylight = day;
        bool isDay = day > 0.5f;
        if (isDay && !wasDay && Application.isPlaying && initialised) Dawn?.Invoke();   // never on the first apply
        wasDay = isDay;
        initialised = true;

        if (sun != null)
        {
            Color c = Color.Lerp(nightColor, dayColor, day);
            c = Color.Lerp(c, sunsetColor, edge * 0.7f);
            sun.color = c;
            sun.intensity = Mathf.Lerp(nightIntensity, dayIntensity, day);

            float sunPitch = Mathf.Lerp(8f, 68f, Mathf.Clamp01(elevation));
            float pitch = Mathf.Lerp(moonPitch, sunPitch, day);
            sun.transform.rotation = Quaternion.Euler(pitch, sunYaw, 0f);
        }

        Color ambient = Color.Lerp(nightAmbient, dayAmbient, day);
        Color fog = Color.Lerp(nightFog, dayFog, day);
        fog = Color.Lerp(fog, sunsetColor * 0.8f, edge * 0.35f);
        RenderSettings.ambientLight = ambient * AmbientScale;
        RenderSettings.fogColor = fog;
        if (mainCamera != null && mainCamera.clearFlags == CameraClearFlags.SolidColor)
            mainCamera.backgroundColor = fog;
    }
}
