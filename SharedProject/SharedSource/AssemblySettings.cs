global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Collections.Concurrent;
global using System.Collections.Immutable;
global using System.Linq;
global using System.Reflection;
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

// The attribute type itself comes with the reference set: the base class library only ships it inside the runtime
// pack, where the compiler cannot see it, so what declares it is HarmonyX's MonoMod. The runtime matches the
// attribute by name, so all that matters is that these three lines are here — which is also why the HarmonyX
// reference cannot simply be dropped.
[assembly: IgnoresAccessChecksTo("Barotrauma")]
[assembly: IgnoresAccessChecksTo("BarotraumaCore")]
[assembly: IgnoresAccessChecksTo("DedicatedServer")]

namespace SandboxMenu;
