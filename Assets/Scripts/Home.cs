using UnityEngine;

/// <summary>
/// Counts survivors who made it home and turns on one reward per arrival.
/// Rewards are the children of rewardsRoot, activated in order.
/// </summary>
public class Home : MonoBehaviour
{
    public static Home Instance { get; private set; }

    [SerializeField] private Transform rewardsRoot;

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

    public void SurvivorArrived(Survivor survivor)
    {
        int index = SurvivorsHome;
        SurvivorsHome++;

        if (rewardsRoot != null && index < rewardsRoot.childCount)
            rewardsRoot.GetChild(index).gameObject.SetActive(true);

        Debug.Log($"Survivor '{survivor.name}' is home. Total: {SurvivorsHome}");
    }
}
