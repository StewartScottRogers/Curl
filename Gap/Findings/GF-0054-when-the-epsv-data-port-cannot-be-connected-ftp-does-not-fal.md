---
id: GF-0054
title: When the EPSV data port cannot be connected, FTP does not fall back to PASV
area: behaviour
platform: windows
key: behaviour:ftp-epsv-connect-failure-no-pasv-fallback
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test1233]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task: BL-1984
tasks: [BL-1984]
---
# GF-0054 - When the EPSV data port cannot be connected, FTP does not fall back to PASV

## Summary

Curl differs from upstream curl in behaviour: When the EPSV data port cannot be connected, FTP does not fall back to PASV.

## Evidence

behaviour:test1233 (EPSV answered '229 Entering Passive Mode (|||1|)') expected 'upstream test1233 passes' with EPSV, PASV, TYPE I, SIZE, RETR, QUIT, actual 'curl did not finish within 20 seconds'. Curl.Protocol.Ftp.UnitLibrary/FtpSession.OpenPassiveDataConnectionAsync falls back to PASV only when the EPSV reply is not 229, not when the connection to its port fails. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1233

## Suggestion

In Curl.Protocol.Ftp.UnitLibrary's FtpSession.OpenPassiveDataConnectionAsync, when the data connection to the EPSV port fails, send PASV and use its port, as curl 8.21.0 does ('Failed EPSV attempt. Switching to PASV'). Pin it with a connector that refuses the EPSV port. If the 20-second hang comes from the harness's FTP connector never refusing port 1, fix that in Curl.Conformance.UnitLibrary as well.

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

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1984.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
