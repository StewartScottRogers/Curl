---
id: BL-5445
title: Fix fitness finding STD-127: Windows: a PEM-encoded CRL served at the distribution point is not used (curl accepts it)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5445 — Fix fitness finding STD-127: Windows: a PEM-encoded CRL served at the distribution point is not used (curl accepts it)

## Goal

STD-127 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: a PEM-encoded CRL served at the distribution point is not used (curl accepts it). Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (fails closed)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** HTTPS revocation below an intermediate anchor, Windows
- **Repro:** root → `intA.pem` → leaf (SAN localhost, serverAuth) with CRL DP `http://127.0.0.1:40080/g/pem.crl`; that URL returns intA's valid CRL as PEM text (`-----BEGIN X509 CRL-----` …). `curl -sS https://localhost:<p>/ --cacert intA.pem`; repeat with a leaf that this CRL lists as revoked.
- **Expected (REF):** good leaf: the body, exit 0. Revoked leaf: `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.
- **Actual (PORT):** `curl: (60) schannel: the certificate chain is incomplete`, exit 60, in both cases.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs` `DecodeOrNothing` expects DER only; strip PEM armour before decoding.
- **Acceptance check:** the good leaf exits 0; the revoked leaf exits 60 with the "has been revoked" text.

## Acceptance criteria

- [ ] the good leaf exits 0; the revoked leaf exits 60 with the "has been revoked" text.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-127 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: e0f43b79 69116afe 3d836d5e.
