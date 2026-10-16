using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;

namespace Libcurl.Multi;

/// <summary>
/// One message from <see cref="CurlMulti.InfoRead"/>: libcurl's <c>struct CURLMsg</c>, with the
/// engine's transfer mapped back to the <see cref="CurlEasy"/> that was added.
/// </summary>
/// <param name="Type"><c>msg</c>: the message's kind; <see cref="CurlMultiMessageType.Done"/> is the only one libcurl sends.</param>
/// <param name="Easy"><c>easy_handle</c>: the easy handle the message is about.</param>
/// <param name="Result"><c>data.result</c>: the finished transfer's result.</param>
public sealed record CurlMessage(CurlMultiMessageType Type, CurlEasy Easy, CurlCode Result);
