using Curl.Api.EngineContracts.Multi;
using Curl.Original.Protocol.Http;

namespace Curl.Engine;

/// <summary>
/// The push sink the HTTP handler of a transfer in a multi handle with <see cref="MultiOptions.Push" /> set is given
/// (BL-2469): each HTTP/2 <c>PUSH_PROMISE</c> on the transfer's streams becomes a duplicate of the transfer bound to
/// the pushed response, offered to <see cref="ManagedMultiTransfer.OfferPush" /> with the promise's header lines, as
/// curl 8.22.0's <c>push_promise</c> in <c>lib/http2.c</c> duplicates the parent handle and asks the push callback.
/// </summary>
/// <param name="parent">The transfer the server pushes on.</param>
/// <param name="multi">The multi handle <paramref name="parent" /> is in.</param>
internal sealed class EngineHttp2PushSink(ManagedEasyTransfer parent, ManagedMultiTransfer multi) : IHttp2PushSink
{
    /// <summary>
    /// Takes the push when the multi handle's push callback answers <see cref="CurlPushResult.Ok" />: the duplicate
    /// joins the handle and reads <paramref name="response" /> on its next perform. Any other answer refuses it, and
    /// <see cref="CurlPushResult.ErrorOut" /> also ends the parent with <c>CURLE_HTTP2</c>.
    /// </summary>
    /// <param name="headers">The promised request's header lines, <c>name:value</c> as curl's <c>h2_pushed_hdr</c> holds them.</param>
    /// <param name="response">The pushed response.</param>
    /// <returns><see langword="true" /> when the pushed transfer joined the multi handle.</returns>
    public bool TryAcceptPush(IReadOnlyList<string> headers, IHttpPushedResponse response) =>
        multi.OfferPush(parent, parent.DuplicateForPush(response), headers) == CurlPushResult.Ok;
}
