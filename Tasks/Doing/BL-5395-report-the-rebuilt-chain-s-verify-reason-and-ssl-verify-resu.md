---
id: BL-5395
title: Report the rebuilt chain's verify reason and ssl_verify_result for --capath failures on Linux
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/RebuiltChainVerifyReasonTests.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5395 — Report the rebuilt chain's verify reason and ssl_verify_result for --capath failures on Linux

## Goal

With `--capath` or `--proxy-capath` on the Linux and macOS builds, a failing chain reports the reason and `%{ssl_verify_result}` code of the chain rebuilt against the directory's anchors (10 for expired, 26 for a wrong purpose), not 20.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-102 (Low, Confirmed, 2+ runs: exit code correct, diagnostics and scripting mismatch). Area: HTTPS and HTTPS-proxy verification with `--capath`, Linux build.

**Repro.** (1) `openssl req -x509 -newkey rsa:2048 -nodes -keyout ca.key -out ca.pem -days 5 -subj /CN=TestCA -addext basicConstraints=critical,CA:TRUE -addext keyUsage=critical,keyCertSign,cRLSign`. (2) `mkdir capath && cp ca.pem capath/ && openssl rehash capath`. (3) An expired leaf (SAN `DNS:localhost`, EKU serverAuth, `-not_before 20240101000000Z -not_after 20240102000000Z`) served on 127.0.0.1:42110, and a valid clientAuth-only leaf served on 42105. (4) `curl -sS --capath capath https://localhost:42110/`; `curl -s --capath capath -w '%{ssl_verify_result}\n' -o /dev/null https://localhost:42110/`; the same `-w` command against 42105.
**Expected (REF2).** `curl: (60) SSL certificate OpenSSL verify result: certificate has expired (10)`; `%{ssl_verify_result}` is `10` for the expired leaf and `26` for the clientAuth leaf. The port already gets this right with `--cacert ca.pem` and with `SSL_CERT_DIR=capath`.
**Actual (PORT).** `unable to get local issuer certificate (20)`; `%{ssl_verify_result}` is `20` for both (stderr for the clientAuth leaf correctly says `(26)`).
**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `Refusal` / `VerifyPeer` (~line 330) feeds the system-store chain to `TlsFailureMessages.OpenSslVerifyError`; use the chain rebuilt by `BuildAgainstAnchors` for the reason and code, and set the code in `PurposeRefusal`.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests` (a new file) prove that with a CA directory (`--capath`) an expired leaf reports `certificate has expired (10)` and verify result 10, and a clientAuth-only leaf reports verify result 26.
- [ ] A test proves the same for `--proxy-capath` (the proxy's verification).
- [ ] A test proves `--cacert` and `SSL_CERT_DIR` results are unchanged (no regression).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [ ] Hand check recorded in the Log: the first command prints `certificate has expired (10)`; the `-w` commands print `10` and `26`. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the first command prints `certificate has expired (10)`; the `-w` commands print `10` and `26`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: aa26c374 1e50d592 10bcf0a0 7ae10ef7 a0b49a29 ea633b50.
