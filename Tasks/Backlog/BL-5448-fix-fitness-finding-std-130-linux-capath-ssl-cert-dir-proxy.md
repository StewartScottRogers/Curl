---
id: BL-5448
title: Fix fitness finding STD-130: Linux --capath / SSL_CERT_DIR / --proxy-capath still download a missing intermediate via AIA (the STD-103 fix covered only the file and system-store routes)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5448 — Fix fitness finding STD-130: Linux --capath / SSL_CERT_DIR / --proxy-capath still download a missing intermediate via AIA (the STD-103 fix covered only the file and system-store routes)

## Goal

STD-130 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux --capath / SSL_CERT_DIR / --proxy-capath still download a missing intermediate via AIA (the STD-103 fix covered only the file and system-store routes). Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (fails open only toward a chain that still ends at a trusted root; differs from OpenSSL curl)
- **Confidence:** Confirmed (3 runs; the AIA server logs the fetch every time)
- **Protocol/area:** HTTPS and HTTPS-proxy chain building, Linux build
- **Repro (Linux/WSL):**
  1. PKI as in STD-121, plus `leaf1aia` signed by int1 with `authorityInfoAccess=caIssuers;URI:http://127.0.0.1:41080/int1.der`. `openssl x509 -in int1.pem -outform der -out int1.der`; serve that directory with `python3 -m http.server 41080 --bind 127.0.0.1`.
  2. TLS server on 127.0.0.1:41102 serving `leaf1aia` alone (no intermediate); same certificate on 41122 as an HTTPS proxy answering 200.
  3. `mkdir capath; cp root.pem capath/; openssl rehash capath`. Before each run delete `~/.dotnet/corefx/cryptography/x509stores/ca/<SHA1 of int1>.pfx` (see STD-131).
  4. `curl -sS --capath capath https://localhost:41102/`; `SSL_CERT_DIR=capath curl -sS https://localhost:41102/`; `curl -sS --proxy-capath capath -x https://localhost:41122 http://example.invalid/`
  Public equivalent: `curl -sS -o /dev/null -w '%{http_code}\n' --capath /etc/ssl/certs https://incomplete-chain.badssl.com/`
- **Expected (REF2):** `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`, exit 60; no request reaches 41080. The PORT already does this for `--cacert root.pem`, `SSL_CERT_FILE` and `CURL_CA_BUNDLE`.
- **Actual (PORT):** `hello`, exit 0; 41080 logs `GET /int1.der`. The badssl command prints `200`.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs` `RebuildAgainstAnchors` creates a `new X509Chain()` without `DisableCertificateDownloads = true` (used by `ChainLeadsToAnyOf`, `AnchorsRefuseServerPurpose`, `ReasonChainFromAnchors`). ADR-1296's fix went only into `ReadChainAnchors` and `SystemStoreChainPolicy`.
- **Acceptance check:** with the cache cleared, the three commands exit 60 with `(20)` and the 41080 log stays empty; `--capath capath` against a server sending leaf + int1 still exits 0.

## Acceptance criteria

- [ ] with the cache cleared, the three commands exit 60 with `(20)` and the 41080 log stays empty; `--capath capath` against a server sending leaf + int1 still exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-130 (Low).