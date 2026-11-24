---
id: BL-5849
title: Fix fitness finding STD-181: Windows: an excluded directoryName name constraint is bypassed when the leaf's subject attribute is a UniversalString
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5849 — Fix fitness finding STD-181: Windows: an excluded directoryName name constraint is bypassed when the leaf's subject attribute is a UniversalString

## Goal

STD-181 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Windows: an excluded directoryName name constraint is bypassed when the leaf's subject attribute is a UniversalString. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Each of these findings names its own certificates, but they share one harness. A `root.pem` self-signed CA signs every other CA: `intA.pem` (plain intermediate), `ncExclDir.pem` (critical nameConstraints `excluded: directoryName O=Evil`, PrintableString), `ncPermDir.pem` (`permitted: directoryName O=Good`), `ncPermEmail.pem` (`permitted: rfc822Name example.com`), `ncExclEmail.pem` (`excluded: rfc822Name other.org`), `ncExclEmailDot.pem` (`excluded: rfc822Name .other.org`), `ncExclDns.pem` (`excluded: dNSName localhost`). A plain HTTP server on `127.0.0.1:21080` serves CRLs. Each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated, and is served by its own TLS server (leaf + issuing CA) answering `hello <leaf>`. `--ssl-no-revoke` is used where a leaf has no CDP. The reviewer's generator is `gen.py` and the servers are `servers.py` in the review scratchpad. The finding, verbatim:


- **Severity:** High · **Confidence:** Confirmed (4 runs; re-verified by the orchestrator) · **Area:** HTTPS / HTTPS-proxy name constraints, Windows build, intermediate-only `--cacert` / `--proxy-cacert` anchor. Next to last round's STD-160 fix.
- **Certificates:** CA `ncExclDir.pem` (excluded `O=Evil`, PrintableString). Leaf `xdir_univ`: subject `O=Evil` encoded as **UniversalString (ASN.1 tag 28)**, `CN=localhost`. Leaf `xdir_univ_case`: subject `O="eVIL "` as UniversalString. Leaf `pxdir_univ`: as `xdir_univ` plus a CDP `http://127.0.0.1:21080/b3/ncExclDir.crl` serving a valid, empty CRL.
- **Repro:**
  - `curl -sS https://localhost:<port of xdir_univ>/ --cacert ncExclDir.pem --ssl-no-revoke`
  - `curl -sS https://localhost:<port of xdir_univ_case>/ --cacert ncExclDir.pem --ssl-no-revoke`
  - `curl -sS -x https://localhost:<port of pxdir_univ> --proxy-cacert ncExclDir.pem http://127.0.0.1:21999/x`
- **Expected (REF 8.22.0 Schannel):** `curl: (60) schannel: error 0x00008000`, exit 60, for all three.
- **Actual (.NET):** `hello xdir_univ` / `hello xdir_univ_case` / `hello pxdir_univ`, exit 0. The PrintableString, UTF8String, BMPString, T61String and IA5String forms of `O=Evil` are refused correctly; `--cacert root.pem` is refused correctly (Windows enforces the constraint itself). The reverse case also fails: leaf `dir_good_univ` (UniversalString `O=Good`) under `ncPermDir.pem` is accepted by REF (exit 0) but refused by the port (exit 60, `schannel: error 0x00004000`).
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs`: `StringTagValues = [12, 19, 20, 22, 30]` omits UniversalString (28), so `NormalisedValue` falls back to hex and never compares equal. Add tag 28 and decode it as UTF-32BE.
- **Acceptance check:** `curl -sS https://localhost:<port of xdir_univ>/ --cacert ncExclDir.pem --ssl-no-revoke` exits 60 with `schannel: error 0x00008000` (same for `xdir_univ_case` and the proxy form), and `dir_good_univ` under `ncPermDir.pem` exits 0.

## Acceptance criteria

- [ ] `curl -sS https://localhost:<port of xdir_univ>/ --cacert ncExclDir.pem --ssl-no-revoke` exits 60 with `schannel: error 0x00008000` (same for `xdir_univ_case` and the proxy form), and `dir_good_univ` under `ncPermDir.pem` exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.