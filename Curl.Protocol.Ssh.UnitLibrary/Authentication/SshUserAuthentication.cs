using System.Buffers.Binary;
using System.Net;
using System.Text;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The client side of SSH user authentication (RFC 4252, RFC 4256) as curl 8.21.0 drives
/// libssh2 1.11.1 through it (ADR-0214): the <c>ssh-userauth</c> service request, which
/// libssh2 sends as the last step of starting the session, before the host key is
/// checked; then, after the check, a <c>none</c> request for the server's method list,
/// <c>password</c> and <c>keyboard-interactive</c>, in that order.
/// </summary>
/// <remarks>
/// While it waits for an answer it skips every other message, as libssh2 queues them, and
/// answers a <c>KEXINIT</c> with a key re-exchange. <c>SSH_MSG_USERAUTH_BANNER</c> is
/// among the skipped: curl never shows it.
/// </remarks>
/// <param name="transport">The transport, after its first key exchange.</param>
/// <param name="credentialEncoding">
/// How the user name and password become bytes: the system ANSI code page on Windows and
/// UTF-8 elsewhere, as the platform's curl sends them (ADR-0022).
/// </param>
internal sealed class SshUserAuthentication(SshTransport transport, Encoding credentialEncoding)
{
    /// <summary>The service the client requests before authenticating.</summary>
    internal const string UserAuthService = "ssh-userauth";

    /// <summary>The service every authentication request asks to start once it succeeds.</summary>
    internal const string ConnectionService = "ssh-connection";

    /// <summary>The <c>none</c> method, which asks for the server's method list.</summary>
    internal const string NoneMethod = "none";

    /// <summary>The <c>password</c> method (RFC 4252 section 8).</summary>
    internal const string PasswordMethod = "password";

    /// <summary>The <c>keyboard-interactive</c> method (RFC 4256).</summary>
    internal const string KeyboardInteractiveMethod = "keyboard-interactive";

    /// <summary>
    /// The most prompts one <c>keyboard-interactive</c> round may carry: measured
    /// 2026-09-29, 100 are answered and 101 end the method.
    /// </summary>
    internal const int MaximumPrompts = 100;

    /// <summary>
    /// Sends <c>SSH_MSG_SERVICE_REQUEST</c> for <c>ssh-userauth</c> and waits for the
    /// server's <c>SSH_MSG_SERVICE_ACCEPT</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the server accepts.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 2, <c>Failure establishing ssh session: &lt;code&gt;, Failed to get response to
    /// ssh-userauth request</c>, with <c>-43</c> when the peer closes or breaks the
    /// framing, <c>-13</c> when it disconnects, and <c>-4</c> or <c>-12</c> when the answer
    /// fails its MAC or tag; with <c>-14, Unexpected packet length</c> for an answer shorter
    /// than five bytes and <c>-14, Invalid response received from server</c> for one naming
    /// another service.
    /// </exception>
    internal async ValueTask RequestServiceAsync(CancellationToken cancellationToken)
    {
        SshWireWriter request = new();
        request.WriteByte(SshMessageNumber.ServiceRequest);
        request.WriteString(Encoding.ASCII.GetBytes(UserAuthService));
        await transport.PacketWriter.WriteAsync(request.ToArray(), cancellationToken).ConfigureAwait(false);
        CheckServiceAccept(await ReadServiceAnswerAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Authenticates <paramref name="credentials" />' user: asks for the method list with
    /// <c>none</c>, then tries <c>password</c> and <c>keyboard-interactive</c> when the list
    /// names them, as curl matches it, by substring. A partial success counts as a
    /// failure, and the list is never read again.
    /// </summary>
    /// <param name="credentials">The user and password, or <see langword="null" /> for curl's empty user and password.</param>
    /// <param name="cancellationToken">Cancels the authentication.</param>
    /// <returns>A task that completes once the server reports success.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 79, <c>Error in the SSH layer</c>, when the answer to <c>none</c> cannot be read;
    /// exit 67, <c>Login denied</c>, when <c>keyboard-interactive</c> was tried and failed;
    /// exit 67, <c>Authentication failure</c>, when every other method failed.
    /// </exception>
    internal async ValueTask AuthenticateAsync(NetworkCredential? credentials, CancellationToken cancellationToken)
    {
        byte[] user = credentialEncoding.GetBytes(credentials?.UserName ?? string.Empty);
        byte[] password = credentialEncoding.GetBytes(credentials?.Password ?? string.Empty);
        string? methods = await ListMethodsAsync(user, cancellationToken).ConfigureAwait(false);
        if (methods is not null)
        {
            await AuthenticateWithMethodsAsync(methods, user, password, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SshTransferException ServiceRequestFailed(int libssh2ErrorCode) =>
        SshTransferException.SessionEstablishmentFailed(libssh2ErrorCode, Libssh2ErrorCode.FailedToGetUserAuthResponse);

    // One round's outcome: the answer to send next, or none once the method has ended,
    // with whether it ended in success.
    private static (byte[]? NextMessage, bool Succeeded) NextKeyboardInteractiveStep(byte[]? answer, byte[] password)
    {
        if (answer is null || answer[0] != SshAuthenticationMessageNumber.InfoRequest)
        {
            return (null, answer is not null && answer[0] == SshAuthenticationMessageNumber.Success);
        }

        int? promptCount = CountPrompts(answer);
        return promptCount is null ? (null, false) : (InfoResponse(promptCount.Value, password), false);
    }

    private async ValueTask AuthenticateWithMethodsAsync(string methods, byte[] user, byte[] password, CancellationToken cancellationToken)
    {
        if (methods.Contains(PasswordMethod, StringComparison.Ordinal) && await TryPasswordAsync(user, password, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await RequireKeyboardInteractiveAsync(methods, user, password, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RequireKeyboardInteractiveAsync(string methods, byte[] user, byte[] password, CancellationToken cancellationToken)
    {
        if (!methods.Contains(KeyboardInteractiveMethod, StringComparison.Ordinal))
        {
            throw SshTransferException.AuthenticationFailure();
        }

        if (!await TryKeyboardInteractiveAsync(user, password, cancellationToken).ConfigureAwait(false))
        {
            throw SshTransferException.LoginDenied();
        }
    }

    // libssh2 checks the length first, then the service name's length field and bytes.
    private static void CheckServiceAccept(byte[] answer)
    {
        if (answer.Length < 5)
        {
            throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.Protocol, Libssh2ErrorCode.UnexpectedPacketLength);
        }

        ReadOnlySpan<byte> service = "ssh-userauth"u8;
        if (BinaryPrimitives.ReadUInt32BigEndian(answer.AsSpan(1)) != service.Length || !answer.AsSpan(5).StartsWith(service))
        {
            throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.Protocol, Libssh2ErrorCode.InvalidResponseReceivedFromServer);
        }
    }

    // RFC 4256 section 3.2: name, instruction, language tag, then the prompts, each a
    // string and an echo flag. Null when the request is malformed or has too many prompts.
    private static int? CountPrompts(byte[] infoRequest)
    {
        SshWireReader reader = new(infoRequest.AsMemory(1));
        try
        {
            reader.ReadString();
            reader.ReadString();
            reader.ReadString();
            uint count = reader.ReadUInt32();
            for (uint index = 0; index < count && count <= MaximumPrompts; index++)
            {
                reader.ReadString();
                reader.ReadBoolean();
            }

            return count <= MaximumPrompts ? (int)count : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // curl answers the password to a round of exactly one prompt, and an empty string to
    // every prompt of any other round.
    private static byte[] InfoResponse(int promptCount, byte[] password)
    {
        SshWireWriter response = new();
        response.WriteByte(SshAuthenticationMessageNumber.InfoResponse);
        response.WriteUInt32((uint)promptCount);
        for (int index = 0; index < promptCount; index++)
        {
            response.WriteString(promptCount == 1 ? password : []);
        }

        return response.ToArray();
    }

    private async ValueTask<byte[]> ReadServiceAnswerAsync(CancellationToken cancellationToken)
    {
        byte[] answer;
        try
        {
            answer = await ReadAnswerAsync([SshMessageNumber.ServiceAccept], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException)
        {
            throw ServiceRequestFailed(Libssh2ErrorCode.SocketReceive);
        }
        catch (SshPacketAuthenticationException exception)
        {
            throw ServiceRequestFailed(exception.Libssh2ErrorCode);
        }

        return answer[0] == SshMessageNumber.Disconnect ? throw ServiceRequestFailed(Libssh2ErrorCode.SocketDisconnect) : answer;
    }

    // Null when the answer to "none" is success; the method list as curl reads it otherwise.
    private async ValueTask<string?> ListMethodsAsync(byte[] user, CancellationToken cancellationToken)
    {
        byte[] answer = await TryExchangeAsync(
            Request(user, NoneMethod, _ => { }),
            [SshAuthenticationMessageNumber.Success, SshAuthenticationMessageNumber.Failure],
            cancellationToken).ConfigureAwait(false)
            ?? throw SshTransferException.SshLayerError();
        if (answer[0] == SshAuthenticationMessageNumber.Success)
        {
            return null;
        }

        try
        {
            return Encoding.Latin1.GetString(new SshWireReader(answer.AsMemory(1)).ReadString().Span);
        }
        catch (InvalidDataException)
        {
            throw SshTransferException.SshLayerError();
        }
    }

    // A password change request, a failure (partial success included), a disconnect or a
    // close all fail the method, and curl goes on to the next.
    private async ValueTask<bool> TryPasswordAsync(byte[] user, byte[] password, CancellationToken cancellationToken)
    {
        byte[] request = Request(
            user,
            PasswordMethod,
            fields =>
            {
                fields.WriteBoolean(false);
                fields.WriteString(password);
            });
        byte[]? answer = await TryExchangeAsync(
            request,
            [SshAuthenticationMessageNumber.Success, SshAuthenticationMessageNumber.Failure, SshAuthenticationMessageNumber.PasswordChangeRequest],
            cancellationToken).ConfigureAwait(false);
        return answer?[0] == SshAuthenticationMessageNumber.Success;
    }

    // The request carries an empty language tag and empty submethods; each round's answer
    // is sent until the server reports success or anything else ends the method.
    private async ValueTask<bool> TryKeyboardInteractiveAsync(byte[] user, byte[] password, CancellationToken cancellationToken)
    {
        byte[] message = Request(
            user,
            KeyboardInteractiveMethod,
            fields =>
            {
                fields.WriteString([]);
                fields.WriteString([]);
            });
        byte[]? nextMessage = message;
        bool succeeded = false;
        while (nextMessage is not null)
        {
            byte[]? answer = await TryExchangeAsync(
                nextMessage,
                [SshAuthenticationMessageNumber.Success, SshAuthenticationMessageNumber.Failure, SshAuthenticationMessageNumber.InfoRequest],
                cancellationToken).ConfigureAwait(false);
            (nextMessage, succeeded) = NextKeyboardInteractiveStep(answer, password);
        }

        return succeeded;
    }

    // RFC 4252 section 5: the user, the service to start, the method, then its own fields.
    private static byte[] Request(byte[] user, string method, Action<SshWireWriter> writeMethodFields)
    {
        SshWireWriter request = new();
        request.WriteByte(SshAuthenticationMessageNumber.Request);
        request.WriteString(user);
        request.WriteString(Encoding.ASCII.GetBytes(ConnectionService));
        request.WriteString(Encoding.ASCII.GetBytes(method));
        writeMethodFields(request);
        return request.ToArray();
    }

    // Sends one message and reads the answer. Null when the peer closes, disconnects,
    // breaks the framing or fails a packet's check: libssh2 fails the method in hand, and
    // curl carries on with the next.
    private async ValueTask<byte[]?> TryExchangeAsync(byte[] message, byte[] wanted, CancellationToken cancellationToken)
    {
        try
        {
            await transport.PacketWriter.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            byte[] answer = await ReadAnswerAsync(wanted, cancellationToken).ConfigureAwait(false);
            return answer[0] == SshMessageNumber.Disconnect ? null : answer;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SshPacketAuthenticationException)
        {
            return null;
        }
    }

    // The first message of a wanted type, or a disconnect; every other message is skipped,
    // and a KEXINIT is answered with a key re-exchange first.
    private async ValueTask<byte[]> ReadAnswerAsync(byte[] wanted, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] payload = await transport.PacketReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (payload[0] == SshMessageNumber.Disconnect || wanted.AsSpan().Contains(payload[0]))
            {
                return payload;
            }

            if (payload[0] == SshMessageNumber.KeyExchangeInit)
            {
                await transport.ReExchangeKeysAsync(payload, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
