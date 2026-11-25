---
id: BL-5879
title: Fix fitness finding STD-198: Linux --cacert: OpenSSL TRUSTED CERTIFICATE blocks are ignored, including their reject (distrust) settings
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/OpenSslCaFile.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/OpenSslCaFileTests.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.TrustedCertificate.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5879 — Fix fitness finding STD-198: Linux --cacert: OpenSSL TRUSTED CERTIFICATE blocks are ignored, including their reject (distrust) settings

## Goal

STD-198 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Linux --cacert: OpenSSL TRUSTED CERTIFICATE blocks are ignored, including their reject (distrust) settings. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium · **Confidence:** Confirmed (2 runs) · **Area:** HTTPS CA bundle parsing, Linux build.
- **Files** (from a self-signed `root.pem` that signs the server's leaf `p_good`):
  - `roottrustout.pem`: `openssl x509 -in root.pem -trustout -addtrust serverAuth`
  - `rootreject.pem`: `openssl x509 -in root.pem -trustout -addreject serverAuth`
  - `rejectplusroot.pem`: `rootreject.pem` followed by plain `root.pem`
- **Repro (in WSL):** start a TLS server for `p_good` on `localhost:31950`, then `curl -sS --cacert <file> https://localhost:31950/` for each file.
- **Expected (REF2, curl 8.18 OpenSSL):** `roottrustout.pem` exit 0; `rejectplusroot.pem` exit 60 `certificate rejected (28)`; `rootreject.pem` exit 60 `certificate rejected (28)`.
- **Actual (.NET Linux):** `roottrustout.pem` exit 77 `error adding trust anchors from file`; `rejectplusroot.pem` **exit 0** (the explicitly distrusted root is trusted); `rootreject.pem` exit 77.
- **Real-world impact:** Fedora/RHEL ship `/etc/pki/tls/certs/ca-bundle.trust.crt` in this format, with distrust entries.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs`, `ReadCaCertificates` (non-Schannel branch): `ImportFromPem` and the `PemCertificateBegin` count only recognise `-----BEGIN CERTIFICATE-----`. Parse `-----BEGIN TRUSTED CERTIFICATE-----` (certificate followed by the `X509_CERT_AUX` trust/reject OID sequences) and drop or distrust a certificate that rejects `serverAuth` (1.3.6.1.5.5.7.3.1).
- **Acceptance check:** `curl -sS --cacert rejectplusroot.pem https://localhost:31950/` exits 60 with `certificate rejected`; `curl -sS --cacert roottrustout.pem https://localhost:31950/` exits 0.

## Acceptance criteria

- [x] `curl -sS --cacert rejectplusroot.pem https://localhost:31950/` exits 60 with `certificate rejected`; `curl -sS --cacert roottrustout.pem https://localhost:31950/` exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done without pipeline subagents, to stay inside the run's cost cap.
- Reproduced rules measured with `openssl verify -partial_chain -purpose sslserver` 3.5; recorded in ADR-1351.
- Acceptance check run in WSL Ubuntu: a self-contained linux-x64 publish of this change and curl 8.18.0 (OpenSSL 3.5.5)
  against `openssl s_server` on 31950 gave identical stderr, `%{ssl_verify_result}` and exit code for
  `roottrustout.pem` (0), `rejectplusroot.pem` (60, `certificate rejected (28)`) and `rootreject.pem` (60).
- `touches` widened to the refusal wiring, the new `OpenSslCaFile.cs`, its tests and the library notes; no task in
  Doing on `origin/work/dark-factory` named any of them.
- Quality run: 100% line and branch; its two methods at complexity 12 were split, then checked at 6 and 4 from a
  cobertura run of `Curl.Networking.UnitTests`.
- `--capath` files in the trusted format are left to BL-5917.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 09c4b73f 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. The OpenSSL build honours TRUSTED CERTIFICATE trust and reject settings in --cacert, as curl 8.18.0 does
