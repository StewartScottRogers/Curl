---
id: BL-622
title: Read, match and write an alt-svc cache file as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-622 — Read, match and write an alt-svc cache file as curl does

## Goal

An `AltSvcCache` in `Curl.Core.UnitLibrary` reads curl 8.21.0's alt-svc file format (one line per entry: source ALPN, host, port, destination ALPN, host, port, expiry, persist and priority fields), parses `Alt-Svc` header values (RFC 7838: `h1`/`h2`/`h3` alternatives, `ma`, `persist`, `clear`), answers the alternative for an origin at a given time, and writes the file back in curl's format.

## Context

- Conformance audit 2026-09-28, row 20 (Major, M). Wiring is BL-623.
- Format: https://curl.se/docs/alt-svc.html (8.21.0 reference) and `CurlManual.txt` (`--alt-svc`). Take text in, give text out; inject `TimeProvider`.
- Measure the written file: the reference curl with `--alt-svc cache.txt` against `Record-CurlExchange.ps1 -Tls -k` answering `Alt-Svc: h2=":8443"; ma=60, h3=":443"` and `Alt-Svc: clear`; copy the file.

## Acceptance criteria

- [ ] Measured first as above; the written files copied into Notes.
- [ ] `Curl.Core.UnitTests` pin reading, header parsing, lookup (expired, cleared) and the written text byte for byte for the measured cases.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
