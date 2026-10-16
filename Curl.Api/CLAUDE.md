# Curl.Api

The .NET-idiomatic programming interface to Curl (ADR-0506), not a `Curl.<Area>.UnitLibrary`:
`Curl.Api` is a naming exception, with `Curl.Console` and `Libcurl`. Namespace `Curl.Api`
for `Client/` (`CurlClient` and `CurlClientOptions`) and `Composition/` (the engine-building
half of the partial `CurlClient`), `Messages/`, `ResponseBodies/` and `RequestBodies/`; `Curl.Api.Errors` for `Errors/`.

- `Messages/`: `CurlRequest` and `CurlResponse` records and the `SendAsync` half of the partial
  `CurlClient` (BL-2499, ADR-0632). `CurlRequestOptions` maps a request onto `EasyOptions`;
  `ResponseHeaderCollector` is the header callback that splits header lines into status code,
  version, reason, headers and trailers. `SendAsync` returns once the body starts or the transfer
  ends (ADR-0635). Its tests use the scripted fakes in `Curl.Api.UnitTests/Messages`.

- `ResponseBodies/`: the streamed `CurlResponse.Body` (BL-2501, ADR-0635). `ResponseBodyBuffer` is
  the write callback: a bounded buffer that answers `CURL_WRITEFUNC_PAUSE` when full and unpauses the
  handle once read down to half. `ResponseBodyStream` is the async-only, read-only body over it; it
  owns the easy handle, disposes it when the transfer ends, and aborts the transfer when disposed
  first. Its tests drive `SteppedEasyTransfer`, a fake fed chunk by chunk, in
  `Curl.Api.UnitTests/ResponseBodies`.

- `RequestBodies/`: `CurlContent`, the type of `CurlRequest.Body` (BL-2500, ADR-0634). `BytesContent`
  posts in-memory bytes as `CURLOPT_POSTFIELDS`; `StreamContent` uploads a stream through
  `StreamUpload`'s read and seek callbacks, which never let an exception reach the engine. Its tests
  use the uploading fakes in `Curl.Api.UnitTests/RequestBodies`.

- `Errors/`: `CurlException`, the one exception every failure in `Curl.Api` throws, carrying the
  `CurlCode`; `CurlException.ThrowIfFailed` maps a call's result to it, and `CurlCodeText` is its own
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

Reference `Curl.Api.UnitLibrary` (the seam) and `Curl.Engine.UnitLibrary` (only for
`ManagedCurlEngine` in `Composition/`), nothing else. Never reference `Libcurl` or a protocol
library.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`.
