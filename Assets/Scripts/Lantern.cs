using UnityEngine;

/// <summary>
/// The player's light. Its range grows when the Firekeeper is home.
/// </summary>
public class Lantern : MonoBehaviour
{
    [SerializeField] private Light lantern;
    [SerializeField] private float blendSpeed = 2f;

    private float baseRange;

    private void Awake()
    {
        if (lantern == null)
        {
            Transform t = transform.Find("Lantern");
            if (t != null) lantern = t.GetComponent<Light>();
        }
        if (lantern != null) baseRange = lantern.range;
    }

    private void Update()
    {
        if (lantern == null) return;
        float want = baseRange * HomeBonuses.LanternRangeMultiplier;
        lantern.range = Mathf.Lerp(lantern.range, want, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
    }
}
