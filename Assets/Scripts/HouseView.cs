using UnityEngine;

/// <summary>
/// Owns which build of the house is showing, and swaps it between its outside
/// look and its inside look.
///
/// Each level is one look for the house: an exterior shown while the player is
/// outside, an interior whose renderers show only while inside (colliders on it
/// never change), an optional upstairs that shows only once the player has
/// climbed above upstairsShowHeight, and a set of level-only objects
/// (colliders, door, lights, home-zone boxes) that are active only while that
/// level is current. Reward lights move to the level's spots so they land
/// inside whichever room is current. Listens to HomeZone so the inside/outside
/// flip happens on the same frame as the camera nudge.
/// </summary>
public class HouseView : MonoBehaviour
{
    [System.Serializable]
    public class Level
    {
        public string name = "Level";
        public GameObject exteriorRoot;
        public GameObject interiorRoot;
        [Tooltip("Optional upper floor. Shown only while the player is home and above upstairsShowHeight.")]
        public GameObject upstairsRoot;
        public float upstairsShowHeight = 1.2f;
        [Tooltip("Active only while this level is current: colliders, door, lights, zone boxes.")]
        public GameObject[] levelOnly;
        [Tooltip("House-local positions for the reward lights, in order. Leave empty to keep them where they are.")]
        public Vector3[] rewardSpots;
    }

    public static HouseView Instance { get; private set; }

    [SerializeField] private Level[] levels;
    [SerializeField] private Transform rewardsRoot;

    public int CurrentLevel { get; private set; }
    public int LevelCount => levels != null ? levels.Length : 0;
    public bool CanUpgrade => levels != null && CurrentLevel < levels.Length - 1;

    private bool playerIsHome = true;
    private bool playerUpstairs;
    private Transform player;

    private void Awake()
    {
        Instance = this;
        if (rewardsRoot == null) rewardsRoot = transform.Find("Rewards");
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        HomeZone.PlayerHomeChanged += OnHomeChanged;
    }

    private void OnDisable()
    {
        HomeZone.PlayerHomeChanged -= OnHomeChanged;
    }

    private void Start()
    {
        playerIsHome = HomeZone.Instance == null || HomeZone.Instance.PlayerIsHome;
        SetLevel(CurrentLevel);
    }

    private void Update()
    {
        if (player == null || levels == null || levels.Length == 0) return;
        Level cur = levels[CurrentLevel];
        if (cur.upstairsRoot == null) return;

        bool up = player.position.y >= cur.upstairsShowHeight;
        if (up != playerUpstairs)
        {
            playerUpstairs = up;
            Apply();
        }
    }

    private void OnHomeChanged(bool isHome)
    {
        playerIsHome = isHome;
        Apply();
    }

    /// <summary>Go up one level, if there is one.</summary>
    public void Upgrade()
    {
        if (CanUpgrade) SetLevel(CurrentLevel + 1);
    }

    /// <summary>Switches the house to the given level. Works in the editor too.</summary>
    public void SetLevel(int level)
    {
        if (levels == null || levels.Length == 0) return;
        CurrentLevel = Mathf.Clamp(level, 0, levels.Length - 1);

        for (int i = 0; i < levels.Length; i++)
        {
            bool current = i == CurrentLevel;
            Level l = levels[i];
            if (l.levelOnly != null)
                foreach (GameObject go in l.levelOnly)
                    if (go != null) go.SetActive(current);
            if (!current)
            {
                if (l.exteriorRoot != null) l.exteriorRoot.SetActive(false);
                if (l.interiorRoot != null) l.interiorRoot.SetActive(false);
                if (l.upstairsRoot != null) l.upstairsRoot.SetActive(false);
            }
        }

        Level cur = levels[CurrentLevel];
        if (rewardsRoot != null && cur.rewardSpots != null && cur.rewardSpots.Length > 0)
        {
            for (int i = 0; i < rewardsRoot.childCount && i < cur.rewardSpots.Length; i++)
                rewardsRoot.GetChild(i).localPosition = cur.rewardSpots[i];
        }

        if (HomeZone.Instance != null) HomeZone.Instance.Refresh();
        Apply();
    }

    private void Apply()
    {
        if (levels == null || levels.Length == 0) return;
        Level cur = levels[CurrentLevel];

        if (cur.exteriorRoot != null) cur.exteriorRoot.SetActive(!playerIsHome);
        if (cur.interiorRoot != null)
        {
            // The interior object stays active so its colliders keep working;
            // only what is rendered changes.
            cur.interiorRoot.SetActive(true);
            foreach (Renderer r in cur.interiorRoot.GetComponentsInChildren<Renderer>(true))
                r.enabled = playerIsHome;
        }
        if (cur.upstairsRoot != null)
            cur.upstairsRoot.SetActive(playerIsHome && playerUpstairs);
    }
}
