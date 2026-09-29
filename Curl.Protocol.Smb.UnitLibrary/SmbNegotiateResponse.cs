using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// What curl 8.21.0 takes from the server's SMB_COM_NEGOTIATE response: the 8-byte
/// challenge the NTLMv1 responses answer and the session key echoed back in the session
/// setup. Nothing else is checked - not the dialect index, the word count or the
/// encryption key length - as <c>smb_connection_state</c> checks nothing else.
/// </summary>
/// <param name="Challenge">The 8-byte challenge.</param>
/// <param name="SessionKey">The server's session key.</param>
internal sealed record SmbNegotiateResponse(byte[] Challenge, uint SessionKey)
{
    /// <summary>The length of the challenge.</summary>
    public const int ChallengeLength = 8;

    // sizeof(struct smb_negotiate_response) less its one-byte "bytes" placeholder: where
    // the challenge starts.
    private const int ChallengeOffset = 73;

    private const int SessionKeyOffset = 52;

    /// <summary>
    /// Reads a received negotiate response, or refuses it as curl does: a status other
    /// than success, or fewer bytes received than reach the end of the challenge.
    /// </summary>
    /// <param name="received">Every byte received for the message, as curl counts <c>got</c>.</param>
    /// <param name="response">The challenge and session key when the response is accepted.</param>
    /// <returns><see langword="true" /> when the response is accepted.</returns>
    public static bool TryRead(ReadOnlySpan<byte> received, out SmbNegotiateResponse? response)
    {
        if (received.Length < ChallengeOffset + ChallengeLength || SmbMessageHeader.ReadStatus(received) != 0)
        {
            response = null;
            return false;
        }

        response = new SmbNegotiateResponse(
            received.Slice(ChallengeOffset, ChallengeLength).ToArray(),
            BinaryPrimitives.ReadUInt32LittleEndian(received[SessionKeyOffset..]));
        return true;
    }
}
