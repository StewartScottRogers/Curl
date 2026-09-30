using System.Buffers.Binary;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The client side of SSH user authentication (RFC 4252, RFC 4256) as curl 8.21.0 drives
/// libssh2 1.11.1 through it (ADR-0215): the <c>ssh-userauth</c> service request, which
/// libssh2 sends as the last step of starting the session, before the host key is
/// checked; then, after the check, a <c>none</c> request for the server's method list,
/// <c>publickey</c> (ADR-0230), <c>password</c> and <c>keyboard-interactive</c>, in that
/// order.
/// </summary>
/// <remarks>
/// While it waits for an answer it skips every other message, as libssh2 queues them, and
/// answers a <c>KEXINIT</c> with a key re-exchange. <c>SSH_MSG_USERAUTH_BANNER</c> is
/// among the skipped: curl never shows it. An <c>SSH_MSG_EXT_INFO</c> is skipped too, after
/// its <c>server-sig-algs</c>, if any, is kept for choosing an RSA key's signature algorithm.
/// </remarks>
/// <param name="transport">The transport, after its first key exchange.</param>
/// <param name="credentialEncoding">
/// How the user name and password become bytes: the system ANSI code page on Windows and
/// UTF-8 elsewhere, as the platform's curl sends them (ADR-0022).
/// </param>
/// <param name="userKeys">
/// The user's key files for <c>publickey</c>, or <see langword="null" /> to skip the method.
/// </param>
/// <param name="events">
/// Where curl's <c>-v</c> lines for each method go (ADR-0262), or <see langword="null" /> for nowhere.
/// </param>
internal sealed class SshUserAuthentication(SshTransport transport, Encoding credentialEncoding, SshUserKeySource? userKeys = null, ITransferEvents? events = null)
{
    private readonly ITransferEvents events = events ?? NoTransferEvents.Instance;

    /// <summary>The service the client requests before authenticating.</summary>
    internal const string UserAuthService = "ssh-userauth";

    /// <summary>The service every authentication request asks to start once it succeeds.</summary>
    internal const string ConnectionService = "ssh-connection";

    /// <summary>The <c>none</c> method, which asks for the server's method list.</summary>
    internal const string NoneMethod = "none";

    /// <summary>The <c>publickey</c> method (RFC 4252 section 7).</summary>
    internal const string PublicKeyMethod = "publickey";

    /// <summary>The <c>password</c> method (RFC 4252 section 8).</summary>
    internal const string PasswordMethod = "password";

    /// <summary>The <c>keyboard-interactive</c> method (RFC 4256).</summary>
    internal const string KeyboardInteractiveMethod = "keyboard-interactive";

    /// <summary>
    /// The most prompts one <c>keyboard-interactive</c> round may carry: measured
    /// 2026-09-29, 100 are answered and 101 end the method.
    /// </summary>
    internal const int MaximumPrompts = 100;

    private const string ServerSignatureAlgorithmsExtension = "server-sig-algs";

    // The last server-sig-algs value an SSH_MSG_EXT_INFO carried, or null before one did.
    private string? serverSignatureAlgorithms;

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
    /// <c>none</c>, then tries <c>publickey</c>, <c>password</c> and
    /// <c>keyboard-interactive</c> when the list names them, as curl matches it, by
    /// substring. A partial success counts as a failure, and the list is never read again.
    /// A key that cannot be read, has no signature algorithm the server accepts, or is
    /// refused fails <c>publickey</c> alone, and curl goes on to <c>password</c>.
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
        if (methods is null)
        {
            events.ReportInfo(SshInfoLines.AcceptedWithoutAuthentication);
            return;
        }

        events.ReportInfo(SshInfoLines.OffersAuthentication(methods));
        await AuthenticateWithMethodsAsync(methods, user, password, cancellationToken).ConfigureAwait(false);
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
        bool offersPublicKey = methods.Contains(PublicKeyMethod, StringComparison.Ordinal);
        if (offersPublicKey && await TryPublicKeyAsync(user, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (methods.Contains(PasswordMethod, StringComparison.Ordinal) && await TryPasswordAsync(user, password, cancellationToken).ConfigureAwait(false))
        {
            events.ReportInfo(SshInfoLines.PasswordAuthenticated);
            return;
        }

        // curl then asks the SSH agent, which this client never finds, as the reference
        // machine found none (ADR-0262).
        if (offersPublicKey)
        {
            events.ReportInfo(SshInfoLines.TryingAgent);
            events.ReportInfo(SshInfoLines.AgentConnectFailed);
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

        events.ReportInfo(SshInfoLines.KeyboardInteractiveAuthenticated);
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

    // libssh2 first asks whether the key would do, without a signature; PK_OK is answered
    // with the signed request, and SUCCESS to the question authenticates at once. The
    // public key comes from --pubkey or the private key, so with --pubkey the question is
    // asked even when the private key cannot be read. The -v lines name the key files, then
    // the outcome with libssh2's reason for a denial (ADR-0262).
    private async ValueTask<bool> TryPublicKeyAsync(byte[] user, CancellationToken cancellationToken)
    {
        if (userKeys is null)
        {
            return false;
        }

        SshUserKeyFiles files = await userKeys.LocateAsync(cancellationToken).ConfigureAwait(false);
        if (files.PublicKeyPath is { } publicKeyPath)
        {
            events.ReportInfo(SshInfoLines.TryingPublicKeyFile(publicKeyPath));
        }

        events.ReportInfo(SshInfoLines.TryingPrivateKeyFile(files.PrivateKeyPath));
        string? denial = await DenyPublicKeyAsync(user, files, cancellationToken).ConfigureAwait(false);
        events.ReportInfo(denial is null ? SshInfoLines.AuthenticatedViaPublicKey : SshInfoLines.PublicKeyDenied(denial));
        return denial is null;
    }

    // Null when the key authenticated the user; libssh2's reason otherwise.
    private async ValueTask<string?> DenyPublicKeyAsync(byte[] user, SshUserKeyFiles files, CancellationToken cancellationToken)
    {
        SshPublicKey? publicKey = await userKeys!.ReadPublicKeyAsync(files, cancellationToken).ConfigureAwait(false);
        string? algorithm = publicKey is null ? null : SignatureAlgorithmFor(publicKey.KeyType);
        return algorithm is null
            ? SshInfoLines.ReasonUnknown
            : await AskPublicKeyQuestionAsync(user, algorithm, publicKey!, files, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<string?> AskPublicKeyQuestionAsync(byte[] user, string algorithm, SshPublicKey publicKey, SshUserKeyFiles files, CancellationToken cancellationToken)
    {
        byte[]? answer = await TryExchangeAsync(
            PublicKeyRequest(user, algorithm, publicKey.Blob, signed: false),
            [SshAuthenticationMessageNumber.Success, SshAuthenticationMessageNumber.Failure, SshAuthenticationMessageNumber.PublicKeyOk],
            cancellationToken).ConfigureAwait(false);
        byte answerType = MessageTypeOf(answer);
        if (answerType == SshAuthenticationMessageNumber.PublicKeyOk)
        {
            return await SendSignedPublicKeyAsync(user, algorithm, publicKey, files, cancellationToken).ConfigureAwait(false);
        }

        return answerType == SshAuthenticationMessageNumber.Success ? null : SshInfoLines.PublicKeyCombinationInvalid;
    }

    // The answer's message number, or 0, which no message has, when the method ended without one.
    private static byte MessageTypeOf(byte[]? answer) => answer is null ? (byte)0 : answer[0];

    // RFC 4252 section 7: the signature covers the session identifier as a string, then the
    // request up to and including the public key blob. A private key that cannot be read or
    // is not of the public key's type fails the method before anything is sent.
    private async ValueTask<string?> SendSignedPublicKeyAsync(byte[] user, string algorithm, SshPublicKey publicKey, SshUserKeyFiles files, CancellationToken cancellationToken)
    {
        SshPrivateKey? privateKey = await userKeys!.ReadPrivateKeyAsync(files, cancellationToken).ConfigureAwait(false);
        if (privateKey?.KeyType != publicKey.KeyType)
        {
            return SshInfoLines.SignCallbackFailed;
        }

        byte[] request = PublicKeyRequest(user, algorithm, publicKey.Blob, signed: true);
        SshWireWriter signedData = new();
        signedData.WriteString(transport.SessionIdentifier);
        signedData.WriteBytes(request);
        SshWireWriter message = new();
        message.WriteBytes(request);
        message.WriteString(privateKey!.Sign(algorithm, signedData.ToArray()));
        byte[]? answer = await TryExchangeAsync(
            message.ToArray(),
            [SshAuthenticationMessageNumber.Success, SshAuthenticationMessageNumber.Failure],
            cancellationToken).ConfigureAwait(false);
        return answer?[0] == SshAuthenticationMessageNumber.Success ? null : SshInfoLines.SignatureRefused;
    }

    // libssh2 upgrades only an ssh-rsa key: once the server has sent server-sig-algs, the
    // first of its own RSA algorithms the server names, compared whole, and none when it
    // names none of them; before, ssh-rsa itself. Other key types sign as their type.
    private string? SignatureAlgorithmFor(string keyType)
    {
        if (keyType != RsaSshPrivateKey.RsaKeyType || serverSignatureAlgorithms is null)
        {
            return keyType;
        }

        string[] accepted = serverSignatureAlgorithms.Split(',');
        return RsaSshPrivateKey.SignatureAlgorithms.FirstOrDefault(accepted.Contains);
    }

    // The publickey request: the signature flag, the algorithm and the public key blob; the
    // signature, when there is one, follows.
    private static byte[] PublicKeyRequest(byte[] user, string algorithm, byte[] publicKeyBlob, bool signed) =>
        Request(
            user,
            PublicKeyMethod,
            fields =>
            {
                fields.WriteBoolean(signed);
                fields.WriteString(Encoding.ASCII.GetBytes(algorithm));
                fields.WriteString(publicKeyBlob);
            });

    // RFC 8308 section 2.3: a count, then name and value strings. libssh2 reads it only when
    // it is at least five bytes long, keeps the last server-sig-algs, and stops at a pair
    // cut short.
    private void KeepServerSignatureAlgorithms(byte[] extensionInfo)
    {
        SshWireReader reader = new(extensionInfo.AsMemory(1));
        try
        {
            for (uint count = reader.ReadUInt32(); count > 0; count--)
            {
                string name = Encoding.Latin1.GetString(reader.ReadString().Span);
                string value = Encoding.Latin1.GetString(reader.ReadString().Span);
                serverSignatureAlgorithms = name == ServerSignatureAlgorithmsExtension ? value : serverSignatureAlgorithms;
            }
        }
        catch (InvalidDataException)
        {
            // A pair cut short ends the list; what came before it stands.
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
    // and a KEXINIT is answered with a key re-exchange first. A success starts the delayed
    // compression the transport agreed before the next packet either way.
    private async ValueTask<byte[]> ReadAnswerAsync(byte[] wanted, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] payload = await transport.PacketReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (payload[0] == SshAuthenticationMessageNumber.Success)
            {
                transport.StartDelayedCompression();
            }

            if (payload[0] == SshMessageNumber.Disconnect || wanted.AsSpan().Contains(payload[0]))
            {
                return payload;
            }

            if (payload[0] == SshMessageNumber.KeyExchangeInit)
            {
                await transport.ReExchangeKeysAsync(payload, cancellationToken).ConfigureAwait(false);
            }

            if (payload[0] == SshMessageNumber.ExtensionInfo)
            {
                KeepServerSignatureAlgorithms(payload);
            }
        }
    }
}
