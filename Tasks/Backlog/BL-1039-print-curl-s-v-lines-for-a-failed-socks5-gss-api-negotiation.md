---
id: BL-1039
title: Print curl's -v lines for a failed SOCKS5 GSS-API negotiation
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-615]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1039 — Print curl's -v lines for a failed SOCKS5 GSS-API negotiation

## Goal

Under `-v`, a SOCKS5 proxy that picks GSS-API and whose negotiation fails prints curl 8.21.0's informational lines before the `curl: (97)` line, as each platform's build prints them.

## Context

- Found in BL-615 (ADR-0276): `Socks5GssapiNegotiation` returns the exit 97 message but reports no `-v` line. Measured there (BL-615 Notes): Windows prints `* SSPI error: InitializeSecurityContext failed: ...`, `* Failed to initialize security context.`, `* Unable to negotiate SOCKS5 GSS-API context.`; Linux prints `* GSS-API error: gss_init_sec_context failed: ...` (two lines), `* Failed to initial GSS-API token.`, `* Unable to negotiate SOCKS5 GSS-API context.`.
- Code: `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs`, `Socks5Handshake.cs`; the target's `Events` is how `TcpConnector` reports `-v` lines (ADR-0100). BL-1038 adds the `Opened SOCKS connection` line; look at how it reaches the SOCKS code first.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pin the measured `-v` lines, in order, for a context with no credential on each build (`UsesSspiTexts` true and false).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-30: Created.
