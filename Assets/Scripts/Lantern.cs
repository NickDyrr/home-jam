using UnityEngine;

/// <summary>
/// The player's light. Always lit: it is how she sees, and how they see her.
/// Range grows when the Firekeeper is home.
/// </summary>
public class Lantern : MonoBehaviour
{
    /// <summary>Kept for the systems that read it; the lantern no longer switches off.</summary>
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

    private void Update()
    {
        if (lantern != null)
            lantern.range = Mathf.Lerp(lantern.range, baseRange * HomeBonuses.LanternRangeMultiplier, 1f - Mathf.Exp(-2f * Time.deltaTime));
    }
}
