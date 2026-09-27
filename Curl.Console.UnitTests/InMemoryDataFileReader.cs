using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// An <see cref="IDataFileReader" /> over files held in memory, recording every path it is asked
/// to read, so config-file tests never touch the disk.
/// </summary>
internal sealed class InMemoryDataFileReader : IDataFileReader
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public List<string> PathsRead { get; } = [];

    public bool TryReadFile(string path, out byte[] contents)
    {
        PathsRead.Add(path);

        return Files.TryGetValue(path, out contents!);
    }

    public byte[] ReadStandardInput() => [];

    public bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason)
    {
        modificationTime = default;
        failureReason = "not held in memory";

        return false;
    }
}
