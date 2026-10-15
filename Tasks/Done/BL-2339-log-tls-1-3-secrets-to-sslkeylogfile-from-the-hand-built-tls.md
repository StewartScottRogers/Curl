---
id: BL-2339
title: Log TLS 1.3 secrets to SSLKEYLOGFILE from the hand-built TLS 1.3 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2339 — Log TLS 1.3 secrets to SSLKEYLOGFILE from the hand-built TLS 1.3 client

## Goal

With `SSLKEYLOGFILE` set off Windows, a TLS 1.3 handshake on the hand-built client appends the NSS key-log lines curl's OpenSSL build writes: `CLIENT_HANDSHAKE_TRAFFIC_SECRET`, `SERVER_HANDSHAKE_TRAFFIC_SECRET`, `CLIENT_TRAFFIC_SECRET_0`, `SERVER_TRAFFIC_SECRET_0` and `EXPORTER_SECRET` (and `CLIENT_EARLY_TRAFFIC_SECRET` when early data is offered), each `<label> <client random hex> <secret hex>`.

## Context

- BL-2327 (ADR-0518) added `Curl.Tls`'s `ITlsKeyLog`, `Tls12ClientSettings.KeyLog` (the TLS 1.2 `CLIENT_RANDOM` line), `Curl.Networking`'s `NssKeyLogFile`, `TlsClientOptions.KeyLogFile` read from `SSLKEYLOGFILE` by `Curl.Engine`'s `TlsClientOptionsMapping`, and a `TlsClientRouting` row sending such a connection to `HandBuiltTlsProvider`. TLS 1.3 logs nothing yet.
- Add `Tls13ClientSettings.KeyLog` and call it where `Tls13ClientHandshake` derives each secret (`Tls13KeySchedule.DeriveClientHandshakeTrafficSecret` and the rest); the client random is the ClientHello's (the inner hello's when ECH is accepted). Set it in `HandBuiltTlsProvider`'s `ClientSettings.ToTls13` from `TlsClientOptions.KeyLogFile`.
- Order as OpenSSL 3.5 writes them: client handshake, server handshake, then after the server Finished client traffic 0, server traffic 0, exporter.

## Acceptance criteria

- [x] A `Curl.Tls.UnitTests` test runs `Tls13ClientConnection` with a recording `ITlsKeyLog` and checks the five labels in order, each secret equal to the key schedule's.
- [x] A `Curl.Networking.UnitTests` test runs `HandBuiltTlsProvider` over TLS 1.3 with `KeyLogFile` set and finds the five lines in the file (excluded on macOS, which has no TLS 1.3 `SslStream` server).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary,Curl.Networking.UnitLibrary` passes.

## Notes

- Done directly rather than through the full `/feature` stages: the Context spelled out the design, so planning agents would have cost more than they saved.
- `Tls13ClientHandshake.LogSecret` logs with the random of the hello the transcript holds (`transcriptHello`): the inner hello when ECH is accepted, the outer one after a rejection, as OpenSSL logs the hello it continued with. `CLIENT_EARLY_TRAFFIC_SECRET` is logged at `Start` when early data is offered (pinned against RFC 8448's trace in `Rfc8448ResumedHandshakeTests`).
- `HandBuiltTlsProvider` now builds one `NssKeyLogFile` on `ClientSettings.KeyLog`, shared by `ToTls13` and `ToTls12`, rather than `ToTls12` building its own.
- Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary,Curl.Networking.UnitLibrary: 0 failing members. Fast tests: all green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The hand-built TLS 1.3 client logs its NSS key-log secrets to SSLKEYLOGFILE in OpenSSL's order
