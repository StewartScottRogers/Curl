using System.Diagnostics.CodeAnalysis;
using Curl.Output;

namespace Curl.Console;

/// <summary>
/// The <see cref="IWriteOutFileOpener" /> the executable uses: it opens each <c>%output{file}</c>
/// target of a <c>-w</c> template on disk, truncating it, or appending to it for
/// <c>%output{&gt;&gt;file}</c>, and shares it for writing, because curl 8.21.0 opens the same
/// file twice in <c>%output{f}A%output{&gt;&gt;f}B</c>. A file it cannot open is refused
/// without throwing, so the output stays where it was, as curl leaves it.
/// </summary>
/// <param name="writesLineFeedAsCrLf">
/// Whether each opened file is written in text mode, every line feed as CR LF through
/// <see cref="LineFeedToCrLfStream" />, as curl 8.21.0 writes these files on Windows (ADR-0081).
/// </param>
internal sealed class DiskWriteOutFileOpener(bool writesLineFeedAsCrLf) : IWriteOutFileOpener
{
    /// <inheritdoc />
    public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
    {
        try
        {
            FileStream file = new(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            stream = writesLineFeedAsCrLf ? new LineFeedToCrLfStream(file, ownsInner: true) : file;

            return true;
        }
        catch (Exception exception) when (IsOpenFailure(exception))
        {
            stream = null;

            return false;
        }
    }

    /// <summary>
    /// Tells whether <paramref name="exception" /> is one opening a <see cref="FileStream" />
    /// raises for a file the operating system will not open for writing.
    /// </summary>
    /// <param name="exception">The exception the open threw.</param>
    /// <returns>
    /// <see langword="true" /> for an <see cref="IOException" /> (a missing directory included),
    /// an <see cref="UnauthorizedAccessException" /> (a directory on Windows) or an
    /// <see cref="ArgumentException" /> (an empty name).
    /// </returns>
    internal static bool IsOpenFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException;
}
