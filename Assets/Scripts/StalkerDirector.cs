using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pressure. Tracks how long the player has been outside, spawns the
/// stalker at the edge of the dark after a delay, removes it when the player
/// gets home, and handles the player being caught.
///
/// Being caught does NOT reload the scene. Survivors already home stay home.
/// The player respawns inside, any survivor being escorted is lost, and the
/// stalker goes away until the player steps out again.
/// </summary>
public class StalkerDirector : MonoBehaviour
{
    public static StalkerDirector Instance { get; private set; }

    [SerializeField] private GameObject stalkerPrefab;
    [SerializeField] private float spawnDelay = 4f;
    [SerializeField] private float spawnDistance = 22f;
    [SerializeField] private float respawnDelay = 1.2f;
    [SerializeField] private Vector3 respawnPoint = new Vector3(0f, 1.1f, 0f);

    /// <summary>Seconds since the player last left home. Zero while inside.</summary>
    public float TimeOutside { get; private set; }

    /// <summary>How many times the player has been caught this session.</summary>
    public int TimesCaught { get; private set; }

    private Transform player;
    private GameObject stalker;
    private bool playerOutside;
    private bool respawning;

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
        if (!playerOutside || respawning) return;

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
        if (respawning) return;
        respawning = true;
        TimesCaught++;
        Debug.Log($"Player caught ({TimesCaught}). Respawning at home.");
        StartCoroutine(RespawnAfter(respawnDelay));
    }

    private IEnumerator RespawnAfter(float seconds)
    {
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement != null) movement.enabled = false;

        // Whoever was being escorted is lost. Copy the list: Taken() destroys.
        var escorted = new List<Survivor>();
        foreach (Survivor s in Survivor.All)
            if (s.CurrentState == Survivor.State.Following) escorted.Add(s);
        foreach (Survivor s in escorted) s.Taken();

        yield return new WaitForSeconds(seconds);

        Despawn();
        TimeOutside = 0f;

        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = respawnPoint;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        if (movement != null) movement.enabled = true;
        respawning = false;
    }
}
