using Curl.Protocol.Ssh.Authentication;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Writes what an SSH key test arranges and gets - key file text, the key read with its type
/// and public key blob as hex, a public key file's reading, and the exception a refused input
/// threw - as <c>ARRANGE</c>, <c>ACT</c>, <c>BYTES</c> and <c>ASSERT</c> lines through the
/// shared <see cref="TestDiagnostics" /> helper (BL-1625).
/// </summary>
internal static class SshKeyDiagnostics
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

    /// <summary>Writes the private key a test read: its class and key type, then its public key blob.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="key">The key, or <see langword="null" /> when none was read.</param>
    public static void ActKey(this TestDiagnostics diagnostics, SshPrivateKey? key)
    {
        diagnostics.Act("key", key is null ? "(none)" : $"{key.GetType().Name} {key.KeyType}");
        if (key is not null)
        {
            diagnostics.Bytes("public key blob", key.PublicKeyBlob);
        }
    }

    /// <summary>Writes the public key a test read: its key type, then its blob.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="key">The key, or <see langword="null" /> when none was read.</param>
    public static void ActPublicKey(this TestDiagnostics diagnostics, SshPublicKey? key)
    {
        diagnostics.Act("public key", key is null ? "(none)" : key.KeyType);
        if (key is not null)
        {
            diagnostics.Bytes("public key blob", key.Blob);
        }
    }

    /// <summary>Writes a public key file's reading: its key, then the reason it was denied.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="reading">The reading.</param>
    public static void ActReading(this TestDiagnostics diagnostics, SshPublicKeyReading reading)
    {
        diagnostics.ActPublicKey(reading.Key);
        diagnostics.Act("denial reason", reading.DenialReason ?? "(none)");
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
