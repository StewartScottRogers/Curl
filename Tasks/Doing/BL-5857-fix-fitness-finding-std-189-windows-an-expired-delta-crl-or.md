---
id: BL-5857
title: Fix fitness finding STD-189: Windows: an expired delta CRL, or one whose base number is above the base CRL's, gives "revocation status is unknown" where curl says "has been revoked"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5857 — Fix fitness finding STD-189: Windows: an expired delta CRL, or one whose base number is above the base CRL's, gives "revocation status is unknown" where curl says "has been revoked"

## Goal

STD-189 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Windows: an expired delta CRL, or one whose base number is above the base CRL's, gives "revocation status is unknown" where curl says "has been revoked". Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Each of these findings names its own certificates, but they share one harness. A `root.pem` self-signed CA signs every other CA: `intA.pem` (plain intermediate), `ncExclDir.pem` (critical nameConstraints `excluded: directoryName O=Evil`, PrintableString), `ncPermDir.pem` (`permitted: directoryName O=Good`), `ncPermEmail.pem` (`permitted: rfc822Name example.com`), `ncExclEmail.pem` (`excluded: rfc822Name other.org`), `ncExclEmailDot.pem` (`excluded: rfc822Name .other.org`), `ncExclDns.pem` (`excluded: dNSName localhost`). A plain HTTP server on `127.0.0.1:21080` serves CRLs. Each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated, and is served by its own TLS server (leaf + issuing CA) answering `hello <leaf>`. `--ssl-no-revoke` is used where a leaf has no CDP. The reviewer's generator is `gen.py` and the servers are `servers.py` in the review scratchpad. The finding, verbatim:


- **Severity:** Low (message only; both exit 60) · **Confidence:** Confirmed (direct and proxy) · **Area:** delta CRLs, Windows build.
- **Certificates** (all under `intA.pem`): `d_expired` — base CRL (number 5) whose FreshestCRL delta lists the leaf with thisUpdate now−5d, nextUpdate now−1d; `d_leafexp` — the leaf's own FreshestCRL points to a similarly expired delta; `d_bigind` — base number 5, delta with DeltaCRLIndicator(10), CRLNumber 12, listing the leaf.
- **Repro:** `curl -sS https://localhost:<p>/ --cacert intA.pem` (and the `-x … --proxy-cacert intA.pem` form).
- **Expected (REF):** `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.
- **Actual (.NET):** `curl: (60) schannel: the revocation status is unknown`.
- **Likely location:** `ServerCertificateVerification.AnchorRevocation.cs` ~line 146: `DeltaRevocationOf` filters with `delta.NextUpdate >= now && delta.IsDeltaOf(...)` and reports Unresolved.
- **Acceptance check:** the three leaves print the "has been revoked" text, exit 60.

## Acceptance criteria

- [ ] the three leaves print the "has been revoked" text, exit 60.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: e019e0b5 69116afe 3d836d5e.
