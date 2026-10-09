namespace SandboxMenu.UI.ViewModels;

internal static class EntrySummary
{
    internal static LocalizedString Text(SpawnEntry entry)
        => entry switch
        {
            ItemEntry item => Name(item) + AmountSuffix(item),
            ReferenceEntry reference => $"@ {reference.TemplateName}",
            _ => string.Empty
        };

    internal static LocalizedString AmountSuffix(ItemEntry item)
    {
        LocalizedString amount = item.Amount is { } range ? $" ×{range}" : string.Empty;
        LocalizedString stack = item.Stacks is { } stacks
            ? " " + TextManager.GetWithVariable("sandboxmenu.summary.stacks", "[count]", stacks.ToString())
            : string.Empty;

        return amount + stack;
    }

    private static LocalizedString Name(ItemEntry item)
        => string.IsNullOrEmpty(item.Identifier) ? TextManager.Get("sandboxmenu.summary.unset") : item.Identifier;
}
