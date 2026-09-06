using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using SpaceCraft;

namespace PlayerInventoryStacks;

internal static class Backpacks
{
    private static readonly HashSet<int> Ids = new HashSet<int>();
    private static readonly FieldInfo LockTime = typeof(WorldObject).GetField("_lockInInventoryTime", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void Clear() => Ids.Clear();
    internal static void Register(int id) { if (id > 0) Ids.Add(id); }
    internal static bool IsPlayer(Inventory inventory) => inventory != null && Ids.Contains(inventory.GetId());

    // Never combine different DNA, blueprint contents, charge, names, or other item state.
    // Objects owning an inventory stay separate, even while carried in the backpack.
    internal static string Key(WorldObject item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (item.GetLinkedInventoryId() != 0 || item.GetSecondaryInventoriesId()?.Count > 0
            || item.GetLinkedWorldObject() != 0 || (float)LockTime.GetValue(item) != 0f)
            return "unique:" + item.GetId();
        var b = new StringBuilder();
        void Add(object value)
        {
            string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            b.Append(s.Length).Append(':').Append(s).Append('|');
        }
        Add(item.GetGroup().GetId());
        Add(item.GetText());
        Add(item.GetSetting());
        Add(item.GetGeneticTraitType()); Add(item.GetGeneticTraitValue());
        var c = item.GetColor(); Add(c.r); Add(c.g); Add(c.b); Add(c.a);
        Add(item.GetEnergy()); Add(item.GetGrowth()); Add(item.GetHunger()); Add(item.GetPetTime());
        Add(item.GetCount().x); Add(item.GetCount().y); Add(item.GetPlanetLinkedHash());
        var groups = item.GetLinkedGroups();
        Add(groups?.Count ?? 0);
        if (groups != null) foreach (var group in groups) Add(group.GetId());
        var panels = item.GetPanelsId();
        Add(panels?.Count ?? 0);
        if (panels != null) foreach (int panel in panels) Add(panel);
        return b.ToString();
    }

    internal static List<StackLayout.Stack<WorldObject>> Layout(Inventory inventory) =>
        StackLayout.Create(inventory.GetInsideWorldObjects(), Key);

    internal static bool FullFor(Inventory inventory, WorldObject item)
    {
        if (!IsPlayer(inventory)) return inventory.IsFull();
        if (item == null) return Layout(inventory).Count >= inventory.GetSize();
        if (inventory.ContainWorldObject(item)) return false;
        return !StackLayout.CanAdd(inventory.GetInsideWorldObjects(), item, inventory.GetSize(), Key);
    }

    internal static bool FullForGroup(Inventory inventory, Group group) =>
        FullFor(inventory, new WorldObject(0, group));

    internal static bool FitsCraft(Inventory inventory, List<Group> ingredients, Group output, bool freeCraft)
    {
        var remaining = inventory.GetInsideWorldObjects().ToList();
        if (!freeCraft)
            foreach (var group in ingredients)
            {
                int index = remaining.FindLastIndex(item => item.GetGroup() == group);
                if (index >= 0) remaining.RemoveAt(index);
            }
        return StackLayout.CanAdd(remaining, new WorldObject(0, output), inventory.GetSize(), Key);
    }
}
