namespace SandboxMenu.UI.Models;

internal static class RowFilter
{
    internal static IReadOnlyList<TRow> Select<TRow>(IReadOnlyList<TRow> rows, Func<TRow, bool> shows)
    {
        List<TRow> visible = new(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            if (shows(rows[i])) { visible.Add(rows[i]); }
        }

        return visible;
    }
}
