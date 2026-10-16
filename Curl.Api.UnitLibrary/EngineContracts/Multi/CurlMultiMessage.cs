using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.Multi;

/// <summary>One message from <c>curl_multi_info_read</c>: <c>struct CURLMsg</c>.</summary>
/// <param name="Type"><c>msg</c>: the message's kind; <see cref="CurlMultiMessageType.Done"/> is the only one libcurl sends.</param>
/// <param name="Transfer"><c>easy_handle</c>: the transfer the message is about.</param>
/// <param name="Result"><c>data.result</c>: the finished transfer's result.</param>
public sealed record CurlMultiMessage(CurlMultiMessageType Type, IEasyTransfer Transfer, CurlCode Result);
