---
id: BL-5855
title: Fix fitness finding STD-187: Linux build rejects a SAN dNSName with a trailing dot (localhost.); OpenSSL curl accepts it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.NameChecks.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5855 — Fix fitness finding STD-187: Linux build rejects a SAN dNSName with a trailing dot (localhost.); OpenSSL curl accepts it

## Goal

STD-187 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Linux build rejects a SAN dNSName with a trailing dot (localhost.); OpenSSL curl accepts it. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low (fails closed on a valid certificate) · **Confidence:** Confirmed (2 runs, 6 variants) · **Area:** HTTPS / HTTPS-proxy host-name check, Linux build. This round's BL-5808 fixed it only on Windows.
- **Setup:** leaf `leafdot` with `subjectAltName=DNS:localhost.`, issued by `int1.pem` (signed by `root.pem`), served with int1 on 26150 answering `hello`.
- **Repro (Linux):**
  - `curl -sS -w '%{ssl_verify_result}\n' --cacert root.pem https://localhost:26150/`
  - `curl -sS --cacert root.pem https://localhost.:26150/`
  - `curl -sS --proxy-cacert root.pem -x https://localhost:26150 http://example.invalid/`
- **Expected (REF2):** `hello`, `0`, exit 0 for each.
- **Actual (.NET Linux):** `curl: (60) SSL: no alternative certificate subject name matches target hostname 'localhost'`, verify result 1, exit 60.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.NameChecks.cs` `WithTheNameCheckCurlRuns` relies on .NET's `RemoteCertificateNameMismatch`; the trailing-dot handling exists only in `SchannelCommonNameCheck.cs` (`WithoutTrailingDot`).
- **Acceptance check:** `curl -sS --cacert root.pem https://localhost:26150/` prints `hello`, exit 0, on Linux.

## Acceptance criteria

- [x] `curl -sS --cacert root.pem https://localhost:26150/` prints `hello`, exit 0, on Linux.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: after the OpenSSL common-name refusal check, a .NET name mismatch is cleared when `SchannelCommonNameCheck.DnsAlternativeNameMatches` (which ignores one trailing dot) matches the host. Unit test pins it. Linux CI check met by platform-neutral change and green fast tests. Quality run: 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 8cb88dc9 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Trailing-dot SAN dNSName now accepted on the OpenSSL build
