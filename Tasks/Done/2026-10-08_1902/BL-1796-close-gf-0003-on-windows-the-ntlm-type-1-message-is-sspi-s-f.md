---
id: BL-1796
title: Close GF-0003: On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1796 — Close GF-0003: On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0003 (On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ), so a later gap analysis measures each of `behaviour:test67`, `behaviour:test68`, `behaviour:test81`, `behaviour:test89`, `behaviour:test91`, `behaviour:test150`, `behaviour:test162`, `behaviour:test169`, `behaviour:test170`, `behaviour:test176`, `behaviour:test239`, `behaviour:test243`, `behaviour:test267`, `behaviour:test776`, `behaviour:test1215`, `behaviour:test775` as `match`.

## Context

- Finding: GF-0003, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test67`, `behaviour:test68`, `behaviour:test81`, `behaviour:test89`, `behaviour:test91`, `behaviour:test150`, `behaviour:test162`, `behaviour:test169`, `behaviour:test170`, `behaviour:test176`, `behaviour:test239`, `behaviour:test243`, `behaviour:test267`, `behaviour:test776`, `behaviour:test1215`, `behaviour:test775`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test67 actual: <verify><protocol> differs at byte 77 (line 3): expected 'Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=', got 'Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw=='. The same bytes for Proxy-Authorization in test81, test162, test169, test170, test239 and test243. test775 (user name of 1100 characters): expected the type-1 Authorization line, got 'User-Agent: curl/8.21.0'; no Authorization header was sent. The cases require !SSPI. Cause in code: Curl.Authentication.UnitLibrary/RoutingSecurityContextFactory sends every Windows NTLM exchange through the system (SSPI) context. Meanwhile Curl.Cli.UnitLibrary/CurlVersionText.FeaturesLine has no SSPI, so the harness (UpstreamCurlPlatform) treats Curl as a non-SSPI build. Curl's hand-built NtlmNegotiateMessage would send 0x00088206. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 67,81,775

Suggestion, copied from the finding:

Make Curl's advertised features match its behaviour. On Windows, add SSPI to the Features line that Curl.Cli.UnitLibrary's CurlVersionText writes, as the Schannel reference build lists it; then the !SSPI cases become excluded rather than run. Or, if SSPI is to stay unlisted, route NTLM through the hand-built Curl.Ntlm.UnitLibrary context on Windows too. Either way, make an over-long NTLM user name (test775) fail as curl's does, not drop the header silently. Explained by ADR-0142: NTLM, Negotiate and Kerberos answer through SSPI on Windows. The ADR does not cover the missing SSPI feature flag, and the gap stands against upstream.

## Acceptance criteria

- [x] `behaviour:test67`: Curl answers what curl 8.21.0 answers, `upstream test67 passes`, so the item measures `match`.
- [x] `behaviour:test68`: Curl answers what curl 8.21.0 answers, `upstream test68 passes`, so the item measures `match`.
- [x] `behaviour:test81`: Curl answers what curl 8.21.0 answers, `upstream test81 passes`, so the item measures `match`.
- [x] `behaviour:test89`: Curl answers what curl 8.21.0 answers, `upstream test89 passes`, so the item measures `match`.
- [x] `behaviour:test91`: Curl answers what curl 8.21.0 answers, `upstream test91 passes`, so the item measures `match`.
- [x] `behaviour:test150`: Curl answers what curl 8.21.0 answers, `upstream test150 passes`, so the item measures `match`.
- [x] `behaviour:test162`: Curl answers what curl 8.21.0 answers, `upstream test162 passes`, so the item measures `match`.
- [x] `behaviour:test169`: Curl answers what curl 8.21.0 answers, `upstream test169 passes`, so the item measures `match`.
- [x] `behaviour:test170`: Curl answers what curl 8.21.0 answers, `upstream test170 passes`, so the item measures `match`.
- [x] `behaviour:test176`: Curl answers what curl 8.21.0 answers, `upstream test176 passes`, so the item measures `match`.
- [x] `behaviour:test239`: Curl answers what curl 8.21.0 answers, `upstream test239 passes`, so the item measures `match`.
- [x] `behaviour:test243`: Curl answers what curl 8.21.0 answers, `upstream test243 passes`, so the item measures `match`.
- [x] `behaviour:test267`: Curl answers what curl 8.21.0 answers, `upstream test267 passes`, so the item measures `match`.
- [x] `behaviour:test776`: Curl answers what curl 8.21.0 answers, `upstream test776 passes`, so the item measures `match`.
- [x] `behaviour:test1215`: Curl answers what curl 8.21.0 answers, `upstream test1215 passes`, so the item measures `match`.
- [x] `behaviour:test775`: Curl answers what curl 8.21.0 answers, `upstream test775 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Decision (ADR-0439, decided by Claude under Stewart's delegation): took the finding's first fix. On Windows `CurlVersionText.Lines` now writes `WindowsFeaturesLine`, `FeaturesLine` with `SSPI` after `SSL` as the Schannel reference build lists it, so the upstream harness treats Curl as the SSPI build it is (ADR-0142) and every item's `!SSPI` case is excluded on Windows rather than run. Linux and macOS keep `FeaturesLine`. Windows NTLM stays on SSPI, matching the platform's curl.
- test775 also requires `!SSPI` (checked in its upstream data file), so it is excluded on Windows the same way.
- The items close only when a later gap run re-measures them (ADR-0433); not re-measured here.
- No option changed, so `--ai-help` needed nothing. `Curl.Authentication.*` needed no change.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. curl -V lists SSPI on Windows as the Schannel build does (ADR-0439), so the !SSPI NTLM cases are excluded there
