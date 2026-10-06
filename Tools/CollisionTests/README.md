# Collision regressions

Run from the repository root:

```powershell
dotnet build Tools/CollisionTests/CollisionTests.csproj
dotnet Tools/CollisionTests/bin/Debug/net8.0/CollisionTests.dll Content/Models/level_one.3dmodel Content/Models/level.3dmodel
```

To also check the model loader with a real DesktopGL graphics device:

```powershell
dotnet Tools/CollisionTests/bin/Debug/net8.0/CollisionTests.dll --graphics Content/Models/level_one.3dmodel Content/Models/level.3dmodel
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
