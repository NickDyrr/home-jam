using UnityEngine;

/// <summary>
/// A short world-space message that faces the camera, drifts up a little and
/// fades. Used for "who came home and what they give" and the end of the game.
/// </summary>
public class FloatingText : MonoBehaviour
{
    private TextMesh text;
    private Transform cam;
    private float born, life, rise;
    private Color color;

    /// <summary>Spawn a message at a world point. size is the character size; life 0 keeps it forever.</summary>
    public static FloatingText Show(Vector3 pos, string message, float life = 4f, float size = 0.07f, float rise = 0.6f)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = message;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 64;
        tm.characterSize = size;
        tm.color = new Color(1f, 0.93f, 0.8f);
        go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        var ft = go.AddComponent<FloatingText>();
        ft.text = tm; ft.life = life; ft.rise = rise; ft.born = Time.time; ft.color = tm.color;
        if (Camera.main != null) ft.cam = Camera.main.transform;
        return ft;
    }

    private void Update()
    {
        if (cam != null) transform.rotation = cam.rotation;
        if (life <= 0f) return;
        float t = (Time.time - born) / life;
        if (t >= 1f) { Destroy(gameObject); return; }
        transform.position += Vector3.up * (rise * Time.deltaTime / life);
        Color c = color; c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
        text.color = c;
    }
}
