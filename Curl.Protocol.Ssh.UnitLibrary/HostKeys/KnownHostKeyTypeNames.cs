namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Maps between SSH key type names and <see cref="KnownHostKeyType" />, as libssh2 1.11.1
/// names the types it recognizes in a known-hosts file and in a host key.
/// </summary>
internal static class KnownHostKeyTypeNames
{
    private static readonly Dictionary<string, KnownHostKeyType> TypesByName = new(StringComparer.Ordinal)
    {
        ["ssh-rsa"] = KnownHostKeyType.SshRsa,
        ["ecdsa-sha2-nistp256"] = KnownHostKeyType.EcdsaNistP256,
        ["ecdsa-sha2-nistp384"] = KnownHostKeyType.EcdsaNistP384,
        ["ecdsa-sha2-nistp521"] = KnownHostKeyType.EcdsaNistP521,
        ["ssh-ed25519"] = KnownHostKeyType.SshEd25519,
    };

    /// <summary>
    /// Classifies a key type name: one of the five recognized names, or
    /// <see cref="KnownHostKeyType.Unknown" />.
    /// </summary>
    /// <param name="name">The key type name, as a known-hosts line or a key blob spells it.</param>
    /// <returns>The key type.</returns>
    internal static KnownHostKeyType FromName(string name) =>
        TypesByName.GetValueOrDefault(name, KnownHostKeyType.Unknown);

    /// <summary>
    /// Gives the name of a recognized key type, the name the host-key list is narrowed to.
    /// </summary>
    /// <param name="type">One of the five recognized types.</param>
    /// <returns>Its name.</returns>
    internal static string NameOf(KnownHostKeyType type) =>
        TypesByName.First(pair => pair.Value == type).Key;
}
