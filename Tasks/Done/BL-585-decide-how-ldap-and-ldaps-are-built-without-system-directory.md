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
completed: 2026-09-28
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

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measured facts, alternatives weighed, the BER approach, the per-platform behaviour, the URL model, the output format source, and the exit-code mapping.
- [x] Consequences list BL-586 to BL-589.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- ADR-0165 (number checked unused on every local branch; highest elsewhere 0164). Measured with `Record-CurlExchange.ps1 -Script` and canned BER: Windows curl 8.21.0 (mingw, WinLDAP) and the Linux OpenLDAP build via WSL (curl 8.18.0, OpenLDAP 2.6.10; `-Curl wsl.exe -ListenAddress 172.26.96.1`) - the only OpenLDAP build at hand, recorded as such in the ADR. Recordings: successful search, bind 49, bind 53, bind 49 answered twice (Windows' LDAPv2 retry), search 32.
- Key facts: WinLDAP writes constructed lengths as `84 00 00 00 nn` and retries a failed bind as LDAPv2; OpenLDAP writes shortest lengths and maps bind 49 to exit 67 "Login denied" (other bind failures 38 "LDAP: cannot bind"). Default filter goes out as `ObjectClass` (WinLDAP) vs `objectclass` (OpenLDAP). Non-printable values are base64'd with `::` on both; OpenLDAP writes one more `\n` after an entry.
- Decision: hand-built on IConnection; read with `System.Formats.Asn1` (as ADR-0163), write with a hand-built `LdapBerWriter` because AsnWriter cannot write WinLDAP's long-form lengths; an `LdapDialect` chosen in CurlComposition decides every per-platform difference so tests pin both on every OS.
- The WSL `exit=$?` echo read 0 because the outer shell expanded it; the exit codes come from curl's own `curl: (NN)` prefix, which is the exit code it returns.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0165 fixes how ldap and ldaps are hand-built on IConnection, with measured WinLDAP and OpenLDAP dialects, BER approach, URL model, output format and exits 38, 39 and 67
