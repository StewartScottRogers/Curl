---
id: BL-5606
title: Match curl's OpenLDAP-build exit code for --ntlm against an LDAP server that accepts NTLM
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5606 — Match curl's OpenLDAP-build exit code for --ntlm against an LDAP server that accepts NTLM

## Goal

On Linux and macOS (the OpenLDAP dialect), `curl --ntlm -u user:pass ldap://...` against a server that accepts NTLM gives the same result as the reference curl OpenSSL/OpenLDAP build.

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #9. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

With the OpenLDAP backend, `--ntlm` exits 67 even against a server that accepts NTLM; other bind refusals are 38 (`LDAP: cannot bind`). The fitness review of 2026-11-18 (STD-117) pinned `--ntlm` against a server *not* offering NTLM as 67 `Login denied`, matching curl 8.18.0 OpenLDAP; this is the other case. Measure the reference (the gap office's Linux curl built with OpenLDAP) against a server whose supportedSASLMechanisms lists NTLM (Surl's LDAP server, per the report): if curl does an NTLM SASL bind there, implement it in the OpenLDAP dialect; if curl also exits 67, the port matches and only a test is added.

## Acceptance criteria

- [ ] The Notes record the reference curl's result on Linux for `--ntlm` against a server offering NTLM.
- [ ] The port's exit code and stderr on Linux match the reference for that case, pinned by a test that runs off Windows.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (2203 #9).
