using UnityEngine;

/// <summary>
/// Swaps the house between its outside look and its inside look.
/// Outside: the cabin exterior (exteriorRoot) is shown and the interior's
/// renderers are hidden so nothing pokes through the cabin walls.
/// Inside: the reverse. Colliders never change; only what is rendered.
/// Listens to HomeZone so it flips on the same frame as the camera nudge.
/// </summary>
public class HouseView : MonoBehaviour
{
    [SerializeField] private GameObject exteriorRoot;
    [SerializeField] private GameObject interiorOnlyRoot;

    private Renderer[] interiorRenderers;

    private void Awake()
    {
        if (interiorOnlyRoot != null)
            interiorRenderers = interiorOnlyRoot.GetComponentsInChildren<Renderer>(true);
    }

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
        if (interiorRenderers != null)
            foreach (Renderer r in interiorRenderers) r.enabled = playerIsHome;
    }
}
