namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>Builds the unmasked frames a WebSocket server sends (RFC 6455 section 5.2).</summary>
public static class LibcurlWebSocketFrames
{
    /// <summary>The text opcode.</summary>
    public const int Text = 1;

    /// <summary>The binary opcode.</summary>
    public const int Binary = 2;

    /// <summary>The continuation opcode.</summary>
    public const int Continuation = 0;

    /// <summary>The close opcode.</summary>
    public const int Close = 8;

    /// <summary>The ping opcode.</summary>
    public const int Ping = 9;

    /// <summary>The pong opcode.</summary>
    public const int Pong = 10;

    private const int ShortLengthLimit = 126;

    /// <summary>Builds one unmasked frame.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="payload">Its payload, shorter than 65536 bytes.</param>
    /// <param name="final">Whether its FIN bit is set.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] Frame(int opcode, byte[] payload, bool final = true)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var first = (byte)((final ? 0x80 : 0) | opcode);
        byte[] length = payload.Length < ShortLengthLimit
            ? [(byte)payload.Length]
            : [ShortLengthLimit, (byte)(payload.Length >> 8), (byte)payload.Length];
        return [first, .. length, .. payload];
    }
}
