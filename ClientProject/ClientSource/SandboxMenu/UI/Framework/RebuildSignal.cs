namespace SandboxMenu.UI.Framework;

internal sealed class RebuildSignal
{
    private bool _signalled;

    internal void Signal() => _signalled = true;

    internal bool Take()
    {
        if (!_signalled) { return false; }

        _signalled = false;
        return true;
    }
}
