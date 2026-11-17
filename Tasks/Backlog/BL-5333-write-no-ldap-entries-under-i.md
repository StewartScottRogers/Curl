---
id: BL-5333
title: Write no LDAP entries under -I
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryWriter.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapNoBodyTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5333 — Write no LDAP entries under -I

## Goal

`curl -I ldap://...` binds and searches but writes no entry to stdout and exits 0, as curl does.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-083 (Low, Confirmed, 2 runs). Area: LDAP with `-I`/`--head`, Linux build. (curl 8.22 on Windows could not be compared against the review's server: WinLDAP's bind fails for both curl and the port.)

**Repro (Linux).** The LDAP server of BL-5332 (a BER stand-in in PowerShell or a C# file-based app, never Python) returning entries `cn=alice,dc=x` (cn, mail, jpegPhoto) and `cn=bob,dc=x`. `curl -sS -m 8 -I "ldap://127.0.0.1:35389/dc=x?cn"; echo $?`

**Expected (curl 8.18).** Empty stdout, exit 0 (it binds and searches but writes no entry).

**Actual (port).** Prints `DN: cn=alice,dc=x` with its attributes, exit 0.

**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryWriter.cs` / `LdapProtocolHandler.cs` ignore the no-body flag (`ITransferContext.NoBody`, `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs:114`; neither LDAP file reads it today). Keep the search and bind on the wire; only the entry output is suppressed. Check whether the Windows (WinLDAP) dialect behaves the same under `-I` before applying it to both; if curl's source or measurement says otherwise, note it in the Log.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ldap.UnitTests/LdapNoBodyTests.cs` prove that with `NoBody` set a search that returns two entries writes zero bytes to the output stream, still sends the bind and SearchRequest (stand-in connection), and returns exit 0.
- [ ] A test proves that without `NoBody` the output is byte-for-byte what the existing tests expect (`LdapProtocolHandlerTests.Output.cs` stays green).
- [ ] Tests are platform-neutral (stand-in connection).
- [ ] Hand check, recorded in the Log: the repro command prints nothing and exits 0; without `-I` the output is unchanged; measure real curl with `Record-CurlExchange.ps1` or WSL and say which version.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints nothing and exits 0; without `-I` the output is unchanged.
- This task touches `LdapProtocolHandler.cs`, which BL-5332 may also change; the two never run at once.

## Log

- 2026-11-16: Created.
