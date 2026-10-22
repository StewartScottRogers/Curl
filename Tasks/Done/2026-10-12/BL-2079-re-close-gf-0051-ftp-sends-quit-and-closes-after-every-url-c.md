---
id: BL-2079
title: Re-close GF-0051: FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2079 — Re-close GF-0051: FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0051 (FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit), so a later gap analysis measures each of `behaviour:test1010`, `behaviour:test1096`, `behaviour:test1149`, `behaviour:test1217`, `behaviour:test1225`, `behaviour:test146`, `behaviour:test149`, `behaviour:test215`, `behaviour:test216`, `behaviour:test698`, `behaviour:test2002`, `behaviour:test2003` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0051 ([BL-1981]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- 2026-10-11, lane 1 (interactive only): the gap could not be reproduced from a lane.
  BL-1981's fix (61542cdeb, ADR-0468) reached `master` (e67018991, 08:41) before gap run
  2026-10-11_0936. Measured with `Record-CurlExchange.ps1 -Ftp` and this tree's curl
  (`dotnet run --no-build --project Curl.Console`): two URLs `ftp://127.0.0.1:<port>/a/`
  give USER, PASS, PWD, CWD a, EPSV, TYPE A, LIST, then EPSV, LIST on the same control
  connection and QUIT only at exit, exit 0 - test215's expected sequence. So the measured
  QUIT after the first URL depends on something in the upstream case or its server that
  the recorder does not reproduce (most likely the upstream ftpserver's replies or
  data-connection close timing making `FtpSession` judge the connection not intact, or how
  the harness invokes Curl), and both the reproduction (`Gap/Tools/Measure-UpstreamCases.cs`)
  and the upstream data under `%LOCALAPPDATA%\Curl\gap\` are refused to lanes by the
  audit guard (ADR-0433).
- Remove `lane: no` once an interactive session has run
  `Measure-UpstreamCases.cs ... 215,146,1217,2002` on the current tree, captured test215's
  protocol transcript, and written under Notes which reply or step makes Curl send QUIT
  after the first URL, so a lane can fix it without the Gap paths.
- Tip: call `Record-CurlExchange.ps1` with `&`, not `pwsh -File`; `-File` flattens a
  `-CurlArgs` array into one string.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. requeue as interactive only; this tree keeps the FTP control connection for a test215-shaped run, so GF-0051 differs only under the gap harness, whose paths lanes cannot read

Closed by evidence: the fixed gap harness (PR #119, merged 2026-10-12) re-measured every item of this task as `match` with the gap office's own Measure-UpstreamCases tool. The finding itself closes at the next gap run.
- 2026-10-11: Backlog -> Doing. PR #119 re-measured every item as match
- 2026-10-11: Doing -> Done. PR #119 re-measured every item as match
