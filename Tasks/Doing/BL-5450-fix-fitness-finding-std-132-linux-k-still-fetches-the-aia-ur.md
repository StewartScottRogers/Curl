---
id: BL-5450
title: Fix fitness finding STD-132: Linux -k still fetches the AIA URL named in the server's certificate
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5450 — Fix fitness finding STD-132: Linux -k still fetches the AIA URL named in the server's certificate

## Goal

STD-132 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux -k still fetches the AIA URL named in the server's certificate. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (a request curl never makes, to a URL the server chose)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** HTTPS `-k`, Linux build
- **Repro (Linux/WSL):** the 41102 server and 41080 AIA server from STD-130. `curl -sS -k -o /dev/null https://localhost:41102/`; check the 41080 access log.
- **Expected (REF2):** exit 0, no request to 127.0.0.1:41080.
- **Actual (PORT):** exit 0, but 41080 logs `GET /int1.der`.
- **Likely location:** `ServerCertificateVerification.ReadTrustAnchors` returns a null chain policy when `options.Insecure`, so `SslStream` builds the chain with downloads allowed. On the OpenSSL build pass a policy with `DisableCertificateDownloads = true` (or skip chain building).
- **Acceptance check:** the command exits 0 and the 41080 log stays empty.

## Acceptance criteria

- [x] the command exits 0 and the 41080 log stays empty.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: ReadTrustAnchors under -k in the OpenSSL build returns the downloads-disabled system-store policy (no AIA fetch); Schannel build keeps null. Linux behaviour kept platform-neutral; unit tests in AiaDownloadDisabledTests. Quality run: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-132 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: c86390a1 69116afe 3d836d5e.
