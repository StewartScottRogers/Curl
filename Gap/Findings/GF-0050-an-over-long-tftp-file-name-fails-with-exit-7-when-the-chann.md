---
id: GF-0050
title: An over-long TFTP file name fails with exit 7 when the channel cannot open, where curl refuses it first with exit 71
area: behaviour
platform: windows
key: behaviour:tftp-filename-too-long-checked-after-channel-opens
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test1453]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
task: BL-1980
tasks: [BL-1980]
---
# GF-0050 - An over-long TFTP file name fails with exit 7 when the channel cannot open, where curl refuses it first with exit 71

## Summary

Curl differs from upstream curl in behaviour: An over-long TFTP file name fails with exit 7 when the channel cannot open, where curl refuses it first with exit 71.

## Evidence

behaviour:test1453 (tftp://%HOSTIP:%NOLISTENPORT/ with a 504-character name) expected 'upstream test1453 passes', actual '<verify><errorcode>: expected exit code 71, got 7'. Cause: Curl.Protocol.Tftp.UnitLibrary's TftpProtocolHandler opens the datagram channel (line 152) before TftpRequestFile.TryBuildRequest checks the length, so a channel that fails to open wins. curl's UDP socket opens without contacting the peer, and tftp_send_first refuses the name with exit 71. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1453

## Suggestion

In Curl.Protocol.Tftp.UnitLibrary's TftpProtocolHandler, run TftpRequestFile's length checks ('TFTP filename too long', 'TFTP buffer too small for options', exit 71) before opening the datagram channel, as the 'Missing filename' check already is. Pin it with a test whose datagram connector refuses to open.

## Measurements

- 2026-10-10_0657: 1 of 1 items are gaps.
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

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1980.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
