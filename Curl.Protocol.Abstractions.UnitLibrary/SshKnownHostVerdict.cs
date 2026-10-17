namespace Curl.Protocol.Abstractions;

/// <summary>
/// What <see cref="SshOptions.KnownHostDecision" /> decides about the server's SSH host key, numbered as
/// libcurl's <c>enum curl_khstat</c> (ADR-0623). Any value not named here rejects the key, as libcurl's
/// <c>default:</c> case does.
/// </summary>
public enum SshKnownHostVerdict
{
    /// <summary>Accept the key and add it to the known-hosts file.</summary>
    AcceptAndAddToFile = 0,

    /// <summary>Accept the key.</summary>
    Accept = 1,

    /// <summary>Reject the key: the transfer fails with exit 60.</summary>
    Reject = 2,

    /// <summary>Neither accept nor reject the key now: the transfer fails with exit 60.</summary>
    Defer = 3,

    /// <summary>Accept the key and replace the host's old key in the known-hosts file.</summary>
    AcceptAndReplaceInFile = 4,
}
