using System;
using UnityEngine;

/// <summary>
/// Axis-aligned volume covering the house interior. Answers "is this point home?"
/// and tracks whether the player is inside. Everything else asks this.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class HomeZone : MonoBehaviour
{
    public static HomeZone Instance { get; private set; }

    /// <summary>Fires when the player crosses the threshold. True = now inside.</summary>
    public static event Action<bool> PlayerHomeChanged;

    public bool PlayerIsHome { get; private set; } = true;

    private BoxCollider box;
    private Transform player;

    private void Awake()
    {
        Instance = this;
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Contains(Vector3 worldPos)
    {
        return box.bounds.Contains(worldPos);
    }

    private void Update()
    {
        if (player == null) return;

        bool inside = Contains(player.position);
        if (inside == PlayerIsHome) return;

        PlayerIsHome = inside;
        PlayerHomeChanged?.Invoke(inside);
    }
}
