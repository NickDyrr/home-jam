using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counts survivors who made it home and turns on one reward per arrival.
/// Rewards are the children of rewardsRoot, activated in order.
/// Each survivor settles next to the reward they unlocked, adds ammo to the
/// pistol's reserve, and brings their job's bonus (see HomeBonuses). Rewards
/// land only here, never on pickup.
///
/// Survivors lost to the dark are gone for good. When everyone still alive is
/// home the game is won; when everyone left outside is lost, the house stops
/// growing. At each survivor threshold in upgradeAtSurvivors the house goes
/// up a level (see HouseView); survivors already home walk to their reward's
/// new spot.
/// </summary>
public class Home : MonoBehaviour
{
    public static Home Instance { get; private set; }

    [SerializeField] private Transform rewardsRoot;
    [SerializeField] private Vector3 fallbackSettleSpot = new Vector3(0f, 0f, 1f);
    [SerializeField] private int ammoPerSurvivor = 6;
    [Tooltip("The fenced yard, in world XZ. A following survivor inside it heads for the front door on its own.")]
    [SerializeField] private Vector2 yardMin = new Vector2(-9.9f, -9.9f);
    [SerializeField] private Vector2 yardMax = new Vector2(9.9f, 7.7f);

    [Header("Upgrades")]
    [Tooltip("Survivor counts at which the house goes up a level. Element 0 unlocks level 1, and so on.")]
    [SerializeField] private int[] upgradeAtSurvivors = { 3 };
    [Tooltip("Upgrade by itself when the count is reached. Off: she has to build it at the workbench.")]
    [SerializeField] private bool autoUpgrade = false;

    /// <summary>Survivors needed for the next house level, or -1 when there is no next level.</summary>
    public int NextUpgradeNeeded
    {
        get
        {
            if (HouseView.Instance == null || !HouseView.Instance.CanUpgrade) return -1;
            int next = HouseView.Instance.CurrentLevel;
            return upgradeAtSurvivors != null && next < upgradeAtSurvivors.Length ? upgradeAtSurvivors[next] : 0;
        }
    }

    /// <summary>Enough people are home to build the next level.</summary>
    public bool CanBuildNext => NextUpgradeNeeded >= 0 && SurvivorsHome >= NextUpgradeNeeded;
    [Tooltip("Debug: press U to upgrade the house without bringing anyone home.")]
    [SerializeField] private bool debugUpgradeKey = true;

    public int SurvivorsHome { get; private set; }
    public int SurvivorsLost { get; private set; }
    public int SurvivorsTotal { get; private set; }
    public bool Won { get; private set; }
    /// <summary>Everyone accounted for, one way or the other.</summary>
    public bool Ended { get; private set; }
    public string EndText { get; private set; } = "";

    /// <summary>Seconds from the end of the intro to the moment everyone was accounted for. The score.</summary>
    public float RunSeconds { get; private set; }
    /// <summary>Best full-rescue time saved on this machine, or -1 if none.</summary>
    public float BestSeconds { get; private set; } = -1f;
    /// <summary>True when this run set the saved best.</summary>
    public bool NewBest { get; private set; }
    private float runStart = -1f;
    private const string BestKey = "HOME.BestSeconds";

    private readonly List<Survivor> arrivals = new List<Survivor>();

    [Header("Upgrades that change play")]
    [Tooltip("With the first house level the porch lantern pushes the stalkers' line this far out past the fence.")]
    [SerializeField] private float porchGrace = 6f;
    [SerializeField] private GameObject porchLantern;
    [SerializeField] private Bell bell;
    [Tooltip("Cook home: the house lights reach this much further.")]
    [SerializeField] private float cookGlowMultiplier = 1.4f;
    [Tooltip("Watchman home: he calls out when a stalker is within this distance of the house while she is in the yard.")]
    [SerializeField] private float watchmanRange = 45f;
    private float nextWatchCall;
    private Light[] houseLights; private float[] houseLightRanges;

    /// <summary>Metres past the fence that still count as hers, once the porch lantern is up.</summary>
    public float YardGrace => HouseView.Instance != null && HouseView.Instance.CurrentLevel >= 1 ? porchGrace : 0f;

    /// <summary>True when the point is inside the fence, by at least margin.</summary>
    public bool InYard(Vector3 p, float margin = 0.6f)
    {
        return p.x > yardMin.x + margin && p.x < yardMax.x - margin && p.z > yardMin.y + margin && p.z < yardMax.y - margin;
    }

    private void Awake()
    {
        Instance = this;
        HomeBonuses.Reset();
        if (rewardsRoot == null) rewardsRoot = transform.Find("Rewards");

        if (rewardsRoot != null)
        {
            foreach (Transform child in rewardsRoot)
                child.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        SurvivorsTotal = Survivor.All.Count;
    }

    private void OnEnable()  { DayNightCycle.Dawn += OnDawn; }
    private void OnDisable() { DayNightCycle.Dawn -= OnDawn; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // The clock starts the moment the intro hands over control.
        if (runStart < 0f && !Intro.Playing) runStart = Time.time;

        // Porch lantern shows with the first upgrade.
        if (porchLantern != null && porchLantern.activeSelf != (YardGrace > 0f)) porchLantern.SetActive(YardGrace > 0f);

        // Cook home: the house glows further. Scale every house light off its base range.
        if (houseLights == null)
        {
            houseLights = GetComponentsInChildren<Light>(true);
            houseLightRanges = new float[houseLights.Length];
            for (int i = 0; i < houseLights.Length; i++) houseLightRanges[i] = houseLights[i].range;
        }
        float glow = HomeBonuses.Has(SurvivorJob.Cook) ? cookGlowMultiplier : 1f;
        for (int i = 0; i < houseLights.Length; i++)
            if (houseLights[i] != null && houseLights[i].transform.parent != null && houseLights[i].transform.parent.name != "Rewards")
                houseLights[i].range = houseLightRanges[i] * glow;

        // Watchman home: a warning from the fence when something is close and she is in the yard.
        if (HomeBonuses.Has(SurvivorJob.Watchman) && Time.time >= nextWatchCall && PlayerMovement.Instance != null && InYard(PlayerMovement.Instance.transform.position, 0f))
        {
            foreach (Stalker s in Stalker.All)
            {
                Vector3 d = s.transform.position - transform.position; d.y = 0f;
                if (d.magnitude <= watchmanRange)
                {
                    nextWatchCall = Time.time + 12f;
                    Vector3 dir = d.normalized;
                    string where = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? (dir.x > 0 ? "east" : "west") : (dir.z > 0 ? "north" : "south");
                    FloatingText.Show(transform.position + Vector3.up * 3.4f, "Watchman: something to the " + where + ".", 4f);
                    break;
                }
            }
        }

        if (!debugUpgradeKey) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.uKey.wasPressedThisFrame) Upgrade();
    }

    /// <summary>mm:ss for the end screen.</summary>
    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    private void OnDawn()
    {
        int rounds = HomeBonuses.AmmoPerDawn;
        if (rounds > 0 && Pistol.Instance != null)
        {
            Pistol.Instance.AddReserve(rounds);
            FloatingText.Show(transform.position + Vector3.up * 3f, $"Dawn. The hunter left {rounds} rounds.", 4f);
        }
    }

    /// <summary>
    /// Registers an arrival and returns the world-space floor point where the
    /// survivor should go stand. Y is ignored by the caller.
    /// </summary>
    public Vector3 SurvivorArrived(Survivor survivor)
    {
        int index = SurvivorsHome;
        SurvivorsHome++;
        arrivals.Add(survivor);

        if (Pistol.Instance != null) Pistol.Instance.AddReserve(ammoPerSurvivor);
        HomeBonuses.Add(survivor.Job);
        FloatingText.Show(survivor.transform.position + Vector3.up * 2.6f, HomeBonuses.Describe(survivor.Job), 5f);

        if (rewardsRoot != null && index < rewardsRoot.childCount)
            rewardsRoot.GetChild(index).gameObject.SetActive(true);

        Debug.Log($"Survivor '{survivor.name}' is home. Total: {SurvivorsHome}");

        // Upgrade first, so this survivor's spot is computed for the new room.
        if (autoUpgrade && HouseView.Instance != null && HouseView.Instance.CanUpgrade)
        {
            int next = HouseView.Instance.CurrentLevel;
            if (upgradeAtSurvivors != null && next < upgradeAtSurvivors.Length && SurvivorsHome >= upgradeAtSurvivors[next])
                Upgrade();
        }

        CheckEnd();
        return SpotFor(index);
    }

    /// <summary>A survivor out there was taken. Their camp is dark now.</summary>
    public void SurvivorLost(Survivor survivor)
    {
        SurvivorsLost++;
        FloatingText.Show(transform.position + Vector3.up * 3f, $"{survivor.name} is gone. {SurvivorsTotal - SurvivorsHome - SurvivorsLost} still out there.", 5f);
        CheckEnd();
    }

    private void CheckEnd()
    {
        if (Won || SurvivorsTotal <= 0) return;
        if (SurvivorsHome + SurvivorsLost < SurvivorsTotal) return;

        Ended = true;
        RunSeconds = runStart >= 0f ? Time.time - runStart : 0f;
        if (SurvivorsLost == 0)
        {
            Won = true;
            EndText = "Everyone is home.";
            // Only a full rescue counts for the record.
            BestSeconds = PlayerPrefs.GetFloat(BestKey, -1f);
            if (BestSeconds < 0f || RunSeconds < BestSeconds)
            {
                NewBest = true;
                BestSeconds = RunSeconds;
                PlayerPrefs.SetFloat(BestKey, RunSeconds);
                PlayerPrefs.Save();
            }
        }
        else
        {
            EndText = $"{SurvivorsHome} made it home. {SurvivorsLost} did not.";
            BestSeconds = PlayerPrefs.GetFloat(BestKey, -1f);
        }
        if (StalkerDirector.Instance != null) StalkerDirector.Instance.Retire();
    }

    /// <summary>A stalker took her. The run ends here, however many were home.</summary>
    public void PlayerTaken()
    {
        if (Ended) return;
        Ended = true;
        Won = false;
        RunSeconds = runStart >= 0f ? Time.time - runStart : 0f;
        BestSeconds = PlayerPrefs.GetFloat(BestKey, -1f);
        int outThere = Mathf.Max(0, SurvivorsTotal - SurvivorsHome - SurvivorsLost);
        EndText = SurvivorsHome > 0
            ? $"They took her. {SurvivorsHome} home, {outThere} still out there."
            : "They took her.";
    }

    /// <summary>Go up one house level and re-seat everyone already home.</summary>
    public void Upgrade()
    {
        if (HouseView.Instance == null || !HouseView.Instance.CanUpgrade) return;
        HouseView.Instance.Upgrade();
        Debug.Log($"Home upgraded to level {HouseView.Instance.CurrentLevel}.");

        for (int i = 0; i < arrivals.Count; i++)
            if (arrivals[i] != null) arrivals[i].Resettle(SpotFor(i));
    }

    private Vector3 SpotFor(int index)
    {
        Vector3 spot = transform.TransformPoint(fallbackSettleSpot);
        if (rewardsRoot != null && index < rewardsRoot.childCount)
        {
            Transform reward = rewardsRoot.GetChild(index);
            // Stand a little toward the room centre from the lamp so it isn't inside it.
            Vector3 toward = (transform.position - reward.position);
            toward.y = 0f;
            spot = reward.position + toward.normalized * 0.8f;
        }
        return spot;
    }
}
