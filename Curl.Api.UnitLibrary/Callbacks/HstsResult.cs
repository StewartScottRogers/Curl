namespace Curl.Api.Callbacks;

/// <summary>
/// What an <see cref="HstsReadCallback"/> or <see cref="HstsWriteCallback"/> returns: libcurl's
/// <c>CURLSTScode</c> (<c>CURLSTS_*</c>), with upstream curl 8.22.0's numbers.
/// </summary>
public enum HstsResult
{
    /// <summary><c>CURLSTS_OK</c> (0): the entry was handed over; call again for the next one.</summary>
    Ok = 0,

    /// <summary><c>CURLSTS_DONE</c> (1): there are no more entries.</summary>
    Done = 1,

    /// <summary><c>CURLSTS_FAIL</c> (2): fail the transfer.</summary>
    Fail = 2,
}
