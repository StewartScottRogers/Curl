---
id: GF-0081
title: Plain HTTP sent through -x to the HTTPS port ends in a clean close (exit 52) instead of stunnel's reset (exit 56)
area: behaviour
platform: windows
key: behaviour:harness-tls-server-closes-cleanly-on-plaintext
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0304
closed: 2026-10-15_0543
regression: false
items: [behaviour:test1244]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2272
tasks: [BL-2272]
---
# GF-0081 - Plain HTTP sent through -x to the HTTPS port ends in a clean close (exit 52) instead of stunnel's reset (exit 56)

## Summary

Curl differs from upstream curl in behaviour: Plain HTTP sent through -x to the HTTPS port ends in a clean close (exit 52) instead of stunnel's reset (exit 56).

## Evidence

behaviour:test1244 expected 'upstream test1244 passes', actual '<verify><errorcode>: expected exit code 56, got 52'. The case's second transfer is '--next http://%HOSTIP:%HTTPPORT/<N>00001 -x %HOSTIP:%HTTPSPORT', so curl speaks plain HTTP to the TLS server; upstream's stunnel drops that connection with a reset and curl reports 56 (Recv failure). Curl.Conformance.UnitLibrary/TlsServerConnection.cs ServeAsync swallows the failed handshake and disposes the in-memory stream, which the client reads as a clean end of stream, so Curl reports 52 (empty reply) for what it was given. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- '<release>/tests/data' $env:TEMP/raw.json 1244

## Suggestion

In Curl.Conformance.UnitLibrary TlsServerConnection.ServeAsync, end a connection whose handshake fails on a non-TLS first record as a reset rather than a clean close: let InMemoryDuplexStream fault the client's next read with an IOException carrying SocketException(ConnectionReset), as a socket closed with unread data does. Then confirm Curl.Protocol.Http.UnitLibrary maps a reset on the first response read to exit 56 'Recv failure: Connection was reset', with a focused test in Curl.Protocol.Http.UnitTests, and pin the harness change in Curl.Conformance.UnitTests.

## Measurements

- 2026-10-15_0304: 1 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-15_0304: Opened by gap-behaviour.
- 2026-10-15_0349: Filed BL-2272.
- 2026-10-15_0543: Closed: run 2026-10-15_0543 measured every item as match or excluded.
