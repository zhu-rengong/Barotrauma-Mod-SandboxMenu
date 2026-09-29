namespace SandboxMenu.Domain.Model;

internal static class SpawnSlots
{
    private const char Separator = ',';

    internal static IReadOnlyList<InvSlotType> All { get; } =
    [
        .. Enum.GetValues<InvSlotType>().Where(slot => slot != InvSlotType.None)
    ];

    internal static string? ToXml(InvSlotType[]? slots)
        => slots is null ? null : string.Join(Separator, slots.Select(slot => slot.ToString()));

    internal static InvSlotType[]? FromXml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return null; }

        InvSlotType[] parsed =
        [
            .. text
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => Enum.TryParse(name, out InvSlotType slot) ? slot : (InvSlotType?)null)
                .OfType<InvSlotType>()
        ];

        return parsed.Length == 0 ? null : parsed;
    }
}
