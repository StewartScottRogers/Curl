namespace Curl.Ntlm;

/// <summary>
/// The outcome of <see cref="NtlmTargetInformation.Decode" />: the pairs read, or why they
/// could not be read.
/// </summary>
public sealed class NtlmTargetInformationDecoding
{
    private NtlmTargetInformationDecoding(IReadOnlyList<NtlmAvPair> pairs, NtlmMessageFailure failure)
    {
        Pairs = pairs;
        Failure = failure;
    }

    /// <summary>The pairs before <c>MsvAvEOL</c>, in message order; empty on failure.</summary>
    public IReadOnlyList<NtlmAvPair> Pairs { get; }

    /// <summary><see cref="NtlmMessageFailure.None" /> when the list was read, otherwise why not.</summary>
    public NtlmMessageFailure Failure { get; }

    internal static NtlmTargetInformationDecoding Decoded(IReadOnlyList<NtlmAvPair> pairs) => new(pairs, NtlmMessageFailure.None);

    internal static NtlmTargetInformationDecoding Failed(NtlmMessageFailure failure) => new([], failure);
}
