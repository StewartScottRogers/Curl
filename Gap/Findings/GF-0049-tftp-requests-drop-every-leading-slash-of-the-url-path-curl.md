---
id: GF-0049
title: TFTP requests drop every leading slash of the URL path; curl drops only the first, so tftp://host//N asks for /N
area: behaviour
key: behaviour:tftp-path-loses-every-leading-slash
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test1007, behaviour:test1009, behaviour:test1049, behaviour:test1093, behaviour:test1094, behaviour:test1099, behaviour:test1238, behaviour:test1242, behaviour:test1243, behaviour:test271, behaviour:test283, behaviour:test284, behaviour:test285, behaviour:test286, behaviour:test332]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
task: BL-1979
tasks: [BL-1979]
---
# GF-0049 - TFTP requests drop every leading slash of the URL path; curl drops only the first, so tftp://host//N asks for /N

## Summary

Curl differs from upstream curl in behaviour: TFTP requests drop every leading slash of the URL path; curl drops only the first, so tftp://host//N asks for /N.

## Evidence

Every item expects 'upstream test<N> passes'. test271 (tftp://%HOSTIP:%TFTPPORT//271): '<verify><protocol> differs at byte 59 (line 5): expected "filename = /271\n", got "filename = 271\n"'. test1007: expected 'filename = /invalid-file', got 'filename = invalid-file'. test1243/285/286 (WRQ): expected 'filename = /test1243.txt', got 'filename = test1243.txt'. Cause: Curl.Protocol.Tftp.UnitLibrary/TftpRequestFile.FromUrlPath does absolutePath.TrimStart('/'), stripping every leading slash. curl's tftp.c skips only the one separator slash (RFC 3617). Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 271,1007,1243

## Suggestion

In Curl.Protocol.Tftp.UnitLibrary's TftpRequestFile.FromUrlPath, remove exactly one leading '/' from the URL path, not all of them. tftp://host//271 then requests '/271' as curl 8.21.0 does. Pin it in Curl.Protocol.Tftp.UnitTests for RRQ and WRQ.

## Measurements

- 2026-10-10_0657: 15 of 15 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1979.
