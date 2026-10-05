global using System.Runtime.CompilerServices;
global using Barotrauma;
global using Barotrauma.Extensions;
global using Microsoft.Xna.Framework;
global using UiFramework.Data;
global using UiFramework.Styling;

// The framework reaches the host's internals the same way the mod does; it never references the mod's own assembly.
[assembly: IgnoresAccessChecksTo("Barotrauma")]
[assembly: IgnoresAccessChecksTo("BarotraumaCore")]
[assembly: IgnoresAccessChecksTo("DedicatedServer")]
