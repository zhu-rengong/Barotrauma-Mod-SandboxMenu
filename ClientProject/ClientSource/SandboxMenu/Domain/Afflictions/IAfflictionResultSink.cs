namespace SandboxMenu.Domain.Afflictions;

internal interface IAfflictionResultSink
{
    void OnAfflictionResult(AfflictionStatus status);
}
