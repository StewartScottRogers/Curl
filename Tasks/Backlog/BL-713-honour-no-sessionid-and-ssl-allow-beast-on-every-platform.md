---
id: BL-713
title: Honour --no-sessionid and --ssl-allow-beast on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-490, BL-617, BL-701, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-713 — Honour --no-sessionid and --ssl-allow-beast on every platform

## Goal

`--no-sessionid` stops TLS session reuse between the transfers of one run, and `--ssl-allow-beast` (and `--proxy-ssl-allow-beast`) turns off the TLS 1.0 CBC 1/n-1 record split, on every platform, as curl 8.21.0's OpenSSL build does.

## Context

- Conformance audit 2026-09-28, row 2; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): these switches act, they are not parse-only. BL-489 parses them, BL-490 wires the others. `SslStream` has no control for either (Windows caches sessions process-wide; Schannel always splits), so BL-617's ADR decides the route: the hand-built client (BL-708) when the switch is given, with sessions from BL-701/BL-703 and the split from BL-702.
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1 -Tls -k -Connections 2`: two URLs with and without `--no-sessionid` (does the second handshake resume: record the ClientHello session ID and PSK), and `--tlsv1.0 --tls-max 1.0 --ssl-allow-beast` against a TLS 1.0 CBC server (record the first application-data record sizes).

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests show the second transfer resuming by default and doing a full handshake with `--no-sessionid`, and the first TLS 1.0 CBC application record split 1/n-1 by default and whole with `--ssl-allow-beast`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
