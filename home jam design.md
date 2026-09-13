# HOME — Game Jam Design Doc

**Theme:** HOME
**Engine:** Unity 6, URP (Universal 3D template)
**Scope:** Game jam. Everything below is ordered so the game is shippable at every checkpoint.

---

## One-line pitch

You are alone in a house in the dark. Go out, find survivors, bring them home. Each one you bring back makes the home bigger, brighter, and better armed — but only once they're through the door.

---

## Core loop

1. **Prepare** — stand at the door, choose what supplies to take. Ammo is scarce.
2. **Go out** — dark, empty, cold. Your light is nearly the only light.
3. **Find** — locate a survivor somewhere out there.
4. **Escort** — walk them home. Slower, more exposed. This is the tense half.
5. **Arrive** — survivor enters. Home gains a light, a prop, a sound. Ammo and gear unlock.
6. **Repeat** — with slightly more resources and slightly more danger.

The walk out is exploration. The walk back is tension. Two different feelings per run.

---

## Design rules (the things that make it work)

**The enemy IS the pressure — no separate meter.**
No abstract distance or time bar. The threat scales with how long you stay out. The player should read danger off the screen, not off a UI element. One system instead of two.

**Light is the central tension.**
Your light lets you find survivors. Your light attracts the enemy. Safety and progress draw on the same resource. Every step out is a real decision.

**Survivors must cost something.**
Finding a survivor cannot be pure upside, or the optimal play is "grab everything" and there are no decisions. Escorting slows you down and/or the survivor is the thing that needs protecting.

**Rewards land only at home.**
Ammo, guns, and upgrades from a survivor unlock when they're *through the door* — never on pickup. Every escort is unrealized value you're carrying. This is the emotional core of the loop; protect it.

**Guns buy distance, they don't win.**
Shooting must be expensive and defensive. It should buy ~3 seconds of escape, never clear a room. If a magazine can wipe a group, the dread is gone and this is just a shooter with a house in it. Running remains the correct answer most of the time. An empty gun should be frightening.

**Start with one gun.**
Pistol. Six rounds. Slow reload. If that loop is tense, a second gun takes twenty minutes. If it isn't, a second gun won't save it.

---

## Build order

Build in this order. Every stopping point is a valid stopping point.

1. **Walk out, walk back, home exists.** Greybox. ~20 minutes.
2. **One survivor you can find and escort home.** This is the whole game.
3. **Enemy that makes the return trip scary.**
4. **One gun.** Tune *after* the enemy is already threatening — build the gun first and you'll make it feel powerful, then you can't make the enemy dangerous again without it feeling unfair.
5. **Survivors visibly change the home** — light, prop, sound.
6. **Difficulty scales across runs.**
7. **New rooms unlock at survivor thresholds.** *Only if 1–6 are done and fun.*

**Ship line:** through step 5 is a jam entry. Stopping at step 3 is a tech demo.

**First cut if short on time:** new rooms (step 7). Most expensive thing on the list — new geometry, navigation, camera framing — for the least visible payoff. Lights and props do the same "home is growing" job for a fraction of the cost and read better in a clip.

**Scope caps:** three survivors, three visible changes to the home. Not eight. Ship three that land.

---

## Art direction

**Style:** low-poly stylized toy diorama. Matte materials, clean silhouettes, simple readable shapes, minimal unnecessary detail.

**Camera:** fixed isometric, cutaway walls on the near side. **No player-controlled rotation.** This is the single biggest scope saver — no camera controller to tune, no occlusion problems, no wall in front of the character. Set the angle once and never touch it.

**Camera nudge:** lerp the camera slightly further out when the player is outside. Smaller player in more empty space reads as exposed. One float, big payoff.

**Palette:**
- Architecture stays near-grey / desaturated. Structural elements are neutral.
- Props carry saturated accents — the color contrast is what makes it look rich rather than muddy.
- Cold desaturated blues outside, warm amber inside.
- **Let the warm area grow with every survivor.** One color decision carrying the entire theme.

**Cozy vs. dread is arrangement, not assets.**
Cozy = density. Rugs, mugs, stacked books, blanket over a chair, stuff overlapping stuff.
Dread = space with nothing in it. Outside should feel *underpopulated*, not menacing.
Same asset pack, opposite placement. Dread from absence is free; horror from assets you don't have time to make is not.

**Density is the expensive part.** Hundreds of prop placements is where the hours actually go — not modeling. Pick one or two hero clusters (fireplace corner, rug-and-cushions circle), let everything else stay sparse. Density only where the camera lingers.

**Don't show the enemy clearly.** Silhouette, distance, a shape that doesn't resolve at this camera angle. The moment the player gets a good look it stops being frightening and becomes a monster they're evaluating. Also saves you from modeling something genuinely scary.

---

## Lighting & post

**Realtime, not baked.** Survivors turn lights on during play, so baking is off the table anyway.

**Many small point lights**, floor and waist height, tight falloff, against a near-black exterior. URP Forward+ in Unity 6 handles a pile of these fine. The lighting *is* the art direction — simple geometry reads beautifully under it.

**Shadows off on almost every light.** Shadow-casting lights are the expensive ones and at this scale nobody can tell. Keep shadows on one or two hero lights (the fireplace); everything else is shadowless fill. This is what lets you run twenty-plus lights.

**URP post volume** — roughly ten minutes of setup, closes about half the gap between greybox and reference:
- Bloom on lantern/fire sources
- Slight vignette
- Color grading: warm-shifted highlights, cool-shifted shadows

**Outside should be flatter and darker than any reference image you're working from.** The player's own light should be nearly the only thing out there.

---

## Audio

Audio is doing more work here than the visuals, and it's the part jam entries skip.

**Inside:** low hum, fire crackle, muffled everything, maybe a clock.
**Outside:** mostly silence, with your own footsteps too loud in it. Hearing yourself clearly means nothing else is covering for you — that's the whole trick.

**The threshold moment is the most important audio cue in the game.** Crossing the door, duck the outside ambience hard and bring the interior loop up over ~0.5s. Reverse on the way out. This single transition is what makes the home *feel* like a home rather than a place with better lighting — it makes the door the most emotional object in the game.

---

## Technical setup

- Unity Hub → **Universal 3D** template (not the Sample variant)
- Connect project to Unity Cloud under the **nickdyrr** org — an org mismatch will look like a licensing failure when it isn't
- Install the **AI Assistant package** via Package Manager (packages don't carry between projects)
- `git init` and commit a clean baseline **before generating anything** — Unity's undo stack doesn't reliably catch agent edits made through MCP, so Ctrl+Z is not a safety net

---

## Cut list (in order of what dies first)

1. Room unlocks
2. Second gun
3. Difficulty scaling
4. Survivors 2 and 3

Everything above the cut line: walk out, escort home, be afraid, watch the house get warmer.
---

## Loop update (Sep 12)

**Night is the clock.** Stalkers only walk at night (10-minute day, DayNightCycle). Leave at first light, get home before dark.

**Finding people.** No listen key (removed Sep 13: it made survivors too easy to find and the run too short). You search the forest for their camp fires; the longer you are out, the more nights you spend out there.

**Escort is a job.** A hunting stalker within 8 m makes the survivor panic: they freeze and yell (wakes dormant stalkers within 18 m). They only move again once the player stands within 2.2 m for half a second. Five-second cooldown before they can panic again.

**Jobs.** Each survivor brings a home bonus: Hunter (+4 rounds each dawn), Soldier (reload twice as fast), Scout (camp fire light reaches x1.6), Cook (survivors walk faster), Firekeeper (lantern reach x1.5), Watchman (stalkers give up sooner).

**Loss is permanent.** A taken survivor's camp fire goes out and the pool shrinks. Everyone home with nobody lost = win; everyone accounted for otherwise = the house stops growing. Both end with a message over the house and the woods go quiet.

**Upgrades** stay free at the workbench for now.

## Story (Sep 12)

**Intro (in-world flyover, subtitles one line at a time, any key skips; edit in Intro.cs; voice clips Resources/Audio/Intro1..5 + IntroHint):**
The snow came early this year. Something came down with it. / The rest of us ran for the forest. / I made it back here. They come as far as the fence and no further. I don't know why. / Out past the trees, fires are still burning. Every night there are fewer. / I'm going to find them, and I'm going to bring them home.
*Light finds them. Light finds you. Be home before dark.*

Each survivor speaks one line when reached (HomeBonuses.Greeting), and one when they are home (HomeBonuses.Describe). The end message comes from Home.CheckEnd.

## World update (Sep 14)

**Bigger world.** Ground is 420 x 420 (about three times the area). The forest fills it to the edge: 6311 trees. A ring of rock (CliffRing, built at start) closes the map at +-200 m, jagged, 20 m at the crest, with colliders. Chunks that stand between the camera and the player go see-through the way trees do, and so do chunks on the camera's side of her when she is within 40 m, since the camera looks from the south-west.

**Survivors far apart.** Survivor1 stays close (10, -38) as the tutorial. The rest are 120 to 195 m out in different directions: S2 (-150, 60), S3 (120, 140), S4 (175, -70), S5 (-125, -150), S6 (-60, 185). Camps 1.7 m toward home from each.

**Slower feet.** Walk 4, sprint 6.5 (was 5 / 8.5). Sprinting is no longer a free win.

**Stalkers follow the hunt.** The night group is scattered 18 to 48 m around the player (never in the yard, never within 12 m of home). If she leaves them all more than 110 m behind, they melt away and a new group gathers nearer. Danger is everywhere, not just near the house.

## Night loop (Sep 14)

**Night is the game.** The clock is 180 s of day and 420 s of night, and the game starts at dusk. At home by day, T races the clock to dusk.

**They only show after dark.** Waiting survivors are hidden by day and their fires are cold. At dusk the fires catch (light range 24, x1.6 with the Scout home) and the survivor steps out to it. A survivor already following keeps following through dawn.

**Tracks are the clue.** Everyone left tracks when they ran: a wandering trail of old prints, 45 to 70 m long, starting on the home side of each camp and leading to it. Permanent. Cross one, follow it, find the fire.

**Fire glow cue.** A warm glow at the screen edge toward any burning fire within 70 m that is off screen. Short range on purpose.

**Ending.** When everyone is home or lost: end screen with nights survived, R restarts (statics are reset on scene load). Esc quits in the build.

**Tuning.** Stalker group 5 (+1 per night), scattered 18 to 48 m around the player. Walk 4, sprint 6.5.
