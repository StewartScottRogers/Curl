---
id: GF-0059
title: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response
area: behaviour
platform: windows
key: behaviour:sasl-external-without-user-skipped
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-14_0904
regression: true
items: [behaviour:test838, behaviour:test840, behaviour:test884, behaviour:test886, behaviour:test943, behaviour:test945]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
task: BL-2161
tasks: [BL-1989, BL-2161]
---
# GF-0059 - ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response

## Summary

Curl differs from upstream curl in behaviour: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response.

## Evidence

Every item expects 'upstream test<N> passes'. test838 ('imap://;AUTH=EXTERNAL@host/...'): '<verify><protocol> differs at byte 17 (line 2): expected "A002 AUTHENTICATE EXTERNAL\r\n", got the end'; 840 (SASL-IR) expected 'A002 AUTHENTICATE EXTERNAL ='. test884/886 (pop3): expected 'AUTH EXTERNAL' / 'AUTH EXTERNAL =', got 'RETR 884'. test943/945 (smtp 'external authentication without credentials'): expected 'AUTH EXTERNAL' / 'AUTH EXTERNAL =', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 838,840,884,943

## Suggestion

In Curl.Authentication.UnitLibrary's SaslAuthenticator/SaslMechanismRanking and the IMAP, POP3 and SMTP login decisions, let a ;AUTH=EXTERNAL login option start SASL without a user name or password. EXTERNAL's response is the (empty) user name, sent as '=' when empty, inline under SASL-IR or after the '+' / '334' continuation, as curl 8.21.0 does.

## Measurements

- 2026-10-10_0657: 6 of 6 items are gaps.
- 2026-10-11_0936: 0 of 6 items are gaps.
- 2026-10-11_1950: 0 of 6 items are gaps.
- 2026-10-12_0951: 0 of 6 items are gaps.
- 2026-10-12_2042: 0 of 6 items are gaps.
- 2026-10-13_1406: 3 of 6 items are gaps.
- 2026-10-14_0904: 0 of 6 items are gaps.
- 2026-10-15_0304: 0 of 6 items are gaps.
- 2026-10-15_0543: 0 of 6 items are gaps.
- 2026-10-15_0751: 0 of 6 items are gaps.
- 2026-10-15_1657: 0 of 6 items are gaps.
- 2026-10-17_2225: 0 of 6 items are gaps.
- 2026-10-19_0804: 0 of 6 items are gaps.
- 2026-10-19_1329: 0 of 6 items are gaps.
- 2026-10-22_0148: 0 of 6 items are gaps.
- 2026-10-23_1637: 0 of 6 items are gaps.
- 2026-10-25_1542: 0 of 6 items are gaps.
- 2026-10-30_2103: 0 of 6 items are gaps.
- 2026-10-31_1828: 0 of 6 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1989.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
- 2026-10-13_1406: Reopened as a regression: run 2026-10-13_1406 measured behaviour:test838, behaviour:test884, behaviour:test943 as gap again.
- 2026-10-13_1457: Filed BL-2161.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
