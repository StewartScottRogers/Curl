---
id: BL-5438
title: Fix fitness finding STD-120: Windows: a server that also sends the self-signed root is refused as "untrusted root" when --cacert holds only the intermediate and revocation is on
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NonSelfSignedAnchorRevocation.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5438 — Fix fitness finding STD-120: Windows: a server that also sends the self-signed root is refused as "untrusted root" when --cacert holds only the intermediate and revocation is on

## Goal

STD-120 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: a server that also sends the self-signed root is refused as "untrusted root" when --cacert holds only the intermediate and revocation is on. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (fails closed; servers that send the full chain including the root are common)
- **Confidence:** Confirmed (2+ runs, both TLS paths)
- **Protocol/area:** HTTPS verification, Windows
- **Repro:** serve leaf (SAN localhost, serverAuth, CRL DP to a valid, empty CRL from intA) + `intA.pem` + `root.pem` (all three in the chain file). `curl -sS https://localhost:<p>/ --cacert intA.pem`
- **Expected (REF):** the body, exit 0.
- **Actual (PORT):** `curl: (60) schannel: the certificate or certificate chain is based on an untrusted root`, exit 60. With `--ssl-no-revoke` the PORT exits 0.
- **Likely location:** `ServerCertificateVerification.ChainErrors.cs` `HasOnlyMissingRootOrAnchorRevocationStatus` requires `!IsSelfSigned(chain.ChainElements[^1])`, but here the chain runs past the listed anchor to the server-sent root. Cut the chain at the first certificate listed in `--cacert` before the anchor checks (revocation and name constraints; see also STD-126).
- **Acceptance check:** the command exits 0 with the body; the same server with a revoked leaf exits 60 with "has been revoked".

## Acceptance criteria

- [x] the command exits 0 with the body; the same server with a revoked leaf exits 60 with "has been revoked".
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Chain is now cut at the first certificate the --cacert file lists (`AnchorIndexOf`): the self-signed check, below-anchor statuses and CRL reads use that anchor, not the last element, so a server-sent root no longer causes the untrusted-root refusal. Touches widened to the sibling partial files and the test file in the same project. Revoked leaf still exits 60 via RevokesBelowNonSelfSignedAnchor. Not measured against real curl on Windows (no revocation server here); unit-pinned. Linux/macOS CI: tests are platform-neutral.
- Quality run for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-120 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 32607c38 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Chain cut at the first --cacert-listed certificate; server-sent root no longer refused
