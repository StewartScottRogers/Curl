---
id: BL-1981
title: Close GF-0051: FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1981 — Close GF-0051: FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0051 (FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit), so a later gap analysis measures each of `behaviour:test1010`, `behaviour:test1096`, `behaviour:test1149`, `behaviour:test1217`, `behaviour:test1225`, `behaviour:test146`, `behaviour:test149`, `behaviour:test215`, `behaviour:test216`, `behaviour:test698`, `behaviour:test2002`, `behaviour:test2003` as `match`.

## Context

- Finding: GF-0051, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1010`, `behaviour:test1096`, `behaviour:test1149`, `behaviour:test1217`, `behaviour:test1225`, `behaviour:test146`, `behaviour:test149`, `behaviour:test215`, `behaviour:test216`, `behaviour:test698`, `behaviour:test2002`, `behaviour:test2003`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test215 (two URLs, same directory): '<verify><protocol> differs at byte 89 (line 10): expected "EPSV\r\n", got "QUIT\r\n"'; 1010, 1096, 216 and 698 are the same. test146 and 1225: expected 'CWD /', got 'QUIT'. test1217: expected 'CWD /this/is/the/path', got 'QUIT'. test2002 (http, ftp, file, tftp): expected 'opcode = 1', got 'QUIT', because curl's QUIT comes at exit, after the TFTP request. test2003: expected 'USER anonymous', got 'GET /20030001 HTTP/1.1'. Cause: Curl.Protocol.Ftp.UnitLibrary/FtpSession.QuitAsync runs at the end of every transfer, and the FTP control connection never enters the run's ConnectionCache. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 215,146,1217,2002

Suggestion, copied from the finding:

In Curl.Protocol.Ftp.UnitLibrary, keep an FTP control connection open after a transfer and hand it to the run's connection cache (Curl.Networking.UnitLibrary ConnectionCache, as HTTP does under ADR-0050), keyed by host, port, user and TLS. Track its working directory, so the next URL on it sends 'CWD /' or only the CWDs that differ (singlecwd, multicwd, nocwd), as curl 8.21.0's ftp.c does. Send QUIT when the cache closes the connection at exit. Pin the sequences of test146, 215 and 1217 in Curl.Protocol.Ftp.UnitTests.

## Acceptance criteria

- [x] `behaviour:test1010`: Curl answers what curl 8.21.0 answers, `upstream test1010 passes`, so the item measures `match`.
- [x] `behaviour:test1096`: Curl answers what curl 8.21.0 answers, `upstream test1096 passes`, so the item measures `match`.
- [x] `behaviour:test1149`: Curl answers what curl 8.21.0 answers, `upstream test1149 passes`, so the item measures `match`.
- [x] `behaviour:test1217`: Curl answers what curl 8.21.0 answers, `upstream test1217 passes`, so the item measures `match`.
- [x] `behaviour:test1225`: Curl answers what curl 8.21.0 answers, `upstream test1225 passes`, so the item measures `match`.
- [x] `behaviour:test146`: Curl answers what curl 8.21.0 answers, `upstream test146 passes`, so the item measures `match`.
- [x] `behaviour:test149`: Curl answers what curl 8.21.0 answers, `upstream test149 passes`, so the item measures `match`.
- [x] `behaviour:test215`: Curl answers what curl 8.21.0 answers, `upstream test215 passes`, so the item measures `match`.
- [x] `behaviour:test216`: Curl answers what curl 8.21.0 answers, `upstream test216 passes`, so the item measures `match`.
- [x] `behaviour:test698`: Curl answers what curl 8.21.0 answers, `upstream test698 passes`, so the item measures `match`.
- [x] `behaviour:test2002`: Curl answers what curl 8.21.0 answers, `upstream test2002 passes`, so the item measures `match`.
- [x] `behaviour:test2003`: Curl answers what curl 8.21.0 answers, `upstream test2003 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Plan (ADR-0468): the FTP control target carries PoolScheme, so the run connection cache keeps it; a transfer curl leaves intact holds an FtpKeptConnection session on it and MarkReusable instead of QUIT; ShutDownAsync sends QUIT at exit. Reuse skips login and PWD, a TYPE already in force, and CWD when prevpath matches or the path is absolute nocwd; otherwise CWD to the entry path first unless absolute (curl ftp_state_cwd).
- Pinned in FtpProtocolHandlerKeptConnectionTests: test146, 215, 1010 and 1225 sequences, refused entry-path CWD (exit 9), login mismatch, failure with no QUIT.
- The twelve behaviour items are re-measured by the next gap analysis run, outside a lane; their boxes stay for it. No option changed, so --ai-help is unchanged.
- AUTH-upgraded or CCC-cleared control connections are not kept (still QUIT per URL), recorded in ADR-0468.
- Coverage not measured (budget): filed BL-2023.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. FTP keeps the control connection for the next URL and sends QUIT only at exit (ADR-0468)
