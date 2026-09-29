---
id: BL-620
title: Read, match and write an HSTS cache file as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-620 — Read, match and write an HSTS cache file as curl does

## Goal

An `HstsCache` in `Curl.Core.UnitLibrary` reads curl 8.21.0's HSTS file format (`[.]host "YYYYMMDD HH:MM:SS"` lines, `unlimited`, comments), answers whether a host (or a parent with `includeSubDomains`) is known and unexpired at a given time, applies a `Strict-Transport-Security` header value (`max-age`, `includeSubDomains`, `max-age=0` removal), and writes the file back in curl's format and order.

## Context

- Conformance audit 2026-09-28, row 20 (Major, M). Wiring is BL-621.
- Format: https://curl.se/docs/hsts.html (8.21.0 reference) and `CurlManual.txt` (`--hsts`). Take text in and give text out; inject `TimeProvider`.
- Measure the written file: run the reference curl with `--hsts cache.txt` against `Record-CurlExchange.ps1 -Tls -k` answering `Strict-Transport-Security: max-age=31536000; includeSubDomains`, and with `max-age=0`, and copy the file.

## Acceptance criteria

- [ ] Measured first as above; the written files copied into Notes.
- [ ] `Curl.Core.UnitTests` pin reading, matching (exact, subdomain, expired), header application and the written text byte for byte for the measured cases.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
