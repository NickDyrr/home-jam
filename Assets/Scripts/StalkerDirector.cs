using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pressure. When the player leaves home, a group of stalkers is
/// scattered through the dark. They stand dormant until something comes near.
/// They all vanish when the player gets home. Tracks time outside for the
/// speed ramp, and handles the player being caught.
///
/// A survivor she is escorting is what they go for first; she is the fallback.
/// If one takes her, the run is over.
/// </summary>
public class StalkerDirector : MonoBehaviour
{
    public static StalkerDirector Instance { get; private set; }

    [Header("Spawning")]
    [SerializeField] private GameObject stalkerPrefab;
    [SerializeField] private int stalkerCount = 4;
    [Tooltip("Quiet spell after she leaves the yard before the first group gathers.")]
    [SerializeField] private float spawnDelay = 12f;
    [Tooltip("Never inside this distance of home: the yard stays theirs to circle, not enter.")]
    [SerializeField] private float minFromHome = 35f;
    [Tooltip("The group is scattered around the player, between these distances.")]
    [SerializeField] private float minFromPlayer = 30f;
    [SerializeField] private float maxFromPlayer = 60f;
    [SerializeField] private float minBetween = 8f;
    [Tooltip("When every stalker is further than this from the player, the group melts away and a new one gathers nearer.")]
    [SerializeField] private float regroupDistance = 110f;

    [Header("As the night goes on")]
    [Tooltip("This many more join the group around her by the end of the night, one at a time.")]
    [SerializeField] private int nightGrowth = 4;
    [Tooltip("Seconds between newcomers when the group is under strength.")]
    [SerializeField] private float reinforceSeconds = 20f;
    [Tooltip("More stand up out there over the night, at fixed spots, spread evenly from dusk to dawn.")]
    [SerializeField] private int latePlacedCount = 10;
    private float nextReinforce;
    private int placedSpawned;

    /// <summary>0 at dusk, 1 at dawn.</summary>
    private static float NightProgress => DayNightCycle.Instance != null ? DayNightCycle.Instance.NightProgress : 0.5f;

    /// <summary>How big the group around her should be right now: base, plus a night survived, plus the hour.</summary>
    private int WantedInGroup => stalkerCount + Nights + Mathf.FloorToInt(NightProgress * (nightGrowth + 0.999f));

    [Header("Placed")]
    [Tooltip("Stalkers standing dormant at fixed spots across the whole map every night, so a careful walk can still meet one.")]
    [SerializeField] private int placedCount = 14;
    [SerializeField] private float placedMinFromHome = 40f;
    [SerializeField] private float placedMaxFromHome = 190f;
    [SerializeField] private float placedMinFromCamp = 22f;
    [SerializeField] private int placedSeed = 5;
    private readonly List<GameObject> placed = new List<GameObject>();
    private Vector3[] placedSpots;


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
        if (Retired || Suppressed) return;

        // The placed ones stand out there all night, whether or not she is home.
        if (IsNight && placed.Count == 0 && !dawnSounded) SpawnPlaced();
        else if (!IsNight && placed.Count > 0) DismissPlaced();
        if (IsNight) { dawnSounded = false; SpawnLatePlaced(); }

        if (!playerOutside || respawning) return;

        TimeOutside += Time.deltaTime;

        if (!IsNight)
        {
            if (stalkers.Count > 0) DismissAll();   // dawn: they turn and walk off into the trees
            return;
        }

        if (stalkers.Count > 0 && AllFarFromPlayer()) DespawnAll();   // left behind: they regroup around her

        if (stalkers.Count == 0 && TimeOutside >= spawnDelay)
            SpawnGroup();
        else if (stalkers.Count > 0 && Time.time >= nextReinforce)
        {
            // The night deepens: one more slips in to join the group, until it is at strength.
            nextReinforce = Time.time + reinforceSeconds;
            stalkers.RemoveAll(s => s == null);
            if (stalkers.Count < WantedInGroup && stalkerPrefab != null && player != null)
            {
                Vector3 home = HomeZone.Instance != null ? HomeZone.Instance.transform.position : Vector3.zero; home.y = 0f;
                if (TryPickSpot(home, out Vector3 pos)) { pos.y = 1.1f; stalkers.Add(Instantiate(stalkerPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f))); }
            }
        }
    }

    /// <summary>The late ones: spot placedCount + k stands up when the night is k/(n+1) gone.</summary>
    private void SpawnLatePlaced()
    {
        if (stalkerPrefab == null || placedSpots == null) return;
        int total = placedSpots.Length;
        while (placedSpawned < total)
        {
            int k = placedSpawned - placedCount + 1;
            if (NightProgress < k / (float)(latePlacedCount + 1)) return;
            Vector3 p = placedSpots[placedSpawned++];
            placed.Add(Instantiate(stalkerPrefab, p + Vector3.up * 1.1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
        }
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

        int count = WantedInGroup;
        nextReinforce = Time.time + reinforceSeconds;
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

        // Walking someone home? Then the group gathers between her and the house: the way back
        // is never the way she came.
        bool escorting = false;
        foreach (Survivor s in Survivor.All) if (s.CurrentState == Survivor.State.Following) { escorting = true; break; }
        Vector3 toHome = home - p; toHome.y = 0f;
        float homeAngle = Mathf.Atan2(toHome.x, toHome.z) * Mathf.Rad2Deg;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 dir;
            if (escorting && toHome.sqrMagnitude > 1f)
            {
                float a = (homeAngle + Random.Range(-70f, 70f)) * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            }
            else dir = Random.insideUnitCircle.normalized;
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

    private bool dawnSounded;

    /// <summary>The sun is up: every roaming stalker walks off on screen, and one long note marks it.</summary>
    private void DismissAll()
    {
        foreach (GameObject s in stalkers) if (s != null) { var st = s.GetComponent<Stalker>(); if (st != null) st.Dismiss(); else Destroy(s); }
        stalkers.Clear();
        SoundDawn();
    }

    private void DismissPlaced()
    {
        foreach (GameObject s in placed) if (s != null) { var st = s.GetComponent<Stalker>(); if (st != null) st.Dismiss(); else Destroy(s); }
        placed.Clear();
        SoundDawn();
    }

    private void SoundDawn()
    {
        if (dawnSounded || AudioManager.Instance == null || player == null) return;
        dawnSounded = true;
        AudioManager.Instance.Play(AudioManager.Instance.Dawn, player.position, 0.7f, 1000f, 1f);
    }

    /// <summary>
    /// Fixed spots, chosen once from a seed. They stand where she wants to go: two circling each
    /// far camp at 18-30 m, plus a few scattered ones. Never near home, never in the empty quarters.
    /// </summary>
    private void PickPlacedSpots()
    {
        var rng = new System.Random(placedSeed);
        var spots = new List<Vector3>();
        Vector3 home = HomeZone.Instance != null ? HomeZone.Instance.transform.position : Vector3.zero; home.y = 0f;
        bool Clear(Vector3 p)
        {
            if (Mathf.Abs(p.x) > 192f || Mathf.Abs(p.z) > 192f) return false;
            Vector3 dh = p - home; dh.y = 0f; if (dh.sqrMagnitude < placedMinFromHome * placedMinFromHome) return false;
            foreach (Campfire c in Campfire.All) { Vector3 d = c.transform.position - p; d.y = 0f; if (d.sqrMagnitude < 14f * 14f) return false; }
            foreach (Vector3 q in spots) if ((q - p).sqrMagnitude < 12f * 12f) return false;
            return true;
        }
        // Two per far camp.
        foreach (Campfire c in Campfire.All)
        {
            Vector3 cp = c.transform.position; cp.y = 0f;
            if ((cp - home).magnitude < 60f) continue;   // the tutorial camp stays quiet
            for (int k = 0, tries = 0; k < 2 && tries < 30; tries++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(18f, 30f, (float)rng.NextDouble());
                Vector3 p = cp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                if (Clear(p)) { spots.Add(p); k++; }
            }
        }
        // A few more out in the open, up to placedCount; then the late ones, packed a little tighter.
        for (int attempt = 0; attempt < 600 && spots.Count < placedCount + latePlacedCount; attempt++)
        {
            float spacing = spots.Count < placedCount ? 35f : 24f;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Lerp(placedMinFromHome, placedMaxFromHome, (float)rng.NextDouble());
            Vector3 p = home + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
            bool ok = Clear(p);
            foreach (Vector3 q in spots) if (ok && (q - p).sqrMagnitude < spacing * spacing) ok = false;
            if (ok) spots.Add(p);
        }
        placedSpots = spots.ToArray();
    }

    /// <summary>Dusk: the first placedCount stand up at once. The rest come with the hours (SpawnLatePlaced).</summary>
    private void SpawnPlaced()
    {
        if (stalkerPrefab == null) return;
        if (placedSpots == null) PickPlacedSpots();
        placedSpawned = 0;
        int first = Mathf.Min(placedCount, placedSpots.Length);
        for (; placedSpawned < first; placedSpawned++)
            placed.Add(Instantiate(stalkerPrefab, placedSpots[placedSpawned] + Vector3.up * 1.1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
    }

    private void DespawnPlaced()
    {
        foreach (GameObject s in placed)
            if (s != null) Destroy(s);
        placed.Clear();
    }

    /// <summary>It got her. That is the end of the run: the house falls quiet and the end screen shows.</summary>
    public void PlayerCaught()
    {
        if (respawning) return;
        respawning = true;
        TimesCaught++;
        Debug.Log("Player caught. Game over.");

        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement != null) movement.enabled = false;
        var pistol = player != null ? player.GetComponent<Pistol>() : null;
        if (pistol != null) pistol.enabled = false;

        Retire();
        StartCoroutine(FallAndEnd());
    }

    [Tooltip("Seconds her death plays before the end screen.")]
    [SerializeField] private float deathSeconds = 2.6f;

    /// <summary>She goes down where she stands, and only then is the run over.</summary>
    private IEnumerator FallAndEnd()
    {
        var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("Run", false);
            animator.SetFloat("Armed", 0f);
            if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);   // the gun arm lets go
            animator.SetTrigger("Die");
            var prints = player.GetComponent<FootprintEmitter>(); if (prints != null) prints.enabled = false;
            yield return new WaitForSeconds(deathSeconds);
        }
        if (Home.Instance != null) Home.Instance.PlayerTaken();
    }
}
