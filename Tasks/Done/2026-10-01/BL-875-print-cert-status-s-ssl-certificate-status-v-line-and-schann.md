---
id: BL-875
title: Print --cert-status's SSL certificate status -v line and Schannel's automatic client certificate line
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-610]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-875 — Print --cert-status's SSL certificate status -v line and Schannel's automatic client certificate line

## Goal

With `-v`, a `--cert-status` transfer prints curl's `* SSL certificate status: good (0)` (or `revoked (1)`, `unknown (2)`, before the exit 91 line), and a transfer with or without `--ssl-auto-client-cert` prints the Schannel build's `* schannel: enabled automatic use of client certificate` / `disabled ...` line where ADR-0191 says the platform's curl prints it.

## Context

- ADR-0191, decision 5 (BL-610): both lines were measured and left out, because `TlsHandshakeEvent` carries neither the stapled response's status nor the automatic-certificate choice.
- Measured 2026-09-29 (BL-610 Notes): curl 8.18.0 / OpenSSL 3.5.5 prints `* SSL certificate status: good (0)` after `* SSL certificate verified via OpenSSL.`; for a revoked certificate `* SSL certificate status: revoked (1)` then `* SSL certificate revocation reason: keyCompromise (1)`. curl 8.21.0 Schannel prints `* schannel: disabled automatic use of client certificate` (or `enabled`) after `Trying`, before `ALPN: curl offers`, for every https transfer.
- The status reaches the provider as `Tls13ClientStream`/`Tls12ClientStream.CertificateStatus` (BL-705) or `TlsHandshakeFailure.CertificateStatusRejection`. Measure whether the Schannel line appears for every transfer before pinning it; it may belong only to the Schannel-build text set.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` pin the `-v` status lines for a good and a revoked stapled response, as measured.
- [x] The Schannel automatic-certificate line is measured again with `Record-CurlExchange.ps1 -Tls -k` and pinned (or recorded as not printed) with the reason in Notes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Decided (ADR-0335): `HandBuiltTlsProvider` reports the line itself as an info line through
  `CertificateStatusText.Report`: after the handshake event for a completed handshake, so it
  follows `SSL certificate verified via OpenSSL.`, and before the exit 91 failure for a rejected
  one. `TlsHandshakeEvent` does not carry it, because a failed handshake reports no event. So
  `Curl.Protocol.Abstractions` and `Curl.Output` are unchanged; `HandBuiltHandshake` carries
  the stream's `CertificateStatus`. Good, revoked and unknown statuses get the line, as in
  curl's `verifystatus()`. Every other outcome fails before the line. Every platform prints it
  (ADR-0191: the OpenSSL and LibreSSL texts apply everywhere).
- The Schannel line was measured again on 2026-10-01 with `Record-CurlExchange.ps1 -Tls -k`
  against `C:\Windows\System32\curl.exe` 8.21.0. It printed `* schannel: disabled automatic use
  of client certificate`, or `enabled` with `--ssl-auto-client-cert`, after `Trying` and before
  `ALPN: curl offers`. It also printed with no `-k`. BL-1083 already prints this
  (`SchannelTrustText`, pinned in `SchannelTrustTextTests`), so it needed no new code.
- Tests: `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithCertStatus_ReportsTheCertificateStatusLine`
  (good, revoked, unknown), `..._WithCertStatusAndNoStapledResponse_ReportsNoStatusLine`, and
  `CurlCommandRunnerCertificateStatusVerboseTests`, which checks the `* ` lines as the console
  writes them. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` gave 100/100 with
  0 failing members. Only tests changed in Curl.Console.UnitTests.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v prints SSL certificate status: good/revoked/unknown for --cert-status on every platform; Schannel auto client cert line confirmed already printed
