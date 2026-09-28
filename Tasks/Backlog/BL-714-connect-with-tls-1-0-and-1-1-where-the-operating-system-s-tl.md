---
id: BL-714
title: Connect with TLS 1.0 and 1.1 where the operating system's TLS stack refuses them
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-502, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-714 — Connect with TLS 1.0 and 1.1 where the operating system's TLS stack refuses them

## Goal

When the command line allows TLS 1.0 or 1.1 (`--tlsv1.0`, `--tlsv1.1`, `--tls-max 1.0`/`1.1`) and the operating system's TLS stack refuses to offer them (Windows 11 Schannel, OpenSSL 3 at its default security level), Curl connects through the hand-built TLS client as curl.se's official build of curl does, on every platform.

## Context

- Conformance audit 2026-09-28, row 5; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): what any official curl build does, Curl does everywhere. BL-502 makes `SslStream` negotiate the range it can and pins today's answer; this task closes the remaining gap. curl.se's Windows build of curl 8.22.0 uses LibreSSL 4.3.2 (https://curl.se/windows/, checked 2026-09-28); measure whether it completes a TLS 1.0 and a TLS 1.1 handshake with `--tlsv1.0 --tls-max 1.0` against a legacy server, and the OpenSSL build's answer, before pinning.
- Routing: BL-617's ADR and BL-708 (a new row: minimum or maximum below 1.2); protocol: BL-702 and BL-703.

## Acceptance criteria

- [ ] Measured first through `Record-CurlExchange.ps1 -NoServer` against a TLS 1.0-only and a TLS 1.1-only server (for example `openssl s_server -tls1 -cipher DEFAULT@SECLEVEL=0`); stderr and exit code copied into Notes per build.
- [ ] Tests show `--tlsv1.0 --tls-max 1.0` and `--tlsv1.1 --tls-max 1.1` completing a transfer through the hand-built client against in-memory TLS 1.0/1.1 servers on every platform, and the default range still using `SslStream`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
