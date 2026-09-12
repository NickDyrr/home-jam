using UnityEngine;

/// <summary>
/// The little that needs saying on screen: who is home, lost and still out
/// there, the time of day with a warning as dusk comes, the lantern state and
/// the two keys that are not obvious. Plain OnGUI, like the ammo counter.
/// </summary>
public class GameHUD : MonoBehaviour
{
    private GUIStyle label, small, warn;
    private Texture2D white;

    private void Build()
    {
        label = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        label.normal.textColor = Color.white;
        small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        small.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
        warn = new GUIStyle(label); warn.normal.textColor = new Color(1f, 0.6f, 0.35f);
        white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
    }

    private void OnGUI()
    {
        if (label == null) Build();
        float x = 20f, y = 16f;

        // Survivors
        if (Home.Instance != null)
        {
            int home = Home.Instance.SurvivorsHome, lost = Home.Instance.SurvivorsLost;
            int outThere = Mathf.Max(0, Home.Instance.SurvivorsTotal - home - lost);
            GUI.Label(new Rect(x, y, 500, 30), $"Home {home}    Out there {outThere}    Lost {lost}", label);
            y += 30f;
        }

        // Clock: a bar that fills through the day, with dusk warning
        var dn = DayNightCycle.Instance;
        if (dn != null)
        {
            float t = dn.TimeOfDay;
            float w = 220f, h = 8f;
            GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(x, y + 8f, w, h), white);
            // Day window is roughly 0.25..0.75; paint it warm, the rest cold.
            GUI.color = new Color(0.25f, 0.35f, 0.6f, 0.9f); GUI.DrawTexture(new Rect(x, y + 8f, w * 0.25f, h), white); GUI.DrawTexture(new Rect(x + w * 0.75f, y + 8f, w * 0.25f, h), white);
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.9f); GUI.DrawTexture(new Rect(x + w * 0.25f, y + 8f, w * 0.5f, h), white);
            GUI.color = Color.white; GUI.DrawTexture(new Rect(x + w * t - 2f, y + 4f, 4f, h + 8f), white);
            y += 26f;
            string when;
            if (t >= 0.25f && t < 0.75f)
            {
                float secondsToDusk = (0.75f - t) * dn.CycleSeconds;
                when = secondsToDusk < 90f ? $"Dusk in {Mathf.CeilToInt(secondsToDusk)} s. Get home." : $"Day. Dusk in {Mathf.FloorToInt(secondsToDusk / 60f)}:{Mathf.FloorToInt(secondsToDusk % 60f):00}";
                GUI.Label(new Rect(x, y, 400, 24), when, secondsToDusk < 90f ? warn : small);
            }
            else
            {
                float toDawn = ((t < 0.25f ? 0.25f - t : 1.25f - t)) * dn.CycleSeconds;
                GUI.Label(new Rect(x, y, 400, 24), $"Night. Dawn in {Mathf.FloorToInt(toDawn / 60f)}:{Mathf.FloorToInt(toDawn % 60f):00}", warn);
            }
            y += 24f;
        }

        // Keys
        GUI.Label(new Rect(x, y, 400, 22), (Lantern.IsOn ? "Lantern on" : "Lantern off") + "  (F)      Listen  (hold Q)", small);
    }
}
