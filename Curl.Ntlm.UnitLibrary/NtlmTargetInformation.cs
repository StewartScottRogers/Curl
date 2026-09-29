using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// Reads the AV_PAIR list a CHALLENGE message carries as its target information (MS-NLMP
/// section 2.2.2.1): pairs of a 16-bit <c>AvId</c>, a 16-bit <c>AvLen</c> and that many
/// value bytes, ending with <c>MsvAvEOL</c>. curl itself copies the target information
/// into its NTLMv2 response without reading it; this is for the callers that need a pair
/// (a timestamp, a server name).
/// </summary>
public static class NtlmTargetInformation
{
    private const int PairHeaderSize = 4;

    /// <summary>
    /// Reads every pair before <c>MsvAvEOL</c>. Bytes after <c>MsvAvEOL</c> are ignored.
    /// </summary>
    public static NtlmTargetInformationDecoding Decode(ReadOnlySpan<byte> targetInformation)
    {
        List<NtlmAvPair> pairs = [];
        ReadOnlySpan<byte> remaining = targetInformation;
        while (remaining.Length >= PairHeaderSize)
        {
            NtlmAvId id = (NtlmAvId)BinaryPrimitives.ReadUInt16LittleEndian(remaining);
            int valueLength = BinaryPrimitives.ReadUInt16LittleEndian(remaining[2..]);
            if (id == NtlmAvId.EndOfList)
            {
                return NtlmTargetInformationDecoding.Decoded(pairs);
            }

            if (remaining.Length - PairHeaderSize < valueLength)
            {
                return NtlmTargetInformationDecoding.Failed(NtlmMessageFailure.AvPairTruncated);
            }

            pairs.Add(new NtlmAvPair(id, remaining.Slice(PairHeaderSize, valueLength).ToArray()));
            remaining = remaining[(PairHeaderSize + valueLength)..];
        }

        return NtlmTargetInformationDecoding.Failed(
            remaining.IsEmpty ? NtlmMessageFailure.AvPairListUnterminated : NtlmMessageFailure.AvPairTruncated);
    }
}
