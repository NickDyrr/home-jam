using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pressure. When the player leaves home, a group of stalkers is
/// scattered through the dark. They stand dormant until something comes near.
/// They all vanish when the player gets home. Tracks time outside for the
/// speed ramp, and handles the player being caught.
///
/// Being caught does NOT reload the scene. Survivors already home stay home.
/// The player respawns inside, any survivor being escorted is lost.
/// </summary>
public class StalkerDirector : MonoBehaviour
{
    public static StalkerDirector Instance { get; private set; }

    [Header("Spawning")]
    [SerializeField] private GameObject stalkerPrefab;
    [SerializeField] private int stalkerCount = 4;
    [SerializeField] private float spawnDelay = 0.5f;
    [Tooltip("Never inside this distance of home: the yard stays theirs to circle, not enter.")]
    [SerializeField] private float minFromHome = 12f;
    [Tooltip("The group is scattered around the player, between these distances.")]
    [SerializeField] private float minFromPlayer = 18f;
    [SerializeField] private float maxFromPlayer = 48f;
    [SerializeField] private float minBetween = 8f;
    [Tooltip("When every stalker is further than this from the player, the group melts away and a new one gathers nearer.")]
    [SerializeField] private float regroupDistance = 110f;

    [Header("Caught")]
    [SerializeField] private float respawnDelay = 1.2f;
    [SerializeField] private Vector3 respawnPoint = new Vector3(0f, 1.1f, 0f);

    /// <summary>Seconds since the player last left home. Zero while inside.</summary>
    public float TimeOutside { get; private set; }

    /// <summary>How many times the player has been caught this session.</summary>
    public int TimesCaught { get; private set; }

    private Transform player;
    private readonly List<GameObject> stalkers = new List<GameObject>();
    private bool playerOutside;
    private bool respawning;

    private void Awake()
    {
        Instance = this;
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    /// <summary>Dawns survived. Each one adds a stalker to the woods.</summary>
    public int Nights { get; private set; }

    private void OnEnable()  { HomeZone.PlayerHomeChanged += OnPlayerHomeChanged; DayNightCycle.Dawn += OnDawn; }
    private void OnDisable() { HomeZone.PlayerHomeChanged -= OnPlayerHomeChanged; DayNightCycle.Dawn -= OnDawn; }

    private void OnDawn() { Nights++; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnPlayerHomeChanged(bool isHome)
    {
        playerOutside = !isHome;
        if (isHome)
        {
            TimeOutside = 0f;
            DespawnAll();
        }
    }

    /// <summary>True once everyone is home; nothing spawns any more.</summary>
    public bool Retired { get; private set; }

    /// <summary>Set by the intro while it owns the scene: no spawning.</summary>
    public static bool Suppressed;

    public GameObject StalkerPrefab => stalkerPrefab;

    /// <summary>Stalkers only walk at night. By day the woods are empty.</summary>
    public static bool IsNight => DayNightCycle.Instance == null || DayNightCycle.Instance.Daylight < 0.3f;

    /// <summary>The game is won: clear the woods for good.</summary>
    public void Retire()
    {
        Retired = true;
        DespawnAll();
    }

    private void Update()
    {
        if (!playerOutside || respawning || Retired || Suppressed) return;

        TimeOutside += Time.deltaTime;

        if (!IsNight)
        {
            if (stalkers.Count > 0) DespawnAll();   // dawn: they slip away
            return;
        }

        if (stalkers.Count > 0 && AllFarFromPlayer()) DespawnAll();   // left behind: they regroup around her

        if (stalkers.Count == 0 && TimeOutside >= spawnDelay)
            SpawnGroup();
    }

    private bool AllFarFromPlayer()
    {
        float sq = regroupDistance * regroupDistance;
        Vector3 p = player.position;
        foreach (GameObject s in stalkers)
        {
            if (s == null) continue;
            Vector3 d = s.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < sq) return false;
        }
        return true;
    }

    private void SpawnGroup()
    {
        if (stalkerPrefab == null || player == null) return;

        Vector3 home = HomeZone.Instance != null ? HomeZone.Instance.transform.position : Vector3.zero;
        home.y = 0f;

        int count = stalkerCount + Nights;
        for (int i = 0; i < count; i++)
        {
            if (!TryPickSpot(home, out Vector3 pos)) continue;
            pos.y = 1.1f;
            stalkers.Add(Instantiate(stalkerPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
        }
    }

    private bool TryPickSpot(Vector3 home, out Vector3 pos)
    {
        Vector3 p = player.position; p.y = 0f;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(minFromPlayer, maxFromPlayer);
            pos = p + new Vector3(dir.x, 0f, dir.y) * r;

            if ((pos - home).sqrMagnitude < minFromHome * minFromHome) continue;
            if (Home.Instance != null && Home.Instance.InYard(pos, -2f)) continue;

            bool tooClose = false;
            foreach (GameObject s in stalkers)
            {
                Vector3 sp = s.transform.position; sp.y = 0f;
                if ((pos - sp).sqrMagnitude < minBetween * minBetween) { tooClose = true; break; }
            }
            if (tooClose) continue;

            return true;
        }

        pos = Vector3.zero;
        return false;
    }

    private void DespawnAll()
    {
        foreach (GameObject s in stalkers)
            if (s != null) Destroy(s);
        stalkers.Clear();
    }

    public void PlayerCaught()
    {
        if (respawning) return;
        respawning = true;
        TimesCaught++;
        Debug.Log($"Player caught ({TimesCaught}). Respawning at home.");
        StartCoroutine(RespawnAfter(respawnDelay));
    }

    private IEnumerator RespawnAfter(float seconds)
    {
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement != null) movement.enabled = false;

        // Whoever was being escorted is lost. Copy the list: Taken() destroys.
        var escorted = new List<Survivor>();
        foreach (Survivor s in Survivor.All)
            if (s.CurrentState == Survivor.State.Following) escorted.Add(s);
        foreach (Survivor s in escorted) s.Taken();

        yield return new WaitForSeconds(seconds);

        DespawnAll();
        TimeOutside = 0f;

        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = respawnPoint;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        if (movement != null) movement.enabled = true;
        respawning = false;
    }
}
