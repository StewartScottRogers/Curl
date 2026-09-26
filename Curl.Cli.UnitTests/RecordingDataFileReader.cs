namespace Curl.Cli;

/// <summary>
/// An <see cref="IDataFileReader"/> that answers from memory and records every read, so parser
/// tests never touch the disk or standard input. A file not in <see cref="Files"/> cannot be read.
/// </summary>
internal sealed class RecordingDataFileReader : IDataFileReader
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public byte[] StandardInput { get; init; } = [];

    public List<string> Reads { get; } = [];

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
}
