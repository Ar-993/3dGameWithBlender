# Collision regressions

The runner also checks scene lifecycle and deferred transitions without graphics.
It verifies that rendering comes from the public `Ars3D.Rendering` assembly,
which has no dependency on the game, item logic or Assimp. Model-loader and
graphics ownership checks exercise that library through its explicit file paths.
`--graphics` additionally loads, draws and unloads the real gameplay scene twice,
checking GPU resource disposal and that the host's content survives transitions.
See `Help/SceneArchitecture.md` for the scene layout and switching API.

Run from the repository root:

```powershell
dotnet build Tools/CollisionTests/CollisionTests.csproj
dotnet Tools/CollisionTests/bin/Debug/net8.0/CollisionTests.dll Content/Models/level_one.3dmodel Content/Models/level.3dmodel Content/Models/gas_station.3dmodel
```

The standalone item library has its own tests. They reference only
`Ars3D.Items`, use an analytic collision world, and run without MonoGame,
assets, a graphics device, or external test packages:

```powershell
dotnet run --project Libraries/Ars3D.Items.Tests/Ars3D.Items.Tests.csproj
```

These checks cover independent assembly dependencies, 100 instances sharing
one definition with unique IDs, falling and sleeping rigid bodies, pickup,
the hidden slot, held poses, release, button edges, and input suppression.

To also check the model loader with a real DesktopGL graphics device:

```powershell
dotnet Tools/CollisionTests/bin/Debug/net8.0/CollisionTests.dll --graphics Content/Models/level_one.3dmodel Content/Models/level.3dmodel Content/Models/gas_station.3dmodel
```

The optional graphics checks use a small window outside the display and exit
automatically. They exercise file and byte loading, static and animated draw
calls, owned versus borrowed resources, partial mesh construction failure, and
cleanup after an invalid PNG. Tests inspect DesktopGL's weak resource registry;
that fixture may need updating if MonoGame changes its internal registry.

The console runner exits with a nonzero code on failure. It checks thin and
zero-thickness floors, fast vertical falls, mesh-local floor heights, slopes,
openings, reversed winding, ceilings, foot-radius overlap, and sustained ground
contact, stair risers, step-height limits, and clearance under ceilings.
Capsule-specific tests check rounded head clearance, analytic slope/ceiling
and edge/vertex contacts, fast wall casts, wall sliding, corners, penetration
recovery, jumping, steep slopes, and the sphere limit. Edge-height expectations
use the lower hemisphere rather than the previous controller's flat foot.
For each model argument it verifies that every mesh has collision geometry and
that arbitrary, duplicate, empty, and formerly special node names leave all
colliders unchanged. For `level_one.3dmodel` it also tests the actual imported
`Platform.003` and simulates both spawn points for 600 frames.

Hierarchy checks cover parent-child transform composition, case-insensitive
marker and subtree lookup, missing roots, and preservation of cached bind
transforms while evaluating a working pose.

Animation checks cover TRS interpolation, bind-pose fallbacks, clip wrapping
and clamping, invalid pose buffers, interrupted transitions, playback segments,
and upper-body overlay fading. Sampler and animator checks use CPU-only fixtures.

The default CPU tests read `ModelData`, construct `ModelHierarchy`, and call
`LevelGeometryBuilder` directly without a graphics device. Reflection is only
used to populate the private collider lists of `Level` fixtures and, in the
optional graphics checks, inspect the device resource registry. Spawn checks
look up markers through `ModelHierarchy`. Check `Level.TryGetMarkerPosition`
integration by launching the game.

First-person checks cover the eye inside the invisible player capsule, mouse
look, pitch limits, horizontal WASD movement, diagonal speed, and Space press
handling across jumps and pauses. CPU input checks exercise the same
`GameplayInputState` used by the game: button edges, held keys across pause,
focus recovery, mouse capture clicks, slot selection and mouse displacement.
`GameplayInput` polls devices and applies cursor state; `GameplayInputFrame`
contains the actions consumed by the player and world.
Projectile CPU checks cover firing range and timing, movement, a single hit and
expiry. `ProjectileSystem`, `Bullet` and `ShootingCube` need no graphics device;
`ProjectileRenderer` owns their model loading and drawing resources.
The graphics checks also verify that the packaged Old Standard TT fonts include
Cyrillic characters and fit the menu buttons and inventory hint.
Item checks cover gravity, settling, fast
throws, slopes, nearby pickup, wall occlusion, the single hidden slot, E press
handling, and release along a pitched camera view. Model fitting checks cover
static and skinned bind transforms. The gas station fixture verifies that Santa
falls from `ItemSpawn` and rests on the imported ground.

Rigid item checks cover rotation around the center of mass, tumbling drops,
thin walls/floors, contact torque/friction, resting poses, and removal of floor
support. Rigid interaction checks cover pickup from rotated bounds, held-state
freezing, the visible carried pose at release, and wall-safe spinning throws.

The graphics checks also render the real Santa asset and Russian interaction
HUD during the rotating fall, on its side, and in pickup, held, and thrown states.
They load the JSON item catalog, verify 100 Santa instances share one model
and definition, and confirm another item type loads its model only on demand.
Preview PNGs are written to
`artifacts/first-person/` relative to the repository working directory.

The detailed Russian integration and configuration guide is
[ItemSystemStepByStep.md](../../Help/ItemSystemStepByStep.md).
