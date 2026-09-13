using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A jagged wall of rock around the edge of the map, so the world ends in
/// mountains instead of a drop. Built at start from a fixed seed: a closed
/// loop around a square, extruded outward through a height profile, cut into
/// chunks. Each chunk has a collider (the player cannot leave), and a TreeFade
/// on the tree layer so a chunk that blocks the camera's view of the player
/// goes see-through like a tree would. Because the camera looks from the
/// south-west, chunks on that side of the player also fade when she is near
/// them, otherwise the wall would hide her. Faces are double-sided. Faces
/// toward the map are rock; the crest and the back are snow.
/// </summary>
public class CliffRing : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Half-width of the square the wall starts at (its inner foot).")]
    [SerializeField] private float innerHalf = 200f;
    [Tooltip("Metres between samples along the perimeter.")]
    [SerializeField] private float sampleSpacing = 3f;
    [Tooltip("Samples per chunk (chunk length = samples x spacing).")]
    [SerializeField] private int samplesPerChunk = 8;
    [Tooltip("Cross-section from the inner foot outward: x = metres out, y = height.")]
    [SerializeField] private Vector2[] profile = { new Vector2(0f, 0f), new Vector2(4f, 9f), new Vector2(8f, 20f), new Vector2(14f, 17f), new Vector2(22f, 11f) };
    [Tooltip("Profile rows from this index outward use the snow material.")]
    [SerializeField] private int snowFromRow = 2;
    [SerializeField] private float heightNoise = 3f;
    [SerializeField] private float radialNoise = 1.5f;
    [SerializeField] private int seed = 7;

    [Header("Look")]
    [SerializeField] private Material rock;
    [SerializeField] private Material rockTransparent;
    [SerializeField] private Material snow;
    [SerializeField] private Material snowTransparent;
    [SerializeField] private int layer = 0;
    [Tooltip("Chunks on the camera's side of the player fade when she is within this distance of them.")]
    [SerializeField] private float fadeDistance = 40f;

    private readonly List<TreeFade> fades = new List<TreeFade>();
    private readonly List<Bounds> chunkBounds = new List<Bounds>();
    private Transform player;
    private Camera cam;

    private void Awake()
    {
        if (transform.childCount == 0) Build();
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (player == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
        MarkNear(player.position, fwd);
    }

    /// <summary>Flags chunks that sit between the camera side and the player, close by, to fade this frame.</summary>
    public void MarkNear(Vector3 playerPos, Vector3 camForwardFlat)
    {
        playerPos.y = 0f;
        float sq = fadeDistance * fadeDistance;
        for (int i = 0; i < fades.Count; i++)
        {
            Bounds b = chunkBounds[i];
            Vector3 closest = b.ClosestPoint(playerPos); closest.y = 0f;
            if ((closest - playerPos).sqrMagnitude > sq) continue;
            Vector3 centre = b.center; centre.y = 0f;
            if (Vector3.Dot(centre - playerPos, camForwardFlat) < 0f) fades[i].OccludingThisFrame = true;
        }
    }

    /// <summary>All the fade components on the chunks (for editor previews).</summary>
    public IReadOnlyList<TreeFade> Fades => fades;

    /// <summary>Removes any previously built chunks.</summary>
    public void Clear()
    {
        fades.Clear(); chunkBounds.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
    }

    /// <summary>Builds the ring under this object.</summary>
    public void Build()
    {
        Clear();
        var rng = new System.Random(seed);

        // Closed loop around the square, sampled evenly along the perimeter.
        float side = innerHalf * 2f, perimeter = side * 4f;
        int samples = Mathf.Max(8, Mathf.RoundToInt(perimeter / sampleSpacing));
        var loop = new Vector3[samples];
        for (int i = 0; i < samples; i++)
        {
            float d = perimeter * i / samples;
            int edge = Mathf.FloorToInt(d / side); float along = d - edge * side;
            switch (edge)
            {
                case 0:  loop[i] = new Vector3(-innerHalf + along, 0f, -innerHalf); break;   // south, west to east
                case 1:  loop[i] = new Vector3(innerHalf, 0f, -innerHalf + along); break;    // east, south to north
                case 2:  loop[i] = new Vector3(innerHalf - along, 0f, innerHalf); break;     // north, east to west
                default: loop[i] = new Vector3(-innerHalf, 0f, innerHalf - along); break;    // west, north to south
            }
        }

        // One vertex ring per sample: the profile pushed outward along the radial from the map centre.
        int rows = profile.Length;
        var ring = new Vector3[samples, rows];
        for (int i = 0; i < samples; i++)
        {
            Vector3 radial = loop[i]; radial.y = 0f; radial.Normalize();
            for (int r = 0; r < rows; r++)
            {
                float hN = r == 0 ? 0f : ((float)rng.NextDouble() * 2f - 1f) * heightNoise * (r == rows - 1 ? 0.5f : 1f);
                float rN = r == 0 ? 0f : ((float)rng.NextDouble() * 2f - 1f) * radialNoise;
                ring[i, r] = loop[i] + radial * (profile[r].x + rN) + Vector3.up * Mathf.Max(0f, profile[r].y + hN);
            }
        }

        int chunks = Mathf.CeilToInt(samples / (float)samplesPerChunk);
        for (int c = 0; c < chunks; c++)
        {
            int s0 = c * samplesPerChunk, s1 = Mathf.Min(samples, s0 + samplesPerChunk);
            BuildChunk(c, ring, samples, rows, s0, s1);
        }
    }

    private void BuildChunk(int index, Vector3[,] ring, int samples, int rows, int s0, int s1)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var rockTris = new List<int>();
        var snowTris = new List<int>();

        // Flat-shaded and double-sided: every quad gets its own vertices for each side.
        for (int i = s0; i < s1; i++)
        {
            int j = (i + 1) % samples;
            for (int r = 0; r < rows - 1; r++)
            {
                Vector3 a = ring[i, r], b = ring[j, r], c = ring[j, r + 1], d = ring[i, r + 1];
                var tris = r >= snowFromRow ? snowTris : rockTris;
                for (int sideIdx = 0; sideIdx < 2; sideIdx++)
                {
                    int v = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                    foreach (var p in new[] { a, b, c, d }) uvs.Add(new Vector2(p.x + p.z, p.y) * 0.15f);
                    if (sideIdx == 0) { tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3); }   // faces the map
                    else              { tris.Add(v); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v); tris.Add(v + 3); tris.Add(v + 2); }   // faces out
                }
            }
        }

        var mesh = new UnityEngine.Mesh { name = "Cliff_" + index };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(rockTris, 0);
        mesh.SetTriangles(snowTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Cliff_" + index);
        go.transform.SetParent(transform, false);
        go.layer = layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { rock, snow };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        var fade = go.AddComponent<TreeFade>();
        fade.SetMaterials(new[] { rock, snow }, new[] { rockTransparent, snowTransparent }, 0.1f);   // a wall is big: fade it further than a tree
        fades.Add(fade);
        chunkBounds.Add(mesh.bounds);
    }
}
