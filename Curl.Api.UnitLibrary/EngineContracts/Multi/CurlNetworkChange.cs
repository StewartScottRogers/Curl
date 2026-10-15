namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The bitmask <c>CURLMOPT_NETWORK_CHANGED</c> takes (<c>CURLMNWC_*</c>), with upstream curl
/// 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
[Flags]
public enum CurlNetworkChange
{
    /// <summary>0: nothing to clear.</summary>
    None = 0,

    /// <summary><c>CURLMNWC_CLEAR_CONNS</c> (<c>1 &lt;&lt; 0</c>): close every idle connection.</summary>
    ClearConnections = 1 << 0,

    /// <summary><c>CURLMNWC_CLEAR_DNS</c> (<c>1 &lt;&lt; 1</c>): empty the DNS cache.</summary>
    ClearDns = 1 << 1,
}
