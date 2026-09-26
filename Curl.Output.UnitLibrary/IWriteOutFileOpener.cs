using System.Diagnostics.CodeAnalysis;

namespace Curl.Output;

/// <summary>
/// Opens the file a <c>%output{file}</c> or <c>%output{&gt;&gt;file}</c> directive in a
/// <c>-w</c> template sends the rest of the output to.
/// </summary>
/// <remarks>
/// As curl does, the renderer opens the next file before it closes the current one, having
/// flushed it; for <c>%output{f}A%output{&gt;&gt;f}B</c> both handles are open on the same
/// file at once, so a disk implementation must share it for writing
/// (<see cref="FileShare.ReadWrite"/>).
/// </remarks>
public interface IWriteOutFileOpener
{
    /// <summary>
    /// Opens <paramref name="path"/> for writing, truncating it, or appending to it when
    /// <paramref name="append"/> is set, and creating it when it does not exist.
    /// </summary>
    /// <param name="path">The file name exactly as written in the template.</param>
    /// <param name="append"><see langword="true"/> for <c>%output{&gt;&gt;file}</c>.</param>
    /// <param name="stream">The open stream, which the caller disposes, when the file could be opened.</param>
    /// <returns><see langword="false"/> when the file cannot be opened; curl then keeps writing where it was.</returns>
    bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream);
}
