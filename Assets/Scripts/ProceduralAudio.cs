using UnityEngine;

/// <summary>
/// Small synthesized sound set so the game has audio without any assets:
/// wind, camp fire crackle, snow footsteps, gunshot, a yell, a growl and a
/// heartbeat. Everything is built once at startup from noise and sines.
/// Replace any of these with a real clip later by swapping the AudioClip.
/// </summary>
public static class ProceduralAudio
{
    private const int Rate = 22050;
    private static System.Random rng = new System.Random(12345);

    private static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

    private static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample, bool loop = false)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i, i / (float)Rate), -1f, 1f);
        if (loop)
        {
            // Crossfade the tail into the head so the loop point is silent.
            int fade = Mathf.Min(n / 8, Rate / 4);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[n - fade + i] = data[n - fade + i] * (1f - t) + data[i] * t;
            }
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Slow, breathy wind bed. Loop.</summary>
    public static AudioClip Wind()
    {
        float lp = 0f, lp2 = 0f;
        return Make("Wind", 6f, (i, t) =>
        {
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 0.23f + 1f);
            lp += (Noise() - lp) * 0.035f;
            lp2 += (lp - lp2) * 0.08f;
            return lp2 * 2.2f * gust;
        }, true);
    }

    /// <summary>Camp fire: soft hiss with random pops. Loop.</summary>
    public static AudioClip Crackle()
    {
        float lp = 0f; float pop = 0f;
        return Make("Crackle", 3f, (i, t) =>
        {
            lp += (Noise() - lp) * 0.2f;
            if (rng.NextDouble() < 0.0009) pop = 0.8f + Noise() * 0.2f;
            pop *= 0.94f;
            return lp * 0.18f + pop * Noise();
        }, true);
    }

    /// <summary>Snow crunch. heavy = a big thing stepping.</summary>
    public static AudioClip Footstep(bool heavy)
    {
        float lp = 0f;
        float len = heavy ? 0.22f : 0.13f;
        return Make(heavy ? "StepHeavy" : "Step", len, (i, t) =>
        {
            float env = Mathf.Exp(-t * (heavy ? 22f : 40f));
            lp += (Noise() - lp) * (heavy ? 0.12f : 0.3f);
            float thump = heavy ? Mathf.Sin(t * 2f * Mathf.PI * 70f) * Mathf.Exp(-t * 30f) * 0.6f : 0f;
            return (lp * 0.9f + thump) * env;
        });
    }

    /// <summary>Pistol shot: sharp crack with a low body.</summary>
    public static AudioClip Gunshot()
    {
        float lp = 0f;
        return Make("Gunshot", 0.5f, (i, t) =>
        {
            float crack = Noise() * Mathf.Exp(-t * 60f);
            lp += (Noise() - lp) * 0.08f;
            float body = lp * Mathf.Exp(-t * 9f) * 1.6f;
            float boom = Mathf.Sin(t * 2f * Mathf.PI * 55f) * Mathf.Exp(-t * 14f) * 0.8f;
            return crack + body + boom;
        });
    }

    /// <summary>A scared human yell.</summary>
    public static AudioClip Yell()
    {
        return Make("Yell", 0.9f, (i, t) =>
        {
            float env = Mathf.Min(1f, t * 25f) * Mathf.Exp(-t * 3.2f);
            float f = 330f - t * 90f + Mathf.Sin(t * 2f * Mathf.PI * 6f) * 12f;
            float ph = 2f * Mathf.PI * f * t;
            float voice = (Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.3f * Mathf.Sin(3f * ph) + 0.15f * Mathf.Sin(5f * ph));
            return (voice * 0.35f + Noise() * 0.08f) * env;
        });
    }

    /// <summary>Stalker waking: a low, throaty growl.</summary>
    public static AudioClip Growl()
    {
        float lp = 0f;
        return Make("Growl", 1.1f, (i, t) =>
        {
            float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-t * 2.4f);
            float f = 62f - t * 18f + Mathf.Sin(t * 2f * Mathf.PI * 11f) * 4f;
            float ph = 2f * Mathf.PI * f * t;
            float saw = 2f * (ph / (2f * Mathf.PI) - Mathf.Floor(ph / (2f * Mathf.PI) + 0.5f));
            lp += (saw + Noise() * 0.35f - lp) * 0.15f;
            return lp * 0.8f * env;
        });
    }

    /// <summary>One lub-dub. Played faster as dread rises.</summary>
    public static AudioClip Heartbeat()
    {
        return Make("Heartbeat", 0.5f, (i, t) =>
        {
            float a = Mathf.Sin(t * 2f * Mathf.PI * 52f) * Mathf.Exp(-t * 26f);
            float t2 = t - 0.16f;
            float b = t2 > 0f ? Mathf.Sin(t2 * 2f * Mathf.PI * 46f) * Mathf.Exp(-t2 * 30f) * 0.7f : 0f;
            return (a + b) * 0.9f;
        });
    }
}
