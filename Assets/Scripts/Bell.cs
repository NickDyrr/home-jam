using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The bell on its post by the door, unlocked with the second house level. Ring
/// it from the yard (B) and every stalker on the map turns toward the house for
/// a minute, walking to the fence and no further, which clears the far woods for
/// a run. Then a long cooldown. Loud is a choice here, not an accident.
/// </summary>
public class Bell : MonoBehaviour
{
    public static Bell Instance { get; private set; }

    [SerializeField] private int unlockLevel = 2;
    [SerializeField] private float lureSeconds = 60f;
    [SerializeField] private float cooldownSeconds = 180f;
    [SerializeField] private Renderer[] visuals;

    private float readyAt;
    private Transform player;

    public bool Unlocked => HouseView.Instance != null && HouseView.Instance.CurrentLevel >= unlockLevel;
    public bool Ready => Time.time >= readyAt;
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

    /// <summary>True while the player stands in the yard with a working bell: the prompt shows.</summary>
    public bool CanRing => Unlocked && Ready && player != null && Home.Instance != null && Home.Instance.InYard(player.position, 0f) && !Intro.Playing;

    private void Awake()
    {
        Instance = this;
        var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
        if (visuals == null || visuals.Length == 0) visuals = GetComponentsInChildren<Renderer>(true);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        bool show = Unlocked;
        foreach (var r in visuals) if (r != null && r.enabled != show) r.enabled = show;
        if (!CanRing) return;
        var kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame) Ring();
    }

    private void Ring()
    {
        readyAt = Time.time + cooldownSeconds;
        if (AudioManager.Instance != null) AudioManager.Instance.Play(AudioManager.Instance.Bell, transform.position, 0.9f, 1000f, 1f);
        Vector3 here = transform.position; here.y = 0f;
        Stalker.Lure(here, lureSeconds);
        FloatingText.Show(transform.position + Vector3.up * 2.6f, "The bell carries. They come.", 3.5f);
    }
}
