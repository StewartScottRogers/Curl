using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Writes the RFC 8448 replay tests' inputs and results through <see cref="TestDiagnostics" />:
/// a secret, key or random as hex, a whole message or record as its length and
/// <c>BYTES</c>, and every derived value with a <c>DIFF</c> against the trace's.
/// </summary>
internal static class Rfc8448Diagnostics
{
    // A value up to this long is a secret, key, IV, random or hash, written as hex on one line.
    private const int HexLineLimit = 64;

    /// <summary>Writes a trace input as <c>ARRANGE</c> and returns its bytes.</summary>
    public static byte[] ArrangeHex(this TestDiagnostics diagnostics, string label, string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        if (bytes.Length <= HexLineLimit)
        {
            diagnostics.Arrange(label, hex);
        }
        else
        {
            diagnostics.Arrange(label, $"{bytes.Length} bytes");
            diagnostics.Bytes(label, bytes);
        }

        return bytes;
    }

    /// <summary>
    /// Writes a derived value as <c>ACT</c> (and <c>BYTES</c> when it is a whole message)
    /// and a <c>DIFF</c> against the trace's value, and returns its lowercase hex.
    /// </summary>
    public static string ActAndDiffHex(this TestDiagnostics diagnostics, string label, string expected, byte[] actual)
    {
        string actualHex = Convert.ToHexStringLower(actual);
        if (actual.Length <= HexLineLimit)
        {
            diagnostics.Act(label, actualHex);
        }
        else
        {
            diagnostics.Act(label, $"{actual.Length} bytes");
            diagnostics.Bytes(label, actual);
        }

        diagnostics.Diff(label, expected, actualHex);
        return actualHex;
    }
}
