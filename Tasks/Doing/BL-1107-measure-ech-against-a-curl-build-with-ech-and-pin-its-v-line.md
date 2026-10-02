---
id: BL-1107
title: Measure --ech against a curl build with ECH and pin its -v lines and exit 101 text
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-711]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1107 — Measure --ech against a curl build with ECH and pin its -v lines and exit 101 text

## Goal

Under `-v`, `--ech` writes curl 8.21.0's `* ECH: ...` information lines, and a rejected offer's exit 101 prints the OpenSSL build's own error text. Both are measured with a curl build that has ECH.

## Context

- ADR-0327 (BL-711) took these from source. No build on the machine had ECH: the mingw Schannel 8.21.0 build and WSL's OpenSSL 8.18.0 build show no `ECH` in `curl -V`. Today exit 101 prints `ECH attempted but failed` (`TlsFailureMessages.EchRequired`), and no `ECH:` line is written.
- Build or get a curl with ECH (OpenSSL 4.0 or BoringSSL), for example in WSL. Then run `Record-CurlExchange.ps1 -Tls -k` with `--ech grease|true|hard` and `ecl:`/`pn:` against a server without ECH, with and without `--doh-url`. Copy stderr and the exit code into Notes. Also check the DoH HTTPS query bytes BL-707 pinned.

## Acceptance criteria

- [ ] The measurements are in Notes.
- [ ] `Curl.Networking.UnitTests` pin each mode's `-v` ECH lines and the exit 101 text as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
