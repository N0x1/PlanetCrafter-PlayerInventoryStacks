using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PlayerInventoryStacks;
using SpaceCraft;
using UnityEngine;
using HarmonyLib;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
Group Group(string name)
{
    var group = (Group)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Group));
    group.id = name;
    return group;
}
var iron = Group("Iron"); var cobalt = Group("Cobalt"); var titanium = Group("Titanium");
int next = 1000;
WorldObject Item(Group group) => new WorldObject(next++, group);
List<WorldObject> Items(Group group, int count) => Enumerable.Range(0, count).Select(_ => Item(group)).ToList();
foreach (int count in new[] { 0, 1, 63, 64, 65, 127, 128, 129, 768 })
{
    var items = Items(iron, count);
    var stacks = StackLayout.Create(items, Backpacks.Key);
    Check(stacks.Count == (count + 63) / 64 && stacks.All(s => s.Items.Count <= 64)
        && stacks.SelectMany(s => s.Items).SequenceEqual(items), $"{count} iron: exact slots, bounded stacks, all item identities preserved");
}
var mixed = Items(iron, 63).Concat(Items(cobalt, 64)).ToList();
var backpack = new Inventory(1, 2, mixed);
Backpacks.Register(1);
Check(!Backpacks.FullFor(backpack, Item(iron)), "Full slot grid accepts 64th iron");
Check(Backpacks.FullFor(backpack, Item(cobalt)), "64 cobalt cannot become 65 in one slot");
Check(Backpacks.FullFor(backpack, Item(titanium)), "Full slot grid rejects new item type");
Check(!Backpacks.IsPlayer(new Inventory(2, 2)), "Chest inventory is excluded");
Check(!Backpacks.IsPlayer(new Inventory(3, 2)), "Equipment inventory is excluded");
var chest = new Inventory(2, 2, Items(iron, 2));
Check(Backpacks.FullFor(chest, Item(iron)), "Chest still full after two individual items");
chest.RemoveItem(chest.GetInsideWorldObjects()[0]);
Check(!Backpacks.FullFor(chest, Item(iron)), "Chest has one free vanilla slot after removal");
Check(!Backpacks.FitsCraft(backpack, new List<Group> { iron, cobalt }, titanium, false), "Craft cannot consume ingredients without room for output");
Check(Backpacks.FitsCraft(backpack, new List<Group> { iron }, iron, false), "Craft into an existing matching stack fits");
var oneEach = new Inventory(1, 2, new List<WorldObject> { Item(iron), Item(cobalt) });
Check(Backpacks.FitsCraft(oneEach, new List<Group> { iron }, titanium, false), "Consuming final ingredient releases an output slot");
Check(!Backpacks.FitsCraft(oneEach, new List<Group> { iron }, titanium, true), "Free crafting does not pretend ingredients were removed");
var shrinkItems = Items(iron, 65).Concat(Items(cobalt, 2)).ToList();
var shrink = StackLayout.Create(shrinkItems, Backpacks.Key);
Check(shrink.Take(1).SelectMany(s => s.Items).Count() == 64 && shrink.Skip(1).SelectMany(s => s.Items).Count() == 3,
    "Backpack shrink keeps 64 iron and selects only overflow stacks");
var a = Item(iron); var b = Item(iron);
Check(Backpacks.Key(a).Equals(Backpacks.Key(b)), "Ordinary identical items share key");
b.SetEnergy(0.25f);
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Different battery charge stays separate");
b.SetEnergy(1); b.SetText("Named item");
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Named items stay separate");
b.SetText(""); b.SetLinkedGroups(new List<Group> { cobalt });
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Different blueprint/DNA contents stay separate");
b.SetLinkedGroups(null); b.SetColor(new Color(1, 0, 0, 1));
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Different item colors stay separate");
b.SetColor(default); b.SetText(""); b.SetLinkedGroups(new List<Group>()); b.SetPanelsId(new List<int>());
Check(Backpacks.Key(a).Equals(Backpacks.Key(b)), "Null and empty optional item state remain compatible");
b.SetSetting(7);
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Different item settings stay separate");
b.SetSetting(0); b.SetPanelsId(new List<int> { 12 });
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Different panel contents stay separate");
b.SetPanelsId(null); b.SetLinkedInventoryId(42);
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Items owning an inventory stay separate");
b.SetLinkedInventoryId(0); b.SetLinkedWorldObject(42);
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Items linked to a world object stay separate");
b.SetLinkedWorldObject(0); b.SetLockInInventoryTime(42f);
Check(!Backpacks.Key(a).Equals(Backpacks.Key(b)), "Items with an inventory lock stay separate");
var secondaryInventoryItem = new WorldObject(next++, iron, Vector3.zero, Quaternion.identity, 0, 0,
    default, null, null, 0, new List<int> { 42 });
Check(!Backpacks.Key(a).Equals(Backpacks.Key(secondaryInventoryItem)), "Items owning secondary inventories stay separate");
var interleaved = new[] { Item(iron), Item(cobalt), Item(iron), Item(titanium), Item(cobalt) };
var arranged = StackLayout.Create(interleaved, Backpacks.Key);
Check(arranged.Select(s => s.Items.Count).SequenceEqual(new[] { 2, 2, 1 }) && arranged.Select(s => s.Items[0].GetGroup()).SequenceEqual(new[] { iron, cobalt, titanium }),
    "Interleaved pickups group by first-seen order without dropping items");
// Use the game's real Inventory.AddItem and RemoveItem operations, just as the
// server transfer routine does. Destination is deliberately an ordinary chest.
var transferSource = new Inventory(1, 1, Items(iron, 64));
var transferChest = new Inventory(2, 10);
var clicked = transferSource.GetInsideWorldObjects()[0];
if (transferChest.AddItem(clicked)) transferSource.RemoveItem(clicked);
Check(transferSource.GetInsideWorldObjects().Count == 63 && transferChest.GetInsideWorldObjects().Count == 1,
    "Normal click: 64 iron becomes 63; chest gets one individual iron");
transferSource = new Inventory(1, 1, Items(iron, 64));
transferChest = new Inventory(2, 10);
foreach (var item in transferSource.GetInsideWorldObjects().Reverse().ToList())
    if (transferChest.AddItem(item)) transferSource.RemoveItem(item);
Check(transferSource.GetInsideWorldObjects().Count == 54 && transferChest.GetInsideWorldObjects().Count == 10 && transferChest.IsFull(),
    "Quick transfer: 64 iron becomes 54; ten-slot chest has ten separate iron");
Check(transferSource.GetInsideWorldObjects().Concat(transferChest.GetInsideWorldObjects()).Select(item => item.GetId()).Distinct().Count() == 64,
    "Quick transfer conserves all 64 distinct item IDs");
// Test conservation and capacity decisions across randomized heterogeneous inventories.
// Exercise the actual display prefix: right-click receives its representative
// WorldObject, and vanilla consumption removes precisely that one object.
foreach (string consumable in new[] { "WaterBottle1", "FoodGrower1", "OxygenCapsule1" })
{
    var consumableGroup = Group(consumable);
    var consumableItems = Items(consumableGroup, 64);
    var consumeInventory = new Inventory(1, 1, consumableItems);
    object[] displayArgs = { consumeInventory, consumeInventory.GetInsideWorldObjects(), null };
    AccessTools.Method(typeof(DisplayStacks), "Prefix").Invoke(null, displayArgs);
    var visible = (System.Collections.ObjectModel.ReadOnlyCollection<WorldObject>)displayArgs[1];
    Check(visible.Count == 1 && ReferenceEquals(visible[0], consumableItems[0]),
        consumable + ": visible slot passes one real item to the original consume handler");
    var survivingIds = consumableItems.Skip(1).Select(item => item.GetId()).ToArray();
    consumeInventory.RemoveItem(visible[0]);
    var after = Backpacks.Layout(consumeInventory);
    Check(after.Count == 1 && after[0].Items.Count == 63
        && consumeInventory.GetInsideWorldObjects().Select(item => item.GetId()).SequenceEqual(survivingIds),
        consumable + ": consuming representative changes 64 to 63, preserving all remaining items");
    foreach (var item in consumeInventory.GetInsideWorldObjects().ToList()) consumeInventory.RemoveItem(item);
    Check(Backpacks.Layout(consumeInventory).Count == 0, consumable + ": consuming final item leaves an empty slot");
}
var random = new System.Random(64064);
for (int trial = 0; trial < 500; trial++)
{
    int slots = random.Next(1, 15);
    var items = Enumerable.Range(0, random.Next(0, 900)).Select(_ => random.Next(0, 8).ToString()).ToList();
    string candidate = random.Next(0, 10).ToString();
    int expected = items.Append(candidate).GroupBy(s => s).Sum(g => (g.Count() + 63) / 64);
    bool actual = StackLayout.CanAdd(items, candidate, slots, s => s);
    if (actual != (expected <= slots)) throw new Exception("Random capacity mismatch");
}
Check(true, "500 randomized capacity comparisons against independent group-count calculation");

// Compare the optimized dismantling capacity path with the allocation-heavy
// string-key implementation used in 1.0.0.
string LegacyKey(WorldObject item)
{
    var builder = new StringBuilder();
    void Add(object value)
    {
        string part = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        builder.Append(part.Length).Append(':').Append(part).Append('|');
    }
    Add(item.GetGroup().GetId()); Add(item.GetText()); Add(item.GetSetting());
    Add(item.GetGeneticTraitType()); Add(item.GetGeneticTraitValue());
    var color = item.GetColor(); Add(color.r); Add(color.g); Add(color.b); Add(color.a);
    Add(item.GetEnergy()); Add(item.GetGrowth()); Add(item.GetHunger()); Add(item.GetPetTime());
    Add(item.GetCount().x); Add(item.GetCount().y); Add(item.GetPlanetLinkedHash());
    var groups = item.GetLinkedGroups(); Add(groups?.Count ?? 0);
    if (groups != null) foreach (var group in groups) Add(group.GetId());
    var panels = item.GetPanelsId(); Add(panels?.Count ?? 0);
    if (panels != null) foreach (int panel in panels) Add(panel);
    return builder.ToString();
}
long Measure(Action action, int iterations)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var timer = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++) action();
    timer.Stop();
    return timer.ElapsedTicks;
}
var performanceGroups = Enumerable.Range(0, 8).Select(i => Group("Perf" + i)).ToArray();
var performanceItems = Enumerable.Range(0, 4096).Select(i => Item(performanceGroups[i % performanceGroups.Length])).ToList();
var performanceInventory = new Inventory(1, 64, performanceItems);
_ = StackLayout.Create(performanceItems, LegacyKey).Count;
_ = Backpacks.IsFull(performanceInventory);
long legacyTicks = Measure(() => _ = StackLayout.Create(performanceItems, LegacyKey).Count, 20);
long optimizedTicks = Measure(() => _ = Backpacks.IsFull(performanceInventory), 20);
Check(optimizedTicks < legacyTicks,
    $"Optimized 4,096-item capacity scan is faster ({optimizedTicks * 1000.0 / Stopwatch.Frequency:F1} ms vs {legacyTicks * 1000.0 / Stopwatch.Frequency:F1} ms for 20 scans)");
Backpacks.Clear();
Check(!Backpacks.IsPlayer(backpack), "World reset removes old backpack IDs");

using var game = AssemblyDefinition.ReadAssembly(typeof(Inventory).Assembly.Location);
TypeDefinition Type(string name) => game.MainModule.Types.Single(t => t.FullName == "SpaceCraft." + name);
MethodDefinition Method(string type, string name) => Type(type).Methods.Single(m => m.Name == name);
bool FullCall(Instruction instruction) => instruction.Operand is MethodReference m && m.DeclaringType.FullName == "SpaceCraft.Inventory" && m.Name == "IsFull";
var consumeCalls = Method("InventoryDisplayer", "Consume").Body.Instructions
    .Select(i => i.Operand).OfType<MethodReference>().ToList();
Check(new[] { "Drink", "Eat", "Breath" }.All(name => consumeCalls.Any(m => m.DeclaringType.FullName == "SpaceCraft.PlayerGaugesHandler" && m.Name == name))
    && consumeCalls.Any(m => m.DeclaringType.FullName == "SpaceCraft.InventoriesHandler" && m.Name == "RemoveItemFromInventory")
    && !consumeCalls.Any(m => m.Name == "RemoveItemsFromInventory" || m.Name == "RemoveAndDestroyAllItemsFromInventory"),
    "Installed consume handler uses single-item removal for water, food and oxygen; no bulk removal");
Check(Method("InventoriesHandler", "RemoveItemFromInventoryServerRpc").Body.Instructions
    .Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "SpaceCraft.Inventory" && m.Name == "RefreshDisplayerContent"),
    "Successful server consumption refreshes the inventory display");
foreach (var target in new[] { ("Inventory", "AddItemInInventory", 1), ("ActionMinable", "OnAction", 1), ("ActionGrabable", "OnAction", 1), ("CraftManager", "TryToCraftInInventory", 2), ("ActionPanelDeconstruct", "Deconstruct", 1), ("WorldObjectsHandler", "RetrieveResourcesFromDeconstructionServerRpc", 1) })
    Check(Method(target.Item1, target.Item2).Body.Instructions.Count(FullCall) == target.Item3, "Installed assembly capacity patch: " + target.Item1 + "." + target.Item2);
var iterator = Type("ActionMinable").NestedTypes.Single(t => t.Name.StartsWith("<FinishMining>"));
Check(iterator.Fields.Any(f => f.Name == "<>4__this") && iterator.Methods.Single(m => m.Name == "MoveNext").Body.Instructions.Count(FullCall) == 1,
    "Installed mining coroutine has correct owner and capacity check");
var display = Method("InventoryDisplayer", "SetInventoryBlocks");
Check(display.Parameters[0].Name == "inventoryWorldObjects" && Type("InventoryDisplayer").Fields.Any(f => f.Name == "_grid")
    && Type("InventoryDisplayer").Fields.Any(f => f.Name == "_inventory"), "Display patch signature matches installed game");
Check(Method("PlayerBackpack", "SetInventory").Parameters[0].Name == "_playerInventory"
    && Method("PlayerMainController", "InitPlayerClientRpc").Parameters[0].Name == "inventoryId", "Backpack registration patch signatures match");
// Invoke the real transpilers against instructions read from the installed binary.
foreach (var patchType in new[] { typeof(AddItemCapacity), typeof(StartMiningCapacity), typeof(FinishMiningCapacity), typeof(GrabCapacity), typeof(CraftCapacity), typeof(PanelCapacity), typeof(QuickTransferKey) })
{
    System.Reflection.MethodBase target;
    if (patchType == typeof(FinishMiningCapacity))
        target = AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(ActionMinable), "FinishMining"));
    else
    {
        var attr = patchType.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Single();
        target = AccessTools.Method(attr.info.declaringType, attr.info.methodName);
    }
    var original = PatchProcessor.GetOriginalInstructions(target);
    var transform = AccessTools.Method(patchType, "Transpiler");
    object[] parameters = transform.GetParameters().Length == 2 ? new object[] { original, target } : new object[] { original };
    var modified = ((IEnumerable<CodeInstruction>)transform.Invoke(null, parameters)).ToList();
    var helperType = patchType == typeof(QuickTransferKey) ? typeof(QuickTransferKey) : typeof(Backpacks);
    Check(!modified.Any(c => c.Calls(PatchTools.IsFull)) && modified.Any(c => c.operand is System.Reflection.MethodInfo m && m.DeclaringType == helperType),
        "Actual Harmony transformation: " + patchType.Name);
}
// The desktop CLR cannot resolve Unity Netcode's default interface methods in
// this server RPC. Read its IL with Cecil to validate the candidate-selection
// transformation without loading those unsupported runtime interfaces.
var emitCodes = typeof(System.Reflection.Emit.OpCodes).GetFields().Where(f => f.FieldType == typeof(System.Reflection.Emit.OpCode))
    .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(op => op.Name);
var deconstructIL = Method("WorldObjectsHandler", "RetrieveResourcesFromDeconstructionServerRpc").Body.Instructions.Select(i =>
{
    object operand = i.Operand;
    if (FullCall(i)) operand = PatchTools.IsFull;
    else if (i.Operand is MethodReference m && m.DeclaringType.FullName == "SpaceCraft.WorldObject" && m.Name == "GetGroup")
        operand = AccessTools.Method(typeof(WorldObject), "GetGroup");
    else if (i.Operand is VariableDefinition v) operand = v.Index;
    return new CodeInstruction(emitCodes[i.OpCode.Name], operand);
}).ToList();
var deconstructed = ((IEnumerable<CodeInstruction>)AccessTools.Method(typeof(DeconstructionCapacity), "Transpiler").Invoke(null, new object[] { deconstructIL })).ToList();
Check(!deconstructed.Any(c => c.Calls(PatchTools.IsFull)) && deconstructed.Any(c => c.Calls(AccessTools.Method(typeof(Backpacks), "FullFor"))),
    "Deconstruction candidate-selection transformation against installed RPC IL");
Console.WriteLine($"All {passed} checks passed. These are offline checks, not a live gameplay test.");
