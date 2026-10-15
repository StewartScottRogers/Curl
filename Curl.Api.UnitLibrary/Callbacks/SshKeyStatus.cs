namespace Curl.Api.Callbacks;

/// <summary>What an <see cref="SshKeyCallback"/> returns: libcurl's <c>enum curl_khstat</c> (<c>CURLKHSTAT_*</c>), with upstream curl 8.22.0's numbers.</summary>
public enum SshKeyStatus
{
    /// <summary><c>CURLKHSTAT_FINE_ADD_TO_FILE</c> (0): accept the key and add it to the known-hosts file.</summary>
    FineAddToFile = 0,

    /// <summary><c>CURLKHSTAT_FINE</c> (1): accept the key.</summary>
    Fine = 1,

    /// <summary><c>CURLKHSTAT_REJECT</c> (2): reject the key and close the connection.</summary>
    Reject = 2,

    /// <summary><c>CURLKHSTAT_DEFER</c> (3): neither accept nor reject the key now; the connection fails.</summary>
    Defer = 3,

    /// <summary><c>CURLKHSTAT_FINE_REPLACE</c> (4): accept the key and replace the old one in the known-hosts file.</summary>
    FineReplace = 4,
}
