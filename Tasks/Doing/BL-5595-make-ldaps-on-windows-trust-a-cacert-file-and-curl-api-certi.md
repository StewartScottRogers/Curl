---
id: BL-5595
title: Make ldaps:// on Windows trust a --cacert file (and Curl.Api CertificateAuthorityBlob) as https:// does, if curl 8.22.0 Schannel does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5595 — Make ldaps:// on Windows trust a --cacert file (and Curl.Api CertificateAuthorityBlob) as https:// does, if curl 8.22.0 Schannel does

## Goal

On Windows, `curl --cacert server.pem ldaps://...` against a server whose certificate that file trusts behaves as the reference curl 8.22.0 Schannel build does, and `Curl.Api`'s `CertificateAuthorityBlob` follows the same rule.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #1. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`surl --cert server.pem --key server.key ... ldaps://127.0.0.1:PORT/`, then `curl --cacert server.pem "ldaps://127.0.0.1:PORT/dc=example,dc=com?cn?sub"`: exit 60, `SEC_E_UNTRUSTED_ROOT`. The same `--cacert` works for `https://` against the same certificate. `Curl.Api`'s `CertificateAuthorityBlob` fails the same way; only `-k` works. Linux and macOS not yet checked.

**Measure first:** upstream curl's WinLDAP backend may itself ignore `CURLOPT_CAINFO` for ldaps (WinLDAP verifies against the Windows store). Run the same command with the reference `curl.exe` 8.22.0 Schannel (`Record-CurlExchange.ps1` or the gap office's reference) and with the OpenSSL build on Linux. If the reference also exits 60 on Windows, the port already matches: record that in an ADR and in the LDAP docs (and the `--ai-help` LDAP section) and change nothing else. If the reference trusts the file, make the WinLDAP path do the same (for example through the server-certificate verification callback, checking the chain against the file as the HTTPS path does).

## Acceptance criteria

- [ ] The Notes record the reference curl 8.22.0 Schannel result for the repro on Windows and the OpenSSL build's result on Linux.
- [ ] The port's exit code and error text for the repro match the reference on each platform, pinned by a test.
- [ ] If the port keeps exit 60 on Windows, an ADR and the LDAP docs say `--cacert` does not apply to ldaps there; if it now trusts the file, `CertificateAuthorityBlob` does too, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (1942 #1).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 3ce73eb5 40717039 c58aac16 3d836d5e.
