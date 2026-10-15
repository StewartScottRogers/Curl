# Curl.Conformance.Http2Server.UnitLibrary

The upstream case runner's stand-in for nghttpx serving h2c in front of `sws` (upstream's
`http/2` server), so the HTTP/2 cases in upstream's `tests/data` can run in memory (BL-2244).
Read ADR-0491 before changing it.

## Rules

- It references `Curl.Http2.UnitLibrary` for HPACK and the frame codec; never copy that code
  here. It references `Curl.Conformance.UnitLibrary` (for `InMemoryDuplexStream`) and
  `Curl.Protocol.Abstractions.UnitLibrary`, and no `Curl.Protocol.*` library beyond that.
- BCL only, no socket: connections are `InMemoryDuplexStream` pairs.
- Held to the `*.UnitLibrary` gates: 100% line and branch coverage, complexity at most 10.

## Shape

- `UpstreamHttp2ServerFactory.Create(backend)` is the one public entry: an `IConnector`
  (`Http2ServerConnector`) over the backend, the case's sws emulation.
- `Http2ServerSession` serves one connection: preface or `Upgrade: h2c`, SETTINGS, PING,
  WINDOW_UPDATE, header blocks with CONTINUATION, DATA, and GOAWAY at the end.
- `Http2ServerRequestRelay` turns a stream's request into nghttpx's HTTP/1.1 request and the
  backend's reply (read by `Http2ServerBackendReader`) into an `Http2ServerReply`.
