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

    [Header("Upgrades")]
    [Tooltip("Survivor counts at which the house goes up a level. Element 0 unlocks level 1, and so on.")]
    [SerializeField] private int[] upgradeAtSurvivors = { 3 };
    [Tooltip("Debug: press U to upgrade the house without bringing anyone home.")]
    [SerializeField] private bool debugUpgradeKey = true;

    public int SurvivorsHome { get; private set; }
    public int SurvivorsLost { get; private set; }
    public int SurvivorsTotal { get; private set; }
    public bool Won { get; private set; }

    private readonly List<Survivor> arrivals = new List<Survivor>();

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

#if ENABLE_INPUT_SYSTEM
    private void Update()
    {
        if (!debugUpgradeKey) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.uKey.wasPressedThisFrame) Upgrade();
    }
#endif

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
        if (HouseView.Instance != null && HouseView.Instance.CanUpgrade)
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

        if (SurvivorsLost == 0)
        {
            Won = true;
            FloatingText.Show(transform.position + Vector3.up * 4f, "Everyone is home.", 0f, 0.16f);
        }
        else
        {
            FloatingText.Show(transform.position + Vector3.up * 4f, $"{SurvivorsHome} made it home. {SurvivorsLost} did not.", 0f, 0.12f);
        }
        if (StalkerDirector.Instance != null) StalkerDirector.Instance.Retire();
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
