namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The counts <c>curl_multi_get_offt</c> reports (<c>CURLMINFO_*</c>), with upstream curl 8.22.0's
/// numbers from <c>include/curl/multi.h</c>.
/// </summary>
public enum CurlMultiInfo
{
    /// <summary><c>CURLMINFO_NONE</c> (0): no count; never asked for.</summary>
    None = 0,

    /// <summary><c>CURLMINFO_XFERS_CURRENT</c> (1): transfers added and not yet removed.</summary>
    TransfersCurrent = 1,

    /// <summary><c>CURLMINFO_XFERS_RUNNING</c> (2): transfers running, neither done nor pending.</summary>
    TransfersRunning = 2,

    /// <summary><c>CURLMINFO_XFERS_PENDING</c> (3): transfers waiting for a connection or a limit.</summary>
    TransfersPending = 3,

    /// <summary><c>CURLMINFO_XFERS_DONE</c> (4): transfers done and not yet removed.</summary>
    TransfersDone = 4,

    /// <summary><c>CURLMINFO_XFERS_ADDED</c> (5): transfers ever added to the multi handle.</summary>
    TransfersAdded = 5,
}
