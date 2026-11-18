---
id: BL-5436
title: Fix fitness finding STD-118: Windows: directoryName, rfc822Name and URI name constraints are not evaluated
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5436 — Fix fitness finding STD-118: Windows: directoryName, rfc822Name and URI name constraints are not evaluated

## Goal

STD-118 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: directoryName, rfc822Name and URI name constraints are not evaluated. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (security weakening of CA policy: the chain is accepted although Schannel refuses it; host identity is unaffected unless combined with STD-114)
- **Confidence:** Confirmed (2 runs each)
- **Protocol/area:** HTTPS verification, `--cacert` holding a name-constrained intermediate, Windows
- **Repro:** three intermediates signed by a root with critical name constraints, each with a leaf having SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth:
  1. `ncPermDir.pem` (`permitted;dirName:O=Good`), leaf subject `O=Evil, CN=localhost`.
  2. `ncPermEmail.pem` (`permitted;email:example.com`), leaf with extra SAN `email:evil@other.org`.
  3. `ncPermUri.pem` (`permitted;URI:.example.com`), leaf with extra SAN `URI:https://evil.other.org/`.
  Run `curl -sS https://localhost:<p>/ --cacert <int>.pem --ssl-no-revoke` for each.
- **Expected (REF):** `curl: (60) schannel: error 0x00004000`, exit 60, for all three. A control leaf with subject `O=Good` is accepted by both.
- **Actual (PORT):** exit 0 with the body for all three.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` — `ReadSubtrees` keeps only dNSName and iPAddress subtrees, `ListNames` only DNS and IP SANs. Add directoryName (subject DN prefix match), rfc822Name and URI-host matching.
- **Acceptance check:** the three commands exit 60 with `schannel: error 0x00004000`; the `O=Good` leaf still exits 0.

## Acceptance criteria

- [ ] the three commands exit 60 with `schannel: error 0x00004000`; the `O=Good` leaf still exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-118 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 01f1d0a3 69116afe 3d836d5e.
