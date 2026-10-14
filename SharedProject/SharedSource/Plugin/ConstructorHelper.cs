using System.Reflection;

namespace SandboxMenu;

internal static class ConstructorHelper
{
    internal static void RunConstructors()
    {
        foreach (Type type in typeof(Plugin).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<RunConstructorOnInitAttribute>() is null) { continue; }

            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }
}
