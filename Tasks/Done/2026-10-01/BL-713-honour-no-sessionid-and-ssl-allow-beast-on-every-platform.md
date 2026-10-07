---
id: BL-713
title: Honour --no-sessionid and --ssl-allow-beast on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-490, BL-617, BL-701, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-713 — Honour --no-sessionid and --ssl-allow-beast on every platform

## Goal

`--no-sessionid` stops TLS session reuse between the transfers of one run, and `--ssl-allow-beast` (and `--proxy-ssl-allow-beast`) turns off the TLS 1.0 CBC 1/n-1 record split, on every platform, as curl 8.21.0's OpenSSL build does.

## Context

- Conformance audit 2026-09-28, row 2; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): these switches act, they are not parse-only. BL-489 parses them, BL-490 wires the others. `SslStream` has no control for either (Windows caches sessions process-wide; Schannel always splits), so BL-617's ADR decides the route: the hand-built client (BL-708) when the switch is given, with sessions from BL-701/BL-703 and the split from BL-702.
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1 -Tls -k -Connections 2`: two URLs with and without `--no-sessionid` (does the second handshake resume: record the ClientHello session ID and PSK), and `--tlsv1.0 --tls-max 1.0 --ssl-allow-beast` against a TLS 1.0 CBC server (record the first application-data record sizes).

## Acceptance criteria

- [x] Measured first as above; copied into Notes.
- [x] Tests show the second transfer resuming by default and doing a full handshake with `--no-sessionid`, and the first TLS 1.0 CBC application record split 1/n-1 by default and whole with `--ssl-allow-beast`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-28 (BL-702): the split curl's LibreSSL and OpenSSL builds make is an empty application data record before each TLS 1.0 CBC write, not 1/n-1 (Schannel's); `Tls12RecordWriteState.Create(..., insertEmptyFragment: false)` is the `--ssl-allow-beast` switch. See ADR-0150; measure and pin that shape.
- 2026-10-01 (BL-713): measured with WSL's curl 8.18.0 / OpenSSL 3.5.5 against `openssl s_server` on loopback (Record-CurlExchange's server is SslStream on Windows, which cannot serve TLS 1.0 or report resumption). Two URLs, `-H "Connection: close"`: by default the second handshake resumes (`* SSL reusing session with ALPN '-'`, no Certificate/CertificateVerify, server counts 1 cache hit); with `--no-sessionid` both are full handshakes. `--tlsv1.0 --tls-max 1.0 --ciphers AES128-SHA:@SECLEVEL=0` against `s_server -tls1`: client application records `17 03 01 00 24` (empty fragment) then `17 03 01 00 64`; with `--ssl-allow-beast` only `17 03 01 00 64`. Pinned in ADR-0330.
- 2026-10-01 (BL-713): decisions in ADR-0330: the hand-built provider now always gets the run's `TlsSessionCache` (resumes by default, as curl); `TlsClientOptions.NoSessionId` routes hand-built and offers/keeps nothing; `AllowBeast` routes hand-built only with a TLS 1.0 minimum (a 1.0/1.1 ceiling already routes) and sets `InsertEmptyFragment` off; the proxy forms map onto the proxy's options. Tests: `HandBuiltTlsProviderTests.SessionIdAndBeast` (PSK offered from the cache by default, none and cache untouched with NoSessionId, `InsertsEmptyFragment`), with `Curl.Tls`'s existing `TlsOneZeroCbcWritesAnEmptyRecordFirstUnlessTurnedOff` showing the record shapes; routing and mapping rows. Measure-CodeQuality: Networking and Console 100/100, 0 failing.
- 2026-10-01 (BL-713): added `Curl.Cli.UnitLibrary` to touches (no Doing task held it) to correct the `AllowBeast` and `ReuseSessionIds` doc comments that said "Parsed only". Not done: curl's `-v` line `* SSL reusing session with ALPN '-'`.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --no-sessionid routes to the hand-built client, which resumes from the run's session cache by default and not at all under it; --ssl-allow-beast turns off the TLS 1.0 CBC empty fragment
