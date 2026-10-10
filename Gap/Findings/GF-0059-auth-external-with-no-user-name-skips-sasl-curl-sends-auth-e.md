---
id: GF-0059
title: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response
area: behaviour
key: behaviour:sasl-external-without-user-skipped
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test838, behaviour:test840, behaviour:test884, behaviour:test886, behaviour:test943, behaviour:test945]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
task:
tasks: []
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

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
