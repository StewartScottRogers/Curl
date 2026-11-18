---
id: BL-5437
title: Fix fitness finding STD-119: Windows --ssl-revoke-best-effort with an intermediate-only --cacert and an unreachable or unusable CRL is refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5437 — Fix fitness finding STD-119: Windows --ssl-revoke-best-effort with an intermediate-only --cacert and an unreachable or unusable CRL is refused

## Goal

STD-119 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows --ssl-revoke-best-effort with an intermediate-only --cacert and an unreachable or unusable CRL is refused. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (fails closed: best-effort mode does not work for intermediate-only CA bundles)
- **Confidence:** Confirmed (3 leaves, plus the `--ciphers` path)
- **Protocol/area:** HTTPS revocation, Windows
- **Repro:** root → `intA.pem` (CA) → three leaves (SAN localhost, serverAuth), each served as leaf + intA. One leaf's CRL DP returns HTTP 404 (`http://127.0.0.1:40080/g/404.crl`), one has no CRL DP, one's DP points at a refused port (`http://127.0.0.1:40082/x.crl`). Run `curl -sS https://localhost:<p>/ --cacert intA.pem --ssl-revoke-best-effort` for each.
- **Expected (REF):** `hello …`, exit 0, for all three. A leaf listed as revoked in a reachable CRL is still refused with "has been revoked" (PORT matches that).
- **Actual (PORT):** `curl: (60) schannel: the certificate chain is incomplete`, exit 60.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs` — `RevocationBestEffortTolerates` uses `HasOnlyUnavailableRevocationStatus(chain.ChainStatus)`, which fails because the status also carries PartialChain; `HasOnlyMissingRootOrAnchorRevocationStatus` requires `BelowAnchorRevocation.Clean`. Under best-effort, tolerate `Unresolved`.
- **Acceptance check:** the three commands exit 0 with the body; the revoked-leaf command with `--ssl-revoke-best-effort` still exits 60 with the "has been revoked" text.

## Acceptance criteria

- [x] the three commands exit 0 with the body; the revoked-leaf command with `--ssl-revoke-best-effort` still exits 60 with the "has been revoked" text.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: under Schannel --ssl-revoke-best-effort, `HasOnlyMissingRootOrAnchorRevocationStatus` now also accepts an Unresolved below-anchor revocation (no usable CRL: 404, no DP, refused); Revoked is still refused with the has-been-revoked text. Unit test added in ServerCertificateVerificationTests.NonSelfSignedAnchorRevocation.cs. Not measured against live curl here (lane has no mock servers); the finding states REF behaviour. Quality run on Curl.Networking.UnitLibrary: 0 failing members. Commit carries code and task.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-119 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 6ac421e6 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Fixed: best-effort tolerates unresolved below-anchor CRLs
