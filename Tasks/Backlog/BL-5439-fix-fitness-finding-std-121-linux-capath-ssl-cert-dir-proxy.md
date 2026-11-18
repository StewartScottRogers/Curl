---
id: BL-5439
title: Fix fitness finding STD-121: Linux --capath / SSL_CERT_DIR / --proxy-capath holding only an intermediate CA refuses chains that OpenSSL curl accepts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5439 — Fix fitness finding STD-121: Linux --capath / SSL_CERT_DIR / --proxy-capath holding only an intermediate CA refuses chains that OpenSSL curl accepts

## Goal

STD-121 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux --capath / SSL_CERT_DIR / --proxy-capath holding only an intermediate CA refuses chains that OpenSSL curl accepts. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (false refusal of a valid configuration; fails closed)
- **Confidence:** Confirmed (2 runs, 3 routes)
- **Protocol/area:** HTTPS and HTTPS-proxy verification with a CA directory, Linux build
- **Repro (Linux/WSL):**
  1. PKI with openssl: `root.pem` self-signed CA; `int1.pem` CA (basicConstraints critical CA:TRUE, keyUsage keyCertSign,cRLSign) signed by root; `leaf1.pem` signed by int1 (CA:FALSE, EKU serverAuth, SAN `DNS:localhost,IP:127.0.0.1`).
  2. TLS server on 127.0.0.1:41101 serving `leaf1.pem` + `int1.pem` (answers 200 `hello`); a second on 41121 with the same chain answering any request with 200 (used as HTTPS proxy).
  3. `mkdir capath_int; cp int1.pem capath_int/; openssl rehash capath_int`
  4. `curl -sS -w '%{ssl_verify_result}\n' --capath capath_int https://localhost:41101/`; `SSL_CERT_DIR=capath_int curl -sS https://localhost:41101/`; `curl -sS --proxy-capath capath_int -x https://localhost:41121 http://example.invalid/`
- **Expected (REF2):** all three print `hello`, exit 0, `%{ssl_verify_result}` 0 (OpenSSL curl sets `X509_V_FLAG_PARTIAL_CHAIN`). `--cacert int1.pem` exits 0 in both builds.
- **Actual (PORT):** `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`, exit 60, `%{ssl_verify_result}` 20, on all three routes.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs` — `ChainLeadsToAnyOf` / `RebuildAgainstAnchors` / `EndsAtAnchor` accept only a fully built CustomRootTrust chain. The `--cacert` path's partial-chain tolerance (`ServerCertificateVerification.ChainErrors.cs` `CaCertificateFileAnchorsChain`) is missing on the `--capath` path.
- **Acceptance check:** the three commands exit 0 with `hello` and `%{ssl_verify_result}` 0. A directory holding only an intermediate with `nameConstraints=critical,excluded;DNS:localhost`, against a leaf for localhost signed by it, still exits 60.

## Acceptance criteria

- [ ] the three commands exit 0 with `hello` and `%{ssl_verify_result}` 0. A directory holding only an intermediate with `nameConstraints=critical,excluded;DNS:localhost`, against a leaf for localhost signed by it, still exits 60.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-121 (Medium).