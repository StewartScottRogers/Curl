---
id: GF-0003
title: On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ
area: behaviour
key: behaviour:ntlm-type1-sspi-flavoured-without-sspi-feature
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test67, behaviour:test68, behaviour:test81, behaviour:test89, behaviour:test91, behaviour:test150, behaviour:test162, behaviour:test169, behaviour:test170, behaviour:test176, behaviour:test239, behaviour:test243, behaviour:test267, behaviour:test776, behaviour:test1215, behaviour:test775, behaviour:test822, behaviour:test827, behaviour:test831, behaviour:test868, behaviour:test873, behaviour:test877, behaviour:test906, behaviour:test921, behaviour:test933]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
task: BL-1796
tasks: [BL-1796]
---
# GF-0003 - On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ

## Summary

Curl differs from upstream curl in behaviour: On Windows the NTLM type-1 message is SSPI's (flags 0xa2088207 plus VERSION), but curl -V does not list SSPI, so the !SSPI cases run and differ.

## Evidence

Every item expects 'upstream test<N> passes'. test67 actual: <verify><protocol> differs at byte 77 (line 3): expected 'Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=', got 'Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw=='. The same bytes for Proxy-Authorization in test81, test162, test169, test170, test239 and test243. test775 (user name of 1100 characters): expected the type-1 Authorization line, got 'User-Agent: curl/8.21.0'; no Authorization header was sent. The cases require !SSPI. Cause in code: Curl.Authentication.UnitLibrary/RoutingSecurityContextFactory sends every Windows NTLM exchange through the system (SSPI) context. Meanwhile Curl.Cli.UnitLibrary/CurlVersionText.FeaturesLine has no SSPI, so the harness (UpstreamCurlPlatform) treats Curl as a non-SSPI build. Curl's hand-built NtlmNegotiateMessage would send 0x00088206. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 67,81,775

## Suggestion

Make Curl's advertised features match its behaviour. On Windows, add SSPI to the Features line that Curl.Cli.UnitLibrary's CurlVersionText writes, as the Schannel reference build lists it; then the !SSPI cases become excluded rather than run. Or, if SSPI is to stay unlisted, route NTLM through the hand-built Curl.Ntlm.UnitLibrary context on Windows too. Either way, make an over-long NTLM user name (test775) fail as curl's does, not drop the header silently. Explained by ADR-0142: NTLM, Negotiate and Kerberos answer through SSPI on Windows. The ADR does not cover the missing SSPI feature flag, and the gap stands against upstream.

## Measurements

- 2026-10-08_1640: 16 of 16 items are gaps.
- 2026-10-08_2029: 16 of 16 items are gaps.
- 2026-10-10_0657: 25 of 25 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1796.
- 2026-10-10_0657: Added behaviour:test822, behaviour:test827, behaviour:test831, behaviour:test868, behaviour:test873, behaviour:test877, behaviour:test906, behaviour:test921, behaviour:test933 from gap-behaviour.
