---
id: BL-4022
title: CGR-224 (a)(b): Schannel text for --tls-max 1.0 (SEC_E_UNSUPPORTED_FUNCTION) and --tlsv1.0 --tls-max 1.1 (SEC_E_ILLEGAL_MESSAGE) against curl 8.22.0
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4009]
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.HandBuilt.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4022 — CGR-224 (a)(b): Schannel text for --tls-max 1.0 (SEC_E_UNSUPPORTED_FUNCTION) and --tlsv1.0 --tls-max 1.1 (SEC_E_ILLEGAL_MESSAGE) against curl 8.22.0

## Goal

When the server refuses a sub-TLS 1.2 range with an alert, Curl's Schannel-matching hand-built client prints the line curl 8.22.0's Schannel build prints: SEC_E_ILLEGAL_MESSAGE when it offered both TLS 1.0 and TLS 1.1, and SEC_E_UNSUPPORTED_FUNCTION when it offered only one of them.

## Context

CGR-224 TLS-12 (a)(b) in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, left over from BL-3753. Today `TlsFailureMessages.SchannelHandBuiltHandshakeFailure` returns `failed to receive handshake` for every range whose ceiling is TLS 1.0 or 1.1 (curl 8.21.0, BL-502, against a server that closed). `HandBuiltTlsProvider` passes only `OffersOnlyVersionsBelowTls12` (from the ceiling), so it must also pass whether the range offers both TLS 1.0 and TLS 1.1 (`MinimumVersion` Tls10 with `MaximumVersion` Tls11).

Measured 2026-10-28 on this lane (Windows 11, curl 8.22.0 Schannel; `openssl s_server -WWW` 3.5.9, which logged `unsupported protocol` for every case and so got a ClientHello and sent a protocol_version alert):

| server | curl options | curl prints (exit 35) |
|---|---|---|
| default | `--tls-max 1.0` | SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported |
| default | `--tls-max 1.1` | SEC_E_UNSUPPORTED_FUNCTION |
| default | `--tlsv1.0 --tls-max 1.0` | SEC_E_UNSUPPORTED_FUNCTION |
| default | `--tlsv1.1 --tls-max 1.1` | SEC_E_UNSUPPORTED_FUNCTION |
| `-tls1_2` | `--tls-max 1.0`, `--tls-max 1.1`, `--tlsv1.1 --tls-max 1.1` | SEC_E_UNSUPPORTED_FUNCTION |
| default, `-tls1_2`, `-tls1_3` | `--tlsv1.0 --tls-max 1.1` | SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received ... (curl's schannel.c text) |

Each line is prefixed `curl: (35) schannel: next InitializeSecurityContext failed: `.

## Acceptance criteria

- [x] `TlsFailureMessagesTests.HandBuilt.cs` pins, for an alert received with a ceiling below TLS 1.2: SEC_E_ILLEGAL_MESSAGE's line when the range offers TLS 1.0 and TLS 1.1, and SEC_E_UNSUPPORTED_FUNCTION's line when it offers one of them; the transport closing still gives `failed to receive handshake`.
- [x] `HandBuiltTlsProvider` passes the offered range so the Schannel build's `--tls-max 1.0` and `--tlsv1.0 --tls-max 1.1` failures against `openssl s_server` print the texts in the Context table (checked with Curl.Console against the same server).
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the rule and the measurement.
- [x] Measure-CodeQuality.ps1 for Curl.Networking.UnitLibrary reports no member outside a gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-28 (lane 1): The task was filed as an empty template; Goal, Context and criteria were written from the gap report and this lane's measurement (table above) after moving it to Backlog, so the next claim records them. The fix needs `HandBuiltTlsProvider.cs`, held by BL-4009 in Doing, so it was added to touches and BL-4009 to depends-on. No code was changed. Decision for the next lane: apply the measured rule to any alert received under a sub-1.2 ceiling (only protocol_version was measured), keep TransportClosed as `failed to receive handshake`.

- 2026-10-28 (lane 2): `SchannelHandBuiltHandshakeFailure` now takes the offered range (`MinimumVersion`, `MaximumVersion`) instead of a ceiling flag, so the whole rule sits in the method its tests drive and `HandBuiltTlsProvider` adds no branch (a `bool` property for the range left a branch uncovered). An alert the client sent under a sub-1.2 ceiling (unmeasured) keeps `failed to receive handshake`. ADR-0987 records it. Checked with Curl.Console against `openssl s_server -WWW` 3.5.9: `--tls-max 1.0` and `--tls-max 1.1` print SEC_E_UNSUPPORTED_FUNCTION, `--tlsv1.0 --tls-max 1.1` SEC_E_ILLEGAL_MESSAGE, exit 35. Measure-CodeQuality: 0 failing members in Curl.Networking.UnitLibrary (100/100). Tests are pure string checks, platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs (to pass the offered minimum, not just the ceiling), which BL-4009 holds in Doing; depends-on BL-4009
- 2026-10-28: Backlog -> Doing. Criteria at claim: b0de8074 eddd41c7 4f4028d3 329708f7 3d836d5e.
- 2026-10-28: Doing -> Done. Schannel hand-built client prints SEC_E_UNSUPPORTED_FUNCTION or SEC_E_ILLEGAL_MESSAGE for an alert under a TLS 1.0/1.1 ceiling, as curl 8.22.0
