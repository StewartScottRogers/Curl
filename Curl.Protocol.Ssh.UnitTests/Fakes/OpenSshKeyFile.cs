using System.Text;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Writes <c>openssh-key-v1</c> bodies by hand (OpenSSH's <c>PROTOCOL.key</c>), for keys
/// and malformations no tool writes.
/// </summary>
internal static class OpenSshKeyFile
{
    /// <summary>
    /// An unencrypted body: the magic, cipher and KDF <c>none</c>, empty KDF options, one
    /// key, and the private section with equal check integers, <paramref name="keyFields" />,
    /// a comment and OpenSSH's padding to eight bytes.
    /// </summary>
    /// <param name="publicKeyBlob">The public key blob.</param>
    /// <param name="keyFields">The key type and its private fields.</param>
    /// <returns>The body.</returns>
    internal static byte[] Body(byte[] publicKeyBlob, byte[] keyFields) =>
        Body("none", "none", 1, publicKeyBlob, Section(0x01020304, 0x01020304, keyFields));

    /// <summary>
    /// A body with every field given.
    /// </summary>
    /// <param name="cipher">The cipher name.</param>
    /// <param name="kdf">The KDF name.</param>
    /// <param name="keyCount">The number of keys.</param>
    /// <param name="publicKeyBlob">The public key blob.</param>
    /// <param name="section">The private section.</param>
    /// <returns>The body.</returns>
    internal static byte[] Body(string cipher, string kdf, uint keyCount, byte[] publicKeyBlob, byte[] section) =>
        Join(Encoding.ASCII.GetBytes("openssh-key-v1\0"), Name(cipher), Name(kdf), String([]), UInt32(keyCount), String(publicKeyBlob), String(section));

    /// <summary>
    /// A private section: the two check integers, the key fields, a comment and padding.
    /// </summary>
    /// <param name="check1">The first check integer.</param>
    /// <param name="check2">The second.</param>
    /// <param name="keyFields">The key type and its private fields.</param>
    /// <returns>The section.</returns>
    internal static byte[] Section(uint check1, uint check2, byte[] keyFields)
    {
        byte[] section = Join(UInt32(check1), UInt32(check2), keyFields, Name("test"));
        int padding = (8 - (section.Length % 8)) % 8;
        return [.. section, .. Enumerable.Range(1, padding).Select(value => (byte)value)];
    }
}
