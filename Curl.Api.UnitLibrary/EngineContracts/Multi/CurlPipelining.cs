namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The bitmask <c>CURLMOPT_PIPELINING</c> takes (<c>CURLPIPE_*</c>), with upstream curl 8.22.0's
/// numbers from <c>include/curl/multi.h</c>.
/// </summary>
[Flags]
public enum CurlPipelining
{
    /// <summary><c>CURLPIPE_NOTHING</c> (0): no pipelining and no multiplexing.</summary>
    Nothing = 0,

    /// <summary><c>CURLPIPE_HTTP1</c> (1): HTTP/1.1 pipelining; libcurl has ignored it since 7.62.0.</summary>
    Http1 = 1,

    /// <summary><c>CURLPIPE_MULTIPLEX</c> (2): multiplex transfers over HTTP/2 and HTTP/3 connections.</summary>
    Multiplex = 2,
}
