using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the pressure. Tracks how long the player has been outside, spawns the
/// stalker at the edge of the dark after a delay, removes it when the player
/// gets home, and restarts the run if the player is caught.
/// </summary>
public class StalkerDirector : MonoBehaviour
{
    public static StalkerDirector Instance { get; private set; }

    [SerializeField] private GameObject stalkerPrefab;
    [SerializeField] private float spawnDelay = 4f;
    [SerializeField] private float spawnDistance = 22f;
    [SerializeField] private float restartDelay = 1.2f;

    /// <summary>Seconds since the player last left home. Zero while inside.</summary>
    public float TimeOutside { get; private set; }

    private Transform player;
    private GameObject stalker;
    private bool playerOutside;
    private bool restarting;

    private void Awake()
    {
        Instance = this;
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void OnEnable()  { HomeZone.PlayerHomeChanged += OnPlayerHomeChanged; }
    private void OnDisable() { HomeZone.PlayerHomeChanged -= OnPlayerHomeChanged; }

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
            Despawn();
        }
    }

    private void Update()
    {
        if (!playerOutside || restarting) return;

        TimeOutside += Time.deltaTime;

        if (stalker == null && TimeOutside >= spawnDelay)
            Spawn();
    }

    private void Spawn()
    {
        if (stalkerPrefab == null || player == null) return;

        // Somewhere out in the dark, biased away from home so it comes from behind.
        Vector3 awayFromHome = player.position;
        awayFromHome.y = 0f;
        Vector3 dir = awayFromHome.sqrMagnitude > 1f ? awayFromHome.normalized : Random.insideUnitSphere;
        dir.y = 0f;
        dir = (dir + Random.insideUnitSphere * 0.6f);
        dir.y = 0f;
        dir.Normalize();

        Vector3 pos = player.position + dir * spawnDistance;
        pos.y = 1.1f;
        stalker = Instantiate(stalkerPrefab, pos, Quaternion.identity);
    }

    private void Despawn()
    {
        if (stalker != null) Destroy(stalker);
        stalker = null;
    }

    public void PlayerCaught()
    {
        if (restarting) return;
        restarting = true;
        Debug.Log("Player caught. Restarting run.");
        StartCoroutine(RestartAfter(restartDelay));
    }

    private IEnumerator RestartAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
