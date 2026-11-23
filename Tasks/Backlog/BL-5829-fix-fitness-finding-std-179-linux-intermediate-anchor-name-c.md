---
id: BL-5829
title: Fix fitness finding STD-179: Linux, intermediate anchor: name constraints are applied to a dotless subject CN (CryptoAPI rule); OpenSSL curl ignores it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5829 — Fix fitness finding STD-179: Linux, intermediate anchor: name constraints are applied to a dotless subject CN (CryptoAPI rule); OpenSSL curl ignores it

## Goal

STD-179 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux, intermediate anchor: name constraints are applied to a dotless subject CN (CryptoAPI rule); OpenSSL curl ignores it. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (fails closed, but the result depends on which certificate is the anchor)
- **Confidence:** Confirmed (2 runs, 3 routes)
- **Protocol/area:** HTTPS / HTTPS-proxy name constraints, Linux build
- **Repro (Linux):** `intexcl` is a CA signed by root with `nameConstraints=critical,excluded;DNS:localhost`; leaf `leafcnexcl` (`CN=localhost`, no SAN, EKU serverAuth) is served with intexcl on 26110. `intnc` has `permitted;DNS:example.com`; leaf `leafcnnc` (`CN=localhost`, no SAN) is served with intnc on 26111. `capath_excl/` holds intexcl (rehashed).
  ```
  curl -sS -w '%{ssl_verify_result}\n' --cacert pki/intexcl.pem https://localhost:26110/
  curl -sS -w '%{ssl_verify_result}\n' --capath pki/capath_excl https://localhost:26110/
  curl -sS --proxy-cacert pki/intexcl.pem -x https://localhost:26110 http://example.invalid/
  curl -sS -w '%{ssl_verify_result}\n' --cacert pki/intnc.pem https://localhost:26111/
  curl -sS -w '%{ssl_verify_result}\n' --cacert pki/root.pem https://localhost:26110/
  ```
- **Expected (REF2):** every command prints `hello` and `0`, exit 0 (OpenSSL checks a CN against DNS constraints only if it looks like a hostname with a dot). With a dotted CN (`www.example.com` under `excluded;DNS:example.com`) both builds exit 60.
- **Actual (.NET):** the first three exit 60 with `SSL certificate OpenSSL verify result: excluded subtree violation (48)`; the fourth exits 60 with `permitted subtree violation (47)`; the root-anchored fifth exits 0.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` `ListNames` → `DnsNamesOrCommonNames`: the BL-5432 CryptoAPI rule also runs in the OpenSSL build, where it should follow OpenSSL (only a CN with a dot and valid hostname characters).
- **Acceptance check:** the first four commands exit 0 with `hello` and `0`; a leaf with `CN=www.example.com` under `excluded;DNS:example.com` still exits 60 with `(48)`.

## Acceptance criteria

- [ ] the first four commands exit 0 with `hello` and `0`; a leaf with `CN=www.example.com` under `excluded;DNS:example.com` still exits 60 with `(48)`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.