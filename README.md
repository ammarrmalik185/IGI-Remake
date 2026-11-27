# IGI Rebuilt

A Unity recreation of **Trainyard**, the first Project I.G.I. mission, using locally imported game assets.
Infiltrate the compound, avoid or fight its guards, and press **E** beside the truck to secure it.
The environment uses the original terrain geometry, texture regions and object placements.
Trainyard mesh winding preserves the source face order. Single-sheet room walls have inward linings
using their imported texture coordinates and materials; thin props retain two-sided materials and source alpha masks. Trainyard uses original
player collision meshes, with doorway portal faces excluded from solid collision.
The six original building-attached ladders provide climbing routes to roofs and tower platforms.
The first security building includes its three original interior room attachments. Its detailed
collision meshes preserve the doorway portals, including the exit beside the starting ladder.

The remaining campaign data and art are available for authoring and reference.
Open **Tools > IGI Rebuilt > Imported campaign** to inspect the 14 missions, briefings,
objectives and placed objects, and generate mission reference scenes. Imported weapons are
`WeaponDefinition` assets under `Assets/IGIImported/Weapons` and can be assigned in the asset workbench.
Characters currently use static poses and Unity patrol/combat controllers. Truck interaction completes
the mission; driving and original scripted cutscenes remain future work. Original QVM logic and animations
are preserved for further porting. See
[`Assets/IGIImported/IMPORT-NOTES.txt`](Assets/IGIImported/IMPORT-NOTES.txt) for details and limitations.

Repeat the extraction with `powershell -File tools/Import-IGI.ps1`; this uses the existing local
OpenIGI decoder binaries. Import reports preserve source hashes and identify prototype table
entries whose files are absent from the installation.

## Run

Use **Unity 6000.3.25f1** (already installed on this machine) with an active Unity license.

```powershell
.\tools\Project.ps1 -Action Open
```

Open `Assets/IGIRebuilt/Scenes/Trainyard.unity` and press **Play**, then choose **Begin mission**.
To regenerate the authored environment, use **Tools > IGI Rebuilt > Rebuild playable Trainyard**.
After a fresh asset extraction, run `dotnet run --project tools/TrainyardExport -- . --models`
to export Trainyard's native collision meshes and interior-sector metadata before rebuilding it.
Run `dotnet run --project tools/TrainyardExport -- . --ladders` to regenerate the ladder attachment data.
Run `dotnet run --project tools/TrainyardExport -- . --first-building` to regenerate the first building's
original interior bindings and collision meshes with doorway openings.
The original training-compound generator remains available as the fallback for custom configurations.

The VS Code **Run Task** menu includes Open, Verify, Test, Build, Play build and Smoke test.

## Controls

| Control | Action |
|---|---|
| WASD / mouse | Move / look |
| Shift | Sprint |
| Space | Jump |
| E near a ladder | Grab it at the bottom or top |
| W / S on a ladder | Climb up / down; step off automatically at either end |
| E or Space on a ladder | Let go |
| C or left Ctrl | Toggle crouch |
| Left / right mouse | Fire / aim |
| R | Reload; restart after mission end |
| 1 / 2 / 3 | Quick select Glock 17 / MP5 SD3 / knife |
| Mouse wheel | Cycle the full arsenal (HUD shows the selected slot) |
| Hold Tab | Show the arsenal list; use the wheel to select |
| E | Secure the truck near its driver interaction point |
| Escape | Pause / release cursor |

## Replace assets

1. Put your art under `Assets/Art`. Unity imports FBX, OBJ, PNG and JPG directly; the included official glTFast
   package adds GLB/glTF importing. Other file formats require an importer or conversion supported by Unity.
2. Drag an imported model into a scene, configure its materials, and save it as a prefab.
3. Open **Tools > IGI Rebuilt > Asset workbench**. Select a weapon and assign its **Visual Prefab**.
4. Adjust **View Position**, **View Rotation** and **Model Length**. Those values affect the whole imported model.
   The system does not retain any invisible old weapon parts or require a particular material count.
5. Edit damage, magazine size, fire rate, recoil, reload duration and sound clips on the same weapon definition.

Trainyard equips all 19 original player weapons and equipment items: Glock 17, MP5 SD3,
knife, Colt Anaconda, Desert Eagle, Uzi, dual Uzis, AK47, M16 A2, SPAS 12, Jackhammer,
Minimi, Dragunov, LAW 80 (the source calls its asset RPG18), hand grenades, flashbangs,
proximity mines, binoculars and medipacks. Editable definitions are under
`Assets/IGIRebuilt/Data/TrainyardWeapons`. The downloaded starter FBX models and attribution remain under `Assets/Art/Weapons`.
Use **Tools > IGI Rebuilt > Add original player arsenal** to regenerate the definitions and
update the current configuration without rebuilding the environment. Rebuilding Trainyard also installs this roster.
The mouse wheel wraps through all slots; hold Tab to see the full list. 1/2/3 retain the initial quick selections. Ammunition
stays with each weapon when switching, and switching cancels an unfinished reload.

Shotguns fire eight pellets per shell. Among firearms, right mouse zoom is enabled only for the
MP5 SD3 and Dragunov; Dragunov uses the original scope mask and crosshair and hides the held rifle.
Binoculars also retain their equipment zoom. Other weapons keep the normal world field of view.
Left mouse launches rockets, starts grenade/flashbang wind-up, places mines on nearby ground,
or uses a medipack. Mines arm after 1.5 seconds and trigger on nearby living guards.
Flashbangs stun exposed guards for five seconds and can blind the operator. Medipacks restore
60 health and are retained at full health. Explosions use distance falloff, damage the operator
when nearby, and are blocked by solid walls. Grenades and flashbangs leave the hand at native
release event 3, after the pin/wind-up and release motion (about 1.17 seconds after pressing fire).
Ammunition, throw audio and fuse timing start at release. Switching before release cancels the
throw without consuming a grenade. Throw progress, projectiles and fuses stop while paused.
Automatic weapons use a single round extracted from each retail repeating WAV, faded at the end,
and play it once per fired bullet. Guard AK47 shots use the same single-round sample.
The imported models and sounds are original; damage, pellet spread, blast radii, healing and
flash effects are recreation tuning. APC, helicopter, tank, sentry and mounted M2HB definitions
remain imported reference assets, outside the carried player roster.

`OriginalArsenalValidation.Run` applies the roster and runs an isolated Unity Play Mode check
of the 19 visual/audio bindings, selection, firing, ammunition, reload cancellation, shotgun pellets,
zoom settings, rockets, grenade fuse/pause, blast obstruction and self damage, flash stun,
mine arming/triggering and healing. It writes a weapon contact sheet to `Logs/original-arsenal-contact.png`.
The original arsenal uses complete multipart view prefabs under `Assets/IGIRebuilt/Data/WeaponViews`.
Bodies, magazines, slides and bolts follow bones 3, 52 and 55 of the native `005.iff` view rig,
using each weapon's standing, firing and reload clips. The SPAS reload combines its opening,
shell insertion and closing clips. Native views retain their original grip offsets and scale;
the generic centering/size settings apply to replacement models. Dual Uzis use the original
second-gun attachment. First-person hands remain a later step.

Regenerate the view data with `dotnet run --project tools/TrainyardExport -- . --weapon-views`,
then run **Add original player arsenal**. Weapons render in a separate depth pass with an individually
tuned 66–82 degree horizontal field of view (`View Horizontal Field Of View`), so world camera zoom and nearby walls do not
shrink or slice the gun. The arsenal validation checks complete assemblies, seated magazines and
slides in the ready pose, foreground projection, and moving reload parts. Its reload contact sheet
is `Logs/weapon-reload-contact.png`. The behavior checks also cover permitted zoom, the rendered
Dragunov mask/reticle (`Logs/dragunov-scoped.png`), single-round audio duration, and actual grenade
release timing, pause, recovery and cancellation.

**Enemies:** assign any character prefab to **Enemy Visual Prefab** on
`Assets/IGIRebuilt/Resources/GameConfiguration.asset`. Its size and rotation are configurable; AI and damage stay
on the independent guard controller. Trainyard's spawn markers select the original character model for each guard.
Custom character animation controllers can be added to the visual prefab.

**Environment:** assign an environment prefab on the same configuration. Add `MissionLayout` to that prefab,
assign player, terminal and extraction transforms, and fill its guard spawn array. Static blocking geometry
must use the **World** layer (8), with colliders. Navigation is rebuilt from those colliders at runtime.
Trainyard's navigation grid samples terrain heights and checks obstacles. Multi-floor navigation and
large streaming worlds are outside this first version.

## Project structure

```text
Assets/Art/Weapons/          downloaded starter FBX models, textures and attribution
Assets/IGIImported/          original campaign data, reference scenes, models, textures, story and audio
Assets/IGIRebuilt/Runtime/   player, weapons, AI, navigation, mission, HUD
Assets/IGIRebuilt/Editor/    first-import setup, asset workbench and build/verify commands
Assets/IGIRebuilt/Scenes/    Trainyard.unity and the earlier Boot.unity
Assets/IGIRebuilt/Trainyard/ original terrain conversion and authored environment prefab
Assets/IGIRebuilt/Resources/ GameConfiguration.asset (created on first Unity import)
Assets/IGIRebuilt/Data/      weapon definitions (created on first Unity import)
Assets/IGIRebuilt/Prefabs/   editable complete-weapon prefabs (created on first Unity import)
Assets/IGIRebuilt/Tests/     EditMode gameplay tests
tools/Project.ps1           editor launch, verification, test, build and smoke commands
```

## Verify and build

Close other Unity instances for this project before running batch commands:

```powershell
.\tools\Project.ps1 -Action Verify
.\tools\Project.ps1 -Action Trainyard
.\tools\Project.ps1 -Action Test
.\tools\Project.ps1 -Action Build
.\tools\Project.ps1 -Action PlayBuild
.\tools\Project.ps1 -Action Smoke
```

The Windows player is `Build/Windows/IGIRebuilt.exe`. Logs and test results go to `Logs/`.
The standalone smoke test verifies that frames advance, a shot consumes ammunition, the player has ground
at the spawn, thin surfaces render from both sides, a player capsule cannot cross representative native
building walls from either side, remote truck capture is rejected, and the truck can be secured nearby.
The Trainyard check also climbs all six ladders in both directions, checks their supported landings,
and verifies pause, release, and weapon restrictions while climbing.
It walks a player-sized controller through both first-building doorways in both directions and
captures the main interior and ladder room.
It writes JSON results and direct camera screenshots in `Logs/Smoke` (scene and weapons; HUD excluded). These checks require the actual Unity editor
and player; compiling C# against Unity's managed assemblies alone does not prove the game runs.

Play Mode performance: HUD styles are cached and the live HUD draws only during repaint. Ladder
interaction uses an active registry rather than searching the scene every frame. The scope image
effect is enabled only while scoped, and the foreground camera updates projection settings only
when they change. Invisible guards keep animation time and their weapon sockets current while
skipping mesh deformation/uploads; animation-wide bounds preserve visibility when they return.
Settled corpses stop uploading meshes. The default frame cap is 60 FPS, configurable through
`Target Frame Rate` on `GameConfiguration`; the game disables VSync to honor that cap.

In the editor, each Play session records real Game view FPS, worst frame, collections, resolution
and memory every 30 active seconds after a ten-second warm-up to `Logs/playmode-performance.jsonl`.
The file contains the latest session. `IdlePerformanceValidation.Verify` runs a three-minute
stationary soak with explicit 1280x720 scene rendering, records frame/memory/object statistics,
and checks ladder registration, scope gating, hidden animation/socket behavior and corpse settling.
Its report is `Logs/idle-performance-optimized.json`. Batch-editor rendering excludes live IMGUI
cost and can differ from the visible Game view; a stable soak does not establish that an
editor-specific slowdown has been reproduced or resolved. The recorded soak kept object,
material and mesh counts stable and reported no runtime errors.

Rooftop investigation performance: the live slowdown was reproduced with Jones at approximately
(-28.39, 4.44, -17.12) and alerted guards trying to investigate the roof. In a 30-second rendered
reproduction, failed grid searches consumed 14.9 seconds, with one search taking 1,006 ms and
about 1.7 million clearance checks overall. The grid now builds optimistic connected regions from
its walkability, height and diagonal rules. Different regions prove that a walking route cannot
exist, so a ground-to-roof request returns before A*. Nearest-cell selection considers each cell
once in distance order; searches reuse their scratch buffers, and clearance sweeps reuse a buffer
that grows on overflow. Repeated failures at an unchanged destination back off to at most one
full retry per eight seconds; changed destinations and nearby door opening allow fresh attempts.
The matching reproduction measured 68.5 ms of total grid work, an 8.2 ms worst search, and 6,469
clearance checks. `IdlePerformanceValidation.RoofBaseline` / `RoofVerify` exercise this state.
`GuardBehaviorValidation.Run` also checks disconnected-roof rejection without expansion, a reachable
ground detour, repeated searches, wall clearance and existing guard combat/navigation behavior.

## Next stages

- Add first-person hands to the native weapon poses and tune replacement-model pivots.
- Animate the imported characters and add first-person hands.
- Build authored missions, doors, alarms, stealth feedback, save/load and audio.
- Optimize imported textures and add proper mesh LODs.

Keep source asset licenses and author credits. `Assets/IGIImported` now contains locally extracted
retail IGI data, which retains its original ownership. The prototype gameplay code remains independent
of the sibling engine; the extraction tool references its local decoder binaries.


Trainyard building and terrain repairs: all five barracks now include their native 418_02_1 interior and attached furniture. The environment restores the other authored room/prop attachments, and uses detailed portal-aware collision for these building models. The 17 original sliding doors and six gate panels are placed from `Position start` definitions. Aim at a panel within 3 metres and press **E** to open or close it; gate halves operate together. Moving panels retain collision and pause when a player or guard occupies their swept volume. These controls currently use direct interaction rather than the original scripted switch/lock expressions.

Terrain now includes the source close-range detail passes, fading between 25 and 65 metres, with trilinear filtering and anisotropy 8. `Tools/IGI Rebuilt/Rebuild playable Trainyard` regenerates the Unity scene; no executable build is needed for Play Mode checks. `TrainyardRepairValidation.Run` checks all five barracks, all 23 panels opening/closing, capsule clearance through the open doorways, gate pairing, actor obstruction, and the terrain detail materials in an isolated editor session.


Enemy and security systems: Trainyard now spawns 21 guards; the scripted cutscene copy of Jones (3031) is excluded. Guards confirm targets within a forward vision cone, lose sight behind walls, investigate audible shots/footsteps, search the last known position, alert nearby allies, fire three-round bursts, reload, separate while moving, open doors, and try nearby alarm buttons. Trainyard guards use their original per-guard navigation graph and patrol/look commands, including the original red-beret alarm routes. The prototype compound retains its navigation grid. Actor movement remains constrained by the collision controller.

Original rig-001 IFF motion has been baked for each of the three enemy meshes: idle, walk, run, firing, hit, death, walking fire and running fire. Moving-fire clips layer native firing on upper-body bone 1 over the walking/running legs, including the animated weapon socket. Playback deforms each guard's private mesh instance with interpolated native poses; these are not Unity Humanoid Animator clips. Native AK47, gravel footsteps, hit/death cries and alarm WAVs are bound through `Resources/GuardPresentationSettings`. Gunshot hearing is independent of audio playback: unsuppressed shots reach 100 metres, MP5 SD shots 18 metres, and intervening walls reduce the hearing radius to 55 percent. Crouch/walk/sprint footsteps use 2/7/15 metres.

Five source security-camera placements use their imported cone and range. Moving cameras sweep back and forth around the upright mount, capped at 60 degrees from their starting view on either side, preserving their pitch and endpoint pauses. The two authored stationary cameras retain their fixed views. A camera holds its view while spotting the player and requires four seconds of unobstructed sight to raise the alarm; detection fades over two seconds after sight is lost. The imported warning beep accelerates from a 0.8-second interval toward 0.08 seconds as detection builds. Shooting a camera disables detection and triggers the imported camera explosion sound with a fireball/smoke burst. Three original alarm buttons can be used with E to raise the alarm. Confirmed guard sightings can send a guard to a button, and an active alarm notifies only the twelve red berets. The siren and alarm remain active indefinitely until mission restart; camera computers and alarm buttons do not silence them.

In the first building, computers **501** (approximately x -7.88, z -24.67) and **502** (x -11.54, z -27.11) both disable all cameras. Aim at a computer within 2.8 metres and hold **E for five seconds**. Moving away or releasing E resets hacking progress; pausing suspends it. HUD messages show detection, alarm and camera status. Both computers control surveillance; neither changes an active alarm.

`EnemySecurityValidation.Run` regenerates the scene and runs an isolated Unity Play Mode check of patrol movement, all guard animation/audio bindings, actual player gunfire hearing, hearing and sight obstruction, target memory, camera detection/destruction, alarm buttons, computer hacking/interruption/pause, and navigation through doors versus solid walls. No player executable is required. The recorded run moved 18 guards on patrol and passed the complete check; audio was checked through imported clip bindings in a headless editor, so final loudness and animation appearance should be reviewed in Unity.


Guard locomotion correction: movement uses the native walking clip 57 (about 1.26 metres/second) and normal jogging clip 3 (about 3.40 metres/second). The former assignment used the sprint clip for walking and the slow walk for running. Animation export version 2 stores each clip's measured native travel speed, samples at 30 fps without the old 100-frame cap, and closes loop endpoints. Runtime cadence follows measured horizontal actor speed and visual scale. Presentation runs after controller movement, smooths speed, and uses separate start/stop and walk/run thresholds so slopes and tiny collision adjustments do not flicker between gaits. Guards turn toward a route before advancing. `GuardLocomotionValidation.Run` checks all six gait clips and renders `Logs/guard-locomotion-contact.png`; loop continuity, pose sampling, movement-speed matching, and stationary feet passed.


Guard corrections: weapon position and rotation now come from native bone 22, exported with every body animation frame (animation format version 3). The weapon retains the source grip origin and follows the interpolated hand transform, including walk/run transitions. Guard spawn metadata now includes the correct graph, initial patrol/look commands, and alarm response commands. This preserves indoor sentry facing instead of inventing square patrols. Native graph edge clearance checks the higher support plane at a doorway, so a descending floor-lip sweep no longer seals the barracks exits. Guard step offset is 0.4 metres, and guards wait for sliding doors to open and avoid lateral separation forces in narrow doorways.

Health damage now carries the attack origin. A surviving struck guard immediately faces the attacker, becomes fully suspicious, and notifies nearby allies, including for suppressed gunfire and melee. Alarm notifications remain separate and go only to red berets. `GuardCorrectionsValidation.Run` checks these rules in isolated Play Mode, including all twelve red berets physically leaving the actual barracks, persistent alarms beyond the former timeout, camera-only computer hacks, authored indoor facing, moving weapon sockets, and an actual player shot alerting its target. The complete run passed; `Logs/guard-weapon-contact.png` contains rendered weapon poses for all three enemy models.

Guard navigation falls back to the terrain grid when the imported graph cannot provide a route. Grid anchors and edges require capsule clearance, and weapon-height noise/attack destinations are projected onto their supporting floor. Brief sight interruptions preserve the initial firing deadline so guards can retaliate after a hit. Sight, muzzle clearance and sound occlusion check both directions across single-sided imported walls. `GuardBehaviorValidation.Run` checks wall detours, single-hit retaliation, interrupted sight, moving-fire animation and all five barracks; `GuardLocomotionValidation.RunCombat` renders the combined gait poses.

Alarm runners choose a reachable floor position beside a usable button, abandon blocked attempts, and return to combat when another guard activates the alarm. A moving guard turns toward its route once per frame; aiming no longer cancels that turn. Sight has a three-centimetre clearance margin at wall seams, and spread-adjusted bullet impacts are checked against both sides of walls before applying damage. `GuardEncounterValidation.Run` exercises the actual Trainyard guards with authored routes and alarm buttons intact; `GuardLiveDiagnostics.Capture` records a Play Mode snapshot for debugging reported encounters.

Fence and glass interactions use the native collision material rows (18 for fence, 14 for glass). Guards and security cameras can see through both. Player and enemy bullets pass through fence mesh unless intercepted (20 percent per fence crossing by default); interceptions play the imported `bul_metal_fnc_*` sounds. A shot through breakable glass removes the individual pane's rendering and collision, emits shards, plays one `glass_crush_*` sound, and continues toward the target. The barred barracks windows remain intact. Frames, neighboring panes and solid walls remain intact. Fences still block actor movement.

Run `dotnet run --project tools/TrainyardExport -- . --surfaces` to regenerate this metadata; the model and attachment exporters also refresh it. `SurfaceInteractionSetup.Apply` updates the existing environment and sound settings, while rebuilding Trainyard preserves the setup. `SurfaceInteractionValidation.Run` checks native scene bindings, visibility, fence interception, both shooters, independent pane destruction, imported audio and solid-wall protection. The recorded fence test stopped 35 of 200 shots.

The checkpoint cabin (1110, model `425_01_1`) has three breakable window attachments using `425_02_1` and `425_03_1`. Its shell and adjacent boom barrier use detailed native collision so their coarse human collision does not block sight or bullets across the windows. `CabinGlassValidation.Run` checks guard sight and actual player/enemy shots through all three cabin windows, imported break sounds, and preservation of the cabin frame and barred barracks glass. Rendered checks are in `Logs/checkpoint-cabin-intact.png` and `Logs/checkpoint-cabin-broken.png`.
