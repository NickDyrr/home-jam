using System.Collections.Generic;
using UnityEngine;

/// <summary>What a survivor is good for once they are home.</summary>
public enum SurvivorJob { None, Hunter, Soldier, Scout, Cook, Firekeeper, Watchman }

/// <summary>
/// The bonuses granted by everyone who made it home. Systems read the
/// multipliers each frame, so a survivor arriving changes the game at once.
/// Reset by Home at the start of a run.
/// </summary>
public static class HomeBonuses
{
    private static readonly HashSet<SurvivorJob> home = new HashSet<SurvivorJob>();

    public static void Reset() { home.Clear(); }
    public static void Add(SurvivorJob job) { if (job != SurvivorJob.None) home.Add(job); }
    public static bool Has(SurvivorJob job) => home.Contains(job);

    /// <summary>Hunter: rounds added to the reserve every dawn.</summary>
    public static int AmmoPerDawn => Has(SurvivorJob.Hunter) ? 4 : 0;
    /// <summary>Soldier: pistol reload time multiplier.</summary>
    public static float ReloadMultiplier => Has(SurvivorJob.Soldier) ? 0.5f : 1f;
    /// <summary>Scout: how far the listen key reaches.</summary>
    public static float ListenRangeMultiplier => Has(SurvivorJob.Scout) ? 2f : 1f;
    /// <summary>Cook: how fast following survivors move.</summary>
    public static float SurvivorSpeedMultiplier => Has(SurvivorJob.Cook) ? 1.2f : 1f;
    /// <summary>Firekeeper: the player's lantern range.</summary>
    public static float LanternRangeMultiplier => Has(SurvivorJob.Firekeeper) ? 1.5f : 1f;
    /// <summary>Watchman: stalkers give up the chase sooner.</summary>
    public static float StalkerLoseMultiplier => Has(SurvivorJob.Watchman) ? 0.7f : 1f;

    public static string Describe(SurvivorJob job)
    {
        switch (job)
        {
            case SurvivorJob.Hunter:     return "Hunter home: +4 rounds every dawn";
            case SurvivorJob.Soldier:    return "Soldier home: reloads twice as fast";
            case SurvivorJob.Scout:      return "Scout home: you can hear survivors twice as far";
            case SurvivorJob.Cook:       return "Cook home: survivors walk faster";
            case SurvivorJob.Firekeeper: return "Firekeeper home: your lantern reaches further";
            case SurvivorJob.Watchman:   return "Watchman home: stalkers give up sooner";
            default:                     return "Home safe";
        }
    }
}
