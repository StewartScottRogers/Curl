using System.Diagnostics.CodeAnalysis;
using Curl.Output;

namespace Curl.Console;

/// <summary>
/// The <see cref="IWriteOutFileOpener" /> <see cref="CurlCommandRunner" /> renders through: it
/// opens each <c>%output{file}</c> with <paramref name="inner" /> and wraps the file in a
/// <see cref="WriteFailureIgnoringStream" />, as curl 8.18.0 ignores a failed write or close of
/// that file (<c>%output{/dev/full}</c> exits 0 with nothing on standard error; CGR-026, BL-3830).
/// </summary>
/// <param name="inner">Opens the files.</param>
internal sealed class WriteFailureIgnoringWriteOutFileOpener(IWriteOutFileOpener inner) : IWriteOutFileOpener
{
    /// <inheritdoc />
    public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
    {
        if (!inner.TryOpen(path, append, out Stream? file))
        {
            stream = null;
            return false;
        }

        stream = new WriteFailureIgnoringStream(file);
        return true;
    }
}
