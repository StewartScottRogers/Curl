---
id: BL-589
title: Register the LDAP handler for ldap and ldaps in Curl.Console with its -v lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-588, BL-830, BL-853]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-589 — Register the LDAP handler for ldap and ldaps in Curl.Console with its -v lines

## Goal

`curl ldap://...` and `curl ldaps://...` run end to end through `Curl.Console`, and `-v` writes the lines the platform's curl 8.21.0 build writes for an LDAP transfer.

## Context

- Conformance audit 2026-09-28, row 37. Handler: BL-586 to BL-588.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `ldap`/`ldaps` with ports 389 and 636; add them to the `-V` protocol list as ADR-0021 requires if that list is built here. Events go through `ITransferEvents` (ADR-0046).
- Measure `-v` for a successful search and a failed bind with `Record-CurlExchange.ps1 -Script`.

## Acceptance criteria

- [ ] Measured first as above; stderr copied into Notes with varying parts marked.
- [ ] `Curl.Console.UnitTests` run an `ldap://` search and an `ldaps://` variant through fake connectors, pinning stdout, stderr, exit code and the `-v` lines.
- [ ] `curl -V` lists `ldap` and `ldaps`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 1): The `-V` `Protocols:` line is the constant `CurlVersionText.ProtocolsLine` in `Curl.Cli.UnitLibrary`, pinned by `Curl.Cli.UnitTests/CurlVersionTextTests.cs`, and ADR-0021 Decision 6 requires it to change in the same change that registers the handler. So `touches` now names `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`. BL-645 (in Doing) touches both, so the task went back to Backlog until BL-645 is done. `ProtocolDispatcher` needs no change: it dispatches any scheme a registered handler claims, and `LdapProtocolHandler` already defaults the ports to 389 and 636.

## Log

- 2026-09-28: Created.
- 2026-09-28: Now depends on BL-853 (BL-830): WinLDAP seals the session after its logon bind, so the handler waits for that before it is registered.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Cli.UnitLibrary and Curl.Cli.UnitTests (the -V Protocols line, ADR-0021 Decision 6), which BL-645 in Doing touches; restart once BL-645 is done.
