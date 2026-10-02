---
id: BL-1105
title: Send --tls-earlydata as 0-RTT early data on a resumed --ssl-sessions session
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-710]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1105 — Send --tls-earlydata as 0-RTT early data on a resumed --ssl-sessions session

## Goal

`--tls-earlydata` routes to the hand-built TLS client and, on a TLS 1.3 session resumed from `--ssl-sessions` whose server allows early data, sends the first HTTP request as 0-RTT early data, as curl 8.21.0's OpenSSL build does, on every platform.

## Context

- Split from BL-710, which delivered `--ssl-sessions` (ADR-0319): `TlsSessionCache`, `HandBuiltTlsProvider` offering `ResumptionSession`, and the file load and save in `Curl.Console`.
- `Curl.Tls.UnitLibrary/Tls13ClientConnection.ConnectWithEarlyDataAsync` (BL-701) already sends early data, but needs the request bytes before the handshake: the HTTP handler today hands the TLS provider a connection and writes the request after the handshake. A deferred-handshake connection (the handshake runs on the first write, carrying it as early data) is the likely seam.
- The `Curl.Networking.Fakes` TLS 1.3 test server sends no NewSessionTicket and accepts no PSK; extend it (or reuse `Curl.Tls.UnitTests`' resumption server) so a resumed handshake runs through `HandBuiltTlsProvider` end to end.
- curl's `lib/vtls/openssl.c` at tag `curl-8_21_0` (`ossl_connect_step2` early data path) and `docs/cmdline-opts/tls-earlydata.md` give the `-v` lines; measure with an OpenSSL build through `Record-CurlExchange.ps1 -Tls -k` when one is available, and say so in Notes when not.

## Acceptance criteria

- [ ] A `TlsClientRoutingTests` row sends `--tls-earlydata` to the hand-built client.
- [ ] A test resumes a session through `HandBuiltTlsProvider` (second handshake offers the ticket the first received) and pins the early-data request bytes and the `-v` lines.
- [ ] Without a resumable session, or when the server rejects early data, the request is sent after the handshake and the transfer still succeeds, pinned by a test.
- [ ] `--ai-help` describes `--tls-earlydata` as honoured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
