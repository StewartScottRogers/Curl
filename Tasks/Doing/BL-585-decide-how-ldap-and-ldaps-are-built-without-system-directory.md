---
id: BL-585
title: Decide how ldap and ldaps are built without System.DirectoryServices.Protocols
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-585 — Decide how ldap and ldaps are built without System.DirectoryServices.Protocols

## Goal

An ADR fixes how `Curl.Protocol.Ldap.UnitLibrary` speaks LDAPv3 on `IConnection` with the base class library only, which reference build's behaviour it matches on each platform (the Windows build uses WinLDAP, OpenSSL builds usually OpenLDAP; their output and errors may differ), how the URL is read (RFC 4516), how results are written, and which exits (38 bind, 39 search, others) apply when.

## Context

- Conformance audit 2026-09-28, row 37 (Major, L; exits 38 and 39 never produced). `Curl.Protocol.Ldap.UnitLibrary/CLAUDE.md`: Abstractions only, `IConnection`.
- `System.DirectoryServices.Protocols` is a NuGet package, not part of the base class library, so it is out (adding it would need Stewart). `System.Formats.Asn1` is in the BCL and handles BER/DER; decide whether to use it or hand-write the small BER subset LDAP needs.
- Measure first: `curl -V` on the reference build (confirm `ldap`, `ldaps` and the LDAP library it names), then `Record-CurlExchange.ps1 -Script` (BL-532) with canned BER replies, or `-NoServer` (BL-528) against a local OpenLDAP `slapd` if one is available, for a successful search, a failed bind and a failed search.
- Depends on BL-498 (timeouts) and BL-515 (endpoints).

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measured facts, alternatives weighed, the BER approach, the per-platform behaviour, the URL model, the output format source, and the exit-code mapping.
- [ ] Consequences list BL-586 to BL-589.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
