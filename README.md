# IGI Rebuilt

A Unity recreation of **Trainyard**, the first Project I.G.I. mission, using locally imported game assets.
Infiltrate the compound, avoid or fight its guards, and press **E** beside the truck to secure it.
The environment uses the original terrain geometry, texture regions and object placements.
Trainyard mesh winding preserves the source face order. Single-sheet room walls have inward linings
using their imported texture coordinates and materials; thin props retain two-sided materials and source alpha masks. Trainyard uses original
player collision meshes, with doorway portal faces excluded from solid collision.

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
The original training-compound generator remains available as the fallback for custom configurations.

The VS Code **Run Task** menu includes Open, Verify, Test, Build, Play build and Smoke test.

## Controls

| Control | Action |
|---|---|
| WASD / mouse | Move / look |
| Shift | Sprint |
| Space | Jump |
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
building walls from either side, remote truck capture is rejected, and the truck can be secured nearby. It writes JSON results
and direct camera screenshots in `Logs/Smoke` (scene and weapons; HUD excluded). These checks require the actual Unity editor
and player; compiling C# against Unity's managed assemblies alone does not prove the game runs.

## Next stages

- Tune first-person gun pivots and create hand/reload animations.
- Animate the imported characters and add first-person hands.
- Build authored missions, doors, alarms, stealth feedback, save/load and audio.
- Optimize imported textures and add proper mesh LODs.

Keep source asset licenses and author credits. `Assets/IGIImported` now contains locally extracted
retail IGI data, which retains its original ownership. The prototype gameplay code remains independent
of the sibling engine; the extraction tool references its local decoder binaries.
