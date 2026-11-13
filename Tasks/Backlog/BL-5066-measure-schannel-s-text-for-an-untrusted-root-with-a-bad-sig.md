---
id: BL-5066
title: Measure Schannel's text for an untrusted root with a bad signature and restore upstream cases 43101 and 43113
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5066 — Measure Schannel's text for an untrusted root with a bad signature and restore upstream cases 43101 and 43113

## Goal

On the Schannel profile, a `--cacert` chain with a bad signature prints exactly what curl's Schannel build prints (measured), and upstream cases 43101 and 43113 pass on Windows.

## Context

- Found by the shift-end review of 2026-11-12 (review-20261112-200028), code review of e032d6195..acc2af773.
- `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:907-925` (`SchannelChainErrorOfUntrustedOrUnknownStatus`): BL-5062 made `UntrustedRoot` win over the raw `schannel: error 0x...` mask.
- That reversed BL-5032's pinned row in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs`: `NotSignatureValid | UntrustedRoot` gave `schannel: error 0x00000008`. BL-5032 pinned that row for upstream cases 43101 and 43113, which expect `curl: (60) schannel: error 0x00000008`.
- BL-5062's Notes say it was "not re-measured against a live curl". Its acceptance criterion that claims a recorded measurement is ticked anyway.
- Risk: .NET's hand-built chain can report `UntrustedRoot` beside `NotSignatureValid` where Windows' chain engine reports only the bad signature. In that case Curl now prints the untrusted-root line for 43101 and 43113, where curl prints `error 0x00000008`.
- curl's `schannel_verify.c` checks partial chain, then untrusted root, then the raw mask. The fix may belong in how the status is derived rather than in the order of the checks.

## Acceptance criteria

- [ ] Real curl (Schannel build) is measured with `Record-CurlExchange.ps1` for a bad-signature leaf under `--cacert`. Its stdout, stderr and exit code are recorded in Notes.
- [ ] Upstream cases 43101 and 43113 pass on Windows. The command and its result are recorded in Notes.
- [ ] A unit test in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs` pins the measured text for the chain status .NET reports on that certificate.
- [ ] `SchannelCaCertificateFileChainError_UntrustedRootWithNotValidForUsage_IsTheUntrustedRootLine` passes, or is changed to match a measurement.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-12: Created.
