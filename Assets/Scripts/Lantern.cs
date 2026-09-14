using UnityEngine;

/// <summary>
/// The player's light. Lit after dark, out by day: it is how she sees, and how they see her.
/// Range grows when the Firekeeper is home.
/// </summary>
public class Lantern : MonoBehaviour
{
    /// <summary>True while the lantern is burning (night).</summary>
    public static bool IsOn { get; private set; } = true;

    [SerializeField] private Light lantern;

    private float baseRange;

    private void Awake()
    {
        if (lantern == null)
        {
            Transform t = transform.Find("Lantern");
            if (t != null) lantern = t.GetComponent<Light>();
        }
        if (lantern != null) { baseRange = lantern.range; lantern.enabled = true; }
        IsOn = true;
        DayNightCycle.AmbientScale = 1f;
    }

    private float baseIntensity, lit = 1f;

    private void Update()
    {
        if (lantern == null) return;
        if (baseIntensity <= 0f) baseIntensity = lantern.intensity;
        // Lit once the light goes, dark by day. Eases over a couple of seconds either way.
        bool wantLit = DayNightCycle.Instance == null || DayNightCycle.Instance.Daylight < 0.55f;
        lit = Mathf.MoveTowards(lit, wantLit ? 1f : 0f, Time.deltaTime * 0.6f);
        IsOn = lit > 0.5f;
        lantern.enabled = lit > 0.02f;
        lantern.intensity = baseIntensity * lit;
        float found = Inventory.BetterLantern ? 1.5f : 1f;   // the hunter's lantern
        lantern.range = Mathf.Lerp(lantern.range, baseRange * HomeBonuses.LanternRangeMultiplier * found, 1f - Mathf.Exp(-2f * Time.deltaTime));
    }
}
