---
id: BL-815
title: Run the TLS 1.2, 1.1 and 1.0 client over a byte stream in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-703]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions/ADR-0158-tls-1-2-and-below-run-over-a-byte-stream-in-their-own-connection-and-stream.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-815 — Run the TLS 1.2, 1.1 and 1.0 client over a byte stream in the hand-built TLS client

## Goal

`Curl.Tls.UnitLibrary` connects to a TLS 1.2, 1.1 or 1.0 server over a caller's `Stream` and returns a stream that reads and writes application data, as `Tls13ClientConnection` and `Tls13ClientStream` do for TLS 1.3 (ADR-0157), so `Curl.Networking.UnitLibrary`'s hand-built `ITlsProvider` (BL-708) can run every version the routing rule sends it.

## Context

- Found by BL-708: BL-703 built `Tls12ClientHandshake` (I/O-free) and BL-702 the record states (`Tls12RecordWriteState`, `Tls12RecordReadState`), but nothing drives them over a byte stream. ADR-0140's "Connection" row names a `TlsClientConnection` that sends the hello and picks the TLS 1.3 or 1.2 path from the ServerHello; today only the TLS 1.3 half exists.
- Start from `Tls13ClientConnection.cs`, `Tls13RecordLayer.cs`, `Tls13ClientStream.cs` and `Tls13ConnectResult.cs`: the same shape (a static `ConnectAsync(Stream, settings, ITlsRandomSource, IServerCertificateVerifier, CancellationToken)` returning a result with the stream or a `TlsHandshakeFailure` with its `Origin`; only cancellation and transport failures thrown).
- The driver: send the ClientHello in a plaintext record; read records one at a time, framing handshake content that spans records with `HandshakeMessageReader`; hand handshake content to `ReceiveHandshake` and a `change_cipher_spec` to `ReceiveChangeCipherSpec`, switching the read state after the server's is accepted; write `Tls12OutgoingMessage`s in order, switching the write state after the client's ChangeCipherSpec; a received alert ends the handshake with origin server.
- The stream: application data under the negotiated record protection, `close_notify` sent on dispose, 0 returned at `close_notify` or a bare transport end with `CloseNotifyReceived` telling them apart (as ADR-0157 decides for TLS 1.3), any other alert thrown as `TlsAlertException`, HelloRequest ignored. It exposes the negotiated version, suite and ALPN protocol as the TLS 1.3 stream does.
- Either a separate `Tls12ClientConnection` or one connection that picks the path from the ServerHello, whichever ADR-0140's `TlsClientConnection` row is best served by; record the choice in an ADR if it departs from ADR-0140's names.
- Tests drive it against `Tls12TestServer` over `InMemoryPipe` (both in `Curl.Tls.UnitTests`), for TLS 1.2 ECDHE-GCM, TLS 1.2 CBC, TLS 1.0 CBC (with and without the empty-fragment split), and resumption.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` complete a TLS 1.2 exchange over a byte stream against `Tls12TestServer` (request written, response read, `close_notify` both ways), and a TLS 1.1 and a TLS 1.0 CBC exchange.
- [x] A rejected chain, a server alert during the handshake and a transport end mid-handshake each return a `TlsHandshakeFailure` with the right `Origin`; a bare transport end after the handshake reads 0 with `CloseNotifyReceived` false.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Tls.UnitLibrary`.

## Notes

- Plan: mirror the TLS 1.3 shape. Internal `Tls12RecordLayer` (reads whole records, unprotects with the read state in force, writes under the write state in force, serialised writes, best-effort alerts); public `Tls12ClientConnection.ConnectAsync`, `Tls12ClientStream`, `Tls12ConnectResult`. Tests: `Tls12RecordTestServer` puts `Tls12TestServer` on an `InMemoryPipe`; `Tls12TestServer.ReceiveClientFlight` is split into `ReceiveClientKeyExchange` and `ReceiveClientFinished` so the record server can switch its read state at the client's ChangeCipherSpec.
- Decision (ADR-0158, decided by Claude under Stewart's delegation): separate `Tls12ClientConnection`/`Tls12ClientStream` rather than ADR-0140's single version-picking `TlsClientConnection`, which needs one ClientHello spanning TLS 1.3 and 1.2 that neither handshake supports yet. ClientHello record carries 0x0301 (OpenSSL); later records must carry the negotiated version, otherwise `protocol_version`. HelloRequest ignored; one with a body is `decode_error`.
- Deviation from the Context's "close_notify sent on dispose": the stream follows ADR-0157 and `SslStream` - `ShutdownAsync` sends it once, disposing does not. The tests send `close_notify` both ways through `ShutdownAsync`.
- Default taken: `Tls12ClientSettings.InsertEmptyFragment` (default true) carries the TLS 1.0 CBC BEAST split into the write state; `--ssl-allow-beast` turns it off.
- `touches` widened to the ADR-0158 file under `Documentation/Planning/Decisions` for the decision record; no task in Doing names it.
- Handshake content spanning records needs no framing in the driver: `Tls12ClientHandshake.ReceiveHandshake` already buffers partial messages through `HandshakeMessageReader` (tested with 7-byte handshake records).
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Tls.UnitTests 666 passed, 36 of them new); `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line, 100% branch, 0 failing members, worst CRAP 10.
## Log

- 2026-09-28: Created.
- 2026-09-28: Filed by BL-708, which needs it to run TLS 1.2 and below through its provider.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Tls12ClientConnection runs TLS 1.2, 1.1 and 1.0 over a byte stream and Tls12ClientStream reads and writes application data (ADR-0158)
