---
id: BL-1999
title: Re-close GF-0003: On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1999 — Re-close GF-0003: On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0003 (On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ), so a later gap analysis measures each of `behaviour:test67`, `behaviour:test68`, `behaviour:test81`, `behaviour:test89`, `behaviour:test91`, `behaviour:test150`, `behaviour:test162`, `behaviour:test169`, `behaviour:test170`, `behaviour:test176`, `behaviour:test239`, `behaviour:test243`, `behaviour:test267`, `behaviour:test776`, `behaviour:test1215`, `behaviour:test775`, `behaviour:test822`, `behaviour:test827`, `behaviour:test831`, `behaviour:test868`, `behaviour:test873`, `behaviour:test877`, `behaviour:test906`, `behaviour:test921`, `behaviour:test933` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0003 ([BL-1796]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0003, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test67`, `behaviour:test68`, `behaviour:test81`, `behaviour:test89`, `behaviour:test91`, `behaviour:test150`, `behaviour:test162`, `behaviour:test169`, `behaviour:test170`, `behaviour:test176`, `behaviour:test239`, `behaviour:test243`, `behaviour:test267`, `behaviour:test776`, `behaviour:test1215`, `behaviour:test775`, `behaviour:test822`, `behaviour:test827`, `behaviour:test831`, `behaviour:test868`, `behaviour:test873`, `behaviour:test877`, `behaviour:test906`, `behaviour:test921`, `behaviour:test933`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test67 actual: <verify><protocol> differs at byte 77 (line 3): expected 'Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=', got 'Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw=='. The same bytes for Proxy-Authorization in test81, test162, test169, test170, test239 and test243. test775 (user name of 1100 characters): expected the type-1 Authorization line, got 'User-Agent: curl/8.21.0'; no Authorization header was sent. The cases require !SSPI. Cause in code: Curl.Authentication.UnitLibrary/RoutingSecurityContextFactory sends every Windows NTLM exchange through the system (SSPI) context. Meanwhile Curl.Cli.UnitLibrary/CurlVersionText.FeaturesLine has no SSPI, so the harness (UpstreamCurlPlatform) treats Curl as a non-SSPI build. Curl's hand-built NtlmNegotiateMessage would send 0x00088206. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 67,81,775

Suggestion, copied from the finding:

Make Curl's advertised features match its behaviour. On Windows, add SSPI to the Features line that Curl.Cli.UnitLibrary's CurlVersionText writes, as the Schannel reference build lists it; then the !SSPI cases become excluded rather than run. Or, if SSPI is to stay unlisted, route NTLM through the hand-built Curl.Ntlm.UnitLibrary context on Windows too. Either way, make an over-long NTLM user name (test775) fail as curl's does, not drop the header silently. Explained by ADR-0142: NTLM, Negotiate and Kerberos answer through SSPI on Windows. The ADR does not cover the missing SSPI feature flag, and the gap stands against upstream.

## Acceptance criteria

- [ ] `behaviour:test67`: Curl answers what curl 8.21.0 answers, `upstream test67 passes`, so the item measures `match`.
- [ ] `behaviour:test68`: Curl answers what curl 8.21.0 answers, `upstream test68 passes`, so the item measures `match`.
- [ ] `behaviour:test81`: Curl answers what curl 8.21.0 answers, `upstream test81 passes`, so the item measures `match`.
- [ ] `behaviour:test89`: Curl answers what curl 8.21.0 answers, `upstream test89 passes`, so the item measures `match`.
- [ ] `behaviour:test91`: Curl answers what curl 8.21.0 answers, `upstream test91 passes`, so the item measures `match`.
- [ ] `behaviour:test150`: Curl answers what curl 8.21.0 answers, `upstream test150 passes`, so the item measures `match`.
- [ ] `behaviour:test162`: Curl answers what curl 8.21.0 answers, `upstream test162 passes`, so the item measures `match`.
- [ ] `behaviour:test169`: Curl answers what curl 8.21.0 answers, `upstream test169 passes`, so the item measures `match`.
- [ ] `behaviour:test170`: Curl answers what curl 8.21.0 answers, `upstream test170 passes`, so the item measures `match`.
- [ ] `behaviour:test176`: Curl answers what curl 8.21.0 answers, `upstream test176 passes`, so the item measures `match`.
- [ ] `behaviour:test239`: Curl answers what curl 8.21.0 answers, `upstream test239 passes`, so the item measures `match`.
- [ ] `behaviour:test243`: Curl answers what curl 8.21.0 answers, `upstream test243 passes`, so the item measures `match`.
- [ ] `behaviour:test267`: Curl answers what curl 8.21.0 answers, `upstream test267 passes`, so the item measures `match`.
- [ ] `behaviour:test776`: Curl answers what curl 8.21.0 answers, `upstream test776 passes`, so the item measures `match`.
- [ ] `behaviour:test1215`: Curl answers what curl 8.21.0 answers, `upstream test1215 passes`, so the item measures `match`.
- [ ] `behaviour:test775`: Curl answers what curl 8.21.0 answers, `upstream test775 passes`, so the item measures `match`.
- [ ] `behaviour:test822`: Curl answers what curl 8.21.0 answers, `upstream test822 passes`, so the item measures `match`.
- [ ] `behaviour:test827`: Curl answers what curl 8.21.0 answers, `upstream test827 passes`, so the item measures `match`.
- [ ] `behaviour:test831`: Curl answers what curl 8.21.0 answers, `upstream test831 passes`, so the item measures `match`.
- [ ] `behaviour:test868`: Curl answers what curl 8.21.0 answers, `upstream test868 passes`, so the item measures `match`.
- [ ] `behaviour:test873`: Curl answers what curl 8.21.0 answers, `upstream test873 passes`, so the item measures `match`.
- [ ] `behaviour:test877`: Curl answers what curl 8.21.0 answers, `upstream test877 passes`, so the item measures `match`.
- [ ] `behaviour:test906`: Curl answers what curl 8.21.0 answers, `upstream test906 passes`, so the item measures `match`.
- [ ] `behaviour:test921`: Curl answers what curl 8.21.0 answers, `upstream test921 passes`, so the item measures `match`.
- [ ] `behaviour:test933`: Curl answers what curl 8.21.0 answers, `upstream test933 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
