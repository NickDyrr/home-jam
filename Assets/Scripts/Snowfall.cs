using UnityEngine;

/// <summary>
/// Keeps the snowfall emitter above the player and turns it off while the
/// player is home. Particles simulate in world space, so flakes already in
/// the air stay put as you walk. On stepping outside the system is
/// pre-simulated so the sky is already full of snow.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class Snowfall : MonoBehaviour
{
    [SerializeField] private float heightAbovePlayer = 14f;
    [SerializeField] private float prewarmSeconds = 8f;

    private ParticleSystem ps;
    private Transform player;
    private float rateOutside;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        rateOutside = ps.emission.rateOverTime.constant;
    }

    private void OnEnable()  { HomeZone.PlayerHomeChanged += OnHomeChanged; }
    private void OnDisable() { HomeZone.PlayerHomeChanged -= OnHomeChanged; }

    private void Start()
    {
        Follow();
        bool home = HomeZone.Instance == null || HomeZone.Instance.PlayerIsHome;
        OnHomeChanged(home);
    }

    private void LateUpdate()
    {
        Follow();
    }

    private void Follow()
    {
        if (player == null) return;
        transform.position = player.position + Vector3.up * heightAbovePlayer;
    }

    private void OnHomeChanged(bool isHome)
    {
        if (isHome)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        Follow();
        ps.Clear(true);
        ps.Simulate(prewarmSeconds, true, true);
        ps.Play(true);
    }
}
