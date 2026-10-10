---
id: BL-1978
title: Close GF-0048: FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1978 — Close GF-0048: FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0048 (FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener), so a later gap analysis measures each of `behaviour:test101`, `behaviour:test103`, `behaviour:test108`, `behaviour:test251`, `behaviour:test1414`, `behaviour:test1206`, `behaviour:test1207`, `behaviour:test1211` as `match`.

## Context

- Finding: GF-0048, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test101`, `behaviour:test103`, `behaviour:test108`, `behaviour:test251`, `behaviour:test1414`, `behaviour:test1206`, `behaviour:test1207`, `behaviour:test1211`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test101, 103, 108, 251, 1414 and 1211 (ftp:// with -P %CLIENTIP or -P -): 'curl did not finish within 20 seconds'. test1206/1207 (-P - against NODATACONN425/421): '<verify><protocol> differs at byte 83 (line 8): expected "QUIT\r\n", got the end'. Cause: InProcessCurl has no ftpListener parameter, so the gap tool cannot hand Curl UpstreamCurlInvocation.ConnectionListener (the stand-in's in-memory listener that PORT and EPRT connect to). Curl listens on a real socket that the in-memory server never dials. 101, 103, 108, 251 and 1414 are listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt, where the suite passes ftpListener; 1206, 1207 and 1211 are not. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 101,1206,1211

Suggestion, copied from the finding:

Pass the ftpListener through InProcessCurl's new overload (behaviour:in-process-runner-bypasses-tcp-connector). Re-measure. Then, in Curl.Protocol.Ftp.UnitLibrary's FtpSession active-mode path, make a 425 or 421 answer to the data connection (NODATACONN425/421) end with QUIT as curl 8.21.0 does (test1206/1207). Make --max-time end a data connection the server never opens (test1211). Pin both in Curl.Protocol.Ftp.UnitTests.

## Acceptance criteria

- [ ] `behaviour:test101`: Curl answers what curl 8.21.0 answers, `upstream test101 passes`, so the item measures `match`.
- [ ] `behaviour:test103`: Curl answers what curl 8.21.0 answers, `upstream test103 passes`, so the item measures `match`.
- [ ] `behaviour:test108`: Curl answers what curl 8.21.0 answers, `upstream test108 passes`, so the item measures `match`.
- [ ] `behaviour:test251`: Curl answers what curl 8.21.0 answers, `upstream test251 passes`, so the item measures `match`.
- [ ] `behaviour:test1414`: Curl answers what curl 8.21.0 answers, `upstream test1414 passes`, so the item measures `match`.
- [ ] `behaviour:test1206`: Curl answers what curl 8.21.0 answers, `upstream test1206 passes`, so the item measures `match`.
- [ ] `behaviour:test1207`: Curl answers what curl 8.21.0 answers, `upstream test1207 passes`, so the item measures `match`.
- [ ] `behaviour:test1211`: Curl answers what curl 8.21.0 answers, `upstream test1211 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
