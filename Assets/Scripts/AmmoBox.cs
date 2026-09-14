using UnityEngine;

/// <summary>
/// A few rounds left at a camp. Walk over it to take them. Rounds are scarce
/// now, so these are worth the detour.
/// </summary>
public class AmmoBox : MonoBehaviour
{
    [SerializeField] private int rounds = 3;
    [SerializeField] private float pickupRadius = 1.4f;

    private Transform player;
    private bool taken;

    private void Awake()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (taken || player == null || Intro.Playing) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > pickupRadius * pickupRadius) return;
        taken = true;
        if (Pistol.Instance != null) Pistol.Instance.AddReserve(rounds);
        FloatingText.Show(transform.position + Vector3.up * 1.6f, $"+{rounds} rounds", 2.5f);
        Destroy(gameObject);
    }
}
