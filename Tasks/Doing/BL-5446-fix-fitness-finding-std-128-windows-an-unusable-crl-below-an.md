---
id: BL-5446
title: Fix fitness finding STD-128: Windows: an unusable CRL below an intermediate anchor gives "the certificate chain is incomplete"; curl says "the revocation status is unknown"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5446 — Fix fitness finding STD-128: Windows: an unusable CRL below an intermediate anchor gives "the certificate chain is incomplete"; curl says "the revocation status is unknown"

## Goal

STD-128 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: an unusable CRL below an intermediate anchor gives "the certificate chain is incomplete"; curl says "the revocation status is unknown". Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (message only; exit 60 matches)
- **Confidence:** Confirmed (more than 15 variants)
- **Protocol/area:** HTTPS and proxy revocation, Windows
- **Repro:** root → `intA.pem` → leaf (SAN localhost) served as leaf + intA, whose CRL DP returns HTTP 404 (variants: garbage, empty body, 11 MB, slow body, expired CRL, wrong signing key, other issuer, https/ldap/file DP, refused port, no CDP). `curl -sS https://localhost:<p>/ --cacert intA.pem`
- **Expected (REF):** `curl: (60) schannel: the revocation status is unknown`, exit 60.
- **Actual (PORT):** `curl: (60) schannel: the certificate chain is incomplete`, exit 60.
- **Likely location:** `ServerCertificateVerification.ChainErrors.cs` / `TlsFailureMessages`: when `ReadBelowAnchorRevocation` is `Unresolved`, report the revocation-unknown text instead of the PartialChain text.
- **Acceptance check:** the 404 variant prints `schannel: the revocation status is unknown`, exit 60.

## Acceptance criteria

- [ ] the 404 variant prints `schannel: the revocation status is unknown`, exit 60.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-128 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: b3371520 69116afe 3d836d5e.
