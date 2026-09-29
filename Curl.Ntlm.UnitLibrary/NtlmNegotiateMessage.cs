using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// The NEGOTIATE (Type 1) message of MS-NLMP section 2.2.1.1 as curl 8.21.0 writes it in
/// <c>Curl_auth_create_ntlm_type1_message</c> (<c>lib/vauth/ntlm.c</c>): 32 bytes, the
/// <see cref="CurlFlags" />, and empty domain and workstation buffers with offset zero. It
/// names neither the user nor the host, so it is the same for every request.
/// </summary>
public static class NtlmNegotiateMessage
{
    /// <summary>The length of curl's NEGOTIATE message in bytes.</summary>
    public const int Length = 32;

    /// <summary>
    /// The flags curl sets: OEM, request target, NTLM, always sign and extended session
    /// security, <c>0x00088206</c>.
    /// </summary>
    public const NtlmNegotiateFlags CurlFlags =
        NtlmNegotiateFlags.NegotiateOem
        | NtlmNegotiateFlags.RequestTarget
        | NtlmNegotiateFlags.NegotiateNtlm
        | NtlmNegotiateFlags.NegotiateAlwaysSign
        | NtlmNegotiateFlags.NegotiateExtendedSessionSecurity;

    /// <summary>Writes curl's NEGOTIATE message.</summary>
    public static byte[] Encode()
    {
        byte[] message = new byte[Length];
        NtlmMessageLayout.WriteSignatureAndType(message, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(12), (uint)CurlFlags);
        return message;
    }
}
