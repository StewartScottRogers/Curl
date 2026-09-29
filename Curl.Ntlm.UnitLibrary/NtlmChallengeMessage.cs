using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// A server's CHALLENGE (Type 2) message (MS-NLMP section 2.2.1.2), read as curl 8.21.0's
/// <c>Curl_auth_decode_ntlm_type2_message</c> reads it: at least 32 bytes, the signature
/// and type checked, the flags and server challenge taken, and the target information taken
/// only when <see cref="NtlmNegotiateFlags.NegotiateTargetInfo" /> is set and the message
/// holds its 48-byte header.
/// </summary>
/// <param name="Flags">The negotiated flags, at offset 20.</param>
/// <param name="ServerChallenge">The 8-byte server challenge, at offset 24.</param>
/// <param name="TargetName">
/// The target name's bytes (UTF-16LE or OEM, as <paramref name="Flags" /> says); empty when
/// its buffer lies outside the message, which curl never reads and so never rejects.
/// </param>
/// <param name="TargetInformation">
/// The target information's raw AV_PAIR bytes, which an NTLMv2 response copies verbatim;
/// read them with <see cref="NtlmTargetInformation.Decode" />. Empty when the flag is clear,
/// the message is shorter than 48 bytes, or the length is zero.
/// </param>
/// <param name="Version">
/// The 8-byte VERSION structure at offset 48 when
/// <see cref="NtlmNegotiateFlags.NegotiateVersion" /> is set and the message holds it;
/// otherwise empty.
/// </param>
public sealed record NtlmChallengeMessage(
    NtlmNegotiateFlags Flags,
    byte[] ServerChallenge,
    byte[] TargetName,
    byte[] TargetInformation,
    byte[] Version)
{
    /// <summary>The shortest CHALLENGE message curl accepts.</summary>
    public const int MinimumLength = 32;

    /// <summary>
    /// The length of the header that holds the target information buffer; target
    /// information may not start inside it.
    /// </summary>
    public const int TargetInformationHeaderLength = 48;

    private const int VersionOffset = 48;

    private const int VersionLength = 8;

    /// <summary>
    /// Reads <paramref name="message" />, checking every offset and length against it.
    /// </summary>
    public static NtlmChallengeDecoding Decode(ReadOnlySpan<byte> message)
    {
        if (message.Length < MinimumLength)
        {
            return NtlmChallengeDecoding.Failed(NtlmMessageFailure.TooShort);
        }

        if (!NtlmMessageLayout.HasSignatureAndType(message, 2))
        {
            return NtlmChallengeDecoding.Failed(NtlmMessageFailure.WrongSignatureOrType);
        }

        NtlmNegotiateFlags flags = (NtlmNegotiateFlags)BinaryPrimitives.ReadUInt32LittleEndian(message[20..]);
        if (!TryReadTargetInformation(message, flags, out byte[] targetInformation))
        {
            return NtlmChallengeDecoding.Failed(NtlmMessageFailure.TargetInfoOutOfRange);
        }

        return NtlmChallengeDecoding.Decoded(new NtlmChallengeMessage(
            flags,
            message.Slice(24, 8).ToArray(),
            ReadTargetName(message),
            targetInformation,
            ReadVersion(message, flags)));
    }

    // curl's ntlm_decode_type2_target: a zero length is no target information, and a
    // non-zero one must lie inside the message and after the 48-byte header.
    private static bool TryReadTargetInformation(ReadOnlySpan<byte> message, NtlmNegotiateFlags flags, out byte[] targetInformation)
    {
        targetInformation = [];
        if (!flags.HasFlag(NtlmNegotiateFlags.NegotiateTargetInfo) || message.Length < TargetInformationHeaderLength)
        {
            return true;
        }

        int length = BinaryPrimitives.ReadUInt16LittleEndian(message[40..]);
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(message[44..]);
        if (length == 0)
        {
            return true;
        }

        if (offset < TargetInformationHeaderLength || offset + length > message.Length)
        {
            return false;
        }

        targetInformation = message.Slice((int)offset, length).ToArray();
        return true;
    }

    private static byte[] ReadTargetName(ReadOnlySpan<byte> message)
    {
        int length = BinaryPrimitives.ReadUInt16LittleEndian(message[12..]);
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(message[16..]);
        return offset + length <= message.Length ? message.Slice((int)offset, length).ToArray() : [];
    }

    private static byte[] ReadVersion(ReadOnlySpan<byte> message, NtlmNegotiateFlags flags) =>
        flags.HasFlag(NtlmNegotiateFlags.NegotiateVersion) && message.Length >= VersionOffset + VersionLength
            ? message.Slice(VersionOffset, VersionLength).ToArray()
            : [];
}
