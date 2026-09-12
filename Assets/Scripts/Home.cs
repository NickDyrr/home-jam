using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counts survivors who made it home and turns on one reward per arrival.
/// Rewards are the children of rewardsRoot, activated in order.
/// Each survivor settles next to the reward they unlocked, and adds ammo
/// to the pistol's reserve. Rewards land only here, never on pickup.
///
/// At each survivor threshold in upgradeAtSurvivors the house goes up a
/// level (see HouseView). Survivors already home walk to their reward's new
/// spot so nobody is left standing in the snow where a wall used to be.
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

    private readonly List<Survivor> arrivals = new List<Survivor>();

    private void Awake()
    {
        Instance = this;
        if (rewardsRoot == null) rewardsRoot = transform.Find("Rewards");

        if (rewardsRoot != null)
        {
            foreach (Transform child in rewardsRoot)
                child.gameObject.SetActive(false);
        }
    }

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

        if (rewardsRoot != null && index < rewardsRoot.childCount)
            rewardsRoot.GetChild(index).gameObject.SetActive(true);

        Debug.Log($"Survivor '{survivor.name}' is home. Total: {SurvivorsHome}");

        // Upgrade first, so this survivor's spot is computed for the new room.
        if (HouseView.Instance != null && HouseView.Instance.CanUpgrade)
        {
            int next = HouseView.Instance.CurrentLevel;   // index into thresholds for the next level
            if (upgradeAtSurvivors != null && next < upgradeAtSurvivors.Length && SurvivorsHome >= upgradeAtSurvivors[next])
                Upgrade();
        }

        return SpotFor(index);
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
