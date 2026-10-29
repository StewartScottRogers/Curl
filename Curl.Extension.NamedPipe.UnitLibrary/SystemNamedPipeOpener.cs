using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;

namespace Curl.Extension.NamedPipe;

/// <summary>Opens a named pipe with <see cref="NamedPipeClientStream" />, failing at once when it is not there.</summary>
public sealed class SystemNamedPipeOpener : INamedPipeOpener
{
    /// <inheritdoc />
    // Excluded per ADR-0083: every line needs a live pipe, which only a server process can offer.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin named pipe adapter; every line needs a live pipe.")]
    public async ValueTask<Stream> OpenAsync(NamedPipeName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        NamedPipeClientStream pipe = new(name.ServerName, name.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            // No wait for a pipe that is not there, as --unix-socket fails at once on a missing socket (ADR-1021).
            await pipe.ConnectAsync(0, cancellationToken).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
