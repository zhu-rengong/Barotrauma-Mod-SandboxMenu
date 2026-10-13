namespace SandboxMenu.Domain.Editing;

internal static class ComponentOrdinal
{
    internal static T? Find<T>(IEnumerable<T> candidates, string name, int index, Func<T, string> nameOf) where T : class
    {
        int wanted = Math.Max(1, index);
        int seen = 0;

        foreach (T candidate in candidates)
        {
            if (!string.Equals(nameOf(candidate), name, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++seen != wanted) { continue; }

            return candidate;
        }

        return null;
    }
}
