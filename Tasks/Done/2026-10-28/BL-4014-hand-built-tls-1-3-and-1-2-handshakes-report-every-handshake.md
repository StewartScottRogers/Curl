---
id: BL-4014
title: Hand-built TLS 1.3 and 1.2 handshakes report every handshake message sent and received to an observer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/Tls13ClientHandshake.cs, Curl.Tls.UnitLibrary/Tls12ClientHandshake.cs, Curl.Tls.UnitLibrary/Tls13ClientConnection.cs, Curl.Tls.UnitLibrary/Tls12ClientConnection.cs, Curl.Tls.UnitLibrary/TlsClientConnection.cs, Curl.Tls.UnitLibrary/Tls13ClientSettings.cs, Curl.Tls.UnitLibrary/Tls12ClientSettings.cs, Curl.Tls.UnitLibrary/TlsHandshakeMessageObserver.cs, Curl.Tls.UnitLibrary/Tls12RecordContent.cs, Curl.Tls.UnitLibrary/Tls12RecordLayer.cs, Curl.Tls.UnitTests/TlsHandshakeMessageObserverTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4014 — Hand-built TLS 1.3 and 1.2 handshakes report every handshake message sent and received to an observer

## Goal

`Tls13ClientHandshake` and `Tls12ClientHandshake` (and the connections over them) hand an optional observer every handshake message they send and every one they receive - header included, the TLS 1.3 ones after ServerHello as decrypted - with its direction and record version, plus the ChangeCipherSpec records, in wire order.

## Context

Split out of BL-4009 (OpenSSL's `-v` message-callback trace from the hand-built client). `SslStream` hides TLS 1.3 messages after ServerHello; only the hand-built client sees them. Curl.Tls does not reference `Curl.Protocol.Abstractions`' `ITransferEvents`, so the hook is a Curl.Tls type (e.g. a `TlsHandshakeMessageObserver` delegate or interface on `Tls13ClientSettings`/`Tls12ClientSettings`, which `Tls13ClientConnection`, `Tls12ClientConnection` and `TlsClientConnection` pass through). Sent messages leave through `Tls13ClientHandshake.SendHandshakeMessage` and the ClientHello send in `Start`; received ones are framed by `HandshakeMessageReader` in `ReceiveAtLevel`. TLS 1.2 sends `Tls12OutgoingMessage`s and takes `ReceiveHandshake`/`ReceiveChangeCipherSpec`. Decide whether the ChangeCipherSpec and the record version belong in the handshake classes or the connection classes (the connections write the records) and record it in an ADR. QUIC's `QuicClientConnectionState` builds `Tls13ClientHandshake` too; leave the observer off there.

## Acceptance criteria

- [x] A unit test in `Curl.Tls.UnitTests` pins, in order, the messages (type, direction, bytes) a TLS 1.3 handshake against `Tls13RecordTestServer` reports, from ClientHello to the client Finished.
- [x] A unit test pins the same for a TLS 1.2 handshake against `Tls12RecordTestServer`, ChangeCipherSpec included.
- [x] No observer set reports nothing and changes nothing (existing tests green).
- [x] Curl.Tls.UnitLibrary stays at 100% line and branch coverage, complexity at most 10.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-0971): `TlsHandshakeMessageObserver` is a delegate (direction as `TlsTrafficDirection`, content type, record version, bytes) on `Tls13ClientSettings`/`Tls12ClientSettings.HandshakeMessageObserver`. The connections call it for every sent handshake message and `change_cipher_spec` (with the record header version they write) and every received `change_cipher_spec`, since only they know the wire order and record versions; received handshake messages come from the handshake classes, which frame them and hand each whole message to an internal `MessageReceived` hook the connection binds with the version of the record that completed it. QUIC never sets the hook. `TlsClientConnection` needed no change: it passes each version's settings through.
- Added `Curl.Tls.UnitLibrary/Tls12RecordContent.cs` and `Tls12RecordLayer.cs` to touches: TLS 1.2 received records did not carry their header version. No other task in Doing on origin/work/dark-factory names any Curl.Tls file.
- Measured: Curl.Tls.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10. Fast tests green (46 test projects). The TLS 1.2 test server sends no NewSessionTicket, so the 1.2 trace pins ClientHello to the server Finished without one; its 100-byte handshake records make received messages span records.
- The project CLAUDE.md does not yet mention the observer; it was outside touches.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: eb996827 a2c21a96 17840642 a221f870 3d836d5e.
- 2026-10-28: Doing -> Done. Hand-built TLS 1.3 and 1.2 connections report every handshake message and change_cipher_spec, direction and record version, to a TlsHandshakeMessageObserver in wire order
