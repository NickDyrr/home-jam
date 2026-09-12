using UnityEngine;

/// <summary>
/// Keeps the snowfall emitter above the player. Particles simulate in world
/// space, so flakes already in the air stay put as you walk. Snow keeps
/// falling while the player is home so it can be seen through the cutaway
/// around the house; flakes over the house footprint are culled so nothing
/// falls into the rooms. The system is pre-simulated at start so the sky is
/// already full of snow.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class Snowfall : MonoBehaviour
{
    [SerializeField] private float heightAbovePlayer = 14f;
    [SerializeField] private float prewarmSeconds = 8f;

    private ParticleSystem ps;
    private Transform player;
    private ParticleSystem.Particle[] buffer;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Start()
    {
        Follow();
        ps.Clear(true);
        ps.Simulate(prewarmSeconds, true, true);
        ps.Play(true);
    }

    private void LateUpdate()
    {
        Follow();
        CullOverHouse();
    }

    private void Follow()
    {
        if (player == null) return;
        transform.position = player.position + Vector3.up * heightAbovePlayer;
    }

    /// <summary>Removes flakes whose ground footprint lies inside the home, at any height.</summary>
    private void CullOverHouse()
    {
        if (HomeZone.Instance == null) return;
        int max = ps.main.maxParticles;
        if (buffer == null || buffer.Length < max) buffer = new ParticleSystem.Particle[max];

        int count = ps.GetParticles(buffer);
        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = buffer[i].position;
            pos.y = 1.5f;
            if (HomeZone.Instance.Contains(pos))
            {
                buffer[i].remainingLifetime = -1f;
                changed = true;
            }
        }
        if (changed) ps.SetParticles(buffer, count);
    }
}
