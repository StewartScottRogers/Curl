using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Ntlm;

/// <summary>
/// The AUTHENTICATE (Type 3) message of MS-NLMP section 2.2.1.3 as curl 8.21.0 writes it in
/// <c>Curl_auth_create_ntlm_type3_message</c>: a 64-byte header of six security buffers
/// and the flags, the session key buffer left zero, then the LM response, the NT response,
/// the domain, the user and the workstation, in that order. The responses are supplied;
/// computing them is not this type's work.
/// </summary>
/// <param name="Flags">
/// The flags written at offset 60: curl writes the CHALLENGE message's flags, less
/// <see cref="NtlmNegotiateFlags.NegotiateExtendedSessionSecurity" /> when it answers with
/// NTLMv1. <see cref="NtlmNegotiateFlags.NegotiateUnicode" /> also chooses the string encoding.
/// </param>
/// <param name="LmChallengeResponse">The LM or LMv2 response; curl's is always 24 bytes.</param>
/// <param name="NtChallengeResponse">The NT or NTLMv2 response.</param>
/// <param name="Domain">The domain, empty for none (see <see cref="NtlmUserName.SplitDomain" />).</param>
/// <param name="User">The user.</param>
/// <param name="Workstation">The workstation; curl always sends <see cref="CurlWorkstation" />.</param>
public sealed record NtlmAuthenticateMessage(
    NtlmNegotiateFlags Flags,
    byte[] LmChallengeResponse,
    byte[] NtChallengeResponse,
    string Domain,
    string User,
    string Workstation)
{
    /// <summary>
    /// The workstation name curl sends in place of the real host name, copied from Firefox
    /// so as not to leak it.
    /// </summary>
    public const string CurlWorkstation = "WORKSTATION";

    /// <summary>The length of the header, which is where the payload starts.</summary>
    public const int HeaderLength = 64;

    /// <summary>curl's <c>NTLM_BUFSIZE</c>: the buffer the whole message must fit.</summary>
    public const int CurlBufferSize = 1024;

    /// <summary>
    /// Writes the message, or returns <see langword="false" /> where curl fails with
    /// <c>CURLE_TOO_LARGE</c>: when the responses do not fit <see cref="CurlBufferSize" />
    /// after the header, or the domain, user and workstation do not fit strictly inside it
    /// after them.
    /// </summary>
    /// <remarks>
    /// Strings are written as curl writes them: their UTF-8 bytes as they are, or with
    /// <see cref="NtlmNegotiateFlags.NegotiateUnicode" /> each byte widened to a 16-bit
    /// little-endian unit, which is UTF-16LE for ASCII.
    /// </remarks>
    public bool TryEncode([NotNullWhen(true)] out byte[]? message)
    {
        message = null;
        bool unicode = Flags.HasFlag(NtlmNegotiateFlags.NegotiateUnicode);
        byte[] domain = EncodeString(Domain, unicode);
        byte[] user = EncodeString(User, unicode);
        byte[] workstation = EncodeString(Workstation, unicode);

        int responsesEnd = HeaderLength + LmChallengeResponse.Length + NtChallengeResponse.Length;
        int messageLength = responsesEnd + domain.Length + user.Length + workstation.Length;
        if (responsesEnd > CurlBufferSize || messageLength >= CurlBufferSize)
        {
            return false;
        }

        byte[] written = new byte[messageLength];
        NtlmMessageLayout.WriteSignatureAndType(written, 3);
        int offset = HeaderLength;
        offset = WritePayload(written, 12, LmChallengeResponse, offset);
        offset = WritePayload(written, 20, NtChallengeResponse, offset);
        offset = WritePayload(written, 28, domain, offset);
        offset = WritePayload(written, 36, user, offset);
        WritePayload(written, 44, workstation, offset);
        BinaryPrimitives.WriteUInt32LittleEndian(written.AsSpan(60), (uint)Flags);
        message = written;
        return true;
    }

    private static int WritePayload(byte[] message, int fieldOffset, byte[] payload, int payloadOffset)
    {
        NtlmMessageLayout.WriteSecurityBuffer(message.AsSpan(fieldOffset), payload.Length, payloadOffset);
        payload.CopyTo(message, payloadOffset);
        return payloadOffset + payload.Length;
    }

    // curl's unicodecpy: each byte of the C string becomes the low byte of a 16-bit unit.
    private static byte[] EncodeString(string text, bool unicode)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        if (!unicode)
        {
            return bytes;
        }

        byte[] widened = new byte[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            widened[2 * index] = bytes[index];
        }

        return widened;
    }
}
