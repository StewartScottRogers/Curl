---
id: GF-0057
title: IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection
area: behaviour
platform: windows
key: behaviour:imap-connection-not-reused-and-tag-letter-fixed
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_1950
regression: false
items: [behaviour:test1982, behaviour:test804, behaviour:test815, behaviour:test816, behaviour:test836, behaviour:test779]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Console, Curl.Console.UnitTests]
task: BL-2080
tasks: [BL-1987, BL-2080]
---
# GF-0057 - IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection

## Summary

Curl differs from upstream curl in behaviour: IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection.

## Evidence

Every item expects 'upstream test<N> passes'. test804 (two URLs, same mailbox): '<verify><protocol> differs at byte 87 (line 5): expected "A005 FETCH 456 BODY[2.3]\r\n", got "A005 LOGOUT\r\n"'. 815/816 (-X STORE then -X CLOSE/EXPUNGE): expected 'A005 CLOSE' or 'A005 EXPUNGE', got 'A005 LOGOUT'. 1982: expected 'A005 UID FETCH 2 BODY[]', got 'A005 LOGOUT'. test836 (second user, so a second connection) and test779 (http redirect to imap): expected 'B001 CAPABILITY', got 'A001 CAPABILITY'. Cause: Curl.Protocol.Imap.UnitLibrary's ImapControlChannel builds every tag as $"A{commandId:D3}", and the session always sends LOGOUT and closes. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 804,815,836,779

## Suggestion

In Curl.Protocol.Imap.UnitLibrary, keep an IMAP connection open after a transfer and hand it to the run's connection cache, keyed by host, port, user and auth mechanism. A next URL on it continues the tag count (A005 ...) without a second login, and LOGOUT is sent when the cache closes it at exit. Make the tag letter 'A' plus the connection's number mod 26 (curl's imap.c), taking the number from the run's IConnectionNumbers, so a second connection's tags start B001. Pin both in Curl.Protocol.Imap.UnitTests.

## Measurements

- 2026-10-10_0657: 6 of 6 items are gaps.
- 2026-10-11_0936: 2 of 6 items are gaps.
- 2026-10-11_1950: 0 of 6 items are gaps.
- 2026-10-12_0951: 0 of 6 items are gaps.
- 2026-10-12_2042: 0 of 6 items are gaps.
- 2026-10-13_1406: 0 of 6 items are gaps.
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

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1987.
- 2026-10-11_1029: Filed BL-2080.
- 2026-10-11_1950: Closed: run 2026-10-11_1950 measured every item as match or excluded.
