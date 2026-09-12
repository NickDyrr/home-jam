using System;
using UnityEngine;

/// <summary>
/// Volume covering the house interior. Answers "is this point home?" and
/// tracks whether the player is inside. Everything else asks this.
/// The volume is the union of every active BoxCollider on this object and its
/// children, so an L-shaped room is two boxes, and each house level keeps its
/// own boxes under here and toggles them. Call Refresh after toggling.
/// </summary>
public class HomeZone : MonoBehaviour
{
    public static HomeZone Instance { get; private set; }

    /// <summary>Fires when the player crosses the threshold. True = now inside.</summary>
    public static event Action<bool> PlayerHomeChanged;

    public bool PlayerIsHome { get; private set; } = true;

    private BoxCollider[] boxes;
    private Transform player;

    private void Awake()
    {
        Instance = this;
        Refresh();

        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Re-collects the active boxes. Call after a level change.</summary>
    public void Refresh()
    {
        boxes = GetComponentsInChildren<BoxCollider>(false);
        foreach (BoxCollider b in boxes) b.isTrigger = true;
    }

    public bool Contains(Vector3 worldPos)
    {
        if (boxes == null) Refresh();
        foreach (BoxCollider b in boxes)
            if (b.bounds.Contains(worldPos)) return true;
        return false;
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
