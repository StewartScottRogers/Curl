using System.Text;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Turns a handle's <c>CURLOPT_SSH_HOSTKEYFUNCTION</c> and <c>CURLOPT_SSH_KEYFUNCTION</c> into the SSH
/// handler's <see cref="SshOptions.HostKeyAcceptance" /> and <see cref="SshOptions.KnownHostDecision" />
/// (ADR-0623): the key type name becomes libcurl's <see cref="SshKeyType" /> as <c>convert_ssh2_keytype</c>
/// maps libssh2's, the known-hosts entry's key is its base64 text as libssh2 holds it, and the match and
/// the answer pass through by number, as curl casts them.
/// </summary>
internal static class CallbackSshHostKeys
{
    /// <summary>The handler's host-key acceptance over <paramref name="callback" />.</summary>
    /// <param name="callback">The handle's <c>CURLOPT_SSH_HOSTKEYFUNCTION</c>, or <see langword="null" />.</param>
    /// <returns>The acceptance, true only for <see cref="SshKeyMatch.Ok" />; <see langword="null" /> without a callback.</returns>
    internal static Func<string, ReadOnlyMemory<byte>, bool>? AcceptanceOf(SshHostKeyCallback? callback) =>
        callback is null ? null : new HostKeyCallback(callback).Accepts;

    /// <summary>The handler's known-host decision over <paramref name="callback" />.</summary>
    /// <param name="callback">The handle's <c>CURLOPT_SSH_KEYFUNCTION</c>, or <see langword="null" />.</param>
    /// <returns>The decision; <see langword="null" /> without a callback.</returns>
    internal static Func<SshKnownHostQuery, SshKnownHostVerdict>? DecisionOf(SshKeyCallback? callback) =>
        callback is null ? null : new KeyCallback(callback).Decide;

    /// <summary>
    /// libcurl's key type for an SSH key type name: <c>ssh-rsa</c>, <c>ssh-dss</c>, any <c>ecdsa-sha2-*</c> and
    /// <c>ssh-ed25519</c>, else <see cref="SshKeyType.Unknown" />.
    /// </summary>
    /// <param name="keyTypeName">The name, as the key blob spells it.</param>
    /// <returns>The key type.</returns>
    internal static SshKeyType KeyTypeOf(string keyTypeName) => keyTypeName switch
    {
        "ssh-rsa" => SshKeyType.Rsa,
        "ssh-dss" => SshKeyType.Dss,
        "ssh-ed25519" => SshKeyType.Ed25519,
        _ when keyTypeName.StartsWith("ecdsa-sha2-", StringComparison.Ordinal) => SshKeyType.Ecdsa,
        _ => SshKeyType.Unknown,
    };

    private sealed class HostKeyCallback(SshHostKeyCallback callback)
    {
        internal bool Accepts(string keyTypeName, ReadOnlyMemory<byte> key) =>
            callback(KeyTypeOf(keyTypeName), key.Span) == SshKeyMatch.Ok;
    }

    private sealed class KeyCallback(SshKeyCallback callback)
    {
        internal SshKnownHostVerdict Decide(SshKnownHostQuery query)
        {
            SshKeyType keyType = KeyTypeOf(query.KeyTypeName);
            SshKey? known = query.KnownKeyText is { } text ? new SshKey(Encoding.ASCII.GetBytes(text), keyType) : null;
            return (SshKnownHostVerdict)(int)callback(known, new SshKey(query.FoundKey, keyType), (SshKeyMatch)(int)query.Match);
        }
    }
}
