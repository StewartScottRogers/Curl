---
id: BL-5858
title: Fix fitness finding STD-190: Windows: a name-constraint violation is reported as "revocation status is unknown" even when the leaf's CRL was fetched and is clean
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5858 — Fix fitness finding STD-190: Windows: a name-constraint violation is reported as "revocation status is unknown" even when the leaf's CRL was fetched and is clean

## Goal

STD-190 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Windows: a name-constraint violation is reported as "revocation status is unknown" even when the leaf's CRL was fetched and is clean. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Each of these findings names its own certificates, but they share one harness. A `root.pem` self-signed CA signs every other CA: `intA.pem` (plain intermediate), `ncExclDir.pem` (critical nameConstraints `excluded: directoryName O=Evil`, PrintableString), `ncPermDir.pem` (`permitted: directoryName O=Good`), `ncPermEmail.pem` (`permitted: rfc822Name example.com`), `ncExclEmail.pem` (`excluded: rfc822Name other.org`), `ncExclEmailDot.pem` (`excluded: rfc822Name .other.org`), `ncExclDns.pem` (`excluded: dNSName localhost`). A plain HTTP server on `127.0.0.1:21080` serves CRLs. Each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated, and is served by its own TLS server (leaf + issuing CA) answering `hello <leaf>`. `--ssl-no-revoke` is used where a leaf has no CDP. The reviewer's generator is `gen.py` and the servers are `servers.py` in the review scratchpad. The finding, verbatim:


- **Severity:** Low (message only; exit 60 in both) · **Confidence:** Confirmed (direct and proxy) · **Area:** HTTPS, Windows build. Neighbour of this round's STD-165 fix.
- **Certificates:** leaf `pxdir_evil` under `ncExclDir.pem` (excluded `O=Evil`), subject `O=Evil` (PrintableString), `CN=localhost`, CDP `http://127.0.0.1:21080/b3/ncExclDir.crl` serving a valid, empty CRL.
- **Repro:** `curl -sS https://localhost:<p>/ --cacert ncExclDir.pem` and the `-x … --proxy-cacert ncExclDir.pem http://127.0.0.1:21999/x` form.
- **Expected (REF):** `curl: (60) schannel: error 0x00008000`.
- **Actual (.NET):** `curl: (60) schannel: the revocation status is unknown`, although the port fetched the CRL. With `--ssl-no-revoke` it prints `0x00008000`.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs`, `RevocationStatusIsUnknownAndChecked` (~line 108): it checks only the .NET chain's RevocationStatusUnknown flags, not whether the port's own below-anchor check resolved the status (`ReadBelowAnchorRevocation(chain) is Clean`).
- **Acceptance check:** `pxdir_evil` prints `schannel: error 0x00008000`; a leaf with no CDP under `ncExclDns.pem` still prints "the revocation status is unknown".

## Acceptance criteria

- [ ] `pxdir_evil` prints `schannel: error 0x00008000`; a leaf with no CDP under `ncExclDns.pem` still prints "the revocation status is unknown".
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.