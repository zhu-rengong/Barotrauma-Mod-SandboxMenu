namespace SandboxMenu.Domain.Spawning;

internal interface ISpawnResultSink
{
    void OnSpawnResult(SpawnStatus status, int queued, int problems);
}
