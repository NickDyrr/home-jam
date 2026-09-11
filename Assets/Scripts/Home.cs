using UnityEngine;

/// <summary>
/// Counts survivors who made it home and turns on one reward per arrival.
/// Rewards are the children of rewardsRoot, activated in order.
/// Each survivor settles next to the reward they unlocked, and adds ammo
/// to the pistol's reserve. Rewards land only here, never on pickup.
/// </summary>
public class Home : MonoBehaviour
{
    public static Home Instance { get; private set; }

    [SerializeField] private Transform rewardsRoot;
    [SerializeField] private Vector3 fallbackSettleSpot = new Vector3(0f, 0f, 1f);
    [SerializeField] private int ammoPerSurvivor = 6;

    public int SurvivorsHome { get; private set; }

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

    /// <summary>
    /// Registers an arrival and returns the world-space floor point where the
    /// survivor should go stand. Y is ignored by the caller.
    /// </summary>
    public Vector3 SurvivorArrived(Survivor survivor)
    {
        int index = SurvivorsHome;
        SurvivorsHome++;

        if (Pistol.Instance != null) Pistol.Instance.AddReserve(ammoPerSurvivor);

        Vector3 spot = transform.TransformPoint(fallbackSettleSpot);

        if (rewardsRoot != null && index < rewardsRoot.childCount)
        {
            Transform reward = rewardsRoot.GetChild(index);
            reward.gameObject.SetActive(true);
            // Stand a little toward the room centre from the lamp so it isn't inside it.
            Vector3 toward = (transform.position - reward.position);
            toward.y = 0f;
            spot = reward.position + toward.normalized * 0.8f;
        }

        Debug.Log($"Survivor '{survivor.name}' is home. Total: {SurvivorsHome}");
        return spot;
    }
}
