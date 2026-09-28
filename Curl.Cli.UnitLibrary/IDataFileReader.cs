namespace Curl.Cli;

/// <summary>
/// Reads the file a <c>-d @file</c> / <c>--data @file</c>, <c>-H @file</c> / <c>--header @file</c> or
/// <c>-K file</c> / <c>--config file</c> value names, or standard input for <c>@-</c> and <c>-K -</c>,
/// and the modification time of the file a <c>-z</c> / <c>--time-cond</c> value names when it is not
/// a date, for <see cref="CommandLineParser"/>; injected so the parser never touches the disk or the
/// console and tests can supply the bytes and times.
/// </summary>
public interface IDataFileReader
{
    /// <summary>Reads every byte of the file at <paramref name="path"/>.</summary>
    /// <param name="path">The path as typed after <c>@</c>, or as given to <c>-K</c>, possibly empty.</param>
    /// <param name="contents">The file's bytes when it was read; otherwise empty.</param>
    /// <returns><see langword="true"/> when the file was read; <see langword="false"/> when it cannot be opened or read.</returns>
    bool TryReadFile(string path, out byte[] contents);

    /// <summary>Reads standard input to its end.</summary>
    /// <returns>Every byte read; empty when standard input is empty or absent.</returns>
    /// <exception cref="IOException">Standard input could not be read.</exception>
    byte[] ReadStandardInput();

    /// <summary>
    /// Reads the modification time of the file at <paramref name="path"/>, as curl 8.21.0's tool does
    /// for a <c>-z</c> / <c>--time-cond</c> value that is not a date.
    /// </summary>
    /// <param name="path">The <c>-z</c> value after any <c>-</c>, <c>+</c> or <c>=</c> prefix, possibly empty.</param>
    /// <param name="modificationTime">The file's modification time, to the whole second, when it was read; otherwise <see langword="default"/>.</param>
    /// <param name="failureReason">
    /// When the lookup failed and curl says why, the reason it prints after
    /// <c>Warning: Failed to get filetime: </c>, such as <c>CreateFile failed: GetLastError 0x00000005</c>
    /// on Windows or <c>Not a directory</c> elsewhere; otherwise <see langword="null"/>, which curl's
    /// Windows build gives for a file that does not exist.
    /// </param>
    /// <returns><see langword="true"/> when the modification time was read.</returns>
    bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason);
}
