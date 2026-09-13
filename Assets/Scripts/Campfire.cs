using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A survivor's camp fire. Burns while its survivor is still out there. When
/// the survivor is taken the fire goes out for good, and when the player
/// listens for them it flares so it can be picked out in the dark.
/// </summary>
public class Campfire : MonoBehaviour
{
    public static readonly List<Campfire> All = new List<Campfire>();

    [SerializeField] private float flareIntensity = 9f;
    [SerializeField] private float flareSeconds = 1.2f;

    public bool IsOut { get; private set; }

    private Light fire;
    private FlickerLight flicker;
    private float baseIntensity;
    private float flareUntil;

    private void Awake()
    {
        fire = GetComponentInChildren<Light>(true);
        flicker = GetComponentInChildren<FlickerLight>(true);
        if (fire != null) baseIntensity = fire.intensity;
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private AudioSource crackle;

    private void Start()
    {
        var audio = AudioManager.Ensure();
        crackle = audio.Loop(audio.Crackle, transform, 0.12f);
    }

    private void Update()
    {
        if (IsOut || fire == null) return;
        if (Time.time < flareUntil)
        {
            float t = (flareUntil - Time.time) / flareSeconds;
            fire.intensity = Mathf.Lerp(baseIntensity, flareIntensity, t);
        }
    }

    /// <summary>Brief bright pulse, used when the player listens for this camp.</summary>
    public void Flare()
    {
        if (IsOut) return;
        flareUntil = Time.time + flareSeconds;
        if (flicker != null) flicker.enabled = false;
        Invoke(nameof(RestoreFlicker), flareSeconds);
    }

    private void RestoreFlicker()
    {
        if (!IsOut && flicker != null) flicker.enabled = true;
    }

    /// <summary>The survivor is gone. The fire dies and the camp goes dark.</summary>
    public void PutOut()
    {
        if (IsOut) return;
        IsOut = true;
        if (flicker != null) flicker.enabled = false;
        if (fire != null) fire.enabled = false;
        if (crackle != null) crackle.Stop();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.4f, 1f));
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>Nearest burning camp fire to a point, or null.</summary>
    public static Campfire Nearest(Vector3 pos, float maxDistance)
    {
        Campfire best = null; float bestSq = maxDistance * maxDistance;
        foreach (Campfire c in All)
        {
            if (c.IsOut) continue;
            Vector3 d = c.transform.position - pos; d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = c; }
        }
        return best;
    }
}
