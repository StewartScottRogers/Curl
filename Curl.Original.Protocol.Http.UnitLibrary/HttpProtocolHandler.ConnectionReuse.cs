using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that decides whether a connection stays alive and whether a request may go again on a fresh one.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Decides whether the connection can carry another request: not after a body a status of
    /// 300 or above cut short, which curl 8.21.0 shuts the connection on (measured, BL-319 and
    /// BL-395 Notes), nor after a 101, nor when the body was left unread; and else as
    /// <see cref="HttpConnectionPersistence" /> says (ADR-0050), for a head the peer closed among
    /// its headers as for one with no body, since curl 8.21.0 decides that a body runs until the
    /// server closes only at the head's empty line and leaves such a connection intact
    /// (measured, BL-483 Notes). Over HTTP/1.x it also cannot after bytes followed the body
    /// (<paramref name="foundExcess" />), nor after body bytes nobody asked for
    /// (<see cref="ReceivedUnwantedBody" />).
    /// </summary>
    /// <remarks>
    /// Over HTTP/2 and HTTP/3 the stream, not the connection, ends with the response, so the connection
    /// carries another request once the exchange ran whole unless the peer sent GOAWAY or
    /// closed it (<see cref="IHttpStreamSession.AcceptsNewStreams" />); an upload a status cut
    /// short ends only its stream too (measured over HTTP/2, BL-5973 Notes).
    /// </remarks>
    private static bool KeepsAlive(HttpRequestPlan plan, HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery, IHttpStreamSession? streams, bool foundExcess) =>
        (streams is not null || !upload.CutShort)
            && DeliveredResponseWhole(head, headReader, delivery)
            && (streams?.AcceptsNewStreams ?? PersistsOverHttp1(plan, head, headReader, foundExcess));

    /// <summary>What <see cref="KeepsAlive" /> asks of an HTTP/1.x connection beyond a whole exchange.</summary>
    private static bool PersistsOverHttp1(HttpRequestPlan plan, HttpResponseHead head, HttpResponseHeadReader headReader, bool foundExcess) =>
        !foundExcess && !ReceivedUnwantedBody(plan, head) && HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody || !headReader.EndedAtEmptyLine, plan.Options.PassesTransferCoding, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding, IsThroughHttpProxy(plan.Options));

    /// <summary>
    /// Decides whether body bytes arrived with the head of a response to <c>-I</c>, which wants
    /// none: curl 8.22.0 then shuts the connection down whatever the head says, a
    /// <c>Content-Length</c> or none, a <c>204</c> or HTTP/1.0's <c>keep-alive</c> (measured,
    /// BL-5948 Notes).
    /// </summary>
    private static bool ReceivedUnwantedBody(HttpRequestPlan plan, HttpResponseHead head) =>
        plan.Context.NoBody && !head.BodyPrefix.IsEmpty;

    /// <summary>
    /// Decides whether the connection is reported left intact although the server closed it
    /// to end the body, as curl 8.21.0 does for an HTTP/1.0 keep-alive response with no length
    /// (measured, BL-471 Notes; ADR-0324). It is marked reusable, as curl pools it, and the pool
    /// reports it dead before any reuse (ADR-0112).
    /// </summary>
    private static bool LeftIntactAfterServerClosed(HttpRequestPlan plan, HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        (DeliveredWhole(head, upload, headReader, delivery)
            && HttpConnectionPersistence.KeepsHttp10AliveUntilServerCloses(head, plan.Context.NoBody, plan.Options.PassesTransferCoding, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding))
        || EndedAtUnsolicitedSwitchingProtocols(head, upload, headReader, delivery);

    /// <summary>
    /// Decides whether the response was a final unsolicited <c>101</c> whose body ran to the close,
    /// which curl 8.22.0 reports left intact and pools, the pool then finding it dead (measured, BL-3870, ADR-0939).
    /// </summary>
    private static bool EndedAtUnsolicitedSwitchingProtocols(HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        !upload.CutShort
            && delivery == HttpBodyDelivery.Deliver
            && headReader.SwitchedProtocols
            && !headReader.IsSwitchedToHttp2()
            && head.StatusLine.StatusCode == 101;

    /// <summary>
    /// Decides whether the exchange ran whole: its request body was not cut short, its
    /// response body was delivered, or was the server's own answer to <c>-C</c> or <c>-z</c>
    /// (<see cref="HttpDownloadConditions.IsServerAnswer" />), and the response did not switch
    /// protocols, unless it switched to HTTP/2 after an h2c upgrade's <c>101</c> and was read
    /// from stream 1 (BL-866).
    /// </summary>
    private static bool DeliveredWhole(HttpResponseHead head, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        !upload.CutShort && DeliveredResponseWhole(head, headReader, delivery);

    /// <summary>Decides what <see cref="DeliveredWhole" /> does of the response alone, whatever became of the request body.</summary>
    private static bool DeliveredResponseWhole(HttpResponseHead head, HttpResponseHeadReader headReader, HttpBodyDelivery delivery) =>
        (delivery == HttpBodyDelivery.Deliver || HttpDownloadConditions.IsServerAnswer(head, delivery))
            && (!headReader.SwitchedProtocols || headReader.IsSwitchedToHttp2());

    /// <summary>
    /// Decides whether a failed exchange was on a reused connection that died while idle, so
    /// the request is sent again once on a fresh one, as curl 8.21.0 does (BL-336 Notes): the
    /// connection was reused, from the pool or by an earlier request of this transfer such as
    /// the one a 401 answered (BL-1797), it failed sending or receiving before any byte of the response
    /// arrived, the request has not already been sent again for this reason, and its body, if
    /// any, is bytes that can be sent again.
    /// </summary>
    private static bool DiedBeforeResponse(HttpRequestPlan plan, bool reusedConnection, HttpResponseHeadReader headReader, HttpTransferException failure) =>
        !headReader.HasReceived
            && failure.ExitCode is CurlExitCode.GotNothing or CurlExitCode.SendError or CurlExitCode.RecvError
            && CanSendAgainOnFreshConnection(plan, reusedConnection);

    /// <summary>
    /// Decides whether <paramref name="plan" /> may be sent again on a fresh connection: it went
    /// out on a reused one, it has not already been sent again, and its body, if any, is bytes
    /// that can be sent again or an empty stream, which has nothing to rewind (a libcurl handle's
    /// zero-byte MIME post, upstream test584, BL-2620).
    /// </summary>
    private static bool CanSendAgainOnFreshConnection(HttpRequestPlan plan, bool reusedConnection) =>
        reusedConnection
            && !plan.SentOnFreshConnection
            && BodyCanBeSentAgain(plan.Framing.Body);
}
