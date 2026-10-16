namespace Curl.Api.Callbacks;

/// <summary>An SSH host key as an <see cref="SshKeyCallback"/> sees it: libcurl's <c>struct curl_khkey</c>.</summary>
/// <param name="Key">The key's bytes: the raw key, or its base64 text as the known-hosts file holds it.</param>
/// <param name="KeyType">The key's type.</param>
public sealed record SshKey(ReadOnlyMemory<byte> Key, SshKeyType KeyType);
