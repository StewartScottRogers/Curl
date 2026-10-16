namespace Curl.Api.Options.Protocols;

/// <summary>The SCP and SFTP options of one easy handle, each defaulting to libcurl 8.22.0's default.</summary>
public sealed record EasySshOptions
{
    /// <summary>
    /// <c>CURLOPT_SSH_AUTH_TYPES</c>: the authentication methods SSH may use. Default
    /// <see cref="SshAuthenticationTypes.Any"/> (libcurl's <c>CURLSSH_AUTH_DEFAULT</c>).
    /// </summary>
    public SshAuthenticationTypes AuthenticationTypes { get; init; } = SshAuthenticationTypes.Any;

    /// <summary><c>CURLOPT_SSH_COMPRESSION</c>: ask for SSH compression. Default <see langword="false"/>.</summary>
    public bool Compression { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSH_HOST_PUBLIC_KEY_MD5</c>: the 32 hexadecimal digits of the MD5 of the host's
    /// public key, which the host's key must match. Default <see langword="null"/>, meaning no check.
    /// </summary>
    public string? HostPublicKeyMd5 { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256</c>: the base64 SHA-256 of the host's public key,
    /// which the host's key must match. Default <see langword="null"/>, meaning no check.
    /// </summary>
    public string? HostPublicKeySha256 { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSH_PUBLIC_KEYFILE</c>: the public key file. Default <see langword="null"/>,
    /// meaning libcurl derives it from the private key file.
    /// </summary>
    public string? PublicKeyFile { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSH_PRIVATE_KEYFILE</c>: the private key file. Default <see langword="null"/>,
    /// meaning <c>~/.ssh/id_rsa</c> or <c>~/.ssh/id_dsa</c>.
    /// </summary>
    public string? PrivateKeyFile { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSH_KNOWNHOSTS</c>: the known_hosts file the host's key is checked against.
    /// Default <see langword="null"/>, meaning no check.
    /// </summary>
    public string? KnownHostsFile { get; init; }
}
