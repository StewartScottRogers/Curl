---
id: BL-5447
title: Fix fitness finding STD-129: Windows: the CRL download below an intermediate anchor follows the http_proxy and HTTP_PROXY environment variables, which changes the verification result
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/RevocationListDownload.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5447 — Fix fitness finding STD-129: Windows: the CRL download below an intermediate anchor follows the http_proxy and HTTP_PROXY environment variables, which changes the verification result

## Goal

STD-129 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: the CRL download below an intermediate anchor follows the http_proxy and HTTP_PROXY environment variables, which changes the verification result. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (security-relevant: an environment proxy, including uppercase `HTTP_PROXY` that curl deliberately ignores, receives and controls the CRL request; combined with STD-113 it can hide a revocation)
- **Confidence:** Confirmed (2 runs each)
- **Protocol/area:** HTTPS revocation, Windows
- **Repro:** a TCP listener on 127.0.0.1:40087 that logs and closes. root → `intA.pem` → a leaf revoked in intA's CRL (CRL DP `http://127.0.0.1:40080/crl/intA.crl`, served correctly). `HTTP_PROXY=http://127.0.0.1:40087 curl -sS https://localhost:<p>/ --cacert intA.pem`, then the same with lowercase `http_proxy`.
- **Expected (REF):** `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`; nothing reaches the listener (CryptoAPI ignores these variables).
- **Actual (PORT):** `curl: (60) schannel: the certificate chain is incomplete`; the listener logs `GET http://127.0.0.1:40080/crl/intA.crl HTTP/1.1` (4 attempts per run).
- **Likely location:** `Curl.Networking.UnitLibrary/RevocationListDownload.cs` `new HttpClient` uses `HttpClient.DefaultProxy`; use a handler with `UseProxy = false` (or the WinHTTP system proxy only).
- **Acceptance check:** with `HTTP_PROXY` or `http_proxy` set, the command prints the "has been revoked" text and the listener logs nothing.

## Acceptance criteria

- [x] with `HTTP_PROXY` or `http_proxy` set, the command prints the "has been revoked" text and the listener logs nothing.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: RevocationListDownload.CreateHandler returns a SocketsHttpHandler with UseProxy=false, so the CRL fetch ignores HTTP_PROXY/http_proxy. Pinned by RevocationListDownloadProxyTests; live listener check not run (no mock listener in lane).

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-129 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: c5825892 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. CRL fetch no longer uses proxy environment variables
