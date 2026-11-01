---
id: GF-0043
title: A telnet -T upload is sometimes never sent: the upload is cancelled when the peer closes first
area: behaviour
platform: windows
key: behaviour:telnet-upload-lost-when-peer-closes
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_2029
closed: 2026-10-10_0657
regression: false
items: [behaviour:test1327]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
task: BL-1853
tasks: [BL-1853]
---
# GF-0043 - A telnet -T upload is sometimes never sent: the upload is cancelled when the peer closes first

## Summary

Curl differs from upstream curl in behaviour: A telnet -T upload is sometimes never sent: the upload is cancelled when the peer closes first.

## Evidence

Measured: test1327 expected 'upstream test1327 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET /we/want/1327 HTTP/1.0\r\n", got the end'. The command is telnet://127.0.0.1:8990 -T %LOGDIR/1327.txt with an empty <reply>. Rerun alone on 6383c570, the case passed, so the failure depends on timing under the run's parallel load; reproducing it may take several runs. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 1327

## Suggestion

In Curl.Protocol.Telnet.UnitLibrary's TelnetProtocolHandler, the -T upload runs as its own task (SendUploadAsync). When ReceiveUntilClosedAsync returns on a read of 0, the finally block cancels that task, even before it has written the file. Order the upload's write with the receive loop: send what the upload file has ready before taking the peer's close as the end, as curl's telnet loop does. Pin it with a unit test whose fake connection closes at once. If Curl.Conformance.UnitLibrary's SwsHttpServerConnector closes an empty-reply connection before reading anything (the real sws reads the request first), fix that too.

## Measurements

- 2026-10-08_2029: 1 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.
- 2026-10-11_0936: 0 of 1 items are gaps.
- 2026-10-11_1950: 0 of 1 items are gaps.
- 2026-10-12_0951: 0 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
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
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-08_2029: Opened by gap-behaviour.
- 2026-10-08_2131: Filed BL-1853.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
