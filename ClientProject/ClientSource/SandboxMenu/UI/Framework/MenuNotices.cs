namespace SandboxMenu.UI.Framework;

internal static class MenuNotices
{
    private static bool _resolution;

    static MenuNotices() => StaticState.Register(() => _resolution = false);

    internal static void ResolutionChanged() => _resolution = true;

    internal static bool TakeResolution()
    {
        bool resolution = _resolution;
        _resolution = false;

        return resolution;
    }
}
