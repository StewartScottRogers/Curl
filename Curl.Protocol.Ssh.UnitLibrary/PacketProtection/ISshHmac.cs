namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// A keyed HMAC that <see cref="SshMac" /> feeds one packet at a time:
/// <see cref="BclSshHmac" /> for the hashes the BCL offers, <see cref="Ripemd160SshHmac" />
/// for RIPEMD-160.
/// </summary>
internal interface ISshHmac : IDisposable
{
    /// <summary>Adds <paramref name="data" /> to the message being authenticated.</summary>
    /// <param name="data">The bytes.</param>
    void AppendData(ReadOnlySpan<byte> data);

    /// <summary>Returns the whole HMAC of the message and starts a new one under the same key.</summary>
    /// <returns>The HMAC.</returns>
    byte[] GetHashAndReset();
}
