using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Ntlm;

namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_SESSION_SETUP_ANDX request curl 8.21.0 sends after the negotiate
/// (<c>smb_send_setup</c>): thirteen parameter words, then the 24-byte LM and NT
/// responses to the server's challenge (NTLMv1, <see cref="NtlmResponseComputation.ComputeV1" />,
/// not NTLMSSP), the user, the domain, curl's host triple and <c>curl</c>, each ended by a NUL.
/// </summary>
internal static class SmbSessionSetupRequest
{
    /// <summary>The most bytes curl's <c>struct smb_setup</c> holds after its parameter words.</summary>
    public const int MaxByteCount = 1024;

    // Word count through byte count: 1 + 4 (AndX) + 2 + 2 + 2 + 4 + 4 (lengths) + 4 + 4 + 2.
    private const int ParametersLength = 29;

    private const int ResponseLength = 24;

    // SMB_COM_NO_ANDX_COMMAND.
    private const byte NoAndXCommand = 0xff;

    // SMB_WC_SETUP_ANDX.
    private const byte WordCount = 0x0d;

    // MAX_MESSAGE_SIZE: MAX_PAYLOAD_SIZE (0x8000) plus 0x1000.
    private const ushort MaxBufferSize = 0x9000;

    // SMB_CAP_LARGE_FILES.
    private const uint Capabilities = 0x08;

    private const string ClientName = "curl";

    /// <summary>
    /// Encodes the request, NetBIOS header first, or refuses it as curl does when its
    /// bytes would pass <see cref="MaxByteCount" />.
    /// </summary>
    /// <param name="password">The password the responses are computed from.</param>
    /// <param name="identity">The user and domain to send.</param>
    /// <param name="operatingSystem">curl's host triple, such as <c>x86_64-pc-linux-gnu</c>.</param>
    /// <param name="negotiate">The challenge and session key the negotiate response carried.</param>
    /// <returns>The bytes to send, or <see langword="null" /> when they would not fit.</returns>
    public static byte[]? Encode(string password, SmbIdentity identity, string operatingSystem, SmbNegotiateResponse negotiate)
    {
        byte[] user = Encoding.UTF8.GetBytes(identity.User);
        byte[] domain = Encoding.UTF8.GetBytes(identity.Domain);
        int byteCount = (2 * ResponseLength) + user.Length + domain.Length + operatingSystem.Length + ClientName.Length + 4;
        if (byteCount > MaxByteCount)
        {
            return null;
        }

        var message = new byte[SmbMessageHeader.Length + ParametersLength + byteCount];
        SmbMessageHeader.Write(message, SmbMessageHeader.SessionSetupAndXCommand, 0, 0, ParametersLength + byteCount);
        WriteParameters(message.AsSpan(SmbMessageHeader.Length, ParametersLength), negotiate.SessionKey, byteCount);
        Span<byte> bytes = message.AsSpan(SmbMessageHeader.Length + ParametersLength);
        WriteResponses(bytes, password, negotiate.Challenge);
        int offset = 2 * ResponseLength;
        offset = AppendTerminated(bytes, offset, user);
        offset = AppendTerminated(bytes, offset, domain);
        offset = AppendTerminated(bytes, offset, Encoding.ASCII.GetBytes(operatingSystem));
        AppendTerminated(bytes, offset, Encoding.ASCII.GetBytes(ClientName));
        return message;
    }

    private static void WriteParameters(Span<byte> parameters, uint sessionKey, int byteCount)
    {
        parameters[0] = WordCount;
        parameters[1] = NoAndXCommand;
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[5..], MaxBufferSize);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[7..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[9..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[11..], sessionKey);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[15..], ResponseLength);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[17..], ResponseLength);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[23..], Capabilities);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[27..], (ushort)byteCount);
    }

    private static void WriteResponses(Span<byte> bytes, string password, byte[] challenge)
    {
        NtlmResponses responses = NtlmResponseComputation.ComputeV1(password, challenge, default);
        responses.LmChallengeResponse.CopyTo(bytes);
        responses.NtChallengeResponse.CopyTo(bytes[ResponseLength..]);
        CryptographicOperations.ZeroMemory(responses.LmChallengeResponse);
        CryptographicOperations.ZeroMemory(responses.NtChallengeResponse);
        CryptographicOperations.ZeroMemory(responses.SessionBaseKey);
        CryptographicOperations.ZeroMemory(responses.KeyExchangeKey);
    }

    private static int AppendTerminated(Span<byte> bytes, int offset, byte[] text)
    {
        text.CopyTo(bytes[offset..]);
        return offset + text.Length + 1;
    }
}
