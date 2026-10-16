namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The events of a <see cref="CurlWaitSocket"/> (<c>CURL_WAIT_POLL*</c>), with upstream curl
/// 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
[Flags]
public enum CurlWaitEvents
{
    /// <summary>0: no event.</summary>
    None = 0,

    /// <summary><c>CURL_WAIT_POLLIN</c> (<c>0x0001</c>): data to read.</summary>
    PollIn = 0x0001,

    /// <summary><c>CURL_WAIT_POLLPRI</c> (<c>0x0002</c>): urgent data to read.</summary>
    PollPriority = 0x0002,

    /// <summary><c>CURL_WAIT_POLLOUT</c> (<c>0x0004</c>): writing will not block.</summary>
    PollOut = 0x0004,
}
