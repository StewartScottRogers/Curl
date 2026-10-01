namespace Curl.Quic;

/// <summary>
/// Shared helpers for the QUIC tests.
/// </summary>
internal static class QuicTest
{
    /// <summary>
    /// The <c>[Timeout]</c> of every asynchronous QUIC test: a test whose wait is never answered
    /// fails after two minutes instead of hanging the run (BL-1067). Longer than
    /// <see cref="QuicTestLiveChannel.HangGuard" />, so a guarded wait fails first with its own message.
    /// </summary>
    public const int HangTimeoutMilliseconds = 120_000;

    /// <summary>Reads hex written as the RFCs print it, with spaces and line breaks.</summary>
    public static byte[] Hex(string hex) => Convert.FromHexString(string.Concat(hex.Where(char.IsAsciiHexDigit)));

    /// <summary>Runs an action that must fail with a transport error, and returns the error.</summary>
    public static QuicTransportErrorCode ErrorOf(Action action) => Assert.ThrowsExactly<QuicTransportException>(action).ErrorCode;

    /// <summary>Returns the lowercase hex of some bytes.</summary>
    public static string HexOf(ReadOnlyMemory<byte> bytes) => Convert.ToHexStringLower(bytes.Span);
}
