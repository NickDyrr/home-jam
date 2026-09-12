using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's light, and the game's central trade. F toggles it. Lit, she
/// can see and is easy to notice; dark, the world closes in but stalkers
/// have to stumble into her. Range grows when the Firekeeper is home.
/// </summary>
public class Lantern : MonoBehaviour
{
    public static bool IsOn { get; private set; } = true;

    [SerializeField] private Light lantern;
    [SerializeField] private float blendSpeed = 4f;
    [Tooltip("Ambient light multiplier while the lantern is off at night.")]
    [SerializeField] private float darkAmbient = 0.35f;

    private float baseRange, baseIntensity;
    private float lit = 1f;

    private void Awake()
    {
        if (lantern == null)
        {
            Transform t = transform.Find("Lantern");
            if (t != null) lantern = t.GetComponent<Light>();
        }
        if (lantern != null) { baseRange = lantern.range; baseIntensity = lantern.intensity; }
        IsOn = true;
    }

    private void OnDisable()
    {
        DayNightCycle.AmbientScale = 1f;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame) IsOn = !IsOn;

        lit = Mathf.Lerp(lit, IsOn ? 1f : 0f, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
        if (lantern != null)
        {
            lantern.enabled = lit > 0.02f;
            lantern.intensity = baseIntensity * lit;
            lantern.range = Mathf.Lerp(lantern.range, baseRange * HomeBonuses.LanternRangeMultiplier, 1f - Mathf.Exp(-2f * Time.deltaTime));
        }

        // Dark only matters at night and outside; the house keeps its own lights.
        bool home = HomeZone.Instance != null && HomeZone.Instance.PlayerIsHome;
        float night = DayNightCycle.Instance != null ? 1f - DayNightCycle.Instance.Daylight : 1f;
        float dark = home ? 0f : (1f - lit) * night;
        DayNightCycle.AmbientScale = Mathf.Lerp(1f, darkAmbient, dark);
    }
}
