using System.Collections.Frozen;

namespace SandboxMenu.Domain.Model;

internal static class SpawnSlots
{
    private const char Separator = ',';

    private static readonly FrozenDictionary<string, InvSlotType> ByName =
        Enum.GetValues<InvSlotType>()
            .Where(slot => slot != InvSlotType.None)
            .ToFrozenDictionary(slot => slot.ToString(), StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<InvSlotType> All { get; } = [.. ByName.Values];

    internal static string? ToXml(InvSlotType[]? slots)
        => slots is null ? null : string.Join(Separator, slots.Select(slot => slot.ToString()));

    internal static InvSlotType[]? FromXml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return null; }

        InvSlotType[] parsed =
        [
            .. text
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => ByName.TryGetValue(name, out InvSlotType slot) ? slot : (InvSlotType?)null)
                .OfType<InvSlotType>()
        ];

        return parsed.Length == 0 ? null : parsed;
    }
}
