---
id: BL-5859
title: Fix fitness finding STD-191: Linux build reports "unable to get local issuer certificate (20)" for an excluded-subtree violation through a subject UniversalString or emailAddress
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5804, BL-5849]
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5859 — Fix fitness finding STD-191: Linux build reports "unable to get local issuer certificate (20)" for an excluded-subtree violation through a subject UniversalString or emailAddress

## Goal

STD-191 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Linux build reports "unable to get local issuer certificate (20)" for an excluded-subtree violation through a subject UniversalString or emailAddress. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

Part of this depends on the name-constraint fixes for STD-181 and STD-161 (find their tasks by finding ID); this task fixes the Linux verify-result mapping.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Each of these findings names its own certificates, but they share one harness. A `root.pem` self-signed CA signs every other CA: `intA.pem` (plain intermediate), `ncExclDir.pem` (critical nameConstraints `excluded: directoryName O=Evil`, PrintableString), `ncPermDir.pem` (`permitted: directoryName O=Good`), `ncPermEmail.pem` (`permitted: rfc822Name example.com`), `ncExclEmail.pem` (`excluded: rfc822Name other.org`), `ncExclEmailDot.pem` (`excluded: rfc822Name .other.org`), `ncExclDns.pem` (`excluded: dNSName localhost`). A plain HTTP server on `127.0.0.1:21080` serves CRLs. Each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated, and is served by its own TLS server (leaf + issuing CA) answering `hello <leaf>`. `--ssl-no-revoke` is used where a leaf has no CDP. The reviewer's generator is `gen.py` and the servers are `servers.py` in the review scratchpad. The finding, verbatim:


- **Severity:** Low (fails closed with the wrong message) · **Confidence:** Confirmed (2 runs) · **Area:** TLS name constraints, Linux build.
- **Certificates:** the Windows TLS PKI above, served inside WSL: `xdir_univ` (UniversalString `O=Evil` under `ncExclDir.pem`) and `xem_subj` (subject email `evil@other.org` under `ncExclEmail.pem`).
- **Repro (Linux):** `curl -sS https://localhost:<xdir_univ port>/ --cacert ncExclDir.pem` and `curl -sS https://localhost:<xem_subj port>/ --cacert ncExclEmail.pem`
- **Expected (REF2):** `curl: (60) SSL certificate OpenSSL verify result: excluded subtree violation (48)`.
- **Actual (.NET Linux):** `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`. The PrintableString `O=Evil` leaf correctly prints `(48)`.
- **Likely location:** the Linux verify-result mapping, plus the `CertificateNameConstraints.cs` gaps described in STD-181 and STD-161.
- **Acceptance check:** both commands print `excluded subtree violation (48)`.

## Acceptance criteria

- [ ] both commands print `excluded subtree violation (48)`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes
Requeued unedited (no code written): the fix lives in CertificateNameConstraints.cs, which BL-5804 (Doing, STD-161, subject emailAddress as rfc822Name) holds, and BL-5849 (Backlog, STD-181, same file) covers directoryName value matching. Needed here: add UniversalString (tag 28) to StringTagValues so a UniversalString O=Evil matches the excluded directoryName, and treat subject emailAddress as an rfc822Name; both land with those two tasks, then re-measure this finding on Linux and fix any remaining verify-result mapping in ServerCertificateVerification.cs. Added both to depends-on and the file to touches.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: d911915c 69116afe 3d836d5e.
- 2026-11-24: Doing -> Backlog. Needs CertificateNameConstraints.cs held by BL-5804 (and BL-5849 for the same name matching); depends-on added
- 2026-11-24: Backlog -> Doing. Criteria at claim: d911915c 69116afe 3d836d5e.
