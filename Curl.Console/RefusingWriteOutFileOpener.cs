using System.Diagnostics.CodeAnalysis;
using Curl.Output;

namespace Curl.Console;

/// <summary>
/// The <see cref="IWriteOutFileOpener" /> <see cref="CurlCommandRunner" /> uses when it is given
/// none: it opens no file, so a <c>%output{file}</c> in a <c>-w</c> template leaves the output
/// where it was, as curl does for a file it cannot open. Opening the files on disk is BL-280.
/// </summary>
internal sealed class RefusingWriteOutFileOpener : IWriteOutFileOpener
{
    /// <inheritdoc />
    /// <returns>Always <see langword="false" />.</returns>
    public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
    {
        stream = null;
        return false;
    }
}
