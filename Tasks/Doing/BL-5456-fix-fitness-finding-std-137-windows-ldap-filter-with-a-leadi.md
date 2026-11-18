---
id: BL-5456
title: Fix fitness finding STD-137: Windows LDAP filter with a leading or trailing TAB/CR/LF: curl exits 3 "Bad LDAP URL"; the port sends a garbage filter (exit 0) or exits 39
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5456 — Fix fitness finding STD-137: Windows LDAP filter with a leading or trailing TAB/CR/LF: curl exits 3 "Bad LDAP URL"; the port sends a garbage filter (exit 0) or exits 39

## Goal

STD-137 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP filter with a leading or trailing TAB/CR/LF: curl exits 3 "Bad LDAP URL"; the port sends a garbage filter (exit 0) or exits 39. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (4 shapes, 2 runs)
- **Protocol/area:** LDAP URL/filter, WinLDAP dialect, Windows
- **Repro:** the server and relay from STD-134. `curl -sS -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?%09(uid=alice)"`; also `%0a(uid=alice)`, `%0d(uid=alice)` and `(uid=alice)%09`.
- **Expected (REF):** `curl: (3) Bad LDAP URL: No Memory`, exit 3, nothing sent.
- **Actual (PORT):** the leading forms send an equalityMatch with attribute `\t(uid` and value `alice)` (`040509287569640406616c69636529`), exit 0, no output. The trailing TAB gives `curl: (39) LDAP remote: Filter Error`, exit 39.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs` and `LdapFilterEncoder.cs`.
- **Acceptance check:** each command exits 3 with `curl: (3) Bad LDAP URL: No Memory`.

## Acceptance criteria

- [x] each command exits 3 with `curl: (3) Bad LDAP URL: No Memory`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

WinLdapUrlReader now refuses a decoded filter starting or ending with TAB, CR or LF with "Bad LDAP URL: No Memory" (exit 3). Leading or trailing space was not in the finding and is left as is. Not re-measured against real curl here; pinned from the finding. Quality run: 0 failing.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-137 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 2516460f 69116afe 3d836d5e.
