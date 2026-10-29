# Curl.Api

The .NET-idiomatic programming interface to Curl (ADR-0506), not a `Curl.<Area>.UnitLibrary`:
`Curl.Api` is a naming exception, with `Curl.Console` and `Libcurl`. Namespace `Curl.Api`
for `Client/` (`CurlClient` and `CurlClientOptions`) and `Composition/` (the engine-building
half of the partial `CurlClient`), `Messages/`, `ResponseBodies/`, `RequestBodies/`, `Mime/`, `Cancellation/`, `Progress/`, `Concurrency/`, `Stores/` and `HttpMessageHandler/` (`Core/`, `Versions/`, `Proxy/`, `Behaviour/`); `Curl.Api.Errors` for `Errors/`.

- `Cancellation/`: `TransferCancellation` (BL-2504, ADR-0639) carries a call's `CancellationToken`
  into its transfer: the `CURLOPT_XFERINFOFUNCTION` answers abort once the token is cancelled and
  the handle is unpaused so a paused transfer sees it. A transfer that fails after the caller
  cancelled throws `OperationCanceledException`; any other failure, a timeout included, throws
  `CurlException`; a token cancelled before the start creates no handle. Its tests drive
  `CancellableEasyTransfer`, a fake that ignores the perform token, in `Curl.Api.UnitTests/Cancellation`.

- `Progress/`: `CurlRequest.Progress` (BL-2505, ADR-0643), an `IProgress<CurlTransferProgress>` given a
  report (download and upload totals and counts, the engine's speeds, `CurlTransferPhase`) per
  `CURLOPT_XFERINFOFUNCTION` call on the engine's thread. `TransferProgressReporter` is that callback,
  chained before the cancellation's; a report that throws aborts the transfer (`CurlException`,
  `AbortedByCallback`), and `ResponseBodyStream` ends its reports when perform returns. Its tests use
  `ProgressEasyTransfer` in `Curl.Api.UnitTests/Progress`.

- `Concurrency/`: `MultiTransferDriver` (BL-2509, ADR-0644) runs every `SendAsync` transfer of a client
  on one multi handle, created on the first transfer and disposed with the client: one loop adds,
  performs, finishes each `CURLMSG_DONE` transfer and waits in `PollAsync`, woken by `Wakeup`; only the
  loop calls the handle. A cancelled call's transfer is removed from the handle. `CurlClientOptions`'
  `MaxConnections` and `MaxConnectionsPerHost` (this folder's partial half) set
  `CURLMOPT_MAX_TOTAL_CONNECTIONS` and `CURLMOPT_MAX_HOST_CONNECTIONS`, and `CurlClient`'s partial half
  here holds the driver and the closing token `DisposeAsync` cancels to abort every running transfer.
  Its tests use `ConcurrentCurlEngine`, `GatedEasyTransfer` and `EasyPerformingMultiTransfer` (the fake
  multi handle every sending test engine returns) in `Curl.Api.UnitTests/Concurrency`.

- `Messages/`: `CurlRequest` and `CurlResponse` records and the `SendAsync` half of the partial
  `CurlClient` (BL-2499, ADR-0632). `CurlRequestOptions` maps a request onto `EasyOptions`;
  `ResponseHeaderCollector` is the header callback that splits header lines into status code,
  version, reason, headers and trailers. `SendAsync` returns once the body starts or the transfer
  ends (ADR-0635). Its tests use the scripted fakes in `Curl.Api.UnitTests/Messages`.

- `ResponseBodies/`: the streamed `CurlResponse.Body` (BL-2501, ADR-0635). `ResponseBodyBuffer` is
  the write callback: a bounded buffer that answers `CURL_WRITEFUNC_PAUSE` when full and unpauses the
  handle once read down to half. `ResponseBodyStream` is the async-only, read-only body over it; it
  owns the easy handle and the handles the request body created (`Owned`), disposes them when the transfer ends, and aborts the transfer (unpausing it)
  when disposed first, when the `SendAsync` token is cancelled or when the client is disposed. Its tests drive `SteppedEasyTransfer`, a fake fed chunk by chunk, in
  `Curl.Api.UnitTests/ResponseBodies`.

- `RequestBodies/`: `CurlContent`, the type of `CurlRequest.Body` (BL-2500, ADR-0634). `BytesContent`
  posts in-memory bytes as `CURLOPT_POSTFIELDS`; `StreamContent` uploads a stream through
  `StreamUpload`'s read and seek callbacks, which never let an exception reach the engine. Its tests
  use the uploading fakes in `Curl.Api.UnitTests/RequestBodies`. `CurlContent.ApplyTo` gets the engine and
  a list of owned handles, the body stream's `Owned`, disposed when the transfer ends.

- `Mime/`: `CurlMultipartContent`, the multipart `CurlContent` of immutable `CurlMimePart`s (BL-2506,
  ADR-0637): text, bytes, file, stream or nested parts with name, file name, type, encoder and headers,
  built on a new `IMimeBuilder` per send as `CURLOPT_MIMEPOST` (`MimeDataSetter` sets each content kind).
  A failing setter throws `CurlException`. Its tests use the MIME fakes in `Curl.Api.UnitTests/Mime`.

- `Errors/`: `CurlException`, the one exception every failure in `Curl.Api` throws (the caller's
  own cancellation aside, which is `OperationCanceledException`), carrying the `CurlCode`; `CurlException.ThrowIfFailed` maps a call's result to it, and `CurlCodeText` is its own
  copy of curl 8.22.0's `curl_easy_strerror` text (ADR-0630).

`CurlClient` (BL-2496) is constructed over an `ICurlEngine`, owns it unless told not to,
creates easy handles with `CurlClientOptions` applied (internal `CreateEasy`, which binds them to the
client's stores once it has any), and throws
`ObjectDisposedException` after `DisposeAsync`. Tests use the hand-written fakes in
`Curl.Api.UnitTests/Client`, never a real engine.

`CurlClient.Create()` and `Create(CurlClientOptions)` (BL-2497) build a client over a new
`ManagedCurlEngine`, constructed directly (no reflection), which the client owns and disposes.
`Curl.Api.UnitTests/Composition` is the one place tests use the real engine, for version info
and handle creation only; nothing is sent over the network.

`WebSockets/` (BL-2508, ADR-0633, namespace `Curl.Api`): `CurlClient.ConnectWebSocketAsync`
(the WebSocket half of the partial `CurlClient`) performs a handle with `CURLOPT_CONNECT_ONLY` 2 and
`CurlWebSocketOptions`' `CURLOPT_WS_OPTIONS`, and `CurlWebSocket` owns it: `ReceiveAsync` gives whole
`CurlWebSocketMessage`s (fragments joined, control frames between them), `ReadMessagesAsync`
(`CurlWebSocketMessageReader`) enumerates them through the peer's close frame, the send methods
frame text, binary, ping, pong, close and raw bytes, and `ReceiveFrameAsync`, `SendFrameAsync` and
`CurrentFrame` are the frame API. A call the engine answers `Again` waits 1 ms and asks again. Tests
use the fakes in `Curl.Api.UnitTests/WebSockets` (`FakeEasyWebSocket` and friends).

`Options/Transport/` (BL-2502, ADR-0636, namespace `Curl.Api`): `CurlNetworkOptions`, `CurlTlsOptions`
(with `CurlTlsPeerOptions`) and `CurlProxyOptions` map one to one onto `EasyNetworkOptions`,
`EasyTlsOptions` and `EasyProxyOptions`, validating each value as it is set (`TransportOptionGuard`).
They hang off `CurlClientOptions` and `CurlRequest` as `Network`, `Tls` and `Proxy` (the partial halves
live in this folder); `CurlTransportOptions.Apply` puts the client's on in `CreateEasy` and the
request's in `SendAsync`, a set record replacing its whole group. Tests in
`Curl.Api.UnitTests/Options/Transport`.

`Options/Protocol/` (BL-2503, ADR-0640, namespace `Curl.Api`): `CurlHttpOptions`,
`CurlAuthenticationOptions`, `CurlFtpOptions`, `CurlSshOptions`, `CurlMailOptions`, `CurlTftpOptions`,
`CurlTelnetOptions` and `CurlRtspOptions` follow the transport records onto `EasyHttpOptions`,
`EasyAuthenticationOptions` and `EasyProtocolOptions`, hanging off `CurlClientOptions` and `CurlRequest`
under the same names less `Curl` and `Options` (`Mail` for SMTP); `CurlProtocolOptions.Apply` puts them
on after the transport records. `CurlHttpOptions` leaves the request's headers, method and body to
`CurlRequest`; `CurlAuthenticationOptions.ToString()` redacts every secret. Tests in
`Curl.Api.UnitTests/Options/Protocol`.

`Stores/` (BL-2507, ADR-0645, namespace `Curl.Api`): `CurlStores` holds the `CurlCookieStore`
(Netscape cookie-file lines), `CurlHstsStore` (`HstsEntry`s) and `CurlAltSvcStore` (the alt-svc cache
file, with `CurlAltSvcEntry` lines), each loadable from and savable to curl's file format, and owns one
`IShareHandle` sharing cookies and HSTS. `CurlClientOptions.Stores` shares them between clients;
otherwise `CurlClient.Stores` (and `.Cookies`, `.Hsts`, `.AltSvc`) makes the client's own on first use,
disposed with the client. Once a client has stores, `CreateEasy` wraps each handle in
`StoreBoundEasyTransfer`, which applies them at perform (`CURLOPT_SHARE`, `CURLOPT_COOKIELIST` `ALL`
then each cookie, the HSTS read and write callbacks, the alt-svc file) and, at dispose, keeps the
transfer's `CURLINFO_COOKIELIST`. Tests use the fakes in `Curl.Api.UnitTests/Stores` (`StoreEngine` and friends).

`HttpMessageHandler/Core/` (BL-2510, ADR-0647, namespace `Curl.Api`): `CurlHttpMessageHandler`, an
`HttpMessageHandler` over a `CurlClient` it does not own. `HttpRequestMapping` turns an
`HttpRequestMessage` into a `CurlRequest` (request then content headers with values joined, no
`Content-Length`, the content as a stream body, the version as `CURLOPT_HTTP_VERSION` on the client's
`CurlHttpOptions`); `HttpResponseMapping` turns the `CurlResponse` into an `HttpResponseMessage`, headers
split by .NET's header table, the body streamed through `TrailerFillingStream`, which fills
`TrailingHeaders` at the body's end. A `CurlException` becomes `HttpRequestException`. Tests drive an
`HttpClient` over the existing fakes in `Curl.Api.UnitTests/HttpMessageHandler/Core`.

`HttpMessageHandler/Versions/` (BL-2511, ADR-0649): `HttpVersionPolicyGuard` lets only HTTP 1.0, 1.1, 2.0
and 3.0 through (a higher one under `RequestVersionOrLower` as 3.0) and checks the response's major
version against `VersionPolicy` (`Exact` the same, `OrHigher` no lower), the handler disposing a refused
response's body; either failure is `HttpRequestException` with `VersionNegotiationError`.
`HttpMessageHandler/Proxy/` (BL-2511, ADR-0649): the handler's partial half with `HttpClientHandler`'s
`Proxy`, `UseProxy` and `DefaultProxyCredentials`; `HandlerProxyMapping` asks the proxy per
request (`IsBypassed`, `GetProxy`) for `CURLOPT_PROXY` on the client's `CurlProxyOptions` (`""` for a
direct connection) and puts the proxy credentials on the request's `CurlAuthenticationOptions`. Tests in
`Curl.Api.UnitTests/HttpMessageHandler/Versions` and `/Proxy`.

`HttpMessageHandler/Behaviour/` (BL-2512, ADR-0650): the partial handler's `SocketsHttpHandler`
properties with its defaults. `AutomaticDecompression` sets `CURLOPT_ACCEPT_ENCODING` and drops the
decoded response's `Content-Encoding` and `Content-Length`; `AllowAutoRedirect` and
`MaxAutomaticRedirections` set `CURLFOLLOW_OBEYCODE` and `CURLOPT_MAXREDIRS` (a limit exceeded is
`HttpRequestException`) and move `RequestUri` to the effective URL; `Credentials` gives the strongest
scheme's user and password with `CURLAUTH_ONLY` unless `PreAuthenticate`; `UseCookies` sends
`CookieContainer`'s cookies as `CURLOPT_COOKIE` and stores the final response's `Set-Cookie`. Core's
`SendAsync` calls its `WithBehaviour`, then `HandlerProxyMapping`, and `ApplyBehaviour`. Tests in
`Curl.Api.UnitTests/HttpMessageHandler/Behaviour`.

Reference `Curl.Api.UnitLibrary` (the seam), `Curl.Extension.Composition.UnitLibrary` (only for the extensions' handlers `Create` passes the engine, off under `CURL_EXTENSIONS=0`; ADR-0999, ADR-1071) and `Curl.Engine.UnitLibrary` (only for
`ManagedCurlEngine` in `Composition/`), nothing else. Never reference `Libcurl` or a protocol
library.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`.

`README.md` (BL-2513) is the usage guide: each of its `csharp` blocks follows a `<!-- sample: name -->`
line and is identical to the source between the matching `// <sample name>` markers in
`Curl.Api.UnitTests/Readme/ReadmeSamples.cs`, which `ReadmeTests` runs against `ReadmeEngine`'s fakes; the
test also fails when a public type of `Curl.Api` is not named in backticks in the README. Change a
sample in both places.
