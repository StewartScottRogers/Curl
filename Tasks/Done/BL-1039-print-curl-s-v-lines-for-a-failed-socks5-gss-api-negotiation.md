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
completed: 2026-10-02
---
# BL-1039 — Print curl's -v lines for a failed SOCKS5 GSS-API negotiation

## Goal

Under `-v`, a SOCKS5 proxy that picks GSS-API and whose negotiation fails prints curl 8.21.0's informational lines before the `curl: (97)` line, as each platform's build prints them.

## Context

- Found in BL-615 (ADR-0276): `Socks5GssapiNegotiation` returns the exit 97 message but reports no `-v` line. Measured there (BL-615 Notes): Windows prints `* SSPI error: InitializeSecurityContext failed: ...`, `* Failed to initialize security context.`, `* Unable to negotiate SOCKS5 GSS-API context.`; Linux prints `* GSS-API error: gss_init_sec_context failed: ...` (two lines), `* Failed to initial GSS-API token.`, `* Unable to negotiate SOCKS5 GSS-API context.`.
- Code: `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs`, `Socks5Handshake.cs`; the target's `Events` is how `TcpConnector` reports `-v` lines (ADR-0100). BL-1038 adds the `Opened SOCKS connection` line; look at how it reaches the SOCKS code first.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` pin the measured `-v` lines, in order, for a context with no credential on each build (`UsesSspiTexts` true and false).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Done directly rather than through the full `/feature` stages: a small change in one library, with BL-615's measurements as the specification.
- `target.Events` now reaches `Socks5GssapiNegotiation.RunAsync` through `SocksProxyTunnel.OpenAsync` and `Socks5Handshake.RunAsync` (the same `Events` BL-1038's `Opened SOCKS connection` line uses).
- `Socks5GssapiFailureText.VerboseLines` gives the lines: the exit 97 message (curl's `failf` echoes under `-v`); after a failed context step, `Failed to initialize security context.` (SSPI) or `Failed to initial GSS-API token.` (GSS-API); then `Unable to negotiate SOCKS5 GSS-API context.`. The GSS-API build's two-line message is one info text with its line feed, the same as the exit message.
- Choice (sensible default): every failed GSS-API negotiation, not only a missing credential, prints its message and then `Unable to negotiate SOCKS5 GSS-API context.`, because curl's `socks.c` calls that `failf` after any failure of `Curl_SOCKS5_gssapi_negotiate`. Only the no-credential case was measured. Other SOCKS failures (for example `cannot complete SOCKS5 connection`) still print no `-v` line, which is outside this task.
- Tests: `TcpConnectorTests.Socks5Authentication.cs`, four new tests (6 cases). They compare the last lines, because the `Trying` and connect lines come first.
- Gates: `dotnet build Curl.slnx -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing of 1206 members.

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A failed SOCKS5 GSS-API negotiation prints curl's -v lines for each platform's build (SSPI and GSS-API)
