using Curl.Testing;

namespace Curl.Ssh;

/// <summary>
/// Writes what an SSH test arranges and gets - text, bytes as hex, and the exception a
/// refused input threw - as <c>ARRANGE</c>, <c>ACT</c>, <c>BYTES</c> and <c>ASSERT</c> lines
/// through the shared <see cref="TestDiagnostics" /> helper (BL-1625).
/// </summary>
internal static class SshByteDiagnostics
{
    /// <summary>Writes a text input, escaped and capped so it stays on one line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the text is.</param>
    /// <param name="text">The text.</param>
    public static void ArrangeText(this TestDiagnostics diagnostics, string label, string? text) =>
        diagnostics.Arrange(label, text is null ? "(null)" : SshAuthenticationDiagnostics.Text(text));

    /// <summary>Writes bytes a test got: their length as an ACT line, then the bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the bytes are.</param>
    /// <param name="bytes">The bytes.</param>
    public static void ActBytes(this TestDiagnostics diagnostics, string label, byte[] bytes)
    {
        diagnostics.Act(label, $"{bytes.Length} bytes");
        diagnostics.Bytes(label, bytes);
    }


    /// <summary>Writes the exception a test caught, then its type against the type expected.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expectedType">The type the test expects, or its description when a subclass is allowed.</param>
    /// <param name="failure">The exception caught.</param>
    public static void ActAndAssertThrown(this TestDiagnostics diagnostics, string expectedType, Exception failure)
    {
        diagnostics.Act("exception", $"{failure.GetType().Name}: {SshAuthenticationDiagnostics.Text(failure.Message)}");
        diagnostics.Assert("exception type", expectedType, failure.GetType().Name);
    }

    /// <summary>Writes the ASSERT line between an expected and an actual byte array as hex, then their first difference.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected bytes.</param>
    /// <param name="actual">The actual bytes, or <see langword="null" />.</param>
    public static void AssertBytes(this TestDiagnostics diagnostics, string label, byte[] expected, byte[]? actual)
    {
        diagnostics.Assert(label, Convert.ToHexString(expected), actual is null ? "(null)" : Convert.ToHexString(actual));
        diagnostics.Diff(label, expected, actual ?? []);
    }
}
