using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Opens host key and signature blobs, which both start with a name that must be the one
/// negotiated (RFC 4253 section 6.6).
/// </summary>
internal static class SshKeyBlobReader
{
    /// <summary>
    /// Opens a blob and checks the name it starts with.
    /// </summary>
    /// <param name="blob">The blob.</param>
    /// <param name="expectedName">The name it must start with.</param>
    /// <returns>A reader positioned after the name.</returns>
    /// <exception cref="InvalidDataException">The blob is malformed or names something else.</exception>
    internal static SshWireReader Open(ReadOnlyMemory<byte> blob, string expectedName)
    {
        SshWireReader reader = new(blob);
        string name = reader.ReadName();
        if (name != expectedName)
        {
            throw new InvalidDataException($"The SSH server sent a {name} blob where {expectedName} was negotiated.");
        }

        return reader;
    }

    /// <summary>
    /// Reads a signature blob's bytes after checking its algorithm name.
    /// </summary>
    /// <param name="signature">The signature blob.</param>
    /// <param name="expectedName">The signature algorithm negotiated.</param>
    /// <returns>The signature's bytes.</returns>
    /// <exception cref="InvalidDataException">The blob is malformed or names another algorithm.</exception>
    internal static byte[] ReadSignature(ReadOnlyMemory<byte> signature, string expectedName) =>
        Open(signature, expectedName).ReadString().ToArray();
}
