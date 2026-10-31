namespace Curl.Original.Protocol.Http;

/// <summary>
/// Is offered each HTTP/2 server push (<c>PUSH_PROMISE</c>) on a transfer's stream, as curl
/// 8.22.0's <c>lib/http2.c</c> <c>push_promise</c> offers one to a multi handle's push callback
/// (BL-2516). An <see cref="HttpProtocolHandler" /> given a sink runs its HTTP/2 connections with
/// SETTINGS_ENABLE_PUSH 1; one without keeps sending 0, so a server never pushes to it.
/// </summary>
public interface IHttp2PushSink
{
    /// <summary>
    /// Takes or refuses a push. It is called while the connection's frames are being read, so it
    /// must return without reading <paramref name="response" />; a refused push is reset with
    /// RST_STREAM CANCEL, as curl resets a push its callback denies.
    /// </summary>
    /// <param name="headers">
    /// The promised request's header block as curl's <c>h2_pushed_hdr</c> list holds it: one
    /// <c>name:value</c> string per field, pseudo-headers included, in the order they arrived.
    /// </param>
    /// <param name="response">The pushed response, read once the push is taken.</param>
    /// <returns><see langword="true" /> to take the push, <see langword="false" /> to refuse it.</returns>
    bool TryAcceptPush(IReadOnlyList<string> headers, IHttpPushedResponse response);
}
