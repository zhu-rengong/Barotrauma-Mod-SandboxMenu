namespace SandboxMenu;

public sealed partial class Plugin : IBarotraumaPlugin
{
    public void Init()
    {
        InitProjSpecific();

        ConstructorHelper.RunConstructors();
    }

    public void OnContentLoaded()
    {
    }

    public void Dispose() => DisposeProjSpecific();

    partial void InitProjSpecific();

    partial void DisposeProjSpecific();
}
