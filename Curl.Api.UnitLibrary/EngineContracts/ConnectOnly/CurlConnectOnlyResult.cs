using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.ConnectOnly;

/// <summary>What one <c>curl_easy_recv</c> or <c>curl_easy_send</c> call returned.</summary>
/// <param name="Result">The call's result; <see cref="CurlCode.Again"/> when the socket has nothing ready.</param>
/// <param name="ByteCount"><c>*n</c>: how many bytes were received or sent.</param>
public readonly record struct CurlConnectOnlyResult(CurlCode Result, int ByteCount);
