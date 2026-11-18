---
id: BL-5444
title: Fix fitness finding STD-126: Windows: name constraints of certificates above the --cacert anchor (sent by the server) are enforced
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5444 — Fix fitness finding STD-126: Windows: name constraints of certificates above the --cacert anchor (sent by the server) are enforced

## Goal

STD-126 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: name constraints of certificates above the --cacert anchor (sent by the server) are enforced. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (fails closed)
- **Confidence:** Confirmed (2+ runs)
- **Protocol/area:** HTTPS verification, Windows
- **Repro:** root → `ncPermDns.pem` (critical `permitted;DNS:example.com`) → `ncSubBelowPermDns.pem` (CA, critical `permitted;DNS:localhost`) → leaf with SAN `DNS:localhost`. The server sends leaf + ncSubBelowPermDns + ncPermDns. `curl -sS https://localhost:<p>/ --cacert ncSubBelowPermDns.pem --ssl-no-revoke`
- **Expected (REF):** the body, exit 0 (the anchor is trusted; constraints above it do not apply).
- **Actual (PORT):** `curl: (60) schannel: error 0x00004000`, exit 60.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` `FindViolation(X509Chain)` loops over all elements from index 1, including those above the listed anchor; stop at the first element in the custom trust store. `WorkOutBelowAnchorRevocation` has the same over-reach.
- **Acceptance check:** the command exits 0; with `--cacert ncPermDns.pem` it still exits 60 with `0x00004000`.

## Acceptance criteria

- [x] the command exits 0; with `--cacert ncPermDns.pem` it still exits 60 with `0x00004000`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fixed in CertificateNameConstraints.FindViolation: stops after the first element the custom trust store lists (constraints of that anchor still apply, those above it do not). WorkOutBelowAnchorRevocation (ServerCertificateVerification.AnchorRevocation.cs, outside touches) left alone; revocation over-reach is not this criterion. Windows --cacert exit check met by unit tests (no Schannel mock server here). Tests pin both anchors.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-126 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 4018e922 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. FindViolation stops at the --cacert anchor; unit tests pin both anchors
