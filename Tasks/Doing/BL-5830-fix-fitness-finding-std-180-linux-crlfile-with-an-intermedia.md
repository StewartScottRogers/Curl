---
id: BL-5830
title: Fix fitness finding STD-180: Linux --crlfile with an intermediate-only anchor reports "certificate revoked (23)" where OpenSSL reports "unable to get issuer certificate (2)"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5830 — Fix fitness finding STD-180: Linux --crlfile with an intermediate-only anchor reports "certificate revoked (23)" where OpenSSL reports "unable to get issuer certificate (2)"

## Goal

STD-180 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux --crlfile with an intermediate-only anchor reports "certificate revoked (23)" where OpenSSL reports "unable to get issuer certificate (2)". Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (exit 60 in both; wrong message and `%{ssl_verify_result}`)
- **Confidence:** Confirmed (2 runs, 2 routes)
- **Protocol/area:** HTTPS CRL check, Linux build
- **Repro (Linux):** `int1.crl.pem` is int1's CRL revoking leaf1; 26101 serves leaf1 + int1; `capath_int/` holds int1 (rehashed).
  ```
  curl -sS -w '%{ssl_verify_result}\n' --cacert pki/int1.pem --crlfile pki/int1.crl.pem https://localhost:26101/
  curl -sS -w '%{ssl_verify_result}\n' --capath pki/capath_int --crlfile pki/int1.crl.pem https://localhost:26101/
  curl -sS -w '%{ssl_verify_result}\n' --cacert pki/root.pem --crlfile pki/int1.crl.der https://localhost:26101/
  ```
- **Expected (REF2):** the first two: `curl: (60) SSL certificate OpenSSL verify result: unable to get issuer certificate (2)`, `%{ssl_verify_result}` 2. The third (DER CRL): exit 82, `%{ssl_verify_result}` 1.
- **Actual (.NET):** the first two: `… certificate revoked (23)`, `%{ssl_verify_result}` 23, exit 60. The third: exit 82, `%{ssl_verify_result}` 0.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` `Refusal`, the CRL path used with `CaCertificateFileAnchorsChain` (partial chain): map to `OpenSslVerifyResult` 2 on the OpenSSL build; set the verify result to 1 before exit 82.
- **Acceptance check:** the first two print `(2)` and `2`, exit 60; the third prints `1`, exit 82.

## Acceptance criteria

- [ ] the first two print `(2)` and `2`, exit 60; the third prints `1`, exit 82.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 1898b3a1 69116afe 3d836d5e.
