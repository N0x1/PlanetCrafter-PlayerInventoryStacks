using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace PlayerInventoryStacks;

internal static class Backpacks
{
    private static readonly HashSet<int> Ids = new HashSet<int>();

    // Unity gameplay runs on the main thread. Reusing this table removes the large
    // allocation burst previously caused by every capacity check during dismantling.
    [ThreadStatic]
    private static Dictionary<StackKey, int> usageCounts;

    internal static void Clear()
    {
        Ids.Clear();
        usageCounts?.Clear();
    }

    internal static void Register(int id) { if (id > 0) Ids.Add(id); }
    internal static bool IsPlayer(Inventory inventory) => inventory != null && Ids.Contains(inventory.GetId());
    internal static StackKey Key(WorldObject item) => new StackKey(item);

    internal static List<StackLayout.Stack<WorldObject>> Layout(Inventory inventory) =>
        StackLayout.Create(inventory.GetInsideWorldObjects(), Key);

    internal static bool IsFull(Inventory inventory) => CountUsage(inventory, default, false, out _) >= inventory.GetSize();

    internal static bool FullFor(Inventory inventory, WorldObject item)
    {
        if (!IsPlayer(inventory)) return inventory.IsFull();
        if (item == null) return IsFull(inventory);
        if (inventory.ContainWorldObject(item)) return false;

        var candidate = new StackKey(item);
        int usedSlots = CountUsage(inventory, candidate, true, out int candidateCount);
        return usedSlots > inventory.GetSize()
            || (candidateCount % StackLayout.Limit == 0 && usedSlots >= inventory.GetSize());
    }

    internal static bool FullForGroup(Inventory inventory, Group group)
    {
        if (!IsPlayer(inventory)) return inventory.IsFull();
        var candidate = new StackKey(group);
        int usedSlots = CountUsage(inventory, candidate, true, out int candidateCount);
        return usedSlots > inventory.GetSize()
            || (candidateCount % StackLayout.Limit == 0 && usedSlots >= inventory.GetSize());
    }

    private static int CountUsage(Inventory inventory, StackKey candidate, bool findCandidate, out int candidateCount)
    {
        var counts = usageCounts ?? (usageCounts = new Dictionary<StackKey, int>(64));
        counts.Clear();
        int usedSlots = 0;
        candidateCount = 0;
        try
        {
            foreach (var item in inventory.GetInsideWorldObjects())
            {
                var key = new StackKey(item);
                counts.TryGetValue(key, out int count);
                if (count % StackLayout.Limit == 0) usedSlots++;
                count++;
                counts[key] = count;
                if (findCandidate && key.Equals(candidate)) candidateCount = count;
            }
            return usedSlots;
        }
        finally
        {
            // Release references to item metadata while retaining the table's capacity.
            counts.Clear();
        }
    }

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

// Compact, allocation-free equivalent of the original serialized string key.
internal readonly struct StackKey : IEquatable<StackKey>
{
    private static readonly AccessTools.FieldRef<WorldObject, float> LockTime =
        AccessTools.FieldRefAccess<WorldObject, float>("_lockInInventoryTime");

    private readonly bool unique;
    private readonly int uniqueId;
    private readonly string groupId;
    private readonly string text;
    private readonly int setting;
    private readonly DataConfig.GeneticTraitType geneticType;
    private readonly int geneticValue;
    private readonly Color color;
    private readonly float energy;
    private readonly float growth;
    private readonly float hunger;
    private readonly float petTime;
    private readonly Vector2Int count;
    private readonly int planetLinkedHash;
    private readonly List<Group> linkedGroups;
    private readonly List<int> panels;
    private readonly int hashCode;

    internal StackKey(WorldObject item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        unique = item.GetLinkedInventoryId() != 0 || item.GetSecondaryInventoriesId()?.Count > 0
            || item.GetLinkedWorldObject() != 0 || LockTime(item) != 0f;
        uniqueId = unique ? item.GetId() : 0;
        groupId = item.GetGroup()?.GetId() ?? string.Empty;
        text = item.GetText() ?? string.Empty;
        setting = item.GetSetting();
        geneticType = item.GetGeneticTraitType();
        geneticValue = item.GetGeneticTraitValue();
        color = item.GetColor();
        energy = item.GetEnergy();
        growth = item.GetGrowth();
        hunger = item.GetHunger();
        petTime = item.GetPetTime();
        count = item.GetCount();
        planetLinkedHash = item.GetPlanetLinkedHash();
        linkedGroups = item.GetLinkedGroups();
        panels = item.GetPanelsId();
        hashCode = ComputeHash();
    }

    internal StackKey(Group group)
    {
        unique = false;
        uniqueId = 0;
        groupId = group?.GetId() ?? string.Empty;
        text = string.Empty;
        setting = 0;
        geneticType = DataConfig.GeneticTraitType.Null;
        geneticValue = 0;
        color = default;
        energy = 1f;
        growth = 0f;
        hunger = 0f;
        petTime = 0f;
        count = default;
        planetLinkedHash = 0;
        linkedGroups = null;
        panels = null;
        hashCode = ComputeHash();
    }

    public bool Equals(StackKey other)
    {
        if (unique || other.unique) return unique && other.unique && uniqueId == other.uniqueId;
        return StringComparer.Ordinal.Equals(groupId, other.groupId)
            && StringComparer.Ordinal.Equals(text, other.text)
            && setting == other.setting
            && geneticType == other.geneticType
            && geneticValue == other.geneticValue
            && color.r.Equals(other.color.r) && color.g.Equals(other.color.g)
            && color.b.Equals(other.color.b) && color.a.Equals(other.color.a)
            && energy.Equals(other.energy) && growth.Equals(other.growth)
            && hunger.Equals(other.hunger) && petTime.Equals(other.petTime)
            && count.Equals(other.count) && planetLinkedHash == other.planetLinkedHash
            && GroupsEqual(linkedGroups, other.linkedGroups)
            && ListsEqual(panels, other.panels);
    }

    public override bool Equals(object obj) => obj is StackKey other && Equals(other);
    public override int GetHashCode() => hashCode;

    private int ComputeHash()
    {
        if (unique) return uniqueId;
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(groupId);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(text);
            hash = hash * 31 + setting;
            hash = hash * 31 + (int)geneticType;
            hash = hash * 31 + geneticValue;
            hash = hash * 31 + color.r.GetHashCode(); hash = hash * 31 + color.g.GetHashCode();
            hash = hash * 31 + color.b.GetHashCode(); hash = hash * 31 + color.a.GetHashCode();
            hash = hash * 31 + energy.GetHashCode(); hash = hash * 31 + growth.GetHashCode();
            hash = hash * 31 + hunger.GetHashCode(); hash = hash * 31 + petTime.GetHashCode();
            hash = hash * 31 + count.GetHashCode(); hash = hash * 31 + planetLinkedHash;
            if (linkedGroups != null)
                foreach (var group in linkedGroups)
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(group?.GetId() ?? string.Empty);
            if (panels != null) foreach (int panel in panels) hash = hash * 31 + panel;
            return hash;
        }
    }

    private static bool GroupsEqual(List<Group> left, List<Group> right)
    {
        int leftCount = left?.Count ?? 0;
        if (leftCount != (right?.Count ?? 0)) return false;
        for (int i = 0; i < leftCount; i++)
            if (!StringComparer.Ordinal.Equals(left[i]?.GetId(), right[i]?.GetId())) return false;
        return true;
    }

    private static bool ListsEqual(List<int> left, List<int> right)
    {
        int leftCount = left?.Count ?? 0;
        if (leftCount != (right?.Count ?? 0)) return false;
        for (int i = 0; i < leftCount; i++) if (left[i] != right[i]) return false;
        return true;
    }
}
