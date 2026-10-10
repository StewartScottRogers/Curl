---
id: GF-0052
title: --ignore-content-length does not stop FTP from sending SIZE before RETR
area: behaviour
key: behaviour:ftp-ignore-content-length-still-sends-size
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test1137, behaviour:test416]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0052 - --ignore-content-length does not stop FTP from sending SIZE before RETR

## Summary

Curl differs from upstream curl in behaviour: --ignore-content-length does not stop FTP from sending SIZE before RETR.

## Evidence

Every item expects 'upstream test<N> passes'. test1137: '<verify><protocol> differs at byte 63 (line 7): expected "RETR 1137\r\n", got "SIZE 1137\r\n"'. test416 (EPSV, Range) the same at byte 57. Curl.Protocol.Ftp.UnitLibrary has no reference to the option. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1137,416

## Suggestion

Carry --ignore-content-length to the FTP handler (Curl.Console's transfer-context mapping) and, in Curl.Protocol.Ftp.UnitLibrary's FtpSession download path, skip SIZE and go straight to RETR when it is set, as curl 8.21.0 does (data->set.ignorecl).

## Measurements

- 2026-10-10_0657: 2 of 2 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
