---
id: GF-0048
title: FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener
area: behaviour
key: behaviour:in-process-runner-lacks-ftp-active-listener
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test101, behaviour:test103, behaviour:test108, behaviour:test251, behaviour:test1414, behaviour:test1206, behaviour:test1207, behaviour:test1211]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
task:
tasks: []
---
# GF-0048 - FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener

## Summary

Curl differs from upstream curl in behaviour: FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener.

## Evidence

Every item expects 'upstream test<N> passes'. test101, 103, 108, 251, 1414 and 1211 (ftp:// with -P %CLIENTIP or -P -): 'curl did not finish within 20 seconds'. test1206/1207 (-P - against NODATACONN425/421): '<verify><protocol> differs at byte 83 (line 8): expected "QUIT\r\n", got the end'. Cause: InProcessCurl has no ftpListener parameter, so the gap tool cannot hand Curl UpstreamCurlInvocation.ConnectionListener (the stand-in's in-memory listener that PORT and EPRT connect to). Curl listens on a real socket that the in-memory server never dials. 101, 103, 108, 251 and 1414 are listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt, where the suite passes ftpListener; 1206, 1207 and 1211 are not. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 101,1206,1211

## Suggestion

Pass the ftpListener through InProcessCurl's new overload (behaviour:in-process-runner-bypasses-tcp-connector). Re-measure. Then, in Curl.Protocol.Ftp.UnitLibrary's FtpSession active-mode path, make a 425 or 421 answer to the data connection (NODATACONN425/421) end with QUIT as curl 8.21.0 does (test1206/1207). Make --max-time end a data connection the server never opens (test1211). Pin both in Curl.Protocol.Ftp.UnitTests.

## Measurements

- 2026-10-10_0657: 8 of 8 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
