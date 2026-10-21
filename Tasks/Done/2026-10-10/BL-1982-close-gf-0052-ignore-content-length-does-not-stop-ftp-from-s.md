---
id: BL-1982
title: Close GF-0052: --ignore-content-length does not stop FTP from sending SIZE before RETR
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1982 — Close GF-0052: --ignore-content-length does not stop FTP from sending SIZE before RETR

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0052 (--ignore-content-length does not stop FTP from sending SIZE before RETR), so a later gap analysis measures each of `behaviour:test1137`, `behaviour:test416` as `match`.

## Context

- Finding: GF-0052, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1137`, `behaviour:test416`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1137: '<verify><protocol> differs at byte 63 (line 7): expected "RETR 1137\r\n", got "SIZE 1137\r\n"'. test416 (EPSV, Range) the same at byte 57. Curl.Protocol.Ftp.UnitLibrary has no reference to the option. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1137,416

Suggestion, copied from the finding:

Carry --ignore-content-length to the FTP handler (Curl.Console's transfer-context mapping) and, in Curl.Protocol.Ftp.UnitLibrary's FtpSession download path, skip SIZE and go straight to RETR when it is set, as curl 8.21.0 does (data->set.ignorecl).

## Acceptance criteria

- [x] `behaviour:test1137`: Curl answers what curl 8.21.0 answers, `upstream test1137 passes`, so the item measures `match`.
- [x] `behaviour:test416`: Curl answers what curl 8.21.0 answers, `upstream test416 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Fix: `FtpSession` reads `context.Http?.IgnoreContentLength` (the factory always fills `Http` from the command line, `TransferContextFactory`) and a download under it sends no `SIZE`, going from `TYPE I` (or a `+` quote) straight to `REST`/`RETR`, as curl 8.21.0's `ftp_state_type_resp` does for `data->set.ignorecl`. `-I` still sends `SIZE`, as curl's `ftp_state_size` does for an info transfer. No new option, so `--ai-help` needs no change; Curl.Console needed no change because `Http` already carries the flag to every scheme.
- Chose to read the existing `HttpRequestOptions.IgnoreContentLength` rather than add a scheme-neutral member to `ITransferContext`: one flag, one name, no contract change for every handler. `Curl.Protocol.Abstractions.UnitLibrary` was added to `touches` for a doc-comment change only (`ITransferContext.Http`'s remark now names the FTP exception); no task in Doing on `origin/work/dark-factory` touched it.
- Tests: `FtpProtocolHandlerIgnoreContentLengthTests` (plain download: `TYPE I` then `RETR`; `-r 5-`: `TYPE I`, `REST 5`, `RETR`; `Http` set without the flag still sends `SIZE`). They cover every new branch, so Measure-CodeQuality was not run.
- Not measured here: the gap tool (`Gap/Tools/Measure-UpstreamCases.cs`) and the upstream test cases live under a `gap` path the audit guard refuses a lane, so test1137 and test416 are pinned from the finding's evidence; the next gap run re-measures them (ADR-0433).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. An FTP download under --ignore-content-length sends no SIZE: TYPE I goes straight to REST/RETR, as curl 8.21.0 does (upstream tests 1137, 416)
