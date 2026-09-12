using UnityEngine;

/// <summary>
/// Creates the runtime-only systems (audio, dread, HUD) when a scene starts,
/// so nothing extra has to live in the scene file.
/// </summary>
public static class GameBoot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        AudioManager.Ensure();
        var systems = new GameObject("GameSystems");
        systems.AddComponent<Dread>();
        systems.AddComponent<GameHUD>();
    }
}
