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

**Escort is a job.** (Scream removed Sep 13.) A stalker within 8 m makes a following survivor run to stay on her heels; the stalker goes for them first and its swing takes half a second to land, so a shot in that window saves them.

**Jobs.** Each survivor brings a home bonus: Hunter (+4 rounds each dawn), Soldier (reload twice as fast), Scout (camp fire light reaches x1.6), Cook (survivors walk faster), Firekeeper (lantern reach x1.5), Watchman (stalkers give up sooner).

**Loss is permanent.** A taken survivor's camp fire goes out and the pool shrinks. Everyone home with nobody lost = win; everyone accounted for otherwise = the house stops growing. Both end with a message over the house and the woods go quiet.

**Upgrades** stay free at the workbench for now.

## Story (Sep 12)

**Intro (in-world flyover, subtitles one line at a time, any key skips; edit in Intro.cs; voice clips Resources/Audio/Intro1..5 + IntroHint):**
The snow came early this year. Something came down with it. / The rest of us ran for the forest. / I made it back here. They come as far as the fence and no further. I don't know why. / Out past the trees, fires are still burning. Every night there are fewer. / I'm going to find them, and I'm going to bring them home.
*Light finds them. Light finds you. Be home before dark.*

Each survivor speaks one line when reached (HomeBonuses.Greeting), and one when they are home (HomeBonuses.Describe). The end message comes from Home.CheckEnd.

## World update (Sep 13)

**Bigger world.** Ground is 420 x 420 (about three times the area). The forest fills it to the edge: 6311 trees. A ring of rock (CliffRing, built at start) closes the map at +-200 m, jagged, 20 m at the crest, with colliders. Chunks that stand between the camera and the player go see-through the way trees do, and so do chunks on the camera's side of her when she is within 40 m, since the camera looks from the south-west.

**Survivors far apart.** Survivor1 stays close (10, -38) as the tutorial. The rest are 120 to 195 m out in different directions: S2 (-150, 60), S3 (120, 140), S4 (175, -70), S5 (-125, -150), S6 (-60, 185). Camps 1.7 m toward home from each.

**Slower feet.** Walk 4, sprint 6.5 (was 5 / 8.5). Sprinting is no longer a free win.

**Stalkers follow the hunt.** The night group is scattered 18 to 48 m around the player (never in the yard, never within 12 m of home). If she leaves them all more than 110 m behind, they melt away and a new group gathers nearer. Danger is everywhere, not just near the house.

## Night loop (Sep 13)

**Night is the game.** The clock is 180 s of day and 420 s of night, and the game starts at dusk. At home by day, T races the clock to dusk.

**They only show after dark.** Waiting survivors are hidden by day and their fires are cold. At dusk the fires catch (light range 24, x1.6 with the Scout home) and the survivor steps out to it. A survivor already following keeps following through dawn.

**Tracks are the clue.** Everyone left tracks when they ran: a wandering trail of old prints, 45 to 70 m long, starting on the home side of each camp and leading to it. Permanent. Cross one, follow it, find the fire.

**Fire glow cue.** A warm glow at the screen edge toward any burning fire within 70 m that is off screen. Short range on purpose.

**Ending.** When everyone is home or lost: end screen with nights survived, R restarts (statics are reset on scene load). Esc quits in the build.

**Tuning.** Stalker group 5 (+1 per night), scattered 18 to 48 m around the player. Walk 4, sprint 6.5.

**Trim (Sep 13, evening).** After the intro the game starts in late afternoon (about 70 s of light) so the first night arrives after a short walk, not on a jump. The top HUD is gone: no counters, no clock. What remains is the ammo counter, the fire glow at the screen edge, the first-night hint, "T wait for dark" when at home by day, the pause screen with the controls, and the end screen. The lantern is always lit; the F toggle is removed (stalkers notice her at 1.2x a survivor's reach).

**Bullets slow, never kill (Sep 13, late).** Each pistol hit staggers a stalker for 1.2 s, knocks it back, and takes 18% off its speed permanently (floor 30%). Six rounds turn a runner into a shambler; nothing ever dies. Said in the intro: "Bullets don't kill them. They only slow them down."

**Stealth and stock (Sep 13, night).** Sprinting is loud: it wakes dormant stalkers within 24 m and they notice her from 1.2x their reach. Walking is quiet: they have to come within half their reach. Fourteen stalkers stand dormant at fixed spots across the map every night (seeded, clear of camps and home) on top of the roaming group, so a slow walk can still meet one. Twelve rounds to start (6 loaded, 6 spare), six more per survivor brought home. The house is built out at the table: E with two home, again with four; otherwise it says how many more are needed. The game starts at sundown. A notebook on the crate by the front door (E to read) holds the rules in her words. Cliff chunks fade only when they actually cover her on screen.

**Fires and the clock (Sep 13, late).** A survivor's fire goes out when they reach the house, not only when they are taken, so the map darkens as the job gets done. The end screen shows the run time from the end of the intro to the last person accounted for, with a saved best for full rescues. Nothing hints at it before then: you only find out it was a race once you have won.

**Chase pressure (Sep 13, late).** A hunting stalker gains 0.45 m/s for every second of the chase, from 4 up to 8.5 (she sprints at 6.5), and the clock resets only when it loses her. Bullets still take 18% off for good. So running buys time, a bullet buys distance, and neither alone is enough for long.

**Being taken ends it (Sep 13, late).** Stalkers go for a survivor she is escorting before her. If one gets hold of her and she cannot shoot it off in the grab window, the run is over: end screen, time shown, no best. The fire glow at the screen edge reaches 40 m so it only hints when a fire is close.

**Deaths that land (Sep 13, late).** A stalker that reaches a survivor winds up a swing for 0.55 s and only kills if they are still within 2.3 m when it lands; a hit on the stalker in that window breaks it off. The survivor plays a flying-back death (Mixamo "Flying Back Death", Assets/Characters/Survivors/Anims) and the body stays where it fell. Panicked survivors scream in place for one second, then run to the player and stick to her heels until she has been beside them half a second. Interior light ranges shortened so nothing shines out of the chimney. In the intro she pushes forward off the seat as she stands.

## Fun pass (Sep 13, night)

**Shorter clock.** 120 s day, 300 s night (was 180/420).

**Scarce rounds.** Three per survivor home (was six). A crate with three rounds sits by each of the three farthest camps; walk over it.

**Traits.** The girl is skittish: a stalker within 10 m sends her back into hiding and you must go back for her. The old man is hurt: 62% speed and cannot run. The soldier is stubborn: stand at his fire three seconds before he follows.

**The way back.** While escorting, the next stalker group gathers between her and the house (within 70 degrees of the home direction).

**Dawn.** Stalkers turn away from the house and walk into the trees for seven seconds before they are gone, with one long low note.

**Placed stalkers.** Two circle each far camp at 18-30 m; a few more scattered; none near home.

**Landmarks.** A frozen pond (60, -118), a ruined shack (-108, -42) that is a refuge with a crate inside, a ridge of fifteen boulders from (-10, 120) to (70, 150), and a dead giant at (104, 38). Two dead-end trails end at a dark patch of snow.

**Upgrades that change play.** Level one lights the porch lantern: the stalkers' line moves 6 m out past the fence. Level two unlocks the bell by the door: B from the yard pulls every stalker on the map toward the house for 60 s (they stop at the fence, then wander off), three minute cooldown. Cook home: the house lights reach 1.4x further. Watchman home: he calls out the direction when a stalker is within 45 m while she is in the yard. Breath loop (Resources/Audio/Breath) rises with a hunter close behind.

**Light and sound (Sep 13, evening).** The rule, stated in the notebook: light scares them away, sound draws them closer. The muzzle flash sends every stalker within 14 m running for 4 s (Stalker.Scare); they stand quiet where they stop and for 5 s only wake if she walks within 4 m. The bang still wakes dormant ones out to 32 m, so the far ones come in while the near ones run: a shot buys space, not safety, and lingering or coming back wakes them again (not told to the player). A hit stalker runs when the stagger ends. A burning camp fire is a small refuge (7 m, Refuge.FireLight): hunters break off and dormant ones ignore anyone standing in its light, which is why the survivors light them. That refuge goes with the fire when the survivor comes home. The skittish girl now only hides from a stalker that is actually hunting within 10 m, and will not step out ("Not with that thing here.") until it is driven off, so the fix is a shot. Story: when it came, the others ran for the trees. Intro voice line 4 could carry it ("The others ran for the trees when it came. They hide by day, and at night they keep their fires lit."): the recorded lines are unchanged until Nick re-records.

**Her death.** When a grab lands she plays the same flying-back death (Die trigger on Player.controller, gun arm layer dropped), the stalker backs off into the dark, and the end screen comes 2.6 s later.

**Landmarks moved onto the trails (Sep 13, evening).** Nobody found them out in the void, so each now sits on the straight line from a far camp back toward home, 24-34 m before the camp and a few metres to the side, with the trees cleared around it: the pond on the way to Survivor4 at (154, -52), the dead giant on the way to Survivor3 at (110, 117), the shack and its crate on the way to Survivor2 at (-127, 42), and the ridge as a wall of rocks across the path to Survivor6 at (-50, 153) with a gap the trail runs through.

**The night deepens (Sep 13, evening).** More of them the longer she is out. The roaming group starts at 5 + nights survived and grows by one every 20 s toward +4 by the end of the night (StalkerDirector.nightGrowth, WantedInGroup). On top of the 14 placed at dusk, ten more stand up at fixed seeded spots spread evenly from dusk to dawn (latePlacedCount), so the map is roughly twice as thick at the end of a night as at the start. DayNightCycle.NightProgress is the clock for both.

**Ammo at landmarks.** Every landmark has a crate now (ten on the map, three rounds each): pond bank, foot of the dead giant, the ridge gap, both shacks, and three at the far camps. New landmarks built from what was already in the project: a hunter's cabin (second shack, refuge and crate) 30 m before Survivor5's camp at (-112, -122); a cold camp at the end of the first dead-end trail at (56, 76), a ring of stones and a burnt log with a crate beside it; a boulder hollow at the other dead end at (-102, -21), rocks in a horseshoe with a crate inside. Wanted from Nick as models: a half-buried pickup truck, a hunting stand or fire tower, a wrecked snowmobile, a tent, and the crash site.

**Bodies, doors, lanterns, flinches (Sep 13, evening).** The flying-back death clip now bakes root height (from feet) and root XZ into the pose, so the body drops to the snow and flies back instead of hanging at hip height; nothing in code ever removes a body. Survivors standing at home have their controller off, so she walks through them, and a home spot within 2.4 m of the current doorway is pushed inside the room. The porch lantern hangs beside whichever door the current house level uses (Home.Update, from DoorOpener.Active). A bullet is a shove now: 0.2 s stagger, then the stalker wheels on the spot and runs (fleeSpeed 9, never below 70 percent) before standing quiet. Reload: Pistol fires a "Reload" trigger if the player controller ever gets one; no clip yet.

**Reload without a clip (Sep 13, evening).** No pistol reload on Mixamo, so Pistol.cs poses it: the aim layer holds the gun up for the whole reload, and the left upper arm and forearm swing down over the first third (60 and 25 degrees about her right axis, in LateUpdate after the animator), hold, and come back by 85 percent. At the bottom of the swing a small grey magazine leaves her left hand (DroppedMag: scripted fall, tumbles, lies flat on the snow, the last forty kept) and a fresh one sits in her hand on the way back up. Ammo boxes breathe gold: emission on their own material instance plus a small gold point light, one breath every two seconds, phases staggered, the light only running within 45 m.
