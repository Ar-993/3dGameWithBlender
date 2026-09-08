# Collision regressions

Run from the repository root:

```powershell
dotnet build Tools/CollisionTests/CollisionTests.csproj
dotnet Tools/CollisionTests/bin/Debug/net8.0/CollisionTests.dll Content/Models/level_one.3dmodel Content/Models/level.3dmodel
```

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
`Plane.003` and simulates both spawn points for 600 frames.

The tests call the game's collision code without a graphics device. Reflection
is confined to fixture setup so the level importer can be exercised without
opening the game or changing its public API.
