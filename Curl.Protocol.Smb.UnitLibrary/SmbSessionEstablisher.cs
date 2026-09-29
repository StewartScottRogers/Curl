using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Opens an SMBv1 session over a connection as curl 8.21.0 does (ADR-0200): sends the
/// negotiate request, answers the negotiate response's challenge with a session setup
/// carrying NTLMv1 responses, and takes the UID the session setup response assigns.
/// </summary>
/// <param name="connection">The connection to the server, already open (and in TLS for <c>smbs</c>).</param>
/// <param name="reader">Reads the server's messages from <paramref name="connection" />.</param>
/// <param name="operatingSystem">curl's host triple, sent in the session setup.</param>
internal sealed class SmbSessionEstablisher(IConnection connection, SmbMessageReader reader, string operatingSystem)
{
    /// <summary>
    /// Negotiates and sets up the session.
    /// </summary>
    /// <param name="password">The password the NTLMv1 responses are computed from.</param>
    /// <param name="identity">The user and domain to send.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>
    /// The UID and no failure once the server accepts the session setup; otherwise the
    /// failure curl reports: exit 56 for a malformed frame, exit 7 for a refused or short
    /// negotiate response, exit 63 for a session setup that would pass 1024 bytes, and
    /// exit 67 for a refused session setup.
    /// </returns>
    public async ValueTask<(ushort UserId, TransferResult? Failure)> EstablishAsync(
        string password,
        SmbIdentity identity,
        CancellationToken cancellationToken)
    {
        await SendAsync(SmbNegotiateRequest.Encode(), cancellationToken).ConfigureAwait(false);
        SmbReceivedMessage negotiate = await reader.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (negotiate.Bytes is null)
        {
            return (0, ReceiveFailure(negotiate));
        }

        if (!SmbNegotiateResponse.TryRead(negotiate.Bytes, out SmbNegotiateResponse? response))
        {
            return (0, TransferResult.Failure(CurlExitCode.CouldntConnect, SmbMessages.NegotiateFailed));
        }

        byte[]? setup = SmbSessionSetupRequest.Encode(password, identity, operatingSystem, response!);
        if (setup is null)
        {
            return (0, TransferResult.Failure(CurlExitCode.FilesizeExceeded, SmbMessages.MessageTooLarge));
        }

        await SendAsync(setup, cancellationToken).ConfigureAwait(false);
        return await ReadSetupResponseAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TransferResult ReceiveFailure(SmbReceivedMessage message) =>
        TransferResult.Failure(CurlExitCode.RecvError, message.ErrorMessage!);

    private async ValueTask<(ushort UserId, TransferResult? Failure)> ReadSetupResponseAsync(CancellationToken cancellationToken)
    {
        SmbReceivedMessage setupResponse = await reader.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (setupResponse.Bytes is null)
        {
            return (0, ReceiveFailure(setupResponse));
        }

        if (SmbMessageHeader.ReadStatus(setupResponse.Bytes) != 0)
        {
            return (0, TransferResult.Failure(CurlExitCode.LoginDenied, SmbMessages.LoginDenied));
        }

        return (SmbMessageHeader.ReadUserId(setupResponse.Bytes), null);
    }

    private async ValueTask SendAsync(byte[] message, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
