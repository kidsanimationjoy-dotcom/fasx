namespace Fasx.Agent.Core.Pairing;

/// <summary>
/// Persistence boundary for trusted device public identities.
/// Implementations must protect data at rest and must not store device private keys.
/// </summary>
public interface IPairedDeviceStore
{
    IReadOnlyList<PairedDevice> Load();
    void Save(IReadOnlyCollection<PairedDevice> devices);
}
