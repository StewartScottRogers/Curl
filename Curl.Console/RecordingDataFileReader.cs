using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Reads through another <see cref="IDataFileReader" /> and keeps the path of every file it was
/// asked to read, and whether it could be read, so the runner can log which files the
/// command-line parse tried (<c>.curlrc</c> candidates, <c>-K</c> and the <c>@file</c> values read
/// while parsing) once the diagnostic log is open.
/// </summary>
/// <param name="inner">The reader that reads the files.</param>
internal sealed class RecordingDataFileReader(IDataFileReader inner) : IDataFileReader
{
    private readonly List<(string Path, bool WasRead)> filesTried = [];

    /// <summary>
    /// Gets every file asked for so far, in order, with whether it could be read.
    /// </summary>
    internal IReadOnlyList<(string Path, bool WasRead)> FilesTried => filesTried;

    /// <inheritdoc />
    public bool TryReadFile(string path, out byte[] contents)
    {
        bool wasRead = inner.TryReadFile(path, out contents);
        filesTried.Add((path, wasRead));
        return wasRead;
    }

    /// <inheritdoc />
    public byte[] ReadStandardInput() => inner.ReadStandardInput();

    /// <inheritdoc />
    public bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason) =>
        inner.TryReadModificationTime(path, out modificationTime, out failureReason);
}
