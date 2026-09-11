# DESIGN DOC — All Tools Allowed #2 (Critics Arcade AI Game Jam)

Single source of truth for this jam. If a Claude session is lost, read this file first, then index.html.
Last updated: Thu Sep 10 2026 (day before jam). Theme section is empty until Fri 11 AM.

---

## 0. THEME (fill in Fri Sep 11, 11:00 AM MDT)

THEME: ________________________________

Chosen concept: ________________________________
One-line pitch: ________________________________

---

## 1. Jam facts

- Jam page: https://itch.io/jam/all-tools-allowed-2 (host: Critics Arcade, hashtag #AllToolsAllowed)
- 72-hour jam. Theme drops the moment the clock starts.

| Event              | Nick's time (Mountain, UTC-6) | Official (BST)  |
|--------------------|-------------------------------|-----------------|
| Start + theme      | Fri Sep 11, 11:00 AM          | 6 PM            |
| Submissions close  | Mon Sep 14, 11:00 AM          | 6 PM            |
| Voting             | Sep 14 – Sep 21               |                 |
| Winners            | Sep 23                        |                 |

### Rules
- Any tool allowed: generative AI, coding agents, asset packs, any engine.
- MANDATORY: disclose every tool used on the submission page. Keep the list in section 9 current.
- You are responsible for tool terms and third-party rights to generated content.
- Only assets you have rights to. No trademarked/infringing material (so no Q*bert, no Pudgy Penguins, no brand IP).
- Browser builds encouraged. "Obvious zero-effort, single-prompt dumps may be removed." Iteration and polish matter.
- Solo or teams of any size. We are solo (Nick) + Claude Code.

### Judging: 1–5 on four criteria (community + invited judges)
| Criterion          | What it means for our choices |
|--------------------|-------------------------------|
| Immersion          | Consistent world, audio always present, no dead UI, camera feels good, feedback on every action. |
| Player Experience  | Instant understanding (controls on title screen), 30 sec to fun, fair difficulty curve, restart in 1 click, no bugs. |
| Cohesion & Craft   | One palette, one visual style, one font, everything looks like it belongs together. Small and finished beats big and rough. |
| Theme alignment    | Theme must be in the core mechanic, not just the title. Judges should "get it" without reading the description. |

---

## 2. Schedule reality

Nick is out most of Sunday. Real build time is Friday + Saturday, plus Sunday night and Monday morning for polish/submit.

| Block                     | Goal |
|---------------------------|------|
| Fri 11:00 – 11:45 AM      | Theme drops. Brainstorm + pick concept + write section 0 and section 4 of this doc. HARD STOP at 45 min. |
| Fri 11:45 AM – 3:00 PM    | Core loop playable (move, do the thing, score, lose, restart). Ugly is fine. |
| Fri 3:00 PM – late        | Make the core loop FUN. Tune numbers. Add the second system only if the first feels good. |
| Sat all day               | Content + juice + audio + difficulty curve + title/game-over screens. Feature freeze Sat 9 PM. |
| Sat late                  | First real itch upload (private/draft). Test on phone + a second browser. |
| Sun (limited)             | Bug fixes only. No new features. |
| Mon 8:00 – 10:30 AM       | Final build, screenshots, cover image, page text, tool disclosure, SUBMIT. 30-min buffer before 11 AM. |

Rule: anything not playable by Saturday 9 PM gets cut. Polish beats scope on every judging criterion.

---

## 3. Scope rules (decided before theme, do not renegotiate under pressure)

- ONE core mechanic. ONE loop. Sessions of 2–5 minutes. Score or time-based, "one more try" structure.
- Screens: Title (with controls) -> Play -> Game Over (score + Retry). Nothing else. No settings, no saves, no levels select.
- 3D via Three.js unless the theme screams 2D. Low-poly / flat-shaded primitives + fog + strong palette. No character modeling, no rigged animation. Motion comes from code (bob, squash, spin, tween).
- Audio: synth SFX from the built-in beep() helper plus a simple generated music loop. Audio must exist by Saturday. Silence kills Immersion scores.
- Input: keyboard + mouse on desktop, touch drag on mobile. Both must work; itch judges play on laptops, community votes on phones.
- Enemies/obstacles: max 2–3 types. Variety comes from combination and pacing, not from count.
- No multiplayer, no networking, no leaderboards, no procedural world gen beyond simple random spawns.
- Performance target: 60 fps on a mid laptop with integrated graphics. Cap shadow map at 2048, cap draw calls, reuse geometries/materials, use InstancedMesh if >100 of a thing.

### Cut order if behind (cut from the top)
1. Second enemy type / extra mechanic
2. Music loop (keep SFX)
3. Screen effects (post-processing)
4. Mobile touch polish (keep basic drag)
5. Difficulty curve tuning (ship easier rather than broken)

Never cut: title screen with controls, game over + restart, sound on actions, theme in the core mechanic.

---

## 4. Concept (fill in Friday morning)

Brainstorm 5 one-liners fast, then score each 1–5 on: Theme fit / Fun in 30 sec / Buildable in 2 days / 3D helps it.
Pick the highest total. Ties go to the smallest scope.

| # | Concept one-liner | Theme | Fun | Buildable | 3D | Total |
|---|-------------------|-------|-----|-----------|----|-------|
| 1 |                   |       |     |           |    |       |
| 2 |                   |       |     |           |    |       |
| 3 |                   |       |     |           |    |       |
| 4 |                   |       |     |           |    |       |
| 5 |                   |       |     |           |    |       |

### Chosen concept spec
- Player verb (what you DO every second): 
- Goal (why you keep going): 
- Fail state: 
- Score: 
- How the theme lives in the mechanic: 
- Camera: (chase / top-down / fixed iso / first-person)
- Palette (3–4 hex colors): 
- Vibe words (3): 

### MVP feature list (Friday)
- [ ] 
- [ ] 
- [ ] 

### Stretch (Saturday, only if MVP is fun)
- [ ] 
- [ ] 

---

## 5. Tech stack (decided)

- Three.js r186, vendored in `lib/` (three.module.min.js + three.core.min.js + controls/OrbitControls.js). No CDN, no bundler, no npm in the project.
- Import map in index.html: `three` -> ./lib/three.module.min.js, `three/addons/` -> ./lib/
- One `index.html` with a `<script type="module">`. If it gets past ~1500 lines, split into `src/*.js` modules and add `src` to build.ps1's $include.
- Assets folder (if any) goes in `assets/` and gets added to $include in build.ps1.
- 2D fallback: vanilla canvas at 480x270 logical resolution scaled to window. Claude can regenerate that starter in a minute if needed; templates also exist in `D:\qbert workspace\templets`.

### Starter status (index.html, tested headless Sep 10, zero console errors)
- WebGLRenderer, ACES tone mapping, PCF shadows, fog, dark navy background (#0b0b12), grid floor.
- Hemisphere + directional sun light with shadows.
- Yellow capsule player (#ffd23f), WASD/arrows + touch-drag movement, faces movement direction.
- Chase camera with lerp follow and `S.shake` screen shake.
- `beep(freq, dur, type, vol)` synth SFX helper (Web Audio, no files).
- State machine `S.state`: 'title' | 'play' | 'over'. `start()`, `gameOver()`, `reset()`, `ui()`.
- HUD: #score top-right, #title and #over overlay screens (`.screen[hidden]` fix is in; don't remove it).
- `THREE.Timer` for delta time (Clock is deprecated in r186). dt capped at 0.05.
- `S.ents` array is the placeholder for entities; `update(dt)` has the TODO for spawn/move/collide.

### Known Three.js r186 gotchas
- PCFSoftShadowMap was removed; use PCFShadowMap.
- THREE.Clock is deprecated; use THREE.Timer (call timer.update() each frame).
- The minified module imports "./three.core.min.js" (we patched the path). Don't re-download lib files without re-patching.
- CapsuleGeometry args: (radius, length, capSegments, radialSegments).

---

## 6. Dev workflow

```
# run locally (ES modules will NOT load from file://)
powershell -ExecutionPolicy Bypass -File serve.ps1     # then open http://localhost:8080

# build the itch zip -> build/jam-game.zip
powershell -ExecutionPolicy Bypass -File build.ps1
```

- build.ps1 uses Windows tar.exe so zip entries have forward slashes (Compress-Archive writes backslashes and breaks on itch).
- Claude can verify renders with a headless Playwright screenshot (script lives in the session scratchpad; takes ~10 s). Use it after any risky change.
- Test in Chrome and Firefox and on a phone before the Saturday-night upload.

---

## 7. itch.io upload steps

1. itch.io Dashboard -> Create new project.
2. Title, URL slug. Kind of project: **HTML**.
3. Upload `build/jam-game.zip`. Tick **"This file will be played in the browser."**
4. Embed options: Viewport **960 x 540**, tick **Fullscreen button**, tick **Mobile friendly**. Orientation: landscape.
5. Cover image 630x500. At least 3 screenshots. Short description (1 line) + full description with controls.
6. Add the **Tools used** paragraph from section 9 to the description. Add credits for any third-party assets with license.
7. Visibility: Draft first to test, then Public.
8. Go to the jam page -> **Submit your project** -> pick the project. Publishing alone does NOT enter the jam.
9. Re-upload the zip any time before Mon 11:00 AM MDT; the submission stays linked.

---

## 8. Juice checklist (Saturday)

- [ ] SFX on: move start, action, hit, score, lose, button click
- [ ] Screen shake on hits (S.shake already exists)
- [ ] Squash/stretch or scale-pop on the player when acting
- [ ] Particles (simple Points or small cubes) on hit/collect
- [ ] Color flash on damage
- [ ] Score pop animation in HUD
- [ ] Simple music loop (generated, or a CC0 track with credit)
- [ ] Title screen has the game's palette + 1-line controls + PLAY
- [ ] Game over shows score + best (localStorage) + RETRY
- [ ] Difficulty ramps over ~2 minutes
- [ ] Fog + palette consistent everywhere, one font

---

## 9. Tools used (keep current; paste into itch page)

Draft disclosure text:
> Built solo in 72 hours. Code, design planning, and debugging with Claude Code (Anthropic, Claude Fable 5.1). Engine: Three.js r186 (MIT). Sound effects synthesized in-browser with the Web Audio API. [Add: any image generator, music generator, or asset pack used, with license.]

Running list:
- Claude Code (Claude Fable 5.1) — code, design, planning, testing
- Three.js r186 (MIT)
- 

---

## 10. Friday 11:00 AM kickoff script (do exactly this)

1. Paste the theme into Claude and into section 0.
2. Claude proposes 5 one-liners; Nick adds any of his own. Score in the section 4 table. 10 minutes max.
3. Pick one. Fill in the "Chosen concept spec" and MVP list. 15 minutes max.
4. Claude builds the MVP loop on top of index.html. Nick playtests every 20–30 minutes and calls out what's not fun.
5. Do not touch visuals or audio until the loop is fun or it's 3 PM, whichever comes first.
