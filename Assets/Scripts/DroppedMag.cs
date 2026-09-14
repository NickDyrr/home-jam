using UnityEngine;

/// <summary>
/// An empty magazine let go of mid-reload. Falls under its own little gravity,
/// tumbling, and lies where it lands for the rest of the run. No physics, so
/// it costs nothing and behaves the same in the browser.
/// </summary>
public class DroppedMag : MonoBehaviour
{
    private const float GroundY = 0.012f;
    private const int Keep = 40;
    private static readonly System.Collections.Generic.Queue<DroppedMag> Recent = new System.Collections.Generic.Queue<DroppedMag>();

    private Vector3 velocity;
    private Vector3 spin;
    private bool landed;

    private void Start()
    {
        velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.2f, 0.6f), Random.Range(-0.4f, 0.4f));
        spin = Random.onUnitSphere * Random.Range(240f, 540f);
        Recent.Enqueue(this);
        while (Recent.Count > Keep)
        {
            var old = Recent.Dequeue();
            if (old != null) Destroy(old.gameObject);
        }
    }

    private void Update()
    {
        if (landed) return;
        velocity += Vector3.down * 9.81f * Time.deltaTime;
        Vector3 p = transform.position + velocity * Time.deltaTime;
        transform.rotation = Quaternion.Euler(spin * Time.deltaTime) * transform.rotation;
        if (p.y <= GroundY)
        {
            p.y = GroundY;
            landed = true;
            // Lying flat on the snow, pointing wherever it fell.
            transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        }
        transform.position = p;
    }
}
