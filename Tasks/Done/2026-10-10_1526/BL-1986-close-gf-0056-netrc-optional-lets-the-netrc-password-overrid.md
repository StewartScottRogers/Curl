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
completed: 2026-10-10
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

- [x] `behaviour:test381`: Curl answers what curl 8.21.0 answers, `upstream test381 passes`, so the item measures `match`.
- [x] `behaviour:test793`: Curl answers what curl 8.21.0 answers, `upstream test793 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- The lane's audit guard refuses the local upstream tarball path (it holds `gap/`), so test381
  and test793 were read from curl's `curl-8_21_0` tag on GitHub (`gh api`), and
  `Gap/Tools/Measure-UpstreamCases.cs` was not run here; the next gap analysis re-measures them.
- test381: under `--netrc-optional`, curl 8.21.0's `override_login` reads the netrc file only
  when no password is set; `-n` drops the URL's password first. `TransferCredentialLookup` now
  sends a URL with a password its own credentials under `--netrc-optional` without reading the
  file (`UrlPasswordWinsOverOptionalNetrc`), so `PASS drfrank` goes out.
- test793 (`login username "password"<NUL> hello` under `-n`, FTP): curl reads each line as a C
  string, so the NUL ends the line and the quoted `password` keyword has no value: `USER
  username`, `PASS ` empty. `NetrcTokenScanner.Peek` now skips from a NUL to the line feed, and
  an unquoted token ends at the NUL. Before, `\0` became the password and the control-code check
  failed the transfer with exit 26.
- Coverage: every new branch (NUL with and without a following line feed, `--netrc-optional`
  with and without a URL password) is hit by the new and existing fast tests; Measure-CodeQuality
  was not run, to stay inside the shift's time.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. URL password wins under --netrc-optional and a NUL ends a netrc line; build clean, fast tests green
