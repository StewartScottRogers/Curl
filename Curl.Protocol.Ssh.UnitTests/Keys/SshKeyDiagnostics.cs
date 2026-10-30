using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Writes what an SSH key test gets - the key read with its type
/// and public key blob as hex, and a public key file's reading
/// - as <c>ACT</c> and <c>BYTES</c> lines through the
/// shared <see cref="TestDiagnostics" /> helper (BL-1625).
/// </summary>
internal static class SshKeyDiagnostics
{
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
}
