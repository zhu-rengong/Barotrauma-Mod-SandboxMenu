global using System.Runtime.CompilerServices;
global using Barotrauma;
global using Barotrauma.Extensions;
global using Barotrauma.Plugins;
global using Microsoft.Xna.Framework;
global using SandboxMenu.Domain.Editing;
global using SandboxMenu.Domain.Model;
global using SandboxMenu.Domain.Spawning;
global using SandboxMenu.Infrastructure;
global using SandboxMenu.Networking;

// IgnoresAccessChecksTo comes from HarmonyX's MonoMod, so that reference cannot be dropped.
[assembly: IgnoresAccessChecksTo("Barotrauma")]
[assembly: IgnoresAccessChecksTo("BarotraumaCore")]
[assembly: IgnoresAccessChecksTo("DedicatedServer")]
