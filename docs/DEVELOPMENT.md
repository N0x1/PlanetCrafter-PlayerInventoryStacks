# Development and verification

## Build

Build on Windows with the .NET 9 SDK, .NET Framework 4.7.2 or later, and an installed copy of The Planet Crafter.

Copy `BepInEx.dll`, `0Harmony.dll`, `Mono.Cecil*.dll`, `MonoMod.RuntimeDetour.dll`, and `MonoMod.Utils.dll` from BepInExPack 5.4.2305's `BepInEx/core` directory into a local `lib/` folder. These build/test references are excluded from Git and release packages.

Run `powershell -ExecutionPolicy Bypass -File ./build.ps1`.

For a non-default install, add `-GameManaged 'D:\SteamLibrary\steamapps\common\The Planet Crafter\Planet Crafter_Data\Managed'`.

The script builds the mod, runs its offline checks, and creates ZIPs in `dist/`. Game assemblies and local decompilation output are not distributed.

## Validation

- Built against the installed game assembly on 6 September 2026, using Unity 6000.3.2.
- Build completed with zero warnings or errors.
- All 68 offline checks passed, covering stack boundaries, item identity, container capacity, transfers, consumables, crafting space, patch transformations, and the dismantling capacity hot path.
- The maintainer reported successful in-game testing on 6 September 2026.
- Multiplayer-specific validation has not been reported. The host and participating clients need the same mod.

## Implementation

The mod retains individual world objects and native save data. It groups compatible objects for backpack display and capacity checks. Distinct saved item state and carried objects owning inventories remain separate.

If a required patch fails, the plugin removes its own patches and logs the error. Successful startup writes this line to the profile's `BepInEx/LogOutput.log`:

`Player Inventory Stacks 1.0.2 loaded: 64 items per backpack stack; containers unchanged.`

## Manual installation

Install BepInExPack 5.4.2305 or later in the mod manager profile. Import the release ZIP using **Import local mod**, or copy `PlayerInventoryStacks.dll` into the profile's `BepInEx/plugins/PlayerInventoryStacks/` folder, then select **Start modded**.
