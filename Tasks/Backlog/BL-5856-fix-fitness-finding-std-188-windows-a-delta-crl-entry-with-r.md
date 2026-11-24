---
id: BL-5856
title: Fix fitness finding STD-188: Windows: a delta CRL entry with reason removeFromCRL does not lift a certificateHold in the base CRL
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5856 — Fix fitness finding STD-188: Windows: a delta CRL entry with reason removeFromCRL does not lift a certificateHold in the base CRL

## Goal

STD-188 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Windows: a delta CRL entry with reason removeFromCRL does not lift a certificateHold in the base CRL. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Each of these findings names its own certificates, but they share one harness. A `root.pem` self-signed CA signs every other CA: `intA.pem` (plain intermediate), `ncExclDir.pem` (critical nameConstraints `excluded: directoryName O=Evil`, PrintableString), `ncPermDir.pem` (`permitted: directoryName O=Good`), `ncPermEmail.pem` (`permitted: rfc822Name example.com`), `ncExclEmail.pem` (`excluded: rfc822Name other.org`), `ncExclEmailDot.pem` (`excluded: rfc822Name .other.org`), `ncExclDns.pem` (`excluded: dNSName localhost`). A plain HTTP server on `127.0.0.1:21080` serves CRLs. Each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated, and is served by its own TLS server (leaf + issuing CA) answering `hello <leaf>`. `--ssl-no-revoke` is used where a leaf has no CDP. The reviewer's generator is `gen.py` and the servers are `servers.py` in the review scratchpad. The finding, verbatim:


- **Severity:** Low (fails closed) · **Confidence:** Confirmed (direct and proxy) · **Area:** delta CRLs, Windows build, intermediate-only anchor. Next to this round's STD-158 fix.
- **Certificates:** leaf `d_remove` signed by `intA.pem`, CDP `http://127.0.0.1:21080/b3/base_drm.crl`. Base CRL: CRLNumber 5, lists the leaf with reason certificateHold, FreshestCRL → `/b3/delta_rm.crl`. Delta: CRLNumber 6, critical DeltaCRLIndicator(5), lists the leaf with reason removeFromCRL.
- **Repro:** `curl -sS https://localhost:<p>/ --cacert intA.pem` and `curl -sS -x https://localhost:<p> --proxy-cacert intA.pem http://127.0.0.1:21999/x`
- **Expected (REF):** `hello d_remove`, exit 0.
- **Actual (.NET):** `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`, exit 60. Control `d_hold` (hold in base, no delta) exits 60 in both.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs` ~line 122: `list.Revokes(certificate) ? Revoked : DeltaRevocationOf(...)` returns Revoked from the base without consulting the delta.
- **Acceptance check:** `d_remove` exits 0, direct and proxy; `d_hold` still exits 60.

## Acceptance criteria

- [ ] `d_remove` exits 0, direct and proxy; `d_hold` still exits 60.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.