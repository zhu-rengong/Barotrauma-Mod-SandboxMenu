namespace SandboxMenu.Infrastructure;

internal static class Identifiers
{
    internal static Identifier Of(string? name) => (name ?? string.Empty).ToIdentifier();
}
