using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using SpaceCraft;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerInventoryStacks;

[BepInPlugin(Id, "Player Inventory Stacks", "1.0.1")]
[BepInProcess("Planet Crafter.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "local.planetcrafter.playerinventorystacks";
    private Harmony harmony;

    private void Awake()
    {
        harmony = new Harmony(Id);
        try
        {
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Player Inventory Stacks 1.0.1 loaded: 64 items per backpack stack; containers unchanged.");
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf();
            Logger.LogError("Player Inventory Stacks could not patch this game version; all its patches were removed. " + ex);
        }
    }

    private void OnDestroy() => harmony?.UnpatchSelf();
}

[HarmonyPatch(typeof(InventoriesHandler), "OnNetworkSpawn")]
internal static class ResetWorld
{
    static void Prefix() => Backpacks.Clear();
}

[HarmonyPatch(typeof(PlayerBackpack), nameof(PlayerBackpack.SetInventory))]
internal static class RegisterBackpack
{
    static void Prefix(Inventory _playerInventory) { if (_playerInventory != null) Backpacks.Register(_playerInventory.GetId()); }
}

// Register before the first client inventory snapshot is reconstructed, or its items
// beyond vanilla slot count would be rejected before PlayerBackpack.SetInventory runs.
[HarmonyPatch(typeof(PlayerMainController), "InitPlayerClientRpc")]
internal static class RegisterClientBackpack
{
    static void Prefix(int inventoryId) => Backpacks.Register(inventoryId);
}

[HarmonyPatch(typeof(SavedDataHandler), nameof(SavedDataHandler.LoadSavedData))]
internal static class RegisterSavedBackpacks
{
    static void Postfix(SavedDataHandler __instance)
    {
        var players = __instance.GetLastLoadedPlayerData();
        if (players != null) foreach (var player in players) Backpacks.Register(player.inventoryId);
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
internal static class InventoryFull
{
    static bool Prefix(Inventory __instance, ref bool __result)
    {
        if (!Backpacks.IsPlayer(__instance)) return true;
        __result = Backpacks.IsFull(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), "AddItemInInventory")]
internal static class AddItemCapacity
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        PatchTools.ReplaceFull(instructions, new[] { new CodeInstruction(OpCodes.Ldarg_1) }, nameof(Backpacks.FullFor));
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.DropObjectsIfNotEnoughSpace))]
internal static class BackpackShrink
{
    static bool Prefix(Inventory __instance, Vector3 dropPosition, bool removeOnly, ref List<WorldObject> __result)
    {
        if (!Backpacks.IsPlayer(__instance)) return true;
        var overflow = Backpacks.Layout(__instance).Skip(Math.Max(0, __instance.GetSize())).SelectMany(s => s.Items).ToList();
        foreach (var item in overflow)
        {
            if (!removeOnly) WorldObjectsHandler.Instance.DropOnFloor(item, dropPosition);
            __instance.RemoveItem(item);
        }
        __result = overflow;
        return false;
    }
}

[HarmonyPatch(typeof(InventoryDisplayer), "SetInventoryBlocks")]
internal static class DisplayStacks
{
    static void Prefix(Inventory ____inventory, ref ReadOnlyCollection<WorldObject> inventoryWorldObjects,
        out List<StackLayout.Stack<WorldObject>> __state)
    {
        __state = null;
        if (!Backpacks.IsPlayer(____inventory)) return;
        __state = Backpacks.Layout(____inventory);
        var visibleItems = new List<WorldObject>(__state.Count);
        foreach (var stack in __state) visibleItems.Add(stack.Items[0]);
        inventoryWorldObjects = visibleItems.AsReadOnly();
    }

    static void Postfix(Inventory ____inventory, UnityEngine.UI.GridLayoutGroup ____grid, List<StackLayout.Stack<WorldObject>> __state)
    {
        if (__state == null) return;
        // Vanilla destruction is deferred: only the last GetSize() grid children
        // belong to the freshly rebuilt display, not the old pending destruction UI.
        var blocks = ____grid.GetComponentsInChildren<InventoryBlock>();
        int firstNewBlock = Math.Max(0, blocks.Length - ____inventory.GetSize());
        int visibleCount = Math.Min(blocks.Length - firstNewBlock, __state.Count);
        for (int i = 0; i < visibleCount; i++)
        {
            int count = __state[i].Items.Count;
            if (count <= 1) continue;
            var go = new GameObject("PlayerStackCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(blocks[firstNewBlock + i].transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3, 3); rect.offsetMax = new Vector2(-5, -3);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = count.ToString();
            label.fontSize = 22;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(0, 0, 0, 255);
        }
    }
}

internal static class PatchTools
{
    internal static readonly MethodInfo IsFull = AccessTools.Method(typeof(Inventory), nameof(Inventory.IsFull));

    // Fail closed on an incompatible game update; never silently leave half a patch.
    internal static IEnumerable<CodeInstruction> ReplaceFull(IEnumerable<CodeInstruction> source,
        IEnumerable<CodeInstruction> loadCandidate, string helper, int expected = 1)
    {
        var result = new List<CodeInstruction>();
        int matches = 0;
        foreach (var instruction in source)
        {
            if (instruction.Calls(IsFull))
            {
                var load = loadCandidate.Select(c => new CodeInstruction(c)).ToList();
                load[0].labels.AddRange(instruction.labels);
                load[0].blocks.AddRange(instruction.blocks);
                result.AddRange(load);
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Backpacks), helper)));
                matches++;
            }
            else result.Add(instruction);
        }
        if (matches != expected) throw new InvalidOperationException($"Expected {expected} capacity checks, found {matches}.");
        return result;
    }

    internal static WorldObject ActionItem(Component component) => component.GetComponentInParent<WorldObjectAssociated>()?.GetWorldObject();

    internal static IEnumerable<CodeInstruction> ActionCapacity(IEnumerable<CodeInstruction> source, FieldInfo owner = null)
    {
        var load = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_0) };
        if (owner != null) load.Add(new CodeInstruction(OpCodes.Ldfld, owner));
        load.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchTools), nameof(ActionItem))));
        return ReplaceFull(source, load, nameof(Backpacks.FullFor));
    }
}

[HarmonyPatch(typeof(ActionMinable), nameof(ActionMinable.OnAction))]
internal static class StartMiningCapacity
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => PatchTools.ActionCapacity(instructions);
}

[HarmonyPatch]
internal static class FinishMiningCapacity
{
    static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(ActionMinable), "FinishMining"));
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        PatchTools.ActionCapacity(instructions, AccessTools.Field(original.DeclaringType, "<>4__this"));
}

[HarmonyPatch(typeof(ActionGrabable), nameof(ActionGrabable.OnAction))]
internal static class GrabCapacity
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => PatchTools.ActionCapacity(instructions);
}

[HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
internal static class CraftCapacity
{
    static bool Prefix(PlayerMainController playerController, GroupItem groupItem, ref bool __result)
    {
        var inventory = playerController.GetPlayerBackpack().GetInventory();
        if (!Backpacks.IsPlayer(inventory)) return true;
        bool free = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();
        if (Backpacks.FitsCraft(inventory, groupItem.GetRecipe().GetIngredientsGroupInRecipe(), groupItem, free)) return true;
        // Reject before vanilla consumes ingredients: a partly consumed stack might
        // not release a slot for a newly crafted item type.
        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f);
        __result = false;
        return false;
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        PatchTools.ReplaceFull(instructions, new[] { new CodeInstruction(OpCodes.Ldarg_2) }, nameof(Backpacks.FullForGroup), 2);
}

[HarmonyPatch(typeof(WorldObjectsHandler), "RetrieveResourcesFromDeconstructionServerRpc")]
internal static class DeconstructionCapacity
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        int full = code.FindIndex(c => c.Calls(PatchTools.IsFull));
        int group = full < 0 ? -1 : code.FindLastIndex(full, c => c.Calls(AccessTools.Method(typeof(WorldObject), nameof(WorldObject.GetGroup))));
        if (group <= 0 || !code[group - 1].IsLdloc())
            throw new InvalidOperationException("Could not identify deconstruction resource local.");
        return PatchTools.ReplaceFull(code, new[] { new CodeInstruction(code[group - 1].opcode, code[group - 1].operand) }, nameof(Backpacks.FullFor));
    }
}

[HarmonyPatch(typeof(ActionPanelDeconstruct), "Deconstruct")]
internal static class PanelCapacity
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        // Locate the compiler's closure containing the current returned ingredient.
        var candidates = original.GetMethodBody().LocalVariables
            .Select(local => new { local, field = local.LocalType.GetField("group", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) })
            .Where(pair => pair.field?.FieldType == typeof(Group)).ToList();
        if (candidates.Count != 1) throw new InvalidOperationException("Could not identify panel ingredient local.");
        var candidate = candidates[0];
        return PatchTools.ReplaceFull(instructions, new[] {
            new CodeInstruction(OpCodes.Ldloc, candidate.local.LocalIndex),
            new CodeInstruction(OpCodes.Ldfld, candidate.field)
        }, nameof(Backpacks.FullForGroup));
    }
}

[HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.AnInventoryHasBeenClicked))]
internal static class QuickTransferKey
{
    internal static bool IsPressed(PlayerInputDispatcher input, Inventory source)
    {
        if (input.IsPressingAccessibilityKey()) return true;
        if (Keyboard.current == null || !Keyboard.current.shiftKey.isPressed) return false;
        var windows = Managers.GetManager<WindowsHandler>();
        var container = windows.GetWindowViaUiId(windows.GetOpenedUi()) as UiWindowContainer;
        return container != null && (Backpacks.IsPlayer(source) || Backpacks.IsPlayer(container.GetOtherInventory(source)));
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = new List<CodeInstruction>();
        int matches = 0;
        var key = AccessTools.Method(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.IsPressingAccessibilityKey));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(key))
            {
                var loadSource = new CodeInstruction(OpCodes.Ldarg_1);
                loadSource.labels.AddRange(instruction.labels);
                loadSource.blocks.AddRange(instruction.blocks);
                result.Add(loadSource);
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(QuickTransferKey), nameof(IsPressed))));
                matches++;
            }
            else result.Add(instruction);
        }
        if (matches != 1) throw new InvalidOperationException("Could not identify inventory quick-transfer key check.");
        return result;
    }
}
