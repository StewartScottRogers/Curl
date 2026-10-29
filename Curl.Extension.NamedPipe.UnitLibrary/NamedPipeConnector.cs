using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>
/// Opens every connection of a transfer over one named pipe, whatever host and port its URL names, as
/// <c>--unix-socket</c> does over a socket (ADR-1021).
/// </summary>
/// <param name="name">The pipe.</param>
/// <param name="opener">Opens the pipe.</param>
internal sealed class NamedPipeConnector(NamedPipeName name, INamedPipeOpener opener) : IConnector
{
    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UseTls)
        {
            return ConnectResult.Failed(CurlExitCode.NotBuiltIn, $"TLS over the named pipe {name} is not built in");
        }

        try
        {
            Stream pipe = await opener.OpenAsync(name, cancellationToken).ConfigureAwait(false);
            return ConnectResult.Connected(new NamedPipeConnection(pipe));
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to the named pipe {name}: {exception.Message}");
        }
    }
}
