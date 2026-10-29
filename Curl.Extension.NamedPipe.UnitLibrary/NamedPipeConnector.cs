using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>
/// Opens every connection of a transfer over one named pipe, whatever host and port its URL names, as
/// <c>--unix-socket</c> does over a socket (ADR-1021), and runs curl's TLS handshake over the pipe for a TLS
/// target (ADR-1049).
/// </summary>
/// <param name="name">The pipe.</param>
/// <param name="opener">Opens the pipe.</param>
/// <param name="tlsProvider">
/// The TLS provider curl built from the option group's TLS options, layered over the pipe for a TLS target; with
/// <see langword="null" />, a host that hands none, a TLS target is refused as not built in.
/// </param>
internal sealed class NamedPipeConnector(NamedPipeName name, INamedPipeOpener opener, ITlsProvider? tlsProvider = null) : IConnector
{
    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UseTls && tlsProvider is null)
        {
            return ConnectResult.Failed(CurlExitCode.NotBuiltIn, $"TLS over the named pipe {name} is not built in");
        }

        Stream pipe;
        try
        {
            pipe = await opener.OpenAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to the named pipe {name}: {exception.Message}");
        }

        NamedPipeConnection connection = new(pipe);
        return target.UseTls
            ? await tlsProvider!.AuthenticateAsClientAsync(connection, target.TlsServerName ?? target.Host, target.Events, cancellationToken).ConfigureAwait(false)
            : ConnectResult.Connected(connection);
    }
}
