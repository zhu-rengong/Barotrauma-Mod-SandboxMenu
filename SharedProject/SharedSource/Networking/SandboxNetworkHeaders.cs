namespace SandboxMenu.Networking;

// The game derives this plugin's opcode range from the values of the enum it registers, so the two halves must
// register the same declaration: both compile this one file, and renumbering it desynchronises client and server.
public enum SandboxNetworkHeaders
{
    SpawnRequest = 0,
    SpawnResponse = 1
}
