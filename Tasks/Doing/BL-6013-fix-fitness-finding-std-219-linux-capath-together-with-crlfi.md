---
id: BL-6013
title: Fix fitness finding STD-219: Linux: --capath together with --crlfile always fails with "unable to get issuer certificate (2)", even when the --capath directory holds the self-signed root
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6013 — Fix fitness finding STD-219: Linux: --capath together with --crlfile always fails with "unable to get issuer certificate (2)", even when the --capath directory holds the self-signed root

## Goal

STD-219 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Linux: --capath together with --crlfile always fails with "unable to get issuer certificate (2)", even when the --capath directory holds the self-signed root. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (a valid server is refused; a revoked one gets the wrong reason) · **Confidence:** Confirmed (2 runs, 3 directories × 4 CRL files) · **Area:** HTTPS CRL checks, Linux build. Next to last round's STD-180 fix.
- **Certificates:** self-signed `root.pem` signs `int1.pem`, which signs `leaf1` (SAN `localhost`, `127.0.0.1`); the server sends leaf1 + int1 on port 22101 and answers `hello`. CRL files: `allcrl_empty.pem` (empty root CRL + empty int1 CRL), `allcrl.pem` (int1 CRL revokes leaf1), `int1.crl.pem` (revokes leaf1), `int1_empty.crl`.
- **Repro (WSL, Linux build):**
  - `mkdir /tmp/cpr && cp root.pem /tmp/cpr/ && openssl rehash /tmp/cpr`
  - `curl -sS -w '%{ssl_verify_result}\n' --capath /tmp/cpr --crlfile allcrl_empty.pem https://localhost:22101/`
  - the same with `--crlfile allcrl.pem`, `int1.crl.pem` and `int1_empty.crl`
- **Expected (REF2 8.18 OpenSSL):** `allcrl_empty.pem`: `hello`, `0`, exit 0. `allcrl.pem` and `int1.crl.pem`: `curl: (60) SSL certificate OpenSSL verify result: certificate revoked (23)`. `int1_empty.crl`: `unable to get certificate CRL (3)`.
- **Actual (.NET Linux):** all four print `curl: (60) SSL certificate OpenSSL verify result: unable to get issuer certificate (2)`, verify result 2, exit 60. The same CRL files with `--cacert root.pem`, `SSL_CERT_DIR=/tmp/cpr`, or `--proxy-capath … --proxy-crlfile` match REF2.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `RevocationListVerdict`, passes the system-store chain to `CertificateRevocationListFile.FirstRefusal`; with `--capath` that chain ends at int1, not the `--capath` root, so the anchor looks non-self-signed and yields `UnableToGetIssuerCertificate`. Use the chain rebuilt against the `--capath` anchors (`RebuildAgainstAnchors`).
- **Acceptance check:** the `allcrl_empty.pem` command prints `hello` and exits 0; the `allcrl.pem` command prints `certificate revoked (23)` and exits 60.

## Acceptance criteria

- [ ] the `allcrl_empty.pem` command prints `hello` and exits 0; the `allcrl.pem` command prints `certificate revoked (23)` and exits 60.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 433190ab 69116afe 3d836d5e.
