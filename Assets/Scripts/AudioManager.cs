using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the synthesized sounds. The camera sits 80 m from the player, so
/// distance is measured from the player rather than the listener: every
/// sound is 2D, attenuated by how far it is from her, with a little pan.
/// Created on demand by GameBoot; nothing needs placing in the scene.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioClip Wind, Crackle, Step, StepHeavy, Gunshot, Yell, Growl, Heartbeat;

    private readonly List<AudioSource> pool = new List<AudioSource>();
    private AudioSource windSource;
    private Transform player;

    public static AudioManager Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        return go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        Instance = this;
        // A real clip in Assets/Resources/Audio/<Name>.wav (or .ogg/.mp3) replaces the synthesized one.
        // Footsteps have no synthesized fallback: silent until a real clip is dropped in.
        Wind = Resources.Load<AudioClip>("Audio/Wind") ?? ProceduralAudio.Wind();
        Crackle = Resources.Load<AudioClip>("Audio/Crackle") ?? ProceduralAudio.Crackle();
        Step = Resources.Load<AudioClip>("Audio/Footstep");
        StepHeavy = Resources.Load<AudioClip>("Audio/FootstepHeavy") ?? Step;
        Gunshot = Resources.Load<AudioClip>("Audio/Gunshot") ?? ProceduralAudio.Gunshot();
        Yell = Resources.Load<AudioClip>("Audio/Yell") ?? ProceduralAudio.Yell();
        Growl = Resources.Load<AudioClip>("Audio/Growl") ?? ProceduralAudio.Growl();
        Heartbeat = Resources.Load<AudioClip>("Audio/Heartbeat") ?? ProceduralAudio.Heartbeat();

        windSource = gameObject.AddComponent<AudioSource>();
        windSource.clip = Wind; windSource.loop = true; windSource.volume = 0.35f; windSource.spatialBlend = 0f; windSource.Play();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Transform Player()
    {
        if (player == null) { var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
        return player;
    }

    /// <summary>Wind bed volume, 0..1. Quieter indoors.</summary>
    public void SetWind(float volume)
    {
        if (windSource != null) windSource.volume = Mathf.Lerp(windSource.volume, volume, 1f - Mathf.Exp(-2f * Time.deltaTime));
    }

    /// <summary>One-shot at a world position, attenuated by distance from the player.</summary>
    public void Play(AudioClip clip, Vector3 pos, float volume = 1f, float maxDistance = 30f, float pitch = 1f)
    {
        if (clip == null) return;
        float gain = volume;
        Transform p = Player();
        float pan = 0f;
        if (p != null)
        {
            Vector3 d = pos - p.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist > maxDistance) return;
            gain *= 1f - Mathf.Pow(dist / maxDistance, 1.5f);
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null) pan = Mathf.Clamp(Vector3.Dot(d, cam.right) / maxDistance * 1.5f, -0.6f, 0.6f);
        }
        AudioSource src = Get();
        src.transform.position = pos;
        src.clip = clip; src.volume = gain; src.pitch = pitch; src.panStereo = pan; src.spatialBlend = 0f; src.loop = false;
        src.Play();
    }

    /// <summary>A looping source attached to an object, volume set each frame by distance.</summary>
    public AudioSource Loop(AudioClip clip, Transform on, float volume)
    {
        var src = on.gameObject.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.spatialBlend = 0f; src.volume = 0f; src.playOnAwake = false;
        src.time = Random.Range(0f, clip.length);
        src.Play();
        var follower = on.gameObject.AddComponent<DistanceVolume>();
        follower.Init(src, volume, 24f);
        return src;
    }

    private AudioSource Get()
    {
        foreach (var s in pool) if (!s.isPlaying) return s;
        var go = new GameObject("OneShot");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        pool.Add(src);
        return src;
    }

}

/// <summary>Sets a looping source's volume from its distance to the player.</summary>
public class DistanceVolume : MonoBehaviour
{
    private AudioSource src; private float volume, maxDistance; private Transform player;
    public void Init(AudioSource s, float v, float max) { src = s; volume = v; maxDistance = max; }
    private void Update()
    {
        if (src == null) return;
        if (player == null) { var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; else return; }
        Vector3 d = transform.position - player.position; d.y = 0f;
        float t = Mathf.Clamp01(d.magnitude / maxDistance);
        src.volume = volume * (1f - t * t);
    }
}
