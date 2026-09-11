using UnityEngine;

/// <summary>
/// Swaps the house between its outside look and its inside look.
/// Outside: full walls and a roof (exteriorRoot). Inside: the cutaway
/// greybox you can see into. Collision never changes; only renderers do.
/// Listens to HomeZone so it flips on the same frame as the camera nudge.
/// </summary>
public class HouseView : MonoBehaviour
{
    [SerializeField] private GameObject exteriorRoot;
    [SerializeField] private GameObject interiorOnlyRoot;   // optional: things only shown inside (e.g. furniture props)

    private void OnEnable()
    {
        HomeZone.PlayerHomeChanged += Apply;
    }

    private void OnDisable()
    {
        HomeZone.PlayerHomeChanged -= Apply;
    }

    private void Start()
    {
        bool home = HomeZone.Instance == null || HomeZone.Instance.PlayerIsHome;
        Apply(home);
    }

    private void Apply(bool playerIsHome)
    {
        if (exteriorRoot != null) exteriorRoot.SetActive(!playerIsHome);
        if (interiorOnlyRoot != null) interiorOnlyRoot.SetActive(playerIsHome);
    }
}
