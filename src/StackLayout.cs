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

    internal static List<Stack<T>> Create<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var result = new List<Stack<T>>();
        var open = new Dictionary<string, Stack<T>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            string id = key(item);
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

    internal static bool CanAdd<T>(IEnumerable<T> items, T candidate, int slots, Func<T, string> key)
    {
        var stacks = Create(items, key);
        if (stacks.Count < slots) return true;
        if (stacks.Count > slots) return false;
        string id = key(candidate);
        foreach (var stack in stacks)
            if (stack.Items.Count < Limit && key(stack.Items[0]) == id) return true;
        return false;
    }
}
