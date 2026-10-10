---
id: BL-1978
title: Close GF-0048: FTP active mode (-P) hangs in process: the FTP stand-in's PORT/EPRT cannot reach Curl's real TCP listener
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
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

- [x] `behaviour:test101`: Curl answers what curl 8.21.0 answers, `upstream test101 passes`, so the item measures `match`.
- [x] `behaviour:test103`: Curl answers what curl 8.21.0 answers, `upstream test103 passes`, so the item measures `match`.
- [x] `behaviour:test108`: Curl answers what curl 8.21.0 answers, `upstream test108 passes`, so the item measures `match`.
- [x] `behaviour:test251`: Curl answers what curl 8.21.0 answers, `upstream test251 passes`, so the item measures `match`.
- [x] `behaviour:test1414`: Curl answers what curl 8.21.0 answers, `upstream test1414 passes`, so the item measures `match`.
- [x] `behaviour:test1206`: Curl answers what curl 8.21.0 answers, `upstream test1206 passes`, so the item measures `match`.
- [x] `behaviour:test1207`: Curl answers what curl 8.21.0 answers, `upstream test1207 passes`, so the item measures `match`.
- [x] ~~`behaviour:test1211`: Curl answers what curl 8.21.0 answers, `upstream test1211 passes`, so the item measures `match`.~~ Moved to BL-2021 (see Notes).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured in process through `UpstreamConformanceTests` (which already passes `ftpListener`): 101, 103, 108, 251 and 1414 pass there (listed); 1206 and 1207 timed out after `--max-time 8` without `QUIT`, 1211 after 20 seconds.
- `InProcessCurl.RunAsync` gains an overload taking an `IConnectionListener ftpListener` beside the environment, so `Gap/Tools/Measure-UpstreamCases.cs` can pass `UpstreamCurlInvocation.ConnectionListener` as the conformance suite does. **Interactive check left open:** a lane may not read or change `Gap/`, so an interactive session must switch that tool to the new overload (`ftpListener: invocation.ConnectionListener`) and re-measure; until then the gap tool's 101, 103, 108, 251 and 1414 still listen on a real port.
- `FtpSession.AcceptDataConnectionAsync` now checks `FtpControlChannel.HasBufferedNegativeReply` before waiting: a 4xx/5xx reply already read behind the `150` is read (its failure, a `421`'s exit 28 included, ignored as curl's `ReceivedServerConnect` ignores `Curl_GetFTPResponse`'s result), `-v` gets `There is negative response in cache while serv connect`, and the transfer ends with exit 10 (`FTP: The server failed to connect to data port`, curl's `curl_easy_strerror` text, since curl sets no message there) after `QUIT`. 1206 and 1207 now pass and are listed (1170 passing). Only the buffered case is handled, as curl's first check is; a refusal arriving later on the control connection still waits for the accept timeout or `-m`.
- Decision (sensible default, rule 1): test1211 (same server, no `--max-time`, upstream expects exit 28 and no `QUIT`) cannot be told apart from 1206 by anything Curl knows yet, and Curl now ends it with exit 10 and `QUIT`. It needs real curl measured, so it moved to BL-2021 instead of holding the rest of this task back.
- `touches` gained `Curl.Conformance.UnitTests` to list 1206 and 1207 in `PassingUpstreamCases.txt`; no task in Doing on `origin/work/dark-factory` named it.
- No option was added or changed, so `--ai-help` is unchanged.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. 1206 and 1207 pass and are listed, InProcessCurl takes an FTP listener; 1211 moved to BL-2021
