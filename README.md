# HOME

*The snow came early. Something came down with it. Find their fires. Bring them home.*

A short top-down survival game made in 72 hours for [All Tools Allowed #2](https://itch.io/jam/all-tools-allowed-2) (theme: HOME). It's my first published game on itch.io, and my first in Unity.

**Play it in the browser on itch.io:** https://slicknickstudio.itch.io/bringthemhome

## The game

It's her house. They come as far as the fence and no further. They don't like the light. The others are still out in the trees; they move by day and keep their fires at night. Walk out past the fence after dark, follow the tracks in the snow, find a fire, and bring whoever is at it home. The house grows with the people in it. Bring everyone back and the game tells you your time.

- Light scares them off. Sound draws them in. A burning fire is ground they won't cross.
- Bullets don't kill them. A hit slows one down and sends it running, but it comes back, and it learns.
- Losses are permanent. They go for the person you're escorting first. If one gets you, the run is over.
- Scavenge the woods: six weapons with their own ammo, a medkit, tin cans, bear traps, torn maps, scrap for the workbench.

## Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Shift | Sprint (loud) |
| Mouse / Left click | Aim / Shoot |
| R | Reload |
| 1 to 6 | Pistol, rifle, shotgun, flare gun, auto rifle, sniper, once found |
| G / V / M | Throw a can / set a bear trap / read a map scrap |
| E | Read the notebook, build at the workbench |
| T | Wait for dark (at home, by day) |
| B | Ring the bell (second house level, from the yard) |
| Esc | Pause and controls |

## How it was made

Built in a loop of playing and describing what felt wrong in plain words, with Claude Code editing the Unity project directly through its MCP integration: scripts, scene placement, props, builds. Roughly forty rounds of that over three days. The design document that grew alongside it is `home jam design.md`.

**Tools:** Unity 6 (URP) · Claude Code (Claude Fable 5.1) for most of the C#, level scripts, HUD and builds · Tripo for the 3D models · Mixamo for rigging and animation · ElevenLabs for the intro voice-over · Unity asset generation (GPT Image 1.5) for the inventory icons · a footstep recording from Epidemic Sound; other sounds synthesized in-engine.

Design, story, voice direction, art direction, playtesting and every cut were mine.

## Building it

Open with Unity 6000.5.4f1. The scene is `Assets/Scenes/Greybox.unity`. Windows and WebGL builds go to `Builds/` (ignored by git). The WebGL build uses the custom template in `Assets/WebGLTemplates/HOME` and Gzip with decompression fallback; zip it with forward-slash entry names for itch.
