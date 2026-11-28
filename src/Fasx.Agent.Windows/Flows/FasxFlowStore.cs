using System.IO;
using System.Text.Json;
using Fasx.Agent.Core.Flows;

namespace Fasx.Agent.Windows.Flows;

internal sealed class FasxFlowStore
{
    private const long MaximumStoreBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FASX",
        "flows-v1.json");

    public IReadOnlyList<FasxFlow> Load()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            var info = new FileInfo(_path);
            if (info.Length is <= 0 or > MaximumStoreBytes) return [];

            var content = File.ReadAllText(_path);
            var flows = JsonSerializer.Deserialize<List<FasxFlow>>(content, JsonOptions);
            if (flows is null || flows.Count > FasxFlowPolicy.MaximumFlows) return [];

            var valid = flows
                .Where(FasxFlowPolicy.IsValid)
                .GroupBy(flow => flow.Id)
                .Select(group => group.First())
                .Take(FasxFlowPolicy.MaximumFlows)
                .ToArray();
            return valid;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<FasxFlow> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);
        if (flows.Count > FasxFlowPolicy.MaximumFlows ||
            flows.Any(flow => !FasxFlowPolicy.IsValid(flow)) ||
            flows.Select(flow => flow.Id).Distinct().Count() != flows.Count)
        {
            throw new InvalidOperationException("The flow collection is invalid or exceeds the FASX limit.");
        }

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The FASX flow storage path is invalid.");
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(flows, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumStoreBytes)
        {
            throw new InvalidOperationException("The saved flows exceed the local storage limit.");
        }

        var temporary = Path.Combine(directory, $".flows-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
        }
    }
}
