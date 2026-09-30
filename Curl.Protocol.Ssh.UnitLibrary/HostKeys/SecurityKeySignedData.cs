using System.Security.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// What a security-key (<c>sk-</c>) signature signs, from OpenSSH's <c>PROTOCOL.u2f</c>:
/// <c>SHA256(application) || flags || counter || SHA256(message)</c>, where the flags byte
/// and the 32-bit counter follow the signature in the signature blob.
/// </summary>
internal static class SecurityKeySignedData
{
    /// <summary>
    /// Reads the flags and counter that follow the signature bytes and composes the data the
    /// security key signed.
    /// </summary>
    /// <param name="application">The application string of the key blob, such as <c>ssh:</c>.</param>
    /// <param name="signature">The signature blob, positioned after its signature bytes.</param>
    /// <param name="message">The data signed, H for a host key.</param>
    /// <returns>The data the signature is over.</returns>
    /// <exception cref="InvalidDataException">The flags or counter are missing.</exception>
    internal static byte[] Read(ReadOnlyMemory<byte> application, SshWireReader signature, byte[] message)
    {
        ReadOnlySpan<byte> flagsAndCounter = signature.ReadBytes(sizeof(byte) + sizeof(uint)).Span;
        return [.. SHA256.HashData(application.Span), .. flagsAndCounter, .. SHA256.HashData(message)];
    }
}
