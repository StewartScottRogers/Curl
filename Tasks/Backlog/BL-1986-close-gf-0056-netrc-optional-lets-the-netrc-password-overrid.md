---
id: BL-1986
title: Close GF-0056: --netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1986 — Close GF-0056: --netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0056 (--netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer), so a later gap analysis measures each of `behaviour:test381`, `behaviour:test793` as `match`.

## Context

- Finding: GF-0056, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test381`, `behaviour:test793`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test381 (ftp://mary:drfrank@... --netrc-optional): '<verify><protocol> differs at byte 16 (line 2): expected "PASS drfrank\r\n", got "PASS yram\r\n"'. test793 (.netrc with an embedded NUL and a quoted token): expected 'USER username', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 381,793

Suggestion, copied from the finding:

In Curl.Console's TransferCredentialLookup, under --netrc-optional, keep a password the URL gives and take only what the URL lacks from .netrc, as curl 8.21.0 does. In Curl.Authentication.UnitLibrary's NetrcTokenScanner/NetrcFile, read a .netrc holding a NUL byte as curl does, ending the token or line there and carrying on with the quoted-token rules, rather than failing the lookup.

## Acceptance criteria

- [ ] `behaviour:test381`: Curl answers what curl 8.21.0 answers, `upstream test381 passes`, so the item measures `match`.
- [ ] `behaviour:test793`: Curl answers what curl 8.21.0 answers, `upstream test793 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
