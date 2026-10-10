using System.Text;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The server's side of <c>ssh-userauth</c> (RFC 4252), for <see cref="SshServerClientAccount" />
/// alone: <c>none</c> and every other method fail, naming <c>publickey,password</c>;
/// <c>publickey</c> gets <c>PK_OK</c> for one of the account's keys, Ed25519 or RSA, and <c>SUCCESS</c> for its valid
/// signature; <c>password</c> succeeds with the account's password. Any other user fails.
/// </summary>
internal static class SshServerUserAuthentication
{
    /// <summary>The methods a failure names, as upstream's test <c>sshd</c> offers them.</summary>
    internal const string OfferedMethods = "publickey,password";

    /// <summary>
    /// Answers the client's authentication requests until one succeeds.
    /// </summary>
    /// <param name="transport">The session's transport, past the <c>ssh-userauth</c> service request.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The user name that authenticated.</returns>
    /// <exception cref="InvalidDataException">The client sends a message that is not an authentication request.</exception>
    internal static async ValueTask<string> AuthenticateAsync(SshServerTransport transport, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] request = await transport.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
            if (request[0] != SshAuthenticationMessageNumber.Request)
            {
                throw new InvalidDataException($"The SSH client sent message {request[0]} where the server expects an authentication request.");
            }

            (byte[] reply, string? user) = Answer(request, transport.SessionIdentifier!);
            await transport.WritePacketAsync(reply, cancellationToken).ConfigureAwait(false);
            if (user is not null)
            {
                return user;
            }
        }
    }

    /// <summary>Answers one <c>SSH_MSG_USERAUTH_REQUEST</c>.</summary>
    /// <param name="request">The request's payload, message number first.</param>
    /// <param name="sessionIdentifier">The session identifier a <c>publickey</c> signature covers.</param>
    /// <returns>The reply's payload, and the user name when the reply is <c>SUCCESS</c>.</returns>
    internal static (byte[] Reply, string? User) Answer(byte[] request, byte[] sessionIdentifier)
    {
        SshWireReader reader = new(request);
        reader.ReadByte();
        string user = Encoding.UTF8.GetString(reader.ReadString().Span);
        reader.ReadName();
        string method = reader.ReadName();
        byte[]? reply = user == SshServerClientAccount.User ? AnswerMethod(method, reader, request, sessionIdentifier) : null;
        if (reply is null)
        {
            return (Failure(), null);
        }

        return (reply, reply[0] == SshAuthenticationMessageNumber.Success ? user : null);
    }

    // The account's answer to one method: SUCCESS or PK_OK, or null for a failure.
    private static byte[]? AnswerMethod(string method, SshWireReader reader, byte[] request, byte[] sessionIdentifier) => method switch
    {
        "password" => AnswerPassword(reader),
        "publickey" => AnswerPublicKey(reader, request, sessionIdentifier),
        _ => null,
    };

    private static byte[]? AnswerPassword(SshWireReader reader)
    {
        reader.ReadBoolean();
        return reader.ReadName() == SshServerClientAccount.Password ? [SshAuthenticationMessageNumber.Success] : null;
    }

    // RFC 4252 section 7: without a signature the client asks whether the key would do; with
    // one, the signature covers the session identifier and the request up to the signature.
    private static byte[]? AnswerPublicKey(SshWireReader reader, byte[] request, byte[] sessionIdentifier)
    {
        bool signed = reader.ReadBoolean();
        string algorithm = reader.ReadName();
        ReadOnlyMemory<byte> blob = reader.ReadString();
        if (!SshServerClientAccount.AcceptsKey(algorithm, blob.Span))
        {
            return null;
        }

        if (!signed)
        {
            return PublicKeyOk(algorithm, blob.Span);
        }

        int signedLength = request.Length - reader.RemainingLength;
        ReadOnlyMemory<byte> signature = reader.ReadString();
        SshWireWriter signedData = new();
        signedData.WriteString(sessionIdentifier);
        signedData.WriteBytes(request.AsSpan(0, signedLength));
        return SshServerClientAccount.Verifies(algorithm, signedData.ToArray(), signature) ? [SshAuthenticationMessageNumber.Success] : null;
    }

    private static byte[] PublicKeyOk(string algorithm, ReadOnlySpan<byte> blob)
    {
        SshWireWriter reply = new();
        reply.WriteByte(SshAuthenticationMessageNumber.PublicKeyOk);
        reply.WriteString(Encoding.ASCII.GetBytes(algorithm));
        reply.WriteString(blob);
        return reply.ToArray();
    }

    private static byte[] Failure()
    {
        SshWireWriter reply = new();
        reply.WriteByte(SshAuthenticationMessageNumber.Failure);
        reply.WriteNameList(OfferedMethods.Split(','));
        reply.WriteBoolean(false);
        return reply.ToArray();
    }
}
