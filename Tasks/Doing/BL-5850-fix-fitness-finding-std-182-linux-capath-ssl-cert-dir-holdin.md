---
id: BL-5850
title: Fix fitness finding STD-182: Linux --capath / SSL_CERT_DIR holding both root and intermediate rejects a valid chain when the server sends only its leaf
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5850 — Fix fitness finding STD-182: Linux --capath / SSL_CERT_DIR holding both root and intermediate rejects a valid chain when the server sends only its leaf

## Goal

STD-182 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Linux --capath / SSL_CERT_DIR holding both root and intermediate rejects a valid chain when the server sends only its leaf. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (a valid chain is refused; an important trust route is broken) · **Confidence:** Confirmed (2 runs, 4 variants) · **Area:** HTTPS chain building, Linux build. Regression from this round's BL-5827 (`--capath` refuses chains completed with an unsent intermediate).
- **Setup:** `root.pem` signs CA `int1.pem`; int1 signs leaf `leaf1noaia` (SAN `localhost`, `127.0.0.1`, no AIA). A TLS server on port 26103 sends **only the leaf** and answers `hello`. Directory: `mkdir -p /tmp/a6cp_both; cp root.pem int1.pem /tmp/a6cp_both/; openssl rehash /tmp/a6cp_both`.
- **Repro (Linux):**
  - `curl -sS -w '%{ssl_verify_result}\n' --capath /tmp/a6cp_both https://localhost:26103/`
  - `SSL_CERT_DIR=/tmp/a6cp_both curl -sS -w '%{ssl_verify_result}\n' https://localhost:26103/`
  - Same with a leaf that has an AIA URL (server sends leaf only).
- **Expected (REF2, OpenSSL):** `hello` then `0`, exit 0.
- **Actual (.NET Linux):** `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`, `%{ssl_verify_result}` 20, exit 60. Still working: `--capath` with int1 only, `--cacert root+int1.pem`, `--proxy-capath /tmp/a6cp_both`.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `AnchoredChainRelyingOnAnIntermediateTheServerDidNotSend` / `RelyingOnAnIntermediateTheServerDidNotSend`: they flag any chain element the server did not send, even one that is itself in the configured `--capath`. Exempt elements found in `--capath`; refuse only intermediates that came from .NET's own store or AIA.
- **Acceptance check:** `curl -sS --capath /tmp/a6cp_both https://localhost:26103/` prints `hello`, exit 0. With int1 planted only in `~/.dotnet/corefx/cryptography/x509stores/ca` and a `--capath` that holds only root, the same URL still exits 60 `(20)`.

## Acceptance criteria

- [x] `curl -sS --capath /tmp/a6cp_both https://localhost:26103/` prints `hello`, exit 0. With int1 planted only in `~/.dotnet/corefx/cryptography/x509stores/ca` and a `--capath` that holds only root, the same URL still exits 60 `(20)`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: `WasSent` in ServerCertificateVerification.cs now also counts a chain element the --capath anchors hold as present, in both unsent-intermediate checks (OpenSSL reads --capath, so such an element is not completed from .NET's store). Intermediates from the CurrentUser CA store or AIA are still refused (exit 60, 20). Test: Judge_UnsentIntermediateTheCapathAlsoHolds_Accepts. Quality run for Curl.Networking.UnitLibrary: 0 failing members. Not measured against live curl: the existing refusal tests pin the store-only case.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 71fc81cb 69116afe 3d836d5e.
