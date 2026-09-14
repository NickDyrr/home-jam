using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the runtime-only systems (audio, dread, HUD) when a scene starts,
/// so nothing extra has to live in the scene file.
/// </summary>
public static class GameBoot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        // This runs once per launch. A restart reloads the scene, which used to come up with no
        // HUD, no meeting scene, no intro and no trails: hook every load after the first too.
        Setup();
        SceneManager.sceneLoaded += (scene, mode) => Setup();
    }

    private static void Setup()
    {
        if (GameObject.Find("GameSystems") != null) return;   // already set up for this scene

        // The browser gets the plain Forward renderer without ambient occlusion: WebGL 2 has no
        // Forward+ and the extra passes are the first thing to misbehave there.
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            if (QualitySettings.GetQualityLevel() != 0) QualitySettings.SetQualityLevel(0, true);
            // WebGL 2 samples a shadow map that was never made ("mismatch between texture format and
            // sampler type") and then draws nothing lit at all. No light gets shadows in the browser.
            QualitySettings.shadows = ShadowQuality.Disable;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                l.shadows = LightShadows.None;
        }

        // Statics survive a scene reload (restart): put them back.
        HouseView.ForceOutside = false;
        StalkerDirector.Suppressed = false;
        DoorOpener.HoldClosed = false;
        DayNightCycle.AmbientScale = 1f;
        Inventory.Reset();

        // Her hands: the keys that use what she finds.
        var player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<ItemUse>() == null) player.AddComponent<ItemUse>();

        AudioManager.Ensure();
        var systems = new GameObject("GameSystems");
        systems.AddComponent<Dread>();
        systems.AddComponent<GameHUD>();
        systems.AddComponent<Intro>();
        systems.AddComponent<Encounter>();
        systems.AddComponent<Trails>();
    }
}
