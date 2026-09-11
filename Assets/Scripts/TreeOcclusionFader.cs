using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the player visible through the forest. Each frame, sphere-casts
/// from the camera to the player and tells every TreeFade it passes through
/// to fade. Trees not hit fade back in. Also fades trees very close in front
/// of the player so the trunk she is walking past never covers her.
/// </summary>
public class TreeOcclusionFader : MonoBehaviour
{
    [SerializeField] private LayerMask treeMask;
    [SerializeField] private float castRadius = 1.4f;
    [SerializeField] private float stopShort = 1.0f;

    private Transform player;
    private readonly List<TreeFade> all = new List<TreeFade>();
    private readonly RaycastHit[] hits = new RaycastHit[64];

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        all.AddRange(FindObjectsByType<TreeFade>(FindObjectsSortMode.None));
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 from = transform.position;
        Vector3 dir = target - from;
        float dist = dir.magnitude - stopShort;
        if (dist > 0.01f)
        {
            dir /= dir.magnitude;
            int n = Physics.SphereCastNonAlloc(from, castRadius, dir, hits, dist, treeMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                TreeFade tf = hits[i].collider.GetComponentInParent<TreeFade>();
                if (tf != null) tf.OccludingThisFrame = true;
            }
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null) all[i].Tick(dt);
    }
}
