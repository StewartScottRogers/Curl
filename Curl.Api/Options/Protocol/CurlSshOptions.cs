using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// SCP and SFTP settings, mapped one to one onto <see cref="EasySshOptions"/> (BL-2503, ADR-0640). Each
/// member starts at libcurl 8.22.0's default. Set on <see cref="CurlClientOptions.Ssh"/> for every
/// request or on <see cref="CurlRequest.Ssh"/> for one; a request's record replaces the client's whole.
/// </summary>
public sealed record CurlSshOptions : IEasyOptionRecord<EasySshOptions>
{
    /// <summary><c>CURLOPT_SSH_AUTH_TYPES</c>: the authentication methods allowed. Default <see cref="SshAuthenticationTypes.Any"/>.</summary>
    public SshAuthenticationTypes AuthenticationTypes { get; init; } = SshAuthenticationTypes.Any;

    /// <summary><c>CURLOPT_SSH_COMPRESSION</c>: ask for compression. Default <see langword="false"/>.</summary>
    public bool Compression { get; init; }

    /// <summary><c>CURLOPT_SSH_HOST_PUBLIC_KEY_MD5</c>: the host key's expected MD5, 32 hex digits. Default <see langword="null"/>.</summary>
    public string? HostPublicKeyMd5 { get; init; }

    /// <summary><c>CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256</c>: the host key's expected SHA-256, base64. Default <see langword="null"/>.</summary>
    public string? HostPublicKeySha256 { get; init; }

    /// <summary><c>CURLOPT_SSH_PUBLIC_KEYFILE</c>: the public key file. Default <see langword="null"/>.</summary>
    public string? PublicKeyFile { get; init; }

    /// <summary><c>CURLOPT_SSH_PRIVATE_KEYFILE</c>: the private key file. Default <see langword="null"/>.</summary>
    public string? PrivateKeyFile { get; init; }

    /// <summary><c>CURLOPT_SSH_KNOWNHOSTS</c>: the known hosts file. Default <see langword="null"/>.</summary>
    public string? KnownHostsFile { get; init; }

    /// <summary>An <see cref="EasySshOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's SSH options.</returns>
    EasySshOptions IEasyOptionRecord<EasySshOptions>.ToEasy() => new()
    {
        AuthenticationTypes = AuthenticationTypes,
        Compression = Compression,
        HostPublicKeyMd5 = HostPublicKeyMd5,
        HostPublicKeySha256 = HostPublicKeySha256,
        PublicKeyFile = PublicKeyFile,
        PrivateKeyFile = PrivateKeyFile,
        KnownHostsFile = KnownHostsFile,
    };
}
