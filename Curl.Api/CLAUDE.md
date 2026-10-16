# Curl.Api

The .NET-idiomatic programming interface to Curl (ADR-0506), not a `Curl.<Area>.UnitLibrary`:
`Curl.Api` is a naming exception, with `Curl.Console` and `Libcurl`. Namespace `Curl.Api`
for `Client/` (`CurlClient` and `CurlClientOptions`) and `Composition/` (the engine-building
half of the partial `CurlClient`), `Messages/`, `ResponseBodies/`, `RequestBodies/`, `Mime/` and `Cancellation/`; `Curl.Api.Errors` for `Errors/`.

- `Cancellation/`: `TransferCancellation` (BL-2504, ADR-0639) carries a call's `CancellationToken`
  into its transfer: the `CURLOPT_XFERINFOFUNCTION` answers abort once the token is cancelled and
  the handle is unpaused so a paused transfer sees it. A transfer that fails after the caller
  cancelled throws `OperationCanceledException`; any other failure, a timeout included, throws
  `CurlException`; a token cancelled before the start creates no handle. Its tests drive
  `CancellableEasyTransfer`, a fake that ignores the perform token, in `Curl.Api.UnitTests/Cancellation`.

- `Messages/`: `CurlRequest` and `CurlResponse` records and the `SendAsync` half of the partial
  `CurlClient` (BL-2499, ADR-0632). `CurlRequestOptions` maps a request onto `EasyOptions`;
  `ResponseHeaderCollector` is the header callback that splits header lines into status code,
  version, reason, headers and trailers. `SendAsync` returns once the body starts or the transfer
  ends (ADR-0635). Its tests use the scripted fakes in `Curl.Api.UnitTests/Messages`.

- `ResponseBodies/`: the streamed `CurlResponse.Body` (BL-2501, ADR-0635). `ResponseBodyBuffer` is
  the write callback: a bounded buffer that answers `CURL_WRITEFUNC_PAUSE` when full and unpauses the
  handle once read down to half. `ResponseBodyStream` is the async-only, read-only body over it; it
  owns the easy handle and the handles the request body created (`Owned`), disposes them when the transfer ends, and aborts the transfer (unpausing it)
  when disposed first or when the `SendAsync` token is cancelled. Its tests drive `SteppedEasyTransfer`, a fake fed chunk by chunk, in
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
creates easy handles with `CurlClientOptions` applied (internal `CreateEasy`), and throws
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

Reference `Curl.Api.UnitLibrary` (the seam) and `Curl.Engine.UnitLibrary` (only for
`ManagedCurlEngine` in `Composition/`), nothing else. Never reference `Libcurl` or a protocol
library.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`.
