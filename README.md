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
| 1 / 2 / 3 or mouse wheel | Switch Glock 17 / MP5 SD3 / knife |
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

Trainyard uses the imported Glock 17, MP5 SD3 and knife. Editable definitions are under
`Assets/IGIRebuilt/Data/TrainyardWeapons`. The downloaded starter FBX models and attribution remain under `Assets/Art/Weapons`.
Reload currently lowers the whole gun; authored magazine, bolt and hand animations are a later step.

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

## Next stages

- Tune first-person gun pivots and create hand/reload animations.
- Animate the imported characters and add first-person hands.
- Build authored missions, doors, alarms, stealth feedback, save/load and audio.
- Optimize imported textures and add proper mesh LODs.

Keep source asset licenses and author credits. `Assets/IGIImported` now contains locally extracted
retail IGI data, which retains its original ownership. The prototype gameplay code remains independent
of the sibling engine; the extraction tool references its local decoder binaries.


Trainyard building and terrain repairs: all five barracks now include their native 418_02_1 interior and attached furniture. The environment restores the other authored room/prop attachments, and uses detailed portal-aware collision for these building models. The 17 original sliding doors and six gate panels are placed from `Position start` definitions. Aim at a panel within 3 metres and press **E** to open or close it; gate halves operate together. Moving panels retain collision and pause when a player or guard occupies their swept volume. These controls currently use direct interaction rather than the original scripted switch/lock expressions.

Terrain now includes the source close-range detail passes, fading between 25 and 65 metres, with trilinear filtering and anisotropy 8. `Tools/IGI Rebuilt/Rebuild playable Trainyard` regenerates the Unity scene; no executable build is needed for Play Mode checks. `TrainyardRepairValidation.Run` checks all five barracks, all 23 panels opening/closing, capsule clearance through the open doorways, gate pairing, actor obstruction, and the terrain detail materials in an isolated editor session.


Enemy and security systems: Trainyard now spawns 21 guards; the scripted cutscene copy of Jones (3031) is excluded. Guards confirm targets within a forward vision cone, lose sight behind walls, investigate audible shots/footsteps, search the last known position, alert nearby allies, fire three-round bursts, reload, separate while moving, open doors, and try nearby alarm buttons. Trainyard guards use their original per-guard navigation graph and patrol/look commands, including the original red-beret alarm routes. The prototype compound retains its navigation grid. Actor movement remains constrained by the collision controller.

Original rig-001 IFF motion has been baked for each of the three enemy meshes: idle, walk, run, firing, hit and death. Playback deforms each guard's private mesh instance with interpolated native poses; these are not Unity Humanoid Animator clips. Native AK47, gravel footsteps, hit/death cries and alarm WAVs are bound through `Resources/GuardPresentationSettings`. Gunshot hearing is independent of audio playback: unsuppressed shots reach 100 metres, MP5 SD shots 18 metres, and intervening walls reduce the hearing radius to 55 percent. Crouch/walk/sprint footsteps use 2/7/15 metres.

Five source security-camera placements scan with their authored sweep, cone and range. They require unobstructed sight and sustained detection to raise the alarm; shooting a camera destroys its ability to detect. Three original alarm buttons can be used with E to raise the alarm. Confirmed guard sightings can send a guard to a button, and an active alarm notifies only the twelve red berets. The siren and alarm remain active indefinitely until mission restart; camera computers and alarm buttons do not silence them.

In the first building, computers **501** (approximately x -7.88, z -24.67) and **502** (x -11.54, z -27.11) both disable all cameras. Aim at a computer within 2.8 metres and hold **E for five seconds**. Moving away or releasing E resets hacking progress; pausing suspends it. HUD messages show detection, alarm and camera status. Both computers control surveillance; neither changes an active alarm.

`EnemySecurityValidation.Run` regenerates the scene and runs an isolated Unity Play Mode check of patrol movement, all guard animation/audio bindings, actual player gunfire hearing, hearing and sight obstruction, target memory, camera detection/destruction, alarm buttons, computer hacking/interruption/pause, and navigation through doors versus solid walls. No player executable is required. The recorded run moved 18 guards on patrol and passed the complete check; audio was checked through imported clip bindings in a headless editor, so final loudness and animation appearance should be reviewed in Unity.


Guard locomotion correction: movement uses the native walking clip 57 (about 1.26 metres/second) and normal jogging clip 3 (about 3.40 metres/second). The former assignment used the sprint clip for walking and the slow walk for running. Animation export version 2 stores each clip's measured native travel speed, samples at 30 fps without the old 100-frame cap, and closes loop endpoints. Runtime cadence follows measured horizontal actor speed and visual scale. Presentation runs after controller movement, smooths speed, and uses separate start/stop and walk/run thresholds so slopes and tiny collision adjustments do not flicker between gaits. Guards turn toward a route before advancing. `GuardLocomotionValidation.Run` checks all six gait clips and renders `Logs/guard-locomotion-contact.png`; loop continuity, pose sampling, movement-speed matching, and stationary feet passed.


Guard corrections: weapon position and rotation now come from native bone 22, exported with every body animation frame (animation format version 3). The weapon retains the source grip origin and follows the interpolated hand transform, including walk/run transitions. Guard spawn metadata now includes the correct graph, initial patrol/look commands, and alarm response commands. This preserves indoor sentry facing instead of inventing square patrols. Native graph edge clearance checks the higher support plane at a doorway, so a descending floor-lip sweep no longer seals the barracks exits. Guard step offset is 0.4 metres, and guards wait for sliding doors to open and avoid lateral separation forces in narrow doorways.

Health damage now carries the attack origin. A surviving struck guard immediately faces the attacker, becomes fully suspicious, and notifies nearby allies, including for suppressed gunfire and melee. Alarm notifications remain separate and go only to red berets. `GuardCorrectionsValidation.Run` checks these rules in isolated Play Mode, including all twelve red berets physically leaving the actual barracks, persistent alarms beyond the former timeout, camera-only computer hacks, authored indoor facing, moving weapon sockets, and an actual player shot alerting its target. The complete run passed; `Logs/guard-weapon-contact.png` contains rendered weapon poses for all three enemy models.
