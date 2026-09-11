using UnityEngine;

/// <summary>
/// Gentle organic flicker for fire and lantern lights. Two layered noise
/// bands so it never looks like a strobe.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [SerializeField] private float amount = 0.25f;
    [SerializeField] private float speed = 6f;
    [SerializeField] private float rangeAmount = 0.08f;

    private Light l;
    private float baseIntensity, baseRange, seed;

    private void Awake()
    {
        l = GetComponent<Light>();
        baseIntensity = l.intensity;
        baseRange = l.range;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float t = Time.time * speed + seed;
        float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 3.1f, seed + 7f) * 0.3f;   // 0..1
        float k = 1f + (n - 0.5f) * 2f * amount;
        l.intensity = baseIntensity * k;
        l.range = baseRange * (1f + (n - 0.5f) * 2f * rangeAmount);
    }
}
