# Player Inventory Stacks

![Player Inventory Stacks icon](icon.png)

[Download the install ZIP](https://github.com/N0x1/PlanetCrafter-PlayerInventoryStacks/releases/tag/v1.0.0)

A single-purpose BepInEx 5 mod for The Planet Crafter. Matching items stack to **64 in player backpacks only**, with a small white quantity at the bottom right of each stack. The 65th item starts another slot. Single items have no number, like Minecraft.

Chests, storage containers, machines, vehicles and equipped-item slots keep their normal capacity and one-item-per-slot behavior. Items with different saved state (such as DNA, names or charge) remain separate. Carried objects that own another inventory also remain separate.

## Install with r2modman

1. Select **The Planet Crafter** and your profile in r2modman.
2. Install **BepInExPack by BepInEx**, version **5.4.2305** or later, from the Online list. This is the mod loader, not another gameplay mod. [Official package](https://thunderstore.io/c/the-planet-crafter/p/BepInEx/BepInExPack/).
3. Use **Import local mod** and select `PlayerInventoryStacks-1.0.0.zip`. Depending on r2modman version, this is under the Profile menu / Import-Update or Settings / Profile.
4. Click **Start modded**.

If local ZIP import is unavailable, use r2modman's **Browse profile folder**, then copy the supplied `PlayerInventoryStacks.dll` into `BepInEx/plugins/PlayerInventoryStacks/`. Install the loader in step 2 first. Do not put this DLL in the game's Managed folder.

## Moving and using items

- **Left-click:** moves one item. A backpack stack of 64 becomes 63, and the chest receives one item in one slot.
- **Shift + left-click**, or the game's configured **accessibility key + left-click:** uses the game's transfer-all-matching-items action, stopping when the destination cannot accept more. A stack of 64 becomes 54 when filling an empty ten-slot chest. If several backpack stacks contain the same item type, this action can transfer from all of them, as in the base game.
- Moving items back into the backpack combines compatible items up to 64 per slot.
- Consume, drop, equipment actions, sorting, and Move All retain their normal controls. Consuming or dropping a displayed item acts on one item.
- **Tab, then right-click water, food, or an oxygen refill:** consumes exactly one item when the game accepts its use (64 becomes 63), refreshes the quantity, and clears the slot when its final item is consumed. The game's usual full-gauge restrictions still apply.

There are no settings, extra hotkeys beyond the requested Shift transfer, or other gameplay features.

## Saves and verification

The mod keeps the game's individual item IDs and native save format; it does not replace items with synthetic quantity objects. Before disabling or removing the mod, unload your backpack until its **individual item count** fits the normal number of backpack slots. Otherwise vanilla capacity handling can drop or reject overflow items.

Built against the installed `Assembly-CSharp.dll` on 6 September 2026 (Unity 6000.3.2). Build completed with zero warnings/errors. **60 offline checks passed**, including the exact 64-to-63 and 64-to-54 transfer cases, single-item consumption of water/food/oxygen, 500 randomized capacity comparisons, item-state separation, and patch transformations checked against the installed game assembly.

**Live gameplay and visual rendering have not been tested.** Try a test save first: mine two iron, inspect the quantity label, transfer to a chest, test Shift transfer, craft an item, and save/reload. Multiplayer has registration support but has not been validated; the host and all participating clients need the same mod.

The profile's `BepInEx/LogOutput.log` should contain:

`Player Inventory Stacks 1.0.0 loaded: 64 items per backpack stack; containers unchanged.`

If a future update changes a required patch target, the mod reports an error and removes its own patches instead of continuing with a partial patch set.

## Source and build

Source lives in `src/`. `PlayerInventoryStacks.csproj` references your installed game and the local build-only DLLs in `lib/`. Game, Unity, BepInEx, and Harmony DLLs are not included in the distributable mod ZIP.

Build on Windows with the .NET 9 SDK, .NET Framework 4.7.2 or later, and a legally installed copy of the game. Copy `BepInEx.dll`, `0Harmony.dll`, `Mono.Cecil*.dll`, `MonoMod.RuntimeDetour.dll`, and `MonoMod.Utils.dll` from BepInExPack 5.4.2305's `BepInEx/core` directory into a local `lib/` folder. These are build/test references only and are excluded from Git.

Run `powershell -ExecutionPolicy Bypass -File ./build.ps1` from the project directory. For a non-default Steam install, pass `-GameManaged 'D:\SteamLibrary\steamapps\common\The Planet Crafter\Planet Crafter_Data\Managed'`. The script builds, runs the offline checks, and creates the install ZIP under `dist/`. Inspection output under `inspection/` is local reference material and is not part of the repository or releases.
