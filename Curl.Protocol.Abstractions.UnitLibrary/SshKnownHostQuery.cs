namespace Curl.Protocol.Abstractions;

/// <summary>
/// What <see cref="SshOptions.KnownHostDecision" /> is asked about once the server's host key has been
/// looked up in the known-hosts file: libcurl's <c>CURLOPT_SSH_KEYFUNCTION</c> arguments (ADR-0623).
/// </summary>
/// <param name="KeyTypeName">The host key's type name, as its blob spells it (<c>ssh-ed25519</c>, for one).</param>
/// <param name="FoundKey">The host key blob the server presented.</param>
/// <param name="KnownKeyText">
/// The base64 key text of the file's entry for the host, or <see langword="null" /> when
/// <paramref name="Match" /> is <see cref="SshKnownHostMatch.Missing" />.
/// </param>
/// <param name="Match">How <paramref name="FoundKey" /> compared with the file.</param>
public sealed record SshKnownHostQuery(string KeyTypeName, ReadOnlyMemory<byte> FoundKey, string? KnownKeyText, SshKnownHostMatch Match);
