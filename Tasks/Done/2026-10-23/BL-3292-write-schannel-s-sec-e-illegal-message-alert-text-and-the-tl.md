---
id: BL-3292
title: Write Schannel's SEC_E_ILLEGAL_MESSAGE alert text and the --tls-max 1.2 SEC_E_UNSUPPORTED_FUNCTION failure
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3292 — Write Schannel's SEC_E_ILLEGAL_MESSAGE alert text and the --tls-max 1.2 SEC_E_UNSUPPORTED_FUNCTION failure

## Goal

A handshake the server ends with a fatal alert writes the same `schannel: next InitializeSecurityContext failed: ...` line as Schannel curl 8.22.0, including the `--tls-max 1.2` against a TLS 1.3-only server case.

## Context

Found by BL-3235's CLI TLS differential run (cases `tls12-server-tls13only`, `tls-public-badssl-rc4`, `tls-public-badssl-dh480`, `tls-max-1.2-vs-1.3`). For SEC_E_ILLEGAL_MESSAGE real curl writes "... SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log." where Curl writes "... - The message received was unexpected or badly formatted." (curl's schannel.c replaces the system text for this code). For `--tls-max 1.2` against a TLS 1.3-only server real curl fails with SEC_E_UNSUPPORTED_FUNCTION (0x80090302) "The function requested is not supported", Curl with SEC_E_ILLEGAL_MESSAGE. Find where Curl.Tls.UnitLibrary words Schannel failures (narrow touches once found).

## Acceptance criteria

- [x] Unit tests pin both measured messages.
- [x] The build is clean and the fast tests pass.

## Notes

- Touches changed from `Curl.Tls.UnitLibrary`: Schannel failure text is worded in `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`, and the ceiling comes from `SslStreamTlsProvider.cs` (the route these cases take). No task in Doing on origin/work/dark-factory named these files.
- ADR-0796: every `SEC_E_ILLEGAL_MESSAGE` is curl's fatal-alert line; under a TLS 1.2 ceiling one whose `AuthenticationException` names a `ProtocolVersion` alert is curl's measured `SEC_E_UNSUPPORTED_FUNCTION` line. Hand-built path unchanged (not the route here; it already writes the fatal-alert line).
- Build clean; fast tests green (Curl.Networking.UnitTests 3427 passed); Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.
- Confirmed on Windows: `SslStream` with `--tls-max 1.2` against a TLS 1.3-only `SslStream` server fails with 0x80090326 and the `ProtocolVersion` alert; the new `SslStreamTlsProviderTests` test pins the resulting line. Tests are platform-neutral (that test is Inconclusive off Windows, like its neighbour).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e8f98b8a c46ca32a.
- 2026-10-23: Doing -> Done. Schannel build writes curl's SEC_E_ILLEGAL_MESSAGE fatal-alert text and SEC_E_UNSUPPORTED_FUNCTION for --tls-max 1.2 against a TLS 1.3-only server
