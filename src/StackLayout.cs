using System;
using System.Collections.Generic;

namespace PlayerInventoryStacks;

// The same layout defines both capacity and the visible slots.
internal static class StackLayout
{
    internal const int Limit = 64;

    internal sealed class Stack<T>
    {
        internal readonly List<T> Items = new List<T>();
    }

    internal static List<Stack<T>> Create<T, TKey>(IEnumerable<T> items, Func<T, TKey> key,
        IEqualityComparer<TKey> comparer = null)
    {
        var result = new List<Stack<T>>();
        var open = new Dictionary<TKey, Stack<T>>(comparer ?? EqualityComparer<TKey>.Default);
        foreach (var item in items)
        {
            TKey id = key(item);
            if (!open.TryGetValue(id, out var stack) || stack.Items.Count == Limit)
            {
                stack = new Stack<T>();
                open[id] = stack;
                result.Add(stack);
            }
            stack.Items.Add(item);
        }
        return result;
    }

    internal static bool CanAdd<T, TKey>(IEnumerable<T> items, T candidate, int slots, Func<T, TKey> key,
        IEqualityComparer<TKey> comparer = null)
    {
        var equality = comparer ?? EqualityComparer<TKey>.Default;
        var counts = new Dictionary<TKey, int>(equality);
        int usedSlots = 0;
        foreach (var item in items)
        {
            TKey id = key(item);
            counts.TryGetValue(id, out int count);
            if (count % Limit == 0) usedSlots++;
            counts[id] = count + 1;
        }

        TKey candidateId = key(candidate);
        if (usedSlots > slots) return false;
        return (counts.TryGetValue(candidateId, out int candidateCount) && candidateCount % Limit != 0)
            || usedSlots < slots;
    }
}
