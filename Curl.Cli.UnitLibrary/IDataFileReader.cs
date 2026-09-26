namespace Curl.Cli;

/// <summary>
/// Reads the file a <c>-d @file</c> / <c>--data @file</c> or <c>-H @file</c> / <c>--header @file</c> value names, or standard input for
/// <c>@-</c>, for <see cref="CommandLineParser"/>; injected so the parser never touches the disk or
/// the console and tests can supply the bytes.
/// </summary>
public interface IDataFileReader
{
    /// <summary>Reads every byte of the file at <paramref name="path"/>.</summary>
    /// <param name="path">The path as typed after <c>@</c>, possibly empty.</param>
    /// <param name="contents">The file's bytes when it was read; otherwise empty.</param>
    /// <returns><see langword="true"/> when the file was read; <see langword="false"/> when it cannot be opened or read.</returns>
    bool TryReadFile(string path, out byte[] contents);

    /// <summary>Reads standard input to its end.</summary>
    /// <returns>Every byte read; empty when standard input is empty or absent.</returns>
    /// <exception cref="IOException">Standard input could not be read.</exception>
    byte[] ReadStandardInput();
}
