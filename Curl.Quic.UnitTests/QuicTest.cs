namespace Curl.Quic;

/// <summary>
/// Shared helpers for the QUIC tests.
/// </summary>
internal static class QuicTest
{
    /// <summary>Reads hex written as the RFCs print it, with spaces and line breaks.</summary>
    public static byte[] Hex(string hex) => Convert.FromHexString(string.Concat(hex.Where(char.IsAsciiHexDigit)));

    /// <summary>Runs an action that must fail with a transport error, and returns the error.</summary>
    public static QuicTransportErrorCode ErrorOf(Action action) => Assert.ThrowsExactly<QuicTransportException>(action).ErrorCode;

    /// <summary>Returns the lowercase hex of some bytes.</summary>
    public static string HexOf(ReadOnlyMemory<byte> bytes) => Convert.ToHexStringLower(bytes.Span);
}
