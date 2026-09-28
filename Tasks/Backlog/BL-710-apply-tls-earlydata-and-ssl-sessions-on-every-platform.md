---
id: BL-710
title: Apply --tls-earlydata and --ssl-sessions on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-701, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-710 — Apply --tls-earlydata and --ssl-sessions on every platform

## Goal

`--ssl-sessions <file>` loads TLS sessions from the file before the transfers and saves them after, in curl 8.21.0's file format, and `--tls-earlydata` sends the request as 0-RTT early data on a resumed session, as curl does, on every platform.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Parsing: BL-618; routing: BL-617's ADR and BL-708; session records and early data: BL-701.
- The file format is curl's (`lib/vtls/vtls_spack.c` and `docs/cmdline-opts/ssl-sessions.md` at tag `curl-8_21_0`: read them and record the format in the XML docs); sessions whose TLS backend data curl cannot reuse are skipped as curl skips them.
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1 -Tls -k`: two runs sharing `--ssl-sessions f` (the second resumes), `--tls-earlydata` on the second run, and `-v` lines for both; the file bytes copied into Notes.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests pin the session file bytes written for a fixed session, a resumed second transfer with the early-data request bytes, the `-v` lines, and a corrupt file handled as curl handles it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
