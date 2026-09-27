namespace Curl.Cli;

/// <summary>
/// An <see cref="IDataFileReader"/> that answers from memory and records every read, so parser
/// tests never touch the disk or standard input. A file not in <see cref="Files"/> cannot be read;
/// a modification time not in <see cref="ModificationTimes"/> fails with the reason in
/// <see cref="ModificationTimeFailures"/>, or as file not found.
/// </summary>
internal sealed class RecordingDataFileReader : IDataFileReader
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public byte[] StandardInput { get; init; } = [];

    public List<string> Reads { get; } = [];

    public Dictionary<string, DateTimeOffset> ModificationTimes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ModificationTimeFailures { get; } = new(StringComparer.Ordinal);

    public bool TryReadFile(string path, out byte[] contents)
    {
        Reads.Add(path);
        if (Files.TryGetValue(path, out byte[]? found))
        {
            contents = found;
            return true;
        }

        contents = [];
        return false;
    }

    public byte[] ReadStandardInput()
    {
        Reads.Add("-");
        return StandardInput;
    }

    public bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason)
    {
        Reads.Add(path);
        failureReason = ModificationTimeFailures.GetValueOrDefault(path);
        return ModificationTimes.TryGetValue(path, out modificationTime);
    }
}
