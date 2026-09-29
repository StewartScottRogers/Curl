namespace Curl.Ntlm;

/// <summary>
/// The outcome of <see cref="NtlmChallengeMessage.Decode" />: the message read, or why it
/// could not be read. curl fails every such message with <c>CURLE_BAD_CONTENT_ENCODING</c>.
/// </summary>
public sealed class NtlmChallengeDecoding
{
    private NtlmChallengeDecoding(NtlmChallengeMessage? message, NtlmMessageFailure failure)
    {
        Message = message;
        Failure = failure;
    }

    /// <summary>The message read; <see langword="null" /> on failure.</summary>
    public NtlmChallengeMessage? Message { get; }

    /// <summary><see cref="NtlmMessageFailure.None" /> when the message was read, otherwise why not.</summary>
    public NtlmMessageFailure Failure { get; }

    internal static NtlmChallengeDecoding Decoded(NtlmChallengeMessage message) => new(message, NtlmMessageFailure.None);

    internal static NtlmChallengeDecoding Failed(NtlmMessageFailure failure) => new(null, failure);
}
