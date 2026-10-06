---
id: BL-1323
title: Write the Schannel build's SEC_E_UNTRUSTED_ROOT -v line before an untrusted certificate's exit 60
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1323 — Write the Schannel build's SEC_E_UNTRUSTED_ROOT -v line before an untrusted certificate's exit 60

## Goal

When the Schannel build refuses a server certificate whose chain ends in an untrusted root (no `-k`, no `--cacert`), the TLS provider reports curl 8.21.0's `failf` text, `schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.`, as a `-v` info line after `* ALPN: curl offers http/1.1`, so `-v` prints it before `* closing connection #0` and the `curl: (60)` line.

## Context

- Today the text exists as `TlsFailureMessages.SchannelUntrustedRoot` (`Curl.Networking.UnitLibrary/TlsFailureMessages.cs` line 23, chosen at line 249) and becomes the exit 60 `ConnectResult` message, but nothing reports it to the transfer's events: `-v` shows the ALPN offer (BL-1149, `TlsHandshakeEvent.Failed`) and then nothing until `curl: (60)`.
- curl's Schannel backend reports the verification failure with `failf` (`lib/vtls/schannel.c`, tag `curl-8_21_0`), and `failf` writes its text as an info line under `-v` as well as keeping it for the `curl: (N)` line.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -Tls -CurlArgs '-v','https://127.0.0.1:PORT/'` (the recorder's throwaway self-signed certificate), stderr exactly:
  ```
  *   Trying 127.0.0.1:PORT...
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  * ALPN: curl offers http/1.1
  * schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.
  * closing connection #0
  curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.
  More details here: https://curl.se/docs/sslcerts.html
  ```
  followed by the certificate help block, exit 60.
- Where the line goes: report it with `events.ReportInfo(...)` in the provider that made the decision (`SslStreamTlsProvider`, after its failed `ReportTlsHandshake` near line 579; `HandBuiltTlsProvider` near line 271 when it answers as the Schannel build), the way `PeerVerification` reports `TlsFailureMessages.PinnedPublicKeyMismatch` (line 74). Only the Schannel build: the OpenSSL build's lines before exit 60 differ (certificate details) and are not this task's.
- BL-1324 does the same for exit 35's `failed to receive handshake`; keep the change small enough that it reuses whatever this task adds.

## Acceptance criteria

- [x] A test in `Curl.Networking.UnitTests` (beside the existing `SEC_E_UNTRUSTED_ROOT` cases in `SslStreamTlsProviderTests.cs`, which run as the Schannel build on every platform) asserts the events receive the info line `schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.` exactly once, after the failed handshake event, and that the result is still exit 60 with the same message.
- [x] The same is pinned for `HandBuiltTlsProvider` answering as the Schannel build, if it can reach `SchannelUntrustedRoot`; if it cannot, the Notes say why.
- [x] Tests pin that `-k` (verification off), and the OpenSSL build refusing the same certificate, report no such line.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

- Plan: one small shared seam, `SchannelFailureEcho.Report(events, matchesSchannelBuild, failure)` in `Curl.Networking.UnitLibrary`, called on the failed-handshake return of both `SslStreamTlsProvider` and `HandBuiltTlsProvider` (`FailAsync`). It reports the failure's text as an info line when the provider is the Schannel build and the text is `TlsFailureMessages.SchannelUntrustedRoot` (now `internal const`), and returns the failure unchanged. BL-1324 widens the set it echoes with exit 35's `failed to receive handshake`.
- Placement: last, after the failed handshake event, the pinned-key refusal lines and (hand-built) the certificate-status and ECH lines, so `-v` shows it after `ALPN: curl offers` and before `closing connection #0`, as measured. It also echoes the same text when `--cacert` names an untrusted chain, since curl's `failf` echoes every failure under `-v`.
- `HandBuiltTlsProvider` reaches `SchannelUntrustedRoot` (its verifier's rejection is the SslStream provider's verdict, BL-708), so it is pinned too.
- No ADR: the behaviour is measured curl output, not a design choice.
- Tests: `SslStreamTlsProviderTests` and `HandBuiltTlsProviderTests` each gain the Schannel echo test (once, after the failed handshake event, exit 60 with the same message) and a `-k` / OpenSSL-build test that reports no such line. Networking fast tests 2953 passed; full solution fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members. The first measurement run failed in one of the test projects it reached (output lost); the rerun was green, so it is recorded here as a flaky failure that did not reproduce.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Schannel build's -v prints SEC_E_UNTRUSTED_ROOT before exit 60, in both TLS providers
