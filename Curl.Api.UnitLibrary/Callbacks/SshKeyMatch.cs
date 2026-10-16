namespace Curl.Api.Callbacks;

/// <summary>
/// How a server's SSH host key compares with the known-hosts file: libcurl's
/// <c>enum curl_khmatch</c> (<c>CURLKHMATCH_*</c>), with upstream curl 8.22.0's numbers. An
/// <see cref="SshHostKeyCallback"/> returns <see cref="Ok"/> or <see cref="Mismatch"/>.
/// </summary>
public enum SshKeyMatch
{
    /// <summary><c>CURLKHMATCH_OK</c> (0): the key matches.</summary>
    Ok = 0,

    /// <summary><c>CURLKHMATCH_MISMATCH</c> (1): the host is known with a different key.</summary>
    Mismatch = 1,

    /// <summary><c>CURLKHMATCH_MISSING</c> (2): the host is not in the file.</summary>
    Missing = 2,
}
